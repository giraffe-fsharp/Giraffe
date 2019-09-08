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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
let ``routef: GET "/foo/blah blah/bar" returns "blah blah"`` () =
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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

[<Theory>]
[<InlineData( "/API/hello/", "routeStartsWithf:hello" )>]
[<InlineData( "/API/hello/more", "routeStartsWithf:hello" )>]
[<InlineData( "/aPi/hello/", "routeStartsWithCif:hello" )>]
[<InlineData( "/APi/hello/more/", "routeStartsWithCif:hello" )>]
[<InlineData( "/aPI/hello/more", "routeStartsWithCif:hello" )>]
[<InlineData( "/test/hello/more", "Not found" )>]
[<InlineData( "/TEST/hello/more", "Not found" )>]
let ``routeStartsWith(f|Cif)`` (uri:string, expected:string) =

    let app =
        GET >=> choose [
            routeStartsWithf "/API/%s/" (fun capture -> text ("routeStartsWithf:" + capture))
            routeStartsWithCif "/api/%s/" (fun capture -> text ("routeStartsWithCif:" + capture))
            setStatusCode 404 >=> text "Not found"
        ]

    let ctx = mockHttpContext Version40
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString(uri)) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s"  expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``routef: GET "/foo/%O/bar/%O" returns "Guid1: ..., Guid2: ..."`` () =
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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

