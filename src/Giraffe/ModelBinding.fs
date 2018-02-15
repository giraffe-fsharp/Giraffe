[<AutoOpen>]
module Giraffe.ModelBinding

open System
open System.IO
open System.Text
open System.Globalization
open System.Reflection
open System.Collections.Generic
open System.ComponentModel
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers
open Microsoft.FSharp.Reflection

// ---------------------------
// Model parsing functions
// ---------------------------

/// ** Description **
/// Module for parsing models from a generic data set.
module ModelParser =

    type private FSharpOption<'T> = Microsoft.FSharp.Core.Option<'T>
    type private FSharpList<'T>   = Microsoft.FSharp.Collections.List<'T>

    let private getValueForMissingProperty (t : Type) =
        let isGeneric = t.GetTypeInfo().IsGenericType
        if not isGeneric
        then None
        else
            let genericTypeDef = t.GetGenericTypeDefinition()
            let isOption = if isGeneric then genericTypeDef = typedefof<FSharpOption<_>> else false
            if not isOption
            then None
            else
                let cases = FSharpType.GetUnionCases t
                FSharpValue.MakeUnion(cases.[0], [||])
                |> Some

    let rec private parseValue (t : Type) (rawValues : StringValues) (culture : CultureInfo) =
        // First load up some more type information,
        // whether the type is generic, a list or an option type.
        let isGeneric = t.GetTypeInfo().IsGenericType
        let isList, isOption, genArgType =
            if not isGeneric then false, false, null
            else
                let genericTypeDef = t.GetGenericTypeDefinition()
                genericTypeDef = typedefof<FSharpList<_>>,
                genericTypeDef = typedefof<FSharpOption<_>>,
                t.GetGenericArguments().[0]
        if isList then
            let cases = FSharpType.GetUnionCases t
            let emptyList = FSharpValue.MakeUnion(cases.[0], [||])
            if rawValues.Count = 0
            then Some emptyList
            else
                let consCase = cases.[1]
                Array.foldBack(
                    fun (str : string) (items : obj) ->
                        let item = StringValues str
                        match parseValue genArgType item culture with
                        | None   -> items
                        | Some v -> FSharpValue.MakeUnion(consCase, [| v; items |]))
                    (rawValues.ToArray())
                    emptyList
                |> Some
        else if isGeneric then
            let value = parseValue genArgType rawValues culture
            match value with
            | None -> None
            | Some value ->
                if not isOption then Some value
                else
                    let cases = FSharpType.GetUnionCases t
                    if isNull value
                    then FSharpValue.MakeUnion(cases.[0], [||])
                    else FSharpValue.MakeUnion(cases.[1], [| value |])
                    |> Some
        else if FSharpType.IsUnion t then
            let unionName = rawValues.ToString()
            let cases = FSharpType.GetUnionCases t
            if String.IsNullOrWhiteSpace unionName
            then None
            else
                cases
                |> Array.tryFind (fun c -> c.Name.Equals(unionName, StringComparison.OrdinalIgnoreCase))
                |> function
                   | None   -> None
                   | Some c -> Some (FSharpValue.MakeUnion(c, [||]))
        else
            let converter =
                if t.GetTypeInfo().IsValueType
                then (typedefof<Nullable<_>>).MakeGenericType([| t |])
                else t
                |> TypeDescriptor.GetConverter
            try
                converter.ConvertFromString(null, culture, rawValues.ToString())
                |> Some
            with
                | :? FormatException -> None

    let private parseModel<'T> (cultureInfo : CultureInfo option)
                               (data        : IDictionary<string, StringValues>)
                               (strict      : bool) =
        // Normalize data
        let normalizeKey (key : string) =
            key.ToLowerInvariant().TrimEnd([| '['; ']' |])
        let data =
            data
            |> Seq.map (fun i -> normalizeKey i.Key, i.Value)
            |> dict

        // Create culture and model objects
        let culture = defaultArg cultureInfo CultureInfo.InvariantCulture
        let model   = Activator.CreateInstance<'T>()

        let success =
            // Iterate through all properties of the model
            model.GetType().GetProperties(BindingFlags.Instance ||| BindingFlags.Public)
            |> Seq.fold(
                fun success prop ->
                    // If model binding is set to strict and a previous property
                    // failed to parse then short circuit and return false.
                    if strict && not success then false
                    else
                        let value =
                            // Check the dictionary for an entry which matches the property name.
                            // If the dictionary has no entry which matches the property name,
                            // then try to generate a value for the property without any data (will only work for an option type).
                            // If there was an entry then try to get the value by parsing the rawValue.
                            match data.TryGetValue (prop.Name.ToLowerInvariant()) with
                            | false, _        -> getValueForMissingProperty prop.PropertyType
                            | true , rawValue -> parseValue prop.PropertyType rawValue culture

                        // Check if a value was able to get successfully parsed.
                        // If it couldn't be parsed, then return false to denote that the model
                        // could not be successfully created when in strict mode, otherwise skip
                        // setting a value, but return true in order to proceed to the next property.
                        // If a value was successfully parsed, then set the property of the model
                        // and return true.
                        match strict, value with
                        | true , None   -> false
                        | false, None   -> true
                        | _    , Some v ->
                            prop.SetValue(model, v, null)
                            true
            ) true
        // Only return the model if all properties were successfully
        // parsed and set on the model, or when model binding is not strict.
        // (strict means to return a model even when only partially parsed)
        if not strict || success then Some model else None

    /// ** Description **
    /// Tries to create an instance of type `'T` from a given set of `data`.
    /// It will try to match each property of `'T` with a key from the `data` dictionary and parse the associated value to the value of `'T`'s property.
    /// ** Parameters **
    ///     - `culture`: An optional `CultureInfo` element to be used when parsing culture specific data such as `float`, `DateTime` or `decimal` values.
    ///     - `data`: A key-value dictionary of values for each property of type `'T`. Only optional properties can be omitted from the dictionary.
    /// ** Output **
    /// If all properties were able to successfully parse then `Some 'T` will be returned, otherwise `None`.
    let tryParse<'T> (culture : CultureInfo option)
                     (data    : IDictionary<string, StringValues>) =
        parseModel<'T> culture data true

    /// ** Description **
    /// Create an instance of type `'T` from a given set of `data`.
    /// It will try to match each property of `'T` with a key from the `data` dictionary and parse the associated value to the value of `'T`'s property. If a property is missing from the `data` set or cannot be parsed then it will be omitted and a default value will be set (either `null` for reference types or a default value for value types).
    /// ** Parameters **
    ///     - `culture`: An optional `CultureInfo` element to be used when parsing culture specific data such as `float`, `DateTime` or `decimal` values.
    ///     - `data`: A key-value dictionary of values for each property of type `'T`.
    /// ** Output **
    /// An instance of type `'T`. Not all properties might be set. Null checks are required for reference types.
    let parse<'T> (culture : CultureInfo option)
                  (data    : IDictionary<string, StringValues>) =
        (parseModel<'T> culture data false).Value

// ---------------------------
// HttpContext extensions
// ---------------------------

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
    /// Parses all input elements from an HTML form into an object of type `'T`.
    /// ** Parameters **
    ///     - `cultureInfo`: An optional `CultureInfo` element to be used when parsing culture specific data such as `float`, `DateTime` or `decimal` values.
    /// ** Output **
    /// Returns a `Task<'T>`.
    member this.BindFormAsync<'T> (?cultureInfo : CultureInfo) =
        task {
            let! form = this.Request.ReadFormAsync()
            return
                form
                |> Seq.map (fun i -> i.Key, i.Value)
                |> dict
                |> ModelParser.parse<'T> cultureInfo
        }

    /// ** Description **
    /// Tries to parse all input elements from an HTML form into an object of type `'T`.
    /// ** Parameters **
    ///     - `cultureInfo`: An optional `CultureInfo` element to be used when parsing culture specific data such as `float`, `DateTime` or `decimal` values.
    /// ** Output **
    /// Returns a `Task<'T option>`.
    member this.TryBindFormAsync<'T> (?cultureInfo : CultureInfo) =
        task {
            let! form = this.Request.ReadFormAsync()
            return
                form
                |> Seq.map (fun i -> i.Key, i.Value)
                |> dict
                |> ModelParser.tryParse<'T> cultureInfo
        }

    /// ** Description **
    /// Parses all parameters of a request's query string into an object of type `'T`.
    /// ** Parameters **
    ///     - `cultureInfo`: An optional `CultureInfo` element to be used when parsing culture specific data such as `float`, `DateTime` or `decimal` values.
    /// ** Output **
    /// Returns an instance of type `'T`.
    member this.BindQueryString<'T> (?cultureInfo : CultureInfo) =
        this.Request.Query
        |> Seq.map (fun i -> i.Key, i.Value)
        |> dict
        |> ModelParser.parse<'T> cultureInfo

    /// ** Description **
    /// Tries to parse all parameters of a request's query string into an object of type `'T`.
    /// ** Parameters **
    ///     - `cultureInfo`: An optional `CultureInfo` element to be used when parsing culture specific data such as `float`, `DateTime` or `decimal` values.
    /// ** Output **
    /// Returns an `Option<'T>`.
    member this.TryBindQueryString<'T> (?cultureInfo : CultureInfo) =
        this.Request.Query
        |> Seq.map (fun i -> i.Key, i.Value)
        |> dict
        |> ModelParser.tryParse<'T> cultureInfo

    /// ** Description **
    /// Parses the request body into an object of type `'T` based on the request's `Content-Type` header.
    /// ** Parameters **
    ///     - `cultureInfo`: An optional `CultureInfo` element to be used when parsing culture specific data such as `float`, `DateTime` or `decimal` values.
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

// ---------------------------
// HttpHandler functions
// ---------------------------

/// ** Description **
/// Parses a JSON payload into an instance of type `'T`.
/// ** Parameters **
///     - `f`: A function which accepts an object of type `'T` and returns a `HttpHandler` function.
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let bindJson<'T> (f : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! model = ctx.BindJsonAsync<'T>()
            return! f model next ctx
        }

