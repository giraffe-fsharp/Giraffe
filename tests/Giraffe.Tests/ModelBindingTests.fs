module Giraffe.ModelBindingTests

open System
open System.Collections.Generic
open System.IO
open System.Text
open Xunit
open NSubstitute
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Giraffe.Common
open Giraffe.ModelBinding
open Giraffe.HttpHandlers

let assertFailf format args = 
    let msg = sprintf format args
    Assert.True(false, msg)

let initNewContext() =
    let ctx      = Substitute.For<HttpContext>()
    let services = Substitute.For<IServiceProvider>()
    let logger   = Substitute.For<ILogger>()
    let handlerCtx =
        {
            HttpContext = ctx
            Services    = services
            Logger      = logger
        }
    ctx, handlerCtx

let getBody (ctx : HttpHandlerContext) =
    ctx.HttpContext.Response.Body.Position <- 0L
    use reader = new StreamReader(ctx.HttpContext.Response.Body, Encoding.UTF8)
    reader.ReadToEnd()

[<CLIMutable>]
type Customer =
    {
        Name          : string
        IsVip         : bool
        BirthDate     : DateTime
        Balance       : float
        LoyaltyPoints : int
    }
    override this.ToString() =
        sprintf "Name: %s, IsVip: %b, BirthDate: %s, Balance: %.2f, LoyaltyPoints: %i"
            this.Name
            this.IsVip
            (this.BirthDate.ToString("yyyy-MM-dd"))
            this.Balance
            this.LoyaltyPoints

