module Giraffe.Tests.PreconditionsTests

open System
open System.Net
open System.Net.Http
open System.IO
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Xunit
open Giraffe
open Microsoft.Extensions.Logging

// ---------------------------------
// Text file used for feature testing
// ---------------------------------

// ### TEXT REPRESENTATION
// ---------------------------------

    // 0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ


// ### BYTE REPRESENTATION
// ---------------------------------

    // 48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90


// ### TABULAR BYTE REPRESENTATION
// ---------------------------------

    // 0  ,1  ,2  ,3  ,4  ,5  ,6  ,7  ,8  ,9
    // ----------------------------------------
    // 48 ,49 ,50 ,51 ,52 ,53 ,54 ,55 ,56 ,57
    // 97 ,98 ,99 ,100,101,102,103,104,105,106
    // 107,108,109,110,111,112,113,114,115,116
    // 117,118,119,120,121,122,65 ,66 ,67 ,68
    // 69 ,70 ,71 ,72 ,73 ,74 ,75 ,76 ,77 ,78
    // 79 ,80 ,81 ,82 ,83 ,84 ,85 ,86 ,87 ,88
    // 89 ,90


// ---------------------------------
// Streaming App
// ---------------------------------

module Urls =

    let rangeProcessingEnabled  = "/range-processing-enabled"
    let rangeProcessingDisabled = "/range-processing-disabled"

module WebApp =

    let streamHandler (enableRangeProcessing : bool) eTag lastModified : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let stream =
                new FileStream(
                    "TestFiles/streaming2.txt",
                    FileMode.Open)
            ctx.WriteStreamAsync enableRangeProcessing stream eTag lastModified

    let webApp eTag lastModified =
        choose [
            route Urls.rangeProcessingEnabled  >=> streamHandler true eTag lastModified
            route Urls.rangeProcessingDisabled >=> streamHandler false eTag lastModified
        ]

    let errorHandler (ex : Exception) (_ : ILogger) : HttpHandler =
        printfn "Error: %s" ex.Message
        printfn "StackTrace:%s %s" Environment.NewLine ex.StackTrace
        setStatusCode 500 >=> text ex.Message

    let configureApp eTag lastModified (app : IApplicationBuilder) =
        app.UseGiraffeErrorHandler(errorHandler)
           .UseGiraffe(webApp eTag lastModified)

    let configureServices (services : IServiceCollection) =
        services.AddGiraffe() |> ignore

// ---------------------------------
// Test server/client setup
// ---------------------------------

let createHost eTag lastModified =
    WebHostBuilder()
        .UseContentRoot(Directory.GetCurrentDirectory())
        .Configure(Action<IApplicationBuilder> (WebApp.configureApp eTag lastModified))
        .ConfigureServices(Action<IServiceCollection> WebApp.configureServices)

// ---------------------------------
// Helper functions
// ---------------------------------

let waitForDebuggerToAttach() =
    printfn "Waiting for debugger to attach."
    printfn "Press enter when debugger is attached in order to continue test execution..."
    Console.ReadLine() |> ignore

let runTask task =
    task
    |> Async.AwaitTask
    |> Async.RunSynchronously

let createETag (eTag : string) =
    Some (
        Microsoft.Net.Http.Headers.EntityTagHeaderValue(
            Microsoft.Extensions.Primitives.StringSegment(eTag)))

// ---------------------------------
// Compose web request functions
// ---------------------------------

let createRequest (method : HttpMethod) (path : string) =
    let url = "http://127.0.0.1" + path
    new HttpRequestMessage(method, url)

let makeRequest eTag lastModified (request : HttpRequestMessage) =
    use server = new TestServer(createHost eTag lastModified)
    use client = server.CreateClient()
    request
    |> client.SendAsync
    |> runTask

let addHeader (key : string) (value : string) (request : HttpRequestMessage) =
    request.Headers.Add(key, value)
    request

// ---------------------------------
// Validate response functions
// ---------------------------------

let isStatus (code : HttpStatusCode) (response : HttpResponseMessage) =
    Assert.Equal(code, response.StatusCode)
    response

let containsHeader (flag : bool) (name : string) (response : HttpResponseMessage) =
    match flag with
    | true  -> Assert.True(response.Headers.Contains name)
    | false -> Assert.False(response.Headers.Contains name)
    response

