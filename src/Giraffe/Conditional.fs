[<AutoOpen>]
module Giraffe.Conditional

open System
open System.Linq
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Headers
open Microsoft.Net.Http.Headers
open Microsoft.Extensions.Primitives
open Giraffe.Common

type PreConditionResult =
    | NotSpecified
    | NotModified     // 304 Not Modifed
    | ConditionFailed // 412 Precondition Failed
    | IsMatch         // Proceed 2xx

type EntityTagHeaderValue with
    member __.FromString (isWeak : bool) (eTag : string) =
        EntityTagHeaderValue(StringSegment(eTag), isWeak)

type HttpContext with

    member private __.ValidateIfMatch (requestHeaders : RequestHeaders) (eTag : EntityTagHeaderValue option) =
        match  isNotNull requestHeaders.IfMatch
            && requestHeaders.IfMatch.Any() with
        | false -> NotSpecified
        | true  ->
            match eTag with
            | None      -> ConditionFailed
            | Some eTag ->
                requestHeaders.IfMatch
                |> Seq.exists (fun t -> t.Compare(eTag, true))
                |> function
                    | true  -> IsMatch
                    | false -> ConditionFailed

    member private __.ValidateIfUnmodifiedSince (requestHeaders : RequestHeaders) (lastModified : DateTimeOffset option) =
        match requestHeaders.IfUnmodifiedSince.HasValue with
        | false -> NotSpecified
        | true  ->
            match lastModified with
            | None              -> IsMatch
            | Some lastModified ->
                match  requestHeaders.IfUnmodifiedSince.Value >= DateTimeOffset.UtcNow
                    && requestHeaders.IfUnmodifiedSince.Value >= lastModified with
                | true  -> ConditionFailed
                | false -> IsMatch

    member private this.ValidateIfNoneMatch (requestHeaders : RequestHeaders) (eTag : EntityTagHeaderValue option) =
        match  isNotNull requestHeaders.IfNoneMatch
            && requestHeaders.IfNoneMatch.Any() with
        | false -> NotSpecified
        | true  ->
            match eTag with
            | None      -> IsMatch
            | Some eTag ->
                requestHeaders.IfNoneMatch
                |> Seq.exists (fun t -> t.Compare(eTag, false))
                |> function
                    | false -> IsMatch
                    | true  ->
                        match  HttpMethods.IsHead this.Request.Method
                            || HttpMethods.IsGet this.Request.Method with
                        | true  -> NotModified
                        | false -> ConditionFailed

    member private this.ValidateIfModifiedSince (requestHeaders : RequestHeaders) (lastModified : DateTimeOffset option) =
        match requestHeaders.IfModifiedSince.HasValue with
        | false -> NotSpecified
        | true  ->
            match lastModified with
            | None              -> IsMatch
            | Some lastModified ->
                match  requestHeaders.IfModifiedSince.Value <= DateTimeOffset.UtcNow
                    && requestHeaders.IfModifiedSince.Value < lastModified
                    && (HttpMethods.IsHead this.Request.Method
                    || HttpMethods.IsGet this.Request.Method) with
                | true  -> NotModified
                | false -> IsMatch



    member this.ValidatePreConditions (eTag : EntityTagHeaderValue option) (lastModified : DateTimeOffset option) =
        let bind (result : PreConditionResult) =
            function
            | NotModified     -> NotModified
            | ConditionFailed -> ConditionFailed
            | IsMatch         -> result
            | NotSpecified    -> result

        // Parse headers
        let responseHeaders = this.Response.GetTypedHeaders()
        let requestHeaders  = this.Request.GetTypedHeaders()

        // Set ETag and Last-Modified in the response
        if eTag.IsSome         then responseHeaders.ETag         <- eTag.Value
        if lastModified.IsNone then responseHeaders.LastModified <- Nullable(lastModified.Value)

        // Validate headers in correct precedence
        // RFC: https://tools.ietf.org/html/rfc7232#section-6
        this.ValidateIfMatch requestHeaders eTag
        |> function
            | NotSpecified ->
                // Only validate If-Unmodified-Since when the If-Match was not set
                this.ValidateIfUnmodifiedSince requestHeaders lastModified
                |> bind (this.ValidateIfNoneMatch requestHeaders eTag)
                |> function
                    // If the If-None-Match is true skip the If-Modified-Since validation
                    | IsMatch -> IsMatch  // Go to If-Range
                    | result ->
                        result
                        |> bind (this.ValidateIfModifiedSince requestHeaders lastModified)
            | result ->
                result
                |> bind (this.ValidateIfNoneMatch requestHeaders eTag)
                |> function
                    // If the If-None-Match is true skip the If-Modified-Since validation
                    | IsMatch -> IsMatch // Go to If-Range
                    | result ->
                        result
                        |> bind (this.ValidateIfModifiedSince requestHeaders lastModified)