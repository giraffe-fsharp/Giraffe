# ASP.NET Core Lambda

A functional ASP.NET Core micro framework for building rich web applications.

## Table of contents

- About
- Basics
    - HttpHandler
    - Combinators
- Default HttpHandlers
    - choose
    - GET, POST, PUT, PATCH, DELETE
    - mustAccept
    - route
    - routef
    - routeci
    - routecif
    - setStatusCode
    - setHttpHeader
    - setBody
    - setBodyAsString
    - text
    - json
    - dotLiquid
    - htmlTemplate
    - htmlFile
- Custom HttpHandlers
- Installation
- Examples
- License
- Contribution

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

A `HttpHandler` is a simple function which takes in a tuple of `IHostingEnvironment` and `HttpContext` and returns a tuple of the same type in the end.

Inside that function it can process an incoming `HttpRequest` and make changes to the `HttpResponse` of the given `HttpContext`. By receiving and returning a `HttpContext` there's nothing which cannot be done from inside such an `HttpHandler`.

A `HttpHandler` can decide to not further process an incoming request and return `None` instead. In this case another `HttpHandler` might continue processing the request or the middleware will simply defer to the next `RequestDelegate` in the ASP.NET Core pipeline.

### Combinators

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

The `bind` function takes in two different http handlers and a `WebContext` (wctx) object. It first invokes the first handler and checks its result. If it returned a new `WebContext` then it will pass it on to the second `HttpHandler` and return the overall result of that function. If there was nothing then it will short circuit after the first handler and return `None`.

With this in mind one can compose many smaller `HttpHandler` functions into a bigger web application.

## Default HttpHandlers

### choose

`choose` iterates through a list of `HttpHandler` and invokes them until the first handler returns a result.

#### Example:

```
let app = 
    choose [
        route "/foo" >>= text "Foo"
        route "/bar" >>= text "Bar"
    ]
```

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

`routef` matches a given format string with the actual request path and invokes a given `HttpHandler` with automatically resolved arguments on success.

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

### setHttpHeader

### setBody

### setBodyAsString

### text

### json

### dotLiquid

### htmlTemplate

### htmlFile