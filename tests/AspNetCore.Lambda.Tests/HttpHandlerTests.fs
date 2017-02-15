module AspNetCore.Lambda.HttpHandlerTests

open System
open System.Collections.Generic
open System.IO
open System.Text
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Xunit
open NSubstitute
open AspNetCore.Lambda.HttpHandlers

// ---------------------------------
// Helper functions
// ---------------------------------

let getBody (ctx : HttpHandlerContext) =
    ctx.HttpContext.Response.Body.Position <- 0L
    use reader = new StreamReader(ctx.HttpContext.Response.Body, Encoding.UTF8)
    reader.ReadToEnd()

let getContentType (response : HttpResponse) =
    response.Headers.["Content-Type"].[0]

let assertFail msg = Assert.True(false, msg)

let assertFailf format args = 
    let msg = sprintf format args
    Assert.True(false, msg)

// ---------------------------------
// Mocks
// ---------------------------------

let ctx      = Substitute.For<HttpContext>()
let services = Substitute.For<IServiceProvider>()

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
                routeCi "/json"     >>= text "BaR"
                routef "/foo/%s/bar" text
                routef "/foo/%s/%i" (fun (name, age) -> text (sprintf "Name: %s, Age: %d" name age))
            ]
        POST >>=
            choose [
                route "/post/1" >>= text "1"
                route "/post/2" >>= text "2"
                route "/text"   >>= mustAccept [ "text/plain" ] >>= text "text"
                route "/json"   >>= mustAccept [ "application/json" ] >>= json "json"
                route "/either" >>= mustAccept [ "text/plain"; "application/json" ] >>= text "either"
                routeCif "/post/%i" json
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
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/foo" returns "bar"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "bar"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/FOO" returns 404 "Not found"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/FOO")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Not found"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal(404, ctx.HttpContext.Response.StatusCode)

[<Fact>]
let ``GET "/json" returns json object`` () =
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/json")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "{\"Foo\":\"john\",\"Bar\":\"doe\",\"Age\":30}"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``POST "/post/1" returns "1"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/post/1")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "1"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``POST "/post/2" returns "2"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/post/2")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "2"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``PUT "/post/2" returns 404 "Not found"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "PUT" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/post/2")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Not found"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal(404, ctx.HttpContext.Response.StatusCode)

[<Fact>]
let ``GET "/dotLiquid" returns rendered html view`` () =
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/dotLiquid")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "<html><head><title>DotLiquid</title></head><body><p>John Doe is 30 years old.</p></body></html>"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("text/html", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``POST "/text" with supported Accept header returns "good"`` () =
    let headers = new HeaderDictionary()
    headers.Add("Accept", new StringValues("text/plain"))
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/text")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "text"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("text/plain", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``POST "/json" with supported Accept header returns "json"`` () =
    let headers = new HeaderDictionary()
    headers.Add("Accept", new StringValues("application/json"))
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/json")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "\"json\""

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("application/json", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``POST "/either" with supported Accept header returns "either"`` () =
    let headers = new HeaderDictionary()
    headers.Add("Accept", new StringValues("application/json"))
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/either")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "either"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("text/plain", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``POST "/either" with unsupported Accept header returns 404 "Not found"`` () =
    let headers = new HeaderDictionary()
    headers.Add("Accept", new StringValues("application/xml"))
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/either")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Not found"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal(404, ctx.HttpContext.Response.StatusCode)

[<Fact>]
let ``GET "/JSON" returns "BaR"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/JSON")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "BaR"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/foo/blah blah/bar" returns "blah blah"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo/blah blah/bar")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "blah%20blah"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/foo/johndoe/59" returns "Name: johndoe, Age: 59"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo/johndoe/59")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Name: johndoe, Age: 59"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``POST "/POsT/1" returns "1"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/POsT/1")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "1"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``POST "/POsT/523" returns "523"`` () =
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/POsT/523")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "523"

    let result = 
        { HttpContext = ctx; Services = services }
        |> testApp
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/api" returns "api root"`` () =
    let app = 
        GET >>= choose [
            route "/"    >>= text "Hello World"
            route "/foo" >>= text "bar"
            subPath "/api" (
                choose [
                    route ""       >>= text "api root"
                    route "/admin" >>= text "admin"
                    route "/users" >>= text "users" ] )
            route "/api/test" >>= text "test"
            setStatusCode 404 >>= text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "api root"

    let result = 
        { HttpContext = ctx; Services = services }
        |> app
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/api/users" returns "users"`` () =
    let app = 
        GET >>= choose [
            route "/"    >>= text "Hello World"
            route "/foo" >>= text "bar"
            subPath "/api" (
                choose [
                    route ""       >>= text "api root"
                    route "/admin" >>= text "admin"
                    route "/users" >>= text "users" ] )
            route "/api/test" >>= text "test"
            setStatusCode 404 >>= text "Not found" ]
    
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api/users")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "users"

    let result = 
        { HttpContext = ctx; Services = services }
        |> app
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/api/test" returns "test"`` () =
    let app = 
        GET >>= choose [
            route "/"    >>= text "Hello World"
            route "/foo" >>= text "bar"
            subPath "/api" (
                choose [
                    route ""       >>= text "api root"
                    route "/admin" >>= text "admin"
                    route "/users" >>= text "users" ] )
            route "/api/test" >>= text "test"
            setStatusCode 404 >>= text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api/test")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "test"

    let result = 
        { HttpContext = ctx; Services = services }
        |> app
        |> Async.RunSynchronously

    match result with
    | None          -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)