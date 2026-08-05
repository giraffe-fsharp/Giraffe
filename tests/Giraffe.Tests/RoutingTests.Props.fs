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

    type GiraffeShortGuid =
        static member ShortGuid() =
            ArbMap.defaults
            |> ArbMap.arbitrary<Guid>
            |> Arb.convert ShortGuid.fromGuid ShortGuid.toGuid

    type GiraffeShortId =
        static member ShortId() =
            ArbMap.defaults
            |> ArbMap.arbitrary<uint64>
            |> Arb.convert ShortId.fromUInt64 ShortId.toUInt64

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
            routeCi "/hello-ci" >=> text "CI works"
            routef "/is-valid/%b" (fun isValid -> text (sprintf "IsValid: %b" isValid))
            routef "/char/%c" (fun char -> text (sprintf "Char: %c" char))
            routef "/name/%s" (fun name -> text (sprintf "Name: %s" name))
            routef "/age/%i" (fun age -> text (sprintf "Age: %i" age))
            routef "/big-age/%d" (fun (age: int64) -> text (sprintf "BigAge: %d" age))
            routef "/price/%f" (fun price -> text (sprintf "Price: %f" price))
            routef "/guid/%O" (fun (guid: Guid) -> text (sprintf "GUID: %O" guid))
            routef "/short-guid/%s" (fun shortGuid -> text (sprintf "Short GUID: %O" shortGuid))
            routef "/short-id/%s" (fun shortId -> text (sprintf "Short ID: %O" shortId))
            routef "/combo/%s/%i" (fun (name, age) -> text (sprintf "Combo: %s is %i" name age))
            setStatusCode 404 >=> text "Not found"
        ]

    /// Re-cases every character of `s` according to `flags`, cycling through
    /// `flags` when it is shorter than `s`. Used to exercise case-insensitive
    /// route matching with arbitrary casing.
    let toggleCase (flags: bool[]) (s: string) =
        if flags.Length = 0 then
            s
        else
            String.mapi
                (fun i c ->
                    if flags.[i % flags.Length] then
                        Char.ToUpperInvariant c
                    else
                        Char.ToLowerInvariant c
                )
                s

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

[<Property>]
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

[<Property>]
let ``routef: GET "/big-age/%d" works`` (x: int64) =
    let ctx = Substitute.For<HttpContext>()

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore

    ctx.Request.Path.ReturnsForAnyArgs(PathString(sprintf "/big-age/%d" x))
    |> ignore

    ctx.Response.Body <- new MemoryStream()
    let expected = sprintf "BigAge: %d" x

    task {
        let! result = Utils.app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Property(Arbitrary = [| typeof<Utils.GiraffeString> |])>]
let ``routef: GET "/combo/%s/%i" works with multiple captured arguments`` (name: string, age: int) =
    let ctx = Substitute.For<HttpContext>()

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore

    ctx.Request.Path.ReturnsForAnyArgs(PathString(sprintf "/combo/%s/%i" name age))
    |> ignore

    ctx.Response.Body <- new MemoryStream()
    let expected = sprintf "Combo: %s is %i" name age

    task {
        let! result = Utils.app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Property>]
let ``routeCi: GET "/hello-ci" works regardless of casing`` (flags: bool[]) =
    let ctx = Substitute.For<HttpContext>()
    let path = Utils.toggleCase flags "/hello-ci"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs(PathString path) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "CI works"

    task {
        let! result = Utils.app next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Property(Arbitrary = [| typeof<Utils.GiraffeString> |])>]
let ``routef: GET on an unmatched path always falls through to 404`` (suffix: string) =
    let ctx = Substitute.For<HttpContext>()
    let path = sprintf "/does-not-exist/%s" suffix

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs(PathString path) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = Utils.app next ctx

        match result with
        | None -> assertFail "Result was expected to be Some ctx with a 404 response"
        | Some ctx ->
            Assert.Equal(404, ctx.Response.StatusCode)
            Assert.Equal("Not found", getBody ctx)
    }

module ShortGuidProps =

    [<Property>]
    let ``ShortGuid: fromGuid >> toGuid roundtrips`` (guid: Guid) =
        guid |> ShortGuid.fromGuid |> ShortGuid.toGuid = guid

    [<Property>]
    let ``ShortGuid: fromGuid always produces a 22 character string`` (guid: Guid) =
        (ShortGuid.fromGuid guid).Length = 22

module ShortIdProps =

    [<Property>]
    let ``ShortId: fromUInt64 >> toUInt64 roundtrips`` (id: uint64) =
        id |> ShortId.fromUInt64 |> ShortId.toUInt64 = id

    [<Property>]
    let ``ShortId: fromUInt64 always produces an 11 character string`` (id: uint64) =
        (ShortId.fromUInt64 id).Length = 11
