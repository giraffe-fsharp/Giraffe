[<AutoOpen>]
module Giraffe.ResponseWriters

open System
open System.IO
open System.Text
open System.Buffers
open Microsoft.AspNetCore.Http
open Microsoft.Net.Http.Headers
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe.GiraffeViewEngine

// ---------------------------
// Helper functions
// ---------------------------

let private MinimumCapacity = 5000
let private MaximumCapacity = 100000
let private MaximumLifetime = TimeSpan.FromMinutes 10.0

type private StringBuilderPool =

    [<DefaultValue; ThreadStatic>]
    static val mutable private instance : StringBuilder

    [<DefaultValue; ThreadStatic>]
    static val mutable private created : DateTimeOffset

    static member Rent () =
        let lifetime = DateTimeOffset.Now - StringBuilderPool.created
        let expired  = lifetime > MaximumLifetime
        let sb       = StringBuilderPool.instance
        if not expired && sb <> null then
            StringBuilderPool.instance <- null
            sb.Clear()
        else new StringBuilder(MinimumCapacity)

    static member Return (sb: StringBuilder) =
        if sb.Capacity <= MaximumCapacity then
            StringBuilderPool.instance <- sb
            StringBuilderPool.created  <- DateTimeOffset.Now

let inline private nodeToUtf8HtmlDoc (node : XmlNode) : byte[] =
    let sb = StringBuilderPool.Rent()
    ViewBuilder.buildHtmlDocument sb node
    let chars = ArrayPool<char>.Shared.Rent sb.Length
    sb.CopyTo(0, chars, 0, sb.Length)
    let result = Encoding.UTF8.GetBytes(chars, 0, sb.Length)
    StringBuilderPool.Return sb
    ArrayPool<char>.Shared.Return chars
    result

// ---------------------------
// HttpContext extensions
// ---------------------------

type HttpContext with
    /// **Description**
    ///
    /// Writes a byte array to the body of the HTTP response and sets the HTTP `Content-Length` header accordingly.
    ///
    /// **Parameters**
    ///
    /// - `bytes`: The byte array to be send back to the client.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.WriteBytesAsync (bytes : byte[]) =
        task {
            this.SetHttpHeader HeaderNames.ContentLength bytes.Length
            do! this.Response.Body.WriteAsync(bytes, 0, bytes.Length)
            return Some this
        }

    /// **Description**
    ///
    /// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP `Content-Length` header accordingly.
    ///
    /// **Parameters**
    ///
    /// - `str`: The string value to be send back to the client.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.WriteStringAsync (str : string) =
        this.WriteBytesAsync(Encoding.UTF8.GetBytes str)

    /// **Description**
    ///
    /// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP `Content-Length` header accordingly, as well as the `Content-Type` header to `text/plain`.
    ///
    /// **Parameters**
    ///
    /// - `str`: The string value to be send back to the client.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.WriteTextAsync (str : string) =
        this.SetContentType "text/plain"
        this.WriteStringAsync str

    /// **Description**
    ///
    /// Serializes an object to JSON and writes the output to the body of the HTTP response.
    ///
    /// It also sets the HTTP `Content-Type` header to `application/json` and sets the `Content-Length` header accordingly.
    ///
    /// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type `IJsonSerializer`.
    ///
    /// **Parameters**
    ///
    /// - `dataObj`: The object to be send back to the client.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.WriteJsonAsync<'T> (dataObj : 'T) =
        this.SetContentType "application/json"
        let serializer = this.GetJsonSerializer()
        serializer.SerializeToBytes dataObj
        |> this.WriteBytesAsync

    /// **Description**
    ///
    /// Serializes an object to JSON and writes the output to the body of the HTTP response using chunked transfer encoding.
    ///
    /// It also sets the HTTP `Content-Type` header to `application/json` and sets the `Transfer-Encoding` header to `chunked`.
    ///
    /// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type `IJsonSerializer`.
    ///
    /// **Parameters**
    ///
    /// - `dataObj`: The object to be send back to the client.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.WriteJsonChunkedAsync<'T> (dataObj : 'T) =
        task {
            this.SetContentType "application/json"
            this.SetHttpHeader "Transfer-Encoding" "chunked"
            let serializer = this.GetJsonSerializer()
            do! serializer.SerializeToStreamAsync dataObj this.Response.Body
            return Some this
        }

    /// **Description**
    ///
    /// Serializes an object to XML and writes the output to the body of the HTTP response.
    ///
    /// It also sets the HTTP `Content-Type` header to `application/xml` and sets the `Content-Length` header accordingly.
    ///
    /// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type `IXmlSerializer`.
    ///
    /// **Parameters**
    ///
    /// - `dataObj`: The object to be send back to the client.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.WriteXmlAsync (dataObj : obj) =
        this.SetContentType "application/xml"
        let serializer = this.GetXmlSerializer()
        serializer.Serialize dataObj
        |> this.WriteBytesAsync

    /// **Description**
    ///
    /// Reads a HTML file from disk and writes its contents to the body of the HTTP response.
    ///
    /// It also sets the HTTP header `Content-Type` to `text/html` and sets the `Content-Length` header accordingly.
    ///
    /// **Parameters**
    ///
    /// - `filePath`: A relative or absolute file path to the HTML file.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.WriteHtmlFileAsync (filePath : string) =
        task {
            let filePath =
                match Path.IsPathRooted filePath with
                | true  -> filePath
                | false ->
                    let env = this.GetHostingEnvironment()
                    Path.Combine(env.ContentRootPath, filePath)
            this.SetContentType "text/html"
            let! html = readFileAsStringAsync filePath
            return! this.WriteStringAsync html
        }

    /// **Description**
    ///
    /// Writes a HTML string to the body of the HTTP response.
    ///
    /// It also sets the HTTP header `Content-Type` to `text/html` and sets the `Content-Length` header accordingly.
    ///
    /// **Parameters**
    ///
    /// - `html`: The HTML string to be send back to the client.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.WriteHtmlStringAsync (html : string) =
        this.SetContentType "text/html"
        this.WriteStringAsync html

    /// **Description**
    ///
    /// Compiles a `Giraffe.GiraffeViewEngine.XmlNode` object to a HTML view and writes the output to the body of the HTTP response.
    ///
    /// It also sets the HTTP header `Content-Type` to `text/html` and sets the `Content-Length` header accordingly.
    ///
    /// **Parameters**
    ///
    /// - `htmlView`: An `XmlNode` object to be send back to the client and which represents a valid HTML view.
    ///
    /// **Output**
    ///
    /// Task of `Some HttpContext` after writing to the body of the response.
    ///
    member this.WriteHtmlViewAsync (htmlView : XmlNode) =
        let bytes = nodeToUtf8HtmlDoc htmlView
        this.SetContentType "text/html"
        this.WriteBytesAsync bytes

