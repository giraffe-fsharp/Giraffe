namespace Giraffe.Serialization

open System.Reflection
open Microsoft.FSharp.Reflection
open Microsoft.IO

// ---------------------------
// JSON
// ---------------------------

[<AutoOpen>]
module Json =
    open System
    open System.IO
    open System.Text
    open System.Threading.Tasks
    open Newtonsoft.Json
    open Newtonsoft.Json.Serialization
    open FSharp.Control.Tasks.V2.ContextInsensitive
    open Utf8Json

    let recyclableMemoryStreamManager = RecyclableMemoryStreamManager()

    /// <summary>
    ///  Interface defining JSON serialization methods. Use this interface to customize JSON serialization in Giraffe.
    /// </summary>
    [<AllowNullLiteral>]
    type IJsonSerializer =
        abstract member SerializeToString<'T>      : 'T -> string
        abstract member SerializeToBytes<'T>       : 'T -> byte array
        abstract member SerializeToStreamAsync<'T> : 'T -> Stream -> Task

        abstract member Deserialize<'T>      : string -> 'T
        abstract member Deserialize<'T>      : byte[] -> 'T
        abstract member DeserializeAsync<'T> : Stream -> Task<'T>
        abstract member TryDeserializeAsync<'T> : Stream -> Task<Result<'T, string>>

    /// <summary>
    /// <see cref="Utf8JsonSerializer" /> is an alternative serializer with 
    /// great performance and supports true chunked transfer encoding.
    ///
    /// It uses Utf8Json as the underlying JSON serializer to (de-)serialize
    /// JSON content. Utf8Json is currently
    /// the fastest JSON serializer for .NET.
    /// </summary>
    /// <remarks>https://github.com/neuecc/Utf8Json</remarks>
    type Utf8JsonSerializer (resolver : IJsonFormatterResolver) =

        static member DefaultResolver = Utf8Json.Resolvers.StandardResolver.CamelCase

        interface IJsonSerializer with
            member __.SerializeToString (x : 'T) =
                JsonSerializer.ToJsonString (x, resolver)

            member __.SerializeToBytes (x : 'T) =
                JsonSerializer.Serialize (x, resolver)

            member __.SerializeToStreamAsync (x : 'T) (stream : Stream) =
                JsonSerializer.SerializeAsync(stream, x, resolver)

            member __.Deserialize<'T> (json : string) : 'T =
                let bytes = Encoding.UTF8.GetBytes json
                JsonSerializer.Deserialize(bytes, resolver)

            member __.Deserialize<'T> (bytes : byte array) : 'T =
                JsonSerializer.Deserialize(bytes, resolver)

            member __.DeserializeAsync<'T> (stream : Stream) : Task<'T> =
                JsonSerializer.DeserializeAsync(stream, resolver)
                
            member __.TryDeserializeAsync<'T> (stream : Stream) : Task<Result<'T, string>> =
                raise (NotImplementedException("Not implemented"))
    
    type private RequireObjectPropertiesContractResolver() =
        inherit CamelCasePropertyNamesContractResolver()

        override x.CreateObjectContract(objectType : Type) = 
            let contract = base.CreateObjectContract(objectType)
            contract.ItemRequired <- System.Nullable<Required>(Required.Always);
            contract

        override x.CreateProperty(memberInfo : MemberInfo, memberSerialization : MemberSerialization) =
            let jsonProperty = base.CreateProperty(memberInfo, memberSerialization);
            // https://stackoverflow.com/questions/20696262/reflection-to-find-out-if-property-is-of-option-type
            let isOption =
                jsonProperty.PropertyType.IsGenericType &&
                jsonProperty.PropertyType.GetGenericTypeDefinition() = typedefof<Option<_>>
            if isOption then (
                jsonProperty.Required <- Required.Default        
                jsonProperty.NullValueHandling <- System.Nullable<NullValueHandling>(NullValueHandling.Ignore)
            )
            jsonProperty
    type private OptionConverter() =
        inherit JsonConverter()
        
        override x.CanConvert (t) =
            t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>>

        override x.WriteJson(writer, value, serializer) =
            let value =
                if value = null then
                    null
                else
                    let _, fields = FSharpValue.GetUnionFields(value, value.GetType())
                    fields |> Array.exactlyOne
            serializer.Serialize(writer, value)

        override x.ReadJson(reader, t, existingValue, serializer) =
            let innerType = t.GetGenericArguments() |> Array.exactlyOne
            let innerType =
                if innerType.IsValueType then
                    (typedefof<Nullable<_>>).MakeGenericType([|innerType|])
                else
                    innerType
            let value = serializer.Deserialize(reader, innerType)
            let cases = FSharpType.GetUnionCases(t)
            if value = null then
                FSharpValue.MakeUnion(cases.[0], [||])
            else
                FSharpValue.MakeUnion(cases.[1], [|value|])
    
    let private copyProperties<'T when 'T : (new : unit -> 'T)> (source : 'T) : 'T =
        let destination = new 'T()
            
        typeof<'T>.GetProperties()
        |> Seq.filter(fun x -> x.CanRead && x.CanWrite)
        |> Seq.iter(fun x -> x.SetValue(destination, x.GetValue(source)))
            
        destination

    /// <summary>
    ///
    /// Default JSON serializer in Giraffe.
    ///
    /// Serializes objects to camel cased JSON code.
    /// </summary>
    type NewtonsoftJsonSerializer (settings : JsonSerializerSettings) =
        let serializer = JsonSerializer.Create settings
        let optionConvertingSerializer =
            let settings = copyProperties settings
            settings.Converters.Add(OptionConverter())
            settings.ContractResolver <- RequireObjectPropertiesContractResolver()
            JsonSerializer.Create settings
        let Utf8EncodingWithoutBom = new UTF8Encoding(false)

        static member DefaultSettings =
            JsonSerializerSettings(
                ContractResolver = CamelCasePropertyNamesContractResolver())

        interface IJsonSerializer with
            member __.SerializeToString (x : 'T) =
                JsonConvert.SerializeObject(x, settings)

            member __.SerializeToBytes (x : 'T) =
                JsonConvert.SerializeObject(x, settings)
                |> Encoding.UTF8.GetBytes

            member __.SerializeToStreamAsync (x : 'T) (stream : Stream) =
                task {
                    use memoryStream = recyclableMemoryStreamManager.GetStream()
                    use streamWriter = new StreamWriter(memoryStream, Utf8EncodingWithoutBom)
                    use jsonTextWriter = new JsonTextWriter(streamWriter)
                    serializer.Serialize(jsonTextWriter, x)
                    jsonTextWriter.Flush()
                    memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
                    do! memoryStream.CopyToAsync(stream, 65536)
                } :> Task

            member __.Deserialize<'T> (json : string) =
                JsonConvert.DeserializeObject<'T>(json, settings)

            member __.Deserialize<'T> (bytes : byte array) =
                let json = Encoding.UTF8.GetString bytes
                JsonConvert.DeserializeObject<'T>(json, settings)

            member __.DeserializeAsync<'T> (stream : Stream) =
                task {
                    use memoryStream = new MemoryStream()
                    do! stream.CopyToAsync(memoryStream)
                    memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
                    use streamReader = new StreamReader(memoryStream)
                    use jsonTextReader = new JsonTextReader(streamReader)
                    return serializer.Deserialize<'T>(jsonTextReader)
                }
                
            member __.TryDeserializeAsync<'T> (stream : Stream) : Task<Result<'T, string>> =
                task {
                    use memoryStream = new MemoryStream()
                    do! stream.CopyToAsync(memoryStream)
                    memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
                    use streamReader = new StreamReader(memoryStream)
                    use jsonTextReader = new JsonTextReader(streamReader)
                    let thing =
                        try
                            Ok(optionConvertingSerializer.Deserialize<'T>(jsonTextReader))
                        with
                        | :? JsonSerializationException as ex ->
                            Error(ex.Message)
                    return thing
                }

    open System.Text.Json

    /// <summary>
    ///
    /// <see cref="SystemTextJsonSerializer" /> is an alternaive <see cref="IJsonSerializer"/> in Giraffe.
    ///
    /// It uses <see cref="System.Text.Json"/> as the underlying JSON serializer to (de-)serialize
    /// JSON content. For support of F# unions and records, look at https://github.com/Tarmil/FSharp.SystemTextJson
    /// which plugs into this serializer.
    /// </summary>
    type SystemTextJsonSerializer (options: JsonSerializerOptions) =

        static member DefaultOptions =
           JsonSerializerOptions(
               PropertyNamingPolicy = JsonNamingPolicy.CamelCase
           )

        interface IJsonSerializer with
            member __.SerializeToString (x : 'T) =
                JsonSerializer.Serialize(x,  options)

            member __.SerializeToBytes (x : 'T) =
                JsonSerializer.SerializeToUtf8Bytes(x, options)

            member __.SerializeToStreamAsync (x : 'T) (stream : Stream) =
                JsonSerializer.SerializeAsync(stream, x, options)

            member __.Deserialize<'T> (json : string) : 'T =
                JsonSerializer.Deserialize<'T>(json, options)

            member __.Deserialize<'T> (bytes : byte array) : 'T =
                JsonSerializer.Deserialize<'T>(Span<_>.op_Implicit(bytes.AsSpan()), options)

            member __.DeserializeAsync<'T> (stream : Stream) : Task<'T> =
                JsonSerializer.DeserializeAsync<'T>(stream, options).AsTask()
                
            member __.TryDeserializeAsync<'T> (stream : Stream) : Task<Result<'T, string>> =
                raise (NotImplementedException("Not implemented"))

// ---------------------------
// XML
// ---------------------------

[<AutoOpen>]
module Xml =
    open System.Text
    open System.IO
    open System.Xml
    open System.Xml.Serialization

    /// <summary>
    /// Interface defining XML serialization methods. Use this interface to customize XML serialization in Giraffe.
    /// </summary>
    [<AllowNullLiteral>]
    type IXmlSerializer =
        abstract member Serialize       : obj    -> byte array
        abstract member Deserialize<'T> : string -> 'T

    /// <summary>
    /// Default XML serializer in Giraffe.
    ///
    /// Serializes objects to UTF8 encoded indented XML code.
    /// </summary>
    type DefaultXmlSerializer (settings : XmlWriterSettings) =
        static member DefaultSettings =
            XmlWriterSettings(
                Encoding           = Encoding.UTF8,
                Indent             = true,
                OmitXmlDeclaration = false
            )

        interface IXmlSerializer with
            member __.Serialize (o : obj) =
                use stream = new MemoryStream()
                use writer = XmlWriter.Create(stream, settings)
                let serializer = XmlSerializer(o.GetType())
                serializer.Serialize(writer, o)
                stream.ToArray()

            member __.Deserialize<'T> (xml : string) =
                let serializer = XmlSerializer(typeof<'T>)
                use reader = new StringReader(xml)
                serializer.Deserialize reader :?> 'T