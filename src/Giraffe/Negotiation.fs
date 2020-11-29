[<AutoOpen>]
module Giraffe.Negotiation

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open Microsoft.AspNetCore.Http

// ---------------------------
// Configuration types
// ---------------------------

/// <summary>
/// Interface defining the negotiation rules and the <see cref="HttpHandler" /> for unacceptable requests when doing content negotiation in Giraffe.
/// </summary>
type INegotiationConfig =
    /// <summary>
    /// A dictionary of mime types and response writing <see cref="HttpHandler" /> functions.
    ///
    /// Each mime type must be mapped to a function which accepts an obj and returns a <see cref="HttpHandler" /> which will send a response in the associated mime type.
    /// </summary>
    /// <example>
    /// <code>
    /// dict [ "application/json", json; "application/xml" , xml ]
    /// </code>
    /// </example>
    abstract member Rules : IDictionary<string, obj -> HttpHandler>

    /// <summary>
    /// A <see cref="HttpHandler" /> function which will be invoked if none of the accepted mime types can be satisfied. Generally this <see cref="HttpHandler" /> would send a response with a status code of 406 Unacceptable.
    /// </summary>
    /// <returns></returns>
    abstract member UnacceptableHandler : HttpHandler

let private unacceptableHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
    (setStatusCode 406
    >=> ((ctx.Request.Headers.["Accept"]).ToString()
        |> sprintf "%s is unacceptable by the server."
        |> text)) next ctx

/// <summary>
/// The default implementation of <see cref="INegotiationConfig."/>
///
/// Supported mime types:
///
/// */*: If a client accepts any content type then the server will return a JSON response.
///
/// application/json: Server will send a JSON response.
///
/// application/xml: Server will send an XML response.
///
/// text/xml: Server will send an XML response.
///
/// text/plain: Server will send a plain text response (by suing an object's ToString() method).
/// </summary>
type DefaultNegotiationConfig() =
    interface INegotiationConfig with
        member __.Rules =
            dict [
                "*/*"             , json
                "application/json", json
                "application/xml" , xml
                "text/xml"        , xml
                "text/plain"      , fun x -> x.ToString() |> text
            ]
        member __.UnacceptableHandler = unacceptableHandler


/// <summary>
/// An implementation of INegotiationConfig which allows returning JSON only.
///
/// Supported mime types:
///
/// */*: If a client accepts any content type then the server will return a JSON response.
/// application/json: Server will send a JSON response.
/// </summary>
type JsonOnlyNegotiationConfig() =
    interface INegotiationConfig with
        member __.Rules =
            dict [
                "*/*"             , json
                "application/json", json
            ]
        member __.UnacceptableHandler = unacceptableHandler

// ---------------------------
// HttpContext extensions
// ---------------------------

[<Extension>]
type NegotiationExtensions() =
    /// <summary>
    /// Sends a response back to the client based on the request's Accept header.
    ///
    /// If the Accept header cannot be matched with one of the supported mime types from the negotiationRules then the unacceptableHandler will be invoked.
    /// </summary>
    /// <param name="negotiationRules">A dictionary of mime types and response writing <see cref="HttpHandler" /> functions. Each mime type must be mapped to a function which accepts an obj and returns a <see cref="HttpHandler" /> which will send a response in the associated mime type (e.g.: dict [ "application/json", json; "application/xml" , xml ]).</param>
    /// <param name="unacceptableHandler"> A <see cref="HttpHandler" /> function which will be invoked if none of the accepted mime types can be satisfied. Generally this <see cref="HttpHandler" /> would send a response with a status code of 406 Unacceptable.</param>
    /// <param name="responseObj">The object to send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member NegotiateWithAsync
        (ctx                : HttpContext,
        negotiationRules    : IDictionary<string, obj -> HttpHandler>,
        unacceptableHandler : HttpHandler,
        responseObj         : obj) =
        let acceptedMimeTypes = (ctx.Request.GetTypedHeaders()).Accept
        if isNull acceptedMimeTypes || acceptedMimeTypes.Count = 0 then
            let kv = negotiationRules |> Seq.head
            kv.Value responseObj earlyReturn ctx
        else
            let mutable mimeType    = Unchecked.defaultof<_>
            let mutable bestQuality = Double.NegativeInfinity
            let mutable currQuality = 1.
            // Filter the list of acceptedMimeTypes by the negotiationRules
            // and selects the mimetype with the greatest quality
            for x in acceptedMimeTypes do
                if negotiationRules.ContainsKey x.MediaType.Value then
                    currQuality <- (if x.Quality.HasValue then x.Quality.Value else 1.)

                    if  bestQuality <  currQuality then
                        bestQuality <- currQuality
                        mimeType    <- x

            if isNull mimeType then
                unacceptableHandler earlyReturn ctx
            else
                negotiationRules.[mimeType.MediaType.Value] responseObj earlyReturn ctx

    /// <summary>
    /// Sends a response back to the client based on the request's Accept header.
    ///
    /// The negotiation rules as well as a <see cref="HttpHandler" /> for unacceptable requests can be configured in the ASP.NET Core startup code by registering a custom class of type <see cref="INegotiationConfig"/>.
    /// </summary>
    /// <param name="responseObj">The object to send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member NegotiateAsync (ctx : HttpContext, responseObj : obj) =
        let config = ctx.GetService<INegotiationConfig>()
        ctx.NegotiateWithAsync(config.Rules, config.UnacceptableHandler, responseObj)

// ---------------------------
// HttpHandler functions
// ---------------------------

/// <summary>
/// Sends a response back to the client based on the request's Accept header.
///
/// If the Accept header cannot be matched with one of the supported mime types from the negotiationRules then the unacceptableHandler will be invoked.
/// </summary>
/// <param name="negotiationRules">A dictionary of mime types and response writing <see cref="HttpHandler" /> functions. Each mime type must be mapped to a function which accepts an obj and returns a <see cref="HttpHandler" /> which will send a response in the associated mime type (e.g.: dict [ "application/json", json; "application/xml" , xml ]).</param>
/// <param name="unacceptableHandler">A <see cref="HttpHandler" /> function which will be invoked if none of the accepted mime types can be satisfied. Generally this <see cref="HttpHandler" /> would send a response with a status code of 406 Unacceptable.</param>
/// <param name="responseObj">The object to send back to the client.</param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler" /> function which can be composed into a bigger web application.</returns>
let negotiateWith (negotiationRules    : IDictionary<string, obj -> HttpHandler>)
                  (unacceptableHandler : HttpHandler)
                  (responseObj         : obj)
                  : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.NegotiateWithAsync(negotiationRules, unacceptableHandler, responseObj)

/// <summary>
/// Sends a response back to the client based on the request's Accept header.
///
/// The negotiation rules as well as a <see cref="HttpHandler" /> for unacceptable requests can be configured in the ASP.NET Core startup code by registering a custom class of type <see cref="INegotiationConfig"/>.
/// </summary>
/// <param name="responseObj">The object to send back to the client.</param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler" /> function which can be composed into a bigger web application.</returns>
let negotiate (responseObj : obj) : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.NegotiateAsync responseObj