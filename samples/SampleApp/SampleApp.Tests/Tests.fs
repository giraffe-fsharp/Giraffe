module Tests

open System
open System.Net
open System.Net.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.TestHost;
open AspNetCore.Lambda.HttpHandlers
open AspNetCore.Lambda.Middleware
open Xunit

let runTask fa = 
    fa 
    |> Async.AwaitTask
    |> Async.RunSynchronously

let host = 
    WebHostBuilder()
        .UseContentRoot(System.IO.Directory.GetCurrentDirectory())
        .Configure(Action<IApplicationBuilder> SampleApp.configureApp)
        .ConfigureServices(Action<IServiceCollection> SampleApp.configureServices)

let uri = Uri("http://127.0.0.1:5000")

let server = new TestServer(host)

let createClient = 
    let client = server.CreateClient()
    client.BaseAddress <- uri
    client

let getWith (client: HttpClient) (path: string) =
    client.GetAsync(path)
    |> runTask

let req (client: HttpClient) (request: HttpRequestMessage) =
    client.SendAsync(request)
    |> runTask

let get (path: string) = 
    use client = server.CreateClient()
    getWith client path

let readText (response: Http.HttpResponseMessage) = 
    response.Content.ReadAsStringAsync()
    |> runTask

let readStatus (response: Http.HttpResponseMessage) = 
    response.StatusCode

let readStatusAndText (response: Http.HttpResponseMessage) = 
    let status = readStatus response
    let text = readText response
    (status, text)

[<Fact>]
let ``Test / is routed to index`` () =
    let response = 
        get "/"
        |> readText

    Assert.Equal("index", response)

[<Fact>]
let ``Test /error returns status code 500`` () = 
    let (status, text) = 
        get "/error"
        |> readStatusAndText

    Assert.Equal(HttpStatusCode.InternalServerError, status)
    Assert.Equal("One or more errors occurred. (Something went wrong!)", text)

[<Fact>]
let ``Test /user returns error when not logged in`` () = 
    let (status, text) = 
        get "/user"
        |> readStatusAndText

    Assert.Equal(HttpStatusCode.Unauthorized, status)
    Assert.Equal("Access Denied", text)

[<Fact>]
let ``Test /user/{id} returns success when logged in as user`` () = 
    
    use client = server.CreateClient()
    let get = getWith client

    let loginResult = 
        get "/login"

    Assert.Equal(HttpStatusCode.OK, loginResult.StatusCode)

    //https://github.com/aspnet/Hosting/issues/191
    //The session cookie is not automatically forwarded to the next request. To overcome this we have to manually do:
    let request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1/user/1");
    request.Headers.Add("Cookie", loginResult.Headers.GetValues("Set-Cookie"));

    let (status, text) = 
        req client request
        |> readStatusAndText

    Assert.Equal(HttpStatusCode.OK, status)
    Assert.Equal("User ID: 1", text)