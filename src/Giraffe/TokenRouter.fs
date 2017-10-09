module Giraffe.TokenRouter


open System.Threading.Tasks
open FSharp.Core.Printf
open Microsoft.AspNetCore.Http
open System.Collections.Generic
open Microsoft.FSharp.Reflection
open Giraffe.HttpHandlers
open System.Text

// implimenation of (router) Trie Node
// assumptions: memory and compile time not relevant, all about execution speed, initially testing with Dictionary edges

open NonStructuralComparison // needed for parser performance, non boxing of struct equality
open OptimizedClosures       // needed to apply multi-curry args at once with adapt (invoke method)

type Parser = FSharpFunc<string,int,int,struct(bool*obj)>

let inline between x l u = (x - l) * (u - x) >= LanguagePrimitives.GenericZero

let inline rtrn (o:obj) = struct (true ,o)
let failure      = struct (false,Unchecked.defaultof<obj>)

/// Private Range Parsers that quickly try parse over matched range (all fpos checked before running in preceeding functions)

let private stringParse (path:string) ipos fpos = path.Substring(ipos,fpos - ipos + 1) |> box |> rtrn

let private  charParse (path:string) ipos _ = path.[ipos] |> box |> rtrn // this is not ideal method (but uncommonly used)

let private boolParse (path:string) ipos fpos =
    if between (fpos - ipos) 4 5 then 
        match path.[ipos] with
        | 't' | 'T' -> true  |> box |> rtrn // todo: Lazy matching, i'll complete later
        | 'f' | 'F' -> false |> box |> rtrn
        | _ -> failure
    else failure

let private intParse (path:string) ipos fpos =

    let mutable result = 0
    let mutable negNumber = false
    let rec go pos =
        let charDiff = int path.[pos] - int '0'
        if between charDiff 0 9 then
            result <- (result * 10) + charDiff
            if pos = fpos then
                if negNumber then - result else result 
                |> box |> rtrn 
            else go (pos + 1)       // continue iter
        else failure
    //Start Parse taking into account sign operator
    match path.[ipos] with
    | '-' -> negNumber <- true ; go (ipos + 1)
    | '+' -> go (ipos + 1)
    | _ -> go (ipos)
    
let private int64Parse (path:string) ipos fpos =

    let mutable result = 0L
    let mutable negNumber = false
    let rec go pos =
        let charDiff = int64 path.[pos] - int64 '0'
        if between charDiff 0L 9L then
            result <- (result * 10L) + charDiff
            if pos = fpos then
                if negNumber then - result else result 
                |> box |> rtrn 
            else go (pos + 1)       // continue iter
        else failure
    //Start Parse taking into account sign operator
    match path.[ipos] with
    | '-' -> negNumber <- true ; go (ipos + 1)
    | '+' -> go (ipos + 1)
    | _ -> go (ipos)


let private decPower = [|1.;10.;100.;1000.;10000.;100000.;1000000.;10000000.;100000000.;100000000.|] 
let private decDivide = [|1.;10.;100.;1000.;10000.;100000.;1000000.;10000000.;100000000.;100000000.|] |> Array.map (fun d -> 1. / d) // precompute inverse once at compile time
    
let private floatParse (path:string) ipos fpos =
    let mutable result = 0.
    let mutable decPlaces = 0
    let mutable negNumber = false
    
    let rec go pos =
        if path.[pos] = '.' then
            decPlaces <- 1
            if pos < fpos then go (pos + 1) else failure
        else
            let charDiff = float path.[pos] - float '0'
            if between charDiff 0. 9. then
                if decPlaces = 0 then 
                    result <- (result * 10.) + charDiff
                else
                    //result <- result + charDiff 
                    result <- result + ( charDiff * decDivide.[decPlaces]) // char is divided using multiplication of pre-computed divisors
                    decPlaces <- decPlaces + 1
                if pos = fpos || decPlaces > 9 then
                    if negNumber then - result else result 
                    |> box |> rtrn
                else go (pos + 1)   // continue iter
            else failure   // Invalid Character in path

    //Start Parse taking into account sign operator
    match path.[ipos] with
    | '-' -> negNumber <- true ; go (ipos + 1)
    | '+' -> go (ipos + 1)
    | _ -> go (ipos)

// let floatParse2 (path:string) ipos fpos =
//     let mutable result = 0.
//     let mutable nominator = 0L
//     let mutable demoninator = 0L
//     let mutable decPlaces = 0
//     let mutable negNumber = false
    
