# Giraffe

![Giraffe](https://raw.githubusercontent.com/giraffe-fsharp/Giraffe/develop/giraffe.png)

A functional ASP.NET Core micro web framework for building rich web applications.

Read [this blog post on functional ASP.NET Core](https://dusted.codes/functional-aspnet-core) for more information.

[![NuGet Info](https://buildstats.info/nuget/Giraffe?includePreReleases=true)](https://www.nuget.org/packages/Giraffe/)

| Windows | Linux |
| :------ | :---- |
| [![Windows Build status](https://ci.appveyor.com/api/projects/status/0ft2427dflip7wti/branch/develop?svg=true)](https://ci.appveyor.com/project/dustinmoris/giraffe/branch/develop) | [![Linux Build status](https://travis-ci.org/giraffe-fsharp/Giraffe.svg?branch=develop)](https://travis-ci.org/giraffe-fsharp/Giraffe/builds?branch=develop) |
| [![Windows Build history](https://buildstats.info/appveyor/chart/dustinmoris/giraffe?branch=develop&includeBuildsFromPullRequest=false)](https://ci.appveyor.com/project/dustinmoris/giraffe/history?branch=develop) | [![Linux Build history](https://buildstats.info/travisci/chart/giraffe-fsharp/Giraffe?branch=develop&includeBuildsFromPullRequest=false)](https://travis-ci.org/giraffe-fsharp/Giraffe/builds?branch=develop) |


## Table of contents

- [About](#about)
- [Installation](#installation)
- [Default HttpHandlers](#default-httphandlers)
    - [mustAccept](#mustaccept)
    - [clearResponse](#clearResponse)
- [Additional HttpHandlers](#additional-httphandlers)
    - [Giraffe.TokenRouter](#giraffetokenrouter)
        - [router](#router)
        - [routing functions](#routing-functions)
    - [Additional NuGet packages](#additional-nuget-packages)
- [Custom HttpHandlers](#custom-httphandlers)
- [Customizing Giraffe](#customizing-giraffe)
    - [Customize JSON serialization](#customize-json-serialization)
    - [Customize XML serialization](#customize-xml-serialization)
- [Sample applications](#sample-applications)
- [Benchmarks](#benchmarks)
- [Building and developing](#building-and-developing)
- [Contributing](#contributing)
- [Nightly builds and NuGet feed](#nightly-builds-and-nuget-feed)
- [Blog posts](#blog-posts)
- [Videos](#videos)
- [License](#license)
- [Contact and Slack Channel](#contact-and-slack-channel)

## About

[Giraffe](https://www.nuget.org/packages/Giraffe) is an F# micro web framework for building rich web applications. It has been heavily inspired and is similar to [Suave](https://suave.io/), but has been specifically designed with [ASP.NET Core](https://www.asp.net/core) in mind and can be plugged into the ASP.NET Core pipeline via [middleware](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware). Giraffe applications are composed of so called `HttpHandler` functions which can be thought of a mixture of Suave's WebParts and ASP.NET Core's middleware.

If you'd like to learn more about the motivation of this project please read my [blog post on functional ASP.NET Core](https://dusted.codes/functional-aspnet-core) (some code samples in this blog post might be outdated today).

### Who is it for?

[Giraffe](https://www.nuget.org/packages/Giraffe) is intended for developers who want to build rich web applications on top of ASP.NET Core in a functional first approach. ASP.NET Core is a powerful web platform which has support by Microsoft and a huge developer community behind it and Giraffe is aimed at F# developers who want to benefit from that eco system.

It is not designed to be a competing web product which can be run standalone like NancyFx or Suave, but rather a lean micro framework which aims to complement ASP.NET Core where it comes short for functional developers. The fundamental idea is to build on top of the strong foundation of ASP.NET Core and re-use existing ASP.NET Core building blocks so F# developers can benefit from both worlds.

You can think of [Giraffe](https://www.nuget.org/packages/Giraffe) as the functional counter part of the ASP.NET Core MVC framework.

## Installation

### Using dotnet-new

The easiest way to get started with Giraffe is by installing the [`giraffe-template`](https://www.nuget.org/packages/giraffe-template) package, which adds a new template to your `dotnet new` command line tool:

```
dotnet new -i "giraffe-template::*"
```

Afterwards you can create a new Giraffe application by running `dotnet new giraffe`.

For more information about the Giraffe tempalte please visit the official [giraffe-template repository](https://github.com/giraffe-fsharp/giraffe-template).

### Doing it manually

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
    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =

        app.UseGiraffe webApp
```

## Default HttpHandlers

### mustAccept

`mustAccept` filters a request by the `Accept` HTTP header. You can use it to check if a client accepts a certain mime type before returning a response.

#### Example:

```fsharp
let app =
    mustAccept [ "text/plain"; "application/json" ] >=>
        choose [
            route "/foo" >=> text "Foo"
            route "/bar" >=> json "Bar"
        ]
```

### clearResponse

`clearResponse` tries to clear the current response. This can be useful inside an error handler to reset the response before writing an error message to the body of the HTTP response object.

#### Example:

```fsharp
let errorHandler (ex : Exception) (logger : ILogger) =
    clearResponse
    >=> ServerErrors.INTERNAL_ERROR ex.Message

let webApp =
    choose [
        route "/foo" >=> text "Foo"
        route "/bar" >=> text "Bar"
    ]

type Startup() =
    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =
        app.UseGiraffeErrorHandler errorHandler
        app.UseGiraffe webApp
```

## Additional HttpHandlers


### Giraffe.TokenRouter

The `Giraffe.TokenRouter` module adds alternative `HttpHandler` functions to route incoming HTTP requests through a basic [Radix Tree](https://en.wikipedia.org/wiki/Radix_tree). Several routing handlers (e.g.: `routef` and `subRoute`) have been overridden in such a way that path matching and value parsing are significantly faster than using the basic `choose` function.

This implementation assumes that additional memory and compilation time is not an issue. If speed and performance of parsing and path matching is required then the `Giraffe.TokenRouter` is the preferred option.

#### router

The base of all routing decisions is a `router` function instead of the default `choose` function when using the `Giraffe.TokenRouter` module.

The `router` HttpHandler takes two arguments, a `HttpHandler` to execute when no route can be matched (typical 404 Not Found handler) and secondly a list of all routing functions.

##### Example:

Defining a basic router and routes

```fsharp
let notFound = RequestErrors.NOT_FOUND "Page not found"
let app =
    router notFound [
        route "/"       (text "index")
        route "/about"  (text "about")
    ]
```

#### routing functions

When using the `Giraffe.TokenRouter` module the main routing functions have been slightly overridden to match the alternative (speed improved) implementation.

The `route` and `routef` handlers work the exact same way as before, except that the continuation handler needs to be enclosed in parentheses or captured by the `<|` or `=>` operators.

The http handlers `GET`, `POST`, `PUT` and `DELETE` are functions which take a list of nested http handler functions similar to before.

The `subRoute` handler has been altered in order to accept an additional parameter of child routing functions. All child routing functions will presume that the given sub path has been prepended.

### Example:

Defining a basic router and routes

```fsharp
let notFound = RequestErrors.NOT_FOUND "Page not found"
let app =
    router notFound [
        route "/"       (text "index")
        route "/about"  (text "about")
        routef "parsing/%s/%i" (fun (s,i) -> text (sprintf "Received %s & %i" s i))
        subRoute "/api" [
            GET [
                route "/"       (text "api index")
                route "/about"  (text "api about")
                subRoute "/v2" [
                    route "/"       (text "api v2 index")
                    route "/about"  (text "api v2 about")
                ]
            ]

        ]
    ]
```

### Additional NuGet packages

There are more `HttpHandler` functions available through additional NuGet packages:

- [Giraffe.Razor](https://github.com/giraffe-fsharp/Giraffe.Razor): Adds native Razor view functionality to Giraffe web applications.
- [Giraffe.DotLiquid](https://github.com/giraffe-fsharp/Giraffe.DotLiquid): Adds native DotLiquid template functionality to Giraffe web applications.

## Custom HttpHandlers

Defining a new `HttpHandler` is fairly easy. All you need to do is to create a new function which matches the signature of `HttpFunc -> HttpContext -> Task<HttpContext option>`. Through currying your custom `HttpHandler` can extend the original signature as long as the partial application of your function will still return a function of `HttpFunc -> HttpContext -> Task<HttpContext option>` (`HttpFunc -> HttpFunc`).

### Example:

Defining a custom HTTP handler to partially filter a route:

*(After creating this example I added the `routeStartsWith` HttpHandler to the list of default handlers as it turned out to be quite useful)*

```fsharp
let routeStartsWith (subPath : string) =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if ctx.Request.Path.ToString().StartsWith subPath
        then next ctx
        else Task.FromResult None
```

Defining another custom HTTP handler to validate a mandatory HTTP header:

```fsharp
let requiresToken (expectedToken : string) (handler : HttpHandler) =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let token    = ctx.Request.Headers.["X-Token"].ToString()
        let response =
            if token.Equals(expectedToken)
            then handler
            else setStatusCode 401 >=> text "Token wrong or missing"
        response next ctx
```

Composing a web application from smaller HTTP handlers:

```fsharp
let app =
    choose [
        route "/"       >=> htmlFile "index.html"
        route "/about"  >=> htmlFile "about.html"
        routeStartsWith "/api/v1/" >=>
            requiresToken "secretToken" (
                choose [
                    route "/api/v1/foo" >=> text "something"
                    route "/api/v1/bar" >=> text "bar"
                ]
            )
        RequestErrors.NOT_FOUND "Page not found"
    ] : HttpHandler
```

## Customizing Giraffe

Giraffe uses the [ASP.NET Core dependency injection](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection) framework to register and retrieve several services which can be used or overridden by applications.

Currently you can modify the following functionality in Giraffe through dependency injection*:

- [JSON serialization](#customize-json-serialization)
- [XML serialization](#customize-xml-serialization)
- [Content negotiation](#customize-content-negotiation)

*) Note that in functional programming there is no direct equivalent to dependency injection as known in OOP and Giraffe uses the [service locator pattern](https://msdn.microsoft.com/en-us/library/ff648968.aspx) to work with ASP.NET's DI framework.

### Customize JSON serialization

By default Giraffe uses the [Newtonsoft's JSON.NET](https://www.newtonsoft.com/json) serializer for (de-)serializing JSON content. An application can modify the serializer by registering a new instance of the `IJsonSerializer` interface during application startup.

#### Example: Customizing JsonSerializerSettings

You can change the default `JsonSerializerSettings` of the `NewtonsoftJsonSerializer` by registering a new instance of `IJsonSerializer` in your application startup module:

```fsharp
let configureServices (services : IServiceCollection) =
    // First register all default Giraffe dependencies
    services.AddGiraffe() |> ignore

    // Now customize only the IJsonSerializer by providing a custom
    // object of JsonSerializerSettings
    let customSettings = JsonSerializerSettings(
        Culture = CultureInfo("de-DE"))
    services.AddSingleton<IJsonSerializer>(
        NewtonsoftJsonSerializer(customSettings)) |> ignore

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

#### Example: Using a different JSON serializer

You can change the underlying JSON serializer to a complete different serializer alltogether by creating a new class which implements the `IJsonSerializer` interface:

```fsharp
type CustomJsonSerializer() =
    interface IJsonSerializer with
        member __.Serialize (o : obj) = // ...
        member __.Deserialize<'T> (json : string) = // ...
        member __.Deserialize<'T> (stream : Stream) = // ...
        member __.DeserializeAsync<'T> (stream : Stream) = // ...
```

Then register a new instance of the newly created type during application startup:

```fsharp
let configureServices (services : IServiceCollection) =
    // First register all default Giraffe dependencies
    services.AddGiraffe() |> ignore

    // Now register your custom IJsonSerializer
    services.AddSingleton<IJsonSerializer, CustomJsonSerializer>() |> ignore

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

#### Example: Retrieving the JSON serializer from a custom HttpHandler

If you need you retrieve the registered JSON serializer from a custom `HttpHandler` function then you can do this with the `GetJsonSerializer` extension method:

```fsharp
let customHandler (dataObj : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let serializer = ctx.GetJsonSerializer()
        // ... do more...
```

### Customize XML serialization

By default Giraffe uses the `System.Xml.Serialization.XmlSerializer` for (de-)serializing XML content. An application can modify the serializer by registering a new instance of the `IXmlSerializer` interface during application startup.

#### Example: Customizing XmlWriterSettings

You can change the default `XmlWriterSettings` of the `DefaultXmlSerializer` by registering a new instance of `IXmlSerializer` in your application startup module:

```fsharp
let configureServices (services : IServiceCollection) =
    // First register all default Giraffe dependencies
    services.AddGiraffe() |> ignore

    // Now customize the IXmlSerializer
    let customSettings =
        XmlWriterSettings(
                Encoding           = Encoding.UTF8,
                Indent             = false,
                OmitXmlDeclaration = true
            )

    services.AddSingleton<IXmlSerializer>(
        DefaultXmlSerializer(customSettings)) |> ignore

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

#### Example: Using a different XML serializer

You can change the underlying XML serializer to a complete different serializer alltogether by creating a new class which implements the `IXmlSerializer` interface:

```fsharp
type CustomXmlSerializer() =
    interface IXmlSerializer with
        member __.Serialize (o : obj) = // ...
        member __.Deserialize<'T> (xml : string) = // ...
```

Then register a new instance of the newly created type during application startup:

```fsharp
let configureServices (services : IServiceCollection) =
    // First register all default Giraffe dependencies
    services.AddGiraffe() |> ignore

    // Now register your custom IXmlSerializer
    services.AddSingleton<IXmlSerializer, CustomXmlSerializer>() |> ignore

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

#### Example: Retrieving the XML serializer from a custom HttpHandler

If you need you retrieve the registered XML serializer from a custom `HttpHandler` function then you can do this with the `GetXmlSerializer` extension method:

```fsharp
let customHandler (dataObj : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let serializer = ctx.GetXmlSerializer()
        // ... do more...
```

## Sample applications

### Demo apps

There are three basic sample applications in the [`/samples`](https://github.com/giraffe-fsharp/Giraffe/tree/develop/samples) folder. The [IdentityApp](https://github.com/giraffe-fsharp/Giraffe/tree/develop/samples/IdentityApp) demonstrates how ASP.NET Core Identity can be used with Giraffe, the [JwtApp](https://github.com/giraffe-fsharp/Giraffe/tree/develop/samples/JwtApp) shows how to configure JWT tokens in Giraffe and the [SampleApp](https://github.com/giraffe-fsharp/Giraffe/tree/develop/samples/SampleApp) is a generic sample application covering multiple features.

### Live apps

An example of a live website which uses Giraffe is [https://buildstats.info](https://buildstats.info). It uses the [GiraffeViewEngine](#renderhtml) to build dynamically rich SVG images and Docker to run the application in the Google Container Engine (see [GitHub repository](https://github.com/dustinmoris/CI-BuildStats)).

More sample applications will be added in the future.

## Benchmarks

Currently Giraffe has only been tested against a simple plain text route and measured the total amount of handled requests per second. The latest result yielded an average of 79093 req/s over a period of 10 seconds, which was only closely after plain Kestrel which was capable of handling 79399 req/s on average.

Please check out [Jimmy Byrd](https://github.com/TheAngryByrd)'s [dotnet-web-benchmarks](https://github.com/TheAngryByrd/dotnet-web-benchmarks) for more details.

## Building and developing

Giraffe is built with the latest [.NET Core SDK](https://www.microsoft.com/net/download/core).

You can either install [Visual Studio 2017](https://www.visualstudio.com/vs/) which comes with the latest SDK or manually download and install the [.NET SDK 2.0](https://www.microsoft.com/net/download/core).

After installation you should be able to run the `.\build.ps1` script to successfully build, test and package the library.

The build script supports the following flags:

- `-IncludeTests` will build and run the tests project as well
- `-IncludeSamples` will build and test the samples project as well
- `-All` will build and test all projects
- `-Release` will build Giraffe with the `Release` configuration
- `-Pack` will create a NuGet package for Giraffe and giraffe-template.
- `-OnlyNetStandard` will build Giraffe only targeting the NETStandard1.6 framework

Examples:

Only build the Giraffe project in `Debug` mode:
```
PS > .\build.ps1
```

Build the Giraffe project in `Release` mode:
```
PS > .\build.ps1 -Release
```

Build the Giraffe project in `Debug` mode and also build and run the tests project:
```
PS > .\build.ps1 -IncludeTests
```

Same as before, but also build and test the samples project:
```
PS > .\build.ps1 -IncludeTests -IncludeSamples
```

One switch to build and test all projects:
```
PS > .\build.ps1 -All
```

Build and test all projects, use the `Release` build configuration and create all NuGet packages:
```
PS > .\build.ps1 -Release -All -Pack
```

### Building on Linux or macOS

In order to successfully run the build script on Linux or macOS you will have to [install PowerShell for Linux or Mac](https://github.com/PowerShell/PowerShell#get-powershell).

Additionally you will have to [install the latest version of Mono](http://www.mono-project.com/download/) and execute the `./build.sh` script which will set the correct `FrameworkPathOverride` before subsequently executing the `./build.ps1` PowerShell script.

### Development environment

Currently the best way to work with F# on .NET Core is to use [Visual Studio Code](https://code.visualstudio.com/) with the [Ionide](http://ionide.io/) extension. Intellisense and debugging is supported with the latest versions of both.

## Contributing

Help and feedback is always welcome and pull requests get accepted.

### TL;DR

- First open an issue to discuss your changes
- After your change has been formally approved please submit your PR **against the develop branch**
- Please follow the code convention by examining existing code
- Add/modify the `README.md` as required
- Add/modify unit tests as required
- Please document your changes in the upcoming release notes in `RELEASE_NOTES.md`
- PRs can only be approved and merged when all checks succeed (builds on Windows and Linux)

### Discuss your change first

When contributing to this repository, please first discuss the change you wish to make via an [open issue](https://github.com/giraffe-fsharp/Giraffe/issues/new) before submitting a pull request. For new feature requests please describe your idea in more detail and how it could benefit other users as well.

Please be aware that Giraffe strictly aims to remain as light as possible while providing generic functionality for building functional web applications. New feature work must be applicable to a broader user base and if this requirement cannot be sufficiently met then a pull request might get rejected. In the case of doubt the maintainer might rather reject a potentially useful feature than adding one too many. This measure is to protect the repository from feature bloat and shall not be taken personally.

### Code conventions

When making changes please use existing code as a guideline for coding style and documentation. For example add spaces when creating tuples (`(a,b)` --> `(a, b)`), annotating variable types (`str:string` --> `str : string`) or other language constructs.

Examples:

```fsharp
let someHttpHandler:HttpHandler =
    fun (ctx:HttpContext) next -> task {
        // Some work
    }
```

should be:

```fsharp
let someHttpHandler : HttpHandler =
    fun (ctx : HttpContext) (next : HttpFunc) ->
        task {
            // Some work
        }
```

### Keep documentation and unit tests up to date

If you intend to add or change an existing `HttpHandler` then please update the `README.md` file to reflect these changes there as well. If applicable unit tests must be added or updated and the project must successfully build before a pull request can be accepted.

### Submit a pull request against develop

The `develop` branch is the main and only branch which should be used for all pull requests. A merge into `develop` means that your changes are scheduled to go live with the very next release, which could happen any time from the same day up to a couple weeks (depending on priorities and urgency).

Only pull requests which pass all build checks and comply with the general coding guidelines can be approved.

If you have any further questions please let me know.

You can file an [issue on GitHub](https://github.com/giraffe-fsharp/Giraffe/issues/new) or contact me via [https://dusted.codes/about](https://dusted.codes/about).

## Nightly builds and NuGet feed

All official Giraffe packages are published to the official and public NuGet feed.

Unofficial builds (such as pre-release builds from the `develop` branch and pull requests) produce unofficial pre-release NuGet packages which can be pulled from the project's public NuGet feed on AppVeyor:

```
https://ci.appveyor.com/nuget/giraffe
```

If you add this source to your NuGet CLI or project settings then you can pull unofficial NuGet packages for quick feature testing or urgent hot fixes.

**Please be aware that unofficial builds have not gone through the scrunity of offical releases and their usage is on your own risk.**

## Blog posts

- [Functional ASP.NET Core](https://dusted.codes/functional-aspnet-core) (by Dustin M. Gorski)
- [Functional ASP.NET Core part 2 - Hello world from Giraffe](https://dusted.codes/functional-aspnet-core-part-2-hello-world-from-giraffe) (by Dustin M. Gorski)
- [Carry On! … Continuation over binding pipelines for functional web](https://medium.com/@gerardtoconnor/carry-on-continuation-over-binding-pipelines-for-functional-web-58bd7e6ea009) (by Gerard)
- [A Functional Web with ASP.NET Core and F#'s Giraffe](https://www.hanselman.com/blog/AFunctionalWebWithASPNETCoreAndFsGiraffe.aspx) (by Scott Hanselman)
- [Build a web service with F# and .NET Core 2.0](https://blogs.msdn.microsoft.com/dotnet/2017/09/26/build-a-web-service-with-f-and-net-core-2-0/) (by Phillip Carter)
- [Giraffe brings F# functional programming to ASP.Net Core](https://www.infoworld.com/article/3229005/web-development/f-and-functional-programming-come-to-asp-net-core.html) (by Paul Krill from InfoWorld)

If you have blogged about Giraffe, demonstrating a useful topic or some other tips or tricks then please feel free to submit a pull request and add your article to this list as a reference for other Giraffe users. Thank you!

## Videos

- [Getting Started with ASP.NET Core Giraffe](https://www.youtube.com/watch?v=HyRzsPZ0f0k&t=461s) (by Ody Mbegbu)
- [Nikeza - Building the Backend with F#](https://www.youtube.com/watch?v=lANg1kn835s) (by Let's Code .NET)

## License

[Apache 2.0](https://raw.githubusercontent.com/giraffe-fsharp/Giraffe/master/LICENSE)

## Contact and Slack Channel

If you have any further questions feel free to reach out to me via any of the mentioned social media on [https://dusted.codes/about](https://dusted.codes/about) or join the `#giraffe` Slack channel in the [Functional Programming Slack Team](https://functionalprogramming.slack.com/). Please use [this link](https://fpchat-invite.herokuapp.com/) to request an invitation to the Functional Programming Slack Team.
