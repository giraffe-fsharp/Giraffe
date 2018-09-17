module Giraffe.Tests.RoutingTests

open System
open System.IO
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks.V2.ContextInsensitive
open Xunit
open NSubstitute
open Giraffe

// ---------------------------------
// route Tests
// ---------------------------------

[<Fact>]
let ``route: GET "/" returns "Hello World"`` () =
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
let ``route: GET "/foo" returns "bar"`` () =
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
let ``route: GET "/FOO" returns 404 "Not found"`` () =
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

// ---------------------------------
// routeCi Tests
// ---------------------------------

[<Fact>]
let ``GET "/JSON" returns "BaR"`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx (Newtonsoft None)
    let app =
        GET >=> choose [
            route   "/"       >=> text "Hello World"
            route   "/foo"    >=> text "bar"
            route   "/json"   >=> text "FOO"
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

// ---------------------------------
// routex Tests
// ---------------------------------

[<Fact>]
let ``routex: GET "/" returns "Hello World"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            routex "/"    >=> text "Hello World"
            routex "/foo" >=> text "bar"
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
let ``routex: GET "/foo" returns "bar"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            routex "/"    >=> text "Hello World"
            routex "/foo" >=> text "bar"
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
let ``routex: GET "/FOO" returns 404 "Not found"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            routex "/"    >=> text "Hello World"
            routex "/foo" >=> text "bar"
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
let ``routex: GET "/foo///" returns "bar"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            routex "/"        >=> text "Hello World"
            routex "/foo(/*)" >=> text "bar"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo///")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "bar"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``routex: GET "/foo2" returns "bar"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            routex "/"         >=> text "Hello World"
            routex "/foo2(/*)" >=> text "bar"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo2")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "bar"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

// ---------------------------------
// routeCix Tests
// ---------------------------------

[<Fact>]
let ``routeCix: GET "/CaSe///" returns "right"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            routex   "/case(/*)" >=> text "wrong"
            routeCix "/case(/*)" >=> text "right"
            setStatusCode 404    >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/CaSe///")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "right"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

// ---------------------------------
// routef Tests
// ---------------------------------

[<Fact>]
let ``routef: GET "/foo/blah blah/bar" returns "blah blah"`` () =
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
let ``routef: GET "/foo/johndoe/59" returns "Name: johndoe, Age: 59"`` () =
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
let ``routef: GET "/foo/b%2Fc/bar" returns "b%2Fc"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route  "/"       >=> text "Hello World"
            route  "/foo"    >=> text "bar"
            routef "/foo/%s/bar" text
            routef "/foo/%s/%i" (fun (name, age) -> text (sprintf "Name: %s, Age: %d" name age))
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo/b%2Fc/bar")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "b/c"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``routef: GET "/foo/a%2Fb%2Bc.d%2Ce/bar" returns "a%2Fb%2Bc.d%2Ce"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route  "/"       >=> text "Hello World"
            route  "/foo"    >=> text "bar"
            routef "/foo/%s/bar" text
            routef "/foo/%s/%i" (fun (name, age) -> text (sprintf "Name: %s, Age: %d" name age))
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo/a%2Fb%2Bc.d%2Ce/bar")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "a/b%2Bc.d%2Ce"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``routef: GET "/foo/%O/bar/%O" returns "Guid1: ..., Guid2: ..."`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route  "/"       >=> text "Hello World"
            route  "/foo"    >=> text "bar"
            routef "/foo/%s/bar" text
            routef "/foo/%s/%i" (fun (name, age) -> text (sprintf "Name: %s, Age: %d" name age))
            routef "/foo/%O/bar/%O" (fun (guid1 : Guid, guid2 : Guid) -> text (sprintf "Guid1: %O, Guid2: %O" guid1 guid2))
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo/4ec87f064d1e41b49342ab1aead1f99d/bar/2a6c9185-95d9-4d8c-80a6-575f99c2a716")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Guid1: 4ec87f06-4d1e-41b4-9342-ab1aead1f99d, Guid2: 2a6c9185-95d9-4d8c-80a6-575f99c2a716"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``routef: GET "/foo/%u/bar/%u" returns "Id1: ..., Id2: ..."`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            route  "/"       >=> text "Hello World"
            route  "/foo"    >=> text "bar"
            routef "/foo/%s/bar" text
            routef "/foo/%s/%i" (fun (name, age) -> text (sprintf "Name: %s, Age: %d" name age))
            routef "/foo/%u/bar/%u" (fun (id1 : uint64, id2 : uint64) -> text (sprintf "Id1: %u, Id2: %u" id1 id2))
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo/r1iKapqh_s4/bar/5aLu720NzTs")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Id1: 12635000945053400782, Id2: 16547050693006839099"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

// ---------------------------------
// routeCif Tests
// ---------------------------------

[<Fact>]
let ``POST "/POsT/1" returns "1"`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx (Newtonsoft None)
    let app =
        choose [
            GET >=> choose [
                route "/" >=> text "Hello World" ]
            POST >=> choose [
                route    "/post/1" >=> text "2"
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
    mockJson ctx (Newtonsoft None)
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

// ---------------------------------
// routeBind Tests
// ---------------------------------

[<CLIMutable>]
type RouteBind   = { Foo : string; Bar : int; Id : Guid }

[<CLIMutable>]
type RouteBindId = { Id : Guid }

type PaymentMethod =
    | Credit
    | Debit

[<CLIMutable>]
type Purchase = { PaymentMethod : PaymentMethod }

[<Fact>]
let ``routeBind: Route has matching union type``() =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            routeBind<Purchase> "/{paymentMethod}"
                (fun p -> sprintf "%s" (p.PaymentMethod.ToString()) |> text)
            setStatusCode 404 >=> text "Not found" ]
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/credit")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Credit"
    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``routeBind: Route doesn't match union type``() =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            routeBind<Purchase> "/{paymentMethod}"
                (fun p -> sprintf "%s" (p.PaymentMethod.ToString()) |> text)
            setStatusCode 404 >=> text "Not found" ]
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/wrong")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Not found"
    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``routeBind: Normal route``() =
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
let ``routeBind: Normal route with trailing slash``() =
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
let ``routeBind: Route with (/*) matches mutliple trailing slashes``() =
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
let ``routeBind: Route with (/*) matches no trailing slash``() =
    let ctx = Substitute.For<HttpContext>()
    let app = GET >=> routeBind<RouteBind> "/{foo}/{bar}/{id}(/*)" (fun m -> sprintf "%s %i %O" m.Foo m.Bar m.Id |> text)
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/Hello/2/f40580b1-d55b-4fe2-b6fb-ca4f90749a9d")) |> ignore
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
let ``routeBind: Route with (/?) matches single trailing slash``() =
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
let ``routeBind: Route with (/?) matches no trailing slash``() =
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

[<Fact>]
let ``routeBind: Route with non parameterised part``() =
    let ctx = Substitute.For<HttpContext>()
    let app = GET >=> routeBind<RouteBind> "/api/{foo}/{bar}/{id}" (fun m -> sprintf "%s %i %O" m.Foo m.Bar m.Id |> text)
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api/Hello/1/f40580b1-d55b-4fe2-b6fb-ca4f90749a9d")) |> ignore
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
let ``routeBind: Route with non parameterised part and with Guid binding``() =
    let ctx = Substitute.For<HttpContext>()
    let app = GET >=> routeBind<RouteBindId> "/api/{id}" (fun m -> sprintf "%O" m.Id |> text)
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api/f40580b1-d55b-4fe2-b6fb-ca4f90749a9d")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    task {
        let! result = app next ctx

        match result with
        | None     -> assertFail "It was expected that the result would be f40580b1-d55b-4fe2-b6fb-ca4f90749a9d"
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal("f40580b1-d55b-4fe2-b6fb-ca4f90749a9d", body)
    }

[<Fact>]
let ``routeBind: Route with non parameterised part and with (/?)``() =
    let ctx = Substitute.For<HttpContext>()
    let app = GET >=> routeBind<RouteBindId> "/api/{id}(/?)" (fun m -> sprintf "%O" m.Id |> text)
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/api/f40580b1-d55b-4fe2-b6fb-ca4f90749a9d/")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    task {
        let! result = app next ctx

        match result with
        | None     -> assertFail "It was expected that the result would be f40580b1-d55b-4fe2-b6fb-ca4f90749a9d"
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal("f40580b1-d55b-4fe2-b6fb-ca4f90749a9d", body)
    }

[<Fact>]
let ``routeBind: Route nested after subRoute``() =
    let ctx = Substitute.For<HttpContext>()
    let app = GET >=> subRoute "/test" (routeBind<RouteBindId> "/api/{id}(/?)" (fun m -> sprintf "%O" m.Id |> text))
    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/test/api/f40580b1-d55b-4fe2-b6fb-ca4f90749a9d/")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    task {
        let! result = app next ctx

        match result with
        | None     -> assertFail "It was expected that the result would be f40580b1-d55b-4fe2-b6fb-ca4f90749a9d"
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal("f40580b1-d55b-4fe2-b6fb-ca4f90749a9d", body)
    }

// ---------------------------------
// subRoute Tests
// ---------------------------------

[<Fact>]
let ``subRoute: Route with empty route`` () =
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
let ``subRoute: Normal nested route after subRoute`` () =
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
let ``subRoute: Route after subRoute has same beginning of path`` () =

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
let ``subRoute: Nested sub routes`` () =
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
let ``subRoute: Multiple nested sub routes`` () =
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
let ``subRoute: Route after nested sub routes has same beginning of path`` () =
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
let ``subRoute: routef inside subRoute`` () =
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

// ---------------------------------
// subRoutef Tests
// ---------------------------------

[<Fact>]
let ``routef: Validation`` () =
    Assert.Throws( fun () ->
        GET >=> choose [
            route   "/"       >=> text "Hello World"
            route   "/foo"    >=> text "bar"
            routef "/foo/%s/%d" (fun (name, age) -> text (sprintf "Name: %s, Age: %d" name age))
            setStatusCode 404 >=> text "Not found" ]
        |> ignore
    ) |> ignore

[<Fact>]
let ``subRoutef: GET "/" returns "Not found"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            subRoutef "/%s/%i" (fun (lang, version) ->
                choose [
                    route  "/foo" >=> text "bar"
                    routef "/%s" (fun name -> text (sprintf "Hello %s! Lang: %s, Version: %i" name lang version))
                ])
            route "/bar" >=> text "foo"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Not found"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``subRoutef: GET "/bar" returns "foo"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            subRoutef "/%s/%i" (fun (lang, version) ->
                choose [
                    route  "/foo" >=> text "bar"
                    routef "/%s" (fun name -> text (sprintf "Hello %s! Lang: %s, Version: %i" name lang version))
                ])
            route "/bar" >=> text "foo"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/bar")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "foo"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``subRoutef: GET "/John/5/foo" returns "bar"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            subRoutef "/%s/%i" (fun (lang, version) ->
                choose [
                    route  "/foo" >=> text "bar"
                    routef "/%s" (fun name -> text (sprintf "Hello %s! Lang: %s, Version: %i" name lang version))
                ])
            route "/bar" >=> text "foo"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/John/5/foo")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "bar"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``subRoutef: GET "/en/10/Julia" returns "Hello Julia! Lang: en, Version: 10"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            subRoutef "/%s/%i" (fun (lang, version) ->
                choose [
                    route  "/foo" >=> text "bar"
                    routef "/%s" (fun name -> text (sprintf "Hello %s! Lang: %s, Version: %i" name lang version))
                ])
            route "/bar" >=> text "foo"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/en/10/Julia")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Hello Julia! Lang: en, Version: 10"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``subRoutef: GET "/en/10/api/Julia" returns "Hello Julia! Lang: en, Version: 10"`` () =
    let ctx = Substitute.For<HttpContext>()
    let app =
        GET >=> choose [
            subRoutef "/%s/%i/api" (fun (lang, version) ->
                choose [
                    route  "/foo" >=> text "bar"
                    routef "/%s" (fun name -> text (sprintf "Hello %s! Lang: %s, Version: %i" name lang version))
                ])
            route "/bar" >=> text "foo"
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/en/10/api/Julia")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Hello Julia! Lang: en, Version: 10"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }