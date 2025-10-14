module Giraffe.Tests.XmlTests

open System
open System.IO
open System.Text
open System.Net.Http
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.DependencyInjection
open Xunit
open NSubstitute
open Giraffe

// ---------------------------------
// XML Tests
// ---------------------------------

[<CLIMutable>]
type MyXmlRecord =
    {
        Foo: string
        Bar: string
        Baz: float
    }

    static member Empty = { Foo = null; Bar = null; Baz = 0.0 }

let private dictForDefaultSerializer =
    [
        {
            Foo = "hello"
            Bar = "world"
            Baz = 12.5
        }
        {
            Foo = "hello"
            Bar = null
            Baz = 12.5
        }
        {
            Foo = "hello"
            Bar = null
            Baz = 12.5
        }
        {
            Foo = "hello"
            Bar = "world"
            Baz = 0.0
        }
        { Foo = "hello"; Bar = null; Baz = 0.0 }
        { Foo = null; Bar = null; Baz = 0.0 }
        { Foo = null; Bar = null; Baz = 0.0 }
    ]
    |> List.mapi (fun i r -> i, r)
    |> dict

let private xmlParserHandler (expected: MyXmlRecord) : HttpHandler =
    fun next (ctx: HttpContext) ->
        task {
            try
                let! model = ctx.BindXmlAsync<MyXmlRecord>()

                match model with
                | m when m.Foo = expected.Foo && m.Bar = expected.Bar && m.Baz = expected.Baz ->
                    return! (setStatusCode 201 >=> xml model) next ctx
                | _ ->
                    return!
                        (setStatusCode 400
                         >=> xml
                                 {|
                                     Error = "Expected is different from actual"
                                 |})
                            next
                            ctx
            with ex ->
                return! (setStatusCode 500 >=> xml {| Error = ex.Message |}) next ctx
        }

// ----------------------------------------------------------
// Using the default XML serializer with Giraffe's internal routing mechanism

[<Theory>]
[<InlineData("""<MyXmlRecord><Foo>hello</Foo><Bar>world</Bar><Baz>12.5</Baz></MyXmlRecord>""", 0)>]
[<InlineData("""<MyXmlRecord><Foo>hello</Foo><Baz>12.5</Baz></MyXmlRecord>""", 1)>]
[<InlineData("""<MyXmlRecord><Foo>hello</Foo><Baz>12.5</Baz></MyXmlRecord>""", 2)>]
[<InlineData("""<MyXmlRecord><Foo>hello</Foo><Bar>world</Bar><Baz>0</Baz></MyXmlRecord>""", 3)>]
[<InlineData("""<MyXmlRecord><Foo>hello</Foo><Baz>0</Baz></MyXmlRecord>""", 4)>]
[<InlineData("""<MyXmlRecord><Baz>0</Baz></MyXmlRecord>""", 5)>]
[<InlineData("""<MyXmlRecord><Baz>0</Baz></MyXmlRecord>""", 6)>]
let ``xml parsing works properly`` (reqBody: string, expectedDictKey: int) =
    task {
        let expected = dictForDefaultSerializer.[expectedDictKey]

        let app = choose [ POST >=> route "/parse-xml" >=> xmlParserHandler expected ]

        let ctx = Substitute.For<HttpContext>()
        mockXml ctx

        let stream = new MemoryStream()
        let writer = new StreamWriter(stream, Text.Encoding.UTF8)
        writer.Write reqBody
        writer.Flush()
        stream.Position <- 0L

        ctx.Request.Method.ReturnsForAnyArgs "POST" |> ignore
        ctx.Request.Path.ReturnsForAnyArgs(PathString "/parse-xml") |> ignore
        ctx.Response.Body <- new MemoryStream()
        ctx.Request.Body <- stream

        let! result = app next ctx

        Assert.Equal(true, result.IsSome)
        Assert.Equal(201, ctx.Response.StatusCode)
    }

// ----------------------------------------------------------
// Using the default XML serializer with endpoint routing

open Giraffe.EndpointRouting

[<Theory>]
[<InlineData("""<MyXmlRecord><Foo>hello</Foo><Bar>world</Bar><Baz>12.5</Baz></MyXmlRecord>""", 0)>]
[<InlineData("""<MyXmlRecord><Foo>hello</Foo><Baz>12.5</Baz></MyXmlRecord>""", 1)>]
[<InlineData("""<MyXmlRecord><Foo>hello</Foo><Baz>12.5</Baz></MyXmlRecord>""", 2)>]
[<InlineData("""<MyXmlRecord><Foo>hello</Foo><Bar>world</Bar><Baz>0</Baz></MyXmlRecord>""", 3)>]
[<InlineData("""<MyXmlRecord><Foo>hello</Foo><Baz>0</Baz></MyXmlRecord>""", 4)>]
[<InlineData("""<MyXmlRecord><Baz>0</Baz></MyXmlRecord>""", 5)>]
[<InlineData("""<MyXmlRecord><Baz>0</Baz></MyXmlRecord>""", 6)>]
let ``xml parsing works properly with endpoint routing`` (reqBody: string, expectedDictKey: int) =
    task {
        let expected = dictForDefaultSerializer.[expectedDictKey]

        let endpoints: Endpoint list =
            [ POST [ route "/xml-parser" (xmlParserHandler expected) ] ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services.AddRouting().AddGiraffe() |> ignore

        let request = createRequest HttpMethod.Post "/xml-parser"
        request.Content <- new StringContent(reqBody, Encoding.UTF8, "application/xml")

        let! response = makeRequest (fun () -> configureApp) configureServices () request

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode)
    }
