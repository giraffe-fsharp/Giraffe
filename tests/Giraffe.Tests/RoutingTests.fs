module Giraffe.Tests.RoutingTests

open System.IO
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open Xunit
open NSubstitute
open Giraffe

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