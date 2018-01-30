# Giraffe Documentation

## Table of contents

- [Fundamentals](#fundamentals)
    - [HttpHandler](#httphandler)
    - [Giraffe pipeline vs. ASP.NET Core pipeline](#giraffe-pipeline-vs-aspnet-core-pipeline)
    - [Combinators](#combinators)
        - [compose (>=>)](#compose-)
        - [choose](#choose)
    - [Warbler](#warbler)
    - [Tasks](#tasks)
- [Basics](#basics)
    - [Dependency Management](#dependency-management)
    - [Multiple Environments and Configuration](#multiple-environments-and-configuration)
    - [Logging](#logging)
    - [Error Handling](#error-handling)
- [Web Request Processing](#web-request-processing)
    - [HTTP Headers](#http-headers)
    - [HTTP Verbs](#http-verbs)
    - [HTTP Status Codes](#http-status-codes)
    - [Routing](#routing)
    - [Authentication and Authorization](#authentication-and-authorization)
    - [Query Strings](#query-strings)
    - [Model Binding](#model-binding)
    - [Conditional Requests](#conditional-requests)
    - [Content Negotiation](#content-negotiation)
    - [Response Writing](#response-writing)
    - [Streaming](#streaming)
    - [Redirection](#redirection)
- [Giraffe View Engine](#giraffe-view-engine)
    - [HTML Elements](#html-elements)
    - [HTML Attributes](#html-attributes)
    - [Custom Elements](#custom-elements)
    - [Rendering Views](#rendering-views)
- [Serialization](#serialization)
    - [JSON](#json)
    - [XML](#xml)
- [Miscellaneous](#miscellaneous)
    - [Common Helper Functions](#common-helper-functions)
    - [Computation Expressions](#computation-expressions)
- [Additional Features](#additional-features)
    - [TokenRouter](#tokenrouter)
    - [Razor](#razor)
    - [DotLiquid](#dotliquid)

## Fundamentals

### HttpHandler

The main building block in Giraffe is a so called `HttpHandler`:

```fsharp
type HttpFuncResult = Task<HttpContext option>
type HttpFunc = HttpContext -> HttpFuncResult
type HttpHandler = HttpFunc -> HttpContext -> HttpFuncResult
```

A `HttpHandler` is a simple function which takes two curried arguments, a `HttpFunc` and a `HttpContext`, and returns a `HttpContext` (wrapped in an `option` and `Task` workflow) when finished.

On a high level a `HttpHandler` function receives and returns an ASP.NET Core `HttpContext` object, which means every `HttpHandler` function has full control of the incoming `HttpRequest` and the resulting `HttpResponse`.

Each `HttpHandler` can process an incoming `HttpRequest` before passing it further down the Giraffe pipeline by invoking the next `HttpFunc` or short circuit the execution by returning an option of `Some HttpContext`.

If a `HttpHandler` doesn't want to process an incoming `HttpRequest` at all, then it can return `None` instead. In this case a surrounding `HttpHandler` might pick up the incoming `HttpRequest` or the Giraffe middleware will defer the request to the next `RequestDelegate` from the ASP.NET Core pipeline.

The easiest way to get your head around a Giraffe `HttpHandler` is to think of it as a functional equivalent to the ASP.NET Core middleware. Each handler has the full `HttpContext` at its disposal and can decide whether it wants to return `Some HttpContext`, `None` or pass it on to the "next" `HttpFunc`.

### Giraffe pipeline vs. ASP.NET Core pipeline

The Giraffe pipeline is a (sort of) functional equivalent of the (object oriented) ASP.NET Core pipeline. The ASP.NET Core pipeline is defined by nested middleware and the Giraffe pipeline is defined by `HttpHandler` functions. The Giraffe pipeline is plugged into the wider ASP.NET Core pipeline through the `GiraffeMiddleware` itself and therefore an addition to it rather than a replacement.

If the Giraffe pipeline didn't process an incoming `HttpRequest` (because the final result was `None` and not `Some HttpContext`) then other ASP.NET Core middleware can still process the request (e.g. static file middleware or another web framework plugged in after Giraffe).

This architecture allows F# developers to build rich web applications through a functional composition of `HttpHandler` functions while at the same time benefiting from the wider ASP.NET Core eco system by making use of already existing ASP.NET Core middleware.

### Combinators

#### compose (>=>)

The `compose` combinator combines two `HttpHandler` functions into one:

```fsharp
let compose (handler1 : HttpHandler) (handler2 : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) ->
        let func = next |> handler2 |> handler1
        fun (ctx : HttpContext) ->
            match ctx.Response.HasStarted with
            | true  -> next ctx
            | false -> func ctx
```

It is the main combinator as it allows composing many smaller `HttpHandler` functions into a bigger web application.

If you would like to learn more about the `>=>` (fish) operator then please check out [Scott Wlaschin's blog post on Railway oriented programming](http://fsharpforfunandprofit.com/posts/recipe-part2/).

##### Example:

```fsharp
let app = route "/" >=> Successful.OK "Hello World"
```

#### choose

The `choose` combinator function iterates through a list of `HttpHandler` functions and invokes each individual handler until the first `HttpHandler` returns a positive result.

##### Example:

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
A warbler will ensure that a function will get evaluated every time the route is hit.

#### Example
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

Another important aspect of Giraffe is that it natively works with .NET's `Task` and `Task<'T>` objects instead of relying on F#'s `async {}` workflows. The main benefit of this is that it removes the necessity of converting back and forth between tasks and async workflows when building a Giraffe web application (because ASP.NET Core only works with tasks out of the box).

For this purpose Giraffe has it's own `task {}` workflow which comes with the `Giraffe.Tasks` NuGet package. Syntactically it works identical to F#'s async workflows:

```fsharp
open Giraffe.Tasks
open Giraffe.HttpHandlers

let personHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! person = ctx.BindModelAsync<Person>()
            return! json person next ctx
        }
```

The `task {}` workflow is not strictly tied to Giraffe and can also be used from other places in an F# application. All you have to do is add a reference to the `Giraffe.Tasks` NuGet package and open the module:

```fsharp
open Giraffe.Tasks

let readFileAndDoSomething (filePath : string) =
    task {
        use stream = new FileStream(filePath, FileMode.Open)
        use reader = new StreamReader(stream)
        let! contents = reader.ReadToEndAsync()

        // do something with contents

        return contents
    }
```

For more information please visit the official [Giraffe.Tasks](https://github.com/giraffe-fsharp/Giraffe.Tasks) GitHub repository.

## Basics

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
    WebHostBuilder()
        .UseKestrel()
        .Configure(Action<IApplicationBuilder> configureApp)
        // Calling ConfigureServices to set up dependencies
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
```

#### Retrieving Services

Retrieving registered services from within a Giraffe `HttpHandler` function is done through the built in service locator (`RequestServices`) which comes with a `HttpContext` object:

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

Additionally Giraffe exposes a `GetHostingEnvironment()` extension method which can be used to easier retrieve an `IHostingEnvironment` object from within a `HttpHandler` function:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let env = ctx.GetHostingEnvironment()
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
    WebHostBuilder()
        .UseKestrel()
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        // Calling ConfigureLogging to set up logging providers
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
```

Just like dependency management the logging API is configured the same way as it is done for any other ASP.NET Core web application.

#### Logging from within a HttpHandler function

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

The Giraffe `ErrorHandler` function accepts an `Exception` object and a default `ILogger` and returns a `HttpHandler` function:

```fsharp
type ErrorHandler = exn -> ILogger -> HttpHandler
```

Because the Giraffe `ErrorHandler` returns a `HttpHandler` function it is possible to create anything from a simple error handling function to a complex error handling application.

#### Simple ErrorHandler example

This simple `errorHandler` function writes the entire `Exception` object to the logs, clears the response object and returns a HTTP 500 server error response:

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
    WebHostBuilder()
        .UseKestrel()
        // Calling Configure to set up all middleware
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
```

... or the equivalent by using a `Startup` class:

```fsharp
type Startup() =
    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =
        app.UseGiraffeErrorHandler errorHandler
           .UseGiraffe webApp

[<EntryPoint>]
let main _ =
    WebHostBuilder()
        .UseKestrel()
        .UseStartup<Startup>()
        .Build()
        .Run()
    0
```

It is recommended to set the error handler as the first middleware in the ASP.NET Core pipeline, so that any unhandled exception from other middleware can be caught and processed by the error handling function.

## Web Request Processing

Giraffe comes with a large set of default `HttpContext` extension methods as well as default `HttpHandler` functions which can be used to build rich web applications.

### HTTP Headers

Working with HTTP headers in Giraffe is plain simple. The `TryGetRequestHeader (key : string)` extension method tries to retrieve the value of a given HTTP header and either returns `Some string` or `None`:

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

This method is useful for retrieving optional HTTP headers in a web application.

If a HTTP header is mandatory then the `GetRequestHeader (key : string)` extension method might be a better fit. Instead of returning an `Option<string>` it returns a `Result<string, string>` object:

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

Setting a HTTP header in the response can be done via the `SetHttpHeader (key : string) (value : obj)` extension method:

```fsharp
let someHttpHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetHttpHeader "X-CustomHeader" "some-value"
        // Do other stuff...
        // Return a Task<HttpContext option>
```

You can also set a HTTP header via the `setHttpHeader` handler:

```fsharp
let notFoundHandler : HttpHandler =
    setHttpHeader "X-CustomHeader"
    >=> RequestErrors.NOT_FOUND "Not Found"

let webApp =
    choose [
        route "/foo" >=> text "Foo"
        route "/bar" >=> text "Bar"
        notFoundHandler
    ]
```

Please note that these are additional Giraffe functions which complement already existing HTTP header functionality exposed through the core ASP.NET Core framework. ASP.NET Core offers higher level HTTP header functionality through the `ctx.Request.GetTypedHeaders()` method.

### HTTP Verbs


- [HTTP Status Codes](#http-status-codes)
- [Routing](#routing)
- [Authentication and Authorization](#authentication-and-authorization)
- [Query Strings](#query-strings)
- [Model Binding](#model-binding)
- [Conditional Requests](#conditional-requests)
- [Content Negotiation](#content-negotiation)
- [Response Writing](#response-writing)
- [Streaming](#streaming)
- [Redirection](#redirection)