[<AutoOpen>]
module Giraffe.Middleware

open System
open System.Runtime.CompilerServices
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.DependencyInjection.Extensions
open FSharp.Control.Tasks.Builders

// ---------------------------
// Default middleware
// ---------------------------

type GiraffeMiddleware (next          : RequestDelegate,
                        handler       : HttpHandler,
                        loggerFactory : ILoggerFactory) =

    do if isNull next then raise (ArgumentNullException("next"))

    let logger = loggerFactory.CreateLogger<GiraffeMiddleware>()

    // pre-compile the handler pipeline
    let func : HttpFunc = handler earlyReturn

    member __.Invoke (ctx : HttpContext) =
        task {
            let start = System.Diagnostics.Stopwatch.GetTimestamp();

            let! result = func ctx

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

[<Extension>]
type ApplicationBuilderExtensions() =
    /// <summary>
    /// Adds the <see cref="GiraffeMiddleware" /> into the ASP.NET Core pipeline. Any web request which doesn't get handled by a surrounding middleware can be picked up by the Giraffe <see cref="HttpHandler" /> pipeline.
    ///
    /// It is generally recommended to add the <see cref="GiraffeMiddleware" /> after the error handling, static file and any authentication middleware.
    /// </summary>
    /// <param name="handler">The Giraffe <see cref="HttpHandler" /> pipeline. The handler can be anything from a single handler to an entire web application which has been composed from many smaller handlers.</param>
    /// <returns><see cref="Microsoft.FSharp.Core.Unit"/></returns>
    [<Extension>]
    static member UseGiraffe
        (builder : IApplicationBuilder, handler : HttpHandler) =
        builder.UseMiddleware<GiraffeMiddleware> handler
        |> ignore

    /// <summary>
    /// Adds the <see cref="GiraffeErrorHandlerMiddleware" /> into the ASP.NET Core pipeline. The <see cref="GiraffeErrorHandlerMiddleware" /> has been configured in such a way that it only invokes the <see cref="ErrorHandler" /> when an unhandled exception bubbles up to the middleware. It therefore is recommended to add the <see cref="GiraffeErrorHandlerMiddleware" /> as the very first middleware above everything else.
    /// </summary>
    /// <param name="handler">The Giraffe <see cref="ErrorHandler" /> pipeline. The handler can be anything from a single handler to a bigger error application which has been composed from many smaller handlers.</param>
    /// <returns>Returns an <see cref="Microsoft.AspNetCore.Builder.IApplicationBuilder"/> builder object.</returns>
    [<Extension>]
    static member UseGiraffeErrorHandler
        (builder : IApplicationBuilder, handler : ErrorHandler) =
        builder.UseMiddleware<GiraffeErrorHandlerMiddleware> handler

[<Extension>]
type ServiceCollectionExtensions() =
    /// <summary>
    /// Adds default Giraffe services to the ASP.NET Core service container.
    ///
    /// The default services include features like <see cref="Json.ISerializer"/>, <see cref="Xml.ISerializer"/>, <see cref="INegotiationConfig"/> or more. Please check the official Giraffe documentation for an up to date list of configurable services.
    /// </summary>
    /// <returns>Returns an <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/> builder object.</returns>
    [<Extension>]
    static member AddGiraffe(svc : IServiceCollection) =
        svc.TryAddSingleton<Json.ISerializer>(
            NewtonsoftJson.Serializer(NewtonsoftJson.Serializer.DefaultSettings))
        svc.TryAddSingleton<Xml.ISerializer>(
            SystemXml.Serializer(SystemXml.Serializer.DefaultSettings))
        svc.TryAddSingleton<INegotiationConfig, DefaultNegotiationConfig>()
        svc
