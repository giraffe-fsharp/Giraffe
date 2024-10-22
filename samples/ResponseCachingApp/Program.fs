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

let dateTimeHandler: HttpHandler =
    fun (_next: HttpFunc) (ctx: HttpContext) ->
        let query1 = ctx.GetQueryStringValue("query1")
        let query2 = ctx.GetQueryStringValue("query2")

        let now = expensiveOperation ()
        setStatusCode 200 |> ignore // for testing purposes

        match (query1, query2) with
        | Ok q1, Ok q2 -> ctx.WriteTextAsync $"Parameters: query1 {q1} query2 {q2} -> DateTime: {now}"
        | _ -> ctx.WriteTextAsync $"Hello World -> DateTime: {now}"

let responseCachingMiddleware: HttpHandler =
    responseCaching
        (Public(TimeSpan.FromSeconds(float 30)))
        (Some "Accept, Accept-Encoding")
        (Some [| "query1"; "query2" |])

let endpoints: Endpoint list =
    [
        subRoute "/cached" [] [
            GET [
                route "/public" [] (publicResponseCaching 30 None >=> dateTimeHandler)
                route "/private" [] (privateResponseCaching 30 None >=> dateTimeHandler)
                route "/not" [] (noResponseCaching >=> dateTimeHandler)
                route "/vary/not" [] (publicResponseCaching 30 None >=> dateTimeHandler)
                route "/vary/yes" [] (responseCachingMiddleware >=> dateTimeHandler)
            ]
        ]
    ]

let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

let configureServices (services: IServiceCollection) =
    services.AddRouting().AddResponseCaching().AddGiraffe() |> ignore

let configureApp (appBuilder: IApplicationBuilder) =
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
