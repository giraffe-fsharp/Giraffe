module AspNetCore.Lambda.Middleware

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open AspNetCore.Lambda.HttpHandlers 

type HttpHandlerMiddleware (next : RequestDelegate, handler : HttpHandler) =
    member __.Invoke (ctx : HttpContext, env : IHostingEnvironment) =
        async {
            let! result = handler (env, ctx)
            if (result.IsNone) then
                next.Invoke ctx
                |> Async.AwaitTask
                |> ignore
        } |> Async.StartAsTask

type IApplicationBuilder with
    member this.UseHttpHandler (handler : HttpHandler) =
        this.UseMiddleware<HttpHandlerMiddleware>(handler)
        |> ignore