module Giraffe.Tests.EndpointRoutingTests

open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Xunit
open Giraffe
open Giraffe.EndpointRouting
open System.Net.Http
open System.Net

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
                    route "/" (text "Hello World")
                    route "/foo" (text "bar")
                    routef "/try-a-guid/%O" (fun (guid: Guid) -> text $"Success: {guid}")
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

[<Theory>]
[<InlineData("/", "Hello World")>]
[<InlineData("/foo", "bar")>]
[<InlineData("/bar", "baz")>]
[<InlineData("/NOT-EXIST", "Not Found")>]
let ``routeWithExtensions: GET request returns expected result`` (path: string, expected: string) =
    let endpoints =
        [
            GET [
                routeWithExtensions (id) "/" (text "Hello World")
                route "/foo" (text "bar")
                routeWithExtensions (fun eb -> eb.RequireRateLimiting("nothing")) "/bar" (text "baz")
            ]
        ]

    let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

    let configureApp (app: IApplicationBuilder) =
        app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

    let configureServices (services: IServiceCollection) =
        services.AddRouting().AddGiraffe() |> ignore

    task {
        let request = createRequest HttpMethod.Get path

        let! response = makeRequest (fun () -> configureApp) configureServices () request

        let! content = response |> readText

        content |> shouldEqual expected
    }

[<Theory>]
[<InlineData("/empty/5", "/empty i = 5")>]
[<InlineData("/normal/10", "/normal i = 10")>]
[<InlineData("/cache/25", "/cache i = 25")>]
[<InlineData("/NOT-EXIST", "Not Found")>]
let ``routefWithExtensions: GET request returns expected result`` (path: string, expected: string) =
    let endpoints =
        [
            GET [
                routefWithExtensions (id) "/empty/%i" (fun i -> text $"/empty i = {i}")
                routef "/normal/%i" (fun i -> text $"/normal i = {i}")
                routefWithExtensions (fun eb -> eb.CacheOutput("nothing")) "/cache/%i" (fun i -> text $"/cache i = {i}")
            ]
        ]

    let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

    let configureApp (app: IApplicationBuilder) =
        app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

    let configureServices (services: IServiceCollection) =
        services.AddRouting().AddGiraffe() |> ignore

    task {
        let request = createRequest HttpMethod.Get path

        let! response = makeRequest (fun () -> configureApp) configureServices () request

        let! content = response |> readText

        content |> shouldEqual expected
    }

[<Theory>]
[<InlineData("/api/foo/5", "/foo i = 5")>]
[<InlineData("/api/bar/10", "/bar i = 10")>]
[<InlineData("/api/baz/25", "/baz i = 25")>]
[<InlineData("/NOT-EXIST", "Not Found")>]
let ``subRouteWithExtensions: GET request returns expected result`` (path: string, expected: string) =
    let endpoints =
        [
            subRouteWithExtensions (id) "/api" [
                GET [
                    routefWithExtensions (id) "/foo/%i" (fun i -> text $"/foo i = {i}")
                    routef "/bar/%i" (fun i -> text $"/bar i = {i}")
                    routefWithExtensions (fun eb -> eb.CacheOutput("nothing")) "/baz/%i" (fun i -> text $"/baz i = {i}")
                ]
            ]
        ]

    let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

    let configureApp (app: IApplicationBuilder) =
        app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

    let configureServices (services: IServiceCollection) =
        services.AddRouting().AddGiraffe() |> ignore

    task {
        let request = createRequest HttpMethod.Get path

        let! response = makeRequest (fun () -> configureApp) configureServices () request

        let! content = response |> readText

        content |> shouldEqual expected
    }

