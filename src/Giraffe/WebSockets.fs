module Giraffe.WebSocket

open System
open System.Net.WebSockets
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks.ContextInsensitive

/// WebSocket subprotocol type. For negotiation of subprotocols.
type WebSocketSubprotocol = {
    /// The subprotocol name.
    Name : string
}

/// Default WebSocket communication options.
let DefaultWebSocketOptions = 
    let webSocketOptions = Microsoft.AspNetCore.Builder.WebSocketOptions()
    webSocketOptions.KeepAliveInterval <- TimeSpan.FromSeconds 120.
    webSocketOptions.ReceiveBufferSize <- 4 * 1024
    webSocketOptions

/// Internal reference to a WebSocket. Includes WebSocketID and Subprotocol.
type WebSocketReference = {
    /// A reference to the WebSocket.
    WebSocket : WebSocket
    /// The selected subprotocol.
    Subprotocol : WebSocketSubprotocol option
    /// The internal ID of the WebSocket.
    ID : string
    /// Task that will be started after websocket is closed
    OnClose : unit -> Task<unit>
}
    with
        /// Sends a UTF-8 encoded text message to the WebSocket client.
        member this.SendTextAsync(msg:string,?cancellationToken) = task {
            if not (isNull this.WebSocket) then
                try
                    if this.WebSocket.State = WebSocketState.Open then
                        let byteResponse = System.Text.Encoding.UTF8.GetBytes msg
                        let segment = ArraySegment<byte>(byteResponse, 0, byteResponse.Length)
                        let cancellationToken = cancellationToken |> Option.defaultValue CancellationToken.None
                        do! this.WebSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken)
                with
                | _ -> 
                    // TODO: Tracing 
                    ()
        }
        

        /// Closes the connection to the WebSocket client.
        member this.CloseAsync(?reason,?cancellationToken) = task {
            if not (isNull this.WebSocket) then
                try
                if this.WebSocket.State = WebSocketState.Open then
                    let cancellationToken = cancellationToken |> Option.defaultValue CancellationToken.None
                    let reason = reason |> Option.defaultValue "Closed by the WebSocket server"
                    do! this.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, cancellationToken)
                with
                | _ -> 
                    // TODO: Tracing 
                    ()
        }

        
        /// Creates a new reference to a WebSocket.
        static member FromWebSocket(websocket,onClose,?webSocketID,?subProtocol) : WebSocketReference = {
            WebSocket  = websocket
            Subprotocol = subProtocol
            OnClose = onClose
            ID = webSocketID |> Option.defaultWith (fun _ -> Guid.NewGuid().ToString())
        }

