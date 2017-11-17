module Giraffe.HttpHandlerTests

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Xunit
open NSubstitute
open Giraffe.HttpHandlers
open Giraffe.Middleware
open Giraffe.XmlViewEngine
open Giraffe.DotLiquid.HttpHandlers
open Giraffe.Tests.Asserts
open Giraffe.Tasks
open Newtonsoft.Json

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

[<CLIMutable>]
type Person =
    {
        FirstName : string
        LastName  : string
        BirthDate : DateTime
        Height    : float
        Piercings : string[]
    }
    override this.ToString() =
        let nl = Environment.NewLine
        sprintf "First name: %s%sLast name: %s%sBirth date: %s%sHeight: %.2f%sPiercings: %A"
            this.FirstName nl
            this.LastName nl
            (this.BirthDate.ToString("yyyy-MM-dd")) nl
            this.Height nl
            this.Piercings

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``GET "/" returns "Hello World"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"    >=> text "Hello World"
            route "/foo" >=> text "bar"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Hello World"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``GET "/foo" returns "bar"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"    >=> text "Hello World"
            route "/foo" >=> text "bar"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "bar"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``GET "/FOO" returns 404 "Not found"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"    >=> text "Hello World"
            route "/foo" >=> text "bar"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/FOO")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Not found"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal(404, ctx.Response.StatusCode)
    }

[<Fact>]
let ``GET "/json" returns json object`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/json" >=> json { Foo = "john"; Bar = "doe"; Age = 30 }
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/json")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "{\"foo\":\"john\",\"bar\":\"doe\",\"age\":30}"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]

let ``GET "/json" with a custom json handler returns json object`` () =
    let customJson (dataObj : obj) : HttpHandler =
        let settings = JsonSerializerSettings()
        makeJsonHandler dataObj settings

    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/json" >=> customJson { Foo = "john"; Bar = "doe"; Age = 30 }
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/json")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "{\"Foo\":\"john\",\"Bar\":\"doe\",\"Age\":30}"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``POST "/post/1" returns "1"`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``POST "/post/2" returns "2"`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``PUT "/post/2" returns 404 "Not found"`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal(404, ctx.Response.StatusCode)
    }

[<Fact>]
let ``GET "/dotLiquid" returns rendered html view`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal("text/html", ctx.Response |> getContentType)
    }

[<Fact>]
let ``POST "/text" with supported Accept header returns "good"`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal("text/plain", ctx.Response |> getContentType)
    }

[<Fact>]
let ``POST "/json" with supported Accept header returns "json"`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal("application/json", ctx.Response |> getContentType)
    }

[<Fact>]
let ``POST "/either" with supported Accept header returns "either"`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal("text/plain", ctx.Response |> getContentType)
    }

[<Fact>]
let ``POST "/either" with unsupported Accept header returns 404 "Not found"`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal(404, ctx.Response.StatusCode)
    }

[<Fact>]
let ``GET "/JSON" returns "BaR"`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``GET "/foo/blah blah/bar" returns "blah blah"`` () =
    let ctx = Substitute.For<HttpContext>()
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
    let expected = "blah blah"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``GET "/foo/johndoe/59" returns "Name: johndoe, Age: 59"`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``POST "/POsT/1" returns "1"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        choose [
            GET >=> choose [
                route "/" >=> text "Hello World" ]
            POST >=> choose [
                route    "/post/1" >=> text "1"
                routeCif "/post/%i" json ]
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/POsT/1")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "1"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``POST "/POsT/523" returns "523"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        choose [
            GET >=> choose [
                route "/" >=> text "Hello World" ]
            POST >=> choose [
                route    "/post/1" >=> text "1"
                routeCif "/post/%i" json ]
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/POsT/523")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "523"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``Sub route with empty route`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``Sub route with non empty route`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``Route after sub route with same beginning of path`` () =

    task {
        let ctx = Substitute.For<HttpContext>()

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

        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``Nested sub routes`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``Route after nested sub routes with same beginning of path`` () =
    let ctx = Substitute.For<HttpContext>()
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
                    route "/yada" >=> text "yada"
                ]
            )
            route "/api/test"   >=> text "test"
            route "/api/v2/else" >=> text "else"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api/v2/else")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "else"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``Multiple nested sub routes`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"    >=> text "Hello World"
            route "/foo" >=> text "bar"
            subRoute "/api" (
                choose [
                    route "/users" >=> text "users"
                    subRoute "/v2" (
                        choose [
                            route "/admin" >=> text "admin v2"
                            route "/users" >=> text "users v2"
                        ]
                    )
                    subRoute "/v2" (
                        route "/admin2" >=> text "correct admin2"
                    )
                ]
            )
            route "/api/test"   >=> text "test"
            route "/api/v2/else" >=> text "else"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api/v2/admin2")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "correct admin2"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``GET "/api/foo/bar/yadayada" returns "yadayada"`` () =
    let ctx = Substitute.For<HttpContext>()
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

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``GET "/person" returns rendered HTML view`` () =
    let ctx = Substitute.For<HttpContext>()
    let personView model =
        html [] [
            head [] [
                title [] [ encodedText "Html Node" ]
            ]
            body [] [
                p [] [ sprintf "%s %s is %i years old." model.Foo model.Bar model.Age |> encodedText ]
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

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = (getBody ctx).Replace(Environment.NewLine, String.Empty)
            Assert.Equal(expected, body)
            Assert.Equal("text/html", ctx.Response |> getContentType)
    }

[<Fact>]
let ``Get "/auto" with Accept header of "application/json" returns JSON object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "left ear"; "nose" |]
        }

    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/auto" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/json"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "{\"firstName\":\"John\",\"lastName\":\"Doe\",\"birthDate\":\"1990-07-12T00:00:00\",\"height\":1.85,\"piercings\":[\"left ear\",\"nose\"]}"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal("application/json", ctx.Response |> getContentType)
    }

