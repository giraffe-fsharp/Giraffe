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
open FSharp.Control.Tasks.V2.ContextInsensitive
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
        Numbers    : int list option
    }
    override this.ToString() =
        let formatNicknames() =
            match this.Numbers with
            | None       -> "--"
            | Some items ->
                items
                |> List.fold (
                    fun state i ->
                        if String.IsNullOrEmpty state
                        then i.ToString()
                        else sprintf "%s, %i" state i
                    ) ""

        sprintf "Name: %s %s; Sex: %s; Numbers: %s"
            this.FirstName
            this.LastName
            (this.Sex.ToString())
            (formatNicknames())

// ---------------------------------
// ModelParser.tryParse Tests
// ---------------------------------

[<Fact>]
let ``ModelParser.tryParse with complete model data`` () =
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
    let result = ModelParser.tryParse<Model> culture modelData
    match result with
    | Ok model  -> Assert.Equal(expected, model)
    | Error err -> assertFailf "Model didn't bind successfully: %s." err

[<Fact>]
let ``ModelParser.tryParse with model data without optional parameters`` () =
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
    let result = ModelParser.tryParse<Model> culture modelData
    match result with
    | Ok model  -> Assert.Equal(expected, model)
    | Error err -> assertFailf "Model didn't bind successfully: %s." err

[<Fact>]
let ``ModelParser.tryParse with complete model data but mixed casing`` () =
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
    let result = ModelParser.tryParse<Model> culture modelData
    match result with
    | Ok model  -> Assert.Equal(expected, model)
    | Error err -> assertFailf "Model didn't bind successfully: %s." err

[<Fact>]
let ``ModelParser.tryParse with incomplete model data`` () =
    let modelData =
        dict [
            "FirstName" , StringValues "Susan"
            "MiddleName", StringValues "Elisabeth"
            "Sex"       , StringValues "Female"
            "BirthDate" , StringValues "1986-12-29"
            "Nicknames" , StringValues [| "Susi"; "Eli"; "Liz" |]
        ]
    let culture = None
    let result = ModelParser.tryParse<Model> culture modelData
    match result with
    | Ok _      -> assertFail "Model had incomplete data and should have not bound successfully."
    | Error err -> Assert.Equal("Missing value for required property Id.", err)

[<Fact>]
let ``ModelParser.tryParse with complete model data but wrong union case`` () =
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
    let result = ModelParser.tryParse<Model> culture modelData
    match result with
    | Ok _      -> assertFail "Model had incomplete data and should have not bound successfully."
    | Error err -> Assert.Equal("The value 'wrong' is not a valid case for type Giraffe.Tests.ModelBindingTests+Sex.", err)

[<Fact>]
let ``ModelParser.tryParse with complete model data but wrong data`` () =
    let id = Guid.NewGuid()
    let modelData =
        dict [
            "Id"        , StringValues (id.ToString())
            "FirstName" , StringValues "Susan"
            "MiddleName", StringValues "Elisabeth"
            "LastName"  , StringValues "Doe"
            "Sex"       , StringValues "Female"
            "BirthDate" , StringValues "wrong"
            "Nicknames" , StringValues [| "Susi"; "Eli"; "Liz" |]
        ]
    let culture = None
    let result = ModelParser.tryParse<Model> culture modelData
    match result with
    | Ok _      -> assertFail "Model had incomplete data and should have not bound successfully."
    | Error err -> Assert.Equal("Could not parse value 'wrong' to type System.DateTime.", err)

// ---------------------------------
// ModelParser.parse Tests
// ---------------------------------

[<Fact>]
let ``ModelParser.parse with complete model data`` () =
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
    let result = ModelParser.parse<Model> culture modelData
    Assert.Equal(expected, result)

[<Fact>]
let ``ModelParser.parse with model data without optional parameters`` () =
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
    let result = ModelParser.parse<Model> culture modelData
    Assert.Equal(expected, result)

