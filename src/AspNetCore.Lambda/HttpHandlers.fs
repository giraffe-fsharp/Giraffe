module AspNetCore.Lambda.HttpHandlers

open System
open System.Text
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open FSharp.Core.Printf
open Newtonsoft.Json
open DotLiquid
open AspNetCore.Lambda.Common
open AspNetCore.Lambda.FormatExpressions

type HttpHandlerContext =
    {
        HttpContext : HttpContext
        Services    : IServiceProvider
    }

type HttpHandler = HttpHandlerContext -> Async<HttpHandlerContext option>

type ErrorHandler = exn -> HttpHandler

let bind (handler : HttpHandler) (handler2 : HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        async {
            let! result = handler ctx
            match result with
            | None      -> return None
            | Some ctx2 ->
                match ctx2.HttpContext.Response.HasStarted with
                | true  -> return  Some ctx2
                | false -> return! handler2 ctx2
        }

let (>>=) = bind

let rec choose (handlers : HttpHandler list) =
    fun (ctx : HttpHandlerContext) ->
        async {
            match handlers with
            | []                -> return None
            | handler :: tail   ->
                let! result = handler ctx
                match result with
                | Some c    -> return Some c
                | None      -> return! choose tail ctx
        }

let httpVerb (verb : string) =
    fun (ctx : HttpHandlerContext) ->
        if ctx.HttpContext.Request.Method.Equals verb
        then Some ctx
        else None
        |> async.Return

let GET     = httpVerb "GET"    : HttpHandler
let POST    = httpVerb "POST"   : HttpHandler
let PUT     = httpVerb "PUT"    : HttpHandler
let PATCH   = httpVerb "PATCH"  : HttpHandler
let DELETE  = httpVerb "DELETE" : HttpHandler

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

let challenge (authScheme : string) =
    fun (ctx : HttpHandlerContext) ->
        async {
            let auth = ctx.HttpContext.Authentication
            do! auth.ChallengeAsync authScheme |> Async.AwaitTask
            return Some ctx
        }

let signOff (authScheme : string) =
    fun (ctx : HttpHandlerContext) ->
        async {
            let auth = ctx.HttpContext.Authentication
            do! auth.SignOutAsync authScheme |> Async.AwaitTask
            return Some ctx
        }

let requiresAuthentication (authFailedHandler : HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        let user = ctx.HttpContext.User
        if isNotNull user && user.Identity.IsAuthenticated
        then async.Return (Some ctx)
        else authFailedHandler ctx

let requiresRole (role : string) (authFailedHandler : HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        let user = ctx.HttpContext.User
        if user.IsInRole role
        then async.Return (Some ctx)
        else authFailedHandler ctx

let requiresRoleOf (roles : string list) (authFailedHandler : HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        let user = ctx.HttpContext.User
        roles
        |> List.exists user.IsInRole 
        |> function
            | true  -> async.Return (Some ctx)
            | false -> authFailedHandler ctx

let clearResponse =
    fun (ctx : HttpHandlerContext) ->
        ctx.HttpContext.Response.Clear()
        async.Return (Some ctx)

let route (path : string) =
    fun (ctx : HttpHandlerContext) ->
        if ctx.HttpContext.Request.Path.ToString().Equals path 
        then Some ctx
        else None
        |> async.Return

let routef (route : StringFormat<_, 'T>) (routeHandler : 'T -> HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        tryMatchInput route (ctx.HttpContext.Request.Path.ToString()) false
        |> function
            | None      -> async.Return None
            | Some args -> routeHandler args ctx

let routeCi (path : string) =
    fun (ctx : HttpHandlerContext) ->
        if String.Equals(ctx.HttpContext.Request.Path.ToString(), path, StringComparison.CurrentCultureIgnoreCase)
        then Some ctx
        else None
        |> async.Return

let routeCif (route : StringFormat<_, 'T>) (routeHandler : 'T -> HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        tryMatchInput route (ctx.HttpContext.Request.Path.ToString()) true
        |> function
            | None      -> None |> async.Return
            | Some args -> routeHandler args ctx

let routeStartsWith (partOfPath : string) =
    fun (ctx : HttpHandlerContext) ->
        if ctx.HttpContext.Request.Path.ToString().StartsWith partOfPath 
        then Some ctx
        else None
        |> async.Return

let routeStartsWithCi (partOfPath : string) =
    fun (ctx : HttpHandlerContext) ->
        if ctx.HttpContext.Request.Path.ToString().StartsWith(partOfPath, StringComparison.CurrentCultureIgnoreCase) 
        then Some ctx
        else None
        |> async.Return

let setStatusCode (statusCode : int) =
    fun (ctx : HttpHandlerContext) ->
        async {
            ctx.HttpContext.Response.StatusCode <- statusCode
            return Some ctx
        }

let setHttpHeader (key : string) (value : obj) =
    fun (ctx : HttpHandlerContext) ->
        async {
            ctx.HttpContext.Response.Headers.[key] <- new StringValues(value.ToString())
            return Some ctx
        }

let setBody (bytes : byte array) =
    fun (ctx : HttpHandlerContext) ->
        async {            
            ctx.HttpContext.Response.Headers.["Content-Length"] <- new StringValues(bytes.Length.ToString())
            ctx.HttpContext.Response.Body.WriteAsync(bytes, 0, bytes.Length)
            |> Async.AwaitTask
            |> ignore
            return Some ctx
        }

let setBodyAsString (str : string) =
    Encoding.UTF8.GetBytes str
    |> setBody

let text (str : string) =
    setHttpHeader "Content-Type" "text/plain"
    >>= setBodyAsString str

let json (dataObj : obj) =
    setHttpHeader "Content-Type" "application/json"
    >>= setBodyAsString (JsonConvert.SerializeObject dataObj)

let dotLiquid (contentType : string) (template : string) (model : obj) =
    let view = Template.Parse template
    setHttpHeader "Content-Type" contentType
    >>= (model
        |> Hash.FromAnonymousObject
        |> view.Render
        |> setBodyAsString)

let htmlTemplate (relativeTemplatePath : string) (model : obj) = 
    fun (ctx : HttpHandlerContext) ->
        async {
            let env = ctx.Services.GetService<IHostingEnvironment>()
            let templatePath = env.ContentRootPath + relativeTemplatePath
            let! template = readFileAsString templatePath
            return! dotLiquid "text/html" template model ctx
        }

let htmlFile (relativeFilePath : string) =
    fun (ctx : HttpHandlerContext) ->
        async {
            let env = ctx.Services.GetService<IHostingEnvironment>()
            let filePath = env.ContentRootPath + relativeFilePath
            let! html = readFileAsString filePath
            return!
                ctx
                |> (setHttpHeader "Content-Type" "text/html"
                >>= setBodyAsString html)
        }