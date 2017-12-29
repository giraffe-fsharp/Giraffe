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


let configure (app : IApplicationBuilder,cm,token) =
    app.UseWebSockets()
    |> useF (echoSocket cm token)
    |> ignore

    let abc = Giraffe.HttpStatusCodeHandlers.Successful.ok (text "ok")
    app.UseGiraffe abc

let createClient (server:TestServer,token) = task {
    let wsClient = server.CreateWebSocketClient()
    return! wsClient.ConnectAsync(server.BaseAddress, token)
}

let receiveText (websocket:WebSocket,token) = task {
    let buffer = Array.zeroCreate DefaultWebSocketOptions.ReceiveBufferSize |> ArraySegment<byte>
    let! _ = websocket.ReceiveAsync(buffer, token)
    return ConvertToMsg buffer.Array
}

let createServer(cm,token) =
    new TestServer(
                WebHostBuilder()
                    .Configure(fun app -> configure(app,cm,token)))

[<Fact>]
let ``Simple Echo Test`` () = task {
    let token = CancellationToken.None
    let cm = ConnectionManager()

    use server = createServer (cm,token)
    let! wsClient = createClient(server, token)

    let expected = "Hello"
    let! _ = SendText wsClient expected token
    let! actual = receiveText (wsClient,token)

    Assert.Equal(expected,actual)
}

[<Theory>]
[<InlineData(0)>]
[<InlineData(1)>]
[<InlineData(10)>]
[<InlineData(100)>]
[<InlineData(1000)>]
let ``Broadcast Test`` (n:int) = task {
    let token = CancellationToken.None
    let cm = ConnectionManager()

    use server = createServer (cm,token)
    let! clients =
        [1..n]
        |> List.map (fun _ -> createClient(server,token))
        |> Task.WhenAll
   
    let expected = "Hello"
    let! _ = cm.SendToAll(expected,token)

    let! results =
        clients
        |> Array.map (fun ws -> receiveText (ws,token))
        |> Task.WhenAll

    for i in 1..n do
        Assert.Equal(expected, results.[i])
}
