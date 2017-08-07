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
    use stream = new FileStream(filePath, FileMode.Open)
    use reader = new StreamReader(stream)
    reader.ReadToEndAsync()

/// ---------------------------
/// Serializers
/// ---------------------------

let inline serializeJson x = JsonConvert.SerializeObject x

let inline deserializeJson<'T> str = JsonConvert.DeserializeObject<'T> str

let serializeXml x =
    use stream = new MemoryStream()
    let settings = XmlWriterSettings(Encoding = Encoding.UTF8, Indent = true, OmitXmlDeclaration = false)
    use writer = XmlWriter.Create(stream, settings)
    let serializer = XmlSerializer(x.GetType())
    serializer.Serialize(writer, x)
    stream.ToArray()

let deserializeXml<'T> str =
    let serializer = XmlSerializer(typeof<'T>)
    use reader = new StringReader(str)
    serializer.Deserialize reader :?> 'T