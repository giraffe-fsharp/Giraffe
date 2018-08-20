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
// Private sub route helper functions
// ---------------------------

[<Literal>]
let private RouteKey = "giraffe_route"

let private getSavedSubPath (ctx : HttpContext) =
    if ctx.Items.ContainsKey RouteKey
    then ctx.Items.Item RouteKey |> string |> strOption
    else None

let private getPath (ctx : HttpContext) =
    match getSavedSubPath ctx with
    | Some p when ctx.Request.Path.Value.Contains p -> ctx.Request.Path.Value.[p.Length..]
    | _   -> ctx.Request.Path.Value

let private handlerWithRootedPath (path : string) (handler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let savedSubPath = getSavedSubPath ctx
            ctx.Items.Item RouteKey <- ((savedSubPath |> Option.defaultValue "") + path)
            let! result = handler next ctx
            match result with
            | Some _ -> ()
            | None ->
                match savedSubPath with
                | Some savedSubPath -> ctx.Items.Item   RouteKey <- savedSubPath
                | None              -> ctx.Items.Remove RouteKey |> ignore
            return result
        }

// ---------------------------
// Public routing HttpHandler functions
// ---------------------------

/// **Description**
///
/// Filters an incoming HTTP request based on the port.
///
/// **Parameters**
///
/// - `fns`: List of port to `HttpHandler` mappings
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
                | false, _    -> abort
            else
                abort

/// **Description**
///
/// Filters an incoming HTTP request based on the request path (case sensitive).
///
/// **Parameters**
///
/// - `path`: Request path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let route (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (getPath ctx).Equals path
        then next ctx
        else abort

/// **Description**
///
/// Filters an incoming HTTP request based on the request path (case insensitive).
///
/// **Parameters**
///
/// - `path`: Request path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeCi (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if String.Equals(getPath ctx, path, StringComparison.CurrentCultureIgnoreCase)
        then next ctx
        else abort

/// **Description**
///
/// Filters an incoming HTTP request based on the request path using Regex (case sensitive).
///
/// **Parameters**
///
/// - `path`: Regex path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routex (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let pattern = sprintf "^%s$" path
        let regex   = Regex(pattern, RegexOptions.Compiled)
        let result  = regex.Match (getPath ctx)
        match result.Success with
        | true -> next ctx
        | false -> abort

/// **Description**
///
/// Filters an incoming HTTP request based on the request path using Regex (case insensitive).
///
/// **Parameters**
///
/// - `path`: Regex path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeCix (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let pattern = sprintf "^%s$" path
        let regex   = Regex(pattern, RegexOptions.IgnoreCase ||| RegexOptions.Compiled)
        let result  = regex.Match (getPath ctx)
        match result.Success with
        | true -> next ctx
        | false -> abort

/// **Description**
///
/// Filters an incoming HTTP request based on the request path (case sensitive).
///
/// If the route matches the incoming HTTP request then the arguments from the `PrintfFormat<...>` will be automatically resolved and passed into the supplied `routeHandler`.
///
/// **Supported format chars**
///
/// - `%b`: `bool`
/// - `%c`: `char`
/// - `%s`: `string`
/// - `%i`: `int`
/// - `%d`: `int64`
/// - `%f`: `float`/`double`
/// - `%O`: `Guid`
///
/// **Parameters**
///
/// - `path`: A format string representing the expected request path.
/// - `routeHandler`: A function which accepts a tuple `'T` of the parsed arguments and returns a `HttpHandler` function which will subsequently deal with the request.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routef (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path
    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path (getPath ctx) false
        |> function
            | None      -> abort
            | Some args -> routeHandler args next ctx

/// **Description**
///
/// Filters an incoming HTTP request based on the request path (case insensitive).
///
/// If the route matches the incoming HTTP request then the arguments from the `PrintfFormat<...>` will be automatically resolved and passed into the supplied `routeHandler`.
///
/// **Supported format chars**
///
/// - `%b`: `bool`
/// - `%c`: `char`
/// - `%s`: `string`
/// - `%i`: `int`
/// - `%d`: `int64`
/// - `%f`: `float`/`double`
/// - `%O`: `Guid`
///
/// **Parameters**
///
/// - `path`: A format string representing the expected request path.
/// - `routeHandler`: A function which accepts a tuple `'T` of the parsed arguments and returns a `HttpHandler` function which will subsequently deal with the request.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeCif (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path
    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path (getPath ctx) true
        |> function
            | None      -> abort
            | Some args -> routeHandler args next ctx

