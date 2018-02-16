module Giraffe.Tests.ModelValidationTests

open System
open System.Net
open System.Net.Http
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Xunit
open Giraffe
open Microsoft.Extensions.Logging

// ---------------------------------
// Model Validation App
// ---------------------------------

[<CLIMutable>]
type Adult =
    {
        FirstName  : string
        MiddleName : string option
        LastName   : string
        Age        : int
    }
    override this.ToString() =
        sprintf "Name: %s%s %s, Age: %i"
            this.FirstName
            (if this.MiddleName.IsSome then " " + this.MiddleName.Value else "")
            this.LastName
            this.Age

    member this.HasErrors() =
        if      this.FirstName.Length < 3  then Some "First name is too short."
        else if this.FirstName.Length > 50 then Some "First name is too long."
        else if this.LastName.Length  < 3  then Some "Last name is too short."
        else if this.LastName.Length  > 50 then Some "Last name is too long."
        else if this.Age < 18              then Some "Person must be an adult (age >= 18)."
        else if this.Age > 150             then Some "Person must be a human being."
        else None

    interface IModelValidation<Adult> with
        member this.Validate() =
            match this.HasErrors() with
            | Some msg -> Error (RequestErrors.badRequest (text msg))
            | None     -> Ok this

module Urls =
    let person  = "/person"

module WebApp =
    let textHandler (x : obj) = text (x.ToString())
    let parsingErrorHandler err = RequestErrors.badRequest (text err)
    let culture = None
    let tryBindQueryToAdult = tryBindQuery<Adult> parsingErrorHandler culture

    let webApp _ =
        choose [
            route Urls.person
            >=> tryBindQueryToAdult (validateModel textHandler)
        ]

    let errorHandler (ex : Exception) (_ : ILogger) : HttpHandler =
        printfn "Error: %s" ex.Message
        printfn "StackTrace:%s %s" Environment.NewLine ex.StackTrace
        setStatusCode 500 >=> text ex.Message

    let configureApp args (app : IApplicationBuilder) =
        app.UseGiraffeErrorHandler(errorHandler)
           .UseGiraffe(webApp args)

    let configureServices (services : IServiceCollection) =
        services.AddGiraffe() |> ignore

let makeRequest req = makeRequest WebApp.configureApp WebApp.configureServices req

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``validateModel with valid model`` () =
    let url = sprintf "%s?firstName=John&lastName=Doe&age=35" Urls.person
    createRequest HttpMethod.Get url
    |> makeRequest (None, None)
    |> isStatus HttpStatusCode.OK
    |> readText
    |> shouldEqual "Name: John Doe, Age: 35"

[<Fact>]
let ``validateModel with invalid model`` () =
    let url = sprintf "%s?firstName=John&lastName=Doe&age=17" Urls.person
    createRequest HttpMethod.Get url
    |> makeRequest (None, None)
    |> isStatus HttpStatusCode.BadRequest
    |> readText
    |> shouldEqual "Person must be an adult (age >= 18)."