[<Fact>]
let ``Get "/auto" with Accept header of "application/xml; q=0.9, application/json" returns JSON object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "left ear"; "nose" |]
        }

    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/auto" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/xml; q=0.9, application/json"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "{\"firstName\":\"John\",\"lastName\":\"Doe\",\"birthDate\":\"1990-07-12T00:00:00\",\"height\":1.85,\"piercings\":[\"left ear\",\"nose\"]}"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal("application/json", ctx.Response |> getContentType)
    }

[<Fact>]
let ``Get "/auto" with Accept header of "application/xml" returns XML object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/auto" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/xml"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
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

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            XmlAssert.equals expected body
            Assert.Equal("application/xml", ctx.Response |> getContentType)
    }

[<Fact>]
let ``Get "/auto" with Accept header of "application/xml, application/json" returns XML object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/auto" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/xml, application/json"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
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

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            XmlAssert.equals expected body
            Assert.Equal("application/xml", ctx.Response |> getContentType)
    }

[<Fact>]
let ``Get "/auto" with Accept header of "application/json, application/xml" returns JSON object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/auto" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/json, application/xml"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "{\"firstName\":\"John\",\"lastName\":\"Doe\",\"birthDate\":\"1990-07-12T00:00:00\",\"height\":1.85,\"piercings\":[\"ear\",\"nose\"]}"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal("application/json", ctx.Response |> getContentType)
    }

[<Fact>]
let ``Get "/auto" with Accept header of "application/json; q=0.5, application/xml" returns XML object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/auto" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/json; q=0.5, application/xml"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
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

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            XmlAssert.equals expected body
            Assert.Equal("application/xml", ctx.Response |> getContentType)
    }

[<Fact>]
let ``Get "/auto" with Accept header of "application/json; q=0.5, application/xml; q=0.6" returns XML object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/auto" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/json; q=0.5, application/xml; q=0.6"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
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

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            XmlAssert.equals expected body
            Assert.Equal("application/xml", ctx.Response |> getContentType)
    }

[<Fact>]
let ``Get "/auto" with Accept header of "text/plain; q=0.7, application/xml; q=0.6" returns text object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/auto" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("text/plain; q=0.7, application/xml; q=0.6"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = @"First name: John
Last name: Doe
Birth date: 1990-07-12
Height: 1.85
Piercings: [|""ear""; ""nose""|]"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal("text/plain", ctx.Response |> getContentType)
    }

