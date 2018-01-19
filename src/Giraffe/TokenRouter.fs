module Giraffe.TokenRouter

open System.Collections.Generic
open System.Text
open Microsoft.FSharp.Reflection
open Printf
open Giraffe
open Giraffe.TokenParsers

// Implemenation of (router) Trie Node
// --------------------------------------
// Assumptions: Memory and compile time are not relevant.
// Optimised for execution speed (initially testing with Dictionary edges)

open NonStructuralComparison // needed for parser performance, non boxing of struct equality

// --------------------------------------
// Node Token (Radix) Tree using node mapping functions
// --------------------------------------

/// Tail Clip: clip end of 'str' string staring from int pos -> end
let inline private (-|) (str:string) (from:int) = str.Substring(from,str.Length - from)
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

type ParseFnCache(ifn:obj -> HttpHandler) =
    member val TupleFn : ( obj [] -> obj ) option = None with get,set
    member val fn = ifn with get

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
    member __.EdgeCount
        with get () = edges.Count
    member __.GetEdgeKeys = edges.Keys
    member __.TryGetValue v = edges.TryGetValue v
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
                    for _ in 0 .. depth do sb.Append("\t") |> ignore
                    sb  .Append(kvp.Key)
                        .Append(" => ") |> ignore
                    kvp.Value.ToString(depth + 1,sb)
                for _ in 0 .. depth do sb.Append("\t") |> ignore
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

    static member ExtendPath (node:Node) (path:string) (rc:ContType) =
        if path = "" then
            Node.AddFn node rc
            node
        else
            match node.TryGetValue path.[0] with
            | true, cnode ->
                Node.AddPath cnode path rc // recursive path scan
            | false, _    ->
                let nnode = Node(path)
                node.Edges.Add(path.[0], nnode)
                Node.AddFn nnode rc
                nnode

    static member AddPath (node:Node) (path:string) (rc:ContType) =

        //printfn "'%s' -> %s" path (node.ToString())

        match getPathMatch path node.Token with
        | ZeroToken ->
            // if node empty/root
            node.Token <- path
            Node.AddFn node rc
            node
        | ZeroMatch ->
            failwith <| sprintf "path passed to node with non-matching start in error:%s -> %s\n\n%s\n" path node.Token (node.ToString())
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
            | false, _    ->
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
| ApplyMatch of (char * (char []) * (Node)) // (parser , nextChar(s) , contNode) list
| ApplyMatchAndComplete of ( char * int * ParseFnCache) // (lastParser, No# Parsers, Cont Fn)
| MethodMatch of string * Node
    member x.Precedence
        with get () =
            match x with
            | MethodMatch _ -> 3
            | ApplyMatch (c,_,_) -> (int c)
            | ApplyMatchAndComplete (c,_,_) -> 256 + (int c)

and EndCont =
| HandlerMap of HttpHandler
| MatchComplete of ( (int) * ParseFnCache ) // ( No# Parsers, Cont Fn)
    member x.Precedence
        with get () =
            match x with
            | HandlerMap _ -> 1
            | MatchComplete _ -> 2

and ContType =
| Empty
| Mid of MidCont
| End of EndCont


// --------------------------------------
// Helper Functions
// --------------------------------------

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

// --------------------------------------
// Routing Node Map Functions used to build trie
// --------------------------------------

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
    Node.ExtendPath root path (fn |> HandlerMap |> End)

///**Description**
/// Matches and parses url values from a route using `printf` string formatting, results are passes to function as tuple.
///**Parameters**
///  * `path` : `PrintfFormat<_,_,_,_,'T>` - the route to match & parse using wildcard format of `printf`
///  * `fn` : `'T -> HttpHandler` - function that accepts the parsed tuple value and returns HttpHandler
///
///**Output Type**
///  * `parent` : `Node` - This parameter is applied by `router`, and is ommitted when building api such that function is partially applied fn
///  * `Node`
let routef (path : PrintfFormat<_,_,_,_,'T>) (fn:'T -> HttpHandler) (root:Node) =
    FormatExpressions.validateFormat path

