module Giraffe.ComputationExpressions

open System

type OptionBuilder() =
    member x.Bind(v, f) = Option.bind f v
    member x.Return v   = Some v
    member x.Zero()     = None

let opt = OptionBuilder()

type ResultBuilder() =
    member x.Bind(v, f) = Result.bind f v
    member x.Return v   = Ok v

let res = ResultBuilder()