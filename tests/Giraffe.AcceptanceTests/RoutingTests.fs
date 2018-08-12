module Giraffe.Tests.RoutingTests

open System
open System.Net
open System.Net.Http
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Xunit
open Giraffe

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

let webApp : HttpHandler =
    choose [
        GET >=>
            choose [
                route  "/"           >=> text "index"
                route  "/ping"       >=> text "pong"
                routef "/encode/%s"  text
            ]
        RequestErrors.notFound (text "Not Found") ]

let configureApp (app : IApplicationBuilder) =
    app.UseGiraffeErrorHandler(errorHandler)
       .UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore

// ---------------------------------
// Helper functions
// ---------------------------------

let createHost() =
    WebHostBuilder()
        .UseContentRoot(Directory.GetCurrentDirectory())
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(Action<IServiceCollection> configureServices)

let runTask task =
    task
    |> Async.AwaitTask
    |> Async.RunSynchronously

let get (client : HttpClient) (path : string) =
    path
    |> client.GetAsync
    |> runTask

let createRequest (method : HttpMethod) (path : string) =
    let url = "http://127.0.0.1" + path
    new HttpRequestMessage(method, url)

let makeRequest (client : HttpClient) (request : HttpRequestMessage) =
    use server = new TestServer(createHost())
    use client = server.CreateClient()
    request
    |> client.SendAsync
    |> runTask

let ensureSuccess (response : HttpResponseMessage) =
    if not response.IsSuccessStatusCode
    then response.Content.ReadAsStringAsync() |> runTask |> failwithf "%A"
    else response

let isStatus (code : HttpStatusCode) (response : HttpResponseMessage) =
    Assert.Equal(code, response.StatusCode)
    response

let isOfType (contentType : string) (response : HttpResponseMessage) =
    Assert.Equal(contentType, response.Content.Headers.ContentType.MediaType)
    response

let readText (response : HttpResponseMessage) =
    response.Content.ReadAsStringAsync()
    |> runTask

let shouldEqual expected actual =
    Assert.Equal(expected, actual)

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``Test url decode`` () =
    use server = new TestServer(createHost())
    use client = server.CreateClient()
    get client "/encode/a%2Fb%2Bc.d%2Ce"
    |> readText
    |> shouldEqual "a/b+c.d,e"