module Giraffe.WebSocketTests 

open System
open System.Net.WebSockets
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http.Internal
open Microsoft.Extensions.Primitives
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open Xunit
open Giraffe.Tasks
open Giraffe.WebSocket
open System.Threading.Tasks
open System.Threading


let echoSocket (connectionManager : ConnectionManager) token (httpContext : HttpContext) (next : unit -> Task) = task {
    // ECHO
    if httpContext.WebSockets.IsWebSocketRequest then
        let! (websocket : WebSocket) = httpContext.WebSockets.AcceptWebSocketAsync()
        let connected socket id = task { 
            ()
        }

        let onMessage data = task {
            return! SendText websocket data token
        }

        do! connectionManager.RegisterClient(websocket,connected,onMessage,token)
    else
        do! next()
}

let useF (middlware : HttpContext -> (unit -> Task) -> Task<unit>) (app:IApplicationBuilder) =
    app.Use(
        Func<HttpContext,Func<Task>,Task>(
            fun ctx next -> 
                middlware ctx next.Invoke  :> Task
            ))


[<Fact>]
let ``Simple Echo Test`` () = task {
    let token = CancellationToken.None
    let cm = ConnectionManager()
    let configure (app : IApplicationBuilder) =
        app.UseWebSockets()
        |> useF (echoSocket cm token)
        |> ignore

        let abc = Giraffe.HttpStatusCodeHandlers.Successful.ok (text "ok")
        app.UseGiraffe abc

    use server =
         new TestServer(
                WebHostBuilder()
                    .Configure(fun app -> configure app))

    let wsClient = server.CreateWebSocketClient()
    let! websocket = wsClient.ConnectAsync(server.BaseAddress, token)

    let expected = "Hello"
    let! _ = SendText websocket expected token
    let buffer = Array.zeroCreate DefaultWebSocketOptions.ReceiveBufferSize |> ArraySegment<byte>
    
    let! result = websocket.ReceiveAsync(buffer, token)
    let actual = ConvertToMsg buffer.Array

    Assert.Equal(expected,actual)
}
