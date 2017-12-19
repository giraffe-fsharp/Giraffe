module Giraffe.FormatExpressions

open System.Text.RegularExpressions
open System.Net
open Microsoft.FSharp.Reflection
open FSharp.Core

let formatStringMap =
    dict [
    // Char    Regex                    Parser
    // ----------------------------------------------------------
        'b', ("(?i:(true|false)){1}",   bool.Parse           >> box)  // bool
        'c', ("(.{1})",                 char                 >> box)  // char
        's', ("(.+)",                   WebUtility.UrlDecode >> box)  // string
        'i', ("(-?\d+)",                int32                >> box)  // int
        'd', ("(-?\d+)",                int64                >> box)  // int64
        'f', ("(-?\d+\.{1}\d+)",        float                >> box)  // float
    ]

let convertToRegexPatternAndFormatChars (formatString : string) =
    let rec convert (chars : char list) =
        match chars with
        | '%' :: '%' :: tail ->
            let pattern, formatChars = convert tail
            "%" + pattern, formatChars
        | '%' :: c :: tail ->
            let pattern, formatChars = convert tail
            let regex, _ = formatStringMap.[c]
            regex + pattern, c :: formatChars
        | c :: tail ->
            let pattern, formatChars = convert tail
            c.ToString() + pattern, formatChars
        | [] -> "", []

    formatString.ToCharArray()
    |> Array.toList
    |> convert
    |> (fun (pattern, formatChars) -> sprintf "^%s$" pattern, formatChars)

let tryMatchInput (format : PrintfFormat<_,_,_,_, 'T>) (input : string) (ignoreCase : bool) =
    try
        let pattern, formatChars =
            format.Value
            |> Regex.Escape
            |> convertToRegexPatternAndFormatChars

        let options =
            match ignoreCase with
            | true  -> RegexOptions.IgnoreCase
            | false -> RegexOptions.None

        let result = Regex.Match(input, pattern, options)

        if result.Groups.Count <= 1
        then None
        else
            let groups =
                result.Groups
                |> Seq.cast<Group>
                |> Seq.skip 1

            let values =
                (groups, formatChars)
                ||> Seq.map2 (fun g c ->
                    let _, parser   = formatStringMap.[c]
                    let value       = parser g.Value
                    value)
                |> Seq.toArray

            let result =
                match values.Length with
                | 1 -> values.[0]
                | _ ->
                    let types =
                        values
                        |> Array.map (fun v -> v.GetType())
                    let tupleType = FSharpType.MakeTupleType types
                    FSharpValue.MakeTuple(values, tupleType)
            result
            :?> 'T
            |> Some
    with
    | _ -> None

let matchDict = 
    dict [
        'b', typeof<bool>           // bool
        'c', typeof<char>           // char
        's', typeof<string>         // string
        'i', typeof<int32>          // int
        'd', typeof<int64>          // int64
        'f', typeof<float>          // float
        'O', typeof<System.Guid>    // guid
]
let parseValidate (format : PrintfFormat<_,_,_,_, 'T>) =

    let t = typeof<'T>
    let path = format.Value
    let mutable parseChars = []
    let mutable matchNext = false
    let mutable matches = 0
    for i in 0 .. path.Length - 1 do
        let mchar = path.[i]
        if matchNext then
            match matchDict.TryGetValue mchar with
            | true , v -> 
                parseChars <- (mchar,v) :: parseChars
                matches <- matches + 1                            
            | false, _ -> ()
            matchNext <- false
        else
            if mchar = '%' then matchNext <- true

    if FSharpType.IsTuple(t) then 
        let types = FSharpType.GetTupleElements(t)
        if types.Length <> matches then failwithf "Error reading match expression: tuple of %i vs chars of %i" types.Length matches
        
        let rec check(ls,pos) =
            match ls, pos with
            | [] , -1 -> ()
            | (mchar,ct) :: xs , i -> 
                if ct <> types.[i] then 
                    failwithf "Error with routef parse types: '%%%c' for parse type %s does not match expected type %s at tuple param:%i" mchar ct.FullName types.[i].FullName (i + 1)
                else
                    check(xs,i - 1)
            | x , y -> failwithf "Unknown parse validation error: %A [%i]" x y                

        check( parseChars , types.Length - 1)                                        

    else
        if matches <> 1 then failwithf "Error reading match expression: %i match chars found when expecting one " matches
        match parseChars with
        | [(mchar,ct)] -> 
            if ct <> t then failwithf "Error with routef parse type: '%%%c' for parse type %s does not match expected type %s" mchar ct.FullName t.FullName
        | x -> failwithf "Unknown parse validation error: %A" x 

