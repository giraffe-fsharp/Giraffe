# Giraffe Documentation

## Table of contents

- [Basics](#basics)
    - [HttpHandler](#httphandler)
    - [Giraffe pipeline vs. ASP.NET Core pipeline](#giraffe-pipeline-vs-aspnet-core-pipeline)
    - [Combinators](#combinators)
        - [compose (>=>)](#compose-)
        - [choose](#choose)
    - [Warbler](#warbler)
    - [Tasks](#tasks)
- [Configuration](#configuration)
    - [Dependency Management](#dependency-management)
    - [Environment Settings](#environment-settings)
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

## Basics

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