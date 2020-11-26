[<AutoOpen>]
module Giraffe.ResponseWriters

open System.IO
open System.Text
open Microsoft.AspNetCore.Http
open Microsoft.Net.Http.Headers
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe.ViewEngine

// ---------------------------
// HttpContext extensions
// ---------------------------

type HttpContext with
    /// <summary>
    /// Writes a byte array to the body of the HTTP response and sets the HTTP Content-Length header accordingly.
    /// </summary>
    /// <param name="bytes">The byte array to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    member this.WriteBytesAsync (bytes : byte[]) =
        task {
            this.SetHttpHeader HeaderNames.ContentLength bytes.Length
            if this.Request.Method <> HttpMethods.Head then
                do! this.Response.Body.WriteAsync(bytes, 0, bytes.Length)
            return Some this
        }

    /// <summary>
    /// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP Content-Length header accordingly.
    /// </summary>
    /// <param name="str">The string value to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    member this.WriteStringAsync (str : string) =
        this.WriteBytesAsync(Encoding.UTF8.GetBytes str)

    /// <summary>
    /// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP `Content-Length` header accordingly, as well as the `Content-Type` header to `text/plain`.
    /// </summary>
    /// <param name="str">The string value to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    member this.WriteTextAsync (str : string) =
        this.SetContentType "text/plain; charset=utf-8"
        this.WriteStringAsync str

    /// <summary>
    /// Serializes an object to JSON and writes the output to the body of the HTTP response.
    /// It also sets the HTTP Content-Type header to application/json and sets the Content-Length header accordingly.
    /// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type <see cref="IJsonSerializer"/>
    /// </summary>
    /// <param name="dataObj">The object to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    member this.WriteJsonAsync<'T> (dataObj : 'T) =
        this.SetContentType "application/json; charset=utf-8"
        let serializer = this.GetJsonSerializer()
        serializer.SerializeToBytes dataObj
        |> this.WriteBytesAsync

    /// <summary>
    /// Serializes an object to JSON and writes the output to the body of the HTTP response using chunked transfer encoding.
    /// It also sets the HTTP Content-Type header to application/json and sets the Transfer-Encoding header to chunked.
    /// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type <see cref="IJsonSerializer"/>.
    /// </summary>
    /// <param name="dataObj">The object to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    member this.WriteJsonChunkedAsync<'T> (dataObj : 'T) =
        task {
            // Don't set the Transfer-Encoding to chunked manually.  If we do, we'll have to do the chunking manually
            // ourselves rather than rely on asp.net to do it for us.
            // Example : https://github.com/aspnet/AspNetCore/blame/728110ec9ee1b98b2d9c9ff247ba2955d6c05846/src/Servers/Kestrel/test/InMemory.FunctionalTests/ChunkedResponseTests.cs#L494
            this.SetContentType "application/json; charset=utf-8"
            if this.Request.Method <> HttpMethods.Head then
                let serializer = this.GetJsonSerializer()
                do! serializer.SerializeToStreamAsync dataObj this.Response.Body
            return Some this
        }

    /// <summary>
    /// Serializes an object to XML and writes the output to the body of the HTTP response.
    /// It also sets the HTTP Content-Type header to application/xml and sets the Content-Length header accordingly.
    /// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type <see cref="IXmlSerializer"/>.
    /// </summary>
    /// <param name="dataObj">The object to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    member this.WriteXmlAsync (dataObj : obj) =
        this.SetContentType "application/xml; charset=utf-8"
        let serializer = this.GetXmlSerializer()
        serializer.Serialize dataObj
        |> this.WriteBytesAsync

    /// <summary>
    /// Reads a HTML file from disk and writes its contents to the body of the HTTP response.
    /// It also sets the HTTP header Content-Type to text/html and sets the Content-Length header accordingly.
    /// </summary>
    /// <param name="filePath">A relative or absolute file path to the HTML file.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    member this.WriteHtmlFileAsync (filePath : string) =
        task {
            let filePath =
                match Path.IsPathRooted filePath with
                | true  -> filePath
                | false ->
                    let env = this.GetHostingEnvironment()
                    Path.Combine(env.ContentRootPath, filePath)
            this.SetContentType "text/html; charset=utf-8"
            let! html = readFileAsStringAsync filePath
            return! this.WriteStringAsync html
        }

    /// <summary>
    /// Writes a HTML string to the body of the HTTP response.
    /// It also sets the HTTP header Content-Type to text/html and sets the Content-Length header accordingly.
    /// </summary>
    /// <param name="html">The HTML string to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    member this.WriteHtmlStringAsync (html : string) =
        this.SetContentType "text/html; charset=utf-8"
        this.WriteStringAsync html

    /// <summary>
    /// <para>Compiles a `Giraffe.GiraffeViewEngine.XmlNode` object to a HTML view and writes the output to the body of the HTTP response.</para>
    /// <para>It also sets the HTTP header `Content-Type` to `text/html` and sets the `Content-Length` header accordingly.</para>
    /// <param name="htmlView">An `XmlNode` object to be send back to the client and which represents a valid HTML view.</param>
    /// <returns>Task of `Some HttpContext` after writing to the body of the response.</returns>
    member this.WriteHtmlViewAsync (htmlView : XmlNode) =
        let bytes = RenderView.AsBytes.htmlDocument htmlView
        this.SetContentType "text/html; charset=utf-8"
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
/// `bytes`: The byte array to be send back to the client.
///
/// **Output**
///
/// A Giraffe <see cref="HttpHandler" /> function which can be composed into a bigger web application.

