open System
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Giraffe.EndpointRouting

let handler1 source : HttpHandler =
    fun (_ : HttpFunc) ->
        fun (ctx : HttpContext) ->
            ctx.WriteTextAsync "Hello World"
        |> source

let handler2 (firstName : string, age : int) source : HttpHandler =
    fun (_ : HttpFunc) ->
        fun (ctx : HttpContext) ->
            sprintf "Hello %s, you are %i years old." firstName age
            |> ctx.WriteTextAsync
        |> source

let handler3 (a : string, b : string, c : string, d : int) source : HttpHandler =
    fun (_ : HttpFunc) ->
        fun (ctx : HttpContext) ->
            sprintf "Hello %s %s %s %i" a b c d
            |> ctx.WriteTextAsync
        |> source
let endpoints =
    [
        subRoute "/foo" [
            GET [
                route "/bar" (text "Aloha!")
            ]
        ]
        GET [
            route  "/" (text "Hello World")
            routef "/%s/%i" handler2
            routef "/%s/%s/%s/%i" handler3
        ]
        GET_HEAD [
            route "/foo" (text "Bar")
            route "/x"   (text "y")
            route "/abc" (text "def")
        ]
        // Not specifying a http verb means it will listen to all verbs
        subRoute "/sub" [
            route "/test" handler1
        ]
    ]

let notFoundHandler : HttpHandler =
    TEXT "Not Found"
    |> RequestErrors.notFound

let configureApp (appBuilder : IApplicationBuilder) =
    appBuilder
        .UseRouting()
        .UseGiraffe(endpoints)
        .UseGiraffe(notFoundHandler)

let configureServices (services : IServiceCollection) =
    services
        .AddRouting()
        .AddGiraffe()
    |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services

    let app = builder.Build()

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    configureApp app
    app.Run()

    0
