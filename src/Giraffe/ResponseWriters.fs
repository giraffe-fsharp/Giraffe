[<AutoOpen>]
module Giraffe.ResponseWriters

open System.IO
open System.Text
open Microsoft.AspNetCore.Http
open Microsoft.Net.Http.Headers
open Giraffe.GiraffeViewEngine

// ---------------------------
// HttpContext extensions
// ---------------------------

type HttpContext with

    member this.WriteBytesAsync (bytes : byte[]) =
        task {
            this.SetHttpHeader HeaderNames.ContentLength bytes.Length
            do! this.Response.Body.WriteAsync(bytes, 0, bytes.Length)
            return Some this
        }

    member this.WriteStringAsync (value : string) =
        this.WriteBytesAsync(Encoding.UTF8.GetBytes value)

    member this.WriteTextAsync (value : string) =
        this.SetContentType "text/plain"
        this.WriteStringAsync value

    member this.WriteJsonAsync (value : obj) =
        this.SetContentType "application/json"
        let serializer = this.GetJsonSerializer()
        serializer.Serialize value
        |> this.WriteStringAsync

    member this.WriteXmlAsync (value : obj) =
        this.SetContentType "application/xml"
        let serializer = this.GetXmlSerializer()
        serializer.Serialize value
        |> this.WriteBytesAsync

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

    member this.WriteHtmlStringAsync (html : string) =
        this.SetContentType "text/html"
        this.WriteStringAsync html

    member this.WriteHtmlViewAsync (htmlView : XmlNode) =
        this.SetContentType "text/html"
        this.WriteStringAsync (renderHtmlDocument htmlView)

// ---------------------------
// HttpHandler functions
// ---------------------------

/// Writes a byte array to the body of the HTTP response and sets the HTTP Content-Length header accordingly.
let setBody (bytes : byte array) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteBytesAsync bytes

/// Writes a string to the body of the HTTP response and sets the HTTP Content-Length header accordingly.
let setBodyFromString (str : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteStringAsync str

/// Writes a string to the body of the HTTP response.
/// It also sets the HTTP Content-Type header to 'text/plain' and sets the Content-Length header accordingly.
let text (str : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteTextAsync str

/// Serializes an object to JSON and writes the output to the body of the HTTP response.
/// It also sets the HTTP Content-Type header to 'application/json' and sets the Content-Length header accordingly.
let json (dataObj : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteJsonAsync dataObj

/// Serializes an object to XML and writes the output to the body of the HTTP response.
/// It also sets the HTTP header Content-Type to 'application/xml' and sets the Content-Length header accordingly.
let xml (dataObj : obj) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteXmlAsync dataObj

/// Reads a HTML file from disk and writes its contents to the body of the HTTP response.
/// It also sets the HTTP header Content-Type to 'text/html' and sets the Content-Length header accordingly.
let htmlFile (filePath : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteHtmlFileAsync filePath

/// Writes a string to the body of the HTTP response and sets the HTTP header Content-Type to 'text/html`
/// and sets the Content-Length header accordingly.
let htmlString (html : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteHtmlStringAsync html

/// Compiles a Giraffe.GiraffeViewEngine XmlNode to a HTML view and writes the output to the body of the HTTP response.
/// It also sets the HTTP header Content-Type to 'text/html' and sets the Content-Length header accordingly.
let htmlView (view : XmlNode) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteHtmlViewAsync view