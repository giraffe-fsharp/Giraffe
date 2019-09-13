[<AutoOpen>]
module Giraffe.Routing

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe.FormatExpressions

// ---------------------------
// Sub Routing Feature
// ---------------------------

type ISubRoutingFeature =
    /// **Description**
    ///
    /// Gets the next part of the `HttpRequest` path, which hasn't been resolved by the `ISubRoutingFeature` yet.
    ///
    /// **Output**
    ///
    /// A `string` value holding the next part of the request path.
    ///
    abstract member GetNextPartOfPath : HttpContext -> string

    /// **Description**
    ///
    /// Matches the `HttpRequest` path with a given sub path and executes the next `HttpHandler` on based on the result.
    ///
    /// **Output**
    ///
    /// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
    ///
    abstract member RouteWithSubPath  : string -> HttpHandler -> HttpHandler

type SubRoutingFeature() =
    [<Literal>]
    let RouteKey = "giraffe_route"

    let mutable subPath = ValueNone

    let getSubPathFromItems (ctx : HttpContext) =
        if ctx.Items.ContainsKey RouteKey
        then ctx.Items.Item RouteKey |> string |> strValueOption
        else ValueNone

    let getSubPath (ctx : HttpContext) =
        match subPath with
        | ValueSome p -> ValueSome p
        | ValueNone   ->
            let giraffeOptions = ctx.GetGiraffeOptions()
            match giraffeOptions.CompatibilityMode with
            | Version36 -> getSubPathFromItems ctx
            | Version40 -> ValueNone

    let getNextPartOfPath (ctx : HttpContext) =
        let requestPath = ctx.Request.Path.Value
        match getSubPath ctx with
        | ValueSome p when requestPath.StartsWith p -> requestPath.[p.Length..]
        | _ -> requestPath

    let setSubPath (ctx : HttpContext) (path : string) =
        subPath <- ValueSome path
        let giraffeOptions = ctx.GetGiraffeOptions()
        if giraffeOptions.CompatibilityMode = Version36 then
            ctx.Items.[RouteKey] <- path

    let clearSubPath (ctx : HttpContext) =
        subPath <- ValueNone
        let giraffeOptions = ctx.GetGiraffeOptions()
        if giraffeOptions.CompatibilityMode = Version36 then
            ctx.Items.Remove RouteKey |> ignore

    let routeWithSubPath (path : string) (handler : HttpHandler) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let tempSubPath = getSubPath ctx
                setSubPath
                    ctx
                    ((tempSubPath |> ValueOption.defaultValue "") + path)
                let! result = handler next ctx
                match result with
                | Some _ -> ()
                | None ->
                    match tempSubPath with
                    | ValueSome p -> setSubPath ctx p
                    | ValueNone   -> clearSubPath ctx |> ignore
                return result
            }

    interface ISubRoutingFeature with
        member __.GetNextPartOfPath ctx         = getNextPartOfPath ctx
        member __.RouteWithSubPath path handler = routeWithSubPath path handler

type HttpContext with

    /// **Description**
    ///
    /// Returns the next part of the request path, which hasn't been resolved by a routing handler yet.
    ///
    member this.GetNextPartOfPath() =
        let subRoutingFeature = this.Features.Get<ISubRoutingFeature>()
        subRoutingFeature.GetNextPartOfPath this

// ---------------------------
// Public routing HttpHandler functions
// ---------------------------

/// **Description**
///
/// Filters an incoming HTTP request based on the port.
///
/// **Parameters**
///
/// `fns`: List of port to `HttpHandler` mappings
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routePorts (fns : (int * HttpHandler) list) : HttpHandler =
    fun next ->
        let portMap = Dictionary<_, _>(fns.Length)
        fns |> List.iter (fun (p, h) -> portMap.Add(p, h next))
        fun (ctx : HttpContext) ->
            let port = ctx.Request.Host.Port
            if port.HasValue then
                match portMap.TryGetValue port.Value with
                | true , func -> func ctx
                | false, _    -> skipPipeline
            else skipPipeline

/// **Description**
///
/// Filters an incoming HTTP request based on the request path (case sensitive).
///
/// **Parameters**
///
/// `path`: Request path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let route (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if ctx.GetNextPartOfPath().Equals path
        then next ctx
        else skipPipeline

/// **Description**
///
/// Filters an incoming HTTP request based on the request path (case insensitive).
///
/// **Parameters**
///
/// `path`: Request path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeCi (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if ctx.GetNextPartOfPath().Equals(path, StringComparison.OrdinalIgnoreCase)
        then next ctx
        else skipPipeline

