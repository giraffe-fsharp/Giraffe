[<AutoOpen>]
module Giraffe.HttpHandlers

open System.Security.Claims
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Giraffe.Tasks
open Giraffe.Common

/// ---------------------------
/// HttpHandler definition
/// ---------------------------

type HttpFuncResult = Task<HttpContext option>
type HttpFunc       = HttpContext -> HttpFuncResult
type HttpHandler    = HttpFunc -> HttpFunc
type ErrorHandler   = exn -> ILogger -> HttpHandler

/// ---------------------------
/// Globally useful functions
/// ---------------------------

let inline warbler f (next : HttpFunc) (ctx : HttpContext) = f (next, ctx) next ctx

let internal abort  : HttpFuncResult = Task.FromResult None
let internal finish : HttpFunc       = Some >> Task.FromResult

/// ---------------------------
/// Default Combinators
/// ---------------------------

/// Combines two HttpHandler functions into one.
let compose (handler1 : HttpHandler) (handler2 : HttpHandler) : HttpHandler =
    fun (final : HttpFunc) ->
        let func = final |> handler2 |> handler1
        fun (ctx : HttpContext) ->
            match ctx.Response.HasStarted with
            | true  -> final ctx
            | false -> func ctx

/// Combines two HttpHandler functions into one.
/// See compose for more information.
let (>=>) = compose

// Allows a pre-complied list of HttpFuncs to be tested,
// by pre-applying next to handler list passed from choose
let rec private chooseHttpFunc (funcs : HttpFunc list) : HttpFunc =
    fun (ctx : HttpContext) ->
        task {
            match funcs with
            | [] -> return None
            | func :: tail ->
                let! result = func ctx
                match result with
                | Some c -> return Some c
                | None   -> return! chooseHttpFunc tail ctx
        }

/// Iterates through a list of HttpHandler functions and returns the
/// result of the first HttpHandler which outcome is Some HttpContext
let choose (handlers : HttpHandler list) : HttpHandler =
    fun (next : HttpFunc) ->
        let funcs = handlers |> List.map (fun h -> h next)
        fun (ctx : HttpContext) ->
            chooseHttpFunc funcs ctx

/// ---------------------------
/// Default HttpHandlers
/// ---------------------------

/// Filters an incoming HTTP request based on the HTTP verb
let httpVerb (validate : string -> bool) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        if validate ctx.Request.Method
        then next ctx
        else abort

let GET     : HttpHandler = httpVerb HttpMethods.IsGet
let POST    : HttpHandler = httpVerb HttpMethods.IsPost
let PUT     : HttpHandler = httpVerb HttpMethods.IsPut
let PATCH   : HttpHandler = httpVerb HttpMethods.IsPatch
let DELETE  : HttpHandler = httpVerb HttpMethods.IsDelete
let HEAD    : HttpHandler = httpVerb HttpMethods.IsHead
let OPTIONS : HttpHandler = httpVerb HttpMethods.IsOptions
let TRACE   : HttpHandler = httpVerb HttpMethods.IsTrace
let CONNECT : HttpHandler = httpVerb HttpMethods.IsConnect

/// Filters an incoming HTTP request based on the accepted
/// mime types of the client.
let mustAccept (mimeTypes : string list) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let headers = ctx.Request.GetTypedHeaders()
        headers.Accept
        |> Seq.map    (fun h -> h.ToString())
        |> Seq.exists (fun h -> mimeTypes |> Seq.contains h)
        |> function
            | true  -> next ctx
            | false -> abort

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

/// Attempts to clear the current HttpResponse object.
/// This can be useful inside an error handler when the response
/// needs to be overwritten in the case of a failure.
let clearResponse : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.Response.Clear()
        next ctx

/// Sets the HTTP response status code.
let setStatusCode (statusCode : int) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetStatusCode statusCode
        next ctx

/// Sets a HTTP header in the HTTP response.
let setHttpHeader (key : string) (value : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetHttpHeader key value
        next ctx

/// Redirects to a different location with a 302 or 301 (when permanent) HTTP status code.
let redirectTo (permanent : bool) (location : string) : HttpHandler  =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.Response.Redirect(location, permanent)
        Task.FromResult (Some ctx)