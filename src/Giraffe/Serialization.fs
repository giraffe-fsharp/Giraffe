namespace Giraffe.Serialization

// ---------------------------
// JSON
// ---------------------------

[<AutoOpen>]
module Json =
    open System.IO
    open System.Threading.Tasks
    open Newtonsoft.Json
    open Newtonsoft.Json.Serialization
    open FSharp.Control.Tasks.ContextInsensitive

    /// ** Description **
    /// Interface defining JSON serialization methods. Use this interface to customize JSON serialization in Giraffe.
    [<AllowNullLiteral>]
    type IJsonSerializer =
        abstract member Serialize            : obj    -> string
        abstract member Deserialize<'T>      : string -> 'T
        abstract member Deserialize<'T>      : Stream -> 'T
        abstract member DeserializeAsync<'T> : Stream -> Task<'T>

    /// ** Description **
    /// Default JSON serializer in Giraffe.
    /// Serializes objects to camel cased JSON code.
    type NewtonsoftJsonSerializer (settings : JsonSerializerSettings) =
        static member DefaultSettings =
            JsonSerializerSettings(
                ContractResolver = CamelCasePropertyNamesContractResolver())

        interface IJsonSerializer with
            member __.Serialize (o : obj) = JsonConvert.SerializeObject(o, settings)

            member __.Deserialize<'T> (json : string) = JsonConvert.DeserializeObject<'T>(json, settings)

            member __.Deserialize<'T> (stream : Stream) =
                use sr = new StreamReader(stream, true)
                use jr = new JsonTextReader(sr)
                let sr = JsonSerializer.Create settings
                sr.Deserialize<'T> jr

            member __.DeserializeAsync<'T> (stream : Stream) =
                task {
                    use sr = new StreamReader(stream, true)
                    use jr = new JsonTextReader(sr)
                    let sr = JsonSerializer.Create settings
                    return sr.Deserialize<'T> jr
                }

// ---------------------------
// XML
// ---------------------------

[<AutoOpen>]
module Xml =
    open System.Text
    open System.IO
    open System.Xml
    open System.Xml.Serialization

    /// ** Description **
    /// Interface defining XML serialization methods. Use this interface to customize XML serialization in Giraffe.
    [<AllowNullLiteral>]
    type IXmlSerializer =
        abstract member Serialize       : obj    -> byte array
        abstract member Deserialize<'T> : string -> 'T

    /// ** Description **
    /// Default XML serializer in Giraffe.
    /// Serializes objects to UTF8 encoded indented XML code.
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