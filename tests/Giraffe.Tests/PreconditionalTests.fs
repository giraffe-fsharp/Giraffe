module Giraffe.Tests.PreconditionsTests

open System
open System.Net
open System.Net.Http
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open FSharp.Control.Tasks.Builders
open Xunit
open Giraffe

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
    let streamHandler (enableRangeProcessing : bool) args : HttpHandler =
        args
        ||> streamFile enableRangeProcessing "streaming2.txt"

    let webApp args =
        choose [
            route Urls.rangeProcessingEnabled  >=> streamHandler true args
            route Urls.rangeProcessingDisabled >=> streamHandler false args
        ]

    let errorHandler (ex : Exception) (_ : ILogger) : HttpHandler =
        printfn "Error: %s" ex.Message
        printfn "StackTrace:%s %s" Environment.NewLine ex.StackTrace
        setStatusCode 500 >=> text ex.Message

    let configureApp args (app : IApplicationBuilder) =
        app.UseGiraffeErrorHandler(errorHandler)
           .UseGiraffe(webApp args)

    let configureServices (services : IServiceCollection) =
        services.AddGiraffe() |> ignore

let makeRequest req = makeRequest WebApp.configureApp WebApp.configureServices req

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``HTTP GET with If-Match and no ETag`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Match" "\"111\", \"222\", \"333\""
            |> makeRequest (None, None)
        let! content =
            response
            |> isStatus HttpStatusCode.PreconditionFailed
            |> readBytes
        content
        |> shouldBeEmpty
    }

[<Fact>]
let ``HTTP GET with If-Match and not matching ETag`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Match" "\"111\", \"222\", \"333\""
            |> makeRequest (createETag "000", None)
        let! content =
            response
            |> isStatus HttpStatusCode.PreconditionFailed
            |> readBytes
        content
        |> shouldBeEmpty
    }

[<Fact>]
let ``HTTP GET with If-Match and matching ETag`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Match" "\"111\", \"222\", \"333\""
            |> makeRequest (createETag "222", None)
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``HTTP GET with If-Unmodified-Since and no lastModified`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Unmodified-Since" (DateTimeOffset.UtcNow.ToHtmlString())
            |> makeRequest (None, None)
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``HTTP GET with If-Unmodified-Since in the future`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Unmodified-Since" (DateTimeOffset.UtcNow.AddDays(1.0).ToHtmlString())
            |> makeRequest (None, Some DateTimeOffset.UtcNow)
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``HTTP GET with If-Unmodified-Since not in the future but greater than lastModified`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Unmodified-Since" (DateTimeOffset.UtcNow.AddDays(-10.0).ToHtmlString())
            |> makeRequest (None, Some (DateTimeOffset.UtcNow.AddDays(-11.0)))
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``HTTP GET with If-Unmodified-Since and less than lastModified`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Unmodified-Since" (DateTimeOffset.UtcNow.AddDays(-10.0).ToHtmlString())
            |> makeRequest (None, Some (DateTimeOffset.UtcNow.AddDays(-9.0)))
        let! content =
            response
            |> isStatus HttpStatusCode.PreconditionFailed
            |> readBytes
        content
        |> shouldBeEmpty
    }

[<Fact>]
let ``HTTP GET with If-Unmodified-Since not in the future and equal to lastModified`` () =
    task {
        let lastModified = DateTimeOffset(DateTimeOffset.UtcNow.AddDays(-5.0).Date)
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Unmodified-Since" (lastModified.ToHtmlString())
            |> makeRequest (None, Some lastModified)
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``ValidatePreconditions with If-Unmodified-Since is equal to lastModified`` () =
    let ctx = DefaultHttpContext() :> Microsoft.AspNetCore.Http.HttpContext
    ctx.Request.GetTypedHeaders().IfUnmodifiedSince <- Nullable(DateTimeOffset.Parse "Sat, 01 Jan 2000 00:00:00 GMT")
    let result = ctx.ValidatePreconditions(None, (Some (DateTimeOffset.Parse "Sat, 01 Jan 2000 00:00:00 GMT")))
    match result with
    | AllConditionsMet -> ()
    | _ -> Assert.True(false, "The request should have met all pre-conditions.")

[<Fact>]
let ``HTTP GET with If-None-Match without ETag`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-None-Match" "\"111\", \"222\", \"333\""
            |> makeRequest (None, None)
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``HTTP GET with If-None-Match with non-matching ETag`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-None-Match" "\"111\", \"222\", \"333\""
            |> makeRequest (createETag "444", None)
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``HTTP GET with If-None-Match with matching ETag`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-None-Match" "\"111\", \"222\", \"333\""
            |> makeRequest (createETag "333", None)
        let! content =
            response
            |> isStatus HttpStatusCode.NotModified
            |> readBytes
        content
        |> shouldBeEmpty
    }

[<Fact>]
let ``HTTP HEAD with If-None-Match with matching ETag`` () =
    task {
        let! response =
            createRequest HttpMethod.Head Urls.rangeProcessingDisabled
            |> addHeader "If-None-Match" "\"111\", \"222\", \"333\""
            |> makeRequest (createETag "222", None)
        let! content =
            response
            |> isStatus HttpStatusCode.NotModified
            |> readBytes
        content
        |> shouldBeEmpty
    }

