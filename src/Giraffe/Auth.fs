[<AutoOpen>]
module Giraffe.Auth

open System.Security.Claims
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks.ContextInsensitive

/// ** Description **
/// Challenges a client to authenticate via a specific `authScheme`.
///
/// ** Parameters **
///     - `authScheme`: The name of an authentication scheme from your application.
///
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let challenge (authScheme : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            do! ctx.ChallengeAsync authScheme
            return! next ctx
        }

/// ** Description **
/// Signs out the currently logged in user via the provided `authScheme`.
///
/// ** Parameters **
///     - `authScheme`: The name of an authentication scheme from your application.
///
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let signOut (authScheme : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            do! ctx.SignOutAsync authScheme
            return! next ctx
        }

/// ** Description **
/// Validates if a `ClaimsPrincipal` satisfies a certain condition. If the `policy` returns `true` then it will continue with the `next` function otherwise it will shortcircuit to the `authFailedHandler`.
///
/// ** Parameters **
///     - `policy`: One or many conditions which a `ClaimsPrincipal` must meet. The `policy` function should return `true` on success and `false` on failure.
///     - `authFailedHandler`: A `HttpHandler` function which will be executed when the `policy` returns `false`.
///
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let requiresAuthPolicy (policy : ClaimsPrincipal -> bool) (authFailedHandler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if policy ctx.User
        then next ctx
        else authFailedHandler finish ctx

/// ** Description **
/// Validates if a user has successfully authenticated. This function checks if the auth middleware was able to establish a user's identity by validating certain parts of the HTTP request (e.g. a cookie or a token) and set the `User` object of the `HttpContext`.
///
/// ** Parameters **
///     - `authFailedHandler`: A `HttpHandler` function which will be executed when authentication failed.
///
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let requiresAuthentication (authFailedHandler : HttpHandler) : HttpHandler =
    requiresAuthPolicy
        (fun user -> isNotNull user && user.Identity.IsAuthenticated)
        authFailedHandler

/// ** Description **
/// Validates if a user is in a specific role.
///
/// ** Parameters **
///     - `role`: The required role which a user must have in order to pass the validation.
///     - `authFailedHandler`: A `HttpHandler` function which will be executed when validation fails.
///
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let requiresRole (role : string) (authFailedHandler : HttpHandler) : HttpHandler =
    requiresAuthPolicy
        (fun user -> user.IsInRole role)
        authFailedHandler

/// ** Description **
/// Validates if a user is in at least one of many roles.
///
/// ** Parameters **
///     - `roles`: A list of roles which a user must be part of (minimum one) in order to pass the validation.
///     - `authFailedHandler`: A `HttpHandler` function which will be executed when validation fails.
///
/// ** Output **
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
let requiresRoleOf (roles : string list) (authFailedHandler : HttpHandler) : HttpHandler =
    requiresAuthPolicy
        (fun user -> List.exists user.IsInRole roles)
        authFailedHandler