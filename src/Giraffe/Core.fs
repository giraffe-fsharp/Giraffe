[<AutoOpen>]
module Giraffe.Core

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Microsoft.Net.Http.Headers
open Giraffe.Serialization

/// ---------------------------
/// Useful extension methods
/// ---------------------------

type DateTime with
    member this.ToHtmlString() = this.ToString("r")

type DateTimeOffset with
    member this.ToHtmlString() = this.ToString("r")

/// ---------------------------
/// HttpContext extensions
/// ---------------------------

type HttpContext with

    /// Dependency management

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

    /// Common helpers

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

/// ---------------------------
/// HttpHandler definition
/// ---------------------------

type HttpFuncResult = Task<HttpContext option>
type HttpFunc       = HttpContext -> HttpFuncResult
type HttpHandler    = HttpFunc -> HttpFunc
type ErrorHandler   = exn -> ILogger -> HttpHandler

/// ---------------------------
/// Globally useful functions
/// ---------------------------

let inline warbler f (next : HttpFunc) (ctx : HttpContext) = f (next, ctx) next ctx

let internal abort  : HttpFuncResult = Task.FromResult None
let internal finish : HttpFunc       = Some >> Task.FromResult

/// ---------------------------
/// Default Combinators
/// ---------------------------

/// Combines two HttpHandler functions into one.
let compose (handler1 : HttpHandler) (handler2 : HttpHandler) : HttpHandler =
    fun (final : HttpFunc) ->
        let func = final |> handler2 |> handler1
        fun (ctx : HttpContext) ->
            match ctx.Response.HasStarted with
            | true  -> final ctx
            | false -> func ctx

/// Combines two HttpHandler functions into one.
/// See compose for more information.
let (>=>) = compose

// Allows a pre-complied list of HttpFuncs to be tested,
// by pre-applying next to handler list passed from choose
let rec private chooseHttpFunc (funcs : HttpFunc list) : HttpFunc =
    fun (ctx : HttpContext) ->
        task {
            match funcs with
            | [] -> return None
            | func :: tail ->
                let! result = func ctx
                match result with
                | Some c -> return Some c
                | None   -> return! chooseHttpFunc tail ctx
        }

/// Iterates through a list of HttpHandler functions and returns the
/// result of the first HttpHandler which outcome is Some HttpContext
let choose (handlers : HttpHandler list) : HttpHandler =
    fun (next : HttpFunc) ->
        let funcs = handlers |> List.map (fun h -> h next)
        fun (ctx : HttpContext) ->
            chooseHttpFunc funcs ctx

/// ---------------------------
/// Default HttpHandlers
/// ---------------------------

/// Filters an incoming HTTP request based on the HTTP verb
let httpVerb (validate : string -> bool) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if validate ctx.Request.Method
        then next ctx
        else abort

let GET     : HttpHandler = httpVerb HttpMethods.IsGet
let POST    : HttpHandler = httpVerb HttpMethods.IsPost
let PUT     : HttpHandler = httpVerb HttpMethods.IsPut
let PATCH   : HttpHandler = httpVerb HttpMethods.IsPatch
let DELETE  : HttpHandler = httpVerb HttpMethods.IsDelete
let HEAD    : HttpHandler = httpVerb HttpMethods.IsHead
let OPTIONS : HttpHandler = httpVerb HttpMethods.IsOptions
let TRACE   : HttpHandler = httpVerb HttpMethods.IsTrace
let CONNECT : HttpHandler = httpVerb HttpMethods.IsConnect

/// Attempts to clear the current HttpResponse object.
/// This can be useful inside an error handler when the response
/// needs to be overwritten in the case of a failure.
let clearResponse : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.Response.Clear()
        next ctx

/// Sets the HTTP response status code.
let setStatusCode (statusCode : int) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetStatusCode statusCode
        next ctx

/// Sets a HTTP header in the HTTP response.
let setHttpHeader (key : string) (value : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetHttpHeader key value
        next ctx

/// Filters an incoming HTTP request based on the accepted
/// mime types of the client.
let mustAccept (mimeTypes : string list) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let headers = ctx.Request.GetTypedHeaders()
        headers.Accept
        |> Seq.map    (fun h -> h.ToString())
        |> Seq.exists (fun h -> mimeTypes |> Seq.contains h)
        |> function
            | true  -> next ctx
            | false -> abort

/// Redirects to a different location with a 302 or 301 (when permanent) HTTP status code.
let redirectTo (permanent : bool) (location : string) : HttpHandler  =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.Response.Redirect(location, permanent)
        Task.FromResult (Some ctx)