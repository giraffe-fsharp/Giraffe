module Giraffe.HttpHandlerTests

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
open Giraffe.HttpHandlers
open Giraffe.Middleware
open Giraffe.HtmlEngine

// ---------------------------------
// Helper functions
// ---------------------------------

let getStatusCode (ctx : HttpHandlerContext) =
    ctx.HttpContext.Response.StatusCode

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

let initNewContext() =
    let ctx      = Substitute.For<HttpContext>()
    let services = Substitute.For<IServiceProvider>()
    let logger   = Substitute.For<ILogger>()
    let handlerCtx =
        {
            HttpContext = ctx
            Services    = services
            Logger      = logger
        }
    ctx, handlerCtx

// ---------------------------------
// Test Types
// ---------------------------------

type Dummy =
    {
        Foo : string
        Bar : string
        Age : int
    }

[<CLIMutable>]
type Person =
    {
        FirstName : string
        LastName  : string
        BirthDate : DateTime
        Height    : float
        Piercings : string[]
    }

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``GET "/" returns "Hello World"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [ 
            route "/"    >=> text "Hello World"
            route "/foo" >=> text "bar"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Hello World"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/foo" returns "bar"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [ 
            route "/"    >=> text "Hello World"
            route "/foo" >=> text "bar"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "bar"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/FOO" returns 404 "Not found"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [ 
            route "/"    >=> text "Hello World"
            route "/foo" >=> text "bar"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/FOO")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Not found"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal(404, ctx.HttpContext.Response.StatusCode)

[<Fact>]
let ``GET "/json" returns json object`` () =
    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [ 
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/json" >=> json { Foo = "john"; Bar = "doe"; Age = 30 }
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/json")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "{\"Foo\":\"john\",\"Bar\":\"doe\",\"Age\":30}"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``POST "/post/1" returns "1"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        choose [
            GET >=> choose [ 
                route "/"     >=> text "Hello World"
                route "/foo"  >=> text "bar" ]
            POST >=> choose [
                route "/post/1" >=> text "1"
                route "/post/2" >=> text "2" ]
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/post/1")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "1"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``POST "/post/2" returns "2"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        choose [
            GET >=> choose [ 
                route "/"     >=> text "Hello World"
                route "/foo"  >=> text "bar" ]
            POST >=> choose [
                route "/post/1" >=> text "1"
                route "/post/2" >=> text "2" ]
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/post/2")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "2"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``PUT "/post/2" returns 404 "Not found"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        choose [
            GET >=> choose [ 
                route "/"     >=> text "Hello World"
                route "/foo"  >=> text "bar" ]
            POST >=> choose [
                route "/post/1" >=> text "1"
                route "/post/2" >=> text "2" ]
            setStatusCode 404 >=> text "Not found" ]
    
    ctx.Request.Method.ReturnsForAnyArgs "PUT" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/post/2")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Not found"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal(404, ctx.HttpContext.Response.StatusCode)

[<Fact>]
let ``GET "/dotLiquid" returns rendered html view`` () =
    let ctx, hctx = initNewContext()
    let dotLiquidTemplate =
        "<html><head><title>DotLiquid</title></head>" + 
        "<body><p>{{ foo }} {{ bar }} is {{ age }} years old.</p>" +
        "</body></html>"

    let obj = { Foo = "John"; Bar = "Doe"; Age = 30 }

    let app = 
        choose [
            GET >=> choose [ 
                route "/"          >=> text "Hello World"
                route "/dotLiquid" >=> dotLiquid "text/html" dotLiquidTemplate obj ]
            POST >=> choose [
                route "/post/1"    >=> text "1" ]
            setStatusCode 404      >=> text "Not found" ]
    
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/dotLiquid")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "<html><head><title>DotLiquid</title></head><body><p>John Doe is 30 years old.</p></body></html>"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("text/html", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``POST "/text" with supported Accept header returns "good"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        choose [
            GET >=> choose [ 
                route "/"     >=> text "Hello World"
                route "/foo"  >=> text "bar" ]
            POST >=> choose [
                route "/text"   >=> mustAccept [ "text/plain" ] >=> text "text"
                route "/json"   >=> mustAccept [ "application/json" ] >=> json "json"
                route "/either" >=> mustAccept [ "text/plain"; "application/json" ] >=> text "either" ]
            setStatusCode 404 >=> text "Not found" ]
    
    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("text/plain"))
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/text")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "text"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("text/plain", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``POST "/json" with supported Accept header returns "json"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        choose [
            GET >=> choose [ 
                route "/"     >=> text "Hello World"
                route "/foo"  >=> text "bar" ]
            POST >=> choose [
                route "/text"   >=> mustAccept [ "text/plain" ] >=> text "text"
                route "/json"   >=> mustAccept [ "application/json" ] >=> json "json"
                route "/either" >=> mustAccept [ "text/plain"; "application/json" ] >=> text "either" ]
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/json"))
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/json")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "\"json\""

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("application/json", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``POST "/either" with supported Accept header returns "either"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        choose [
            GET >=> choose [ 
                route "/"     >=> text "Hello World"
                route "/foo"  >=> text "bar" ]
            POST >=> choose [
                route "/text"   >=> mustAccept [ "text/plain" ] >=> text "text"
                route "/json"   >=> mustAccept [ "application/json" ] >=> json "json"
                route "/either" >=> mustAccept [ "text/plain"; "application/json" ] >=> text "either" ]
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/json"))
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/either")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "either"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("text/plain", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``POST "/either" with unsupported Accept header returns 404 "Not found"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        choose [
            GET >=> choose [ 
                route "/"     >=> text "Hello World"
                route "/foo"  >=> text "bar" ]
            POST >=> choose [
                route "/text"   >=> mustAccept [ "text/plain" ] >=> text "text"
                route "/json"   >=> mustAccept [ "application/json" ] >=> json "json"
                route "/either" >=> mustAccept [ "text/plain"; "application/json" ] >=> text "either" ]
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/xml"))
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/either")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Not found"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal(404, ctx.HttpContext.Response.StatusCode)

[<Fact>]
let ``GET "/JSON" returns "BaR"`` () =
    let ctx, hctx = initNewContext()
    let app =
        GET >=> choose [ 
            route   "/"       >=> text "Hello World"
            route   "/foo"    >=> text "bar"
            route   "/json"   >=> json { Foo = "john"; Bar = "doe"; Age = 30 }
            routeCi "/json"   >=> text "BaR"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/JSON")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "BaR"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/foo/blah blah/bar" returns "blah blah"`` () =
    let ctx, hctx = initNewContext()
    let app =
        GET >=> choose [ 
            route   "/"       >=> text "Hello World"
            route   "/foo"    >=> text "bar"
            routef "/foo/%s/bar" text
            routef "/foo/%s/%i" (fun (name, age) -> text (sprintf "Name: %s, Age: %d" name age))
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo/blah blah/bar")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "blah%20blah"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/foo/johndoe/59" returns "Name: johndoe, Age: 59"`` () =
    let ctx, hctx = initNewContext()
    let app =
        GET >=> choose [ 
            route   "/"       >=> text "Hello World"
            route   "/foo"    >=> text "bar"
            routef "/foo/%s/bar" text
            routef "/foo/%s/%i" (fun (name, age) -> text (sprintf "Name: %s, Age: %d" name age))
            setStatusCode 404 >=> text "Not found" ]
    
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo/johndoe/59")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Name: johndoe, Age: 59"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``POST "/POsT/1" returns "1"`` () =
    let ctx, hctx = initNewContext()
    let app =
        choose [
            GET >=> choose [ 
                route "/"          >=> text "Hello World" ]
            POST >=> choose [
                route    "/post/1" >=> text "1"
                routeCif "/post/%i" json ]
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/POsT/1")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "1"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``POST "/POsT/523" returns "523"`` () =
    let ctx, hctx = initNewContext()
    let app =
        choose [
            GET >=> choose [ 
                route "/"          >=> text "Hello World" ]
            POST >=> choose [
                route    "/post/1" >=> text "1"
                routeCif "/post/%i" json ]
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/POsT/523")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "523"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/api" returns "api root"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"    >=> text "Hello World"
            route "/foo" >=> text "bar"
            subRoute "/api" (
                choose [
                    route ""       >=> text "api root"
                    route "/admin" >=> text "admin"
                    route "/users" >=> text "users" ] )
            route "/api/test" >=> text "test"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "api root"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/api/users" returns "users"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"    >=> text "Hello World"
            route "/foo" >=> text "bar"
            subRoute "/api" (
                choose [
                    route ""       >=> text "api root"
                    route "/admin" >=> text "admin"
                    route "/users" >=> text "users" ] )
            route "/api/test" >=> text "test"
            setStatusCode 404 >=> text "Not found" ]
    
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api/users")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "users"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/api/test" returns "test"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"    >=> text "Hello World"
            route "/foo" >=> text "bar"
            subRoute "/api" (
                choose [
                    route ""       >=> text "api root"
                    route "/admin" >=> text "admin"
                    route "/users" >=> text "users" ] )
            route "/api/test" >=> text "test"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api/test")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "test"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/api/v2/users" returns "users v2"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"    >=> text "Hello World"
            route "/foo" >=> text "bar"
            subRoute "/api" (
                choose [
                    route ""       >=> text "api root"
                    route "/admin" >=> text "admin"
                    route "/users" >=> text "users"
                    subRoute "/v2" (
                        choose [
                            route ""       >=> text "api root v2"
                            route "/admin" >=> text "admin v2"
                            route "/users" >=> text "users v2"
                        ]
                    )
                ]
            )
            route "/api/test" >=> text "test"
            setStatusCode 404 >=> text "Not found" ]
    
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api/v2/users")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "users v2"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/api/foo/bar/yadayada" returns "yadayada"`` () =
    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"    >=> text "Hello World"
            route "/foo" >=> text "bar"
            subRoute "/api" (
                choose [
                    route  "" >=> text "api root"
                    routef "/foo/bar/%s" text ] )
            route "/api/test" >=> text "test"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api/foo/bar/yadayada")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "yadayada"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``GET "/person" returns rendered HTML view`` () =
    let ctx, hctx = initNewContext()
    let personView model =
        html [] [
            head [] [
                title [] (encodedText "Html Node")
            ]
            body [] [
                p [] (sprintf "%s %s is %i years old." model.Foo model.Bar model.Age |> encodedText)
            ]
        ]

    let johnDoe = { Foo = "John"; Bar = "Doe"; Age = 30 }

    let app = 
        choose [
            GET >=> choose [ 
                route "/"          >=> text "Hello World"
                route "/person"    >=> (personView johnDoe |> renderHtml) ]
            POST >=> choose [
                route "/post/1"    >=> text "1" ]
            setStatusCode 404      >=> text "Not found" ]
    
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/person")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "<!DOCTYPE html><html><head><title>Html Node</title></head><body><p>John Doe is 30 years old.</p></body></html>"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = (getBody ctx).Replace(Environment.NewLine, String.Empty)
        Assert.Equal(expected, body)
        Assert.Equal("text/html", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``Get "/flex" with Accept header of "application/json" returns JSON object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "left ear"; "nose" |]
        }

    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/flex" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/json"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/flex")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "{\"FirstName\":\"John\",\"LastName\":\"Doe\",\"BirthDate\":\"1990-07-12T00:00:00\",\"Height\":1.85,\"Piercings\":[\"left ear\",\"nose\"]}"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("application/json", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``Get "/flex" with Accept header of "application/xml; q=0.9, application/json" returns JSON object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "left ear"; "nose" |]
        }

    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/flex" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/xml; q=0.9, application/json"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/flex")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "{\"FirstName\":\"John\",\"LastName\":\"Doe\",\"BirthDate\":\"1990-07-12T00:00:00\",\"Height\":1.85,\"Piercings\":[\"left ear\",\"nose\"]}"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("application/json", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``Get "/flex" with Accept header of "application/xml" returns XML object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/flex" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/xml"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/flex")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Person xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <FirstName>John</FirstName>
  <LastName>Doe</LastName>
  <BirthDate>1990-07-12T00:00:00</BirthDate>
  <Height>1.85</Height>
  <Piercings>
    <string>ear</string>
    <string>nose</string>
  </Piercings>
</Person>"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("application/xml", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``Get "/flex" with Accept header of "application/xml, application/json" returns XML object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/flex" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/xml, application/json"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/flex")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Person xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <FirstName>John</FirstName>
  <LastName>Doe</LastName>
  <BirthDate>1990-07-12T00:00:00</BirthDate>
  <Height>1.85</Height>
  <Piercings>
    <string>ear</string>
    <string>nose</string>
  </Piercings>
</Person>"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("application/xml", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``Get "/flex" with Accept header of "application/json, application/xml" returns JSON object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/flex" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/json, application/xml"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/flex")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "{\"FirstName\":\"John\",\"LastName\":\"Doe\",\"BirthDate\":\"1990-07-12T00:00:00\",\"Height\":1.85,\"Piercings\":[\"ear\",\"nose\"]}"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("application/json", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``Get "/flex" with Accept header of "application/json; q=0.5, application/xml" returns XML object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/flex" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/json; q=0.5, application/xml"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/flex")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Person xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <FirstName>John</FirstName>
  <LastName>Doe</LastName>
  <BirthDate>1990-07-12T00:00:00</BirthDate>
  <Height>1.85</Height>
  <Piercings>
    <string>ear</string>
    <string>nose</string>
  </Piercings>
</Person>"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("application/xml", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``Get "/flex" with Accept header of "application/json; q=0.5, application/xml; q=0.6" returns XML object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/flex" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/json; q=0.5, application/xml; q=0.6"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/flex")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Person xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <FirstName>John</FirstName>
  <LastName>Doe</LastName>
  <BirthDate>1990-07-12T00:00:00</BirthDate>
  <Height>1.85</Height>
  <Piercings>
    <string>ear</string>
    <string>nose</string>
  </Piercings>
</Person>"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("application/xml", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``Get "/flex" with Accept header of "text/html" returns a 406 response`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/flex" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("text/html"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/flex")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "text/html is unacceptable by the server."

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(406, getStatusCode ctx)
        Assert.Equal(expected, body)
        Assert.Equal("text/plain", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``Get "/flex" without an Accept header returns a JSON object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx, hctx = initNewContext()
    let app = 
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/flex" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/flex")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "{\"FirstName\":\"John\",\"LastName\":\"Doe\",\"BirthDate\":\"1990-07-12T00:00:00\",\"Height\":1.85,\"Piercings\":[\"ear\",\"nose\"]}"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
        Assert.Equal("application/json", ctx.HttpContext.Response |> getContentType)

[<Fact>]
let ``Warbler function should execute inner function`` () =
    let inner (a: int) = a.ToString()
    let warbled = warbler (fun (a:int) -> inner)
    let result = warbled 42
    Assert.Equal("42", result)