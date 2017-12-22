module SampleApp.Tests

open System
open System.Net
open System.Net.Http
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Xunit

// ---------------------------------
// Test server/client setup
// ---------------------------------

let createHost() =
    WebHostBuilder()
        .UseContentRoot(Directory.GetCurrentDirectory())
        .Configure(Action<IApplicationBuilder> SampleApp.App.configureApp)
        .ConfigureServices(Action<IServiceCollection> SampleApp.App.configureServices)

// ---------------------------------
// Helper functions
// ---------------------------------

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

let addCookiesFromResponse (response : HttpResponseMessage)
                           (request  : HttpRequestMessage) =
    request.Headers.Add("Cookie", response.Headers.GetValues("Set-Cookie"))
    request

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
let ``Test / is routed to index`` () =
    use server = new TestServer(createHost())
    use client = server.CreateClient()

    get client "/"
    |> ensureSuccess
    |> readText
    |> shouldEqual "index"

[<Fact>]
let ``Test /error returns status code 500`` () =
    use server = new TestServer(createHost())
    use client = server.CreateClient()

    get client "/error"
    |> isStatus HttpStatusCode.InternalServerError
    |> readText
    |> shouldEqual "Something went wrong!"

[<Fact>]
let ``Test /user returns error when not logged in`` () =
    use server = new TestServer(createHost())
    use client = server.CreateClient()

    get client "/user"
    |> isStatus HttpStatusCode.Unauthorized
    |> readText
    |> shouldEqual "Access Denied"

[<Fact>]
let ``Test /user/{id} returns success when logged in as user`` () =
    use server = new TestServer(createHost())
    use client = server.CreateClient()

    let loginResponse =
        get client "/login"
        |> ensureSuccess

    loginResponse
    |> readText
    |> shouldEqual "Successfully logged in"

    // https://github.com/aspnet/Hosting/issues/191
    // The session cookie is not automatically forwarded to the next request.
    // To overcome this we have to manually do:
    "/user/1"
    |> createRequest HttpMethod.Get
    |> addCookiesFromResponse loginResponse
    |> makeRequest client
    |> ensureSuccess
    |> readText
    |> shouldEqual "User ID: 1"