module Giraffe.SwaggerUi

open System
open System.Collections.Generic
open System.IO
open System.IO.Compression
open Microsoft.AspNetCore.Http

let swaggerUiHandler (swaggerUiPath:string) swJsonPath =
  let handle (next : HttpFunc) (ctx : HttpContext) =
    let combineUrls (u1:string) (u2:string) =
      let sp = if u2.StartsWith "/" then u2.Substring 1 else u2
      u1 + sp
  
    let p =
      match ctx.Request.Path with
      | v when not v.HasValue -> "index.html"
      | v -> 
          let path = v.Value.Substring(swaggerUiPath.Length)
          if String.IsNullOrWhiteSpace path
          then "index.html"
          else path

    let assembly = System.Reflection.Assembly.GetExecutingAssembly()
    let rn = assembly.GetManifestResourceNames() |> Seq.find (fun n -> n.EndsWith "swagger-ui.zip")
    let fs = assembly.GetManifestResourceStream rn
    let zip = new ZipArchive(fs)
    match zip.Entries |> Seq.tryFind (fun e -> e.FullName = p) with
    | Some ze ->
      let mimetype =
        match System.IO.Path.GetExtension p with
        | ".htm"
        | ".html" -> "text/html"
        | ".css" -> "text/css"
        | ".js" -> "text/javascript"
        | ext when [".gif";".png";".jpeg";".jpg";".bmp";".webp"] |> List.contains ext -> sprintf "image/%s" ext
        | ext when ext.StartsWith "." -> "application/" + ext.Substring(1)
        | _ -> "application/octet-stream"
        
      ctx.Response.ContentType <- mimetype
      use r = new StreamReader(ze.Open())
      let bytes =
        r.ReadToEnd()
          .Replace("http://petstore.swagger.io/v2/swagger.json", (combineUrls "/" swJsonPath))
        |> r.CurrentEncoding.GetBytes
      setBody bytes next ctx
    | None ->
        (setStatusCode 404 >=> text "Ressource not found") next ctx
  
  routeStartsWithCi swaggerUiPath >=> handle


