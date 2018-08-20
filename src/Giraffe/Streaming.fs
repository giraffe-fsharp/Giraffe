[<AutoOpen>]
module Giraffe.Streaming

open System
open System.IO
open System.Linq
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers
open FSharp.Control.Tasks.V2.ContextInsensitive

// ---------------------------
// HTTP Range parsing
// ---------------------------

type internal RangeBoundary =
    {
        Start : int64
        End   : int64
    }
    member this.Length = this.End - this.Start + 1L

/// **Description**
///
/// A collection of helper functions to parse and validate the `Range` and `If-Range` HTTP headers of a request.
///
module internal RangeHelper =

    /// **Description**
    ///
    /// Parses the `Range` HTTP header of a request.
    ///
    /// Original code taken from ASP.NET Core:
    ///
    /// https://github.com/aspnet/StaticFiles/blob/dev/shared/Microsoft.AspNetCore.RangeHelper.Sources/RangeHelper.cs
    ///
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

    /// **Description**
    ///
    /// Validates if the provided set of `ranges` can be satisfied with the given `contentLength`.
    ///
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

    /// **Description**
    ///
    /// Parses and validates the `If-Range` HTTP header
    ///
    let isIfRangeValid (request      : HttpRequest)
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

// ---------------------------
// HttpContext extensions
// ---------------------------

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

    /// **Description**
    ///
    /// Streams data to the client.
    ///
    /// The handler will respect any valid HTTP pre-conditions (e.g. `If-Match`, `If-Modified-Since`, etc.) and return the most appropriate response. If the optional parameters `eTag` and/or `lastModified` have been set, then it will also set the `ETag` and/or `Last-Modified` HTTP headers in the response.
    ///
    /// **Parameters**
    ///
    /// - `enableRangeProcessing`: If enabled then the handler will respect the `Range` and `If-Range` HTTP headers of the request as well as set all necessary HTTP headers in the response to enable HTTP range processing.
    /// - `stream`: The stream to be send to the client.
    /// - `eTag`: An optional entity tag which identifies the exact version of the data.
    /// - `lastModified`: An optional parameter denoting the last modifed date time of the data.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.WriteStreamAsync (enableRangeProcessing : bool)
                                 (stream                : Stream)
                                 (eTag                  : EntityTagHeaderValue option)
                                 (lastModified          : DateTimeOffset option) =
        task {
            match this.ValidatePreconditions eTag lastModified with
            | ConditionFailed     -> return this.PreconditionFailedResponse()
            | ResourceNotModified -> return this.NotModifiedResponse()

            // If all pre-conditions have been met (or didn't exist) then proceed with web request execution
            | AllConditionsMet | NoConditionsSpecified ->
                if      not stream.CanSeek        then return! this.WriteStreamToBodyAsync stream None
                else if not enableRangeProcessing then return! this.WriteStreamToBodyAsync stream None
                else
                    // Set HTTP header to tell clients that Range processing is enabled
                    this.SetHttpHeader HeaderNames.AcceptRanges this.RangeUnit

                    match RangeHelper.parseRange this.Request with
                    | None         -> return! this.WriteStreamToBodyAsync stream None
                    | Some ranges  ->
                        // Check and validate If-Range HTTP header
                        match RangeHelper.isIfRangeValid this.Request eTag lastModified with
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

    /// **Description**
    ///
    /// Streams a file to the client.
    ///
    /// The handler will respect any valid HTTP pre-conditions (e.g. `If-Match`, `If-Modified-Since`, etc.) and return the most appropriate response. If the optional parameters `eTag` and/or `lastModified` have been set, then it will also set the `ETag` and/or `Last-Modified` HTTP headers in the response.
    ///
    /// **Parameters**
    ///
    /// - `enableRangeProcessing`: If enabled then the handler will respect the `Range` and `If-Range` HTTP headers of the request as well as set all necessary HTTP headers in the response to enable HTTP range processing.
    /// - `filePath`: The absolute or relative path (to `ContentRoot`) of the file.
    /// - `eTag`: An optional entity tag which identifies the exact version of the file.
    /// - `lastModified`: An optional parameter denoting the last modifed date time of the file.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.WriteFileStreamAsync (enableRangeProcessing : bool)
                                     (filePath              : string)
                                     (eTag                  : EntityTagHeaderValue option)
                                     (lastModified          : DateTimeOffset option) =
        task {
            let filePath =
                match Path.IsPathRooted filePath with
                | true  -> filePath
                | false ->
                    let env = this.GetHostingEnvironment()
                    Path.Combine(env.ContentRootPath, filePath)
            use stream = new FileStream(filePath, FileMode.Open, FileAccess.Read)
            return! this.WriteStreamAsync enableRangeProcessing stream eTag lastModified
        }

// ---------------------------
// HttpHandler functions
// ---------------------------

/// **Description**
///
/// Streams data to the client.
///
/// The handler will respect any valid HTTP pre-conditions (e.g. `If-Match`, `If-Modified-Since`, etc.) and return the most appropriate response. If the optional parameters `eTag` and/or `lastModified` have been set, then it will also set the `ETag` and/or `Last-Modified` HTTP headers in the response.
///
/// **Parameters**
///
/// - `enableRangeProcessing`: If enabled then the handler will respect the `Range` and `If-Range` HTTP headers of the request as well as set all necessary HTTP headers in the response to enable HTTP range processing.
/// - `stream`: The stream to be send to the client.
/// - `eTag`: An optional entity tag which identifies the exact version of the data.
/// - `lastModified`: An optional parameter denoting the last modifed date time of the file.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let streamData (enableRangeProcessing : bool)
               (stream                : Stream)
               (eTag                  : EntityTagHeaderValue option)
               (lastModified          : DateTimeOffset option)
               : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteStreamAsync enableRangeProcessing stream eTag lastModified

/// **Description**
///
/// Streams a file to the client.
///
/// The handler will respect any valid HTTP pre-conditions (e.g. `If-Match`, `If-Modified-Since`, etc.) and return the most appropriate response. If the optional parameters `eTag` and/or `lastModified` have been set, then it will also set the `ETag` and/or `Last-Modified` HTTP headers in the response.
///
/// **Parameters**
///
/// - `enableRangeProcessing`: If enabled then the handler will respect the `Range` and `If-Range` HTTP headers of the request as well as set all necessary HTTP headers in the response to enable HTTP range processing.
/// - `filePath`: The absolute or relative path (to `ContentRoot`) of the file.
/// - `eTag`: An optional entity tag which identifies the exact version of the file.
/// - `lastModified`: An optional parameter denoting the last modifed date time of the file.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let streamFile (enableRangeProcessing : bool)
               (filePath              : string)
               (eTag                  : EntityTagHeaderValue option)
               (lastModified          : DateTimeOffset option)
               : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteFileStreamAsync enableRangeProcessing filePath eTag lastModified