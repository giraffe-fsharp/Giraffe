module Giraffe.Tests.AuthTests

open System.IO
open System.Security.Claims
open Microsoft.AspNetCore.Http
open Xunit
open NSubstitute
open FsCheck
open FsCheck.Xunit
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe

[<AutoOpen>]
module TestApp =
    module Route =
        let [<Literal>] Anonymous       = "/anonymous"
        let [<Literal>] Authenticated   = "/authenticated"
        let [<Literal>] AdminOnly       = "/admin-only"
        let [<Literal>] AdminOrOperator = "/admin-or-operator"
        let [<Literal>] JohnOnly        = "/john-only"

    module Response =
        let [<Literal>] Anonymous       = "Hi, stranger"
        let [<Literal>] Authenticated   = "Hi, should I know you?"
        let [<Literal>] AdminOnly       = "Hi, admin"
        let [<Literal>] AdminOrOperator = "Hi, are you admin or operator?"
        let [<Literal>] JohnOnly        = "Hi, John"
        let [<Literal>] AccessDenied    = "Access Denied"

    type ExpectedResponse =
        | Unauthorized
        | Ok of string

    type User =
        | Anonymous
        | Authenticated of Claim list
        with
            member x.AsClaimsPrincipal =
                match x with
                | Anonymous       -> ClaimsPrincipal (ClaimsIdentity ())
                | Authenticated c -> ClaimsPrincipal (ClaimsIdentity (c, "foo"))

    let private accessDenied = setStatusCode 401 >=> text Response.AccessDenied
    let private ok content   = setStatusCode 200 >=> text content

    let private mustBeLoggedIn = requiresAuthentication accessDenied
    let private mustBeAdmin = requiresRole "admin" accessDenied
    let private mustBeOperatorOrAdmin = requiresRoleOf ["admin"; "operator"] accessDenied

    let private isJohn (user : ClaimsPrincipal) = user.HasClaim (ClaimTypes.Name, "John")
    let private mustBeJohn = evaluateUserPolicy isJohn accessDenied

    let app =
        GET >=> choose [
            route Route.Anonymous       >=> ok Response.Anonymous
            route Route.Authenticated   >=> mustBeLoggedIn        >=> ok Response.Authenticated
            route Route.AdminOnly       >=> mustBeAdmin           >=> ok Response.AdminOnly
            route Route.AdminOrOperator >=> mustBeOperatorOrAdmin >=> ok Response.AdminOrOperator
            route Route.JohnOnly        >=> mustBeJohn            >=> ok Response.JohnOnly
        ]

