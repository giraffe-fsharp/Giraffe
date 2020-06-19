[<AutoOpen>]
module Giraffe.Common

open System
open System.IO
open FSharp.Control.Tasks.Builders

// ---------------------------
// Useful extension methods
// ---------------------------

type DateTime with

    /// <summary>
    /// Converts a <see cref="System.DateTime" /> object into an RFC822 formatted <see cref="System.String" />.
    /// </summary>
    /// <remarks>Using specification https://www.ietf.org/rfc/rfc822.txt</remarks>
    /// 
    ///
    /// <returns>Formatted string value.</returns>
    member this.ToHtmlString() = this.ToString("r")

    /// <summary>
    /// Converts a <see cref="System.DateTime" /> object into an RFC3339 formatted <see cref="System.String" />.
    /// </summary>
    /// <remarks>Using specification https://www.ietf.org/rfc/rfc3339.txt</remarks>
    /// <returns>Formatted string value.</returns>
    member this.ToIsoString() = this.ToString("o")

type DateTimeOffset with
    /// <summary>
    /// Converts a <see cref="System.DateTimeOffset" /> object into an RFC822 formatted <see cref="System.String" />.
    /// </summary>
    /// <remarks>Using specification https://www.ietf.org/rfc/rfc822.txt</remarks>
    /// <returns>Formatted string value.</returns>
    member this.ToHtmlString() = this.ToString("r")

    /// <summary>
    /// Converts a <see cref="System.DateTimeOffset" /> object into an RFC3339 formatted <see cref="System.String" />.
    /// </summary>
    /// <remarks>Using specification https://www.ietf.org/rfc/rfc3339.txt</remarks>
    /// <returns>Formatted string value.</returns>
    member this.ToIsoString() = this.ToString("o")

// ---------------------------
// Common helper functions
// ---------------------------

/// <summary>
/// Checks if an object is not null.
/// </summary>
/// <param name="x">The object to validate against `null`.</param>
/// <returns>Returns true if the object is not null otherwise false.</returns>
let inline isNotNull x = not (isNull x)

/// <summary>
/// Converts a string into a string option where null or an empty string will be converted to None and everything else to Some string.
/// </summary>
/// <param name="str">The string value to be converted into an option of string.</param>
/// <returns>Returns None if the string was null or empty otherwise Some string.</returns>
let inline strOption (str : string) =
    if String.IsNullOrEmpty str then None else Some str

/// <summary>
/// Reads a file asynchronously from the file system.
/// </summary>
/// <param name="filePath">The absolute path of the file.</param>
/// <returns>Returns the string contents of the file wrapped in a Task.</returns>
let readFileAsStringAsync (filePath : string) =
    task {
        use reader = new StreamReader(filePath)
        return! reader.ReadToEndAsync()
    }

// ---------------------------
// Short GUIDs and IDs
// ---------------------------

/// <summary>
/// Short GUIDs are a shorter, URL-friendlier version
/// of the traditional <see cref="System.Guid" /> type.
///
/// Short GUIDs are always 22 characters long, which let's
/// one save a total of 10 characters in comparison to using
/// a normal <see cref="System.Guid" /> as identifier.
///
/// Additionally a Short GUID is by default a URL encoded
/// string which doesn't need extra character replacing
/// before using of it in a URL query parameter.
///
/// All Short GUID strings map directly to a <see cref="System.Guid" />
/// object and the `ShortGuid` module can be used to convert
/// a <see cref="System.Guid" /> into a short GUID <see cref="System.String" /> and vice versa.
///
/// For more information please check:
/// https://madskristensen.net/blog/A-shorter-and-URL-friendly-GUID
/// </summary>
[<RequireQualifiedAccess>]
module ShortGuid =
 
    /// <summary>
    /// Converts a <see cref="System.Guid" /> into a 22 character long
    /// short GUID string.
    /// </summary>
    /// <param name="guid">The <see cref="System.Guid" />  to be converted into a short GUID.</param>
    /// <returns>Returns a 22 character long URL encoded short GUID string.</returns>
    let fromGuid (guid : Guid) =
        guid.ToByteArray()
        |> Convert.ToBase64String
        |> (fun str ->
            str.Replace("/", "_")
               .Replace("+", "-")
               .Substring(0, 22))
    
    /// <summary>
    /// Converts a 22 character short GUID string into the matching <see cref="System.Guid" />.
    /// </summary>
    /// <param name="shortGuid">The short GUID string to be converted into a <see cref="System.Guid" />.</param>
    /// <returns>Returns a <see cref="System.Guid" /> object.</returns>
    let toGuid (shortGuid : string) =
        shortGuid.Replace("_", "/")
                 .Replace("-", "+")
        |> (fun str -> str + "==")
        |> Convert.FromBase64String
        |> Guid

/// <summary>
/// Short IDs are a shorter, URL-friendlier version
/// of an unsigned 64-bit integer value (`uint64` in F# and `ulong` in C#).
///
/// Short IDs are always 11 characters long, which let's
/// one save a total of 9 characters in comparison to using
/// a normal `uint64` value as identifier.
///
/// Additionally a Short ID is by default a URL encoded
/// string which doesn't need extra character replacing
/// before using it in a URL query parameter.
///
/// All Short ID strings map directly to a `uint64` object
/// and the `ShortId` module can be used to convert an
/// `uint64` value into a short ID `string` and vice versa.
///
/// For more information please check:
/// https://webapps.stackexchange.com/questions/54443/format-for-id-of-youtube-video
/// </summary>
[<RequireQualifiedAccess>]
module ShortId =

    /// <summary>
    /// Converts a uint64 value into a 11 character long
    /// short ID string.
    /// </summary>
    /// <param name="id">The uint64 to be converted into a short ID.</param>
    /// <returns>Returns a 11 character long URL encoded short ID string.</returns>
    let fromUInt64 (id : uint64) =
        BitConverter.GetBytes id
        |> (fun arr ->
            match BitConverter.IsLittleEndian with
            | true  -> Array.Reverse arr; arr
            | false -> arr)
        |> Convert.ToBase64String
        |> (fun str ->
            str.Remove(11, 1)
               .Replace("/", "_")
               .Replace("+", "-"))

    /// <summary>
    /// Converts a 11 character short ID string into the matching uint64 value.
    /// </summary>
    /// <param name="shortId">The short ID string to be converted into a uint64 value.</param>
    /// <returns>The short ID string to be converted into a uint64 value.</returns>
    let toUInt64 (shortId : string) =
        let bytes =
            shortId.Replace("_", "/")
                   .Replace("-", "+")
            |> (fun str -> str + "=")
            |> Convert.FromBase64String
            |> (fun arr ->
                match BitConverter.IsLittleEndian with
                | true  -> Array.Reverse arr; arr
                | false -> arr)
        BitConverter.ToUInt64 (bytes, 0)