/// **Description**
///
/// Filters an incoming HTTP request based on the request path (case insensitive).
///
/// If the route matches the incoming HTTP request then the parameters from the string will be used to create an instance of `'T` and subsequently passed into the supplied `routeHandler`.
///
/// **Parameters**
///
/// - `route`: A string representing the expected request path. Use `{propertyName}` for reserved parameter names which should map to the properties of type `'T`. You can also use valid `Regex` within the `route` string.
/// - `routeHandler`: A function which accepts a tuple `'T` of the parsed parameters and returns a `HttpHandler` function which will subsequently deal with the request.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeBind<'T> (route : string) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let pattern = route.Replace("{", "(?<").Replace("}", ">[^/\n]+)") |> sprintf "^%s$"
        let regex   = Regex(pattern, RegexOptions.IgnoreCase)
        let result  = regex.Match (getPath ctx)
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
            | Error _  -> abort
            | Ok model -> routeHandler model next ctx
        | _ -> abort

/// **Description**
///
/// Filters an incoming HTTP request based on the beginning of the request path (case sensitive).
///
/// **Parameters**
///
/// - `subPath`: The expected beginning of a request path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeStartsWith (subPath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (getPath ctx).StartsWith subPath
        then next ctx
        else abort

/// **Description**
///
/// Filters an incoming HTTP request based on the beginning of the request path (case insensitive).
///
/// **Parameters**
///
/// - `subPath`: The expected beginning of a request path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let routeStartsWithCi (subPath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (getPath ctx).StartsWith(subPath, StringComparison.CurrentCultureIgnoreCase)
        then next ctx
        else abort

/// **Description**
///
/// Filters an incoming HTTP request based on a part of the request path (case sensitive).
///
/// Subsequent routing handlers inside the given `handler` function should omit the already validated path.
///
/// **Parameters**
///
/// - `path`: A part of an expected request path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let subRoute (path : string) (handler : HttpHandler) : HttpHandler =
    routeStartsWith path >=>
    handlerWithRootedPath path handler

/// **Description**
///
/// Filters an incoming HTTP request based on a part of the request path (case insensitive).
///
/// Subsequent route handlers inside the given `handler` function should omit the already validated path.
///
/// **Parameters**
///
/// - `path`: A part of an expected request path.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let subRouteCi (path : string) (handler : HttpHandler) : HttpHandler =
    routeStartsWithCi path >=>
    handlerWithRootedPath path handler

/// **Description**
///
/// Filters an incoming HTTP request based on a part of the request path (case sensitive).
///
/// If the sub route matches the incoming HTTP request then the arguments from the `PrintfFormat<...>` will be automatically resolved and passed into the supplied `routeHandler`.
///
/// **Supported format chars**
///
/// - `%b`: `bool`
/// - `%c`: `char`
/// - `%s`: `string`
/// - `%i`: `int`
/// - `%d`: `int64`
/// - `%f`: `float`/`double`
/// - `%O`: `Guid`
///
/// Subsequent routing handlers inside the given `handler` function should omit the already validated path.
///
/// **Parameters**
///
/// - `path`: A format string representing the expected request sub path.
/// - `routeHandler`: A function which accepts a tuple `'T` of the parsed arguments and returns a `HttpHandler` function which will subsequently deal with the request.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let subRoutef (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
        validateFormat path
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let paramCount   = (path.Value.Split '/').Length
            let subPathParts = (getPath ctx).Split '/'
            if paramCount > subPathParts.Length then abort
            else
                let subPath =
                    subPathParts
                    |> Array.take paramCount
                    |> Array.fold (fun state elem ->
                        if String.IsNullOrEmpty elem
                        then state
                        else sprintf "%s/%s" state elem) ""
                tryMatchInput path subPath false
                |> function
                    | None      -> abort
                    | Some args -> handlerWithRootedPath subPath (routeHandler args) next ctx