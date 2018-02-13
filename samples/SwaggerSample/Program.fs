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
open Giraffe.Swagger
open Giraffe.Swagger.Common
open Giraffe.Swagger.Analyzer
open Giraffe.Swagger.Generator
open Giraffe.Swagger.Dsl
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

let tcar = typeof<Car>

let docAddendums =
    fun (route:Analyzer.RouteInfos) (path:string,verb:HttpVerb,pathDef:PathDefinition) ->
    
        // routef params are automatically added to swagger, but you can customize their names like this 
        let changeParamName oldName newName (parameters:ParamDefinition list) =
            parameters |> Seq.find (fun p -> p.Name = oldName) |> fun p -> { p with Name = newName }
    
        match path,verb,pathDef with
        | _,_, def when def.OperationId = "say_hello_in_french" ->
            let firstname = def.Parameters |> changeParamName "arg0" "Firstname"
            let lastname = def.Parameters |> changeParamName "arg1" "Lastname"
            "/hello/{Firstname}/{Lastname}", verb, { def with Parameters = [firstname; lastname] }
        | "/", HttpVerb.Get,def ->
            // This is another solution to add operation id or other infos
            path, verb, { def with OperationId = "Home"; Tags=["home page"] }
        
        | "/car", HttpVerb.Post,def ->
            let ndef = 
                (def
                    .AddConsume "model" "application/json" Body typeof<Car>)
                    .AddResponse 200 "application/json" "A car" typeof<Car>
            path, verb, ndef
        | _ -> path,verb,pathDef

let port = 5000

let docsConfig c = 
    let describeWith desc  = 
        { desc
            with
                Title="Sample 1"
                Description="Create a swagger with Giraffe"
                TermsOfService="Coucou"
        } 
    
    { c with 
        Description = describeWith
        Host = sprintf "localhost:%d" port
        DocumentationAddendums = docAddendums
        MethodCallRules = 
                (fun rules -> 
                    // You can extend quotation expression analysis
                    rules.Add ({ ModuleName="App"; FunctionName="httpFailWith" }, 
                       (fun ctx -> 
                           ctx.AddResponse 500 "text/plain" (typeof<string>)
                )))
    }

let webApp =
    swaggerOf
        (choose [
              GET >=>
                 choose [
                      route  "/"           >=> text "index" 
                      route  "/ping"       >=> text "pong"
                      route  "/error"      >=> (fun _ _ -> failwith "Something went wrong!")
                      route  "/logout"     >=> signOut authScheme >=> text "Successfully logged out."
                      route  "/once"       >=> (time() |> text)
  
                      route  "/everytime"  >=> warbler (fun _ -> (time() |> text))

                      // Swagger operation id can be defined like this or with DocumentationAddendums
                      operationId "say_hello_in_french" ==> 
                          routef "/hello/%s/%s" bonjour
                 ]
              route  "/test"       >=> text "test"
              
              subRouteCi "/api"
                      (choose [
                          subRouteCi "/v1"
                              (choose [
                                  route "/foo" >=> text "Foo 1"
                                  route "/bar" >=> text "Bar 1" ])
                          subRouteCi "/v2"
                              (choose [
                                  route "/foo" >=> text "Foo 2"
                                  route "/bar" >=> text "Bar 2" ]) ])
                                  
              POST >=>  
                  choose [
                          route "/car" >=> submitCar
                          
                          operationId "send_a_car" ==>
                              consumes tcar ==>
                                  produces typeof<Car> ==>
                                      route "/car2" >=> submitCar
                          
                          route "/hello" 
                            >=>
                              (fun next ctx ->
                                let name = ctx.Request.Form.Item "name" |> Seq.head
                                let nickname = ctx.Request.Form.Item "nickname" |> Seq.head
                                let message = sprintf "hello %s" name
                                if name <> "kevin"
                                then text message next ctx
                                else httpFailWith "your are blacklisted" next ctx
                                 )
                    ]
  
              RequestErrors.notFound (text "Not Found")
               ]) |> withConfig docsConfig

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
        .AddGiraffe()
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
    
    let url = sprintf "http://+:%d" port
    
    WebHost.CreateDefaultBuilder()
        .UseUrls(url)
        .UseWebRoot(webRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0