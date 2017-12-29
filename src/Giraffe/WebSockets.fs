module Giraffe.WebSocket

open System
open System.Net.WebSockets
open System.Threading
open System.Threading.Tasks
open Giraffe.Tasks

type SocketStatus =
    | Connected of string


[<RequireQualifiedAccess>]
type WebSocketMsg<'Data> =
   | StatusMsg of SocketStatus
   | Opening
   | Closing
   | Error of string
   | Data of 'Data

type WebSocketSubprotocol = {
    Name : string
}

let DefaultWebSocketOptions = 
    let webSocketOptions = Microsoft.AspNetCore.Builder.WebSocketOptions()
    webSocketOptions.KeepAliveInterval <- TimeSpan.FromSeconds 120.
    webSocketOptions.ReceiveBufferSize <- 4 * 1024
    webSocketOptions

/// Converts a message buffer to a proper text message
let ConvertToMsg (buffer:byte [])=
    buffer
    |> System.Text.Encoding.UTF8.GetString
    |> fun s -> s.TrimEnd(char 0)

let NegotiateSubProtocol(requestedSubProtocols,supportedProtocols) =
    supportedProtocols
    |> Seq.tryFind (fun (supported:WebSocketSubprotocol) ->
        requestedSubProtocols |> Seq.contains supported.Name)

let SendText (ws:WebSocket) (message:string) cancellationToken = task {
    if not (isNull ws) && ws.State = WebSocketState.Open then
        let a = System.Text.Encoding.UTF8.GetBytes(message)
        let buffer = new ArraySegment<byte>(a, 0, a.Length)
        let! _ =  ws.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken)
        return true
    else
        return false
}

type private WebSocketConnectionDictionary() =
    let sockets = new System.Collections.Concurrent.ConcurrentDictionary<string, WebSocket>()

    with 
        member __.AddSocket(id:string,socket) =
            sockets.AddOrUpdate(id, socket, Func<_,_,_>(fun _ _ -> socket)) |> ignore

        member __.RemoveSocket websocketID = 
            match sockets.TryRemove websocketID with
            | true, s -> Some s
            | _ -> None


        member __.GetSocket websocketID = 
            match sockets.TryGetValue websocketID with
            | true, s -> Some s
            | _ -> None

        member __.AllConnections = sockets |> Seq.toArray

type ConnectionManager(?messageSize) =
    let messageSize = defaultArg messageSize DefaultWebSocketOptions.ReceiveBufferSize

    let connections = WebSocketConnectionDictionary()
    with
        member __.Send(webSocket:WebSocket,msg:string,cancellationToken) = task {
            let byteResponse =
                msg
                |> System.Text.Encoding.UTF8.GetBytes

            if not (isNull webSocket) then
                if webSocket.State = WebSocketState.Open then
                    do! webSocket.SendAsync(new ArraySegment<byte>(byteResponse, 0, byteResponse.Length), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false)
        }

        member __.GetSocket(id:string) = connections.GetSocket id

        member __.SendToAll(msg:string,cancellationToken:CancellationToken) =
            let byteResponse =
                msg
                |> System.Text.Encoding.UTF8.GetBytes

            connections.AllConnections
            |> Seq.map (fun kv -> task {
                if not cancellationToken.IsCancellationRequested then
                    let webSocket = kv.Value
                    if webSocket.State = WebSocketState.Open then
                        do! webSocket.SendAsync(new ArraySegment<byte>(byteResponse, 0, byteResponse.Length), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false)
                    else
                        if webSocket.State = WebSocketState.Closed then
                            connections.RemoveSocket kv.Key |> ignore 
                    return ()
                })
            |> Task.WhenAll

        member private __.Receive<'Msg> (webSocket:WebSocket,webSocketID:string,messageF,cancellationToken:CancellationToken) = task {
            let buffer = Array.zeroCreate messageSize |> ArraySegment<byte>
            use memoryStream = new IO.MemoryStream()
            let mutable endOfMessage = false
            let mutable result = Unchecked.defaultof<_>

            while not endOfMessage do
                let receiveTask = webSocket.ReceiveAsync(buffer, cancellationToken)
                receiveTask.Wait()
                let received = receiveTask.Result

                if received.CloseStatus.HasValue then
                    let t = webSocket.CloseAsync(received.CloseStatus.Value, received.CloseStatusDescription, cancellationToken)
                    t.Wait()
                    result <- false
                    endOfMessage <- true
                else
                    memoryStream.Write(buffer.Array,buffer.Offset,received.Count)
                    if received.EndOfMessage then
                        match received.MessageType with
                        | WebSocketMessageType.Binary ->
                            raise (NotImplementedException())
                        | WebSocketMessageType.Close ->
                            result <- false 
                            endOfMessage <- true
                        | WebSocketMessageType.Text ->
                            let messageProcessingTask : System.Threading.Tasks.Task<bool> = 
                                memoryStream.ToArray()
                                |> ConvertToMsg
                                |> messageF webSocket webSocketID

                            messageProcessingTask.Wait()

                            result <- messageProcessingTask.Result
                            endOfMessage <- true
                        | _ ->
                            raise (NotImplementedException())

            return result
        }

        member this.RegisterClient<'Msg>(webSocket:WebSocket,websocketID,connectedF,messageF,cancellationToken:CancellationToken) = task {
            connections.AddSocket(websocketID,webSocket)
            let t : System.Threading.Tasks.Task<bool> = connectedF webSocket websocketID
            t.Wait()
            let mutable running = t.Result

            while running && not cancellationToken.IsCancellationRequested do
                let! msg = this.Receive<'Msg>(webSocket,websocketID,messageF,cancellationToken)
                running <- msg

            return! this.Disconnecting(websocketID)
        }

        member this.RegisterClient<'Msg>(webSocket:WebSocket,connectedF,messageF,cancellationToken:CancellationToken) =
            let id = Guid.NewGuid().ToString()
            this.RegisterClient<'Msg>(webSocket,id,connectedF,messageF,cancellationToken)

        member private __.Disconnecting (websocketID:string) = task {
            match connections.RemoveSocket(websocketID) with
            | Some socket ->
                if not (isNull socket) then
                    if socket.State = WebSocketState.Open then
                        do! socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the WebSocketManager", CancellationToken.None).ConfigureAwait(false)
            | _ -> ()
        }

        member this.CreateSocket(onConnected,onMessage,cancellationToken) =
            fun next (ctx : Microsoft.AspNetCore.Http.HttpContext) -> task {
                if ctx.WebSockets.IsWebSocketRequest then
                    let! (websocket : WebSocket) = ctx.WebSockets.AcceptWebSocketAsync()

                    do! this.RegisterClient(websocket,onConnected,onMessage,cancellationToken)
                    return! Successful.ok (text "OK") next ctx
                else
                    return! HttpStatusCodeHandlers.RequestErrors.badRequest (text "no websocket request") next ctx
            }