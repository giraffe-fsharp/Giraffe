module AspNetCore.Lambda.Middleware

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open RazorLight
open AspNetCore.Lambda.HttpHandlers

/// ---------------------------
/// Logging helper functions
/// ---------------------------

let private getRequestInfo (ctx : HttpContext) =
    (ctx.Request.Protocol,
     ctx.Request.Method,
     ctx.Request.Path.ToString())
    |||> sprintf "%s %s %s"

/// ---------------------------
/// Default middleware
/// ---------------------------

type LambdaMiddleware (next          : RequestDelegate,
                       handler       : HttpHandler,
                       services      : IServiceProvider,
                       loggerFactory : ILoggerFactory) =
    
    do if isNull next then raise (ArgumentNullException("next"))

    member __.Invoke (ctx : HttpContext) =
        async {
            let logger = loggerFactory.CreateLogger<LambdaMiddleware>()
            let httpHandlerContext =
                {
                    HttpContext = ctx
                    Services    = services
                    Logger      = logger
                }
            let! result = handler httpHandlerContext

            if logger.IsEnabled LogLevel.Debug then
                match result with
                | Some _ -> sprintf "LambdaMiddleware returned Some for %s" (getRequestInfo ctx)
                | None   -> sprintf "LambdaMiddleware returned None for %s" (getRequestInfo ctx)
                |> logger.LogDebug

            if (result.IsNone) then
                return!
                    next.Invoke ctx
                    |> Async.AwaitTask
        } |> Async.StartAsTask

/// ---------------------------
/// Error Handling middleware
/// ---------------------------

type LambdaErrorHandlerMiddleware (next          : RequestDelegate,
                                   errorHandler  : ErrorHandler,
                                   services      : IServiceProvider,
                                   loggerFactory : ILoggerFactory) =

    do if isNull next then raise (ArgumentNullException("next"))

    member __.Invoke (ctx : HttpContext) =
        async {
            let logger = loggerFactory.CreateLogger<LambdaErrorHandlerMiddleware>()
            try
                return!
                    next.Invoke ctx
                    |> Async.AwaitTask
            with ex ->
                try
                    let httpHandlerContext =
                        {
                            HttpContext = ctx
                            Services    = services
                            Logger      = logger
                        }
                    return!
                        errorHandler ex httpHandlerContext
                        |> Async.Ignore
                with ex2 ->
                    logger.LogError(EventId(0), ex,  "An unhandled exception has occurred while executing the request.")
                    logger.LogError(EventId(0), ex2, "An exception was thrown attempting to handle the original exception.")
        } |> Async.StartAsTask

/// ---------------------------
/// Extension methods for convenience
/// ---------------------------

type IApplicationBuilder with
    member this.UseLambda (handler : HttpHandler) =
        this.UseMiddleware<LambdaMiddleware>(handler)
        |> ignore

    member this.UseLambdaErrorHandler (handler : ErrorHandler) =
        this.UseMiddleware<LambdaErrorHandlerMiddleware>(handler)
        |> ignore

type IServiceCollection with
    member this.AddRazorEngine (viewsFolderPath : string) =
        viewsFolderPath
        |> EngineFactory.CreatePhysical
        |> this.AddSingleton<IRazorLightEngine>