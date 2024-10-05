open System
open System.IO
open System.Net.Http

let request = new HttpClient(BaseAddress = new Uri("http://localhost:5000"))

#time

seq { 1..100 }
|> Seq.map (fun _ -> request.GetAsync "/" |> Async.AwaitTask)
|> Async.Parallel
|> Async.RunSynchronously
|> Seq.iteri (fun i response ->
    printfn "\nResponse %i status code: %A" i response.StatusCode

    let responseReader = new StreamReader(response.Content.ReadAsStream())
    printfn "Response %i content: %A" i (responseReader.ReadToEnd())
)

#time
