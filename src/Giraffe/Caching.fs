[<AutoOpen>]
module Giraffe.Caching

open System
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.ResponseCaching

type CacheDirective =
    | NoCache
    | Public  of TimeSpan
    | Private of TimeSpan

let private noCacheHeader = CacheControlHeaderValue(NoCache = true, NoStore = true)

let inline private cacheHeader isPublic duration =
    CacheControlHeaderValue(
        Public = isPublic,
        MaxAge = Nullable duration)


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

let noResponseCaching : HttpHandler = responseCaching NoCache None None

let privateResponseCaching (seconds : int) (vary : string option) : HttpHandler =
    responseCaching (Private (TimeSpan.FromSeconds(float seconds))) vary None

let publicResponseCaching (seconds : int) (vary : string option) : HttpHandler =
    responseCaching (Public (TimeSpan.FromSeconds(float seconds))) vary None