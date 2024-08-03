module Giraffe.Tests.HttpHandlerTests

open System
open System.IO
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Xunit
open NSubstitute
open Giraffe
open Giraffe.ViewEngine

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
        sprintf "First name: %s, Last name: %s, Birth date: %s, Height: %.2f, Piercings: %A"
            this.FirstName
            this.LastName
            (this.BirthDate.ToString("yyyy-MM-dd"))
            this.Height
            this.Piercings

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``GET "/json" returns json object`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx
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

type ResponseWithFsharpType = {
    ValueA: string option
    ValueB: JsonUnionCaseDummy
}
and JsonUnionCaseDummy =
    | JsonUnionCaseDummyA of int
    | JsonUnionCaseDummyB

[<Fact>]
let ``GET "/json" returns json object with fsharp type (JsonUnionCaseDummyA)`` () =
    let ctx = Substitute.For<HttpContext>()
    ctx.RequestServices
        .GetService(typeof<Json.ISerializer>)
        .Returns(Json.FsharpFriendlySerializer())
    |> ignore
    
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/json" >=> json { ValueA = Some "hello"; ValueB = JsonUnionCaseDummyA 42 }
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/json")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = """{"ValueA":"hello","ValueB":{"Case":"JsonUnionCaseDummyA","Fields":[42]}}"""

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let content = getBody ctx
            Assert.Equal(expected, content)
    }

[<Fact>]
let ``GET "/json" returns json object with fsharp type (JsonUnionCaseDummyB)`` () =
    let ctx = Substitute.For<HttpContext>()
    ctx.RequestServices
        .GetService(typeof<Json.ISerializer>)
        .Returns(Json.FsharpFriendlySerializer())
    |> ignore
    
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/json" >=> json { ValueA = Some "hello"; ValueB = JsonUnionCaseDummyB }
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/json")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = """{"ValueA":"hello","ValueB":{"Case":"JsonUnionCaseDummyB"}}"""

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let content = getBody ctx
            Assert.Equal(expected, content)
    }
    
[<Fact>]
let ``GET "/json" returns json object with fsharp type and use custom config`` () =
    let ctx = Substitute.For<HttpContext>()
    let customConfig =
        System.Text.Json.Serialization.JsonFSharpOptions.Default()
            .WithUnionTagNamingPolicy(System.Text.Json.JsonNamingPolicy.CamelCase)
    ctx.RequestServices
        .GetService(typeof<Json.ISerializer>)
        .Returns(Json.FsharpFriendlySerializer(customConfig, Json.Serializer.DefaultOptions))
    |> ignore
    
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/json" >=> json { ValueA = Some "hello"; ValueB = JsonUnionCaseDummyA 42 }
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/json")) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = """{"valueA":"hello","valueB":{"Case":"jsonUnionCaseDummyA","Fields":[42]}}"""

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let content = getBody ctx
            Assert.Equal(expected, content)
    }
    
let DefaultMocksWithSize =
    [
        let ``powers of two`` = [ 1..10 ] |> List.map (pown 2)
        for size in ``powers of two`` do
            yield size
    ] |> toTheoryData

[<Theory>]
[<MemberData("DefaultMocksWithSize")>]
let ``GET "/jsonChunked" returns json object`` (size: int) =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx
    let app =
        GET >=> choose [
            route "/"     >=> text "Hello World"
            route "/foo"  >=> text "bar"
            route "/jsonChunked" >=> json ( Array.replicate size { Foo = "john"; Bar = "doe"; Age = 30 } )
            setStatusCode 404 >=> text "Not found" ]

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/jsonChunked")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected =
        let o = "{\"foo\":\"john\",\"bar\":\"doe\",\"age\":30}"
        let os = Array.replicate size o |> String.concat ","
        "[" +  os + "]"

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

let CamelCasedMocksWithSize =
    [
        let ``powers of two`` = [1..10] |> List.map (pown 2)
        for size in ``powers of two`` do
            yield size
    ] |> toTheoryData

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
let ``POST "/text" with supported Accept header returns "text"`` () =
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
            Assert.Equal("text/plain; charset=utf-8", ctx.Response |> getContentType)
    }

[<Fact>]
let ``POST "/json" with supported Accept header returns "json"`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx
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
            Assert.Equal("application/json; charset=utf-8", ctx.Response |> getContentType)
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
            Assert.Equal("text/plain; charset=utf-8", ctx.Response |> getContentType)
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
let ``POST with "all-medias" header type returns the first available route`` () =
    /// Reference: https://datatracker.ietf.org/doc/html/rfc7231#section-5.3.2
    let ctx = Substitute.For<HttpContext>()
    let app =
        choose [
            POST >=> choose [
                route "/any" >=> mustAccept [ "text/plain" ] >=> text "first route"
                route "/any" >=> mustAccept [ "application/json" ] >=> json "second route"
                route "/any" >=> mustAccept [ "text/plain"; "application/json" ] >=> text "third route" ]
            setStatusCode 404 >=> text "Not found" ]
        
    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("*/*"))
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/any")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "first route"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail $"Result was expected to be %s{expected}"
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal("text/plain; charset=utf-8", ctx.Response |> getContentType)
    }
        