[<Fact>]
let ``ModelParser.parse with complete model data but mixed casing`` () =
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
    let result = ModelParser.parse<Model> culture modelData
    Assert.Equal(expected, result)

[<Fact>]
let ``ModelParser.parse with incomplete model data`` () =
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
    let result = ModelParser.parse<Model> culture modelData
    Assert.Equal(expected, result)

[<Fact>]
let ``ModelParser.parse with incomplete model data and wrong union case`` () =
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
    let result = ModelParser.parse<Model> culture modelData
    Assert.Equal(expected.Id, result.Id)
    Assert.Equal(expected.FirstName, result.FirstName)
    Assert.Equal(expected.MiddleName, result.MiddleName)
    Assert.Null(result.LastName)
    Assert.Null(result.Sex)
    Assert.Equal(expected.BirthDate, result.BirthDate)
    Assert.Equal(expected.Nicknames, result.Nicknames)

[<Fact>]
let ``ModelParser.parse with complete model data but wrong data`` () =
    let modelData =
        dict [
            "FirstName" , StringValues "Susan"
            "MiddleName", StringValues "Elisabeth"
            "Sex"       , StringValues "wrong"
            "BirthDate" , StringValues "wrong"
            "Nicknames" , StringValues [| "Susi"; "Eli"; "Liz" |]
        ]
    let expected =
        {
            Id         = Guid.Empty
            FirstName  = "Susan"
            MiddleName = Some "Elisabeth"
            LastName   = null
            Sex        = Unchecked.defaultof<Sex>
            BirthDate  = Unchecked.defaultof<DateTime>
            Nicknames  = Some [ "Susi"; "Eli"; "Liz" ]
        }
    let culture = None
    let result = ModelParser.parse<Model> culture modelData
    Assert.Equal(expected.Id, result.Id)
    Assert.Equal(expected.FirstName, result.FirstName)
    Assert.Equal(expected.MiddleName, result.MiddleName)
    Assert.Null(result.LastName)
    Assert.Null(result.Sex)
    Assert.Equal(expected.BirthDate, DateTime.MinValue)
    Assert.Equal(expected.Nicknames, result.Nicknames)

// ---------------------------------
// TryBindQueryString Tests
// ---------------------------------

[<Fact>]
let ``tryBindQuery with complete data and list items with []`` () =
    let ctx = Substitute.For<HttpContext>()

    let parsingErrorHandler err = RequestErrors.badRequest (text err)
    let bindQuery = tryBindQuery<QueryModel> parsingErrorHandler None
    let app =
        GET >=> choose [
            route "/query" >=> bindQuery (fun m -> text(m.ToString()))
            setStatusCode 404 >=> text "Not found"
        ]

    let expected = "Name: John Doe; Sex: Male; Numbers: 5, 3, 2"
    let queryStr = "?firstName=John&lastName=Doe&sex=male&numbers[]=5&numbers[]=3&numbers[]=2"

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

    let parsingErrorHandler err = RequestErrors.badRequest (text err)
    let bindQuery = tryBindQuery<QueryModel> parsingErrorHandler None
    let app =
        GET >=> choose [
            route "/query" >=> bindQuery (fun m -> text(m.ToString()))
            setStatusCode 404 >=> text "Not found"
        ]

    let expected = "Name: John Doe; Sex: Male; Numbers: 7, 9, 0"
    let queryStr = "?firstName=John&lastName=Doe&sex=male&numbers=7&numbers=9&numbers=0"

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

    let parsingErrorHandler err = RequestErrors.badRequest (text err)
    let bindQuery = tryBindQuery<QueryModel> parsingErrorHandler None
    let app =
        GET >=> choose [
            route "/query" >=> bindQuery (fun m -> text(m.ToString()))
            setStatusCode 404 >=> text "Not found"
        ]

    let expected = "Name: John Doe; Sex: Male; Numbers: --"
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

    let parsingErrorHandler err = RequestErrors.badRequest (text err)
    let bindQuery = tryBindQuery<QueryModel> parsingErrorHandler None
    let app =
        GET >=> choose [
            route "/query" >=> bindQuery (fun m -> text(m.ToString()))
            setStatusCode 404 >=> text "Not found"
        ]

    let expected = "Missing value for required property FirstName."
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

