module Giraffe.FormatExpressions

open System
open System.Text.RegularExpressions
open Microsoft.FSharp.Reflection
open FSharp.Core

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
        'b', ("(?i:(true|false)){1}",   (fun (s : string) -> bool.Parse s)   >> box)  // bool
        'c', ("([^/]{1})",              char                                 >> box)  // char
        's', ("([^/]+)",                decodeSlashes                        >> box)  // string
        'i', ("(-?\d+)",                int32                                >> box)  // int
        'd', ("(-?\d+)",                int64                                >> box)  // int64
        'f', ("(-?\d+\.{1}\d+)",        float                                >> box)  // float
        'O', (guidPattern,              parseGuid                            >> box)  // Guid
        'u', (shortIdPattern,           ShortId.toUInt64                     >> box)  // uint64
    ]

type MatchMode =
    | Exact                // Will try to match entire string from start to end.
    | StartsWith           // Will try to match a substring. Subject string should start with test case.
    | EndsWith             // Will try to match a substring. Subject string should end with test case.
    | Contains             // Will try to match a substring. Subject string should contain test case.

type MatchOptions = { IgnoreCase: bool; MatchMode: MatchMode; }
with
    static member Exact = { IgnoreCase = false; MatchMode = Exact }
    static member IgnoreCaseExact = { IgnoreCase = true; MatchMode = Exact }

let private convertToRegexPatternAndFormatChars (mode : MatchMode) (formatString : string) =
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

    let inline formatRegex mode pattern =
        match mode with
        | Exact -> "^" + pattern + "$"
        | StartsWith -> "^" + pattern
        | EndsWith -> pattern + "$"
        | Contains -> pattern

    formatString
    |> List.ofSeq
    |> convert
    |> (fun (pattern, formatChars) -> formatRegex mode pattern, formatChars)

/// **Description**
///
/// Tries to parse an input string based on a given format string and return a tuple of all parsed arguments.
///
/// **Parameters**
///
/// `format`: The format string which shall be used for parsing.
/// `input`: The input string from which the parsed arguments shall be extracted.
/// `options`: The options record with specifications on how the matching should behave.
///
/// **Output**
///
/// Matched value as an option of 'T
///
let tryMatchInput (format : PrintfFormat<_,_,_,_, 'T>) (options : MatchOptions) (input : string) =
    try
        let pattern, formatChars =
            format.Value
            |> Regex.Escape
            |> convertToRegexPatternAndFormatChars options.MatchMode

        let options =
            match options.IgnoreCase with
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

/// **Description**
///
/// Tries to parse an input string based on a given format string and return a tuple of all parsed arguments.
///
/// **Parameters**
///
/// `format`: The format string which shall be used for parsing.
/// `input`: The input string from which the parsed arguments shall be extracted.
/// `ignoreCase`: The flag to make matching case insensitive.
///
/// **Output**
///
/// Matched value as an option of 'T
///
let tryMatchInputExact (format : PrintfFormat<_,_,_,_, 'T>) (ignoreCase : bool) (input : string) =
    let options = match ignoreCase with
                  | true -> MatchOptions.IgnoreCaseExact
                  | false -> MatchOptions.Exact
    tryMatchInput format options input


// ---------------------------
// Validation helper functions
// ---------------------------

/// **Description**
///
/// Validates if a given format string can be matched with a given tuple.
///
/// **Parameters**
///
/// `format`: The format string which shall be used for parsing.
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
        'u' , typeof<uint64>         // guid
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

    let rec charTypeMatch ls mChar =
        match ls with
        | [] -> ()
        | (tChar, x) :: xs ->
            if tChar = mChar then
                parseChars <- (mChar,x) :: parseChars
                matches <- matches + 1
            else
                charTypeMatch xs mChar

    let rec typeCharMatch ls (xType : System.Type) =
        match ls with
        | [] -> sprintf "%s has no associated format char parameter" xType.Name
        | (tChar, x) :: xs ->
            if xType = x then
                sprintf "%s uses format char '%%%c'" xType.Name tChar
            else
                typeCharMatch xs xType

    for i in 0 .. path.Length - 1 do
        let mChar = path.[i]
        if matchNext then
            charTypeMatch mapping mChar
            matchNext <- false
        else
            if mChar = '%' then matchNext <- true

    if FSharpType.IsTuple(t) then
        let types = FSharpType.GetTupleElements(t)
        if types.Length <> matches then failwithf "Format string error: Number of parameters (%i) does not match number of tuple variables (%i)." types.Length matches

        let rec check(ls,pos) =
            match ls, pos with
            | [] , -1 -> ()
            | (mChar,ct) :: xs , i ->
                if ct <> types.[i] then
                    let hdlFmt = tuplePrint i (types.Length - 1) types.[i].Name
                    let expFmt = tuplePrint i (types.Length - 1)  ct.Name
                    let guidance = typeCharMatch mapping types.[i]

                    failwithf "Format string error: routef '%s' has type '%s' but handler expects '%s', mismatch on %s parameter '%%%c', %s." path expFmt hdlFmt (posPrint (i + 1)) mChar guidance
                else
                    check(xs,i - 1)
            | x , y -> failwithf "Format string error: Unknown validation error: %A [%i]." x y

        check( parseChars , types.Length - 1)

    else
        if matches <> 1 then failwithf "Format string error: Number of parameters (%i) does not match single variable." matches
        match parseChars with
        | [(mChar,ct)] ->
            if ct <> t then
                let guidance = typeCharMatch mapping t
                failwithf "Format string error: routef '%s' has type '%s' but handler expects '%s', mismatch on parameter '%%%c', %s." path ct.Name t.Name mChar guidance
        | x -> failwithf "Format string error: Unknown validation error: %A." x