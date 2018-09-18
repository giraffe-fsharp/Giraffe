/// **Description**
///
/// A collection of F# computation expressions:
///
/// - `opt {}`: Enables control flow and binding of Option<'T> objects
/// - `res {}`: Enables control flow and binding of Result<'T, 'TError> objects
///
module Giraffe.ComputationExpressions

type OptionBuilder() =
    member __.Bind(v, f)   = Option.bind f v
    member __.Return v     = Some v
    member __.ReturnFrom v = v
    member __.Zero()       = None

/// **Description**
///
/// Enables control flow and binding of `Option<'T>` objects
///
let opt = OptionBuilder()

type ResultBuilder() =
    member __.Bind(v, f) = Result.bind f v
    member __.Return v   = Ok v

/// **Description**
///
/// Enables control flow and binding of `Result<'T, 'TError>` objects
///
let res = ResultBuilder()