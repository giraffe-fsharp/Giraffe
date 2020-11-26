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

[<RequireQualifiedAccess>]
module SubRouting =

    [<Literal>]
    let private RouteKey = "giraffe_route"

    let getSavedPartialPath (ctx : HttpContext) =
        if ctx.Items.ContainsKey RouteKey
        then ctx.Items.Item RouteKey |> string |> strOption
        else None

    let getNextPartOfPath (ctx : HttpContext) =
        match getSavedPartialPath ctx with
        | Some p when ctx.Request.Path.Value.Contains p -> ctx.Request.Path.Value.[p.Length..]
        | _   -> ctx.Request.Path.Value

    let routeWithPartialPath (path : string) (handler : HttpHandler) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let savedPartialPath = getSavedPartialPath ctx
                ctx.Items.Item RouteKey <- ((savedPartialPath |> Option.defaultValue "") + path)
                let! result = handler next ctx
                match result with
                | Some _ -> ()
                | None ->
                    match savedPartialPath with
                    | Some subPath -> ctx.Items.Item   RouteKey <- subPath
                    | None         -> ctx.Items.Remove RouteKey |> ignore
                return result
            }

// ---------------------------
// Public routing HttpHandler functions
// ---------------------------

/// <summary>
/// Filters an incoming HTTP request based on the port.
/// </summary>
/// <param name="fns">List of port to <see cref="HttpHandler"/> mappings</param>
/// <param name="next"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
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

/// <summary>
/// Filters an incoming HTTP request based on the request path (case sensitive).
/// </summary>
/// <param name="path">Request path.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let route (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (SubRouting.getNextPartOfPath ctx).Equals path
        then next ctx
        else skipPipeline

/// <summary>
/// Filters an incoming HTTP request based on the request path (case insensitive).
/// </summary>
/// <param name="path">Request path.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let routeCi (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if String.Equals(SubRouting.getNextPartOfPath ctx, path, StringComparison.OrdinalIgnoreCase)
        then next ctx
        else skipPipeline

/// <summary>
/// Filters an incoming HTTP request based on the request path using Regex (case sensitive).
/// </summary>
/// <param name="path">Regex path.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let routex (path : string) : HttpHandler =
    let pattern = sprintf "^%s$" path
    let regex   = Regex(pattern, RegexOptions.Compiled)
    fun (next : HttpFunc) (ctx : Microsoft.AspNetCore.Http.HttpContext) ->
        let result = regex.Match (SubRouting.getNextPartOfPath ctx)
        match result.Success with
        | true  -> next ctx
        | false -> skipPipeline

/// <summary>
/// Filters an incoming HTTP request based on the request path using Regex (case sensitive).
///
/// If the route matches the incoming HTTP request then the Regex groups will be passed into the supplied `routeHandler`.
///
/// This is similar to routex but also allows to use matched strings as parameters for a controller.
/// </summary>
/// <param name="path">Regex path.</param>
/// <param name="routeHandler">A function which accepts a string sequence of the matched groups and returns a `HttpHandler` function which will subsequently deal with the request.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let routexp (path : string) (routeHandler : seq<string> -> HttpHandler): HttpHandler =
    let pattern = sprintf "^%s$" path
    let regex   = Regex(pattern, RegexOptions.Compiled)

    fun (next : HttpFunc) (ctx : Microsoft.AspNetCore.Http.HttpContext) ->
        let result  = regex.Match (SubRouting.getNextPartOfPath ctx)
        match result.Success with
        | true  ->
            let args = result.Groups |> Seq.map (fun x -> x.Value)
            routeHandler args next ctx
        | false -> skipPipeline

/// <summary>
/// Filters an incoming HTTP request based on the request path using Regex (case insensitive).
/// </summary>
/// <param name="path">Regex path.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let routeCix (path : string) : HttpHandler =
    let pattern = sprintf "^%s$" path
    let regex   = Regex(pattern, RegexOptions.IgnoreCase ||| RegexOptions.Compiled)
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let result = regex.Match (SubRouting.getNextPartOfPath ctx)
        match result.Success with
        | true  -> next ctx
        | false -> skipPipeline