/// ** Description **
/// Parses an XML payload into an instance of type `'T`.
/// ** Parameters **
///     - `f`: A function which accepts an object of type `'T` and returns a `HttpHandler` function.
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let bindXml<'T> (f : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! model = ctx.BindXmlAsync<'T>()
            return! f model next ctx
        }

/// ** Description **
/// Parses a HTTP form payload into an instance of type `'T`.
/// ** Parameters **
///     - `f`: A function which accepts an object of type `'T` and returns a `HttpHandler` function.
///     - `culture`: An optional `CultureInfo` element to be used when parsing culture specific data such as `float`, `DateTime` or `decimal` values.
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let bindForm<'T> (culture : CultureInfo option) (f : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! model =
                match culture with
                | Some c -> ctx.BindFormAsync<'T> c
                | None   -> ctx.BindFormAsync<'T>()
            return! f model next ctx
        }

/// ** Description **
/// Tries to parse a HTTP form payload into an instance of type `'T`.
/// The payload must contain all non-optional properties of type `'T` (with correct data) in order to successfully parse the form data. If some data is missing or wrong then the `parsingErrorHandler` will be executed.
/// ** Parameters **
///     - `parsingErrorHandler`: A `HttpHandler` function will get invoked when the model parsing fails.
///     - `culture`: An optional `CultureInfo` element to be used when parsing culture specific data such as `float`, `DateTime` or `decimal` values.
///     - `successhandler`: A function which accepts an object of type `'T` and returns a `HttpHandler` function.
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let tryBindForm<'T> (parsingErrorHandler : HttpHandler)
                    (culture             : CultureInfo option)
                    (successhandler      : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! result =
                match culture with
                | Some c -> ctx.TryBindFormAsync<'T> c
                | None   -> ctx.TryBindFormAsync<'T>()
            return!
                (match result with
                | None       -> parsingErrorHandler
                | Some model -> successhandler model) next ctx
        }

