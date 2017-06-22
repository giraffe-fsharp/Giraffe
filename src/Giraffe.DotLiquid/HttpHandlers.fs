module Giraffe.DotLiquid.HttpHandlers

open System.IO
open System.Text
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Primitives
open DotLiquid

/// Renders a model and a template with the DotLiquid template engine and sets the HTTP response
/// with the compiled output as well as the Content-Type HTTP header to the given value.
let dotLiquid (contentType : string) (template : string) (model : obj) =
    let view = Template.Parse template
    fun (ctx : HttpContext) ->
        async {
            let bytes =
                model
                |> Hash.FromAnonymousObject
                |> view.Render
                |> Encoding.UTF8.GetBytes
            ctx.Response.Headers.["Content-Type"] <- StringValues contentType
            ctx.Response.Headers.["Content-Length"] <- bytes.Length |> string |> StringValues
            do! ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length) |> Async.AwaitTask
            return Some ctx
        }

/// Reads a dotLiquid template file from disk and compiles it with the given model and sets
/// the compiled output as well as the given contentType as the HTTP reponse.
let dotLiquidTemplate (contentType : string) (templatePath : string) (model : obj) =
    fun (ctx : HttpContext) ->
        async {
            let env = ctx.RequestServices.GetService<IHostingEnvironment>()
            let templatePath = env.ContentRootPath + templatePath
            use stream = new FileStream(templatePath, FileMode.Open)
            use reader = new StreamReader(stream)
            let! template = reader.ReadToEndAsync() |> Async.AwaitTask
            return! dotLiquid contentType template model ctx
        }

/// Reads a dotLiquid template file from disk and compiles it with the given model and sets
/// the compiled output as the HTTP reponse with a Content-Type of text/html.
let dotLiquidHtmlView (templatePath : string) (model : obj) =
    dotLiquidTemplate "text/html" templatePath model