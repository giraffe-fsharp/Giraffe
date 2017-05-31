module Giraffe.HttpRouter

open Giraffe.HttpHandlers
open FSharp.Core.Printf
open System.Collections.Generic

/// Range Parsers that quickly try parse over matched range (all fpos checked before running)

let stringParse (path:string) ipos fpos = path.Substring(ipos,fpos - ipos) |> box |> Some

let charParse (path:string) ipos fpos = path.[ipos] |> box |> Some // this is not ideal method (but uncommonly used)
let boolParse (path:string) ipos fpos = 
    match path.[ipos] with
    | 't' | 'T' | 'y' | 'Y' -> true  |> box |> Some
    | 'f' | 'F' | 'n' | 'N' -> false |> box |> Some
    | _ -> None
let intParse (path:string) ipos fpos =
    let mutable result = 0
    let mutable negNumber = false
    let rec go pos =
        let charDiff = int path.[pos] - 48//- '0'
        if -1 < charDiff && charDiff < 10 then
            result <- (result * 10) + charDiff
            if pos = fpos then
                if negNumber then - result else result 
                |> box |> Some 
            else go (pos + 1)       // continue iter
        else None
    //Start Parse taking into account sign operator
    match path.[ipos] with
    | '-' -> negNumber <- true ; go (ipos + 1)
    | '+' -> go (ipos + 1)
    | _ -> go (ipos)
    
let int64Parse (path:string) ipos fpos =
    let mutable result = 0L
    let mutable negNumber = false
    let rec go pos =
        let charDiff = int64 path.[pos] - 48L//- '0'
        if -1L < charDiff && charDiff < 10L then
            result <- (result * 10L) + charDiff
            if pos = fpos then
                if negNumber then - result else result 
                |> box |> Some 
            else go (pos + 1)       // continue iter
        else None
    //Start Parse taking into account sign operator
    match path.[ipos] with
    | '-' -> negNumber <- true ; go (ipos + 1)
    | '+' -> go (ipos + 1)
    | _ -> go (ipos)

let floatParse (path:string) ipos fpos =
    let mutable result = 0.
    let mutable decPlaces = 0.
    let mutable negNumber = false
    
    let rec go pos =
        if path.[pos] = '.' then
            decPlaces <- 1.
            if pos < fpos then go (pos + 1) else None
        else
            let charDiff = float path.[pos] - 48. // - '0'
            if -1. < charDiff && charDiff < 10. then
                if decPlaces = 0. then 
                    result <- (result * 10.) + charDiff
                else
                    result <- result + ((float charDiff) / (10. * decPlaces))
                if pos = fpos then
                    if negNumber then - result else result 
                    |> box |> Some 
                else go (pos + 1)   // continue iter
            else None   // Invalid Character in path

    match path.[ipos] with
    | '-' -> negNumber <- true ; go (ipos + 1)
    | '+' -> go (ipos + 1)
    | _ -> go (ipos)

let formatStringMap =
    dict [
    // Char    Range Parser
    // ---------------  -------------------------------------------
        'b', (boolParse  )  // bool
        'c', (charParse  )  // char
        's', (stringParse)  // string
        'i', (intParse   )  // int
        'd', (int64Parse )  // int64
        'f', (floatParse )  // float
    ]

// implimenation of Trie Node