// ---------------------------
// HttpHandler functions
// ---------------------------

/// **Description**
///
/// Writes a byte array to the body of the HTTP response and sets the HTTP `Content-Length` header accordingly.
///
/// **Parameters**
///
/// - `bytes`: The byte array to be send back to the client.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let setBody (bytes : byte array) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteBytesAsync bytes

/// **Description**
///
/// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP `Content-Length` header accordingly.
///
/// **Parameters**
///
/// - `str`: The string value to be send back to the client.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let setBodyFromString (str : string) : HttpHandler =
    let bytes = Encoding.UTF8.GetBytes str
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteBytesAsync bytes

/// **Description**
///
/// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP `Content-Length` header accordingly, as well as the `Content-Type` header to `text/plain`.
///
/// **Parameters**
///
/// - `str`: The string value to be send back to the client.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let text (str : string) : HttpHandler =
    let bytes = Encoding.UTF8.GetBytes str
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetContentType "text/plain"
        ctx.WriteBytesAsync bytes

/// **Description**
///
/// Serializes an object to JSON and writes the output to the body of the HTTP response.
///
/// It also sets the HTTP `Content-Type` header to `application/json` and sets the `Content-Length` header accordingly.
///
/// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type `IJsonSerializer`.
///
/// **Parameters**
///
/// - `dataObj`: The object to be send back to the client.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let json<'T> (dataObj : 'T) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteJsonAsync dataObj

/// **Description**
///
/// Serializes an object to JSON and writes the output to the body of the HTTP response using chunked transfer encoding.
///
/// It also sets the HTTP `Content-Type` header to `application/json` and sets the `Transfer-Encoding` header to `chunked`.
///
/// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type `IJsonSerializer`.
///
/// **Parameters**
///
/// - `dataObj`: The object to be send back to the client.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let jsonChunked<'T> (dataObj : 'T) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteJsonChunkedAsync dataObj

/// **Description**
///
/// Serializes an object to XML and writes the output to the body of the HTTP response.
///
/// It also sets the HTTP `Content-Type` header to `application/xml` and sets the `Content-Length` header accordingly.
///
/// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type `IXmlSerializer`.
///
/// **Parameters**
///
/// - `dataObj`: The object to be send back to the client.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let xml (dataObj : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteXmlAsync dataObj

/// **Description**
///
/// Reads a HTML file from disk and writes its contents to the body of the HTTP response.
///
/// It also sets the HTTP header `Content-Type` to `text/html` and sets the `Content-Length` header accordingly.
///
/// **Parameters**
///
/// - `filePath`: A relative or absolute file path to the HTML file.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let htmlFile (filePath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteHtmlFileAsync filePath

/// **Description**
///
/// Writes a HTML string to the body of the HTTP response.
///
/// It also sets the HTTP header `Content-Type` to `text/html` and sets the `Content-Length` header accordingly.
///
/// **Parameters**
///
/// - `html`: The HTML string to be send back to the client.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let htmlString (html : string) : HttpHandler =
    let bytes = Encoding.UTF8.GetBytes html
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetContentType "text/html"
        ctx.WriteBytesAsync bytes

/// **Description**
///
/// Compiles a `Giraffe.GiraffeViewEngine.XmlNode` object to a HTML view and writes the output to the body of the HTTP response.
///
/// It also sets the HTTP header `Content-Type` to `text/html` and sets the `Content-Length` header accordingly.
///
/// **Parameters**
///
/// - `htmlView`: An `XmlNode` object to be send back to the client and which represents a valid HTML view.
///
/// **Output**
///
/// A Giraffe `HttpHandler` function which can be composed into a bigger web application.
///
let htmlView (htmlView : XmlNode) : HttpHandler =
    let bytes = nodeToUtf8HtmlDoc htmlView
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.SetContentType "text/html"
        ctx.WriteBytesAsync bytes