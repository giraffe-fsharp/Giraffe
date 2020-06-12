/// <summary>
/// A collection of F# computation expressions:
///
/// `opt {}`: Enables control flow and binding of Option<'T> objects
/// `res {}`: Enables control flow and binding of Result<'T, 'TError> objects
/// </summary>
module Giraffe.ComputationExpressions

/// <summary>
/// Enables control flow and binding of `Option<'T>` objects
/// </summary>
/// <param name="unit"></param>
/// <returns></returns>
type OptionBuilder() =
    member __.Bind(v, f)   = Option.bind f v
    member __.Return v     = Some v
    member __.ReturnFrom v = v
    member __.Zero()       = None

let opt = OptionBuilder()

/// <summary>
/// Enables control flow and binding of <see cref="Microsoft.FSharp.Core.Result<'T, 'TError>" /> objects
/// </summary>
/// <param name="unit"></param>
/// <returns></returns>
type ResultBuilder() =
    member __.Bind(v, f) = Result.bind f v
    member __.Return v   = Ok v

let res = ResultBuilder()