//     let rec go pos =
//         if path.[pos] = '.' then
//             decPlaces <- 1
//             if pos < fpos then go (pos + 1) else failure
//         else
//             let charDiff = int path.[pos] - int '0'
//             if between charDiff 0 9 then
//                 if decPlaces = 0 then 
//                     nominator <- (nominator * 10L) + int64 charDiff
//                 else
//                     //result <- result + charDiff 
//                     demoninator <- (demoninator * 10L) + int64 charDiff 
//                     //result <- result + ( charDiff * decPower.[decPlaces]) // char is divided using multiplication of pre-computed divisors
//                     decPlaces <- decPlaces + 1
//                 if pos = fpos || decPlaces > 9 then
//                     (float nominator) + (float demoninator) * (decPower.[decPlaces]) * if negNumber then - 1. else 1. 
//                     |> box |> rtrn 
//                 else go (pos + 1)   // continue iter
//             else failure   // Invalid Character in path

//     //Start Parse taking into account sign operator
//     match path.[ipos] with
//     | '-' -> negNumber <- true ; go (ipos + 1)
//     | '+' -> go (ipos + 1)
//     | _ -> go (ipos)


let formatMap =
    dict [
    // Char    Range Parser
    // ---------------  -------------------------------------------
        'b', (FSharpFunc<_,_,_,_>.Adapt boolParse  )  // bool
        'c', (FSharpFunc<_,_,_,_>.Adapt charParse  )  // char
        's', (FSharpFunc<_,_,_,_>.Adapt stringParse)  // string
        'i', (FSharpFunc<_,_,_,_>.Adapt intParse   )  // int
        'd', (FSharpFunc<_,_,_,_>.Adapt int64Parse )  // int64
        'f', (FSharpFunc<_,_,_,_>.Adapt floatParse )  // float
    ]

/////////////////////////////////////////////////////
// End of Parsers
/////////////////////////////////////////////////////

let routerKey = "router_pos"
let notFound : HttpHandler = setStatusCode 404 >=> text "Not found"
    // fun next ctx ->
    //     task {
    //         ctx.Response.StatusCode <- 404
    //         ctx.Response.Headers.["Content-Type"] <- StringValues("text/plain")
    //         let bytes = Encoding.UTF8.GetBytes "Not found"          
    //         ctx.Response.Headers.["Content-Length"] <- StringValues(bytes.Length.ToString())
    //         do! ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length)
    //         return Some ctx
    //     }

type RouteState(path:string) =
    member val path = path with get
    member val pos = 0 with get , set

////////////////////////////////////////////////////
// Node Token (Radix) Tree using node mapping functions
////////////////////////////////////////////////////

/// Tail Clip: clip end of 'str' string staring from int pos -> end
let inline (-|) (str:string) (from:int) = str.Substring(from,str.Length - from)

let private commonPathIndex (str1:string) (idx1:int) (str2:string) =
    let rec go i j =
        if i < str1.Length && j < str2.Length then
            if str1.[i] = str2.[j] 
            then go (i + 1) (j + 1)
            else j                
        else j
    go idx1 0 

let private commonPath (str1:string) (str2:string) =
    let rec go i =
        if i < str1.Length && i < str2.Length then
            if str1.[i] = str2.[i] 
            then go (i + 1)
            else i                               
        else i
    go 0

type PathMatch =
| SubMatch of int
| PathInToken
| TokenInPath
| ZeroToken
| ZeroMatch
| FullMatch

let private getPathMatch (path:string) (token:string) =
    if token.Length = 0 then ZeroToken
    else
        let cp = commonPath path token
        let tokenMatch = cp = token.Length
        let pathMatch = cp = path.Length
        if cp = 0 then ZeroMatch
        elif tokenMatch && pathMatch then FullMatch
        elif tokenMatch then TokenInPath
        elif pathMatch  then PathInToken
        else SubMatch cp