/// **Description**
///
/// Filters an incoming HTTP request based on the request path using Regex (case sensitive).
///
/// **Parameters**
///
/// `path`: Regex path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routex (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let pattern = sprintf "^%s$" path
        let regex   = Regex(pattern, RegexOptions.Compiled)
        let result  = regex.Match (ctx.GetNextPartOfPath())
        match result.Success with
        | true  -> next ctx
        | false -> skipPipeline

/// **Description**
///
/// Filters an incoming HTTP request based on the request path using Regex (case insensitive).
///
/// **Parameters**
///
/// `path`: Regex path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeCix (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let pattern = sprintf "^%s$" path
        let regex   = Regex(pattern, RegexOptions.IgnoreCase ||| RegexOptions.Compiled)
        let result  = regex.Match (ctx.GetNextPartOfPath())
        match result.Success with
        | true  -> next ctx
        | false -> skipPipeline

/// **Description**
///
/// Filters an incoming HTTP request based on the request path (case sensitive).
///
/// If the route matches the incoming HTTP request then the arguments from the `PrintfFormat<...>` will be automatically resolved and passed into the supplied `routeHandler`.
///
/// **Supported format chars**
///
/// `%b`: `bool`
/// `%c`: `char`
/// `%s`: `string`
/// `%i`: `int`
/// `%d`: `int64`
/// `%f`: `float`/`double`
/// `%O`: `Guid`
///
/// **Parameters**
///
/// `path`: A format string representing the expected request path.
/// `routeHandler`: A function which accepts a tuple `'T` of the parsed arguments and returns a `HttpHandler` function which will subsequently deal with the request.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routef (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path
    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path MatchOptions.Exact (ctx.GetNextPartOfPath())
        |> function
            | None      -> skipPipeline
            | Some args -> routeHandler args next ctx

/// **Description**
///
/// Filters an incoming HTTP request based on the request path (case insensitive).
///
/// If the route matches the incoming HTTP request then the arguments from the `PrintfFormat<...>` will be automatically resolved and passed into the supplied `routeHandler`.
///
/// **Supported format chars**
///
/// `%b`: `bool`
/// `%c`: `char`
/// `%s`: `string`
/// `%i`: `int`
/// `%d`: `int64`
/// `%f`: `float`/`double`
/// `%O`: `Guid`
///
/// **Parameters**
///
/// `path`: A format string representing the expected request path.
/// `routeHandler`: A function which accepts a tuple `'T` of the parsed arguments and returns a `HttpHandler` function which will subsequently deal with the request.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeCif (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path
    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path MatchOptions.IgnoreCaseExact (ctx.GetNextPartOfPath())
        |> function
            | None      -> skipPipeline
            | Some args -> routeHandler args next ctx

/// **Description**
///
/// Filters an incoming HTTP request based on the request path (case insensitive).
///
/// If the route matches the incoming HTTP request then the parameters from the string will be used to create an instance of `'T` and subsequently passed into the supplied `routeHandler`.
///
/// **Parameters**
///
/// `route`: A string representing the expected request path. Use `{propertyName}` for reserved parameter names which should map to the properties of type `'T`. You can also use valid `Regex` within the `route` string.
/// `routeHandler`: A function which accepts a tuple `'T` of the parsed parameters and returns a `HttpHandler` function which will subsequently deal with the request.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeBind<'T> (route : string) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let pattern = route.Replace("{", "(?<").Replace("}", ">[^/\n]+)") |> sprintf "^%s$"
        let regex   = Regex(pattern, RegexOptions.IgnoreCase)
        let result  = regex.Match (ctx.GetNextPartOfPath())
        match result.Success with
        | true ->
            let groups = result.Groups
            let result =
                regex.GetGroupNames()
                |> Array.skip 1
                |> Array.map (fun n -> n, StringValues groups.[n].Value)
                |> dict
                |> ModelParser.tryParse None
            match result with
            | Error _  -> skipPipeline
            | Ok model -> routeHandler model next ctx
        | _ -> skipPipeline

/// **Description**
///
/// Filters an incoming HTTP request based on the beginning of the request path (case sensitive).
///
/// **Parameters**
///
/// `subPath`: The expected beginning of a request path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeStartsWith (subPath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if ctx.GetNextPartOfPath().StartsWith subPath
        then next ctx
        else skipPipeline

/// **Description**
///
/// Filters an incoming HTTP request based on the beginning of the request path (case insensitive).
///
/// **Parameters**
///
/// `subPath`: The expected beginning of a request path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeStartsWithCi (subPath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if ctx.GetNextPartOfPath().StartsWith(subPath, StringComparison.OrdinalIgnoreCase)
        then next ctx
        else skipPipeline


