# ASP.NET Core Lambda

A functional ASP.NET Core micro framework for building rich web applications.

THIS PROJECT IS STILL WORK IN PROGRESS

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

ASP.NET Core Lambda is an F# web framework similar to Suave, which can be plugged into the ASP.NET Core pipeline via middleware. ASP.NET Core Lambda has been heavily inspired by Suave and its concept of web parts and the ability to compose many smaller web parts into a large web application.

ASP.NET Core Lambda is intended for developers who want to build web applications on top of ASP.NET Core in a functional first approach. ASP.NET Core is a powerfull web platform with a huge community behind it and ASP.NET Core Lambda is designed for F# developers who want to benefit from that eco system.

## Basics

### HttpHandler

The only building block in ASP.NET Core Lambda is a so called `HttpHandler`:

```
type WebContext  = IHostingEnvironment * HttpContext

type HttpHandler = WebContext -> Async<WebContext option>
```

A `HttpHandler` is a simple function which takes in a tuple of `IHostingEnvironment` and `HttpContext` and returns a tuple of the same type when finished.

Inside that function it can process an incoming `HttpRequest` and make changes to the `HttpResponse` of the given `HttpContext`. By receiving and returning a `HttpContext` there's nothing which cannot be done from inside a `HttpHandler`.

A `HttpHandler` can decide to not further process an incoming request and return `None` instead. In this case another `HttpHandler` might continue processing the request or the middleware will simply defer to the next `RequestDelegate` in the ASP.NET Core pipeline.

### Combinators

#### bind (>>=)

The core combinator is the `bind` function which you might be familiar with:

```
let bind (handler : HttpHandler) (handler2 : HttpHandler) =
    fun wctx ->
        async {
            let! result = handler wctx
            match result with
            | None       -> return  None
            | Some wctx2 -> return! handler2 wctx2
        }

let (>>=) = bind
```

The `bind` function takes in two different http handlers and a `WebContext` (wctx) object. It first invokes the first handler and checks its result. If there was a `WebContext` then it will pass it on to the second `HttpHandler` and return the overall result of that function. If there was nothing then it will short circuit after the first handler and return `None`.

This allows composing many smaller `HttpHandler` functions into a bigger web application.

#### choose

The `choose` combinator function iterates through a list of `HttpHandler`s and invokes each individual until the first handler returns a result.

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

Defining a new `HttpHandler` is fairly easy. All you need to do is to create a new function which matches the signature of `WebContext -> Async<WebContext option>`. Through currying your custom `HttpHandler` can extend the original signature as long as the partial application of your function will still return a function of `WebContext -> Async<WebContext option>`.

### Example:

```
// Defining a custom HTTP handler to partially filter a route

let routeStartsWith (partOfPath : string) =
    fun (env : IHostingEnvironment, ctx : HttpContext) ->
        if ctx.Request.Path.ToString().StartsWith partOfPath 
        then Some (env, ctx)
        else None
        |> async.Return

// Defining another custom HTTP handler to validate a mandatory HTTP header
// (This is only an extremly simmplified example of showing how to add custom authentication handlers)

let requiresToken (expectedToken : string) (handler : HttpHandler) =
    fun (env : IHostingEnvironment, ctx : HttpContext) ->
        let token    = ctx.Request.Headers.["X-Token"].ToString()
        let response =
            if token.Equals(expectedToken)
            then handler
            else setStatusCode 401 >>= text "Token wrong or missing"
        response (env, ctx)

// Composing a web application from smaller HTTP handlers

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

## License

[Apache 2.0](https://raw.githubusercontent.com/dustinmoris/AspNetCore.Lambda/master/LICENSE)

## Contribution

Feedback is more than welcome and pull requests get accepted!

File an [issue on GitHub](https://github.com/dustinmoris/AspNetCore.Lambda/issues/new) or contact me via [https://dusted.codes/about](https://dusted.codes/about).