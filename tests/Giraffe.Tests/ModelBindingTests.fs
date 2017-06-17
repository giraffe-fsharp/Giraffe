module Giraffe.ModelBindingTests

open System
open System.Collections.Generic
open System.IO
open System.Text
open Xunit
open NSubstitute
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http.Internal
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open System.Threading.Tasks
open Giraffe.Common
open Giraffe.HttpContextExtensions
open Giraffe.HttpHandlers

let awaitValueTask (work:Task<_>) = work |> Async.AwaitTask |> Async.RunSynchronously 

let assertFailf format args = 
    let msg = sprintf format args
    Assert.True(false, msg)

let getBody (ctx : HttpContext) =
    ctx.Response.Body.Position <- 0L
    use reader = new StreamReader(ctx.Response.Body, Encoding.UTF8)
    reader.ReadToEnd()

[<CLIMutable>]
type ModelWithOption =
    {
        OptionalInt: int option
        OptionalString: string option
    }

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
    let ctx = Substitute.For<HttpContext>()

    let jsonHandler =
        fun (ctx : HttpContext) -> 
            task {
                let! model = ctx.BindJson<Customer>()
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
        ctx
        |> app
        |> awaitValueTask
           
    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``bindXml test`` () =
    let ctx = Substitute.For<HttpContext>()

    let xmlHandler =
        fun (ctx : HttpContext) -> 
            task {
                let! model = ctx.BindXml<Customer>()
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
        ctx
        |> app
        |> Async.AwaitTask |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)    

[<Fact>]
let ``bindForm test`` () =
    let ctx = Substitute.For<HttpContext>()

    let formHandler =
        fun (ctx : HttpContext) -> 
            task {
                let! model = ctx.BindForm<Customer>()
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
    let formtask = System.Threading.Tasks.Task.FromResult(FormCollection(form) :> IFormCollection)
    ctx.Request.ReadFormAsync().ReturnsForAnyArgs(formtask) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/form")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    let result = 
        ctx
        |> app
        |> Async.AwaitTask |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)

[<Fact>]
let ``bindQueryString test`` () =
    let ctx = Substitute.For<HttpContext>()

    let queryHandler =
        fun (ctx : HttpContext) -> 
            task {
                let! model = ctx.BindQueryString<Customer>()
                return! text (model.ToString()) ctx
            }

    let app = GET >=> route "/query" >=> queryHandler
    
    let queryStr = "?Name=John%20Doe&IsVip=true&BirthDate=1990-04-20&Balance=150000.5&LoyaltyPoints=137"
    let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
    let asdf = QueryCollection(query) :> IQueryCollection
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/query")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    let result = 
        ctx
        |> app
        |> Async.AwaitTask |> Async.RunSynchronously
            
    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)


[<Fact>]
let ``bindQueryString with option property test`` () =
    let testRoute (queryStr:string) (expected:ModelWithOption) =
        let queryHandlerWithSome (ctx : HttpContext) =
            task {
                let! model = ctx.BindQueryString<ModelWithOption>()
                Assert.Equal(expected, model)
                return! setStatusCode 200 ctx
            }

        let app = GET >=> route "/" >=> queryHandlerWithSome

        let ctx = Substitute.For<HttpContext>()
        let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
        ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
        ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
        ctx.Request.Path.ReturnsForAnyArgs (PathString("/")) |> ignore
        ctx.Response.Body <- new MemoryStream()

        ctx
        |> app
        |> Async.AwaitTask |> Async.RunSynchronously
        |> ignore

    testRoute "?OptionalInt=1&OptionalString=Hi" { OptionalInt = Some 1; OptionalString = Some "Hi" }
    testRoute "?" { OptionalInt = None; OptionalString = None }
    testRoute "?OptionalInt=&OptionalString=" { OptionalInt = None; OptionalString = Some "" }

[<Fact>]
let ``bindModel with JSON content returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (ctx : HttpContext) -> 
            task {
                let! model = ctx.BindModel<Customer>()
                return! text (model.ToString()) ctx
            }

    let app = route "/auto" >=> autoHandler
    
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
        ctx
        |> app
        |> Async.AwaitTask |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)


[<Fact>]
let ``bindModel with XML content returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (ctx : HttpContext) -> 
            task {
                let! model = ctx.BindModel<Customer>()
                return! text (model.ToString()) ctx
            }

    let app = route "/auto" >=> autoHandler
    
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
        ctx
        |> app
        |> Async.AwaitTask |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)


[<Fact>]
let ``bindModel with FORM content returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (ctx : HttpContext) -> 
            task {
                let! model = ctx.BindModel<Customer>()
                return! text (model.ToString()) ctx
            }

    let app = route "/auto" >=> autoHandler
    
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
    let formtask = System.Threading.Tasks.Task.FromResult(FormCollection(form) :> IFormCollection)
    ctx.Request.ReadFormAsync().ReturnsForAnyArgs(formtask) |> ignore
    ctx.Request.ContentType.ReturnsForAnyArgs contentType |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    let result = 
        ctx
        |> app
        |> Async.AwaitTask |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)


[<Fact>]
let ``bindModel with JSON content and a specific charset returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (ctx : HttpContext) -> 
            task {
                let! model = ctx.BindModel<Customer>()
                return! text (model.ToString()) ctx
            }

    let app = route "/auto" >=> autoHandler
    
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
        ctx
        |> app
        |> Async.AwaitTask |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)   

[<Fact>]
let ``bindModel during HTTP GET request with query string returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (ctx : HttpContext) -> 
            task {
                let! model = ctx.BindModel<Customer>()
                return! text (model.ToString()) ctx
            }

    let app = route "/auto" >=> autoHandler
    
    let queryStr = "?Name=John%20Doe&IsVip=true&BirthDate=1990-04-20&Balance=150000.5&LoyaltyPoints=137"
    let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
    let asdf = QueryCollection(query) :> IQueryCollection
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    let result = 
        ctx
        |> app
        |> Async.AwaitTask |> Async.RunSynchronously

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)