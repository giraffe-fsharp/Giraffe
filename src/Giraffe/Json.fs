namespace Giraffe

[<RequireQualifiedAccess>]
module Json =
    open System
    open System.IO
    open System.Text.Json
    open System.Threading.Tasks
    open System.Text.Json.Serialization

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

    /// <summary>
    /// <see cref="Serializer" /> is the default <see cref="Json.ISerializer"/> in Giraffe.
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

        interface ISerializer with
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
    
    module FsharpFriendlySerializer =
        let DefaultOptions =
           JsonFSharpOptions.Default()
        
        let private appendJsonFSharpOptions (fsharpOptions: JsonFSharpOptions) (jsonOptions: JsonSerializerOptions) =
            jsonOptions.Converters.Add(JsonFSharpConverter(fsharpOptions))
            jsonOptions
        
        let buildConfig (fsharpOptions: JsonFSharpOptions option) (jsonOptions: JsonSerializerOptions option) =
            jsonOptions
            |> Option.defaultValue (JsonSerializerOptions())
            |> appendJsonFSharpOptions (fsharpOptions |> Option.defaultValue DefaultOptions)
    
    type FsharpFriendlySerializer (?fsharpOptions: JsonFSharpOptions, ?jsonOptions: JsonSerializerOptions) =
        inherit Serializer(FsharpFriendlySerializer.buildConfig fsharpOptions jsonOptions)