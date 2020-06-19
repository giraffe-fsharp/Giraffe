[<AutoOpen>]
module Giraffe.Core

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
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

    /// <summary>
    /// Returns the entire request URL in a fully escaped form, which is suitable for use in HTTP headers and other operations.
    /// </summary>
    /// <returns>Returns a <see cref="System.String"/> URL.</returns>
    member this.GetRequestUrl() =
        this.Request.GetEncodedUrl()

    /// <summary>
    /// Gets an instance of `'T` from the request's service container.
    /// </summary
    /// <returns>Returns an instance of `'T`.</returns>
    member this.GetService<'T>() =
        let t = typeof<'T>
        match this.RequestServices.GetService t with
        | null    -> raise (MissingDependencyException t.Name)
        | service -> service :?> 'T

    /// <summary>
    /// Gets an instance of <see cref="Microsoft.Extensions.Logging.ILogger<'T>" /> from the request's service container.
    ///
    /// The type `'T` should represent the class or module from where the logger gets instantiated.
    /// </summary>
    /// <returns> Returns an instance of <see cref="Microsoft.Extensions.Logging.ILogger<'T>" />.</returns>
    member this.GetLogger<'T>() =
        this.GetService<ILogger<'T>>()

    /// <summary>
    /// Gets an instance of <see cref="Microsoft.Extensions.Logging.ILogger"/> from the request's service container.
    /// </summary>
    /// <param name="categoryName">The category name for messages produced by this logger.</param>
    /// <returns>Returns an instance of <see cref="Microsoft.Extensions.Logging.ILogger"/>.</returns>
    member this.GetLogger (categoryName : string) =
        let loggerFactory = this.GetService<ILoggerFactory>()
        loggerFactory.CreateLogger categoryName

    /// <summary>
    /// Gets an instance of <see cref="Microsoft.Extensions.Hosting.IHostingEnvironment"/> from the request's service container.
    /// </summary>
    /// <returns>Returns an instance of <see cref="Microsoft.Extensions.Hosting.IHostingEnvironment"/>.</returns>
    member this.GetHostingEnvironment() =
        this.GetService<IHostingEnvironment>()
    
    /// <summary>
    /// Gets an instance of <see cref="Giraffe.Serialization.Json.IJsonSerializer"/> from the request's service container.
    /// </summary>
    /// <returns>Returns an instance of <see cref="Giraffe.Serialization.Json.IJsonSerializer"/>.</returns>
    member this.GetJsonSerializer() : IJsonSerializer =
        this.GetService<IJsonSerializer>()

    /// <summary>
    /// Gets an instance of <see cref="Giraffe.Serialization.Xml.IXmlSerializer"/> from the request's service container.
    /// </summary>
    /// <returns>Returns an instance of <see cref="Giraffe.Serialization.Xml.IXmlSerializer"/>.</returns>
    member this.GetXmlSerializer() : IXmlSerializer  =
        this.GetService<IXmlSerializer>()
    
    /// <summary>
    /// Sets the HTTP status code of the response.
    /// </summary>
    /// <param name="httpStatusCode">The status code to be set in the response. For convenience you can use the static <see cref="Microsoft.AspNetCore.Http.StatusCodes"/> class for passing in named status codes instead of using pure int values.</param>
    member this.SetStatusCode (httpStatusCode : int) =
        this.Response.StatusCode <- httpStatusCode
    
    /// <summary>
    /// Adds or sets a HTTP header in the response.
    /// </summary>
    /// <param name="key">The HTTP header name. For convenience you can use the static <see cref="Microsoft.Net.Http.Headers.HeaderNames"/> class for passing in strongly typed header names instead of using pure `string` values.</param>
    /// <param name="value">The value to be set. Non string values will be converted to a string using the object's ToString() method.</param>
    member this.SetHttpHeader (key : string) (value : obj) =
        this.Response.Headers.[key] <- StringValues(value.ToString())
    
    /// <summary>
    /// Sets the Content-Type HTTP header in the response.
    /// </summary>
    /// <param name="contentType">The mime type of the response (e.g.: application/json or text/html).</param>
    member this.SetContentType (contentType : string) =
        this.SetHttpHeader HeaderNames.ContentType contentType
    
    /// <summary>
    /// Tries to get the <see cref="System.String"/> value of a HTTP header from the request.
    /// </summary>
    /// <param name="key">The name of the HTTP header.</param>
    /// <returns> Returns Some string if the HTTP header was present in the request, otherwise returns None.</returns>
    member this.TryGetRequestHeader (key : string) =
        match this.Request.Headers.TryGetValue key with
        | true, value -> Some (value.ToString())
        | _           -> None
    /// <summary>
    /// Retrieves the <see cref="System.String"/> value of a HTTP header from the request.
    /// </summary>
    /// <param name="key">The name of the HTTP header.</param>
    /// <returns>Returns Ok string if the HTTP header was present in the request, otherwise returns Error string.</returns>
    member this.GetRequestHeader (key : string) =
        match this.Request.Headers.TryGetValue key with
        | true, value -> Ok (value.ToString())
        | _           -> Error (sprintf "HTTP request header '%s' is missing." key)

    /// <summary>
    ///  Tries to get the <see cref="System.String"/> value of a query string parameter from the request.
    /// </summary>
    /// <param name="key">The name of the query string parameter.</param>
    /// <returns>Returns Some string if the parameter was present in the request's query string, otherwise returns None.</returns>
    member this.TryGetQueryStringValue (key : string) =
        match this.Request.Query.TryGetValue key with
        | true, value -> Some (value.ToString())
        | _           -> None
    
    /// <summary>
    /// Retrieves the <see cref="System.String"/> value of a query string parameter from the request.
    /// </summary>
    /// <param name="key">The name of the query string parameter.</param>
    /// <returns>Returns Ok string if the parameter was present in the request's query string, otherwise returns Error string.</returns>
    member this.GetQueryStringValue (key : string) =
        match this.Request.Query.TryGetValue key with
        | true, value -> Ok (value.ToString())
        | _           -> Error (sprintf "Query string value '%s' is missing." key)
    
    /// <summary>
    /// Retrieves the <see cref="System.String"/> value of a cookie from the request.
    /// </summary>
    /// <param name="key">The name of the cookie.</param>
    /// <returns>Returns Some string if the cookie was set, otherwise returns None.</returns>
    member this.GetCookieValue (key : string) =
        match this.Request.Cookies.TryGetValue key with
        | true , cookie -> Some cookie
        | false, _      -> None

    /// <summary>
    /// Retrieves the <see cref="System.String"/> value of a form parameter from the request.
    /// </summary>
    /// <param name="key">The name of the form parameter.</param>
    /// <returns>Returns Some string if the form parameter was set, otherwise returns None.</returns>
    member this.GetFormValue (key : string) =
        match this.Request.HasFormContentType with
        | false -> None
        | true  ->
            match this.Request.Form.TryGetValue key with
            | true , value -> Some (value.ToString())
            | false, _     -> None

