module Giraffe.Tests.RequestLimitationTests

open System.IO
open System.Text
open Microsoft.AspNetCore.Http
open Giraffe
open NSubstitute
open Xunit
open System

// ---------------------------------
// header restriction tests
// ---------------------------------

[<Fact>]
let ``block: request with no 'Accept' header`` () =
  let ctx = Substitute.For<HttpContext>()
  let app = GET >=> mustAcceptAny [ "application/json" ] >=> setStatusCode 200 >=> text "allowed"

  ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
  ctx.Request.Path.ReturnsForAnyArgs ("/") |> ignore
  ctx.Response.Body <- new MemoryStream()
  
  task {
    let! result = app next ctx
    match result with
    | None     -> assertFail "Result was expected"
    | Some ctx -> Assert.Equal (StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode) }

[<Fact>]
let ``block: request with unallowed 'Accept' header`` () =
  let ctx = Substitute.For<HttpContext>()
  let app = GET >=> mustAcceptAny [ "application/json" ] >=> setStatusCode 200 >=> text "allowed"

  ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
  ctx.Request.Path.ReturnsForAnyArgs ("/") |> ignore
  ctx.Request.Headers.Accept <- "test/plain"
  ctx.Response.Body <- new MemoryStream()

  task {
    let! result = app next ctx
    match result with
    | None     -> assertFail "Result was expected"
    | Some ctx -> Assert.Equal (StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode) }

[<Fact>]
let ``allow: request with allowed 'Accept' header`` () =
  let ctx = Substitute.For<HttpContext>()
  let app = GET >=> mustAcceptAny [ "application/json" ] >=> setStatusCode 200 >=> text "allowed"

  ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
  ctx.Request.Path.ReturnsForAnyArgs ("/") |> ignore
  ctx.Request.Headers.Accept <- "application/json"
  ctx.Response.Body <- new MemoryStream()

  task {
    let! result = app next ctx
    match result with
    | None     -> assertFail "Result was expected"
    | Some ctx -> Assert.Equal (StatusCodes.Status200OK, ctx.Response.StatusCode) }

[<Fact>]
let ``block: request with no 'Content-Type' header`` () =
  let ctx = Substitute.For<HttpContext>()
  let app = GET >=> haveContentType "application/json" >=> setStatusCode 200 >=> text "allowed"

  ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
  ctx.Request.Path.ReturnsForAnyArgs ("/") |> ignore
  ctx.Response.Body <- new MemoryStream()

  task {
    let! result = app next ctx 
    match result with
    | None     -> assertFail "Result was expected"
    | Some ctx -> Assert.Equal (StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode) }

[<Fact>]
let ``block: request with unallowed 'Content-Type' header`` () =
  let ctx = Substitute.For<HttpContext>()
  let app = GET >=> haveContentType "application/json" >=> setStatusCode 200 >=> text "allowed"

  ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
  ctx.Request.Path.ReturnsForAnyArgs ("/") |> ignore
  ctx.Request.Headers.Add ("Content-Type", "text/plain")
  ctx.Response.Body <- new MemoryStream()

  task {
    let! result = app next ctx 
    match result with
    | None     -> assertFail "Result was expected"
    | Some ctx -> Assert.Equal (StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode) }

[<Fact>]
let ``allow: request with allowed 'Content-Type' header`` () =
  let ctx = Substitute.For<HttpContext>()
  let app = GET >=> haveContentType "application/json" >=> setStatusCode 200 >=> text "allowed"
  
  ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
  ctx.Request.Path.ReturnsForAnyArgs ("/") |> ignore
  ctx.Request.Headers.ReturnsForAnyArgs (new HeaderDictionary ()) |> ignore
  ctx.Request.ContentType <- "application/json"
  ctx.Request.Body <- new MemoryStream(buffer=Encoding.UTF8.GetBytes("{ \"name\": \"John\" }"))
  ctx.Response.Body <- new MemoryStream()

  task {
    let! result = app next ctx
    match result with
    | None     -> assertFail "Result was excpected"
    | Some ctx -> Assert.Equal (StatusCodes.Status200OK, ctx.Response.StatusCode) }

[<Fact>]
let ``block: request without 'Content-Length' header`` () =
  let ctx = Substitute.For<HttpContext>()
  let app = GET >=> maxContentLength 1000L >=> setStatusCode 200 >=> text "allowed"
  
  ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
  ctx.Request.Path.ReturnsForAnyArgs ("/") |> ignore
  ctx.Request.Headers.ReturnsForAnyArgs (new HeaderDictionary ()) |> ignore
  ctx.Request.Body <- new MemoryStream(buffer=Array.zeroCreate<byte> 1)
  ctx.Response.Body <- new MemoryStream()

  task {
    let! result = app next ctx
    match result with
    | None     -> assertFail "Result was excpected"
    | Some ctx -> Assert.Equal (StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode) }

[<Fact>]
let ``block: request with exceeded 'Content-Length' header`` () =
  let ctx = Substitute.For<HttpContext>()
  let app = GET >=> maxContentLength 1L >=> setStatusCode 200 >=> text "allowed"
  
  ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
  ctx.Request.Path.ReturnsForAnyArgs ("/") |> ignore
  ctx.Request.Headers.ReturnsForAnyArgs (new HeaderDictionary ()) |> ignore
  ctx.Request.ContentLength <- 1000L
  ctx.Request.Body <- new MemoryStream(buffer=Array.zeroCreate<byte> 1)
  ctx.Response.Body <- new MemoryStream()

  task {
    let! result = app next ctx
    match result with
    | None     -> assertFail "Result was excpected"
    | Some ctx -> Assert.Equal (StatusCodes.Status406NotAcceptable, ctx.Response.StatusCode) }

[<Fact>]
let ``allow: request with allowed 'Content-Length' header`` () =
  let ctx = Substitute.For<HttpContext>()
  let app = GET >=> maxContentLength 1000L >=> setStatusCode 200 >=> text "allowed"
  
  ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
  ctx.Request.Path.ReturnsForAnyArgs ("/") |> ignore
  ctx.Request.Headers.ReturnsForAnyArgs (new HeaderDictionary ()) |> ignore
  ctx.Request.ContentLength <- 1L
  ctx.Request.Body <- new MemoryStream(buffer=Array.zeroCreate<byte> 1)
  ctx.Response.Body <- new MemoryStream()

  task {
    let! result = app next ctx
    match result with
    | None     -> assertFail "Result was excpected"
    | Some ctx -> Assert.Equal (StatusCodes.Status200OK, ctx.Response.StatusCode) }