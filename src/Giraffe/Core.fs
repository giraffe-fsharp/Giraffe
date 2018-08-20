[<AutoOpen>]
module Giraffe.Core

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Microsoft.Net.Http.Headers
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe.Serialization

// ---------------------------
// Giraffe exception types
// ---------------------------

type MissingDependencyException(dependencyName : string) =
    inherit Exception(
        sprintf "Could not retrieve object of type '%s' from ASP.NET Core's dependency container. Please register all Giraffe dependencies by adding `services.AddGiraffe()` to your startup code. For more information visit https://github.com/giraffe-fsharp/Giraffe." dependencyName)

// ---------------------------
// HttpContext extensions
// ---------------------------

type HttpContext with

    /// **Description**
    ///
    /// Gets an instance of `'T` from the request's service container.
    ///
    /// **Output**
    ///
    /// Returns an instance of `'T`.
    ///
    member this.GetService<'T>() =
        let t = typeof<'T>
        match this.RequestServices.GetService t with
        | null    -> raise (MissingDependencyException t.Name)
        | service -> service :?> 'T

    /// **Description**
    ///
    /// Gets an instance of `ILogger<'T>` from the request's service container.
    ///
    /// The type `'T` should represent the class or module from where the logger gets instantiated.
    ///
    /// **Output**
    ///
    /// Returns an instance of `ILogger<'T>`.
    ///
    member this.GetLogger<'T>() =
        this.GetService<ILogger<'T>>()

    /// **Description**
    ///
    /// Gets an instance of `ILogger` from the request's service container.
    ///
    /// **Parameters**
    ///
    /// - `categoryName`: The category name for messages produced by this logger.
    ///
    /// **Output**
    ///
    /// Returns an instance of `ILogger`.
    ///
    member this.GetLogger (categoryName : string) =
        let loggerFactory = this.GetService<ILoggerFactory>()
        loggerFactory.CreateLogger categoryName

    /// **Description**
    ///
    /// Gets an instance of `IHostingEnvironment` from the request's service container.
    ///
    /// **Output**
    ///
    /// Returns an instance of `IHostingEnvironment`.
    ///
    member this.GetHostingEnvironment() =
        this.GetService<IHostingEnvironment>()

    /// **Description**
    ///
    /// Gets an instance of `IJsonSerializer` from the request's service container.
    ///
    /// **Output**
    ///
    /// Returns an instance of `Giraffe.Serialization.Json.IJsonSerializer`.
    ///
    member this.GetJsonSerializer() : IJsonSerializer =
        this.GetService<IJsonSerializer>()

    /// **Description**
    ///
    /// Gets an instance of `IXmlSerializer` from the request's service container.
    ///
    /// **Output**
    ///
    /// Returns an instance of `Giraffe.Serialization.Xml.IXmlSerializer`.
    ///
    member this.GetXmlSerializer() : IXmlSerializer  =
        this.GetService<IXmlSerializer>()

    /// **Description**
    ///
    /// Sets the HTTP status code of the response.
    ///
    /// **Parameters**
    ///
    /// - `httpStatusCode`: The status code to be set in the response. For convenience you can use the static `Microsoft.AspNetCore.Http.StatusCodes` class for passing in named status codes instead of using pure `int` values.
    ///
    /// **Output**
    ///
    /// Returns `unit`.
    ///
    member this.SetStatusCode (httpStatusCode : int) =
        this.Response.StatusCode <- httpStatusCode

    /// **Description**
    ///
    /// Adds or sets a HTTP header in the response.
    ///
    /// **Parameters**
    ///
    /// - `key`: The HTTP header name. For convenience you can use the static `Microsoft.Net.Http.Headers.HeaderNames` class for passing in strongly typed header names instead of using pure `string` values.
    /// - `value`: The value to be set. Non string values will be converted to a string using the object's `ToString()` method.
    ///
    /// **Output**
    ///
    /// Returns `unit`.
    ///
    member this.SetHttpHeader (key : string) (value : obj) =
        this.Response.Headers.[key] <- StringValues(value.ToString())

    /// **Description**
    ///
    /// Sets the `Content-Type` HTTP header in the response.
    ///
    /// **Parameters**
    ///
    /// - `contentType`: The mime type of the response (e.g.: `application/json` or `text/html`).
    ///
    /// **Output**
    ///
    /// Returns `unit`.
    ///
    member this.SetContentType (contentType : string) =
        this.SetHttpHeader HeaderNames.ContentType contentType

    /// **Description**
    ///
    /// Tries to get the `string` value of a HTTP header from the request.
    ///
    /// **Parameters**
    ///
    /// - `key`: The name of the HTTP header.
    ///
    /// **Output**
    ///
    /// Returns `Some string` if the HTTP header was present in the request, otherwise returns `None`.
    ///
    member this.TryGetRequestHeader (key : string) =
        match this.Request.Headers.TryGetValue key with
        | true, value -> Some (value.ToString())
        | _           -> None

    /// **Description**
    ///
    /// Retrieves the `string` value of a HTTP header from the request.
    ///
    /// **Parameters**
    ///
    /// - `key`: The name of the HTTP header.
    ///
    /// **Output**
    ///
    /// Returns `Ok string` if the HTTP header was present in the request, otherwise returns `Error string`.
    ///
    member this.GetRequestHeader (key : string) =
        match this.Request.Headers.TryGetValue key with
        | true, value -> Ok (value.ToString())
        | _           -> Error (sprintf "HTTP request header '%s' is missing." key)

    /// **Description**
    ///
    /// Tries to get the `string` value of a query string parameter from the request.
    ///
    /// **Parameters**
    ///
    /// - `key`: The name of the query string parameter.
    ///
    /// **Output**
    ///
    /// Returns `Some string` if the parameter was present in the request's query string, otherwise returns `None`.
    ///
    member this.TryGetQueryStringValue (key : string) =
        match this.Request.Query.TryGetValue key with
        | true, value -> Some (value.ToString())
        | _           -> None

    /// **Description**
    ///
    /// Retrieves the `string` value of a query string parameter from the request.
    ///
    /// **Parameters**
    ///
    /// - `key`: The name of the query string parameter.
    ///
    /// **Output**
    ///
    /// Returns `Ok string` if the parameter was present in the request's query string, otherwise returns `Error string`.
    ///
    member this.GetQueryStringValue (key : string) =
        match this.Request.Query.TryGetValue key with
        | true, value -> Ok (value.ToString())
        | _           -> Error (sprintf "Query string value '%s' is missing." key)

