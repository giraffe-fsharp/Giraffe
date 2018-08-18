[<AutoOpen>]
module Giraffe.Preconditional

open System
open System.Linq
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Headers
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers

type Precondition =
    | NoConditionsSpecified
    | ResourceNotModified
    | ConditionFailed
    | AllConditionsMet

type EntityTagHeaderValue with
    /// **Description**
    ///
    /// Creates an object of type `EntityTagHeaderValue`.
    ///
    /// **Parameters**
    ///
    /// - `isWeak`: The difference between a regular (strong) ETag and a weak ETag is that a matching strong ETag guarantees the file is byte-for-byte identical, whereas a matching weak ETag indicates that the content is semantically the same. So if the content of the file changes, the weak ETag should change as well.
    /// - `eTag`: The entity tag value (without quotes or the `W/` prefix).
    ///
    /// **Output**
    ///
    /// Returns an object of `EntityTagHeaderValue`.
    ///
    static member FromString (isWeak : bool) (eTag : string) =
        let eTagValue = sprintf "\"%s\"" eTag
        EntityTagHeaderValue(StringSegment(eTagValue), isWeak)

type HttpContext with
    member private this.IsHeadOrGetRequest() =
        HttpMethods.IsHead this.Request.Method || HttpMethods.IsGet this.Request.Method

    member private __.ValidateIfMatch (eTag : EntityTagHeaderValue option) (requestHeaders : RequestHeaders) =
        match  isNotNull requestHeaders.IfMatch
            && requestHeaders.IfMatch.Any() with
        | false -> NoConditionsSpecified
        | true  ->
            match eTag with
            | None      -> ConditionFailed
            | Some eTag ->
                requestHeaders.IfMatch
                |> Seq.exists (fun t -> t.Compare(eTag, true))
                |> function
                    | true  -> AllConditionsMet
                    | false -> ConditionFailed

    member private __.ValidateIfUnmodifiedSince (lastModified : DateTimeOffset option) (requestHeaders : RequestHeaders) =
        match requestHeaders.IfUnmodifiedSince.HasValue with
        | false -> NoConditionsSpecified
        | true  ->
            match lastModified with
            | None              -> AllConditionsMet
            | Some lastModified ->
                match  requestHeaders.IfUnmodifiedSince.Value > DateTimeOffset.UtcNow
                    || requestHeaders.IfUnmodifiedSince.Value >= lastModified with
                | true  -> AllConditionsMet
                | false -> ConditionFailed

    member private this.ValidateIfNoneMatch (eTag : EntityTagHeaderValue option) (requestHeaders : RequestHeaders) =
        match  isNotNull requestHeaders.IfNoneMatch
            && requestHeaders.IfNoneMatch.Any() with
        | false -> NoConditionsSpecified
        | true  ->
            match eTag with
            | None      -> AllConditionsMet
            | Some eTag ->
                requestHeaders.IfNoneMatch
                |> Seq.exists (fun t -> t.Compare(eTag, false))
                |> function
                    | false -> AllConditionsMet
                    | true  ->
                        match this.IsHeadOrGetRequest() with
                        | true  -> ResourceNotModified
                        | false -> ConditionFailed

    member private this.ValidateIfModifiedSince (lastModified : DateTimeOffset option) (requestHeaders : RequestHeaders) =
        match  requestHeaders.IfModifiedSince.HasValue
            && this.IsHeadOrGetRequest() with
        | false -> NoConditionsSpecified
        | true  ->
            match lastModified with
            | None              -> AllConditionsMet
            | Some lastModified ->
                match  requestHeaders.IfModifiedSince.Value <= DateTimeOffset.UtcNow
                    && requestHeaders.IfModifiedSince.Value < lastModified with
                | true  -> AllConditionsMet
                | false -> ResourceNotModified

    /// **Description**
    ///
    /// Validates the following conditional HTTP headers of the HTTP request:
    ///
    /// - `If-Match`
    /// - `If-None-Match`
    /// - `If-Modified-Since`
    /// - `If-Unmodified-Since`
    ///
    /// **Parameters**
    ///
    /// - `eTag`: Optional ETag. You can use the static `EntityTagHeaderValue.FromString` helper method to generate a valid `EntityTagHeaderValue` object.
    /// - `lastModified`: Optional `DateTimeOffset` object denoting the last modified date.
    ///
    /// **Output**
    ///
    /// Returns a `Precondition` union type, which can have one of the following cases:
    ///
    /// - `NoConditionsSpecified`: No validation has taken place, because the client didn't send any conditional HTTP headers.
    /// - `ConditionFailed`: At least one condition couldn't be sastisfied. It is advised to return a `412` status code back to the client (you can use the `HttpContext.PreconditionFailedResponse()` method for that purpose).
    /// - `ResourceNotModified`: The resource hasn't changed since the last visit. The server can skip processing this request and return a `304` status code back to the client (you can use the `HttpContext.NotModifiedResponse()` method for that purpose).
    /// - `AllConditionsMet`: All pre-conditions can be satisfied. The server should continue processing the request as normal.
    ///
    member this.ValidatePreconditions (eTag : EntityTagHeaderValue option) (lastModified : DateTimeOffset option) =
        // Parse headers
        let responseHeaders = this.Response.GetTypedHeaders()
        let requestHeaders  = this.Request.GetTypedHeaders()

        // Helper bind functions to chain validation functions
        let bind (result : RequestHeaders -> Precondition) =
            function
            | NoConditionsSpecified -> result requestHeaders
            | AllConditionsMet      -> result requestHeaders
            | ConditionFailed       -> ConditionFailed
            | ResourceNotModified   -> ResourceNotModified

        let ifNotSpecified (result : RequestHeaders -> Precondition) =
            function
            | NoConditionsSpecified -> result requestHeaders
            | AllConditionsMet      -> AllConditionsMet
            | ConditionFailed       -> ConditionFailed
            | ResourceNotModified   -> ResourceNotModified

        // Set ETag and Last-Modified in the response
        if eTag.IsSome         then responseHeaders.ETag         <- eTag.Value
        if lastModified.IsSome then responseHeaders.LastModified <- Nullable(lastModified.Value)

        // Validate headers in correct precedence
        // RFC: https://tools.ietf.org/html/rfc7232#section-6
        requestHeaders
        |> this.ValidateIfMatch eTag
        |> ifNotSpecified (this.ValidateIfUnmodifiedSince lastModified)
        |> bind (this.ValidateIfNoneMatch eTag)
        |> ifNotSpecified (this.ValidateIfModifiedSince lastModified)

    /// **Description**
    ///
    /// Sends a default HTTP `304 Not Modified` response to the client.
    ///
    member this.NotModifiedResponse() =
        this.SetStatusCode StatusCodes.Status304NotModified
        Some this

    /// **Description**
    ///
    /// Sends a default HTTP `412 Precondition Failed` response to the client.
    ///
    member this.PreconditionFailedResponse() =
        this.SetStatusCode StatusCodes.Status412PreconditionFailed
        Some this