[<AutoOpen>]
module Giraffe.Tests.Helpers

open System
open System.IO
open System.Net
open System.Net.Http
open System.Linq
open System.Text
open System.Threading.Tasks
open System.Xml.Linq
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Xunit
open NSubstitute
open Utf8Json
open System.Text.Json
open Newtonsoft.Json
open Giraffe

// ---------------------------------
// Common functions
// ---------------------------------

let toTheoryData xs =
    let data = new TheoryData<_>()
    for x in xs do data.Add x
    data

let toTheoryData2 xs =
    let data = new TheoryData<_,_>()
    for (a,b) in xs do data.Add(a,b)
    data

let waitForDebuggerToAttach() =
    printfn "Waiting for debugger to attach."
    printfn "Press enter when debugger is attached in order to continue test execution..."
    Console.ReadLine() |> ignore

let removeNewLines (html : string) : string =
    html.Replace(Environment.NewLine, String.Empty)

let createETag (eTag : string) =
    Some (Microsoft.Net.Http.Headers.EntityTagHeaderValue.FromString false eTag)

let createWeakETag (eTag : string) =
    Some (Microsoft.Net.Http.Headers.EntityTagHeaderValue.FromString true eTag)

// ---------------------------------
// Assert functions
// ---------------------------------

let assertFail msg = Assert.True(false, msg)

let assertFailf format args =
    let msg = sprintf format args
    Assert.True(false, msg)

module XmlAssert =
    let rec normalize (element : XElement) =
        if element.HasElements then
            XElement(
                element.Name,
                element.Attributes()
                    .Where(fun a -> a.Name.Namespace = XNamespace.Xmlns)
                    .OrderBy(fun a -> a.Name.ToString()),
                element.Elements()
                    .OrderBy(fun a -> a.Name.ToString())
                    .Select(fun e -> normalize(e))
            )
        elif element.IsEmpty then
            XElement(
                element.Name,
                element.Attributes()
                    .OrderBy(fun a -> a.Name.ToString())
              )
         else
            XElement(
                element.Name,
                element.Attributes()
                    .OrderBy(fun a -> a.Name.ToString()), element.Value
               )

    let equals expectedXml actualXml =
        let expectedXElement = XElement.Parse expectedXml |> normalize
        let actualXElement = XElement.Parse actualXml |> normalize
        Assert.Equal(expectedXElement.ToString(), actualXElement.ToString())

// ---------------------------------
// Test server/client setup
// ---------------------------------

let next : HttpFunc = Some >> Task.FromResult

