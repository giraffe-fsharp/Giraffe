module SampleApp.App

open System
open System.IO
open System.Security.Claims
open System.Collections.Generic
open System.Threading
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Features
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Swagger
open Analyzer
open SwaggerUi
open Giraffe.Swagger.Generator

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Web app
// ---------------------------------

let authScheme = CookieAuthenticationDefaults.AuthenticationScheme

let time() = System.DateTime.Now.ToString()

[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let submitCar =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! car = ctx.BindModelAsync<Car>()
            return! json car next ctx
        }

let bonjour (firstName, lastName) =
    let message = sprintf "%s %s, vous avez le bonjour de Giraffe !" lastName firstName
    text message

let httpFailWith message =
    setStatusCode 500 >=> text message

let documentedApp =
    <@
        choose [
            GET >=>
                choose [
                        route  "/"           >=> text "index" 
                        route  "/ping"       >=> text "pong"
                        route  "/error"      >=> (fun _ _ -> failwith "Something went wrong!")
                        route  "/logout"     >=> signOff authScheme >=> text "Successfully logged out."
                        route  "/once"       >=> (time() |> text)
                        route  "/everytime"  >=> warbler (fun _ -> (time() |> text))
                        route "/car" >=> submitCar
                        operationId "say_hello_in_french" ==> routef "/hello/%s/%s" bonjour
                ]
            POST >=>  
                choose [
                        route "/hello" 
                          >=>
                            (fun next ctx ->
                              let name = ctx.Request.Form.Item "name" |> Seq.head
                              let nickname = ctx.Request.Form.Item "nickname" |> Seq.head
                              let message = sprintf "hello %s" name
                              if name <> "kevin"
                              then text message next ctx
                              else
                                httpFailWith "your are blacklisted" next ctx
                               )
                  ]

            RequestErrors.notFound (text "Not Found") ]
    @>
    
let methodCalls = 
    AppAnalyzeRules.Default.MethodCalls
        .Add ({ ModuleName="App"; FunctionName="httpFailWith" }, 
                (fun ctx -> 
                    ctx.AddResponse 500 "text/plain" (typeof<string>)
             ))

let rules = { AppAnalyzeRules.Default with MethodCalls=methodCalls }
let docCtx = analyze documentedApp rules

let swaggerDoc addendums =
    let rawJson (str : string) : HttpHandler =
        setHttpHeader "Content-Type" "application/json"
        >=> setBodyAsString str

    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let description = 
                { ApiDescription.Empty 
                    with
                        Title="Sample 1"
                        Description="Create a swagger with Giraffe"
                }
            let paths = documentRoutes docCtx.Routes addendums
            let doc =
                { Swagger="2.0"
                  Info=description
                  BasePath="/"
                  Host="localhost:5000"
                  Schemes=["http"]
                  Paths=paths
                  Definitions = dict [] }
                  
            return! rawJson (doc.ToJson()) next ctx
            }

let docAddendums =
    fun (route:Analyzer.RouteInfos) (path:string,verb:HttpVerb,pathDef:PathDefinition) ->
        match path,verb with
        | "/", HttpVerb.Get ->
            path, verb, { pathDef with OperationId = "Home" }
        | _ -> path,verb,pathDef

let webApp =
    choose [ 
        route "/swagger.json" >=> swaggerDoc docAddendums
        swaggerUiHandler "/swaggerui/" "/swagger.json"
        buildApp documentedApp
    ]

// ---------------------------------
// Main
// ---------------------------------

let cookieAuth (o : CookieAuthenticationOptions) =
    do
        o.Cookie.HttpOnly     <- true
        o.Cookie.SecurePolicy <- CookieSecurePolicy.SameAsRequest
        o.SlidingExpiration   <- true
        o.ExpireTimeSpan      <- TimeSpan.FromDays 7.0

let configureApp (app : IApplicationBuilder) =
    
    app.UseGiraffeErrorHandler(errorHandler)
       .UseStaticFiles()
       .UseAuthentication()
       .UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services
        .AddAuthentication(authScheme)
        .AddCookie(cookieAuth)   |> ignore
    services.AddDataProtection() |> ignore
    
let configureLogging (loggerBuilder : ILoggingBuilder) =
    loggerBuilder.AddFilter(fun lvl -> lvl.Equals LogLevel.Error)
                 .AddConsole()
                 .AddDebug() |> ignore

[<EntryPoint>]
let main _ =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    
    WebHost.CreateDefaultBuilder()
        .UseWebRoot(webRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0