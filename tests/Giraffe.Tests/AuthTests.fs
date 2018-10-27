module Giraffe.Tests.AuthTests

open System.IO
open System.Security.Claims
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks.V2.ContextInsensitive
open NSubstitute
open Xunit
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

[<AutoOpen>]
module TestData =

    let noClaims = []

    let mkName name =
        [ Claim (ClaimTypes.Name, name) ]

    let adminWithoutName =
        [ Claim (ClaimTypes.Role, "admin") ]

    let operatorWithoutName =
        [ Claim (ClaimTypes.Role, "operator") ]

    let mkAdmin name =
        [ Claim (ClaimTypes.Name, name)
          Claim (ClaimTypes.Role, "admin") ]

    let mkOperator name =
        [ Claim (ClaimTypes.Name, name)
          Claim (ClaimTypes.Role, "operator") ]

let testAuthentication (givenUser : User) givenRoute expected =
    task {
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

        let! result = app next ctx

        match result with
        | None     -> assertFailf "It was expected that the result would be %s" expectedContent
        | Some ctx ->
            Assert.Equal(expectedStatusCode, getStatusCode ctx)
            Assert.Equal(expectedContent, getBody ctx)
    }

[<Fact>]
let ``Anonymous user can access anonymous route`` () =
    testAuthentication Anonymous Route.Anonymous (Ok Response.Anonymous)

[<Fact>]
let ``Anonymous user cannot access authenticated route`` () =
    testAuthentication Anonymous Route.Authenticated Unauthorized

[<Fact>]
let ``Anonymous user can access admin only route`` () =
    testAuthentication Anonymous Route.AdminOnly Unauthorized

[<Fact>]
let ``Anonymous user can access admin or operator route`` () =
    testAuthentication Anonymous Route.AdminOrOperator Unauthorized

[<Fact>]
let ``Anonymous user can access john only route`` () =
    testAuthentication Anonymous Route.JohnOnly Unauthorized

[<Fact>]
let ``Authenticated user with no claims can access anonymous route`` () =
    testAuthentication (Authenticated noClaims) Route.Anonymous (Ok Response.Anonymous)

[<Fact>]
let ``Authenticated user who is not an admin or operator or John can access anonymous route`` () =
    testAuthentication (Authenticated (mkName "Bill")) Route.Anonymous (Ok Response.Anonymous)

[<Fact>]
let ``Authenticated user with no claims can access authenticated route`` () =
    testAuthentication (Authenticated noClaims) Route.Authenticated (Ok Response.Authenticated)

[<Fact>]
let ``Authenticated user who is not an admin or operator or John can access authenticated route`` () =
    testAuthentication (Authenticated (mkName "Bill")) Route.Authenticated (Ok Response.Authenticated)

[<Fact>]
let ``Authenticated user with no claims cannot access admin only route`` () =
    testAuthentication (Authenticated noClaims) Route.AdminOnly Unauthorized

[<Fact>]
let ``Authenticated user who is not an admin or operator or John cannot access admin only route`` () =
    testAuthentication (Authenticated (mkName "Bill")) Route.AdminOnly Unauthorized

[<Fact>]
let ``Authenticated user with no claims cannot access admin or operator route`` () =
    testAuthentication (Authenticated noClaims) Route.AdminOrOperator Unauthorized

[<Fact>]
let ``Authenticated user who is not an admin or operator or John cannot access admin or operator route`` () =
    testAuthentication (Authenticated (mkName "Bill")) Route.AdminOrOperator Unauthorized

[<Fact>]
let ``Authenticated user with no claims cannot access john only route`` () =
    testAuthentication (Authenticated noClaims) Route.JohnOnly Unauthorized

[<Fact>]
let ``Authenticated user who is not an admin or operator or John cannot access john only route`` () =
    testAuthentication (Authenticated (mkName "Bill")) Route.JohnOnly Unauthorized

[<Fact>]
let ``Admin who is not John can access anonymous route`` () =
    testAuthentication (Authenticated (mkAdmin "Susan")) Route.Anonymous (Ok Response.Anonymous)

[<Fact>]
let ``Admin without name can access anonymous route`` () =
    testAuthentication (Authenticated adminWithoutName) Route.Anonymous (Ok Response.Anonymous)

[<Fact>]
let ``Admin who is not John can access authenticated route`` () =
    testAuthentication (Authenticated (mkAdmin "Susan")) Route.Authenticated (Ok Response.Authenticated)

[<Fact>]
let ``Admin without name can access authenticated route`` () =
    testAuthentication (Authenticated adminWithoutName) Route.Authenticated (Ok Response.Authenticated)

[<Fact>]
let ``Admin who is not John can access admin only route`` () =
    testAuthentication (Authenticated (mkAdmin "Susan")) Route.AdminOnly (Ok Response.AdminOnly)

[<Fact>]
let ``Admin without name can access admin only route`` () =
    testAuthentication (Authenticated adminWithoutName) Route.AdminOnly (Ok Response.AdminOnly)

[<Fact>]
let ``Admin who is not John can access admin or operator only route`` () =
    testAuthentication (Authenticated (mkAdmin "Susan")) Route.AdminOrOperator (Ok Response.AdminOrOperator)

