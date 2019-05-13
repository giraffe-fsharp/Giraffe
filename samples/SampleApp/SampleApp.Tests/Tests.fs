module SampleApp.Tests

open System
open System.Net
open System.Net.Http
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks.V2.ContextInsensitive
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

let get (client : HttpClient) (path : string) =
    path
    |> client.GetAsync

let createRequest (method : HttpMethod) (path : string) =
    let url = "http://127.0.0.1" + path
    new HttpRequestMessage(method, url)

let addCookiesFromResponse (response : HttpResponseMessage)
                           (request  : HttpRequestMessage) =
    request.Headers.Add("Cookie", response.Headers.GetValues("Set-Cookie"))
    request

let makeRequest (client : HttpClient) request =
    request
    |> client.SendAsync

let isStatus (code : HttpStatusCode) (response : HttpResponseMessage) =
    Assert.Equal(code, response.StatusCode)
    response

let isOfType (contentType : string) (response : HttpResponseMessage) =
    Assert.Equal(contentType, response.Content.Headers.ContentType.MediaType)
    response

let readText (response : HttpResponseMessage) =
    response.Content.ReadAsStringAsync()

let shouldEqual expected actual =
    Assert.Equal(expected, actual)

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``Test / is routed to index`` () =
    task {
        use server = new TestServer(createHost())
        use client = server.CreateClient()
        let! response = get client "/"
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> readText
        content
        |> shouldEqual "index"
    }

[<Fact>]
let ``Test /error returns status code 500`` () =
    task {
        use server = new TestServer(createHost())
        use client = server.CreateClient()
        let! response = get client "/error"
        let! content =
            response
            |> isStatus HttpStatusCode.InternalServerError
            |> readText
        content
        |> shouldEqual "Something went wrong!"
    }

[<Fact>]
let ``Test /user returns error when not logged in`` () =
    task {
        use server = new TestServer(createHost())
        use client = server.CreateClient()
        let! response = get client "/user"
        let! content =
            response
            |> isStatus HttpStatusCode.Unauthorized
            |> readText
        content
        |> shouldEqual "Access Denied"
    }

[<Fact>]
let ``Test /user/{id} returns success when logged in as user`` () =
    task {
        use server = new TestServer(createHost())
        use client = server.CreateClient()
        let! response = get client "/login"
        let! content =
            response
            |> isStatus HttpStatusCode.OK
            |> readText
        content
        |> shouldEqual "Successfully logged in"

        // https://github.com/aspnet/Hosting/issues/191
        // The session cookie is not automatically forwarded to the next request.
        // To overcome this we have to manually do:
        let! response' =
            "/user/1"
            |> createRequest HttpMethod.Get
            |> addCookiesFromResponse response
            |> makeRequest client
        let! content' =
            response'
            |> isStatus HttpStatusCode.OK
            |> readText
        content'
        |> shouldEqual "User ID: 1"
    }