/// A connection manager keeps track of all connections that are open at a specific endpoint.
type ConnectionManager(?messageSize) =
    let messageSize = defaultArg messageSize DefaultWebSocketOptions.ReceiveBufferSize

    let connections = new System.Collections.Concurrent.ConcurrentDictionary<string, WebSocketReference>()
   

    with

        /// Tries to find a WebSocket by ID. Returns None if the socket wasn't found.
        member __.TryGetWebSocket(websocketID:string) : WebSocketReference option = 
            match connections.TryGetValue websocketID with
            | true, r -> Some r
            | _ -> None

        /// Returns the number of WebSocket connections.
        member __.Count = connections.Count

        /// Sends a UTF-8 encoded text message to all WebSocket connections.
        member __.BroadcastTextAsync(msg:string,?cancellationToken:CancellationToken) = task {
            let byteResponse = System.Text.Encoding.UTF8.GetBytes msg
            
            let cancellationToken = cancellationToken |> Option.defaultValue CancellationToken.None
            let toRemove = System.Collections.Generic.List<_>()
            let! _ =
                connections
                |> Seq.map (fun kv -> task {
                    try
                        if not cancellationToken.IsCancellationRequested then
                            let webSocket = kv.Value.WebSocket
                            if webSocket.State = WebSocketState.Open then
                                try
                                    let segment = ArraySegment<byte>(byteResponse, 0, byteResponse.Length)
                                    do! webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken)
                                with 
                                | _ ->
                                    // TODO: Tracing 
                                    ()
                            else
                                toRemove.Add kv.Key
                    with
                    | _ -> () })
                |> Task.WhenAll
            
            for key in toRemove do
                match connections.TryRemove key with
                | true, reference ->
                    do! reference.OnClose()
                | _ -> ()

            return ()
        }


        /// Creates a new WebSocket connection and negotiates the subprotocol.
        member __.CreateSocket(onConnected:unit ->Task<unit>,onMessage: WebSocketReference -> string -> Task<unit>,onClose:unit ->Task<unit>,?webSocketID,?supportedProtocols:seq<WebSocketSubprotocol>,?cancellationToken) =
            fun next (ctx : Microsoft.AspNetCore.Http.HttpContext) -> task {
                let run(websocket) = task {
                    let webSocketID = webSocketID |> Option.defaultWith (fun _ -> Guid.NewGuid().ToString())
                    let reference = WebSocketReference.FromWebSocket(websocket,onClose,webSocketID=webSocketID)
                    connections.AddOrUpdate(reference.ID, reference, fun _ _ -> reference) |> ignore
                    let cancellationToken = cancellationToken |> Option.defaultValue CancellationToken.None
                    do! onConnected()

                    let mutable finished = false
            
                    while not finished do
                        try
                            let buffer = Array.zeroCreate messageSize
                            let! received = reference.WebSocket.ReceiveAsync(ArraySegment<byte> buffer, cancellationToken)
                            finished <- received.CloseStatus.HasValue
                            if finished then
                                do! reference.WebSocket.CloseAsync(received.CloseStatus.Value, received.CloseStatusDescription, cancellationToken)
                            else
                                if received.EndOfMessage then
                                    match received.MessageType with
                                    | WebSocketMessageType.Binary ->
                                        raise (NotImplementedException())
                                    | WebSocketMessageType.Text ->
                                        let! _r = 
                                            ArraySegment<byte>(buffer, 0, received.Count).Array
                                            |> System.Text.Encoding.UTF8.GetString
                                            |> fun s -> s.TrimEnd(char 0)
                                            |> onMessage reference
                                        ()
                                    | _ ->
                                        raise (NotImplementedException())
                        with
                        | _ ->
                            //TODO: Use giraffe/aspnet logging
                            finished <- true

                    match connections.TryRemove reference.ID with
                    | true, reference ->
                        do! reference.OnClose()
                    | _ -> ()
                    
                    return! Successful.ok (text "OK") next ctx
                }

                try
                    if ctx.WebSockets.IsWebSocketRequest then
                        let requestedSubProtocols = ctx.WebSockets.WebSocketRequestedProtocols
                        match supportedProtocols with
                        | Some supportedProtocols when requestedSubProtocols |> Seq.isEmpty |> not ->
                            match supportedProtocols |> Seq.tryFind (fun supported -> requestedSubProtocols |> Seq.contains supported.Name) with
                            | Some subProtocol ->
                                let! (websocket : WebSocket) = ctx.WebSockets.AcceptWebSocketAsync(subProtocol.Name)
                                use websocket = websocket
                                return! run websocket
                            | None ->
                                return! HttpStatusCodeHandlers.RequestErrors.badRequest (text "websocket subprotocol not supported") next ctx
                        | _ ->
                            let! (websocket : WebSocket) = ctx.WebSockets.AcceptWebSocketAsync()
                            use websocket = websocket
                            return! run websocket
                    else
                        return! HttpStatusCodeHandlers.RequestErrors.badRequest (text "no websocket request") next ctx
                with
                | exn -> return! HttpStatusCodeHandlers.RequestErrors.badRequest (text ("bad websocket request: " +  exn.Message)) next ctx
            }
