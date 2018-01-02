module Giraffe.Swagger

open System
open System.Linq.Expressions
open Microsoft.FSharp.Quotations
open Quotations.DerivedPatterns
open Quotations.ExprShape
open Quotations.Patterns
open Microsoft.FSharp.Reflection
open FSharp.Quotations.Evaluator

let joinMaps (p:Map<'a,'b>) (q:Map<'a,'b>) = 
    Map(Seq.concat [ (Map.toSeq p) ; (Map.toSeq q) ])

let getVerb = Option.defaultWith (fun _ -> "GET")

let toString (o:obj) = o.ToString()

type ParamDescriptor =
  { Name:string
    Type:Type option
    In:ParamContainer
    Required:bool }
  static member InQuery n t =
    {Name=n; Type=(Some t); In=Query; Required=true}
  static member InForm n t =
      {Name=n; Type=(Some t); In=FormData; Required=true}
  static member Named n =
    {Name=n; Type=None; In=Query; Required=true}
and ParamContainer =
  | Query | Header | Path | FormData | Body
  override __.ToString() =
    match __ with
    | Query -> "query" | Header -> "header"
    | Path -> "path" | FormData -> "formData"
    | Body -> "body"

module Analyzer =
  
  type RouteInfos =
    { Verb:string
      Path:string
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
        ArgTypes : Type list ref
        Variables : Map<string, obj> ref
        Routes : RouteInfos list ref
        Responses : ResponseInfos list
        Verb : string option
        CurrentRoute : RouteInfos option ref
        Parameters : ParamDescriptor list
    }
    static member Empty
      with get () =
        {
            ArgTypes = ref List.empty
            Variables = ref Map.empty
            Routes = ref List.empty
            Verb = None
            Responses = List.empty
            CurrentRoute = ref None
            Parameters = List.empty
        }
    member __.PushRoute () =
      match !__.CurrentRoute with
      | Some route -> 
          let r = 
            { route 
                with 
                  Responses=(__.Responses @ route.Responses |> List.distinct)
                  Parameters=(__.Parameters @ route.Parameters) 
            }
          __.Routes := r :: !__.Routes
          __.CurrentRoute := None
          __.ArgTypes := []
          { __ with Parameters=List.Empty; Responses=[] }
      | None -> 
          __.ArgTypes := []
          let routes = 
              match !__.Routes with
              | route :: s -> 
                  { route 
                      with 
                        Responses=(__.Responses @ route.Responses |> List.distinct)
                        Parameters=(__.Parameters @ route.Parameters |> List.distinct) 
                  } :: s
              | v -> v
          __.Routes := routes |> List.distinct
          __
    member __.AddResponse code contentType (modelType:Type) =
      let rs = { StatusCode=code; ContentType=contentType; ModelType=modelType }
      { __ with Responses = rs :: __.Responses }
    member __.AddRoute verb parameters path =
      let ctx = __.PushRoute ()
      ctx.CurrentRoute := Some { Verb=verb; Path=path; Responses=[]; Parameters=( __.Parameters @ parameters) }
      ctx
    member __.AddParameter parameter =
      { __ with Parameters=(parameter :: __.Parameters) }
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
      
      let routes = 
        !__.Routes @ !other.Routes 
        |> List.map (fun route -> { route with Verb=(verb |> getVerb) })
      
      {
          ArgTypes = ref (!__.ArgTypes @ !other.ArgTypes)
          Variables = ref variables
          Routes = ref routes
          Verb = verb
          Responses = __.Responses @ other.Responses
          CurrentRoute = ref currentRoute
          Parameters = (__.Parameters @ other.Parameters) |> List.distinct
      }
  
  let mergeWith (a:AnalyzeContext) =
    a.MergeWith
  
  let pushRoute (a:AnalyzeContext) =
    a.PushRoute()
  
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
              (fun ctx -> (!ctx.Variables).Item "path" |> toString |> ctx.AddRoute (ctx.GetVerb()) List.empty)
          { ModuleName="HttpHandlers"; FunctionName="routeCi" }, 
              (fun ctx -> (!ctx.Variables).Item "path" |> toString |> ctx.AddRoute (ctx.GetVerb()) List.empty)
          
          // route format
          { ModuleName="HttpHandlers"; FunctionName="routef" }, 
              (fun ctx -> 
                let path = (!ctx.Variables).Item "pathFormat" :?> PathFormat
                let parameters = 
                  path.ArgTypes 
                  |> List.mapi(
                        fun i typ -> 
                          let name = (sprintf "arg%d" i)
                          ParamDescriptor.InQuery name typ)
                ctx.AddRoute (ctx.GetVerb()) parameters path.Template
              )
          
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
          
  //        { ModuleName="IFormCollection"; FunctionName="Item" }, (fun ctx -> ctx )
          
        ] |> Map
      { MethodCalls=methodCalls }
  
  let analyze webapp (rules:AppAnalyzeRules) : AnalyzeContext =
    let rec loop exp (ctx:AnalyzeContext) : AnalyzeContext =
  
      let newContext() = 
        { AnalyzeContext.Empty with 
            Responses = ctx.Responses
            ArgTypes = ctx.ArgTypes
            Parameters = ctx.Parameters
            Routes = ctx.Routes }
  
      let analyzeAll exps c =
        exps |> Seq.fold (fun state e -> loop e state) c
  
      match exp with
      | Value (o,_) -> 
          ctx.AddArgType (o.GetType())
      
      | Let (v, NewUnionCase (_,handlers), Lambda (next, Call (None, m, _))) when v.Name = "handlers" && m.Name = "choose" && m.DeclaringType.Name = "HttpHandlers" ->
      
          let ctxs = handlers |> List.map(fun e -> loop e ctx)
          { ctx 
              with 
                Routes = ref (ctxs |> List.collect (fun c -> !c.Routes) |> List.append(!ctx.Routes) |> List.distinct)
                Responses = (ctxs |> List.collect (fun c -> c.Responses) |> List.append(ctx.Responses)  |> List.distinct)
                CurrentRoute = ctx.CurrentRoute
          }
       
      | Let (id,op,t) -> 
          match op with
          | Value (o,_) -> 
              ctx.SetVariable id.Name (o.ToString()) |> loop t
          | o -> 
              analyzeAll [op;t] ctx
          
      | NewUnionCase (a,b) -> analyzeAll b ctx
      | Application (left, right) ->
          let c1 = loop right (newContext()) 
          let c2 = loop left (newContext())
          c1 |> mergeWith c2 |> pushRoute
          
      | Call(instance, method, args) when method.Name = "choose" && method.DeclaringType.Name = "HttpHandlers" ->
          let ctxs = args |> List.map(fun e -> loop e (newContext()))
          { ctx 
              with 
                Routes = ref (ctxs |> List.collect (fun c -> !c.Routes) |> List.append(!ctx.Routes) |> List.distinct)
                Responses = (ctxs |> List.collect (fun c -> c.Responses) |> List.append(ctx.Responses)  |> List.distinct)
                CurrentRoute = ctx.CurrentRoute
          }
      
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
  
  
module Generator =

  open System.Collections.Generic
  open Newtonsoft
  open Newtonsoft.Json
  open Newtonsoft.Json.Serialization
  open Newtonsoft.Json.Linq

  type JsonWriter with 
    member __.WriteProperty name (value:obj) =
      __.WritePropertyName name
      __.WriteValue value

  type RouteDescriptor =
    { Template: string
      Description: string
      Summary: string
      OperationId: string
      Produces: string list
      Consumes: string list
      Tags : string list
      Params: ParamDescriptor list
      Verb:HttpVerb
      Responses:IDictionary<int, ResponseDoc> }
    static member Empty =
      //let defaultResponses = dict [ (200, ResponseDoc.Default) ]
      { Template=""; Description=""; Params=[]; Verb=Get; Summary=""
        OperationId=""; Produces=[]; Responses=dict[]; Consumes=[]; Tags = [] }
  and ResponseDoc =
    { Description:string
      Schema:ObjectDefinition option }
    static member Default = {Description="Not documented"; Schema=None}
    member __.IsDefault() = __ = ResponseDoc.Default
  and HttpVerb =
    | Get | Put | Post | Delete | Options | Head | Patch
    override __.ToString() =
      match __ with
      | Get -> "get" | Put -> "put"
      | Post -> "post" | Delete -> "delete"
      | Options -> "options" | Head -> "head"
      | Patch -> "patch"
    static member Parse (text:string) =
        match text.ToLowerInvariant() with
        | "put" -> Put
        | "post" -> Post
        | "delete" -> Delete
        | "head" -> Head
        | "patch" -> Patch
        | "options" -> Options
        | _ -> Get
  and ApiDescription =
    { Title:string
      Description:string
      TermsOfService:string
      Version:string
      Contact:Contact
      License:LicenseInfos }
    static member Empty =
      { Title=""; Description=""; TermsOfService=""; Version="";
        Contact=Contact.Empty; License=LicenseInfos.Empty }
  and Contact =
    { Name:string; Url:string; Email:string }
    static member Empty = 
      { Name=""; Url=""; Email=null }
  and LicenseInfos =
    { Name:string; Url:string }
    static member Empty =
      { Name=""; Url="" }
  and ObjectDefinition =
    { Id:string
      Properties:IDictionary<string, PropertyDefinition> }
  and PathDefinition =
    { Summary:string
      Description:string
      OperationId:string
      Consumes:string list
      Produces:string list
      Tags:string list
      Parameters:ParamDefinition list
      Responses:IDictionary<int, ResponseDoc> }
    member __.ShouldSerializeParameters() =
      __.Parameters.Length > 0
  and PropertyDefinition =
    | Primitive of Type:string*Format:string
    | Ref of ObjectDefinition
    member __.ToJObject() : JObject =
      let v = JObject()
      match __ with
      | Primitive (t,f) ->
          v.Add("type", JToken.FromObject t)
          v.Add("format", JToken.FromObject f)
      | Ref ref ->
          v.Add("$ref", JToken.FromObject <| sprintf "#/definitions/%s" ref.Id)
      v
    member __.ToJson() : string =
      __.ToJObject().ToString()
  and ParamDefinition =
    { Name:string
      Type:PropertyDefinition option
      In:string
      Required:bool }
    member __.ToJObject() : JObject =
      let v = JObject()
      v.Add("name", JToken.FromObject __.Name)
      v.Add("in", JToken.FromObject __.In)
      v.Add("required", JToken.FromObject __.Required)
      match __.Type with
      | Some t ->
          match t with
          | Primitive (t,_) ->
              v.Add("type", JToken.FromObject t)
          | Ref _ ->
              v.Add("schema", t.ToJObject())
      | None -> ()
      v
    member __.ToJson() : string =
      __.ToJObject().ToString()
      
  and ApiDescriptionConverter() =
    inherit JsonConverter()
        override __.WriteJson(writer:JsonWriter,value:obj,_:JsonSerializer) =
          let d = unbox<ApiDescription>(value)

          writer.WriteStartObject()
          
          writer.WriteProperty "title" d.Title
          writer.WriteProperty "description" d.Description
          writer.WriteProperty "termsOfService" d.TermsOfService
          writer.WriteProperty "version" d.Version
          
          if not (d.Contact = Contact.Empty)
          then writer.WriteProperty "contact" d.Contact

          if not (d.License = LicenseInfos.Empty)
          then writer.WriteProperty "license" d.License

          writer.WriteEndObject()
          writer.Flush()
        override __.ReadJson(_:JsonReader,_:Type,_:obj,_:JsonSerializer) =
          unbox ""
        override __.CanConvert(objectType:Type) =
          objectType = typeof<ApiDescription>
  and ResponseDocConverter() =
    inherit JsonConverter()
        override __.WriteJson(writer:JsonWriter,value:obj,_:JsonSerializer) =
          let rs = unbox<ResponseDoc>(value)

          writer.WriteStartObject()
          writer.WritePropertyName "description"
          writer.WriteValue rs.Description

          writer.WritePropertyName "schema"
          writer.WriteStartObject()
          match rs.Schema with
          | Some sch ->
            writer.WritePropertyName "$ref"
            writer.WriteValue (sprintf "#/definitions/%s" sch.Id)
          | None ->()
          writer.WriteEndObject()

          writer.WriteEndObject()
          writer.Flush()
        override __.ReadJson(_:JsonReader,_:Type,_:obj,_:JsonSerializer) =
          unbox ""
        override __.CanConvert(objectType:Type) =
          objectType = typeof<ResponseDoc>
  and PropertyDefinitionConverter()=
    inherit JsonConverter()
        override __.WriteJson(writer:JsonWriter,value:obj,_:JsonSerializer) =
          let p = unbox<PropertyDefinition>(value)
          writer.WriteStartObject()
          writer.WriteRawValue (p.ToJson())
          writer.WriteEndObject()
          writer.Flush()
        override __.ReadJson(_:JsonReader,_:Type,_:obj,_:JsonSerializer) =
          unbox ""
        override __.CanConvert(objectType:Type) =
          objectType = typeof<PropertyDefinition>
  and ParamDefinitionConverter()=
    inherit JsonConverter()
        override __.WriteJson(writer:JsonWriter,value:obj,_:JsonSerializer) =
          let p = unbox<ParamDefinition>(value)
          writer.WriteRawValue (p.ToJson())
          writer.Flush()
        override __.ReadJson(_:JsonReader,_:Type,_:obj,_:JsonSerializer) =
          unbox ""
        override __.CanConvert(objectType:Type) =
          objectType = typeof<ParamDefinition>
  and DefinitionsConverter() =
    inherit JsonConverter()
      override __.WriteJson(writer:JsonWriter,value:obj,serializer:JsonSerializer) =
          let d = unbox<IDictionary<string,ObjectDefinition>>(value)
          writer.WriteStartObject()
          let c = ObjectDefinitionConverter()
          for k in d.Keys do
            writer.WritePropertyName k
            let v = d.Item k
            c.WriteJson(writer, v, serializer)
          writer.WriteEndObject()
          writer.Flush()
      override __.ReadJson(_:JsonReader,_:Type,_:obj,_:JsonSerializer) =
            unbox ""
      override __.CanConvert(objectType:Type) =
        typeof<IDictionary<string,ObjectDefinition>>.IsAssignableFrom objectType
  and ObjectDefinitionConverter() =
    inherit JsonConverter()
        override __.WriteJson(writer:JsonWriter,value:obj,_:JsonSerializer) =
          let d = unbox<ObjectDefinition>(value)

          writer.WriteStartObject()
          writer.WritePropertyName "type"
          writer.WriteValue "object"
          writer.WritePropertyName "properties"
          
          writer.WriteStartObject()
          for p in d.Properties do
            writer.WritePropertyName p.Key
            writer.WriteRawValue (p.Value.ToJson())
          writer.WriteEndObject()

          writer.WriteEndObject()
          writer.Flush()
        override __.ReadJson(_:JsonReader,_:Type,_:obj,_:JsonSerializer) =
          unbox ""
        override __.CanConvert(objectType:Type) =
          objectType = typeof<ObjectDefinition>

      
  and ApiDocumentation =
    { Swagger:string
      Info:ApiDescription
      BasePath:string
      Host:string
      Schemes:string list
      Paths:IDictionary<string,IDictionary<HttpVerb,PathDefinition>>
      Definitions:IDictionary<string,ObjectDefinition> }
    member __.ToJson() =
      let settings = new JsonSerializerSettings(NullValueHandling = NullValueHandling.Ignore)
      settings.ContractResolver <- new CamelCasePropertyNamesContractResolver()
      settings.Converters.Add(new ApiDescriptionConverter())
      settings.Converters.Add(new ResponseDocConverter())
      settings.Converters.Add(new PropertyDefinitionConverter())
      settings.Converters.Add(new ObjectDefinitionConverter())
      settings.Converters.Add(new DefinitionsConverter())
      settings.Converters.Add(new ParamDefinitionConverter())
      JsonConvert.SerializeObject(__, settings)
  
  let mkRouteDoc (route:Analyzer.RouteInfos) : RouteDescriptor =
    { RouteDescriptor.Empty with 
        Template=route.Path
        Verb=HttpVerb.Parse route.Verb }
