module Giraffe.Swagger

open System
open System.Linq.Expressions
open Microsoft.FSharp.Quotations
open Quotations.DerivedPatterns
open Quotations.ExprShape
open Quotations.Patterns
open Microsoft.FSharp.Reflection
open FSharp.Quotations.Evaluator
open Microsoft.AspNetCore.Http

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
  static member InPath n t =
      {Name=n; Type=(Some t); In=Path; Required=true}
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
          __.CurrentRoute := None
          { __ with Parameters=List.Empty; Responses=[]; ArgTypes=[]; Routes = r :: __.Routes }
      | None -> 
          let routes = 
              match __.Routes with
              | route :: s -> 
                  { route 
                      with 
                        Responses=(__.Responses @ route.Responses |> List.distinct)
                        Parameters=(__.Parameters @ route.Parameters |> List.distinct) 
                  } :: s
              | v -> v
          { __ with ArgTypes=[]; Routes = (List.distinct routes) }
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
      
      let routes = 
        __.Routes @ other.Routes 
        |> List.map (fun route -> { route with Verb=(verb |> getVerb) })
      
      {
          ArgTypes = __.ArgTypes @ other.ArgTypes
          Variables = variables
          Routes = routes
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
              (fun ctx -> ctx.Variables.Item "path" |> toString |> ctx.AddRoute (ctx.GetVerb()) List.empty)
          { ModuleName="HttpHandlers"; FunctionName="routeCi" }, 
              (fun ctx -> ctx.Variables.Item "path" |> toString |> ctx.AddRoute (ctx.GetVerb()) List.empty)
          
          // route format
          { ModuleName="HttpHandlers"; FunctionName="routef" }, 
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
          { ModuleName="HttpHandlers"; FunctionName="text" }, 
              (fun ctx -> ctx.AddResponse 200 "text/plain" (typeof<string>))
          // used to return json content
          { ModuleName="HttpHandlers"; FunctionName="json" }, 
              (fun ctx ->
                  let modelType = 
                    match ctx.ArgTypes |> List.tryHead with
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
  
  let buildApp (webapp:Expr<HttpFunc -> HttpContext -> HttpFuncResult>) : HttpFunc -> HttpContext -> HttpFuncResult =
    QuotationEvaluator.Evaluate webapp
  
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
                Routes = (ctxs |> List.collect (fun c -> c.Routes) |> List.append ctx.Routes |> List.distinct)
                Responses = (ctxs |> List.collect (fun c -> c.Responses) |> List.append ctx.Responses  |> List.distinct)
                CurrentRoute = ctx.CurrentRoute
          }
       
      | Let (id,op,t) -> 
          match op with
          | Value (o,_) -> 
              ctx.SetVariable id.Name (o.ToString()) |> loop t
          | o -> 
              analyzeAll [op;t] ctx
          
      | NewUnionCase (a,b) -> analyzeAll b ctx

      | Application (Application (PropertyGet (None, op_GreaterEqualsGreater, []), exp1 ), ValueWithName _) ->
          let c1 = loop exp1 (newContext())
          let c = ctx |> pushRoute |> mergeWith c1 |> pushRoute
          c 
          
      | Application (PropertyGet (instance, propertyInfo, pargs), Coerce (Var arg, o)) ->
          ctx.AddArgType arg.Type |> 
            rules.ApplyMethodCall propertyInfo.DeclaringType.Name propertyInfo.Name
          
      | Application (left, right) ->
          let c1 = loop right (newContext()) 
          let c2 = loop left (newContext())
//          let c1 = loop right ctx
//          let c2 = loop left ctx
          c1 |> mergeWith c2 |> pushRoute
          
      | Call(instance, method, args) when method.Name = "choose" && method.DeclaringType.Name = "HttpHandlers" ->
          let ctxs = args |> List.map(fun e -> loop e (newContext()))
          { ctx 
              with 
                Routes = (ctxs |> List.collect (fun c -> c.Routes) |> List.append ctx.Routes |> List.distinct)
                Responses = (ctxs |> List.collect (fun c -> c.Responses) |> List.append ctx.Responses  |> List.distinct)
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
  
   module TypeHelpers =
        //http://swagger.io/specification/ -> Data Types
        let typeFormatsNames =
            [
              typeof<string>, ("string", "string")
              typeof<int8>, ("integer", "int8")
              typeof<int16>, ("integer", "int16")
              typeof<int32>, ("integer", "int32")
              typeof<int64>, ("integer", "int64")
              typeof<bool>, ("boolean", "")
              typeof<float>, ("float", "float32")
              typeof<float32>, ("float", "float32")
              typeof<uint8>, ("integer", "int8")
              typeof<uint16>, ("integer", "int16")
              typeof<uint32>, ("integer", "int32")
              typeof<uint64>, ("integer", "int64")
              typeof<DateTime>, ("string", "date-time")
              typeof<byte array>, ("string", "binary")
              typeof<byte list>, ("string", "binary")
              typeof<byte seq>, ("string", "binary")
              typeof<byte>, ("string", "byte")
              typeof<Guid>, ("string", "string")
            ] |> dict

    type Type with
      member this.IsSwaggerPrimitive
        with get () =
          TypeHelpers.typeFormatsNames.ContainsKey this
      member this.FormatAndName
        with get () =
          match this with
          | _ when TypeHelpers.typeFormatsNames.ContainsKey this ->
            Some (TypeHelpers.typeFormatsNames.Item this)
          | _ when this.IsPrimitive ->
            Some (TypeHelpers.typeFormatsNames.Item (typeof<string>))
          | _ -> None

      member this.Describes() : ObjectDefinition =

        let optionalType (t:Type) = 
          if (not t.IsGenericType) || t.GetGenericTypeDefinition() <> typedefof<Option<_>>
          then None
          else
            let arg = t.GenericTypeArguments |> Seq.exactlyOne
            Some arg

        let rec describe (t:Type) = 
          let descProp (tp:Type) name = 
            match tp.FormatAndName with
            | Some (ty,na) ->
                Some (name, Primitive(ty,na))
            | None ->
                let t' = tp
                if t = t'
                then
                  None
                else
                  let d = Ref(describe t')
                  Some (name, d)
          let props =
            t.GetProperties()
            |> Seq.choose (
                fun p ->
                  match optionalType p.PropertyType with
                  | Some t' -> descProp t' p.Name
                  | None -> descProp p.PropertyType p.Name
            ) |> Map
          {Id=t.Name; Properties=props}

        describe this
      
  type ApiDescriptionConverter() =
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
      Paths:Map<string, Map<HttpVerb,PathDefinition>>
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
  
    let responses = 
      route.Responses 
      |> List.map (
           fun rs ->
            let schema = if rs.ModelType.IsSwaggerPrimitive then None else Some (rs.ModelType.Describes())
            let model = { Description = sprintf "code %d returns %s" rs.StatusCode rs.ContentType
                          Schema = schema }
            (rs.StatusCode, model) )
      |> dict
    
    { RouteDescriptor.Empty with
        Responses=responses 
        Template=route.Path
        Verb=HttpVerb.Parse route.Verb
        Params=route.Parameters }

  //IDictionary<string,IDictionary<HttpVerb,PathDefinition>>
  let convertRouteInfos (route:Analyzer.RouteInfos) : (string * HttpVerb * PathDefinition) =
    let verb = HttpVerb.Parse route.Verb
    let parameters = 
      route.Parameters
      |> List.map (
           fun p ->
            let t = 
              match p.Type with 
              | None -> None 
              | Some ty ->
                  if ty.IsSwaggerPrimitive
                  then
                     match ty.FormatAndName with
                     | Some v -> Some(Primitive v)
                     | None -> None
                  else 
                    Some (Ref(ty.Describes()))
            { Name = p.Name
              Type = t
              In = p.In.ToString()
              Required=p.Required }
          )
          
    let responses : ResponseDoc list =
      route.Responses
      |> List.map(
           fun rs -> 
            let ty = rs.ModelType
            let schema = 
              if ty.IsSwaggerPrimitive
              then None
              else Some (ty.Describes())
            { ResponseDoc.Default 
                with Schema=schema } 
         )
          
    let pathDef =
      { Summary=""
        Description=""
        OperationId=""
        Consumes=[]
        Produces=[]
        Tags=[]
        Parameters=parameters
        Responses = dict [] }
    
    if parameters |> List.exists (fun p -> p.In = ParamContainer.Path.ToString())
    then 
      let parts = Analyzer.FormatParser.Parse route.Path
      let tmpl =
        parts
        |> List.fold (
            fun ((i,acc):(int*string)) (p:Analyzer.FormatPart) ->
              match p with
              | Analyzer.Constant c -> i, acc + c
              | Analyzer.Parsed _ ->
                  let pa = parameters.Item i 
                  (i+1), sprintf "%s{%s}" acc pa.Name
            ) (0,"")
        |> snd

      tmpl, verb, pathDef
    else
      route.Path, verb, pathDef

  let documentRoutes  (routes:Analyzer.RouteInfos list) =
    routes 
    |> Seq.map convertRouteInfos
    |> Seq.mapi (
         fun i (tmpl, verb, route) ->
          let operationId = sprintf "Operation %d" i
          if String.IsNullOrWhiteSpace route.OperationId 
          then (tmpl, verb, { route with OperationId=operationId })
          else (tmpl, verb, route)
       )
    |> Seq.groupBy (fun (tmpl, _, _) -> tmpl)
    |> Seq.map (
        fun (tmpl, items) ->
           let paths =
             items 
             |> Seq.map (fun (_, verb, route) -> (verb, route))
             |> Map
           tmpl, paths
       )
    |> Map
    
    
       
       