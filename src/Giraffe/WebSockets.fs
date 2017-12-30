module Giraffe.WebSocket

open System
open System.Net.WebSockets
open System.Threading
open System.Threading.Tasks
open Giraffe.Tasks
open Giraffe

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

let NegotiateSubProtocol(requestedSubProtocols,supportedProtocols:seq<WebSocketSubprotocol>) =
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

        member __.Count = sockets.Count

type WebSocketReference = {
    WebSocket : WebSocket
    ID : string
}
    with
        static member FromWebSocket(websocket) : WebSocketReference = {
            WebSocket  = websocket
            ID = Guid.NewGuid().ToString()
        }

        static member FromWebSocketWithID(websocket,websocketID) : WebSocketReference = {
            WebSocket  = websocket
            ID = websocketID
        }

type ConnectionManager(?messageSize) =
    let messageSize = defaultArg messageSize DefaultWebSocketOptions.ReceiveBufferSize

    let connections = WebSocketConnectionDictionary()
    with
        member __.Send(webSocket:WebSocket,msg:string,cancellationToken) = task {
            let byteResponse =
                msg
                |> System.Text.Encoding.UTF8.GetBytes
            
            let segment = ArraySegment<byte>(byteResponse, 0, byteResponse.Length)

            if not (isNull webSocket) then
                if webSocket.State = WebSocketState.Open then
                    do! webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken)
        }

        member __.GetSocket(websocketID:string) = connections.GetSocket websocketID

        member __.Count = connections.Count

        member __.SendToAll(msg:string,cancellationToken:CancellationToken) = task {
            let byteResponse =
                msg
                |> System.Text.Encoding.UTF8.GetBytes
            let segment = ArraySegment<byte>(byteResponse, 0, byteResponse.Length)

            let! results =
                connections.AllConnections
                |> Array.map (fun kv -> task {
                    if not cancellationToken.IsCancellationRequested then
                        let webSocket = kv.Value
                        if webSocket.State = WebSocketState.Open then
                            do! webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken)
                            return 1
                        else
                            if webSocket.State = WebSocketState.Closed then
                                connections.RemoveSocket kv.Key |> ignore
                            return 0
                    else
                        return 0
                    })
                |> Task.WhenAll
            return results |> Seq.sum
        }

        member private __.Receive<'Msg> (reference:WebSocketReference,messageF: WebSocketReference -> string -> Task<bool>,cancellationToken:CancellationToken) = task {
            let buffer = Array.zeroCreate messageSize |> ArraySegment<byte>
            use memoryStream = new IO.MemoryStream()
            let mutable endOfMessage = false
            let mutable result = Unchecked.defaultof<_>

            while not endOfMessage do
                let! received = reference.WebSocket.ReceiveAsync(buffer, cancellationToken)
                if received.CloseStatus.HasValue then
                    do! reference.WebSocket.CloseAsync(received.CloseStatus.Value, received.CloseStatusDescription, cancellationToken)
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
                            let! r = 
                                memoryStream.ToArray()
                                |> ConvertToMsg
                                |> messageF reference

                            result <- r
                            endOfMessage <- true
                        | _ ->
                            raise (NotImplementedException())

            return result
        }

        member this.RegisterClient<'Msg>(reference:WebSocketReference,connectedF: WebSocketReference -> Task<bool>,messageF,cancellationToken:CancellationToken) = task {
            connections.AddSocket(reference.ID,reference.WebSocket)
            let! connectionAccepted = connectedF reference
            let mutable running = connectionAccepted
            while running && not cancellationToken.IsCancellationRequested do
                let! msg = this.Receive<'Msg>(reference,messageF,cancellationToken)
                running <- msg

            return! this.Disconnecting(reference.ID)
        }

        member this.RegisterClient<'Msg>(webSocket:WebSocket,connectedF,messageF,cancellationToken:CancellationToken) =
            this.RegisterClient<'Msg>(WebSocketReference.FromWebSocket webSocket,connectedF,messageF,cancellationToken)

        member private __.Disconnecting (websocketID:string) = task {
            match connections.RemoveSocket(websocketID) with
            | Some socket ->
                if not (isNull socket) then
                    if socket.State = WebSocketState.Open then
                        do! socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the WebSocketManager", CancellationToken.None)
            | _ -> ()
        }

        member this.CreateSocket(websocketID:string,onConnected,onMessage,cancellationToken) =
            fun next (ctx : Microsoft.AspNetCore.Http.HttpContext) -> task {
                if ctx.WebSockets.IsWebSocketRequest then
                    let! (websocket : WebSocket) = ctx.WebSockets.AcceptWebSocketAsync()
                    let reference = WebSocketReference.FromWebSocketWithID(websocket,websocketID)
                    do! this.RegisterClient(reference,onConnected,onMessage,cancellationToken)
                    return! Successful.ok (text "OK") next ctx
                else
                    return! HttpStatusCodeHandlers.RequestErrors.badRequest (text "no websocket request") next ctx
            }

        member this.CreateSocket(onConnected,onMessage,cancellationToken:CancellationToken) =
            fun next (ctx : Microsoft.AspNetCore.Http.HttpContext) -> task {
                if ctx.WebSockets.IsWebSocketRequest then
                    let! (websocket : WebSocket) = ctx.WebSockets.AcceptWebSocketAsync()
                    let reference = WebSocketReference.FromWebSocket(websocket)
                    do! this.RegisterClient(reference,onConnected,onMessage,cancellationToken)
                    return! Successful.ok (text "OK") next ctx
                else
                    return! HttpStatusCodeHandlers.RequestErrors.badRequest (text "no websocket request") next ctx
            }

        member this.CreateSocket(websocketID:string,supportedProtocols:seq<WebSocketSubprotocol>,onConnected,onMessage,cancellationToken) =
            fun next (ctx : Microsoft.AspNetCore.Http.HttpContext) -> task {
                if ctx.WebSockets.IsWebSocketRequest then
                    match NegotiateSubProtocol(ctx.WebSockets.WebSocketRequestedProtocols,supportedProtocols) with
                    | Some subProtocol ->
                        let! (websocket : WebSocket) = ctx.WebSockets.AcceptWebSocketAsync(subProtocol.Name)
                        let reference = WebSocketReference.FromWebSocketWithID(websocket,websocketID)
                        do! this.RegisterClient(reference,onConnected,onMessage,cancellationToken)
                        return! Successful.ok (text "OK") next ctx
                    | None ->
                        return! HttpStatusCodeHandlers.RequestErrors.badRequest (text "websocket subprotocol not supported") next ctx
                    else
                        return! HttpStatusCodeHandlers.RequestErrors.badRequest (text "no websocket request") next ctx
            }

        member this.CreateSocket(supportedProtocols:seq<WebSocketSubprotocol>,onConnected,onMessage,cancellationToken) =
            fun next (ctx : Microsoft.AspNetCore.Http.HttpContext) -> task {
                if ctx.WebSockets.IsWebSocketRequest then
                    match NegotiateSubProtocol(ctx.WebSockets.WebSocketRequestedProtocols,supportedProtocols) with
                    | Some subProtocol ->
                        let! (websocket : WebSocket) = ctx.WebSockets.AcceptWebSocketAsync(subProtocol.Name)
                        let reference = WebSocketReference.FromWebSocket websocket
                        do! this.RegisterClient(reference,onConnected,onMessage,cancellationToken)
                        return! Successful.ok (text "OK") next ctx
                    | None ->
                        return! HttpStatusCodeHandlers.RequestErrors.badRequest (text "websocket subprotocol not supported") next ctx
                    else
                        return! HttpStatusCodeHandlers.RequestErrors.badRequest (text "no websocket request") next ctx
            }