// ---------------------------
// HttpHandler definition
// ---------------------------

/// <summary>
/// A type alias for <see cref="System.Threading.Tasks.Task{HttpContext option}" />  which represents the result of a HTTP function (HttpFunc).
/// If the result is Some HttpContext then the Giraffe middleware will return the response to the client and end the pipeline. However, if the result is None then the Giraffe middleware will continue the ASP.NET Core pipeline by invoking the next middleware.
/// </summary>
type HttpFuncResult = Task<HttpContext option>

/// <summary>
/// A HTTP function which takes an <see cref="Microsoft.AspNetCore.Http.HttpContext"/> object and returns a <see cref="HttpFuncResult"/>.
/// The function may inspect the incoming <see cref="Microsoft.AspNetCore.Http.HttpRequest"/> and make modifications to the <see cref="Microsoft.AspNetCore.Http.HttpResponse"/> before returning a <see cref="HttpFuncResult"/>. The result can be either a <see cref="System.Threading.Tasks.Task"/> of Some HttpContext or a <see cref="System.Threading.Tasks.Task"/> of None.
/// If the result is Some HttpContext then the Giraffe middleware will return the response to the client and end the pipeline. However, if the result is None then the Giraffe middleware will continue the ASP.NET Core pipeline by invoking the next middleware.
/// </summary>
type HttpFunc = HttpContext -> HttpFuncResult

