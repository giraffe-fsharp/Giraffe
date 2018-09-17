[<AutoOpen>]
module Giraffe.ResponseCaching

open System
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.ResponseCaching

/// **Description**
///
/// Specifies the directive for the `Cache-Control` HTTP header:
///
/// - `NoCache`: The resource should not be cached under any circumstances.
/// - `Public`: Any client and proxy may cache the resource for the given amount of time.
/// - `Private`: Only the end client may cache the resource for the given amount of time.
///
type CacheDirective =
    | NoCache
    | Public  of TimeSpan
    | Private of TimeSpan

let private noCacheHeader = CacheControlHeaderValue(NoCache = true, NoStore = true)

let inline private cacheHeader isPublic duration =
    CacheControlHeaderValue(
        Public = isPublic,
        MaxAge = Nullable duration)

/// **Description**
///
/// Enables (or suppresses) response caching by clients and proxy servers.
/// This http handler integrates with ASP.NET Core's response caching middleware.
///
/// The `responseCaching` http handler will set the relevant HTTP response headers in order to enable response caching on the client, by proxies (if public) and by the ASP.NET Core middleware (if enabled).
///
/// **Parameters**
///
/// - `directive`: Specifies the cache directive to be set in the response's HTTP headers. Use `NoCache` to suppress caching altogether or use `Public`/`Private` to enable caching for everyone or clients only.
/// - `vary`: Optionally specify which HTTP headers have to match in order to return a cached response (e.g. `Accept` and/or `Accept-Encoding`).
/// - `varyByQueryKeys`: An optional list of query keys which will be used by the ASP.NET Core response caching middleware to vary (potentially) cached responses. If this parameter is used then the ASP.NET Core response caching middleware has to be enabled. For more information check the official [VaryByQueryKeys](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/middleware?view=aspnetcore-2.1#varybyquerykeys) documentation.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let responseCaching (directive       : CacheDirective)
                    (vary            : string option)
                    (varyByQueryKeys : string array option) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->

        let tHeaders = ctx.Response.GetTypedHeaders()
        let headers  = ctx.Response.Headers

        match directive with
        | NoCache ->
            tHeaders.CacheControl           <- noCacheHeader
            headers.[ HeaderNames.Pragma ]  <- StringValues [| "no-cache" |]
            headers.[ HeaderNames.Expires ] <- StringValues [| "-1" |]
        | Public duration  -> tHeaders.CacheControl <- cacheHeader true duration
        | Private duration -> tHeaders.CacheControl <- cacheHeader false duration

        if vary.IsSome then headers.[HeaderNames.Vary] <- StringValues [| vary.Value |]

        if varyByQueryKeys.IsSome then
            let responseCachingFeature = ctx.Features.Get<IResponseCachingFeature>()
            if isNotNull responseCachingFeature then
                responseCachingFeature.VaryByQueryKeys <- varyByQueryKeys.Value

        next ctx

/// **Description**
///
/// Disables response caching by clients and proxy servers.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let noResponseCaching : HttpHandler = responseCaching NoCache None None

/// **Description**
///
/// Enables response caching for clients and proxy servers.
/// This http handler integrates with ASP.NET Core's response caching middleware.
///
/// The `responseCaching` http handler will set the relevant HTTP response headers in order to enable response caching on the client, by proxies and by the ASP.NET Core middleware (if enabled).
///
/// **Parameters**
///
/// - `seconds`: Specifies the duration (in seconds) for which the response may be cached.
/// - `vary`: Optionally specify which HTTP headers have to match in order to return a cached response (e.g. `Accept` and/or `Accept-Encoding`).
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let privateResponseCaching (seconds : int) (vary : string option) : HttpHandler =
    responseCaching (Private (TimeSpan.FromSeconds(float seconds))) vary None

/// **Description**
///
/// Enables response caching for clients only.
///
/// The `responseCaching` http handler will set the relevant HTTP response headers in order to enable response caching on the client only.
///
/// **Parameters**
///
/// - `seconds`: Specifies the duration (in seconds) for which the response may be cached.
/// - `vary`: Optionally specify which HTTP headers have to match in order to return a cached response (e.g. `Accept` and/or `Accept-Encoding`).
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let publicResponseCaching (seconds : int) (vary : string option) : HttpHandler =
    responseCaching (Public (TimeSpan.FromSeconds(float seconds))) vary None