[<AutoOpen>]
module Giraffe.Streaming

open System
open System.IO
open System.Linq
open System.Runtime.CompilerServices
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers
open FSharp.Control.Tasks.Builders

// ---------------------------
// HTTP Range parsing
// ---------------------------

type internal RangeBoundary =
    {
        Start : int64
        End   : int64
    }
    member this.Length = this.End - this.Start + 1L

/// <summary>
/// A collection of helper functions to parse and validate the Range and If-Range HTTP headers of a request.
/// </summary>
module internal RangeHelper =

    /// **Description**
    ///
    /// Parses the Range HTTP header of a request.
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

    /// <summary>
    /// Validates if the provided set of ranges can be satisfied with the given contentLength.
    /// </summary>
    /// <param name="ranges"></param>
    /// <param name="contentLength"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Parses and validates the If-Range HTTP header
    /// </summary>
    /// <param name="request"></param>
    /// <param name="eTag"></param>
    /// <param name="lastModified"></param>
    /// <returns></returns>
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

[<Extension>]
type StreamingExtensions() =

    [<Extension>]
    static member internal RangeUnit(_ : HttpContext) = "bytes"

    [<Extension>]
    static member internal WriteStreamToBodyAsync
        (ctx          : HttpContext,
        stream        : Stream,
        rangeBoundary : RangeBoundary option) =
        task {
            try
                use input = stream
                let numberOfBytes =
                    match rangeBoundary with
                    | Some range ->
                        let contentRange = sprintf "%s %i-%i/%i" (ctx.RangeUnit()) range.Start range.End stream.Length

                        // Set additional HTTP headers for range response
                        ctx.SetHttpHeader(HeaderNames.ContentRange, contentRange)
                        ctx.SetHttpHeader(HeaderNames.ContentLength, range.Length)

                        // Set special status code for partial content response
                        ctx.SetStatusCode StatusCodes.Status206PartialContent

                        // Forward to start position of streaming
                        input.Seek(range.Start, SeekOrigin.Begin) |> ignore

                        Nullable<int64>(range.Length)
                    | None ->
                        // Only set HTTP Content-Length if the stream can be seeked
                        if stream.CanSeek then ctx.SetHttpHeader(HeaderNames.ContentLength, input.Length)
                        Nullable()

                // If the HTTP request was not HEAD then write to the body
                if not (HttpMethods.IsHead ctx.Request.Method) then
                    let bufferSize = 64 * 1024
                    do! StreamCopyOperation.CopyToAsync(
                            input,
                            ctx.Response.Body,
                            numberOfBytes,
                            bufferSize,
                            ctx.RequestAborted)
                return Some ctx
            with
            | :? OperationCanceledException ->
                // Don't throw this exception, it's most likely caused by the client disconnecting.
                // However, if it was cancelled for any other reason we need to prevent empty responses.
                ctx.Abort()
                return Some ctx
        }

    /// <summary>
    /// Streams data to the client.
    ///
    /// The handler will respect any valid HTTP pre-conditions (e.g. If-Match, If-Modified-Since, etc.) and return the most appropriate response. If the optional parameters eTag and/or lastModified have been set, then it will also set the ETag and/or Last-Modified HTTP headers in the response.
    /// </summary>
    /// <param name="enableRangeProcessing">If enabled then the handler will respect the Range and If-Range HTTP headers of the request as well as set all necessary HTTP headers in the response to enable HTTP range processing.</param>
    /// <param name="stream">The stream to be send to the client.</param>
    /// <param name="eTag">An optional entity tag which identifies the exact version of the data.</param>
    /// <param name="lastModified">An optional parameter denoting the last modified date time of the data.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteStreamAsync
        (ctx                  : HttpContext,
        enableRangeProcessing : bool,
        stream                : Stream,
        eTag                  : EntityTagHeaderValue option,
        lastModified          : DateTimeOffset option) =
        task {
            match ctx.ValidatePreconditions(eTag, lastModified) with
            | ConditionFailed     -> return ctx.PreconditionFailedResponse()
            | ResourceNotModified -> return ctx.NotModifiedResponse()

            // If all pre-conditions have been met (or didn't exist) then proceed with web request execution
            | AllConditionsMet | NoConditionsSpecified ->
                if      not stream.CanSeek        then return! ctx.WriteStreamToBodyAsync(stream, None)
                else if not enableRangeProcessing then return! ctx.WriteStreamToBodyAsync(stream, None)
                else
                    // Set HTTP header to tell clients that Range processing is enabled
                    ctx.SetHttpHeader(HeaderNames.AcceptRanges, ctx.RangeUnit())

                    match RangeHelper.parseRange ctx.Request with
                    | None         -> return! ctx.WriteStreamToBodyAsync(stream, None)
                    | Some ranges  ->
                        // Check and validate If-Range HTTP header
                        match RangeHelper.isIfRangeValid ctx.Request eTag lastModified with
                        | false -> return! ctx.WriteStreamToBodyAsync(stream, None)
                        | true  ->
                            match RangeHelper.validateRanges ranges stream.Length with
                            | Ok range -> return! ctx.WriteStreamToBodyAsync(stream, Some range)
                            | Error _  ->
                                // If the range header was invalid then return an error response
                                ctx.SetHttpHeader(
                                    HeaderNames.ContentRange,
                                    (sprintf "%s */%i" (ctx.RangeUnit()) stream.Length))
                                ctx.SetStatusCode StatusCodes.Status416RangeNotSatisfiable
                                return Some ctx
        }

    /// <summary>
    /// Streams a file to the client.
    ///
    /// The handler will respect any valid HTTP pre-conditions (e.g. If-Match, If-Modified-Since, etc.) and return the most appropriate response. If the optional parameters eTag and/or lastModified have been set, then it will also set the ETag and/or Last-Modified HTTP headers in the response.
    /// </summary>
    /// <param name="enableRangeProcessing">If enabled then the handler will respect the Range and If-Range HTTP headers of the request as well as set all necessary HTTP headers in the response to enable HTTP range processing.</param>
    /// <param name="filePath">The absolute or relative path (to ContentRoot) of the file.</param>
    /// <param name="eTag">An optional entity tag which identifies the exact version of the file.</param>
    /// <param name="lastModified">An optional parameter denoting the last modified date time of the file.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteFileStreamAsync
        (ctx                  : HttpContext,
        enableRangeProcessing : bool,
        filePath              : string,
        eTag                  : EntityTagHeaderValue option,
        lastModified          : DateTimeOffset option) =
        task {
            let filePath =
                match Path.IsPathRooted filePath with
                | true  -> filePath
                | false ->
                    let env = ctx.GetHostingEnvironment()
                    Path.Combine(env.ContentRootPath, filePath)
            use stream = new FileStream(filePath, FileMode.Open, FileAccess.Read)
            return! ctx.WriteStreamAsync(enableRangeProcessing, stream, eTag, lastModified)
        }

