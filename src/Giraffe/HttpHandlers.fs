module Giraffe.HttpHandlers

open System
open System.Text
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Mvc.Razor
open Microsoft.AspNetCore.Mvc.ViewFeatures
open FSharp.Core.Printf
open DotLiquid
open Giraffe.Common
open Giraffe.FormatExpressions
open Giraffe.RazorEngine
open Giraffe.HtmlEngine

type HttpHandlerContext =
    {
        /// ASP.NET Core HttpContext
        HttpContext : HttpContext

        /// Service Locator to retrieve registered services on demand
        Services    : IServiceProvider

        /// Default logger
        Logger      : ILogger
    }

type HttpHandlerResult = Async<HttpHandlerContext option>

type HttpHandler = HttpHandlerContext -> HttpHandlerResult

type ErrorHandler = exn -> HttpHandler

/// ---------------------------
/// Globally useful functions
/// ---------------------------

let inline warbler f a = f a a

/// ---------------------------
/// Sub route helper functions
/// ---------------------------

[<Literal>]
let private RouteKey = "giraffe_route"

let private getSavedSubPath (ctx : HttpContext) =
    if ctx.Items.ContainsKey RouteKey
    then ctx.Items.Item RouteKey |> string |> strOption 
    else None

let private getPath (ctx : HttpContext) =
    match getSavedSubPath ctx with
    | Some p -> ctx.Request.Path.ToString().[p.Length..]
    | None   -> ctx.Request.Path.ToString()

let private handlerWithRootedPath (path : string) (handler : HttpHandler) = 
    fun (ctx : HttpHandlerContext) ->
        async {
            let savedSubPath = getSavedSubPath ctx.HttpContext
            try
                ctx.HttpContext.Items.Item RouteKey <- ((savedSubPath |> Option.defaultValue "") + path)
                return! handler ctx
            finally
                match savedSubPath with
                | Some savedSubPath -> ctx.HttpContext.Items.Item RouteKey <- savedSubPath
                | None              -> ctx.HttpContext.Items.Remove RouteKey |> ignore
        }

/// ---------------------------
/// Default HttpHandlers
/// ---------------------------

/// Adapts a HttpHandler function to accept a HttpHandlerResult.
/// If the HttpHandlerResult returns Some HttpContext, then it will proceed
/// to the handler, otherwise short circuit and return None as the result.
/// If the response has already been written in the resulting HttpContext,
/// then it will skip the HttpHandler as well.
let bind (handler : HttpHandler) =
    fun (result : HttpHandlerResult) ->
        async {
            let! ctx = result
            match ctx with
            | None   -> return None
            | Some c ->
                match c.HttpContext.Response.HasStarted with
                | true  -> return  Some c
                | false -> return! handler c
        }

