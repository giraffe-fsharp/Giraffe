module Giraffe.EndpointRouting

open System
open System.Net
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.FSharp.Reflection
open FSharp.Core
open FSharp.Control.Tasks.V2.ContextInsensitive

// ToDo:
// ---------------------

// mustAccept
// routePorts
// routex
// routeCix
// routeBind

// Implemented
// ---------------------

// | Before                 | Now                   |
// | ---------------------- | --------------------- |
// | routeCi                | route                 |
// | routeCif               | routef                |
// | subRouteCi             | subRoute              |
// | GET, POST, PUT, etc.   | GET, POST, PUT, ...   |

// Not Supported
// ---------------------

// | Function            | Reason                   |
// | ------------------- | ------------------------ |
// | choose              | Not possible, endpoint routing is a flat level routing engine                     |
// | route               | ASP.NET Core's endpoint routing doesn't support case-sensitive routes             |
// | routef              | ASP.NET Core's endpoint routing doesn't support case-sensitive routes             |
// | subRoute            | ASP.NET Core's endpoint routing doesn't support case-sensitive routes             |
// | subRoutef           | Can't do. Use subRoute with a custom route template and read value from RouteData |
// | routeStartsWith     | Use subRoute             |
// | routeStartsWithCi   | Use subRoute             |
// | routeStartsWithf    | Can't do, see subRoutef  |
// | routeStartsWithCif  | Can't do, see subRoutef  |


