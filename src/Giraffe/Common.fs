[<AutoOpen>]
module Giraffe.Common

open System
open System.IO
open FSharp.Control.Tasks.V2.ContextInsensitive

// ---------------------------
// Useful extension methods
// ---------------------------

type DateTime with
    /// **Description**
    ///
    /// Converts a `DateTime` object into an RFC3339 formatted `string`.
    ///
    /// **Specification**
    ///
    /// https://www.ietf.org/rfc/rfc3339.txt
    ///
    /// **Output**
    ///
    /// Formatted string value.
    ///
    member this.ToHtmlString() = this.ToString("r")

type DateTimeOffset with
    /// **Description**
    ///
    /// Converts a `DateTimeOffset` object into an RFC3339 formatted `string`.
    ///
    /// **Specification**
    ///
    /// https://www.ietf.org/rfc/rfc3339.txt
    ///
    /// **Output**
    ///
    /// Formatted string value.
    ///
    member this.ToHtmlString() = this.ToString("r")

// ---------------------------
// Common helper functions
// ---------------------------

/// **Description**
///
/// Checks if an object is not null.
///
/// **Parameters**
///
/// - `x`: The object to validate against `null`.
///
/// **Output**
///
/// Returns `true` if the object is not `null` otherwise `false`.
///
let inline isNotNull x = not (isNull x)

/// **Description**
///
/// Converts a `string` into a `string option` where `null` or an empty string will be converted to `None` and everything else to `Some string`.
///
/// **Parameters**
///
/// - `str`: The string value to be converted into an option of string.
///
/// **Output**
///
/// Returns `None` if the string was `null` or empty otherwise `Some string`.
///
let inline strOption (str : string) =
    if String.IsNullOrEmpty str then None else Some str

/// **Description**
///
/// Reads a file asynchronously from the file system.
///
/// **Parameters**
///
/// - `filePath`: The absolute path of the file.
///
/// **Output**
///
/// Returns the string contents of the file wrapped in a Task.
///
let readFileAsStringAsync (filePath : string) =
    task {
        use reader = new StreamReader(filePath)
        return! reader.ReadToEndAsync()
    }

// ---------------------------
// Short GUIDs and IDs
// ---------------------------

/// **Description**
///
/// Short GUIDs are a shorter, URL-friendlier version
/// of the traditional `System.Guid` type.
///
/// Short GUIDs are always 22 characters long, which let's
/// one save a total of 10 characters in comparison to using
/// a normal `System.Guid` as identifier.
///
/// Additionally a Short GUID is by default a URL encoded
/// string which doesn't need extra character replacing
/// before using of it in a URL query parameter.
///
/// All Short GUID strings map directly to a `System.Guid`
/// objet and the `ShortGuid` module can be used to convert
/// a `System.Guid` into a short GUID `string` and vice versa.
///
/// For more information please check:
/// https://madskristensen.net/blog/A-shorter-and-URL-friendly-GUID
///
[<RequireQualifiedAccess>]
module ShortGuid =

    /// **Description**
    ///
    /// Converts a `System.Guid` into a 22 character long
    /// short GUID string.
    ///
    /// **Parameters**
    ///
    /// - `guid`: The `System.Guid` to be converted into a short GUID.
    ///
    /// **Output**
    ///
    /// Returns a 22 character long URL encoded short GUID string.
    ///
    let fromGuid (guid : Guid) =
        guid.ToByteArray()
        |> Convert.ToBase64String
        |> (fun str ->
            str.Replace("/", "_")
               .Replace("+", "-")
               .Substring(0, 22))

    /// **Description**
    ///
    /// Converts a 22 character short GUID string into the matching `System.Guid`.
    ///
    /// **Parameters**
    ///
    /// - `shortGuid`: The short GUID string to be converted into a `System.Guid`.
    ///
    /// **Output**
    ///
    /// Returns a `System.Guid` object.
    ///
    let toGuid (shortGuid : string) =
        shortGuid.Replace("_", "/")
                 .Replace("-", "+")
        |> (fun str -> str + "==")
        |> Convert.FromBase64String
        |> Guid

/// **Description**
///
/// Short IDs are a shorter, URL-friendlier version
/// of an unisgned 64-bit integer value (`uint64` in F# and `ulong` in C#).
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
/// `unint64` value into a short ID `string` and vice versa.
///
/// For more information please check:
/// https://webapps.stackexchange.com/questions/54443/format-for-id-of-youtube-video
///
[<RequireQualifiedAccess>]
module ShortId =

    /// **Description**
    ///
    /// Converts a `uint64` value into a 11 character long
    /// short ID string.
    ///
    /// **Parameters**
    ///
    /// - `id`: The `uint64` to be converted into a short ID.
    ///
    /// **Output**
    ///
    /// Returns a 11 character long URL encoded short ID string.
    ///
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

    /// **Description**
    ///
    /// Converts a 11 character short ID string into the matching `uint64` value.
    ///
    /// **Parameters**
    ///
    /// - `shortId`: The short ID string to be converted into a `uint64` value.
    ///
    /// **Output**
    ///
    /// Returns a `uint64` value.
    ///
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