type Node(token:string) = 
    
    let mutable midFns = []
    let mutable endFns = []
    
    let addMidFn (mfn:MidCont) = midFns <- mfn :: midFns |> List.sortBy (fun f -> f.Precedence)
    let addEndFn (efn:EndCont) = endFns <- efn :: endFns |> List.sortBy (fun f -> f.Precedence) 
    
    let mutable edges = Dictionary<char,Node>()
    
    member __.Edges 
        with get() = edges
        and set v = edges <- v        
    member val Token = token with get,set
    
    member __.MidFns
        with get() = midFns 
        and set v = midFns <- v
    member __.AddMidFn = addMidFn
    member __.EndFns 
        with get()  = endFns 
        and set v = endFns <- v 
    member __.AddEndFn = addEndFn
    member x.EdgeCount 
        with get () = edges.Count
    member x.GetEdgeKeys = edges.Keys
    member x.TryGetValue v = edges.TryGetValue v

    override x.ToString() =
        let sb = StringBuilder()
        x.ToString(0, sb)
        sb.ToString()

    member x.ToString (depth:int, sb:StringBuilder) =
            sb  .Append("(")      
                .Append(x.Token)        
                .Append(",{")      
                .Append(sprintf "%A" midFns)      
                .Append("|")                      
                .Append(sprintf "%A" endFns)      
                .Append("},[")          |> ignore
            if x.Edges.Count = 0 then                  
                sb.Append("])\n")          |> ignore
            else
                sb.Append("\n")         |> ignore
                for kvp in x.Edges do
                    for i = 0 to depth do sb.Append("\t") |> ignore
                    sb  .Append(kvp.Key)
                        .Append(" => ") |> ignore
                    kvp.Value.ToString(depth + 1,sb)
                for i = 0 to depth do sb.Append("\t") |> ignore
                sb.Append("])\n")    |> ignore
    
    static member AddFn (node:Node) fn =
        match fn with
        | Empty -> ()
        | Mid mfn -> node.MidFns <- mfn :: node.MidFns |> List.sortBy (fun f -> f.Precedence)
        | End efn -> node.EndFns <- efn :: node.EndFns |> List.sortBy (fun f -> f.Precedence)

    static member Split (node:Node) (pos:int) =
        // need to split existing node out
        let sedges = node.Edges //get ref to pass to split node
        let baseToken = node.Token.Substring(0,pos) //new start base token
        let childToken = (node.Token -| pos)
        let snode = Node(childToken)
        node.Edges <- Dictionary<_,_>() //wipe edges from node before adding new edge
        node.Edges.Add(childToken.[0],snode)
        //node.Add childToken Empty // create split node
        node.Token <- baseToken
        snode.Edges <- sedges //pass old edges dictionary to split node

        //copy over existing functions
        snode.MidFns <- node.MidFns
        snode.EndFns <- node.EndFns
        //clear functions from existing node 
        node.MidFns <- List.empty
        node.EndFns <- List.empty 

    static member AddPath (node:Node) (path:string) (rc:ContType) =

        //printfn "'%s' -> %s" path (node.ToString())

        match getPathMatch path node.Token with
        | ZeroToken ->
            // if node empty/root
            node.Token <- path
            Node.AddFn node rc
            node
        | ZeroMatch ->
            if path = "" then
                Node.AddFn node rc
                node
            else
                // special case adding subroute (ignore subroute token)
                match node.TryGetValue path.[0] with
                | true, cnode ->
                    Node.AddPath cnode path rc // recursive path scan
                | fales, _    ->
                    let nnode = Node(path)
                    node.Edges.Add(path.[0], nnode)
                    Node.AddFn nnode rc
                    nnode
                //failwith <| sprintf "path passed to node with non-matching start in error:%s -> %s\n\n%s\n" path node.Token (node.ToString())
        | FullMatch -> 
            Node.AddFn node rc
            node
        | PathInToken ->
            Node.Split node (path.Length)
            Node.AddFn node rc
            node 
        | TokenInPath ->
            //path extends beyond this node
            let rem = path -| (node.Token.Length)
            match node.TryGetValue rem.[0] with
            | true, cnode ->
                Node.AddPath cnode rem rc // recursive path scan
            | fales, _    ->
                let nnode = Node(rem)
                node.Edges.Add(rem.[0], nnode)
                Node.AddFn nnode rc
                nnode
        | SubMatch (i) ->
            Node.Split node (i)
            let rem = path -| i
            let nnode = Node(rem)
            node.Edges.Add(rem.[0],nnode)
            Node.AddFn nnode rc
            nnode 
                        
// Route Continuation Functions    
and MidCont =
| SubRouteMap of HttpHandler
| ApplyMatch of (char * (char []) * (Node)) // (parser , nextChar , contNode) list
| ApplyMatchAndComplete of ( char * int * (obj -> HttpHandler)) // (lastParser, No# Parsers, Cont Fn)
    member x.Precedence
        with get () =
            match x with
            | SubRouteMap _ -> 1
            | ApplyMatch (c,_,_) -> (int c)
            | ApplyMatchAndComplete (c,_,_) -> 256 + (int c) 