// parsing route that iterates down nodes, parses, and then continues down further notes if needed
    let last = path.Value.Length - 1

    let rec go i ts pcount (node:Node) =
        let pl = path.Value.IndexOf('%',i)
        if pl < 0 || pl = last then
            //Match Complete (no futher parse '%' chars
            if pcount = 0 then
                failwith "'routef' (route Parse) used with no arguments? please add % format args or change to simple 'route' for non-parse routes"
            else
                Node.ExtendPath node (path.Value -| ts) ( MatchComplete( pcount , ParseFnCache( fun(o:obj) -> (o :?> 'T) |> fn ) ) |> End)  //todo: boxing & upcasting bad for performance, need to fix
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
                        Node.ExtendPath node (path.Value.Substring(ts,pl - ts)) (ApplyMatchAndComplete( fmtChar , pcount + 1 , ParseFnCache(fun (o:obj) -> o :?> 'T |> fn )) |> Mid)
                else //otherwise add mid pattern parse apply
                    //get node this parser will be on
                    let nnode = Node.ExtendPath node (path.Value.Substring(ts,pl - ts)) Empty
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
let private processPath (abort:HttpHandler) (root:Node) : HttpHandler =

    fun next ctx ->

        //let abort  = setStatusCode 404 >=> text "Not found"

        let path : string = ctx.Request.Path.Value
        let last = path.Length - 1

        let rec crawl (pos:int , node:Node, mf , ef) =
            if node.Token.Length > 0 then
                let cp = commonPathIndex path pos node.Token
                if cp = node.Token.Length then
                    let nxtChar = pos + node.Token.Length
                    if (nxtChar - 1 ) = last then //if have reached end of path through nodes, run HandlerFn
                        ef(node.EndFns, pos, [] )
                    else
                        match node.TryGetValue path.[nxtChar] with
                        | true, cnode ->
                            if (pos + cnode.Token.Length ) = last then //if have reached end of path through nodes, run HandlerFn
                                ef(cnode.EndFns, pos + node.Token.Length, [] )
                            else                //need to continue down chain till get to end of path
                                crawl (nxtChar,cnode,mf,ef)
                        | false, _ ->
                            // no further nodes, either a static url didnt match or there is a pattern match required
                            mf( node.MidFns, nxtChar, [] )
                else
                    abort next ctx
            elif node.Token.Length = 0 then
                match node.TryGetValue path.[pos] with
                | true, cnode ->
                    crawl (pos,cnode,mf,ef)
                | false, _ ->
                    // no further nodes, either a static url didnt match or there is a pattern match required
                    mf( node.MidFns, pos , [] )
            else
                //printfn ">> failed to match %s path with %s token, commonPath=%i" (path.Substring(pos)) (node.Token) (commonPathIndex path pos node.Token)
                abort next ctx

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
                | struct(false,_,_) -> getNodeCompletion cs (x1 + 1) node // char foundpart of match, not completion string

        let createResult (args:obj list) (argCount:int) (pfc:ParseFnCache) =
            let input =
                match argCount with
                | 0 -> failwith "Error: tried to create a parse result with zero arguments collected"
                | 1 -> match args with | [h] -> h | _ -> failwith (sprintf "Error: argument count 1 not matching parsed results :'%A'" args)
                | _ ->
                    let values = Array.zeroCreate<obj>(argCount) //<<< this can be pooled?

                    let rec revMapVals ls i = // List.rev |> List.toArray not used to minimise list traversal
                        match ls with
                        | [] -> ()
                        | h :: t ->
                            values.[i] <- h
                            revMapVals t (i - 1)

                    revMapVals args (argCount - 1)

                    match pfc.TupleFn with
                    | Some tf -> tf values
                    | None ->
                        let valuesTypes = Array.zeroCreate<System.Type>(argCount) //<<< this should be cached with handler
                        let rec revmapTypes ls i = // List.rev |> List.toArray not used to minimise list traversal
                            match ls with
                            | [] -> ()
                            | h :: t ->
                                valuesTypes.[i] <- h.GetType()
                                revmapTypes t (i - 1)

                        revmapTypes args (argCount - 1)

                        let tupleType = FSharpType.MakeTupleType valuesTypes
                        let tf = FSharpValue.PreComputeTupleConstructor(tupleType)
                        pfc.TupleFn <- Some tf // cache the tuple function (lazy eval)
                        tf values

            let resultTask = pfc.fn input next ctx
            task {
                let! result = resultTask
                match result with
                | Some _ -> return! resultTask
                | None -> return! abort next ctx
            }

        let rec processEnd (fns:EndCont list, _, args) =
            match fns with
            | [] -> abort next ctx
            | h :: _ ->
                match h with
                | HandlerMap fn ->
                    let resultTask = fn next ctx // run function with all parameters
                    task {
                        let! result = resultTask
                        match result with
                        | Some _ -> return! resultTask
                        | None -> return! abort next ctx
                    }
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
            | [] -> abort next ctx
            | h :: t ->
                match h with
                | MethodMatch (m,n) -> if ctx.Request.Method = m then crawl(pos,n,processMid,processEnd) else processMid(t,pos,args)
                | ApplyMatch x -> applyMatch x pos args t
                | ApplyMatchAndComplete x -> applyMatchAndComplete pos args x t

        // begin path crawl process
        crawl(0,root,processMid,processEnd)

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
    let child = Node.ExtendPath parent path Empty
    mapNode fns child
    child

let private methodFns (meth:string) (fns:(Node->Node) list) (parent:Node) =
    let child = Node("")
    mapNode fns child
    parent.AddMidFn <| MethodMatch(meth,child)
    child

let GET    fns = methodFns "GET"    fns
let POST   fns = methodFns "POST"   fns
let PUT    fns = methodFns "PUT"    fns
let DELETE fns = methodFns "DELETE" fns
let PATCH  fns = methodFns "PATCH"  fns

///**Description**
/// HttpHandler funtion that accepts a list of route mapping functions and builds a route tree for fast processing of request routes
///**Parameters**
///  * `abort` : `HttpHandler` - HttpHandler to call if no route matched eg : `notFound`
///  * `fns` : `(Node -> Node) list` - list of routing functions to route
///
///**Output Type**
///  * `HttpHandler`
let router (abort:HttpHandler) (fns:(Node->Node) list) : HttpHandler =
    let root = Node("")
    // precompile the route functions into node trie
    mapNode fns root

    //get path progress (if any so far)
    processPath abort root

let routerDbg (dbg:string -> unit)  (abort:HttpHandler) (fns:(Node->Node) list) : HttpHandler =
    let root = Node("")
    // precompile the route functions into node trie
    mapNode fns root

    dbg (root.ToString())

    //get path progress (if any so far)
    processPath abort root
