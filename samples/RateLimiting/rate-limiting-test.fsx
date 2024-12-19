open System
open System.Net.Http

let request = new HttpClient(BaseAddress = new Uri("http://localhost:5000"))

let program () =
    async {
        let! reqResult1 =
            seq { 1..100 }
            |> Seq.map (fun _ -> request.GetAsync "/no-rate-limit" |> Async.AwaitTask)
            |> Async.Parallel

        reqResult1
#if DEBUG
        |> Seq.iteri (fun i response ->
            printfn "\nResponse %i status code: %A" i response.StatusCode

            let responseReader = new StreamReader(response.Content.ReadAsStream())
            printfn "Response %i content: %A" i (responseReader.ReadToEnd())
        )
#else
        |> Seq.groupBy (fun response -> response.StatusCode)
        |> Seq.iter (fun (group) ->
            let key, seqRes = group
            printfn "Quantity of requests with status code %A: %i" (key) (Seq.length seqRes)
        )
#endif

        printfn "\nWith rate limit now...\n"

        let! reqResult2 =
            seq { 1..100 }
            |> Seq.map (fun _ -> request.GetAsync "/rate-limit" |> Async.AwaitTask)
            |> Async.Parallel

        reqResult2
#if DEBUG
        |> Seq.iteri (fun i response ->
            printfn "\nResponse %i status code: %A" i response.StatusCode

            let responseReader = new StreamReader(response.Content.ReadAsStream())
            printfn "Response %i content: %A" i (responseReader.ReadToEnd())
        )
#else
        |> Seq.groupBy (fun response -> response.StatusCode)
        |> Seq.iter (fun (group) ->
            let key, seqRes = group
            printfn "Quantity of requests with status code %A: %i\n" (key) (Seq.length seqRes)
        )
#endif
    }

#time

program () |> Async.RunSynchronously

#time
