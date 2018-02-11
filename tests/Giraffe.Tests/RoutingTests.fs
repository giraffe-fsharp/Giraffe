module Giraffe.Tests.RoutingTests

open System
open System.IO
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open Xunit
open NSubstitute
open Giraffe

// ---------------------------------
// routeBind Tests
// ---------------------------------

type RouteBind = { Foo : string; Bar : int; Id : Guid }

[<Fact>]
let ``routeBind: GET "/{foo}/{bar}/{id}" returns Hello 1 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
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
let ``routeBind: GET "/{foo}/{bar}/{id}/" returns Hello 1 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
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
let ``routeBind: GET "/{foo}/{bar}/{id}///" returns Hello 1 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
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
let ``routeBind: GET "/{foo}/{bar}/{id}" returns Hello 2 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
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
let ``routeBind: GET "/{foo}/{bar}/{id}/" returns Hello 3 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
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
let ``routeBind: GET "/{foo}/{bar}/{id}" returns Hello 4 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
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
let ``routeBind: GET "/api/{foo}/{bar}/{id}" returns Hello 1 f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
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

type RouteBindId = { Id : Guid }

[<Fact>]
let ``routeBind: GET "/api/{id}" returns f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
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
let ``routeBind: GET "/api/{id}/" returns f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
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
let ``routeBind: GET "/test/api/{id}/" returns f40580b1-d55b-4fe2-b6fb-ca4f90749a9d``() =
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
// subRoutef Tests
// ---------------------------------

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