module Giraffe.Tests.JsonTests

open System
open System.IO
open System.Text
open System.Net.Http
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.DependencyInjection
open Xunit
open NSubstitute
open Giraffe

// ---------------------------------
// JSON Tests
// ---------------------------------

module Utils =

    let next = fun (ctx: HttpContext) -> Task.FromResult(Some ctx)

type MyRecord =
    {
        Foo: string
        Bar: string option
        Baz: float option
    }

    static member Empty = { Foo = null; Bar = None; Baz = None }

let private dictForDefaultSerializer =
    [
        {
            Foo = "hello"
            Bar = Some "world"
            Baz = Some 12.5
        }
        {
            Foo = "hello"
            Bar = None
            Baz = Some 12.5
        }
        {
            Foo = "hello"
            Bar = None
            Baz = Some 12.5
        }
        {
            Foo = "hello"
            Bar = Some "world"
            Baz = None
        }
        {
            Foo = "hello"
            Bar = None
            Baz = None
        }
        { Foo = null; Bar = None; Baz = None }
        { Foo = null; Bar = None; Baz = None }
    ]
    |> List.mapi (fun i r -> i, r)
    |> dict

let private jsonParserHandler (expected: MyRecord) : HttpHandler =
    fun next (ctx: HttpContext) ->
        task {
            try
                let! model = ctx.BindJsonAsync<MyRecord>()

                match model with
                | m when m.Foo = expected.Foo && m.Bar = expected.Bar && m.Baz = expected.Baz ->
                    return! (setStatusCode 201 >=> json model) next ctx
                | _ ->
                    return!
                        (setStatusCode 400
                         >=> json
                                 {|
                                     Error = "Expected is different from actual"
                                 |})
                            next
                            ctx
            with ex ->
                return! (setStatusCode 500 >=> json {| Error = ex.Message |}) next ctx
        }

// ----------------------------------------------------------
// Using the default serializer with Giraffe's internal routing mechanism
// Notice the case sensitivity of the property names

[<Theory>]
[<InlineData("""{"foo":"hello", "bar": "world", "baz": 12.5}""", 0)>]
[<InlineData("""{"foo":"hello", "bar": null, "baz": 12.5}""", 1)>]
[<InlineData("""{"foo":"hello", "baz": 12.5}""", 2)>]
[<InlineData("""{"foo":"hello", "bar": "world"}""", 3)>]
[<InlineData("""{"foo":"hello"}""", 4)>]
[<InlineData("""{}""", 5)>]
[<InlineData("""{"Foo":"hello", "Bar": "world", "Baz": 12.5}""", 6)>] // Test case with different casing, should not parse correctly
let ``json parsing works properly`` (reqBody: string, expectedDictKey: int) =
    task {
        let expected = dictForDefaultSerializer.[expectedDictKey]

        let app = choose [ POST >=> route "/parse-json" >=> jsonParserHandler expected ]

        let ctx = Substitute.For<HttpContext>()
        mockJson ctx

        let stream = new MemoryStream()
        let writer = new StreamWriter(stream, Text.Encoding.UTF8)
        writer.Write reqBody
        writer.Flush()
        stream.Position <- 0L

        ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
        ctx.Request.Path.ReturnsForAnyArgs(PathString "/parse-json") |> ignore
        ctx.Response.Body <- new MemoryStream()
        ctx.Request.Body <- stream

        let! result = app next ctx

        Assert.Equal(true, result.IsSome)
        Assert.Equal(201, ctx.Response.StatusCode)
    }

// ----------------------------------------------------------
// Using the default serializer with endpoint routing
// Notice the case sensitivity of the property names

open Giraffe.EndpointRouting

