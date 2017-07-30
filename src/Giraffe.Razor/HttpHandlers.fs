module Giraffe.Razor.HttpHandlers

open System.Text
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc.Razor
open Microsoft.AspNetCore.Mvc.ViewFeatures
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Primitives
open Giraffe.HttpHandlers
open Giraffe.Razor.Engine

/// Reads a razor view from disk and compiles it with the given model and sets
/// the compiled output as the HTTP reponse with the given contentType.
let razorView (contentType : string) (viewName : string) (model : 'T) : HttpHandler =
    fun (next : HttpAction) (ctx : HttpContext) ->
        async {
            let engine = ctx.RequestServices.GetService<IRazorViewEngine>()
            let tempDataProvider = ctx.RequestServices.GetService<ITempDataProvider>()
            let! result = renderRazorView engine tempDataProvider ctx viewName model
            match result with
            | Error msg -> return (failwith msg)
            | Ok output ->
                let bytes = Encoding.UTF8.GetBytes output
                return! (setHttpHeader "Content-Type" contentType >=> setBody bytes) next ctx
        }

/// Reads a razor view from disk and compiles it with the given model and sets
/// the compiled output as the HTTP reponse with a Content-Type of text/html.
let razorHtmlView (viewName : string) (model : 'T) : HttpHandler =
    razorView "text/html" viewName model