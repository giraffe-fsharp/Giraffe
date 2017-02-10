module AspNetCore.Lambda.Common

open System
open System.IO
open System.Xml.Serialization

let inline isNotNull x = isNull x |> not

let readFileAsString (filePath : string) =
    async {
        use stream = new FileStream(filePath, FileMode.Open)
        use reader = new StreamReader(stream)
        return!
            reader.ReadToEndAsync()
            |> Async.AwaitTask
    }

let serializeXml x =
    let serializer = new XmlSerializer(x.GetType())
    use stream = new MemoryStream()
    serializer.Serialize(stream, x)
    stream.ToArray()

let combinePaths (path1 : string) (path2 : string) =
    Path.GetFullPath(Path.Combine(path1, path2))