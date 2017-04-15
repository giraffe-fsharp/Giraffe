module Giraffe.Common

open System
open System.IO
open System.Xml.Serialization
open Newtonsoft.Json

let inline isNotNull x = isNull x |> not

let inline strOption (str : string) =
    if String.IsNullOrEmpty str then None else Some str

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

let readFileAsString (filePath : string) =
    async {
        use stream = new FileStream(filePath, FileMode.Open)
        use reader = new StreamReader(stream)
        return!
            reader.ReadToEndAsync()
            |> Async.AwaitTask
    }