module Giraffe.Tests.StreamingTests

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

    let streamHandler (enableRangeProcessing : bool) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let stream =
                new FileStream(
                    "TestFiles/streaming.txt",
                    FileMode.Open)
            ctx.WriteStreamAsync enableRangeProcessing stream

    let webApp =
        choose [
            route Urls.rangeProcessingEnabled  >=> streamHandler true
            route Urls.rangeProcessingDisabled >=> streamHandler false
        ]

    let errorHandler (ex : Exception) (_ : ILogger) : HttpHandler =
        setStatusCode 500 >=> text ex.Message

    let configureApp (app : IApplicationBuilder) =
        app.UseGiraffeErrorHandler(errorHandler)
           .UseGiraffe webApp

    let configureServices (services : IServiceCollection) =
        services.AddGiraffe() |> ignore

// ---------------------------------
// Test server/client setup
// ---------------------------------

let createHost() =
    WebHostBuilder()
        .UseContentRoot(Directory.GetCurrentDirectory())
        .Configure(Action<IApplicationBuilder> WebApp.configureApp)
        .ConfigureServices(Action<IServiceCollection> WebApp.configureServices)

// ---------------------------------
// Helper functions
// ---------------------------------

let runTask task =
    task
    |> Async.AwaitTask
    |> Async.RunSynchronously

// ---------------------------------
// Compose web request functions
// ---------------------------------

let createRequest (method : HttpMethod) (path : string) =
    let url = "http://127.0.0.1" + path
    new HttpRequestMessage(method, url)

let makeRequest (request : HttpRequestMessage) =
    use server = new TestServer(createHost())
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
let ``HTTP GET entire file with range processing disabled`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> makeRequest
    |> isStatus HttpStatusCode.OK
    |> containsHeader false "Accept-Ranges"
    |> containsContentHeader false "Content-Range"
    |> hasContentLength 62L
    |> readText
    |> shouldEqual "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"

[<Fact>]
let ``HTTP GET entire file with range processing enabled`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingEnabled
    |> makeRequest
    |> isStatus HttpStatusCode.OK
    |> hasAcceptRanges "bytes"
    |> containsContentHeader false "Content-Range"
    |> hasContentLength 62L
    |> readText
    |> shouldEqual "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"

[<Fact>]
let ``HTTP HEAD entire file with range processing disabled`` () =
    createRequest HttpMethod.Head Urls.rangeProcessingDisabled
    |> makeRequest
    |> isStatus HttpStatusCode.OK
    |> containsHeader false "Accept-Ranges"
    |> containsContentHeader false "Content-Range"
    |> hasContentLength 62L
    |> readText
    |> shouldEqual ""

[<Fact>]
let ``HTTP HEAD entire file with range processing enabled`` () =
    createRequest HttpMethod.Head Urls.rangeProcessingEnabled
    |> makeRequest
    |> isStatus HttpStatusCode.OK
    |> hasAcceptRanges "bytes"
    |> containsContentHeader false "Content-Range"
    |> hasContentLength 62L
    |> readText
    |> shouldEqual ""

[<Fact>]
let ``HTTP HEAD part of file with range processing enabled`` () =
    createRequest HttpMethod.Head Urls.rangeProcessingEnabled
    |> addHeader "Range" "bytes=0-9"
    |> makeRequest
    |> isStatus HttpStatusCode.PartialContent
    |> hasAcceptRanges "bytes"
    |> hasContentRange "bytes 0-9/62"
    |> hasContentLength 10L
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP GET part of file with range processing enabled`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingEnabled
    |> addHeader "Range" "bytes=0-9"
    |> makeRequest
    |> isStatus HttpStatusCode.PartialContent
    |> hasAcceptRanges "bytes"
    |> hasContentRange "bytes 0-9/62"
    |> hasContentLength 10L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57"

[<Fact>]
let ``HTTP GET middle part of file with range processing enabled`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingEnabled
    |> addHeader "Range" "bytes=12-26"
    |> makeRequest
    |> isStatus HttpStatusCode.PartialContent
    |> hasAcceptRanges "bytes"
    |> hasContentRange "bytes 12-26/62"
    |> hasContentLength 15L
    |> readBytes
    |> printBytes
    |> shouldEqual "99,100,101,102,103,104,105,106,107,108,109,110,111,112,113"

[<Fact>]
let ``HTTP GET with range without end and range processing enabled`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingEnabled
    |> addHeader "Range" "bytes=20-"
    |> makeRequest
    |> isStatus HttpStatusCode.PartialContent
    |> hasAcceptRanges "bytes"
    |> hasContentRange "bytes 20-61/62"
    |> hasContentLength 42L
    |> readBytes
    |> printBytes
    |> shouldEqual "107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET middle part of file with range processing disabled`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "Range" "bytes=12-26"
    |> makeRequest
    |> isStatus HttpStatusCode.OK
    |> containsHeader false "Accept-Ranges"
    |> containsContentHeader false "Content-Range"
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP HEAD middle part of file with range processing disabled`` () =
    createRequest HttpMethod.Head Urls.rangeProcessingDisabled
    |> addHeader "Range" "bytes=12-26"
    |> makeRequest
    |> isStatus HttpStatusCode.OK
    |> containsHeader false "Accept-Ranges"
    |> containsContentHeader false "Content-Range"
    |> hasContentLength 62L
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP GET with invalid range and with range processing enabled`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingEnabled
    |> addHeader "Range" "bytes=63-70"
    |> makeRequest
    |> isStatus HttpStatusCode.RequestedRangeNotSatisfiable
    |> hasAcceptRanges "bytes"
    |> hasContentRange "bytes */62"
    |> containsContentHeader false "Content-Length"
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP HEAD with invalid range and with range processing enabled`` () =
    createRequest HttpMethod.Head Urls.rangeProcessingEnabled
    |> addHeader "Range" "bytes=63-70"
    |> makeRequest
    |> isStatus HttpStatusCode.RequestedRangeNotSatisfiable
    |> hasAcceptRanges "bytes"
    |> hasContentRange "bytes */62"
    |> containsContentHeader false "Content-Length"
    |> readBytes
    |> shouldBeEmpty

[<Fact>]
let ``HTTP GET with invalid range and with range processing disabled`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "Range" "bytes=63-70"
    |> makeRequest
    |> isStatus HttpStatusCode.OK
    |> containsHeader false "Accept-Ranges"
    |> containsContentHeader false "Content-Range"
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"

[<Fact>]
let ``HTTP GET with multiple ranges and with range processing enabled`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingEnabled
    |> addHeader "Range" "bytes=5-10, 20-25, 40-"
    |> makeRequest
    |> isStatus HttpStatusCode.OK
    |> hasAcceptRanges "bytes"
    |> containsContentHeader false "Content-Range"
    |> hasContentLength 62L
    |> readBytes
    |> printBytes
    |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"