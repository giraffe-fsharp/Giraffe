module Giraffe.EndpointRouting

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.FSharp.Reflection
open FSharp.Core
open FSharp.Control.Tasks.V2.ContextInsensitive

// mustAccept

// routePorts
// routex
// routeCix
// routeBind
// routeStartsWith
// routeStartsWithCi
// routeStartsWithf
// routeStartsWithCif
// subRoutef

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

// | Function     | Reason       |
// | ------------ | ------------ |
// | choose       | Endpoint routing is a flat level routing engine                        |
// | route        | ASP.NET Core's endpoint routing doesn't support case-sensitive routes  |
// | routef       | ASP.NET Core's endpoint routing doesn't support case-sensitive routes  |
// | subRoute     | ASP.NET Core's endpoint routing doesn't support case-sensitive routes  |



module private RouteTemplateBuilder =
    let private guidPattern =
        "([0-9A-Fa-f]{8}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{12}|[0-9A-Fa-f]{32}|[-_0-9A-Za-z]{22})"
    let private shortIdPattern =
        "([-_0-9A-Za-z]{10}[048AEIMQUYcgkosw])"

    let private getConstraint (i : int) (c : char) =
        match c with
        | 'b' -> sprintf "{b%i:bool}" i                     // bool
        | 'c' -> sprintf "{c%i:length(1)}" i                // char
        | 's' -> sprintf "{s%i}" i                          // string
        | 'i' -> sprintf "{i%i:int}" i                      // int
        | 'd' -> sprintf "{d%i:long}" i                     // int64
        | 'f' -> sprintf "{f%i:double}" i                   // float
        | 'O' -> sprintf "{O%i:regex(%s)}" i guidPattern    // Guid
        | 'u' -> sprintf "{u%i:regex(%s)}" i shortIdPattern // uint64
        | _   -> failwithf "%c is not a supported route format character." c

    let convertToRouteTemplate (path : PrintfFormat<_,_,_,_, 'T>) =
        let rec convert (i : int) (chars : char list) =
            match chars with
            | '%' :: '%' :: tail ->
                let template, formatChars = convert i tail
                "%" + template, formatChars
            | '%' :: c :: tail ->
                let template, formatChars = convert (i + 1) tail
                let placeholder = getConstraint i c
                placeholder + template, c :: formatChars
            | c :: tail ->
                let template, formatChars = convert i tail
                c.ToString() + template, formatChars
            | [] -> "", []

        path.Value
        |> List.ofSeq
        |> convert 0

module private RequestDelegateBuilder =

    let private tryGetSpecialParser (c : char) =
        let decodeSlashes (s : string) = s.Replace("%2F", "/").Replace("%2f", "/")
        let parseGuid     (s : string) =
            match s.Length with
            | 22 -> ShortGuid.toGuid s
            | _  -> Guid s

        match c with
        | 's' -> Some (decodeSlashes    >> box)
        | 'O' -> Some (parseGuid        >> box)
        | 'u' -> Some (ShortId.toUInt64 >> box)
        | _   -> None

    let private convertToTuple (formatChars : char list) (routeData : RouteData) =
        let values =
            (routeData.Values.Values, formatChars)
            ||> Seq.map2 (fun v c ->
                let value =
                    match tryGetSpecialParser c with
                    | Some p -> p (v.ToString())
                    | None   -> v
                value)
            |> Seq.toArray

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
        | None   -> ctx.SetStatusCode 422
        | Some _ -> ()

    let createRequestDelegate (handler : HttpHandler) =
        fun (ctx : HttpContext) ->
            task {
                let! result = handler earlyReturn ctx
                return handleResult result ctx
            } :> Task
        |> wrapDelegate

    let createTokenizedRequestDelegate (formatChars : char list) (tokenizedHandler : 'T -> HttpHandler) =
        fun (ctx : HttpContext) ->
            task {
                let tuple =
                    ctx.GetRouteData()
                    |> convertToTuple formatChars
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

// HttpVerb -> routeTemplate -> RequestDelegate
type Endpoint = HttpVerb * string * RequestDelegate

let private httpVerb (verb : HttpVerb) (endpoint : Endpoint) =
    let _, routeTemplate, requestDelegate = endpoint
    verb, routeTemplate, requestDelegate

let GET     = httpVerb GET
let POST    = httpVerb POST
let PUT     = httpVerb PUT
let PATCH   = httpVerb PATCH
let DELETE  = httpVerb DELETE
let HEAD    = httpVerb HEAD
let OPTIONS = httpVerb OPTIONS
let TRACE   = httpVerb TRACE
let CONNECT = httpVerb CONNECT

let route (path : string) (handler : HttpHandler) =
    HttpVerb.NotSpecified, path, RequestDelegateBuilder.createRequestDelegate handler

let routef (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) =
    let template, chars = RouteTemplateBuilder.convertToRouteTemplate path
    let requestDelegate = RequestDelegateBuilder.createTokenizedRequestDelegate chars routeHandler
    HttpVerb.NotSpecified, template, requestDelegate

let subRoute (path : string) (endpoints : Endpoint list) =
    endpoints
    |> List.map (fun (v, p, d) -> v, sprintf "%s%s" path p, d)

// ---------------------------
// Convenience Handlers
// ---------------------------

type IRouteBuilder with

    member this.MapGiraffe (endpoints : Endpoint list) =
        endpoints
        |> List.iter(fun (verb, routeTemplate, requestDelegate) ->
            match verb with
            | NotSpecified  -> this.MapRoute(routeTemplate, requestDelegate) |> ignore
            | _             -> this.MapVerb(verb.ToString(), routeTemplate, requestDelegate) |> ignore
        )