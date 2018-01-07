module Giraffe.HttpContextExtensionsTests

open System
open System.Globalization
open System.Collections.Generic
open System.IO
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http.Internal
open Microsoft.Extensions.Primitives
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open Xunit
open NSubstitute
open Newtonsoft.Json
open Giraffe
open Giraffe.Serialization
open Giraffe.GiraffeViewEngine

let assertFailf format args =
    let msg = sprintf format args
    Assert.True(false, msg)

let getBody (ctx : HttpContext) =
    ctx.Response.Body.Position <- 0L
    use reader = new StreamReader(ctx.Response.Body, Encoding.UTF8)
    reader.ReadToEnd()

let mockJson (ctx : HttpContext) (settings : JsonSerializerSettings option) =
    let jsonSettings =
        defaultArg settings NewtonsoftJsonSerializer.DefaultSettings
    ctx.RequestServices
       .GetService(typeof<IJsonSerializer>)
       .Returns(NewtonsoftJsonSerializer(jsonSettings))
    |> ignore

let mockXml (ctx : HttpContext) =
    ctx.RequestServices
       .GetService(typeof<IXmlSerializer>)
       .Returns(DefaultXmlSerializer(DefaultXmlSerializer.DefaultSettings))
    |> ignore

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
let ``BindJsonAsync test`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx None

    let jsonHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindJsonAsync<Customer>()
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
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``BindXmlAsync test`` () =
    let ctx = Substitute.For<HttpContext>()
    mockXml ctx

    let xmlHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindXmlAsync<Customer>()
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
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``BindFormAsync test`` () =
    let ctx = Substitute.For<HttpContext>()

    let formHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindFormAsync<Customer>()
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
        | Some ctx -> Assert.Equal(expected, getBody ctx)
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
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/query")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``BindQueryString culture specific test`` () =
    let ctx = Substitute.For<HttpContext>()

    let queryHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let model = ctx.BindQueryString<Customer>(CultureInfo.CreateSpecificCulture("en-GB"))
            text (model.ToString()) next ctx

    let app = GET >=> route "/query" >=> queryHandler

    let queryStr = "?Name=John%20Doe&IsVip=true&BirthDate=12/04/1998 12:34:56&Balance=150000.5&LoyaltyPoints=137"
    let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/query")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1998-04-12, Balance: 150000.50, LoyaltyPoints: 137"

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
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
let ``BindModelAsync with JSON content returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx None

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModelAsync<Customer>()
                return! text (model.ToString()) next ctx
            }

    let app = route "/auto" >=> autoHandler

    let contentType = "application/json"
    let postContent = "{ \"name\": \"John Doe\", \"isVip\": true, \"birthDate\": \"1990-04-20\", \"balance\": 150000.5, \"loyaltyPoints\": 137 }"
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
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``BindModelAsync with JSON content that uses custom serialization settings returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx (Some (JsonSerializerSettings()))

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModelAsync<Customer>()
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
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``BindModelAsync with XML content returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()
    mockXml ctx

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModelAsync<Customer>()
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
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``BindModelAsync with FORM content returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModelAsync<Customer>()
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
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``BindModelAsync with culture aware form content returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModelAsync<Customer>(CultureInfo.CreateSpecificCulture("en-GB"))
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
            "BirthDate", StringValues("04/01/2015 05:45:00")
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

    let expected = "Name: John Doe, IsVip: true, BirthDate: 2015-01-04, Balance: 150000.50, LoyaltyPoints: 137"

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``BindModelAsync with JSON content and a specific charset returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx None

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModelAsync<Customer>()
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
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``BindModelAsync during HTTP GET request with query string returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModelAsync<Customer>()
                return! text (model.ToString()) next ctx
            }

    let app = route "/auto" >=> autoHandler

    let queryStr = "?Name=John%20Doe&IsVip=true&BirthDate=1990-04-20&Balance=150000.5&LoyaltyPoints=137"
    let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 1990-04-20, Balance: 150000.50, LoyaltyPoints: 137"

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``BindModelAsync during HTTP GET request with culture aware query string returns correct result`` () =
    let ctx = Substitute.For<HttpContext>()

    let autoHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindModelAsync<Customer>(CultureInfo.CreateSpecificCulture("en-GB"))
                return! text (model.ToString()) next ctx
            }

    let app = route "/auto" >=> autoHandler

    let queryStr = "?Name=John%20Doe&IsVip=true&BirthDate=15/06/2013 06:00:00&Balance=150000.5&LoyaltyPoints=137"
    let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/auto")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "Name: John Doe, IsVip: true, BirthDate: 2013-06-15, Balance: 150000.50, LoyaltyPoints: 137"

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }


[<Fact>]
let ``TryGetRequestHeader during HTTP GET request with returns correct result`` () =
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
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``TryGetQueryStringValue during HTTP GET request with query string returns correct result`` () =
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
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/test")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = "1990-04-20"

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``RenderHtmlAsync should add html to the context`` () =
    let ctx = Substitute.For<HttpContext>()

    let testHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let htmlDoc =
                html [] [
                    head [] []
                    body [] [
                        h1 [] [ EncodedText "Hello world" ]
                    ]
                ]
            ctx.RenderHtmlAsync(htmlDoc)

    let app = route "/" >=> testHandler

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    let expected = sprintf "<!DOCTYPE html>%s<html><head></head><body><h1>Hello world</h1></body></html>" Environment.NewLine

    task {
        let! result = app (Some >> Task.FromResult) ctx

        match result with
        | None -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

let resultOfTask<'T> (task:Task<'T>) =
    task.Result

[<Fact>]
let ``ReturnHtmlFileAsync should return html from content folder`` () =
    let testHandler : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            ctx.ReturnHtmlFileAsync "index.html"

    let webApp = route "/" >=> testHandler

    let configureApp (app : IApplicationBuilder) =
        app
           .UseStaticFiles()
           .UseGiraffe webApp

    let host =
        WebHostBuilder()
            .UseContentRoot(Path.GetFullPath("webroot"))
            .Configure(Action<IApplicationBuilder> configureApp)

    use server = new TestServer(host)
    use client = server.CreateClient()

    let expectedContent =
        Path.Combine("webroot", "index.html")
        |> File.ReadAllText

    let actualContent =
        client.GetStringAsync "/"
        |> resultOfTask

    Assert.Equal(expectedContent, actualContent)