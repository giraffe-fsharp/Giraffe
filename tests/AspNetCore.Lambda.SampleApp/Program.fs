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

let clear =
    fun ctx ->
        ctx.HttpContext.Response.Clear()
        async.Return (Some ctx)

let webApp = 
    choose [
        route "/"       >>= text "index"
        route "/ping"   >>= text "pong"
        route "/test"   >>= testHandler >>= text "test"
        route "/error"  >>= (fun _ -> failwith "Error OMG what happened!?") ]

let errorHandler (ex : Exception) (ctx : HttpHandlerContext) =
    ctx.Logger.LogError(EventId(0), ex, "An unhandled exception has occurred while executing the request")
    ctx |> (clear >>= setStatusCode 500 >>= text ex.Message)

type Startup() =
    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =
        loggerFactory.AddConsole().AddDebug() |> ignore
        app.UseErrorHandler(errorHandler)
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