/// **Description**
///
/// Filters an incoming HTTP request based on the beginning of the request path (case sensitive).
///
/// If the route matches the incoming HTTP request then the arguments from the `PrintfFormat<...>` will be automatically resolved and passed into the supplied `routeHandler`.
///
/// **Supported format chars**
///
/// `%b`: `bool`
/// `%c`: `char`
/// `%s`: `string`
/// `%i`: `int`
/// `%d`: `int64`
/// `%f`: `float`/`double`
/// `%O`: `Guid`
///
/// **Parameters**
///
/// `path`: A format string representing the expected request path.
/// `routeHandler`: A function which accepts a tuple `'T` of the parsed arguments and returns a `HttpHandler` function which will subsequently deal with the request.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeStartsWithf (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path

    let options = { MatchOptions.IgnoreCase = false; MatchMode = StartsWith }

    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path options (ctx.GetNextPartOfPath())
        |> function
            | None      -> skipPipeline
            | Some args -> routeHandler args next ctx

/// **Description**
///
/// Filters an incoming HTTP request based on the beginning of the request path (case insensitive).
///
/// If the route matches the incoming HTTP request then the arguments from the `PrintfFormat<...>` will be automatically resolved and passed into the supplied `routeHandler`.
///
/// **Supported format chars**
///
/// `%b`: `bool`
/// `%c`: `char`
/// `%s`: `string`
/// `%i`: `int`
/// `%d`: `int64`
/// `%f`: `float`/`double`
/// `%O`: `Guid`
///
/// **Parameters**
///
/// `path`: A format string representing the expected request path.
/// `routeHandler`: A function which accepts a tuple `'T` of the parsed arguments and returns a `HttpHandler` function which will subsequently deal with the request.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeStartsWithCif (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path

    let options = { MatchOptions.IgnoreCase = true; MatchMode = StartsWith }

    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path options (ctx.GetNextPartOfPath())
        |> function
            | None      -> skipPipeline
            | Some args -> routeHandler args next ctx

/// **Description**
///
/// Filters an incoming HTTP request based on a part of the request path (case sensitive).
///
/// Subsequent routing handlers inside the given `handler` function should omit the already validated path.
///
/// **Parameters**
///
/// `path`: A part of an expected request path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let subRoute (path : string) (handler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx: HttpContext) ->
        let subRouting = ctx.Features.Get<ISubRoutingFeature>()
        (routeStartsWith path >=> subRouting.RouteWithSubPath path handler) next ctx

/// **Description**
///
/// Filters an incoming HTTP request based on a part of the request path (case insensitive).
///
/// Subsequent route handlers inside the given `handler` function should omit the already validated path.
///
/// **Parameters**
///
/// `path`: A part of an expected request path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let subRouteCi (path : string) (handler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx: HttpContext) ->
        let subRouting      = ctx.Features.Get<ISubRoutingFeature>()
        let nextPartOfPath  = subRouting.GetNextPartOfPath ctx
        if nextPartOfPath.StartsWith(path, StringComparison.OrdinalIgnoreCase) then
            let matchedPathFragment = nextPartOfPath.[0..path.Length-1]
            subRouting.RouteWithSubPath matchedPathFragment handler next ctx
        else skipPipeline

/// **Description**
///
/// Filters an incoming HTTP request based on a part of the request path (case sensitive).
///
/// If the sub route matches the incoming HTTP request then the arguments from the `PrintfFormat<...>` will be automatically resolved and passed into the supplied `routeHandler`.
///
/// **Supported format chars**
///
/// `%b`: `bool`
/// `%c`: `char`
/// `%s`: `string`
/// `%i`: `int`
/// `%d`: `int64`
/// `%f`: `float`/`double`
/// `%O`: `Guid`
///
/// Subsequent routing handlers inside the given `handler` function should omit the already validated path.
///
/// **Parameters**
///
/// `path`: A format string representing the expected request sub path.
/// `routeHandler`: A function which accepts a tuple `'T` of the parsed arguments and returns a `HttpHandler` function which will subsequently deal with the request.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let subRoutef (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let subRouting   = ctx.Features.Get<ISubRoutingFeature>()
        let paramCount   = (path.Value.Split '/').Length
        let subPathParts = subRouting.GetNextPartOfPath(ctx).Split '/'
        if paramCount > subPathParts.Length then skipPipeline
        else
            let subPath =
                subPathParts
                |> Array.take paramCount
                |> Array.fold (fun state elem ->
                    if String.IsNullOrEmpty elem
                    then state
                    else sprintf "%s/%s" state elem) ""
            tryMatchInput path MatchOptions.Exact subPath
            |> function
                | None      -> skipPipeline
                | Some args -> subRouting.RouteWithSubPath subPath (routeHandler args) next ctx