/// <summary>
/// A HTTP handler is the core building block of a Giraffe web application. It works similarly to ASP.NET Core's middleware where it is self responsible for invoking the next <see cref="HttpFunc"/> function of the pipeline or shortcircuit the execution by directly returning a <see cref="System.Threading.Tasks.Task"/> of HttpContext option.
/// </summary>
type HttpHandler = HttpFunc -> HttpFunc

/// <summary>
/// The error handler function takes an <see cref="System.Exception"/> object as well as an <see cref="Microsoft.Extensions.Logging.ILogger"/> instance and returns a <see cref="HttpHandler"/> function which takes care of handling any uncaught application errors.
/// </summary>
type ErrorHandler = exn -> ILogger -> HttpHandler

// ---------------------------
// Globally useful functions
// ---------------------------

/// <summary>
/// The warbler function is a <see cref="HttpHandler"/> wrapper function which prevents a <see cref="HttpHandler"/> to be pre-evaluated at startup.
/// </summary>
/// <param name="f">A function which takes a HttpFunc * HttpContext tuple and returns a <see cref="HttpHandler"/> function.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <example>
/// <code>
/// warbler(fun _ -> someHttpHandler)
/// </code>
/// </example>
/// <returns>Returns a <see cref="HttpHandler"/> function.</returns>
let inline warbler f (next : HttpFunc) (ctx : HttpContext) = f (next, ctx) next ctx

/// <summary>
/// Use skipPipeline to shortcircuit the <see cref="HttpHandler"/> pipeline and return None to the surrounding <see cref="HttpHandler"/> or the Giraffe middleware (which would subsequently invoke the next middleware as a result of it).
/// </summary>
let skipPipeline : HttpFuncResult = Task.FromResult None

/// <summary>
/// Use earlyReturn to shortcircuit the <see cref="HttpHandler"/> pipeline and return Some HttpContext to the surrounding <see cref="HttpHandler"/> or the Giraffe middleware (which would subsequently end the pipeline by returning the response back to the client).
/// </summary>
let earlyReturn : HttpFunc = Some >> Task.FromResult

// ---------------------------
// Convenience Handlers
// ---------------------------

/// <summary>
/// The handleContext function is a convenience function which can be used to create a new <see cref="HttpHandler"/> function which only requires access to the <see cref="Microsoft.AspNetCore.Http.HttpContext"/> object.
/// </summary>
/// <param name="contextMap">A function which accepts a <see cref="Microsoft.AspNetCore.Http.HttpContext"/> object and returns a <see cref="HttpFuncResult"/> function.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let handleContext (contextMap : HttpContext -> HttpFuncResult) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            match! contextMap ctx with
            | Some c ->
                match c.Response.HasStarted with
                | true  -> return  Some c
                | false -> return! next c
            | None      -> return  None
        }

// ---------------------------
// Default Combinators
// ---------------------------

/// <summary>
/// Combines two <see cref="HttpHandler"/> functions into one.
/// Please mind that both <see cref="HttpHandler"/>  functions will get pre-evaluated at runtime by applying the next <see cref="HttpFunc"/> parameter of each handler.
/// You can also use the fish operator `>=>` as a more convenient alternative to compose.
/// </summary>
/// <param name="handler1"></param>
/// <param name="handler2"></param>
/// <param name="final"></param>
/// <returns>A <see cref="HttpFunc"/>.</returns>
let compose (handler1 : HttpHandler) (handler2 : HttpHandler) : HttpHandler =
    fun (final : HttpFunc) ->
        let func = final |> handler2 |> handler1
        fun (ctx : HttpContext) ->
            match ctx.Response.HasStarted with
            | true  -> final ctx
            | false -> func ctx

/// <summary>
/// Combines two <see cref="HttpHandler"/> functions into one.
/// Please mind that both <see cref="HttpHandler"/> functions will get pre-evaluated at runtime by applying the next <see cref="HttpFunc"/> parameter of each handler.
/// </summary>
let (>=>) = compose

/// <summary>
/// Iterates through a list of `HttpFunc` functions and returns the result of the first `HttpFunc` of which the outcome is `Some HttpContext`.
/// </summary>
/// <param name="funcs"></param>
/// <param name="ctx"></param>
/// <returns>A <see cref="HttpFuncResult"/>.</returns>
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

