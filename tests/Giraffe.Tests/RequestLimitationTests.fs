module Giraffe.Tests.RequestLimitationTests

open System.IO
open System.Text
open Microsoft.AspNetCore.Http
open Giraffe
open NSubstitute
open Xunit

module private Helpers =

    let private customInvalidHeaderValueHandler (header: string) =
        RequestErrors.notAcceptable (
            json
                {|
                    Message = $"{header} header is not valid"
                |}
        )

    let private customHeaderNotFoundHandler (header: string) =
        RequestErrors.notAcceptable (
            json
                {|
                    Message = $"{header} header was not found"
                |}
        )

    let getCustomOptionalErrorHandler (header: string) : RequestLimitation.OptionalErrorHandlers =
        {
            InvalidHeaderValue = Some(customInvalidHeaderValueHandler header)
            HeaderNotFound = Some(customHeaderNotFoundHandler header)
        }

// ---------------------------------
// Header restriction tests
// Accept header
// ---------------------------------

[<Fact>]
let ``block: request with no 'Accept' header`` () =
    let ctx = Substitute.For<HttpContext>()

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        {
            InvalidHeaderValue = None
            HeaderNotFound = None
        }

    let app =
        GET
        >=> mustAcceptAny [ "application/json" ] optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Request rejected because 'Accept' header was not found"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was expected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``block: request with no 'Accept' header using custom response`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        Helpers.getCustomOptionalErrorHandler "Accept"

    let app =
        GET
        >=> mustAcceptAny [ "application/json" ] optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "{\"message\":\"Accept header was not found\"}"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was expected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``block: request with unallowed 'Accept' header`` () =
    let ctx = Substitute.For<HttpContext>()

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        {
            InvalidHeaderValue = None
            HeaderNotFound = None
        }

    let app =
        GET
        >=> mustAcceptAny [ "application/json" ] optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Request.Headers.Accept.ReturnsForAnyArgs("test/plain") |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected =
        "Request rejected because 'Accept' header hasn't got expected MIME type"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was expected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``block: request with unallowed 'Accept' header using custom response`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        Helpers.getCustomOptionalErrorHandler "Accept"

    let app =
        GET
        >=> mustAcceptAny [ "application/json" ] optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Request.Headers.Accept.ReturnsForAnyArgs("test/plain") |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "{\"message\":\"Accept header is not valid\"}"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was expected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``allow: request with allowed 'Accept' header`` () =
    let ctx = Substitute.For<HttpContext>()

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        {
            InvalidHeaderValue = None
            HeaderNotFound = None
        }

    let app =
        GET
        >=> mustAcceptAny [ "application/json" ] optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Request.Headers.Accept.ReturnsForAnyArgs("application/json") |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "allowed"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was expected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

// ---------------------------------
// Content-Type header
// ---------------------------------

[<Fact>]
let ``block: request with no 'Content-Type' header`` () =
    let ctx = Substitute.For<HttpContext>()

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        {
            InvalidHeaderValue = None
            HeaderNotFound = None
        }

    let app =
        GET
        >=> hasContentType "application/json" optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "Request rejected because 'Content-Type' header was not found"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was expected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``block: request with no 'Content-Type' header using custom response`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        Helpers.getCustomOptionalErrorHandler "Content-Type"

    let app =
        GET
        >=> hasContentType "application/json" optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "{\"message\":\"Content-Type header was not found\"}"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was expected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``block: request with unallowed 'Content-Type' header`` () =
    let ctx = Substitute.For<HttpContext>()

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        {
            InvalidHeaderValue = None
            HeaderNotFound = None
        }

    let app =
        GET
        >=> hasContentType "application/json" optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Request.ContentType.ReturnsForAnyArgs("text/plain") |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected =
        "Request rejected because 'Content-Type' header hasn't got expected value"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was expected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``block: request with unallowed 'Content-Type' header using custom handler`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        Helpers.getCustomOptionalErrorHandler "Content-Type"

    let app =
        GET
        >=> hasContentType "application/json" optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Request.ContentType.ReturnsForAnyArgs("text/plain") |> ignore
    ctx.Response.Body <- new MemoryStream()
    let expected = "{\"message\":\"Content-Type header is not valid\"}"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was expected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``allow: request with allowed 'Content-Type' header`` () =
    let ctx = Substitute.For<HttpContext>()

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        {
            InvalidHeaderValue = None
            HeaderNotFound = None
        }

    let app =
        GET
        >=> hasContentType "application/json" optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(new HeaderDictionary()) |> ignore
    ctx.Request.ContentType.ReturnsForAnyArgs("application/json") |> ignore
    ctx.Request.Body <- new MemoryStream(buffer = Encoding.UTF8.GetBytes("{ \"name\": \"John\" }"))
    ctx.Response.Body <- new MemoryStream()
    let expected = "allowed"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was excpected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

// ---------------------------------
// Content-Length header
// ---------------------------------

[<Fact>]
let ``block: request without 'Content-Length' header`` () =
    let ctx = Substitute.For<HttpContext>()

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        {
            InvalidHeaderValue = None
            HeaderNotFound = None
        }

    let app =
        GET
        >=> maxContentLength 1000L optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(new HeaderDictionary()) |> ignore
    ctx.Request.Body <- new MemoryStream(buffer = Array.zeroCreate<byte> 1)
    ctx.Response.Body <- new MemoryStream()
    let expected = "Request rejected because there is no 'Content-Length' header"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was excpected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``block: request without 'Content-Length' header using custom handler`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        Helpers.getCustomOptionalErrorHandler "Content-Length"

    let app =
        GET
        >=> maxContentLength 1000L optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(new HeaderDictionary()) |> ignore
    ctx.Request.Body <- new MemoryStream(buffer = Array.zeroCreate<byte> 1)
    ctx.Response.Body <- new MemoryStream()
    let expected = "{\"message\":\"Content-Length header was not found\"}"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was excpected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``block: request with exceeded 'Content-Length' header`` () =
    let ctx = Substitute.For<HttpContext>()

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        {
            InvalidHeaderValue = None
            HeaderNotFound = None
        }

    let app =
        GET
        >=> maxContentLength 1L optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(new HeaderDictionary()) |> ignore
    ctx.Request.ContentLength <- 1000L
    ctx.Request.Body <- new MemoryStream(buffer = Array.zeroCreate<byte> 1)
    ctx.Response.Body <- new MemoryStream()
    let expected = "Request rejected because 'Content-Length' header is too large"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was excpected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``block: request with exceeded 'Content-Length' header using custom handler`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        Helpers.getCustomOptionalErrorHandler "Content-Length"

    let app =
        GET
        >=> maxContentLength 1L optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(new HeaderDictionary()) |> ignore
    ctx.Request.ContentLength <- 1000L
    ctx.Request.Body <- new MemoryStream(buffer = Array.zeroCreate<byte> 1)
    ctx.Response.Body <- new MemoryStream()
    let expected = "{\"message\":\"Content-Length header is not valid\"}"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was excpected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``allow: request with allowed 'Content-Length' header`` () =
    let ctx = Substitute.For<HttpContext>()

    let optionalErrorHandler: RequestLimitation.OptionalErrorHandlers =
        {
            InvalidHeaderValue = None
            HeaderNotFound = None
        }

    let app =
        GET
        >=> maxContentLength 1000L optionalErrorHandler
        >=> setStatusCode 200
        >=> text "allowed"

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs("/") |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(new HeaderDictionary()) |> ignore
    ctx.Request.ContentLength <- 1L
    ctx.Request.Body <- new MemoryStream(buffer = Array.zeroCreate<byte> 1)
    ctx.Response.Body <- new MemoryStream()
    let expected = "allowed"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Result was excpected"
        | Some ctx ->
            Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode)
            Assert.Equal(expected, getBody ctx)
    }
