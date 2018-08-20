[<AutoOpen>]
module Giraffe.Auth

open System.Security.Claims
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authorization
open FSharp.Control.Tasks.V2.ContextInsensitive

/// **Description**
///
/// Challenges a client to authenticate via a specific `authScheme`.
///
/// **Parameters**
///
/// - `authScheme`: The name of an authentication scheme from your application.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let challenge (authScheme : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            do! ctx.ChallengeAsync authScheme
            return! next ctx
        }

/// **Description**
///
/// Signs out the currently logged in user via the provided `authScheme`.
///
/// **Parameters**
///
/// - `authScheme`: The name of an authentication scheme from your application.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let signOut (authScheme : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            do! ctx.SignOutAsync authScheme
            return! next ctx
        }

/// **Description**
///
/// Validates if a `ClaimsPrincipal` satisfies a certain condition. If the `policy` returns `true` then it will continue with the `next` function otherwise it will shortcircuit to the `authFailedHandler`.
///
/// **Parameters**
///
/// - `policy`: One or many conditions which a `ClaimsPrincipal` must meet. The `policy` function should return `true` on success and `false` on failure.
/// - `authFailedHandler`: A `HttpHandler` function which will be executed when the `policy` returns `false`.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let evaluateUserPolicy (policy : ClaimsPrincipal -> bool) (authFailedHandler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if policy ctx.User
        then next ctx
        else authFailedHandler finish ctx

/// **Description**
///
/// Validates if a user has successfully authenticated. This function checks if the auth middleware was able to establish a user's identity by validating certain parts of the HTTP request (e.g. a cookie or a token) and set the `User` object of the `HttpContext`.
///
/// **Parameters**
///
/// - `authFailedHandler`: A `HttpHandler` function which will be executed when authentication failed.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let requiresAuthentication (authFailedHandler : HttpHandler) : HttpHandler =
    evaluateUserPolicy
        (fun user -> isNotNull user && user.Identity.IsAuthenticated)
        authFailedHandler

/// **Description**
///
/// Validates if a user is a member of a specific role.
///
/// **Parameters**
///
/// - `role`: The required role of which a user must be a member of in order to pass the validation.
/// - `authFailedHandler`: A `HttpHandler` function which will be executed when validation fails.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let requiresRole (role : string) (authFailedHandler : HttpHandler) : HttpHandler =
    evaluateUserPolicy
        (fun user -> user.IsInRole role)
        authFailedHandler

/// **Description**
///
/// Validates if a user is a member of at least one of a given list of roles.
///
/// **Parameters**
///
/// - `roles`: A list of roles of which a user must be a member of (minimum one) in order to pass the validation.
/// - `authFailedHandler`: A `HttpHandler` function which will be executed when validation fails.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let requiresRoleOf (roles : string list) (authFailedHandler : HttpHandler) : HttpHandler =
    evaluateUserPolicy
        (fun user -> List.exists user.IsInRole roles)
        authFailedHandler

/// **Description**
///
/// Validates if a user meets a given authorization policy.
///
/// **Parameters**
///
/// - `policyName`: The name of an `AuthorizationPolicy` which a user must meet in order to pass the validation.
/// - `authFailedHandler`: A `HttpHandler` function which will be executed when validation fails.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let authorizeByPolicyName (policyName : string) (authFailedHandler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let authService = ctx.GetService<IAuthorizationService>()
            let! result = authService.AuthorizeAsync (ctx.User, policyName)
            return! (if result.Succeeded then next else authFailedHandler finish) ctx
        }

/// **Description**
///
/// Validates if a user meets a given authorization policy.
///
/// **Parameters**
///
/// - `policy`: An `AuthorizationPolicy` which a user must meet in order to pass the validation.
/// - `authFailedHandler`: A `HttpHandler` function which will be executed when validation fails.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let authorizeByPolicy (policy : AuthorizationPolicy) (authFailedHandler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let authService = ctx.GetService<IAuthorizationService>()
            let! result = authService.AuthorizeAsync (ctx.User, policy)
            return! (if result.Succeeded then next else authFailedHandler finish) ctx
        }