/// <summary>
/// Iterates through a list of <see cref="HttpHandler"/> functions and returns the result of the first <see cref="HttpHandler"/> of which the outcome is Some HttpContext.
/// Please mind that all <see cref="HttpHandler"/> functions will get pre-evaluated at runtime by applying the next (HttpFunc) parameter to each handler.
/// </summary>
/// <param name="handlers"></param>
/// <param name="next"></param>
/// <returns>A <see cref="HttpFunc"/>.</returns>
let choose (handlers : HttpHandler list) : HttpHandler =
    fun (next : HttpFunc) ->
        let funcs = handlers |> List.map (fun h -> h next)
        fun (ctx : HttpContext) ->
            chooseHttpFunc funcs ctx

// ---------------------------
// Default HttpHandlers
// ---------------------------

/// <summary>
/// Filters an incoming HTTP request based on the HTTP verb.
/// </summary>
/// <param name="validate">A validation function which checks for a single HTTP verb.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let private httpVerb (validate : string -> bool) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if validate ctx.Request.Method
        then next ctx
        else skipPipeline

let GET     : HttpHandler = httpVerb HttpMethods.IsGet
let POST    : HttpHandler = httpVerb HttpMethods.IsPost
let PUT     : HttpHandler = httpVerb HttpMethods.IsPut
let PATCH   : HttpHandler = httpVerb HttpMethods.IsPatch
let DELETE  : HttpHandler = httpVerb HttpMethods.IsDelete
let HEAD    : HttpHandler = httpVerb HttpMethods.IsHead
let OPTIONS : HttpHandler = httpVerb HttpMethods.IsOptions
let TRACE   : HttpHandler = httpVerb HttpMethods.IsTrace
let CONNECT : HttpHandler = httpVerb HttpMethods.IsConnect

let GET_HEAD : HttpHandler = choose [ GET; HEAD ]

/// <summary>
/// Clears the current <see cref="Microsoft.AspNetCore.Http.HttpResponse"/> object.
/// This can be useful if a <see cref="HttpHandler"/> function needs to overwrite the response of all previous <see cref="HttpHandler"/> functions with its own response (most commonly used by an <see cref="ErrorHandler"/> function).
/// </summary>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let clearResponse : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.Response.Clear()
        next ctx

/// <summary>
/// Sets the HTTP status code of the response.
/// </summary>
/// <param name="statusCode">The status code to be set in the response. For convenience you can use the static <see cref="Microsoft.AspNetCore.Http.StatusCodes"/> class for passing in named status codes instead of using pure int values.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let setStatusCode (statusCode : int) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetStatusCode statusCode
        next ctx

/// <summary>
/// Adds or sets a HTTP header in the response.
/// </summary>
/// <param name="key">The HTTP header name. For convenience you can use the static <see cref="Microsoft.Net.Http.Headers.HeaderNames"/> class for passing in strongly typed header names instead of using pure string values.</param>
/// <param name="value">The value to be set. Non string values will be converted to a string using the object's ToString() method.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let setHttpHeader (key : string) (value : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetHttpHeader key value
        next ctx

/// <summary>
/// Filters an incoming HTTP request based on the accepted mime types of the client (Accept HTTP header).
/// If the client doesn't accept any of the provided mimeTypes then the handler will not continue executing the next <see cref="HttpHandler"/> function.
/// </summary>
/// <param name="mimeTypes">List of mime types of which the client has to accept at least one.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let mustAccept (mimeTypes : string list) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let headers = ctx.Request.GetTypedHeaders()
        headers.Accept
        |> Seq.map    (fun h -> h.ToString())
        |> Seq.exists (fun h -> mimeTypes |> Seq.contains h)
        |> function
            | true  -> next ctx
            | false -> skipPipeline

/// <summary>
/// Redirects to a different location with a `302` or `301` (when permanent) HTTP status code.
/// </summary>
/// <param name="permanent">If true the redirect is permanent (301), otherwise temporary (302).</param>
/// <param name="location">The URL to redirect the client to.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let redirectTo (permanent : bool) (location : string) : HttpHandler  =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.Response.Redirect(location, permanent)
        Task.FromResult (Some ctx)