// ---------------------------
// HttpHandler definition
// ---------------------------

/// **Description**
///
/// A type alias for `Task<HttpContext option>` which represents the result of a HTTP function (`HttpFunc`).
///
/// If the result is `Some HttpContext` then the Giraffe middleware will return the response to the client and end the pipeline. However, if the result is `None` then the Giraffe middleware will continue the ASP.NET Core pipeline by invoking the `next` middleware.
///
type HttpFuncResult = Task<HttpContext option>

/// **Description**
///
/// A HTTP function which takes an `HttpContext` object and returns a `HttpFuncResult`.
///
/// The function may inspect the incoming `HttpRequest` and make modifications to the `HttpResponse` before returning a `HttpFuncResult`. The result can be either a `Task` of `Some HttpContext` or a `Task` of `None`.
///
/// If the result is `Some HttpContext` then the Giraffe middleware will return the response to the client and end the pipeline. However, if the result is `None` then the Giraffe middleware will continue the ASP.NET Core pipeline by invoking the `next` middleware.
///
type HttpFunc = HttpContext -> HttpFuncResult

/// **Description**
///
/// A HTTP handler is the core building block of a Giraffe web application. It works similarily to ASP.NET Core's middleware where it is self responsible for invoking the next `HttpFunc` function of the pipeline or shortcircuit the execution by directly returning a `Task` of `HttpContext option`.
///
type HttpHandler = HttpFunc -> HttpFunc

/// **Description**
///
/// The error handler function takes an `Exception` object as well as an `ILogger` instance and returns a `HttpHandler` function which takes care of handling any uncaught application errors.
///
type ErrorHandler = exn -> ILogger -> HttpHandler

// ---------------------------
// Globally useful functions
// ---------------------------

/// **Description**
///
/// The `warbler` function is a `HttpHandler` wrapper function which prevents a `HttpHandler` to be pre-evaluated at startup.
///
/// **Parameters**
///
/// - `f`: A function which takes a `HttpFunc * HttpContext` tuple and returns a `HttpHandler` function.
///
/// **Output**
///
/// Returns a `HttpHandler` function.
///
/// **Example**
///
/// `warbler(fun _ -> someHttpHandler)`
///
let inline warbler f (next : HttpFunc) (ctx : HttpContext) = f (next, ctx) next ctx

/// **Description**
///
/// Use `abort` to shortcircuit the `HttpHandler` pipeline and return `None` to the surrounding `HttpHandler` or the Giraffe middleware (which would subsequently invoke the `next` middleware as a result of it).
///
let internal abort  : HttpFuncResult = Task.FromResult None