type Node(iRouteFn:RouteCont<'T>) = 
    let edges = Dictionary<char,Node>()
    member val RouteFn = iRouteFn with get,set 
    member x.Add v routeFn =
        match edges.TryGetValue v with
        | true, node -> 
            match routeFn with
            | EmptyMap -> () 
            | rf -> 
                x.RouteFn <- rf
            node
        | false, _ -> 
            let node = Node(routeFn)
            edges.Add(v,node)
            node

    member val Edges = edges with get

    member x.Search v =
        match edges.TryGetValue v with
        | true,node-> Some node
        | false,_-> None

    member x.HasCompletion (path:string) ipos =
        let fin = path.Length
        let rec go pos (node:Node) =
            if pos < fin then
                match node.Edges.TryGetValue path.[pos] with
                | true,cNode->
                    match node.RouteFn with
                    | EmptyMap -> go (pos + 1) cNode
                    | x -> Some x                    
                | false,_-> None
            else None
        go ipos x

        /// Multiple Edge designs possible, for now use simple dictionary over sorted array w/ binary search

        // let rec go l r = 
        //     if l > r then 
        //         None 
        //     else
        //         let m = (l + r) / 2
        //         if edges.[m].Char = v then edges.[m].Node |> Some
        //         else if edges.[m].Char < v then go (m + 1) r
        //         else if edges.[m].Char > v then go l (m - 1)
        //         else None
    
        // match edges.Count with 
        // | 0 -> None
        // | 1 -> if edges.[0].Char = v then edges.[0].Node |> Some else None 
        // | n -> go 0 (n - 1) 

and RouteCont<'T> =
| EmptyMap
| HandlerMap of HttpHandler
| MatchMap of ( char list * (Node option) * ('T -> HttpHandler))

let tRoute (path:string) (fn:HttpHandler) (root:Node)=
    let fin = path.Length - 1 
    let rec go i (node:Node) =
        if i = fin then
            node.Add path.[i] (HandlerMap fn)
        else
            let nextNode = node.Add path.[i] EmptyMap
            go (i + 1) nextNode
    go 0 root

let tRoutef (path : StringFormat<_,'U>) (fn:'U -> HttpHandler) (root:Node)=
    let fin = path.Value.Length - 1 
    let rec go i (node:Node) (fMap:char list) =
        if i = fin then
            node.Add path.Value.[i] (MatchMap( fMap , None ))
        else
            if path.Value.[i] = '%' && i + 1 < fin then
                let fmtChar = path.Value.[i + 1]
                if formatStringMap.ContainsKey fmtChar then
                    let nMatchMap = fmtChar :: fMap
                    let newNodeBranch = Node(EmptyMap) //doublr check logic & relation to i + 1 = 2
                    node.RouteFn <- MatchMap( nMatchMap , Some newNodeBranch )
                    go (i + 2) newNodeBranch nMatchMap
                else
                    failwith (sprintf "Routef parsing error, invalid format char '%c' , should be: b | c | s | i | d | f" fmtChar)
                    let nextNode = node.Add path.Value.[i] EmptyMap
                    go (i + 1) nextNode fMap
            else
                let nextNode = node.Add path.Value.[i] EmptyMap
                go (i + 1) nextNode fMap
    go 0 root []

let chooseRoute (fns:(Node->Node) list)  =
    let root = Node(EmptyMap)
    let rec go ls =
        match ls with
        | [] -> root
        | h :: t ->
            h root |> ignore
            go t
    go fns

let builtRoute = 
    chooseRoute [
        tRoute "/api" (Unchecked.defaultof<HttpHandler>)
        tRoute "/test" (Unchecked.defaultof<HttpHandler>)
]

let processPath (path:string) (ipos:int) (inode:Node) : HttpHandler =
    fun succ fail ctx -> 

        let pathLen = path.Length


        let findClosure (path:string) ipos (inode:Node) =
            let pathLen = path.Length
            let go pos node fin =
                if pos < pathLen then
                    match node.Edges.TryGetValue path.[pos] with
                    | true,cnode -> 
                        let nFin = 
                            match fin with
                            | Some v -> fin
                            | None -> Some(pos)
                        match cnode.RouteFn with
                        | EmptyMap -> go (pos + 1) node nFin
                        | x -> nFin
                    | false,_ -> go (pos + 1) node 
                else
                    None
            go ipos inode None
            

        let rec go pos node =
            match node.Search path.[pos] with
            | Some n ->
                match n.routeFn with
                | EmptyMap -> go (pos+1) n
                | HandlerMap fn -> fn
                | RouteCont.MatchMap (cls,nOpt,mfn) -> 
                    let matchBounds =
                        match nOpt with
                        | None -> (pos + 1 , path.Length - 1)
                        | Some snode -> 
                            match findClosure path pos snode with
                            | None -> (pos + 1 , path.Length - 1)
                            | Some fin -> (pos + 1, fin -1)
                    


                    parser pos node
            | None -> fail ctx