// ---------------------------
// HttpHandler functions
// ---------------------------

/// <summary>
/// Streams data to the client.
///
/// The handler will respect any valid HTTP pre-conditions (e.g. If-Match, If-Modified-Since, etc.) and return the most appropriate response. If the optional parameters eTag and/or lastModified have been set, then it will also set the ETag and/or Last-Modified HTTP headers in the response.
/// </summary>
/// <param name="enableRangeProcessing">enableRangeProcessing: If enabled then the handler will respect the Range and If-Range HTTP headers of the request as well as set all necessary HTTP headers in the response to enable HTTP range processing.</param>
/// <param name="stream">The stream to be send to the client.</param>
/// <param name="eTag">An optional entity tag which identifies the exact version of the data.</param>
/// <param name="lastModified">An optional parameter denoting the last modified date time of the file.</param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let streamData (enableRangeProcessing : bool)
               (stream                : Stream)
               (eTag                  : EntityTagHeaderValue option)
               (lastModified          : DateTimeOffset option)
               : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteStreamAsync(enableRangeProcessing, stream, eTag, lastModified)

/// <summary>
/// Streams a file to the client.
///
/// The handler will respect any valid HTTP pre-conditions (e.g. If-Match, If-Modified-Since, etc.) and return the most appropriate response. If the optional parameters eTag and/or lastModified have been set, then it will also set the ETag and/or Last-Modified HTTP headers in the response.
/// </summary>
/// <param name="enableRangeProcessing">If enabled then the handler will respect the Range and If-Range HTTP headers of the request as well as set all necessary HTTP headers in the response to enable HTTP range processing.</param>
/// <param name="filePath">The absolute or relative path (to ContentRoot) of the file.</param>
/// <param name="eTag">An optional entity tag which identifies the exact version of the file.</param>
/// <param name="lastModified">An optional parameter denoting the last modified date time of the file.</param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let streamFile (enableRangeProcessing : bool)
               (filePath              : string)
               (eTag                  : EntityTagHeaderValue option)
               (lastModified          : DateTimeOffset option)
               : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteFileStreamAsync(enableRangeProcessing, filePath, eTag, lastModified)