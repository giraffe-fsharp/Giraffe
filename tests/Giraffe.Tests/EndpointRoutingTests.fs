module Giraffe.Tests.EndpointRoutingTests

open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Xunit
open Giraffe
open Giraffe.EndpointRouting
open System.Net.Http

// ---------------------------------
// routef Tests
// ---------------------------------

[<Theory>]
[<InlineData("00000000-0000-0000-0000-0000000000000", "Not Found")>]
[<InlineData("00000000-0000-0000-0000-000000000000", "Success: 00000000-0000-0000-0000-000000000000")>]
[<InlineData("00000000-0000-0000-0000-00000000000", "Not Found")>]
[<InlineData("0000000000000000000000", "Success: d3344dd3-344d-4dd3-34d3-4d34d34d34d3")>] // ShortGuid
[<InlineData("DBvYFN7y#$@u933Kc8pM#^", "Not Found")>]
[<InlineData("8b3557db-fa-c0c90785ec0b", "Not Found")>]
[<InlineData("8b3557db-fa-c0c90785ec", "Success: e7f9bdf1-5bb7-f6f9-be73-473dd3bf3979")>] // ShortGuid
[<InlineData("8b3557db-fa-c0c90785e", "Not Found")>]
[<InlineData("does-not-make-sense", "Not Found")>]
[<InlineData("1", "Not Found")>]
let ``routef: GET "/try-a-guid/%O" returns "Success: ..." or "Not Found"`` (potentialGuid: string, expected: string) =
    task {
        let endpoints: Endpoint list =
            [
                GET [
                    route "/" [] (text "Hello World")
                    route "/foo" [] (text "bar")
                    routef "/try-a-guid/%O" [] (fun (guid: Guid) -> text $"Success: {guid}")
                ]
            ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services.AddRouting().AddGiraffe() |> ignore

        let request = createRequest HttpMethod.Get $"/try-a-guid/{potentialGuid}"

        let! response = makeRequest (fun () -> configureApp) configureServices () request

        let! content = response |> readText

        content |> shouldEqual expected
    }
