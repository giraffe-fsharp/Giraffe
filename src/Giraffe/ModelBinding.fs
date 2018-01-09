[<AutoOpen>]
module Giraffe.ModelBinding
open System
open System.IO
open System.Text
open System.Globalization
open System.Reflection
open System.ComponentModel
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers
open Microsoft.FSharp.Reflection

type HttpContext with
    member this.ReadBodyFromRequestAsync() =
        task {
            use reader = new StreamReader(this.Request.Body, Encoding.UTF8)
            return! reader.ReadToEndAsync()
        }

    member this.BindJsonAsync<'T>() =
        task {
            let serializer = this.GetJsonSerializer()
            return! serializer.DeserializeAsync<'T> this.Request.Body
        }

    member this.BindXmlAsync<'T>() =
        task {
            let serializer = this.GetXmlSerializer()
            let! body = this.ReadBodyFromRequestAsync()
            return serializer.Deserialize<'T> body
        }

    member this.BindFormAsync<'T> (?cultureInfo : CultureInfo) =
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

    member this.BindQueryString<'T> (?cultureInfo : CultureInfo) =
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

    member this.BindModelAsync<'T> (?cultureInfo : CultureInfo) =
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
                        | "application/json"                  -> this.BindJsonAsync<'T>()
                        | "application/xml"                   -> this.BindXmlAsync<'T>()
                        | "application/x-www-form-urlencoded" -> this.BindFormAsync<'T>(?cultureInfo = cultureInfo)
                        | _ -> failwithf "Cannot bind model from Content-Type '%s'" original.Value
            else return this.BindQueryString<'T>(?cultureInfo = cultureInfo)
        }