Release Notes
=============

## 4.0.1

Fixed dependency references for TFM `netcoreapp3.0` projects.

## 4.0.0

Giraffe 4.0.0 has been tested against `netcoreapp3.0` alongside `netcoreapp2.1` and `net461`. All sample code has been upgraded to .NET Core 3.0 as well.

#### ATTENTION:

This release of Giraffe fixes a bug in the `routef` handler which would have previously matched a route too eagerly.

##### Before:

```
Route: /foo/bar/hello/world
routef: /foo/bar/%s
Match: true
```

##### Now:

```
Route: /foo/bar/hello/world
routef: /foo/bar/%s
Match: false
```

For more information please see [issue #347](https://github.com/giraffe-fsharp/Giraffe/issues/347).

#### New features

- Support array of 'T as a child in form binding
- Added a new `DateTime` extension method `ToIsoString` which produces a RFC3339 formatted string, and corrected the docs on the existing `ToHtmlString` extension method which actually produces a RFC822 formatted string.

#### Bug fixes and breaking changes

- Fixed `routef` to not match more than one URL path segment.
- Fixed the `_ariaLabelledBy` attribute in the `GiraffeViewEngine`
- Fixed case insensitive route handlers on Ubuntu
- Changed minimum version of `Newtonsoft.Json` to `11.0.2`. This allows Giraffe to be compatible with Azure Functions.
- Renamed `tryMatchInput` to `tryMatchInputExact` and swapped the order of arguments so that the string value comes last
- Added new version of `tryMatchInput` which accepts `MatchSettings` record:

    ```fsharp
    type MatchMode =
        | Exact                // Will try to match entire string from start to end.
        | StartsWith           // Will try to match a substring. Subject string should start with test case.
        | EndsWith             // Will try to match a substring. Subject string should end with test case.
        | Contains             // Will try to match a substring. Subject string should contain test case.

    type MatchOptions = { IgnoreCase: bool; MatchMode: MatchMode; }
    ```

## 3.6.0

#### Bug fixes

- Fixed a bug in the `subRouteCi` http handler, which prevented nested sub routes to be case insensitive.

#### New features

- Added two new `HttpContext` extension methods to retrieve cookie and form values:
    - `GetCookieValue (key : string)`
    - `GetFormValue (key : string)`

## 3.5.1

#### Bug fixes

- Fixed a bug in Giraffe's model binding to not try to set read only properties anymore.

## 3.5.0

#### New features

- Updated all packages and framework library dependencies to .NET Core 2.2.
- Added a new `GET_HEAD` http handler (see: [#314](https://github.com/giraffe-fsharp/Giraffe/issues/314) for more info).
- Added a new convenience function called `handleContext`, which can be used for creating new `HttpHandler` functions.

#### Bug fixes

- Fixed the `_data` attribute in the `GiraffeViewEngine` to accept a `key` and `value` parameter now.

## 3.4.0

#### New features

- Added a new http handler called `authorizeRequest` to authorize a request based on a `HttpContext -> bool` predicate.
- Added a new http handler called `authorizeUser` which is an alias for `evaluateUserPolicy`. The `evaluateUserPolicy` handler will be removed in the next major release.

## 3.3.0

#### New features

- Added `str` as an alias for the `encodedText` function from the `GiraffeViewEngine`.
- Added the `HttpContext.GetRequestUrl()` extension method to retrieve the entire URL string of the incoming HTTP request.

## 3.2.0

#### Improvements

- Adding the `charset` parameter in the HTTP `Content-Type` response header when returning a text response (text/plain, text/html) or a JSON or XML response (application/json, application/xml). By default Giraffe is using UTF8 encoding for all its responses.

## 3.1.0

#### New features

- Added a new http handler called `validatePreconditions` to help with conditional requests:

    ```fsharp
    let someHandler (eTag : string) (content : string) =
        let eTagHeader = Some (EntityTagHeaderValue.FromString true eTag)
        validatePreconditions eTagHeader None
        >=> setBodyFromString content
    ```

- Made previously internal functionality for sub routing available through the `SubRouting` module:
    - `SubRouting.getSavedPartialPath`: Returns the currently partially resolved path.
    - `SubRouting.getNextPartOfPath`: Returns the yet unresolved part of the path.
    - `SubRouting.routeWithPartialPath`: Invokes a route handler as part of a sub route.

#### Improvements

- Performance improvements for Giraffe's default response writers.
- Performance improvements of the `htmlView` handler.
- Upgraded to the latest `TaskBuilder.fs` NuGet package which also has the SourceLink integration now.

#### Bug fixes

- Fixed the `Successful.NO_CONTENT` http handler, which threw an exception when calling from ASP.NET Core 2.1.

## 3.0.0

#### Breaking changes

- Changed the type `XmlNode` by removing the `RawText` and `EncodedText` union case and replaced both by a single `Text` union case. The HTML encoding (or not) is being done now when calling one of the two helper functions `rawText` and `encodedText`.

    - This change - even though theoretically a breaking change - should not affect the vast majority of Giraffe users unless you were constructing your own `XmlNode` elements which were of type `RawText` or `EncodedText` (which is extremely unlikely given that there's not much room for more nodes of these two types).

- Removed the `task {}` override in Giraffe which was forcing the `FSharp.Control.Tasks.V2.ContextInsensitive` version of the Task CE. This change has no effect on the behaviour of `task` computation expressions in Giraffe. In the context of an ASP.NET Core web application there is not difference between `ContextSensitive` and `ContextInsensitive` which is why the override has been removed. The only breaking change which could affect an existing Giraffe web application is that in some places you will need to explicitly `open FSharp.Control.Tasks.V2.ContextInsensitive` where before it might have been sufficient to only `open Giraffe`.

- Changed the members of the `IJsonSerializer` interface to accommodate new (de-)serialize methods for chunked encoding transfer.

    The new interface is the following:

    ```fsharp
    type IJsonSerializer =
        abstract member SerializeToString<'T>      : 'T -> string
        abstract member SerializeToBytes<'T>       : 'T -> byte array
        abstract member SerializeToStreamAsync<'T> : 'T -> Stream -> Task

        abstract member Deserialize<'T>      : string -> 'T
        abstract member Deserialize<'T>      : byte[] -> 'T
        abstract member DeserializeAsync<'T> : Stream -> Task<'T>
    ```

#### Improvements

- Significant performance improvements in the `GiraffeViewEngine` by changing the underlying composition of views from simple string concatenation to using a `StringBuilder` object.

#### New features

- Support for short GUIDs and short IDs (aka YouTube IDs) [in route arguments](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#routef) and [query string parameters](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#short-guids-and-short-ids).
- Enabled [SourceLink](https://github.com/dotnet/sourcelink/) support for Giraffe source code (thanks [Cameron Taggart](https://github.com/ctaggart))! For more information check out [Adding SourceLink to your .NET Core Library](https://carlos.mendible.com/2018/08/25/adding-sourcelink-to-your-net-core-library/).
- Added a new JSON serializer called `Utf8JsonSerializer`. This type uses the [Utf8 JSON serializer library](https://github.com/neuecc/Utf8Json/), which is currently the fastest JSON serializer for .NET. `NewtonsoftJsonSerializer` is still the default JSON serializer in Giraffe (for stability and backwards compatibility), but `Utf8JsonSerializer` can be swapped in via [ASP.NET Core's dependency injection API](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#json). The new `Utf8JsonnSerializer` is significantly faster (especially when sending chunked responses) than `NewtonsoftJsonSerializer`.
- Added a new `HttpContext` extension method for chunked JSON transfers: `WriteJsonChunkedAsync<'T> (dataObj : 'T)`. This new `HttpContext` method can write content directly to the HTTP response stream without buffering into a byte array first (see [Writing JSON](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#writing-json)).
- Added a new `jsonChunked` http handler. This handler is the equivalent http handler version of the `WriteJsonChunkedAsync` extension method.
- Added first class support for [ASP.NET Core's response caching](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#response-caching) feature.

#### Special thanks

Special thanks to [Dmitry Kushnir](https://github.com/dv00d00) for doing the bulk work of all the perf improvements in this release as well as adding Giraffe to the [TechEmpower Webframework Benchmarks](https://techempower.com/benchmarks/)!

## 2.0.1

Changed the `task {}` CE to load from `FSharp.Control.Tasks.V2.ContextInsensitive` instead of `FSharp.Control.Tasks.ContextInsensitive`.

## 2.0.0

#### Breaking changes

- Changed the name of the handler `requiresAuthPolicy` to `evaluateUserPolicy` in order to better describe its functionality and to avoid a name clash between two newly added handlers for validating ASP.NET Core's `AuthorizationPolicy` objects (see new features).
- Changed how he `AddGiraffe()` extension method registers Giraffe dependencies in ASP.NET Core. It now follows the `TryAdd` pattern which will only register a dependency if it hasn't been registered beforehand.
- Changed the `HttpContext.GetService<'T>()` extension method to throw a `MissingDependencyException` if it cannot resolve a desired dependency.

#### New features

- Added two new http handlers to validate an ASP.NET Core `AuthorizationPolicy` (see: [Policy based authorization](https://docs.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-2.1)). The `authorizeByPolicyName` and `authorizeByPolicy` http handlers will use ASP.NET Core's authorization service to validate a user against a given policy.
- Updated `TaskBuilder.fs` to version `2.0.*`.
- Updated ASP.NET Core NuGet packages to latest `2.1.*` versions.
- Enabled `return!` for `opt { }` computation expressions.
- Added `blockquote`, `_integrity` and `_scoped` to the `GiraffeViewEngine`.
- Added attributes for mouse, keyboard, touch, drag & drop, focus, input and mouse wheel events to the `GiraffeViewEngine`.
- Added new accessibility attributes to the `GiraffeViewEngine`. These can be used after opening the `Giraffe.GiraffeViewEngine.Accessibility` module.
- Added a new `Successful.NO_CONTENT` http handler which can be used to return a HTTP 204 response.
- Added more structured logging around the Giraffe middleware.

#### Bug fixes

- Fixed a bug in `routef`, `routeCif` and `subRoutef` which prohibited to parse multiple GUIDs
- Fixed a bug in `routef`, `routeCif` and `subRoutef` which wrongly decoded a route argument twice (and therefore turned `+` signs into spaces).
- Fixed XML documentation for all Giraffe functions which should make function tooltips nicely formatted again.
- Enabled the `HttpContext.BindModelAsync<'T>()` extension method and the `bindModel<'T>` http handler to also bind to a model in the case of a `PATCH` or `DELETE` http request.

## 1.1.0

#### New features

- Added `subRoutef` http handler (see [subRoutef](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#subroutef))
- Added `routex` and `routeCix` http handler (see [routex](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#routex))
- Improved model binding (see [Model Binding](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#model-binding))
    - Fixed issues: [#121](https://github.com/giraffe-fsharp/Giraffe/issues/121), [#206](https://github.com/giraffe-fsharp/Giraffe/issues/206)
    - Added a `TryBindFormAsync` and a `TryBindQueryString` `HttpContext` extension methods
    - Added new `HttpHandler` functions to offer a more functional API for model binding:
        - `bindJson<'T>`
        - `bindXml<'T>`
        - `bindForm<'T>`
        - `tryBindForm<'T>`
        - `bindQuery<'T>`
        - `tryBindQuery<'T>`
        - `bindModel<'T>`
- Added new [Model Validation](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#model-validation) API

To see an example of the new features you can check the official [Giraffe 1.1.0 release blog post](https://dusted.codes/giraffe-110-more-routing-handlers-better-model-binding-and-brand-new-model-validation-api).

#### Bug fixes

- `routeBind` works when nested in a `subRoute` handler now
- `routeBind` doesn't crate a model object any more if the route arguments do not match the provided model

## 1.0.0

First RTM release of Giraffe.

This release has many minor breaking changes and a few bigger features. Please read the changelog carefully before updating your existing application.

#### New features

- JSON and XML serialization is now configurable through Dependency Injection (see [Serialization](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#serialization))
- Added new features to validate conditional HTTP headers before processing a web request (see [Conditional Requests](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#conditional-requests))
- Added streaming capabilities (see [Streaming](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#streaming))
- Added `HEAD`, `OPTIONS`, `TRACE`, `CONNECT` http handlers
- Added more `HttpContext` extension methods to create parity between response writing methods and `HttpHandler` functions (see [Response Writing](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#response-writing) and [Content Negotiation](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#content-negotiation))
- Added detailed XML docs to all public facing functions for better Intellisense support
- The `Giraffe.Common` module auto opens now

#### Breaking changes

- Deprecated `Griaffe.Tasks`. Giraffe uses the original [TaskBuilder.fs](https://github.com/rspeele/TaskBuilder.fs) library now.
- Giraffe comes with a default set of required dependencies which need to be registered via `services.AddGiraffe()` during application startup now
- The `Giraffe.TokenRouter` library has been moved to a separate NuGet package under the same name
- Removed redundant serialization methods
    - Removed `serializeJson`, `deserializeJson<'T>`, `deserializeJsonFromStream<'T>`, `defaultJsonSerializerSettings`, `defaultSerializeJson`, `defaultDeserializeJson<'T>`, `serializeXml` and `deserializeXml<'T>`
- Removed the `customJson` http handler
- Renamed the `html` http handler to `htmlString`
- Renamed the `renderHtml` http handler to `htmlView`
- Renamed `setBodyAsString` http handler to `setBodyFromString`
- Renamed `ReturnHtmlFileAsync()` to `WriteHtmlFileAsync()`
    - The function can also accept relative and absolute file paths now
- Renamed `RenderHtmlAsync()` to `WriteHtmlViewAsync()`
- Removed the overloads for `BindJsonAsync<'T>`, `BindModelAsync<'T>` and `WriteJsonAsync` which accepted an object of type `JsonSerializerSettings`
- Renamed the `signOff` http handler to `signOut` to be more consistent with existing ASP.NET Core naming conventions

To get a summary of the new features and changes you can check the official [Giraffe 1.0.0 release blog post](https://dusted.codes/announcing-giraffe-100).

## 0.1.0-beta-700

#### Breaking changes

- Renamed `portRoute` to `routePorts` to be more consistent with other routing functions (`route`, `routef`, `routeStartsWith`, etc.)

#### New features

- `routef` and `routeCif` both support `%O` for matching `System.Guid` values now
- Added HTML attributes helper functions to the `GiraffeViewEngine`.

Example:

```fsharp
let html = p [ _class "someCssClass"; _id "greetingsText" ] [ encodedText "Hello World" ]
```

## 0.1.0-beta-600

#### Breaking changes

- Renamed `Giraffe.XmlViewEngine` to `Giraffe.GiraffeViewEngine` as it represented more than just an XML view engine.

#### New features

- Added automatic validation of the format string inside `routef` and `routeCif` to notify users of the notorious `%d` vs `%i` error during startup.

## 0.1.0-beta-511

#### Bug fixes

- Fixed `ReadBodyFromRequestAsync` where the stream has been disposed before read could complete.

## 0.1.0-beta-510

#### Improvements

- Explicitly set the encoding to UTF-8 when reading the HTTP body during `ReadBodyFromRequestAsync`

#### New features

- Added the `html` http handler which can be used to return a `text/html` response by passing in the html content as a string variable

## 0.1.0-beta-500

#### New features

- Added a new overload for `GetLogger` of the `HttpContext` extension methods, which allows one to pass in a `categoryName` string in order to initialise a new logger: `let logger = ctx.GetLogger "categoryName"`.
- `BindFormAsync`, `BindQueryString` and `BindModelAsync` accept an additional optional parameter for `CultureInfo`.

#### Breaking changes

- Removed `Giraffe.Tasks` from the `Giraffe` NuGet package and added a new dependency on the newly created `Giraffe.Tasks` NuGet package. You can use the `Giraffe.Tasks` NuGet package from non ASP.NET Core projects now as well.

## 0.1.0-beta-400

#### New features

- Added [HTTP status code helper functions](https://github.com/dustinmoris/Giraffe#statuscode-httphandlers).
- Added `defaultSerializeJson` and `defaultDeserializeJson` methods.
- Auto opened default Giraffe modules so that the core functionality can be entirely consumed through a single `open Giraffe` statement.
- The functionality from `Giraffe.Razor.Middleware` and `Giraffe.Razor.HttpHandlers` can be both consumed through a single `open Giraffe.Razor` now.

#### Bug fixes

- Changed the `base` tag from the `XmlViewEngine` from a regular `tag` to a `voidTag` to comply with the HTML spec.

#### Breaking changes

- Renamed all async methods by appending `Async` at the end of the method in order to comply with the general .NET naming convention
    - `readFileAsString` --> `readFileAsStringAsync`
    - `ctx.ReadBodyFromRequest` --> `ctx.ReadBodyFromRequestAsync`
    - `ctx.BindJson` --> `ctx.BindJsonAsync`
    - `ctx.BindXml` --> `ctx.BindXmlAsync`
    - `ctx.BindForm` --> `ctx.BindFormAsync`
    - `ctx.BindModel` --> `ctx.BindModelAsync`
    - `ctx.WriteJson` --> `ctx.WriteJsonAsync`
    - `ctx.WriteXml` --> `ctx.WriteXmlAsync`
    - `ctx.WriteText` --> `ctx.WriteTextAsync`
    - `ctx.RenderHtml` --> `ctx.RenderHtmlAsync`
    - `ctx.ReturnHtmlFile` --> `ctx.ReturnHtmlFileAsync`
- Renamed `Giraffe.DotLiquid.HttpHandlers` module to `Giraffe.DotLiquid`

## 0.1.0-beta-310

#### New features
- Added `portRoute` http handler to filter an incoming request based on the port

#### Breaking changes
- The `GET`, `POST`, `PUT` and `DELETE` http handlers of the `TokenRouter.fs` have changed to accept a list of http handlers now.

#### Bug fixes
- TokenRouter fringe case not being identified before (see #150)

## 0.1.0-beta-300

#### New features

- Added `requiresAuthPolicy` http handler
- Added `RenderHtml` and `ReturnHtmlFile` extension methods to the `HttpContext` object
- Added `customJson` http handler, which allows users to define a custom json handler (with custom serialization settings)
- Added overloads to `BindJson` and `BindModel` where a user can pass in a custom `JsonSerializerSettings` object

#### Breaking changes

- Changed the default json serializer to use camel case for serialization (this change prevents users from being able to change the default serializer through the `JsonConvert.DefaultSettings` object - use `customJson` instead if customization is required)
- Changed the `serializeJson`, `deserializeJson` methods to accept an aditional parameter of type `JsonSerializerSettings`

#### Bug fixes and improvements

- Automatically URL decoding of string values when using `routef`
- Fixed an inference bug with `routef` by replacing the `format` parameter of the `tryMatchInput` method and the `path` parameter of the `routef` and `routeCif` methods from `StringFormat` to `PrintFormat`
- Changed the implementation of `ctx.BindJson<'T>()` for better performance and which aims to fix an Azure bug with Kestrel (#136)
- Fixed a bug with `routeBind` (#129)
- Improved the `htmlFile` http handler by allowing the `filePath` parameter to be either rooted or relative to the `ContentRootPath`



## 0.1.0-beta-200

- Added three additional `HttpContext` extension methods in the `Giraffe.HttpContextExtensions` module: `WriteJson`, `WriteXml` and `WriteText`. These three methods can be used for direct HttpReponse writing from within a custom handler without having to sub-call the `json`, `xml` or `text` http handlers.
- Changed the `UseGiraffeErrorHandler` method to return an `IApplicationBuilder` now. This change allows [middleware chaining](https://github.com/dustinmoris/Giraffe/pull/118). This is a breaking change and you'll either have to chain middleware or append an `|> ignore` in your application set up.

## 0.1.0-beta-110

Added the `Giraffe.TokenRouter` module for [speed improved route handling](https://github.com/dustinmoris/Giraffe/issues/56).

## 0.1.0-beta-102

Improved the `routeBind` http handler to give users more flexibility in mapping routes to HTTP requests (see #110).

## 0.1.0-beta-101

- Fixed bug in connection with the `ExceptionHandlerMiddleware` (see #106)
- Added CORS settings for localhost to the default giraffe-template

## 0.1.0-beta-100

#### Updated Giraffe to .NET Standard 2.0

Attention, this release updated all Giraffe NuGet packages to `net461` and `netstandard2.0`!

You will have to upgrade your ASP.NET Core application to either full .NET framework 461 or to a .NET Core app 2.0.

There were a few minor breaking changes in ASP.NET Core 2.0 which also affected Giraffe. I do not intend to keep maintaining a 1.x version any longer unless there's a very compelling reason. General advice is to upgrade all .NET Core web applications to 2.0.

## 0.1.0-beta-003

#### Giraffe `0.1.0-beta-003`
- Fixed bug where `readFileAsString` closed the stream before the file could be read

#### Giraffe.Razor `0.1.0-beta-002`
- Fixed bug so that `_ViewStart.cshtml` files get respected now

#### giraffe-template `0.1.9`
- Fixed wrong version numbers in package references for `Giraffe` and `Giraffe.Razor`

## 0.1.0-beta-002

- Fixed the `warbler` function. Should work as expected again.
- Added support for `Async<'T>` in the `task {}` workflow. You can use an `Async<'T>` from within `task {}` without having to convert back to a `Task<'T>`
- Set the Giraffe dependency version in the template to a concrete version to avoid breaking changes in the template.

## 0.1.0-beta-001

First Beta release of Giraffe!

#### Major changes:
- [Continuations over binding](https://github.com/dustinmoris/Giraffe/pull/71)
- [Tasks instead of Async](https://github.com/dustinmoris/Giraffe/pull/75)

The `HttpHandler` has slightly [changed](https://github.com/dustinmoris/Giraffe#httphandler).

Blog post with more info is coming shortly!

## 0.1.0-alpha025

Changed the type `XmlAttribute` from the `XmlViewEngine` to accept either a `string * string` key value pair or a boolean attribute of type `string`. This was a missing to enable script tags such as `<script src="..." async></script>`.

Added two helper functions (`attr` and `flag`) to simplify the creation of those attributes:

```fsharp
script [
    attr "src" "http://example.org/example.js"
    attr "lang" "javascript"
    flag "async" ] []
```

## 0.1.0-alpha024

- New `routeBind` http handler
- Annotated all default http handler functions with the `HttpHandler` type

## 0.1.0-alpha023

#### Bug fixes:

- Fixed build error in the Giraffe template.

#### Further improvements to the `XmlViewEngine`:

- Renamed `renderXmlString` to `renderXmlNode` and renamed `renderHtmlString` to `renderHtmlNode`
- Added two more methods which accept a `XmlNode list`: `renderXmlNodes` and `renderHtmlNodes`
- Changed the return value of `encodedText` and `rawText` to return a single `XmlNode` instead of `XmlNode list`. This has the advantage that it can be used from within another list, which was not possible before.

Before:

```
let view =
    html [] [
        head [] [
            title []  (rawText "Giraffe")
        ]
        body [] (encodedText "Hello World")
    ]
```

Now:

```
let view =
    html [] [
        head [] [
            title []  [ rawText "Giraffe" ]
        ]
        body [] [ encodedText "Hello World" ]
    ]
```

This has the advantage that you can also do this, which wasn't possible before:

```
let view =
    html [] [
        head [] [
            title []  [ rawText "Giraffe" ]
        ]
        body [] [
            encodedText "Hello World"
            p [] [ rawText "Hello" ]
        ]
    ]
```

## 0.1.0-alpha022

A few modifications to the former `HtmlEngine` so that it can be used for correct XML rendering as well:

- Renamed the `Giraffe.HtmlEngine` module to `Giraffe.XmlViewEngine`
- Renamed `HtmlAttribute` to `XmlAttribute`, `HtmlElement` to `XmlElement` and `HtmlNode` to `XmlNode`
- Renamed and make the function `nodeToHtmlString` private
- Added `comment` function to the `Giraffe.XmlViewEngine` module for creating XML comments
- Added `renderXmlString` and `renderHtmlString` functions to `Giraffe.XmlViewEngine` module for rendering XML and HTML nodes.

## 0.1.0-alpha021

- Changed `HttpContext.BindQueryString<'T>()` to return `'T` instead of `Async<'T>`
- Added `HttpContext.TryGetQueryStringValue (key : string)` which returns an `Option<string>`
- Added `HttpContext.GetQueryStringValue (key : string)` which returns a `Result<string, string>`

## 0.1.0-alpha020

Split out the Razor view engine and the DotLiquid templating engine into separate NuGet packages:
- `Giraffe.Razor`
- `Giraffe.DotLiquid`

Please reference the additional packages if you were using any of the view or templating handlers.

Also updated the `giraffe-template` NuGet package with the new changes and adapted the `build.ps1` PowerShell script to successfully build on Linux environments too.

Additionally TravisCI builds are run as part of every commit as well now.

## 0.1.0-alpha019

Adds [support for the `Option<'T>` type when model binding from a query string](https://github.com/dustinmoris/Giraffe/issues/51).

## 0.1.0-alpha018

- Added two new `HttpContext` extension methods:
    - `TryGetRequestHeader (key : string)` which returns an `Option<string>`
    - `GetRequestHeader (key : string)` which returns a `Result<string, string>`
- Added default computation expressions for the `Option<'T>` and `Result<'T, 'TError>` types under `Giraffe.ComputationExpressions`

## 0.1.0-alpha017

#### New features
- Added `plain/text` as a new supported mime type to the default `negotiate` handler (it will be using an object's `.ToString()` method to serialize an object into plain text)
- Added new helper functions for retrieving a logger or dependencies as extension methods of the `HttpContext` object: `ctx.GetService<'T>()` and `ctx.GetLogger<'T>()`

#### Breaking changes
- Completely removed the `HttpHandlerContext` type and replaced all usage with the original `HttpContext` object from ASP.NET Core.
- Extended the `ErrorHandler` function with a parameter to retrieve a default `ILogger` object
- Moved model binding functions from the `Giraffe.ModelBinding` module into the `Giraffe.HttpContextExtensions` module and made them extension methods of the `HttpContext` object

Also updated the `giraffe-template` NuGet package with the latest changes.

## 0.1.0-alpha016

Fixes #46

## 0.1.0-alpha015

Changed the signature of the `redirectTo` http handler (swapped `permanent` with `location`).

## 0.1.0-alpha014

Added `redirectTo` http handler.

## 0.1.0-alpha013

Using culture invariant converters in model binders.

## 0.1.0-alpha012

- Added `bindQueryString` which can automatically bind a model from query string parameters
- Extended `bindModel` to include `bindQueryString` when the HTTP method is not `POST` or `PUT`

## 0.1.0-alpha011

#### New features
- Added a `warbler` function
- Added model binding capabilities which can automatically bind a HTTP payload to a strongly typed model: `bindJson`, `bindXml`, `bindForm` and `bindModel`
#### Improvements
- Improved the `negotiateWith` and `negotiate` http handlers by making use of ASP.NET Core's `MediaTypeHeaderValue` class
- Added `*.cshtml` files to the DotNet watcher in the template
#### Bug fixes
- Fixed `AssemblyName` and `PackageId` values in the template

## 0.1.0-alpha010

Added two new `HttpHandler` functions:
- `negotiate` checks the `Accept` header of a request and determines automatically if a response should be sent in JSON or XML
- `negotiateWith` is the same as `negotiate`, but additionally accepts an `IDictionary<string, obj -> HttpHandler>` which allows users to extend the default negotiation rules (e.g. change default serialization if a client is indifferent, or add more supported mime types, etc.)

## 0.1.0-alpha009

- Added a new programmatic view engine called `Giraffe.HtmlEngine`
- Addd a new `HttpHandler` named `renderHtml` to return views from the new view engine

## 0.1.0-alpha008

- Updated `Newtonsoft.Json` to version `10.0.*`
- Updated the Giraffe `dotnet new` template

## 0.1.0-alpha007

- NuGet package is being built with official VS 2017 build image by AppVeyor (using .NET Core SDK 1.0.1)
- Created a NuGet package called `giraffe-template` which is a new `dotnet new` template for Giraffe projects
- Removed `RazorLight` as a dependency and replaced it with the official razor engine by ASP.NET Core MVC
- Renamed the HttpHandler `htmlTemplate` to `dotLiquidHtmlView`
- Added a new HttpHandler named `dotLiquidTemplate`. The difference is that it let's the caller decide what `Content-Type` the response shall be.
- Renamed the HttpHandler `razorView` to `razorHtmlView`
- Added a new HttpHandler named `razorView`. The difference is that it let's the caller decide what `Content-Type` the response shall be.

## 0.1.0-alpha006

Attention, this release creates a new NuGet package named **Giraffe.nupkg**, which will be the new NuGet library for this project going forward.

The old package **AspNetCore.Lambda.nupkg** will remain as is for backwards compatibility and not be removed or updated anymore.

- Added a default logger to the `HttpHandlerContext`
- Renamed NuGet package to Giraffe

## 0.1.0-alpha005

- Changed dependency from `Microsoft.AspNetCore.Hosting` to `Microsoft.AspNetCore.Hosting.Abstractions`
- Added `razorView` HttpHandler

## 0.1.0-alpha004

**This version has some breaking changes**
- Re-factored `bind` to make it a true bind function
- Added a new `compose` combinator
- The `>>=` operator became `>=>` now and `>>=` is the new `bind` function (Fixes #5)
- Upgraded project to .NET Core SDK RC4

## 0.1.0-alpha003

- Upgraded to FSharp.Core 4.1.0
- Added `subRoute` and `subRouteCi` handlers (Fixes #7 )
- Uses culture invariant parse functions for `routef` and `routeCif` (See #8)

## 0.1.0-alpha002

- Changed the `HttpHandlerContext` to include an `IServiceProvider` and removed `IHostingEnvironment` and `ILoggerFactory` instead
- Added more default HttpHandlers: `challenge`, `signOff`, `requiresAuthentication`, `requiresRole`, `requiresRoleOf`, `clearResponse` and `xml`
- Added XML documentation to all default HttpHandlers
- Updated to latest Microsoft.FSharp.Core.netcore NuGet package, which is in RC now
- Changed the name of the `ErrorHandlerMiddleware` to `LambdaErrorHandlerMiddleware` and changed the `IApplicationBuilder` extension method to `UseLambdaErrorHandler`

## 0.1.0-alpha001

First alpha release with a basic set of functionality.
