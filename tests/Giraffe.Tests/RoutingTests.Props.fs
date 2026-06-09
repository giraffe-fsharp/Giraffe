module Giraffe.Tests.RoutingTestsProp

open System
open System.IO
open Microsoft.AspNetCore.Http
open Xunit
open NSubstitute
open Giraffe
open Giraffe.Tests
open FsCheck.FSharp
open FsCheck.Xunit

module Utils =

    type GiraffeGuid =
        static member Guid() =
            ArbMap.defaults |> ArbMap.arbitrary<Guid>

    type GiraffeShortGuid =
        static member ShortGuid() =
            ArbMap.defaults
            |> ArbMap.arbitrary<Guid>
            |> Arb.convert Giraffe.ShortGuid.fromGuid Giraffe.ShortGuid.toGuid

    type GiraffeShortId =
        static member ShortId() =
            ArbMap.defaults
            |> ArbMap.arbitrary<uint64>
            |> Arb.convert Giraffe.ShortId.fromUInt64 Giraffe.ShortId.toUInt64

    type GiraffeFloat =
        static member Float() =
            ArbMap.defaults
            |> ArbMap.arbitrary<float>
            |> Arb.filter (fun x -> not (Double.IsNaN(x) || Double.IsInfinity(x)))

    type GiraffeString =
        static member String() =
            ArbMap.defaults
            |> ArbMap.arbitrary<string>
            |> Arb.mapFilter _.Replace("/", "-") (String.IsNullOrWhiteSpace >> not)

    type GiraffeChar =
        static member Char() =
            ArbMap.defaults |> ArbMap.arbitrary<char> |> Arb.filter ((<>) '/')

    let app =
        GET
        >=> choose [
            route "/" >=> text "Hello World"
            routef "/is-valid/%b" (fun isValid -> text (sprintf "IsValid: %b" isValid))
            routef "/char/%c" (fun char -> text (sprintf "Char: %c" char))
            routef "/name/%s" (fun name -> text (sprintf "Name: %s" name))
            routef "/age/%i" (fun age -> text (sprintf "Age: %i" age))
            routef "/price/%f" (fun price -> text (sprintf "Price: %f" price))
            routef "/guid/%O" (fun (guid: Guid) -> text (sprintf "GUID: %O" guid))
            routef "/short-guid/%s" (fun shortGuid -> text (sprintf "Short GUID: %O" shortGuid))
            routef "/short-id/%s" (fun shortId -> text (sprintf "Short ID: %O" shortId))
            setStatusCode 404 >=> text "Not found"
        ]

[<Property>]
let ``routef: GET "/is-valid/%b" works`` (x: bool) =
    let ctx = Substitute.For<HttpContext>()

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore

    ctx.Request.Path.ReturnsForAnyArgs(PathString(sprintf "/is-valid/%b" x))
    |> ignore

    ctx.Response.Body <- new MemoryStream()
    let expected = sprintf "IsValid: %b" x

    task {
        let! result = Utils.app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Property(Arbitrary = [| typeof<Utils.GiraffeChar> |])>]
let ``routef: GET "/char/%c" works`` (x: char) =
    let ctx = Substitute.For<HttpContext>()

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs(PathString(sprintf "/char/%c" x)) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = sprintf "Char: %c" x

    task {
        let! result = Utils.app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Property(Arbitrary = [| typeof<Utils.GiraffeString> |])>]
let ``routef: GET "/name/%s" works`` (x: string) =
    let ctx = Substitute.For<HttpContext>()

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs(PathString(sprintf "/name/%s" x)) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = sprintf "Name: %s" x

    task {
        let! result = Utils.app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Property>]
let ``routef: GET "/age/%i" works`` (x: int) =
    let ctx = Substitute.For<HttpContext>()

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs(PathString(sprintf "/age/%d" x)) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = sprintf "Age: %d" x

    task {
        let! result = Utils.app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Property(Arbitrary = [| typeof<Utils.GiraffeFloat> |])>]
let ``routef: GET "/price/%f" works`` (x: float) =
    let ctx = Substitute.For<HttpContext>()

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs(PathString(sprintf "/price/%f" x)) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = sprintf "Price: %f" x

    task {
        let! result = Utils.app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Property(Arbitrary = [| typeof<Utils.GiraffeGuid> |])>]
let ``routef: GET "/guid/%O" works`` (x: Guid) =
    let ctx = Substitute.For<HttpContext>()

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs(PathString(sprintf "/guid/%O" x)) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = sprintf "GUID: %O" x

    task {
        let! result = Utils.app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Property(Arbitrary = [| typeof<Utils.GiraffeShortGuid> |])>]
let ``routef: GET "/short-guid/%s" works`` (x: string) =
    let ctx = Substitute.For<HttpContext>()

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore

    ctx.Request.Path.ReturnsForAnyArgs(PathString(sprintf "/short-guid/%s" x))
    |> ignore

    ctx.Response.Body <- new MemoryStream()
    let expected = sprintf "Short GUID: %s" x

    task {
        let! result = Utils.app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Property(Arbitrary = [| typeof<Utils.GiraffeShortId> |])>]
let ``routef: GET "/short-id/%s" works`` (x: string) =
    let ctx = Substitute.For<HttpContext>()

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore

    ctx.Request.Path.ReturnsForAnyArgs(PathString(sprintf "/short-id/%s" x))
    |> ignore

    ctx.Response.Body <- new MemoryStream()
    let expected = sprintf "Short ID: %s" x

    task {
        let! result = Utils.app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }
