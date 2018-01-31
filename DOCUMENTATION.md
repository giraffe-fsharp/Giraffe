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
    - [Query Strings](#query-strings)
    - [Authentication and Authorization](#authentication-and-authorization)
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

This method is useful when trying to retrieve optional HTTP headers from within a `HttpHandler`.

If a HTTP header is mandatory then the `GetRequestHeader (key : string)` extension method might be a better fit. Instead of returning an `Option<string>` object it will return a `Result<string, string>` type:

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

You can also set a HTTP header via the `setHttpHeader` http handler function:

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

This can be useful when implementing a different `HttpHandler` function for the same route, but for different verbs:

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

If you need to check the request's HTTP verb from within a `HttpHandler` function then you can use the default ASP.NET Core `HttpMethods` class:

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

The lower case version let's you combine the `HttpHandler` function with another `HttpHandler` function:

```fsharp
Successful.ok (text "Hello World")
```

This is basically a shorter (and more explicit) version of:

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

*Please be aware that there is no module for `3xx` HTTP status codes available, instead it is recommended to use the `redirectTo` http handler function (see [Redirection](#redirection)).*

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

#### RequestErrors

| HTTP Status Code | Function name | Example |
| ---------------- | ------------- | ------- |
| 400 | badRequest | `route "/" >=> RequestErrors.badRequest (text "Don't like it")` |
| 400 | BAD_REQUEST | `route "/" >=> RequestErrors.BAD_REQUEST "Don't like it"` |
| 401 | unauthorized | `route "/" >=> RequestErrors.unauthorized "Basic" "MyApp" (text "Don't know who you are")` |
| 401 | UNAUTHORIZED | `route "/" >=> RequestErrors.UNAUTHORIZED "Don't know who you are"` |
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

The simplest form of routing can be done with the `route` http handler function:

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
        routeCi "/foo" >=> text "Foo"
        routeCi "/bar" >=> text "Bar"

        // If none of the routes matched then return a 404
        RequestErrors.NOT_FOUND "Not Found"
    ]
```

#### routef

If a route contains user defined parameters then the `routef` http handler can be handy:

```fsharp
let fooHandler (first : string)
               (last  : string)
               (age   : int)
               : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        (sprintf "First: %s, Last: %s, Age: %i" first last age
        |> text) next ctx

let webApp =
    choose [
        routef "/foo/%s/%s/%i" fooHandler
        routef "/bar/%O" (fun guid -> text (guid.ToString()))

        // If none of the routes matched then return a 404
        RequestErrors.NOT_FOUND "Not Found"
    ]
```

The `routef` http handler takes two parameters - a format string and a `HttpHandler` function.

The format string supports the following format chars:

| Format Char | Type |
| ----------- | ---- |
| `%b` | `bool` |
| `%c` | `char` |
| `%s` | `string` |
| `%i` | `int` |
| `%d` | `int64` |
| `%f` | `float`/`double` |
| `%O` | `Guid` |

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

The `routeBind<'T>` http handler can also contain valid `Regex` code to match a variety of different routes.

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

#### routeStartsWtih

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

In contrast to `routeStartsWith` the `subRoute` http handler let's you categorise routes without having to repeat already pre-filtered parts of the route:

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

**Note:** For both `subRoute` and `subRouteCi` if you wish to have a route that represents a default e.g. `/api/v1` (from the above example) then you need to specify the route as `route ""` not `route "/"` this will not match, as `api/v1/` is a fundamentally different route according to the HTTP specification.

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


- [Authentication and Authorization](#authentication-and-authorization)
- [Model Binding](#model-binding)
- [Conditional Requests](#conditional-requests)
- [Content Negotiation](#content-negotiation)
- [Response Writing](#response-writing)
- [Streaming](#streaming)
- [Redirection](#redirection)