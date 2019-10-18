open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.EndpointRouting

let handler1 : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteTextAsync "Hello World"

let handler2 (firstName : string, age : int) : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        sprintf "Hello %s, you are %i years old." firstName age
        |> ctx.WriteTextAsync

let handler3 (a : string, b : string, c : string, d : int) : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        sprintf "Hello %s %s %s %i" a b c d
        |> ctx.WriteTextAsync

let routes =
    [
        yield route "/" handler1
        yield routef "/%s/%i" handler2
        yield routef "/%s/%s/%s/%i" handler3
        yield! subRoute "/sub" [
            route "/test" handler1
        ]
    ]

let configureApp (app : IApplicationBuilder) =
    app.UseRouter(fun r -> r.MapGiraffe(routes))
    |> ignore

let configureServices (services : IServiceCollection) =
    services
        .AddRouting()
        .AddGiraffe()
    |> ignore

[<EntryPoint>]
let main args =
    WebHost
        .CreateDefaultBuilder(args)
        .UseKestrel()
        .Configure(configureApp)
        .ConfigureServices(configureServices)
        .Build()
        .Run()
    0
