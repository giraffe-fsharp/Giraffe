namespace Giraffe.Serialization

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
    open Utf8Json

    /// **Description**
    ///
    /// Interface defining JSON serialization methods. Use this interface to customize JSON serialization in Giraffe.
    ///
    [<AllowNullLiteral>]
    type IJsonSerializer =
        abstract member SerializeToString<'T>      : 'T -> string
        abstract member SerializeToBytes<'T>       : 'T -> byte array
        abstract member SerializeToStreamAsync<'T> : 'T -> Stream -> Task

        abstract member Deserialize<'T>      : string -> 'T
        abstract member Deserialize<'T>      : byte[] -> 'T
        abstract member DeserializeAsync<'T> : Stream -> Task<'T>

    /// **Description**
    ///
    /// The `Utf8JsonSerializer` is the default `IJsonSerializer` in Giraffe.
    ///
    /// It uses `Utf8Json` as the underlying JSON serializer to (de-)serialize
    /// JSON content. [Utf8Json](https://github.com/neuecc/Utf8Json) is currently
    /// the fastest JSON serializer for .NET.
    ///
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

    /// **Description**
    ///
    /// The previous default JSON serializer in Giraffe.
    ///
    /// The `NewtonsoftJsonSerializer` has been replaced by `Utf8JsonSerializer` as
    /// the default `IJsonSerializer` which has much better performance and supports
    /// true chunked transfer encoding.
    ///
    /// The `NewtonsoftJsonSerializer` remains available as an alternative JSON
    /// serializer which can be used to override the `Utf8JsonSerializer` for
    /// backwards compatibility.
    ///
    /// Serializes objects to camel cased JSON code.
    ///
    type NewtonsoftJsonSerializer (settings : JsonSerializerSettings) =

        let Utf8EncodingWithoutBom = new UTF8Encoding(false)
        let DefaultBufferSize = 1024

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
                use sw = new StreamWriter(stream, Utf8EncodingWithoutBom, DefaultBufferSize, true)
                use jw = new JsonTextWriter(sw)
                let sr = JsonSerializer.Create settings
                sr.Serialize(jw, x)
                Task.CompletedTask

            member __.Deserialize<'T> (json : string) =
                JsonConvert.DeserializeObject<'T>(json, settings)

            member __.Deserialize<'T> (bytes : byte array) =
                let json = Encoding.UTF8.GetString bytes
                JsonConvert.DeserializeObject<'T>(json, settings)

            member __.DeserializeAsync<'T> (stream : Stream) =
                use sr = new StreamReader(stream, true)
                use jr = new JsonTextReader(sr)
                let sr = JsonSerializer.Create settings
                Task.FromResult(sr.Deserialize<'T> jr)

// ---------------------------
// XML
// ---------------------------

[<AutoOpen>]
module Xml =
    open System.Text
    open System.IO
    open System.Xml
    open System.Xml.Serialization

    /// **Description**
    ///
    /// Interface defining XML serialization methods. Use this interface to customize XML serialization in Giraffe.
    ///
    [<AllowNullLiteral>]
    type IXmlSerializer =
        abstract member Serialize       : obj    -> byte array
        abstract member Deserialize<'T> : string -> 'T

    /// **Description**
    ///
    /// Default XML serializer in Giraffe.
    ///
    /// Serializes objects to UTF8 encoded indented XML code.
    ///
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