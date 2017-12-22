module Giraffe.Common

open System
open System.IO
open System.Text
open System.Xml
open System.Xml.Serialization
open Newtonsoft.Json
open Newtonsoft.Json.Serialization

/// ---------------------------
/// Helper functions
/// ---------------------------

let inline isNotNull x = isNull x |> not

let inline strOption (str : string) =
    if String.IsNullOrEmpty str then None else Some str

let readFileAsStringAsync (filePath : string) =
    task {
        use stream = new FileStream(filePath, FileMode.Open)
        use reader = new StreamReader(stream)
        return! reader.ReadToEndAsync()
    }

/// ---------------------------
/// Serializers
/// ---------------------------

let inline serializeJson       (settings : JsonSerializerSettings) x   = JsonConvert.SerializeObject(x, settings)
let inline deserializeJson<'T> (settings : JsonSerializerSettings) str = JsonConvert.DeserializeObject<'T>(str, settings)

let inline deserializeJsonFromStream<'T> (settings : JsonSerializerSettings) (stream : Stream) =
    use sr = new StreamReader(stream, true)
    use jr = new JsonTextReader(sr)
    let serializer = JsonSerializer.Create settings
    serializer.Deserialize<'T>(jr)

let defaultJsonSerializerSettings = JsonSerializerSettings(ContractResolver = CamelCasePropertyNamesContractResolver())

let inline defaultSerializeJson x = serializeJson defaultJsonSerializerSettings x
let inline defaultDeserializeJson<'T> str = deserializeJson<'T> defaultJsonSerializerSettings str

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