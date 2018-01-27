[<AutoOpen>]
module Giraffe.Routing

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open Microsoft.AspNetCore.Http
open Newtonsoft.Json.Linq
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

/// Filters an incoming HTTP request based on the port.
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

/// Filters an incoming HTTP request based on the request path (case sensitive).
let route (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (getPath ctx).Equals path
        then next ctx
        else abort

/// Filters an incoming HTTP request based on the request path (case sensitive).
/// The arguments from the format string will be automatically resolved when the
/// route matches and subsequently passed into the supplied routeHandler.
///
/// Supported format chars:
/// %b -> bool
/// %c -> char
/// %s -> string
/// %i -> int
/// %d -> int64
/// %f -> float/double
/// %O -> Guid
let routef (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path
    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path (getPath ctx) false
        |> function
            | None      -> abort
            | Some args -> routeHandler args next ctx

/// Filters an incoming HTTP request based on the request path (case insensitive).
let routeCi (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if String.Equals(getPath ctx, path, StringComparison.CurrentCultureIgnoreCase)
        then next ctx
        else abort

/// Filters an incoming HTTP request based on the request path (case insensitive).
/// The arguments from the format string will be automatically resolved when the
/// route matches and subsequently passed into the supplied routeHandler.
///
/// Supported format chars:
/// %b -> bool
/// %c -> char
/// %s -> string
/// %i -> int
/// %d -> int64
/// %f -> float/double
/// %O -> Guid
let routeCif (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    validateFormat path
    fun (next : HttpFunc) (ctx : HttpContext) ->
        tryMatchInput path (getPath ctx) true
        |> function
            | None      -> abort
            | Some args -> routeHandler args next ctx

/// Filters an incoming HTTP request based on the request path (case insensitive).
/// The parameters from the string will be used to create an instance of 'T
/// and subsequently passed into the supplied routeHandler.
let routeBind<'T> (route: string) (routeHandler : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let pattern = route.Replace("{", "(?<").Replace("}", ">[^/\n]+)") |> sprintf "%s$"
        let regex = Regex(pattern, RegexOptions.IgnoreCase)
        let mtch = regex.Match ctx.Request.Path.Value
        match mtch.Success with
        | true ->
            let groups = mtch.Groups
            let o =
                regex.GetGroupNames()
                |> Array.skip 1
                |> Array.map (fun x -> x, groups.[x].Value)
                |> Array.filter (fun (_, x) -> x.Length > 0)
                |> dict
                |> JObject.FromObject
                |> fun jo -> jo.ToObject<'T>()
            routeHandler o next ctx
        | _ -> abort

/// Filters an incoming HTTP request based on the beginning of the request path (case sensitive).
let routeStartsWith (subPath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (getPath ctx).StartsWith subPath
        then next ctx
        else abort

/// Filters an incoming HTTP request based on the beginning of the request path (case insensitive).
let routeStartsWithCi (subPath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (getPath ctx).StartsWith(subPath, StringComparison.CurrentCultureIgnoreCase)
        then next ctx
        else abort

/// Filters an incoming HTTP request based on a part of the request path (case sensitive).
/// Subsequent route handlers inside the given handler function should omit the already validated path.
let subRoute (path : string) (handler : HttpHandler) : HttpHandler =
    routeStartsWith path >=>
    handlerWithRootedPath path handler

/// Filters an incoming HTTP request based on a part of the request path (case insensitive).
/// Subsequent route handlers inside the given handler function should omit the already validated path.
let subRouteCi (path : string) (handler : HttpHandler) : HttpHandler =
    routeStartsWithCi path >=>
    handlerWithRootedPath path handler