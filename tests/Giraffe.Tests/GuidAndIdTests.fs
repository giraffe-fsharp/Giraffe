module Giraffe.Tests.ShortGuidTests

open System
open Xunit
open Giraffe.Common

// ---------------------------------
// Short Guid Tests
// ---------------------------------

let rndInt64 (rand : Random) =
    let buffer = Array.zeroCreate 8
    rand.NextBytes buffer
    BitConverter.ToUInt64(buffer, 0)

[<Fact>]
let ``Short Guids translate to correct long Guids`` () =
    let testCases =
        [
            "FEx1sZbSD0ugmgMAF_RGHw", Guid "b1754c14-d296-4b0f-a09a-030017f4461f"
            "Xy0MVKupFES9NpmZ9TiHcw", Guid "540c2d5f-a9ab-4414-bd36-9999f5388773"
        ]

    testCases
    |> List.iter (fun (shortGuid, expectedGuid) ->
        let guid = ShortGuid.toGuid shortGuid
        Assert.Equal(expectedGuid, guid)
        |> ignore)

[<Fact>]
let ``Long Guids translate to correct short Guids`` () =
    let testCases =
        [
            "FEx1sZbSD0ugmgMAF_RGHw", Guid "b1754c14-d296-4b0f-a09a-030017f4461f"
            "Xy0MVKupFES9NpmZ9TiHcw", Guid "540c2d5f-a9ab-4414-bd36-9999f5388773"
        ]

    testCases
    |> List.iter (fun (shortGuid, longGuid) ->
        let guid = ShortGuid.fromGuid longGuid
        Assert.Equal(shortGuid, guid)
        |> ignore)

[<Fact>]
let ``Short Guids are always 22 characters long`` () =
    let testCases =
        [ 0..10 ]
        |> List.map (fun _ -> Guid.NewGuid())

    testCases
    |> List.iter (fun guid ->
        let shortGuid = ShortGuid.fromGuid guid
        Assert.Equal(22, shortGuid.Length)
        |> ignore)

[<Fact>]
let ``Short Ids are always 11 characters long`` () =
    let rand = new Random()
    let testCases =
        [ 0..10 ]
        |> List.map (fun _ -> rndInt64 rand)

    testCases
    |> List.iter (fun id ->
        let shortId = ShortId.fromUInt64 id
        Assert.Equal(11, shortId.Length)
        |> ignore)

[<Fact>]
let ``Short Ids translate correctly back and forth`` () =
    let rand = new Random()
    let testCases =
        [ 0..10 ]
        |> List.map (fun _ -> rndInt64 rand)

    testCases
    |> List.iter (fun origId ->
        let shortId = ShortId.fromUInt64 origId
        let id = ShortId.toUInt64 shortId
        Assert.Equal(origId, id)
        |> ignore)

[<Fact>]
let ``Short Ids translate to correct uint64 values`` () =
    let testCases =
        [
            "r1iKapqh_s4", 12635000945053400782UL
            "5aLu720NzTs", 16547050693006839099UL
            "BdQ5vc0d8-I", 420024152605193186UL
            "FOwfPLe6waQ", 1507614320903242148UL
        ]

    testCases
    |> List.iter (fun (shortId, id) ->
        let result = ShortId.toUInt64 shortId
        Assert.Equal(id, result)
        |> ignore)

[<Fact>]
let ``UInt64 values translate to correct short IDs`` () =
    let testCases =
        [
            "r1iKapqh_s4", 12635000945053400782UL
            "5aLu720NzTs", 16547050693006839099UL
            "BdQ5vc0d8-I", 420024152605193186UL
            "FOwfPLe6waQ", 1507614320903242148UL
        ]

    testCases
    |> List.iter (fun (shortId, id) ->
        let result = ShortId.fromUInt64 id
        Assert.Equal(shortId, result)
        |> ignore)