[<Theory>]
[<InlineData("/pet/42", "PetId: 42")>]
[<InlineData("/pet/0", "PetId: 0")>]
[<InlineData("/pet/123", "PetId: 123")>]
[<InlineData("/pet/-1", "PetId: -1")>]
[<InlineData("/pet/abc", "Not Found")>]
[<InlineData("/pet/123abc", "Not Found")>]
[<InlineData("/pet/123.456", "Not Found")>]
[<InlineData("/pet/123-456", "Not Found")>]
let ``routef: GET "/pet/%i:petId" returns named parameter`` (path: string, expected: string) =
    task {
        let endpoints: Endpoint list =
            [
                GET [ routef "/pet/%i:petId" (fun (petId: int) -> text ($"PetId: {petId}")) ]
                GET [
                    routef
                        "/foo/%i/bar/%i:barId"
                        (fun (fooId: int, barId: int) -> text ($"FooId: {fooId}, BarId: {barId}"))
                ]
            ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services.AddRouting().AddGiraffe() |> ignore

        let request = createRequest HttpMethod.Get path

        let! response = makeRequest (fun () -> configureApp) configureServices () request
        let! content = response |> readText
        content |> shouldEqual expected
    }

[<Theory>]
[<InlineData("/foo/123/bar/abc", "FooId: 123, BarId: abc")>]
[<InlineData("/foo/999/bar/789", "FooId: 999, BarId: 789")>]
[<InlineData("/foo/-1/bar/123", "FooId: -1, BarId: 123")>]
[<InlineData("/foo/abc/bar/def", "Not Found")>]
let ``routef: GET "/foo/%i:fooId/bar/%i" returns named and unnamed parameters`` (path: string, expected: string) =
    task {
        let endpoints: Endpoint list =
            [
                GET [
                    routef
                        "/foo/%i:fooId/bar/%s:barId"
                        (fun (fooId: int, barId: string) -> text ($"FooId: {fooId}, BarId: {barId}"))
                ]
            ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services.AddRouting().AddGiraffe() |> ignore

        let request = createRequest HttpMethod.Get path

        let! response = makeRequest (fun () -> configureApp) configureServices () request
        let! content = response |> readText
        content |> shouldEqual expected
    }

[<Theory>]
[<InlineData("/foo/123/bar/000/baz/aaa", "FooId: 123, BarId: 0, BazName: aaa")>]
[<InlineData("/foo/999/bar/789/baz/bbb", "FooId: 999, BarId: 789, BazName: bbb")>]
[<InlineData("/foo/-1/bar/123/baz/ccc", "FooId: -1, BarId: 123, BazName: ccc")>]
[<InlineData("/foo/abc/bar/v01/baz/ddd", "Not Found")>]
let ``routef: GET "/foo/%i:fooId/bar/%i/baz/%s" returns named and unnamed parameters``
    (path: string, expected: string)
    =
    task {
        let endpoints: Endpoint list =
            [
                GET [
                    routef
                        "/foo/%i:fooId/bar/%i/baz/%s"
                        (fun (fooId: int, barId: int, bazName: string) ->
                            text ($"FooId: {fooId}, BarId: {barId}, BazName: {bazName}")
                        )
                ]
            ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services.AddRouting().AddGiraffe() |> ignore

        let request = createRequest HttpMethod.Get path

        let! response = makeRequest (fun () -> configureApp) configureServices () request
        let! content = response |> readText
        content |> shouldEqual expected
    }

[<CLIMutable>]
type Name = { First: string; Last: string }

[<CLIMutable>]
type Person = { Name: Name; Age: int }

[<Theory>]
[<InlineData("/p/John/Doe/32", HttpStatusCode.OK, "Name.First: John, Name.Last: Doe, Age: 32")>]
[<InlineData("/p/John%20Paul/Doe/32", HttpStatusCode.OK, "Name.First: John Paul, Name.Last: Doe, Age: 32")>]
[<InlineData("/p/John%20Paul/Doe/32/", HttpStatusCode.OK, "Name.First: John Paul, Name.Last: Doe, Age: 32")>]
[<InlineData("/p/John/Doe/9111222333", HttpStatusCode.UnprocessableEntity, "")>]
[<InlineData("/p/John/Doe/not-a-number", HttpStatusCode.UnprocessableEntity, "")>]
[<InlineData("/p/John/Doe//", HttpStatusCode.NotFound, "Not Found")>]
let ``routebind: GET "/p/{Name.First}/{Name.Last}/{Age}" returns person object``
    (path: string, expectedStatus: HttpStatusCode, expectedContent: string)
    =
    task {
        let endpoints: Endpoint list =
            [
                GET [
                    routeBindWithExtensions<Person>
                        (fun eb -> eb.WithOrder 1)
                        "/p/{Name.First}/{Name.Last}/{Age}"
                        (fun (person: Person) ->
                            text ($"Name.First: {person.Name.First}, Name.Last: {person.Name.Last}, Age: {person.Age}")
                        )
                    routefWithExtensions
                        (fun eb -> eb.WithOrder 2)
                        "/p/%s:firstName/%s:lastName/%d:age"
                        (fun (firstName: string, lastName: string, age: int64) ->
                            text ($"firstName: {firstName}, lastName: {lastName}, age: {age}")
                        )
                ]
            ]

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp (app: IApplicationBuilder) =
            app.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

        let configureServices (services: IServiceCollection) =
            services.AddRouting().AddGiraffe() |> ignore

        let request = createRequest HttpMethod.Get path

        let! response = makeRequest (fun () -> configureApp) configureServices () request
        let! content = response |> isStatus expectedStatus |> readText

        content |> shouldEqual expectedContent
    }
