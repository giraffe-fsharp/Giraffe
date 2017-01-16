module AspNetCore.Lambda.HttpHandlers

open System
open System.Text
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open FSharp.Core.Printf
open Newtonsoft.Json
open DotLiquid
open AspNetCore.Lambda.Common
open AspNetCore.Lambda.FormatExpressions

type HttpHandlerContext =
    {
        HttpContext  : HttpContext
        Environment  : IHostingEnvironment
        Logger       : ILogger
    }

type HttpHandler = HttpHandlerContext -> Async<HttpHandlerContext option>

type ErrorHandler = exn -> HttpHandler

let bind (handler : HttpHandler) (handler2 : HttpHandler) =
    fun ctx ->
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
    fun ctx ->
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
    fun ctx ->
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
    fun ctx ->
        let headers = ctx.HttpContext.Request.GetTypedHeaders()
        headers.Accept
        |> Seq.map    (fun h -> h.ToString())
        |> Seq.exists (fun h -> mimeTypes |> Seq.contains h)
        |> function
            | true  -> Some ctx
            | false -> None
            |> async.Return

let route (path : string) =
    fun ctx ->
        if ctx.HttpContext.Request.Path.ToString().Equals path 
        then Some ctx
        else None
        |> async.Return

let routef (route : StringFormat<_, 'T>) (routeHandler : 'T -> HttpHandler) =
    fun ctx ->
        tryMatchInput route (ctx.HttpContext.Request.Path.ToString()) false
        |> function
            | None      -> async.Return None
            | Some args -> routeHandler args ctx

let routeCi (path : string) =
    fun ctx ->
        if String.Equals(ctx.HttpContext.Request.Path.ToString(), path, StringComparison.CurrentCultureIgnoreCase)
        then Some ctx
        else None
        |> async.Return

let routeCif (route : StringFormat<_, 'T>) (routeHandler : 'T -> HttpHandler) =
    fun ctx ->
        tryMatchInput route (ctx.HttpContext.Request.Path.ToString()) true
        |> function
            | None      -> None |> async.Return
            | Some args -> routeHandler args ctx

let routeStartsWith (partOfPath : string) =
    fun ctx ->
        if ctx.HttpContext.Request.Path.ToString().StartsWith partOfPath 
        then Some ctx
        else None
        |> async.Return

let routeStartsWithCi (partOfPath : string) =
    fun ctx ->
        if ctx.HttpContext.Request.Path.ToString().StartsWith(partOfPath, StringComparison.CurrentCultureIgnoreCase) 
        then Some ctx
        else None
        |> async.Return

let setStatusCode (statusCode : int) =
    fun ctx ->
        async {
            ctx.HttpContext.Response.StatusCode <- statusCode
            return Some ctx
        }

let setHttpHeader (key : string) (value : obj) =
    fun ctx ->
        async {
            ctx.HttpContext.Response.Headers.[key] <- new StringValues(value.ToString())
            return Some ctx
        }

let setBody (bytes : byte array) =
    fun ctx ->
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
    fun ctx ->
        async {
            let templatePath = ctx.Environment.ContentRootPath + relativeTemplatePath
            let! template = readFileAsString templatePath
            return! dotLiquid "text/html" template model ctx
        }

let htmlFile (relativeFilePath : string) =
    fun ctx ->
        async {
            let filePath = ctx.Environment.ContentRootPath + relativeFilePath
            let! html = readFileAsString filePath
            return!
                ctx
                |> (setHttpHeader "Content-Type" "text/html"
                >>= setBodyAsString html)
        }