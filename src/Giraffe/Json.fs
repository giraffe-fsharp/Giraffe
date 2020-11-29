namespace Giraffe

[<RequireQualifiedAccess>]
module Json =
    open System.IO
    open System.Threading.Tasks

    /// <summary>
    /// Interface defining JSON serialization methods.
    /// Use this interface to customize JSON serialization in Giraffe.
    /// </summary>
    [<AllowNullLiteral>]
    type ISerializer =
        abstract member SerializeToString<'T>       : 'T -> string
        abstract member SerializeToBytes<'T>        : 'T -> byte array
        abstract member SerializeToStreamAsync<'T>  : 'T -> Stream -> Task

        abstract member Deserialize<'T>             : string -> 'T
        abstract member Deserialize<'T>             : byte[] -> 'T
        abstract member DeserializeAsync<'T>        : Stream -> Task<'T>

[<RequireQualifiedAccess>]
module NewtonsoftJson =
    open System.IO
    open System.Text
    open System.Threading.Tasks
    open Microsoft.IO
    open FSharp.Control.Tasks.V2.ContextInsensitive
    open Newtonsoft.Json
    open Newtonsoft.Json.Serialization

    let recyclableMemoryStreamManager = RecyclableMemoryStreamManager()

    /// <summary>
    /// Default JSON serializer in Giraffe.
    /// Serializes objects to camel cased JSON code.
    /// </summary>
    type Serializer (settings : JsonSerializerSettings) =
        let serializer = JsonSerializer.Create settings
        let utf8EncodingWithoutBom = UTF8Encoding(false)

        static member DefaultSettings =
            JsonSerializerSettings(
                ContractResolver = CamelCasePropertyNamesContractResolver())

        interface Json.ISerializer with
            member __.SerializeToString (x : 'T) =
                JsonConvert.SerializeObject(x, settings)

            member __.SerializeToBytes (x : 'T) =
                JsonConvert.SerializeObject(x, settings)
                |> Encoding.UTF8.GetBytes

            member __.SerializeToStreamAsync (x : 'T) (stream : Stream) =
                task {
                    use memoryStream = recyclableMemoryStreamManager.GetStream()
                    use streamWriter = new StreamWriter(memoryStream, utf8EncodingWithoutBom)
                    use jsonTextWriter = new JsonTextWriter(streamWriter)
                    serializer.Serialize(jsonTextWriter, x)
                    jsonTextWriter.Flush()
                    memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
                    do! memoryStream.CopyToAsync(stream, 65536)
                } :> Task

            member __.Deserialize<'T> (json : string) =
                JsonConvert.DeserializeObject<'T>(json, settings)

            member __.Deserialize<'T> (bytes : byte array) =
                let json = Encoding.UTF8.GetString bytes
                JsonConvert.DeserializeObject<'T>(json, settings)

            member __.DeserializeAsync<'T> (stream : Stream) =
                task {
                    use memoryStream = new MemoryStream()
                    do! stream.CopyToAsync(memoryStream)
                    memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
                    use streamReader = new StreamReader(memoryStream)
                    use jsonTextReader = new JsonTextReader(streamReader)
                    return serializer.Deserialize<'T>(jsonTextReader)
                }

[<RequireQualifiedAccess>]
module Utf8Json =
    open System.IO
    open System.Text
    open System.Threading.Tasks
    open Utf8Json

    /// <summary>
    /// <see cref="Utf8Json.Serializer" /> is an alternative serializer with
    /// great performance and supports true chunked transfer encoding.
    ///
    /// It uses Utf8Json as the underlying JSON serializer to (de-)serialize
    /// JSON content. Utf8Json is currently
    /// the fastest JSON serializer for .NET.
    /// </summary>
    /// <remarks>https://github.com/neuecc/Utf8Json</remarks>
    type Serializer (resolver : IJsonFormatterResolver) =

        static member DefaultResolver = Utf8Json.Resolvers.StandardResolver.CamelCase

        interface Json.ISerializer with
            member __.SerializeToString (x : 'T) =
                JsonSerializer.ToJsonString (x, resolver)

            member __.SerializeToBytes (x : 'T) =
                JsonSerializer.Serialize (x, resolver)

            member __.SerializeToStreamAsync (x : 'T) (stream : Stream) =
                JsonSerializer.SerializeAsync(stream, x, resolver)

            member __.Deserialize<'T> (json : string) : 'T =
                let bytes = Encoding.UTF8.GetBytes json
                JsonSerializer.Deserialize(bytes, resolver)

            member __.Deserialize<'T> (bytes : byte array) : 'T =
                JsonSerializer.Deserialize(bytes, resolver)

            member __.DeserializeAsync<'T> (stream : Stream) : Task<'T> =
                JsonSerializer.DeserializeAsync(stream, resolver)

[<RequireQualifiedAccess>]
module SystemTextJson =
    open System
    open System.IO
    open System.Text
    open System.Text.Json
    open System.Threading.Tasks

    /// <summary>
    /// <see cref="SystemTextJson.Serializer" /> is an alternaive <see cref="Json.ISerializer"/> in Giraffe.
    ///
    /// It uses <see cref="System.Text.Json"/> as the underlying JSON serializer to (de-)serialize
    /// JSON content.
    /// For support of F# unions and records, look at https://github.com/Tarmil/FSharp.SystemTextJson
    /// which plugs into this serializer.
    /// </summary>
    type Serializer (options: JsonSerializerOptions) =

        static member DefaultOptions =
           JsonSerializerOptions(
               PropertyNamingPolicy = JsonNamingPolicy.CamelCase
           )

        interface Json.ISerializer with
            member __.SerializeToString (x : 'T) =
                JsonSerializer.Serialize(x,  options)

            member __.SerializeToBytes (x : 'T) =
                JsonSerializer.SerializeToUtf8Bytes(x, options)

            member __.SerializeToStreamAsync (x : 'T) (stream : Stream) =
                JsonSerializer.SerializeAsync(stream, x, options)

            member __.Deserialize<'T> (json : string) : 'T =
                JsonSerializer.Deserialize<'T>(json, options)

            member __.Deserialize<'T> (bytes : byte array) : 'T =
                JsonSerializer.Deserialize<'T>(Span<_>.op_Implicit(bytes.AsSpan()), options)

            member __.DeserializeAsync<'T> (stream : Stream) : Task<'T> =
                JsonSerializer.DeserializeAsync<'T>(stream, options).AsTask()