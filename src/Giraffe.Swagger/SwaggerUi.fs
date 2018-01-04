module Giraffe.SwaggerUi

open System
open System.Collections.Generic
open System.IO
open System.IO.Compression
open Giraffe.HttpHandlers
open Microsoft.AspNetCore.Http

let swaggerUiHandler swJsonPath =
  fun (next : HttpFunc) (ctx : HttpContext) ->
    let combineUrls (u1:string) (u2:string) =
      let sp = if u2.StartsWith "/" then u2.Substring 1 else u2
      u1 + sp
  
    let p =
      match ctx.Request.Path with
      | v when not v.HasValue -> "index.html"
      | v -> v.Value

    let assembly = System.Reflection.Assembly.GetExecutingAssembly()
    let fs = assembly.GetManifestResourceStream "swagger-ui.zip"
    let zip = new ZipArchive(fs)
    match zip.Entries |> Seq.tryFind (fun e -> e.FullName = p) with
    | Some ze ->
      
      let mimetype =
        match System.IO.Path.GetExtension p with
        | ".htm"
        | ".html" -> "text/html"
        | ext when ext.StartsWith "." -> "application/" + ext.Substring(1)
        | _ -> "application/octet-stream"
        
      ctx.Response.ContentType <- mimetype
      use r = new StreamReader(ze.Open())
      let bytes =
        r.ReadToEnd()
          .Replace("http://petstore.swagger.io/v2/swagger.json", (combineUrls "/" swJsonPath))
        |> r.CurrentEncoding.GetBytes
      bytes |> setBody
    | None ->
        text "Ressource not found"
    
