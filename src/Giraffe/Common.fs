namespace Giraffe

open System
open System.IO

/// ---------------------------
/// Helper functions
/// ---------------------------

module Common =
    let inline isNotNull x = isNull x |> not

    let inline strOption (str : string) =
        if String.IsNullOrEmpty str then None else Some str

    let readFileAsStringAsync (filePath : string) =
        task {
            use stream = new FileStream(filePath, FileMode.Open)
            use reader = new StreamReader(stream)
            return! reader.ReadToEndAsync()
        }

/// ---------------------------
/// Useful computation expressions
/// ---------------------------

module ComputationExpressions =

    type OptionBuilder() =
        member __.Bind(v, f) = Option.bind f v
        member __.Return v   = Some v
        member __.Zero()     = None

    let opt = OptionBuilder()

    type ResultBuilder() =
        member __.Bind(v, f) = Result.bind f v
        member __.Return v   = Ok v

    let res = ResultBuilder()