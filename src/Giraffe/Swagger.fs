module Giraffe.Swagger

open System
open Microsoft.FSharp.Quotations
open Quotations.DerivedPatterns
open Quotations.ExprShape
open Quotations.Patterns
open Microsoft.FSharp.Reflection
open FSharp.Quotations.Evaluator

type RouteInfos =
  { Verb:string
    Path:string
    Responses:ResponseInfos list }
and ResponseInfos =
  { StatusCode:int
    ContentType:string
    ModelType:Type }

type AnalyzeContext =
  {
      Variables : Map<string, string> ref
      Routes : RouteInfos list ref
      Responses : ResponseInfos list ref
      Verb : string ref
      CurrentRoute : RouteInfos option ref
  }
  static member Empty
    with get () =
      {
          Variables = ref Map.empty
          Routes = ref []
          Verb = ref "GET"
          Responses = ref []
          CurrentRoute = ref None
      }
  member __.PushRoute () =
    match !__.CurrentRoute with
    | Some route -> 
        let r = { route with Responses=(!__.Responses) }
        __.Routes := r :: !__.Routes
        __.CurrentRoute := None
        __.Responses := []
    | None -> ()
  member __.AddResponse code contentType (modelType:Type) =
    let rs = { StatusCode=code; ContentType=contentType; ModelType=modelType }
    __.Responses := rs :: !__.Responses
  member __.AddRoute verb path =
    __.PushRoute ()
    __.CurrentRoute := Some { Verb=verb; Path=path; Responses=[] }
  member __.ClearVariables () =
    { __ with Variables = ref Map.empty }
  member __.SetVariable name value =
    let vars = !__.Variables
    if vars.ContainsKey name
    then vars.Remove(name).Add(name, value)
    else vars.Add(name, value)
    |> fun vars -> __.Variables := vars

type MethodCallId = 
  { ModuleName:string
    FunctionName:string }
type AnalyzeRuleBody = AnalyzeContext -> AnalyzeContext
type AppAnalyzeRules =
  { MethodCalls:Map<MethodCallId, AnalyzeRuleBody> }
  member __.ApplyMethodCall moduleName functionName ctx =
    let key = { ModuleName=moduleName; FunctionName=functionName }
    if __.MethodCalls.ContainsKey key
    then
      let v = __.MethodCalls.Item key
      v ctx
    else ctx

  static member Default =
    let methodCalls = 
      [ 
        // simple route
        { ModuleName="HttpHandlers"; FunctionName="route" }, 
            (fun ctx -> 
                let path = (!ctx.Variables).Item "path"
                ctx.AddRoute (!ctx.Verb) path
                ctx)
        
        // text used to return raw text content
        { ModuleName="HttpHandlers"; FunctionName="text" }, 
            (fun ctx -> 
                let path = (!ctx.Variables).Item "path"
                ctx.AddResponse 200 "text/plain" (typeof<string>)
                ctx)

      ] |> Map
    { MethodCalls=methodCalls }

let analyze webapp (rules:AppAnalyzeRules) =
  let rec loop exp (ctx:AnalyzeContext)  =

    let analyzeAll exps c =
      exps |> Seq.iter (fun e -> loop e c)

    match exp with
    | Let (id,op,t) -> 
          match op with
          | Value (o,_) -> ctx.SetVariable id.Name (o.ToString())
          | _ -> ()
          loop t ctx
    | NewUnionCase (a,b) -> analyzeAll b ctx
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
          
          rules.ApplyMethodCall method.DeclaringType.Name method.Name ctx |> ignore
    | Lambda(_, e) -> 
          loop e ctx
    | e -> printfn "not implemented %A" e
  
  let ctx = AnalyzeContext.Empty
  loop webapp ctx
  ctx.PushRoute()
  ctx

