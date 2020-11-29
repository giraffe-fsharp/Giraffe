namespace Giraffe

[<AutoOpen>]
module Auth =
    open System
    open System.Security.Claims
    open Microsoft.AspNetCore.Http
    open Microsoft.AspNetCore.Authentication
    open Microsoft.AspNetCore.Authorization
    open FSharp.Control.Tasks.V2.ContextInsensitive

    /// <summary>
    /// Challenges a client to authenticate via a specific authScheme.
    /// </summary>
    /// <param name="authScheme">The name of an authentication scheme from your application.</param>
    /// <param name="next"></param>
    /// <param name="ctx"></param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let challenge (authScheme : string) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                do! ctx.ChallengeAsync authScheme
                return! next ctx
            }

    /// <summary>
    /// Signs out the currently logged in user via the provided authScheme.
    /// </summary>
    /// <param name="authScheme">The name of an authentication scheme from your application.</param>
    /// <param name="next"></param>
    /// <param name="ctx"></param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let signOut (authScheme : string) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                do! ctx.SignOutAsync authScheme
                return! next ctx
            }

    /// <summary>
    /// Validates if a <see cref="Microsoft.AspNetCore.Http.HttpContext"/> satisfies a certain condition. If the policy returns true then it will continue with the next function otherwise it will short circuit and execute the authFailedHandler.
    /// </summary>
    /// <param name="predicate">One or many conditions which a <see cref="Microsoft.AspNetCore.Http.HttpContext"/> must meet. The policy function should return true on success and false on failure.</param>
    /// <param name="authFailedHandler">A <see cref="HttpHandler"/> function which will be executed when the policy returns false.</param>
    /// <param name="next"></param>
    /// <param name="ctx"></param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let authorizeRequest (predicate : HttpContext -> bool) (authFailedHandler : HttpHandler) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            (if predicate ctx then next else authFailedHandler earlyReturn) ctx

    [<Obsolete("Please use `authorizeUser` as a replacement for `evaluateUserPolicy`. In the next major version this function will be removed.")>]
    /// <summary>
    /// Validates if a <see cref="System.Security.Claims.ClaimsPrincipal"/> satisfies a certain condition. If the policy returns true then it will continue with the next function otherwise it will short circuit and execute the authFailedHandler.
    /// </summary>
    /// <param name="policy">One or many conditions which a <see cref="System.Security.Claims.ClaimsPrincipal"/> must meet. The policy function should return true on success and false on failure.</param>
    /// <param name="authFailedHandler">A <see cref="HttpHandler"/> function which will be executed when the policy returns false.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let evaluateUserPolicy (policy : ClaimsPrincipal -> bool) (authFailedHandler : HttpHandler) : HttpHandler =
        authorizeRequest
            (fun ctx -> policy ctx.User)
            authFailedHandler

    /// <summary>
    /// Validates if a <see cref="System.Security.Claims.ClaimsPrincipal"/> satisfies a certain condition. If the policy returns true then it will continue with the next function otherwise it will short circuit and execute the authFailedHandler.
    /// </summary>
    /// <param name="policy">One or many conditions which a <see cref="System.Security.Claims.ClaimsPrincipal"/> must meet. The policy function should return true on success and false on failure.</param>
    /// <param name="authFailedHandler">A <see cref="HttpHandler"/> function which will be executed when the policy returns false.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let authorizeUser = evaluateUserPolicy

    /// <summary>
    /// Validates if a user has successfully authenticated. This function checks if the auth middleware was able to establish a user's identity by validating certain parts of the HTTP request (e.g. a cookie or a token) and set the <see cref="Microsoft.AspNetCore.Http.HttpContext.User"/> object of the <see cref="Microsoft.AspNetCore.Http.HttpContex"/>.
    /// </summary>
    /// <param name="authFailedHandler">A <see cref="HttpHandler"/> function which will be executed when authentication failed.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let requiresAuthentication (authFailedHandler : HttpHandler) : HttpHandler =
        authorizeUser
            (fun user -> isNotNull user && user.Identity.IsAuthenticated)
            authFailedHandler

    /// <summary>
    /// Validates if a user is a member of a specific role.
    /// </summary>
    /// <param name="role">The required role of which a user must be a member of in order to pass the validation.</param>
    /// <param name="authFailedHandler">A <see cref="HttpHandler"/> function which will be executed when validation fails.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let requiresRole (role : string) (authFailedHandler : HttpHandler) : HttpHandler =
        authorizeUser
            (fun user -> user.IsInRole role)
            authFailedHandler

    /// <summary>
    /// Validates if a user is a member of at least one of a given list of roles.
    /// </summary>
    /// <param name="roles">A list of roles of which a user must be a member of (minimum one) in order to pass the validation.</param>
    /// <param name="authFailedHandler">A <see cref="HttpHandler"/> function which will be executed when validation fails.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let requiresRoleOf (roles : string list) (authFailedHandler : HttpHandler) : HttpHandler =
        authorizeUser
            (fun user -> List.exists user.IsInRole roles)
            authFailedHandler

    /// <summary>
    /// Validates if a user meets a given authorization policy.
    /// </summary>
    /// <param name="policyName">The name of an <see cref="Microsoft.AspNetCore.Authorization.AuthorizationPolicy"/> which a user must meet in order to pass the validation.</param>
    /// <param name="authFailedHandler">A <see cref="HttpHandler"/> function which will be executed when validation fails.</param>
    /// <param name="next"></param>
    /// <param name="ctx"></param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let authorizeByPolicyName (policyName : string) (authFailedHandler : HttpHandler) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let authService = ctx.GetService<IAuthorizationService>()
                let! result = authService.AuthorizeAsync (ctx.User, policyName)
                return! (if result.Succeeded then next else authFailedHandler earlyReturn) ctx
            }

    /// <summary>
    /// Validates if a user meets a given authorization policy.
    /// </summary>
    /// <param name="policy">The name of an <see cref="Microsoft.AspNetCore.Authorization.AuthorizationPolicy"/> which a user must meet in order to pass the validation.</param>
    /// <param name="authFailedHandler">A <see cref="HttpHandler"/> function which will be executed when validation fails.</param>
    /// <param name="next"></param>
    /// <param name="ctx"></param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let authorizeByPolicy (policy : AuthorizationPolicy) (authFailedHandler : HttpHandler) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let authService = ctx.GetService<IAuthorizationService>()
                let! result = authService.AuthorizeAsync (ctx.User, policy)
                return! (if result.Succeeded then next else authFailedHandler earlyReturn) ctx
            }