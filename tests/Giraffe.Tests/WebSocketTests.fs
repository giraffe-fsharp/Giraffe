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

let echoSocket (connectionManager : ConnectionManager) token =
    connectionManager.CreateSocket(
        (fun _ref -> task { return true }),
        (fun ref data -> SendText ref.WebSocket data token),
        token)

let webApp cm token =
    let notfound = NOT_FOUND "Page not found"
   
    router notfound [
       GET [
           route "/echo" (echoSocket cm token) 
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
    let moveBuffer (buffer: ArraySegment<'T>) count =
        ArraySegment(buffer.Array, buffer.Offset + count, buffer.Count - count)

    let rec receive receivedBytes = task {
        let! result = websocket.ReceiveAsync(receivedBytes, token)
        let currentBuffer = moveBuffer receivedBytes result.Count
        if result.EndOfMessage then
            return ConvertToMsg currentBuffer.Array
        else
            return! receive currentBuffer
    }

    let buffer = Array.zeroCreate DefaultWebSocketOptions.ReceiveBufferSize 
    return! receive (ArraySegment buffer)
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
//[<InlineData(100)>]
//[<InlineData(1000)>]
let ``Can create some clients`` (n:int) = task {
    let token = CancellationToken.None
    let cm = ConnectionManager()

    use server = createServer (cm,token)
    let! clients =
        [1..n]
        |> List.map (fun _ -> createClient(server,token))
        |> Task.WhenAll

    Assert.Equal(clients.Length,n)
}


[<Theory>]
[<InlineData(0)>]
[<InlineData(1)>]
[<InlineData(10)>]
//[<InlineData(100)>]
//[<InlineData(1000)>]
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

    for x in results do
        Assert.Equal(expected, x)
}