[<Fact>]
let ``HTTP POST with If-None-Match with matching ETag`` () =
    task {
        let! response =
            createRequest HttpMethod.Post Urls.rangeProcessingDisabled
            |> addHeader "If-None-Match" "\"111\", \"222\", \"333\""
            |> makeRequest (createETag "111", None)
        let! content =
            response
            |> isStatus HttpStatusCode.PreconditionFailed
            |> readBytes
        content
        |> shouldBeEmpty
    }

[<Fact>]
let ``HTTP GET with If-Modified-Since witout lastModified`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Modified-Since" (DateTimeOffset.UtcNow.AddDays(-4.0).ToHtmlString())
            |> makeRequest (None, None)
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``HTTP GET with If-Modified-Since in the future and with lastModified`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Modified-Since" (DateTimeOffset.UtcNow.AddDays(10.0).ToHtmlString())
            |> makeRequest (None, Some (DateTimeOffset.UtcNow.AddDays(5.0)))
        let! content =
            response
            |> isStatus HttpStatusCode.NotModified
            |> readBytes
        content
        |> shouldBeEmpty
    }

[<Fact>]
let ``HTTP GET with If-Modified-Since not in the future and with greater lastModified`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Modified-Since" (DateTimeOffset.UtcNow.AddDays(-10.0).ToHtmlString())
            |> makeRequest (None, Some (DateTimeOffset.UtcNow.AddDays(-5.0)))
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``HTTP GET with If-Modified-Since not in the future and with equal lastModified`` () =
    task {
        let lastModified = DateTimeOffset(DateTimeOffset.UtcNow.AddDays(-7.0).Date)
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Modified-Since" (lastModified.ToHtmlString())
            |> makeRequest (None, Some lastModified)
        let! content =
            response
            |> isStatus HttpStatusCode.NotModified
            |> readBytes
        content
        |> shouldBeEmpty
    }

[<Fact>]
let ``HTTP GET with If-Modified-Since not in the future and with smaller lastModified`` () =
    task {
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Modified-Since" (DateTimeOffset.UtcNow.AddDays(-10.0).ToHtmlString())
            |> makeRequest (None, Some (DateTimeOffset.UtcNow.AddDays(-11.0)))
        let! content =
            response
            |> isStatus HttpStatusCode.NotModified
            |> readBytes
        content
        |> shouldBeEmpty
    }

[<Fact>]
let ``HTTP POST with If-Modified-Since not in the future and with smaller lastModified`` () =
    task {
        let! response =
            createRequest HttpMethod.Post Urls.rangeProcessingDisabled
            |> addHeader "If-Modified-Since" (DateTimeOffset.UtcNow.AddDays(-10.0).ToHtmlString())
            |> makeRequest (None, Some (DateTimeOffset.UtcNow.AddDays(-11.0)))
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``Endpoint with eTag has ETag HTTP header set`` () =
    task {
        let! response =
            createRequest HttpMethod.Post Urls.rangeProcessingDisabled
            |> makeRequest (createETag "abc", None)
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasETag "\"abc\""
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``Endpoint with weak eTag has ETag HTTP header set`` () =
    task {
        let! response =
            createRequest HttpMethod.Post Urls.rangeProcessingDisabled
            |> makeRequest (createWeakETag "abc", None)
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasETag "W/\"abc\""
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``Endpoint with lastModified has Last-Modified HTTP header set`` () =
    task {
        let lastModified = DateTimeOffset(DateTimeOffset.UtcNow.AddDays(-7.0).Date)
        let! response =
            createRequest HttpMethod.Post Urls.rangeProcessingDisabled
            |> makeRequest (None, Some lastModified)
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasLastModified lastModified
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``HTTP GET with matching If-Match ignores non-matching If-Unmodified-Since`` () =
    task {
        let lastModified      = DateTimeOffset.UtcNow.AddDays(-9.0)
        let ifUnmodifiedSince = lastModified.AddDays(-1.0).ToHtmlString()
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-Match" "\"abc\""
            |> addHeader "If-Unmodified-Since" ifUnmodifiedSince
            |> makeRequest (createETag "abc", Some lastModified)
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }

[<Fact>]
let ``HTTP GET with non-matching If-None-Match ignores not matching If-Modified-Since`` () =
    task {
        let ifNoneMatch     = "\"123\""
        let lastModified    = DateTimeOffset.UtcNow.AddDays(-5.0)
        let ifModifiedSince = lastModified.AddDays(1.0).ToHtmlString()
        let! response =
            createRequest HttpMethod.Get Urls.rangeProcessingDisabled
            |> addHeader "If-None-Match" ifNoneMatch
            |> addHeader "If-Modified-Since" ifModifiedSince
            |> makeRequest (createETag "abc", Some lastModified)
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> hasContentLength 62L
            |> readBytes
        content
        |> printBytes
        |> shouldEqual "48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90"
    }