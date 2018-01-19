[<AutoOpen>]
module Giraffe.Auth

open System.Security.Claims
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Http
open Giraffe.Tasks
open Giraffe.Common

/// ---------------------------
/// Auth HttpHandler functions
/// ---------------------------

/// Challenges the client to authenticate with a given authentication scheme.
let challenge (authScheme : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            do! ctx.ChallengeAsync authScheme
            return! next ctx
        }

/// Signs off the current user.
let signOff (authScheme : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            do! ctx.SignOutAsync authScheme
            return! next ctx
        }

/// Validates if a user satisfies a policy requirement.
/// If not it will proceed with the authFailedHandler.
let requiresAuthPolicy (policy : ClaimsPrincipal -> bool) (authFailedHandler : HttpHandler) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if policy ctx.User
        then next ctx
        else authFailedHandler finish ctx

/// Validates if a user is authenticated.
/// If not it will proceed with the authFailedHandler.
let requiresAuthentication (authFailedHandler : HttpHandler) : HttpHandler =
    requiresAuthPolicy
        (fun user -> isNotNull user && user.Identity.IsAuthenticated)
        authFailedHandler

/// Validates if a user is in a specific role.
/// If not it will proceed with the authFailedHandler.
let requiresRole (role : string) (authFailedHandler : HttpHandler) : HttpHandler =
    requiresAuthPolicy
        (fun user -> user.IsInRole role)
        authFailedHandler

/// Validates if a user has at least one of the specified roles.
/// If not it will proceed with the authFailedHandler.
let requiresRoleOf (roles : string list) (authFailedHandler : HttpHandler) : HttpHandler =
    requiresAuthPolicy
        (fun user -> List.exists user.IsInRole roles)
        authFailedHandler