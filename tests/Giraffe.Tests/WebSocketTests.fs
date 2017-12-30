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


let echoSocket (connectionManager : ConnectionManager) cancellationToken =
    connectionManager.CreateSocket(
        (fun _ref -> task { return true }),
        (fun ref data -> SendText ref.WebSocket data cancellationToken),
        cancellationToken)

let webApp cm cancellationToken =
    let notfound = NOT_FOUND "Page not found"
   
    router notfound [
       GET [
           route "/echo" (echoSocket cm cancellationToken) 
       ]
    ]

let configure (app : IApplicationBuilder,cm,cancellationToken) =
    app
      .UseWebSockets()
      .UseGiraffe (webApp cm cancellationToken)

let createClient (server:TestServer,cancellationToken) = task {
    let wsClient = server.CreateWebSocketClient()
    let url = server.BaseAddress.AbsoluteUri + "echo"
    return! wsClient.ConnectAsync(Uri(url), cancellationToken)
}

let receiveText (websocket:WebSocket,cancellationToken) = task {
    let moveBuffer (buffer: ArraySegment<'T>) count =
        ArraySegment(buffer.Array, buffer.Offset + count, buffer.Count - count)

    let rec receive receivedBytes = task {
        let! result = websocket.ReceiveAsync(receivedBytes, cancellationToken)
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
    let cancellationToken = CancellationToken.None
    let cm = ConnectionManager()

    use server = createServer (cm, cancellationToken)
    let! wsClient = createClient(server, cancellationToken)

    let expected = "Hello"
    let! _ = SendText wsClient expected cancellationToken
    let! actual = receiveText (wsClient, cancellationToken)

    Assert.Equal(expected,actual)
}



[<Theory>]
[<InlineData(10)>]
[<InlineData(100)>]
[<InlineData(1000)>]
let ``Can connect some clients`` (n:int) = task {
    let cancellationToken = CancellationToken.None
    let cm = ConnectionManager()

    use server = createServer (cm,cancellationToken)
    let! clients =
        [1..n]
        |> List.map (fun _ -> createClient(server,cancellationToken))
        |> Task.WhenAll

    Assert.Equal(n,clients.Length)
    Assert.Equal(n,cm.Count)

    for x in clients do
        Assert.Equal(WebSocketState.Open, x.State)
}


[<Theory>]
[<InlineData(0)>]
[<InlineData(1)>]
[<InlineData(10)>]
[<InlineData(100)>]
[<InlineData(1000)>]
let ``Broadcast Test`` (n:int) = task {
    let cancellationToken = CancellationToken.None
    let cm = ConnectionManager()

    use server = createServer (cm,cancellationToken)
    let! clients =
        [1..n]
        |> List.map (fun _ -> createClient(server,cancellationToken))
        |> Task.WhenAll
   
    let expected = "Hello"
    let! sent = cm.SendToAll(expected,cancellationToken)
    Assert.Equal(n,sent)

    let! results = 
        clients
        |> Seq.map (fun ws -> receiveText (ws,cancellationToken))
        |> Task.WhenAll

    for x in results do
        Assert.Equal(expected, x)
}