/// **Description**
///
/// Use `finish` to shortcircuit the `HttpHandler` pipeline and return `Some HttpContext` to the surrounding `HttpHandler` or the Giraffe middleware (which would subsequently end the pipeline by returning the response back to the client).
///
let internal finish : HttpFunc = Some >> Task.FromResult

// ---------------------------
// Default Combinators
// ---------------------------

/// **Description**
///
/// Combines two `HttpHandler` functions into one.
///
/// Please mind that both `HttpHandler` functions will get pre-evaluated at runtime by applying the `next` `HttpFunc` parameter of each handler.
///
/// You can also use the fish operator `>=>` as a more convenient alternative to `compose`.
///
let compose (handler1 : HttpHandler) (handler2 : HttpHandler) : HttpHandler =
    fun (final : HttpFunc) ->
        let func = final |> handler2 |> handler1
        fun (ctx : HttpContext) ->
            match ctx.Response.HasStarted with
            | true  -> final ctx
            | false -> func ctx

/// **Description**
///
/// Combines two `HttpHandler` functions into one.
///
/// Please mind that both `HttpHandler` functions will get pre-evaluated at runtime by applying the `next` `HttpFunc` parameter of each handler.
///
let (>=>) = compose

/// **Description**
///
/// Iterates through a list of `HttpFunc` functions and returns the result of the first `HttpFunc` of which the outcome is `Some HttpContext`.
///
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

/// **Description**
///
/// Iterates through a list of `HttpHandler` functions and returns the result of the first `HttpHandler` of which the outcome is `Some HttpContext`.
///
/// Please mind that all `HttpHandler` functions will get pre-evaluated at runtime by applying the `next` (`HttpFunc`) parameter to each handler.
///
let choose (handlers : HttpHandler list) : HttpHandler =
    fun (next : HttpFunc) ->
        let funcs = handlers |> List.map (fun h -> h next)
        fun (ctx : HttpContext) ->
            chooseHttpFunc funcs ctx

// ---------------------------
// Default HttpHandlers
// ---------------------------

/// **Description**
///
/// Filters an incoming HTTP request based on the HTTP verb.
///
/// **Parameters**
///
/// - `validate`: A validation function which checks for a single HTTP verb.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let private httpVerb (validate : string -> bool) : HttpHandler =
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

/// **Description**
///
/// Clears the current `HttpResponse` object.
///
/// This can be useful if a `HttpHandler` function needs to overwrite the response of all previous `HttpHandler` functions with its own response (most commonly used by an `ErrorHandler` function).
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let clearResponse : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.Response.Clear()
        next ctx

/// **Description**
///
/// Sets the HTTP status code of the response.
///
/// **Parameters**
///
/// - `statusCode`: The status code to be set in the response. For convenience you can use the static `Microsoft.AspNetCore.Http.StatusCodes` class for passing in named status codes instead of using pure `int` values.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let setStatusCode (statusCode : int) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetStatusCode statusCode
        next ctx

/// **Description**
///
/// Adds or sets a HTTP header in the response.
///
/// **Parameters**
///
/// - `key`: The HTTP header name. For convenience you can use the static `Microsoft.Net.Http.Headers.HeaderNames` class for passing in strongly typed header names instead of using pure `string` values.
/// - `value`: The value to be set. Non string values will be converted to a string using the object's `ToString()` method.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let setHttpHeader (key : string) (value : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetHttpHeader key value
        next ctx

/// **Description**
///
/// Filters an incoming HTTP request based on the accepted mime types of the client (`Accept` HTTP header).
///
/// If the client doesn't accept any of the provided `mimeTypes` then the handler will not continue executing the `next` `HttpHandler` function.
///
/// **Parameters**
///
/// - `mimeTypes`: List of mime types of which the client has to accept at least one.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let mustAccept (mimeTypes : string list) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let headers = ctx.Request.GetTypedHeaders()
        headers.Accept
        |> Seq.map    (fun h -> h.ToString())
        |> Seq.exists (fun h -> mimeTypes |> Seq.contains h)
        |> function
            | true  -> next ctx
            | false -> abort

/// **Description**
///
/// Redirects to a different location with a `302` or `301` (when permanent) HTTP status code.
///
/// **Parameters**
///
/// - `permanent`: If true the redirect is permanent (301), otherwise temporary (302).
/// - `location`: The URL to redirect the client to.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let redirectTo (permanent : bool) (location : string) : HttpHandler  =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.Response.Redirect(location, permanent)
        Task.FromResult (Some ctx)