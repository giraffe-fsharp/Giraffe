module Giraffe.HttpHandlers

open System
open System.Text
open System.Threading.Tasks
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
open Giraffe.HttpContextExtensions
open Giraffe.RazorEngine
open Giraffe.HtmlEngine
open Giraffe.AsyncTask

//type HttpContext = HttpContext 
//Async Handlers
//type HttpHandlerResultAsync = Task<HttpContext>

type Continuation = HttpContext -> Task<HttpContext>

//result of any handler
type HttpHandler = Continuation -> Continuation -> HttpContext -> Task<HttpContext>

/// Combines two HttpHandler functions into one.
let compose (a : HttpHandler) (b : HttpHandler) =
    fun (succ : Continuation) (fail : Continuation) (ctx:HttpContext) ->
        let childCont = b succ fail
        let parentCont = a childCont fail
        parentCont ctx

let (>=>) = compose

type ErrorHandler = exn -> ILogger -> HttpHandler

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

let private handlerWithRootedPath (path : string) = 
    fun succ fail ctx ->
            let savedSubPath = getSavedSubPath ctx
            try
                ctx.Items.Item RouteKey <- ((savedSubPath |> Option.defaultValue "") + path)
                succ ctx
            finally
                match savedSubPath with
                | Some savedSubPath -> ctx.Items.Item RouteKey <- savedSubPath
                | None              -> ctx.Items.Remove RouteKey |> ignore

/// ---------------------------
/// Default HttpHandlers
/// ---------------------------

/// Adapts a HttpHandler function to accept a HttpHandlerResult.
/// If the HttpHandlerResult returns Some HttpContext, then it will proceed
/// to the handler, otherwise short circuit and return None as the result.
/// If the response has already been written in the resulting HttpContext,
/// then it will skip the HttpHandler as well.


//hdlr -> ctx -> hndlr -> tresult
/// Iterates through a list of HttpHandler functions and returns the
/// result of the first HttpHandler which outcome is Some HttpContext
let rec choose (handlers : HttpHandler list) =
    fun (succ: Continuation) (fail : Continuation) (ctx : HttpContext) ->
            match handlers with
            | [] -> fail ctx
            | handler :: tail ->
                let next = choose tail succ fail //if a branch fails, go to next handler in list
                handler succ next ctx


/// Filters an incoming HTTP request based on the HTTP verb
let httpVerb (verb : string) : HttpHandler =
    fun succ fail ctx ->
        if ctx.Request.Method.Equals verb
        then succ ctx
        else fail ctx        

let GET    : HttpHandler = httpVerb "GET"
let POST   : HttpHandler = httpVerb "POST"
let PUT    : HttpHandler = httpVerb "PUT"
let PATCH  : HttpHandler = httpVerb "PATCH"
let DELETE : HttpHandler = httpVerb "DELETE"

/// Filters an incoming HTTP request based on the accepted
/// mime types of the client.
let mustAccept (mimeTypes : string list) : HttpHandler =
    fun succ fail ctx ->
        let headers = ctx.Request.GetTypedHeaders()
        headers.Accept
        |> Seq.map    (fun h -> h.ToString())
        |> Seq.exists (fun h -> mimeTypes |> Seq.contains h)
        |> function
            | true  -> succ ctx
            | false -> fail ctx

/// Challenges the client to authenticate with a given authentication scheme.
let challenge (authScheme : string) :HttpHandler =
    fun succ fail ctx ->
        task {
            let auth = ctx.Authentication
            do! auth.ChallengeAsync authScheme
            return! succ ctx
        }

/// Signs off the current user.
let signOff (authScheme : string) : HttpHandler =
    fun succ fail ctx ->
        task {
            let auth = ctx.Authentication
            do! auth.SignOutAsync authScheme
            return! succ ctx
        }

/// Validates if a user is authenticated.
/// If not it will proceed with the authFailedHandler.
let requiresAuthentication (authFailedHandler : HttpHandler) : HttpHandler =
    fun succ fail ctx ->
        let user = ctx.User
        if isNotNull user && user.Identity.IsAuthenticated
        then succ ctx
        else authFailedHandler succ fail ctx

/// Validates if a user is in a specific role.
/// If not it will proceed with the authFailedHandler.
let requiresRole (role : string) (authFailedHandler : HttpHandler) : HttpHandler =
    fun succ fail ctx ->
        let user = ctx.User
        if user.IsInRole role
        then succ ctx
        else authFailedHandler succ fail ctx