[<Fact>]
let ``Get "/auto" with Accept header of "text/html" returns a 406 response`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/auto" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("text/html"))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "text/html is unacceptable by the server."

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(406, getStatusCode ctx)
            Assert.Equal(expected, body)
            Assert.Equal("text/plain", ctx.Response |> getContentType)
    }

[<Fact>]
let ``Get "/auto" without an Accept header returns a JSON object`` () =
    let johnDoe =
        {
            FirstName = "John"
            LastName  = "Doe"
            BirthDate = DateTime(1990, 7, 12)
            Height    = 1.85
            Piercings = [| "ear"; "nose" |]
        }

    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/auto" >=> negotiate johnDoe
            setStatusCode 404 >=> text "Not found" ]

    let headers = HeaderDictionary()
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "{\"firstName\":\"John\",\"lastName\":\"Doe\",\"birthDate\":\"1990-07-12T00:00:00\",\"height\":1.85,\"piercings\":[\"ear\",\"nose\"]}"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal("application/json", ctx.Response |> getContentType)
    }

[<Fact>]
let ``Warbler function should execute inner function each time`` () =
    let ctx = Substitute.For<HttpContext>()
    let inner() = Guid.NewGuid().ToString()
    let app =
        GET >=> choose [
            route "/foo"  >=> text (inner())
            route "/foo2" >=> warbler (fun _ -> text (inner())) ]
        <| next

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! res1 = app ctx
        let result1 = getBody res1.Value

        ctx.Response.Body <- new MemoryStream()

        let! res2 = app ctx
        let result2 = getBody res2.Value

        Assert.Equal(result1, result2)

        ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo2")) |> ignore
        ctx.Response.Body <- new MemoryStream()

        let! res3 = app ctx
        let result3 = getBody res3.Value

        ctx.Response.Body <- new MemoryStream()

        let! res4 = app ctx
        let result4 = getBody res4.Value

        Assert.False(result3.Equals result4)
    }

[<Fact>]
let ``GET "/redirect" redirect to "/" `` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route "/"         >=> text "Hello World"
            route "/redirect" >=> redirectTo false "/"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/redirect")) |> ignore

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFail "It was expected that the request would be redirected"
        | Some ctx -> ctx.Response.Received().Redirect("/", false)

    }

[<Fact>]
let ``POST "/redirect" redirect to "/" `` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        POST >=> choose [
            route "/"         >=> text "Hello World"
            route "/redirect" >=> redirectTo true "/"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/redirect")) |> ignore

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFail "It was expected that the request would be redirected"
        | Some ctx -> ctx.Response.Received().Redirect("/", true)
    }

type RouteBind = { Foo : string; Bar : int; Id : Guid }

[<Fact>]
let ``GET "/{foo}/{bar}/{id}" returns Hello 1 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
    let ctx = Substitute.For<HttpContext>()
    let app = GET >=> routeBind<RouteBind> "/{foo}/{bar}/{id}" (fun m -> sprintf "%s %i %O" m.Foo m.Bar m.Id |> text)
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/Hello/1/f40580b1-d55b-4fe2-b6fb-ca4f90749a9d")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    task {
        let! result = app next ctx

        match result with
        | None     -> assertFail "It was expected that the result would be Hello 1"
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal("Hello 1 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d", body)
    }

[<Fact>]
let ``GET "/{foo}/{bar}/{id}/" returns Hello 1 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
    let ctx = Substitute.For<HttpContext>()
    let app = GET >=> routeBind<RouteBind> "/{foo}/{bar}/{id}/" (fun m -> sprintf "%s %i %O" m.Foo m.Bar m.Id |> text)
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/Hello/1/f40580b1-d55b-4fe2-b6fb-ca4f90749a9d/")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    task {
        let! result = app next ctx

        match result with
        | None     -> assertFail "It was expected that the result would be Hello 1f40580b1-d55b-4fe2-b6fb-ca4f90749a9d"
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal("Hello 1 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d", body)
    }

[<Fact>]
let ``GET "/{foo}/{bar}/{id}///" returns Hello 1 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
    let ctx = Substitute.For<HttpContext>()
    let app = GET >=> routeBind<RouteBind> "/{foo}/{bar}/{id}(/*)" (fun m -> sprintf "%s %i %O" m.Foo m.Bar m.Id |> text)
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/Hello/1/f40580b1-d55b-4fe2-b6fb-ca4f90749a9d///")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    task {
        let! result = app next ctx

        match result with
        | None     -> assertFail "It was expected that the result would be Hello 1f40580b1-d55b-4fe2-b6fb-ca4f90749a9d"
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal("Hello 1 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d", body)
    }

[<Fact>]
let ``GET "/{foo}/{bar}/{id}" returns Hello 2 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
    let ctx = Substitute.For<HttpContext>()
    let app = GET >=> routeBind<RouteBind> "/{foo}/{bar}/{id}(/*)" (fun m -> sprintf "%s %i %O" m.Foo m.Bar m.Id |> text)
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/Hello/2/f40580b1-d55b-4fe2-b6fb-ca4f90749a9d///")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    task {
        let! result = app next ctx

        match result with
        | None     -> assertFail "It was expected that the result would be Hello 1f40580b1-d55b-4fe2-b6fb-ca4f90749a9d"
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal("Hello 2 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d", body)
    }

[<Fact>]
let ``GET "/{foo}/{bar}/{id}/" returns Hello 3 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
    let ctx = Substitute.For<HttpContext>()
    let app = GET >=> routeBind<RouteBind> "/{foo}/{bar}/{id}(/?)" (fun m -> sprintf "%s %i %O" m.Foo m.Bar m.Id |> text)
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/Hello/3/f40580b1-d55b-4fe2-b6fb-ca4f90749a9d/")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    task {
        let! result = app next ctx

        match result with
        | None     -> assertFail "It was expected that the result would be Hello 3 1f40580b1-d55b-4fe2-b6fb-ca4f90749a9d"
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal("Hello 3 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d", body)
    }

[<Fact>]
let ``GET "/{foo}/{bar}/{id}" returns Hello 4 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
    let ctx = Substitute.For<HttpContext>()
    let app = GET >=> routeBind<RouteBind> "/{foo}/{bar}/{id}(/?)" (fun m -> sprintf "%s %i %O" m.Foo m.Bar m.Id |> text)
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/Hello/4/f40580b1-d55b-4fe2-b6fb-ca4f90749a9d")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    task {
        let! result = app next ctx

        match result with
        | None     -> assertFail "It was expected that the result would be Hello 4 1f40580b1-d55b-4fe2-b6fb-ca4f90749a9d"
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal("Hello 4 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d", body)
    }