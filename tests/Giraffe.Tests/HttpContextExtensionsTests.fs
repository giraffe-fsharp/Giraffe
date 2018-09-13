module Giraffe.Tests.HttpContextExtensionsTests

open System
open System.IO
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http.Internal
open Microsoft.Extensions.Primitives
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open FSharp.Control.Tasks.V2.ContextInsensitive
open Xunit
open NSubstitute
open Giraffe
open Giraffe.GiraffeViewEngine

[<Fact>]
let ``TryGetRequestHeader during HTTP GET request with returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let testHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            (match ctx.TryGetRequestHeader "X-Test" with
            | Some value -> text value
            | None       -> setStatusCode 400 >=> text "Bad Request"
            ) next ctx

    let app = route "/test" >=> testHandler

    let headers = HeaderDictionary()
    headers.Add("X-Test", StringValues("It works!"))
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/test")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "It works!"

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``TryGetQueryStringValue during HTTP GET request with query string returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let testHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            (match ctx.TryGetQueryStringValue "BirthDate" with
            | Some value -> text value
            | None       -> setStatusCode 400 >=> text "Bad Request"
            ) next ctx

    let app = route "/test" >=> testHandler

    let queryStr = "?Name=John%20Doe&IsVip=true&BirthDate=1990-04-20&Balance=150000.5&LoyaltyPoints=137"
    let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/test")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "1990-04-20"

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``WriteHtmlViewAsync should add html to the context`` () =
    let ctx = Substitute.For<HttpContext>()

    let testHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let htmlDoc =
                html [] [
                    head [] []
                    body [] [
                        h1 [] [ Text "Hello world" ]
                    ]
                ]
            ctx.WriteHtmlViewAsync(htmlDoc)

    let app = route "/" >=> testHandler

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = sprintf "<!DOCTYPE html>%s<html><head></head><body><h1>Hello world</h1></body></html>" Environment.NewLine

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

let resultOfTask<'T> (task:Task<'T>) =
    task.Result

[<Fact>]
let ``WriteHtmlFileAsync should return html from content folder`` () =
    let testHandler : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            ctx.WriteHtmlFileAsync "index.html"

    let webApp = route "/" >=> testHandler

    let configureApp (app : IApplicationBuilder) =
        app
           .UseStaticFiles()
           .UseGiraffe webApp

    let host =
        WebHostBuilder()
            .UseContentRoot(Path.GetFullPath("TestFiles"))
            .Configure(Action<IApplicationBuilder> configureApp)

    use server = new TestServer(host)
    use client = server.CreateClient()

    let expectedContent =
        Path.Combine("TestFiles", "index.html")
        |> File.ReadAllText

    let actualContent =
        client.GetStringAsync "/"
        |> resultOfTask

    Assert.Equal(expectedContent, actualContent)