module Giraffe.DotLiquid.HttpHandlers

open System.IO
open System.Text
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Primitives
open DotLiquid
open Giraffe.Common
open Giraffe.HttpHandlers
open Giraffe.Tasks

/// Renders a model and a template with the DotLiquid template engine and sets the HTTP response
/// with the compiled output as well as the Content-Type HTTP header to the given value.
let dotLiquid (contentType : string) (template : string) (model : obj) : HttpHandler =
    let view  = Template.Parse template
    let bytes =
        model
        |> Hash.FromAnonymousObject
        |> view.Render
        |> Encoding.UTF8.GetBytes
    setHttpHeader "Content-Type" contentType
    >=> setBody bytes

/// Reads a dotLiquid template file from disk and compiles it with the given model and sets
/// the compiled output as well as the given contentType as the HTTP reponse.
let dotLiquidTemplate (contentType : string) (templatePath : string) (model : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let env = ctx.RequestServices.GetService<IHostingEnvironment>()
            let path = Path.Combine(env.ContentRootPath, templatePath)
            let! template = readFileAsStringAsync path
            return! dotLiquid contentType template model next ctx
        }

/// Reads a dotLiquid template file from disk and compiles it with the given model and sets
/// the compiled output as the HTTP reponse with a Content-Type of text/html.
let dotLiquidHtmlView (templatePath : string) (model : obj) : HttpHandler =
    dotLiquidTemplate "text/html" templatePath model