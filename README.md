# ASP.NET Core Lambda

A functional ASP.NET Core micro framework for building rich web applications.

Read [this blog post on functional ASP.NET Core](https://dusted.codes/functional-aspnet-core) for more information.

[![Build status](https://ci.appveyor.com/api/projects/status/0ft2427dflip7wti/branch/master?svg=true)](https://ci.appveyor.com/project/dustinmoris/aspnetcore-lambda/branch/master)
[![NuGet Info](https://buildstats.info/nuget/AspNetCore.Lambda?includePreReleases=true)](https://www.nuget.org/packages/AspNetCore.Lambda/)

[![Build history](https://buildstats.info/appveyor/chart/dustinmoris/aspnetcore-lambda?branch=master&includeBuildsFromPullRequest=false)](https://ci.appveyor.com/project/dustinmoris/aspnetcore-lambda/history)

**ATTENTION: THIS PROJECT IS STILL IN ALPHA STAGE**

## Table of contents

- [About](#about)
- [Basics](#basics)
    - [HttpHandler](#httphandler)
    - [Combinators](#combinators)
        - [bind (>>=)](#bind-)
        - [compose (>=>)](#compose-)
        - [choose](#choose)
- [Default HttpHandlers](#default-httphandlers)
    - [choose](#choose)
    - [GET, POST, PUT, PATCH, DELETE](#get-post-put-patch-delete)
    - [mustAccept](#mustaccept)
    - [challenge](#challenge)
    - [signOff](#signoff)
    - [requiresAuthentication](#requiresauthentication)
    - [requiresRole](#requiresrole)
    - [requiresRoleOf](#requiresroleof)
    - [clearResponse](#clearResponse)
    - [route](#route)
    - [routef](#routef)
    - [routeCi](#routeci)
    - [routeCif](#routecif)
    - [routeStartsWith](#routestartswith)
    - [routeStartsWithCi](#routestartswithci)
    - [subRoute](#subroute)
    - [subRouteCi](#subrouteci)
    - [setStatusCode](#setstatuscode)
    - [setHttpHeader](#sethttpheader)
    - [setBody](#setbody)
    - [setBodyAsString](#setbodyasstring)
    - [text](#text)
    - [json](#json)
    - [xml](#xml)
    - [dotLiquid](#dotliquid)
    - [htmlTemplate](#htmltemplate)
    - [htmlFile](#htmlfile)
- [Custom HttpHandlers](#custom-httphandlers)
- [Installation](#installation)
- [Sample applications](#sample-applications)
- [Building and developing](#building-and-developing)
- [Contributing](#contributing)
- [License](#license)

## About

[ASP.NET Core Lambda](https://www.nuget.org/packages/AspNetCore.Lambda) is an F# web framework similar to Suave, but has been designed with [ASP.NET Core](https://www.asp.net/core) in mind and can be plugged into the ASP.NET Core pipeline via [middleware](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware). [ASP.NET Core Lambda](https://www.nuget.org/packages/AspNetCore.Lambda) has been heavily inspired by [Suave](https://suave.io/) and its concept of web parts and the ability to compose many smaller web parts into a larger web application.

If you'd like to learn more about the motivation of this project please read this [blog post on functional ASP.NET Core](https://dusted.codes/functional-aspnet-core).

### Who is it for?

[ASP.NET Core Lambda](https://www.nuget.org/packages/AspNetCore.Lambda) is intended for developers who want to build rich web applications on top of ASP.NET Core in a functional first approach. ASP.NET Core is a powerful web platform which has support by Microsoft and a huge developer community behind it and ASP.NET Core Lambda is aimed at F# developers who want to benefit from that eco system.

It is not designed to be a competing web product which can be run standalone like NancyFx or Suave, but rather a lean micro framework which aims to complement ASP.NET Core where it comes short for functional developers at the moment. The fundamental idea is to build on top of the strong foundation of ASP.NET Core and re-use existing ASP.NET Core building blocks so F# developers can benefit from both worlds.

You can think of [ASP.NET Core Lambda](https://www.nuget.org/packages/AspNetCore.Lambda) as the functional counter part of the ASP.NET Core MVC framework.

## Basics

### HttpHandler

The only building block in ASP.NET Core Lambda is a so called `HttpHandler`:

```fsharp
type HttpHandlerContext =
    {
        HttpContext : HttpContext
        Services    : IServiceProvider
    }

type HttpHandlerResult = Async<HttpHandlerContext option>

type HttpHandler = HttpHandlerContext -> HttpHandlerResult
```

A `HttpHandler` is a simple function which takes in a `HttpHandlerContext` and returns an object of the same type (wrapped in an option and async workflow) when finished.

Inside that function it can process an incoming `HttpRequest` and make changes to the `HttpResponse` of the given `HttpContext`. By receiving and returning a `HttpContext` there's nothing which cannot be done from inside a `HttpHandler`.

A `HttpHandler` can decide to not further process an incoming request and return `None` instead. In this case another `HttpHandler` might continue processing the request or the middleware will simply defer to the next `RequestDelegate` in the ASP.NET Core pipeline.

### Combinators

#### bind (>>=)

The core combinator is the `bind` function which you might be familiar with:

```fsharp
let bind (handler : HttpHandler) =
    fun (result : HttpHandlerResult) ->
        async {
            let! ctx = result
            match ctx with
            | None   -> return None
            | Some c ->
                match c.HttpContext.Response.HasStarted with
                | true  -> return  Some c
                | false -> return! handler c
        }

let (>>=) = bind
```

The `bind` function takes in a `HttpHandler` function and a `HttpHandlerResult`. It first evaluates the `HttpHandlerResult` and checks its return value. If there was `Some HttpHandlerContext` then it will pass it on to the `HttpHandler` function otherwise it will return `None`. If the response object inside the `HttpHandlerContext` has already been written, then it will skip the `HttpHandler` function as well and return the current `HttpHandlerContext` as the final result.

#### compose (>=>)

The `compose` combinator combines two `HttpHandler` functions into one:

```fsharp
let compose (handler : HttpHandler) (handler2 : HttpHandler) =
    fun (ctx : HttpHandlerContext) ->
        handler ctx |> bind handler2
```

It is probably the more useful combinator as it allows composing many smaller `HttpHandler` functions into a bigger web application.

If you would like to learn more about the difference between `>>=` and `>=>` then please check out [Scott Wlaschin's blog post on Railway oriented programming](http://fsharpforfunandprofit.com/posts/recipe-part2/).

#### choose

The `choose` combinator function iterates through a list of `HttpHandler` functions and invokes each individual handler until the first `HttpHandler` returns a result.

#### Example:

```fsharp
let app = 
    choose [
        route "/foo" >=> text "Foo"
        route "/bar" >=> text "Bar"
    ]
```

## Default HttpHandlers

### GET, POST, PUT, PATCH, DELETE

`GET`, `POST`, `PUT`, `PATCH`, `DELETE` filters a request by the specified HTTP verb.

#### Example:

```fsharp
let app = 
    choose [
        GET  >=> route "/foo" >=> text "GET Foo"
        POST >=> route "/foo" >=> text "POST Foo"
        route "/bar" >=> text "Always Bar"
    ]
```

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

### challenge

`challenge` challenges an authentication with a specified authentication scheme (`authScheme`).

#### Example:

```fsharp
let mustBeLoggedIn =
    requiresAuthentication (challenge "Cookie")

let app = 
    choose [
        route "/ping" >=> text "pong"
        route "/admin" >=> mustBeLoggedIn >=> text "You're an admin"
    ]
```

### signOff

`signOff` signs off the currently logged in user.

#### Example:

```fsharp
let app = 
    choose [
        route "/ping" >=> text "pong"
        route "/logout" >=> signOff "Cookie" >=> text "You have successfully logged out."
    ]
```

### requiresAuthentication

`requiresAuthentication` validates if a user is authenticated/logged in. If the user is not authenticated then the handler will execute the `authFailedHandler` function.

#### Example:

```fsharp
let mustBeLoggedIn =
    requiresAuthentication (challenge "Cookie")

let app = 
    choose [
        route "/ping" >=> text "pong"
        route "/user" >=> mustBeLoggedIn >=> text "You're a logged in user."
    ]
```

### requiresRole

`requiresRole` validates if an authenticated user is in a specified role. If the user fails to be in the required role then the handler will execute the `authFailedHandler` function.

#### Example:

```fsharp
let accessDenied = setStatusCode 401 >=> text "Access Denied"

let mustBeAdmin = 
    requiresAuthentication accessDenied 
    >=> requiresRole "Admin" accessDenied

let app = 
    choose [
        route "/ping" >=> text "pong"
        route "/admin" >=> mustBeAdmin >=> text "You're an admin."
    ]
```

### requiresRoleOf

`requiresRoleOf` validates if an authenticated user is in one of the supplied roles. If the user fails to be in one of the required roles then the handler will execute the `authFailedHandler` function.

#### Example:

```fsharp
let accessDenied = setStatusCode 401 >=> text "Access Denied"

let mustBeSomeAdmin = 
    requiresAuthentication accessDenied 
    >=> requiresRoleOf [ "Admin"; "SuperAdmin"; "RootAdmin" ] accessDenied

let app = 
    choose [
        route "/ping" >=> text "pong"
        route "/admin" >=> mustBeSomeAdmin >=> text "You're an admin."
    ]
```

### clearResponse

`clearResponse` tries to clear the current response. This can be useful inside an error handler to reset the response before writing an error message to the body of the HTTP response object.

#### Example:

```fsharp
let errorHandler (ex : Exception) (ctx : HttpHandlerContext) =
    ctx |> (clearResponse >=> setStatusCode 500 >=> text ex.Message)

let app = 
    choose [
        route "/foo" >=> text "Foo"
        route "/bar" >=> text "Bar"
    ]

type Startup() =
    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =
        app.UseLambdaErrorHandler(errorHandler)
        app.UseLambda(webApp)
```

### route

`route` compares a given path with the actual request path and short circuits if it doesn't match.

#### Example:

```fsharp
let app = 
    choose [
        route "/"    >=> text "Index path"
        route "/foo" >=> text "Foo"
        route "/bar" >=> text "Bar"
    ]
```

### routef

`routef` matches a given format string with the actual request path. On success it will resolve the arguments from the format string and invoke the given `HttpHandler` with them.

The following format placeholders are currently supported:

- `%b` for bool
- `%c` for char
- `%s` for string
- `%i` for int32
- `%d` for int64 (this is custom to ASP.NET Core Lambda)
- `%f` for float/double

#### Example:

```fsharp
let app = 
    choose [
        route  "/foo" >=> text "Foo"
        routef "/bar/%s/%i" (fun (name, age) ->
            // name is of type string
            // age is of type int
            text (sprintf "Name: %s, Age: %i" name age))
    ]
```

### routeCi

`routeCi` is the case insensitive version of `route`.

#### Example:

```fsharp
// "/FoO", "/fOO", "/bAr", etc. will match as well

let app = 
    choose [
        routeCi "/"    >=> text "Index path"
        routeCi "/foo" >=> text "Foo"
        routeCi "/bar" >=> text "Bar"
    ]
```

### routeCif

`routeCif` is the case insensitive version of `routef`.

#### Example:

```fsharp
let app = 
    choose [
        route  "/foo" >=> text "Foo"
        routeCif "/bar/%s/%i" (fun (name, age) ->
            text (sprintf "Name: %s, Age: %i" name age))
    ]
```

### routeStartsWith

`routeStartsWith` checks if the current request path starts with the given string. This can be useful when combining with other http handlers, e.g. to validate a subset of routes for authentication.

#### Example:

```fsharp
let app = 
    routeStartsWith "/api/" >=>
        requiresAuthentication (challenge "Cookie") >=>
            choose [
                route "/api/v1/foo" >=> text "Foo"
                route "/api/v1/bar" >=> text "Bar"
            ]
```

### routeStartsWithCi

`routeStartsWithCi` is the case insensitive version of `routeStartsWith`.

#### Example:

```fsharp
let app = 
    routeStartsWithCi "/api/v1/" >=>
        choose [
            route "/api/v1/foo" >=> text "Foo"
            route "/api/v1/bar" >=> text "Bar"
        ]
```

### subRoute

`subRoute` checks if the current path begins with the given `path` and will invoke the passed in `handler` if it was a match. The given `handler` (and any nested handlers within it) should omit the already applied `path` for subsequent route evaluations.

#### Example:

```fsharp
let app = 
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

### subRouteCi

`subRouteCi` is the case insensitive version of `subRoute`.

#### Example:

```fsharp
let app = 
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
```

### setStatusCode

`setStatusCode` changes the status code of the `HttpResponse`.

#### Example:

```fsharp
let app = 
    choose [
        route  "/foo" >=> text "Foo"
        setStatusCode 404 >=> text "Not found"
    ]
```

### setHttpHeader

`setHttpHeader` sets or modifies a HTTP header of the `HttpResponse`.

#### Example:

```fsharp
let app = 
    choose [
        route  "/foo" >=> text "Foo"
        setStatusCode 404 >=> setHttpHeader "X-CustomHeader" "something" >=> text "Not found"
    ]
```

### setBody

`setBody` sets or modifies the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers afterwards will not be able to modify the HTTP headers anymore.

#### Example:

```fsharp
let app = 
    choose [
        route  "/foo" >=> setBody (Encoding.UTF8.GetBytes "Some string")
    ]
```

### setBodyAsString

`setBodyAsString` sets or modifies the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers afterwards will not be able to modify the HTTP headers anymore.

#### Example:

```fsharp
let app = 
    choose [
        route  "/foo" >=> setBodyAsString "Some string"
    ]
```

### text

`text` sets or modifies the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers afterwards will not be able to modify the HTTP headers anymore.

The different between `text` and `setBodyAsString` is that this http handler also sets the `Content-Type` HTTP header to `text/plain`.

#### Example:

```fsharp
let app = 
    choose [
        route  "/foo" >=> text "Some string"
    ]
```

### json

`json` sets or modifies the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers afterwards will not be able to modify the HTTP headers anymore. It also sets the `Content-Type` HTTP header to `application/json`.

#### Example:

```fsharp
type Person =
    {
        FirstName : string
        LastName  : string
    }

let app = 
    choose [
        route  "/foo" >=> json { FirstName = "Foo"; LastName = "Bar" }
    ]
```

### xml

`xml` sets or modifies the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers afterwards will not be able to modify the HTTP headers anymore. It also sets the `Content-Type` HTTP header to `application/xml`.

#### Example:

```fsharp
[<CLIMutable>]
type Person =
    {
        FirstName : string
        LastName  : string
    }

let app = 
    choose [
        route  "/foo" >=> xml { FirstName = "Foo"; LastName = "Bar" }
    ]
```

### dotLiquid

`dotLiquid` uses the [DotLiquid](http://dotliquidmarkup.org/) template engine to set or modify the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers afterwards will not be able to modify the HTTP headers anymore.

The `dotLiquid` handler requires the content type and the actual template to be passed in as two string values together with an object model. This handler is supposed to be used as the base handler for other http handlers which want to utilize the DotLiquid template engine (e.g. you could create an SVG handler on top of it).

#### Example:

```fsharp
type Person =
    {
        FirstName : string
        LastName  : string
    }

let template = "<html><head><title>DotLiquid</title></head><body><p>First name: {{ firstName }}<br />Last name: {{ lastName }}</p></body></html>

let app = 
    choose [
        route  "/foo" >=> dotLiquid "text/html" template { FirstName = "Foo"; LastName = "Bar" }
    ]
```

### htmlTemplate

`htmlTemplate` uses the [DotLiquid](http://dotliquidmarkup.org/) template engine to set or modify the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers afterwards will not be able to modify the HTTP headers anymore.

This http handler takes a relative path of a template file and the associated model as parameters. It also sets the HTTP header `Content-Type` to `text/html`.

#### Example:

```fsharp
type Person =
    {
        FirstName : string
        LastName  : string
    }

let app = 
    choose [
        route  "/foo" >=> htmlTemplate "templates/person.html" { FirstName = "Foo"; LastName = "Bar" }
    ]
```

### htmlFile

`htmlFile` sets or modifies the body of the `HttpResponse` with the contents of a physical html file. This http handler triggers the response being sent to the client and other http handlers afterwards will not be able to modify the HTTP headers anymore.

This http handler takes a relative path of a html file as input parameter and sets the HTTP header `Content-Type` to `text/html`.

#### Example:

```fsharp
let app = 
    choose [
        route  "/" >=> htmlFile "index.html"
    ]
```

## Custom HttpHandlers

Defining a new `HttpHandler` is fairly easy. All you need to do is to create a new function which matches the signature of `HttpHandlerContext -> Async<HttpHandlerContext option>`. Through currying your custom `HttpHandler` can extend the original signature as long as the partial application of your function will still return a function of `HttpHandlerContext -> Async<HttpHandlerContext option>`.

### Example:

Defining a custom HTTP handler to partially filter a route:

*(After creating this example I added the `routeStartsWith` HttpHandler to the list of default handlers as it turned out to be quite useful)*

```fsharp
let routeStartsWith (subPath : string) =
    fun ctx ->
        if ctx.HttpContext.Request.Path.ToString().StartsWith subPath 
        then Some ctx
        else None
        |> async.Return
```

Defining another custom HTTP handler to validate a mandatory HTTP header:

```fsharp
let requiresToken (expectedToken : string) (handler : HttpHandler) =
    fun ctx ->
        let token    = ctx.HttpContext.Request.Headers.["X-Token"].ToString()
        let response =
            if token.Equals(expectedToken)
            then handler
            else setStatusCode 401 >=> text "Token wrong or missing"
        response ctx
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
        setStatusCode 404 >=> text "Not found"
    ] : HttpHandler
```

## Installation

Install the [AspNetCore.Lambda](https://www.nuget.org/packages/AspNetCore.Lambda) NuGet package:

```
PM> Install-Package AspNetCore.Lambda
```

Create a web application and plug it into the ASP.NET Core middleware:

```fsharp
open AspNetCore.Lambda.HttpHandlers
open AspNetCore.Lambda.Middleware

let webApp = 
    choose [
        route "/ping"   >=> text "pong"
        route "/"       >=> htmlFile "/pages/index.html" ]

type Startup() =
    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =
        
        app.UseLambda(webApp)
```

## Sample applications

There is a basic sample application in the `/samples/SampleApp` folder.

More sample applications will be added in the future.

## Building and developing

ASP.NET Core Lambda is using the new MSBuild driven `.fsproj` project system that comes with [.NET Core SDK RC4](https://github.com/dotnet/netcorecli-fsc/wiki/.NET-Core-SDK-rc4).

You can either install [Visual Studio 2017 RC](https://www.visualstudio.com/vs/visual-studio-2017-rc/) which comes with the latest SDK or manually download and install the [.NET SDK RC4](https://github.com/dotnet/core/blob/master/release-notes/rc4-download.md).

After installation you should be able to run `build.cmd` to successfully build, test and package the library.

Currently the best way to work with F# on .NET Core is to use [Visual Studio Code](https://code.visualstudio.com/) with the [Ionide](http://ionide.io/) extension.

## Contributing

Help and feedback is always welcome and pull requests get accepted.

When contributing to this repository, please first discuss the change you wish to make via an open issue before submitting a pull request. For new feature requests please describe your idea in more detail and how it could benefit other users as well.

Please be aware that ASP.NET Core Lambda strictly aims to remain as light as possible while providing generic functionality for building functional web applications. New feature work must be applicable to a broader user base and if this requirement cannot be met sufficiently then a pull request might get rejected. In the case of doubt the maintainer will rather reject a potentially useful feature than adding one too many. This measure is to protect the repository from feature bloat over time and shall not be taken personally.

When making changes please use existing code as a guideline for coding style and documentation. If you intend to add or change an existing `HttpHandler` then please update the README.md file to reflect these changes there as well. If applicable unit tests must be be added or updated and the project must successfully build before a pull request can be accepted.

If you have any further questions please let me know.

You can file an [issue on GitHub](https://github.com/dustinmoris/AspNetCore.Lambda/issues/new) or contact me via [https://dusted.codes/about](https://dusted.codes/about).

## License

[Apache 2.0](https://raw.githubusercontent.com/dustinmoris/AspNetCore.Lambda/master/LICENSE)