[<Theory>]
[<InlineData("""{"foo":"hello", "bar": "world", "baz": 12.5}""", 0)>]
[<InlineData("""{"foo":"hello", "bar": null, "baz": 12.5}""", 1)>]
[<InlineData("""{"foo":"hello", "baz": 12.5}""", 2)>]
[<InlineData("""{"foo":"hello", "bar": "world"}""", 3)>]
[<InlineData("""{"foo":"hello"}""", 4)>]
[<InlineData("""{}""", 5)>]
[<InlineData("""{"Foo":"hello", "Bar": "world", "Baz": 12.5}""", 6)>] // Test case with different casing, should not parse correctly
let ``json parsing works properly with endpoint routing`` (reqBody: string, expectedDictKey: int) =
    task {
        let expected = dictForDefaultSerializer.[expectedDictKey]

        let endpoints: Endpoint list =
            [ POST [ route "/json-parser" (jsonParserHandler expected) ] ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services.AddRouting().AddGiraffe() |> ignore

        let request = createRequest HttpMethod.Post "/json-parser"
        request.Content <- new StringContent(reqBody, Encoding.UTF8, "application/json")

        let! response = makeRequest (fun () -> configureApp) configureServices () request

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode)
    }

// ------------------------------------------------------------------
// Using the FSharpFriendly serializer with endpoint routing
// Notice the case sensitivity of the property names

let private dictForFsharpFriendlySerializer =
    [
        {
            Foo = "hello"
            Bar = Some "world"
            Baz = Some 12.5
        }
        {
            Foo = "hello"
            Bar = None
            Baz = Some 12.5
        }
        { Foo = ""; Bar = None; Baz = None }
    ]
    |> List.mapi (fun i r -> i, r)
    |> dict

[<Theory>]
[<InlineData("""{"Foo":"hello", "Bar": "world", "Baz": 12.5}""", 0)>]
[<InlineData("""{"Foo":"hello", "Bar": null, "Baz": 12.5}""", 1)>]
[<InlineData("""{"Foo": "", "Bar": null, "Baz": null}""", 2)>]
let ``json parsing works properly with endpoint routing using FSharpFriendly serializer``
    (reqBody: string, expectedDictKey: int)
    =
    task {
        let expected = dictForFsharpFriendlySerializer.[expectedDictKey]

        let endpoints: Endpoint list =
            [ POST [ route "/json-parser" (jsonParserHandler expected) ] ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services
                .AddRouting()
                .AddGiraffe()
                .AddSingleton<Json.ISerializer>(fun _ -> Json.FsharpFriendlySerializer() :> Json.ISerializer)
            |> ignore

        let request = createRequest HttpMethod.Post "/json-parser"
        request.Content <- new StringContent(reqBody, Encoding.UTF8, "application/json")

        let! response = makeRequest (fun () -> configureApp) configureServices () request

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode)
    }

[<Theory>]
[<InlineData("""{"Foo": null, "Bar": null, "Baz": null}""")>]
[<InlineData("""{"Foo": "hello", "Baz": 12.5}""")>]
[<InlineData("""{"Foo": "hello", "Bar": "world"}""")>]
[<InlineData("""{"Foo": "hello"}""")>]
[<InlineData("""{}""")>]
[<InlineData("""{"foo": "hello", "bar": "world", "baz": 12.5}""")>]
let ``json parsing fails using FSharpFriendly serializer if some fields are missing or casing is incorrect``
    (reqBody: string)
    =
    task {
        let endpoints: Endpoint list =
            [ POST [ route "/json-parser" (jsonParserHandler MyRecord.Empty) ] ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services
                .AddRouting()
                .AddGiraffe()
                .AddSingleton<Json.ISerializer>(fun _ -> Json.FsharpFriendlySerializer() :> Json.ISerializer)
            |> ignore

        let request = createRequest HttpMethod.Post "/json-parser"
        request.Content <- new StringContent(reqBody, Encoding.UTF8, "application/json")

        let! response = makeRequest (fun () -> configureApp) configureServices () request

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode)
    }

// ------------------------------------------------------------------
// JSON Content-Type header tests