/// <summary>
/// Filters an incoming HTTP request based on the request path (case sensitive).
/// If the route matches the incoming HTTP request then the arguments from the <see cref="Microsoft.FSharp.Core.PrintfFormat"/> will be automatically resolved and passed into the supplied routeHandler.
///
/// Supported format chars**
///
/// %b: bool
/// %c: char
/// %s: string
/// %i: int
/// %d: int64
/// %f: float/double
/// %O: Guid
/// </summary>
/// <param name="path">A format string representing the expected request path.</param>
/// <param name="routeHandler">A function which accepts a tuple 'T of the parsed arguments and returns a <see cref="HttpHandler"/> function which will subsequently deal with the request.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let routef (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path
    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path MatchOptions.Exact (SubRouting.getNextPartOfPath ctx)
        |> function
            | None      -> skipPipeline
            | Some args -> routeHandler args next ctx

/// <summary>
/// Filters an incoming HTTP request based on the request path.
/// If the route matches the incoming HTTP request then the arguments from the <see cref="Microsoft.FSharp.Core.PrintfFormat"/> will be automatically resolved and passed into the supplied routeHandler.
///
/// Supported format chars**
///
/// %b: bool
/// %c: char
/// %s: string
/// %i: int
/// %d: int64
/// %f: float/double
/// %O: Guid
/// </summary>
/// <param name="path">A format string representing the expected request path.</param>
/// <param name="routeHandler">A function which accepts a tuple 'T of the parsed arguments and returns a <see cref="HttpHandler"/> function which will subsequently deal with the request.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let routeCif (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path
    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path MatchOptions.IgnoreCaseExact (SubRouting.getNextPartOfPath ctx)
        |> function
            | None      -> skipPipeline
            | Some args -> routeHandler args next ctx

/// <summary>
/// Filters an incoming HTTP request based on the request path (case insensitive).
/// If the route matches the incoming HTTP request then the parameters from the string will be used to create an instance of 'T and subsequently passed into the supplied routeHandler.
/// </summary>
/// <param name="route">A string representing the expected request path. Use {propertyName} for reserved parameter names which should map to the properties of type 'T. You can also use valid Regex within the route string.</param>
/// <param name="routeHandler">A function which accepts a tuple 'T of the parsed parameters and returns a <see cref="HttpHandler"/> function which will subsequently deal with the request.</param>
/// <typeparam name="'T"></typeparam>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let routeBind<'T> (route : string) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    let pattern = route.Replace("{", "(?<").Replace("}", ">[^/\n]+)") |> sprintf "^%s$"
    let regex   = Regex(pattern, RegexOptions.IgnoreCase)
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let result = regex.Match (SubRouting.getNextPartOfPath ctx)
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

/// <summary>
/// Filters an incoming HTTP request based on the beginning of the request path (case sensitive).
/// </summary>
/// <param name="subPath">The expected beginning of a request path.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let routeStartsWith (subPath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (SubRouting.getNextPartOfPath ctx).StartsWith subPath
        then next ctx
        else skipPipeline

/// <summary>
/// Filters an incoming HTTP request based on the beginning of the request path (case insensitive).
/// </summary>
/// <param name="subPath">The expected beginning of a request path.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let routeStartsWithCi (subPath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (SubRouting.getNextPartOfPath ctx).StartsWith(subPath, StringComparison.OrdinalIgnoreCase)
        then next ctx
        else skipPipeline

