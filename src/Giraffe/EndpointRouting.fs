namespace Giraffe.EndpointRouting

open System
open System.Net
open System.Threading.Tasks
open System.Runtime.CompilerServices
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.FSharp.Reflection
open FSharp.Core
open Giraffe

module RouteTemplateBuilder =
    // We use this regex route constraint to be compatible with Giraffe's default router,
    // which supports ShortGuid's.
    // More information on ASP.NET route constraints:
    // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-8.0#route-constraints
    let private guidPattern =
        "^[0-9A-Fa-f]{{8}}-[0-9A-Fa-f]{{4}}-[0-9A-Fa-f]{{4}}-[0-9A-Fa-f]{{4}}-[0-9A-Fa-f]{{12}}$|^[0-9A-Fa-f]{{32}}$|^[-_0-9A-Za-z]{{22}}$"

    let private shortIdPattern = "([-_0-9A-Za-z]{{10}}[048AEIMQUYcgkosw])"

    let private getConstraint (i: int) (c: char) (name: string option) =
        let name = Option.defaultValue (sprintf "%c%i" c i) name

        match c with
        | 'b' -> name, sprintf "{%s:bool}" name // bool
        | 'c' -> name, sprintf "{%s:length(1)}" name // char
        | 's' -> name, sprintf "{%s}" name // string
        | 'i' -> name, sprintf "{%s:int}" name // int
        | 'd' -> name, sprintf "{%s:long}" name // int64
        | 'f' -> name, sprintf "{%s:double}" name // float
        | 'O' -> name, sprintf "{%s:regex(%s)}" name guidPattern // Guid
        | 'u' -> name, sprintf "{%s:regex(%s)}" name shortIdPattern // uint64
        | _ -> failwithf "%c is not a supported route format character." c

    let convertToRouteTemplate (path: PrintfFormat<_, _, _, _, 'T>) =
        let rec convert (i: int) (chars: char list) =
            match chars with
            | '%' :: '%' :: tail ->
                let template, mappings = convert i tail
                "%" + template, mappings
            | '%' :: c :: tail ->
                match tail with
                | ':' :: stail ->
                    let splitIndex = stail |> List.tryFindIndex (fun c -> c = '/')

                    match splitIndex with
                    | Some splitIndex ->
                        let name, newTail = stail |> List.splitAt splitIndex

                        let placeholderName, placeholderTemplate =
                            getConstraint i c (Some(System.String.Concat(Array.ofList (name))))

                        let template, mappings = convert (i + 1) newTail
                        placeholderTemplate + template, (placeholderName, c) :: mappings
                    | None ->
                        let placeholderName, placeholderTemplate =
                            getConstraint i c (Some(System.String.Concat(Array.ofList (stail))))

                        let template, mappings = convert (i + 1) []
                        placeholderTemplate + template, (placeholderName, c) :: mappings
                | _ ->
                    let placeholderName, placeholderTemplate = getConstraint i c None
                    let template, mappings = convert (i + 1) tail
                    placeholderTemplate + template, (placeholderName, c) :: mappings
            | c :: tail ->
                let template, mappings = convert i tail
                c.ToString() + template, mappings
            | [] -> "", []

        path.Value |> List.ofSeq |> convert 0

module private RequestDelegateBuilder =

    let private tryGetParser (c: char) =
        let decodeSlashes (s: string) =
            s.Replace("%2F", "/").Replace("%2f", "/")

        let parseGuid (s: string) =
            match s.Length with
            | 22 -> ShortGuid.toGuid s
            | _ -> Guid s

        match c with
        | 's' -> Some(decodeSlashes >> box)
        | 'i' -> Some(int >> box)
        | 'b' -> Some(bool.Parse >> box)
        | 'c' -> Some(char >> box)
        | 'd' -> Some(int64 >> box)
        | 'f' -> Some(float >> box)
        | 'O' -> Some(parseGuid >> box)
        | 'u' -> Some(ShortId.toUInt64 >> box)
        | _ -> None

    let private convertToTuple (mappings: (string * char) list) (routeData: RouteData) =
        let values =
            mappings
            |> List.map (fun (placeholderName, formatChar) ->
                let routeValue = routeData.Values.[placeholderName]

                match tryGetParser formatChar with
                | Some parseFn -> parseFn (routeValue.ToString())
                | None -> routeValue
            )
            |> List.toArray

        let result =
            match values.Length with
            | 1 -> values.[0]
            | _ ->
                let types = values |> Array.map (fun v -> v.GetType())
                let tupleType = FSharpType.MakeTupleType types
                FSharpValue.MakeTuple(values, tupleType)

        result

    let private wrapDelegate f = new RequestDelegate(f)

    let private handleResult (result: HttpContext option) (ctx: HttpContext) =
        match result with
        | None -> ctx.SetStatusCode(int HttpStatusCode.UnprocessableEntity)
        | Some _ -> ()

    let createRequestDelegate (handler: HttpHandler) =
        let func: HttpFunc = handler earlyReturn

        fun (ctx: HttpContext) ->
            task {
                let! result = func ctx
                return handleResult result ctx
            }
            :> Task
        |> wrapDelegate

    let createTokenizedRequestDelegate (mappings: (string * char) list) (tokenizedHandler: 'T -> HttpHandler) =
        fun (ctx: HttpContext) ->
            task {
                let tuple = ctx.GetRouteData() |> convertToTuple mappings :?> 'T
                let! result = tokenizedHandler tuple earlyReturn ctx
                return handleResult result
            }
            :> Task
        |> wrapDelegate

// ---------------------------
// Overriding Router Handlers
// ---------------------------

[<AutoOpen>]
module Routers =
    open System.Collections.Generic
    open Microsoft.Extensions.Primitives

    type HttpVerb =
        | GET
        | POST
        | PUT
        | PATCH
        | DELETE
        | HEAD
        | OPTIONS
        | TRACE
        | CONNECT
        | NotSpecified

        override this.ToString() =
            match this with
            | GET -> "GET"
            | POST -> "POST"
            | PUT -> "PUT"
            | PATCH -> "PATCH"
            | DELETE -> "DELETE"
            | HEAD -> "HEAD"
            | OPTIONS -> "OPTIONS"
            | TRACE -> "TRACE"
            | CONNECT -> "CONNECT"
            | _ -> ""

    type RouteTemplate = string
    type RouteTemplateMappings = list<string * char>
    type ConfigureEndpoint = IEndpointConventionBuilder -> IEndpointConventionBuilder

    type Endpoint =
        | SimpleEndpoint of HttpVerb * RouteTemplate * HttpHandler * ConfigureEndpoint
        | TemplateEndpoint of
            HttpVerb *
            RouteTemplate *
            RouteTemplateMappings *
            (obj -> HttpHandler) *
            ConfigureEndpoint
        | NestedEndpoint of RouteTemplate * Endpoint list * ConfigureEndpoint
        | MultiEndpoint of Endpoint list

    let rec private applyHttpVerbToEndpoint (verb: HttpVerb) (endpoint: Endpoint) : Endpoint =
        match endpoint with
        | SimpleEndpoint(_, routeTemplate, requestDelegate, metadata) ->
            SimpleEndpoint(verb, routeTemplate, requestDelegate, metadata)
        | TemplateEndpoint(_, routeTemplate, mappings, requestDelegate, metadata) ->
            TemplateEndpoint(verb, routeTemplate, mappings, requestDelegate, metadata)
        | NestedEndpoint(routeTemplate, endpoints, metadata) ->
            NestedEndpoint(routeTemplate, endpoints |> List.map (applyHttpVerbToEndpoint verb), metadata)
        | MultiEndpoint endpoints -> endpoints |> List.map (applyHttpVerbToEndpoint verb) |> MultiEndpoint

    let rec private applyHttpVerbToEndpoints (verb: HttpVerb) (endpoints: Endpoint list) : Endpoint =
        endpoints
        |> List.map (fun endpoint ->
            match endpoint with
            | SimpleEndpoint(_, routeTemplate, requestDelegate, metadata) ->
                SimpleEndpoint(verb, routeTemplate, requestDelegate, metadata)
            | TemplateEndpoint(_, routeTemplate, mappings, requestDelegate, metadata) ->
                TemplateEndpoint(verb, routeTemplate, mappings, requestDelegate, metadata)
            | NestedEndpoint(routeTemplate, endpoints, metadata) ->
                NestedEndpoint(routeTemplate, endpoints |> List.map (applyHttpVerbToEndpoint verb), metadata)
            | MultiEndpoint endpoints -> applyHttpVerbToEndpoints verb endpoints
        )
        |> MultiEndpoint

    let rec private applyHttpVerbsToEndpoints (verbs: HttpVerb list) (endpoints: Endpoint list) : Endpoint =
        endpoints
        |> List.map (fun endpoint ->
            match endpoint with
            | SimpleEndpoint(_, routeTemplate, requestDelegate, metadata) ->
                verbs
                |> List.map (fun verb -> SimpleEndpoint(verb, routeTemplate, requestDelegate, metadata))
                |> MultiEndpoint
            | TemplateEndpoint(_, routeTemplate, mappings, requestDelegate, metadata) ->
                verbs
                |> List.map (fun verb -> TemplateEndpoint(verb, routeTemplate, mappings, requestDelegate, metadata))
                |> MultiEndpoint
            | NestedEndpoint(routeTemplate, endpoints, metadata) ->
                verbs
                |> List.map (fun verb ->
                    NestedEndpoint(routeTemplate, endpoints |> List.map (applyHttpVerbToEndpoint verb), metadata)
                )
                |> MultiEndpoint
            | MultiEndpoint endpoints ->
                verbs
                |> List.map (fun verb -> applyHttpVerbToEndpoints verb endpoints)
                |> MultiEndpoint
        )
        |> MultiEndpoint

    let GET_HEAD = applyHttpVerbsToEndpoints [ GET; HEAD ]

    let GET = applyHttpVerbToEndpoints GET
    let POST = applyHttpVerbToEndpoints POST
    let PUT = applyHttpVerbToEndpoints PUT
    let PATCH = applyHttpVerbToEndpoints PATCH
    let DELETE = applyHttpVerbToEndpoints DELETE
    let HEAD = applyHttpVerbToEndpoints HEAD
    let OPTIONS = applyHttpVerbToEndpoints OPTIONS
    let TRACE = applyHttpVerbToEndpoints TRACE
    let CONNECT = applyHttpVerbToEndpoints CONNECT

    let routeWithExtensions (configureEndpoint: ConfigureEndpoint) (path: string) (handler: HttpHandler) : Endpoint =
        SimpleEndpoint(HttpVerb.NotSpecified, path, handler, configureEndpoint)

    let route (path: string) (handler: HttpHandler) : Endpoint =
        routeWithExtensions (id) (path) (handler)

    let routefWithExtensions
        (configureEndpoint: ConfigureEndpoint)
        (path: PrintfFormat<_, _, _, _, 'T>)
        (routeHandler: 'T -> HttpHandler)
        : Endpoint =
        let template, mappings = RouteTemplateBuilder.convertToRouteTemplate path

        let boxedHandler (o: obj) =
            let t = o :?> 'T
            routeHandler t

        TemplateEndpoint(HttpVerb.NotSpecified, template, mappings, boxedHandler, configureEndpoint)

    let routef (path: PrintfFormat<_, _, _, _, 'T>) (routeHandler: 'T -> HttpHandler) : Endpoint =
        routefWithExtensions (id) (path) (routeHandler)

    let routeBindWithExtensions<'T>
        (configureEndpoint: ConfigureEndpoint)
        (path: string)
        (routeHandler: 'T -> HttpHandler)
        : Endpoint =

        let bindRouteHandler (handler: 'T -> HttpHandler) : HttpHandler =
            fun next ctx ->
                let routeData =
                    ctx.GetRouteData().Values
                    |> Seq.map (fun kvp -> KeyValuePair(kvp.Key, StringValues(kvp.Value :?> string)))
                    |> Dictionary<string, StringValues>

                match ModelParser.tryParse<'T> None routeData with
                | Ok model -> handler model next ctx
                | Error _ -> skipPipeline

        let newHandler = (bindRouteHandler routeHandler)

        SimpleEndpoint(HttpVerb.NotSpecified, path, newHandler, configureEndpoint)

    let routeBind<'T> (path: string) (routeHandler: 'T -> HttpHandler) : Endpoint =
        routeBindWithExtensions<'T> (id) (path) (routeHandler)

    let subRouteWithExtensions
        (configureEndpoint: ConfigureEndpoint)
        (path: string)
        (endpoints: Endpoint list)
        : Endpoint =
        NestedEndpoint(path, endpoints, configureEndpoint)

    let subRoute (path: string) (endpoints: Endpoint list) : Endpoint =
        subRouteWithExtensions (id) (path) (endpoints)

    let rec applyBefore (httpHandler: HttpHandler) (endpoint: Endpoint) =
        match endpoint with
        | SimpleEndpoint(v, p, h, ce) -> SimpleEndpoint(v, p, httpHandler >=> h, ce)
        | TemplateEndpoint(v, p, m, h, ce) -> TemplateEndpoint(v, p, m, (fun (o: obj) -> httpHandler >=> h o), ce)
        | NestedEndpoint(t, lst, ce) -> NestedEndpoint(t, List.map (applyBefore httpHandler) lst, ce)
        | MultiEndpoint(lst) -> MultiEndpoint(List.map (applyBefore httpHandler) lst)

    let rec applyAfter (httpHandler: HttpHandler) (endpoint: Endpoint) =
        match endpoint with
        | SimpleEndpoint(v, p, h, ce) -> SimpleEndpoint(v, p, h >=> httpHandler, ce)
        | TemplateEndpoint(v, p, m, h, ce) -> TemplateEndpoint(v, p, m, (fun (o: obj) -> h o >=> httpHandler), ce)
        | NestedEndpoint(t, lst, ce) -> NestedEndpoint(t, List.map (applyAfter httpHandler) lst, ce)
        | MultiEndpoint(lst) -> MultiEndpoint(List.map (applyAfter httpHandler) lst)

    let rec configureEndpoint (f: ConfigureEndpoint) (endpoint: Endpoint) =
        match endpoint with
        | SimpleEndpoint(v, p, h, ce) -> SimpleEndpoint(v, p, h, ce >> f)
        | TemplateEndpoint(v, p, m, h, ce) -> TemplateEndpoint(v, p, m, h, ce >> f)
        | NestedEndpoint(t, lst, ce) -> NestedEndpoint(t, lst, ce >> f)
        | MultiEndpoint(lst) -> MultiEndpoint(List.map (configureEndpoint f) lst)

    let addMetadata (metadata: obj) (endpoint: Endpoint) : Endpoint =
        endpoint |> configureEndpoint _.WithMetadata(metadata)

// ---------------------------
// Middleware Extension Methods
// ---------------------------

[<Extension>]
type EndpointRouteBuilderExtensions() =

    [<Extension>]
    static member private MapSingleEndpoint
        (builder: IEndpointRouteBuilder, singleEndpoint: HttpVerb * RouteTemplate * RequestDelegate * ConfigureEndpoint)
        =

        let verb, routeTemplate, requestDelegate, configureEndpoint = singleEndpoint

        match verb with
        | NotSpecified -> builder.Map(routeTemplate, requestDelegate) |> configureEndpoint
        | _ ->
            builder.MapMethods(routeTemplate, [ verb.ToString() ], requestDelegate)
            |> configureEndpoint
        |> ignore


    [<Extension>]
    static member private MapMultiEndpoint
        (builder: IEndpointRouteBuilder, multiEndpoint: RouteTemplate * Endpoint list * ConfigureEndpoint)
        =

        let subRouteTemplate, endpoints, configureEndpoint = multiEndpoint
        let routeTemplate = sprintf "%s%s" subRouteTemplate

        endpoints
        |> List.iter (fun endpoint ->
            match endpoint with
            | SimpleEndpoint(v, t, h, ce) ->
                let d = RequestDelegateBuilder.createRequestDelegate h
                builder.MapSingleEndpoint(v, routeTemplate t, d, configureEndpoint >> ce)
            | TemplateEndpoint(v, t, m, h, ce) ->
                let d = RequestDelegateBuilder.createTokenizedRequestDelegate m h
                builder.MapSingleEndpoint(v, routeTemplate t, d, configureEndpoint >> ce)
            | NestedEndpoint(t, e, ce) -> builder.MapNestedEndpoint(routeTemplate t, e, configureEndpoint >> ce)
            | MultiEndpoint(el) -> builder.MapMultiEndpoint(subRouteTemplate, el, configureEndpoint)
        )

    [<Extension>]
    static member private MapNestedEndpoint
        (builder: IEndpointRouteBuilder, nestedEndpoint: RouteTemplate * Endpoint list * ConfigureEndpoint)
        =

        let subRouteTemplate, endpoints, parentConfigureEndpoint = nestedEndpoint
        let routeTemplate = sprintf "%s%s" subRouteTemplate

        endpoints
        |> List.iter (fun endpoint ->
            match endpoint with
            | SimpleEndpoint(v, t, h, ce) ->
                let d = RequestDelegateBuilder.createRequestDelegate h
                builder.MapSingleEndpoint(v, routeTemplate t, d, parentConfigureEndpoint >> ce)
            | TemplateEndpoint(v, t, m, h, ce) ->
                let d = RequestDelegateBuilder.createTokenizedRequestDelegate m h
                builder.MapSingleEndpoint(v, routeTemplate t, d, parentConfigureEndpoint >> ce)
            | NestedEndpoint(t, e, ce) -> builder.MapNestedEndpoint(routeTemplate t, e, parentConfigureEndpoint >> ce)
            | MultiEndpoint(el) -> builder.MapMultiEndpoint(subRouteTemplate, el, parentConfigureEndpoint)
        )

    [<Extension>]
    static member MapGiraffeEndpoints(builder: IEndpointRouteBuilder, endpoints: Endpoint list) =

        endpoints
        |> List.iter (fun endpoint ->
            match endpoint with
            | SimpleEndpoint(v, t, h, ce) ->
                let d = RequestDelegateBuilder.createRequestDelegate h
                builder.MapSingleEndpoint(v, t, d, ce)
            | TemplateEndpoint(v, t, m, h, ce) ->
                let d = RequestDelegateBuilder.createTokenizedRequestDelegate m h
                builder.MapSingleEndpoint(v, t, d, ce)
            | NestedEndpoint(t, e, ce) -> builder.MapNestedEndpoint(t, e, ce)
            | MultiEndpoint(el) -> builder.MapMultiEndpoint("", el, id)
        )

[<Extension>]
type ApplicationBuilderExtensions() =
    /// <summary>
    /// Uses ASP.NET Core's Endpoint Routing middleware to register Giraffe endpoints.
    /// </summary>
    [<Extension>]
    static member UseGiraffe(builder: IApplicationBuilder, endpoints: Endpoint list) =

        builder.UseEndpoints(fun e -> e.MapGiraffeEndpoints(endpoints))
