namespace Giraffe.Swagger

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

module Common =
  
  let joinMaps (p:Map<'a,'b>) (q:Map<'a,'b>) = 
      Map(Seq.concat [ (Map.toSeq p) ; (Map.toSeq q) ])
  
  let getVerb = Option.defaultWith (fun _ -> "get")
  
  let toString (o:obj) = o.ToString()
  
  type HttpVerb =
    | Get | Put | Post | Delete | Options | Head | Patch
    override __.ToString() =
      match __ with
      | Get -> "get" | Put -> "put"
      | Post -> "post" | Delete -> "delete"
      | Options -> "options" | Head -> "head"
      | Patch -> "patch"
    static member TryParse (text:string) =
        match text.ToLowerInvariant() with
        | "put" -> Some Put
        | "post" -> Some Post
        | "delete" -> Some Delete
        | "head" -> Some Head
        | "patch" -> Some Patch
        | "options" -> Some Options
        | _ -> None
    static member Parse (text:string) =
      text |> HttpVerb.TryParse |> Option.defaultWith (fun _ -> Get)
  
  let (|IsHttpVerb|_|) (prop:PropertyInfo) =
    HttpVerb.TryParse prop.Name
  
  let mergeMaps (m1:Map<'k,'v>) (m2:Map<'k,'v>) =
    m1
    |> Map.fold (
      fun state k v -> 
        if state |> Map.containsKey k
        then state
        else state.Add(k,v)
      ) m2
  
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