module private RouteTemplateBuilder =
    let private guidPattern =
        "([0-9A-Fa-f]{8}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{12}|[0-9A-Fa-f]{32}|[-_0-9A-Za-z]{22})"
    let private shortIdPattern =
        "([-_0-9A-Za-z]{10}[048AEIMQUYcgkosw])"

    let private getConstraint (i : int) (c : char) =
        let name = sprintf "%c%i" c i
        match c with
        | 'b' -> name, sprintf "{%s:bool}" name                     // bool
        | 'c' -> name, sprintf "{%s:length(1)}" name                // char
        | 's' -> name, sprintf "{%s}" name                          // string
        | 'i' -> name, sprintf "{%s:int}" name                      // int
        | 'd' -> name, sprintf "{%s:long}" name                     // int64
        | 'f' -> name, sprintf "{%s:double}" name                   // float
        | 'O' -> name, sprintf "{%s:regex(%s)}" name guidPattern    // Guid
        | 'u' -> name, sprintf "{%s:regex(%s)}" name shortIdPattern // uint64
        | _   -> failwithf "%c is not a supported route format character." c

    let convertToRouteTemplate (path : PrintfFormat<_,_,_,_, 'T>) =
        let rec convert (i : int) (chars : char list) =
            match chars with
            | '%' :: '%' :: tail ->
                let template, mappings = convert i tail
                "%" + template, mappings
            | '%' :: c :: tail ->
                let template, mappings = convert (i + 1) tail
                let placeholderName, placeholderTemplate = getConstraint i c
                placeholderTemplate + template, (placeholderName, c) :: mappings
            | c :: tail ->
                let template, mappings = convert i tail
                c.ToString() + template, mappings
            | [] -> "", []

        path.Value
        |> List.ofSeq
        |> convert 0

module private RequestDelegateBuilder =

    let private tryGetParser (c : char) =
        let decodeSlashes (s : string) = s.Replace("%2F", "/").Replace("%2f", "/")
        let parseGuid     (s : string) =
            match s.Length with
            | 22 -> ShortGuid.toGuid s
            | _  -> Guid s

        match c with
        | 's' -> Some (decodeSlashes    >> box)
        | 'i' -> Some (int              >> box)
        | 'b' -> Some (bool.Parse       >> box)
        | 'c' -> Some (char             >> box)
        | 'd' -> Some (int64            >> box)
        | 'f' -> Some (float            >> box)
        | 'O' -> Some (parseGuid        >> box)
        | 'u' -> Some (ShortId.toUInt64 >> box)
        | _   -> None

    let private convertToTuple (mappings : (string * char) list) (routeData : RouteData) =
        let values =
            mappings
            |> List.map (fun (placeholderName, formatChar) ->
                let routeValue = routeData.Values.[placeholderName]
                match tryGetParser formatChar with
                | Some parseFn -> parseFn (routeValue.ToString())
                | None         -> routeValue)
            |> List.toArray

        let result =
            match values.Length with
            | 1 -> values.[0]
            | _ ->
                let types =
                    values
                    |> Array.map (fun v -> v.GetType())
                let tupleType = FSharpType.MakeTupleType types
                FSharpValue.MakeTuple(values, tupleType)
        result

    let private wrapDelegate f = new RequestDelegate(f)

    let private handleResult (result : HttpContext option) (ctx : HttpContext) =
        match result with
        | None   -> ctx.SetStatusCode (int HttpStatusCode.UnprocessableEntity)
        | Some _ -> ()

    let createRequestDelegate (handler : HttpHandler) =
        fun (ctx : HttpContext) ->
            task {
                let! result = handler earlyReturn ctx
                return handleResult result ctx
            } :> Task
        |> wrapDelegate

    let createTokenizedRequestDelegate (mappings : (string * char) list) (tokenizedHandler : 'T -> HttpHandler) =
        fun (ctx : HttpContext) ->
            task {
                let tuple =
                    ctx.GetRouteData()
                    |> convertToTuple mappings
                    :?> 'T
                let! result = tokenizedHandler tuple earlyReturn ctx
                return handleResult result
            } :> Task
        |> wrapDelegate

// ---------------------------
// Overriding Handlers
// ---------------------------

type HttpVerb =
    | GET | POST | PUT | PATCH | DELETE | HEAD | OPTIONS | TRACE | CONNECT
    | NotSpecified

    override this.ToString() =
        match this with
        | GET        -> "GET"
        | POST       -> "POST"
        | PUT        -> "PUT"
        | PATCH      -> "PATCH"
        | DELETE     -> "DELETE"
        | HEAD       -> "HEAD"
        | OPTIONS    -> "OPTIONS"
        | TRACE      -> "TRACE"
        | CONNECT    -> "CONNECT"
        | _          -> ""

type RouteTemplate = string

type Endpoint =
    | SingleEndpoint of HttpVerb * RouteTemplate * RequestDelegate
    | NestedEndpoint of RouteTemplate * Endpoint list

let inline (=>) (fx : Endpoint -> Endpoint) (x : Endpoint) = fx x

let rec private httpVerb
    (verb     : HttpVerb)
    (endpoint : Endpoint) : Endpoint =
    match endpoint with
    | SingleEndpoint (_, routeTemplate, requestDelegate) ->
        SingleEndpoint (verb, routeTemplate, requestDelegate)
    | NestedEndpoint (routeTemplate, endpoints) ->
        NestedEndpoint (routeTemplate, endpoints |> List.map (httpVerb verb))

let GET     = httpVerb GET
let POST    = httpVerb POST
let PUT     = httpVerb PUT
let PATCH   = httpVerb PATCH
let DELETE  = httpVerb DELETE
let HEAD    = httpVerb HEAD
let OPTIONS = httpVerb OPTIONS
let TRACE   = httpVerb TRACE
let CONNECT = httpVerb CONNECT

let route
    (path     : string)
    (handler  : HttpHandler) : Endpoint =
    SingleEndpoint (HttpVerb.NotSpecified, path, RequestDelegateBuilder.createRequestDelegate handler)

let routef
    (path         : PrintfFormat<_,_,_,_, 'T>)
    (routeHandler : 'T -> HttpHandler) : Endpoint =
    let template, mappings = RouteTemplateBuilder.convertToRouteTemplate path
    let requestDelegate = RequestDelegateBuilder.createTokenizedRequestDelegate mappings routeHandler
    SingleEndpoint (HttpVerb.NotSpecified, template, requestDelegate)

let subRoute
    (path      : string)
    (endpoints : Endpoint list) : Endpoint =
    NestedEndpoint (path, endpoints)

// ---------------------------
// Convenience Handlers
// ---------------------------

type IRouteBuilder with

    member private this.MapSingleEndpoint (singleEndpoint : HttpVerb * RouteTemplate * RequestDelegate) =
        let verb, routeTemplate, requestDelegate = singleEndpoint
        match verb with
        | NotSpecified  -> this.MapRoute(routeTemplate, requestDelegate) |> ignore
        | _             -> this.MapVerb(verb.ToString(), routeTemplate, requestDelegate) |> ignore

    member private this.MapNestedEndpoint (nestedEndpoint : RouteTemplate * Endpoint list) =
        let subRouteTemplate, endpoints = nestedEndpoint
        let routeTemplate = sprintf "%s%s" subRouteTemplate
        endpoints
        |> List.iter (
            fun endpoint ->
                match endpoint with
                | SingleEndpoint (v, t, d) -> this.MapSingleEndpoint(v, routeTemplate t, d)
                | NestedEndpoint (t, e)    -> this.MapNestedEndpoint(routeTemplate t, e)
        )

    member this.MapGiraffeEndpoints (endpoints : Endpoint list) =
        endpoints
        |> List.iter(
            fun endpoint ->
                match endpoint with
                | SingleEndpoint (v, t, d)  -> this.MapSingleEndpoint (v, t, d)
                | NestedEndpoint (t, e)     -> this.MapNestedEndpoint (t, e)
        )