module TestData =

    let noClaims = []

    let mkName name =
        [Claim (ClaimTypes.Name, name)]

    let adminWithoutName =
        [Claim (ClaimTypes.Role, "admin")]

    let operatorWithoutName =
        [Claim (ClaimTypes.Role, "operator")]

    let mkAdmin name =
        [Claim (ClaimTypes.Name, name)
         Claim (ClaimTypes.Role, "admin")]

    let mkOperator name =
        [Claim (ClaimTypes.Name, name)
         Claim (ClaimTypes.Role, "operator")]

    let nonJohnNameGen =
        Arb.Default.NonEmptyString().Generator
        |> Gen.map (fun n -> n.Get)
        |> Gen.filter (fun n -> n <> "John")

    let anonymousGen =
        [Route.Anonymous,       Ok Response.Anonymous
         Route.Authenticated,   Unauthorized
         Route.AdminOnly,       Unauthorized
         Route.AdminOrOperator, Unauthorized
         Route.JohnOnly,        Unauthorized]
        |> Gen.elements
        |> Gen.map (fun (route, expectedResponse) ->
            Anonymous, route, expectedResponse
        )

    let nonJohnNoRoleGen =
        [Route.Anonymous,       Ok Response.Anonymous
         Route.Authenticated,   Ok Response.Authenticated
         Route.AdminOnly,       Unauthorized
         Route.AdminOrOperator, Unauthorized
         Route.JohnOnly,        Unauthorized]
        |> Gen.elements
        |> Gen.zip (Gen.oneof [Gen.map mkName nonJohnNameGen; Gen.constant noClaims])
        |> Gen.map (fun (user, (route, expectedResponse)) ->
            Authenticated user, route, expectedResponse
        )

    let nonJohnAdminGen =
        [Route.Anonymous,       Ok Response.Anonymous
         Route.Authenticated,   Ok Response.Authenticated
         Route.AdminOnly,       Ok Response.AdminOnly
         Route.AdminOrOperator, Ok Response.AdminOrOperator
         Route.JohnOnly,        Unauthorized]
        |> Gen.elements
        |> Gen.zip (Gen.oneof [Gen.map mkAdmin nonJohnNameGen; Gen.constant adminWithoutName])
        |> Gen.map (fun (user, (route, expectedResponse)) ->
            Authenticated user, route, expectedResponse
        )

    let nonJohnOperatorGen =
        [Route.Anonymous,       Ok Response.Anonymous
         Route.Authenticated,   Ok Response.Authenticated
         Route.AdminOnly,       Unauthorized
         Route.AdminOrOperator, Ok Response.AdminOrOperator
         Route.JohnOnly,        Unauthorized]
        |> Gen.elements
        |> Gen.zip (Gen.oneof [Gen.map mkOperator nonJohnNameGen; Gen.constant operatorWithoutName])
        |> Gen.map (fun (user, (route, expectedResponse)) ->
            Authenticated user, route, expectedResponse
        )

    let johnNoRoleGen =
        [Route.Anonymous,       Ok Response.Anonymous
         Route.Authenticated,   Ok Response.Authenticated
         Route.AdminOnly,       Unauthorized
         Route.AdminOrOperator, Unauthorized
         Route.JohnOnly,        Ok Response.JohnOnly]
        |> Gen.elements
        |> Gen.map (fun (route, expectedResponse) ->
            Authenticated (mkName "John"), route, expectedResponse
        )

    let johnAdminGen =
        [Route.Anonymous,       Ok Response.Anonymous
         Route.Authenticated,   Ok Response.Authenticated
         Route.AdminOnly,       Ok Response.AdminOnly
         Route.AdminOrOperator, Ok Response.AdminOrOperator
         Route.JohnOnly,        Ok Response.JohnOnly]
        |> Gen.elements
        |> Gen.map (fun (route, expectedResponse) ->
            Authenticated (mkAdmin "John"), route, expectedResponse
        )

    let johnOperatorGen =
        [Route.Anonymous,       Ok Response.Anonymous
         Route.Authenticated,   Ok Response.Authenticated
         Route.AdminOnly,       Unauthorized
         Route.AdminOrOperator, Ok Response.AdminOrOperator
         Route.JohnOnly,        Ok Response.JohnOnly]
        |> Gen.elements
        |> Gen.map (fun (route, expectedResponse) ->
            Authenticated (mkOperator "John"), route, expectedResponse
        )

    type AuthArb =
        static member Values =
            [
                anonymousGen
                nonJohnNoRoleGen
                nonJohnAdminGen
                nonJohnOperatorGen
                johnNoRoleGen
                johnAdminGen
                johnOperatorGen
            ]
            |> Gen.oneof
            |> Arb.fromGen

[<Property(Arbitrary=[|typeof<TestData.AuthArb>|])>]
let ``test authentication`` (givenUser: User) givenRoute expected =
    let ctx = Substitute.For<HttpContext>()
    ctx.Response.Body <- new MemoryStream ()
    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString (givenRoute)) |> ignore
    // Not injecting ClaimsPrincipal in generator with intention.
    // This is workaround to get nice pretty print in case test fails
    ctx.User <- givenUser.AsClaimsPrincipal

    let expectedStatusCode, expectedContent =
        match expected with
        | Unauthorized -> 401, Response.AccessDenied
        | Ok content   -> 200, content

    task {
        let! result = app next ctx

        match result with
        | None     -> assertFailf "It was expected that the result would be %s" expectedContent
        | Some ctx ->
            Assert.Equal(expectedStatusCode, getStatusCode ctx)
            Assert.Equal(expectedContent, getBody ctx)
    }
    // We have to execute test synchronously
    // https://github.com/fscheck/FsCheck/issues/167
    |> Async.AwaitTask
    |> Async.RunSynchronously