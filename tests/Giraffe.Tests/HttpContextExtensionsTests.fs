module Giraffe.HttpContextExtensionsTests

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Threading.Tasks
open Xunit
open NSubstitute
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http.Internal
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Giraffe.Common
open Giraffe.HttpHandlers
open Giraffe.Tasks

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

let ``BindJson test`` () =
    let ctx = Substitute.For<HttpContext>()

    let jsonHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindJson<Customer>()
                return! text (model.ToString()) next ctx
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

    task {
    let! result = app (Some >> Task.FromResult) ctx


    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
    }

[<Fact>]

let ``BindXml test`` () =
    let ctx = Substitute.For<HttpContext>()

    let xmlHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindXml<Customer>()
                return! text (model.ToString()) next ctx
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

    task {
    let! result = app (Some >> Task.FromResult) ctx


    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
    }

[<Fact>]

let ``BindForm test`` () =
    let ctx = Substitute.For<HttpContext>()

    let formHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindForm<Customer>()
                return! text (model.ToString()) next ctx
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
    let taskColl = System.Threading.Tasks.Task.FromResult(FormCollection(form) :> IFormCollection)
    ctx.Request.ReadFormAsync().ReturnsForAnyArgs(taskColl) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/form")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    task {
        let! result = app (Some >> Task.FromResult) ctx


        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
    }

[<Fact>]

let ``BindQueryString test`` () =
    let ctx = Substitute.For<HttpContext>()

    let queryHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let model = ctx.BindQueryString<Customer>()
            text (model.ToString()) next ctx

    let app = GET >=> route "/query" >=> queryHandler

    let queryStr = "?Name=John%20Doe&IsVip=true&BirthDate=1990-04-20&Balance=150000.5&LoyaltyPoints=137"
    let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
    let asdf = QueryCollection(query) :> IQueryCollection
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/query")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    task {
    let! result = app (Some >> Task.FromResult) ctx


    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
    }

[<Fact>]

let ``BindQueryString with option property test`` () =
    let testRoute queryStr expected =
        let queryHandlerWithSome next (ctx : HttpContext) =
            task {
                let model = ctx.BindQueryString<ModelWithOption>()
                Assert.Equal(expected, model)
                return! setStatusCode 200 next ctx
            }

        let app = GET >=> route "/" >=> queryHandlerWithSome

        let ctx = Substitute.For<HttpContext>()
        let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
        ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
        ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
        ctx.Request.Path.ReturnsForAnyArgs (PathString("/")) |> ignore
        ctx.Response.Body <- new MemoryStream()

        app (Some >> Task.FromResult) ctx
    
    task {
        let! _ = testRoute "?OptionalInt=1&OptionalString=Hi" { OptionalInt = Some 1; OptionalString = Some "Hi" }
        let! _ = testRoute "?" { OptionalInt = None; OptionalString = None }
        return!  testRoute "?OptionalInt=&OptionalString=" { OptionalInt = None; OptionalString = Some "" }
    }


[<Fact>]

let ``BindModel with JSON content returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModel<Customer>()
                return! text (model.ToString()) next ctx
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

    task {
    let! result = app (Some >> Task.FromResult) ctx

    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
    }

[<Fact>]

let ``BindModel with XML content returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModel<Customer>()
                return! text (model.ToString()) next ctx
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

    task {
    let! result = app (Some >> Task.FromResult) ctx


    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
    }

[<Fact>]

let ``BindModel with FORM content returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModel<Customer>()
                return! text (model.ToString()) next ctx
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
    let taskColl = System.Threading.Tasks.Task.FromResult(FormCollection(form) :> IFormCollection)
    ctx.Request.ReadFormAsync().ReturnsForAnyArgs(taskColl) |> ignore
    ctx.Request.ContentType.ReturnsForAnyArgs contentType |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    task {
        let! result = app (Some >> Task.FromResult) ctx


        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx ->
            let body = getBody ctx
            Assert.Equal(expected, body)
    }

[<Fact>]

let ``BindModel with JSON content and a specific charset returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModel<Customer>()
                return! text (model.ToString()) next ctx
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

    task {
    let! result = app (Some >> Task.FromResult) ctx


    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
    }

[<Fact>]

let ``BindModel during HTTP GET request with query string returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModel<Customer>()
                return! text (model.ToString()) next ctx
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

    task {
    let! result = app (Some >> Task.FromResult) ctx


    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
    }

[<Fact>]

let ``TryGetRequestHeader during HTTP GET request with returns correct resultd`` () =
    let ctx = Substitute.For<HttpContext>()

    let testHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            (match ctx.TryGetRequestHeader "X-Test" with
            | Some value -> text value
            | None       -> setStatusCode 400 >=> text "Bad Request"
            ) next ctx

    let app = route "/test" >=> testHandler

    let headers = HeaderDictionary()
    headers.Add("X-Test", StringValues("It works!"))
    ctx.Request.Headers.ReturnsForAnyArgs(headers) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/test")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "It works!"

    task {
    let! result = app (Some >> Task.FromResult) ctx


    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
    }

[<Fact>]

let ``TryGetQueryStringValue during HTTP GET request with query string returns correct resultd`` () =
    let ctx = Substitute.For<HttpContext>()

    let testHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            (match ctx.TryGetQueryStringValue "BirthDate" with
            | Some value -> text value
            | None       -> setStatusCode 400 >=> text "Bad Request"
            ) next ctx

    let app = route "/test" >=> testHandler

    let queryStr = "?Name=John%20Doe&IsVip=true&BirthDate=1990-04-20&Balance=150000.5&LoyaltyPoints=137"
    let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
    let asdf = QueryCollection(query) :> IQueryCollection
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/test")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "1990-04-20"

    task {
    let! result = app (Some >> Task.FromResult) ctx


    match result with
    | None     -> assertFailf "Result was expected to be %s" expected
    | Some ctx ->
        let body = getBody ctx
        Assert.Equal(expected, body)
    }