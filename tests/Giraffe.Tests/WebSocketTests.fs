module Giraffe.WebSocketTests 

open System
open System.Net.WebSockets
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open Xunit
open FSharp.Control.Tasks.ContextInsensitive
open Giraffe.WebSocket
open System.Threading.Tasks
open System.Threading
open RequestErrors

let webApp (connectionManager:ConnectionManager) cancellationToken =
    let notfound = NOT_FOUND "Page not found"
   
    choose [
       route "/echo" >=> (connectionManager.CreateSocket(
                            (fun _ref -> task { return () }),
                            (fun ref msg -> ref.SendTextAsync(msg,cancellationToken)),
                            cancellationToken=cancellationToken)) 
    ]

let configure (app : IApplicationBuilder,cm,cancellationToken) =
    app
      .UseWebSockets()
      .UseGiraffe (webApp cm cancellationToken)

let createClient (server:TestServer,route,cancellationToken) = task {
    let wsClient = server.CreateWebSocketClient()
    let url = server.BaseAddress.AbsoluteUri + route
    return! wsClient.ConnectAsync(Uri(url), cancellationToken)
}


let sendTextAsync (webSocket:WebSocket,message:string,cancellationToken) = task {
    if not (isNull webSocket) && webSocket.State = WebSocketState.Open then
        let bytes = System.Text.Encoding.UTF8.GetBytes(message)
        let buffer = new ArraySegment<byte>(bytes, 0, bytes.Length)
        let! _ =  webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken)
        return true
    else
        return false
}

let receiveTextAsync (websocket:WebSocket,cancellationToken) = task {
    let moveBuffer (buffer: ArraySegment<'T>) count =
        ArraySegment(buffer.Array, buffer.Offset + count, buffer.Count - count)

    let rec receive receivedBytes = task {
        let! result = websocket.ReceiveAsync(receivedBytes, cancellationToken)
        let currentBuffer = moveBuffer receivedBytes result.Count
        if result.EndOfMessage then
            return 
                currentBuffer.Array    
                |> System.Text.Encoding.UTF8.GetString
                |> fun s -> s.TrimEnd(char 0)
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
    let! wsClient = createClient(server, "echo", cancellationToken)

    let expected = "Hello"
    let! _ = sendTextAsync(wsClient,expected,cancellationToken)
    let! actual = receiveTextAsync(wsClient, cancellationToken)

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
        |> List.map (fun _ -> createClient(server, "echo", cancellationToken))
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
        |> List.map (fun _ -> createClient(server, "echo", cancellationToken))
        |> Task.WhenAll
   
    let expected = "Hello"
    do! cm.BroadcastTextAsync(expected,cancellationToken)

    let! results = 
        clients
        |> Seq.map (fun ws -> receiveTextAsync (ws,cancellationToken))
        |> Task.WhenAll

    for x in results do
        Assert.Equal(expected, x)
}
