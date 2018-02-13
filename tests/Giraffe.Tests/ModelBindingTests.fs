module Giraffe.Tests.ModelBindingTests

open System
open System.Globalization
open System.Collections.Generic
open System.IO
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Internal
open Microsoft.Extensions.Primitives
open Xunit
open NSubstitute
open Newtonsoft.Json
open Giraffe

[<CLIMutable>]
type ModelWithOption =
    {
        OptionalInt: int option
        OptionalString: string option
    }

[<CLIMutable>]
type ModelWithNullable =
    {
        NullableInt      : Nullable<int>
        NullableDateTime : Nullable<DateTime>
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

type Sex =
    | Male
    | Female

[<CLIMutable>]
type Model =
    {
        Id         : Guid
        FirstName  : string
        MiddleName : string option
        LastName   : string
        Sex        : Sex
        BirthDate  : DateTime
        Nicknames  : string list option
    }

[<CLIMutable>]
type QueryModel =
    {
        FirstName  : string
        LastName   : string
        Sex        : Sex
        Nicknames  : string list option
    }
    override this.ToString() =
        let formatNicknames() =
            match this.Nicknames with
            | None       -> "--"
            | Some items ->
                items
                |> List.fold (
                    fun state i ->
                        if String.IsNullOrEmpty state
                        then i
                        else sprintf "%s, %s" state i
                    ) ""

        sprintf "Name: %s %s; Sex: %s; Nicknames: %s"
            this.FirstName
            this.LastName
            (this.Sex.ToString())
            (formatNicknames())

// ---------------------------------
// tryBindModel Tests
// ---------------------------------

[<Fact>]
let ``tryBindModel with complete model data`` () =
    let id = Guid.NewGuid()
    let modelData =
        dict [
            "Id"        , StringValues (id.ToString())
            "FirstName" , StringValues "Susan"
            "MiddleName", StringValues "Elisabeth"
            "LastName"  , StringValues "Doe"
            "Sex"       , StringValues "Female"
            "BirthDate" , StringValues "1986-12-29"
            "Nicknames" , StringValues [| "Susi"; "Eli"; "Liz" |]
        ]
    let expected =
        {
            Id         = id
            FirstName  = "Susan"
            MiddleName = Some "Elisabeth"
            LastName   = "Doe"
            Sex        = Female
            BirthDate  = DateTime(1986, 12, 29)
            Nicknames  = Some [ "Susi"; "Eli"; "Liz" ]
        }
    let culture = None
    let result = tryBindModel<Model> culture modelData
    match result with
    | Some m -> Assert.Equal(expected, m)
    | None   -> assertFail "Model didn't bind successfully."

[<Fact>]
let ``tryBindModel with model data without optional parameters`` () =
    let id = Guid.NewGuid()
    let modelData =
        dict [
            "Id"        , StringValues (id.ToString())
            "FirstName" , StringValues "Susan"
            "LastName"  , StringValues "Doe"
            "Sex"       , StringValues "Female"
            "BirthDate" , StringValues "1986-12-29"
        ]
    let expected =
        {
            Id         = id
            FirstName  = "Susan"
            MiddleName = None
            LastName   = "Doe"
            Sex        = Female
            BirthDate  = DateTime(1986, 12, 29)
            Nicknames  = None
        }
    let culture = None
    let result = tryBindModel<Model> culture modelData
    match result with
    | Some m -> Assert.Equal(expected, m)
    | None   -> assertFail "Model didn't bind successfully."

[<Fact>]
let ``tryBindModel with complete model data but mixed casing`` () =
    let id = Guid.NewGuid()
    let modelData =
        dict [
            "id"        , StringValues (id.ToString())
            "firstName" , StringValues "Susan"
            "MiddleName", StringValues "Elisabeth"
            "lastname"  , StringValues "Doe"
            "Sex"       , StringValues "female"
            "BirthDate" , StringValues "1986-12-29"
            "NickNames" , StringValues [| "Susi"; "Eli"; "Liz" |]
        ]
    let expected =
        {
            Id         = id
            FirstName  = "Susan"
            MiddleName = Some "Elisabeth"
            LastName   = "Doe"
            Sex        = Female
            BirthDate  = DateTime(1986, 12, 29)
            Nicknames  = Some [ "Susi"; "Eli"; "Liz" ]
        }
    let culture = None
    let result = tryBindModel<Model> culture modelData
    match result with
    | Some m -> Assert.Equal(expected, m)
    | None   -> assertFail "Model didn't bind successfully."

[<Fact>]
let ``tryBindModel with incomplete model data`` () =
    let modelData =
        dict [
            "FirstName" , StringValues "Susan"
            "MiddleName", StringValues "Elisabeth"
            "Sex"       , StringValues "Female"
            "BirthDate" , StringValues "1986-12-29"
            "Nicknames" , StringValues [| "Susi"; "Eli"; "Liz" |]
        ]
    let culture = None
    let result = tryBindModel<Model> culture modelData
    match result with
    | Some _ -> assertFail "Model had incomplete data and should have not bound successfully."
    | None   -> ()

[<Fact>]
let ``tryBindModel with complete model data but wrong union case`` () =
    let id = Guid.NewGuid()
    let modelData =
        dict [
            "Id"        , StringValues (id.ToString())
            "FirstName" , StringValues "Susan"
            "MiddleName", StringValues "Elisabeth"
            "LastName"  , StringValues "Doe"
            "Sex"       , StringValues "wrong"
            "BirthDate" , StringValues "1986-12-29"
            "Nicknames" , StringValues [| "Susi"; "Eli"; "Liz" |]
        ]
    let culture = None
    let result = tryBindModel<Model> culture modelData
    match result with
    | Some _ -> assertFail "Model had wrong data and should have not bound successfully."
    | None   -> ()

// ---------------------------------
// bindModel Tests
// ---------------------------------

[<Fact>]
let ``bindModel with complete model data`` () =
    let id = Guid.NewGuid()
    let modelData =
        dict [
            "Id"        , StringValues (id.ToString())
            "FirstName" , StringValues "Susan"
            "MiddleName", StringValues "Elisabeth"
            "LastName"  , StringValues "Doe"
            "Sex"       , StringValues "Female"
            "BirthDate" , StringValues "1986-12-29"
            "Nicknames" , StringValues [| "Susi"; "Eli"; "Liz" |]
        ]
    let expected =
        {
            Id         = id
            FirstName  = "Susan"
            MiddleName = Some "Elisabeth"
            LastName   = "Doe"
            Sex        = Female
            BirthDate  = DateTime(1986, 12, 29)
            Nicknames  = Some [ "Susi"; "Eli"; "Liz" ]
        }
    let culture = None
    let result = bindModel<Model> culture modelData
    Assert.Equal(expected, result)

[<Fact>]
let ``bindModel with model data without optional parameters`` () =
    let id = Guid.NewGuid()
    let modelData =
        dict [
            "Id"        , StringValues (id.ToString())
            "FirstName" , StringValues "Susan"
            "LastName"  , StringValues "Doe"
            "Sex"       , StringValues "Female"
            "BirthDate" , StringValues "1986-12-29"
        ]
    let expected =
        {
            Id         = id
            FirstName  = "Susan"
            MiddleName = None
            LastName   = "Doe"
            Sex        = Female
            BirthDate  = DateTime(1986, 12, 29)
            Nicknames  = None
        }
    let culture = None
    let result = bindModel<Model> culture modelData
    Assert.Equal(expected, result)

[<Fact>]
let ``bindModel with complete model data but mixed casing`` () =
    let id = Guid.NewGuid()
    let modelData =
        dict [
            "id"        , StringValues (id.ToString())
            "firstName" , StringValues "Susan"
            "MiddleName", StringValues "Elisabeth"
            "lastname"  , StringValues "Doe"
            "Sex"       , StringValues "female"
            "BirthDate" , StringValues "1986-12-29"
            "NickNames" , StringValues [| "Susi"; "Eli"; "Liz" |]
        ]
    let expected =
        {
            Id         = id
            FirstName  = "Susan"
            MiddleName = Some "Elisabeth"
            LastName   = "Doe"
            Sex        = Female
            BirthDate  = DateTime(1986, 12, 29)
            Nicknames  = Some [ "Susi"; "Eli"; "Liz" ]
        }
    let culture = None
    let result = bindModel<Model> culture modelData
    Assert.Equal(expected, result)

[<Fact>]
let ``bindModel with incomplete model data`` () =
    let modelData =
        dict [
            "FirstName" , StringValues "Susan"
            "MiddleName", StringValues "Elisabeth"
            "Sex"       , StringValues "Female"
            "BirthDate" , StringValues "1986-12-29"
            "Nicknames" , StringValues [| "Susi"; "Eli"; "Liz" |]
        ]
    let expected =
        {
            Id         = Guid.Empty
            FirstName  = "Susan"
            MiddleName = Some "Elisabeth"
            LastName   = null
            Sex        = Female
            BirthDate  = DateTime(1986, 12, 29)
            Nicknames  = Some [ "Susi"; "Eli"; "Liz" ]
        }
    let culture = None
    let result = bindModel<Model> culture modelData
    Assert.Equal(expected, result)

[<Fact>]
let ``bindModel with incomplete model data and wrong union case`` () =
    let modelData =
        dict [
            "FirstName" , StringValues "Susan"
            "MiddleName", StringValues "Elisabeth"
            "Sex"       , StringValues "wrong"
            "BirthDate" , StringValues "1986-12-29"
            "Nicknames" , StringValues [| "Susi"; "Eli"; "Liz" |]
        ]
    let expected =
        {
            Id         = Guid.Empty
            FirstName  = "Susan"
            MiddleName = Some "Elisabeth"
            LastName   = null
            Sex        = Unchecked.defaultof<Sex>
            BirthDate  = DateTime(1986, 12, 29)
            Nicknames  = Some [ "Susi"; "Eli"; "Liz" ]
        }
    let culture = None
    let result = bindModel<Model> culture modelData
    Assert.Equal(expected.Id, result.Id)
    Assert.Equal(expected.FirstName, result.FirstName)
    Assert.Equal(expected.MiddleName, result.MiddleName)
    Assert.Null(result.LastName)
    Assert.Null(result.Sex)
    Assert.Equal(expected.BirthDate, result.BirthDate)
    Assert.Equal(expected.Nicknames, result.Nicknames)

// ---------------------------------
// TryBindQueryString Tests
// ---------------------------------

// ?firstName=John&lastName=Doe&sex=male&nicknames[]=Johnny&nicknames[]=JD&nicknames[]=Jay
// Name: John Doe; Sex: Male; Nicknames: Johnny, JD, Jay

[<Fact>]
let ``tryBindQuery with complete data and list items with []`` () =
    let ctx = Substitute.For<HttpContext>()

    let bindQuery = tryBindQuery<QueryModel> None
    let app =
        GET >=> choose [
            route "/query" >=> bindQuery (fun m -> text(m.ToString()))
            setStatusCode 404 >=> text "Not found"
        ]

    let expected = "Name: John Doe; Sex: Male; Nicknames: Johnny, JD, Jay"
    let queryStr = "?firstName=John&lastName=Doe&sex=male&nicknames[]=Johnny&nicknames[]=JD&nicknames[]=Jay"

    let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/query")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app (Some >> Task.FromResult) ctx
        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``tryBindQuery with complete data and list items without []`` () =
    let ctx = Substitute.For<HttpContext>()

    let bindQuery = tryBindQuery<QueryModel> None
    let app =
        GET >=> choose [
            route "/query" >=> bindQuery (fun m -> text(m.ToString()))
            setStatusCode 404 >=> text "Not found"
        ]

    let expected = "Name: John Doe; Sex: Male; Nicknames: Johnny, JD, Jay"
    let queryStr = "?firstName=John&lastName=Doe&sex=male&nicknames=Johnny&nicknames=JD&nicknames=Jay"

    let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/query")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app (Some >> Task.FromResult) ctx
        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``tryBindQuery without optional data`` () =
    let ctx = Substitute.For<HttpContext>()

    let bindQuery = tryBindQuery<QueryModel> None
    let app =
        GET >=> choose [
            route "/query" >=> bindQuery (fun m -> text(m.ToString()))
            setStatusCode 404 >=> text "Not found"
        ]

    let expected = "Name: John Doe; Sex: Male; Nicknames: --"
    let queryStr = "?firstName=John&lastName=Doe&sex=male"

    let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/query")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app (Some >> Task.FromResult) ctx
        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

[<Fact>]
let ``tryBindQuery with incomplete data`` () =
    let ctx = Substitute.For<HttpContext>()

    let bindQuery = tryBindQuery<QueryModel> None
    let app =
        GET >=> choose [
            route "/query" >=> bindQuery (fun m -> text(m.ToString()))
            setStatusCode 404 >=> text "Not found"
        ]

    let expected = "Not found"
    let queryStr = "?lastName=Doe&sex=male"

    let query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery queryStr
    ctx.Request.Query.ReturnsForAnyArgs(QueryCollection(query) :> IQueryCollection) |> ignore
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/query")) |> ignore
    ctx.Response.Body <- new MemoryStream()

    task {
        let! result = app (Some >> Task.FromResult) ctx
        match result with
        | None     -> assertFailf "Result was expected to be %s" expected
        | Some ctx -> Assert.Equal(expected, getBody ctx)
    }

// ---------------------------------
// HttpContext Model Binding Tests
// ---------------------------------

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
let ``BindQueryString with nullable property test`` () =
    let testRoute queryStr expected =
        let queryHandlerWithSome next (ctx : HttpContext) =
            task {
                let model = ctx.BindQueryString<ModelWithNullable>()
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
        let! _ = testRoute "?NullableInt=1&NullableDateTime=2017-09-01" { NullableInt = Nullable<_>(1); NullableDateTime = Nullable<_>(DateTime(2017,09,01)) }
        let! _ = testRoute "?" { NullableInt = Nullable<_>(); NullableDateTime = Nullable<_>() }
        return!  testRoute "?NullableInt=&NullableDateTime=" { NullableInt = Nullable<_>(); NullableDateTime = Nullable<_>() }
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