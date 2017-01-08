module AspNetCore.Lambda.Tests

open System
open System.IO
open System.Text
open Xunit
open NSubstitute
open AspNetCore.Lambda.HttpHandlers
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting

// ---------------------------------
// Helper functions
// ---------------------------------

let getBody (ctx : HttpContext) =
    ctx.Response.Body.Position <- 0L
    use reader = new StreamReader(ctx.Response.Body, Encoding.UTF8)
    reader.ReadToEnd()

let assertFail msg = Assert.True(false, msg)

let assertFailf format args = 
    let msg = sprintf format args
    Assert.True(false, msg)

// ---------------------------------
// Mocks
// ---------------------------------

let env = Substitute.For<IHostingEnvironment>()
let ctx = Substitute.For<HttpContext>()

// ---------------------------------
// HttpHandler application
// ---------------------------------

type Dummy =
    {
        Foo : string
        Bar : string
        Age : int
    }

let dotLiquidTemplate = "<html><head><title>DotLiquid</title></head>" + 
                        "<body><p>{{ foo }} {{ bar }} is {{ age }} years old.</p>" +
                        "</body></html>"

let testApp =
    choose [
        GET >>=
            choose [
                route "/"           >>= text "Hello World"
                route "/foo"        >>= text "bar"
                route "/json"       >>= json { Foo = "john"; Bar = "doe"; Age = 30 }
                route "/dotLiquid"  >>= dotLiquid "text/html" dotLiquidTemplate { Foo = "John"; Bar = "Doe"; Age = 30 }
            ]
        POST >>=
            choose [
                route "/post/1" >>= text "1"
                route "/post/2" >>= text "2"
            ] 
        setStatusCode 404 >>= text "Not found" ] : HttpHandler

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``GET "/" returns "Hello World"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Hello World"

    let result = 
        (env, ctx)
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some (_, ctx) ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/foo" returns "bar"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "bar"

    let result = 
        (env, ctx)
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some (_, ctx) ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/FOO" returns 404 "Not found"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/FOO")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Not found"

    let result = 
        (env, ctx)
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some (_, ctx) ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal(404, ctx.Response.StatusCode)

[<Fact>]
let ``GET "/json" returns json object`` () =
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/json")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "{\"Foo\":\"john\",\"Bar\":\"doe\",\"Age\":30}"

    let result = 
        (env, ctx)
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some (_, ctx) ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``POST "/post/1" returns "1"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/post/1")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "1"

    let result = 
        (env, ctx)
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some (_, ctx) ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``POST "/post/2" returns "2"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/post/2")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "2"

    let result = 
        (env, ctx)
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some (_, ctx) ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``POST "/post/3" returns 404 "Not found"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/post/3")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Not found"

    let result = 
        (env, ctx)
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some (_, ctx) ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal(404, ctx.Response.StatusCode)

[<Fact>]
let ``GET "/dotLiquid" returns rendered html view`` () =
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/dotLiquid")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "<html><head><title>DotLiquid</title></head><body><p>John Doe is 30 years old.</p></body></html>"

    let result = 
        (env, ctx)
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some (_, ctx) ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        // Assert.Equal("text/html", ctx.Response.)