[<Fact>]
let ``bindJson test`` () =
    let ctx, hctx = initNewContext()

    let jsonHandler =
        fun ctx -> 
            async {
                let! model = bindJson<Customer> ctx
                return! text (model.ToString()) ctx
            }

    let app = POST >=> route "/json" >=> jsonHandler
    
    let postContent = "{ \"Name\": \"John Doe\", \"IsVip\": true, \"BirthDate\": \"1990-04-20\", \"Balance\": 150000.5, \"LoyaltyPoints\": 137 }"
    let stream = new MemoryStream()
    let writer = new StreamWriter(stream, Encoding.UTF8)
    writer.Write postContent
    writer.Flush()
    stream.Position <- 0L

    let headers = HeaderDictionary()
    headers.Add("Content-Type", StringValues("application/json"))
    headers.Add("Content-Length", StringValues(stream.Length.ToString()))
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/json")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Body  <- stream

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``bindXml test`` () =
    let ctx, hctx = initNewContext()

    let xmlHandler =
        fun ctx -> 
            async {
                let! model = bindXml<Customer> ctx
                return! text (model.ToString()) ctx
            }

    let app = POST >=> route "/xml" >=> xmlHandler
    
    let postContent = "<Customer><Name>John Doe</Name><IsVip>true</IsVip><BirthDate>1990-04-20</BirthDate><Balance>150000.5</Balance><LoyaltyPoints>137</LoyaltyPoints></Customer>"
    let stream = new MemoryStream()
    let writer = new StreamWriter(stream, Encoding.UTF8)
    writer.Write postContent
    writer.Flush()
    stream.Position <- 0L

    let headers = HeaderDictionary()
    headers.Add("Content-Type", StringValues("application/xml"))
    headers.Add("Content-Length", StringValues(stream.Length.ToString()))    
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/xml")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Body  <- stream

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``bindForm test`` () =
    let ctx, hctx = initNewContext()

    let formHandler =
        fun ctx -> 
            async {
                let! model = bindForm<Customer> ctx
                return! text (model.ToString()) ctx
            }

    let app = POST >=> route "/form" >=> formHandler

    let headers = HeaderDictionary()
    headers.Add("Content-Type", StringValues("application/x-www-form-urlencoded"))
    ctx.Request.HasFormContentType.ReturnsForAnyArgs true |> ignore
    let form = 
        dict [
            "Name", StringValues("John Doe")
            "IsVip", StringValues("true")
            "BirthDate", StringValues("1990-04-20")
            "Balance", StringValues("150000.5")
            "LoyaltyPoints", StringValues("137")
        ] |> Dictionary
    let task = System.Threading.Tasks.Task.FromResult(FormCollection(form) :> IFormCollection)
    ctx.Request.ReadFormAsync().ReturnsForAnyArgs(task) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/form")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``bindModel with JSON content returns correct result`` () =
    let ctx, hctx = initNewContext()

    let autoHandler =
        fun ctx -> 
            async {
                let! model = bindModel<Customer> ctx
                return! text (model.ToString()) ctx
            }

    let app = POST >=> route "/auto" >=> autoHandler
    
    let contentType = "application/json"
    let postContent = "{ \"Name\": \"John Doe\", \"IsVip\": true, \"BirthDate\": \"1990-04-20\", \"Balance\": 150000.5, \"LoyaltyPoints\": 137 }"
    let stream = new MemoryStream()
    let writer = new StreamWriter(stream, Encoding.UTF8)
    writer.Write postContent
    writer.Flush()
    stream.Position <- 0L

    let headers = HeaderDictionary()
    headers.Add("Content-Type", StringValues(contentType))
    headers.Add("Content-Length", StringValues(stream.Length.ToString()))
    ctx.Request.ContentType.ReturnsForAnyArgs contentType |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Body  <- stream

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``bindModel with XML content returns correct result`` () =
    let ctx, hctx = initNewContext()

    let autoHandler =
        fun ctx -> 
            async {
                let! model = bindModel<Customer> ctx
                return! text (model.ToString()) ctx
            }

    let app = POST >=> route "/auto" >=> autoHandler
    
    let contentType = "application/xml"
    let postContent = "<Customer><Name>John Doe</Name><IsVip>true</IsVip><BirthDate>1990-04-20</BirthDate><Balance>150000.5</Balance><LoyaltyPoints>137</LoyaltyPoints></Customer>"
    let stream = new MemoryStream()
    let writer = new StreamWriter(stream, Encoding.UTF8)
    writer.Write postContent
    writer.Flush()
    stream.Position <- 0L

    let headers = HeaderDictionary()
    headers.Add("Content-Type", StringValues(contentType))
    headers.Add("Content-Length", StringValues(stream.Length.ToString()))
    ctx.Request.ContentType.ReturnsForAnyArgs contentType |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Body  <- stream

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``bindModel with FORM content returns correct result`` () =
    let ctx, hctx = initNewContext()

    let autoHandler =
        fun ctx -> 
            async {
                let! model = bindModel<Customer> ctx
                return! text (model.ToString()) ctx
            }

    let app = POST >=> route "/auto" >=> autoHandler
    
    let contentType = "application/x-www-form-urlencoded"
    let headers = HeaderDictionary()
    headers.Add("Content-Type", StringValues(contentType))
    ctx.Request.HasFormContentType.ReturnsForAnyArgs true |> ignore
    let form = 
        dict [
            "Name", StringValues("John Doe")
            "IsVip", StringValues("true")
            "BirthDate", StringValues("1990-04-20")
            "Balance", StringValues("150000.5")
            "LoyaltyPoints", StringValues("137")
        ] |> Dictionary
    let task = System.Threading.Tasks.Task.FromResult(FormCollection(form) :> IFormCollection)
    ctx.Request.ReadFormAsync().ReturnsForAnyArgs(task) |> ignore
    ctx.Request.ContentType.ReturnsForAnyArgs contentType |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``bindModel with JSON content and a specific charset returns correct result`` () =
    let ctx, hctx = initNewContext()

    let autoHandler =
        fun ctx -> 
            async {
                let! model = bindModel<Customer> ctx
                return! text (model.ToString()) ctx
            }

    let app = POST >=> route "/auto" >=> autoHandler
    
    let contentType = "application/json; charset=utf-8"
    let postContent = "{ \"Name\": \"John Doe\", \"IsVip\": true, \"BirthDate\": \"1990-04-20\", \"Balance\": 150000.5, \"LoyaltyPoints\": 137 }"
    let stream = new MemoryStream()
    let writer = new StreamWriter(stream, Encoding.UTF8)
    writer.Write postContent
    writer.Flush()
    stream.Position <- 0L

    let headers = HeaderDictionary()
    headers.Add("Content-Type", StringValues(contentType))
    headers.Add("Content-Length", StringValues(stream.Length.ToString()))
    ctx.Request.ContentType.ReturnsForAnyArgs contentType |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Body  <- stream

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    let result = 
        hctx
        |> app
        |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)