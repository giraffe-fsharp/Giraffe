open System
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Giraffe.EndpointRouting

type Car =
    {
        Brand: string
        Color: string
        ReleaseYear: int
    }

let handler1: HttpHandler =
    fun (_: HttpFunc) (ctx: HttpContext) -> ctx.WriteTextAsync "Hello World"

let handler2 (firstName: string, age: int) : HttpHandler =
    fun (_: HttpFunc) (ctx: HttpContext) ->
        sprintf "Hello %s, you are %i years old." firstName age |> ctx.WriteTextAsync

let handler3 (a: string, b: string, c: string, d: int) : HttpHandler =
    fun (_: HttpFunc) (ctx: HttpContext) -> sprintf "Hello %s %s %s %i" a b c d |> ctx.WriteTextAsync

let handlerNamed (petId: int) : HttpHandler =
    fun (_: HttpFunc) (ctx: HttpContext) -> sprintf "PetId: %i" petId |> ctx.WriteTextAsync

/// Example request:
///
/// ```bash
/// curl -v localhost:5000/json -X Post -d '{"brand":"Ford", "color":"Black", "releaseYear":2015}'
/// ```
let jsonHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            match! ctx.BindJsonAsync<Car>() with
            | {
                  Brand = _brand
                  Color = _color
                  ReleaseYear = releaseYear
              } when releaseYear >= 1990 -> return! json {| Message = "Valid car" |} next ctx
            | _ -> return! (setStatusCode 400 >=> json {| Message = "Invalid car year" |}) next ctx
        }

let endpoints =
    [
        subRoute "/foo" [ GET [ route "/bar" (text "Aloha!") ] ]
        GET [
            route "/" (text "Hello World")
            routef "/%s/%i" handler2
            routef "/%s/%s/%s/%i" handler3
            routef "/pet/%i:petId" handlerNamed
        ]
        GET_HEAD [
            route "/foo" (text "Bar")
            route "/x" (text "y")
            route "/abc" (text "def")
            route "/123" (text "456")
        ]
        // Not specifying a http verb means it will listen to all verbs
        subRoute "/sub" [ route "/test" handler1 ]
        POST [ route "/json" jsonHandler ]
    ]

let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

let configureApp (appBuilder: IApplicationBuilder) =
    appBuilder.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

let configureServices (services: IServiceCollection) =
    services.AddRouting().AddGiraffe() |> ignore

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
