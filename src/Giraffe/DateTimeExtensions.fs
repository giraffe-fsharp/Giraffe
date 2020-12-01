namespace Giraffe

open System
open System.Runtime.CompilerServices

[<Extension>]
type DateTimeExtensions() =

    /// <summary>
    /// Converts a <see cref="System.DateTime" /> object into an RFC822 formatted <see cref="System.String" />.
    /// </summary>
    /// <remarks>Using specification https://www.ietf.org/rfc/rfc822.txt</remarks>
    /// <returns>Formatted string value.</returns>
    [<Extension>]
    static member inline ToHtmlString(dt : DateTime) = dt.ToString("r")

    /// <summary>
    /// Converts a <see cref="System.DateTime" /> object into an RFC3339 formatted <see cref="System.String" />.
    /// </summary>
    /// <remarks>Using specification https://www.ietf.org/rfc/rfc3339.txt</remarks>
    /// <returns>Formatted string value.</returns>
    [<Extension>]
    static member inline ToIsoString(dt : DateTime) = dt.ToString("o")

    /// <summary>
    /// Converts a <see cref="System.DateTimeOffset" /> object into an RFC822 formatted <see cref="System.String" />.
    /// </summary>
    /// <remarks>Using specification https://www.ietf.org/rfc/rfc822.txt</remarks>
    /// <returns>Formatted string value.</returns>
    [<Extension>]
    static member inline ToHtmlString(dt : DateTimeOffset) = dt.ToString("r")

    /// <summary>
    /// Converts a <see cref="System.DateTimeOffset" /> object into an RFC3339 formatted <see cref="System.String" />.
    /// </summary>
    /// <remarks>Using specification https://www.ietf.org/rfc/rfc3339.txt</remarks>
    /// <returns>Formatted string value.</returns>
    [<Extension>]
    static member inline ToIsoString(dt : DateTimeOffset) = dt.ToString("o")

    [<Extension>]
    static member inline CutOffMs(dt : DateTimeOffset) =
        DateTimeOffset(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, 0, dt.Offset)