module Giraffe.WebSocketTests 

open System
open System.Net.WebSockets
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open Xunit
open Giraffe.Tasks
open Giraffe.WebSocket
open System.Threading.Tasks
open System.Threading
open RequestErrors
open Giraffe.TokenRouter

let echoSocket (connectionManager : ConnectionManager) token next (ctx : HttpContext) = task {
    if ctx.WebSockets.IsWebSocketRequest then
        let! (websocket : WebSocket) = ctx.WebSockets.AcceptWebSocketAsync()
        let connected socket id = task {
            ()
        }

        let onMessage data = task {
            return! SendText websocket data token
        }

        do! connectionManager.RegisterClient(websocket,connected,onMessage,token)
        return! Successful.OK (text "OK") next ctx
    else
        return! BAD_REQUEST (text "no websocket request") next ctx
}


let webApp cm token =
    let notfound = NOT_FOUND "Page not found"
   
    router notfound [
       GET [
           route "/echo" (fun next ctx -> echoSocket cm token next ctx) 
       ]
    ]

let configure (app : IApplicationBuilder,cm,token) =
    app
      .UseWebSockets()
      .UseGiraffe (webApp cm token)

let createClient (server:TestServer,token) = task {
    let wsClient = server.CreateWebSocketClient()
    let url = server.BaseAddress.AbsoluteUri + "echo"
    return! wsClient.ConnectAsync(Uri(url), token)
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