/// Validates if a user has at least one of the specified roles.
/// If not it will proceed with the authFailedHandler.
let requiresRoleOf (roles : string list) (authFailedHandler : HttpHandler) : HttpHandler =
    fun succ fail ctx ->
        let user = ctx.User
        roles
        |> List.exists user.IsInRole 
        |> function
            | true  -> succ ctx
            | false -> authFailedHandler succ fail ctx

/// Attempts to clear the current HttpResponse object.
/// This can be useful inside an error handler when the response
/// needs to be overwritten in the case of a failure.
let clearResponse : HttpHandler =
    fun succ fail ctx ->
        ctx.Response.Clear()
        succ ctx

/// Filters an incoming HTTP request based on the request path (case sensitive).
let route (path : string) =
    fun (succ : Continuation) (fail : Continuation)  (ctx : HttpContext) ->
        if (getPath ctx).Equals path
        then succ ctx
        else fail ctx

/// Filters an incoming HTTP request based on the request path (case sensitive).
/// The arguments from the format string will be automatically resolved when the
/// route matches and subsequently passed into the supplied routeHandler.
let routef (path : StringFormat<_, 'T>) (routeHandler : 'T -> HttpHandler)  : HttpHandler =
    fun succ fail ctx ->
        tryMatchInput path (getPath ctx) false
        |> function
            | None      -> fail ctx 
            | Some args -> (routeHandler args) succ fail ctx

/// Filters an incoming HTTP request based on the request path (case insensitive).
let routeCi (path : string) =
    fun succ fail ctx ->
        if String.Equals(getPath ctx, path, StringComparison.CurrentCultureIgnoreCase)
        then succ ctx
        else fail ctx

/// Filters an incoming HTTP request based on the request path (case insensitive).
/// The arguments from the format string will be automatically resolved when the
/// route matches and subsequently passed into the supplied routeHandler.
let routeCif (path : StringFormat<_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    fun succ fail ctx ->
        tryMatchInput path (getPath ctx) true
        |> function
            | None      -> fail ctx
            | Some args -> routeHandler args succ fail ctx

/// Filters an incoming HTTP request based on the beginning of the request path (case sensitive).
let routeStartsWith (subPath : string) =
    fun succ fail ctx ->
        if (getPath ctx).StartsWith subPath 
        then succ ctx
        else fail ctx

/// Filters an incoming HTTP request based on the beginning of the request path (case insensitive).
let routeStartsWithCi (subPath : string) : HttpHandler =
    fun succ fail ctx ->
        if (getPath ctx).StartsWith(subPath, StringComparison.CurrentCultureIgnoreCase) 
        then succ ctx
        else fail ctx

/// Filters an incoming HTTP request based on a part of the request path (case sensitive).
/// Subsequent route handlers inside the given handler function should omit the already validated path.
let subRoute (path : string) : HttpHandler =
        routeStartsWith path >=>
        handlerWithRootedPath path

/// Filters an incoming HTTP request based on a part of the request path (case insensitive).
/// Subsequent route handlers inside the given handler function should omit the already validated path.
let subRouteCi (path : string) : HttpHandler =
    routeStartsWithCi path >=>
    handlerWithRootedPath path


/// Sets the HTTP response status code.
let setStatusCode (statusCode : int) : HttpHandler =
    fun succ fail ctx ->
        ctx.Response.StatusCode <- statusCode
        succ ctx

/// Sets a HTTP header in the HTTP response.
let setHttpHeader (key : string) (value : obj) : HttpHandler =
    fun succ fail ctx ->
        ctx.Response.Headers.[key] <- StringValues(value.ToString())
        succ ctx

/// Writes to the body of the HTTP response and sets the HTTP header Content-Length accordingly.
let setBody (bytes : byte array) : HttpHandler =
    fun succ fail ctx ->
        task {            
            ctx.Response.Headers.["Content-Length"] <- StringValues(bytes.Length.ToString())
            do! ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length)
            return! succ ctx
        }

/// Writes a string to the body of the HTTP response and sets the HTTP header Content-Length accordingly.
let setBodyAsString (str : string) : HttpHandler =
        (Encoding.UTF8.GetBytes str)
        |> setBody

/// Writes a string to the body of the HTTP response.
/// It also sets the HTTP header Content-Type: text/plain and sets the Content-Length header accordingly.
let text (str : string) : HttpHandler =
    setHttpHeader "Content-Type" "text/plain"
    >=> setBodyAsString str

