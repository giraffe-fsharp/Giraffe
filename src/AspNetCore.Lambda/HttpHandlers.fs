module AspNetCore.Lambda.HttpHandlers

open System
open System.Text
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Newtonsoft.Json
open DotLiquid
open AspNetCore.Lambda.Common
open AspNetCore.Lambda.FormatExpressions

type WebContext  = IHostingEnvironment * HttpContext
type HttpHandler = WebContext -> Async<WebContext option>

let bind (handler : HttpHandler) (handler2 : HttpHandler) =
    fun wctx ->
        async {
            let! result = handler wctx
            match result with
            | None       -> return  None
            | Some wctx2 -> return! handler2 wctx2
        }

let (>>=) = bind

let rec choose (handlers : HttpHandler list) =
    fun wctx ->
        async {
            match handlers with
            | []                -> return None
            | handler :: tail   ->
                let! result = handler wctx
                match result with
                | Some c    -> return Some c
                | None      -> return! choose tail wctx
        }

let httpVerb (verb : string) =
    fun (env : IHostingEnvironment, ctx : HttpContext) ->
        if ctx.Request.Method.Equals verb
        then Some (env, ctx)
        else None
        |> async.Return

let GET     = httpVerb "GET"    : HttpHandler
let POST    = httpVerb "POST"   : HttpHandler
let PUT     = httpVerb "PUT"    : HttpHandler
let PATCH   = httpVerb "PATCH"  : HttpHandler
let DELETE  = httpVerb "DELETE" : HttpHandler

let mustAccept (mimeTypes : string list) =
    fun (env : IHostingEnvironment, ctx : HttpContext) ->
        let headers = ctx.Request.GetTypedHeaders()
        headers.Accept
        |> Seq.map    (fun h -> h.ToString())
        |> Seq.exists (fun h -> mimeTypes |> Seq.contains h)
        |> function
            | true  -> Some (env, ctx)
            | false -> None
            |> async.Return

let route (path : string) =
    fun (env : IHostingEnvironment, ctx : HttpContext) ->
        if ctx.Request.Path.ToString().Equals path 
        then Some (env, ctx)
        else None
        |> async.Return

let setStatusCode (statusCode : int) =
    fun (env : IHostingEnvironment, ctx : HttpContext) ->
        async {
            ctx.Response.StatusCode <- statusCode
            return Some (env, ctx)
        }

let setHttpHeader (key : string) (value : obj) =
    fun (env : IHostingEnvironment, ctx : HttpContext) ->
        async {
            ctx.Response.Headers.[key] <- new StringValues(value.ToString())
            return Some (env, ctx)
        }

let setBody (bytes : byte array) =
    fun (env : IHostingEnvironment, ctx : HttpContext) ->
        async {
            ctx.Response.Headers.["Content-Length"] <- new StringValues(bytes.Length.ToString())
            ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length)
            |> Async.AwaitTask
            |> ignore
            return Some (env, ctx)
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

let htmlTemplate (templatePath : string) (model : obj) = 
    fun wctx ->
        async {
            let! template = readFileAsString templatePath
            return! dotLiquid "text/html" template model wctx
        }

let htmlFile (relativeFilePath : string) =
    fun (env : IHostingEnvironment, ctx : HttpContext) ->
        async {
            let filePath = env.ContentRootPath + relativeFilePath
            let! html = readFileAsString filePath
            return!
                (env, ctx)
                |> (setHttpHeader "Content-Type" "text/html"
                >>= setBodyAsString html)
        }

// let routef (route : StringFormat<_, 'T>) (urlPath : string) (func : 'T -> unit) =
//     tryMatchInput route urlPath
//     |> function
//         | None -> None
//         | Some t -> t |> func |> Some