[<Fact>]
let ``json handler sets correct content type`` () =
    task {
        let jsonHandler = json MyRecord.Empty

        let ctx = Substitute.For<HttpContext>()
        mockJson ctx
        ctx.Response.Body <- new MemoryStream()
        let headers = HeaderDictionary()
        ctx.Response.Headers.ReturnsForAnyArgs headers |> ignore

        let! result = jsonHandler Utils.next ctx

        Assert.True(result.IsSome)

        // Verify Content-Type was set (this would be set by the json handler via SetContentType)
        ctx.Received().SetContentType("application/json; charset=utf-8")
        Assert.Equal("application/json; charset=utf-8", ctx.Response.Headers.["Content-Type"].ToString())
    }

// ------------------------------------------------------------------
// JSON serialization output tests

[<Fact>]
let ``json serialization produces correct JSON output`` () =
    task {
        let testRecord =
            {
                Foo = "hello"
                Bar = Some "world"
                Baz = Some 12.5
            }

        let jsonHandler = json testRecord

        let ctx = Substitute.For<HttpContext>()
        mockJson ctx
        ctx.Response.Body <- new MemoryStream()

        let! result = jsonHandler Utils.next ctx

        Assert.True(result.IsSome)

        let body = getBody ctx
        Assert.Contains("\"foo\":", body)
        Assert.Contains("\"hello\"", body)
        Assert.Contains("\"bar\":", body)
        Assert.Contains("\"world\"", body)
        Assert.Contains("\"baz\":", body)
        Assert.Contains("12.5", body)
    }

[<Fact>]
let ``json serialization handles null and None values`` () =
    task {
        let testRecord = { Foo = null; Bar = None; Baz = None }

        let jsonHandler = json testRecord

        let ctx = Substitute.For<HttpContext>()
        mockJson ctx
        ctx.Response.Body <- new MemoryStream()

        let! result = jsonHandler Utils.next ctx

        Assert.True(result.IsSome)

        let body = getBody ctx
        Assert.Contains("\"foo\":", body)
        // None values are typically serialized as null in JSON
        Assert.Contains("null", body)
        Assert.Contains("\"bar\":", body)
        Assert.Contains("\"baz\":", body)
    }

// ------------------------------------------------------------------
// Complex JSON types tests

[<CLIMutable>]
type ComplexRecord =
    {
        Id: int
        Name: string
        Items: string array
        Nested: MyRecord option
        Timestamp: DateTime
    }

[<Fact>]
let ``json parsing works with complex nested types`` () =
    task {
        let complexJson =
            """
        {
            "id": 42,
            "name": "test object",
            "items": ["item1", "item2", "item3"],
            "nested": {
                "foo": "nested value",
                "bar": "nested bar",
                "baz": 99.99
            },
            "timestamp": "2023-01-01T00:00:00Z"
        }
        """

        let complexHandler: HttpHandler =
            fun next (ctx: HttpContext) ->
                task {
                    let! model = ctx.BindJsonAsync<ComplexRecord>()
                    return! (setStatusCode 201 >=> json model) next ctx
                }

        let endpoints: Endpoint list = [ POST [ route "/complex-json" complexHandler ] ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services.AddRouting().AddGiraffe() |> ignore

        let request = createRequest HttpMethod.Post "/complex-json"
        request.Content <- new StringContent(complexJson, Encoding.UTF8, "application/json")

        let! response = makeRequest (fun () -> configureApp) configureServices () request

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode)
    }

// ------------------------------------------------------------------
// JSON array handling tests

[<Fact>]
let ``json parsing works with arrays`` () =
    task {
        let arrayJson =
            """[
            {"foo": "first", "bar": "test1", "baz": 1.0},
            {"foo": "second", "bar": null, "baz": 2.0},
            {"foo": "third", "baz": 3.0}
        ]"""

        let arrayHandler: HttpHandler =
            fun next (ctx: HttpContext) ->
                task {
                    let! models = ctx.BindJsonAsync<MyRecord[]>()

                    return!
                        (setStatusCode 201
                         >=> json
                                 {|
                                     Count = models.Length
                                     Items = models
                                 |})
                            next
                            ctx
                }

        let endpoints: Endpoint list = [ POST [ route "/json-array" arrayHandler ] ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services.AddRouting().AddGiraffe() |> ignore

        let request = createRequest HttpMethod.Post "/json-array"
        request.Content <- new StringContent(arrayJson, Encoding.UTF8, "application/json")

        let! response = makeRequest (fun () -> configureApp) configureServices () request

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode)

        let! responseBody = response.Content.ReadAsStringAsync()
        Assert.Contains("\"count\":3", responseBody)
    }

