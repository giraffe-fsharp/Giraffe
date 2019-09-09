module Giraffe.Tests.HttpContextExtensionsTests

open System
open System.IO
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Internal
open Microsoft.Extensions.Primitives
open FSharp.Control.Tasks.V2.ContextInsensitive
open Xunit
open NSubstitute
open Giraffe
open Giraffe.GiraffeViewEngine

[<Fact>]
let ``GetRequestUrl returns entire URL of the HTTP request`` () =
    let ctx = mockHttpContext Version40

    ctx.Request.Scheme.Returns("http") |> ignore
    ctx.Request.Host.Returns(HostString("example.org:81")) |> ignore
    ctx.Request.PathBase.Returns(PathString("/something")) |> ignore
    ctx.Request.Path.Returns(PathString("/hello")) |> ignore
    ctx.Request.QueryString.Returns(QueryString("?a=1&b=2")) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Response.Body <- new MemoryStream()

    let testHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            text (ctx.GetRequestUrl()) next ctx

    let app = route "/hello" >=> testHandler

    let expected = "http://example.org:81/something/hello?a=1&b=2"

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``TryGetRequestHeader during HTTP GET request with returns correct result`` () =
    let ctx = mockHttpContext Version40

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
    let ctx = mockHttpContext Version40

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
    let ctx = mockHttpContext Version40

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

[<Fact>]
let ``WriteHtmlFileAsync should return html from physical folder`` () =
    let ctx = mockHttpContext Version40

    let filePath =
        Path.Combine(
            Path.GetFullPath("TestFiles"),
            "index.html")

    let testHandler : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            ctx.WriteHtmlFileAsync filePath

    let app = route "/" >=> testHandler

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = File.ReadAllText filePath

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``WriteTextAsync with HTTP GET should return text in body`` () =
    let ctx = mockHttpContext Version40

    let testHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            ctx.WriteTextAsync "Hello World Giraffe"

    let app = route "/" >=> testHandler

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Hello World Giraffe"

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``WriteTextAsync with HTTP HEAD should not return text in body`` () =
    let ctx = mockHttpContext Version40

    let testHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            ctx.WriteTextAsync "Hello World Giraffe"

    let app = route "/" >=> testHandler

    ctx.Request.Method.ReturnsForAnyArgs "HEAD" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = ""

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }