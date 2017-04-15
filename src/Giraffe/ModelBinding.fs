module Giraffe.ModelBinding

open System
open System.IO
open System.Reflection
open System.ComponentModel
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers
open Giraffe.Common
open Giraffe.HttpHandlers

let readBodyFromRequest (ctx : HttpHandlerContext) =
    async {
        let body = ctx.HttpContext.Request.Body
        use reader = new StreamReader(body, true)
        return! reader.ReadToEndAsync() |> Async.AwaitTask
    }

let bindJson<'T> (ctx : HttpHandlerContext) =
    async {
        let! body = readBodyFromRequest ctx
        return deserializeJson<'T> body
    }

let bindXml<'T> (ctx : HttpHandlerContext) =
    async {
        let! body = readBodyFromRequest ctx
        return deserializeXml<'T> body
    }

let bindForm<'T> (ctx : HttpHandlerContext) =
    async {
        let! form = ctx.HttpContext.Request.ReadFormAsync() |> Async.AwaitTask        
        let obj   = Activator.CreateInstance<'T>()
        let props = obj.GetType().GetProperties(BindingFlags.Instance ||| BindingFlags.Public)
        props
        |> Seq.iter (fun p -> 
            let strValue = ref (StringValues())
            if form.TryGetValue(p.Name, strValue)
            then
                let converter = TypeDescriptor.GetConverter p.PropertyType
                let value = converter.ConvertFromString(strValue.Value.ToString())
                p.SetValue(obj, value, null))
        return obj
    }

let bindQueryString<'T> (ctx : HttpHandlerContext) =
    async {
        let query = ctx.HttpContext.Request.Query
        let obj   = Activator.CreateInstance<'T>()
        let props = obj.GetType().GetProperties(BindingFlags.Instance ||| BindingFlags.Public)
        props
        |> Seq.iter (fun p -> 
            let strValue = ref (StringValues())
            if query.TryGetValue(p.Name, strValue)
            then
                let converter = TypeDescriptor.GetConverter p.PropertyType
                let value = converter.ConvertFromString(strValue.Value.ToString())
                p.SetValue(obj, value, null))
        return obj
    }

let bindModel<'T> (ctx : HttpHandlerContext) =
    async {
        let method = ctx.HttpContext.Request.Method        
        return!
            if method.Equals "POST" || method.Equals "PUT" then
                let original = ctx.HttpContext.Request.ContentType
                let parsed   = ref (MediaTypeHeaderValue("*/*"))
                match MediaTypeHeaderValue.TryParse(original, parsed) with
                | false -> failwithf "Could not parse Content-Type HTTP header value '%s'" original
                | true  ->
                    match parsed.Value.MediaType with
                    | "application/json"                  -> bindJson<'T> ctx
                    | "application/xml"                   -> bindXml<'T>  ctx
                    | "application/x-www-form-urlencoded" -> bindForm<'T> ctx
                    | _ -> failwithf "Cannot bind model from Content-Type '%s'" original
            else bindQueryString<'T> ctx
    }