/// ** Description **
/// Parses a HTTP query string into an instance of type `'T`.
/// ** Parameters **
///     - `f`: A function which accepts an object of type `'T` and returns a `HttpHandler` function.
///     - `culture`: An optional `CultureInfo` element to be used when parsing culture specific data such as `float`, `DateTime` or `decimal` values.
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let bindQuery<'T> (culture : CultureInfo option) (f : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let model =
            match culture with
            | Some c -> ctx.BindQueryString<'T> c
            | None   -> ctx.BindQueryString<'T>()
        f model next ctx

/// ** Description **
/// Tries to parse a query string into an instance of type `'T`.
/// The query string must contain all non-optional properties of type `'T` (with correct data) in order to successfully parse the query string. If some data is missing or wrong then the `parsingErrorHandler` function will be executed.
/// ** Parameters **
///     - `parsingErrorHandler`: A `HttpHandler` function will get invoked when the model parsing fails.
///     - `culture`: An optional `CultureInfo` element to be used when parsing culture specific data such as `float`, `DateTime` or `decimal` values.
///     - `successhandler`: A function which accepts an object of type `'T` and returns a `HttpHandler` function.
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let tryBindQuery<'T> (parsingErrorHandler : HttpHandler)
                     (culture             : CultureInfo option)
                     (successhandler      : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let result =
            match culture with
            | Some c -> ctx.TryBindQueryString<'T> c
            | None   -> ctx.TryBindQueryString<'T>()
        (match result with
        | None       -> parsingErrorHandler
        | Some model -> successhandler model) next ctx

/// ** Description **
/// Parses a HTTP payload into an instance of type `'T`.
/// The model can be sent via XML, JSON, form or query string.
/// ** Parameters **
///     - `f`: A function which accepts an object of type `'T` and returns a `HttpHandler` function.
///     - `culture`: An optional `CultureInfo` element to be used when parsing culture specific data such as `float`, `DateTime` or `decimal` values.
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let bindModel<'T> (culture : CultureInfo option) (f : 'T -> HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! model =
                match culture with
                | Some c -> ctx.BindModelAsync<'T> c
                | None   -> ctx.BindModelAsync<'T>()
            return! f model next ctx
        }