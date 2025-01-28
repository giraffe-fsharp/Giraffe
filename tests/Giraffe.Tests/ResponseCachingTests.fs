module Giraffe.Tests.ResponseCachingTests

open System
open System.IO
open Microsoft.AspNetCore.Http
open Microsoft.Net.Http.Headers
open Microsoft.Extensions.Primitives
open Giraffe
open NSubstitute
open Xunit

// ---------------------------------
// Request caching tests
// ---------------------------------

[<Fact>]
let ``responseCaching is updating 'vary' response header`` () =
    let ctx = Substitute.For<HttpContext>()

    let responseCachingMiddleware: HttpHandler =
        responseCaching
            (Public(TimeSpan.FromSeconds(float 30)))
            (Some "Accept, Accept-Encoding")
            (Some [| "query1"; "query2" |])

    let app =
        GET
        >=> route "/ok"
        >=> responseCachingMiddleware
        >=> setStatusCode 200
        >=> text "ok"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/ok") |> ignore
    ctx.Response.Headers.ReturnsForAnyArgs(new HeaderDictionary()) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Non expected result"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode)

            let expectedVaryHeader = StringValues [| "Accept, Accept-Encoding" |] |> string
            Assert.Equal(string ctx.Response.Headers.[HeaderNames.Vary], expectedVaryHeader)

            let expectedBody = "ok"
            Assert.Equal(expectedBody, getBody ctx)
    }
