module AspNetCore.Lambda.Common

open System
open System.IO

let readFileAsString (filePath : string) =
    async {
        use stream = new FileStream(filePath, FileMode.Open)
        use reader = new StreamReader(stream)
        return!
            reader.ReadToEndAsync()
            |> Async.AwaitTask
    }

let toOption value = 
    if obj.ReferenceEquals(value, null) then None else Some value