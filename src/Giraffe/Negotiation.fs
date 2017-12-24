namespace Giraffe

open System
open System.Collections.Generic
open Microsoft.AspNetCore.Http

module Negotiation =

    type INegotiationConfig =
        abstract member Rules : IDictionary<string, obj -> HttpHandler>
        abstract member UnacceptableHandler : HttpHandler

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

[<AutoOpen>]
module NegotiationHandlers =
    open Negotiation

    /// Checks the HTTP Accept header of the request and determines the most appropriate
    /// response type from a given set of negotiationRules. If the Accept header cannot be
    /// matched with a supported media type then it will invoke the unacceptableHandler.
    ///
    /// The negotiationRules must be a dictionary of supported media types with a matching
    /// HttpHandler which can serve that particular media type.
    ///
    /// Example:
    /// let negotiationRules = dict [ "application/json", json; "application/xml" , xml ]
    /// `json` and `xml` are both the respective default HttpHandler functions in this example.
    let negotiateWith (negotiationRules    : IDictionary<string, obj -> HttpHandler>)
                      (unacceptableHandler : HttpHandler)
                      (responseObj         : obj)
                      : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let acceptedMimeTypes = (ctx.Request.GetTypedHeaders()).Accept
            if isNull acceptedMimeTypes || acceptedMimeTypes.Count = 0 then
                let kv = negotiationRules |> Seq.head
                kv.Value responseObj next ctx
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
                    unacceptableHandler next ctx
                else
                    negotiationRules.[mimeType.MediaType.Value] responseObj next ctx

    /// Same as negotiateWith except that it specifies a default set of negotiation rules
    /// and a default unacceptableHandler.
    ///
    /// The default negotiation rules and the handler for unacceptable HTTP requests can
    /// be modified by implementing an object of type `INegotiationConfig` and
    /// registering in your startup class.
    let negotiate (responseObj : obj) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let config = ctx.GetService<INegotiationConfig>()
            (negotiateWith
                config.Rules
                config.UnacceptableHandler
                responseObj) next ctx