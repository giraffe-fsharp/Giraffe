module Giraffe.Swagger

open System
open Microsoft.FSharp.Quotations
open Quotations.DerivedPatterns
open Quotations.ExprShape
open Quotations.Patterns
open Microsoft.FSharp.Reflection
open FSharp.Quotations.Evaluator

let joinMaps (p:Map<'a,'b>) (q:Map<'a,'b>) = 
    Map(Seq.concat [ (Map.toSeq p) ; (Map.toSeq q) ])

let getVerb = Option.defaultWith (fun _ -> "GET")

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
      ArgTypes : Type list ref
      Variables : Map<string, string> ref
      Routes : RouteInfos list ref
      Responses : ResponseInfos list ref
      Verb : string option
      CurrentRoute : RouteInfos option ref
  }
  static member Empty
    with get () =
      {
          ArgTypes = ref []
          Variables = ref Map.empty
          Routes = ref []
          Verb = None
          Responses = ref []
          CurrentRoute = ref None
      }
  member __.PushRoute () =
    match !__.CurrentRoute with
    | Some route -> 
        let r = { route with Responses=(!__.Responses |> List.distinct) }
        __.Routes := r :: !__.Routes
        __.CurrentRoute := None
        __.Responses := []
    | None -> ()
    __.ArgTypes := []
    __
  member __.AddResponse code contentType (modelType:Type) =
    let rs = { StatusCode=code; ContentType=contentType; ModelType=modelType }
    __.Responses := rs :: !__.Responses
    __
  member __.AddRoute verb path =
    __.PushRoute ()
    __.CurrentRoute := Some { Verb=verb; Path=path; Responses=[] }
    __
  member __.ClearVariables () =
    { __ with Variables = ref Map.empty }
  member __.SetVariable name value =
    let vars = !__.Variables
    if vars.ContainsKey name
    then vars.Remove(name).Add(name, value)
    else vars.Add(name, value)
    |> fun vars -> __.Variables := vars
    __
  member __.AddArgType ``type`` =
    __.ArgTypes := ``type`` :: !__.ArgTypes
    __
  member __.GetVerb() =
    __.Verb |> getVerb 
  member __.MergeWith (other:AnalyzeContext) =
    let variables = joinMaps !__.Variables !other.Variables
    let verb = 
      match __.Verb with 
      | Some v -> Some v
      | None ->
          match other.Verb with
          | Some v -> Some v
          | None -> None
    
    let currentRoute = 
      match !__.CurrentRoute with 
      | Some v -> Some v
      | None ->
          match !other.CurrentRoute with
          | Some v -> Some v
          | None -> None
    
    let routes = !__.Routes @ !other.Routes |> List.map (fun route -> { route with Verb=(verb |> getVerb) })
    {
        ArgTypes = ref (!__.ArgTypes @ !other.ArgTypes)
        Variables = ref variables
        Routes = ref routes
        Verb = verb
        Responses = ref (!__.Responses @ !other.Responses)
        CurrentRoute = ref currentRoute
    }

type MethodCallId = 
  { ModuleName:string
    FunctionName:string }
type AnalyzeRuleBody = AnalyzeContext -> AnalyzeContext
type AppAnalyzeRules =
  { MethodCalls:Map<MethodCallId, AnalyzeRuleBody> }
  member __.ApplyMethodCall moduleName functionName ctx =
    let key = { ModuleName=moduleName; FunctionName=functionName }
    if __.MethodCalls.ContainsKey key
    then ctx |> __.MethodCalls.Item key
    else ctx

  static member Default =
    let methodCalls = 
      [ 
        // simple route
        { ModuleName="HttpHandlers"; FunctionName="route" }, 
            (fun ctx -> (!ctx.Variables).Item "path" |> ctx.AddRoute (ctx.GetVerb()))
        
        // used to return raw text content
        { ModuleName="HttpHandlers"; FunctionName="text" }, 
            (fun ctx -> ctx.AddResponse 200 "text/plain" (typeof<string>))
        // used to return json content
        { ModuleName="HttpHandlers"; FunctionName="json" }, 
            (fun ctx ->
                let modelType = 
                  match !ctx.ArgTypes |> List.tryHead with
                  | Some t -> t
                  | None -> typeof<obj>
                ctx.AddResponse 200 "application/json" modelType
            )

        // HTTP GET method
        { ModuleName="HttpHandlers"; FunctionName="GET" }, (fun ctx -> { ctx with Verb = (Some "GET") })
        // HTTP POST method
        { ModuleName="HttpHandlers"; FunctionName="POST" }, (fun ctx -> { ctx with Verb = (Some "POST") })
        // HTTP PUT method
        { ModuleName="HttpHandlers"; FunctionName="PUT" }, (fun ctx -> { ctx with Verb = (Some "PUT") })
        // HTTP DELETE method
        { ModuleName="HttpHandlers"; FunctionName="DELETE" }, (fun ctx -> { ctx with Verb = (Some "DELETE") })
        // HTTP PATCH method
        { ModuleName="HttpHandlers"; FunctionName="PATCH" }, (fun ctx -> { ctx with Verb = (Some "PATCH") })
        
      ] |> Map
    { MethodCalls=methodCalls }

let analyze webapp (rules:AppAnalyzeRules) : AnalyzeContext =
  let rec loop exp (ctx:AnalyzeContext) : AnalyzeContext =

    let analyzeAll exps c =
      exps |> Seq.fold (fun state e -> loop e state) c

    match exp with
    | Value (o,_) -> 
        ctx.AddArgType (o.GetType())
    | Let (id,op,t) -> 
        match op with
        | Value (o,_) -> 
            ctx.SetVariable id.Name (o.ToString()) |> loop t
        | _ -> loop op ctx
        
    | NewUnionCase (a,b) -> analyzeAll b ctx
    | Application (left, right) ->
        let newContext() = 
          { AnalyzeContext.Empty with Responses = ctx.Responses; ArgTypes = ctx.ArgTypes }
        let c1 = loop right (newContext()) 
        let c2 = loop left (newContext())
        let c3 = (c1.MergeWith c2).PushRoute()
        (c3.MergeWith ctx).PushRoute()
        
    | PropertyGet (instance, propertyInfo, pargs) ->
        rules.ApplyMethodCall propertyInfo.DeclaringType.Name propertyInfo.Name ctx
    | Call(instance, method, args) ->
        let values =
            args
            |> List.choose (
                    function
                    | Value(varVal,_) -> Some varVal 
                    | e -> None
                ) 
        rules.ApplyMethodCall method.DeclaringType.Name method.Name ctx
    | Lambda(_, e2) -> 
        loop e2 ctx
    | IfThenElse(ifExp, thenExp, elseExp) ->
        analyzeAll [ifExp; thenExp; elseExp] ctx
    | Coerce (_,_) -> ctx
    | NewRecord (``type``,_) -> 
        ctx.AddArgType ``type``
    | Var _ -> ctx
    | e -> 
        printfn "not implemented %A" e
        ctx
  
  let ctx = AnalyzeContext.Empty
  let r = loop webapp ctx
  r.PushRoute()
  r