let containsContentHeader (flag : bool) (name : string) (response : HttpResponseMessage) =
    match flag with
    | true  -> Assert.True(response.Content.Headers.Contains name)
    | false -> Assert.False(response.Content.Headers.Contains name)
    response

let hasContentLength (length : int64) (response : HttpResponseMessage) =
    Assert.True(response.Content.Headers.ContentLength.HasValue)
    Assert.Equal(length, response.Content.Headers.ContentLength.Value)
    response

let hasAcceptRanges (value : string) (response : HttpResponseMessage) =
    Assert.Equal(value, response.Headers.AcceptRanges.ToString())
    response

let hasContentRange (value : string) (response : HttpResponseMessage) =
    Assert.Equal(value, response.Content.Headers.ContentRange.ToString())
    response

let hasETag (eTag : string) (response : HttpResponseMessage) =
    Assert.Equal(eTag, response.Headers.ETag.Tag)
    response

let hasLastModified (lastModified : DateTimeOffset) (response : HttpResponseMessage) =
    Assert.True(response.Content.Headers.LastModified.HasValue)
    Assert.Equal(lastModified, response.Content.Headers.LastModified.Value)
    response

let readText (response : HttpResponseMessage) =
    response.Content.ReadAsStringAsync()
    |> runTask

let readBytes (response : HttpResponseMessage) =
    response.Content.ReadAsByteArrayAsync()
    |> runTask

let printBytes (bytes : byte[]) =
    bytes |> Array.fold (
        fun (s : string) (b : byte) ->
            match s.Length with
            | 0 -> sprintf "%i" b
            | _ -> sprintf "%s,%i" s b) ""

let shouldBeEmpty (bytes : byte[]) =
    Assert.True(bytes.Length.Equals 0)