[<Fact>]
let ``POST with an accept header type containing a fuzzy type and concrete subtype returns the first matching route`` () =
    /// Reference: https://datatracker.ietf.org/doc/html/rfc7231#section-5.3.2
    let ctx = Substitute.For<HttpContext>()
    let app =
        choose [
            POST >=> choose [
                route "/any" >=> mustAccept [ "text/plain" ] >=> text "first route"
                route "/any" >=> mustAccept [ "application/xml" ] >=> text "<test>second route</test>"
                route "/any" >=> mustAccept [ "text/plain"; "application/json" ] >=> text "third route" ]
            setStatusCode 404 >=> text "Not found" ]
        
    let headers = HeaderDictionary()
    headers.Add("Accept", StringValues("application/*"))
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/any")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "<test>second route</test>"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail $"Result was expected to be %s{expected}"
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
            Assert.Equal("text/plain; charset=utf-8", ctx.Response |> getContentType)
    }

[<Fact>]
let ``GET "/person" returns rendered HTML view`` () =
    let ctx = Substitute.For<HttpContext>()

    let personView model =
        html [] [
            head [] [
                title [] [ str "Html Node" ]
            ]
            body [] [
                p [] [ sprintf "%s %s is %i years old." model.Foo model.Bar model.Age |> str ]
            ]
        ]

    let johnDoe = { Foo = "John"; Bar = "Doe"; Age = 30 }

    let app =
        choose [
            GET >=> choose [
                route "/"          >=> text "Hello World"
                route "/person"    >=> (personView johnDoe |> htmlView) ]
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
            Assert.Equal("text/html; charset=utf-8", ctx.Response |> getContentType)
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

// ---------------------------------
// Negotiation test fixtures
// ---------------------------------
let johnDoe =
    {
        FirstName = "John"
        LastName  = "Doe"
        BirthDate = DateTime(1990, 7, 12)
        Height    = 1.85
        Piercings = [| "ear"; "nose" |]
    }
let johnDoeAsJson =
    "{\"firstName\":\"John\",\"lastName\":\"Doe\",\"birthDate\":\"1990-07-12T00:00:00\",\"height\":1.85,\"piercings\":[\"ear\",\"nose\"]}"
let johnDoeAsXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
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

let johnDoeAsText = @"First name: John, Last name: Doe, Birth date: 1990-07-12, Height: 1.85, Piercings: [|""ear""; ""nose""|]"

let getNegotiationTestHttpContext
    (negotiationConfig : INegotiationConfig)
    (shouldMockXml : bool)
    (acceptHeaders : StringValues)
    =
    let ctx = Substitute.For<HttpContext>()

    let headers = HeaderDictionary()
    headers.Add("Accept", acceptHeaders)

    mockJson ctx
    if shouldMockXml then mockXml ctx
    mockNegotiation ctx negotiationConfig

    ctx.Items.Returns (Dictionary<obj,obj>() :> IDictionary<obj,obj>) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Response.StatusCode <- 200

    ctx

let negotiationTestApp =
    GET >=> choose [
        route "/"     >=> text "Hello World"
        route "/foo"  >=> text "bar"
        route "/auto" >=> negotiate johnDoe
        setStatusCode 404 >=> text "Not found" ]

let runNegotiationTest (ctx : HttpContext) (expectedString : string) (testChecks : HttpContext -> unit) =
    task {
        let! result = negotiationTestApp next ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expectedString
        | Some ctx -> testChecks ctx
    }

// ---------------------------------
// Negotiation tests
// ---------------------------------
let JsonReturningAcceptHeaderCases =
    [
        {NegotiationConfig = JsonOnlyNegotiationConfig() :> INegotiationConfig; StatusCode = 200; ReturnContentType = "application/json; charset=utf-8"}
        {NegotiationConfig = DefaultNegotiationConfig() :> INegotiationConfig; StatusCode = 200; ReturnContentType = "application/json; charset=utf-8"}
    ] |> toTheoryData

[<Theory>]
[<MemberData("JsonReturningAcceptHeaderCases")>]
let ``Get "/auto" with Accept header of "application/json" returns JSON object`` (config : NegotiationConfigWithExpectedResult) =
    let ctx = getNegotiationTestHttpContext config.NegotiationConfig false (StringValues("application/json"))
    let testChecks (context : HttpContext) =
        let body = getBody context
        Assert.Equal(johnDoeAsJson, body)
        Assert.Equal(config.StatusCode, context.Response.StatusCode)
        Assert.Equal(config.ReturnContentType, context.Response |> getContentType)
    runNegotiationTest ctx johnDoeAsJson testChecks

[<Theory>]
[<MemberData("JsonReturningAcceptHeaderCases")>]
let ``Get "/auto" with Accept header of "application/xml; q=0.9, application/json" returns JSON object`` (config : NegotiationConfigWithExpectedResult) =
    let ctx = getNegotiationTestHttpContext config.NegotiationConfig false (StringValues("application/xml; q=0.9, application/json"))
    let testChecks (context : HttpContext) =
        Assert.Equal(config.StatusCode, context.Response.StatusCode)
        if context.Response.StatusCode = 200 then
            let body = getBody context
            Assert.Equal(johnDoeAsJson, body)
            Assert.Equal(config.ReturnContentType, context.Response |> getContentType)
    runNegotiationTest ctx johnDoeAsJson testChecks

let XmlReturningAcceptHeaderCases =
    [
        {NegotiationConfig = JsonOnlyNegotiationConfig() :> INegotiationConfig; StatusCode = 406; ReturnContentType = ""}
        {NegotiationConfig = DefaultNegotiationConfig() :> INegotiationConfig; StatusCode = 200; ReturnContentType = "application/xml; charset=utf-8"}
    ] |> toTheoryData

[<Theory>]
[<MemberData("XmlReturningAcceptHeaderCases")>]
let ``Get "/auto" with Accept header of "application/xml" returns XML object`` (config : NegotiationConfigWithExpectedResult) =
    let ctx = getNegotiationTestHttpContext config.NegotiationConfig true (StringValues("application/xml"))
    let testChecks (context : HttpContext) =
        Assert.Equal(config.StatusCode, context.Response.StatusCode)
        if context.Response.StatusCode = 200 then
            let body = getBody context
            XmlAssert.equals johnDoeAsXml body
            Assert.Equal(config.ReturnContentType, context.Response |> getContentType)
    runNegotiationTest ctx johnDoeAsXml testChecks

let XmlJsonReturningAcceptHeaderCases =
    [
        {NegotiationConfig = JsonOnlyNegotiationConfig() :> INegotiationConfig; StatusCode = 200; ReturnContentType = "application/json; charset=utf-8"}
        {NegotiationConfig = DefaultNegotiationConfig() :> INegotiationConfig; StatusCode = 200; ReturnContentType = "application/xml; charset=utf-8"}
    ] |> toTheoryData

[<Theory>]
[<MemberData("XmlJsonReturningAcceptHeaderCases")>]
let ``Get "/auto" with Accept header of "application/xml, application/json" returns XML object`` (config : NegotiationConfigWithExpectedResult) =
    let ctx = getNegotiationTestHttpContext config.NegotiationConfig true (StringValues("application/xml, application/json"))
    let testChecks (context : HttpContext) =
        Assert.Equal(config.StatusCode, context.Response.StatusCode)
        if context.Response.StatusCode = 200 && config.ReturnContentType = "application/xml; charset=utf-8" then
            let body = getBody context
            XmlAssert.equals johnDoeAsXml body
            Assert.Equal(config.ReturnContentType, context.Response |> getContentType)
    runNegotiationTest ctx johnDoeAsXml testChecks

[<Theory>]
[<MemberData("JsonReturningAcceptHeaderCases")>]
let ``Get "/auto" with Accept header of "application/json, application/xml" returns JSON object`` (config : NegotiationConfigWithExpectedResult) =
    let ctx = getNegotiationTestHttpContext config.NegotiationConfig false (StringValues("application/json, application/xml"))
    let testChecks (context : HttpContext) =
        Assert.Equal(config.StatusCode, context.Response.StatusCode)
        if context.Response.StatusCode = 200 then
            let body = getBody context
            Assert.Equal(johnDoeAsJson, body)
            Assert.Equal(config.ReturnContentType, context.Response |> getContentType)
    runNegotiationTest ctx johnDoeAsJson testChecks

[<Theory>]
[<MemberData("XmlJsonReturningAcceptHeaderCases")>]
let ``Get "/auto" with Accept header of "application/json; q=0.5, application/xml" returns XML object`` (config : NegotiationConfigWithExpectedResult) =
    let ctx = getNegotiationTestHttpContext config.NegotiationConfig true (StringValues("application/json; q=0.5, application/xml"))
    let testChecks (context : HttpContext) =
        Assert.Equal(config.StatusCode, context.Response.StatusCode)
        if context.Response.StatusCode = 200 && config.ReturnContentType = "application/xml; charset=utf-8" then
            let body = getBody context
            XmlAssert.equals johnDoeAsXml body
            Assert.Equal(config.ReturnContentType, context.Response |> getContentType)
    runNegotiationTest ctx johnDoeAsXml testChecks

[<Theory>]
[<MemberData("XmlJsonReturningAcceptHeaderCases")>]
let ``Get "/auto" with Accept header of "application/json; q=0.5, application/xml; q=0.6" returns XML object`` (config : NegotiationConfigWithExpectedResult) =
    let ctx = getNegotiationTestHttpContext config.NegotiationConfig true (StringValues("application/json; q=0.5, application/xml; q=0.6"))
    let testChecks (context : HttpContext) =
        Assert.Equal(config.StatusCode, context.Response.StatusCode)
        if context.Response.StatusCode = 200 && config.ReturnContentType = "application/xml; charset=utf-8" then
            let body = getBody context
            XmlAssert.equals johnDoeAsXml body
            Assert.Equal(config.ReturnContentType, context.Response |> getContentType)
    runNegotiationTest ctx johnDoeAsXml testChecks

let TextReturningAcceptHeaderCases =
    [
        {NegotiationConfig = JsonOnlyNegotiationConfig() :> INegotiationConfig; StatusCode = 406; ReturnContentType = ""}
        {NegotiationConfig = DefaultNegotiationConfig() :> INegotiationConfig; StatusCode = 200; ReturnContentType = "text/plain; charset=utf-8"}
    ] |> toTheoryData

[<Theory>]
[<MemberData("TextReturningAcceptHeaderCases")>]
let ``Get "/auto" with Accept header of "text/plain; q=0.7, application/xml; q=0.6" returns text object`` (config : NegotiationConfigWithExpectedResult) =
    let ctx = getNegotiationTestHttpContext config.NegotiationConfig false (StringValues("text/plain; q=0.7, application/xml; q=0.6"))
    let testChecks (context : HttpContext) =
        Assert.Equal(config.StatusCode, context.Response.StatusCode)
        if context.Response.StatusCode = 200 then
            let body = getBody context
            Assert.Equal(johnDoeAsText, body)
            Assert.Equal(config.ReturnContentType, context.Response |> getContentType)
    runNegotiationTest ctx johnDoeAsText testChecks

let HtmlReturningAcceptHeaderCases =
    [
        {NegotiationConfig = JsonOnlyNegotiationConfig() :> INegotiationConfig; StatusCode = 406; ReturnContentType = "text/plain; charset=utf-8"}
        {NegotiationConfig = DefaultNegotiationConfig() :> INegotiationConfig; StatusCode = 406; ReturnContentType = "text/plain; charset=utf-8"}
    ] |> toTheoryData

[<Theory>]
[<MemberData("HtmlReturningAcceptHeaderCases")>]
let ``Get "/auto" with Accept header of "text/html" returns a 406 response`` (config : NegotiationConfigWithExpectedResult) =
    let ctx = getNegotiationTestHttpContext config.NegotiationConfig false (StringValues("text/html"))

    let expected = "text/html is unacceptable by the server."
    let testChecks (context : HttpContext) =
        Assert.Equal(config.StatusCode, context.Response.StatusCode)
        if context.Response.StatusCode = 406 then
            let body = getBody context
            Assert.Equal(expected, body)
            Assert.Equal("text/plain; charset=utf-8", context.Response |> getContentType)
    runNegotiationTest ctx expected testChecks

[<Theory>]
[<MemberData("JsonReturningAcceptHeaderCases")>]
let ``Get "/auto" without an Accept header returns a JSON object`` (config : NegotiationConfigWithExpectedResult) =
    let ctx = getNegotiationTestHttpContext config.NegotiationConfig false StringValues.Empty
    let testChecks (ctx : HttpContext) =
        Assert.Equal(config.StatusCode, ctx.Response.StatusCode)
        if ctx.Response.StatusCode = 200 then
            let body = getBody ctx
            Assert.Equal(johnDoeAsJson, body)
            Assert.Equal("application/json; charset=utf-8", ctx.Response |> getContentType)
    runNegotiationTest ctx johnDoeAsJson testChecks