/// Serializes an object to JSON and writes it to the body of the HTTP response.
/// It also sets the HTTP header Content-Type: application/json and sets the Content-Length header accordingly.
let json (dataObj : obj) : HttpHandler =
    setHttpHeader "Content-Type" "application/json"
    >=> setBodyAsString (serializeJson dataObj)

/// Serializes an object to XML and writes it to the body of the HTTP response.
/// It also sets the HTTP header Content-Type: application/xml and sets the Content-Length header accordingly.
let xml (dataObj : obj) : HttpHandler=
    setHttpHeader "Content-Type" "application/xml"
    >=> setBody (serializeXml dataObj)

/// Reads a HTML file from disk and writes its contents to the body of the HTTP response
/// with a Content-Type of text/html.
let htmlFile (relativeFilePath : string) : HttpHandler =
    fun succ fail ctx ->
        task {
            let env = ctx.GetService<IHostingEnvironment>()
            let filePath = env.ContentRootPath + relativeFilePath
            let! html = readFileAsString filePath
            return!
                (setHttpHeader "Content-Type" "text/html"
                >=> setBodyAsString html) succ fail ctx
        }

/// Renders a model and a template with the DotLiquid template engine and sets the HTTP response
/// with the compiled output as well as the Content-Type HTTP header to the given value.
let dotLiquid (contentType : string) (template : string) (model : obj)  : HttpHandler =
    let view = Template.Parse template
    setHttpHeader "Content-Type" contentType
    >=> (model
        |> Hash.FromAnonymousObject
        |> view.Render
        |> setBodyAsString)

/// Reads a dotLiquid template file from disk and compiles it with the given model and sets
/// the compiled output as well as the given contentType as the HTTP reponse.
let dotLiquidTemplate (contentType : string) (templatePath : string) (model : obj) : HttpHandler = 
    fun succ fail ctx ->
        task {
            let env = ctx.GetService<IHostingEnvironment>()
            let templatePath = env.ContentRootPath + templatePath
            let! template = readFileAsString templatePath
            return! dotLiquid contentType template model
        }

/// Reads a dotLiquid template file from disk and compiles it with the given model and sets
/// the compiled output as the HTTP reponse with a Content-Type of text/html.
let dotLiquidHtmlView (templatePath : string) (model : obj)  : HttpHandler =
    dotLiquidTemplate "text/html" templatePath model

/// Reads a razor view from disk and compiles it with the given model and sets
/// the compiled output as the HTTP reponse with the given contentType.
let razorView (contentType : string) (viewName : string) (model : 'T)  : HttpHandler =
    fun succ fail ctx ->
        task {
            let engine = ctx.GetService<IRazorViewEngine>()
            let tempDataProvider = ctx.GetService<ITempDataProvider>()
            let! result = renderRazorView engine tempDataProvider ctx viewName model
            match result with
            | Error msg -> return (failwith msg)
            | Ok output ->
                return! (setHttpHeader "Content-Type" contentType >=> setBodyAsString output) 
        }

/// Reads a razor view from disk and compiles it with the given model and sets
/// the compiled output as the HTTP reponse with a Content-Type of text/html.
let razorHtmlView (viewName : string) (model : 'T) : HttpHandler =
    razorView "text/html" viewName model

/// Uses the Giraffe.HtmlEngine to compile and render a HTML Document from
/// a given HtmlNode. The HTTP response is of Content-Type text/html.
let renderHtml (document: HtmlNode) : HttpHandler =
    let htmlDoc = document |> renderHtmlDocument
    (setHttpHeader "Content-Type" "text/html" >=> setBodyAsString htmlDoc)

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
                  (responseObj         : obj) : HttpHandler =
    fun succ fail ctx ->
        (ctx.Request.GetTypedHeaders()).Accept
        |> fun acceptedMimeTypes ->
            match isNull acceptedMimeTypes || acceptedMimeTypes.Count = 0 with
            | true  ->
                negotiationRules.Keys
                |> Seq.head
                |> fun mediaType -> negotiationRules.[mediaType]
                |> fun handler   -> handler responseObj 
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
    let defaultHandler : HttpHandler = 
        fun succ fail ctx ->
            let msg = (ctx.Request.Headers.["Accept"]).ToString() |> sprintf "%s is unacceptable by the server."
            text msg

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
        (setStatusCode 406 >=> defaultHandler)
        // response object
        responseObj

///Redirects to a different location with a 302 or 301 (when permanent) HTTP status code.
let redirectTo (permanent : bool) (location : string) : HttpHandler =
    fun succ fail ctx -> 
        ctx.Response.Redirect(location, permanent)
        succ ctx