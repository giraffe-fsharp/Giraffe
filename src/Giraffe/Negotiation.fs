[<AutoOpen>]
module Giraffe.Negotiation

open System
open System.Collections.Generic
open Microsoft.AspNetCore.Http

// ---------------------------
// Configuration types
// ---------------------------

/// **Description**
///
/// Interface defining the negotiation rules and the `HttpHandler` for unacceptable requests when doing content negotiation in Giraffe.
///
type INegotiationConfig =
    /// **Description**
    ///
    /// A dictionary of mime types and response writing `HttpHandler` functions.
    ///
    /// Each mime type must be mapped to a function which accepts an `obj` and returns a `HttpHandler` which will send a response in the associated mime type.
    ///
    /// **Example**
    ///
    /// `dict [ "application/json", json; "application/xml" , xml ]`
    ///
    abstract member Rules : IDictionary<string, obj -> HttpHandler>

    /// **Description**
    ///
    /// A `HttpHandler` function which will be invoked if none of the accepted mime types can be satisfied. Generally this `HttpHandler` would send a response with a status code of `406 Unacceptable`.
    ///
    abstract member UnacceptableHandler : HttpHandler

/// **Description**
///
/// The default implementation of `INegotiationConfig`.
///
/// **Supported mime types**
///
/// - `*/*`: If a client accepts any content type then the server will return a JSON response.
/// - `application/json`: Server will send a JSON response.
/// - `application/xml`: Server will send an XML response.
/// - `text/xml`: Server will send an XML response.
/// - `text/plain`: Server will send a plain text response (by suing an object's `ToString()` method).
///
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
        member __.UnacceptableHandler =
            fun (next : HttpFunc) (ctx : HttpContext) ->
                (setStatusCode 406
                >=> ((ctx.Request.Headers.["Accept"]).ToString()
                    |> sprintf "%s is unacceptable by the server."
                    |> text)) next ctx

// ---------------------------
// HttpContext extensions
// ---------------------------

type HttpContext with
    /// **Description**
    ///
    /// Sends a response back to the client based on the request's `Accept` header.
    ///
    /// If the `Accept` header cannot be matched with one of the supported mime types from the `negotiationRules` then the `unacceptableHandler` will be invoked.
    ///
    /// **Parameters**
    ///
    /// - `negotiationRules`: A dictionary of mime types and response writing `HttpHandler` functions. Each mime type must be mapped to a function which accepts an `obj` and returns a `HttpHandler` which will send a response in the associated mime type (e.g.: `dict [ "application/json", json; "application/xml" , xml ]`).
    /// - `unacceptableHandler`: A `HttpHandler` function which will be invoked if none of the accepted mime types can be satisfied. Generally this `HttpHandler` would send a response with a status code of `406 Unacceptable`.
    /// - `responseObj`: The object to send back to the client.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.NegotiateWithAsync (negotiationRules    : IDictionary<string, obj -> HttpHandler>)
                              (unacceptableHandler : HttpHandler)
                              (responseObj         : obj) =
        let acceptedMimeTypes = (this.Request.GetTypedHeaders()).Accept
        if isNull acceptedMimeTypes || acceptedMimeTypes.Count = 0 then
            let kv = negotiationRules |> Seq.head
            kv.Value responseObj finish this
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
                unacceptableHandler finish this
            else
                negotiationRules.[mimeType.MediaType.Value] responseObj finish this

    /// **Description**
    ///
    /// Sends a response back to the client based on the request's `Accept` header.
    ///
    /// The negotiation rules as well as a `HttpHandler` for unacceptable requests can be configured in the ASP.NET Core startup code by registering a custom class of type `INegotiationConfig`.
    ///
    /// **Parameters**
    ///
    /// - `responseObj`: The object to send back to the client.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.NegotiateAsync (responseObj : obj) =
        let config = this.GetService<INegotiationConfig>()
        this.NegotiateWithAsync config.Rules config.UnacceptableHandler responseObj

// ---------------------------
// HttpHandler functions
// ---------------------------

/// **Description**
///
/// Sends a response back to the client based on the request's `Accept` header.
///
/// If the `Accept` header cannot be matched with one of the supported mime types from the `negotiationRules` then the `unacceptableHandler` will be invoked.
///
/// **Parameters**
///
/// - `negotiationRules`: A dictionary of mime types and response writing `HttpHandler` functions. Each mime type must be mapped to a function which accepts an `obj` and returns a `HttpHandler` which will send a response in the associated mime type (e.g.: `dict [ "application/json", json; "application/xml" , xml ]`).
/// - `unacceptableHandler`: A `HttpHandler` function which will be invoked if none of the accepted mime types can be satisfied. Generally this `HttpHandler` would send a response with a status code of `406 Unacceptable`.
/// - `responseObj`: The object to send back to the client.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let negotiateWith (negotiationRules    : IDictionary<string, obj -> HttpHandler>)
                  (unacceptableHandler : HttpHandler)
                  (responseObj         : obj)
                  : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.NegotiateWithAsync negotiationRules unacceptableHandler responseObj

/// **Description**
///
/// Sends a response back to the client based on the request's `Accept` header.
///
/// The negotiation rules as well as a `HttpHandler` for unacceptable requests can be configured in the ASP.NET Core startup code by registering a custom class of type `INegotiationConfig`.
///
/// **Parameters**
///
/// - `responseObj`: The object to send back to the client.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let negotiate (responseObj : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.NegotiateAsync responseObj