// ------------------------------------------------------------------
// JSON error handling tests

[<Theory>]
[<InlineData("""{"foo":"hello", "bar": "world", "baz": "not a number"}""")>] // Invalid type
[<InlineData("""{"foo":"hello", "bar": "world", "baz": 12.5""")>] // Missing closing brace
[<InlineData("""{"foo":"hello", "bar": "world", "baz": 12.5,}""")>] // Trailing comma
[<InlineData("""not json at all""")>] // Not JSON
[<InlineData("""{"foo":}""")>] // Invalid syntax
let ``json parsing fails with malformed JSON`` (malformedJson: string) =
    task {
        let endpoints: Endpoint list =
            [ POST [ route "/json-parser" (jsonParserHandler MyRecord.Empty) ] ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services.AddRouting().AddGiraffe() |> ignore

        let request = createRequest HttpMethod.Post "/json-parser"
        request.Content <- new StringContent(malformedJson, Encoding.UTF8, "application/json")

        let! response = makeRequest (fun () -> configureApp) configureServices () request

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode)
    }

// ------------------------------------------------------------------
// JSON chunked response tests

[<Fact>]
let ``json chunked response works correctly`` () =
    task {
        let largeRecord =
            {
                Foo = String.replicate 10_000 "large data "
                Bar = Some(String.replicate 500 "more data ")
                Baz = Some 42.42
            }

        let chunkedHandler = jsonChunked largeRecord

        let ctx = Substitute.For<HttpContext>()
        mockJson ctx
        ctx.Response.Body <- new MemoryStream()
        let headers = HeaderDictionary()
        ctx.Response.Headers.ReturnsForAnyArgs headers |> ignore

        let! result = chunkedHandler Utils.next ctx

        Assert.True(result.IsSome)

        let body = getBody ctx
        Assert.Contains("large data", body)
        Assert.Contains("more data", body)
    }

// ------------------------------------------------------------------
// JSON with different HTTP methods tests

[<Theory>]
[<InlineData("PUT")>]
[<InlineData("PATCH")>]
[<InlineData("DELETE")>]
[<InlineData("POST")>]
let ``json parsing works with different HTTP methods`` (httpMethod: string) =
    task {
        let testJson = """{"foo": "test", "bar": "value", "baz": 123.45}"""

        let expected =
            {
                Foo = "test"
                Bar = Some "value"
                Baz = Some 123.45
            }

        let methodHandler: HttpHandler =
            fun next (ctx: HttpContext) ->
                task {
                    let! model = ctx.BindJsonAsync<MyRecord>()

                    match model with
                    | m when m.Foo = expected.Foo && m.Bar = expected.Bar && m.Baz = expected.Baz ->
                        return! (setStatusCode 200 >=> json model) next ctx
                    | _ -> return! (setStatusCode 400 >=> text "Mismatch") next ctx
                }

        let endpoints: Endpoint list =
            [
                PUT [ route "/json-method" methodHandler ]
                PATCH [ route "/json-method" methodHandler ]
                DELETE [ route "/json-method" methodHandler ]
                POST [ route "/json-method" methodHandler ]
            ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services.AddRouting().AddGiraffe() |> ignore

        let method =
            match httpMethod with
            | "PUT" -> HttpMethod.Put
            | "PATCH" -> HttpMethod.Patch
            | "DELETE" -> HttpMethod.Delete
            | _ -> HttpMethod.Post

        let request = createRequest method "/json-method"
        request.Content <- new StringContent(testJson, Encoding.UTF8, "application/json")

        let! response = makeRequest (fun () -> configureApp) configureServices () request

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode)
    }