and EndCont = 
| HandlerMap of HttpHandler
| MatchComplete of ( (int) * (obj -> HttpHandler) ) // ( No# Parsers, Cont Fn) 
    member x.Precedence
        with get () =
            match x with
            | HandlerMap _ -> 1
            | MatchComplete _ -> 2 
and ContType =
| Empty
| Mid of MidCont
| End of EndCont   


////////////////////////////////////////////////////
// Helper Functions
////////////////////////////////////////////////////

// temporary compose out handler to allow composition out of route functions, same as wraping in () or using <|
let inline (=>) (a:HttpHandler -> Node -> Node) (b:HttpHandler) = a b

let private addCharArray (c:char) (ary:char []) =
    if ary |> Array.exists (fun v -> v = c) then
        ary
    else 
        let nAry = Array.zeroCreate<_>(ary.Length + 1)
        Array.blit ary 0 nAry 0 ary.Length
        nAry.[ary.Length] <- c
        nAry

// helper to get child node of same match format (slow for now, needs optimisation)
let private getPostMatchNode fmt (nxt:char) (ils:MidCont list) = 
    let rec go (ls:MidCont list) (acc:MidCont list) (no:Node option) =
        match ls with
        | [] -> 
            match no with 
            | None -> 
                let n = Node("")
                n ,(ApplyMatch(fmt,[|nxt|],n)) :: acc |> List.sortBy (fun fn -> fn.Precedence)
            | Some n -> n, acc |> List.sortBy (fun fn -> fn.Precedence)
        | hfn :: tfns ->
            match hfn with
            | ApplyMatch (f,ncl,n) ->
                if f = fmt then
                    let nncl = addCharArray nxt ncl
                    go tfns (ApplyMatch(f,nncl,n)::acc) (Some(n))
                    // finished as found matched format but need to complete acc list
                else go tfns (hfn::acc) no
            | _ -> go tfns (hfn::acc) no
    go ils [] None
////////////////////////////////////////////////////
// Routing Node Map Functions used to build trie
////////////////////////////////////////////////////



///**Description**
/// if url path value matches request, the HttpHandler function is run.
///**Parameters**
///  * `path` : `string` - the route path to match to the incoming request
///  * `fn` : `HttpHandler` - function that accepts `HttpFunc` (Continuation), and `HttpContext`  
///
///**Output Type**
///  * `parent` : `Node` - This parameter is applied by `router`, and is ommitted when building api such that function is partially applied fn
///  * `Node`
let route (path:string) (fn:HttpHandler) (root:Node) = 
    // Simple route that iterates down nodes and if function found, execute as normal
    Node.AddPath root path (fn |> HandlerMap |> End)

let subRouteOld (path:string) (fn:HttpHandler) (root:Node) =
    Node.AddPath root path (fn |> SubRouteMap |> Mid)

