namespace Giraffe

[<AutoOpen>]
module Helpers =
    open System
    open System.IO
    open Microsoft.IO

    /// <summary>Default single RecyclableMemoryStreamManager.</summary>
    let internal recyclableMemoryStreamManager = Lazy<RecyclableMemoryStreamManager>()

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

    /// <summary>
    /// Utility function for matching 1xx HTTP status codes.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>Returns true if the status code is between 100 and 199.</returns>
    let is1xxStatusCode (statusCode : int) =
        100 <= statusCode && statusCode <= 199

    /// <summary>
    /// Utility function for matching 2xx HTTP status codes.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>Returns true if the status code is between 200 and 299.</returns>
    let is2xxStatusCode (statusCode : int) =
        200 <= statusCode && statusCode <= 299

    /// <summary>
    /// Utility function for matching 3xx HTTP status codes.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>Returns true if the status code is between 300 and 399.</returns>
    let is3xxStatusCode (statusCode : int) =
        300 <= statusCode && statusCode <= 399

    /// <summary>
    /// Utility function for matching 4xx HTTP status codes.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>Returns true if the status code is between 400 and 499.</returns>
    let is4xxStatusCode (statusCode : int) =
        400 <= statusCode && statusCode <= 499

    /// <summary>
    /// Utility function for matching 5xx HTTP status codes.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>Returns true if the status code is between 500 and 599.</returns>
    let is5xxStatusCode (statusCode : int) =
        500 <= statusCode && statusCode <= 599

