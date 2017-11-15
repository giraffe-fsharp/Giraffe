[<AutoOpenAttribute>]
module Giraffe.HttpContextExtensions

open System
open System.IO
open System.Reflection
open System.ComponentModel
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Reflection
open Microsoft.Net.Http.Headers
open Giraffe.Common
open Newtonsoft.Json

type HttpContext with

    /// ---------------------------
    /// Dependency management
    /// ---------------------------

    member this.GetService<'T>() =
        this.RequestServices.GetService(typeof<'T>) :?> 'T

    member this.GetLogger<'T>() =
        this.GetService<ILogger<'T>>()

    /// ---------------------------
    /// Common helpers
    /// ---------------------------

    member this.TryGetRequestHeader (key : string) =
        match this.Request.Headers.TryGetValue key with
        | true, value -> Some (value.ToString())
        | _           -> None

    member this.GetRequestHeader (key : string) =
        match this.Request.Headers.TryGetValue key with
        | true, value -> Ok (value.ToString())
        | _           -> Error (sprintf "HTTP request header '%s' is missing." key)

    member this.TryGetQueryStringValue (key : string) =
        match this.Request.Query.TryGetValue key with
        | true, value -> Some (value.ToString())
        | _           -> None

    member this.GetQueryStringValue (key : string) =
        match this.Request.Query.TryGetValue key with
        | true, value -> Ok (value.ToString())
        | _           -> Error (sprintf "Query string value '%s' is missing." key)

    /// ---------------------------
    /// Model binding
    /// ---------------------------

    member this.ReadBodyFromRequest() =
        let body = this.Request.Body
        use reader = new StreamReader(body, true)
        reader.ReadToEndAsync()
    member this.BindJson<'T>() = task {
            let body = this.Request.Body
            use sr = new StreamReader(body, true)
            use jr = new JsonTextReader(sr)
            let serializer = JsonSerializer()
            return serializer.Deserialize<'T>(jr)
    }
    member this.BindXml<'T>() =
        task {
            let! body = this.ReadBodyFromRequest()
            return deserializeXml<'T> body
        }

    member this.BindForm<'T>() =
        task {
            let! form = this.Request.ReadFormAsync()
            let obj   = Activator.CreateInstance<'T>()
            let props = obj.GetType().GetProperties(BindingFlags.Instance ||| BindingFlags.Public)
            props
            |> Seq.iter (fun p ->
                let strValue = ref (StringValues())
                if form.TryGetValue(p.Name, strValue)
                then
                    let converter = TypeDescriptor.GetConverter p.PropertyType
                    let value = converter.ConvertFromInvariantString(strValue.Value.ToString())
                    p.SetValue(obj, value, null))
            return obj
        }

    member this.BindQueryString<'T>() =
        let obj   = Activator.CreateInstance<'T>()
        let props = obj.GetType().GetProperties(BindingFlags.Instance ||| BindingFlags.Public)
        props
        |> Seq.iter (fun p ->
            match this.TryGetQueryStringValue p.Name with
            | None            -> ()
            | Some queryValue ->

                let isOptionType =
                    p.PropertyType.GetTypeInfo().IsGenericType &&
                    p.PropertyType.GetGenericTypeDefinition() = typedefof<Option<_>>

                let propertyType =
                    if isOptionType then
                        p.PropertyType.GetGenericArguments().[0]
                    else
                        p.PropertyType

                let propertyType =
                    if propertyType.GetTypeInfo().IsValueType then
                        (typedefof<Nullable<_>>).MakeGenericType([|propertyType|])
                    else
                        propertyType

                let converter = TypeDescriptor.GetConverter propertyType

                let value = converter.ConvertFromInvariantString(queryValue)

                if isOptionType then
                    let cases = FSharpType.GetUnionCases(p.PropertyType)
                    let value =
                        if isNull value then
                            FSharpValue.MakeUnion(cases.[0], [||])
                        else
                            FSharpValue.MakeUnion(cases.[1], [|value|])
                    p.SetValue(obj, value, null)
                else
                    p.SetValue(obj, value, null))
        obj

    member this.BindModel<'T>() =
        task {
            let method = this.Request.Method
            if method.Equals "POST" || method.Equals "PUT" then
                let original = StringSegment(this.Request.ContentType)
                let parsed   = ref (MediaTypeHeaderValue(StringSegment("*/*")))
                return!
                    match MediaTypeHeaderValue.TryParse(original, parsed) with
                    | false -> failwithf "Could not parse Content-Type HTTP header value '%s'" original.Value
                    | true  ->
                        match parsed.Value.MediaType.Value with
                        | "application/json"                  -> this.BindJson<'T>()
                        | "application/xml"                   -> this.BindXml<'T>()
                        | "application/x-www-form-urlencoded" -> this.BindForm<'T>()
                        | _ -> failwithf "Cannot bind model from Content-Type '%s'" original.Value
            else return this.BindQueryString<'T>()
        }

    /// ---------------------------
    /// Response writers
    /// ---------------------------

    member private this.SetHttpHeader (key : string) (value : obj) =
        this.Response.Headers.[key] <- StringValues(value.ToString())

    member private this.WriteBytes (bytes : byte[]) =
        this.SetHttpHeader "Content-Length" bytes.Length
        this.Response.Body.WriteAsync(bytes, 0, bytes.Length)

    member private this.WriteString (value : string) =
        value |> System.Text.Encoding.UTF8.GetBytes |> this.WriteBytes

    member this.WriteJson (value : obj) =
        task {
            this.SetHttpHeader "Content-Type" "application/json"
            do! value |> serializeJson |> this.WriteString
            return Some this
        }

    member this.WriteXml (value : obj) =
        task {
            this.SetHttpHeader "Content-Type" "application/xml"
            do! value |> serializeXml |> this.WriteBytes
            return Some this
        }

    member this.WriteText (value : string) =
        task {
            this.SetHttpHeader "Content-Type" "text/plain"
            do! value |> this.WriteString
            return Some this
        }