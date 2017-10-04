module SampleApp.App

open System
open System.IO
open System.Text
open System.Security.Claims
open System.Collections.Generic
open System.Threading
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Features
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.Identity.EntityFrameworkCore
open Microsoft.EntityFrameworkCore
open Giraffe.Tasks
open Giraffe.HttpContextExtensions
open Giraffe.XmlViewEngine
open Giraffe.HttpHandlers
open Giraffe.Middleware

// ---------------------------------
// View engine
// ---------------------------------

let masterPage (pageTitle : string) (content : XmlNode list) =
    html [] [
        head [] [
            title [] [ encodedText pageTitle ]
            style [] [ rawText "label { display: inline-box; widht: 80px; }" ]
        ]
        body [] [
            h1 [] [ encodedText pageTitle ]
            main [] content
         ]
    ]

let indexPage =
    [
        p [] [
            a [ attr "href" "/register" ] [ rawText "Register" ]
        ]
        p [] [
            a [ attr "href" "/user" ] [ rawText "User page" ]
        ]
    ] |> masterPage "Home"

let registerPage =
    [
        form [ attr "action" "/register"; attr "method" "POST" ] [
            div [] [
                label [] [ rawText "Email:" ]
                input [ attr "name" "Email"; attr "type" "text" ]
            ]
            div [] [
                label [] [ rawText "User name:" ]
                input [ attr "name" "UserName"; attr "type" "text" ]
            ]
            div [] [
                label [] [ rawText "Password:" ]
                input [ attr "name" "Password"; attr "type" "password" ]
            ]
            input [ attr "type" "submit" ]
        ]
    ] |> masterPage "Register"

let loginPage (loginFailed : bool) =
    [
        if loginFailed then yield p [ attr "style" "color: Red;" ] [ rawText "Login failed." ]

        yield form [ attr "action" "/login"; attr "method" "POST" ] [
            div [] [
                label [] [ rawText "User name:" ]
                input [ attr "name" "UserName"; attr "type" "text" ]
            ]
            div [] [
                label [] [ rawText "Password:" ]
                input [ attr "name" "Password"; attr "type" "password" ]
            ]
            input [ attr "type" "submit" ]
        ]
        yield p [] [
            rawText "Don't have an account yet?"
            a [ attr "href" "/register" ] [ rawText "Go to registration" ]
        ]
    ] |> masterPage "Login"

let userPage (user : IdentityUser) =
    [
        p [] [
            sprintf "User name: %s, Email: %s" user.UserName user.Email
            |> encodedText
        ]
    ] |> masterPage "User details"

// ---------------------------------
// Web app
// ---------------------------------

[<CLIMutable>]
type RegisterModel =
    {
        UserName : string
        Email    : string
        Password : string
    }

[<CLIMutable>]
type LoginModel =
    {
        UserName : string
        Password : string
    }

let showErrors (errors : IdentityError seq) =
    errors
    |> Seq.fold (fun acc err ->
        sprintf "Code: %s, Description: %s" err.Code err.Description
        |> acc.AppendLine : StringBuilder) (StringBuilder(""))
    |> (fun x -> x.ToString())
    |> text

let registerHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! model       = ctx.BindForm<RegisterModel>()
            let  user        = IdentityUser(UserName = model.UserName, Email = model.Email)
            let  userManager = ctx.GetService<UserManager<IdentityUser>>()
            let! result      = userManager.CreateAsync(user, model.Password)

            match result.Succeeded with
            | false -> return! showErrors result.Errors next ctx
            | true  ->
                let signInManager = ctx.GetService<SignInManager<IdentityUser>>()
                do! signInManager.SignInAsync(user, true)
                return! redirectTo false "/user" next ctx
        }

let loginHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! model = ctx.BindForm<LoginModel>()
            let signInManager = ctx.GetService<SignInManager<IdentityUser>>()
            let! result = signInManager.PasswordSignInAsync(model.UserName, model.Password, true, false)
            match result.Succeeded with
            | true  -> return! redirectTo false "/user" next ctx
            | false -> return! renderHtml (loginPage true) next ctx
        }

let userHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let userManager = ctx.GetService<UserManager<IdentityUser>>()
            let! user = userManager.GetUserAsync ctx.User
            return! (user |> userPage |> renderHtml) next ctx
        }

let mustBeLoggedIn : HttpHandler =
    requiresAuthentication (redirectTo false "/login")

let logoutHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let signInManager = ctx.GetService<SignInManager<IdentityUser>>()
            do! signInManager.SignOutAsync()
            return! (redirectTo false "/") next ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"         >=> renderHtml indexPage
                route "/register" >=> renderHtml registerPage
                route "/login"    >=> renderHtml (loginPage false)

                route "/logout"   >=> mustBeLoggedIn >=> logoutHandler
                route "/user"     >=> mustBeLoggedIn >=> userHandler
            ]
        POST >=>
            choose [
                route "/register" >=> registerHandler
                route "/login"    >=> loginHandler
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins("http://localhost:8080").AllowAnyMethod().AllowAnyHeader() |> ignore
    
let configureApp (app : IApplicationBuilder) =
    app.UseCors configureCors |> ignore
    app.UseGiraffeErrorHandler errorHandler
    app.UseAuthentication() |> ignore
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    // Configure InMemory Db for sample application
    services.AddDbContext<IdentityDbContext<IdentityUser>>(
        fun options ->
            options.UseInMemoryDatabase("NameOfDatabase") |> ignore
        ) |> ignore

    // Register Identity Dependencies
    services.AddIdentity<IdentityUser, IdentityRole>()
        .AddEntityFrameworkStores<IdentityDbContext<IdentityUser>>()
        .AddDefaultTokenProviders()
        |> ignore

    // Configure Identity
    services.Configure<IdentityOptions>(
        fun options ->
            // Password settings
            options.Password.RequireDigit   <- true
            options.Password.RequiredLength <- 8
            options.Password.RequireNonAlphanumeric <- false
            options.Password.RequireUppercase <- true
            options.Password.RequireLowercase <- false

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan  <- TimeSpan.FromMinutes 30.0
            options.Lockout.MaxFailedAccessAttempts <- 10

            // User settings
            options.User.RequireUniqueEmail <- true
        ) |> ignore

    // Configure app cookie
    services.ConfigureApplicationCookie(
        fun options ->
            options.ExpireTimeSpan <- TimeSpan.FromDays 150.0
            options.LoginPath      <- PathString "/login"
            options.LogoutPath     <- PathString "/logout"
        ) |> ignore

    // Enable CORS
    services.AddCors |> ignore

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

[<EntryPoint>]
let main argv =
    WebHostBuilder()
        .UseKestrel()
        .UseContentRoot(Directory.GetCurrentDirectory())
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0