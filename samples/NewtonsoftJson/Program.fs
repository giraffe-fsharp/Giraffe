open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Giraffe.EndpointRouting
open Newtonsoft.Json

[<RequireQualifiedAccess>]
module NewtonsoftJson =
    open System.IO
    open System.Text
    open System.Threading.Tasks
    open Microsoft.IO
    open Newtonsoft.Json.Serialization

    type Serializer(settings: JsonSerializerSettings, rmsManager: RecyclableMemoryStreamManager) =
        let serializer = JsonSerializer.Create settings
        let utf8EncodingWithoutBom = UTF8Encoding(false)

        static member DefaultSettings =
            JsonSerializerSettings(ContractResolver = CamelCasePropertyNamesContractResolver())

        interface Json.ISerializer with
            member __.SerializeToString(x: 'T) =
                JsonConvert.SerializeObject(x, settings)

            member __.SerializeToBytes(x: 'T) =
                JsonConvert.SerializeObject(x, settings) |> Encoding.UTF8.GetBytes

            member __.SerializeToStreamAsync (x: 'T) (stream: Stream) =
                task {
                    use memoryStream = rmsManager.GetStream("giraffe-json-serialize-to-stream")
                    use streamWriter = new StreamWriter(memoryStream, utf8EncodingWithoutBom)
                    use jsonTextWriter = new JsonTextWriter(streamWriter)
                    serializer.Serialize(jsonTextWriter, x)
                    jsonTextWriter.Flush()
                    memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
                    do! memoryStream.CopyToAsync(stream, 65536)
                }
                :> Task

            member __.Deserialize<'T>(json: string) : 'T =
                match JsonConvert.DeserializeObject<'T>(json, settings) with
                | null -> failwith "Deserialized object is null"
                | (notNull: 'T) -> notNull

            member __.Deserialize<'T>(bytes: byte array) : 'T =
                let json = Encoding.UTF8.GetString bytes

                match JsonConvert.DeserializeObject<'T>(json, settings) with
                | null -> failwith "Deserialized object is null"
                | (notNull: 'T) -> notNull

            member __.DeserializeAsync<'T>(stream: Stream) : Task<'T> =
                task {
                    use memoryStream = rmsManager.GetStream("giraffe-json-deserialize")
                    do! stream.CopyToAsync(memoryStream)
                    memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
                    use streamReader = new StreamReader(memoryStream)
                    use jsonTextReader = new JsonTextReader(streamReader)

                    return
                        match serializer.Deserialize<'T>(jsonTextReader) with
                        | null -> failwith "Deserialized object is null"
                        | (notNull: 'T) -> notNull
                }

type JsonResponse = { Foo: string; Bar: string; Age: int }

let endpoints: Endpoint list =
    [ GET [ route "/json" (json { Foo = "john"; Bar = "doe"; Age = 30 }) ] ]

let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

let configureServices (services: IServiceCollection) =
    services
        .AddSingleton<Json.ISerializer>(fun serviceProvider ->
            let rmsManager =
                serviceProvider.GetService<Microsoft.IO.RecyclableMemoryStreamManager>()
                |> function
                    | null -> Microsoft.IO.RecyclableMemoryStreamManager()
                    | notNull -> notNull

            NewtonsoftJson.Serializer(JsonSerializerSettings(), rmsManager) :> Json.ISerializer
        )
        .AddRouting()
        .AddResponseCaching()
        .AddGiraffe()
    |> ignore

let configureApp (appBuilder: IApplicationBuilder) =
    appBuilder.UseRouting().UseResponseCaching().UseEndpoints(_.MapGiraffeEndpoints(endpoints)).UseGiraffe
        notFoundHandler

[<EntryPoint>]
let main (args: string array) : int =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services

    let app = builder.Build()

    configureApp app
    app.Run()

    0