[<Fact>]
let ``routef: GET "/foo/bar/baz/qux" returns 404 "Not found"`` () =
    let ctx = mockHttpContext Version40
    let app =
        GET >=> choose [
            routef "/foo/%s/%s" (fun (s1, s2) -> text (sprintf "%s,%s" s1 s2))
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/foo/bar/baz/qux")) |> ignore
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
// routeCif Tests
// ---------------------------------

[<Fact>]
let ``POST "/POsT/1" returns "1"`` () =
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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
    let ctx = mockHttpContext Version40
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

let subRouteTest compatMode requestPath expected =
    let ctx = mockHttpContext compatMode
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
    ctx.Request.Path.ReturnsForAnyArgs (PathString(requestPath)) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

let nestedSubRouteTest compatMode requestPath expected =
    let ctx = mockHttpContext compatMode
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
    ctx.Request.Path.ReturnsForAnyArgs (PathString(requestPath)) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

let multipleNestedSubRouteTest compatMode requestPath expected =
    let ctx = mockHttpContext compatMode
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
    ctx.Request.Path.ReturnsForAnyArgs (PathString(requestPath)) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

let routeAfterSubRoutesWithSimilarPath compatMode requestPath expected =
    let ctx = mockHttpContext compatMode
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
    ctx.Request.Path.ReturnsForAnyArgs (PathString(requestPath)) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

let routefInsideSubRouteTest compatMode requestPath expected =
    let ctx = mockHttpContext compatMode
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
    ctx.Request.Path.ReturnsForAnyArgs (PathString(requestPath)) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``subRoute (compatMode 40): Route with empty route`` () =
    subRouteTest Version40 "/api" "api root"

[<Fact>]
let ``subRoute (compatMode 36): Route with empty route`` () =
    subRouteTest Version36 "/api" "api root"

[<Fact>]
let ``subRoute (compatMode 40): Normal nested route after subRoute`` () =
    subRouteTest Version40 "/api/users" "users"

[<Fact>]
let ``subRoute (compatMode 36): Normal nested route after subRoute`` () =
    subRouteTest Version36 "/api/users" "users"

[<Fact>]
let ``subRoute (compatMode 40): Route after subRoute has same beginning of path`` () =
    subRouteTest Version40 "/api/test" "test"

[<Fact>]
let ``subRoute (compatMode 36): Route after subRoute has same beginning of path`` () =
    subRouteTest Version36 "/api/test" "test"

[<Fact>]
let ``subRoute (compatMode 40): Nested sub routes`` () =
    nestedSubRouteTest Version40 "/api/v2/users" "users v2"

[<Fact>]
let ``subRoute (compatMode 36): Nested sub routes`` () =
    nestedSubRouteTest Version36 "/api/v2/users" "users v2"

[<Fact>]
let ``subRoute (compatMode 40): Multiple nested sub routes`` () =
    multipleNestedSubRouteTest Version40 "/api/v2/admin2" "correct admin2"

[<Fact>]
let ``subRoute (compatMode 36): Multiple nested sub routes`` () =
    multipleNestedSubRouteTest Version36 "/api/v2/admin2" "correct admin2"

[<Fact>]
let ``subRoute (compatMode 40): Route after nested sub routes has same beginning of path`` () =
    routeAfterSubRoutesWithSimilarPath Version40 "/api/v2/else" "else"

[<Fact>]
let ``subRoute (compatMode 36): Route after nested sub routes has same beginning of path`` () =
    routeAfterSubRoutesWithSimilarPath Version36 "/api/v2/else" "else"

[<Fact>]
let ``subRoute (compatMode 40): routef inside subRoute`` () =
    routefInsideSubRouteTest Version40 "/api/foo/bar/yadayada" "yadayada"

[<Fact>]
let ``subRoute (compatMode 36): routef inside subRoute`` () =
    routefInsideSubRouteTest Version36 "/api/foo/bar/yadayada" "yadayada"

// ---------------------------------
// subRoutef Tests
// ---------------------------------

let subRoutefTest compatMode requestPath expected =
    let ctx = mockHttpContext compatMode
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
    ctx.Request.Path.ReturnsForAnyArgs (PathString(requestPath)) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

let subRoutefTest2 compatMode requestPath expected =
    let ctx = mockHttpContext compatMode
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
    ctx.Request.Path.ReturnsForAnyArgs (PathString(requestPath)) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``subRoutef (compatMode 40): GET "/" returns "Not found"`` () =
    subRoutefTest Version40 "/" "Not found"

[<Fact>]
let ``subRoutef (compatMode 36): GET "/" returns "Not found"`` () =
    subRoutefTest Version36 "/" "Not found"

[<Fact>]
let ``subRoutef (compatMode 40): GET "/bar" returns "foo"`` () =
    subRoutefTest Version40 "/bar" "foo"

[<Fact>]
let ``subRoutef (compatMode 36): GET "/bar" returns "foo"`` () =
    subRoutefTest Version36 "/bar" "foo"

[<Fact>]
let ``subRoutef (compatMode 40): GET "/John/5/foo" returns "bar"`` () =
    subRoutefTest Version40 "/John/5/foo" "bar"

[<Fact>]
let ``subRoutef (compatMode 36): GET "/John/5/foo" returns "bar"`` () =
    subRoutefTest Version36 "/John/5/foo" "bar"

[<Fact>]
let ``subRoutef (compatMode 40): GET "/en/10/Julia" returns "Hello Julia! Lang: en, Version: 10"`` () =
    subRoutefTest Version40 "/en/10/Julia" "Hello Julia! Lang: en, Version: 10"

[<Fact>]
let ``subRoutef (compatMode 36): GET "/en/10/Julia" returns "Hello Julia! Lang: en, Version: 10"`` () =
    subRoutefTest Version36 "/en/10/Julia" "Hello Julia! Lang: en, Version: 10"

[<Fact>]
let ``subRoutef (compatMode 40): GET "/en/10/api/Julia" returns "Hello Julia! Lang: en, Version: 10"`` () =
    subRoutefTest2 Version40 "/en/10/api/Julia" "Hello Julia! Lang: en, Version: 10"

[<Fact>]
let ``subRoutef (compatMode 36): GET "/en/10/api/Julia" returns "Hello Julia! Lang: en, Version: 10"`` () =
    subRoutefTest2 Version36 "/en/10/api/Julia" "Hello Julia! Lang: en, Version: 10"

// ---------------------------------
// subRouteCi Tests
// ---------------------------------

[<Fact>]
let ``subRouteCi: Non-filtering handler after subRouteCi is called`` () =
    let ctx = mockHttpContext Version40
    mockJson ctx (Newtonsoft None)
    let app =
        GET >=> choose [
            subRouteCi "/foo" (text "subroute /foo")
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/FOO")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "subroute /foo"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``subRouteCi: Nested route after subRouteCi is called`` () =
    let ctx = mockHttpContext Version40
    mockJson ctx (Newtonsoft None)
    let app =
        GET >=> choose [
            subRouteCi "/foo" (
                route "/bar" >=> text "subroute /foo/bar")
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/FOO/bar")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "subroute /foo/bar"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``subRouteCi: Nested route after subRouteCi is still case sensitive`` () =
    let ctx = mockHttpContext Version40
    mockJson ctx (Newtonsoft None)
    let app =
        GET >=> choose [
            subRouteCi "/foo" (
                choose [
                    route "/bar" >=> text "subroute /foo/bar"
                    setStatusCode 404 >=> text "Not found - nested"
                ])
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/FOO/BAR")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Not found - nested"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``subRouteCi: Nested routeCi after subRouteCi is called`` () =
    let ctx = mockHttpContext Version40
    mockJson ctx (Newtonsoft None)
    let app =
        GET >=> choose [
            subRouteCi "/foo" (
                routeCi "/bar" >=> text "subroute /foo/bar")
            setStatusCode 404 >=> text "Not found" ]

    ctx.Items.Returns (new Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/FOO/BAR")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "subroute /foo/bar"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }