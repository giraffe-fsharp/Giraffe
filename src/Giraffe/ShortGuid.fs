namespace Giraffe

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
    open System

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
    open System

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