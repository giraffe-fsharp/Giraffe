[<AutoOpen>]
module Giraffe.HttpContextExtensions

open System
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Microsoft.Net.Http.Headers
open Giraffe.Serialization

type HttpContext with

    /// ---------------------------
    /// Dependency management
    /// ---------------------------

    member private __.MissingServiceError (t : string) =
        NullReferenceException (sprintf "Could not retrieve object of type '%s'. Please register all Giraffe dependencies by adding `services.AddGiraffe()` to your startup code. For more information please visit https://github.com/giraffe-fsharp/Giraffe." t)

    member this.GetService<'T>() =
        this.RequestServices.GetService(typeof<'T>) :?> 'T

    member this.GetLogger<'T>() =
        this.GetService<ILogger<'T>>()

    member this.GetLogger (categoryName : string) =
        let loggerFactory = this.GetService<ILoggerFactory>()
        loggerFactory.CreateLogger categoryName

    member this.GetHostingEnvironment() =
        this.GetService<IHostingEnvironment>()

    member this.GetJsonSerializer() : IJsonSerializer =
        let serializer = this.GetService<IJsonSerializer>()
        if isNull serializer then raise (this.MissingServiceError "IJsonSerializer")
        serializer

    member this.GetXmlSerializer()  : IXmlSerializer  =
        let serializer = this.GetService<IXmlSerializer>()
        if isNull serializer then raise (this.MissingServiceError "IXmlSerializer")
        serializer

    /// ---------------------------
    /// Common helpers
    /// ---------------------------

    member this.SetStatusCode (httpStatusCode : int) =
        this.Response.StatusCode <- httpStatusCode

    member this.SetHttpHeader (key : string) (value : obj) =
        this.Response.Headers.[key] <- StringValues(value.ToString())

    member this.SetContentType (contentType : string) =
        this.SetHttpHeader HeaderNames.ContentType contentType

    member this.TryGetRequestHeader (key : string) =
        match this.Request.Headers.TryGetValue key with
        | true, value -> Some (value.ToString())
        | _           -> None

    member this.GetRequestHeader (key : string) =
        match this.Request.Headers.TryGetValue key with
        | true, value -> Ok (value.ToString())
        | _           -> Error (sprintf "HTTP request header '%s' is missing." key)

    member this.TryGetQueryStringValue (key : string) =
        match this.Request.Query.TryGetValue key with
        | true, value -> Some (value.ToString())
        | _           -> None

    member this.GetQueryStringValue (key : string) =
        match this.Request.Query.TryGetValue key with
        | true, value -> Ok (value.ToString())
        | _           -> Error (sprintf "Query string value '%s' is missing." key)