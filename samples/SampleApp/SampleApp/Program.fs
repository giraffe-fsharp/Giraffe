module SampleApp.App

open System
open System.IO
open System.Security.Claims
open System.Collections.Generic
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe.HttpContextExtensions
open Giraffe.HttpHandlers
open Giraffe.Middleware
open SampleApp.Models
open SampleApp.HtmlViews

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) (ctx : HttpContext) =
    logger.LogError(EventId(0), ex, "An unhandled exception has occurred while executing the request.")
    ctx |> (clearResponse >=> setStatusCode 500 >=> text ex.Message)


// ---------------------------------
// Web app
// ---------------------------------

let authScheme = "Cookie"

let accessDenied = setStatusCode 401 >=> text "Access Denied"

let mustBeUser = requiresAuthentication accessDenied

let mustBeAdmin = 
    requiresAuthentication accessDenied 
    >=> requiresRole "Admin" accessDenied

let loginHandler =
    fun (ctx : HttpContext) ->
        async {
            let issuer = "http://localhost:5000"
            let claims =
                [
                    Claim(ClaimTypes.Name,      "John",  ClaimValueTypes.String, issuer)
                    Claim(ClaimTypes.Surname,   "Doe",   ClaimValueTypes.String, issuer)
                    Claim(ClaimTypes.Role,      "Admin", ClaimValueTypes.String, issuer)
                ]
            let identity = ClaimsIdentity(claims, authScheme)
            let user     = ClaimsPrincipal(identity)

            do! ctx.Authentication.SignInAsync(authScheme, user) |> Async.AwaitTask
            
            return! text "Successfully logged in" ctx
        }

let userHandler =
    fun (ctx : HttpContext) ->
        text ctx.User.Identity.Name ctx

let showUserHandler id =
    fun (ctx : HttpContext) ->
        mustBeAdmin >=>
        text (sprintf "User ID: %i" id)
        <| ctx

let time() =
    System.DateTime.Now.ToString()

[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let submitCar =
    fun (ctx : HttpContext) ->
        async {
            let! car = ctx.BindModel<Car>()
            return! json car ctx
        }

let webApp = 
    choose [
        GET >=>
            choose [
                route  "/"           >=> text "index"
                route  "/ping"       >=> text "pong"
                route  "/error"      >=> (fun _ -> failwith "Something went wrong!")
                route  "/login"      >=> loginHandler
                route  "/logout"     >=> signOff authScheme >=> text "Successfully logged out."
                route  "/user"       >=> mustBeUser >=> userHandler
                routef "/user/%i"    showUserHandler
                route  "/razor"      >=> razorHtmlView "Person" { Name = "Razor" }
                route  "/person"     >=> (personView { Name = "Html Node" } |> renderHtml)
                route  "/once"       >=> (time() |> text)
                route  "/everytime"  >=> warbler (fun _ -> (time() |> text))
            ]
        route "/car" >=> submitCar
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Main
// ---------------------------------

let cookieAuth =
    new CookieAuthenticationOptions(
            AuthenticationScheme    = authScheme,
            AutomaticAuthenticate   = true,
            AutomaticChallenge      = false,
            CookieHttpOnly          = true,
            CookieSecure            = CookieSecurePolicy.SameAsRequest,
            SlidingExpiration       = true,
            ExpireTimeSpan          = TimeSpan.FromDays 7.0
    )

let configureApp (app : IApplicationBuilder) = 
    app.UseGiraffeErrorHandler errorHandler
    app.UseCookieAuthentication cookieAuth |> ignore
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    let sp  = services.BuildServiceProvider()
    let env = sp.GetService<IHostingEnvironment>()
    let viewsFolderPath = Path.Combine(env.ContentRootPath, "views")

    services.AddAuthentication() |> ignore
    services.AddDataProtection() |> ignore
    services.AddRazorEngine viewsFolderPath |> ignore

let configureLogging (loggerFactory : ILoggerFactory) =
    loggerFactory.AddConsole(LogLevel.Trace).AddDebug() |> ignore


[<EntryPoint>]
let main argv =
    WebHostBuilder()
        .UseKestrel()
        .UseContentRoot(Directory.GetCurrentDirectory())
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(Action<IServiceCollection> configureServices)
        .ConfigureLogging(Action<ILoggerFactory> configureLogging)
        .Build()
        .Run()
    0