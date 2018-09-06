[<AutoOpen>]
module Giraffe.ResponseWriters

open System
open System.IO
open System.Text
open Microsoft.AspNetCore.Http
open Microsoft.Net.Http.Headers
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe.GiraffeViewEngine
open System.Buffers

// ---------------------------
// Internal implementation of string builder caching
// ---------------------------

module private Caching =

    let DefaultCapacity = 8 * 1024
    let MaxBuilderSize = DefaultCapacity * 8

    /// Holds an instance of StringBuilder of maximum capacity per thread.
    /// For `StringBuilder` of larger than `MaxBuilderSize` will behave as `new StringBuilder()` constructor call
    type StringBuilderCache =

        [<ThreadStatic>]
        [<DefaultValue>]
        static val mutable private instance: StringBuilder

        static member Get() : StringBuilder =
            let ms = StringBuilderCache.instance;

            if ms <> null && DefaultCapacity <= ms.Capacity then
                StringBuilderCache.instance <- null;
                ms.Clear()
            else
                new StringBuilder(DefaultCapacity)

        static member Release(ms:StringBuilder) : unit =
            if ms.Capacity <= MaxBuilderSize then
                StringBuilderCache.instance <- ms

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
    member this.WriteJsonAsync (dataObj : obj) =
        this.SetContentType "application/json"
        let serializer = this.GetJsonSerializer()
        serializer.Serialize dataObj
        |> this.WriteStringAsync

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

        /// renders html document to cached string builder instance
        /// and converts it to the utf8 byte array
        let inline render (htmlView: XmlNode): byte[] =
            let sb = Caching.StringBuilderCache.Get()
            buildHtmlDocument sb htmlView |> ignore
            let chars = ArrayPool<char>.Shared.Rent(sb.Length)
            sb.CopyTo(0, chars, 0, sb.Length)
            let result = Encoding.UTF8.GetBytes(chars, 0, sb.Length)
            Caching.StringBuilderCache.Release sb
            ArrayPool<char>.Shared.Return chars
            result

        this.SetContentType "text/html"
        this.WriteBytesAsync <| render htmlView

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
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteStringAsync str

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
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteTextAsync str

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
let json (dataObj : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteJsonAsync dataObj

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
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteHtmlStringAsync html

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
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteHtmlViewAsync htmlView