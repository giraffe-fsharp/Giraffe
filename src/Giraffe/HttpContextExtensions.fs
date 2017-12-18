[<AutoOpen>]
module Giraffe.HttpContextExtensions

open System
open System.Globalization
open System.IO
open System.Reflection
open System.ComponentModel
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Reflection
open Microsoft.Net.Http.Headers
open Newtonsoft.Json
open Common
open XmlViewEngine

type HttpContext with

    /// ---------------------------
    /// Dependency management
    /// ---------------------------

    member this.GetService<'T>() =
        this.RequestServices.GetService(typeof<'T>) :?> 'T

    member this.GetLogger<'T>() =
        this.GetService<ILogger<'T>>()

    member this.GetLogger (categoryName : string) =
        let loggerFactory = this.GetService<ILoggerFactory>()
        loggerFactory.CreateLogger categoryName

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

    member this.ReadBodyFromRequestAsync() =
        let body = this.Request.Body
        use reader = new StreamReader(body, Text.Encoding.UTF8, true)
        reader.ReadToEndAsync()

    member this.BindJsonAsync<'T>() = this.BindJsonAsync<'T> defaultJsonSerializerSettings

    member this.BindJsonAsync<'T> (settings : JsonSerializerSettings) =
        task {
            return deserializeJsonFromStream<'T> settings this.Request.Body
        }

    member this.BindXmlAsync<'T>() =
        task {
            let! body = this.ReadBodyFromRequestAsync()
            return deserializeXml<'T> body
        }

    member this.BindFormAsync<'T>(?cultureInfo : CultureInfo) =
        task {
            let! form   = this.Request.ReadFormAsync()
            let culture = defaultArg cultureInfo CultureInfo.InvariantCulture
            let obj     = Activator.CreateInstance<'T>()
            let props   = obj.GetType().GetProperties(BindingFlags.Instance ||| BindingFlags.Public)
            props
            |> Seq.iter (fun p ->
                let strValue = ref (StringValues())
                if form.TryGetValue(p.Name, strValue)
                then
                    let converter = TypeDescriptor.GetConverter p.PropertyType
                    let value = converter.ConvertFromString(null, culture, strValue.Value.ToString())
                    p.SetValue(obj, value, null))
            return obj
        }

    member this.BindQueryString<'T>(?cultureInfo : CultureInfo) =
        let obj     = Activator.CreateInstance<'T>()
        let culture = defaultArg cultureInfo CultureInfo.InvariantCulture
        let props   = obj.GetType().GetProperties(BindingFlags.Instance ||| BindingFlags.Public)
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

                let value = converter.ConvertFromString(null, culture, queryValue)

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

    member this.BindModelAsync<'T>(?cultureInfo) = this.BindModelAsync<'T>(defaultJsonSerializerSettings, ?cultureInfo = cultureInfo)

    member this.BindModelAsync<'T> (settings : JsonSerializerSettings, ?cultureInfo : CultureInfo) =
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
                        | "application/json"                  -> this.BindJsonAsync<'T> settings
                        | "application/xml"                   -> this.BindXmlAsync<'T>()
                        | "application/x-www-form-urlencoded" -> this.BindFormAsync<'T>(?cultureInfo = cultureInfo)
                        | _ -> failwithf "Cannot bind model from Content-Type '%s'" original.Value
            else return this.BindQueryString<'T>(?cultureInfo = cultureInfo)
        }

    /// ---------------------------
    /// Response writers
    /// ---------------------------

    member private this.SetHttpHeader (key : string) (value : obj) =
        this.Response.Headers.[key] <- StringValues(value.ToString())

    member private this.WriteBytesAsync (bytes : byte[]) =
        this.SetHttpHeader "Content-Length" bytes.Length
        this.Response.Body.WriteAsync(bytes, 0, bytes.Length)

    member private this.WriteStringAsync (value : string) =
        value |> System.Text.Encoding.UTF8.GetBytes |> this.WriteBytesAsync

    member this.WriteJsonAsync (value : obj) = this.WriteJsonAsync(defaultJsonSerializerSettings, value)

    member this.WriteJsonAsync (settings: JsonSerializerSettings, value : obj) =
        task {
            this.SetHttpHeader "Content-Type" "application/json"
            do! serializeJson settings value |> this.WriteStringAsync
            return Some this
        }

    member this.WriteXmlAsync (value : obj) =
        task {
            this.SetHttpHeader "Content-Type" "application/xml"
            do! value |> serializeXml |> this.WriteBytesAsync
            return Some this
        }

    member this.WriteTextAsync (value : string) =
        task {
            this.SetHttpHeader "Content-Type" "text/plain"
            do! value |> this.WriteStringAsync
            return Some this
        }

    member this.RenderHtmlAsync (value: XmlNode) =
        task {
            this.SetHttpHeader "Content-Type" "text/html"
            do! value |> renderHtmlDocument |> this.WriteStringAsync
            return Some this
        }

    member this.ReturnHtmlFileAsync (relativeFilePath: String) =
        task {
            this.SetHttpHeader "Content-Type" "text/html"
            let env = this.GetService<IHostingEnvironment>()
            let filePath = Path.Combine(env.ContentRootPath, relativeFilePath)
            let! html = readFileAsStringAsync filePath
            do! this.WriteStringAsync html
            return Some this
        }