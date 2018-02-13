namespace Giraffe.Swagger

open Giraffe
open System
open System.Linq.Expressions
open System.Reflection
open Microsoft.FSharp.Quotations
open Quotations.DerivedPatterns
open Quotations.ExprShape
open Quotations.Patterns
open Microsoft.FSharp.Reflection
open FSharp.Quotations.Evaluator
open Microsoft.AspNetCore.Http
open System.Collections.Generic
open Newtonsoft
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open Newtonsoft.Json.Linq
open Common

module Analyzer =
  
  type FormatParsed =
    | StringPart | CharPart | BoolPart | IntPart
    | DecimalPart | HexaPart
  type FormatPart =
    | Constant  of string
    | Parsed    of FormatParsed
  type FormatParser =
    { Parts:FormatPart list ref
      Buffer:char list ref
      Format:string
      Position:int ref }
    static member Parse f =
        { Parts = ref List.empty
          Buffer = ref List.empty
          Format = f
          Position = ref 0 }.Parse()
    member x.Acc (s:string) =
        x.Buffer := !x.Buffer @ (s.ToCharArray() |> Seq.toList)
    member x.Acc (c:char) =
        x.Buffer := !x.Buffer @ [c]
    member private x.Finished () =
        !x.Position >= x.Format.Length
    member x.Next() =
        if x.Finished() |> not then
            x.Format.Chars !x.Position |> x.Acc
            x.Position := !x.Position + 1
    member x.PreviewNext() =
        if !x.Position >= x.Format.Length - 1
        then None
        else Some (x.Format.Chars (!x.Position))
    member x.Push t =
        x.Parts := !x.Parts @ t
        x.Buffer := List.empty
    member x.StringBuffer skip =
        let c = !x.Buffer |> Seq.skip skip |> Seq.toArray
        new String(c)
    member x.Parse () =
        while x.Finished() |> not do
            x.Next()
            match !x.Buffer with
            | '%' :: '%' :: _ -> x.Push [Constant (x.StringBuffer 1)]
            | '%' :: 'b' :: _ -> x.Push [Parsed BoolPart]
            | '%' :: 'i' :: _
            | '%' :: 'u' :: _
            | '%' :: 'd' :: _ -> x.Push [Parsed IntPart]
            | '%' :: 'c' :: _ -> x.Push [Parsed StringPart]
            | '%' :: 's' :: _ -> x.Push [Parsed StringPart]
            | '%' :: 'e' :: _
            | '%' :: 'E' :: _
            | '%' :: 'f' :: _
            | '%' :: 'F' :: _
            | '%' :: 'g' :: _
            | '%' :: 'G' :: _ -> x.Push [Parsed DecimalPart]
            | '%' :: 'x' :: _
            | '%' :: 'X' :: _ -> x.Push [Parsed HexaPart]
            | _ :: _ ->
                let n = x.PreviewNext()
                match n with
                | Some '%' -> x.Push [Constant (x.StringBuffer 0)]
                | _ -> ()
            | _ -> ()
        if !x.Buffer |> Seq.isEmpty |> not then x.Push [Constant (x.StringBuffer 0)]
        !x.Parts

  type RouteInfos =
    { Verb:string
      Path:string
      MetaData:Map<string, string>
      Parameters:ParamDescriptor list
      Responses:ResponseInfos list }
  and ResponseInfos =
    { StatusCode:int
      ContentType:string
      ModelType:Type }   
  type PathFormat = 
    { Template:string
      ArgTypes:Type list }
  
  type AnalyzeContext =
    {
        ArgTypes : Type list
        Variables : Map<string, obj>
        Routes : RouteInfos list
        Responses : ResponseInfos list
        Verb : string option
        CurrentRoute : RouteInfos option ref
        Parameters : ParamDescriptor list
        MetaData:Map<string, string>
    }
    static member Empty
      with get () =
        {
            ArgTypes = List.empty
            Variables = Map.empty
            Routes = List.empty
            Verb = None
            Responses = List.empty
            CurrentRoute = ref None
            Parameters = List.empty
            MetaData = Map.empty
        }
    member __.PushRoute () =
      match !__.CurrentRoute with
      | Some route -> 
          let meta = mergeMaps __.MetaData route.MetaData
          let r = 
            { route 
                with 
                  Responses=(__.Responses @ route.Responses |> List.distinct)
                  Parameters=(__.Parameters @ route.Parameters)
                  MetaData=meta
            }
          __.CurrentRoute := None
          { __ with Parameters=List.Empty; Responses=[]; ArgTypes=[]; Routes = r :: __.Routes; MetaData=Map.empty }
      | None ->
          let routes = 
              match __.Routes with
              | route :: s ->
                  let meta = mergeMaps __.MetaData route.MetaData 
                  { route 
                      with 
                        Responses=(__.Responses @ route.Responses |> List.distinct)
                        Parameters=(__.Parameters @ route.Parameters |> List.distinct)
                        MetaData=meta
                  } :: s
              | v -> v
          { __ with ArgTypes=[]; Routes = (List.distinct routes); }
    member __.AddResponse code contentType (modelType:Type) =
      let rs = { StatusCode=code; ContentType=contentType; ModelType=modelType }
      { __ with Responses = rs :: __.Responses }
    member __.AddRoute verb parameters path =
      let ctx = __.PushRoute ()
      ctx.CurrentRoute := Some { Verb=verb; Path=path; Responses=[]; Parameters=( __.Parameters @ parameters); MetaData=Map.empty }
      ctx
    member __.AddParameter parameter =
      { __ with Parameters=(parameter :: __.Parameters) }
    member __.ClearVariables () =
      { __ with Variables = Map.empty }
    member __.SetVariable name value =
      let vars = __.Variables
      let nvars = 
        if vars.ContainsKey name
        then vars.Remove(name).Add(name, value)
        else vars.Add(name, value)
      { __ with Variables = nvars }
    member __.AddArgType ``type`` =
      { __ with ArgTypes = (``type`` :: __.ArgTypes) }
    member __.GetVerb() =
      __.Verb |> getVerb 
    member __.MergeWith (other:AnalyzeContext) =
      let variables = joinMaps __.Variables other.Variables
      let verb = 
        match __.Verb, other.Verb with 
        | Some v, None -> Some v
        | None, Some v -> Some v
        | None, None -> None
        | Some v1, Some v2 -> Some v2
      
      let currentRoute = 
        match !__.CurrentRoute, !other.CurrentRoute with 
        | Some v, None -> Some v
        | None, Some v -> Some v
        | Some route1, Some route2 -> 
            Some { 
              route1 
                with 
                  Parameters = (route1.Parameters @ route2.Parameters) |> List.distinct
                  Responses = (route1.Responses @ route2.Responses) |> List.distinct
              }
        | None, None -> None
      let meta = mergeMaps __.MetaData other.MetaData
      {
          ArgTypes = __.ArgTypes @ other.ArgTypes
          Variables = variables
          Routes = __.Routes @ other.Routes
          Verb = verb
          Responses = __.Responses @ other.Responses
          CurrentRoute = ref currentRoute
          Parameters = (__.Parameters @ other.Parameters) |> List.distinct
          MetaData = meta
      }
  
  let mergeWith (a:AnalyzeContext) =
    a.MergeWith
  
  let pushRoute (a:AnalyzeContext) =
    a.PushRoute()
  
  let handleSingleArgRule argName funcName ctx =
    let arg =  
      match ctx.Variables.Item argName with
      | :? Type as typ -> typ.AssemblyQualifiedName
      | v -> toString v
    let m = ctx.MetaData.Add(funcName, arg)
    { ctx with MetaData=m }
  
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
          { ModuleName="Routing"; FunctionName="route" }, 
              (fun ctx -> ctx.Variables.Item "path" |> toString |> ctx.AddRoute (ctx.GetVerb()) List.empty)
          { ModuleName="Routing"; FunctionName="routeCi" }, 
              (fun ctx -> ctx.Variables.Item "path" |> toString |> ctx.AddRoute (ctx.GetVerb()) List.empty)
          
          // route format
          { ModuleName="Routing"; FunctionName="routef" }, 
              (fun ctx -> 
                let path = ctx.Variables.Item "pathFormat" :?> PathFormat
                let parameters = 
                  path.ArgTypes 
                  |> List.mapi(
                        fun i typ -> 
                          let name = (sprintf "arg%d" i)
                          ParamDescriptor.InPath name typ)
                ctx.AddRoute (ctx.GetVerb()) parameters path.Template
              )
          
          // used to return raw text content
          { ModuleName="Core"; FunctionName="setStatusCode" }, 
              (fun ctx -> 
                let code = ctx.Variables.Item "statusCode" |> toString |> Int32.Parse
                ctx.AddResponse code "text/plain" (typeof<string>)
              )

          // used to return raw text content
          { ModuleName="ResponseWriters"; FunctionName="text" }, 
              (fun ctx -> ctx.AddResponse 200 "text/plain" (typeof<string>))
              
          // used to return json content
          { ModuleName="ResponseWriters"; FunctionName="json" }, 
              (fun ctx ->
                  let modelType = 
                    match ctx.ArgTypes |> List.tryHead with
                    | Some t -> t
                    | None -> typeof<obj>
                  ctx.AddResponse 200 "application/json" modelType
              )
  
          // HTTP GET method
          { ModuleName="Core"; FunctionName="GET" }, (fun ctx -> { ctx with Verb = (Some "GET") })
          // HTTP POST method
          { ModuleName="Core"; FunctionName="POST" }, (fun ctx -> { ctx with Verb = (Some "POST") })
          // HTTP PUT method
          { ModuleName="Core"; FunctionName="PUT" }, (fun ctx -> { ctx with Verb = (Some "PUT") })
          // HTTP DELETE method
          { ModuleName="Core"; FunctionName="DELETE" }, (fun ctx -> { ctx with Verb = (Some "DELETE") })
          // HTTP PATCH method
          { ModuleName="Core"; FunctionName="PATCH" }, (fun ctx -> { ctx with Verb = (Some "PATCH") })
          
          { ModuleName="Dsl"; FunctionName="operationId" }, (handleSingleArgRule "opId" "operationId")
          { ModuleName="Dsl"; FunctionName="consumes" }, (handleSingleArgRule "modelType" "consumes")
          { ModuleName="Dsl"; FunctionName="produces" }, (handleSingleArgRule "modelType" "produces")
          
        ] |> Map
      { MethodCalls=methodCalls }

  let analyze webapp (rules:AppAnalyzeRules) : AnalyzeContext =
  
    let (|IsSubRoute|_|) (m:MethodInfo) =
      if (m.Name = "subRouteCi" || m.Name = "subRoute") && m.DeclaringType.Name = "Routing"
      then Some ()
      else None
  
    let rec loop exp (ctx:AnalyzeContext) : AnalyzeContext =
  
      let newContext() = 
        { AnalyzeContext.Empty with 
            Responses = ctx.Responses
            Verb = ctx.Verb
            ArgTypes = ctx.ArgTypes
            Parameters = ctx.Parameters
            MetaData = ctx.MetaData
            Variables = ctx.Variables
            Routes = ctx.Routes }
  
      let analyzeAll exps c =
        exps |> Seq.fold (fun state e -> loop e state) c
  
      match exp with
      | Value (o,_) -> 
          ctx.AddArgType (o.GetType())
      
      | Let (v, NewUnionCase (_,handlers), Lambda (next, Call (None, m, _))) when v.Name = "handlers" && m.Name = "choose" && m.DeclaringType.Name = "Core" ->
          let ctxs = handlers |> List.map(fun e -> loop e ctx)
          { ctx 
              with 
                Routes = (ctxs |> List.collect (fun c -> c.Routes) |> List.append ctx.Routes |> List.distinct)
                Responses = (ctxs |> List.collect (fun c -> c.Responses) |> List.append ctx.Responses  |> List.distinct)
                CurrentRoute = ctx.CurrentRoute
          }
       
      | Let (id,op,t) -> 
          match op with
          | Value (o,typ) when typ = typeof<Type> -> 
              let v = unbox<Type> o
              ctx.SetVariable id.Name v.AssemblyQualifiedName |> loop t
          | Value (o,_) -> 
              ctx.SetVariable id.Name (o.ToString()) |> loop t
          | Call (None, method, args) when method.Name = "TypeOf" ->
              let ty = method.GetGenericArguments() |> Seq.head
              ctx.SetVariable id.Name (ty.AssemblyQualifiedName) |> loop t
          | o -> 
              analyzeAll [o;t] ctx

      | NewUnionCase (_,exprs) ->
          let mustPush = 
            match exprs with
            | Let _ :: _ -> true
            | NewUnionCase _ :: _ -> true
            | _ ->
              match exprs |> List.tryLast with
              | None -> false
              | Some l -> 
                  match l with 
                  | NewUnionCase _ -> true
                  | _ -> false
          let r = analyzeAll exprs ctx
          if mustPush 
          then pushRoute r 
          else r

      | Application (Application (PropertyGet (None, op, _), PropertyGet (None, (IsHttpVerb verb), _)), exp) when op.Name = "op_GreaterEqualsGreater" ->
          let v = Some(verb.ToString())
          let ctx = { ctx with Verb=v }
          loop exp ctx
          
      | Application (Application (PropertyGet (None, op, []), Let (varname, Value (name,_), Lambda (_,  Call (None, method, _))) ), exp2) when op.Name = "op_EqualsEqualsGreater" ->
          let c2 = loop exp2 AnalyzeContext.Empty
          let vars = c2.Variables.Add (varname.Name, name)
          let c3 = { c2 with Variables=vars }
          let c4 = rules.ApplyMethodCall method.DeclaringType.Name method.Name c3
          c4 |> pushRoute |> mergeWith ctx |> pushRoute
      
      | Application (Application (PropertyGet (None, op, []), exp1 ), ValueWithName _) when op.Name = "op_GreaterEqualsGreater" ->
          let c1 = loop exp1 (newContext())
          let c = ctx |> pushRoute |> mergeWith c1 |> pushRoute
          c
          
      | Application (PropertyGet (instance, propertyInfo, pargs), Coerce (Var arg, o)) ->
          ctx.AddArgType arg.Type |> rules.ApplyMethodCall propertyInfo.DeclaringType.Name propertyInfo.Name
          
      | Application (left, right) ->
          let c1 = loop right (newContext()) 
          let c2 = loop left (newContext())
          c1 |> mergeWith c2 |> pushRoute
          
      | Call(instance, IsSubRoute, args) ->
          match args with
          | Value (v, t) :: args when t = typeof<string> ->
              let path = unbox<string> v
              let ctx2 = analyzeAll args AnalyzeContext.Empty
              let routes = 
                ctx2.Routes
                  |> List.map (
                      fun route -> { route with Path = (path + route.Path) })
              { ctx with Routes = (ctx.Routes @ routes) }
          | _ -> ctx
          
      | Call(instance, method, args) when method.Name = "choose" && method.DeclaringType.Name = "Core" ->
          let ctxs = args |> List.map(fun e -> loop e (newContext()))
          { ctx 
              with 
                Routes = (ctxs |> List.collect (fun c -> c.Routes) |> List.append ctx.Routes |> List.distinct)
                Responses = (ctxs |> List.collect (fun c -> c.Responses) |> List.append ctx.Responses  |> List.distinct)
                CurrentRoute = ctx.CurrentRoute
          }
      
      | Call (None, method, args) ->
          let parameters = method.GetParameters()
          let variables =
            parameters
            |> Array.mapi (fun i p -> i,p)
            |> Array.choose (
                 fun (i,p) ->
                    let arg = args.Item i
                    match arg with
                    | Call (None, m, []) -> 
                        None
                    | PropertyGet (None, prop, []) ->
                        let value = prop.GetValue(null)
                        Some (p.Name, p.ParameterType, value)
                    | _ -> None
               )         
          if Array.isEmpty variables
          then 
            let c1 = analyzeAll args ctx
            rules.ApplyMethodCall method.DeclaringType.Name method.Name c1
          else
            let vars = 
              variables 
              |> Array.fold (
                  fun (state:Map<string,obj>) (name,_, value) ->
                    state.Add(name, value))
                ctx.Variables
            let c3 = { ctx with Variables=vars }
            let c4 = rules.ApplyMethodCall method.DeclaringType.Name method.Name c3
            c4 |> pushRoute |> mergeWith ctx |> pushRoute
      | Call(instance, method, args) ->
          let c1 = analyzeAll args ctx
          rules.ApplyMethodCall method.DeclaringType.Name method.Name c1
          
      | PropertyGet (Some (PropertyGet (Some (PropertyGet (Some _, request, [])), form, [])), item, [Value (varname,_)]) ->
          let c = 
            match form.PropertyType.Name with
            | "IFormCollection" -> FormData
            | _ -> Query
          ctx.AddParameter {Name=(varname.ToString()); Type=None; In=c; Required=true}
      
      | PropertyGet (instance, propertyInfo, pargs) ->
          rules.ApplyMethodCall propertyInfo.DeclaringType.Name propertyInfo.Name ctx
              
      | Lambda(_, e2) -> 
          loop e2 ctx
      | IfThenElse(ifExp, thenExp, elseExp) ->
          analyzeAll [ifExp; thenExp; elseExp] ctx
      | Coerce (_,_) -> ctx
      | NewRecord (``type``,_) -> 
          ctx.AddArgType ``type``
      | Var _ -> ctx
      | NewObject(``constructor``, arguments) ->
          let t = ``constructor``.DeclaringType
          if t.IsGenericType
          then 
            let gt = t.GetGenericTypeDefinition()
            let td = typedefof<PrintfFormat<_,_,_,_,_>>
            if gt = td
            then
              match arguments with
              | [Value (o,ty)] when ty = typeof<string> ->
                  let argType = t.GetGenericArguments() |> Seq.last
                  let types =
                    if argType.IsGenericType
                    then argType.GetGenericArguments() |> Seq.toList
                    else [argType]
  
                  let format:PathFormat = { Template=(o.ToString()); ArgTypes=types }
                  ctx.SetVariable "pathFormat" format
              | _ -> ctx
            else ctx
          else ctx
      | TupleGet (tupledArg, i) ->
          ctx
      | e -> 
          //failwithf "not implemented %A" e
          printfn "not implemented %A" e
          ctx
    
    let ctx = AnalyzeContext.Empty
    let r = loop webapp ctx
    r.PushRoute()
  
  