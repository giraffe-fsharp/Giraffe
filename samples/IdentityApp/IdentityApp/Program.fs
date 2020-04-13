module SampleApp.App

open System
open System.IO
open System.Text
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.Identity.EntityFrameworkCore
open Microsoft.EntityFrameworkCore
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.GiraffeViewEngine

// ---------------------------------
// View engine
// ---------------------------------

let masterPage (pageTitle : string) (content : XmlNode list) =
    html [] [
        head [] [
            title [] [ str pageTitle ]
            style [] [ rawText "label { display: inline-block; width: 80px; }" ]
        ]
        body [] [
            h1 [] [ str pageTitle ]
            main [] content
         ]
    ]

let indexPage =
    [
        p [] [
            a [ _href "/register" ] [ str "Register" ]
        ]
        p [] [
            a [ _href "/user" ] [ str "User page" ]
        ]
    ] |> masterPage "Home"

let registerPage =
    [
        form [ _action "/register"; _method "POST" ] [
            div [] [
                label [] [ str "Email:" ]
                input [ _name "Email"; _type "text" ]
            ]
            div [] [
                label [] [ str "User name:" ]
                input [ _name "UserName"; _type "text" ]
            ]
            div [] [
                label [] [ str "Password:" ]
                input [ _name "Password"; _type "password" ]
            ]
            input [ _type "submit" ]
        ]
    ] |> masterPage "Register"

let loginPage (loginFailed : bool) =
    [
        if loginFailed then yield p [ _style "color: Red;" ] [ str "Login failed." ]

        yield form [ _action "/login"; _method "POST" ] [
            div [] [
                label [] [ str "User name:" ]
                input [ _name "UserName"; _type "text" ]
            ]
            div [] [
                label [] [ str "Password:" ]
                input [ _name "Password"; _type "password" ]
            ]
            input [ _type "submit" ]
        ]
        yield p [] [
            str "Don't have an account yet?"
            a [ _href "/register" ] [ str "Go to registration" ]
        ]
    ] |> masterPage "Login"

let userPage (user : IdentityUser) =
    [
        p [] [
            sprintf "User name: %s, Email: %s" user.UserName user.Email
            |> str
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
            let! model       = ctx.BindFormAsync<RegisterModel>()
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
            let! model = ctx.BindFormAsync<LoginModel>()
            let signInManager = ctx.GetService<SignInManager<IdentityUser>>()
            let! result = signInManager.PasswordSignInAsync(model.UserName, model.Password, true, false)
            match result.Succeeded with
            | true  -> return! redirectTo false "/user" next ctx
            | false -> return! htmlView (loginPage true) next ctx
        }

let userHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let userManager = ctx.GetService<UserManager<IdentityUser>>()
            let! user = userManager.GetUserAsync ctx.User
            return! (user |> userPage |> htmlView) next ctx
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
                route "/"         >=> htmlView indexPage
                route "/register" >=> htmlView registerPage
                route "/login"    >=> htmlView (loginPage false)

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
    app.UseCors(configureCors)
       .UseGiraffeErrorHandler(errorHandler)
       .UseAuthentication()
       .UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    // Configure InMemory Db for sample application
    services.AddDbContext<IdentityDbContext<IdentityUser>>(
        fun options ->
            options.UseInMemoryDatabase("NameOfDatabase") |> ignore
        ) |> ignore

    // Register Identity Dependencies
    services.AddIdentity<IdentityUser, IdentityRole>(
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
        )
        .AddEntityFrameworkStores<IdentityDbContext<IdentityUser>>()
        .AddDefaultTokenProviders()
        |> ignore

    // Configure app cookie
    services.ConfigureApplicationCookie(
        fun options ->
            options.ExpireTimeSpan <- TimeSpan.FromDays 150.0
            options.LoginPath      <- PathString "/login"
            options.LogoutPath     <- PathString "/logout"
        ) |> ignore

    // Enable CORS
    services.AddCors() |> ignore

    // Configure Giraffe dependencies
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

[<EntryPoint>]
let main _ =
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseKestrel()
                    .UseContentRoot(Directory.GetCurrentDirectory())
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0
