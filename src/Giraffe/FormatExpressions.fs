module Giraffe.FormatExpressions

open System
open System.Text.RegularExpressions
open FSharp.Core
open Microsoft.FSharp.Reflection

// ---------------------------
// String matching functions
// ---------------------------

let private formatStringMap =
    let decodeSlashes (str : string) =
        // Kestrel has made the weird decision to
        // partially decode a route argument, which
        // means that a given route argument would get
        // entirely URL decoded except for '%2F' (/).
        // Hence decoding %2F must happen separately as
        // part of the string parsing function.
        //
        // For more information please check:
        // https://github.com/aspnet/Mvc/issues/4599
        str.Replace("%2F", "/").Replace("%2f", "/")

    let parseGuid (str : string) =
        match str.Length with
        | 22 -> ShortGuid.toGuid str
        | _  -> Guid str

    let guidPattern =
        "([0-9A-Fa-f]{8}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{12}|[0-9A-Fa-f]{32}|[-_0-9A-Za-z]{22})"

    let shortIdPattern = "([-_0-9A-Za-z]{10}[048AEIMQUYcgkosw])"

    dict [
    // Char    Regex                    Parser
    // -------------------------------------------------------------
        'b', ("(?i:(true|false)){1}",   bool.Parse           >> box)  // bool
        'c', ("(.{1})",                 char                 >> box)  // char
        's', ("(.+)",                   decodeSlashes        >> box)  // string
        'i', ("(-?\d+)",                int32                >> box)  // int
        'd', ("(-?\d+)",                int64                >> box)  // int64
        'f', ("(-?\d+\.{1}\d+)",        float                >> box)  // float
        'O', (guidPattern,              parseGuid            >> box)  // Guid
        'u', (shortIdPattern,           ShortId.toUInt64     >> box)  // uint64
    ]

let private convertToRegexPatternAndFormatChars (formatString : string) =
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

/// **Description**
///
/// Tries to parse an input string based on a given format string and return a tuple of all parsed arguments.
///
/// **Parameters**
///
/// - `format`: The format string which shall be used for parsing.
/// - `input`: The input string from which the parsed arguments shall be extracted.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
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

// ---------------------------
// Validation helper functions
// ---------------------------

/// **Description**
///
/// Validates if a given format string can be matched with a given tuple.
///
/// **Parameters**
///
/// - `format`: The format string which shall be used for parsing.
///
/// **Output**
///
/// Returns `unit` if validation was successful otherwise will throw an `Exception`.
///
let validateFormat (format : PrintfFormat<_,_,_,_, 'T>) =

    let mapping = [
        'b' , typeof<bool>           // bool
        'c' , typeof<char>           // char
        's' , typeof<string>         // string
        'i' , typeof<int32>          // int
        'd' , typeof<int64>          // int64
        'f' , typeof<float>          // float
        'O' , typeof<System.Guid>    // guid
        'u' , typeof<uint64>    // guid
    ]

    let tuplePrint pos last name =
        let mutable result = "("
        for i in 0 .. last do
            if i = pos
            then result <- result + name
            else result <- result + "_"
            if i <> last then result <- result + ","
        result + ")"

    let posPrint =
        function
        | 1 -> "1st"
        | 2 -> "2nd"
        | 3 -> "3rd"
        | x -> x.ToString() + "th"

    let t = typeof<'T>
    let path = format.Value
    let mutable parseChars = []
    let mutable matchNext = false
    let mutable matches = 0

    let rec charTypeMatch ls mchar =
        match ls with
        | [] -> ()
        | (tchar,x) :: xs ->
            if tchar = mchar then
                parseChars <- (mchar,x) :: parseChars
                matches <- matches + 1
            else
                charTypeMatch xs mchar

    let rec typeCharMatch ls (xtype:System.Type) =
        match ls with
        | [] -> sprintf "%s has no associated format char parameter" xtype.Name
        | (tchar,x) :: xs ->
            if xtype = x then
                sprintf "%s uses format char '%%%c'" xtype.Name tchar
            else
                typeCharMatch xs xtype

    for i in 0 .. path.Length - 1 do
        let mchar = path.[i]
        if matchNext then
            charTypeMatch mapping mchar

            matchNext <- false
        else
            if mchar = '%' then matchNext <- true

    if FSharpType.IsTuple(t) then
        let types = FSharpType.GetTupleElements(t)
        if types.Length <> matches then failwithf "Format string error: Number of parameters (%i) does not match number of tuple variables (%i)." types.Length matches

        let rec check(ls,pos) =
            match ls, pos with
            | [] , -1 -> ()
            | (mchar,ct) :: xs , i ->
                if ct <> types.[i] then
                    let hdlFmt = tuplePrint i (types.Length - 1) types.[i].Name
                    let expFmt = tuplePrint i (types.Length - 1)  ct.Name
                    let guidance = typeCharMatch mapping types.[i]

                    failwithf "Format string error: routef '%s' has type '%s' but handler expects '%s', mismatch on %s parameter '%%%c', %s." path expFmt hdlFmt (posPrint (i + 1)) mchar guidance
                else
                    check(xs,i - 1)
            | x , y -> failwithf "Format string error: Unkown validation error: %A [%i]." x y

        check( parseChars , types.Length - 1)

    else
        if matches <> 1 then failwithf "Format string error: Number of parameters (%i) does not match single variable." matches
        match parseChars with
        | [(mchar,ct)] ->
            if ct <> t then
                let guidance = typeCharMatch mapping t
                failwithf "Format string error: routef '%s' has type '%s' but handler expects '%s', mismatch on parameter '%%%c', %s." path ct.Name t.Name mchar guidance
        | x -> failwithf "Format string error: Unkown validation error: %A." x