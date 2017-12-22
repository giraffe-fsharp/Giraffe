module Giraffe.TokenParsers

open System
open NonStructuralComparison // needed for parser performance, non boxing of struct equality
open OptimizedClosures       // needed to apply multi-curry args at once with adapt (invoke method)

type Parser = FSharpFunc<string, int, int, struct(bool * obj)>

let inline private between x l u = (x - l) * (u - x) >= LanguagePrimitives.GenericZero

let inline private rtrn (o : obj) = struct (true, o)
let private failure = struct (false, Unchecked.defaultof<obj>)

/// Private Range Parsers that quickly try parse over matched range (all fpos checked before running in preceeding functions)

let private stringParse (path : string) ipos fpos = path.Substring(ipos, fpos - ipos + 1) |> box |> rtrn

let private  charParse (path : string) ipos _ = path.[ipos] |> box |> rtrn // this is not ideal method (but uncommonly used)

let private boolParse (path : string) ipos fpos =
    match path.Substring(ipos, fpos - ipos) with
    | "true"  | "True"  | "TRUE"  -> true  |> box |> rtrn
    | "false" | "False" | "FALSE" -> false |> box |> rtrn
    | _ -> failure

let private intParse (path : string) ipos fpos =
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
    | _   -> go (ipos)

let private int64Parse (path : string) ipos fpos =
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
    | _   -> go (ipos)

let private decDivide =
    [| 1.; 10.; 100.; 1000.; 10000.; 100000.; 1000000.; 10000000.; 100000000.; 100000000. |]
    |> Array.map (fun d -> 1. / d) // precompute inverse once at compile time

let private floatParse (path : string) ipos fpos =
    let mutable result    = 0.
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
                    result <- result + (charDiff * decDivide.[decPlaces]) // char is divided using multiplication of pre-computed divisors
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
    | _   -> go (ipos)

let private guidMap = [| 3; 2; 1; 0; 5; 4; 7; 6; 8; 9; 10; 11; 12; 13; 14; 15 |]

let private guidParse (path : string) ipos fpos =
    let byteAry = Array.zeroCreate<byte>(16)
    let mutable bytePos = 0
    let mutable byteCur = 0uy
    let mutable atHead  = true
    let rec go pos =
        if path.[pos] = '-' then // skip over '-' chars
            if pos < fpos then go (pos + 1) else failure
        else
            let cv =  byte path.[pos]

            let value =
                if  cv >= byte '0' then
                    if cv <= byte '9' then cv - byte '0'
                    elif cv >= byte 'A' then
                        if cv <= byte 'F' then cv - byte 'A' + 10uy
                        elif cv >= byte 'a' then
                            if cv <= byte 'f' then cv - byte 'a' + 10uy
                            else 255uy
                        else 255uy
                    else 255uy
                else 255uy

            if value = 255uy then
                failure
            else
                if atHead then
                    byteCur <- value <<< 4
                    atHead  <- false
                    go (pos + 1)   // continue iter
                else
                    byteAry.[guidMap.[bytePos]] <- byteCur ||| value
                    if bytePos = 15 then
                        Guid(byteAry) |> box |> rtrn
                    else
                        byteCur <- 0uy
                        atHead  <- true
                        bytePos <- bytePos + 1
                        go (pos + 1)   // continue iter
    //Start Parse
    go (ipos)

let formatMap =
    dict [
    // Char    Range Parser
    // ----------------------------------------------
        'b', (FSharpFunc<_,_,_,_>.Adapt boolParse  )  // bool
        'c', (FSharpFunc<_,_,_,_>.Adapt charParse  )  // char
        's', (FSharpFunc<_,_,_,_>.Adapt stringParse)  // string
        'i', (FSharpFunc<_,_,_,_>.Adapt intParse   )  // int
        'd', (FSharpFunc<_,_,_,_>.Adapt int64Parse )  // int64
        'f', (FSharpFunc<_,_,_,_>.Adapt floatParse )  // float
        'O', (FSharpFunc<_,_,_,_>.Adapt guidParse  )  // guid
    ]