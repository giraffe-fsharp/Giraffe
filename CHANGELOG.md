# CHANGELOG

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [8.2.0] - 2025-11-12

### Added

- [Add issue templates](https://github.com/giraffe-fsharp/Giraffe/pull/671) - Credits @64J0
- [Add GitHub dependabot configuration](https://github.com/giraffe-fsharp/Giraffe/pull/621) - Credits @64J0
- [Add global rate limiting sample](https://github.com/giraffe-fsharp/Giraffe/pull/622) - Credits @64J0
- [Add OpenApi section to the documentation](https://github.com/giraffe-fsharp/Giraffe/pull/624) - Credits @64J0
- [Add AssemblyVersion attribute](https://github.com/giraffe-fsharp/Giraffe/pull/629) - Credits @64J0
- [Add more links](https://github.com/giraffe-fsharp/Giraffe/pull/633) - Credits @64J0
- [Add .NET 9 as target framework, fine-tune dependabot, update CI and clean tests removing .NET 6/7 from target frameworks](https://github.com/giraffe-fsharp/Giraffe/pull/639) - Credits @64J0
- [[Alpha] Add Endpoint routing functions ...WithExtensions](https://github.com/giraffe-fsharp/Giraffe/pull/634) - Credits @64J0

### Changed

- [Could we avoid allocation of UTF8 byte array?](https://github.com/giraffe-fsharp/Giraffe/pull/692) - Credits @Thorium
- [Update fsharp-analyzers and the analyzer packages](https://github.com/giraffe-fsharp/Giraffe/pull/662) - Credits @Numpsy
- [Improve JSON docs](https://github.com/giraffe-fsharp/Giraffe/pull/665) - Credits @64J0
- [Enhance routef support for named parameters and improve documentation](https://github.com/giraffe-fsharp/Giraffe/pull/656) - Credits @RJSonnenberg
- [Fix assembly version](https://github.com/giraffe-fsharp/Giraffe/pull/655)

### Removed

- [Remove [\<AllowNullLiteral\>] attribute from Json.ISerializer and Xml.ISerializer](https://github.com/giraffe-fsharp/Giraffe/pull/685) - Credits @64J0
- [Remove Obsolete from redirectTo](https://github.com/giraffe-fsharp/Giraffe/pull/695) - Credits @kerams

### Security

- [Some security fixes for Giraffe](https://github.com/giraffe-fsharp/Giraffe/pull/691) - Credits @
- [Code scanning fix patches](https://github.com/giraffe-fsharp/Giraffe/pull/638) - Credits @64J0

## [8.2.0-alpha-002] - 2025-11-11

### Removed

- [Remove Obsolete from redirectTo](https://github.com/giraffe-fsharp/Giraffe/pull/695) - Credits @kerams

## [8.2.0-alpha-001] - 2025-11-10

### Changed

- [Could we avoid allocation of UTF8 byte array?](https://github.com/giraffe-fsharp/Giraffe/pull/692) - Credits @Thorium

### Security

- [Some security fixes for Giraffe](https://github.com/giraffe-fsharp/Giraffe/pull/691) - Credits @Thorium

## [8.1.0-alpha-001] - 2025-10-25

### Added

- [Add issue templates](https://github.com/giraffe-fsharp/Giraffe/pull/671) - Credits @64J0

### Changed

- [Update fsharp-analyzers and the analyzer packages](https://github.com/giraffe-fsharp/Giraffe/pull/662) - Credits @Numpsy
- [Improve JSON docs](https://github.com/giraffe-fsharp/Giraffe/pull/665) - Credits @64J0

### Removed

- [Remove [\<AllowNullLiteral\>] attribute from Json.ISerializer and Xml.ISerializer](https://github.com/giraffe-fsharp/Giraffe/pull/685) - Credits @64J0

## [8.0.0-alpha-003] - 2025-06-09

### Changed

- [Enhance routef support for named parameters and improve documentation](https://github.com/giraffe-fsharp/Giraffe/pull/656) - Credits @RJSonnenberg

## [8.0.0-alpha-002] - 2025-04-14

### Changed

- [Fix assembly version](https://github.com/giraffe-fsharp/Giraffe/pull/655)

## [8.0.0-alpha-001] - 2025-02-11

### Added

- [Add GitHub dependabot configuration](https://github.com/giraffe-fsharp/Giraffe/pull/621) - Credits @64J0
- [Add global rate limiting sample](https://github.com/giraffe-fsharp/Giraffe/pull/622) - Credits @64J0
- [Add OpenApi section to the documentation](https://github.com/giraffe-fsharp/Giraffe/pull/624) - Credits @64J0
- [Add AssemblyVersion attribute](https://github.com/giraffe-fsharp/Giraffe/pull/629) - Credits @64J0
- [Add more links](https://github.com/giraffe-fsharp/Giraffe/pull/633) - Credits @64J0
- [Add .NET 9 as target framework, fine-tune dependabot, update CI and clean tests removing .NET 6/7 from target frameworks](https://github.com/giraffe-fsharp/Giraffe/pull/639) - Credits @64J0
- [[Alpha] Add Endpoint routing functions ...WithExtensions](https://github.com/giraffe-fsharp/Giraffe/pull/634) - Credits @64J0

### Security

- [Code scanning fix patches](https://github.com/giraffe-fsharp/Giraffe/pull/638) - Credits @64J0

## [7.0.2] - 2024-10-16

### Changed

- Combination of the tags:
  - [7.0.2-alpha-001](https://github.com/giraffe-fsharp/Giraffe/releases/tag/v7.0.2-alpha-001)
  - [7.0.2-alpha-002](https://github.com/giraffe-fsharp/Giraffe/releases/tag/v7.0.2-alpha-002)

## [7.0.2-alpha-002] - 2024-09-20

### Added

- [Add maintainers info](https://github.com/giraffe-fsharp/Giraffe/pull/616) - Credits @nojaf

### Fixed

- [Fix ReadBodyFromRequestAsync disposing ctx.Request.Body](https://github.com/giraffe-fsharp/Giraffe/pull/615) - Credits @64J0

## [7.0.2-alpha-001] - 2024-09-06

### Added

- [Moar fantomas](https://github.com/giraffe-fsharp/Giraffe/pull/614) - Credits @nojaf
- [HandleOptionGracefullyAnalyzer for ETag and Last-Modified at Preconditional.fs](https://github.com/giraffe-fsharp/Giraffe/pull/613) - Credits @64J0
- [feat: add request limits on accept, content-type, and content-length headers](https://github.com/giraffe-fsharp/Giraffe/pull/502) - Credits @stijnmoreels

## [7.0.1] - 2024-08-27

### Added

- [Add F# Analyzers](https://github.com/giraffe-fsharp/Giraffe/pull/603) - Credits @1eyewonder
- [Add F# compatible json serializer](https://github.com/giraffe-fsharp/Giraffe/pull/609) - Credits @fpellet

### Changed

- [Update .vscode samples debug configuration](https://github.com/giraffe-fsharp/Giraffe/pull/610) - Credits @64J0

## [7.0.0] - 2024-07-15

### Changed

- Combination of the tags:
  - [6.4.1-alpha-1](https://github.com/giraffe-fsharp/Giraffe/releases/tag/v6.4.1-alpha-1)
  - [6.4.1-alpha-2](https://github.com/giraffe-fsharp/Giraffe/releases/tag/v6.4.1-alpha-2)
  - [6.4.1-alpha-3](https://github.com/giraffe-fsharp/Giraffe/releases/tag/v6.4.1-alpha-3)
  - [7.0.0-alpha-001](https://github.com/giraffe-fsharp/Giraffe/releases/tag/v7.0.0-alpha-001)

## [7.0.0-alpha-001] - 2024-07-08

### Added

- [Add ability to Configure Endpoints via IEndpointConventionBuilder](https://github.com/giraffe-fsharp/Giraffe/pull/599) - Credits @mrtz-j

### Changed

- [Promote System.Text.Json as default JSON serializer](https://github.com/giraffe-fsharp/Giraffe/pull/563) - Credits @esbenbjerre
- [Improve DEVGUIDE with SemVer pre-release observation](https://github.com/giraffe-fsharp/Giraffe/pull/597) - Credits @64J0

## [6.4.1-alpha-3] - 2024-05-14

### Fixed

- [Fix pre-release/release workflows](https://github.com/giraffe-fsharp/Giraffe/pull/596) - Credits @64J0

## [6.4.1-alpha-2] - 2024-05-12

### Fixed

- [Hotfix pre-release workflow](https://github.com/giraffe-fsharp/Giraffe/pull/595) - Credits @64J0

## [6.4.1-alpha-1] - 2024-05-12

### Changed

- [Update README.md giraffe-template installation command](https://github.com/giraffe-fsharp/Giraffe/pull/591) - Credits @dbrattli
- [Change dev instructions and CI](https://github.com/giraffe-fsharp/Giraffe/pull/593) - Credits @64J0

### Fixed

- [Fix EndpointRouting Guid regex + tests](https://github.com/giraffe-fsharp/Giraffe/pull/594) - Credits @64J0

## [6.4.0] - 2024-04-12

### Added

- [Add Fantomas validation to CI](https://github.com/giraffe-fsharp/Giraffe/pull/587) - Credits @64J0

### Changed

- [Upgrade to .NET 8](https://github.com/giraffe-fsharp/Giraffe/pull/527) - Credits @Banashek @fpellet
- [Improve CI by updating actions version and avoid concurrent jobs for the same PR](https://github.com/giraffe-fsharp/Giraffe/pull/582) - Credits @64J0

## [6.4.0-alpha-1] - 2024-03-10

### Changed

- [Upgrade to .NET 8](https://github.com/giraffe-fsharp/Giraffe/pull/527) - Credits @Banashek @fpellet

## [6.3.0] - 2024-03-01

### Changed

- Same as 6.3.0-alpha-1

## [6.3.0-alpha-1] - 2024-02-26

### Added

- [Add explicitly the statement that the usage of Giraffe.EndpointRouting is recommended](https://github.com/giraffe-fsharp/Giraffe/pull/556) - Credits @64J0
- [Add an example app using the cache features](https://github.com/giraffe-fsharp/Giraffe/pull/553) - Credits @64J0
- [Add GetWebHostEnvironment function and add deprecation warning to GetHostingEnvironment](https://github.com/giraffe-fsharp/Giraffe/pull/547) - Credits @64J0

### Changed

- [No need to build so many Regex](https://github.com/giraffe-fsharp/Giraffe/pull/568) - Credits @Thorium
- [Minor code optimisation](https://github.com/giraffe-fsharp/Giraffe/pull/567) - Credits @Thorium
- [Fix requiresAuthentication for null user identity](https://github.com/giraffe-fsharp/Giraffe/pull/557) - Credits @rslopes
- [Change sample EndpointRoutingApp level](https://github.com/giraffe-fsharp/Giraffe/pull/549) - Credits @64J0

## [6.2.0] - 2023-07-06

### Removed

- [remove Utf8Json support](https://github.com/giraffe-fsharp/Giraffe/pull/543) - Credits @jcmrva

## [6.1.0] - 2023-07-05

### Changed

- [Updating github workflow file to use nuget-acceptable version numbers for packaging](https://github.com/giraffe-fsharp/Giraffe/pull/517) - Credits @Banashek
- [Updating mimetype accept header parsing to use builtin aspnet parse/methods](https://github.com/giraffe-fsharp/Giraffe/pull/516) - Credits @Banashek
- [Make recyclableMemoryStreamManager internal](https://github.com/giraffe-fsharp/Giraffe/pull/514) - Credits @kerams
- [Upgrade to .NET 7](https://github.com/giraffe-fsharp/Giraffe/pull/527) - Credits @epoyraz
- [Documentation: Add another tutorial video to README](https://github.com/giraffe-fsharp/Giraffe/pull/533) - Credits @SIRHAMY
- [RFC-compliant Content-Length handling for 1xx, 204 and 205 responses and CONNECT requests](https://github.com/giraffe-fsharp/Giraffe/pull/541) - Credits @retendo
- [Restore 6.0 as TFM](https://github.com/giraffe-fsharp/Giraffe/pull/542) - Credits @TheAngryByrd

### Fixed

- [Fixed Slack invite link](https://github.com/giraffe-fsharp/Giraffe/pull/531) - Credits @anpin
- [Fix spelling in docs for BindJsonAsync and BindXmlAsync](https://github.com/giraffe-fsharp/Giraffe/pull/539) - Credits @onpikono

## [6.0.0] - 2022-09-04

### Changed

- Same as 6.0.0-alpha-2

## [6.0.0-alpha-2] - 2021-11-14

### Added

- Added `setContentType` handler

### Changed

- Made the `RecyclableMemoryStreamManager` configurable through DI
- Improved `Xml.Serializer` to also make use of the `RecyclableMemoryStreamManager`

## [6.0.0-alpha-1] - 2021-11-10

### Changed

- Upgraded to .NET 6 and F#'s new native `task` computation expression.

## [5.0.0] - 2021-05-24

### Changed

- Stable release of latest 5.0.0 RC with additional XML comment fixes to comply with latest F# compiler services.

## [5.0.0-rc-6] - 2020-12-08

### Changed

- Updated `Ply` from `0.1.*` to `0.3.*`.

## [5.0.0-rc-5] - 2020-12-08

### Changed

- Replaced `TaskBuilder.fs` with `Ply` for Giraffe's `task` computation expressions (see [#421](https://github.com/giraffe-fsharp/Giraffe/pull/421)). Ply is being actively developed by Crowded and has better exception stack traces for task computations and several performance improvements over TaskBuilder.fs.

## [5.0.0-rc-4] - 2020-12-07

### Added

- Added an overload for `UseGiraffe` to pass in an `Endpoint list`:
  - Before:

        ```fsharp
        app.UseEndpoints(fun e -> e.MapGiraffeEndpoints(endpoints))
        ```

  - Now:

        ```fsharp
        app.UseGiraffe(endpoints)
        ```

### Fixed

- Fixed bug when a `NestedEndpoint` preceded a `MultiEndpoint` in `Giraffe.EndpointRouting` (see [#452](https://github.com/giraffe-fsharp/Giraffe/issues/452))

### Removed

- Removed the sub-module `GiraffeMiddleware` from the `Giraffe.EndpointRouting` module (simply keep using the `UseGiraffe` extension method of an `IApplicationBuilder`)

## [5.0.0-rc-3] - 2020-12-01

### Added

- Added `ReadBodyBufferedFromRequestAsync` extension method to buffer and read a the request body and make subsequent reads possible (see [#449](https://github.com/giraffe-fsharp/Giraffe/issues/449))
- Added `GET_HEAD` to the endpoint routing functions, which will handle a `HEAD` request for the same `GET` handler.

### Changed

- Changed how the serialization modules are structured:
  - `IJsonSerializer` is now `Json.ISerializer`
  - `Utf8JsonSerializer` is now `Utf8Json.Serializer`
  - `NewtonsoftJsonSerializer` is now `NewtonsoftJson.Serializer`
  - `SystemTextJsonSerializer` is now `SystemTextJson.Serializer`
  - `IXmlSerializer` is now `Xml.ISerializer`
  - `DefaultXmlSerializer` is now `SystemXml.Serializer`
- Converted all `HttpContext` extension methods into C# compatible extension methods, meaning that function arguments had to be merged into tuples
- Changed the `GET`, `POST`, `PUT`, `HEAD`, etc. functions to accept an `Endpoint list` instead of an `Endpoint`
  - Before: `GET => route "/foo" (text "bar")`, After: `GET [ route "/foo" (text "bar") ]`
  - One can now compose routes easier:

        ```fsharp
        GET [
            route "/a" (text "A")
            route "/b" (text "B")
            route "/c" (text "C")
        ]
        ```

### Removed

- Removed the `=>` operator from `Giraffe.EndpointRouting`

## [5.0.0-rc-2] - 2020-11-26

### Added

- Added `routexp` http handler to default router (see [#446](https://github.com/giraffe-fsharp/Giraffe/issues/446))

### Fixed

- Fixed pre-conditions validation issue (see [#424](https://github.com/giraffe-fsharp/Giraffe/issues/424))
- Fixed parsing issue with Guids and ShortIds in `Giraffe.EndpointRouting` (see [#447](https://github.com/giraffe-fsharp/Giraffe/issues/447))

## [5.0.0-rc-1] - 2020-11-22

### Changed

- Upgraded to .NET 5. The 5.x version of Giraffe is targeting `net5.0` and dropping support for all other target frameworks.
- Only supported target framework is .NET 5

- Added `Giraffe.EndpointRouting` namespace with a version of a few routing handlers which integrate with ASP.NET Core's endpoint routing API
  - Currently supported are: `route`, `routef`, `subRoute` and HTTP verb handlers such as `GET`, `POST`, `PUT`, etc.
  - Check the [Endpoint Routing](https://github.com/giraffe-fsharp/Giraffe/blob/v5.0.0-rc-1/DOCUMENTATION.md#edpoint-routing) documentation for more details
  - Or check the [`EndpointRoutingApp` sample app](https://github.com/giraffe-fsharp/Giraffe/tree/v5.0.0-rc-1/samples/EndpointRoutingApp) for how to use `Giraffe.EndpointRouting`
- Replaced `Giraffe.GiraffeViewEngine` with the standalone NuGet package `Giraffe.ViewEngine`
- New `JsonOnlyNegotiationConfig` for setting a content negotiation policy which only supports JSON serialisation (no XML for those who don't need it)
- Added `SystemTextJsonSerializer` which uses `System.Text.Json` for JSON serialisation when configured as the desired JSON serializer in Giraffe
- Improved RegEx http handlers in original (non Endpoint routing) http handlers
- Swapped Markdown docs for XML docs for all functions.
- Added support for complex model binding (see [#416](https://github.com/giraffe-fsharp/Giraffe/issues/416))

## [5.0.0-alpha-003] - 2020-09-01

### Changed

- Enhanced Endpoint routing with a metadata list (see [PR #437](https://github.com/giraffe-fsharp/Giraffe/pull/437))

## [5.0.0-alpha-002] - 2020-06-21

### Added

- Added dependency to new `Giraffe.ViewEngine` package and re-introduced the `htmlView` and `WriteHtmlViewAsync` functions into Giraffe.
- Added support for complex model binding (see [#416](https://github.com/giraffe-fsharp/Giraffe/issues/416))

### Changed

- Swapped Markdown docs for XML docs for all functions.
- Improved endpoint routing by deferring the creation of `RequestDelegate` functions.

## [5.0.0-alpha-001] - 2020-05-27

### Added

- Added `System.Text.Json` serializer
- Added `Giraffe.EndpointRouting` namespace with a super early alpha version of new routing handlers which integrate with ASP.NET Core's endpoint routing API (check out the `EndpointRoutingApp` sample app for examples before the documentation is ready)
- Added `SystemTextJsonSerializer` which uses `System.Text.Json` for JSON serialisation when configured as the desired JSON serializer in Giraffe
- New `JsonOnlyNegotiationConfig` for setting a content negotiation which only supports JSON serialisation and not XML

### Changed

- Only supported target framework is .NET Core 3.1 (in preparation for .NET 5)
- Improved RegEx http handlers in original (non Endpoint routing) http handlers

### Removed

- Removed `Giraffe.GiraffeViewEngine` (in preparation to distribute it as a separate NuGet package, which doesn't exist yet). This release has no `GiraffeViewEngine` which is one of the reasons why it's an `alpha-001` release. Plans are to bring it back in `5.0.0-alpha-002`

## [4.1.0] - 2020-04-13

### Added

- Added `netcoreapp3.1` support

### Removed

- Removed redundant dependencies

### Fixed

- Fixed model binding for arrays (see [#403](https://github.com/giraffe-fsharp/Giraffe/issues/403))
- Fixed pre-condition bug for the `If-Unmodified-Since` HTTP header (see [#402](https://github.com/giraffe-fsharp/Giraffe/issues/402))

## [4.0.1] - 2019-10-18

### Fixed

- Fixed dependency references for TFM `netcoreapp3.0` projects.

## [4.0.0] - 2019-09-29

### Added

- Support for array of `'T` as a child in form binding.
- Added a new `DateTime` extension method `ToIsoString` which produces a RFC3339 formatted string, and corrected the docs on the existing `ToHtmlString` extension method which actually produces a RFC822 formatted string.
- Added new version of `tryMatchInput` which accepts `MatchSettings` record:

    ```fsharp
    type MatchMode =
        | Exact                // Will try to match entire string from start to end.
        | StartsWith           // Will try to match a substring. Subject string should start with test case.
        | EndsWith             // Will try to match a substring. Subject string should end with test case.
        | Contains             // Will try to match a substring. Subject string should contain test case.

    type MatchOptions = { IgnoreCase: bool; MatchMode: MatchMode; }
    ```

### Changed

- Giraffe 4.0.0 has been tested against `netcoreapp3.0` alongside `netcoreapp2.1` and `net461`. All sample code has been upgraded to .NET Core 3.0 as well.
- Changed minimum version of `Newtonsoft.Json` to `11.0.2`. This allows Giraffe to be compatible with Azure Functions.
- Renamed `tryMatchInput` to `tryMatchInputExact` and swapped the order of arguments so that the string value comes last.

### Fixed

- Fixed `routef` to not match more than one URL path segment (see [issue #347](https://github.com/giraffe-fsharp/Giraffe/issues/347)):
  - Before: route `/foo/bar/%s` matched `/foo/bar/hello/world`
  - After: route `/foo/bar/%s` does not match `/foo/bar/hello/world`
- Fixed the `_ariaLabelledBy` attribute in the `GiraffeViewEngine`
- Fixed case insensitive route handlers on Ubuntu

## [3.6.0] - 2019-02-10

### Fixed

- Fixed a bug in the `subRouteCi` http handler, which prevented nested sub routes to be case insensitive.

### Added

- Added two new `HttpContext` extension methods to retrieve cookie and form values:
  - `GetCookieValue (key : string)`
  - `GetFormValue (key : string)`

## [3.5.1] - 2019-01-20

### Fixed

- Fixed a bug in Giraffe's model binding to not try to set read only properties anymore.

## [3.5.0] - 2018-12-28

### Added

- Updated all packages and framework library dependencies to .NET Core 2.2.
- Added a new `GET_HEAD` http handler (see: [#314](https://github.com/giraffe-fsharp/Giraffe/issues/314) for more info).
- Added a new convenience function called `handleContext`, which can be used for creating new `HttpHandler` functions.

### Fixed

- Fixed the `_data` attribute in the `GiraffeViewEngine` to accept a `key` and `value` parameter now.

## [3.4.0] - 2018-10-28

### Added

- Added a new http handler called `authorizeRequest` to authorize a request based on a `HttpContext -> bool` predicate.
- Added a new http handler called `authorizeUser` which is an alias for `evaluateUserPolicy`. The `evaluateUserPolicy` handler will be removed in the next major release.

## [3.3.0] - 2018-10-28

### Added

- Added `str` as an alias for the `encodedText` function from the `GiraffeViewEngine`.
- Added the `HttpContext.GetRequestUrl()` extension method to retrieve the entire URL string of the incoming HTTP request.

## [3.2.0] - 2018-10-10

### Added

- Adding the `charset` parameter in the HTTP `Content-Type` response header when returning a text response (text/plain, text/html) or a JSON or XML response (application/json, application/xml). By default Giraffe is using UTF8 encoding for all its responses.

## [3.1.0] - 2018-09-28

### Added

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

### Changed

- Performance improvements for Giraffe's default response writers.
- Performance improvements of the `htmlView` handler.
- Upgraded to the latest `TaskBuilder.fs` NuGet package which also has the SourceLink integration now.

### Fixed

- Fixed the `Successful.NO_CONTENT` http handler, which threw an exception when calling from ASP.NET Core 2.1.

## [3.0.0] - 2018-08-18

### Changed

- Changed the type `XmlNode` by removing the `RawText` and `EncodedText` union case and replaced both by a single `Text` union case. The HTML encoding (or not) is being done now when calling one of the two helper functions `rawText` and `encodedText`.

  - This change - even though theoretically a breaking change - should not affect the vast majority of Giraffe users unless you were constructing your own `XmlNode` elements which were of type `RawText` or `EncodedText` (which is extremely unlikely given that there's not much room for more nodes of these two types).

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

- Significant performance improvements in the `GiraffeViewEngine` by changing the underlying composition of views from simple string concatenation to using a `StringBuilder` object.

### Removed

- Removed the `task {}` override in Giraffe which was forcing the `FSharp.Control.Tasks.V2.ContextInsensitive` version of the Task CE. This change has no effect on the behaviour of `task` computation expressions in Giraffe. In the context of an ASP.NET Core web application there is not difference between `ContextSensitive` and `ContextInsensitive` which is why the override has been removed. The only breaking change which could affect an existing Giraffe web application is that in some places you will need to explicitly `open FSharp.Control.Tasks.V2.ContextInsensitive` where before it might have been sufficient to only `open Giraffe`.

### Added

- Support for short GUIDs and short IDs (aka YouTube IDs) [in route arguments](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#routef) and [query string parameters](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#short-guids-and-short-ids).
- Enabled [SourceLink](https://github.com/dotnet/sourcelink/) support for Giraffe source code (thanks [Cameron Taggart](https://github.com/ctaggart))! For more information check out [Adding SourceLink to your .NET Core Library](https://carlos.mendible.com/2018/08/25/adding-sourcelink-to-your-net-core-library/).
- Added a new JSON serializer called `Utf8JsonSerializer`. This type uses the [Utf8 JSON serializer library](https://github.com/neuecc/Utf8Json/), which is currently the fastest JSON serializer for .NET. `NewtonsoftJsonSerializer` is still the default JSON serializer in Giraffe (for stability and backwards compatibility), but `Utf8JsonSerializer` can be swapped in via [ASP.NET Core's dependency injection API](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#json). The new `Utf8JsonnSerializer` is significantly faster (especially when sending chunked responses) than `NewtonsoftJsonSerializer`.
- Added a new `HttpContext` extension method for chunked JSON transfers: `WriteJsonChunkedAsync<'T> (dataObj : 'T)`. This new `HttpContext` method can write content directly to the HTTP response stream without buffering into a byte array first (see [Writing JSON](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#writing-json)).
- Added a new `jsonChunked` http handler. This handler is the equivalent http handler version of the `WriteJsonChunkedAsync` extension method.
- Added first class support for [ASP.NET Core's response caching](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#response-caching) feature.
- Special thanks to [Dmitry Kushnir](https://github.com/dv00d00) for doing the bulk work of all the perf improvements in this release as well as adding Giraffe to the [TechEmpower Webframework Benchmarks](https://techempower.com/benchmarks/)!

## [2.0.1] - 2018-08-20

### Changed

- Changed the `task {}` CE to load from `FSharp.Control.Tasks.V2.ContextInsensitive` instead of `FSharp.Control.Tasks.ContextInsensitive`.

## [2.0.0] - 2018-08-18

### Changed

- Changed the name of the handler `requiresAuthPolicy` to `evaluateUserPolicy` in order to better describe its functionality and to avoid a name clash between two newly added handlers for validating ASP.NET Core's `AuthorizationPolicy` objects (see new features).
- Changed how he `AddGiraffe()` extension method registers Giraffe dependencies in ASP.NET Core. It now follows the `TryAdd` pattern which will only register a dependency if it hasn't been registered beforehand.
- Changed the `HttpContext.GetService<'T>()` extension method to throw a `MissingDependencyException` if it cannot resolve a desired dependency.

### Added

- Added two new http handlers to validate an ASP.NET Core `AuthorizationPolicy` (see: [Policy based authorization](https://docs.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-2.1)). The `authorizeByPolicyName` and `authorizeByPolicy` http handlers will use ASP.NET Core's authorization service to validate a user against a given policy.
- Updated `TaskBuilder.fs` to version `2.0.*`.
- Updated ASP.NET Core NuGet packages to latest `2.1.*` versions.
- Enabled `return!` for `opt { }` computation expressions.
- Added `blockquote`, `_integrity` and `_scoped` to the `GiraffeViewEngine`.
- Added attributes for mouse, keyboard, touch, drag & drop, focus, input and mouse wheel events to the `GiraffeViewEngine`.
- Added new accessibility attributes to the `GiraffeViewEngine`. These can be used after opening the `Giraffe.GiraffeViewEngine.Accessibility` module.
- Added a new `Successful.NO_CONTENT` http handler which can be used to return a HTTP 204 response.
- Added more structured logging around the Giraffe middleware.

### Fixed

- Fixed a bug in `routef`, `routeCif` and `subRoutef` which prohibited to parse multiple GUIDs
- Fixed a bug in `routef`, `routeCif` and `subRoutef` which wrongly decoded a route argument twice (and therefore turned `+` signs into spaces).
- Fixed XML documentation for all Giraffe functions which should make function tooltips nicely formatted again.
- Enabled the `HttpContext.BindModelAsync<'T>()` extension method and the `bindModel<'T>` http handler to also bind to a model in the case of a `PATCH` or `DELETE` http request.

## [1.1.0] - 2018-02-16

### Added

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
- For more details, see the official [Giraffe 1.1.0 release blog post](https://dusted.codes/giraffe-110-more-routing-handlers-better-model-binding-and-brand-new-model-validation-api).

### Fixed

- `routeBind` works when nested in a `subRoute` handler now
- `routeBind` doesn't crate a model object any more if the route arguments do not match the provided model

## [1.0.0] - 2018-02-08

### Added

- JSON and XML serialization is now configurable through Dependency Injection (see [Serialization](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#serialization))
- Added new features to validate conditional HTTP headers before processing a web request (see [Conditional Requests](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#conditional-requests))
- Added streaming capabilities (see [Streaming](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#streaming))
- Added `HEAD`, `OPTIONS`, `TRACE`, `CONNECT` http handlers
- Added more `HttpContext` extension methods to create parity between response writing methods and `HttpHandler` functions (see [Response Writing](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#response-writing) and [Content Negotiation](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#content-negotiation))
- Added detailed XML docs to all public facing functions for better Intellisense support
- The `Giraffe.Common` module auto opens now

### Changed

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
- For more details, see the official [Giraffe 1.0.0 release blog post](https://dusted.codes/announcing-giraffe-100).

## [0.1.0-beta-700] - 2017-12-22

### Changed

- Renamed `portRoute` to `routePorts` to be more consistent with other routing functions (`route`, `routef`, `routeStartsWith`, etc.)

### Added

- `routef` and `routeCif` both support `%O` for matching `System.Guid` values now
- Added HTML attributes helper functions to the `GiraffeViewEngine`:

  ```fsharp
  let html = p [ _class "someCssClass"; _id "greetingsText" ] [ encodedText "Hello World" ]
  ```

## [0.1.0-beta-600] - 2017-12-20

### Changed

- Renamed `Giraffe.XmlViewEngine` to `Giraffe.GiraffeViewEngine` as it represented more than just an XML view engine.

### Added

- Added automatic validation of the format string inside `routef` and `routeCif` to notify users of the notorious `%d` vs `%i` error during startup.

## [0.1.0-beta-511] - 2017-12-19

### Fixed

- Fixed `ReadBodyFromRequestAsync` where the stream has been disposed before read could complete.

## [0.1.0-beta-510] - 2017-12-19

### Changed

- Explicitly set the encoding to UTF-8 when reading the HTTP body during `ReadBodyFromRequestAsync`

### Added

- Added the `html` http handler which can be used to return a `text/html` response by passing in the html content as a string variable

## [0.1.0-beta-500] - 2017-12-12

### Added

- Added a new overload for `GetLogger` of the `HttpContext` extension methods, which allows one to pass in a `categoryName` string in order to initialise a new logger: `let logger = ctx.GetLogger "categoryName"`.
- `BindFormAsync`, `BindQueryString` and `BindModelAsync` accept an additional optional parameter for `CultureInfo`.

### Changed

- Removed `Giraffe.Tasks` from the `Giraffe` NuGet package and added a new dependency on the newly created `Giraffe.Tasks` NuGet package. You can use the `Giraffe.Tasks` NuGet package from non ASP.NET Core projects now as well.

## [0.1.0-beta-400] - 2017-12-04

### Added

- Added [HTTP status code helper functions](https://github.com/dustinmoris/Giraffe#statuscode-httphandlers).
- Added `defaultSerializeJson` and `defaultDeserializeJson` methods.
- Auto opened default Giraffe modules so that the core functionality can be entirely consumed through a single `open Giraffe` statement.
- The functionality from `Giraffe.Razor.Middleware` and `Giraffe.Razor.HttpHandlers` can be both consumed through a single `open Giraffe.Razor` now.

### Fixed

- Changed the `base` tag from the `XmlViewEngine` from a regular `tag` to a `voidTag` to comply with the HTML spec.

### Changed

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

## [0.1.0-beta-310] - 2017-11-30

### Added

- Added `portRoute` http handler to filter an incoming request based on the port

### Changed

- The `GET`, `POST`, `PUT` and `DELETE` http handlers of the `TokenRouter.fs` have changed to accept a list of http handlers now.

### Fixed

- TokenRouter fringe case not being identified before (see #150)

## [0.1.0-beta-300] - 2017-11-18

### Added

- Added `requiresAuthPolicy` http handler
- Added `RenderHtml` and `ReturnHtmlFile` extension methods to the `HttpContext` object
- Added `customJson` http handler, which allows users to define a custom json handler (with custom serialization settings)
- Added overloads to `BindJson` and `BindModel` where a user can pass in a custom `JsonSerializerSettings` object

### Changed

- Changed the default json serializer to use camel case for serialization (this change prevents users from being able to change the default serializer through the `JsonConvert.DefaultSettings` object - use `customJson` instead if customization is required)
- Changed the `serializeJson`, `deserializeJson` methods to accept an aditional parameter of type `JsonSerializerSettings`

### Fixed

- Automatically URL decoding of string values when using `routef`
- Fixed an inference bug with `routef` by replacing the `format` parameter of the `tryMatchInput` method and the `path` parameter of the `routef` and `routeCif` methods from `StringFormat` to `PrintFormat`
- Changed the implementation of `ctx.BindJson<'T>()` for better performance and which aims to fix an Azure bug with Kestrel (#136)
- Fixed a bug with `routeBind` (#129)
- Improved the `htmlFile` http handler by allowing the `filePath` parameter to be either rooted or relative to the `ContentRootPath`

## [0.1.0-beta-200] - 2017-10-29

### Added

- Added three additional `HttpContext` extension methods in the `Giraffe.HttpContextExtensions` module: `WriteJson`, `WriteXml` and `WriteText`. These three methods can be used for direct HttpReponse writing from within a custom handler without having to sub-call the `json`, `xml` or `text` http handlers.
- Changed the `UseGiraffeErrorHandler` method to return an `IApplicationBuilder` now. This change allows [middleware chaining](https://github.com/dustinmoris/Giraffe/pull/118). This is a breaking change and you'll either have to chain middleware or append an `|> ignore` in your application set up.

## [0.1.0-beta-110] - 2017-10-25

### Added

- Added the `Giraffe.TokenRouter` module for [speed improved route handling](https://github.com/dustinmoris/Giraffe/issues/56).

## [0.1.0-beta-102] - 2017-10-19

### Changed

- Improved the `routeBind` http handler to give users more flexibility in mapping routes to HTTP requests (see [#110](https://github.com/dustinmoris/Giraffe/issues/110)).

## [0.1.0-beta-101] - 2017-10-10

### Added

- Added CORS settings for localhost to the default giraffe-template

### Fixed

- Fixed bug in connection with the `ExceptionHandlerMiddleware` (see #106)

## [0.1.0-beta-100] - 2017-08-28

### Changed

- Updated Giraffe to .NET Standard 2.0. All Giraffe NuGet packages now target `net461` and `netstandard2.0`. You will have to upgrade your ASP.NET Core application to either full .NET framework 461 or to a .NET Core app 2.0. General advice is to upgrade all .NET Core web applications to 2.0.

## [0.1.0-beta-003] - 2017-08-26

### Fixed

- Fixed bug where `readFileAsString` closed the stream before the file could be read
- [Giraffe.Razor 0.1.0-beta-002] Fixed bug so that `_ViewStart.cshtml` files get respected now
- [giraffe-template 0.1.9] Fixed wrong version numbers in package references for `Giraffe` and `Giraffe.Razor`

## [0.1.0-beta-002] - 2017-08-19

### Added

- Added support for `Async<'T>` in the `task {}` workflow. You can use an `Async<'T>` from within `task {}` without having to convert back to a `Task<'T>`

### Fixed

- Fixed the `warbler` function. Should work as expected again.
- Set the Giraffe dependency version in the template to a concrete version to avoid breaking changes in the template.

## [0.1.0-beta-001] - 2017-08-12

### Added

- First Beta release of Giraffe.

### Changed

- [Continuations over binding](https://github.com/dustinmoris/Giraffe/pull/71)
- [Tasks instead of Async](https://github.com/dustinmoris/Giraffe/pull/75)

The `HttpHandler` has slightly [changed](https://github.com/dustinmoris/Giraffe#httphandler).

Blog post with more info is coming shortly!

## [0.1.0-alpha025] - 2017-07-22

### Changed

Changed the type `XmlAttribute` from the `XmlViewEngine` to accept either a `string * string` key value pair or a boolean attribute of type `string`. This was a missing to enable script tags such as `<script src="..." async></script>`.

### Added

Added two helper functions (`attr` and `flag`) to simplify the creation of those attributes:

```fsharp
script [
    attr "src" "http://example.org/example.js"
    attr "lang" "javascript"
    flag "async" ] []
```

## [0.1.0-alpha024] - 2017-07-16

### Changed

- New `routeBind` http handler
- Annotated all default http handler functions with the `HttpHandler` type

## [0.1.0-alpha023] - 2017-07-05

### Fixed

- Fixed build error in the Giraffe template.

### Changed

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

## [0.1.0-alpha022] - 2017-07-01

### Changed

A few modifications to the former `HtmlEngine` so that it can be used for correct XML rendering as well:

- Renamed the `Giraffe.HtmlEngine` module to `Giraffe.XmlViewEngine`
- Renamed `HtmlAttribute` to `XmlAttribute`, `HtmlElement` to `XmlElement` and `HtmlNode` to `XmlNode`
- Renamed and make the function `nodeToHtmlString` private
- Added `comment` function to the `Giraffe.XmlViewEngine` module for creating XML comments
- Added `renderXmlString` and `renderHtmlString` functions to `Giraffe.XmlViewEngine` module for rendering XML and HTML nodes.

## [0.1.0-alpha021] - 2017-07-01

### Changed

- Changed `HttpContext.BindQueryString<'T>()` to return `'T` instead of `Async<'T>`

### Added

- Added `HttpContext.TryGetQueryStringValue (key : string)` which returns an `Option<string>`
- Added `HttpContext.GetQueryStringValue (key : string)` which returns a `Result<string, string>`

## [0.1.0-alpha020] - 2017-06-23

### Changed

Split out the Razor view engine and the DotLiquid templating engine into separate NuGet packages:

- `Giraffe.Razor`
- `Giraffe.DotLiquid`

Please reference the additional packages if you were using any of the view or templating handlers.

Also updated the `giraffe-template` NuGet package with the new changes and adapted the `build.ps1` PowerShell script to successfully build on Linux environments too.

Additionally TravisCI builds are run as part of every commit as well now.

## [0.1.0-alpha019] - 2017-05-31

### Added

Adds [support for the `Option<'T>` type when model binding from a query string](https://github.com/dustinmoris/Giraffe/issues/51).

## [0.1.0-alpha018] - 2017-05-17

### Added

- Added two new `HttpContext` extension methods:
  - `TryGetRequestHeader (key : string)` which returns an `Option<string>`
  - `GetRequestHeader (key : string)` which returns a `Result<string, string>`
- Added default computation expressions for the `Option<'T>` and `Result<'T, 'TError>` types under `Giraffe.ComputationExpressions`

## [0.1.0-alpha017] - 2017-05-14

### Added

- Added `plain/text` as a new supported mime type to the default `negotiate` handler (it will be using an object's `.ToString()` method to serialize an object into plain text)
- Added new helper functions for retrieving a logger or dependencies as extension methods of the `HttpContext` object: `ctx.GetService<'T>()` and `ctx.GetLogger<'T>()`

### Changed

- Completely removed the `HttpHandlerContext` type and replaced all usage with the original `HttpContext` object from ASP.NET Core.
- Extended the `ErrorHandler` function with a parameter to retrieve a default `ILogger` object
- Moved model binding functions from the `Giraffe.ModelBinding` module into the `Giraffe.HttpContextExtensions` module and made them extension methods of the `HttpContext` object
- Updated the `giraffe-template` NuGet package with the latest changes.

## [0.1.0-alpha016] - 2017-05-04

### Fixed

- Fixed [#46](https://github.com/giraffe-fsharp/Giraffe/issues/46)

## [0.1.0-alpha015] - 2017-04-26

### Changed

- Changed the signature of the `redirectTo` http handler (swapped `permanent` with `location`).

## [0.1.0-alpha014] - 2017-04-26

### Added

- Added `redirectTo` http handler.

## [0.1.0-alpha013] - 2017-04-16

### Changed

- Using culture invariant converters in model binders.

## [0.1.0-alpha012] - 2017-04-15

### Added

- Added `bindQueryString` which can automatically bind a model from query string parameters
- Extended `bindModel` to include `bindQueryString` when the HTTP method is not `POST` or `PUT`

## [0.1.0-alpha011] - 2017-04-15

### Added

- Added a `warbler` function
- Added model binding capabilities which can automatically bind a HTTP payload to a strongly typed model: `bindJson`, `bindXml`, `bindForm` and `bindModel`

### Changed

- Improved the `negotiateWith` and `negotiate` http handlers by making use of ASP.NET Core's `MediaTypeHeaderValue` class
- Added `*.cshtml` files to the DotNet watcher in the template

### Fixed

- Fixed `AssemblyName` and `PackageId` values in the template

## [0.1.0-alpha010] - 2017-04-13

### Added

- Added two new `HttpHandler` functions:
  - `negotiate` checks the `Accept` header of a request and determines automatically if a response should be sent in JSON or XML
  - `negotiateWith` is the same as `negotiate`, but additionally accepts an `IDictionary<string, obj -> HttpHandler>` which allows users to extend the default negotiation rules (e.g. change default serialization if a client is indifferent, or add more supported mime types, etc.)

## [0.1.0-alpha009] - 2017-04-11

### Added

- Added a new programmatic view engine called `Giraffe.HtmlEngine`
- Addd a new `HttpHandler` named `renderHtml` to return views from the new view engine

## [0.1.0-alpha008] - 2017-04-01

### Changed

- Updated `Newtonsoft.Json` to version `10.0.*`
- Updated the Giraffe `dotnet new` template

## [0.1.0-alpha007] - 2017-03-31

### Added

- Added a new HttpHandler named `dotLiquidTemplate`. The difference is that it let's the caller decide what `Content-Type` the response shall be.
- Added a new HttpHandler named `razorView`. The difference is that it let's the caller decide what `Content-Type` the response shall be.
- Created a NuGet package called `giraffe-template` which is a new `dotnet new` template for Giraffe projects

### Changed

- NuGet package is being built with official VS 2017 build image by AppVeyor (using .NET Core SDK 1.0.1)
- Renamed the HttpHandler `htmlTemplate` to `dotLiquidHtmlView`
- Renamed the HttpHandler `razorView` to `razorHtmlView`

### Removed

- Removed `RazorLight` as a dependency and replaced it with the official razor engine by ASP.NET Core MVC

## [0.1.0-alpha006] - 2017-03-03

### Changed

- This release creates a new NuGet package named **Giraffe.nupkg**, which will be the new NuGet library for this project going forward. The old package **AspNetCore.Lambda.nupkg** will remain as-is for backwards compatibility and will not be removed or updated anymore.
- Added a default logger to the `HttpHandlerContext`
- Renamed NuGet package to Giraffe

## [0.1.0-alpha005] - 2017-03-01

### Changed

- Changed dependency from `Microsoft.AspNetCore.Hosting` to `Microsoft.AspNetCore.Hosting.Abstractions`
- Added `razorView` HttpHandler

## [0.1.0-alpha004] - 2017-03-01

### Changed

- Re-factored `bind` to make it a true bind function
- Added a new `compose` combinator
- The `>>=` operator became `>=>` now and `>>=` is the new `bind` function (Fixes #5)
- Upgraded project to .NET Core SDK RC4

## [0.1.0-alpha003] - 2017-03-01

### Changed

- Upgraded to FSharp.Core 4.1.0
- Added `subRoute` and `subRouteCi` handlers (Fixes #7 )
- Uses culture invariant parse functions for `routef` and `routeCif` (See #8)

## [0.1.0-alpha002] - 2017-03-01

### Changed

- Changed the `HttpHandlerContext` to include an `IServiceProvider` and removed `IHostingEnvironment` and `ILoggerFactory` instead
- Added more default HttpHandlers: `challenge`, `signOff`, `requiresAuthentication`, `requiresRole`, `requiresRoleOf`, `clearResponse` and `xml`
- Added XML documentation to all default HttpHandlers
- Updated to latest Microsoft.FSharp.Core.netcore NuGet package, which is in RC now
- Changed the name of the `ErrorHandlerMiddleware` to `LambdaErrorHandlerMiddleware` and changed the `IApplicationBuilder` extension method to `UseLambdaErrorHandler`

## [0.1.0-alpha001] - 2017-03-01

### Added

- First alpha release with a basic set of functionality.