[<Fact>]
let ``Admin without name can access admin or operator only route`` () =
    testAuthentication (Authenticated adminWithoutName) Route.AdminOrOperator (Ok Response.AdminOrOperator)

[<Fact>]
let ``Admin who is not John cannot access john only route`` () =
    testAuthentication (Authenticated (mkAdmin "Susan")) Route.JohnOnly Unauthorized

[<Fact>]
let ``Admin without name cannot access john only route`` () =
    testAuthentication (Authenticated adminWithoutName) Route.JohnOnly Unauthorized

[<Fact>]
let ``Operator who is not John can access anonymous route`` () =
    testAuthentication (Authenticated (mkOperator "Kate")) Route.Anonymous (Ok Response.Anonymous)

[<Fact>]
let ``Operator without name can access anonymous route`` () =
    testAuthentication (Authenticated operatorWithoutName) Route.Anonymous (Ok Response.Anonymous)

[<Fact>]
let ``Operator who is not John can access authenticated route`` () =
    testAuthentication (Authenticated (mkOperator "Kate")) Route.Authenticated (Ok Response.Authenticated)

[<Fact>]
let ``Operator without name can access authenticated route`` () =
    testAuthentication (Authenticated operatorWithoutName) Route.Authenticated (Ok Response.Authenticated)

[<Fact>]
let ``Operator who is not John cannot access admin only route`` () =
    testAuthentication (Authenticated (mkOperator "Kate")) Route.AdminOnly Unauthorized

[<Fact>]
let ``Operator without name cannot access admin only route`` () =
    testAuthentication (Authenticated operatorWithoutName) Route.AdminOnly Unauthorized

[<Fact>]
let ``Operator who is not John can access admin or operator route`` () =
    testAuthentication (Authenticated (mkOperator "Kate")) Route.AdminOrOperator (Ok Response.AdminOrOperator)

[<Fact>]
let ``Operator without name can access admin or operator route`` () =
    testAuthentication (Authenticated operatorWithoutName) Route.AdminOrOperator (Ok Response.AdminOrOperator)

[<Fact>]
let ``Operator who is not John cannot access john only route`` () =
    testAuthentication (Authenticated (mkOperator "Kate")) Route.JohnOnly Unauthorized

[<Fact>]
let ``Operator without name cannot access john only route`` () =
    testAuthentication (Authenticated operatorWithoutName) Route.JohnOnly Unauthorized

[<Fact>]
let ``John without a role can access anonymous route`` () =
    testAuthentication (Authenticated (mkName "John")) Route.Anonymous (Ok Response.Anonymous)

[<Fact>]
let ``John without a role can access authenticated route`` () =
    testAuthentication (Authenticated (mkName "John")) Route.Authenticated (Ok Response.Authenticated)

[<Fact>]
let ``John without a role cannot access admin only route`` () =
    testAuthentication (Authenticated (mkName "John")) Route.AdminOnly Unauthorized

[<Fact>]
let ``John without a role cannot access admin or operator route`` () =
    testAuthentication (Authenticated (mkName "John")) Route.AdminOrOperator Unauthorized

[<Fact>]
let ``John without a role can access john only route`` () =
    testAuthentication (Authenticated (mkName "John")) Route.JohnOnly (Ok Response.JohnOnly)

[<Fact>]
let ``John who is an admin can access anonymous route`` () =
    testAuthentication (Authenticated (mkAdmin "John")) Route.Anonymous (Ok Response.Anonymous)

[<Fact>]
let ``John who is an admin can access authenticated route`` () =
    testAuthentication (Authenticated (mkAdmin "John")) Route.Authenticated (Ok Response.Authenticated)

[<Fact>]
let ``John who is an admin can access admin only route`` () =
    testAuthentication (Authenticated (mkAdmin "John")) Route.AdminOnly (Ok Response.AdminOnly)

[<Fact>]
let ``John who is an admin can access admin or operator route`` () =
    testAuthentication (Authenticated (mkAdmin "John")) Route.AdminOrOperator (Ok Response.AdminOrOperator)

[<Fact>]
let ``John who is an admin can access john only route`` () =
    testAuthentication (Authenticated (mkAdmin "John")) Route.JohnOnly (Ok Response.JohnOnly)

[<Fact>]
let ``John who is an operator can access anonymous route`` () =
    testAuthentication (Authenticated (mkOperator "John")) Route.Anonymous (Ok Response.Anonymous)

[<Fact>]
let ``John who is an operator can access authenticated route`` () =
    testAuthentication (Authenticated (mkOperator "John")) Route.Authenticated (Ok Response.Authenticated)

[<Fact>]
let ``John who is an operator cannot access admin only route`` () =
    testAuthentication (Authenticated (mkOperator "John")) Route.AdminOnly Unauthorized

[<Fact>]
let ``John who is an operator can access admin or operator route`` () =
    testAuthentication (Authenticated (mkOperator "John")) Route.AdminOrOperator (Ok Response.AdminOrOperator)

[<Fact>]
let ``John who is an operator can access john only route`` () =
    testAuthentication (Authenticated (mkOperator "John")) Route.JohnOnly (Ok Response.JohnOnly)