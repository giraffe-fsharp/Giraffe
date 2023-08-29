open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Giraffe.EndpointRouting

let expensiveOperation () : DateTime =
    let fiveSeconds = 5000 // ms
    Threading.Thread.Sleep fiveSeconds
    DateTime.Now

let dateTimeHandler : HttpHandler =
    fun (_next : HttpFunc) (ctx : HttpContext) ->
        let now = expensiveOperation ()
        ctx.WriteTextAsync $"Hello World -> DateTime: {now}"

let endpoints: Endpoint list =
    [
        subRoute "/cached" [
            GET [
                route "/public" (publicResponseCaching 30 None >=> dateTimeHandler)
                route "/private" (privateResponseCaching 30 None >=> dateTimeHandler)
                route "/not" (noResponseCaching >=> dateTimeHandler)
            ]
        ]
    ]

let notFoundHandler =
    "Not Found"
    |> text
    |> RequestErrors.notFound

let configureServices (services : IServiceCollection) =
    services
        .AddRouting()
        .AddResponseCaching()
        .AddGiraffe()
    |> ignore

let configureApp (appBuilder : IApplicationBuilder) =
    appBuilder
        .UseRouting()
        .UseResponseCaching()
        .UseEndpoints(fun e -> e.MapGiraffeEndpoints(endpoints))
        .UseGiraffe(notFoundHandler)

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
