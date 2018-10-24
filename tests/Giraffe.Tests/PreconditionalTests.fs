module Giraffe.Tests.PreconditionsTests

open System
open System.Net
open System.Net.Http
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Xunit
open Giraffe

// ---------------------------------
// Text file used for feature testing
// ---------------------------------

// ### TEXT REPRESENTATION
// ---------------------------------

    // 0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ


// ### BYTE REPRESENTATION
// ---------------------------------

    // 48,49,50,51,52,53,54,55,56,57,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90


// ### TABULAR BYTE REPRESENTATION
// ---------------------------------

    // 0  ,1  ,2  ,3  ,4  ,5  ,6  ,7  ,8  ,9
    // ----------------------------------------
    // 48 ,49 ,50 ,51 ,52 ,53 ,54 ,55 ,56 ,57
    // 97 ,98 ,99 ,100,101,102,103,104,105,106
    // 107,108,109,110,111,112,113,114,115,116
    // 117,118,119,120,121,122,65 ,66 ,67 ,68
    // 69 ,70 ,71 ,72 ,73 ,74 ,75 ,76 ,77 ,78
    // 79 ,80 ,81 ,82 ,83 ,84 ,85 ,86 ,87 ,88
    // 89 ,90

// ---------------------------------
// Streaming App
// ---------------------------------

module Urls =
    let rangeProcessingEnabled  = "/range-processing-enabled"
    let rangeProcessingDisabled = "/range-processing-disabled"

module WebApp =
    let streamHandler (enableRangeProcessing : bool) args : HttpHandler =
        args
        ||> streamFile enableRangeProcessing "streaming2.txt"

    let webApp args =
        choose [
            route Urls.rangeProcessingEnabled  >=> streamHandler true args
            route Urls.rangeProcessingDisabled >=> streamHandler false args
        ]

    let errorHandler (ex : Exception) (_ : ILogger) : HttpHandler =
        printfn "Error: %s" ex.Message
        printfn "StackTrace:%s %s" Environment.NewLine ex.StackTrace
        setStatusCode 500 >=> text ex.Message

    let configureApp args (app : IApplicationBuilder) =
        app.UseGiraffeErrorHandler(errorHandler)
           .UseGiraffe(webApp args)

    let configureServices (services : IServiceCollection) =
        services.AddGiraffe() |> ignore

let makeRequest req = makeRequest WebApp.configureApp WebApp.configureServices req

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``HTTP GET with If-Match and no ETag`` () =
    createRequest HttpMethod.Get Urls.rangeProcessingDisabled
    |> addHeader "If-Match" "\"111\", \"222\", \"333\""
    |> makeRequest (None, None)