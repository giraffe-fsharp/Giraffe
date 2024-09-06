[<AutoOpen>]
module Giraffe.RequestLimitation

open System
open Microsoft.AspNetCore.Http

/// <summary>
/// Use this record to specify your custom error handlers. If you use the Option.None value, we'll use the default
/// handlers that changes the status code to 406 (not acceptable) and responds with a piece of text.
/// </summary>
type OptionalErrorHandlers =
    {
        InvalidHeaderValue: HttpHandler option
        HeaderNotFound: HttpHandler option
    }

/// <summary>
/// Filters an incoming HTTP request based on the accepted mime types of the client (Accept HTTP header).
/// If the client doesn't accept any of the provided mimeTypes then the handler will not continue executing the next <see cref="HttpHandler"/> function.
/// </summary>
/// <param name="mimeTypes">List of mime types of which the client has to accept at least one.</param>
/// <param name="optionalErrorHandler">OptionalErrorHandlers record with HttpHandler options to define the server
/// response either if the header does not exist or has an invalid value. If both are `Option.None`, we use default
/// handlers.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let mustAcceptAny (mimeTypes: string list) (optionalErrorHandler: OptionalErrorHandlers) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let headers = ctx.Request.GetTypedHeaders()

        let headerNotFoundHandler =
            optionalErrorHandler.HeaderNotFound
            |> Option.defaultValue (
                RequestErrors.notAcceptable (text "Request rejected because 'Accept' header was not found")
            )

        let invalidHeaderValueHandler =
            optionalErrorHandler.InvalidHeaderValue
            |> Option.defaultValue (
                RequestErrors.notAcceptable (
                    text "Request rejected because 'Accept' header hasn't got expected MIME type"
                )
            )

        match Option.ofObj (headers.Accept :> _ seq) with
        | Some xs when Seq.map (_.ToString()) xs |> Seq.exists (fun x -> Seq.contains x mimeTypes) -> next ctx
        | Some xs when Seq.isEmpty xs -> headerNotFoundHandler earlyReturn ctx
        | Some _ -> invalidHeaderValueHandler earlyReturn ctx
        | None -> headerNotFoundHandler earlyReturn ctx

/// <summary>
/// Limits to only requests with one of the specified `Content-Type` headers,
/// returning `406 NotAcceptable` when the request header doesn't exists in the set of specified types.
/// </summary>
/// <param name="contentTypes">The sequence of accepted content types.</param>
/// <param name="optionalErrorHandler">OptionalErrorHandlers record with HttpHandler options to define the server
/// response either if the header does not exist or has an invalid value. If both are `Option.None`, we use default
/// handlers.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let hasAnyContentTypes (contentTypes: string list) (optionalErrorHandler: OptionalErrorHandlers) =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let headerNotFoundHandler =
            optionalErrorHandler.HeaderNotFound
            |> Option.defaultValue (
                RequestErrors.notAcceptable (text "Request rejected because 'Content-Type' header was not found")
            )

        let invalidHeaderValueHandler =
            optionalErrorHandler.InvalidHeaderValue
            |> Option.defaultValue (
                RequestErrors.notAcceptable (
                    text "Request rejected because 'Content-Type' header hasn't got expected value"
                )
            )

        match Option.ofObj ctx.Request.ContentType with
        | Some header when Seq.contains header contentTypes -> next ctx
        | Some header when String.IsNullOrEmpty header -> headerNotFoundHandler earlyReturn ctx
        | Some _ -> invalidHeaderValueHandler earlyReturn ctx
        | None -> headerNotFoundHandler earlyReturn ctx


/// <summary>
/// Limits to only requests with a specific `Content-Type` header,
/// returning `406 NotAcceptable` when the request header value doesn't match the specified type.
/// </summary>
/// <param name="contentType">The single accepted content type.</param>
/// <param name="optionalErrorHandler">OptionalErrorHandlers record with HttpHandler options to define the server
/// response either if the header does not exist or has an invalid value. If both are `Option.None`, we use default
/// handlers.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let hasContentType (contentType: string) (optionalErrorHandler: OptionalErrorHandlers) =
    hasAnyContentTypes [ contentType ] (optionalErrorHandler: OptionalErrorHandlers)

/// <summary>
/// Limits request `Content-Length` header to a specified length,
/// returning `406 NotAcceptable` when no such header is present or the value exceeds the maximum specified length.
/// </summary>
/// <param name="maxLength">The maximum accepted length of the incoming request.</param>
/// <param name="optionalErrorHandler">OptionalErrorHandlers record with HttpHandler options to define the server
/// response either if the header does not exist or has an invalid value. If both are `Option.None`, we use default
/// handlers.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let maxContentLength (maxLength: int64) (optionalErrorHandler: OptionalErrorHandlers) =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let header = ctx.Request.ContentLength

        let headerNotFoundHandler =
            optionalErrorHandler.HeaderNotFound
            |> Option.defaultValue (
                RequestErrors.notAcceptable (text "Request rejected because there is no 'Content-Length' header")
            )

        let invalidHeaderValueHandler =
            optionalErrorHandler.InvalidHeaderValue
            |> Option.defaultValue (
                RequestErrors.notAcceptable (text "Request rejected because 'Content-Length' header is too large")
            )

        match Option.ofNullable header with
        | Some v when v <= maxLength -> next ctx
        | Some _ -> invalidHeaderValueHandler earlyReturn ctx
        | None -> headerNotFoundHandler earlyReturn ctx
