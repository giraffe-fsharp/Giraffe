module Giraffe.Common

open System
open System.IO
open System.Text
open System.Xml
open System.Xml.Serialization
open Newtonsoft.Json

/// ---------------------------
/// Helper functions
/// ---------------------------

let inline isNotNull x = isNull x |> not

let inline strOption (str : string) =
    if String.IsNullOrEmpty str then None else Some str

let readFileAsString (filePath : string) =
    async {
        use stream = new FileStream(filePath, FileMode.Open)
        use reader = new StreamReader(stream)
        return!
            reader.ReadToEndAsync()
            |> Async.AwaitTask
    }

/// ---------------------------
/// Serializers
/// ---------------------------

let inline serializeJson x = JsonConvert.SerializeObject x

let inline deserializeJson<'T> str = JsonConvert.DeserializeObject<'T> str

let serializeXml x =
    use stream = new MemoryStream()
    let xmlWriterSettings = XmlWriterSettings(Encoding= Encoding.UTF8, Indent = true, OmitXmlDeclaration=false )
    use xmlWriter = XmlWriter.Create(stream, xmlWriterSettings)
    let serializer = XmlSerializer(x.GetType())
    serializer.Serialize(xmlWriter, x)
    stream.ToArray()

let deserializeXml<'T> str =
    let serializer = XmlSerializer(typeof<'T>)
    use reader = new StringReader(str)
    serializer.Deserialize reader :?> 'T