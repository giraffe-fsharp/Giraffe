module Giraffe.BasicRouter

open System
open System.Threading.Tasks
open System.Text.RegularExpressions
open Newtonsoft.Json.Linq
open FSharp.Core.Printf
open Microsoft.AspNetCore.Http
open System.Collections.Generic
open Microsoft.FSharp.Reflection
open Giraffe.HttpHandlers
open Giraffe.Common
open Giraffe.FormatExpressions

let private abort  : HttpFuncResult = Task.FromResult None
let private finish : HttpFunc       = Some >> Task.FromResult

/// ---------------------------
/// Sub route helper functions
/// ---------------------------
[<Literal>] let private RouteKey = "giraffe_route"

let private getSavedSubPath (ctx : HttpContext) =
    if ctx.Items.ContainsKey RouteKey
    then ctx.Items.Item RouteKey |> string |> strOption
    else None

let private getPath (ctx : HttpContext) =
    match getSavedSubPath ctx with
    | Some p -> ctx.Request.Path.ToString().[p.Length..]
    | None   -> ctx.Request.Path.ToString()

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

/// Filters an incoming HTTP request based on the request path (case sensitive).
let route (path : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if (getPath ctx).Equals path
        then next ctx
        else abort

/// Filters an incoming HTTP request based on the request path (case sensitive).
/// The arguments from the format string will be automatically resolved when the
/// route matches and subsequently passed into the supplied routeHandler.
let routef (path : StringFormat<_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
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
let routeCif (path : StringFormat<_, 'T>) (routeHandler : 'T -> HttpHandler) : HttpHandler =
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
        let pattern = route.Replace("{", "(?<").Replace("}", ">.+)") |> sprintf "^%s$"
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