module AspNetCore.Lambda.Middleware

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open AspNetCore.Lambda.HttpHandlers

type LambdaMiddleware (next          : RequestDelegate,
                       handler       : HttpHandler,
                       env           : IHostingEnvironment,
                       loggerFactory : ILoggerFactory) =

    member __.Invoke (ctx : HttpContext) =
        async {
            let logger = loggerFactory.CreateLogger<LambdaMiddleware>()

            let httpHandlerContext =
                {
                    HttpContext = ctx
                    Environment = env
                    Logger      = logger
                }

            let! result = handler httpHandlerContext
            if (result.IsNone) then
                next.Invoke ctx
                |> Async.AwaitTask
                |> ignore
        } |> Async.StartAsTask

type IApplicationBuilder with
    member this.UseLambda (handler : HttpHandler) =
        this.UseMiddleware<LambdaMiddleware>(handler)
        |> ignore