open System
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.AspNetCore.RateLimiting
open System.Threading.RateLimiting

let MY_RATE_LIMITER = "fixed"

let endpoints: list<Endpoint> =
    [
        GET [
            routeWithExtensions "/rate-limit" [ AspNetExtension.RateLimiting MY_RATE_LIMITER ] (text "Hello World")
            route "/no-rate-limit" (text "Hello World: No Rate Limit!")
        ]
    ]

let notFoundHandler = text "Not Found" |> RequestErrors.notFound

let configureApp (appBuilder: IApplicationBuilder) =
    appBuilder
        .UseRouting()
        .UseRateLimiter()
        .UseGiraffe(endpoints)
        .UseGiraffe(notFoundHandler)

let configureServices (services: IServiceCollection) =
    // From https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-8.0#fixed-window-limiter
    let configureRateLimiter (rateLimiterOptions: RateLimiterOptions) =
        rateLimiterOptions.RejectionStatusCode <- StatusCodes.Status429TooManyRequests

        rateLimiterOptions.AddFixedWindowLimiter(
            policyName = MY_RATE_LIMITER,
            configureOptions =
                (fun (options: FixedWindowRateLimiterOptions) ->
                    options.PermitLimit <- 10
                    options.Window <- TimeSpan.FromSeconds(int64 12)
                    options.QueueProcessingOrder <- QueueProcessingOrder.OldestFirst
                    options.QueueLimit <- 1
                )
        )
        |> ignore

    services.AddRateLimiter(configureRateLimiter).AddRouting().AddGiraffe()
    |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services

    let app = builder.Build()

    configureApp app
    app.Run()

    0
