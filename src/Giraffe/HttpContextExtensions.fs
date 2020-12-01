namespace Giraffe

open System
open System.IO
open System.Text
open System.Globalization
open System.Runtime.CompilerServices
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open Microsoft.Net.Http.Headers
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe.ViewEngine

type MissingDependencyException(dependencyName : string) =
    inherit Exception(
        sprintf "Could not retrieve object of type '%s' from ASP.NET Core's dependency container. Please register all Giraffe dependencies by adding `services.AddGiraffe()` to your startup code. For more information visit https://github.com/giraffe-fsharp/Giraffe." dependencyName)

[<Extension>]
type HttpContextExtensions() =

    /// <summary>
    /// Returns the entire request URL in a fully escaped form, which is suitable for use in HTTP headers and other operations.
    /// </summary>
    /// <returns>Returns a <see cref="System.String"/> URL.</returns>
    [<Extension>]
    static member GetRequestUrl(ctx : HttpContext) =
        ctx.Request.GetEncodedUrl()

    /// <summary>
    /// Gets an instance of `'T` from the request's service container.
    /// </summary
    /// <returns>Returns an instance of `'T`.</returns>
    [<Extension>]
    static member GetService<'T>(ctx : HttpContext) =
        let t = typeof<'T>
        match ctx.RequestServices.GetService t with
        | null    -> raise (MissingDependencyException t.Name)
        | service -> service :?> 'T

    /// <summary>
    /// Gets an instance of <see cref="Microsoft.Extensions.Logging.ILogger<'T>" /> from the request's service container.
    ///
    /// The type `'T` should represent the class or module from where the logger gets instantiated.
    /// </summary>
    /// <returns> Returns an instance of <see cref="Microsoft.Extensions.Logging.ILogger<'T>" />.</returns>
    [<Extension>]
    static member GetLogger<'T>(ctx : HttpContext) =
        ctx.GetService<ILogger<'T>>()

    /// <summary>
    /// Gets an instance of <see cref="Microsoft.Extensions.Logging.ILogger"/> from the request's service container.
    /// </summary>
    /// <param name="categoryName">The category name for messages produced by this logger.</param>
    /// <returns>Returns an instance of <see cref="Microsoft.Extensions.Logging.ILogger"/>.</returns>
    [<Extension>]
    static member GetLogger (ctx : HttpContext, categoryName : string) =
        let loggerFactory = ctx.GetService<ILoggerFactory>()
        loggerFactory.CreateLogger categoryName

    /// <summary>
    /// Gets an instance of <see cref="Microsoft.Extensions.Hosting.IHostingEnvironment"/> from the request's service container.
    /// </summary>
    /// <returns>Returns an instance of <see cref="Microsoft.Extensions.Hosting.IHostingEnvironment"/>.</returns>
    [<Extension>]
    static member GetHostingEnvironment(ctx : HttpContext) =
        ctx.GetService<IHostingEnvironment>()

    /// <summary>
    /// Gets an instance of <see cref="Giraffe.Serialization.Json.ISerializer"/> from the request's service container.
    /// </summary>
    /// <returns>Returns an instance of <see cref="Giraffe.Serialization.Json.ISerializer"/>.</returns>
    [<Extension>]
    static member GetJsonSerializer(ctx : HttpContext) : Json.ISerializer =
        ctx.GetService<Json.ISerializer>()

    /// <summary>
    /// Gets an instance of <see cref="Giraffe.Serialization.Xml.Xml.ISerializer"/> from the request's service container.
    /// </summary>
    /// <returns>Returns an instance of <see cref="Giraffe.Serialization.Xml.Xml.ISerializer"/>.</returns>
    [<Extension>]
    static member GetXmlSerializer(ctx : HttpContext) : Xml.ISerializer  =
        ctx.GetService<Xml.ISerializer>()

    /// <summary>
    /// Sets the HTTP status code of the response.
    /// </summary>
    /// <param name="httpStatusCode">The status code to be set in the response. For convenience you can use the static <see cref="Microsoft.AspNetCore.Http.StatusCodes"/> class for passing in named status codes instead of using pure int values.</param>
    [<Extension>]
    static member SetStatusCode (ctx : HttpContext, httpStatusCode : int) =
        ctx.Response.StatusCode <- httpStatusCode

    /// <summary>
    /// Adds or sets a HTTP header in the response.
    /// </summary>
    /// <param name="key">The HTTP header name. For convenience you can use the static <see cref="Microsoft.Net.Http.Headers.HeaderNames"/> class for passing in strongly typed header names instead of using pure `string` values.</param>
    /// <param name="value">The value to be set. Non string values will be converted to a string using the object's ToString() method.</param>
    [<Extension>]
    static member SetHttpHeader (ctx : HttpContext, key : string, value : obj) =
        ctx.Response.Headers.[key] <- StringValues(value.ToString())

    /// <summary>
    /// Sets the Content-Type HTTP header in the response.
    /// </summary>
    /// <param name="contentType">The mime type of the response (e.g.: application/json or text/html).</param>
    [<Extension>]
    static member SetContentType (ctx : HttpContext, contentType : string) =
        ctx.SetHttpHeader(HeaderNames.ContentType, contentType)

    /// <summary>
    /// Tries to get the <see cref="System.String"/> value of a HTTP header from the request.
    /// </summary>
    /// <param name="key">The name of the HTTP header.</param>
    /// <returns> Returns Some string if the HTTP header was present in the request, otherwise returns None.</returns>
    [<Extension>]
    static member TryGetRequestHeader (ctx : HttpContext, key : string) =
        match ctx.Request.Headers.TryGetValue key with
        | true, value -> Some (value.ToString())
        | _           -> None
    /// <summary>
    /// Retrieves the <see cref="System.String"/> value of a HTTP header from the request.
    /// </summary>
    /// <param name="key">The name of the HTTP header.</param>
    /// <returns>Returns Ok string if the HTTP header was present in the request, otherwise returns Error string.</returns>
    [<Extension>]
    static member GetRequestHeader (ctx : HttpContext, key : string) =
        match ctx.Request.Headers.TryGetValue key with
        | true, value -> Ok (value.ToString())
        | _           -> Error (sprintf "HTTP request header '%s' is missing." key)

    /// <summary>
    ///  Tries to get the <see cref="System.String"/> value of a query string parameter from the request.
    /// </summary>
    /// <param name="key">The name of the query string parameter.</param>
    /// <returns>Returns Some string if the parameter was present in the request's query string, otherwise returns None.</returns>
    [<Extension>]
    static member TryGetQueryStringValue (ctx : HttpContext, key : string) =
        match ctx.Request.Query.TryGetValue key with
        | true, value -> Some (value.ToString())
        | _           -> None

    /// <summary>
    /// Retrieves the <see cref="System.String"/> value of a query string parameter from the request.
    /// </summary>
    /// <param name="key">The name of the query string parameter.</param>
    /// <returns>Returns Ok string if the parameter was present in the request's query string, otherwise returns Error string.</returns>
    [<Extension>]
    static member GetQueryStringValue (ctx : HttpContext, key : string) =
        match ctx.Request.Query.TryGetValue key with
        | true, value -> Ok (value.ToString())
        | _           -> Error (sprintf "Query string value '%s' is missing." key)

    /// <summary>
    /// Retrieves the <see cref="System.String"/> value of a cookie from the request.
    /// </summary>
    /// <param name="key">The name of the cookie.</param>
    /// <returns>Returns Some string if the cookie was set, otherwise returns None.</returns>
    [<Extension>]
    static member GetCookieValue (ctx : HttpContext, key : string) =
        match ctx.Request.Cookies.TryGetValue key with
        | true , cookie -> Some cookie
        | false, _      -> None

    /// <summary>
    /// Retrieves the <see cref="System.String"/> value of a form parameter from the request.
    /// </summary>
    /// <param name="key">The name of the form parameter.</param>
    /// <returns>Returns Some string if the form parameter was set, otherwise returns None.</returns>
    [<Extension>]
    static member GetFormValue (ctx : HttpContext, key : string) =
        match ctx.Request.HasFormContentType with
        | false -> None
        | true  ->
            match ctx.Request.Form.TryGetValue key with
            | true , value -> Some (value.ToString())
            | false, _     -> None

    /// <summary>
    /// Reads the entire body of the <see cref="Microsoft.AspNetCore.Http.HttpRequest"/> asynchronously and returns it as a <see cref="System.String"/> value.
    /// </summary>
    /// <returns>Returns the contents of the request body as a <see cref="System.Threading.Tasks.Task{System.String}"/>.</returns>
    [<Extension>]
    static member ReadBodyFromRequestAsync(ctx : HttpContext) =
        task {
            use reader = new StreamReader(ctx.Request.Body, Encoding.UTF8)
            return! reader.ReadToEndAsync()
        }

    /// <summary>
    /// Reads the entire body of the <see cref="Microsoft.AspNetCore.Http.HttpRequest"/> asynchronously and returns it as a <see cref="System.String"/> value.
    /// This method buffers the response and makes subsequent reads possible.
    /// </summary>
    /// <returns>Returns the contents of the request body as a <see cref="System.Threading.Tasks.Task{System.String}"/>.</returns>
    [<Extension>]
    static member ReadBodyBufferedFromRequestAsync(ctx : HttpContext) =
        task {
            ctx.Request.EnableBuffering()
            use reader =
                new StreamReader(
                    ctx.Request.Body,
                    encoding = Encoding.UTF8,
                    detectEncodingFromByteOrderMarks = false,
                    leaveOpen = true)
            let! body = reader.ReadToEndAsync()
            ctx.Request.Body.Position <- 0L
            return body
        }

    /// <summary>
    /// Uses the <see cref="Json.ISerializer"/> to deserializes the entire body of the <see cref="Microsoft.AspNetCore.Http.HttpRequest"/> asynchronously into an object of type 'T.
    /// </summary>
    /// <typeparam name="'T"></typeparam>
    /// <returns>Retruns a <see cref="System.Threading.Tasks.Task{T}"/></returns>
    [<Extension>]
    static member BindJsonAsync<'T>(ctx : HttpContext) =
        task {
            let serializer = ctx.GetJsonSerializer()
            return! serializer.DeserializeAsync<'T> ctx.Request.Body
        }

    /// <summary>
    /// Uses the <see cref="Xml.ISerializer"/> to deserializes the entire body of the <see cref="Microsoft.AspNetCore.Http.HttpRequest"/> asynchronously into an object of type 'T.
    /// </summary>
    /// <typeparam name="'T"></typeparam>
    /// <returns>Retruns a <see cref="System.Threading.Tasks.Task{T}"/></returns>
    [<Extension>]
    static member BindXmlAsync<'T>(ctx : HttpContext) =
        task {
            let serializer = ctx.GetXmlSerializer()
            let! body = ctx.ReadBodyFromRequestAsync()
            return serializer.Deserialize<'T> body
        }

    /// <summary>
    /// Parses all input elements from an HTML form into an object of type 'T.
    /// </summary>
    /// <param name="cultureInfo">An optional <see cref="System.Globalization.CultureInfo"/> element to be used when parsing culture specific data such as float, DateTime or decimal values.</param>
    /// <typeparam name="'T"></typeparam>
    /// <returns>Returns a <see cref="System.Threading.Tasks.Task{T}"/></returns>
    [<Extension>]
    static member BindFormAsync<'T> (ctx : HttpContext, ?cultureInfo : CultureInfo) =
        task {
            let! form = ctx.Request.ReadFormAsync()
            return
                form
                |> Seq.map (fun i -> i.Key, i.Value)
                |> dict
                |> ModelParser.parse<'T> cultureInfo
        }

    /// <summary>
    /// Tries to parse all input elements from an HTML form into an object of type 'T.
    /// </summary>
    /// <param name="cultureInfo">An optional <see cref="System.Globalization.CultureInfo"/> element to be used when parsing culture specific data such as float, DateTime or decimal values.</param>
    /// <typeparam name="'T"></typeparam>
    /// <returns>Returns an object 'T if model binding succeeded, otherwise a <see cref="System.String"/> message containing the specific model parsing error.</returns>
    [<Extension>]
    static member TryBindFormAsync<'T> (ctx : HttpContext, ?cultureInfo : CultureInfo) =
        task {
            let! form = ctx.Request.ReadFormAsync()
            return
                form
                |> Seq.map (fun i -> i.Key, i.Value)
                |> dict
                |> ModelParser.tryParse<'T> cultureInfo
        }

    /// <summary>
    /// Parses all parameters of a request's query string into an object of type 'T.
    /// </summary>
    /// <param name="cultureInfo">An optional <see cref="System.Globalization.CultureInfo"/> element to be used when parsing culture specific data such as float, DateTime or decimal values.</param>
    /// <typeparam name="'T"></typeparam>
    /// <returns>Returns an instance of type 'T</returns>
    [<Extension>]
    static member BindQueryString<'T> (ctx : HttpContext, ?cultureInfo : CultureInfo) =
        ctx.Request.Query
        |> Seq.map (fun i -> i.Key, i.Value)
        |> dict
        |> ModelParser.parse<'T> cultureInfo

    /// <summary>
    /// Tries to parse all parameters of a request's query string into an object of type 'T.
    /// </summary>
    /// <param name="cultureInfo">An optional <see cref="System.Globalization.CultureInfo"/> element to be used when parsing culture specific data such as float, DateTime or decimal values.</param>
    /// <typeparam name="'T"></typeparam>
    /// <returns>Returns an object 'T if model binding succeeded, otherwise a <see cref="System.String"/> message containing the specific model parsing error.</returns>
    [<Extension>]
    static member TryBindQueryString<'T> (ctx : HttpContext, ?cultureInfo : CultureInfo) =
        ctx.Request.Query
        |> Seq.map (fun i -> i.Key, i.Value)
        |> dict
        |> ModelParser.tryParse<'T> cultureInfo

    /// <summary>
    /// Parses the request body into an object of type 'T based on the request's Content-Type header.
    /// </summary>
    /// <param name="cultureInfo">An optional <see cref="System.Globalization.CultureInfo"/> element to be used when parsing culture specific data such as float, DateTime or decimal values.</param>
    /// <typeparam name="'T"></typeparam>
    /// <returns>Returns a <see cref="System.Threading.Tasks.Task{T}"/></returns>
    [<Extension>]
    static member BindModelAsync<'T> (ctx : HttpContext, ?cultureInfo : CultureInfo) =
        task {
            let method = ctx.Request.Method
            if method.Equals "POST" || method.Equals "PUT" || method.Equals "PATCH" || method.Equals "DELETE" then
                let original = StringSegment(ctx.Request.ContentType)
                let parsed   = ref (MediaTypeHeaderValue(StringSegment("*/*")))
                return!
                    match MediaTypeHeaderValue.TryParse(original, parsed) with
                    | false -> failwithf "Could not parse Content-Type HTTP header value '%s'" original.Value
                    | true  ->
                        match parsed.Value.MediaType.Value with
                        | "application/json"                  -> ctx.BindJsonAsync<'T>()
                        | "application/xml"                   -> ctx.BindXmlAsync<'T>()
                        | "application/x-www-form-urlencoded" -> ctx.BindFormAsync<'T>(?cultureInfo = cultureInfo)
                        | _ -> failwithf "Cannot bind model from Content-Type '%s'" original.Value
            else return ctx.BindQueryString<'T>(?cultureInfo = cultureInfo)
        }

    /// <summary>
    /// Writes a byte array to the body of the HTTP response and sets the HTTP Content-Length header accordingly.
    /// </summary>
    /// <param name="bytes">The byte array to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteBytesAsync (ctx : HttpContext, bytes : byte[]) =
        task {
            ctx.SetHttpHeader(HeaderNames.ContentLength, bytes.Length)
            if ctx.Request.Method <> HttpMethods.Head then
                do! ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length)
            return Some ctx
        }

    /// <summary>
    /// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP Content-Length header accordingly.
    /// </summary>
    /// <param name="str">The string value to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteStringAsync (ctx : HttpContext, str : string) =
        ctx.WriteBytesAsync(Encoding.UTF8.GetBytes str)

    /// <summary>
    /// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP `Content-Length` header accordingly, as well as the `Content-Type` header to `text/plain`.
    /// </summary>
    /// <param name="str">The string value to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteTextAsync (ctx : HttpContext, str : string) =
        ctx.SetContentType "text/plain; charset=utf-8"
        ctx.WriteStringAsync str

    /// <summary>
    /// Serializes an object to JSON and writes the output to the body of the HTTP response.
    /// It also sets the HTTP Content-Type header to application/json and sets the Content-Length header accordingly.
    /// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type <see cref="Json.ISerializer"/>
    /// </summary>
    /// <param name="dataObj">The object to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteJsonAsync<'T> (ctx : HttpContext, dataObj : 'T) =
        ctx.SetContentType "application/json; charset=utf-8"
        let serializer = ctx.GetJsonSerializer()
        serializer.SerializeToBytes dataObj
        |> ctx.WriteBytesAsync

    /// <summary>
    /// Serializes an object to JSON and writes the output to the body of the HTTP response using chunked transfer encoding.
    /// It also sets the HTTP Content-Type header to application/json and sets the Transfer-Encoding header to chunked.
    /// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type <see cref="Json.ISerializer"/>.
    /// </summary>
    /// <param name="dataObj">The object to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteJsonChunkedAsync<'T> (ctx : HttpContext, dataObj : 'T) =
        task {
            // Don't set the Transfer-Encoding to chunked manually.  If we do, we'll have to do the chunking manually
            // ourselves rather than rely on asp.net to do it for us.
            // Example : https://github.com/aspnet/AspNetCore/blame/728110ec9ee1b98b2d9c9ff247ba2955d6c05846/src/Servers/Kestrel/test/InMemory.FunctionalTests/ChunkedResponseTests.cs#L494
            ctx.SetContentType "application/json; charset=utf-8"
            if ctx.Request.Method <> HttpMethods.Head then
                let serializer = ctx.GetJsonSerializer()
                do! serializer.SerializeToStreamAsync dataObj ctx.Response.Body
            return Some ctx
        }

    /// <summary>
    /// Serializes an object to XML and writes the output to the body of the HTTP response.
    /// It also sets the HTTP Content-Type header to application/xml and sets the Content-Length header accordingly.
    /// The JSON serializer can be configured in the ASP.NET Core startup code by registering a custom class of type <see cref="Xml.ISerializer"/>.
    /// </summary>
    /// <param name="dataObj">The object to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteXmlAsync (ctx : HttpContext, dataObj : obj) =
        ctx.SetContentType "application/xml; charset=utf-8"
        let serializer = ctx.GetXmlSerializer()
        serializer.Serialize dataObj
        |> ctx.WriteBytesAsync

    /// <summary>
    /// Reads a HTML file from disk and writes its contents to the body of the HTTP response.
    /// It also sets the HTTP header Content-Type to text/html and sets the Content-Length header accordingly.
    /// </summary>
    /// <param name="filePath">A relative or absolute file path to the HTML file.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteHtmlFileAsync (ctx : HttpContext, filePath : string) =
        task {
            let filePath =
                match Path.IsPathRooted filePath with
                | true  -> filePath
                | false ->
                    let env = ctx.GetHostingEnvironment()
                    Path.Combine(env.ContentRootPath, filePath)
            ctx.SetContentType "text/html; charset=utf-8"
            let! html = readFileAsStringAsync filePath
            return! ctx.WriteStringAsync html
        }

    /// <summary>
    /// Writes a HTML string to the body of the HTTP response.
    /// It also sets the HTTP header Content-Type to text/html and sets the Content-Length header accordingly.
    /// </summary>
    /// <param name="html">The HTML string to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteHtmlStringAsync (ctx : HttpContext, html : string) =
        ctx.SetContentType "text/html; charset=utf-8"
        ctx.WriteStringAsync html

    /// <summary>
    /// <para>Compiles a `Giraffe.GiraffeViewEngine.XmlNode` object to a HTML view and writes the output to the body of the HTTP response.</para>
    /// <para>It also sets the HTTP header `Content-Type` to `text/html` and sets the `Content-Length` header accordingly.</para>
    /// <param name="htmlView">An `XmlNode` object to be send back to the client and which represents a valid HTML view.</param>
    /// <returns>Task of `Some HttpContext` after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteHtmlViewAsync (ctx : HttpContext, htmlView : XmlNode) =
        let bytes = RenderView.AsBytes.htmlDocument htmlView
        ctx.SetContentType "text/html; charset=utf-8"
        ctx.WriteBytesAsync bytes