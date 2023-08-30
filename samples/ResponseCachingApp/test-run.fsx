#r "nuget: FsHttp, 11.0.0"

open System
open FsHttp

// Uncomment if you don't want FsHttp debug logs
// Fsi.disableDebugLogs()

type QueryParams = (string * obj) list

let URLMap =
    [ ("not_cached", "http://localhost:5000/cached/not")
      ("public_cached", "http://localhost:5000/cached/public")
      ("private_cached", "http://localhost:5000/cached/private")
      ("public_cached_no_vary_by_query_keys", "http://localhost:5000/cached/vary/not")
      ("cached_vary_by_query_keys", "http://localhost:5000/cached/vary/yes") ]
    |> Map.ofList

let waitForOneSecond () =
    let OneSecond = 1000 // ms
    Threading.Thread.Sleep OneSecond

let queryParams1: QueryParams = [ ("query1", "a"); ("query2", "b") ]
let queryParams2: QueryParams = [ ("query1", "c"); ("query2", "d") ]

let makeRequest (url: string) (queryParams: list<string * obj>) =
    let response =
        http {
            GET url
            CacheControl "max-age=3600"
            query queryParams
        }
        |> Request.send
        |> Response.toFormattedText

    printfn "%s" response
    printfn ""

let printRunTitle (title: string) =
    printfn "-----------------------------------"
    printfn "%s" title
    printfn ""

let printTimeTaken (totalSeconds: float) =
    printfn "The time it took to finish:"
    printfn "%.2f seconds" totalSeconds
    printfn ""

let runFiveRequests (title: string) (url: string) =
    printRunTitle (title)

    let stopWatch = Diagnostics.Stopwatch.StartNew()

    for _ in [ 1..5 ] do
        makeRequest url [] |> waitForOneSecond

    stopWatch.Stop()
    printTimeTaken stopWatch.Elapsed.TotalSeconds

let testPublicCachedNoVaryByQueryKeys () =
    printRunTitle "Testing the /cached/vary/not endpoint"

    let url = URLMap.Item "public_cached_no_vary_by_query_keys"

    let stopWatch = Diagnostics.Stopwatch.StartNew()

    makeRequest url queryParams1 |> waitForOneSecond

    makeRequest url queryParams1 |> waitForOneSecond

    makeRequest url queryParams2 |> waitForOneSecond

    makeRequest url queryParams2 |> waitForOneSecond

    stopWatch.Stop()
    printTimeTaken stopWatch.Elapsed.TotalSeconds

let testCachedVaryByQueryKeys () =
    printRunTitle "Testing the /cached/vary/yes endpoint"

    let url = URLMap.Item "cached_vary_by_query_keys"

    let stopWatch = Diagnostics.Stopwatch.StartNew()

    makeRequest url queryParams1 |> waitForOneSecond

    makeRequest url queryParams1 |> waitForOneSecond

    makeRequest url queryParams2 |> waitForOneSecond

    makeRequest url queryParams2 |> waitForOneSecond

    stopWatch.Stop()
    printTimeTaken stopWatch.Elapsed.TotalSeconds

let main () =
    runFiveRequests ("Testing the /cached/not endpoint") (URLMap.Item "not_cached")
    runFiveRequests ("Testing the /cached/public endpoint") (URLMap.Item "public_cached")
    runFiveRequests ("Testing the /cached/private endpoint") (URLMap.Item "private_cached")
    testPublicCachedNoVaryByQueryKeys ()
    testCachedVaryByQueryKeys ()

main ()