/// <summary>
/// Writes a byte array to the body of the HTTP response and sets the HTTP Content-Length header accordingly.
/// </summary>
/// <param name="bytes">The byte array to be send back to the client.</param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler" /> function which can be composed into a bigger web application.</returns>
let setBody (bytes : byte array) : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteBytesAsync bytes

/// <summary>
/// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP Content-Length header accordingly.
/// </summary>
/// <param name="str">The string value to be send back to the client.</param>
/// <returns>A Giraffe <see cref="HttpHandler" /> function which can be composed into a bigger web application.</returns>
let setBodyFromString (str : string) : HttpHandler =
    let bytes = Encoding.UTF8.GetBytes str
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteBytesAsync bytes

/// <summary>
/// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP Content-Length header accordingly, as well as the Content-Type header to text/plain.
/// </summary>
/// <param name="str">The string value to be send back to the client.</param>
/// <returns>A Giraffe <see cref="HttpHandler" /> function which can be composed into a bigger web application.</returns>
let text (str : string) : HttpHandler =
    let bytes = Encoding.UTF8.GetBytes str
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.SetContentType "text/plain; charset=utf-8"
        ctx.WriteBytesAsync bytes

/// <summary>
/// Serializes an object to JSON and writes the output to the body of the HTTP response.
/// It also sets the HTTP Content-Type header to application/json and sets the Content-Length header accordingly.
/// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type <see cref="IJsonSerializer"/>.
/// </summary>
/// <param name="dataObj">The object to be send back to the client.</param>
/// <param name="ctx"></param>
/// <typeparam name="'T"></typeparam>
/// <returns>A Giraffe <see cref="HttpHandler" /> function which can be composed into a bigger web application.</returns>
let json<'T> (dataObj : 'T) : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteJsonAsync dataObj

/// <summary>
/// Serializes an object to JSON and writes the output to the body of the HTTP response using chunked transfer encoding.
/// It also sets the HTTP Content-Type header to application/json and sets the Transfer-Encoding header to chunked.
/// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type <see cref="IJsonSerializer"/>.
/// </summary>
/// <param name="dataObj">The object to be send back to the client.</param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler" /> function which can be composed into a bigger web application.</returns>
let jsonChunked<'T> (dataObj : 'T) : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteJsonChunkedAsync dataObj

/// <summary>
/// Serializes an object to XML and writes the output to the body of the HTTP response.
/// It also sets the HTTP Content-Type header to application/xml and sets the Content-Length header accordingly.
/// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type <see cref="IXmlSerializer"/>.
/// </summary>
/// <param name="dataObj">The object to be send back to the client.</param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler" /> function which can be composed into a bigger web application.</returns>
let xml (dataObj : obj) : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteXmlAsync dataObj

/// <summary>
/// Reads a HTML file from disk and writes its contents to the body of the HTTP response.
/// It also sets the HTTP header Content-Type to text/html and sets the Content-Length header accordingly.
/// </summary>
/// <param name="filePath">A relative or absolute file path to the HTML file.</param>
/// <param name="ctx"></param>
/// <returns>A Giraffe <see cref="HttpHandler" /> function which can be composed into a bigger web application.</returns>
let htmlFile (filePath : string) : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteHtmlFileAsync filePath

/// <summary>
/// Writes a HTML string to the body of the HTTP response.
/// It also sets the HTTP header Content-Type to text/html and sets the Content-Length header accordingly.
/// </summary>
/// <param name="html">The HTML string to be send back to the client.</param>
/// <returns>A Giraffe <see cref="HttpHandler" /> function which can be composed into a bigger web application.</returns>
let htmlString (html : string) : HttpHandler =
    let bytes = Encoding.UTF8.GetBytes html
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.SetContentType "text/html; charset=utf-8"
        ctx.WriteBytesAsync bytes

/// <summary>
/// <para>Compiles a `Giraffe.GiraffeViewEngine.XmlNode` object to a HTML view and writes the output to the body of the HTTP response.</para>
/// <para>It also sets the HTTP header `Content-Type` to `text/html` and sets the `Content-Length` header accordingly.</para>
/// <param name="htmlView">An `XmlNode` object to be send back to the client and which represents a valid HTML view.</param>
/// <returns>A Giraffe `HttpHandler` function which can be composed into a bigger web application.</returns>
let htmlView (htmlView : XmlNode) : HttpHandler =
    let bytes = RenderView.AsBytes.htmlDocument htmlView
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.SetContentType "text/html; charset=utf-8"
        ctx.WriteBytesAsync bytes