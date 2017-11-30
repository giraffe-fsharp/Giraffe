namespace Giraffe
open HttpHandlers

module Intermediate =
    let CONTINUE next ctx  = (setStatusCode 100 >=> setBody [||]) next ctx
    
    let SWITCHING_PROTO next ctx = (setStatusCode 101 >=> setBody [||]) next ctx


// 2xx
module Successful =

    let ok s = setStatusCode 200 >=> s

    let OK s = ok (text s)

    let created s = setStatusCode 201 >=> s

    let CREATED s = created (text s)

    let accepted s = setStatusCode 202 >=> s

    let ACCEPTED s = accepted (text s)

// 3xx
module Redirection =


    let moved_permanently location =
        setStatusCode 301 
        >=> setHttpHeader "Location" location
        >=> setBody [||]
    
    let MOVED_PERMANENTLY location = moved_permanently location

    let found location =
        setStatusCode 302
        >=> setHttpHeader "Location" location
        >=> setBody [||]

    let FOUND location = found location

    let redirect url =
        let str = sprintf """<html><body><a href=\"%s\">%s</a></body></html>""" url "Object moved temporarily -- see URI list"

        setStatusCode 302
        >=> setHttpHeader "Location" url
        >=> setHttpHeader "Content-Type" "text/html; charset=utf-8"
        >=> setBodyAsString str

// 4xx
module RequestErrors = 

    let bad_request s = setStatusCode 400 >=> s

    let BAD_REQUEST message = bad_request (text message)


    /// 401: see http://stackoverflow.com/questions/3297048/403-forbidden-vs-401-unauthorized-http-responses/12675357
    let unauthorized s =
        setStatusCode 401 
        >=> setHttpHeader "WWW-Authenticate" "Basic realm=\"protected\"" 
        >=> s

    let UNAUTHORIZED message = unauthorized (text message)
 
    let forbidden s = setStatusCode 403 >=> s

    let FORBIDDEN message = forbidden (text message)
    let not_found s = setStatusCode 404 >=> s

    let NOT_FOUND message = not_found (text message)


    let method_not_allowed s = setStatusCode 405 >=> s

    let METHOD_NOT_ALLOWED s = method_not_allowed (text s)

    let not_acceptable s = setStatusCode 406 >=> s

    let NOT_ACCEPTABLE message = not_acceptable (text message)

    let conflict s = setStatusCode 409 >=> s

    let CONFLICT message = conflict (text message)

    let gone s = setStatusCode 410 >=> s

    let GONE s = gone (text s)

    let unsupported_media_type s = setStatusCode 415 >=> s

    let UNSUPPORTED_MEDIA_TYPE s = unsupported_media_type (text s)

    let unprocessable_entity s = setStatusCode 422 >=> s

    let UNPROCESSABLE_ENTITY s = unprocessable_entity (text s)

    let precondition_required s = setStatusCode 428 >=> s

    let PRECONDITION_REQUIRED s = precondition_required (text s)

    let too_many_requests s = setStatusCode 429 >=> s

    let TOO_MANY_REQUESTS s = too_many_requests (text s)    

// 5xx
module ServerErrors =

    let internal_error s = setStatusCode 500 >=> s

    let INTERNAL_ERROR message = internal_error (text message)

    let not_implemented s = setStatusCode 501 >=> s

    let NOT_IMPLEMENTED message = not_implemented (text message)

    let bad_gateway s = setStatusCode 502 >=> s

    let BAD_GATEWAY message = bad_gateway (text message)
    
    let service_unavailable s = setStatusCode 503 >=> s

    let SERVICE_UNAVAILABLE message = service_unavailable (text message)

    let gateway_timeout s = setStatusCode 504 >=> s

    let GATEWAY_TIMEOUT message = gateway_timeout (text message)

    let invalid_http_version s = setStatusCode 505 >=> s
