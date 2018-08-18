[<AutoOpen>]
module Giraffe.HttpStatusCodeHandlers

/// **Description**
///
/// A collection of `HttpHandler` functions to return HTTP status code `1xx` responses.
///
module Intermediate =
    let CONTINUE        : HttpHandler = setStatusCode 100 >=> setBody [||]
    let SWITCHING_PROTO : HttpHandler = setStatusCode 101 >=> setBody [||]

/// **Description**
///
/// A collection of `HttpHandler` functions to return HTTP status code `2xx` responses.
///
module Successful =
    let ok x        = setStatusCode 200 >=> x
    let OK x        = ok (negotiate x)

    let created x   = setStatusCode 201 >=> x
    let CREATED x   = created (negotiate x)

    let accepted x  = setStatusCode 202 >=> x
    let ACCEPTED x  = accepted (negotiate x)

    let NO_CONTENT : HttpHandler = setStatusCode 204 >=> setBody [||]

/// **Description**
///
/// A collection of `HttpHandler` functions to return HTTP status code `4xx` responses.
///
module RequestErrors =
    let badRequest x  = setStatusCode 400 >=> x
    let BAD_REQUEST x = badRequest (negotiate x)

    /// **Description**
    ///
    /// Sends a `401 Unauthorized` HTTP status code response back to the client.
    ///
    /// Use the `unauthorized` status code handler when a user could not be authenticated by the server (either missing or wrong authentication data). By returnign a `401 Unauthorized` HTTP response the server tells the client that it must know **who** is making the request before it can return a successful response. As such the server must also include which authentiation scheme the client must use in order to successfully authenticate.
    ///
    /// **More information**
    ///
    /// - https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/WWW-Authenticate
    /// - http://stackoverflow.com/questions/3297048/403-forbidden-vs-401-unauthorized-http-responses/12675357
    ///
    /// **List of authentication schemes**
    ///
    /// - https://developer.mozilla.org/en-US/docs/Web/HTTP/Authentication#Authentication_schemes
    ///
    let unauthorized scheme realm x =
        setStatusCode 401
        >=> setHttpHeader "WWW-Authenticate" (sprintf "%s realm=\"%s\"" scheme realm)
        >=> x
    let UNAUTHORIZED scheme realm x = unauthorized scheme realm (negotiate x)

    let forbidden x                 = setStatusCode 403 >=> x
    let FORBIDDEN x                 = forbidden (negotiate x)

    let notFound x                  = setStatusCode 404 >=> x
    let NOT_FOUND x                 = notFound (negotiate x)

    let methodNotAllowed x          = setStatusCode 405 >=> x
    let METHOD_NOT_ALLOWED x        = methodNotAllowed (negotiate x)

    let notAcceptable x             = setStatusCode 406 >=> x
    let NOT_ACCEPTABLE x            = notAcceptable (negotiate x)

    let conflict x                  = setStatusCode 409 >=> x
    let CONFLICT x                  = conflict (negotiate x)

    let gone x                      = setStatusCode 410 >=> x
    let GONE x                      = gone (negotiate x)

    let unsupportedMediaType x      = setStatusCode 415 >=> x
    let UNSUPPORTED_MEDIA_TYPE x    = unsupportedMediaType (negotiate x)

    let unprocessableEntity x       = setStatusCode 422 >=> x
    let UNPROCESSABLE_ENTITY x      = unprocessableEntity (negotiate x)

    let preconditionRequired x      = setStatusCode 428 >=> x
    let PRECONDITION_REQUIRED x     = preconditionRequired (negotiate x)

    let tooManyRequests x           = setStatusCode 429 >=> x
    let TOO_MANY_REQUESTS x         = tooManyRequests (negotiate x)


/// **Description**
///
/// A collection of `HttpHandler` functions to return HTTP status code `5xx` responses.
///
module ServerErrors =
    let internalError x         = setStatusCode 500 >=> x
    let INTERNAL_ERROR x        = internalError (negotiate x)

    let notImplemented x        = setStatusCode 501 >=> x
    let NOT_IMPLEMENTED x       = notImplemented (negotiate x)

    let badGateway x            = setStatusCode 502 >=> x
    let BAD_GATEWAY x           = badGateway (negotiate x)

    let serviceUnavailable x    = setStatusCode 503 >=> x
    let SERVICE_UNAVAILABLE x   = serviceUnavailable (negotiate x)

    let gatewayTimeout x        = setStatusCode 504 >=> x
    let GATEWAY_TIMEOUT x       = gatewayTimeout (negotiate x)

    let invalidHttpVersion x    = setStatusCode 505 >=> x