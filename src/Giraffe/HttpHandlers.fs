module Giraffe.HttpHandlers

open System
open System.Text
open System.Collections.Generic
open System.Threading.Tasks
open System.Text.RegularExpressions
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open FSharp.Core.Printf
open Newtonsoft.Json.Linq
open Giraffe.Tasks
open Giraffe.Common
open Giraffe.FormatExpressions
open Giraffe.XmlViewEngine

type HttpFuncResult = Task<HttpContext option>
type HttpFunc       = HttpContext -> HttpFuncResult
type HttpHandler    = HttpFunc  -> HttpFunc
type ErrorHandler   = exn -> ILogger -> HttpHandler

/// ---------------------------
/// Globally useful functions
/// ---------------------------

let inline warbler f (next : HttpFunc) (ctx : HttpContext) = f (next, ctx) next ctx

let private abort  : HttpFuncResult = Task.FromResult None
let private finish : HttpFunc       = Some >> Task.FromResult

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
    | Some p when ctx.Request.Path.Value.Contains p -> ctx.Request.Path.Value.[p.Length..]
    | _   -> ctx.Request.Path.Value
    
let private handlerWithRootedPath (path : string) (handler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let savedSubPath = getSavedSubPath ctx
            ctx.Items.Item RouteKey <- ((savedSubPath |> Option.defaultValue "") + path)
            let! result = handler next ctx
            match result with
            | Some _ -> ()
            | None ->
                match savedSubPath with
                | Some savedSubPath -> ctx.Items.Item   RouteKey <- savedSubPath
                | None              -> ctx.Items.Remove RouteKey |> ignore
            return result
        }

/// ---------------------------
/// Default HttpHandlers
/// ---------------------------

/// Combines two HttpHandler functions into one.
let compose (handler1 : HttpHandler) (handler2 : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) ->
        let func = next |> handler2 |> handler1
        fun (ctx : HttpContext) ->
            match ctx.Response.HasStarted with
            | true  -> next ctx
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

