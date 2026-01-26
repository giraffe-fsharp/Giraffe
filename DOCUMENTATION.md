# Giraffe Documentation

An in depth functional reference to all of Giraffe's default features.

## Table of contents

- [Fundamentals](#fundamentals)
    - [HttpHandler](#httphandler)
    - [Giraffe pipeline vs. ASP.NET Core pipeline](#giraffe-pipeline-vs-aspnet-core-pipeline)
    - [Combinators](#combinators)
        - [compose (>=>)](#compose-)
        - [choose](#choose)
    - [Warbler](#warbler)
    - [Tasks](#tasks)
    - [Ways of creating a new HttpHandler](#ways-of-creating-a-new-httphandler)
    - [Continue vs. Return vs. Skip](#continue-vs-return-vs-skip)
- [Basics](#basics)
    - [Plugging Giraffe into ASP.NET Core](#plugging-giraffe-into-aspnet-core)
    - [Dependency Management](#dependency-management)
    - [Multiple Environments and Configuration](#multiple-environments-and-configuration)
    - [Logging](#logging)
    - [Error Handling](#error-handling)
- [Web Request Processing](#web-request-processing)
    - [HTTP Headers](#http-headers)
    - [HTTP Verbs](#http-verbs)
    - [HTTP Status Codes](#http-status-codes)
    - [Routing](#routing)
    - [Query Strings](#query-strings)
    - [Model Binding](#model-binding)
    - [Model Validation](#model-validation)
    - [File Uploads](#file-uploads)
    - [Authentication and Authorization](#authentication-and-authorization)
    - [Conditional Requests](#conditional-requests)
    - [Request Limitation](#request-limitation)
    - [Response Writing](#response-writing)
    - [Content Negotiation](#content-negotiation)
    - [Streaming](#streaming)
    - [Redirection](#redirection)
        - [Safe Redirection](#safe-redirection)
    - [Response Caching](#response-caching)
    - [Response Compression](#response-compression)
- [Giraffe View Engine](#giraffe-view-engine)
- [Serialization](#serialization)
    - [JSON](#json)
    - [XML](#xml)
- [Testing](#testing)
- [Miscellaneous](#miscellaneous)
    - [Short GUIDs and Short IDs](#short-guids-and-short-ids)
    - [Common Helper Functions](#common-helper-functions)
    - [Computation Expressions](#computation-expressions)
    - [CSRF Protection Helpers](#csrf-protection-helpers)
- [Additional Features](#additional-features)
    - [Endpoint Routing](#endpoint-routing)
    - [TokenRouter](#tokenrouter)
    - [Razor](#razor)
    - [DotLiquid](#dotliquid)
    - [OpenApi](#openapi)
- [Special Mentions](#special-mentions)
- [Appendix](#appendix)

## Fundamentals

### HttpHandler

The main building block in Giraffe is a so called `HttpHandler`:

```fsharp
type HttpFuncResult = Task<HttpContext option>
type HttpFunc = HttpContext -> HttpFuncResult
type HttpHandler = HttpFunc -> HttpContext -> HttpFuncResult
```

an `HttpHandler` is a function which takes two curried arguments, an `HttpFunc` and an `HttpContext`, and returns an `HttpContext` (wrapped in an `option` and `Task` workflow) when finished.

On a high level an `HttpHandler` function receives and returns an ASP.NET Core `HttpContext` object, which means every `HttpHandler` function has full control of the incoming `HttpRequest` and the resulting `HttpResponse`.

Each `HttpHandler` can process an incoming `HttpRequest` before passing it further down the Giraffe pipeline by invoking the next `HttpFunc` or short circuit the execution by returning an option of `Some HttpContext`.

If an `HttpHandler` doesn't want to process an incoming `HttpRequest` at all, then it can return `None` instead. In this case a surrounding `HttpHandler` might pick up the incoming `HttpRequest` or the Giraffe middleware will defer the request to the next `RequestDelegate` from the ASP.NET Core pipeline.

The easiest way to get your head around a Giraffe `HttpHandler` is to think of it as a functional equivalent to the ASP.NET Core middleware. Each handler has the full `HttpContext` at its disposal and can decide whether it wants to return `Some HttpContext`, `None` or pass it on to the "next" `HttpFunc`.

### Giraffe pipeline vs. ASP.NET Core pipeline

The Giraffe pipeline is a (sort of) functional equivalent of the (object oriented) ASP.NET Core pipeline. The ASP.NET Core pipeline is defined by nested middleware and the Giraffe pipeline is defined by `HttpHandler` functions. The Giraffe pipeline is plugged into the wider ASP.NET Core pipeline through the `GiraffeMiddleware` itself and therefore an addition to it rather than a replacement.

If the Giraffe pipeline didn't process an incoming `HttpRequest` (because the final result was `None` and not `Some HttpContext`) then other ASP.NET Core middleware can still process the request (e.g. static file middleware or another web framework plugged in after Giraffe).

This architecture allows F# developers to build rich web applications through a functional composition of `HttpHandler` functions while at the same time benefiting from the wider ASP.NET Core eco system by making use of already existing ASP.NET Core middleware.

### Combinators

#### compose (>=>)

The `compose` combinator combines two `HttpHandler` functions into one.

It is the main combinator in Giraffe which allows composing many smaller `HttpHandler` functions into a bigger web application:

```fsharp
let app = compose (route "/") (Successful.OK "Hello World")
```

A slightly more convenient and more commonly used form of `compose` is the fish operator `>=>`:

```fsharp
let app = route "/" >=> Successful.OK "Hello World"
```

There is no limit to how many `HttpHandler` functions can be chained with `compose` or the fish operator:

```fsharp
let app =
    route "/"
    >=> setHttpHeader "X-Foo" "Bar"
    >=> setStatusCode 200
    >=> setBodyFromString "Hello World"
```

If you would like to learn more about the `>=>` (fish) operator then please check out [Scott Wlaschin's blog post on Railway oriented programming](http://fsharpforfunandprofit.com/posts/recipe-part2/).

#### choose

The `choose` combinator function iterates through a list of `HttpHandler` functions and invokes each individual handler until the first `HttpHandler` returns a positive result:

```fsharp
let app =
    choose [
        route "/foo" >=> text "Foo"
        route "/bar" >=> text "Bar"
    ]
```

### Warbler

If your route is not returning a static response, then you should wrap your function with a `warbler`:

```fsharp
// ('a -> 'a -> 'b) -> 'a -> 'b
let warbler f a = f a a
```

Functions in F# are eagerly evaluated and a normal route will only be evaluated the first time.
A warbler will ensure that a function will get evaluated every time the route is hit:

```fsharp
// unit -> string
let time() = System.DateTime.Now.ToString()

let webApp =
    choose [
        route "/normal"  >=> text (time())
        route "/warbler" >=> warbler (fun _ -> text (time()))
    ]
```

### Tasks
Another important aspect of Giraffe is that it natively works with .NET's `Task` and `Task<'T>` objects instead of relying on F#'s historic `async {}` workflows. The main benefit of this is that it removes the necessity of converting back and forth between tasks and async workflows when building a Giraffe web application (because ASP.NET Core only works with tasks out of the box).

### Tasks - Giraffe 6 and later
Giraffe 6 targets .NET 6 and uses [F# 6's built-in task support](https://docs.microsoft.com/en-us/dotnet/fsharp/whats-new/fsharp-6#task-) without any additional dependencies.

When building web apps using Giraffe, we recommend you use this built-in support too.

### Tasks - Giraffe 5
In Giraffe 5, we use the `task {}` computation expression from the [Ply](https://www.nuget.org/packages/Ply/) NuGet package. Syntactically it works identical to F#'s async workflows (after opening the `FSharp.Control.Tasks` module):

```fsharp
open Giraffe

let personHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! person = ctx.BindModelAsync<Person>()
            return! json person next ctx
        }
```

The `task {}` CE is an independent project maintained by [Crowded](https://github.com/crowded), for more information please visit the official [Ply](https://github.com/crowded/ply) GitHub repository.

**IMPORTANT NOTICE**

If you have `do!` bindings in your Giraffe 5 web application then you must open the `FSharp.Control.Tasks` namespace to resolve any type inference issues:

```fsharp
open FSharp.Control.Tasks
```

### Ways of creating a new HttpHandler

There's multiple ways how one can create a new `HttpHandler` in Giraffe.

The easiest way is to re-use an existing `HttpHandler` function:

```fsharp
let sayHelloWorld : HttpHandler = text "Hello World, from Giraffe"
```

You can also add additional parameters before returning an existing `HttpHandler` function:

```fsharp
let sayHelloWorld (name : string) : HttpHandler =
    let greeting = sprintf "Hello World, from %s" name
    text greeting
```

If you need to access the `HttpContext` object then you'll have to explicitly return an `HttpHandler` function which accepts an `HttpFunc` and `HttpContext` object and returns an `HttpFuncResult`:

```fsharp
let sayHelloWorld : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let name =
            ctx.TryGetQueryStringValue "name"
            |> Option.defaultValue "Giraffe"
        let greeting = sprintf "Hello World, from %s" name
        text greeting next ctx
```

Because an `HttpHandler` is defined as `HttpFunc -> HttpContext -> HttpFuncResult` you will need to apply the `next` and `ctx` parameters to the subsequent handler (in this case `text`).

The most verbose version of defining a new `HttpHandler` function is by explicitly returning a `Task<HttpContext option>`. This is useful when an async operation needs to be called from within an `HttpHandler` function:

```fsharp
type Person = { Name : string }

let sayHelloWorld : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! person = ctx.BindJsonAsync<Person>()
            let greeting = sprintf "Hello World, from %s" person.Name
            return! text greeting next ctx
        }
```

#### handleContext

Starting with version 3.5.0 and onwards Giraffe exposes an additional convenience function which can be used to generate new handler functions which only require access to the `HttpContext` object without having to define the full verbose implementation as shown above.

The `handleContext` function can be used like this:

```fsharp
let handlerWithLogging : HttpHandler =
    handleContext(
        fun ctx ->
            let logger = ctx.GetService<ILogger>()
            logger.LogInformation("From the context")
            ctx.WriteTextAsync "")
```

Or alternatively if additional asynchronous work needs to be done:

```fsharp
let handlerWithLogging2 : HttpHandler =
    handleContext(
        fun ctx ->
            task {
                let logger = ctx.GetService<ILogger>()
                logger.LogInformation("From the context")
                // Do more async stuff
                return! ctx.WriteTextAsync "Done working"
            })
```

Please note that the `handleContext` function doesn't have control over the `next` handler and therefore cannot "skip" the handler pipeline like normal `HttpHandler` functions can do (see: [Continue vs. Return vs. Skip](#continue-vs-return-vs-skip)).

#### Deferred execution of Tasks

Please be also aware that a `Task<'T>` in .NET is just a promise of `'T` when a task eventually finishes asynchronously. Unless you define an `HttpHandler` function in the most verbose way (with the `task {}` CE) and actively await a nested result with either `let!` or `return!` then the handler will not wait for the task to complete before returning to the `GiraffeMiddleware`.

This has important implications if you want to execute code in an `HttpHandler` after invoking the next handler, such as cleaning up resources with the `use` keyword. For example, in the code below, the `IDisposable` will get disposed **before** the actual `handler` gets executed. This is because a `HttpHandler` is a `HttpFunc -> HttpContext -> Task<HttpContext option>` and therefore `handler next ctx` only returns a `Task<HttpContext option>` which hasn't been completed yet:

```fsharp
let doSomething handler : HttpHandler =
    fun next ctx ->
        use __ = somethingToBeDisposedAtTheEndOfTheRequest
        handler next ctx
```

However, by explicitly invoking the `handler` from within a `task {}` CE one can ensure that the `handler` gets executed before the `IDisposable` gets disposed:

```fsharp
let doSomething handler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            use __ = somethingToBeDisposedAtTheEndOfTheRequest
            return! handler next ctx
        }
```

### Continue vs. Return vs. Skip

In Giraffe there are three scenarios which a given `HttpHandler` can invoke:

- Continue with next handler
- Return early
- Skip

#### Continue

A handler performs some actions on the `HttpRequest` and/or `HttpResponse` object and then invokes the `next` handler to **continue** with the pipeline.

A great example is the `setHttpHeader` handler, which sets a given HTTP header and afterwards always calls into the `next` http handler:

```fsharp
let setHttpHeader key value : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetHttpHeader key value
        next ctx
```

#### Return early

Sometimes an `HttpHandler` wants to return early and not continue with the remaining `HttpHandler` pipeline.

A typical example would be an authentication or authorization handler, which would not continue with the remaining pipeline if a user wasn't authenticated. Instead it might want to return a `401 Unauthorized` response:

```fsharp
let earlyReturn : HttpFunc = Some >> Task.FromResult

let checkUserIsLoggedIn : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if isNotNull ctx.User && ctx.User.Identity.IsAuthenticated
        then next ctx
        else setStatusCode 401 earlyReturn ctx
```

In the `else` clause the `checkUserIsLoggedIn` handler returns a `401 Unauthorized` HTTP response and skips the remaining `HttpHandler` pipeline by not invoking `next` but an already completed task.

If you were to have an `HttpHandler` defined with the `task {}` CE then you could alternatively also return `Some HttpContext` in order to return early:

```fsharp
let checkUserIsLoggedIn : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            if isNotNull ctx.User && ctx.User.Identity.IsAuthenticated
            then return! next ctx
            else
                ctx.SetStatusCode 401
                return Some ctx
        }
```

#### Skip

There may be cases where an `HttpHandler` function might conclude that a given `HttpRequest` should not be handled by the handler or the remaining `HttpHandler` pipeline. In such a case an `HttpHandler` can skip the pipeline and defer the handling of the web request to either another `HttpHandler` function (when nested in a `choose` handler) or to another ASP.NET Core middleware altogether.

The `GET` handler is a good example of such a scenario. If a web request doesn't match the specified HTTP verb then the handler will skip the subsequent pipeline and defer to another handler or another ASP.NET Core middleware:

```fsharp
let skip : HttpFuncResult = Task.FromResult None

let GET : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if HttpMethods.IsGet ctx.Request.Method
        then next ctx
        else skip
```

If you were to have an `HttpHandler` defined with the `task {}` CE then you could alternatively also return `None` in order to skip the remaining pipeline:

```fsharp
let GET : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            if HttpMethods.IsGet ctx.Request.Method
            then return! next ctx
            else return None
        }
```

## Basics

### Plugging Giraffe into ASP.NET Core

Install the [Giraffe](https://www.nuget.org/packages/Giraffe) NuGet package:

```
PM> Install-Package Giraffe
```

Create a web application and plug it into the ASP.NET Core middleware:

```fsharp
open Giraffe

let webApp =
    choose [
        route "/ping"   >=> text "pong"
        route "/"       >=> htmlFile "/pages/index.html" ]

type Startup() =
    member __.ConfigureServices (services : IServiceCollection) =
        // Register default Giraffe dependencies
        services.AddGiraffe() |> ignore

    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =
        // Add Giraffe to the ASP.NET Core pipeline
        app.UseGiraffe webApp

[<EntryPoint>]
let main _ =
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseStartup<Startup>()
                    |> ignore)
        .Build()
        .Run()
    0
```

Instead of creating a `Startup` class you can also add Giraffe in a more functional way:

```fsharp
open Giraffe

let webApp =
    choose [
        route "/ping"   >=> text "pong"
        route "/"       >=> htmlFile "/pages/index.html" ]

let configureApp (app : IApplicationBuilder) =
    // Add Giraffe to the ASP.NET Core pipeline
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    // Add Giraffe dependencies
    services.AddGiraffe() |> ignore

[<EntryPoint>]
let main _ =
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                    |> ignore)
        .Build()
        .Run()
    0
```

### Dependency Management

ASP.NET Core has built in [dependency management](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection) which works out of the box with Giraffe.

#### Registering Services

[Registering services](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection#registering-services) is done the same way as it is done for any other ASP.NET Core web application:

```fsharp
let configureServices (services : IServiceCollection) =
    // Add default Giraffe dependencies
    services.AddGiraffe() |> ignore

    // Add other dependencies
    // ...

[<EntryPoint>]
let main _ =
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .Configure(configureApp)
                    // Calling ConfigureServices to set up dependencies
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0
```

#### Retrieving Services

Retrieving registered services from within a Giraffe `HttpHandler` function is done through the built in service locator (`RequestServices`) which comes with an `HttpContext` object:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let fooBar =
            ctx.RequestServices.GetService(typeof<IFooBar>)
            :?> IFooBar
        // Do something with `fooBar`...
        // Return a Task<HttpContext option>
```

Giraffe has an additional `HttpContext` extension method called `GetService<'T>` to make the code less cumbersome:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let fooBar = ctx.GetService<IFooBar>()
        // Do something with `fooBar`...
        // Return a Task<HttpContext option>
```

There's a handful more extension methods available to retrieve a few default dependencies like an `IHostingEnvironment` or `ILogger` object which are covered in the respective sections of this document.

### Multiple Environments and Configuration

ASP.NET Core has built in support for [working with multiple environments](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/environments) and [configuration management](https://docs.microsoft.com/en-gb/aspnet/core/fundamentals/configuration/?tabs=basicconfiguration), which both work out of the box with Giraffe.

Additionally Giraffe exposes a `GetWebHostEnvironment()` extension method which can be used to easier retrieve an `IWebHostEnvironment` object from within an `HttpHandler` function:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let env = ctx.GetWebHostEnvironment()
        // Do something with `env`...
        // Return a Task<HttpContext option>
```

Configuration options can be retrieved via the `GetService<'T>` extension method:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let settings = ctx.GetService<IOptions<MySettings>>()
        // Do something with `settings`...
        // Return a Task<HttpContext option>
```

If you need to access the configuration when configuring services, you can access it like this:

```fsharp
let configureServices (services : IServiceCollection) =
    let serviceProvider = services.BuildServiceProvider()
    let settings = serviceProvider.GetService<IConfiguration>()
    // Configure services using the `settings`...
    services.AddGiraffe() |> ignore
```

### Logging

ASP.NET Core has a built in [Logging API](https://docs.microsoft.com/en-gb/aspnet/core/fundamentals/logging/?tabs=aspnetcore2x) which works out of the box with Giraffe.

#### Configuring logging providers

One or more logging providers can be configured during application startup:

```fsharp
let configureLogging (builder : ILoggingBuilder) =
    // Set a logging filter (optional)
    let filter (l : LogLevel) = l.Equals LogLevel.Error

    // Configure the logging factory
    builder.AddFilter(filter) // Optional filter
           .AddConsole()      // Set up the Console logger
           .AddDebug()        // Set up the Debug logger

           // Add additional loggers if wanted...
    |> ignore

[<EntryPoint>]
let main _ =
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                    // Calling ConfigureLogging to set up logging providers
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0
```

Just like dependency management the logging API is configured the same way as it is done for any other ASP.NET Core web application.

#### Logging from within an HttpHandler function

After one or more logging providers have been configured you can retrieve an `ILogger` object (which can be used for logging) through the `GetLogger<'T>()` or `GetLogger (categoryName : string)` extension methods:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        // Retrieve an ILogger through one of the extension methods
        let loggerA = ctx.GetLogger<ModuleName>()
        let loggerB = ctx.GetLogger("someHttpHandler")

        // Log some data
        loggerA.LogCritical("Something critical")
        loggerB.LogInformation("Logging some random info")
        // etc.

        // Return a Task<HttpContext option>
```

### Error Handling

Giraffe exposes a separate error handling middleware which can be used to configure a functional error handler, which can react to any unhandled exception of the entire ASP.NET Core web application.

The Giraffe `ErrorHandler` function accepts an `Exception` object and a default `ILogger` and returns an `HttpHandler` function:

```fsharp
type ErrorHandler = exn -> ILogger -> HttpHandler
```

Because the Giraffe `ErrorHandler` returns an `HttpHandler` function it is possible to create anything from a simple error handling function to a complex error handling application.

#### Simple ErrorHandler example

This simple `errorHandler` function writes the entire `Exception` object to the logs, clears the response object and returns an HTTP 500 server error response:

```fsharp
let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse
    >=> ServerErrors.INTERNAL_ERROR ex.Message
```

#### Registering the Giraffe ErrorHandler middleware

In order to enable the error handler you have to configure the `GiraffeErrorHandlerMiddleware` in your application startup:

```fsharp
// Define the error handler function
let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse
    >=> ServerErrors.INTERNAL_ERROR ex.Message

// Register all ASP.NET Core middleware
let configureApp (app : IApplicationBuilder) =
    // Register the error handler first, so that all exceptions from other middleware can bubble up and be caught by the ErrorHandler function:
    app.UseGiraffeErrorHandler(errorHandler)
       .UseGiraffe webApp

[<EntryPoint>]
let main _ =
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    // Calling Configure to set up all middleware
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0
```

... or the equivalent by using a `Startup` class:

```fsharp
type Startup() =
    member __.ConfigureServices (services : IServiceCollection) =
        // Register default Giraffe dependencies
        services.AddGiraffe() |> ignore

    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =
        app.UseGiraffeErrorHandler errorHandler
           .UseGiraffe webApp

[<EntryPoint>]
let main _ =
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseStartup<Startup>()
                    |> ignore)
        .Build()
        .Run()
    0
```

It is recommended to set the error handler as the first middleware in the ASP.NET Core pipeline, so that any unhandled exception from other middleware can be caught and processed by the error handling function.

## Web Request Processing

Giraffe comes with a large set of default `HttpContext` extension methods as well as default `HttpHandler` functions which can be used to build rich web applications.

### HTTP Headers

Working with HTTP headers in Giraffe is plain simple. The `TryGetRequestHeader (key : string)` extension method tries to retrieve the value of a given HTTP header and then returns either `Some string` or `None`:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let someValue =
            match ctx.TryGetRequestHeader "X-MyOwnHeader" with
            | None -> "default value"
            | Some headerValue -> headerValue

        // Do something with `someValue`...
        // Return a Task<HttpContext option>
```

This method is useful when trying to retrieve optional HTTP headers from within an `HttpHandler`.

If an HTTP header is mandatory then the `GetRequestHeader (key : string)` extension method might be a better fit. Instead of returning an `Option<string>` object it will return a `Result<string, string>` type:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.GetRequestHeader "X-MyOwnHeader" with
        | Error msg ->
            // Mandatory header is missing.
            // Log error message
            // Return error response to the client.
        | Ok headerValue ->
            // Do something with `headerValue`...
            // Return a Task<HttpContext option>
```

Setting an HTTP header in the response can be done via the `SetHttpHeader (key : string) (value : obj)` extension method:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetHttpHeader "X-CustomHeader" "some-value"
        // Do other stuff...
        // Return a Task<HttpContext option>
```

You can also set an HTTP header via the `setHttpHeader` http handler:

```fsharp
let notFoundHandler : HttpHandler =
    setHttpHeader "X-CustomHeader" "Some value"
    >=> RequestErrors.NOT_FOUND "Not Found"

let webApp =
    choose [
        route "/foo" >=> text "Foo"
        route "/bar" >=> text "Bar"
        notFoundHandler
    ]
```

Please note that these are additional Giraffe functions which complement already existing HTTP header functionality in the ASP.NET Core framework. ASP.NET Core offers higher level HTTP header functionality through the `ctx.Request.GetTypedHeaders()` method.

### HTTP Verbs

Giraffe exposes a set of `HttpHandler` functions which can filter a request based on the request's HTTP verb:

- `GET`
- `POST`
- `PUT`
- `PATCH`
- `DELETE`
- `HEAD`
- `OPTIONS`
- `TRACE`
- `CONNECT`

There is an additional `GET_HEAD` handler which can filter an HTTP `GET` and `HEAD` request at the same time.

Filtering requests based on their HTTP verb can be useful when implementing a route which should behave differently based on the verb (e.g. `GET` vs. `POST`):

```fsharp
let submitFooHandler : HttpHandler =
    // Do something

let submitBarHandler : HttpHandler =
    // Do something

let webApp =
    choose [
        // Filters for GET requests
        GET  >=> choose [
            route "/foo" >=> text "Foo"
            route "/bar" >=> text "Bar"
        ]
        // Filters for POST requests
        POST >=> choose [
            route "/foo" >=> submitFooHandler
            route "/bar" >=> submitBarHandler
        ]
        // If the HTTP verb or the route didn't match return a 404
        RequestErrors.NOT_FOUND "Not Found"
    ]
```

If you need to check the request's HTTP verb from within an `HttpHandler` function then you can use the default ASP.NET Core `HttpMethods` class:

```fsharp
open Microsoft.AspNetCore.Http

let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if HttpMethods.IsPut ctx.Request.Method then
            // Do something
        else
            // Do something else
        // Return a Task<HttpContext option>
```

The `GET_HEAD` handler is a special handler which can be used to enable `GET` and `HEAD` requests on a resource at the same time. This can be very useful when caching is enabled and clients might want to send `HEAD` requests to check the `ETag` or `Last-Modified` HTTP headers before issuing a `GET`.

More combinations can be easily created via the `choose` http handler:

```fsharp
let POST_HEAD : HttpHandler = choose [ POST; HEAD ]
```

### HTTP Status Codes

Setting the HTTP status code of a response can be done either via the `SetStatusCode (httpStatusCode : int)` extension method or with the `setStatusCode (statusCode : int)` function:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetStatusCode 200
        // Return a Task<HttpContext option>

// or...

let someHttpHandler : HttpHandler =
    setStatusCode 200
    >=> text "Hello World"
```

Giraffe also offers a default set of pre-defined `HttpHandler` functions, which can be used to return a response with a specific HTTP status code.

These `HttpHandler` functions are categorised in four sub modules:

- [Intermediate](#intermediate) (1xx status codes)
- [Successful](#successful) (2xx status codes)
- [RequestErrors](#requesterrors) (4xx status codes)
- [ServerErrors](#servererrors) (5xx status codes)

For the majority of status code `HttpHandler` functions (except the `Intermediate` module) there are two versions for each individual status code available - a lower case and an upper case function (e.g. `Successful.ok` and `Successful.OK`).

The lower case version lets you combine the `HttpHandler` function with another `HttpHandler` function:

```fsharp
Successful.ok (text "Hello World")
```

This is a shorter (and more explicit) version of:

```fsharp
setStatusCode 200 >=> text "Hello World"
```

On the other hand the upper case version can be used to send an object directly to the client:


```fsharp
[<CLIMutable>]
type Person =
    {
        FirstName : string
        LastName  : string
    }

let johnDoe =
    {
        FirstName = "John"
        LastName  = "Doe"
    }

let app = choose [
    route `/`     >=> Successful.OK "Hello World"
    route `/john` >=> Successful.OK johnDoe
]
```

The upper case function is the equivalent and shorter version of:

```fsharp
setStatusCode 200 >=> negotiate johnDoe
```

The `negotiate` handler attempts to return an object back to the client based on the client's accepted mime types (see [Content Negotiation](#content-negotiation)).

The following sub modules and status code `HttpHandler` functions are available out of the box:

*Please note that there is no module for `3xx` HTTP status codes available, instead it is recommended to use the `redirectTo` http handler for redirection functionality (see [Redirection](#redirection)).*

#### Intermediate

| HTTP Status Code | Function name | Example |
| ---------------- | ------------- | ------- |
| 100 | CONTINUE | `route "/" >=> Intermediate.CONTINUE` |
| 101 | SWITCHING_PROTO | `route "/" >=> Intermediate.SWITCHING_PROTO` |

#### Successful

| HTTP Status Code | Function name | Example |
| ---------------- | ------------- | ------- |
| 200 | ok | `route "/" >=> Successful.ok (text "Hello World")` |
| 200 | OK | `route "/" >=> Successful.OK "Hello World"` |
| 201 | created | `route "/" >=> Successful.created (json someObj)` |
| 201 | CREATED | `route "/" >=> Successful.CREATED someObj` |
| 202 | accepted | `route "/" >=> Successful.accepted (xml someObj)` |
| 202 | ACCEPTED | `route "/" >=> Successful.ACCEPTED someObj` |
| 204 | NO_CONTENT | `route "/" >=> Successful.NO_CONTENT` |

#### RequestErrors

| HTTP Status Code | Function name | Example |
| ---------------- | ------------- | ------- |
| 400 | badRequest | `route "/" >=> RequestErrors.badRequest (text "Don't like it")` |
| 400 | BAD_REQUEST | `route "/" >=> RequestErrors.BAD_REQUEST "Don't like it"` |
| 401 | unauthorized | `route "/" >=> RequestErrors.unauthorized "Basic" "MyApp" (text "Don't know who you are")` |
| 401 | UNAUTHORIZED | `route "/" >=> RequestErrors.UNAUTHORIZED "Basic" "MyApp" "Don't know who you are"` |
| 403 | forbidden | `route "/" >=> RequestErrors.forbidden (text "Not enough permissions")` |
| 403 | FORBIDDEN | `route "/" >=> RequestErrors.FORBIDDEN "Not enough permissions"` |
| 404 | notFound | `route "/" >=> RequestErrors.notFound (text "Page not found")` |
| 404 | NOT_FOUND | `route "/" >=> RequestErrors.NOT_FOUND "Page not found"` |
| 405 | methodNotAllowed | `route "/" >=> RequestErrors.methodNotAllowed (text "Don't support this")` |
| 405 | METHOD_NOT_ALLOWED | `route "/" >=> RequestErrors.METHOD_NOT_ALLOWED "Don't support this"` |
| 406 | notAcceptable | `route "/" >=> RequestErrors.notAcceptable (text "Not having this")` |
| 406 | NOT_ACCEPTABLE | `route "/" >=> RequestErrors.NOT_ACCEPTABLE "Not having this"` |
| 409 | conflict | `route "/" >=> RequestErrors.conflict (text "some conflict")` |
| 409 | CONFLICT | `route "/" >=> RequestErrors.CONFLICT "some conflict"` |
| 410 | gone | `route "/" >=> RequestErrors.gone (text "Too late, not here anymore")` |
| 410 | GONE | `route "/" >=> RequestErrors.GONE "Too late, not here anymore"` |
| 415 | unsupportedMediaType | `route "/" >=> RequestErrors.unsupportedMediaType (text "Please send in different format")` |
| 415 | UNSUPPORTED_MEDIA_TYPE | `route "/" >=> RequestErrors.UNSUPPORTED_MEDIA_TYPE "Please send in different format"` |
| 422 | unprocessableEntity | `route "/" >=> RequestErrors.unprocessableEntity (text "Can't do anything with this")` |
| 422 | UNPROCESSABLE_ENTITY | `route "/" >=> RequestErrors.UNPROCESSABLE_ENTITY "Can't do anything with this"` |
| 428 | preconditionRequired | `route "/" >=> RequestErrors.preconditionRequired (test "Please do something else first")` |
| 428 | PRECONDITION_REQUIRED | `route "/" >=> RequestErrors.PRECONDITION_REQUIRED "Please do something else first"` |
| 429 | tooManyRequests | `route "/" >=> RequestErrors.tooManyRequests (text "Slow down champ")` |
| 429 | TOO_MANY_REQUESTS | `route "/" >=> RequestErrors.TOO_MANY_REQUESTS "Slow down champ"` |

Note that the `unauthorized` and `UNAUTHORIZED` functions require two additional parameters, an [authentication scheme](https://developer.mozilla.org/en-US/docs/Web/HTTP/Authentication#Authentication_schemes) and a realm.

#### ServerErrors

| HTTP Status Code | Function name | Example |
| ---------------- | ------------- | ------- |
| 500 | internalError | `route "/" >=> ServerErrors.internalError (text "Ops, something went wrong")` |
| 500 | INTERNAL_ERROR | `route "/" >=> ServerErrors.INTERNAL_ERROR "Not implemented"` |
| 501 | notImplemented | `route "/" >=> ServerErrors.notImplemented (text "Not implemented")` |
| 501 | NOT_IMPLEMENTED | `route "/" >=> ServerErrors.NOT_IMPLEMENTED "Ops, something went wrong"` |
| 502 | badGateway | `route "/" >=> ServerErrors.badGateway (text "Bad gateway")` |
| 502 | BAD_GATEWAY | `route "/" >=> ServerErrors.BAD_GATEWAY "Bad gateway"` |
| 503 | serviceUnavailable | `route "/" >=> ServerErrors.serviceUnavailable (text "Service unavailable")` |
| 503 | SERVICE_UNAVAILABLE | `route "/" >=> ServerErrors.SERVICE_UNAVAILABLE "Service unavailable"` |
| 504 | gatewayTimeout | `route "/" >=> ServerErrors.gatewayTimeout (text "Gateway timeout")` |
| 504 | GATEWAY_TIMEOUT | `route "/" >=> ServerErrors.GATEWAY_TIMEOUT "Gateway timeout"` |
| 505 | invalidHttpVersion | `route "/" >=> ServerErrors.invalidHttpVersion (text "Invalid HTTP version")` |

### Routing

Giraffe offers a variety of routing `HttpHandler` functions to accommodate the majority of use cases.

#### route

The simplest form of routing can be done with the `route` http handler:

```fsharp
let webApp =
    choose [
        route "/foo" >=> text "Foo"
        route "/bar" >=> text "Bar"

        // If none of the routes matched then return a 404
        RequestErrors.NOT_FOUND "Not Found"
    ]
```

#### routeCi

The `route` http handler expects an exact match. If the HTTP request was made to a slightly different route (e.g. `/Bar` or `/bAr`) then the `route "/bar"` handler will not serve the request.

This can be avoided by using the case insensitive `routeCi` http handler:

```fsharp
let webApp =
    choose [
        route   "/foo" >=> text "Foo"
        routeCi "/foo" >=> text "Bar"

        // If none of the routes matched then return a 404
        RequestErrors.NOT_FOUND "Not Found"
    ]
```

In the example above a request made to `https://example.org/FOO` would return `Bar` in the response.

#### routex

According to the HTTP specification a route with a trailing slash is not equivalent to the same route without a trailing slash:

```
https://example.org/foo
https://example.org/foo/
```

A web server might (rightfully) want to serve a different response for each route:

```fsharp
let webApp =
    choose [
        route "/foo"  >=> text "Foo"
        route "/foo/" >=> text "Bar"

        // If none of the routes matched then return a 404
        RequestErrors.NOT_FOUND "Not Found"
    ]
```

However many web applications choose to treat both routes as the same. If you would like to achieve this behaviour by using a single route in Giraffe then you can use the `routex` http handler which accepts a `Regex` string for matching routes:

```fsharp
let webApp =
    choose [
        routex "/foo(/?)" >=> text "Bar"

        // If none of the routes matched then return a 404
        RequestErrors.NOT_FOUND "Not Found"
    ]
```

The `(/?)` regex pattern specifies that a `/` can occur zero or one time at the end of the route, which means it would successfully match the following two routes:

```
https://example.org/foo
https://example.org/foo/
```

However, this example wouldn't match a request made to `https://example.org/foo///`. If you want to match any number of trailing slashes then you must use `(/*)` instead:

```fsharp
let webApp =
    choose [
        routex "/foo(/*)" >=> text "Bar"

        // If none of the routes matched then return a 404
        RequestErrors.NOT_FOUND "Not Found"
    ]
```

Please be aware that such a `routex` can create a conflict and unexpected behaviour if you have a similar matching `routef` (see [routef](#routef)):

```fsharp
let webApp =
    choose [
        routex "/foo(/*)" >=> text "Bar"
        routef "/foo/%s/%s/%s" (fun (s1, s2, s3) -> text (sprintf "%s%s%s" s1 s2 s3))

        // If none of the routes matched then return a 404
        RequestErrors.NOT_FOUND "Not Found"
    ]
```

In the above scenario it is not clear which one of the two http handlers a user want to be invoked when a request is made to `https://example.org/foo///`.

If you want to learn more about `Regex` please check the [Regular Expression Language Reference](https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference).

#### routexp

The `routexp` http handler is a combination of `routex` and `routef`. It resolves a route exactly like `routex`, but then passes the resolved Regex Groups as a `Seq<string>` parameter into the supplied handler function similar to how `routef` invokes the next handler in the pipeline.

#### routeCix

The `routeCix` http handler is the case insensitive version of `routex`:

```fsharp
let webApp =
    choose [
        routex   "/foo(/?)" >=> text "Foo"
        routeCix "/foo(/?)" >=> text "Bar"

        // If none of the routes matched then return a 404
        RequestErrors.NOT_FOUND "Not Found"
    ]
```

In the example above a request made to `https://example.org/FOO/` would return `Bar` in the response.

#### routef

If a route contains user defined parameters then the `routef` http handler can be handy:

```fsharp
let fooHandler (first : string,
               last  : string,
               age   : int)
               : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        (sprintf "First: %s, Last: %s, Age: %i" first last age
        |> text) next ctx

let webApp =
    choose [
        // Classic usage:
        routef "/foo/%s/%s/%i" fooHandler
        routef "/bar/%O" (fun guid -> text (guid.ToString()))

        // If none of the routes matched then return a 404
        RequestErrors.NOT_FOUND "Not Found"
    ]
```

The `routef` http handler takes two parameters - a format string and an `HttpHandler` function.

The format string supports the following format chars:

| Format Char | Type |
| ----------- | ---- |
| `%b` | `bool` |
| `%c` | `char` |
| `%s` | `string` |
| `%i` | `int` |
| `%d` | `int64` |
| `%f` | `float`/`double` |
| `%O` | `Guid` (including short GUIDs*) |
| `%u` | `uint64` (formatted as a short ID*) |

**Named parameters**: When using ASP.NET Core’s [Endpoint Routing](#endpoint-routing) with Giraffe, you can use `%<type>:<name>` (e.g., `%i:petId`) to assign a name to a route parameter, which is especially useful for OpenAPI/Swagger documentation and for clarity. 

For example, the route `routef "/pet/%i:petId"` will match `/pet/42` and bind `42` to `petId`. 

If you'd like to see an example, check the [EndpointRoutingApp sample](https://github.com/giraffe-fsharp/Giraffe/blob/master/samples/EndpointRoutingApp/Program.fs) at the official repository.

*) Please note that the `%O` and `%u` format characters also support URL friendly short GUIDs and IDs.

The `%O` format character supports GUIDs in the format of:

- `00000000000000000000000000000000`
- `00000000-0000-0000-0000-000000000000`
- `Xy0MVKupFES9NpmZ9TiHcw`

The last string represents an example of a [Short GUID](https://madskristensen.net/blog/A-shorter-and-URL-friendly-GUID) which is a normal GUID shortened into a URL encoded 22 character long string. Routes which use the `%O` format character will be able to automatically resolve a [Short GUID](https://madskristensen.net/blog/A-shorter-and-URL-friendly-GUID) as well as a normal GUID into a `System.Guid` argument.

The `%u` format character can only resolve an 11 character long [Short ID](https://webapps.stackexchange.com/questions/54443/format-for-id-of-youtube-video) (aka YouTube ID) into a `uint64` value.

Short GUIDs and short IDs are popular choices to make URLs shorter and friendlier whilst still mapping to a unique `System.Guid` or `uint64` value on the server side.

[Short GUIDs and IDs can also be resolved from query string parameters](#short-guids-and-short-ids) by making use of the `ShortGuid` and `ShortId` helper modules.

#### routeCif

The case insensitive version of `routef` is `routeCif`:

```fsharp
let webApp =
    choose [
        routeCif "/foo/%s/bar" (fun str -> text str)

        // If none of the routes matched then return a 404
        RequestErrors.NOT_FOUND "Not Found"
    ]
```

Please be aware that a case insensitive URL matching will return unexpected results in combination with case sensitive arguments such as short GUIDs and short IDs.

#### routeBind

If you need to bind route parameters directly to a type then you can use the `routeBind<'T>` http handler. Unlike `routef` or `routeCif` which work with a format string the `routeBind<'T>` http handler tries to match named parameters to the properties of a given type `'T`:

```fsharp
[<CLIMutable>]
type Person =
    {
        FirstName : string
        LastName  : string
    }

let personHandler (person : Person) : HttpHandler =
    sprintf "Hello %s %s" person.FirstName person.LastName
    |> Successful.OK

let webApp =
    choose [
        routeBind<Person> "/p/{firstName}/{lastName}" personHandler
    ]
```

The `routeBind<'T>` http handler from the `Giraffe.Routing` module can also contain valid `Regex` code to match a variety of different routes.

For example by definition (according to the spec) a route with a trailing slash **is not** the same as the equivalent route without a trailing slash. Therefore it is perfectly valid if a web server doesn't serve (or serves a different response) for the following two routes:

```
/p/{firstName}/{lastName}
/p/{firstName}/{lastName}/
```

However many web applications choose to treat both URLs as the same. The `routeBind<'T>` http handler can make use of `Regex` to enable such use cases:

```fsharp
[<CLIMutable>]
type Blah =
    {
        Foo : string
        Bar : string
    }

let blahHandler (blah : Blah) : HttpHandler =
    sprintf "Hello %s %s" blah.Foo blah.Bar
    |> Successful.OK

let webApp =
    choose [
        routeBind<Blah> "/p/{foo}/{bar}(/?)" blahHandler
    ]
```

By appending the `Regex` code `(/?)` to the end of the route we tell the `routeBind<'T>` handler to match any route which has either zero or one trailing slash.

If any number of trailing slashes should be allowed then you can swap the `?` with a `*` in the `Regex`:

```fsharp
routeBind<Blah> "/p/{foo}/{bar}(/*)" blahHandler
```

For a complete list of valid `Regex` codes please visit the official [Regular Expression Language Reference](https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference).

In case you are using `Giraffe.EndpointRouting`, the request path is handled by ASP.NET Core’s Endpoint Routing infrastructure. We therefore recommend reviewing the following sections of the official documentation: [url matching](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-10.0#url-matching) and [route constraints](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-10.0#route-constraints).

#### routeStartsWith

Sometimes it can be useful to pre-filter a route in order to enable certain functionality which should only be applied to a specific collection of routes.

The `routeStartsWith` http handler does just that:

```fsharp
let webApp =
    routeStartsWith "/api/" >=>
        // Pre-filter because only API calls require Auth
        requiresAuthentication (challenge "Cookie") >=>
            choose [
                route "/api/v1/foo" >=> text "Foo"
                route "/api/v1/bar" >=> text "Bar"
            ]
```

#### routeStartsWithCi

The case insensitive version of `routeStartsWith` is `routeStartsWithCi`:

```fsharp
let webApp =
    routeStartsWithCi "/api/" >=>
        // Pre-filter because only API calls require Auth
        requiresAuthentication (challenge "Cookie") >=>
            choose [
                route "/api/v1/foo" >=> text "Foo"
                route "/api/v1/bar" >=> text "Bar"
            ]
```

Please note that the `routeStartsWith` and `routeStartsWithCi` http handlers do not change how subsequent routing functions are matched. The final URL to get a "Foo" response is still `http[s]://your-domain.com/api/v1/foo` (single `/api`) and not `http[s]://your-domain.com/api/api/v1/foo` (double `/api`).

#### subRoute

In contrast to `routeStartsWith` the `subRoute` http handler lets you categorise routes without having to repeat already pre-filtered parts of the route:

```fsharp
let webApp =
    subRoute "/api"
        (choose [
            subRoute "/v1"
                (choose [
                    route "/foo" >=> text "Foo 1"
                    route "/bar" >=> text "Bar 1" ])
            subRoute "/v2"
                (choose [
                    route "/foo" >=> text "Foo 2"
                    route "/bar" >=> text "Bar 2" ]) ])
```

In this example the final URL to retrieve "Bar 2" would be `http[s]://your-domain.com/api/v2/bar`.

#### subRouteCi

As you might expect the `subRouteCi` http handler is the case insensitive version of `subRoute`:

```fsharp
let webApp =
    subRouteCi "/api"
        (choose [
            subRouteCi "/v1"
                (choose [
                    route ""     >=> text "Default"
                    route "/foo" >=> text "Foo 1"
                    route "/bar" >=> text "Bar 1" ])
            subRouteCi "/v2"
                (choose [
                    route "/foo" >=> text "Foo 2"
                    route "/bar" >=> text "Bar 2" ]) ])
```

Please note that only the path specified for `subRouteCi` is case insensitive. Nested routes after `subRouteCi` will be evaluated as per definition of each individual route.

**Note:** If you wish to have a default route for any `subRoute` handler (e.g. `/api/v1` from the above example) then you need to specify the route as `route ""` and not as `route "/"`, because `/api/v1/` is a fundamentally different than `/api/v1` according to the HTTP specification.

#### subRoutef

The `subRoutef` http handler is a combination of the `routef` and the `subRoute` http handler:

```fsharp
let app =
    GET >=> choose [
        route "/"    >=> text "index"
        route "/foo" >=> text "bar"

        subRoutef "/%s/api" (fun lang ->
            requiresAuthentication (challenge "Cookie") >=>
                choose [
                    route  "/blah" >=> text "blah"
                    routef "/%s" (fun n -> text (sprintf "Hello %s! Lang: %s" n lang))
                ])
        setStatusCode 404 >=> text "Not found" ]
```

This can be useful when an application has dynamic parameters at the beginning of each route (e.g. language parameter):

```
https://example.org/en/users/John
https://example.org/de/users/Ryan
https://example.org/fr/users/Nicky
...
```

#### routePorts

If your web server is listening to multiple ports through `WebHost.UseUrls` then you can use the `routePorts` http handler to filter incoming requests based on their port:

```fsharp
let guiApp =
    choose [
        route   "/"    >=> text "Hello World"
        routeCi "/foo" >=> text "Bar"
    ]

let apiApp =
    subRoute "/api"
        (choose [
            subRoute "/v1"
                (choose [
                    route "/foo" >=> text "Foo 1"
                    route "/bar" >=> text "Bar 1" ])
            subRoute "/v2"
                (choose [
                    route "/foo" >=> text "Foo 2"
                    route "/bar" >=> text "Bar 2" ]) ])

let webApp =
    routePorts [
        (9001, guiApp)
        (9002, apiApp)
    ]
```

### Query Strings

Working with query strings is very similar to working with HTTP headers in Giraffe. The `TryGetQueryStringValue (key : string)` extension method tries to retrieve the value of a given query string parameter and then returns either `Some string` or `None`:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let someValue =
            match ctx.TryGetQueryStringValue "q" with
            | None   -> "default value"
            | Some q -> q

        // Do something with `someValue`...
        // Return a Task<HttpContext option>
```

This method is useful when trying to retrieve optional query string parameters from within an `HttpHandler`.

If a query string parameter is mandatory then the `GetQueryStringValue (key : string)` extension method might be a better fit. Instead of returning an `Option<string>` object it will return a `Result<string, string>` type:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.GetQueryStringValue "q" with
        | Error msg ->
            // Mandatory query string value is missing.
            // Log error message
            // Return error response to the client.
        | Ok q ->
            // Do something with `q`...
            // Return a Task<HttpContext option>
```

You can also access the query string through the `ctx.Request.Query` object which returns an `IQueryCollection` object which allows you to perform more actions on it.

Last but not least there is also an `HttpContext` extension method called `BindQueryString<'T>` which lets you bind an entire query string to an object of type `'T` (see [Binding Query Strings](#binding-query-strings)).

### Model Binding

Giraffe offers out of the box a few default `HttpContext` extension methods and equivalent `HttpHandler` functions which make it possible to bind the payload or query string of an HTTP request to a custom object.

#### Binding JSON

The `BindJsonAsync<'T>()` extension method can be used to bind a JSON payload to an object of type `'T`:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let submitCar : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Binds a JSON payload to a Car object
            let! car = ctx.BindJsonAsync<Car>()

            // Sends the object back to the client
            return! Successful.OK car next ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        POST >=> route "/car" >=> submitCar
    ]
```

Alternatively you can also use the `bindJson<'T>` http handler:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        POST
        >=> route "/car"
        >=> bindJson<Car> (fun car -> Successful.OK car)
    ]
```

Both, the `HttpContext` extension method as well as the `HttpHandler` function will try to create an instance of type `'T` regardless if the submitted payload contained a complete representation of `'T` or not. The parsed object might only contain partial data (where some properties might be `null`) and additional `null` checks might be required before further processing.

Please note that in order for the model binding to work the record type must be decorated with the `[<CLIMutable>]` attribute, which will make sure that the type will have a parameterless constructor.

The underlying JSON serializer can be configured as a dependency during application startup (see [JSON](#json)).

#### Binding XML

The `BindXmlAsync<'T>()` extension method binds an XML payload to an object of type `'T`:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let submitCar : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Binds an XML payload to a Car object
            let! car = ctx.BindXmlAsync<Car>()

            // Sends the object back to the client
            return! Successful.OK car next ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        POST >=> route "/car" >=> submitCar
    ]
```
Alternatively you can also use the `bindXml<'T>` http handler:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        POST
        >=> route "/car"
        >=> bindXml<Car> (fun car -> Successful.OK car)
    ]
```

Both, the `HttpContext` extension method as well as the `HttpHandler` function will try to create an instance of type `'T` regardless if the submitted payload contained a complete representation of `'T` or not. The parsed object might only contain partial data (where some properties might be `null`) and additional `null` checks might be required before further processing.

Please note that in order for the model binding to work the record type must be decorated with the `[<CLIMutable>]` attribute, which will make sure that the type will have a parameterless constructor.

The underlying XML serializer can be configured as a dependency during application startup (see [XML](#xml)).

#### Binding Forms

The `BindFormAsync<'T> (?cultureInfo : CultureInfo)` extension method binds form data to an object of type `'T`. You can also specify an optional `CultureInfo` object for parsing culture specific data such as `DateTime` objects or floating point numbers:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let submitCar : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Binds a form payload to a Car object
            let! car = ctx.BindFormAsync<Car>()

            // or with a CultureInfo:
            let british = CultureInfo.CreateSpecificCulture("en-GB")
            let! car2 = ctx.BindFormAsync<Car>(british)

            // Sends the object back to the client
            return! Successful.OK car next ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        POST >=> route "/car" >=> submitCar
    ]
```

Alternatively you can use the `bindForm<'T>` http handler (which also accepts an additional parameter of type `CultureInfo option`):

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let british = CultureInfo.CreateSpecificCulture("en-GB")

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        POST
        >=> route "/car"
        >=> bindForm<Car> (Some british) (fun car -> Successful.OK car)
    ]
```

Just like in the previous examples the record type must be decorated with the `[<CLIMutable>]` attribute in order for the model binding to work.

The `BindFormAsync<'T>` extension method and the `bindForm<'T>` http handler are both very loose model binding functions, which means they will try to create an instance of type `'T` even if some data was missing or provided in the wrong format (in which case it will just skip parsing the field).

While this has its own advantages it is not very idiomatic to functional programming.

For a more stricter (and more functional) model binding you can use the `TryBindFormAsync<'T>` extension method or the `tryBindForm<'T>` http handler function.

They are both very similar to the previous binding methods, except that they will not create an instance of type `'T` if the submitted payload did not contain all mandatory fields (any field which is not an F# option type) or had badly formatted data.

The `TryBindFormAsync<'T>` method returns an object of type `Result<'T, string>`. If the model binding was successful then the result will contain an instance of type `'T`, otherwise a `string` value containing the parsing error message:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let submitCar : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Binds a form payload to a Car object
            let! result = ctx.TryBindFormAsync<Car>()

            // or with a CultureInfo:
            let british = CultureInfo.CreateSpecificCulture("en-GB")
            let! result2 = ctx.TryBindFormAsync<Car>(british)

            return!
                (match result2 with
                | Ok car -> Successful.OK car
                | Error err -> RequestErrors.BAD_REQUEST err) next ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        POST >=> route "/car" >=> submitCar
    ]
```

The `tryBindForm<'T>` http handler is very similar, but instead of returning a `Result<'T, string>` object it will invoke an error handler function if the model binding does not succeed:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let british = CultureInfo.CreateSpecificCulture("en-GB")
let parsingError (err : string) = RequestErrors.BAD_REQUEST err

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        POST
        >=> route "/car"
        >=> tryBindForm<Car> parsingError (Some british) (fun car -> Successful.OK car)
        RequestErrors.NOT_FOUND "Not found"
    ]
```

In this example if a `Car` object could not be successfully created then the `parsingError` handler will get invoked which will return an `Http Bad Request` response with the parsing error message.

#### Binding Query Strings

The `BindQueryString<'T> (?cultureInfo : CultureInfo)` extension method binds query string parameters to an object of type `'T`. An optional `CultureInfo` object can be specified for parsing culture specific data such as `DateTime` objects and floating point numbers:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let submitCar : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        // Binds the query string to a Car object
        let car = ctx.BindQueryString<Car>()

        // or with a CultureInfo:
        let british = CultureInfo.CreateSpecificCulture("en-GB")
        let car2 = ctx.BindQueryString<Car>(british)

        // Sends the object back to the client
        Successful.OK car next ctx

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
                route "/car" >=> submitCar
            ]
    ]
```

Alternatively you can use the `bindQuery<'T>` http handler (which also accepts an additional parameter of type `CultureInfo option`):

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let british = CultureInfo.CreateSpecificCulture("en-GB")

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        POST
        >=> route "/car"
        >=> bindQuery<Car> (Some british) (fun car -> Successful.OK car)
    ]
```

Just like in the previous examples the record type must be decorated with the `[<CLIMutable>]` attribute in order for the model binding to work.

The `BindQueryString<'T>` extension method and the `bindQuery<'T>` http handler are both very loose model binding functions, which means they will try to create an instance of type `'T` even if some data was missing or provided in the wrong format (in which case it will just skip parsing the field).

While this has its own advantages it is not very idiomatic to functional programming.

For a more stricter (and more functional) model binding approach you can use the `TryBindQueryString<'T>` extension method or the `tryBindQuery<'T>` http handler function.

They are both very similar to the previous binding methods, except that they will not create an instance of type `'T` if the submitted query string did not contain all mandatory fields (any field which is not an F# option type) or had badly formatted data.

The `TryBindQueryString<'T>` method returns an object of type `Result<'T, string>`. If the model binding was successful then the result will contain an instance of type `'T`, otherwise a `string` value containing the parsing error message:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let submitCar : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        // Binds a form payload to a Car object
        let result = ctx.TryBindQueryString<Car>()

        // or with a CultureInfo:
        let british = CultureInfo.CreateSpecificCulture("en-GB")
        let result2 = ctx.TryBindQueryString<Car>(british)

        (match result2 with
        | Ok car -> Successful.OK car
        | Error err -> RequestErrors.BAD_REQUEST err) next ctx

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        POST >=> route "/car" >=> submitCar
    ]
```

The `tryBindQuery<'T>` http handler is very similar, but instead of returning a `Result<'T, string>` object it will invoke an error handler function if the model binding does not succeed:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let british = CultureInfo.CreateSpecificCulture("en-GB")
let parsingError (err : string) = RequestErrors.BAD_REQUEST err

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        POST
        >=> route "/car"
        >=> tryBindQuery<Car> parsingError (Some british) (fun car -> Successful.OK car)
        RequestErrors.NOT_FOUND "Not found"
    ]
```

In this example if a `Car` object could not be successfully created then the `parsingError` handler will get invoked which will return an `Http Bad Request` response containing the parsing error message.

**Special note**

[Aleksander Heintz](https://github.com/Alxandr) has created a [Gist](https://gist.github.com/Alxandr/50aef7fbe4806ceb4c2889f1cbde1438) which contains a re-usable query string API based on how Chiron works, which allows one doing something like the following:

```fsharp
type Report =
  { author: string option
    project: string option
    week: int option
    summary: string option
    progress: string list
    comments: string list
    plan: string list }

  static member FromQuery (_: Report) =
        fun author project week summary progress comments plan ->
          { author = author
            project = project
            week = week
            summary = summary
            progress = progress
            comments = comments
            plan = plan }
    <!> Query.read "author"
    <*> Query.read "project"
    <*> Query.read "week"
    <*> Query.read "summary"
    <*> Query.read "progress"
    <*> Query.read "comments"
    <*> Query.read "plan"

let reportRoute = route "/report" >=> Query.bind (fun (r: Report) -> text <| sprintf "%A" r)
```

Even though this API didn't quite fit with Giraffe's existing `tryBindQuery` and [model validation](#model-validation) function it is a nice example of how Giraffe can be extended to do similar things in different ways.

If you prefer this API you can either copy paste [Aleksander](https://github.com/Alxandr)'s code from the [provided Gist](https://gist.github.com/Alxandr/50aef7fbe4806ceb4c2889f1cbde1438) or find the contents of the Gist in the [appendix](#aleksander-heintzs-query-string-binder-api) of this document (in case the Gist gets ever deleted).

#### Binding Models (catch all)

The `BindModelAsync<'T> (?cultureInfo : CultureInfo)` method is a generic model binding function which will try to pick the right model parsing function based on a request's HTTP verb and `Content-Type` header. With the help of `BindModelAsync<'T>` it is possible to create a single endpoint which can bind JSON, XML, form and query string data:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let submitCar : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Binds a Car object
            let! car = ctx.BindModelAsync<Car>()

            // or with a CultureInfo:
            let british = CultureInfo.CreateSpecificCulture("en-GB")
            let! car2 = ctx.BindModelAsync<Car>(british)

            // Sends the object back to the client
            return! Successful.OK car next ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        route "/car" >=> submitCar
    ]
```

Alternatively you can use the `bindModel<'T>` http handler:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }

let british = CultureInfo.CreateSpecificCulture("en-GB")

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "/ping" >=> text "pong"
            ]
        POST
        >=> route "/car"
        >=> bindModel<Car> (Some british) (fun car -> Successful.OK car)
    ]
```

Again like before, the record type `'T` must be decorated with the `[<CLIMutable>]` attribute in order for the model binding to work.

### Model Validation

Giraffe exposes an `IModelValidation<'T>` interface and an accompanying `validateModel<'T>` http handler which can be used to validate a model in a more functional way.

Let's take a look at the following example:

```fsharp
[<CLIMutable>]
type Adult =
    {
        FirstName  : string
        MiddleName : string option
        LastName   : string
        Age        : int
    }
    override this.ToString() =
        sprintf "Name: %s%s %s, Age: %i"
            this.FirstName
            (if this.MiddleName.IsSome then " " + this.MiddleName.Value else "")
            this.LastName
            this.Age

module WebApp =
    let textHandler (x : obj) = text (x.ToString())
    let parsingError err = RequestErrors.BAD_REQUEST err

    let webApp _ =
        choose [
            route "/person"
            >=> tryBindQuery<Adult> parsingError None textHandler
            RequestErrors.NOT_FOUND "Not found"
        ]
```

The `Adult` type is a normal F# record type which defines four properties (one of them optional) and an override of the `ToString()` method.

The `/person` route will try to bind a query string to an object of type `Adult` before invoking the `textHandler` which will eventually output the model by calling its `ToString()` method.

The model has three mandatory properties (`FirstName`, `LastName` and `Age`) and only one optional property `MiddleName`, which means that a query string must contain at least the fields for the first- and last name, as well as the age for the model binding to succeed.

However, what if someone wants to define additional validation logic before responding with an `Http 200` to a client?

For example the `Adult` type could have an additional validation method called `HasErrors`:

```fsharp
[<CLIMutable>]
type Adult =
    {
        FirstName  : string
        MiddleName : string option
        LastName   : string
        Age        : int
    }
    override this.ToString() =
        sprintf "Name: %s%s %s, Age: %i"
            this.FirstName
            (if this.MiddleName.IsSome then " " + this.MiddleName.Value else "")
            this.LastName
            this.Age

    member this.HasErrors() =
        if      this.FirstName.Length < 3  then Some "First name is too short."
        else if this.FirstName.Length > 50 then Some "First name is too long."
        else if this.LastName.Length  < 3  then Some "Last name is too short."
        else if this.LastName.Length  > 50 then Some "Last name is too long."
        else if this.Age < 18              then Some "Person must be an adult (age >= 18)."
        else if this.Age > 150             then Some "Person must be a human being."
        else None
```

The `HasErrors` method is checking business logic which is specific to the type `Adult`. For instance if `Age` is less than 18 then the person is not an adult and therefore `HasErrors` would return an F# option type with `Some "Person must be an adult (age >= 18)."`.

It is a generic validation method which can be used from anywhere in an F# application to validate if a given `Adult` object has logically correct data.

In order to make use of that validation method from within a Giraffe `HttpHandler` one could create a custom handler to invoke the method:

```fsharp
module WebApp =
    let adultHandler (adult : Adult) : HttpHandler =
        match adult.HasErrors() with
        | Some msg -> RequestErrors.BAD_REQUEST msg
        | None     -> text (adult.ToString())

    let parsingError err = RequestErrors.BAD_REQUEST err

    let webApp _ =
        choose [
            route "/person"
            >=> tryBindQuery<Adult> parsingError None adultHandler
            RequestErrors.NOT_FOUND "Not found"
        ]
```

If an application has only one model to deal with then this is fairly straight forward, but if an application has more models which require additional data validation steps like in the case of `Adult` then you'll quickly end up writing a lot of boilerplate code. This can be avoided with the help of `IModelValidation<'T>` and `validateModel<'T>`:

```fsharp
[<CLIMutable>]
type Adult =
    {
        FirstName  : string
        MiddleName : string option
        LastName   : string
        Age        : int
    }
    override this.ToString() =
        sprintf "Name: %s%s %s, Age: %i"
            this.FirstName
            (if this.MiddleName.IsSome then " " + this.MiddleName.Value else "")
            this.LastName
            this.Age

    member this.HasErrors() =
        if      this.FirstName.Length < 3  then Some "First name is too short."
        else if this.FirstName.Length > 50 then Some "First name is too long."
        else if this.LastName.Length  < 3  then Some "Last name is too short."
        else if this.LastName.Length  > 50 then Some "Last name is too long."
        else if this.Age < 18              then Some "Person must be an adult (age >= 18)."
        else if this.Age > 150             then Some "Person must be a human being."
        else None

    interface IModelValidation<Adult> with
        member this.Validate() =
            match this.HasErrors() with
            | Some msg -> Error (RequestErrors.badRequest (text msg))
            | None     -> Ok this

module WebApp =
    let textHandler (x : obj) = text (x.ToString())

    let parsingError err = RequestErrors.BAD_REQUEST err

    let webApp _ =
        choose [
            route Urls.person
            >=> tryBindQuery<Adult> parsingError None (validateModel textHandler)
        ]
```

Now the `Adult` type has implemented the `IModelValidation<'T>` interface from where it was able to re-use the already existing `HasErrors` method to either return a validated object of type `Adult` or an error of type `HttpHandler`.

The `validateModel` method has now been added between the `tryBindQuery<Adult>` and `textHandler` functions, which means it will validate the model using its `IModelValidation<Adult>.Validate()` method.

On success the `textHandler` will be executed as normal and on error it will invoke the error handler returned from `Validate()`.

### File Uploads

ASP.NET Core makes it really easy to process uploaded files.

The `HttpContext.Request.Form.Files` collection can be used to process one or many small files which have been sent by a client:

```fsharp
open Giraffe

let fileUploadHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            return!
                (match ctx.Request.HasFormContentType with
                | false -> RequestErrors.BAD_REQUEST "Bad request"
                | true  ->
                    ctx.Request.Form.Files
                    |> Seq.fold (fun acc file -> sprintf "%s\n%s" acc file.FileName) ""
                    |> text) next ctx
        }

let webApp = route "/upload" >=> fileUploadHandler
```

You can also read uploaded files by utilizing the `IFormFeature` and the `ReadFormAsync` method:

```fsharp
let fileUploadHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let formFeature = ctx.Features.Get<IFormFeature>()
            let! form = formFeature.ReadFormAsync CancellationToken.None
            return!
                (form.Files
                |> Seq.fold (fun acc file -> sprintf "%s\n%s" acc file.FileName) ""
                |> text) next ctx
        }

let webApp = route "/upload" >=> fileUploadHandler
```

For large file uploads it is recommended to [stream the file](https://docs.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads#uploading-large-files-with-streaming) in order to prevent resource exhaustion.

See also [large file uploads in ASP.NET Core](https://stackoverflow.com/questions/36437282/dealing-with-large-file-uploads-on-asp-net-core-1-0) on StackOverflow.

### Authentication and Authorization

ASP.NET Core has a wealth of [Authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/index) and [Authorization](https://docs.microsoft.com/en-us/aspnet/core/security/authorization/index) options which work out of the box with Giraffe.

Additionally Giraffe offers a few `HttpHandler` functions which make it easier to work with ASP.NET Core's authentication and authorization APIs in a functional way.

#### requiresAuthentication

The `requiresAuthentication (authFailedHandler : HttpHandler)` http handler validates if a user has been authenticated by one of ASP.NET Core's authentication middleware. If the identity of a user could not be established then the `authFailedHandler` will be executed:

```fsharp
let notLoggedIn =
    RequestErrors.UNAUTHORIZED
        "Basic"
        "Some Realm"
        "You must be logged in."

let mustBeLoggedIn = requiresAuthentication notLoggedIn

let webApp =
    choose [
        route "/"     >=> text "Hello World"
        route "/user" >=>
            mustBeLoggedIn >=>
                choose [
                    GET  >=> readUserHandler
                    POST >=> submitUserHandler
                ]
    ]
```

#### requiresRole

The `requiresRole (role : string) (authFailedHandler : HttpHandler)` http handler checks if an authenticated user is part of a given `role`. If a user fails to be in a certain role then the `authFailedHandler` will be executed:

```fsharp
let notLoggedIn =
    RequestErrors.UNAUTHORIZED
        "Basic"
        "Some Realm"
        "You must be logged in."

let notAdmin =
    RequestErrors.FORBIDDEN
        "Permission denied. You must be an admin."

let mustBeLoggedIn = requiresAuthentication notLoggedIn

let mustBeAdmin = requiresRole "Admin" notAdmin

let webApp =
    choose [
        route "/"     >=> text "Hello World"
        route "/user" >=>
            mustBeLoggedIn >=> mustBeAdmin >=>
                choose [
                    routef "/user/%s/edit"   editUserHandler
                    routef "/user/%s/delete" deleteUserHandler
                ]
    ]
```

#### requiresRoleOf

The `requiresRoleOf (roles : string list) (authFailedHandler : HttpHandler)` http handler checks if an authenticated user is part of a list of given `roles`. If a user fails to be in at least one of the `roles` then the `authFailedHandler` will be executed:

```fsharp
let notLoggedIn =
    RequestErrors.UNAUTHORIZED
        "Basic"
        "Some Realm"
        "You must be logged in."

let notProUserOrAdmin =
    RequestErrors.FORBIDDEN
        "Permission denied. You must be a pro user or admin."

let mustBeLoggedIn = requiresAuthentication notLoggedIn

let mustBeProUserOrAdmin =
    requiresRoleOf [ "ProUser"; "Admin" ] notProUserOrAdmin

let webApp =
    choose [
        route "/"     >=> text "Hello World"
        route "/user" >=>
            mustBeLoggedIn >=> mustBeProUserOrAdmin >=>
                choose [
                    routef "/user/%s/edit"   editUserHandler
                    routef "/user/%s/delete" deleteUserHandler
                ]
    ]
```

#### authorizeRequest

The `authorizeRequest (predicate : HttpContext -> bool) (authFailedHandler : HttpHandler)` http handler validates a request based on a given predicate. If the predicate returns false then the `authFailedHandler` will get executed:

```fsharp
let apiKey = "some-secret-key-1234"

let validateApiKey (ctx : HttpContext) =
    match ctx.TryGetRequestHeader "X-API-Key" with
    | Some key -> apiKey.Equals key
    | None     -> false

let accessDenied   = setStatusCode 401 >=> text "Access Denied"
let requiresApiKey =
    authorizeRequest validateApiKey accessDenied

let webApp =
    choose [
        route "/" >=> text "Hello World"
        route "/private"
        >=> requiresApiKey
        >=> protectedResource
    ]
```

#### authorizeUser

The `authorizeUser (policy : ClaimsPrincipal -> bool) (authFailedHandler : HttpHandler)` http handler checks if an authenticated user meets a given user policy. If the policy cannot be satisfied then the `authFailedHandler` will get executed:

```fsharp
let notLoggedIn =
    RequestErrors.UNAUTHORIZED
        "Basic"
        "Some Realm"
        "You must be logged in."

let accessDenied = setStatusCode 401 >=> text "Access Denied"

let mustBeLoggedIn = requiresAuthentication notLoggedIn

let mustBeJohn =
    authorizeUser (fun u -> u.HasClaim (ClaimTypes.Name, "John")) accessDenied

let webApp =
    choose [
        route "/" >=> text "Hello World"
        route "/john-only"
        >=> mustBeLoggedIn
        >=> mustBeJohn
        >=> userHandler
    ]
```

#### authorizeByPolicyName

The `authorizeByPolicyName (policyName : string) (authFailedHandler : HttpHandler)` http handler checks if an authenticated user meets a given authorization policy. If the policy cannot be satisfied then the `authFailedHandler` will get executed:

```fsharp
let notLoggedIn =
    RequestErrors.UNAUTHORIZED
        "Basic"
        "Some Realm"
        "You must be logged in."

let accessDenied = setStatusCode 401 >=> text "Access Denied"

let mustBeLoggedIn = requiresAuthentication notLoggedIn

let mustBeOver21 =
    authorizeByPolicyName "MustBeOver21" accessDenied

let webApp =
    choose [
        route "/" >=> text "Hello World"
        route "/adults-only"
        >=> mustBeLoggedIn
        >=> mustBeOver21
        >=> userHandler
    ]
```

#### authorizeByPolicy

The `authorizeByPolicy (policy : AuthorizationPolicy) (authFailedHandler : HttpHandler)` http handler checks if an authenticated user meets a given authorization policy. If the policy cannot be satisfied then the `authFailedHandler` will get executed.

See [authorizeByPolicyName](#authorizebypolicyname) for more information.

#### challenge

The `challenge (authScheme : string)` http handler will challenge the client to authenticate with a specific `authScheme`. This function is often used in combination with the `requiresAuthentication` http handler:

```fsharp
let webApp =
    choose [
        route "/"     >=> text "Hello World"
        route "/user" >=>
            requiresAuthentication (challenge "Cookie") >=>
                choose [
                    GET  >=> readUserHandler
                    POST >=> submitUserHandler
                ]
    ]
```

In this example the client will be challenged to authenticate with a scheme called "Cookie". The scheme name must match one of the registered authentication schemes from the configuration of the ASP.NET Core auth middleware.

#### signOut

The `signOut (authScheme : string)` http handler will sign a user out from a given `authScheme`:

```fsharp
let logout = signOut "Cookie" >=> redirectTo false "/"

let webApp =
    choose [
        route "/"     >=> text "Hello World"
        route "/user" >=>
            requiresAuthentication (challenge "Cookie") >=>
                choose [
                    GET  >=> readUserHandler
                    POST >=> submitUserHandler
                    route "/user/logout" >=> logout
                ]
    ]
```

### Conditional Requests

Conditional HTTP headers (e.g. `If-Match`, `If-Modified-Since`, etc.) are a common pattern to improve performance (web caching), to combat the [lost update problem](https://www.w3.org/1999/04/Editing/) or to perform [optimistic concurrency control](https://en.wikipedia.org/wiki/Optimistic_concurrency_control) when a client requests a resource from a web server.

Giraffe offers the `validatePreconditions` http handler which can be used to run HTTP pre-validation checks against a given `ETag` and/or `Last-Modified` value of an incoming HTTP request:

```fsharp
let someHandler (eTag         : string)
                (lastModified : DateTimeOffset)
                (content      : string) =
    let eTagHeader = Some (EntityTagHeaderValue.FromString true eTag)
    validatePreconditions eTagHeader (Some lastModified)
    >=> setBodyFromString content
```

The `validatePreconditions` handler takes in two optional parameters - an `eTag` and a `lastMofified` date time value - which will be used to validate a conditional HTTP request. If all conditions can be met, or if no conditions have been submitted, then the `next` http handler (of the Giraffe pipeline) will get invoked. Otherwise, if one of the pre-conditions fails or if the resource hasn't changed since the last check, then a `412 Precondition Failed` or a `304 Not Modified` response will get returned.

The [ETag (Entity Tag)](https://tools.ietf.org/html/rfc7232#section-2.3) value is an opaque identifier assigned by a web server to a specific version of a resource found at a URL. The [Last-Modified](https://tools.ietf.org/html/rfc7232#section-2.2) value provides a timestamp indicating the date and time at which the origin server believes the selected representation was last modified.

Giraffe's `validatePreconditions` http handler validates the following conditional HTTP headers:

- `If-Match`
- `If-None-Match`
- `If-Modified-Since`
- `If-Unmodified-Since`

The `If-Range` HTTP header will not get validated as part the `validatePreconditions` http handler, because it is a streaming specific check which gets handled by Giraffe's [Streaming](#streaming) functionality.

Alternatively Giraffe exposes the `HttpContext` extension method `ValidatePreconditions (eTag) (lastModified)` which can be used to create a custom conditional http handler. The `ValidatePreconditions` method takes the same two optional parameters and returns a result of type `Precondition`.

The `Precondition` union type contains the following cases:

| Case | Description and Recommended Action |
| ---- | ---------------------------------- |
| `NoConditionsSpecified` | No validation has taken place, because the client didn't send any conditional HTTP headers. Proceed with web request as normal. |
| `ConditionFailed` | At least one condition couldn't be satisfied. It is advised to return a `412` status code back to the client (you can use the `HttpContext.PreconditionFailedResponse()` method for that purpose). |
| `ResourceNotModified` | The resource hasn't changed since the last visit. The server can skip processing this request and return a `304` status code back to the client (you can use the `HttpContext.NotModifiedResponse()` method for that purpose). |
| `AllConditionsMet` | All pre-conditions were satisfied. The server should continue processing the request as normal. |

The `validatePreconditions` http handler as well as the `ValidatePreconditions` extension method will not only validate all conditional HTTP headers, but also set the required `ETag` and/or `Last-Modified` HTTP response headers according to the HTTP spec.

Both functions follow latest HTTP guidelines and validate all conditional headers in the correct precedence as defined in [RFC 2616](https://tools.ietf.org/html/rfc2616).

Example of `HttpContext.ValidatePreconditions`:

```fsharp
// Pass an optional eTag and lastModified timestamp into the handler, because generating an eTag might require to load the entire resource into memory and therefore this is not something which should be done on every request.
let someHttpHandler eTag lastModified : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            match ctx.ValidatePreconditions eTag lastModified with
            | ConditionFailed     -> return ctx.PreconditionFailedResponse()
            | ResourceNotModified -> return ctx.NotModifiedResponse()
            | AllConditionsMet | NoConditionsSpecified ->
                // Continue as normal
                // Do stuff
        }

let webApp =
    choose [
        route "/"    >=> text "Hello World"
        route "/foo" >=> someHttpHandler
    ]
```

### Request Limitation

With this feature, we can add guards or limitations to the kind of requests that reach the server. Requests with a certain value for the `Accept`, `Content-Type` or `Content-Length` headers can be checked for acceptable values and a configurable user-friendly error message is send back to the consumer automatically when the conditions are not met.

In order to configure this response, you must use a [record](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records) type named `OptionalErrorHandlers`:

```fsharp
// the type definition
type OptionalErrorHandlers =
    { InvalidHeaderValue: HttpHandler option
      HeaderNotFound: HttpHandler option }

// to use the default handlers
let optionalErrorHandlers =
    { InvalidHeaderValue = None; HeaderNotFound = None }
```

As shown at the previous code block, you can simply use `None` for the record and use our default handlers, which will change the response status code to 406 (not acceptable), and return a piece of text to the client explaining what happened.

For now, the helper middlewares we offer are:

**Accept**
Guards http request based on its `Accept` header:

```fsharp
// Only allow http requests with an `Accept` header equals `application/json`.
let webApp =
  GET >=> mustAcceptAny [ "application/json" ] optionalErrorHandlers >=> text "Hello World"

// Http request with `Accept` = `application/json`    -> Pass through
// Http request without `Accept` = `application/json  -> Error status code 406.
//  If you define your custom error handler, we use them, otherwise will return one of the following text messages:
//  1) Request rejected because 'Accept' header was not found
//  2) Request rejected because 'Accept' header hasn't got expected MIME type
```

**Content-Type**
Guards http request based on its `Content-Type` header:

```fsharp
// Only allow http request with a `Content-Type` header `equals `application/json`.
let webApp =
  GET >=> hasAnyContentTypes "application/json" optionalErrorHandlers >=> text "Hello World"

// Http request with    `Content-Type` = `application/json` -> Pass through
// Http request without `Content-Type` = `application/json` -> Error status code 406.
//   If you define your custom error handler, we use them, otherwise will return one of the following text messages:
//   1) Request rejected because 'Content-Type' header was not found
//   2) Request rejected because 'Content-Type' header hasn't got expected value
```

* Note: with `hasAnyContentTypes` multiple `Content-Type` headers can be passed to verify if the http request has any of the provided header values.

**Content-Length**
Guards http request based on its `Content-Length` header:

```fsharp
// Only allow http request with a `Content-Length` header less than or equal than provided maximum bytes.
let webApp =
  GET >=> maxContentLength 100L >=> text "Hello World"

// Http request with    `Content-Length` = `45`   -> Pass through
// Http request without `Content-Length` = `3042` -> Error status code 406.
//   If you define your custom error handler, we use them, otherwise will return one of the following text messages:
//   1) Request rejected because there is no 'Content-Length' header
//   2) Request rejected because 'Content-Length' header is too large
```

### Response Writing

Sending a response back to a client in Giraffe can be done through a small range of `HttpContext` extension methods and their equivalent `HttpHandler` functions.

#### Writing Bytes

The `WriteBytesAsync (bytes : byte[])` extension method and the `setBody (bytes : byte array)` http handler both write a `byte array` to the response stream of the HTTP request:

```fsharp
let someHandler (bytes : byte array) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Do stuff
            return! ctx.WriteBytesAsync bytes
        }

// or...

let someHandler (bytes : byte array) : HttpHandler =
    // Do stuff
    setBody bytes
```

Both functions will also set the `Content-Length` HTTP header to the length of the `byte array`.

#### Writing Strings

The `WriteStringAsync (str : string)` extension method and the `setBodyFromString (str : string)` http handler are both small helper functions which `UTF8` decode the `string` into a `byte array` and subsequently write the `byte array` to the response stream of the HTTP request.

Both functions will also set the `Content-Length` HTTP header to the correct length of the response:

```fsharp
let someHandler (str : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Do stuff
            return! ctx.WriteStringAsync str
        }

// or...

let someHandler (str : string) : HttpHandler =
    // Do stuff
    setBodyFromString str
```

The `setBody` and `setBodyFromString` http handlers (and their `HttpContext` extension method equivalents) are useful when you want to create your own response writing function for a specific media type which is not provided by Giraffe yet.

For example Giraffe doesn't have any functionality for serializing and writing a YAML response back to a client. However, you can reference another third party library which can serialize an object into a YAML string and then create your own `yaml` http handler like this:

```fsharp
let yaml (x : obj) : HttpHandler =
    setHttpHeader "Content-Type" "text/yaml"
    >=> setBodyFromString (YamlSerializer.toYaml x)

```

#### Writing Text

The `WriteTextAsync (str : string)` extension method and the `text (str : string)` http handler are the same as [writing strings](#writing-strings) except that they will also set the `Content-Type` HTTP header to `text/plain` in the response:

```fsharp
let someHandler (str : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Do stuff
            return! ctx.WriteTextAsync str
        }

// or...

let someHandler (str : string) : HttpHandler =
    // Do stuff
    text str
```

#### Writing JSON

The `WriteJsonAsync<'T> (dataObj : 'T)` extension method and the `json<'T> (dataObj : 'T)` http handler will both serialize an object to a JSON string and write the output to the response stream of the HTTP request. They will also set the `Content-Length` HTTP header and the `Content-Type` header to `application/json` in the response:

```fsharp
let someHandler (animal : Animal) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Do stuff
            return! ctx.WriteJsonAsync animal
        }

// or...

let someHandler (animal : Animal) : HttpHandler =
    // Do stuff
    json animal
```

The underlying JSON serializer can be configured as a dependency during application startup (see [JSON](#json)).

The `WriteJsonChunkedAsync<'T> (dataObj : 'T)` extension method and the `jsonChunked (dataObj : 'T)` http handler write directly to the response stream of the HTTP request without extra buffering into a byte array. They will not set a `Content-Length` header and instead set the `Transfer-Encoding: chunked` header and `Content-Type: application/json`:

```fsharp
let someHandler (person : Person) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Do stuff
            return! ctx.WriteJsonChunkedAsync person
        }

// or...

let someHandler (person : Person) : HttpHandler =
    // Do stuff
    jsonChunked person
```

#### Writing XML

The `WriteXmlAsync (dataObj : obj)` extension method and the `xml (dataObj : obj)` http handler will both serialize an object to an XML string and write the output to the response stream of the HTTP request. They will also set the `Content-Length`HTTP header and the `Content-Type` header to `application/xml` in the response:

```fsharp
let someHandler (dataObj : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Do stuff
            return! ctx.WriteXmlAsync dataObj
        }

// or...

let someHandler (dataObj : obj) : HttpHandler =
    // Do stuff
    xml dataObj
```

The underlying XML serializer can be configured as a dependency during application startup (see [XML](#xml)).

#### Writing HTML

The `WriteHtmlFileAsync (filePath : string)` extension method and the `htmlFile (filePath : string)` http handler will both read a file from the local file system and write the content to the response stream of the HTTP request. They will also set the `Content-Length` HTTP header and the `Content-Type` header to `text/html`:

```fsharp
let someHandler (dataObj : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Do stuff
            return! ctx.WriteHtmlFileAsync "index.html"
        }

// or...

let someHandler (dataObj : obj) : HttpHandler =
    // Do stuff
    htmlFile "index.html"
```

Both functions accept either a relative or an absolute path to the HTML file.

#### Writing HTML Strings

The `WriteHtmlStringAsync (html : string)` extension method and the `htmlString (html : string)` http handler are both equivalent to [writing strings](#writing-strings) except that they will also set the `Content-Type` header to `text/html`:

```fsharp
let someHandler (dataObj : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Do stuff
            return! ctx.WriteHtmlStringAsync "<html><head></head><body>Hello World</body></html>"
        }

// or...

let someHandler (dataObj : obj) : HttpHandler =
    // Do stuff
    htmlString "<html><head></head><body>Hello World</body></html>"
```

#### Writing HTML Views

Giraffe comes with its own extremely powerful view engine for functional developers (see [Giraffe View Engine](#giraffe-view-engine)). The `WriteHtmlViewAsync (htmlView : XmlNode)` extension method and the `htmlView (htmlView : XmlNode)` http handler will both compile a given html view into valid HTML code and write the output to the response stream of the HTTP request. Additionally they will both set the `Content-Length` HTTP header to the correct value and set the `Content-Type` header to `text/html`:

```fsharp
let indexView =
    html [] [
        head [] [
            title [] [ str "Giraffe" ]
        ]
        body [] [
            h1 [] [ str "Giraffe" ]
            p [] [ str "Hello World." ]
        ]
    ]

let someHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Do stuff
            return! ctx.WriteHtmlViewAsync indexView
        }

// or...

let someHandler : HttpHandler =
    // Do stuff
    htmlView indexView
```

### Content Negotiation

Giraffe's default [response writers](#response-writing) will always send a response in a specific media type regardless of a client's own requirements. Content negotiation on the other hand allows a Giraffe web server to examine a web request's `Accept` HTTP header and decide an appropriate data representation on the fly.

The `NegotiateAsync (responseObj : obj)` extension method and the `negotiate (responseObj : obj)` http handler will both pick the most appropriate data representation based on a request's `Accept` HTTP header and write a data object to the response stream of an HTTP request:

```fsharp
[<CLIMutable>]
type Person =
    {
        FirstName : string
        LastName  : string
    }

let johnDoe =
    {
        FirstName = "John"
        LastName  = "Doe"
    }

let someHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Do stuff
            return! ctx.NegotiateAsync johnDoe
        }

// or...

let webApp =
    choose [
        route "/foo" >=> negotiate johnDoe
        route "/bar" >=> someHandler
    ]
```

Currently Giraffe only supports plain text, JSON and XML responses during content negotiation out of the box. If a client doesn't accept any of these media types then the default negotiation function will return a `406 Unacceptable` HTTP response.

#### Configuring Content Negotiation

The default negotiation behaviour can be customized by creating a new class which implements the `INegotiationConfig` interface and set up a new dependency of that type during application startup.

The `INegotiationConfig` has two members which must be implemented:

- `Rules` of type `IDictionary<string, obj -> HttpHandler>`
- `UnacceptableHandler` of type `HttpHandler`


The `Rules` property is of type `IDictionary<string, obj -> HttpHandler>` and represents a key/value dictionary, where the key denotes a supported `Content-Type` and the value represents a function which turns a given `obj` into an `HttpHandler`.

For example the rules of the `DefaultNegotiationConfig` are as following:

```fsharp
dict [
    "*/*"             , json
    "application/json", json
    "application/xml" , xml
    "text/xml"        , xml
    "text/plain"      , fun x -> x.ToString() |> text
]
```

In addition to the `DefaultNegotiationConfig`, there is also a `JsonOnlyNegotiationConfig` provided which only returns JSON.

As you can see from the example above the default dictionary uses the `json` and `xml` http handlers to define the response handler for the respective media types. If a client requests a `text/plain` response then a new function had to be created which accepts an `obj` and uses the `.ToString()` method in combination with the `text` http handler to return a plain text response.

If a client has no particular preference (`*/*`) then the default response is `json`.

The `UnacceptableHandler` is an http handler which will be invoked if none of the client's accepted media types are supported by the web server and therefore the request cannot be satisfied.

#### Example: Adding BSON support to content negotiation

Let's assume you have created your own `bson` http handler which can serialize an object into BSON and write the contents to the response stream of the request:

```fsharp
let bson (o : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        // Implement BSON handler here
```

In order for `negotiate` and `NegotiateAsync` to support the new `bson` http handler we need to create a new type which implements `INegotiationConfig`:

```fsharp
type CustomNegotiationConfig (baseConfig : INegotiationConfig) =
    let plainText x = text (x.ToString())

    interface INegotiationConfig with

        member __.UnacceptableHandler =
            baseConfig.UnacceptableHandler

        member __.Rules =
                dict [
                    "*/*"             , json
                    "application/json", json
                    "application/xml" , xml
                    "text/xml"        , xml
                    "application/bson", bson
                    "text/plain"      , plainText
                ]
```

Then register an instance of the newly created class during application startup:

```fsharp
let configureServices (services : IServiceCollection) =
    // First register all default Giraffe dependencies
    services.AddGiraffe() |> ignore

    // Now register your custom INegotiationConfig
    services.AddSingleton<INegotiationConfig>(
        CustomNegotiationConfig(
            DefaultNegotiationConfig())
    ) |> ignore

[<EntryPoint>]
let main _ =
    WebHost.CreateDefaultBuilder()
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
```

In this example the `CustomNegotiationConfig` uses composition to re-use the `UnacceptableHandler` from the `DefaultNegotiationConfig` without having to use inheritance.

#### Configuring content negotiation through partial application

Alternatively you can also use the `NegotiateWithAsync` extension method or the `negotiateWith` http handler to configure content negotiation through partial function application:

```fsharp
let customNegotiationRules =
    dict [
        "*/*"             , json
        "application/json", json
        "application/xml" , xml
        "text/xml"        , xml
        "application/bson", bson
        "text/plain"      , plainText
    ]

let customUnacceptableHandler =
    setStatusCode 406
    >=> text "Request cannot be satisfied by the web server."

// Override the default negotiate handler with a new custom implementation
let negotiate =
    negotiateWith
        customNegotiationRules
        customUnacceptableHandler
```

### Streaming

Sometimes a large file or block of data has to be send to a client and in order to avoid loading the entire data into memory a Giraffe web application can use streaming to send a response in a more efficient way.

The `WriteStreamAsync` extension method and the `streamData` http handler can be used to stream an object of type `Stream` to a client.

Both functions accept the following parameters:

- `enableRangeProcessing`: If true a client can request a sub range of data to be streamed (useful when a client wants to continue streaming after a paused download, or when internet connection has been lost, etc.)
- `stream`: The stream object to be returned to the client.
- `eTag`: Entity header tag used for conditional requests (see [Conditional Requests](#conditional-requests)).
- `lastModified`: Last modified timestamp used for conditional requests (see [Conditional Requests](#conditional-requests)).

If the `eTag` or `lastModified` timestamp are set then both functions will also set the `ETag` and/or `Last-Modified` HTTP headers during the response:

```fsharp
let someStream : Stream = ...

let someHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Do stuff
            return! ctx.WriteStreamAsync(
                true, // enableRangeProcessing
                someStream,
                None, // eTag
                None) // lastModified
        }

// or...

let someHandler : HttpHandler =
    // Do stuff
    streamData
        true // enableRangeProcessing
        someStream
        None // eTag
        None // lastModified
```

In most cases a web application will want to stream a file directly from the local file system. In this case you can use the `WriteFileStreamAsync` extension method or the `streamFile` http handler, which are both the same as `WriteStreamAsync` and `streamData` except that they accept a relative or absolute `filePath` instead of a `Stream` object:

```fsharp
let someHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Do stuff
            return! ctx.WriteFileStreamAsync(
                true, // enableRangeProcessing
                "large-file.zip",
                None, // eTag
                None) // lastModified
        }

// or...

let someHandler : HttpHandler =
    // Do stuff
    streamFile
        true // enableRangeProcessing
        "large-file.zip"
        None // eTag
        None // lastModified
```

All streaming functions in Giraffe will also validate conditional HTTP headers, including the `If-Range` HTTP header if `enableRangeProcessing` has been set to `true`.

### Redirection

The `redirectTo (permanent : bool) (location : string)` http handler can be used to redirect a client to a different location when handling an incoming web request:

```fsharp
let webApp =
    choose [
        route "/new" >=> text "Hello World"
        route "/old" >=> redirectTo true "https://myserver.com/new"
    ]
```

Please note that if the `permanent` flag is set to `true` then the Giraffe web application will send a `301` HTTP status code to browsers which will tell them that the redirection is permanent. This often leads to browsers cache the information and not hit the deprecated URL a second time any more. If this is not desired then please set `permanent` to `false` in order to guarantee that browsers will continue hitting the old URL before redirecting to the (temporary) new one.

#### Safe Redirection

The `redirectTo` http handler, although giving you more freedom when specifying the redirection logic, does not validate for a common security problem named [open redirect](https://learn.snyk.io/lesson/open-redirect).

In order to deal with this threat you can either implement your own logic (example from Microsoft docs [Prevent open redirect attacks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/preventing-open-redirects)), or you can leverage the `safeRedirectTo (permanent: bool) (location: string)` http handler, which provides a handler with the necessary validation and a default error handler.

Furthermore, if you want to use Giraffe's own open redirect validation, although with a different error handler, you can use the `safeRedirectToExt (permanent: bool) (location: string) (invalidRedirectHandler: HttpHandler option)` http handler, which as the signature suggests, accepts a custom `invalidRedirectHandler` that will be executed if the validation fails.

### Response Caching

ASP.NET Core comes with a standard [Response Caching Middleware](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/middleware?view=aspnetcore-2.1) which works out of the box with Giraffe.

If you are not already using one of the two ASP.NET Core meta packages (`Microsoft.AspNetCore.App` or `Microsoft.AspNetCore.All`) then you will have to add an additional reference to the [Microsoft.AspNetCore.ResponseCaching](https://www.nuget.org/packages/Microsoft.AspNetCore.ResponseCaching/) NuGet package.

After adding the NuGet package you need to register the response caching middleware inside your application's startup code before registering Giraffe:

```fsharp
let configureServices (services : IServiceCollection) =
    services
        .AddResponseCaching() // <-- Here the order doesn't matter
        .AddGiraffe()         // This is just registering dependencies
    |> ignore

let configureApp (app : IApplicationBuilder) =
    app.UseGiraffeErrorHandler(errorHandler)
       .UseStaticFiles()     // Optional if you use static files
       .UseAuthentication()  // Optional if you use authentication
       .UseResponseCaching() // <-- Before UseGiraffe webApp
       .UseGiraffe webApp
```

After setting up the [ASP.NET Core response caching middleware](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/middleware?view=aspnetcore-2.1#configuration) you can use Giraffe's response caching http handlers to add response caching to your routes:

```fsharp
// A test handler which generates a new GUID on every request
let generateGuidHandler : HttpHandler =
    warbler (fun _ -> text (Guid.NewGuid().ToString()))

let webApp =
    GET >=> choose [
        route "/route1" >=> publicResponseCaching 30 None >=> generateGuidHandler
        route "/route2" >=> noResponseCaching >=> generateGuidHandler
    ]
```

Requests to `/route1` can be cached for up to 30 seconds whilst requests to `/route2` have response caching completely disabled.

*Note: if you test the above code with [Postman](https://www.getpostman.com/) then make sure you [disable the No-Cache feature](https://www.getpostman.com/docs/v6/postman/launching_postman/settings) in Postman in order to test the correct caching behaviour.*

Giraffe offers in total 4 http handlers which can be used to configure response caching for an endpoint.

In the above example we used the `noResponseCaching` http handler to completely disable response caching on the client and on any proxy server. The `noResponseCaching` http handler will send the following HTTP headers in the response:

```
Cache-Control: no-store, no-cache
Pragma: no-cache
Expires: -1
```

The `publicResponseCaching` or `privateResponseCaching` http handlers will enable response caching on the client and/or on proxy servers. The
`publicResponseCaching` http handler will set the `Cache-Control` directive to `public`, which means that not only the client is allowed to cache a response for the given cache duration, but also any intermediary proxy server as well as the ASP.NET Core middleware. This is useful for HTTP GET/HEAD endpoints which do not hold any user specific data, authentication data or any cookies and where the response data doesn't change frequently.

The `privateResponseCaching` http handler sets the `Cache-Control` directive to `private` which means that only the end client is allowed to store the response for the given cache duration. Proxy servers and the ASP.NET Core response caching middleware must not cache the response.

Both http handlers require the cache duration in seconds and an optional `vary` parameter:

```fsharp
// Cache for 10 seconds without any vary headers
publicResponseCaching 10 None

// Cache for 30 seconds with Accept and Accept-Encoding as vary headers
publicResponseCaching 30 (Some "Accept, Accept-Encoding")
```

The `vary` parameter specifies which HTTP request headers must be respected to vary the cached response. For example if an endpoint returns a different response (`Content-Type`) based on the client's `Accept` header (= [content negotiation](#content-negotiation)) then the `Accept` header must also be considered when returning a response from the cache. The same applies if the web server has response compression enabled. If a response varies based on the client's accepted compression algorithm then the cache must also respect the client's `Accept-Encoding` HTTP header when serving a response from the cache.

#### VaryByQueryKeys

The ASP.NET Core response caching middleware offers one more additional feature which is not part of the response's HTTP headers. By default, if a route is cacheable then the middleware will try to return a cached response even if the query parameters were different.

For example if a request to `/foo/bar` has been cached, then the cached version will also be returned if a request is made to `/foo/bar?query1=a` or `/foo/bar?query1=a&query2=b`.

Sometimes this is not desired and the `VaryByQueryKeys` feature lets the [middleware vary its cached responses based on a request's query keys](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/middleware?view=aspnetcore-2.1#varybyquerykeys).

The generic `responseCaching` http handler is the most basic response caching handler which can be used to configure custom response caching handlers as well as make use of the `VaryByQueryKeys` feature:

```fsharp
responseCaching
    (Public (TimeSpan.FromSeconds (float 5)))
    (Some "Accept, Accept-Encoding")
    (Some [| "query1"; "query2" |])
```

The first parameter is of type `CacheDirective` which is defines as following:

```fsharp
type CacheDirective =
    | NoCache
    | Public  of TimeSpan
    | Private of TimeSpan
```

The second parameter is an `string option` which defines the `vary` parameter.

The third and last parameter is a `string list option` which defines an optional list of query parameter values which must be used to vary a cached response by the ASP.NET Core response caching middleware. Please be aware that this feature only applies to the ASP.NET Core response caching middleware and will not be respected by any intermediate proxy servers.

### Response Compression

ASP.NET Core has its own [Response Compression Middleware](https://docs.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-2.1&tabs=aspnetcore2x) which works out of the box with Giraffe. There's no additional functionality or http handlers required in order to make it work with Giraffe web applications.

## Giraffe View Engine

Giraffe has its own functional view engine which can be used to build rich UIs for web applications. The single biggest and best contrast to other view engines (e.g. Razor, Liquid, etc.) is that the Giraffe View Engine is entirely functional, written in normal (and compiled) F# code.

This means that the Giraffe View Engine is by definition one of the most feature rich view engines available, requires no disk IO to load a view and views are automatically compiled at build time.

The Giraffe View Engine uses traditional functions and F# record types to generate rich HTML/XML views.

Please visit the [Giraffe.ViewEngine](https://github.com/giraffe-fsharp/Giraffe.ViewEngine) project page to learn more about it!

## Serialization

### JSON

By default, Giraffe uses `System.Text.Json` for (de-)serializing JSON content, using [JsonNamingPolicy.CamelCase](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties#use-a-built-in-naming-policy) as the *PropertyNamingPolicy*. Then, due to using this option for the *PropertyNamingPolicy*, your application needs to provide the input JSON in a format that conforms to the expectation, otherwise it will not be able to parse the value correctly. For example, consider that you want to interact with the following record:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name: string
        Make: string
        Wheels: int
        Built: DateTime
    }
```

Your client needs to send the data following the naming policy defined, like:

```bash
curl -X POST http://localhost:5000/json \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Model S",
    "make": "Tesla",
    "wheels": 4,
    "built": "2023-05-01T12:00:00"
}'
```

* Notice that the JSON keys start with lowercase letters.

Furthermore, an application can modify the default serializer by registering a new dependency which implements the [Json.ISerializer](https://github.com/giraffe-fsharp/Giraffe/blob/master/src/Giraffe/Json.fs) interface during application startup. Check the next example on how to use the `Json.FsharpFriendlySerializer` instead of `Json.Serializer` (C#-like), that uses the [Tarmil/FSharp.SystemTextJson](https://github.com/Tarmil/FSharp.SystemTextJson) project to customize `System.Text.Json`:

```fsharp
open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe

let webApp =
    choose [
        // ...
    ]

let configureServices (services: IServiceCollection) =
    services
        .AddGiraffe()
        // Here we add the custom serializer that uses `Json.FsharpFriendlySerializer'
        .AddSingleton<Json.ISerializer>(fun _ -> Json.FsharpFriendlySerializer() :> Json.ISerializer)
    |> ignore

let configureApp (appBuilder: IApplicationBuilder) =
    appBuilder.UseGiraffe(webApp)

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services

    let app = builder.Build()

    // ...

    configureApp app
    app.Run()

    0
```

* Check Tarmil's repository to learn more about the FSharp.SystemTextJson configuration, and how to tweak it considering your specific demands.

#### Using a different JSON serializer

Other than the JSON serializers provided by Giraffe, you can change the entire underlying JSON serializer by creating a new class that implements the [Json.ISerializer](https://github.com/giraffe-fsharp/Giraffe/blob/master/src/Giraffe/Json.fs) interface:

```fsharp
type CustomJsonSerializer() =
    interface Json.ISerializer with
        // Use different JSON library ...
        member __.SerializeToString<'T>      (x : 'T) = // ...
        member __.SerializeToBytes<'T>       (x : 'T) = // ...
        member __.SerializeToStreamAsync<'T> (x : 'T) = // ...

        member __.Deserialize<'T> (json : string) = // ...
        member __.Deserialize<'T> (bytes : byte[]) = // ...
        member __.DeserializeAsync<'T> (stream : Stream) = // ...
```

For example, one could define a `Newtonsoft.Json` serializer (inspired by [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)):

```fsharp
[<RequireQualifiedAccess>]
 module NewtonsoftJson =
     open System.IO
     open System.Text
     open System.Threading.Tasks
     open Microsoft.IO
     open Newtonsoft.Json
     open Newtonsoft.Json.Serialization

     type Serializer (settings : JsonSerializerSettings, rmsManager : RecyclableMemoryStreamManager) =
         let serializer = JsonSerializer.Create settings
         let utf8EncodingWithoutBom = UTF8Encoding(false)

         static member DefaultSettings =
             JsonSerializerSettings(
                 ContractResolver = CamelCasePropertyNamesContractResolver())

         interface Json.ISerializer with
             member __.SerializeToString (x : 'T) =
                 JsonConvert.SerializeObject(x, settings)

             member __.SerializeToBytes (x : 'T) =
                 JsonConvert.SerializeObject(x, settings)
                 |> Encoding.UTF8.GetBytes

             member __.SerializeToStreamAsync (x : 'T) (stream : Stream) =
                 task {
                     use memoryStream = rmsManager.GetStream("giraffe-json-serialize-to-stream")
                     use streamWriter = new StreamWriter(memoryStream, utf8EncodingWithoutBom)
                     use jsonTextWriter = new JsonTextWriter(streamWriter)
                     serializer.Serialize(jsonTextWriter, x)
                     jsonTextWriter.Flush()
                     memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
                     do! memoryStream.CopyToAsync(stream, 65536)
                 } :> Task

             member __.Deserialize<'T> (json : string) =
                 JsonConvert.DeserializeObject<'T>(json, settings)

             member __.Deserialize<'T> (bytes : byte array) =
                 let json = Encoding.UTF8.GetString bytes
                 JsonConvert.DeserializeObject<'T>(json, settings)

             member __.DeserializeAsync<'T> (stream : Stream) =
                 task {
                     use memoryStream = rmsManager.GetStream("giraffe-json-deserialize")
                     do! stream.CopyToAsync(memoryStream)
                     memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
                     use streamReader = new StreamReader(memoryStream)
                     use jsonTextReader = new JsonTextReader(streamReader)
                     return serializer.Deserialize<'T>(jsonTextReader)
                 }
```

Then register a new instance of the newly created type during application startup:

```fsharp
let configureServices (services : IServiceCollection) =
    // First register all default Giraffe dependencies
    services.AddGiraffe() |> ignore

    // Now register your custom Json.ISerializer
    services.AddSingleton<Json.ISerializer>(fun serviceProvider ->
            NewtonsoftJson.Serializer(JsonSerializerSettings(), serviceProvider.GetService<Microsoft.IO.RecyclableMemoryStreamManager>()) :> Json.ISerializer) |> ignore

[<EntryPoint>]
let main _ =
    WebHost.CreateDefaultBuilder()
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
```

Check this [samples/NewtonsoftJson](https://github.com/giraffe-fsharp/Giraffe/tree/master/samples/NewtonsoftJson) project to find this code in a working program.

#### Customizing JsonSerializerSettings

You can change the default `JsonSerializerSettings` of a JSON serializer by registering a new instance of `Json.ISerializer` during application startup. For example, the [`Microsoft.FSharpLu` project](https://github.com/Microsoft/fsharplu/wiki/fsharplu.json) provides a Newtonsoft JSON converter (`CompactUnionJsonConverter`) that serializes and deserializes `Option`s and discriminated unions much more succinctly. If you wanted to use it, and set the culture to German, your configuration would look something like:

 ```fsharp
 let configureServices (services : IServiceCollection) =
     // First register all default Giraffe dependencies
     services.AddGiraffe() |> ignore
     // Now customize only the Json.ISerializer by providing a custom
     // object of JsonSerializerSettings
     let customSettings = JsonSerializerSettings(
         Culture = CultureInfo("de-DE"))
     customSettings.Converters.Add(CompactUnionJsonConverter(true))
     services.AddSingleton<Json.ISerializer>(
         NewtonsoftJson.Serializer(customSettings)) |> ignore

 [<EntryPoint>]
 let main _ =
     WebHost.CreateDefaultBuilder()
         .Configure(Action<IApplicationBuilder> configureApp)
         .ConfigureServices(configureServices)
         .ConfigureLogging(configureLogging)
         .Build()
         .Run()
     0
 ```

#### Retrieving the JSON serializer from a custom HttpHandler

If you need you retrieve the registered JSON serializer from a custom `HttpHandler` function then you can do this with the `GetJsonSerializer` extension method:

```fsharp
let customHandler (dataObj : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let serializer = ctx.GetJsonSerializer()
        let json = serializer.Serialize dataObj
        // ... do more...
```

### XML

By default Giraffe uses the `System.Xml.Serialization.XmlSerializer` for (de-)serializing XML content. An application can modify the serializer by registering a new dependency which implements the `Xml.ISerializer` interface during application startup.

Customizing Giraffe's XML serialization can either happen via providing a custom object of `XmlWriterSettings` when instantiating the default `SystemXml.Serializer` or swap in an entire different XML library by creating a new class which implements the `Xml.ISerializer` interface.

Notice that Giraffe does secure XML parsing, i.e., when using the `Deserialize<'T>(xml: string)` method, both DTD (Document Type Definition) processing and external entities are disabled to prevent [XXE attacks](https://learn.snyk.io/lesson/xxe).

#### Customizing XmlWriterSettings

You can change the default `XmlWriterSettings` of the `SystemXml.Serializer` by registering a new instance of `SystemXml.Serializer` during application startup:

```fsharp
let configureServices (services : IServiceCollection) =
    // First register all default Giraffe dependencies
    services.AddGiraffe() |> ignore

    // Now customize the Xml.ISerializer
    let customSettings =
        XmlWriterSettings(
                Encoding           = Encoding.UTF8,
                Indent             = false,
                OmitXmlDeclaration = true
            )

    services.AddSingleton<Xml.ISerializer>(
        SystemXml.Serializer(customSettings)) |> ignore

[<EntryPoint>]
let main _ =
    WebHost.CreateDefaultBuilder()
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
```

####  Using a different XML serializer

You can change the entire underlying XML serializer by creating a new class which implements the `Xml.ISerializer` interface:

```fsharp
type CustomXmlSerializer() =
    interface Xml.ISerializer with
        // Use different XML library ...
        member __.Serialize (o : obj) = // ...
        member __.Deserialize<'T> (xml : string) = // ...
```

Then register a new instance of the newly created type during application startup:

```fsharp
let configureServices (services : IServiceCollection) =
    // First register all default Giraffe dependencies
    services.AddGiraffe() |> ignore

    // Now register your custom Xml.ISerializer
    services.AddSingleton<Xml.ISerializer, CustomXmlSerializer>() |> ignore

[<EntryPoint>]
let main _ =
    WebHost.CreateDefaultBuilder()
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
```

#### Retrieving the XML serializer from a custom HttpHandler

If you need you retrieve the registered XML serializer from a custom `HttpHandler` function then you can do this with the `GetXmlSerializer` extension method:

```fsharp
let customHandler (dataObj : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let serializer = ctx.GetXmlSerializer()
        let xml = serializer.Serialize dataObj
        // ... do more...
```

## Testing

Testing a Giraffe application follows the concept of [ASP.NET Core testing](https://docs.microsoft.com/en-us/aspnet/core/test/middleware?view=aspnetcore-3.1).

### Necessary imports:

```fsharp
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open Microsoft.AspNetCore.Hosting
open System.Net.Http
```

### Build a test host:

```fsharp
let getTestHost() =
    WebHostBuilder()
        .UseTestServer()
        .Configure(Action<IApplicationBuilder> [YourApp].configureApp)
        .ConfigureServices([YourApp].configureServices)
        .ConfigureLogging([YourApp].configureLogging)
        .UseUrls([YourUrl])
```

### Create a helper function to issue test requests:

```fsharp
let testRequest (request : HttpRequestMessage) =
    let resp = task {
        use server = new TestServer(getTestHost())
        use client = server.CreateClient()
        let! response = request |> client.SendAsync
        return response
    }
    resp.Result
```

### Examples (using Xunit):

```fsharp
// Import needed for the code below:
open System.Net

[<Fact>]
let ``Hello world endpoint says hello`` () =
    let response = testRequest (new HttpRequestMessage(HttpMethod.Get, "/hello-world"))
    let content = response.Content.ReadAsStringAsync().Result
    Assert.Equal(response.StatusCode, HttpStatusCode.OK)
    Assert.Equal(content, "hello")

[<Fact>]
let ``Example HTTP Post`` () =
    let request = new HttpRequestMessage(HttpMethod.Post, "/hello-world")
    request.Content <- "{\"JsonField\":\"JsonValue\"}"
    let response = testRequest request
    Assert.Equal(response.StatusCode, HttpStatusCode.OK)
    // Check the json content
```

## Miscellaneous

On top of default HTTP related functions such as `HttpContext` extension methods and `HttpHandler` functions Giraffe also provides a few other helper functions which are commonly required in Giraffe web applications.

### Short GUIDs and Short IDs

The `ShortGuid` and `ShortId` modules offer helper functions to work with [Short GUIDs](https://madskristensen.net/blog/A-shorter-and-URL-friendly-GUID) and [Short IDs](https://webapps.stackexchange.com/questions/54443/format-for-id-of-youtube-video) inside Giraffe.

#### ShortGuid

The `ShortGuid.fromGuid` function will convert a `System.Guid` into a URL friendly 22 character long `string` value.

The `ShortGuid.toGuid` function will convert a 22 character short GUID `string` into a valid `System.Guid` object. This function can be useful when converting a `string` query parameter into a valid `Guid` argument:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let guid =
            match ctx.TryGetQueryStringValue "id" with
            | None           -> Guid.Empty
            | Some shortGuid -> ShortGuid.toGuid shortGuid

        // Do something with `guid`...
        // Return a Task<HttpContext option>
```

#### ShortId

The `ShortId.fromUInt64` function will convert an `uint64` into a URL friendly 11 character long `string` value.

The `ShortId.toUInt64` function will convert a 11 character short ID `string` into a `uint64` value. This function can be useful when converting a `string` query parameter into a valid `uint64` argument:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let id =
            match ctx.TryGetQueryStringValue "id" with
            | None         -> 0UL
            | Some shortId -> ShortId.toUInt64 shortId

        // Do something with `id`...
        // Return a Task<HttpContext option>
```

Short GUIDs and short IDs can also be [automatically resolved from route arguments](#routef).

### Common Helper Functions

#### Additional useful HttpContext extension methods

The `GetRequestUrl` extension method of the `HttpContext` type can be used to retrieve the entire URL of the HTTP request as a `string` value:

```fsharp
let someHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let requestUrl = ctx.GetRequestUrl()
        text (sprintf "The request URL is: %s" requestUrl) next ctx
```

#### DateTime Extension methods

Giraffe automatically adds two extensions methods to the `DateTime` and `DateTimeOffset` objects. `ToIsoString()` formats a given timestamp into an [RFC3339](https://www.ietf.org/rfc/rfc3339.txt) formatted string and `ToHtmlString()` which formats the given timestamp into an [RFC822](https://www.ietf.org/rfc/rfc822.txt) formatted string:

```fsharp
let now = DateTimeOffset.UtcNow
let isoFormattedTimestamp = now.ToIsoString()
```
```fsharp
let now = DateTimeOffset.UtcNow
let htmlFormattedTimestamp = now.ToHtmlString()
```

#### isNotNull

The F# language provides an `isNull` function for checking `null` values when interoping with other .NET languages. Unfortunately there is no `isNotNull` function by default. Giraffe closes that gap by providing an additional `isNotNull` function:

```fsharp
if isNotNull someObj then
    // ... do stuff here
else
    // ... do other stuff here
```

#### strOption

An F# application often has to check if a `string` value is `null` when interoping with other .NET languages. Representing an optionally missing value with `null` is unnatural in F# and therefore Giraffe provides the `strOption` function which can convert a `string` into an `Option<string>` value for a more natural F# experience. If a string is `null` then the `strOptoin` function will return `None`, otherwise `Some string`:

```fsharp
let someDateTime =
    match strOption someString with
    | Some str -> DateTimeOffset.Parse str
    | None     -> DateTimeOffset.UtcNow
```

#### readFileAsStringAsync

Reading a file from the local file system is often a common use case in a web application. The `readFileAsStringAsync` function will asynchronously read the entire content of a given `filePath` from the local file system:

```fsharp
let someFunction =
    task {
        let! content = readFileAsStringAsync "myfile.txt"
        // ... do stuff
    }
```

### Computation Expressions

Giraffe provides two additional computation expressions which can be used with `Option<'T>` and `Result<'T, 'TError>` objects.


The `opt {}` computation expression can be used to bind options and the `res {}` computation expression can be used to bind result objects:

```fsharp
open Giraffe.ComputationExpressions

let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let result =
            res {
                let! header1 = ctx.GetRequestHeader "X-Header-1"
                let! header2 = ctx.GetRequestHeader "X-Header-2"
                let! header3 = ctx.GetRequestHeader "X-Header-3"
                return (header1, header2, header3)
            }
        match result with
        | Ok (h1, h2, h3) ->
            sprintf "%s, %s, %s" h1 h2 h3
            |> ctx.WriteTextAsync
        | Error msg -> RequestErrors.BAD_REQUEST msg next ctx
```

### CSRF Protection Helpers

CSRF stands for Cross-Site Request Forgery, and according to the OWASP website can be defined as:

> Cross-Site Request Forgery (CSRF) is an attack that forces an end user to execute unwanted actions on a web application in which they’re currently authenticated. With a little help of social engineering (such as sending a link via email or chat), an attacker may trick the users of a web application into executing actions of the attacker’s choosing. If the victim is a normal user, a successful CSRF attack can force the user to perform state changing requests like transferring funds, changing their email address, and so forth. If the victim is an administrative account, CSRF can compromise the entire web application.
>
> -- Reference [link](https://owasp.org/www-community/attacks/csrf).

The ASP.NET documentation gives us a tutorial on how to deal with it ([link](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery)), but you can also leverage the Giraffe's `HttpHandler` helpers from the `Csrf` module:

- `validateCsrfTokenExt (invalidTokenHandler: HttpHandler option)`: Validates the CSRF token from the request. Checks for token in header (`X-CSRF-TOKEN`) or form field (`__RequestVerificationToken`).
- `requireAntiforgeryTokenExt`: Alias for `validateCsrfTokenExt` - validates anti-forgery tokens from requests with custom error handler.
- `validateCsrfToken`: Validates the CSRF token from the request with default error handling. Checks for token in header (`X-CSRF-TOKEN`) or form field (`__RequestVerificationToken`). Uses default error handling (403 Forbidden) for invalid tokens.
- `requireAntiforgeryToken`: Alias for `validateCsrfToken` - validates anti-forgery tokens from requests.
- `generateCsrfToken`: Generates a CSRF token and adds it to the `HttpContext` items for use in views. The token can be accessed via `ctx.Items["CsrfToken"]` and `ctx.Items["CsrfTokenHeaderName"]`.
- `csrfTokenJson`: Returns the CSRF token as JSON for AJAX requests. Response format: `{ "token": "...", "headerName": "X-CSRF-TOKEN" }`.
- `csrfTokenHtml`: Returns the CSRF token as an HTML hidden input field. Can be included directly in forms.

## Additional Features

There's more features available for Giraffe web applications through additional NuGet packages:

### Endpoint Routing

Starting with Giraffe 5.x we introduced a new module called `Giraffe.EndpointRouting`. The endpoint routing module implements an alternative router to Giraffe's default routing functions which integrates with [ASP.NET Core's endpoint routing APIs](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-5.0).

Given the way how ASP.NET Core's Endpoint Routing works this module comes with several benefits (and unfortunately also some minor downsides) in comparison to Giraffe's default router. The main benefit of `Giraffe.EndpointRouting` is that it nicely integrates with the rest of ASP.NET Core and can benefit from everything which Endpoint Routing makes possible. It also means that any performance improvements made to the ASP.NET Core router will directly translate to Giraffe. The downsides are that several existing routing functions couldn't be ported to `Giraffe.EndpointRouting` and routes are case-insensitive by default. Whilst this can be a problem with some applications overall the limitations are minimal and the benefits should greatly outweigh the downsides in the long term. Endpoint Routing is definitely the new preferred option of routing in ASP.NET Core and will undoubtedly see a lot of investment and improvements by the ASP.NET team over the years.

At last it is possible to have the `Giraffe.EndpointRouting` module and Giraffe's default router work side by side, benefiting from Endpoint Routing where possible and keeping the default router elsewhere.

Notice that the usage of `Giraffe.EndpointRouting` is recommended, as described in [this issue](https://github.com/giraffe-fsharp/Giraffe/issues/534).

#### Endpoint Routing Basics

In order to make use of Giraffe's endpoint routing functions one has to open the required module first:

 ```fsharp
open Giraffe.EndpointRouting
```

Giraffe's HTTP handlers remain unchanged regardless if they are used from a typical Giraffe router or the `Giraffe.EndpointRouting` module. This makes porting to the `Giraffe.EndpointRouting` module tremendously easy:

```fsharp
let handler1 : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteTextAsync "Hello World"
```

Unlike Giraffe's default router (which really is just a big `HttpHandler` function often implemented with the help of the `choose` function) the endpoint router requires a flat list of `Endpoint` functions:

```fsharp
let endpoints =
    [
        GET [
            route "/" (text "Hello World")
            routef "/%s/%i" handler2
            routef "/%s/%s/%s/%i" handler3
        ]
        subRoute "/sub" [
            // Not specifying a http verb means it will listen to all verbs
            route "/test" handler1
        ]
    ]
```

Then the `Endpoint list` must be initialised with ASP.NET Core's `EndpointMiddleware` instead of being passed into the `GiraffeMiddleware`:

```fsharp
let configureApp (appBuilder : IApplicationBuilder) =
    appBuilder
        .UseRouting()
        .UseEndpoints(fun e -> e.MapGiraffeEndpoints(endpoints))
    |> ignore
```

The main differences are:

- Additionally to `HttpHandler` functions there is a new type called `Endpoint`
- The router is a flat list of `Endpoint` functions
- The `GET`, `POST`, `route`, etc. functions map a conventional `HttpHandler` to an `Endpoint` function (when the `Giraffe.EndpointRouting` module has been opened)
- The final `Endpoint list` has to be passed into ASP.NET Core's `EndpointMiddleware` instead of using the `GiraffeMiddleware`

The `MapGiraffeEndpoints` extension method translates those functions into the final `RequestDelegate` functions which the `EndpointMiddleware` relies on and therefore the `Giraffe.EndpointRouting` module doesn't add any extra overhead or runtime cost to ASP.NET Core's endpoint routing resolution.

#### Endpoint Routing Functions

The following routing functions are available as part of the `Giraffe.EndpointRouting` module:

- `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS`, `TRACE`, `CONNECT`
- `route`
- `routeWithExtensions` (*alpha*)
- `routef`
- `routefWithExtensions` (*alpha*)
- `subRoute`
- `subRouteWithExtensions` (*alpha*)

The `route`, `routef` and `subRoute` handlers are all case-insensitive. Other handlers such as `routex`, `subRoutef` or `choose` are not supported by the `Giraffe.EndpointRouting` module.

The `choose` handler is replaced by composing an `Endpoint list`.

Other routing handlers couldn't be ported like for like, but the ASP.NET Core Endpoint Routing API allows for greater control and better insight into an endpoint by exposing useful helper functions.

Using the `GetRouteData` extension method one can get access to route values and data tokens from within a handler:

```fsharp
open Microsoft.AspNetCore.Routing

let myHandler (foo : int, bar : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->

        let routeData = ctx.GetRouteData()
        routeData.Values     // Values produced on the current path
        routeData.DataTokens // Tokens produced on the current path

        sprintf "Yada Yada %i %s" foo bar
        |> ctx.WriteTextAsync
```

The `GetEndpoint` extension method returns the endpoint for the currently executed path and can be used to further explore the metadata and other data attached to this endpoint:

```fsharp
let myHandler (foo : int, bar : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let endpoint = ctx.GetEndpoint()
```

For more information about ASP.NET Core Endpoint Routing please refer to the [official documentation](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-5.0).

##### ALPHA :: Endpoint Routing Functions with Extensions

+ Note that this feature is currently in **alpha**, and major changes are expected.

ASP.NET Core provides several "extension" functions which can be used to fine-tune the HTTP handler behaviour. For example, there's the [Rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit) and [Output caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output) middlewares.

By using the Endpoint Routing module we can leverage this along with the `...WithExtensions` routing functions: `routeWithExtensions`, `routefWithExtensions` and `subRouteWithExtensions`.

Basically, whenever you decide to use a routing function variant with `...WithExtensions` you're required to provide as the first parameter a function that obbeys the `ConfigureEndpoint` type definition:

```fsharp
// Note: IEndpointConventionBuilder is a shorter version of Microsoft.AspNetCore.Builder.IEndpointConventionBuilder
type ConfigureEndpoint = IEndpointConventionBuilder -> IEndpointConventionBuilder
```

And you can use it like this:

```fsharp
let MY_RATE_LIMITER = "fixed"

let endpoints: list<Endpoint> =
    [
        GET [
            routeWithExtensions (fun eb -> eb.RequireRateLimiting MY_RATE_LIMITER) "/rate-limit" (text "Hello World")
            route "/no-rate-limit" (text "Hello World: No Rate Limit!")
        ]
    ]
```

In this example, we're using the ASP.NET [Rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit) middleware for the path `/rate-limit`, and not using it for `/no-rate-limit`. If you'd like to test it, check the sample at the [official repository](https://github.com/giraffe-fsharp/Giraffe) under the path *samples/RateLimiting/*. There's a `README.md` file with instructions on how to run it locally.

Note that for those extensions to work properly, you'll probably need to make additional changes to the server. Please check the official extension documentation page to know more about this.

### TokenRouter

The `Giraffe.TokenRouter` NuGet package exposes an alternative routing `HttpHandler` which is based on top of a [Radix Tree](https://en.wikipedia.org/wiki/Radix_tree). Several routing handlers (e.g.: `routef` and `subRoute`) have been overridden in such a way that path matching and value parsing are significantly faster than using the basic `choose` function.

This implementation assumes that additional memory and compilation time is not an issue. If speed and performance of parsing and path matching is required then the `Giraffe.TokenRouter` can be a much better fit.

Please check the official [Giraffe TokenRouter](https://github.com/giraffe-fsharp/Giraffe.TokenRouter) GitHub repository for more information.

### Razor

The `Giraffe.Razor` NuGet package adds fully featured Razor support to Giraffe web applications.

For more information please visit the official [Giraffe Razor](https://github.com/giraffe-fsharp/Giraffe.Razor) GitHub repository.

### DotLiquid

The `Giraffe.DotLiquid` NuGet package adds [DotLiquid](http://dotliquidmarkup.org/) support to Giraffe web applications.

For more information please visit the official [Giraffe DotLiquid](https://github.com/giraffe-fsharp/Giraffe.DotLiquid) GitHub repository.

### OpenApi

The `Giraffe.OpenApi` NuGet package, inspired by [Oxpecker's code](https://github.com/Lanayx/Oxpecker), adds [OpenAPI](https://swagger.io/specification/) support to Giraffe, helping to improve the documentation of the project.

For more information please visit the official [Giraffe OpenApi](https://github.com/giraffe-fsharp/Giraffe.OpenApi) GitHub repository.

## Special Mentions

### Saturn

[Saturn](https://github.com/SaturnFramework/Saturn) is an opinionated, web development framework built on top of Giraffe which implements the server-side, functional MVC pattern for F#.

Saturn is not directly part of Giraffe but builds a [Phoenix](http://phoenixframework.org/) inspired MVC pattern on top of Giraffe. It is being developed and maintained by the author of the [Ionide project](https://github.com/ionide/ionide-vscode-fsharp).

## Appendix

### Aleksander Heintz's query string binder API

```fsharp
[<AutoOpen>]
module Giraffe.Query

open Aether
open Microsoft.AspNetCore.Http

module Helpers =
  let konst v _ = v

  [<RequireQualifiedAccess>]
  module Option =
    let inline ofBool b = if b then Some [] else None

open Helpers

[<AutoOpen>]
module Values =
  type QueryValue = string list option

  [<RequireQualifiedAccess>]
  module QueryValue =

    let inline private (|Empty|NonEmpty|) xs =
      match xs with
      | [] -> Empty
      | _  -> NonEmpty xs

    (* Epimorphisms *)

    let private Zero__ =
      (function | None -> Some ()
                | _    -> None), konst None

    let private Bool__ =
      (function | None       -> Some false
                | Some Empty -> Some true
                | _          -> None), Option.ofBool

    let private String__ =
      (function | Some [v] -> Some v
                | _        -> None), List.singleton >> Some

    let private List__ =
      (function | None    -> Some []
                | Some vs -> Some vs), Some

    (* Prisms *)

    let Zero_ =
      Prism.ofEpimorphism Zero__

    let Bool_ =
      Prism.ofEpimorphism Bool__

    let String_ =
      Prism.ofEpimorphism String__

    let List_ =
      Prism.ofEpimorphism List__

  (* Functional *)
  [<AutoOpen>]
  module Functional =
    type QueryValueResult<'a> = Result<'a, string>
    type QueryValue<'a> = QueryValue -> QueryValueResult<'a> * QueryValue

    (* Functions *)

    [<RequireQualifiedAccess>]
    module QueryValue =
      let inline unit (a: 'a) : QueryValue<_> =
        fun value ->
          Ok a, value

      let zero = unit ()

      let inline error (e: string) : QueryValue<_> =
        fun value ->
          Error e, value

      let inline internal ofResult result =
        fun value ->
          result, value

      let inline bind (m: QueryValue<'a>) (f: 'a -> QueryValue<'b>) : QueryValue<'b> =
        fun value ->
          match m value with
          | Ok a, value    -> f a value
          | Error e, value -> Error e, value

      let inline apply (f: QueryValue<'a -> 'b>) (m: QueryValue<'a>) : QueryValue<'b> =
        bind f (fun f' ->
          bind m (f' >> unit))

      let inline map (f: 'a -> 'b) (m: QueryValue<'a>) : QueryValue<'b> =
        bind m (f >> unit)

      let inline map2 (f: 'a -> 'b -> 'c) (m1: QueryValue<'a>) (m2: QueryValue<'b>) : QueryValue<'c> =
        apply (apply (unit f) m1) m2

    (* Operators *)

    module Operators =
      let inline (>>=) m f =
        QueryValue.bind m f

      let inline (=<<) f m =
        QueryValue.bind m f

      let inline (<*>) f m =
        QueryValue.apply f m

      let inline (<!>) f m =
        QueryValue.map f m

      let inline ( *>) m1 m2 =
        QueryValue.map2 (konst id) m1 m2

      let inline (<* ) m1 m2 =
        QueryValue.map2 konst m1 m2

      let inline (>=>) f g =
        fun x -> f x >>= g

      let inline (<=<) g f =
        fun x -> f x >>= g

  module Builder =
    open Operators

    type QueryValueBuilder () =
      member inline __.Bind (m1, f) = m1 >>= f

      member inline __.Combine (m1, m2) = m1 *> m2

      member inline __.Delay f = QueryValue.zero >>= f

      member inline __.Return x = QueryValue.unit x

      member inline __.Zero () = QueryValue.zero

  let queryValue = Builder.QueryValueBuilder ()

  [<AutoOpen>]
  module Optic =

    [<RequireQualifiedAccess>]
    module QueryValue =

      [<RequireQualifiedAccess>]
      module Optic =

        type Get =
          | Get with

          static member (^.) (Get, l: Lens<QueryValue, 'b>) : QueryValue<_> =
            fun value ->
              Ok (Optic.get l value), value

          static member (^.) (Get, p: Prism<QueryValue, 'b>) : QueryValue<_> =
            fun value ->
              match Optic.get p value with
              | Some x -> Ok x, value
              | None   -> Error (sprintf "Couldn't use Prism %A on query string value: '%A'" p value), value

        let inline get o : QueryValue<_> =
          (Get ^. o)

        type TryGet =
          | TryGet with

          static member (^.) (TryGet, l: Lens<QueryValue, 'b>) : QueryValue<_> =
            fun value ->
              Ok (Some (Optic.get l value)), value

          static member (^.) (TryGet, p: Prism<QueryValue, 'b>) : QueryValue<_> =
            fun value ->
              Ok (Optic.get p value), value

        let inline tryGet o : QueryValue<_> =
          (TryGet ^. o)

        let inline set o v : QueryValue<_> =
          fun query ->
            Ok (), Optic.set o v query

        let inline map o f : QueryValue<_> =
          fun query ->
            Ok (), Optic.map o f query

  [<AutoOpen>]
  module Mapping =
    open Operators

    (* From *)

    (* Defaults *)

    type FromQueryValueDefaults = FromQueryValueDefaults with

      (* Basic Types *)

      static member inline FromQueryValue (_: unit) =
        QueryValue.Optic.get QueryValue.Zero_

      static member inline FromQueryValue (_: bool) =
        QueryValue.Optic.get QueryValue.Bool_

      static member inline FromQueryValue (_: string) =
        QueryValue.Optic.get QueryValue.String_

      static member inline FromQueryValue (_: QueryValue) =
        QueryValue.Optic.get id_

    (* Mapping Functions *)

    let inline internal fromQueryValueDefaults (a: ^a, _: ^b) =
      ((^a or ^b) : (static member FromQueryValue: ^a -> ^a QueryValue) a)

    let inline internal fromQueryValue x =
      fst (fromQueryValueDefaults (Unchecked.defaultof<'a>, FromQueryValueDefaults) x)

    let inline internal fromQueryValueFold xs =
      List.fold (fun r x ->
        match r with
        | Error e -> Error e
        | Ok xs   ->
          match fromQueryValue x with
          | Ok x    -> Ok (x :: xs)
          | Error e -> Error e) (Ok []) (xs |> List.map (List.singleton >> Some) |> List.rev)

    let inline private tryParse name f =
      fun value ->
        match f value with
        | true, v -> fun value -> Ok v, value
        | _       -> fun value -> Error (sprintf "Failed to parse '%A' as %s" value name), value

    (* Defaults *)

    open System
    type FromQueryValueDefaults with

      (* Numbers *)

      static member inline FromQueryValue (_: float) =
            tryParse "float" Double.TryParse
        =<< QueryValue.Optic.get QueryValue.String_

      static member inline FromQueryValue (_: decimal) =
            tryParse "decimal" Decimal.TryParse
        =<< QueryValue.Optic.get QueryValue.String_

      static member inline FromQueryValue (_: int) =
            tryParse "int" Int32.TryParse
        =<< QueryValue.Optic.get QueryValue.String_

      static member inline FromQueryValue (_: int16) =
            tryParse "int16" Int16.TryParse
        =<< QueryValue.Optic.get QueryValue.String_

      static member inline FromQueryValue (_: int64) =
            tryParse "int64" Int64.TryParse
        =<< QueryValue.Optic.get QueryValue.String_

      static member inline FromQueryValue (_: float32) =
            tryParse "float32" Single.TryParse
        =<< QueryValue.Optic.get QueryValue.String_

      static member inline FromQueryValue (_: uint16) =
            tryParse "uint16" UInt16.TryParse
        =<< QueryValue.Optic.get QueryValue.String_

      static member inline FromQueryValue (_: uint32) =
            tryParse "uint32" UInt32.TryParse
        =<< QueryValue.Optic.get QueryValue.String_

      static member inline FromQueryValue (_: uint64) =
            tryParse "uint64" UInt64.TryParse
        =<< QueryValue.Optic.get QueryValue.String_

      (* Lists *)

      static member inline FromQueryValue (_: 'a list) : QueryValue<'a list> =
            fromQueryValueFold >> QueryValue.ofResult
        =<< QueryValue.Optic.get QueryValue.List_

      static member inline FromQueryValue (_: 'a array) : QueryValue<'a array> =
            fromQueryValueFold >> Result.map Array.ofList >> QueryValue.ofResult
        =<< QueryValue.Optic.get QueryValue.List_


      (* Set *)

      static member inline FromQueryValue (_: Set<'a>) : QueryValue<Set<'a>> =
            fromQueryValueFold >> Result.map Set.ofList >> QueryValue.ofResult
        =<< QueryValue.Optic.get QueryValue.List_


      (* Options *)

      static member inline FromQueryValue (_: 'a option) : QueryValue<'a option> =
        fun value ->
          match fromQueryValue value with
          | Ok v    -> Ok (Some v), value
          | _       -> Ok None, value

type Query = Map<string, string list>

module Convert =
  open Microsoft.AspNetCore.WebUtilities

  let toQuery (qs: QueryString) : Query =
    QueryHelpers.ParseQuery qs.Value
    |> Seq.map (fun kvp -> kvp.Key, kvp.Value |> List.ofSeq)
    |> Map.ofSeq

(* Functional *)

[<AutoOpen>]
module Functional =
  type QueryResult<'a> = Result<'a, string>
  type Query<'a> = Query -> QueryResult<'a> * Query

  (* Functions *)

  [<RequireQualifiedAccess>]
  module Query =
    let inline unit x : Query<_> =
      fun query ->
        Ok x, query

    let zero = unit ()

    let inline error e : Query<_> =
      fun query ->
        Error e, query

    let inline internal ofResult result =
      fun query ->
        result, query

    let inline bind (m: Query<'a>) (f: 'a -> Query<'b>) : Query<'b> =
      fun query ->
        match m query with
        | Ok a, query    -> f a query
        | Error e, query -> Error e, query

    let inline apply f m =
      bind f (fun f' ->
        bind m (f' >> unit))

    let inline map f m =
      bind m (f >> unit)

    let inline map2 f m1 m2 =
      apply (apply (unit f) m1) m2

(* Operators *)

module Operators =
  let inline (>>=) m f =
    Query.bind m f

  let inline (=<<) f m =
    Query.bind m f

  let inline (<*>) f m =
    Query.apply f m

  let inline (<!>) f m =
    Query.map f m

  let inline ( *>) m1 m2 =
    Query.map2 (konst id) m1 m2

  let inline (<* ) m1 m2 =
    Query.map2 konst m1 m2

  let inline (>=>) f g =
    fun x -> f x >>= g

  let inline (<=<) g f =
    fun x -> f x >>= g

(* Builder *)

module Builder =
  open Operators

  type QueryBuilder () =
    member inline __.Bind (m1, f) = m1 >>= f

    member inline __.Combine (m1, m2) = m1 *> m2

    member inline __.Delay f = Query.zero >>= f

    member inline __.Return x = Query.unit x

    member inline __.Zero () = Query.zero

let query = Builder.QueryBuilder ()

[<AutoOpen>]
module Mapping =
  open Operators

  (* From *)

  (* Defaults *)

  type FromQueryDefaults = FromQueryDefaults with

    static member inline FromQuery (_: Query) : Query<Query> =
      fun query -> Ok query, query

    static member inline FromQuery (_: Map<string, string>) : Query<Map<string, string>> =
      fun query ->
        let ret =
          query
          |> Map.filter (konst (function | [_] -> true | _ -> false))
          |> Map.map (konst List.head)
        Ok ret, query

  (* Mapping Functions *)

  let inline internal fromQueryDefaults (a: ^a, _: ^b) =
    ((^a or ^b) : (static member FromQuery: ^a -> ^a Query) a)

  let inline internal fromQuery x =
    fst (fromQueryDefaults (Unchecked.defaultof<'a>, FromQueryDefaults) x)

  (* Functions *)

  [<RequireQualifiedAccess>]
  module Query =

    (* Read *)

    let private readValue key =
      fun query ->
        Ok (Map.tryFind key query), query

    let readMemberWith fromQueryValue key =
         readValue key
      >>= fun value ->
         match fromQueryValue value with
         | Ok v    -> Query.unit v
         | Error e -> Query.error (sprintf "%s: %s" key e)

    let inline readWith fromQueryValue key =
      readMemberWith fromQueryValue key

    let inline read key =
      readWith fromQueryValue key

    let inline parse qs =
      fromQuery (Convert.toQuery qs)
      |> function | Ok a         -> a
                  | Error e      -> failwith e

    let inline tryParse qs =
      fromQuery (Convert.toQuery qs)
      |> function | Ok a         -> Some a
                  | Error _      -> None

[<AutoOpen>]
module HttpHandlers =
  open Giraffe.HttpHandlers
  open System.Threading.Tasks

  module Query =

    let inline bind (f: ^a -> HttpHandler) : HttpHandler =
      fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
          match Query.tryParse ctx.Request.QueryString with
          | None   -> return None
          | Some a -> return! f a next ctx
        }

    let inline bindTask (f: ^a -> Task<HttpHandler>) : HttpHandler =
      fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
          match Query.tryParse ctx.Request.QueryString with
          | None   -> return None
          | Some a ->
            let! handler = f a
            return! handler next ctx
        }
```
