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
      { Template=""; Description=""; Params=[]; Verb=Get; Summary=""
        OperationId=""; Produces=[]; Responses=dict[]; Consumes=[]; Tags = [] }
  and ResponseDoc =
    { Description:string
      Schema:ObjectDefinition option }
    static member Default = {Description="Not documented"; Schema=None}
    member __.IsDefault() = __ = ResponseDoc.Default
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
          if isNull t
          then
            failwith ""
          else
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
      
  type PathDefinition =
    { Summary:string
      Description:string
      OperationId:string
      Consumes:string list
      Produces:string list
      Tags:string list
      Parameters:ParamDefinition list
      Responses:Map<int, ResponseDoc> }
    static member Empty =
      { Summary=""
        Description=""
        OperationId=""
        Consumes=[]
        Produces=[]
        Tags=[]
        Parameters=[]
        Responses=Map [] }
    member __.ShouldSerializeParameters() =
      __.Parameters.Length > 0
    member __.AddResponse code mimetype description (modelType:Type) =
      let dt = modelType.Describes()
      let rs = __.Responses.Add(code, { Description=description; Schema=Some dt })
      let produces = mimetype :: __.Produces
      { __ with Responses=rs; Produces=produces }
    member __.AddConsume name mimetype (``in``:ParamContainer) (modelType:Type) =
      let dt = modelType.Describes()
      let parameters = 
          { Name=name
            Type=Some (PropertyDefinition.Ref dt)
            In=``in``.ToString()
            Required=true } :: __.Parameters
      let consumes = mimetype :: __.Consumes
      { __ with Consumes=consumes; Parameters=parameters }
        
      
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

  type DocumentationAddendumProvider = Analyzer.RouteInfos -> string * HttpVerb * PathDefinition -> string * HttpVerb * PathDefinition
  let DefaultDocumentationAddendumProvider = fun _ doc -> doc

  let convertRouteInfos (route:Analyzer.RouteInfos) (addendums:DocumentationAddendumProvider) : (string * HttpVerb * PathDefinition) =
    let verb = HttpVerb.Parse route.Verb
    
    let describeType (ty:Type) =
      if ty.IsSwaggerPrimitive
      then
         match ty.FormatAndName with
         | Some v -> Some(Primitive v)
         | None -> None
      else 
        Some (Ref(ty.Describes()))
    
    let consumedTypes =
      match route.MetaData |> Map.tryFind "consumes" with
      | Some t -> 
          let ty = t |> Type.GetType |> describeType
          [{ Name = "body"
             Type = ty
             In = ParamContainer.Body.ToString()
             Required=true }]
      | None -> []
        
    let parameters = 
      route.Parameters
       |> List.map (
           fun p ->
            let t = 
              match p.Type with 
              | None -> None 
              | Some ty -> describeType ty
            { Name = p.Name
              Type = t
              In = p.In.ToString()
              Required=p.Required })
       |> List.append consumedTypes
       
    let producedTypes =
      match route.MetaData |> Map.tryFind "produces" with
      | Some t -> 
          let ty = t |> Type.GetType
          let schema = 
            if ty.IsSwaggerPrimitive
            then None
            else Some (ty.Describes())
          Map [200, { ResponseDoc.Default with Schema=schema }]
      | None -> Map.empty
    
    let responses =
      route.Responses
      |> List.map(
           fun rs -> 
            let ty = rs.ModelType
            let schema = 
              if ty.IsSwaggerPrimitive
              then None
              else Some (ty.Describes())
            rs.StatusCode, { ResponseDoc.Default with Schema=schema } 
         )
      |> Map |> mergeMaps producedTypes
    let operationId = if route.MetaData.ContainsKey "operationId" then route.MetaData.["operationId"] else ""
    let consumes = 
      if parameters |> List.exists (fun p -> p.In = ParamContainer.FormData.ToString())
      then ["application/x-www-form-urlencoded"] else []
    
    let produces = []
    
    let pathDef =
      { Summary=""
        Description=""
        OperationId=operationId
        Consumes=consumes
        Produces=produces
        Tags=[]
        Parameters=parameters
        Responses=responses }
    let result =
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
    addendums route result

  let documentRoutes (routes:Analyzer.RouteInfos list) addendums =
    routes 
    |> Seq.map (fun r -> convertRouteInfos r addendums)
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