/// <summary>
/// Filters an incoming HTTP request based on the beginning of the request path (case sensitive).
/// If the route matches the incoming HTTP request then the arguments from the <see cref="Microsoft.FSharp.Core.PrintfFormat"/> will be automatically resolved and passed into the supplied routeHandler.
///
/// Supported format chars**
///
/// %b: bool
/// %c: char
/// %s: string
/// %i: int
/// %d: int64
/// %f: float/double
/// %O: Guid
/// </summary>
/// <param name="path">A format string representing the expected request path.</param>
/// <param name="routeHandler">A function which accepts a tuple 'T of the parsed arguments and returns a <see cref="HttpHandler"/> function which will subsequently deal with the request.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let routeStartsWithf (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path

    let options = { MatchOptions.IgnoreCase = false; MatchMode = StartsWith }

    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path options (SubRouting.getNextPartOfPath ctx)
        |> function
            | None      -> skipPipeline
            | Some args -> routeHandler args next ctx

/// <summary>
/// Filters an incoming HTTP request based on the beginning of the request path (case insensitive).
/// If the route matches the incoming HTTP request then the arguments from the <see cref="Microsoft.FSharp.Core.PrintfFormat"/> will be automatically resolved and passed into the supplied routeHandler.
///
/// Supported format chars**
///
/// %b: bool
/// %c: char
/// %s: string
/// %i: int
/// %d: int64
/// %f: float/double
/// %O: Guid
/// </summary>
/// <param name="path">A format string representing the expected request path.</param>
/// <param name="routeHandler">A function which accepts a tuple 'T of the parsed arguments and returns a <see cref="HttpHandler"/> function which will subsequently deal with the request.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let routeStartsWithCif (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path

    let options = { MatchOptions.IgnoreCase = true; MatchMode = StartsWith }

    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path options (SubRouting.getNextPartOfPath ctx)
        |> function
            | None      -> skipPipeline
            | Some args -> routeHandler args next ctx

/// <summary>
/// Filters an incoming HTTP request based on a part of the request path (case sensitive).
/// Subsequent route handlers inside the given handler function should omit the already validated path.
/// </summary>
/// <param name="path">A part of an expected request path.</param>
/// <param name="handler">A Giraffe <see cref="HttpHandler"/> function.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let subRoute (path : string) (handler : HttpHandler) : HttpHandler =
    routeStartsWith path >=>
    SubRouting.routeWithPartialPath path handler

/// <summary>
/// Filters an incoming HTTP request based on a part of the request path (case insensitive).
/// Subsequent route handlers inside the given handler function should omit the already validated path.
/// </summary>
/// <param name="path">A part of an expected request path.</param>
/// <param name="handler">A Giraffe <see cref="HttpHandler"/> function.</param>
/// <param name="next"></param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let subRouteCi (path : string) (handler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx: HttpContext) ->
        let nextPartOfPath = SubRouting.getNextPartOfPath ctx
        if nextPartOfPath.StartsWith(path, StringComparison.OrdinalIgnoreCase) then
            let matchedPathFragment = nextPartOfPath.[0..path.Length-1]
            SubRouting.routeWithPartialPath matchedPathFragment handler next ctx
        else skipPipeline

/// <summary>
/// Filters an incoming HTTP request based on a part of the request path (case sensitive).
/// If the sub route matches the incoming HTTP request then the arguments from the <see cref="Microsoft.FSharp.Core.PrintfFormat"/> will be automatically resolved and passed into the supplied routeHandler.
///
/// Supported format chars
///
/// %b: bool
/// %c: char
/// %s: string
/// %i: int
/// %d: int64
/// %f: float/double
/// %O: Guid
///
/// Subsequent routing handlers inside the given handler function should omit the already validated path.
/// </summary>
/// <param name="path">A format string representing the expected request sub path.</param>
/// <param name="routeHandler">A function which accepts a tuple 'T of the parsed arguments and returns a <see cref="HttpHandler"/> function which will subsequently deal with the request.</param>
/// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
let subRoutef (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
        validateFormat path
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let paramCount   = (path.Value.Split '/').Length
            let subPathParts = (SubRouting.getNextPartOfPath ctx).Split '/'
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
                    | Some args -> SubRouting.routeWithPartialPath subPath (routeHandler args) next ctx