[<Fact>]
let ``tryBindQuery with complete data but baldy formated list items`` () =
    let ctx = Substitute.For<HttpContext>()

    let parsingErrorHandler err = RequestErrors.badRequest (text err)
    let bindQuery = tryBindQuery<QueryModel> parsingErrorHandler None
    let app =
        GET >=> choose [
            route "/query" >=> bindQuery (fun m -> text(m.ToString()))
            setStatusCode 404 >=> text "Not found"
        ]

    let expected = "Could not parse value 'wrong' to type System.Int32."
    let queryStr = "?firstName=John&lastName=Doe&sex=male&numbers[]=7&numbers[]=wrong&numbers[]=0"

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

[<Theory>]
[<MemberData("PreserveCaseData", MemberType = typedefof<JsonSerializersData>)>]
let ``BindJsonAsync test`` (settings) =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx settings

    let outputCustomer (c : Customer) = text (c.ToString())
    let app =
        POST
        >=> route "/json"
        >=> bindJson<Customer> outputCustomer

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

    let outputCustomer (c : Customer) = text (c.ToString())
    let app =
        POST
        >=> route "/xml"
        >=> bindXml<Customer> outputCustomer

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

    let outputCustomer (c : Customer) = text (c.ToString())
    let app =
        POST
        >=> route "/form"
        >=> bindForm<Customer> None outputCustomer

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

[<Theory>]
[<MemberData("DefaultData", MemberType = typedefof<JsonSerializersData>)>]
let ``BindModelAsync with JSON content returns correct result`` (settings) =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx settings

    let outputCustomer (c : Customer) = text (c.ToString())
    let app =
        route "/auto"
        >=> bindModel<Customer> None outputCustomer

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

[<Theory>]
[<MemberData("PreserveCaseData", MemberType = typedefof<JsonSerializersData>)>]
let ``BindModelAsync with JSON content that uses custom serialization settings returns correct result`` (settings) =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx settings

    let outputCustomer (c : Customer) = text (c.ToString())
    let app =
        route "/auto"
        >=> bindModel<Customer> None outputCustomer

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

    let outputCustomer (c : Customer) = text (c.ToString())
    let app =
        route "/auto"
        >=> bindModel<Customer> None outputCustomer

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

    let outputCustomer (c : Customer) = text (c.ToString())
    let app =
        route "/auto"
        >=> bindModel<Customer> None outputCustomer

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

    let outputCustomer (c : Customer) = text (c.ToString())
    let english = CultureInfo.CreateSpecificCulture("en-GB")
    let app =
        route "/auto"
        >=> bindModel<Customer> (Some english) outputCustomer

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

[<Theory>]
[<MemberData("PreserveCaseData", MemberType = typedefof<JsonSerializersData>)>]
let ``BindModelAsync with JSON content and a specific charset returns correct result`` (settings) =
    let ctx = Substitute.For<HttpContext>()
    mockJson ctx settings

    let outputCustomer (c : Customer) = text (c.ToString())
    let app =
        route "/auto"
        >=> bindModel<Customer> None outputCustomer

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

    let outputCustomer (c : Customer) = text (c.ToString())
    let app =
        route "/auto"
        >=> bindModel<Customer> None outputCustomer

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

    let outputCustomer (c : Customer) = text (c.ToString())
    let english = CultureInfo.CreateSpecificCulture("en-GB")
    let app =
        route "/auto"
        >=> bindModel<Customer> (Some english) outputCustomer

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