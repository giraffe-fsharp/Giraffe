[<AutoOpen>]
module Giraffe.Common

open System
open System.IO

// ---------------------------
// Override the default task CE
// ---------------------------

/// Context insensitive Task CE
/// All tasks are configured with `ConfigurAwait(false)`.
let task = FSharp.Control.Tasks.ContextInsensitive.task

// ---------------------------
// Useful extension methods
// ---------------------------

type DateTime with
    /// ** Description **
    /// Converts a `DateTime` object into an RFC3339 formatted `string`.
    /// ** Specification **
    /// https://www.ietf.org/rfc/rfc3339.txt
    /// ** Output **
    /// Formatted string value.
    member this.ToHtmlString() = this.ToString("r")

type DateTimeOffset with
    /// ** Description **
    /// Converts a `DateTimeOffset` object into an RFC3339 formatted `string`.
    /// ** Specification **
    /// https://www.ietf.org/rfc/rfc3339.txt
    /// ** Output **
    /// Formatted string value.
    member this.ToHtmlString() = this.ToString("r")

// ---------------------------
// Common helper functions
// ---------------------------

/// ** Description **
/// Checks if an object is not null.
///
/// ** Parameters **
///     - `x`: The object to validate against `null`.
///
/// ** Output **
/// Returns `true` if the object is not `null` otherwise `false`.
let inline isNotNull x = not (isNull x)

/// ** Description **
/// Converts a `string` into a `string option` where `null` or an empty string will be converted to `None` and everything else to `Some string`.
///
/// ** Parameters **
///     - `str`: The string value to be converted into an option of string.
///
/// ** Output **
/// Returns `None` if the string was `null` or empty otherwise `Some string`.
let inline strOption (str : string) =
    if String.IsNullOrEmpty str then None else Some str

/// ** Description **
/// Reads a file asynchronously from the file system.
///
/// ** Parameters **
///     - `filePath`: The absolute path of the file.
///
/// ** Output **
/// Returns the string contents of the file wrapped in a Task.
let readFileAsStringAsync (filePath : string) =
    task {
        use reader = new StreamReader(filePath)
        return! reader.ReadToEndAsync()
    }