# Giraffe

![Giraffe](https://raw.githubusercontent.com/dustinmoris/Giraffe/develop/giraffe.png)

A functional ASP.NET Core micro framework for building rich web applications.

Read [this blog post on functional ASP.NET Core](https://dusted.codes/functional-aspnet-core) for more information.

[![Build status](https://ci.appveyor.com/api/projects/status/0ft2427dflip7wti/branch/master?svg=true)](https://ci.appveyor.com/project/dustinmoris/giraffe/branch/master)
[![NuGet Info](https://buildstats.info/nuget/Giraffe?includePreReleases=true)](https://www.nuget.org/packages/Giraffe/)

[![Build history](https://buildstats.info/appveyor/chart/dustinmoris/giraffe?branch=master&includeBuildsFromPullRequest=false)](https://ci.appveyor.com/project/dustinmoris/giraffe/history)

#### ATTENTION:

 This project has been fairly stable for a while now and will be entering a beta phase relatively soon. Bigger API changes are less frequent now.

Giraffe was formerly known as [ASP.NET Core Lambda](https://www.nuget.org/packages/AspNetCore.Lambda) and has been recently [renamed to better distinguish from AWS Lambda](https://github.com/dustinmoris/Giraffe/issues/15) as well as establish a more unique brand.

The old NuGet package has been unlisted and will not receive any updates any more. Please use the new NuGet package [Giraffe](https://www.nuget.org/packages/Giraffe) going forward.

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
    - [negotiate](#negotiate)
    - [negotiateWith](#negotiatewith)
    - [htmlFile](#htmlfile)
    - [dotLiquid](#dotliquid)
    - [dotLiquidTemplate](#dotliquidtemplate)
    - [dotLiquidHtmlView](#dotliquidhtmlview)
    - [razorView](#razorview)
    - [razorHtmlView](#razorhtmlview)
    - [renderHtml](#renderhtml)
    - [redirectTo](#redirectto)
    - [warbler](#warbler)
- [Custom HttpHandlers](#custom-httphandlers)
- [Model Binding](#model-binding)
    - [BindJson](#bindjson)
    - [BindXml](#bindxml)
    - [BindForm](#bindform)
    - [BindQueryString](#bindquerystring)
    - [BindModel](#bindmodel)
- [Error Handling](#error-handling)
- [Installation](#installation)
- [Sample applications](#sample-applications)
- [Benchmarks](#benchmarks)
- [Building and developing](#building-and-developing)
- [Contributing](#contributing)
- [Contributors](#contributors)
- [License](#license)

## About

[Giraffe](https://www.nuget.org/packages/Giraffe) is an F# web framework similar to Suave, but has been designed with [ASP.NET Core](https://www.asp.net/core) in mind and can be plugged into the ASP.NET Core pipeline via [middleware](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware). [Giraffe](https://www.nuget.org/packages/Giraffe) has been heavily inspired by [Suave](https://suave.io/) and its concept of web parts and the ability to compose many smaller web parts into a larger web application.

If you'd like to learn more about the motivation of this project please read this [blog post on functional ASP.NET Core](https://dusted.codes/functional-aspnet-core).

### Who is it for?

[Giraffe](https://www.nuget.org/packages/Giraffe) is intended for developers who want to build rich web applications on top of ASP.NET Core in a functional first approach. ASP.NET Core is a powerful web platform which has support by Microsoft and a huge developer community behind it and Giraffe is aimed at F# developers who want to benefit from that eco system.

It is not designed to be a competing web product which can be run standalone like NancyFx or Suave, but rather a lean micro framework which aims to complement ASP.NET Core where it comes short for functional developers at the moment. The fundamental idea is to build on top of the strong foundation of ASP.NET Core and re-use existing ASP.NET Core building blocks so F# developers can benefit from both worlds.

You can think of [Giraffe](https://www.nuget.org/packages/Giraffe) as the functional counter part of the ASP.NET Core MVC framework.

## Basics

### HttpHandler

The only building block in Giraffe is a so called `HttpHandler`:

```fsharp
type HttpHandlerResult = Async<HttpContext option>

type HttpHandler = HttpContext -> HttpHandlerResult
```

A `HttpHandler` is a simple function which takes in a `HttpContext` and returns a `HttpContext` (wrapped in an option and async workflow) when finished.

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

The `bind` function takes in a `HttpHandler` function and a `HttpHandlerResult`. It first evaluates the `HttpHandlerResult` and checks its return value. If there was `Some HttpContext` then it will pass it on to the `HttpHandler` function otherwise it will return `None`. If the response object inside the `HttpContext` has already been written, then it will skip the `HttpHandler` function as well and return the current `HttpContext` as the final result.

#### compose (>=>)

The `compose` combinator combines two `HttpHandler` functions into one:

```fsharp
let compose (handler : HttpHandler) (handler2 : HttpHandler) =
    fun (ctx : HttpContext) ->
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
let errorHandler (ex : Exception) (logger : ILogger) (ctx : HttpContext) =
    ctx |> (clearResponse >=> setStatusCode 500 >=> text ex.Message)

let webApp = 
    choose [
        route "/foo" >=> text "Foo"
        route "/bar" >=> text "Bar"
    ]

type Startup() =
    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =
        app.UseGiraffeErrorHandler(errorHandler)
        app.UseGiraffe(webApp)
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
- `%d` for int64 (this is custom to Giraffe)
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

`setBody` sets or modifies the body of the `HttpResponse`. This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more.

#### Example:

```fsharp
let app = 
    choose [
        route  "/foo" >=> setBody (Encoding.UTF8.GetBytes "Some string")
    ]
```

### setBodyAsString

`setBodyAsString` sets or modifies the body of the `HttpResponse`. This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more.

#### Example:

```fsharp
let app = 
    choose [
        route  "/foo" >=> setBodyAsString "Some string"
    ]
```

### text

`text` sets or modifies the body of the `HttpResponse` by sending a plain text value to the client.. This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more.

The different between `text` and `setBodyAsString` is that this http handler also sets the `Content-Type` HTTP header to `text/plain`.

#### Example:

```fsharp
let app = 
    choose [
        route  "/foo" >=> text "Some string"
    ]
```

### json

`json` sets or modifies the body of the `HttpResponse` by sending a JSON serialized object to the client. This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more. It also sets the `Content-Type` HTTP header to `application/json`.

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

`xml` sets or modifies the body of the `HttpResponse` by sending an XML serialized object to the client. This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more. It also sets the `Content-Type` HTTP header to `application/xml`.

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

### negotiate

`negotiate` sets or modifies the body of the `HttpResponse` by inspecting the `Accept` header of the HTTP request and deciding if the response should be sent in JSON or XML or plain text. If the client is indifferent then the default response will be sent in JSON.

This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more.

#### Example:

```fsharp
[<CLIMutable>]
type Person =
    {
        FirstName : string
        LastName  : string
    }
    // The ToString method is used to serialize the object as text/plain during content negotiation
    override this.ToString() =
        sprintf "%s %s" this.FirstName this.LastNam

let app = 
    choose [
        route  "/foo" >=> negotiate { FirstName = "Foo"; LastName = "Bar" }
    ]
```

### negotiateWith

`negotiateWith` sets or modifies the body of the `HttpResponse` by inspecting the `Accept` header of the HTTP request and deciding in what mimeType the response should be sent. A dictionary of type `IDictionary<string, obj -> HttpHandler>` is used to determine which `obj -> HttpHandler` function should be used to convert an object into a `HttpHandler` for a given mime type.

This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more.

#### Example:

```fsharp
[<CLIMutable>]
type Person =
    {
        FirstName : string
        LastName  : string
    }

// xml and json are the two HttpHandler functions from above
let rules =
    dict [
        "*/*"             , xml
        "application/json", json
        "application/xml" , xml
    ]

let app = 
    choose [
        route  "/foo" >=> negotiateWith rules { FirstName = "Foo"; LastName = "Bar" }
    ]
```

### htmlFile

`htmlFile` sets or modifies the body of the `HttpResponse` with the contents of a physical html file. This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more.

This http handler takes a relative path of a html file as input parameter and sets the HTTP header `Content-Type` to `text/html`.

#### Example:

```fsharp
let app = 
    choose [
        route  "/" >=> htmlFile "index.html"
    ]
```

### dotLiquid

`dotLiquid` uses the [DotLiquid](http://dotliquidmarkup.org/) template engine to set or modify the body of the `HttpResponse`. This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more.

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

### dotLiquidTemplate

`dotLiquidTemplate` uses the [DotLiquid](http://dotliquidmarkup.org/) template engine to set or modify the body of the `HttpResponse`. This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more.

This http handler takes a relative path of a template file, an associated model and the contentType of the response as parameters.

#### Example:

```fsharp
type Person =
    {
        FirstName : string
        LastName  : string
    }

let app = 
    choose [
        route  "/foo" >=> dotLiquidTemplate "text/html" "templates/person.html" { FirstName = "Foo"; LastName = "Bar" }
    ]
```

### dotLiquidHtmlView

`dotLiquidHtmlView` is the same as `dotLiquidTemplate` except that it automatically sets the response as `text/html`.

#### Example:

```fsharp
type Person =
    {
        FirstName : string
        LastName  : string
    }

let app = 
    choose [
        route  "/foo" >=> dotLiquidHtmlView "templates/person.html" { FirstName = "Foo"; LastName = "Bar" }
    ]
```

### razorView

`razorView` uses the official ASP.NET Core MVC Razor view engine to compile a page and set the body of the `HttpResponse`. This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more.

The `razorView` handler requires the view name, an object model and the contentType of the response to be passed in. It also requires to be enabled through the `AddRazorEngine` function during start-up.

#### Example:
Add the razor engine service during start-up:

```fsharp
type Startup() =
    member __.ConfigureServices (services : IServiceCollection, env : IHostingEnvironment) =    
        let viewsFolderPath = Path.Combine(env.ContentRootPath, "views")
        services.AddRazorEngine(viewsFolderPath) |> ignore
```

Use the razorView function:

```fsharp
let model = { WelcomeText = "Hello World" }

let app = 
    choose [
        // Assuming there is a view called "Index.cshtml"
        route  "/" >=> razorView "text/html" "Index" model
    ]
```

### razorHtmlView

`razorHtmlView` is the same as `razorView` except that it automatically sets the response as `text/html`.

#### Example:
Add the razor engine service during start-up:

```fsharp
type Startup() =
    member __.ConfigureServices (services : IServiceCollection, env : IHostingEnvironment) =    
        let viewsFolderPath = Path.Combine(env.ContentRootPath, "views")
        services.AddRazorEngine(viewsFolderPath) |> ignore
```

Use the razorView function:

```fsharp
let model = { WelcomeText = "Hello World" }

let app = 
    choose [
        // Assuming there is a view called "Index.cshtml"
        route  "/" >=> razorHtmlView "Index" model
    ]
```

### renderHtml

`renderHtml` is a more functional way of generating HTML by composing HTML elements in F# to generate a rich Model-View output.

It is based on [Suave's Experimental Html](https://github.com/SuaveIO/suave/blob/master/src/Experimental/Html.fs) and bears some resemblance with [Elm](http://elm-lang.org/examples).

#### Example:
Create a function that accepts a model and returns a `HtmlNode`:

```fsharp
open Giraffe.HtmlEngine

let model = { Name = "John Doe" }

let layout (content: HtmlNode list) =
    html [] [
        head [] [
            title [] (encodedText "Giraffe")
        ]
        body [] content
    ]

let partial () =
    p [] (encodedText "Some partial text.")

let personView model =
    [
        div [] [
                h3 [] (sprintf "Hello, %s" model.Name |> encodedText)
            ]
        div [] [partial()]
    ] |> layout

let app = 
    choose [
        route "/" >=> (personView model |> renderHtml)
    ]
```

### redirectTo

`redirectTo` uses a 302 or 301 (when permanent) HTTP response code to redirect the client to the specified location. It takes in two parameters, a boolean flag denoting whether the redirect should be permanent or not and the location to redirect to.

#### Example:

```fsharp
let app = 
    choose [
        route "/"          >=> redirectTo false "/foo"
        route "/permanent" >=> redirectTo true "http://example.org"
        route "/foo"       >=> text "Some string"
    ]
```

### warbler

If your route is not returning a static response, then you should wrap your function with a warbler.

#### Example
```fsharp
// unit -> string
let time() =
    System.DateTime.Now.ToString()

let webApp = 
    choose [
        GET >=>
            choose [
                route "/once"      >=> (time() |> text)
                route "/everytime" >=> warbler (fun _ -> (time() |> text))
            ]
    ]
```

Functions in F# are eagerly evaluated and the `/once` route will only be evaluated the first time.
A warbler will help to evaluate the function every time the route is hit.

```fsharp
// ('a -> 'a -> 'b) -> 'a -> 'b
let warbler f a = f a a
```

## Custom HttpHandlers

Defining a new `HttpHandler` is fairly easy. All you need to do is to create a new function which matches the signature of `HttpContext -> Async<HttpContext option>`. Through currying your custom `HttpHandler` can extend the original signature as long as the partial application of your function will still return a function of `HttpContext -> Async<HttpContext option>`.

### Example:

Defining a custom HTTP handler to partially filter a route:

*(After creating this example I added the `routeStartsWith` HttpHandler to the list of default handlers as it turned out to be quite useful)*

```fsharp
let routeStartsWith (subPath : string) =
    fun (ctx : HttpContext) ->
        if ctx.Request.Path.ToString().StartsWith subPath 
        then Some ctx
        else None
        |> async.Return
```

Defining another custom HTTP handler to validate a mandatory HTTP header:

```fsharp
let requiresToken (expectedToken : string) (handler : HttpHandler) =
    fun (ctx : HttpContext) ->
        let token    = ctx.Request.Headers.["X-Token"].ToString()
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

## Model Binding

The `Giraffe.HttpContextExtensions` module exposes a default set of model binding functions which extend the `HttpContext` object.

### BindJson

`ctx.BindJson<'T>()` can be used to bind a JSON payload to a strongly typed model.

#### Example

Define an F# record type with the `CLIMutable` attribute which will add a parameterless constructor to the type:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }
```

Then create a new `HttpHandler` which uses `BindJson` and use it from an app:

```fsharp
open Giraffe.HttpHandlers
open Giraffe.HttpContextExtensions

let submitCar =
    fun (ctx : HttpContext) ->
        async {
            // Binds a JSON payload to a Car object
            let! car = ctx.BindJson<Car>()

            // Serializes the Car object back into JSON
            // and sends it back as the response.
            return! json car ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "ping" >=> text "pong" ]
        POST >=> route "/car" >=> submitCar ]
```

You can test the bind function by sending a HTTP request with a JSON payload:

```
POST http://localhost:5000/car HTTP/1.1
Host: localhost:5000
Connection: keep-alive
Content-Length: 77
Cache-Control: no-cache
Content-Type: application/json
Accept: */*

{ "Name": "DB9", "Make": "Aston Martin", "Wheels": 4, "Built": "2016-01-01" }
```

### bindXml

`ctx.BindXml<'T>()` can be used to bind an XML payload to a strongly typed model.

#### Example

Define an F# record type with the `CLIMutable` attribute which will add a parameterless constructor to the type:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }
```

Then create a new `HttpHandler` which uses `BindXml` and use it from an app:

```fsharp
open Giraffe.HttpHandlers
open Giraffe.HttpContextExtensions

let submitCar =
    fun (ctx : HttpContext) ->
        async {
            // Binds an XML payload to a Car object
            let! car = ctx.BindXml<Car>()

            // Serializes the Car object back into JSON
            // and sends it back as the response.
            return! json car ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "ping" >=> text "pong" ]
        POST >=> route "/car" >=> submitCar ]
```

You can test the bind function by sending a HTTP request with an XML payload:

```
POST http://localhost:5000/car HTTP/1.1
Host: localhost:5000
Connection: keep-alive
Content-Length: 104
Cache-Control: no-cache
Content-Type: application/xml
Accept: */*

<Car>
    <Name>DB9</Name>
    <Make>Aston Martin</Make>
    <Wheels>4</Wheels>
    <Built>2016-01-01</Built>
</Car>
```

### bindForm

`ctx.BindForm<'T>()` can be used to bind a form urlencoded payload to a strongly typed model.

#### Example

Define an F# record type with the `CLIMutable` attribute which will add a parameterless constructor to the type:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }
```

Then create a new `HttpHandler` which uses `BindForm` and use it from an app:

```fsharp
open Giraffe.HttpHandlers
open Giraffe.HttpContextExtensions

let submitCar =
    fun (ctx : HttpContext) ->
        async {
            // Binds a form urlencoded payload to a Car object
            let! car = ctx.BindForm<Car>()

            // Serializes the Car object back into JSON
            // and sends it back as the response.
            return! json car ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "ping" >=> text "pong" ]
        POST >=> route "/car" >=> submitCar ]
```

You can test the bind function by sending a HTTP request with a form payload:

```
POST http://localhost:5000/car HTTP/1.1
Host: localhost:5000
Connection: keep-alive
Content-Length: 52
Cache-Control: no-cache
Content-Type: application/x-www-form-urlencoded
Accept: */*

Name=DB9&Make=Aston+Martin&Wheels=4&Built=2016-01-01
```

### bindQueryString

`ctx.BindQueryString<'T>()` can be used to bind a query string to a strongly typed model.

#### Example

Define an F# record type with the `CLIMutable` attribute which will add a parameterless constructor to the type:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }
```

Then create a new `HttpHandler` which uses `BindQueryString` and use it from an app:

```fsharp
open Giraffe.HttpHandlers
open Giraffe.HttpContextExtensions

let submitCar =
    fun (ctx : HttpContext) ->
        async {
            // Binds a query string to a Car object
            let! car = ctx.BindQueryString<Car>()

            // Serializes the Car object back into JSON
            // and sends it back as the response.
            return! json car ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "ping" >=> text "pong"
                route "/car" >=> submitCar ]
```

You can test the bind function by sending a HTTP request with a query string:

```
GET http://localhost:5000/car?Name=Aston%20Martin&Make=DB9&Wheels=4&Built=1990-04-20 HTTP/1.1
Host: localhost:5000
Cache-Control: no-cache
Accept: */*

```

### bindModel

`ctx.BindModel<'T>()` can be used to automatically detect the method and `Content-Type` of a HTTP request and automatically bind a JSON, XML,or form urlencoded payload or a query string to a strongly typed model.

#### Example

Define an F# record type with the `CLIMutable` attribute which will add a parameterless constructor to the type:

```fsharp
[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime
    }
```

Then create a new `HttpHandler` which uses `BindModel` and use it from an app:

```fsharp
open Giraffe.HttpHandlers
open Giraffe.HttpContextExtensions

let submitCar =
    fun (ctx : HttpContext) ->
        async {
            // Binds a JSON, XML or form urlencoded payload to a Car object
            let! car = ctx.BindModel<Car>()

            // Serializes the Car object back into JSON
            // and sends it back as the response.
            return! json car ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/"    >=> text "index"
                route "ping" >=> text "pong" ]
        // Can accept GET and POST requests and
        // bind a model from the payload or query string
        route "/car" >=> submitCar ]
```

## Error Handling

Similar to defining a web application in Giraffe you can also set a global error handler, which can react to unhandled server exceptions.

The `ErrorHandler` is a function which accepts the unhandled exception object and a default logger and returns a `HttpHandler` function which essentially works the same way as the `HttpHandler` functions of a web application:

```fsharp
type ErrorHandler = exn -> ILogger -> HttpHandler
```

For example you could create an error handler which logs the unhandled exception and returns a HTTP 500 response with the error message as plain text:

```fsharp
let errorHandler (ex : Exception) (logger : ILogger) (ctx : HttpContext) =
    logger.LogError(EventId(0), ex, "An unhandled exception has occurred while executing the request.")
    ctx |> (clearResponse >=> setStatusCode 500 >=> text ex.Message)
```

In order to enable the error handler you have to configure the error handler in your application startup:

```fsharp
type Startup() =
    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =
        app.UseGiraffeErrorHandler errorHandler
        app.UseGiraffe webApp
```

It is recommended to set the error handler as the first middleware in the pipeline, so that any exception from a following middleware can be caught by the error handling function.

## Installation

### Using dotnet-new

The easiest way to get started with Giraffe is by installing the [`giraffe-template`](https://www.nuget.org/packages/giraffe-template) NuGet package, which adds a new template to your `dotnet new` command:

```
dotnet new -i giraffe-template::*
```

Afterwards you can create a new Giraffe application by running `dotnet new giraffe`.

### Doing it manually

Install the [Giraffe](https://www.nuget.org/packages/Giraffe) NuGet package:

```
PM> Install-Package Giraffe
```

Create a web application and plug it into the ASP.NET Core middleware:

```fsharp
open Giraffe.HttpHandlers
open Giraffe.Middleware

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

## Sample applications

There is a basic sample application in the [`/samples/SampleApp`](https://github.com/dustinmoris/Giraffe/tree/develop/samples/SampleApp) folder.

More sample applications will be added in the future.

## Benchmarks

Currently Giraffe has only been tested against a simple plain text route and measured the total amount of handled requests per second. The latest result yielded an average of 79093 req/s over a period of 10 seconds, which was only closely after plain Kestrel which was capable of handling 79399 req/s on average.

Please check out [Jimmy Byrd](https://github.com/TheAngryByrd)'s [dotnet-web-benchmarks](https://github.com/TheAngryByrd/dotnet-web-benchmarks) for more details.

## Building and developing

Giraffe is built with the latest [.NET Core SDK](https://www.microsoft.com/net/download/core).

You can either install [Visual Studio 2017](https://www.visualstudio.com/vs/) which comes with the latest SDK or manually download and install the [.NET SDK 1.1](https://www.microsoft.com/net/download/core).

After installation you should be able to run the `.\build.ps1` script to successfully build, test and package the library.

The build script supports the following flags:

- `-IncludeTests` will build and run the tests project as well
- `-IncludeSamples` will build and test the samples project as well
- `-All` will build and test all projects
- `-Release` will build Giraffe with the `Release` configuration
- `-Pack` will create a NuGet package for Giraffe and giraffe-template.
- `-OnlyNetStandard` will build Giraffe only targeting the NETStandard1.6 framework ([see known issues](#known-issues))

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

### Development environment

Currently the best way to work with F# on .NET Core is to use [Visual Studio Code](https://code.visualstudio.com/) with the [Ionide](http://ionide.io/) extension. Intellisense and debugging is supported with the latest versions of both.

#### Known issues

Currently there is a known issue with Ionide where [Intellisense breaks when a project targets multiple frameworks](https://github.com/ionide/ionide-vscode-fsharp/issues/416).

This issue affects Giraffe because it targets more than one framework and therefore breaks Intellisense when building the project with the default configuration.

During development you can workaround this issue by invoking the build script with the `-OnlyNetStandard` flag:

```
PS > .\build.ps1 -OnlyNetStandard
``` 

This switch will override the default configuration and allow a frictionless development experience.

The official build by the build server doesn't use this setting and builds the project against all supported target frameworks as you would expect it.

## Contributing

Help and feedback is always welcome and pull requests get accepted.

When contributing to this repository, please first discuss the change you wish to make via an open issue before submitting a pull request. For new feature requests please describe your idea in more detail and how it could benefit other users as well.

Please be aware that Giraffe strictly aims to remain as light as possible while providing generic functionality for building functional web applications. New feature work must be applicable to a broader user base and if this requirement cannot be met sufficiently then a pull request might get rejected. In the case of doubt the maintainer will rather reject a potentially useful feature than adding one too many. This measure is to protect the repository from feature bloat over time and shall not be taken personally.

When making changes please use existing code as a guideline for coding style and documentation. If you intend to add or change an existing `HttpHandler` then please update the README.md file to reflect these changes there as well. If applicable unit tests must be be added or updated and the project must successfully build before a pull request can be accepted.

If you have any further questions please let me know.

You can file an [issue on GitHub](https://github.com/dustinmoris/Giraffe/issues/new) or contact me via [https://dusted.codes/about](https://dusted.codes/about).

## Contributors

Special thanks to all developers who helped me by submitting pull requests with new feature work, bug fixes and other improvements to keep the project in good shape (in no particular order):

- [slang25](https://github.com/slang25) (Added subRoute feature and general help to keep things in good shape)
- [Nicolás Herrera](https://github.com/nicolocodev) (Added razor engine feature)
- [Dave Shaw](https://github.com/xdaDaveShaw) (Extended sample application and general help to keep things in good shape)
- [Tobias Burger](https://github.com/toburger) (Fixed issues with culture specific parsers)
- [David Sinclair](https://github.com/dsincl12) (Created the dotnet-new template for Giraffe)
- [Florian Verdonck](https://github.com/nojaf) (Ported Suave's experimental Html into Giraffe, implemented the warbler and general help with the project)
- [Roman Melnikov](https://github.com/Neftedollar) (Added `redirectTo` route)
- [Diego B. Fernandez](https://github.com/diegobfernandez) (Added support for the `Option<'T>` type in the query string model binding)

If you submit a pull request please feel free to add yourself to this list as part of the PR.

## License

[Apache 2.0](https://raw.githubusercontent.com/dustinmoris/Giraffe/master/LICENSE)