// Learn more about F# at http://fsharp.org

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open AspNetCore.Lambda.HttpHandlers
open AspNetCore.Lambda.Middleware

let testHandler =
    fun ctx ->
        ctx.Logger.LogCritical("Something critical")
        ctx.Logger.LogInformation(ctx.Environment.EnvironmentName)
        async.Return (Some ctx)

let webApp = 
    choose [
        route "/"       >>= text "index"
        route "/ping"   >>= text "pong"
        route "/test"   >>= testHandler >>= text "test" ]

type Startup() =
    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =
        loggerFactory.AddConsole().AddDebug() |> ignore
        app.UseLambda(webApp)


[<EntryPoint>]
let main argv = 
    let host =
        WebHostBuilder()
            .UseKestrel()
            .UseContentRoot(Directory.GetCurrentDirectory())
            .UseStartup<Startup>()
            .Build()
    host.Run()
    0