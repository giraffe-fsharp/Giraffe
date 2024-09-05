#r "nuget: FsHttp, 11.0.0"

open System
open FsHttp

// Uncomment if you don't want FsHttp debug logs
// Fsi.disableDebugLogs()

type QueryParams = (string * obj) list
type Url = Url of string
type Title = Title of string

let urls =
    {|
        notCached = Url "http://localhost:5000/cached/not"
        publicCached = Url "http://localhost:5000/cached/public"
        privateCached = Url "http://localhost:5000/cached/private"
        publicCachedNoVaryByQueryKeys = Url "http://localhost:5000/cached/vary/not"
        cachedVaryByQueryKeys = Url "http://localhost:5000/cached/vary/yes"
    |}

let queryParams1: QueryParams = [ ("query1", "a"); ("query2", "b") ]
let queryParams2: QueryParams = [ ("query1", "c"); ("query2", "d") ]

let waitForOneSecond () =
    do Threading.Thread.Sleep(TimeSpan.FromSeconds 1)

let makeRequest (Url url: Url) (queryParams: QueryParams) =
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

let printRunTitle (Title title: Title) =
    printfn "-----------------------------------"
    printfn "%s" title
    printfn ""

let printTimeTaken (duration: TimeSpan) =
    printfn "The time it took to finish:"
    printfn "%.2f seconds" duration.TotalSeconds
    printfn ""

let run (qps: QueryParams list) (title: Title) (url: Url) =
    printRunTitle title

    let stopWatch = Diagnostics.Stopwatch.StartNew()

    for queryParams in qps do
        makeRequest url queryParams |> waitForOneSecond

    stopWatch.Stop()
    printTimeTaken stopWatch.Elapsed

let runFiveRequests =
    run
        [
            for _ in 1..5 do
                []
        ]

let testPublicCachedNoVaryByQueryKeys () =
    let allQueryParams = [ queryParams1; queryParams1; queryParams2; queryParams2 ]
    let title = Title "Testing the /cached/vary/not endpoint"
    let url = urls.publicCachedNoVaryByQueryKeys
    run allQueryParams title url

let testCachedVaryByQueryKeys () =
    let allQueryParams = [ queryParams1; queryParams1; queryParams2; queryParams2 ]
    let title = Title "Testing the /cached/vary/yes endpoint"
    let url = urls.cachedVaryByQueryKeys
    run allQueryParams title url

let main () =
    runFiveRequests (Title "Testing the /cached/not endpoint") urls.notCached
    runFiveRequests (Title "Testing the /cached/public endpoint") urls.publicCached
    runFiveRequests (Title "Testing the /cached/private endpoint") urls.privateCached
    testPublicCachedNoVaryByQueryKeys ()
    testCachedVaryByQueryKeys ()

main ()
