Release Notes
=============

## 0.1.0-beta 700

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