/// Filters an incoming HTTP request based on the HTTP verb
let httpVerb (verb : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if ctx.Request.Method.Equals verb
        then next ctx
        else abort

let GET    : HttpHandler = httpVerb "GET"
let POST   : HttpHandler = httpVerb "POST"
let PUT    : HttpHandler = httpVerb "PUT"
let PATCH  : HttpHandler = httpVerb "PATCH"
let DELETE : HttpHandler = httpVerb "DELETE"

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

/// Challenges the client to authenticate with a given authentication scheme.
let challenge (authScheme : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            do! ctx.ChallengeAsync authScheme
            return! next ctx
        }

/// Signs off the current user.
let signOff (authScheme : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            do! ctx.SignOutAsync authScheme
            return! next ctx
        }

/// Validates if a user is authenticated.
/// If not it will proceed with the authFailedHandler.
let requiresAuthentication (authFailedHandler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if isNotNull ctx.User && ctx.User.Identity.IsAuthenticated
        then next ctx
        else authFailedHandler finish ctx

/// Validates if a user is in a specific role.
/// If not it will proceed with the authFailedHandler.
let requiresRole (role : string) (authFailedHandler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if ctx.User.IsInRole role
        then next ctx
        else authFailedHandler finish ctx

/// Validates if a user has at least one of the specified roles.
/// If not it will proceed with the authFailedHandler.
let requiresRoleOf (roles : string list) (authFailedHandler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        roles
        |> List.exists ctx.User.IsInRole
        |> function
            | true  -> next ctx
            | false -> authFailedHandler finish ctx

/// Attempts to clear the current HttpResponse object.
/// This can be useful inside an error handler when the response
/// needs to be overwritten in the case of a failure.
let clearResponse : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.Response.Clear()
        next ctx

/// Filters an incoming HTTP request based on the request path (case sensitive).
let route (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (getPath ctx).Equals path
        then next ctx
        else abort

/// Filters an incoming HTTP request based on the request path (case sensitive).
/// The arguments from the format string will be automatically resolved when the
/// route matches and subsequently passed into the supplied routeHandler.
let routef (path : StringFormat<_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path (getPath ctx) false
        |> function
            | None      -> abort
            | Some args -> routeHandler args next ctx

/// Filters an incoming HTTP request based on the request path (case insensitive).
let routeCi (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if String.Equals(getPath ctx, path, StringComparison.CurrentCultureIgnoreCase)
        then next ctx
        else abort

/// Filters an incoming HTTP request based on the request path (case insensitive).
/// The arguments from the format string will be automatically resolved when the
/// route matches and subsequently passed into the supplied routeHandler.
let routeCif (path : StringFormat<_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path (getPath ctx) true
        |> function
            | None      -> abort
            | Some args -> routeHandler args next ctx

/// Filters an incoming HTTP request based on the request path (case insensitive).
/// The parameters from the string will be used to create an instance of 'T
/// and subsequently passed into the supplied routeHandler.
let routeBind<'T> (route: string) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let pattern = route.Replace("{", "(?<").Replace("}", ">.+)") |> sprintf "^%s$"
        let regex = Regex(pattern, RegexOptions.IgnoreCase)
        let mtch = regex.Match ctx.Request.Path.Value
        match mtch.Success with
        | true ->
            let groups = mtch.Groups
            let o =
                regex.GetGroupNames()
                |> Array.skip 1
                |> Array.map (fun x -> x, groups.[x].Value)
                |> Array.filter (fun (_, x) -> x.Length > 0)
                |> dict
                |> JObject.FromObject
                |> fun jo -> jo.ToObject<'T>()
            routeHandler o next ctx
        | _ -> abort

/// Filters an incoming HTTP request based on the beginning of the request path (case sensitive).
let routeStartsWith (subPath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (getPath ctx).StartsWith subPath
        then next ctx
        else abort

/// Filters an incoming HTTP request based on the beginning of the request path (case insensitive).
let routeStartsWithCi (subPath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (getPath ctx).StartsWith(subPath, StringComparison.CurrentCultureIgnoreCase)
        then next ctx
        else abort

/// Filters an incoming HTTP request based on a part of the request path (case sensitive).
/// Subsequent route handlers inside the given handler function should omit the already validated path.
let subRoute (path : string) (handler : HttpHandler) : HttpHandler =
    routeStartsWith path >=>
    handlerWithRootedPath path handler

/// Filters an incoming HTTP request based on a part of the request path (case insensitive).
/// Subsequent route handlers inside the given handler function should omit the already validated path.
let subRouteCi (path : string) (handler : HttpHandler) : HttpHandler =
    routeStartsWithCi path >=>
    handlerWithRootedPath path handler

/// Sets the HTTP response status code.
let setStatusCode (statusCode : int) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.Response.StatusCode <- statusCode
        next ctx

/// Sets a HTTP header in the HTTP response.
let setHttpHeader (key : string) (value : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.Response.Headers.[key] <- StringValues(value.ToString())
        next ctx

/// Writes to the body of the HTTP response and sets the HTTP header Content-Length accordingly.
let setBody (bytes : byte array) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            ctx.Response.Headers.["Content-Length"] <- StringValues(bytes.Length.ToString())
            do! ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length)
            return Some ctx
        }

/// Writes a string to the body of the HTTP response and sets the HTTP header Content-Length accordingly.
let setBodyAsString (str : string) : HttpHandler =
    Encoding.UTF8.GetBytes str
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
let xml (dataObj : obj) : HttpHandler =
    setHttpHeader "Content-Type" "application/xml"
    >=> setBody (serializeXml dataObj)

/// Reads a HTML file from disk and writes its contents to the body of the HTTP response
/// with a Content-Type of text/html.
let htmlFile (relativeFilePath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let env = ctx.GetService<IHostingEnvironment>()
            let filePath = env.ContentRootPath + relativeFilePath
            let! html = readFileAsString filePath
            return!
                (setHttpHeader "Content-Type" "text/html"
                >=> setBodyAsString html) next ctx
        }

/// Uses the Giraffe.XmlViewEngine to compile and render a HTML Document from
/// an given XmlNode. The HTTP response is of Content-Type text/html.
let renderHtml (document : XmlNode) : HttpHandler =
    setHttpHeader "Content-Type" "text/html"
    >=> (document
        |> renderHtmlDocument
        |> setBodyAsString)

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
        (ctx.Request.GetTypedHeaders()).Accept
        |> fun acceptedMimeTypes ->
            match isNull acceptedMimeTypes || acceptedMimeTypes.Count = 0 with
            | true  ->
                negotiationRules.Keys
                |> Seq.head
                |> fun mediaType -> negotiationRules.[mediaType]
                |> fun handler   -> handler responseObj next ctx
            | false ->
                List.ofSeq acceptedMimeTypes
                |> List.filter (fun x -> negotiationRules.ContainsKey x.MediaType.Value)
                |> fun mimeTypes ->
                    match mimeTypes.Length with
                    | 0 -> unacceptableHandler next ctx
                    | _ ->
                        mimeTypes
                        |> List.sortByDescending (fun x -> if x.Quality.HasValue then x.Quality.Value else 1.0)
                        |> List.head
                        |> fun mimeType -> negotiationRules.[mimeType.MediaType.Value]
                        |> fun handler  -> handler responseObj next ctx

/// Same as negotiateWith except that it specifies a default set of negotiation rules
/// and a default unacceptableHandler.
///
/// The supported media types are:
/// */*              -> serializes object to JSON
/// application/json -> serializes object to JSON
/// application/xml  -> serializes object to XML
/// text/xml         -> serializes object to XML
/// text/plain       -> returns object's ToString() result
let negotiate (responseObj : obj) : HttpHandler =
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
        (fun (next : HttpFunc) (ctx : HttpContext) ->
            (setStatusCode 406
            >=> ((ctx.Request.Headers.["Accept"]).ToString()
                |> sprintf "%s is unacceptable by the server."
                |> text)) next ctx)
        // response object
        responseObj

///Redirects to a different location with a 302 or 301 (when permanent) HTTP status code.
let redirectTo (permanent : bool) (location : string) : HttpHandler  =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.Response.Redirect(location, permanent)
        Task.FromResult (Some ctx)
