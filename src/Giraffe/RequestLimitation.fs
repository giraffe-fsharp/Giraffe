[<AutoOpen>]
module Giraffe.RequestLimitation

open Microsoft.AspNetCore.Http

/// <summary>
/// Filters an incoming HTTP request based on the accepted mime types of the client (Accept HTTP header).
/// If the client doesn't accept any of the provided mimeTypes then the handler will not continue executing the next <see cref="HttpHandler"/> function.
/// </summary>
/// <param name="mimeTypes">List of mime types of which the client has to accept at least one.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let mustAcceptAny (mimeTypes : string list) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let headers = ctx.Request.GetTypedHeaders()
        match Option.ofObj  (headers.Accept :> _ seq) with
        | Some xs when Seq.map (fun x -> x.ToString()) xs |> Seq.exists (fun x -> Seq.contains x mimeTypes) -> next ctx
        | Some _ -> RequestErrors.notAcceptable (text "cannot accept request because header 'Accept' hasn't got expected MIME type") earlyReturn ctx
        | None -> RequestErrors.notAcceptable (text "cannot accept request because the request has no 'Accept' header") earlyReturn ctx

/// <summary>
/// Limits to only requests with one of the specified `Content-Type` headers,
/// returning `406 NotAcceptable` when the request header doesn't exists in the set of specified types.
/// </summary>
/// <param name="contentTypes">The sequence of accepted content types.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let haveAnyContentTypes contentTypes =
    fun next (ctx : HttpContext) ->
      match Option.ofObj ctx.Request.ContentType with
      | Some headers when Seq.contains headers contentTypes -> next ctx
      | Some _ -> RequestErrors.notAcceptable (text "cannot accept request because header 'Content-Type' hasn't got expected value") earlyReturn ctx
      | None -> RequestErrors.notAcceptable (text "cannot accept request because the request has no 'Content-Type' header") earlyReturn ctx


/// <summary>
/// Limits to only requests with a specific `Content-Type` header, 
/// returning `406 NotAcceptable` when the request header value doesn't match the specified type.
/// </summary>
/// <param name="contentType">The single accepted content type.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let haveContentType contentType = haveAnyContentTypes [ contentType ]

/// <summary>
/// Limits request `Content-Length` header to a specified length, 
/// returning `406 NotAcceptable` when no such header is present or the value exceeds the maximum specified length.
/// </summary>
/// <param name="maxLength">The maximum accepted length of the incoming request.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let maxContentLength maxLength =
  fun next (ctx : HttpContext) ->
    let header = ctx.Request.ContentLength
    match Option.ofNullable header with
    | Some v when v <= maxLength ->  next ctx
    | Some _ -> RequestErrors.notAcceptable (text "cannot accept request because header 'Content-Length' is too large") earlyReturn ctx
    | None -> RequestErrors.notAcceptable (text "cannot accept request because the request has no 'Content-Length' header") earlyReturn ctx