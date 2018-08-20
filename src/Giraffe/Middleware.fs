[<AutoOpen>]
module Giraffe.Middleware

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.DependencyInjection.Extensions
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe.Serialization

// ---------------------------
// Default middleware
// ---------------------------

type GiraffeMiddleware (next          : RequestDelegate,
                        handler       : HttpHandler,
                        loggerFactory : ILoggerFactory) =

    do if isNull next then raise (ArgumentNullException("next"))

    // pre-compile the handler pipeline
    let func : HttpFunc = handler (Some >> Task.FromResult)

    member __.Invoke (ctx : HttpContext) =
        task {
            let start = System.Diagnostics.Stopwatch.GetTimestamp();

            let! result = func ctx
            let  logger = loggerFactory.CreateLogger<GiraffeMiddleware>()

            if logger.IsEnabled LogLevel.Debug then
                let freq = double System.Diagnostics.Stopwatch.Frequency
                let stop = System.Diagnostics.Stopwatch.GetTimestamp()
                let elapsedMs = (double (stop - start)) * 1000.0 / freq

                logger.LogDebug(
                    "Giraffe returned {SomeNoneResult} for {HttpProtocol} {HttpMethod} at {Path} in {ElapsedMs}",
                    (if result.IsSome then "Some" else "None"),
                    ctx.Request.Protocol,
                    ctx.Request.Method,
                    ctx.Request.Path.ToString(),
                    elapsedMs)

            if (result.IsNone) then
                return! next.Invoke ctx
        }

// ---------------------------
// Error Handling middleware
// ---------------------------

type GiraffeErrorHandlerMiddleware (next          : RequestDelegate,
                                    errorHandler  : ErrorHandler,
                                    loggerFactory : ILoggerFactory) =

    do if isNull next then raise (ArgumentNullException("next"))

    member __.Invoke (ctx : HttpContext) =
        task {
            try return! next.Invoke ctx
            with ex ->
                let logger = loggerFactory.CreateLogger<GiraffeErrorHandlerMiddleware>()
                try
                    let func = (Some >> Task.FromResult)
                    let! _ = errorHandler ex logger func ctx
                    return ()
                with ex2 ->
                    logger.LogError(EventId(0), ex,  "An unhandled exception has occurred while executing the request.")
                    logger.LogError(EventId(0), ex2, "An exception was thrown attempting to handle the original exception.")
        }

// ---------------------------
// Extension methods for convenience
// ---------------------------

type IApplicationBuilder with
    /// **Description**
    ///
    /// Adds the `GiraffeMiddleware` into the ASP.NET Core pipeline. Any web request which doesn't get handled by a surrounding middleware can be picked up by the Giraffe `HttpHandler` pipeline.
    ///
    /// It is generally recommended to add the `GiraffeMiddleware` after the error handling-, static file- and any authentiation middleware.
    ///
    /// **Parameters**
    ///
    /// - `handler`: The Giraffe `HttpHandler` pipeline. The handler can be anything from a single handler to an entire web application which has been composed from many smaller handlers.
    ///
    /// **Output**
    ///
    /// Returns `unit`.
    ///
    member this.UseGiraffe (handler : HttpHandler) =
        this.UseMiddleware<GiraffeMiddleware> handler
        |> ignore

    /// **Description**
    ///
    /// Adds the `GiraffeErrorHandlerMiddleware` into the ASP.NET Core pipeline. The `GiraffeErrorHandlerMiddleware` has been configured in such a way that it only invokes the `ErrorHandler` when an unhandled exception bubbles up to the middleware. It therefore is recommended to add the `GiraffeErrorHandlerMiddleware` as the very first middleware above everything else.
    ///
    /// **Parameters**
    ///
    /// - `handler`: The Giraffe `ErrorHandler` pipeline. The handler can be anything from a single handler to a bigger error application which has been composed from many smaller handlers.
    ///
    /// **Output**
    ///
    /// Returns `unit`.
    ///
    member this.UseGiraffeErrorHandler (handler : ErrorHandler) =
        this.UseMiddleware<GiraffeErrorHandlerMiddleware> handler

type IServiceCollection with
    /// **Description**
    ///
    /// Adds default Giraffe services to the ASP.NET Core service container.
    ///
    /// The default services include featurs like `IJsonSerializer`, `IXmlSerializer`, `INegotiationConfig` or more. Please check the official Giraffe documentation for an up to date list of configurable services.
    ///
    /// **Output**
    ///
    /// Returns an `IServiceCollection` builder object.
    ///
    member this.AddGiraffe() =
        this.TryAddSingleton<IJsonSerializer>(NewtonsoftJsonSerializer(NewtonsoftJsonSerializer.DefaultSettings))
        this.TryAddSingleton<IXmlSerializer>(DefaultXmlSerializer(DefaultXmlSerializer.DefaultSettings))
        this.TryAddSingleton<INegotiationConfig, DefaultNegotiationConfig>()
        this