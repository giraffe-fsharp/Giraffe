module Giraffe.Swagger

open Microsoft.FSharp.Quotations
open Quotations.DerivedPatterns
open Quotations.ExprShape
open Quotations.Patterns
open Microsoft.FSharp.Reflection
open FSharp.Quotations.Evaluator

type AnalyzeContext =
  {
      Variables : Map<string, string> ref
      Routes : (string * string) list ref
  }
  static member Empty
    with get () =
      {
          Variables = ref Map.empty
          Routes = ref []
      }
  member __.AddRoute verb path =
    __.Routes := (verb, path) :: (!__.Routes)
  member __.ClearVariables () =
    { __ with Variables = ref Map.empty }
  member __.SetVariable name value =
    let vars = !__.Variables
    if vars.ContainsKey name
    then vars.Remove(name).Add(name, value)
    else vars.Add(name, value)
    |> fun vars -> __.Variables := vars

let rec analyze exp (ctx:AnalyzeContext) (verb:string) =

  let analyzeAll exps c =
    exps |> Seq.iter (fun e -> analyze e c verb)

  match exp with
  | Let (id,op,t) -> 
        printfn "let %A" id.Name
        printfn " - op %A" op
        printfn " - t %A" t
        match op with
        | Value (o,_) -> 
            ctx.SetVariable id.Name (o.ToString())
        | _ -> ()
        analyze t ctx verb
  | NewUnionCase (a,b) -> 
        printfn "UnionCase of type %A" a.DeclaringType.Name
        analyzeAll b ctx
  | Application (left, right) ->
        analyzeAll [left; right] ctx
  | Call(instance, method, args) ->
        let values =
            args
                |> List.choose (
                        function
                        | Value(varVal,_) -> Some varVal 
                        | e ->
                            printfn "not handled var %A" e 
                            None
                    )
        for a in args do
            printfn "a raw: [%A]" (a.ToString())
            
        printfn "values: [%A]" values
        printfn "Calling %s.%s with args [%A]" method.DeclaringType.Name method.Name args
        
        if method.Name = "route" && method.DeclaringType.Name = "HttpHandlers"
        then
            let path = (!ctx.Variables).Item "path"
            printfn "route %s" path
            ctx.AddRoute verb path
            
  | Lambda(_, e) -> 
        analyze e ctx verb
  | e -> printfn "not implemented %A" e



