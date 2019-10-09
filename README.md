# Giraffe

![Giraffe](https://raw.githubusercontent.com/giraffe-fsharp/Giraffe/develop/giraffe.png)

A functional ASP.NET Core micro web framework for building rich web applications.

Read [this blog post on functional ASP.NET Core](https://dusted.codes/functional-aspnet-core) for more information.

[![NuGet Info](https://buildstats.info/nuget/Giraffe?includePreReleases=true)](https://www.nuget.org/packages/Giraffe/)

### Windows and Linux Builds

[![Build status](https://ci.appveyor.com/api/projects/status/bcst827ht2tcuhwo/branch/develop?svg=true)](https://ci.appveyor.com/project/dustinmoris/giraffe/branch/develop)

[![Windows Build history](https://buildstats.info/appveyor/chart/dustinmoris/giraffe?branch=develop&includeBuildsFromPullRequest=false)](https://ci.appveyor.com/project/dustinmoris/giraffe/history?branch=develop)


## Table of contents

- [About](#about)
- [Getting Started](#getting-started)
- [Documentation](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md)
- [Sample applications](#sample-applications)
- [Benchmarks](#benchmarks)
- [Building and developing](#building-and-developing)
- [Contributing](#contributing)
- [Nightly builds and NuGet feed](#nightly-builds-and-nuget-feed)
- [Blog posts](#blog-posts)
- [Videos](#videos)
- [License](#license)
- [Contact and Slack Channel](#contact-and-slack-channel)
- [Support](#support)

## About

[Giraffe](https://www.nuget.org/packages/Giraffe) is an F# micro web framework for building rich web applications. It has been heavily inspired and is similar to [Suave](https://suave.io/), but has been specifically designed with [ASP.NET Core](https://www.asp.net/core) in mind and can be plugged into the ASP.NET Core pipeline via [middleware](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware). Giraffe applications are composed of so called `HttpHandler` functions which can be thought of a mixture of Suave's WebParts and ASP.NET Core's middleware.

If you'd like to learn more about the motivation of this project please read my [blog post on functional ASP.NET Core](https://dusted.codes/functional-aspnet-core) (some code samples in this blog post might be outdated today).

### Who is it for?

[Giraffe](https://www.nuget.org/packages/Giraffe) is intended for developers who want to build rich web applications on top of ASP.NET Core in a functional first approach. ASP.NET Core is a powerful web platform which has support by Microsoft and a huge developer community behind it and Giraffe is aimed at F# developers who want to benefit from that eco system.

It is not designed to be a competing web product which can be run standalone like NancyFx or Suave, but rather a lean micro framework which aims to complement ASP.NET Core where it comes short for functional developers. The fundamental idea is to build on top of the strong foundation of ASP.NET Core and re-use existing ASP.NET Core building blocks so F# developers can benefit from both worlds.

You can think of [Giraffe](https://www.nuget.org/packages/Giraffe) as the functional counter part of the ASP.NET Core MVC framework.

## Getting Started

### Using dotnet-new

The easiest way to get started with Giraffe is by installing the [`giraffe-template`](https://www.nuget.org/packages/giraffe-template) package, which adds a new template to your `dotnet new` command line tool:

```
dotnet new -i "giraffe-template::*"
```

Afterwards you can create a new Giraffe application by running `dotnet new giraffe`.

If you are using dotnet core 2.1.4, you will need to specify the language: `dotnet new giraffe -lang F#`

For more information about the Giraffe template please visit the official [giraffe-template repository](https://github.com/giraffe-fsharp/giraffe-template).

### Doing it manually

Install the [Giraffe](https://www.nuget.org/packages/Giraffe) NuGet package*:

```
PM> Install-Package Giraffe
```

*) If you haven't installed the ASP.NET Core NuGet package yet then you'll also need to add a package reference to `Microsoft.AspNetCore.App`:

```
PM> Install-Package Microsoft.AspNetCore.App
```

Alternatively you can also use the .NET CLI to add the packages:

```
dotnet add package Microsoft.AspNetCore.App
dotnet add package Giraffe
```

Next create a web application and plug it into the ASP.NET Core middleware:

```fsharp
open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
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
    WebHostBuilder()
        .UseKestrel()
        .UseStartup<Startup>()
        .Build()
        .Run()
    0
```

Instead of creating a `Startup` class you can also add Giraffe in a more functional way:

```fsharp
open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
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
    WebHostBuilder()
        .UseKestrel()
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .Build()
        .Run()
    0
```

For more information please check the official [Giraffe documentation](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md).

## Sample applications

### Demo apps

There is a few sample applications which can be found in the [`/samples`](https://github.com/giraffe-fsharp/Giraffe/tree/master/samples) folder:

| Sample | Description |
| ------ | ----------- |
| [GoogleAuthApp](https://github.com/giraffe-fsharp/Giraffe/tree/master/samples/GoogleAuthApp) | Demonstrates how Google Auth can be used with Giraffe. |
| [IdentityApp](https://github.com/giraffe-fsharp/Giraffe/tree/master/samples/IdentityApp) | Demonstrates how ASP.NET Core Identity can be used with Giraffe. |
| [JwtApp](https://github.com/giraffe-fsharp/Giraffe/tree/master/samples/JwtApp) | Demonstrates how JWT tokens can be used with Giraffe. |
| [SampleApp](https://github.com/giraffe-fsharp/Giraffe/tree/master/samples/SampleApp) | Generic sample application showcasing multiple features such as file uploads, cookie auth, model binding and validation, etc. |

### Live apps

#### Buildstats.info

The web service [https://buildstats.info](https://buildstats.info) uses Giraffe to build rich SVG widgets for Git repositories. The application runs as a Docker container in the Google Container Engine (see [CI-BuiltStats on GitHub](https://github.com/dustinmoris/CI-BuildStats) for more information).

More sample applications will be added in the future.

## Benchmarks

Giraffe is part of the [TechEmpower Web Framework Benchmarks](https://www.techempower.com/benchmarks/#section=test&runid=a1843d12-6091-4780-92a6-a747fab77cb1&hw=ph&test=plaintext&l=hra0hp-1&p=zik0zj-zik0zj-zijocf-5m9r) and will be listed in the official results page in the upcoming Round 17 for the first time.

Unofficial test results are currently available on the [TFB Status page](https://tfb-status.techempower.com/).

As of today Giraffe competes in the Plaintext, JSON and Fortunes categories and has been doing pretty well so far, even outperforming ASP.NET Core MVC in Plaintext and JSON at the time of writing.

The latest implementation which is being used for the benchmark tests can be seen inside the [TechEmpower repository](https://github.com/TechEmpower/FrameworkBenchmarks/tree/master/frameworks/FSharp/giraffe).

Giraffe is also featured in [Jimmy Byrd](https://github.com/TheAngryByrd)'s [dotnet-web-benchmarks](https://github.com/TheAngryByrd/dotnet-web-benchmarks) where we've run earlier performance tests.

## Building and developing

Giraffe is built with the latest [.NET Core SDK](https://www.microsoft.com/net/download/core).

You can either install [Visual Studio 2017](https://www.visualstudio.com/vs/) which comes with the latest SDK or manually download and install the [.NET SDK 2.1](https://www.microsoft.com/net/download/core).

After installation you should be able to run the `.\build.ps1` script to successfully build, test and package the library.

The build script supports the following flags:

- `-IncludeTests` will build and run the tests project as well
- `-IncludeSamples` will build and test the samples project as well
- `-All` will build and test all projects
- `-Release` will build Giraffe with the `Release` configuration
- `-Pack` will create a NuGet package for Giraffe and giraffe-template.

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

**Please be aware that unofficial builds have not gone through the scrutiny of official releases and their usage is on your own risk.**

## Blog posts

### Blog posts by author

- [Functional ASP.NET Core](https://dusted.codes/functional-aspnet-core)
- [Functional ASP.NET Core part 2 - Hello world from Giraffe](https://dusted.codes/functional-aspnet-core-part-2-hello-world-from-giraffe)
- [Evolving my open source project from a one man repository to an OSS organisation](https://dusted.codes/evolving-my-open-source-project-from-a-one-man-repository-to-a-proper-organisation)
- [Extending the Giraffe template with different view engine options](https://dusted.codes/extending-the-giraffe-template-with-different-view-engine-options)
- [Announcing Giraffe 1.0.0](https://dusted.codes/announcing-giraffe-100)
- [Giraffe 1.1.0 - More routing handlers, better model binding and brand new model validation API](https://dusted.codes/giraffe-110-more-routing-handlers-better-model-binding-and-brand-new-model-validation-api)

### Community blog posts

- [Carry On! … Continuation over binding pipelines for functional web](https://medium.com/@gerardtoconnor/carry-on-continuation-over-binding-pipelines-for-functional-web-58bd7e6ea009) (by Gerard)
- [A Functional Web with ASP.NET Core and F#'s Giraffe](https://www.hanselman.com/blog/AFunctionalWebWithASPNETCoreAndFsGiraffe.aspx) (by Scott Hanselman)
- [Build a web service with F# and .NET Core 2.0](https://blogs.msdn.microsoft.com/dotnet/2017/09/26/build-a-web-service-with-f-and-net-core-2-0/) (by Phillip Carter)
- [Giraffe brings F# functional programming to ASP.Net Core](https://www.infoworld.com/article/3229005/web-development/f-and-functional-programming-come-to-asp-net-core.html) (by Paul Krill from InfoWorld)
- [JSON Web Token with Giraffe and F#](https://medium.com/@dsincl12/json-web-token-with-giraffe-and-f-4cebe1c3ef3b) (by David Sinclair)
- [WebSockets with Giraffe and F#](https://medium.com/@dsincl12/websockets-with-f-and-giraffe-772be829e121) (by David Sinclair)
- [Use appsettings in a Giraffe web app](https://www.devprotocol.com/use-appsettings-in-a-giraffe-web-app/) (by Jan Tourlamain)
- [Integrate Azure AD in your Giraffe web app](https://www.devprotocol.com/integrate-azure-ad-in-your-giraffe-web-app/) (by Jan Tourlamain)
- [Web development in F#: Getting started](https://samueleresca.net/2018/04/web-development-in-f-getting-started/) (by Samuele Resca)
- [Build web service using F# and ASP.NET Core](https://samueleresca.net/2018/04/build-web-service-using-f-and-asp-net-core/) (by Samuele Resca)

If you have blogged about Giraffe, demonstrating a useful topic or some other tips or tricks then please feel free to submit a pull request and add your article to this list as a reference for other Giraffe users. Thank you!

## Videos

- [Getting Started with ASP.NET Core Giraffe](https://www.youtube.com/watch?v=HyRzsPZ0f0k&t=461s) (by Ody Mbegbu)
- [Nikeza - Building the Backend with F#](https://www.youtube.com/watch?v=lANg1kn835s) (by Let's Code .NET)

## License

[Apache 2.0](https://raw.githubusercontent.com/giraffe-fsharp/Giraffe/master/LICENSE)

## Contact and Slack Channel

If you have any further questions feel free to reach out to me via any of the mentioned social media on [https://dusted.codes/about](https://dusted.codes/about) or join the `#giraffe` Slack channel in the [Functional Programming Slack Team](https://functionalprogramming.slack.com/). Please use [this link](https://fpchat-invite.herokuapp.com/) to request an invitation to the Functional Programming Slack Team if you don't have an account registered yet.

## Support

If you've got value from any of the content which I have created, but pull requests are not your thing, then I would also very much appreciate your support by buying me a coffee.

<a href="https://www.buymeacoffee.com/dustinmoris" target="_blank"><img src="https://www.buymeacoffee.com/assets/img/custom_images/yellow_img.png" alt="Buy Me A Coffee" style="height: auto !important;width: auto !important;" ></a>
