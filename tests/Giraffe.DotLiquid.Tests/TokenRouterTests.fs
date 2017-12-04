module Giraffe.DotLiquid.TokenRouterTests

open System.IO
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Xunit
open NSubstitute
open Giraffe
open DotLiquid
open TokenRouter

// ---------------------------------
// Helper functions
// ---------------------------------

let getStatusCode (ctx : HttpContext) =
    ctx.Response.StatusCode

let getBody (ctx : HttpContext) =
    ctx.Response.Body.Position <- 0L
    use reader = new StreamReader(ctx.Response.Body, Encoding.UTF8)
    reader.ReadToEnd()

let getContentType (response : HttpResponse) =
    response.Headers.["Content-Type"].[0]

let assertFail msg = Assert.True(false, msg)

let assertFailf format args =
    let msg = sprintf format args
    Assert.True(false, msg)

let notFound = setStatusCode 404 >=> text "Not found"
let next : HttpFunc = Some >> Task.FromResult

// ---------------------------------
// Test Types
// ---------------------------------

type Dummy =
    {
        Foo : string
        Bar : string
        Age : int
    }

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``GET "/dotLiquid" returns rendered html view`` () =
    let ctx = Substitute.For<HttpContext>()
    let dotLiquidTemplate =
        "<html><head><title>DotLiquid</title></head>" +
        "<body><p>{{ foo }} {{ bar }} is {{ age }} years old.</p>" +
        "</body></html>"

    let obj = { Foo = "John"; Bar = "Doe"; Age = 30 }

    let app =
        router notFound [
            GET [
                route "/"          => text "Hello World"
                route "/dotLiquid" => dotLiquid "text/html" dotLiquidTemplate obj ]
            POST [
                route "/post/1"    => text "1" ]
            ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/dotLiquid")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "<html><head><title>DotLiquid</title></head><body><p>John Doe is 30 years old.</p></body></html>"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal("text/html", ctx.Response |> getContentType)
    }