let createHost (configureApp      : 'Tuple -> IApplicationBuilder -> unit)
               (configureServices : IServiceCollection -> unit)
               (args              : 'Tuple) =
    (WebHostBuilder())
        .UseContentRoot(Path.GetFullPath("TestFiles"))
        .Configure(Action<IApplicationBuilder> (configureApp args))
        .ConfigureServices(Action<IServiceCollection> configureServices)

type MockJsonSettings =
    | Newtonsoft     of JsonSerializerSettings option
    | Utf8           of IJsonFormatterResolver option
    | SystemTextJson of JsonSerializerOptions  option

let mockJson (ctx : HttpContext) (settings : MockJsonSettings) =

    match settings with
    | Newtonsoft settings ->
        let jsonSettings =
            defaultArg settings NewtonsoftJson.Serializer.DefaultSettings
        ctx.RequestServices
           .GetService(typeof<Json.ISerializer>)
           .Returns(NewtonsoftJson.Serializer(jsonSettings))
        |> ignore

    | Utf8 settings ->
        let resolver =
            defaultArg settings Utf8Json.Serializer.DefaultResolver
        ctx.RequestServices
           .GetService(typeof<Json.ISerializer>)
           .Returns(Utf8Json.Serializer(resolver))
        |> ignore

    | SystemTextJson settings ->
        let jsonOptions =
            defaultArg settings SystemTextJson.Serializer.DefaultOptions
        ctx.RequestServices
           .GetService(typeof<Json.ISerializer>)
           .Returns(SystemTextJson.Serializer(jsonOptions))
        |> ignore

type JsonSerializersData =

    static member DefaultSettings = [
            Utf8 None;
            Newtonsoft None
            SystemTextJson None
        ]

    static member DefaultData = JsonSerializersData.DefaultSettings |> toTheoryData

    static member PreserveCaseSettings =
        [
            Utf8 (Some Utf8Json.Resolvers.StandardResolver.Default)
            Newtonsoft (Some (JsonSerializerSettings()))
            SystemTextJson (Some (JsonSerializerOptions()))
        ]

    static member PreserveCaseData = JsonSerializersData.PreserveCaseSettings |> toTheoryData

type NegotiationConfigWithExpectedResult = {
    NegotiationConfig : INegotiationConfig
    StatusCode : int
    ReturnContentType : string
}

let mockXml (ctx : HttpContext) =
    ctx.RequestServices
       .GetService(typeof<Xml.ISerializer>)
       .Returns(SystemXml.Serializer(SystemXml.Serializer.DefaultSettings))
    |> ignore

let mockNegotiation (ctx : HttpContext) (negotiationConfig : INegotiationConfig) =
    ctx.RequestServices
       .GetService(typeof<INegotiationConfig>)
       .Returns(negotiationConfig)
    |> ignore

// ---------------------------------
// Compose web request functions
// ---------------------------------

let createRequest (method : HttpMethod) (path : string) =
    let url = "http://127.0.0.1" + path
    new HttpRequestMessage(method, url)

let makeRequest configureApp configureServices args (request : HttpRequestMessage) =
    task {
        use server = new TestServer(createHost configureApp configureServices args)
        use client = server.CreateClient()
        let! response = request |> client.SendAsync
        return response
    }

let addHeader (key : string) (value : string) (request : HttpRequestMessage) =
    request.Headers.Add(key, value)
    request

// ---------------------------------
// Validate response functions
// ---------------------------------

let getContentType (response : HttpResponse) =
    response.Headers.["Content-Type"].[0]

let getStatusCode (ctx : HttpContext) =
    ctx.Response.StatusCode

let isStatus (code : HttpStatusCode) (response : HttpResponseMessage) =
    Assert.Equal(code, response.StatusCode)
    response

let containsHeader (flag : bool) (name : string) (response : HttpResponseMessage) =
    match flag with
    | true  -> Assert.True(response.Headers.Contains name)
    | false -> Assert.False(response.Headers.Contains name)
    response

let containsContentHeader (flag : bool) (name : string) (response : HttpResponseMessage) =
    match flag with
    | true  -> Assert.True(response.Content.Headers.Contains name)
    | false -> Assert.False(response.Content.Headers.Contains name)
    response

let hasContentLength (length : int64) (response : HttpResponseMessage) =
    Assert.True(response.Content.Headers.ContentLength.HasValue)
    Assert.Equal(length, response.Content.Headers.ContentLength.Value)
    response

let hasAcceptRanges (value : string) (response : HttpResponseMessage) =
    Assert.Equal(value, response.Headers.AcceptRanges.ToString())
    response

let hasContentRange (value : string) (response : HttpResponseMessage) =
    Assert.Equal(value, response.Content.Headers.ContentRange.ToString())
    response

let hasETag (eTag : string) (response : HttpResponseMessage) =
    Assert.Equal(eTag, (response.Headers.ETag.ToString()))
    response

let hasLastModified (lastModified : DateTimeOffset) (response : HttpResponseMessage) =
    Assert.True(response.Content.Headers.LastModified.HasValue)
    Assert.Equal(lastModified, response.Content.Headers.LastModified.Value)
    response

let getBody (ctx : HttpContext) =
    ctx.Response.Body.Position <- 0L
    use reader = new StreamReader(ctx.Response.Body, Encoding.UTF8)
    reader.ReadToEnd()

let readText (response : HttpResponseMessage) =
    response.Content.ReadAsStringAsync()

let readBytes (response : HttpResponseMessage) =
    response.Content.ReadAsByteArrayAsync()

let printBytes (bytes : byte[]) =
    bytes |> Array.fold (
        fun (s : string) (b : byte) ->
            match s.Length with
            | 0 -> sprintf "%i" b
            | _ -> sprintf "%s,%i" s b) ""

let shouldBeEmpty (bytes : byte[]) =
    Assert.True(bytes.Length.Equals 0)

let shouldEqual expected actual =
    Assert.Equal(expected, actual)