[<AutoOpen>]
module Giraffe.Streaming

open System
open System.IO
open System.Linq
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers

type internal RangeBoundary =
    {
        Start : int64
        End   : int64
    }
    member this.Length = this.End - this.Start + 1L

type internal Range =
| Missing
| Invalid
| Valid of RangeBoundary

type RangeHeaderValue with

    /// Helper method to parse a RangeHeaderValue
    member internal this.Parse (contentLength : int64) =
        if      isNull this                then Range.Missing
        else if isNull this.Ranges         then Range.Missing
        else if this.Ranges.Count.Equals 0 then Range.Invalid
        else if contentLength.Equals 0     then Range.Invalid
        else
            // Normalize the range
            let range = this.Ranges.SingleOrDefault()

            match range.From.HasValue with
            | true ->
                if range.From.Value >= contentLength
                then Range.Invalid
                else
                    let endOfRange =
                        if not range.To.HasValue || range.To.Value >= contentLength
                        then contentLength - 1L else range.To.Value
                    Range.Valid { Start = range.From.Value; End = endOfRange }
            | false ->
                // Suffix range "-X" e.g. the last X bytes, resolve
                if range.To.Value.Equals 0
                then Range.Invalid
                else
                    let bytes        = min range.To.Value contentLength
                    let startOfRange = contentLength - bytes
                    let endOfRange   = startOfRange + bytes - 1L
                    Range.Valid { Start = startOfRange; End = endOfRange }

type HttpContext with

    member internal __.RangeUnit = "bytes"

    /// Helper method to parse the Range header from a request
    /// Original code taken from ASP.NET Core:
    /// https://github.com/aspnet/StaticFiles/blob/dev/shared/Microsoft.AspNetCore.RangeHelper.Sources/RangeHelper.cs
    member internal this.ParseRange (contentLength : int64) =
        let rawRangeHeader = this.Request.Headers.[HeaderNames.Range]

        if StringValues.IsNullOrEmpty(rawRangeHeader) then Range.Missing
        // Perf: Check for a single entry before parsing it.
        // The spec allows for multiple ranges but we choose not to support them because the client may request
        // very strange ranges (e.g. each byte separately, overlapping ranges, etc.) that could negatively
        // impact the server. Ignore the header and serve the response normally.
        else if (rawRangeHeader.Count > 1 || rawRangeHeader.[0].IndexOf(',') >= 0) then Range.Missing
        else this.Request.GetTypedHeaders().Range.Parse contentLength

    member internal this.WriteStreamToBodyAsync (stream : Stream) (rangeBoundary : RangeBoundary option) =
        task {
            try
                use input = stream
                let numberOfBytes =
                    match rangeBoundary with
                    | Some range ->
                        let contentRange = sprintf "%s %i-%i/%i" this.RangeUnit range.Start range.End stream.Length

                        // Set additional HTTP headers for range response
                        this.SetHttpHeader HeaderNames.AcceptRanges this.RangeUnit
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

    member this.WriteStreamAsync (enableRangeProcessing : bool) (stream : Stream) =
        task {
            if      not stream.CanSeek        then return! this.WriteStreamToBodyAsync stream None
            else if not enableRangeProcessing then return! this.WriteStreamToBodyAsync stream None
            else
                match this.ParseRange stream.Length with
                | Missing ->
                    // If the range header is missing then return a normal response,
                    // but additionally set the Accept-Ranges header to tell the client
                    // that range processing is allowed.
                    this.SetHttpHeader HeaderNames.AcceptRanges this.RangeUnit
                    return! this.WriteStreamToBodyAsync stream None
                | Invalid ->
                    // If the range header was invalid then return an error response
                    this.SetHttpHeader HeaderNames.AcceptRanges this.RangeUnit
                    this.SetHttpHeader HeaderNames.ContentRange (sprintf "*/%i" stream.Length)
                    this.SetStatusCode StatusCodes.Status416RangeNotSatisfiable
                    return Some this
                | Valid range ->
                    // Write a range to the response body
                    return! this.WriteStreamToBodyAsync stream (Some range)
        }