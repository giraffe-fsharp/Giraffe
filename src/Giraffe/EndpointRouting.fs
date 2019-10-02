module Giraffe.EndpointRouting

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.FSharp.Reflection
open FSharp.Core
open FSharp.Control.Tasks.V2.ContextInsensitive

module private RouteTemplateBuilder =
    let private getConstraint (i : int) (c : char) =
        // Route Constraint Docs:
        // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-3.0#route-constraint-reference
        (dict [
            'b', sprintf "{b%i:int}" i          // bool
            'c', sprintf "{c%i:length(1)}" i    // char
            's', sprintf "{s%i}" i              // string
            'i', sprintf "{i%i:int}" i          // int
            'd', sprintf "{d%i:long}" i         // int64
            'f', sprintf "{f%i:double}" i       // float
            'O', sprintf "{O%i}" i              // ToDo: Guid
            'u', sprintf "{u%i}" i              // ToDo: uint64
        ]).[c]

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

    let private parsers =
        let parseBool     (s : string) = bool.Parse s
        let decodeSlashes (s : string) = s.Replace("%2F", "/").Replace("%2f", "/")
        let parseGuid     (s : string) =
            match s.Length with
            | 22 -> ShortGuid.toGuid s
            | _  -> Guid s

        dict [
            'b', parseBool           >> box  // bool
            'c', char                >> box  // char
            's', decodeSlashes       >> box  // string
            'i', int32               >> box  // int
            'd', int64               >> box  // int64
            'f', float               >> box  // float
            'O', parseGuid           >> box  // Guid
            'u', ShortId.toUInt64    >> box  // uint64
        ]

    let private convertToTuple (formatChars : char list) (routeData : RouteData) =
        let values =
            (routeData.Values.Values, formatChars)
            ||> Seq.map2 (fun v c ->
                let parser = parsers.[c]
                let value  = parser (v.ToString())
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

    let private handleResult result =
        match result with
        | None   -> () // Shall we return here a HTTP 422 Unprocessable Entity?
        | Some _ -> ()

    let createRequestDelegate (handler : HttpHandler) =
        fun (ctx : HttpContext) ->
            task {
                let! result = handler earlyReturn ctx
                return handleResult result
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

let route path handler =
    path, RequestDelegateBuilder.createRequestDelegate handler

let routef (path : PrintfFormat<_,_,_,_, 'T>) (routeHandler : 'T -> HttpHandler) =
    let template, chars = RouteTemplateBuilder.convertToRouteTemplate path
    let requestDelegate = RequestDelegateBuilder.createTokenizedRequestDelegate chars routeHandler
    template, requestDelegate

// ---------------------------
// Convenience Handlers
// ---------------------------

type IRouteBuilder with

    member this.MapGiraffe (routes : (string * RequestDelegate) list) =
        routes
        |> List.iter(fun (routeTemplate, requestDelegate) ->
            this.MapGet(routeTemplate, requestDelegate) |> ignore)