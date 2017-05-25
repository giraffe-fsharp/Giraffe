module Giraffe.Common

open System
open System.IO
open System.Xml.Serialization
open Newtonsoft.Json
open Giraffe.AsyncTask


/// ---------------------------
/// Helper functions
/// ---------------------------

let inline isNotNull x = isNull x |> not

let inline strOption (str : string) =
    if String.IsNullOrEmpty str then None else Some str

let readFileAsString (filePath : string) =
    task {
        use stream = new FileStream(filePath, FileMode.Open)
        use reader = new StreamReader(stream)
        return!
            reader.ReadToEndAsync()
    }

/// ---------------------------
/// Serializers
/// ---------------------------

let inline serializeJson x = JsonConvert.SerializeObject x

let inline deserializeJson<'T> str = JsonConvert.DeserializeObject<'T> str

let serializeXml x =
    let serializer = XmlSerializer(x.GetType())
    use stream = new MemoryStream()
    serializer.Serialize(stream, x)
    stream.ToArray()

let deserializeXml<'T> str =
    let serializer = XmlSerializer(typeof<'T>)
    use reader = new StringReader(str)
    serializer.Deserialize reader :?> 'T