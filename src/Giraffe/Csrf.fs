namespace Giraffe

/// <summary>
/// CSRF (Cross-Site Request Forgery) protection helpers for Giraffe.
/// Provides anti-forgery token generation and validation.
/// </summary>
[<RequireQualifiedAccess>]
module Csrf =
    open System
    open System.Security.Cryptography
    open System.Text
    open Microsoft.AspNetCore.Http
    open Microsoft.Extensions.Logging
    open Microsoft.AspNetCore.Antiforgery

    /// <summary>
    /// Default CSRF token header name
    /// </summary>
    [<Literal>]
    let DefaultCsrfTokenHeaderName = "X-CSRF-TOKEN"

    /// <summary>
    /// Default CSRF token form field name
    /// </summary>
    [<Literal>]
    let DefaultCsrfTokenFormFieldName = "__RequestVerificationToken"

    /// <summary>
    /// Validates the CSRF token from the request.
    /// Checks for token in header (X-CSRF-TOKEN) or form field (__RequestVerificationToken).
    /// </summary>
    /// <param name="next">The next HttpFunc</param>
    /// <param name="ctx">The HttpContext</param>
    /// <returns>HttpFuncResult</returns>
    let validateCsrfToken: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let antiforgery = ctx.GetService<IAntiforgery>()

                try
                    let! isValid = antiforgery.IsRequestValidAsync(ctx)

                    if isValid then
                        return! next ctx
                    else
                        let logger = ctx.GetLogger("Giraffe.Csrf")
                        logger.LogWarning("CSRF token validation failed for request to {Path}", ctx.Request.Path)
                        ctx.Response.StatusCode <- 403
                        return Some ctx
                with ex ->
                    let logger = ctx.GetLogger("Giraffe.Csrf")
                    logger.LogWarning(ex, "CSRF token validation error for request to {Path}", ctx.Request.Path)
                    ctx.Response.StatusCode <- 403
                    return Some ctx
            }

    /// <summary>
    /// Alias for validateCsrfToken - validates anti-forgery tokens from requests.
    /// </summary>
    let requireAntiforgeryToken = validateCsrfToken

    /// <summary>
    /// Generates a CSRF token and adds it to the HttpContext items for use in views.
    /// The token can be accessed via ctx.Items["CsrfToken"] and ctx.Items["CsrfTokenHeaderName"].
    /// </summary>
    /// <param name="next">The next HttpFunc</param>
    /// <param name="ctx">The HttpContext</param>
    /// <returns>HttpFuncResult</returns>
    let generateCsrfToken: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let antiforgery = ctx.GetService<IAntiforgery>()
                let tokens = antiforgery.GetAndStoreTokens(ctx)

                // Store token for view rendering
                ctx.Items.["CsrfToken"] <- tokens.RequestToken
                ctx.Items.["CsrfTokenHeaderName"] <- tokens.HeaderName

                return! next ctx
            }

    /// <summary>
    /// Returns the CSRF token as JSON for AJAX requests.
    /// Response format: { "token": "...", "headerName": "X-CSRF-TOKEN" }
    /// </summary>
    /// <param name="next">The next HttpFunc</param>
    /// <param name="ctx">The HttpContext</param>
    /// <returns>HttpFuncResult</returns>
    let csrfTokenJson: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let antiforgery = ctx.GetService<IAntiforgery>()
                let tokens = antiforgery.GetAndStoreTokens(ctx)

                let response =
                    {|
                        token = tokens.RequestToken
                        headerName = tokens.HeaderName
                    |}

                return! Core.json response next ctx
            }

    /// <summary>
    /// Returns the CSRF token as an HTML hidden input field.
    /// Can be included directly in forms.
    /// </summary>
    /// <param name="next">The next HttpFunc</param>
    /// <param name="ctx">The HttpContext</param>
    /// <returns>HttpFuncResult</returns>
    let csrfTokenHtml: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let antiforgery = ctx.GetService<IAntiforgery>()
                let tokens = antiforgery.GetAndStoreTokens(ctx)

                let html =
                    sprintf "<input type=\"hidden\" name=\"%s\" value=\"%s\" />" tokens.HeaderName tokens.RequestToken

                return! Core.htmlString html next ctx
            }
