module GoogleAuthApp.App

open System
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Authentication
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.GiraffeViewEngine
open GoogleAuthApp.HttpsConfig

// ---------------------------------
// Web app
// ---------------------------------

module AuthSchemes =

    let cookie = "Cookies"
    let google = "Google"

module Urls =

    let index      = "/"
    let login      = "/login"
    let googleAuth = "/google-auth"
    let user       = "/user"
    let logout     = "/logout"
    let missing    = "/missing"

module Views =

    let master (content: XmlNode list) =
        html [] [
            head [] [
                title [] [ str "Google Auth Sample App" ]
            ]
            body [] content
        ]

    let index =
        [
            h1 [] [ str "Google Auth Sample App" ]
            p [] [ str "Welcome to the Google Auth Sample App!" ]
            ul [] [
                li [] [ a [ _href Urls.login ] [ str "Login" ] ]
                li [] [ a [ _href Urls.user ] [ str "User profile" ] ]
            ]
        ] |> master

    let login =
        [
            h1 [] [ str "Login" ]
            p [] [ str "Pick one of the options to log in:" ]
            ul [] [
                li [] [ a [ _href Urls.googleAuth ] [ str "Google" ] ]
                li [] [ a [ _href Urls.missing ] [ str "Facebook" ] ]
                li [] [ a [ _href Urls.missing ] [ str "Twitter" ] ]
            ]
            p [] [
                a [ _href Urls.index ] [ str "Return to home." ]
            ]
        ] |> master

    let user (claims : (string * string) seq) =
        [
            h1 [] [ str "User details" ]
            h2 [] [ str "Claims:" ]
            ul [] [
                yield! claims |> Seq.map (
                    fun (key, value) ->
                        li [] [ sprintf "%s: %s" key value |> str ] )
            ]
            p [] [
                a [ _href Urls.logout ] [ str "Logout" ]
            ]
        ] |> master

    let notFound =
        [
            h1 [] [ str "Not Found" ]
            p [] [ str "The requested resource does not exist." ]
            p [] [ str "Facebook and Twitter auth handlers have not been configured yet." ]
            ul [] [
                li [] [ a [ _href Urls.index ] [ str "Return to home." ] ]
            ]
        ] |> master

module Handlers =

    let index : HttpHandler = Views.index |> htmlView
    let login : HttpHandler = Views.login |> htmlView

    let user : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            (ctx.User.Claims
            |> Seq.map (fun c -> (c.Type, c.Value))
            |> Views.user
            |> htmlView) next ctx

    let logout : HttpHandler =
        signOut AuthSchemes.cookie
        >=> redirectTo false Urls.index

    let challenge (scheme : string) (redirectUri : string) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                do! ctx.ChallengeAsync(
                        scheme,
                        AuthenticationProperties(RedirectUri = redirectUri))
                return! next ctx
            }

    let googleAuth = challenge AuthSchemes.google Urls.user

    let authenticate : HttpHandler =
        requiresAuthentication login

    let notFound : HttpHandler =
        setStatusCode 404 >=>
        (Views.notFound |> htmlView)

    let webApp : HttpHandler =
        choose [
            GET >=>
                choose [
                    route Urls.index      >=> index
                    route Urls.login      >=> login
                    route Urls.user       >=> authenticate >=> user
                    route Urls.logout     >=> logout
                    route Urls.googleAuth >=> googleAuth
                ]
            notFound ]

    let error (ex : Exception) (logger : ILogger) =
        logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureApp (app : IApplicationBuilder) =
    app.UseGiraffeErrorHandler(Handlers.error)
       .UseAuthentication()
       .UseGiraffe Handlers.webApp

let configureServices (services : IServiceCollection) =
    // Enable Authentication providers
    services.AddAuthentication(fun o -> o.DefaultScheme <- AuthSchemes.cookie)
            .AddCookie(
                AuthSchemes.cookie, fun o ->
                    o.LoginPath  <- PathString Urls.login
                    o.LogoutPath <- PathString Urls.logout)
            .AddGoogle(
                AuthSchemes.google, fun o ->
                    o.ClientId     <- "<google client id>"
                    o.ClientSecret <- "<google client secret>")
            |> ignore

    // Add Giraffe dependencies
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

[<EntryPoint>]
let main _ =
    let endpoints =
        [
            EndpointConfiguration.Default
            { EndpointConfiguration.Default with
                Port     = Some 44340
                Scheme   = Https
                FilePath = Some "<path to self signed certificate>"
                Password = Some "<password>" } ]
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseKestrel(fun o -> o.ConfigureEndpoints endpoints)
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0