///**Description**
/// Matches and parses url values from a route using `printf` string formatting, results are passes to function as tuple.
///**Parameters**
///  * `path` : `StringFormat<'a,'T>` - the route to match & parse using wildcard format of `printf`
///  * `fn` : `'T -> HttpHandler` - function that accepts the parsed tuple value and returns HttpHandler  
///
///**Output Type**
///  * `parent` : `Node` - This parameter is applied by `router`, and is ommitted when building api such that function is partially applied fn
///  * `Node`
let routef (path : StringFormat<_,'T>) (fn:'T -> HttpHandler) (root:Node)=
// parsing route that iterates down nodes, parses, and then continues down further notes if needed
    let last = path.Value.Length - 1

    let rec go i ts pcount (node:Node) =
        let pl = path.Value.IndexOf('%',i)
        if pl < 0 || pl = last then
            //Match Complete (no futher parse '%' chars
            if pcount = 0 then
                failwith "'routef' (route Parse) used with no arguments? please add % format args or change to simple 'route' for non-parse routes"
            else
                Node.AddPath node (path.Value -| ts) (MatchComplete( pcount , fun(o:obj) -> (o :?> 'T) |> fn ) |> End)  //todo: boxing & upcasting bad for performance, need to fix              
        else 
            let fmtChar = path.Value.[pl + 1]
            // overrided %% -> % case
            if fmtChar = '%' then
                //keep token start (+1 just one %), skip 
                go (pl + 2) (ts + 1) pcount node
            // formater with valid key
            else if formatMap.ContainsKey fmtChar then

                if pl + 1 = last then // if finishes in a parse
                    if node.MidFns |> List.exists (function | ApplyMatchAndComplete(c,_,_) -> fmtChar = c | _ -> false )
                    then sprintf "duplicate paths detected '%s', Trie Build skipping..." path.Value |> failwith
                    else
                        Node.AddPath node (path.Value.Substring(ts,pl - ts)) (ApplyMatchAndComplete( fmtChar , pcount + 1 , (fun (o:obj) -> o :?> 'T |> fn )) |> Mid)
                else //otherwise add mid pattern parse apply
                    //get node this parser will be on
                    let nnode = Node.AddPath node (path.Value.Substring(ts,pl - ts)) Empty
                    let cnode,midFns = getPostMatchNode fmtChar path.Value.[pl+2] nnode.MidFns                                                    
                    nnode.MidFns <- midFns //update adjusted functions
                    go (pl + 2) (pl + 2) (pcount + 1) cnode
            // badly formated format string that has unknown char after %
            else
                failwith (sprintf "Routef parsing error, invalid format char identifier '%c' , should be: b | c | s | i | d | f" fmtChar)
                go (pl + 1) ts pcount node

    go 0 0 0 root 

// choose root will apply its root node to all route mapping functions to generate Trie at compile time, function produced will take routeState (path) and execute appropriate function

// process path fn that returns httpHandler
let private processPath (rs:RouteState) (root:Node) : HttpHandler =
    fun next (ctx:HttpContext) -> //(succ:Continuation) (fail:Continuation)
    
        let path : string = rs.path
        let ipos = rs.pos
        let last = path.Length - 1

        let rec checkCompletionPath (pos:int) (node:Node) = // this funciton is only used by parser paths
            //this function doesn't test array bounds as all callers do so before
            let success(pos,node) = struct (true,pos,node)
            let failure(pos)      = struct (false,pos,Unchecked.defaultof<Node>)

            if commonPathIndex path pos node.Token = node.Token.Length then
                let nxtChar = pos + node.Token.Length
                if (nxtChar - 1) = last then //if this pattern match shares node chain as substring of another
                    if node.EndFns.IsEmpty
                    then failure pos //pos, None
                    else success(nxtChar,node) //nxtChar, Some node
                else
                    match node.TryGetValue path.[nxtChar] with
                    | true, cnode ->
                        checkCompletionPath nxtChar cnode
                    | false, _ ->
                        // no further nodes, either a static url didnt match or there is a pattern match required            
                        if node.MidFns.IsEmpty
                        then failure pos
                        else success(nxtChar,node)
            else failure pos
        
        /// (next match chars,pos,match completion node) -> (parse end,pos skip completed node,skip completed node) option
        let rec getNodeCompletion (cs:char []) pos (node:Node) =
            let success(prend,nxtpos,nxtnode) = struct (true,prend,nxtpos,nxtnode)
            let failure                       = struct (false,0,0,Unchecked.defaultof<Node>)

            match path.IndexOfAny(cs,pos) with // jump to next char ending (possible instr optimize vs node +1 crawl) 
            | -1 -> failure
            | x1 -> //x1 represents position of match close char but rest of chain must be confirmed 
                match checkCompletionPath x1 node with
                | struct(true,x2,cn) -> success(x1 - 1,x2,cn)                 // from where char found to end of node chain complete
                | struct(false,x2,_) -> getNodeCompletion cs (x1 + 1) node // char foundpart of match, not completion string

        let createResult (args:obj list) (argCount:int) (fn:obj -> HttpHandler) =
            let input =  
                match argCount with
                | 0 -> failwith "Error: tried to create a parse result with zero arguments collected" //Unchecked.defaultof<obj> //HACK: need routeF to throw error on zero args
                | 1 -> args.Head // HACK: look into performant way to safely extract
                | _ ->
                    let values = Array.zeroCreate<obj>(argCount) //<<< this can be pooled?
                    let valuesTypes = Array.zeroCreate<System.Type>(argCount) //<<< this should be cached with handler
                    let rec revmap ls i = // List.rev |> List.toArray not used to minimise list traversal
                        if i < 0 then ()
                        else
                            match ls with
                            | [] -> ()
                            | h :: t -> 
                                values.[i] <- h
                                valuesTypes.[i] <- h.GetType()
                                revmap t (i - 1)
                    revmap args (argCount - 1)
                    
                    let tupleType = FSharpType.MakeTupleType valuesTypes
                    FSharpValue.MakeTuple(values, tupleType)
            fn input next ctx

        let saveRouteState pos = 
            rs.pos <- pos
            ctx.Items.[routerKey] <- rs // probably redundant as rs ref persisted in dict

        let rec processEnd (fns:EndCont list, pos, args) =
            match fns with
            | [] -> notFound next ctx
            | h :: t ->
                match h with                    
                | HandlerMap fn -> fn next ctx // run function with all parameters
                | MatchComplete (i,fn) -> createResult args i fn 

        let rec processMid (fns:MidCont list,pos, args) =
            
            let applyMatchAndComplete pos acc ( f,i,fn ) tail =
                match formatMap.[f].Invoke(path,pos,last) with
                | struct(true, o) -> createResult (o :: acc) i fn
                | struct(false,_) -> processMid(tail, pos, acc) // ??????????????????
            
            let rec applyMatch (f:char,ca:char[],n) pos acc tail  =
                match getNodeCompletion ca pos n with
                | struct(true,fpos,npos,cnode) ->
                    match formatMap.[f].Invoke(path, pos, fpos) with
                    | struct(true, o) -> 
                        if npos - 1 = last then //if have reached end of path through nodes, run HandlerFn
                            processEnd(cnode.EndFns, npos, o::acc )
                        else
                            processMid(cnode.MidFns, npos, o::acc )
                    | struct(false,_) -> processMid(tail, pos, acc) // keep trying    
                | struct(false,_,_,_) -> processMid(tail, pos, acc) // subsequent match could not complete so fail
            
            match fns with
            | [] -> notFound next ctx 
            | h :: t ->
                match h with
                | ApplyMatch x -> applyMatch x pos args t
                | ApplyMatchAndComplete x -> applyMatchAndComplete pos args x t
                | SubRouteMap (fn) ->
                    saveRouteState pos
                    fn next ctx

        let rec crawl (pos:int) (node:Node) =
            let cp = commonPathIndex path pos node.Token 
            if cp = node.Token.Length then
                let nxtChar = pos + node.Token.Length 
                if (nxtChar - 1 ) = last then //if have reached end of path through nodes, run HandlerFn
                    processEnd(node.EndFns, pos, [] )
                else
                    match node.TryGetValue path.[nxtChar] with
                    | true, cnode ->
                        if (pos + cnode.Token.Length ) = last then //if have reached end of path through nodes, run HandlerFn
                            processEnd(cnode.EndFns, pos + node.Token.Length, [] )
                        else                //need to continue down chain till get to end of path
                            crawl (nxtChar) cnode
                    | false, _ ->
                        // no further nodes, either a static url didnt match or there is a pattern match required            
                        processMid( node.MidFns, nxtChar, [] )
            else 
                //printfn ">> failed to match %s path with %s token, commonPath=%i" (path.Substring(pos)) (node.Token) (commonPathIndex path pos node.Token)
                notFound next ctx          

        crawl ipos root

let private mapNode (fns:(Node->Node) list) (parent:Node) =
    let rec go ls =
        match ls with
        | [] -> ()
        | h :: t ->
            h parent |> ignore
            go t
    go fns

///**Description**
/// Subroute allows a group of routes with a common prefix to be grouped together in a subroute to avoid restating prefix each time.
///**Parameters**
///  * `path` : `string` - the prefix subroute path eg: "\api\"
///  * `fns` : `(Node -> Node) list` - A list of routing functions that are to be prefixed with path
///
///**Output Type**
///  * `parent` : `Node` - This parameter is applied by `router`, and is ommitted when building api such that function is partially applied fn
///  * `Node`
let subRoute (path:string) (fns:(Node->Node) list) (parent:Node) : Node =
    let child = Node.AddPath parent path Empty
    mapNode fns child
    child  

///**Description**
/// HttpHandler funtion that accepts a list of route mapping functions and builds a route tree for fast processing of request routes 
///**Parameters**
///  * `fns` : `(Node -> Node) list` - list of routing functions to route
///
///**Output Type**
///  * `HttpHandler`
let router (fns:(Node->Node) list) : HttpHandler =
    let root = Node("")
    // precompile the route functions into node trie
    mapNode fns root

    fun next ctx ->
        //get path progress (if any so far)
        let routeState =
            match ctx.Items.TryGetValue routerKey with
            | true, (v:obj) -> v :?> RouteState  
            | false,_-> RouteState(ctx.Request.Path.Value)
        processPath routeState root next ctx