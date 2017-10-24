# Giraffe

![Giraffe](https://raw.githubusercontent.com/dustinmoris/Giraffe/develop/giraffe.png)

A functional ASP.NET Core micro web framework for building rich web applications.

Read [this blog post on functional ASP.NET Core](https://dusted.codes/functional-aspnet-core) for more information.

[![NuGet Info](https://buildstats.info/nuget/Giraffe?includePreReleases=true)](https://www.nuget.org/packages/Giraffe/)

| Windows | Linux |
| :------ | :---- |
| [![Windows Build status](https://ci.appveyor.com/api/projects/status/0ft2427dflip7wti/branch/develop?svg=true)](https://ci.appveyor.com/project/dustinmoris/giraffe/branch/develop) | [![Linux Build status](https://travis-ci.org/dustinmoris/Giraffe.svg?branch=develop)](https://travis-ci.org/dustinmoris/Giraffe/builds?branch=develop) |
| [![Windows Build history](https://buildstats.info/appveyor/chart/dustinmoris/giraffe?branch=develop&includeBuildsFromPullRequest=false)](https://ci.appveyor.com/project/dustinmoris/giraffe/history) | [![Linux Build history](https://buildstats.info/travisci/chart/dustinmoris/Giraffe?branch=develop&includeBuildsFromPullRequest=false)](https://travis-ci.org/dustinmoris/Giraffe/builds?branch=develop) |

#### ATTENTION:

Giraffe was formerly known as [ASP.NET Core Lambda](https://www.nuget.org/packages/AspNetCore.Lambda) and has been later [renamed to Giraffe](https://github.com/dustinmoris/Giraffe/issues/15) to better distinguish from AWS Lambda and to establish its own unique brand.

The old NuGet package has been unlisted and will no longer receive any updates. Please use the [Giraffe NuGet package](https://www.nuget.org/packages/Giraffe) going forward.

## Table of contents

- [About](#about)
- [Basics](#basics)
    - [HttpHandler](#httphandler)
    - [Combinators](#combinators)
        - [compose (>=>)](#compose-)
        - [choose](#choose)
    - [Tasks](#tasks)
- [Default HttpHandlers](#default-httphandlers)
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
    - [routeBind](#routebind)
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
    - [renderHtml](#renderhtml)
    - [redirectTo](#redirectto)
    - [warbler](#warbler)
- [Additional HttpHandlers](#additional-httphandlers)
    - [Giraffe.Razor](#girafferazor)
        - [razorView](#razorview)
        - [razorHtmlView](#razorhtmlview)
    - [Giraffe.DotLiquid](#giraffedotliquid)
        - [dotLiquid](#dotliquid)
        - [dotLiquidTemplate](#dotliquidtemplate)
        - [dotLiquidHtmlView](#dotliquidhtmlview)
    - [Giraffe.TokenRouter](#giraffetokenrouter)
        - [router](#router)
        - [routing functions](#routing-functions)
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
- [Blog posts](#blog-posts)
- [License](#license)
- [Contact and Slack Channel](#contact-and-slack-channel)

## About

[Giraffe](https://www.nuget.org/packages/Giraffe) is an F# micro web framework for building rich web applications. It has been heavily inspired and is similar to [Suave](https://suave.io/), but has been specifically designed with [ASP.NET Core](https://www.asp.net/core) in mind and can be plugged into the ASP.NET Core pipeline via [middleware](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware). Giraffe applications are composed of so called `HttpHandler` functions which can be thought of a mixture of Suave's WebParts and ASP.NET Core's middleware.

If you'd like to learn more about the motivation of this project please read my [blog post on functional ASP.NET Core](https://dusted.codes/functional-aspnet-core) (some code samples in this blog post might be outdated today).

### Who is it for?

[Giraffe](https://www.nuget.org/packages/Giraffe) is intended for developers who want to build rich web applications on top of ASP.NET Core in a functional first approach. ASP.NET Core is a powerful web platform which has support by Microsoft and a huge developer community behind it and Giraffe is aimed at F# developers who want to benefit from that eco system.

It is not designed to be a competing web product which can be run standalone like NancyFx or Suave, but rather a lean micro framework which aims to complement ASP.NET Core where it comes short for functional developers. The fundamental idea is to build on top of the strong foundation of ASP.NET Core and re-use existing ASP.NET Core building blocks so F# developers can benefit from both worlds.

You can think of [Giraffe](https://www.nuget.org/packages/Giraffe) as the functional counter part of the ASP.NET Core MVC framework.

## Basics

### HttpHandler

The main building block in Giraffe is a so called `HttpHandler`:

```fsharp
type HttpFuncResult = Task<HttpContext option>
type HttpFunc = HttpContext -> HttpFuncResult
type HttpHandler = HttpFunc -> HttpContext -> HttpFuncResult
```

A `HttpHandler` is a simple function which takes two curried arguments, a `HttpFunc` and a `HttpContext`, and returns a `HttpContext` (wrapped in an `option` and `Task` workflow) when finished.

Given that a `HttpHandler` receives and returns an ASP.NET Core `HttpContext` there is literally nothing which cannot be done from within a Giraffe web application which couldn't be done from a regular ASP.NET Core (MVC) application either.

Each `HttpHandler` can process an incoming `HttpRequest` before passing it further down the pipeline by invoking the next `HttpFunc` or short circuit the execution by returning an option of `Some HttpContext`.

If a `HttpHandler` decides to not process an incoming `HttpRequest` at all, then it can return `None` instead. In this case another `HttpHandler` might pick up the incoming `HttpRequest` or the middleware will defer to the next `RequestDelegate` from the ASP.NET Core pipeline.

The easiest way to get your head around a Giraffe `HttpHandler` is to think of it like a functional ASP.NET Core middleware. Each handler has the full `HttpContext` at its disposal and can decide whether it wants to return `Some HttpContext`, `None` or pass it on to the "next" `HttpFunc`.

Please check out the [sample applications](#sample-applications) for a demo as well as a real world example.

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
let app = route "/" >=> setStatusCode 200 >=> text "Hello World"
```

#### choose

The `choose` combinator function iterates through a list of `HttpHandler` functions and invokes each individual handler until the first `HttpHandler` returns a result.

##### Example:

```fsharp
let app =
    choose [
        route "/foo" >=> text "Foo"
        route "/bar" >=> text "Bar"
    ]
```

### Tasks

Another important aspect to Giraffe is that it natively works with .NET's `Task` and `Task<'T>` objects instead of relying on F#'s `async {}` workflows. The main benefit of this is that it removes the necessity of converting back and forth between tasks and async workflows when building a Giraffe web application (because ASP.NET Core works only with tasks out of the box).

For this purpose Giraffe has it's own `task {}` workflow which comes with the `Giraffe.Tasks` module. Syntactically it works identical to F#'s async workflows:

```fsharp
open Giraffe.Tasks
open Giraffe.HttpHandlers

let personHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! person = ctx.BindModel<Person>()
            return! json person next ctx
        }
```

The `task {}` workflow is not strictly tied to Giraffe and can be used from anywhere in an F# application:

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

Note, you can still continue to use regular `Async<'T>` workflows from within the `task{}` computation expression without having to manually convert back into a `Task<'T>`.

If you were to write the same code with F#'s async workflow it would look something like this:


```fsharp
let readFileAndDoSomething (filePath : string) =
    async {
        use stream = new FileStream(filePath, FileMode.Open)
        use reader = new StreamReader(stream)
        let! contents =
            reader.ReadToEndAsync()
            |> Async.AwaitTask

        // do something with contents

        return contents
    }
```

Apart from the convenience of not having to convert from a `Task<'T>` into an `Async<'T>` workflow and then back again into a `Task<'T>` when returning back to the ASP.NET Core pipeline it also proved to improve overall performance by two figure % in comparison to F#'s async implementation.

The original code for Giraffe's task implementation has been taken from [Robert Peele](https://github.com/rspeele)'s [TaskBuilder.fs](https://github.com/rspeele/TaskBuilder.fs) and gradually modified to better fit Giraffe's use case for a highly scalable ASP.NET Core web application.

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
let errorHandler (ex : Exception) (logger : ILogger) =
    clearResponse
    >=> setStatusCode 500
    >=> text ex.Message

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

### routeBind

`routeBind` matches and parses a request path with a given object model. On success it will resolve the arguments from the route and create an instance of type `'T` and invoke the given
`HttpHandler` with it.

The `route` parameter of the `routeBind` handler can include any standard .NET Regex to allow greater flexibility when binding a route to an object model. For example `/{foo}/{bar}(/?)` would additionally specify that the route may end with zero or one trailing slash in order to successfully bind to the model.

#### Example:

```fsharp
type Person =
    {
        FirstName : string
        LastName : string

let app =
    choose [
        routeBind<Person> "/foo/{firstName}/{lastName}(/?)" (fun person ->
            sprintf "%s %s" person.FirstName person.LastName
            |> text)
    ]
    }

// HTTP GET /foo/John/Doe   --> Success
// HTTP GET /foo/John/Doe/  --> Success
// HTTP GET /foo/John/Doe// --> Failure

// The last case will not bind to the Person model, because the Regex doesn't allow more than one trailing slash (change ? to * and it would work)
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

### renderHtml

`renderHtml` is a more functional way of generating HTML by composing HTML elements in F# to generate a rich Model-View output.

It is based on [Suave's Experimental Html](https://github.com/SuaveIO/suave/blob/master/src/Experimental/Html.fs) and bears some resemblance with [Elm](http://elm-lang.org/examples).

#### Example:
Create a function that accepts a model and returns an `XmlNode`:

```fsharp
open Giraffe.XmlViewEngine

let model = { Name = "John Doe" }

let layout (content: XmlNode list) =
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

## Additional HttpHandlers

There's a few additional `HttpHandler` functions which you can get through referencing extra NuGet packages.

### Giraffe.Razor

The `Giraffe.Razor` NuGet package adds additional `HttpHandler` functions to render Razor views from Giraffe.

#### razorView

`razorView` uses the official ASP.NET Core MVC Razor view engine to compile a page and set the body of the `HttpResponse`. This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more.

The `razorView` handler requires the view name, an object model and the contentType of the response to be passed in. It also requires to be enabled through the `AddRazorEngine` function during start-up.

##### Example:
Add the razor engine service during start-up:

```fsharp
open Giraffe.Razor.Middleware

type Startup() =
    member __.ConfigureServices (services : IServiceCollection, env : IHostingEnvironment) =
        let viewsFolderPath = Path.Combine(env.ContentRootPath, "views")
        services.AddRazorEngine(viewsFolderPath) |> ignore
```

Use the razorView function:

```fsharp
open Giraffe.Razor.HttpHandlers

let model = { WelcomeText = "Hello World" }

let app =
    choose [
        // Assuming there is a view called "Index.cshtml"
        route  "/" >=> razorView "text/html" "Index" model
    ]
```

#### razorHtmlView

`razorHtmlView` is the same as `razorView` except that it automatically sets the response as `text/html`.

##### Example:
Add the razor engine service during start-up:

```fsharp
open Giraffe.Razor.Middleware

type Startup() =
    member __.ConfigureServices (services : IServiceCollection, env : IHostingEnvironment) =
        let viewsFolderPath = Path.Combine(env.ContentRootPath, "views")
        services.AddRazorEngine(viewsFolderPath) |> ignore
```

Use the razorView function:

```fsharp
open Giraffe.Razor.HttpHandlers

let model = { WelcomeText = "Hello World" }

let app =
    choose [
        // Assuming there is a view called "Index.cshtml"
        route  "/" >=> razorHtmlView "Index" model
    ]
```

### Giraffe.DotLiquid

The `Giraffe.DotLiquid` NuGet package adds additional `HttpHandler` functions to render DotLiquid templates in Giraffe.

#### dotLiquid

`dotLiquid` uses the [DotLiquid](http://dotliquidmarkup.org/) template engine to set or modify the body of the `HttpResponse`. This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more.

The `dotLiquid` handler requires the content type and the actual template to be passed in as two string values together with an object model. This handler is supposed to be used as the base handler for other http handlers which want to utilize the DotLiquid template engine (e.g. you could create an SVG handler on top of it).

##### Example:

```fsharp
open Giraffe.DotLiquid.HttpHandlers

type Person =
    {
        FirstName : string
        LastName  : string
    }

let template = "<html><head><title>DotLiquid</title></head><body><p>First name: {{ firstName }}<br />Last name: {{ lastName }}</p></body></html>"

let app =
    choose [
        route  "/foo" >=> dotLiquid "text/html" template { FirstName = "Foo"; LastName = "Bar" }
    ]
```

#### dotLiquidTemplate

`dotLiquidTemplate` uses the [DotLiquid](http://dotliquidmarkup.org/) template engine to set or modify the body of the `HttpResponse`. This http handler triggers a response to the client and other http handlers will not be able to modify the HTTP headers afterwards any more.

This http handler takes a relative path of a template file, an associated model and the contentType of the response as parameters.

##### Example:

```fsharp
open Giraffe.DotLiquid.HttpHandlers

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

#### dotLiquidHtmlView

`dotLiquidHtmlView` is the same as `dotLiquidTemplate` except that it automatically sets the response as `text/html`.

##### Example:

```fsharp
open Giraffe.DotLiquid.HttpHandlers

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

### Giraffe.TokenRouter

Including the `Giraffe.TokenRouter` module/namespace adds alternative route `HttpHandler` functions to route incoming requests through a basic [Radix Tree](https://en.wikipedia.org/wiki/Radix_tree) that is modified to handle path matching and parse values significantly faster then using basic `choose`. Each routing function is compiled into the tree so that on runtime it can quickly traverse the tree in small tokens until it matches or fails. If speed/performance on parsing & matching of routes is required it is advised you use `Giraffe.TokenRouter`

#### router

The base of all routing is a `router` function instead of `choose`. The `router` HttpHandler takes two arguments, a `HttpHandler` to run when it fails to match (typlically a `404 "Not Found"`) and the second argument is the list of routing functions.

### Example:

Defining a basic router and routes

```fsharp
let notFound = setStatusCode 404 >=> text "Not found"
let app =
    router notFound [
        route "/"       (text "index")
        route "/about"  (text "about")
    ]
```

#### routing functions

There are 3 routing functions and they work almost exactly the same as the basic ones with the exception of subRoute that has a slightly altered form.

`route` & `routef` both have two args of path & function like before but as they are node mappers, the functions need to be enclosed in parentheses `()` or use `<|` / `=>` to capture the entire function.

`subRoute` now takes a subpath argument like before (such that all child routes will presume this subpath is prepended) and as a second argument takes a list of further child routing functions

### Example:

Defining a basic router and routes

```fsharp
let notFound = setStatusCode 404 >=> text "Not found"
let app =
    router notFound [
        route "/"       (text "index")
        route "/about"  => text "about"
        routef "parsing/%s/%i" (fun (s,i) -> text (sprintf "Recieved %s & %i" s i))
        subRoute "/api" [
            route "/"       <| text "api index"
            route "/about"  (text "api about")
            subRoute "/v2" [
                route "/"       <| text "api v2 index"
                route "/about"  (text "api v2 about")
            ]
        ]
    ]
```

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
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Binds a JSON payload to a Car object
            let! car = ctx.BindJson<Car>()

            // Serializes the Car object back into JSON
            // and sends it back as the response.
            return! json car next ctx
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
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Binds an XML payload to a Car object
            let! car = ctx.BindXml<Car>()

            // Serializes the Car object back into JSON
            // and sends it back as the response.
            return! json car next ctx
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
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Binds a form urlencoded payload to a Car object
            let! car = ctx.BindForm<Car>()

            // Serializes the Car object back into JSON
            // and sends it back as the response.
            return! json car next ctx
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
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Binds a query string to a Car object
            let car = ctx.BindQueryString<Car>()

            // Serializes the Car object back into JSON
            // and sends it back as the response.
            return! json car next ctx
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
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            // Binds a JSON, XML or form urlencoded payload to a Car object
            let! car = ctx.BindModel<Car>()

            // Serializes the Car object back into JSON
            // and sends it back as the response.
            return! json car next ctx
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

Similar to building a web application in Giraffe you can also set a global error handler, which can react to any unhandled exception of your web application.

The `ErrorHandler` is a function which accepts an exception object and a default logger and returns a `HttpHandler` function which is the same as all other `HttpHandler` functions in Giraffe:

```fsharp
type ErrorHandler = exn -> ILogger -> HttpHandler
```

For example you could create an error handler which logs the unhandled exception and returns a HTTP 500 response with the error message as plain text:

```fsharp
let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse
    >=> setStatusCode 500
    >=> text ex.Message
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

It is recommended to set the error handler as the first middleware in the pipeline, so that any unhandled exception from a later middleware can be caught and processed by the error handling function.

## Installation

### Using dotnet-new

The easiest way to get started with Giraffe is by installing the [`giraffe-template`](https://www.nuget.org/packages/giraffe-template) NuGet package, which adds a new template to your `dotnet new` command:

```
dotnet new -i "giraffe-template::*"
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

### Demo apps

There are three basic sample applications in the [`/samples`](https://github.com/dustinmoris/Giraffe/tree/develop/samples) folder. The [IdentityApp](https://github.com/dustinmoris/Giraffe/tree/develop/samples/IdentityApp) demonstrates how ASP.NET Core Identity can be used with Giraffe, the [JwtApp](https://github.com/dustinmoris/Giraffe/tree/develop/samples/JwtApp) shows how to configure JWT tokens in Giraffe and the [SampleApp](https://github.com/dustinmoris/Giraffe/tree/develop/samples/SampleApp) is a generic sample application covering multiple features.

### Live apps

An example of a live website which uses Giraffe is [https://buildstats.info](https://buildstats.info). It uses the [XmlViewEngine](#renderhtml) to build dynamically rich SVG images and Docker to run the application in the Google Container Engine (see [GitHub repository](https://github.com/dustinmoris/CI-BuildStats)).

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

When making changes please use existing code as a guideline for coding style and documentation. If you intend to add or change an existing `HttpHandler` then please update the README.md file to reflect these changes there as well. If applicable unit tests must be added or updated and the project must successfully build before a pull request can be accepted.

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
- [Jimmy Byrd](https://github.com/TheAngryByrd) (Added Linux builds)
- [Jon Canning](https://github.com/JonCanning) (Moved the Razor and DotLiquid http handlers into separate NuGet packages and added the `routeBind` handler as well as some useful `HttpContext` extensions and bug fixes)
- [Andrew Grant](https://github.com/GraanJonlo) (Fixed bug in the `giraffe-template` NuGet package)
- [Gerard](https://github.com/gerardtoconnor) (Changed the API to continuations instead of binding HttpHandlers and to tasks from async)
- [Mitchell Tilbrook](https://github.com/marukami) (Helped to fix documentation)
- [Ody Mbegbu](https://github.com/odytrice) (Helped to improve the giraffe-template)
- [Reed Mullanix](https://github.com/TOTBWF) (Helped with bug fixes)
- [Lukas Nordin](https://github.com/lukethenuke) (Helped with bug fixes)
- [Banashek](https://github.com/Banashek) (Migrated Giraffe to .NET Core 2.0)
- [Yevhenii Tsalko](https://github.com/YTsalko) (Migrated sample app to .NET Core 2.0)
- [Tor Hovland](https://github.com/torhovland) (Helped with the sample applications, demonstrating CORS, JWT and configuration options)
- [dawedawe](https://github.com/dawedawe) (README fixes)

If you submit a pull request please feel free to add yourself to this list as part of the PR.

## Blog posts

- [Functional ASP.NET Core](https://dusted.codes/functional-aspnet-core)
- [Functional ASP.NET Core part 2 - Hello world from Giraffe](https://dusted.codes/functional-aspnet-core-part-2-hello-world-from-giraffe)
- [Carry On! … Continuation over binding pipelines for functional web](https://medium.com/@gerardtoconnor/carry-on-continuation-over-binding-pipelines-for-functional-web-58bd7e6ea009)

If you have blogged about Giraffe, demonstrating a useful topic or some other tips or tricks then please feel free to submit a pull request and add your article to this list as a reference for other Giraffe users. Thank you!

## License

[Apache 2.0](https://raw.githubusercontent.com/dustinmoris/Giraffe/master/LICENSE)

## Contact and Slack Channel

If you have any further questions feel free to reach out to me via any of the mentioned social media on [https://dusted.codes/about](https://dusted.codes/about) or join the `#giraffe` Slack channel in the [Functional Programming Slack Team](https://functionalprogramming.slack.com/). Please use [this link](https://fpchat-invite.herokuapp.com/) to request an invitation to the Functional Programming Slack Team.
