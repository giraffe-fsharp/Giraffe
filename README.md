# ASP.NET Core Lambda

A functional ASP.NET Core micro framework for building rich web applications.

[![Build status](https://ci.appveyor.com/api/projects/status/0ft2427dflip7wti/branch/master?svg=true)](https://ci.appveyor.com/project/dustinmoris/aspnetcore-lambda/branch/master)
[![NuGet Info](https://buildstats.info/nuget/AspNetCore.Lambda?includePreReleases=true)](https://www.nuget.org/packages/AspNetCore.Lambda/)

[![Build history](https://buildstats.info/appveyor/chart/dustinmoris/aspnetcore-lambda)](https://ci.appveyor.com/project/dustinmoris/aspnetcore-lambda/history)

**ATTENTION: THIS PROJECT IS STILL WORK IN PROGRESS**

## Table of contents

- [About](#about)
- [Basics](#basics)
    - [HttpHandler](#httphandler)
    - [Combinators](#combinators)
        - [bind (>>=)](#bind-)
        - [choose](#choose)
- [Default HttpHandlers](#default-httphandlers)
    - [choose](#choose)
    - [GET, POST, PUT, PATCH, DELETE](#get-post-put-patch-delete)
    - [mustAccept](#mustaccept)
    - [route](#route)
    - [routef](#routef)
    - [routeci](#routeci)
    - [routecif](#routecif)
    - [routeStartsWith](#routestartswith)
    - [routeStartsWithCi](#routestartswithci)
    - [setStatusCode](#setstatuscode)
    - [setHttpHeader](#sethttpheader)
    - [setBody](#setbody)
    - [setBodyAsString](#setbodyasstring)
    - [text](#text)
    - [json](#json)
    - [dotLiquid](#dotliquid)
    - [htmlTemplate](#htmltemplate)
    - [htmlFile](#htmlfile)
- [Custom HttpHandlers](#custom-httphandlers)
- [Installation](#installation)
- [License](#license)
- [Contribution](#contribution)

## About

ASP.NET Core Lambda is an F# web framework similar to Suave, but has been designed with [ASP.NET Core](https://www.asp.net/core) in mind and can be plugged into the ASP.NET Core pipeline via [middleware](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware). ASP.NET Core Lambda has been heavily inspired by [Suave](https://suave.io/) and its concept of web parts and the ability to compose many smaller web parts into a large web application.

### Who is it for?

ASP.NET Core Lambda is intended for developers who want to build rich web applications on top of ASP.NET Core in a functional first approach. ASP.NET Core is a powerful web platform, which has Microsoft and a huge developer community behind it and ASP.NET Core Lambda is designed for F# developers who want to benefit from that eco system.

It is designed to be as lean as possible and to extend ASP.NET Core only where there's a lack for functional programmers today instead of re-inventing the entire platform from scratch. It's mentality is to stand on top of the shoulders of a giant and re-use the existing ASP.NET Core building blocks as much as possible.

You can think of it as an equivalent of the MVC framework, but for functional developers.

## Basics

### HttpHandler

The only building block in ASP.NET Core Lambda is a so called `HttpHandler`:

```
type HttpHandlerContext =
    {
        HttpContext  : HttpContext
        Environment  : IHostingEnvironment
        Logger       : ILogger
    }

type HttpHandler = HttpHandlerContext -> Async<HttpHandlerContext option>
```

A `HttpHandler` is a simple function which takes in a `HttpHandlerContext` and returns an object of the same type when finished.

Inside that function it can process an incoming `HttpRequest` and make changes to the `HttpResponse` of the given `HttpContext`. By receiving and returning a `HttpContext` there's nothing which cannot be done from inside a `HttpHandler`.

A `HttpHandler` can decide to not further process an incoming request and return `None` instead. In this case another `HttpHandler` might continue processing the request or the middleware will simply defer to the next `RequestDelegate` in the ASP.NET Core pipeline.

### Combinators

#### bind (>>=)

The core combinator is the `bind` function which you might be familiar with:

```
let bind (handler : HttpHandler) (handler2 : HttpHandler) =
    fun ctx ->
        async {
            let! result = handler ctx
            match result with
            | None      -> return  None
            | Some ctx2 -> return! handler2 ctx2
        }

let (>>=) = bind
```

The `bind` function takes in two different http handlers and a `HttpHandlerContext` (ctx) object. It first invokes the first handler and checks its result. If there was a `HttpHandlerContext` returned then it will pass it on to the second `HttpHandler` and return the overall result of that function. If there was nothing then it will short circuit after the first handler and return `None`.

This allows composing many smaller `HttpHandler` functions into a bigger web application.

#### choose

The `choose` combinator function iterates through a list of `HttpHandler` functions and invokes each individual until the first handler returns a result.

#### Example:

```
let app = 
    choose [
        route "/foo" >>= text "Foo"
        route "/bar" >>= text "Bar"
    ]
```

## Default HttpHandlers

### GET, POST, PUT, PATCH, DELETE

`GET`, `POST`, `PUT`, `PATCH`, `DELETE` filters a request by the given HTTP verb.

#### Example:

```
let app = 
    choose [
        GET  >>= route "/foo" >>= text "GET Foo"
        POST >>= route "/foo" >>= text "POST Foo"
        route "/bar" >>= text "Always Bar"
    ]
```

### mustAccept

`mustAccept` filters a request by the `Accept` HTTP header. You can use it to check if a client accepts a certain mime type before returning a response.

#### Example:

```
let app = 
    mustAccept [ "text/plain"; "application/json" ] >>=
        choose [
            route "/foo" >>= text "Foo"
            route "/bar" >>= json "Bar"
        ]
```

### route

`route` compares a given path with the actual request path and short circuits if it doesn't match.

#### Example:

```
let app = 
    choose [
        route "/"    >>= text "Index path"
        route "/foo" >>= text "Foo"
        route "/bar" >>= text "Bar"
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

```
let app = 
    choose [
        route  "/foo" >>= text "Foo"
        routef "/bar/%s/%i" (fun (name, age) ->
            // name is of type string
            // age is of type int
            text (sprintf "Name: %s, Age: %i" name age))
    ]
```

### routeci

`routeci` is the case insensitive version of `route`.

#### Example:

```
// "/FoO", "/fOO", "/bAr", etc. will match as well

let app = 
    choose [
        routeci "/"    >>= text "Index path"
        routeci "/foo" >>= text "Foo"
        routeci "/bar" >>= text "Bar"
    ]
```

### routecif

`routecif` is the case insensitive version of `routef`.

#### Example:

```
let app = 
    choose [
        route  "/foo" >>= text "Foo"
        routecif "/bar/%s/%i" (fun (name, age) ->
            text (sprintf "Name: %s, Age: %i" name age))
    ]
```

### routeStartsWith

`routeStartsWith` checks if the current request path starts with the given string. This can be a useful filter when a subset of routes require an additional step of verifiation (e.g. admin or api routes).

#### Example:

```
let app = 
    routeStartsWith "/api/v1/" >>=
        choose [
            route "/api/v1/foo" >>= text "Foo"
            route "/api/v1/bar" >>= text "Bar"
        ]
```

### routeStartsWithCi

`routeStartsWithCi` is the case insensitive version of `routeStartsWith`.

#### Example:

```
let app = 
    routeStartsWithCi "/api/v1/" >>=
        choose [
            route "/api/v1/foo" >>= text "Foo"
            route "/api/v1/bar" >>= text "Bar"
        ]
```

### setStatusCode

`setStatusCode` changes the status code of the `HttpResponse`.

#### Example:

```
let app = 
    choose [
        route  "/foo" >>= text "Foo"
        setStatusCode 404 >>= text "Not found"
    ]
```

### setHttpHeader

`setHttpHeader` sets or modifies a HTTP header of the `HttpResponse`.

#### Example:

```
let app = 
    choose [
        route  "/foo" >>= text "Foo"
        setStatusCode 404 >>= setHttpHeader "X-CustomHeader" "something" >>= text "Not found"
    ]
```

### setBody

`setBody` sets or modifies the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers which attempt to modify the HTTP headers of the response cannot be executed afterwards anymore.

#### Example:

```
let app = 
    choose [
        route  "/foo" >>= setBody (Encoding.UTF8.GetBytes "Some string")
    ]
```

### setBodyAsString

`setBodyAsString` sets or modifies the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers which attempt to modify the HTTP headers of the response cannot be executed afterwards anymore.

#### Example:

```
let app = 
    choose [
        route  "/foo" >>= setBodyAsString "Some string"
    ]
```

### text

`text` sets or modifies the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers which attempt to modify the HTTP headers of the response cannot be executed afterwards anymore.

The different between `text` an `setBodyAsString` is that this http handler also sets the `Content-Type` HTTP header to `text/plain`.

#### Example:

```
let app = 
    choose [
        route  "/foo" >>= text "Some string"
    ]
```

### json

`json` sets or modifies the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers which attempt to modify the HTTP headers of the response cannot be executed afterwards anymore.

The different between `json` an `setBodyAsString` or `setBody` is that this http handler also sets the `Content-Type` HTTP header to `application/json`.

#### Example:

```
type Person =
    {
        FirstName : string
        LastName  : string
    }

let app = 
    choose [
        route  "/foo" >>= json { FirstName = "Foo"; LastName = "Bar" }
    ]
```

### dotLiquid

`dotLiquid` uses the [DotLiquid](http://dotliquidmarkup.org/) template engine to set or modify the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers which attempt to modify the HTTP headers of the response cannot be executed afterwards anymore.

The `dotLiquid` handler requires the content type and the actual template of the response as two string values together with a model. This handler is supposed to be used as the base handler for more http handlers being build on the DotLiquid template engine (e.g. you could create an SVG handler on top of it).

#### Example:

```
type Person =
    {
        FirstName : string
        LastName  : string
    }

let template = "<html><head><title>DotLiquid</title></head><body><p>First name: {{ firstName }}<br />Last name: {{ lastName }}</p></body></html>

let app = 
    choose [
        route  "/foo" >>= dotLiquid "text/html" template { FirstName = "Foo"; LastName = "Bar" }
    ]
```

### htmlTemplate

`htmlTemplate` uses the [DotLiquid](http://dotliquidmarkup.org/) template engine to set or modify the body of the `HttpResponse`. This http handler triggers the response being sent to the client and other http handlers which attempt to modify the HTTP headers of the response cannot be executed afterwards anymore.

This http handler takes a relative path of a template file and the associated model to set a HTTP response with a `Content-Type` of `text/html`.

#### Example:

```
type Person =
    {
        FirstName : string
        LastName  : string
    }

let app = 
    choose [
        route  "/foo" >>= htmlTemplate "templates/person.html" { FirstName = "Foo"; LastName = "Bar" }
    ]
```

### htmlFile

`htmlFile` sets or modifies the body of the `HttpResponse` with the contents of a physical html file. This http handler triggers the response being sent to the client and other http handlers which attempt to modify the HTTP headers of the response cannot be executed afterwards anymore.

This http handler takes a relative path of a html file and sets the HTTP response with the `Content-Type` of `text/html`.

#### Example:

```
let app = 
    choose [
        route  "/" >>= htmlFile "index.html"
    ]
```

## Custom HttpHandlers

Defining a new `HttpHandler` is fairly easy. All you need to do is to create a new function which matches the signature of `HttpHandlerContext -> Async<HttpHandlerContext option>`. Through currying your custom `HttpHandler` can extend the original signature as long as the partial application of your function will still return a function of `HttpHandlerContext -> Async<HttpHandlerContext option>`.

### Example:

Defining a custom HTTP handler to partially filter a route:

*(After creating this example HTTP handler I added it to the list of default handlers as it turns out to be quite useful)*

```
let routeStartsWith (partOfPath : string) =
    fun ctx ->
        if ctx.HttpContext.Request.Path.ToString().StartsWith partOfPath 
        then Some ctx
        else None
        |> async.Return
```

Defining another custom HTTP handler to validate a mandatory HTTP header:

*(This is only an extremly simmplified example of showing how to add custom authentication handlers.)*

```
let requiresToken (expectedToken : string) (handler : HttpHandler) =
    fun ctx ->
        let token    = ctx.HttpContext.Request.Headers.["X-Token"].ToString()
        let response =
            if token.Equals(expectedToken)
            then handler
            else setStatusCode 401 >>= text "Token wrong or missing"
        response ctx
```

Composing a web application from smaller HTTP handlers:

```
let app = 
    choose [
        route "/"       >>= htmlFile "index.html"
        route "/about"  >>= htmlFile "about.html"
        routeStartsWith "/api/v1/" >>=
            requiresToken "secretToken" (
                choose [
                    route "/api/v1/foo" >>= text "something"
                    route "/api/v1/bar" >>= text "bar"
                ]
            )
        setStatusCode 404 >>= text "Not found"
    ] : HttpHandler
```

## Installation

Install the [AspNetCore.Lambda](https://www.nuget.org/packages/AspNetCore.Lambda) NuGet package:

```
PM> Install-Package AspNetCore.Lambda
```

Create a web application and plug it into the ASP.NET Core middleware:

```
open AspNetCore.Lambda.HttpHandlers
open AspNetCore.Lambda.Middleware

let webApp = 
    choose [
        route "/ping"   >>= text "pong"
        route "/"       >>= htmlFile "/pages/index.html" ]

type Startup() =
    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =
        
        app.UseLambda(webApp)
```

## License

[Apache 2.0](https://raw.githubusercontent.com/dustinmoris/AspNetCore.Lambda/master/LICENSE)

## Contribution

Feedback is more than welcome and pull requests get accepted!

File an [issue on GitHub](https://github.com/dustinmoris/AspNetCore.Lambda/issues/new) or contact me via [https://dusted.codes/about](https://dusted.codes/about).