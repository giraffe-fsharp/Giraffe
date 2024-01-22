namespace Giraffe

[<RequireQualifiedAccess>]
module Xml =
    /// <summary>
    /// Interface defining XML serialization methods.
    /// Use this interface to customize XML serialization in Giraffe.
    /// </summary>
    [<AllowNullLiteral>]
    type ISerializer =
        abstract member Serialize       : obj    -> byte array
        abstract member Deserialize<'T> : string -> 'T

[<RequireQualifiedAccess>]
module SystemXml =
    open Microsoft.IO
    open System.Text
    open System.IO
    open System.Xml
    open System.Xml.Serialization

    /// <summary>
    /// Default XML serializer in Giraffe.
    /// Serializes objects to UTF8 encoded indented XML code.
    /// </summary>
    type Serializer (settings : XmlWriterSettings, rmsManager : RecyclableMemoryStreamManager) =

        new(settings : XmlWriterSettings) = Serializer(
            settings,
            recyclableMemoryStreamManager.Value)

        static member DefaultSettings =
            XmlWriterSettings(
                Encoding           = Encoding.UTF8,
                Indent             = true,
                OmitXmlDeclaration = false
            )

        interface Xml.ISerializer with
            member __.Serialize (o : obj) =
                use stream = if rmsManager.Settings.ThrowExceptionOnToArray then new MemoryStream() else rmsManager.GetStream("giraffe-xml-serialize")
                use writer = XmlWriter.Create(stream, settings)
                let serializer = XmlSerializer(o.GetType())
                serializer.Serialize(writer, o)
                stream.ToArray()

            member __.Deserialize<'T> (xml : string) =
                let serializer = XmlSerializer(typeof<'T>)
                use reader = new StringReader(xml)
                serializer.Deserialize reader :?> 'T