/// Combines two HttpHandler functions into one.
let compose (handler : HttpHandler) (handler2 : HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        handler ctx |> bind handler2

/// Adapts a HttpHandler function to accept a HttpHandlerResult.
/// See bind for more information.
let (>>=) = bind

/// Combines two HttpHandler functions into one.
/// See bind for more information.
let (>=>) = compose

/// Iterates through a list of HttpHandler functions and returns the
/// result of the first HttpHandler which outcome is Some HttpContext
let rec choose (handlers : HttpHandler list) =
    fun (ctx : HttpHandlerContext) ->
        async {
            match handlers with
            | []              -> return None
            | handler :: tail ->
                let! result = handler ctx
                match result with
                | Some c -> return Some c
                | None   -> return! choose tail ctx
        }

/// Filters an incoming HTTP request based on the HTTP verb
let httpVerb (verb : string) =
    fun (ctx : HttpHandlerContext) ->
        if ctx.HttpContext.Request.Method.Equals verb
        then Some ctx
        else None
        |> async.Return

let GET     = httpVerb "GET"
let POST    = httpVerb "POST"
let PUT     = httpVerb "PUT"
let PATCH   = httpVerb "PATCH"
let DELETE  = httpVerb "DELETE"

/// Filters an incoming HTTP request based on the accepted
/// mime types of the client.
let mustAccept (mimeTypes : string list) =
    fun (ctx : HttpHandlerContext) ->
        let headers = ctx.HttpContext.Request.GetTypedHeaders()
        headers.Accept
        |> Seq.map    (fun h -> h.ToString())
        |> Seq.exists (fun h -> mimeTypes |> Seq.contains h)
        |> function
            | true  -> Some ctx
            | false -> None
            |> async.Return

/// Challenges the client to authenticate with a given authentication scheme.
let challenge (authScheme : string) =
    fun (ctx : HttpHandlerContext) ->
        async {
            let auth = ctx.HttpContext.Authentication
            do! auth.ChallengeAsync authScheme |> Async.AwaitTask
            return Some ctx
        }

/// Signs off the current user.
let signOff (authScheme : string) =
    fun (ctx : HttpHandlerContext) ->
        async {
            let auth = ctx.HttpContext.Authentication
            do! auth.SignOutAsync authScheme |> Async.AwaitTask
            return Some ctx
        }

/// Validates if a user is authenticated.
/// If not it will proceed with the authFailedHandler.
let requiresAuthentication (authFailedHandler : HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        let user = ctx.HttpContext.User
        if isNotNull user && user.Identity.IsAuthenticated
        then async.Return (Some ctx)
        else authFailedHandler ctx

/// Validates if a user is in a specific role.
/// If not it will proceed with the authFailedHandler.
let requiresRole (role : string) (authFailedHandler : HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        let user = ctx.HttpContext.User
        if user.IsInRole role
        then async.Return (Some ctx)
        else authFailedHandler ctx

/// Validates if a user has at least one of the specified roles.
/// If not it will proceed with the authFailedHandler.
let requiresRoleOf (roles : string list) (authFailedHandler : HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        let user = ctx.HttpContext.User
        roles
        |> List.exists user.IsInRole 
        |> function
            | true  -> async.Return (Some ctx)
            | false -> authFailedHandler ctx

/// Attempts to clear the current HttpResponse object.
/// This can be useful inside an error handler when the response
/// needs to be overwritten in the case of a failure.
let clearResponse =
    fun (ctx : HttpHandlerContext) ->
        ctx.HttpContext.Response.Clear()
        async.Return (Some ctx)

/// Filters an incoming HTTP request based on the request path (case sensitive).
let route (path : string) =
    fun (ctx : HttpHandlerContext) ->
        if (getPath ctx.HttpContext).Equals path
        then Some ctx
        else None
        |> async.Return

/// Filters an incoming HTTP request based on the request path (case sensitive).
/// The arguments from the format string will be automatically resolved when the
/// route matches and subsequently passed into the supplied routeHandler.
let routef (path : StringFormat<_, 'T>) (routeHandler : 'T -> HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        tryMatchInput path (getPath ctx.HttpContext) false
        |> function
            | None      -> async.Return None
            | Some args -> routeHandler args ctx

/// Filters an incoming HTTP request based on the request path (case insensitive).
let routeCi (path : string) =
    fun (ctx : HttpHandlerContext) ->
        if String.Equals(getPath ctx.HttpContext, path, StringComparison.CurrentCultureIgnoreCase)
        then Some ctx
        else None
        |> async.Return

/// Filters an incoming HTTP request based on the request path (case insensitive).
/// The arguments from the format string will be automatically resolved when the
/// route matches and subsequently passed into the supplied routeHandler.
let routeCif (path : StringFormat<_, 'T>) (routeHandler : 'T -> HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        tryMatchInput path (getPath ctx.HttpContext) true
        |> function
            | None      -> async.Return None
            | Some args -> routeHandler args ctx

/// Filters an incoming HTTP request based on the beginning of the request path (case sensitive).
let routeStartsWith (subPath : string) =
    fun (ctx : HttpHandlerContext) ->
        if (getPath ctx.HttpContext).StartsWith subPath 
        then Some ctx
        else None
        |> async.Return

/// Filters an incoming HTTP request based on the beginning of the request path (case insensitive).
let routeStartsWithCi (subPath : string) =
    fun (ctx : HttpHandlerContext) ->
        if (getPath ctx.HttpContext).StartsWith(subPath, StringComparison.CurrentCultureIgnoreCase) 
        then Some ctx
        else None
        |> async.Return

/// Filters an incoming HTTP request based on a part of the request path (case sensitive).
/// Subsequent route handlers inside the given handler function should omit the already validated path.
let subRoute (path : string) (handler : HttpHandler) =
    routeStartsWith path >=>
    handlerWithRootedPath path handler

/// Filters an incoming HTTP request based on a part of the request path (case insensitive).
/// Subsequent route handlers inside the given handler function should omit the already validated path.
let subRouteCi (path : string) (handler : HttpHandler) =
    routeStartsWithCi path >=>
    handlerWithRootedPath path handler


/// Sets the HTTP response status code.
let setStatusCode (statusCode : int) =
    fun (ctx : HttpHandlerContext) ->
        async {
            ctx.HttpContext.Response.StatusCode <- statusCode
            return Some ctx
        }

/// Sets a HTTP header in the HTTP response.
let setHttpHeader (key : string) (value : obj) =
    fun (ctx : HttpHandlerContext) ->
        async {
            ctx.HttpContext.Response.Headers.[key] <- StringValues(value.ToString())
            return Some ctx
        }

/// Writes to the body of the HTTP response and sets the HTTP header Content-Length accordingly.
let setBody (bytes : byte array) =
    fun (ctx : HttpHandlerContext) ->
        async {            
            ctx.HttpContext.Response.Headers.["Content-Length"] <- StringValues(bytes.Length.ToString())
            ctx.HttpContext.Response.Body.WriteAsync(bytes, 0, bytes.Length)
            |> Async.AwaitTask
            |> ignore
            return Some ctx
        }

/// Writes a string to the body of the HTTP response and sets the HTTP header Content-Length accordingly.
let setBodyAsString (str : string) =
    Encoding.UTF8.GetBytes str
    |> setBody

/// Writes a string to the body of the HTTP response.
/// It also sets the HTTP header Content-Type: text/plain and sets the Content-Length header accordingly.
let text (str : string) =
    setHttpHeader "Content-Type" "text/plain"
    >=> setBodyAsString str

/// Serializes an object to JSON and writes it to the body of the HTTP response.
/// It also sets the HTTP header Content-Type: application/json and sets the Content-Length header accordingly.
let json (dataObj : obj) =
    setHttpHeader "Content-Type" "application/json"
    >=> setBodyAsString (serializeJson dataObj)

/// Serializes an object to XML and writes it to the body of the HTTP response.
/// It also sets the HTTP header Content-Type: application/xml and sets the Content-Length header accordingly.
let xml (dataObj : obj) =
    setHttpHeader "Content-Type" "application/xml"
    >=> setBody (serializeXml dataObj)

/// Reads a HTML file from disk and writes its contents to the body of the HTTP response
/// with a Content-Type of text/html.
let htmlFile (relativeFilePath : string) =
    fun (ctx : HttpHandlerContext) ->
        async {
            let env = ctx.Services.GetService<IHostingEnvironment>()
            let filePath = env.ContentRootPath + relativeFilePath
            let! html = readFileAsString filePath
            return!
                ctx
                |> (setHttpHeader "Content-Type" "text/html"
                >=> setBodyAsString html)
        }

/// Renders a model and a template with the DotLiquid template engine and sets the HTTP response
/// with the compiled output as well as the Content-Type HTTP header to the given value.
let dotLiquid (contentType : string) (template : string) (model : obj) =
    let view = Template.Parse template
    setHttpHeader "Content-Type" contentType
    >=> (model
        |> Hash.FromAnonymousObject
        |> view.Render
        |> setBodyAsString)

/// Reads a dotLiquid template file from disk and compiles it with the given model and sets
/// the compiled output as well as the given contentType as the HTTP reponse.
let dotLiquidTemplate (contentType : string) (templatePath : string) (model : obj) = 
    fun (ctx : HttpHandlerContext) ->
        async {
            let env = ctx.Services.GetService<IHostingEnvironment>()
            let templatePath = env.ContentRootPath + templatePath
            let! template = readFileAsString templatePath
            return! dotLiquid contentType template model ctx
        }

/// Reads a dotLiquid template file from disk and compiles it with the given model and sets
/// the compiled output as the HTTP reponse with a Content-Type of text/html.
let dotLiquidHtmlView (templatePath : string) (model : obj) =
    dotLiquidTemplate "text/html" templatePath model

/// Reads a razor view from disk and compiles it with the given model and sets
/// the compiled output as the HTTP reponse with the given contentType.
let razorView (contentType : string) (viewName : string) (model : 'T) =
    fun (ctx : HttpHandlerContext) ->
        async {
            let engine = ctx.Services.GetService<IRazorViewEngine>()
            let tempDataProvider = ctx.Services.GetService<ITempDataProvider>()
            let! result = renderRazorView engine tempDataProvider ctx.HttpContext viewName model
            match result with
            | Error msg -> return (failwith msg)
            | Ok output ->
                return!
                    ctx
                    |> (setHttpHeader "Content-Type" contentType
                    >=> setBodyAsString output)
        }

/// Reads a razor view from disk and compiles it with the given model and sets
/// the compiled output as the HTTP reponse with a Content-Type of text/html.
let razorHtmlView (viewName : string) (model : 'T) =
    razorView "text/html" viewName model

/// Uses the Giraffe.HtmlEngine to compile and render a HTML Document from
/// a given HtmlNode. The HTTP response is of Content-Type text/html.
let renderHtml (document: HtmlNode) =
    fun (ctx : HttpHandlerContext) ->
        let htmlHandler =
            document
            |> renderHtmlDocument
            |> setBodyAsString
        ctx |> (setHttpHeader "Content-Type" "text/html" >=> htmlHandler)

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
                  (responseObj         : obj) =
    fun (ctx : HttpHandlerContext) ->
        (ctx.HttpContext.Request.GetTypedHeaders()).Accept
        |> fun acceptedMimeTypes ->
            match isNull acceptedMimeTypes || acceptedMimeTypes.Count = 0 with
            | true  ->
                negotiationRules.Keys
                |> Seq.head
                |> fun mediaType -> negotiationRules.[mediaType]
                |> fun handler   -> handler responseObj ctx
            | false ->
                List.ofSeq acceptedMimeTypes
                |> List.filter (fun x -> negotiationRules.ContainsKey x.MediaType)
                |> fun mimeTypes ->
                    match mimeTypes.Length with
                    | 0 -> unacceptableHandler
                    | _ ->
                        mimeTypes
                        |> List.sortByDescending (fun x -> if x.Quality.HasValue then x.Quality.Value else 1.0)
                        |> List.head
                        |> fun mimeType -> negotiationRules.[mimeType.MediaType]
                        |> fun handler  -> handler responseObj
                <| ctx

/// Same as negotiateWith except that it specifies a default set of negotiation rules
/// and a default unacceptableHandler.
///
/// The supported media types are:
/// */*              -> serializes object to JSON
/// application/json -> serializes object to JSON
/// application/xml  -> serializes object to XML
/// text/xml         -> serializes object to XML
/// text/plain       -> returns object's ToString() result
let negotiate (responseObj : obj) =
    negotiateWith
        // Default negotiation rules
        (dict [
            "*/*"             , json
            "application/json", json
            "application/xml" , xml
            "text/xml"        , xml
            "text/plain"      , fun x -> x.ToString() |> text
        ])
        // Default unacceptable HttpHandler
        (fun (ctx : HttpHandlerContext) ->
            setStatusCode 406
            >=> ((ctx.HttpContext.Request.Headers.["Accept"]).ToString()
                |> sprintf "%s is unacceptable by the server."
                |> text)
            <| ctx)
        // response object
        responseObj

///Redirects to a different location with a 302 or 301 (when permanent) HTTP status code.
let redirectTo (permanent : bool) (location : string)  =
    fun (ctx:HttpHandlerContext) -> 
        ctx.HttpContext.Response.Redirect(location, permanent)
        ctx |> Some |> async.Return