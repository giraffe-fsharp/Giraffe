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
    /// ** Description **
    /// Reads the entire body of the `HttpRequest` asynchronously and returns it as a `string` value.
    /// ** Output **
    /// Returns the contents of the request body as a `Task<string>`.
    member this.ReadBodyFromRequestAsync() =
        task {
            use reader = new StreamReader(this.Request.Body, Encoding.UTF8)
            return! reader.ReadToEndAsync()
        }

    /// ** Description **
    /// Uses the `IJsonSerializer` to deserializes the entire body of the `HttpRequest` asynchronously into an object of type `'T`.
    /// ** Output **
    /// Returns a `Task<'T>`.
    member this.BindJsonAsync<'T>() =
        task {
            let serializer = this.GetJsonSerializer()
            return! serializer.DeserializeAsync<'T> this.Request.Body
        }

    /// ** Description **
    /// Uses the `IXmlSerializer` to deserializes the entire body of the `HttpRequest` asynchronously into an object of type `'T`.
    /// ** Output **
    /// Returns a `Task<'T>`.
    member this.BindXmlAsync<'T>() =
        task {
            let serializer = this.GetXmlSerializer()
            let! body = this.ReadBodyFromRequestAsync()
            return serializer.Deserialize<'T> body
        }

    /// ** Description **
    /// Parses all input elements of a HTML form into an object of type `'T`.
    /// ** Parameters **
    ///     - `cultureInfo`: Optional culture information when parsing culture specific data such as `DateTime` objects for example.
    /// ** Output **
    /// Returns a `Task<'T>`.
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

    /// ** Description **
    /// Parses all parameters of a request's query string into an object of type `'T`.
    /// ** Parameters **
    ///     - `cultureInfo`: Optional culture information when parsing culture specific data such as `DateTime` objects for example.
    /// ** Output **
    /// Returns a `Task<'T>`.
    member this.BindQueryString<'T> (?cultureInfo : CultureInfo) =
        let obj     = Activator.CreateInstance<'T>()
        let culture = defaultArg cultureInfo CultureInfo.InvariantCulture
        let props   = obj.GetType().GetProperties(BindingFlags.Instance ||| BindingFlags.Public)
        props
        |> Seq.iter (fun p ->
            match this.TryGetQueryStringValue p.Name with
            | None            -> ()
            | Some queryValue ->

                let isOptionType, isNullableType =
                    if p.PropertyType.GetTypeInfo().IsGenericType
                    then
                        let typeDef = p.PropertyType.GetGenericTypeDefinition()
                        (typeDef = typedefof<Option<_>>,
                         typeDef = typedefof<Nullable<_>>)
                    else (false, false)

                let propertyType =
                    if isOptionType || isNullableType then
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

    /// ** Description **
    /// Parses the request body into an object of type `'T` based on the request's `Content-Type` header.
    /// ** Parameters **
    ///     - `cultureInfo`: Optional culture information when parsing culture specific data such as `DateTime` objects for example.
    /// ** Output **
    /// Returns a `Task<'T>`.
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