let shouldEqual expected actual =
    Assert.Equal(expected, actual)

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``HTTP GET with If-Match and no ETag`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Match" "\"111\", \"222\", \"333\""
    |> makeRequest None None
    |> isStatus HttpStatusCode.PreconditionFailed
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP GET with If-Match and not matching ETag`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Match" "\"111\", \"222\", \"333\""
    |> makeRequest (createETag "\"000\"") None
    |> isStatus HttpStatusCode.PreconditionFailed
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP GET with If-Match and matching ETag`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Match" "\"111\", \"222\", \"333\""
    |> makeRequest (createETag "\"222\"") None
    |> isStatus HttpStatusCode.OK
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET with If-Unmodified-Since and no lastModified`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Unmodified-Since" (DateTimeOffset.UtcNow.ToHtmlString())
    |> makeRequest None None
    |> isStatus HttpStatusCode.OK
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET with If-Unmodified-Since in the future`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Unmodified-Since" (DateTimeOffset.UtcNow.AddDays(1.0).ToHtmlString())
    |> makeRequest None (Some DateTimeOffset.UtcNow)
    |> isStatus HttpStatusCode.OK
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET with If-Unmodified-Since not in the future but greater than lastModified`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Unmodified-Since" (DateTimeOffset.UtcNow.AddDays(-10.0).ToHtmlString())
    |> makeRequest None (Some (DateTimeOffset.UtcNow.AddDays(-11.0)))
    |> isStatus HttpStatusCode.OK
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET with If-Unmodified-Since and less than lastModified`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Unmodified-Since" (DateTimeOffset.UtcNow.AddDays(-10.0).ToHtmlString())
    |> makeRequest None (Some (DateTimeOffset.UtcNow.AddDays(-9.0)))
    |> isStatus HttpStatusCode.PreconditionFailed
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP GET with If-Unmodified-Since not in the future and equal to lastModified`` () =
    let lastModified = DateTimeOffset(DateTimeOffset.UtcNow.AddDays(-5.0).Date)
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Unmodified-Since" (lastModified.ToHtmlString())
    |> makeRequest None (Some lastModified)
    |> isStatus HttpStatusCode.OK
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET with If-None-Match without ETag`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-None-Match" "\"111\", \"222\", \"333\""
    |> makeRequest None None
    |> isStatus HttpStatusCode.OK
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET with If-None-Match with non-matching ETag`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-None-Match" "\"111\", \"222\", \"333\""
    |> makeRequest (createETag "\"444\"") None
    |> isStatus HttpStatusCode.OK
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET with If-None-Match with matching ETag`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-None-Match" "\"111\", \"222\", \"333\""
    |> makeRequest (createETag "\"333\"") None
    |> isStatus HttpStatusCode.NotModified
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP HEAD with If-None-Match with matching ETag`` () =
    createRequest HttpMethod.Head Urls.rangeProcessingDisabled
    |> addHeader "If-None-Match" "\"111\", \"222\", \"333\""
    |> makeRequest (createETag "\"222\"") None
    |> isStatus HttpStatusCode.NotModified
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP POST with If-None-Match with matching ETag`` () =
    createRequest HttpMethod.Post Urls.rangeProcessingDisabled
    |> addHeader "If-None-Match" "\"111\", \"222\", \"333\""
    |> makeRequest (createETag "\"111\"") None
    |> isStatus HttpStatusCode.PreconditionFailed
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP GET with If-Modified-Since witout lastModified`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Modified-Since" (DateTimeOffset.UtcNow.AddDays(-4.0).ToHtmlString())
    |> makeRequest None None
    |> isStatus HttpStatusCode.OK
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET with If-Modified-Since in the future and with lastModified`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Modified-Since" (DateTimeOffset.UtcNow.AddDays(10.0).ToHtmlString())
    |> makeRequest None (Some (DateTimeOffset.UtcNow.AddDays(5.0)))
    |> isStatus HttpStatusCode.NotModified
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP GET with If-Modified-Since not in the future and with greater lastModified`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Modified-Since" (DateTimeOffset.UtcNow.AddDays(-10.0).ToHtmlString())
    |> makeRequest None (Some (DateTimeOffset.UtcNow.AddDays(-5.0)))
    |> isStatus HttpStatusCode.OK
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET with If-Modified-Since not in the future and with equal lastModified`` () =
    let lastModified = DateTimeOffset(DateTimeOffset.UtcNow.AddDays(-7.0).Date)
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Modified-Since" (lastModified.ToHtmlString())
    |> makeRequest None (Some lastModified)
    |> isStatus HttpStatusCode.NotModified
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP GET with If-Modified-Since not in the future and with smaller lastModified`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Modified-Since" (DateTimeOffset.UtcNow.AddDays(-10.0).ToHtmlString())
    |> makeRequest None (Some (DateTimeOffset.UtcNow.AddDays(-11.0)))
    |> isStatus HttpStatusCode.NotModified
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP POST with If-Modified-Since not in the future and with smaller lastModified`` () =
    createRequest HttpMethod.Post Urls.rangeProcessingDisabled
    |> addHeader "If-Modified-Since" (DateTimeOffset.UtcNow.AddDays(-10.0).ToHtmlString())
    |> makeRequest None (Some (DateTimeOffset.UtcNow.AddDays(-11.0)))
    |> isStatus HttpStatusCode.OK
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``Endpoint with eTag has ETag HTTP header set`` () =
    createRequest HttpMethod.Post Urls.rangeProcessingDisabled
    |> makeRequest (createETag "\"abc\"") None
    |> isStatus HttpStatusCode.OK
    |> hasETag "\"abc\""
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``Endpoint with lastModified has Last-Modified HTTP header set`` () =
    let lastModified = DateTimeOffset(DateTimeOffset.UtcNow.AddDays(-7.0).Date)
    createRequest HttpMethod.Post Urls.rangeProcessingDisabled
    |> makeRequest None (Some lastModified)
    |> isStatus HttpStatusCode.OK
    |> hasLastModified lastModified
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET with matching If-Match ignores non-matching If-Unmodified-Since`` () =
    let eTag              = "\"abc\""
    let lastModified      = DateTimeOffset.UtcNow.AddDays(-9.0)
    let ifUnmodifiedSince = lastModified.AddDays(-1.0).ToHtmlString()
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Match" eTag
    |> addHeader "If-Unmodified-Since" ifUnmodifiedSince
    |> makeRequest (createETag eTag) (Some lastModified)
    |> isStatus HttpStatusCode.OK
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET with non-matching If-None-Match ignores not matching If-Modified-Since`` () =
    let eTag              = "\"abc\""
    let ifNoneMatch       = "\"123\""
    let lastModified      = DateTimeOffset.UtcNow.AddDays(-5.0)
    let ifModifiedSince   = lastModified.AddDays(1.0).ToHtmlString()
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-None-Match" ifNoneMatch
    |> addHeader "If-Modified-Since" ifModifiedSince
    |> makeRequest (createETag eTag) (Some lastModified)
    |> isStatus HttpStatusCode.OK
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"