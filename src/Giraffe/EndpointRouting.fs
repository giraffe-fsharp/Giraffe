[<AutoOpen>]
module Giraffe.EndpointRouting

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks.V2

open Microsoft.AspNetCore.Routing

let private convertToHttpFunc (handler : HttpHandler) =
    handler (Some >> Task.FromResult)

let private convertToRequestDelegate (func : HttpFunc) =
    fun (ctx : HttpContext) ->
        task {
            let! _ = func ctx
            return ()
        } :> Task

type IRouteBuilder with
    member private this.MapGiraffeGetRoute (path : string, func : HttpFunc) =
        let requestDelegate = convertToRequestDelegate func
        this.MapGet(path, requestDelegate)
        |> ignore

    member this.MapGiraffe (routes : (string * HttpHandler) list) (handler : HttpHandler) =
        routes
        |> List.map(fun (p, h) -> (p, convertToHttpFunc h))
        |> List.iter(this.MapGiraffeGetRoute)
        this
