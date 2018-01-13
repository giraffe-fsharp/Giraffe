[<AutoOpen>]
module Giraffe.Streaming

open System
open System.IO
open System.Linq
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers
open Giraffe.Common
open System.Collections.Generic

/// ---------------------------
/// HTTP Range parsing
/// ---------------------------

type internal RangeBoundary =
    {
        Start : int64
        End   : int64
    }
    member this.Length = this.End - this.Start + 1L

module internal RangeHelper =

    /// Helper method to parse the Range header from a request
    /// Original code taken from ASP.NET Core:
    /// https://github.com/aspnet/StaticFiles/blob/dev/shared/Microsoft.AspNetCore.RangeHelper.Sources/RangeHelper.cs
    let parseRange (request : HttpRequest) =
        let rawRangeHeader = request.Headers.[HeaderNames.Range]
        if StringValues.IsNullOrEmpty(rawRangeHeader) then None
        // Perf: Check for a single entry before parsing it.
        // The spec allows for multiple ranges but we choose not to support them because the client may request
        // very strange ranges (e.g. each byte separately, overlapping ranges, etc.) that could negatively
        // impact the server. Ignore the header and serve the response normally.
        else if (rawRangeHeader.Count > 1 || rawRangeHeader.[0].IndexOf(',') >= 0) then None
        else
            let range = request.GetTypedHeaders().Range
            if      isNull range        then None
            else if isNull range.Ranges then None
            else Some range.Ranges

    /// Validates the provided ranges against the actual content length (if they can be satisfied).
    let validateRanges (ranges : ICollection<RangeItemHeaderValue>) (contentLength : int64) =
        if      ranges.Count.Equals  0 then Error "No ranges provided."
        else if contentLength.Equals 0 then Error "Range exceeds content length (which is zero)."
        else
            // Normalize the range
            let range = ranges.SingleOrDefault()

            match range.From.HasValue with
            | true ->
                if range.From.Value >= contentLength
                then Error "Range exceeds content length."
                else
                    let endOfRange =
                        if not range.To.HasValue || range.To.Value >= contentLength
                        then contentLength - 1L else range.To.Value
                    Ok { Start = range.From.Value; End = endOfRange }
            | false ->
                // Suffix range "-X" e.g. the last X bytes, resolve
                if range.To.Value.Equals 0
                then Error "Range end value is zero."
                else
                    let bytes        = min range.To.Value contentLength
                    let startOfRange = contentLength - bytes
                    let endOfRange   = startOfRange + bytes - 1L
                    Ok { Start = startOfRange; End = endOfRange }

    /// Helper method to parse and validate the If-Range HTTP header
    let matchesIfRange (request      : HttpRequest)
                       (eTag         : EntityTagHeaderValue option)
                       (lastModified : DateTimeOffset option) =
        let ifRange = request.GetTypedHeaders().IfRange

        if isNull ifRange then true
        else if isNotNull ifRange.EntityTag then
            match eTag with
            | None   -> false
            | Some x -> ifRange.EntityTag.Compare(x, true)
        else if ifRange.LastModified.HasValue then
            match lastModified with
            | None   -> false
            | Some x -> x <= ifRange.LastModified.Value
        else true

/// ---------------------------
/// HttpContext extensions
/// ---------------------------

type HttpContext with
    member internal __.RangeUnit = "bytes"

    member internal this.WriteStreamToBodyAsync (stream : Stream) (rangeBoundary : RangeBoundary option) =
        task {
            try
                use input = stream
                let numberOfBytes =
                    match rangeBoundary with
                    | Some range ->
                        let contentRange = sprintf "%s %i-%i/%i" this.RangeUnit range.Start range.End stream.Length

                        // Set additional HTTP headers for range response
                        this.SetHttpHeader HeaderNames.ContentRange contentRange
                        this.SetHttpHeader HeaderNames.ContentLength range.Length

                        // Set special status code for partial content response
                        this.SetStatusCode StatusCodes.Status206PartialContent

                        // Forward to start position of streaming
                        input.Seek(range.Start, SeekOrigin.Begin) |> ignore

                        Nullable<int64>(range.Length)
                    | None ->
                        // Only set HTTP Content-Length if the stream can be seeked
                        if stream.CanSeek then this.SetHttpHeader HeaderNames.ContentLength input.Length
                        System.Nullable()

                // If the HTTP request was not HEAD then write to the body
                if not (HttpMethods.IsHead this.Request.Method) then
                    let bufferSize = 64 * 1024
                    do! StreamCopyOperation.CopyToAsync(
                            input,
                            this.Response.Body,
                            numberOfBytes,
                            bufferSize,
                            this.RequestAborted)
                return Some this
            with
            | :? OperationCanceledException ->
                // Don't throw this exception, it's most likely caused by the client disconnecting.
                // However, if it was cancelled for any other reason we need to prevent empty responses.
                this.Abort()
                return Some this
        }

    member this.WriteStreamAsync (enableRangeProcessing : bool)
                                 (stream                : Stream)
                                 (eTag                  : EntityTagHeaderValue option)
                                 (lastModified          : DateTimeOffset option) =
        task {
            match this.ValidatePreConditions eTag lastModified with
            | ConditionFailed        -> return this.NotModifiedResponse()
            | NotModified            -> return this.PreConditionFailedResponse()

            // If all pre-conditions have been met (or didn't exist) then proceed with web request execution
            | IsMatch | NotSpecified ->
                if      not stream.CanSeek        then return! this.WriteStreamToBodyAsync stream None
                else if not enableRangeProcessing then return! this.WriteStreamToBodyAsync stream None
                else
                    // Set HTTP header to tell clients that Range processing is enabled
                    this.SetHttpHeader HeaderNames.AcceptRanges this.RangeUnit

                    match RangeHelper.parseRange this.Request with
                    | None         -> return! this.WriteStreamToBodyAsync stream None
                    | Some ranges  ->
                        // Check and validate If-Range HTTP header
                        match RangeHelper.matchesIfRange this.Request eTag lastModified with
                        | false -> return! this.WriteStreamToBodyAsync stream None
                        | true  ->
                            match RangeHelper.validateRanges ranges stream.Length with
                            | Ok range -> return! this.WriteStreamToBodyAsync stream (Some range)
                            | Error _  ->
                                // If the range header was invalid then return an error response
                                this.SetHttpHeader HeaderNames.ContentRange (sprintf "%s */%i" this.RangeUnit stream.Length)
                                this.SetStatusCode StatusCodes.Status416RangeNotSatisfiable
                                return Some this
        }