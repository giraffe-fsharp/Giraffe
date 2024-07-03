open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open Giraffe
open Microsoft.AspNetCore.Http
open System.Text
open System.Threading.Tasks
open NSubstitute
open System.IO

let mockHttpFunc: HttpFunc = (Some >> Task.FromResult)

let mockCtx: HttpContext =
    let ctx = Substitute.For<HttpContext>()

    ctx.Request.Method.ReturnsForAnyArgs "GET" |> ignore
    ctx.Request.Path.ReturnsForAnyArgs (PathString("/")) |> ignore
    ctx.Response.StatusCode.ReturnsForAnyArgs 200 |> ignore
    ctx.Response.Body <- new MemoryStream()

    ctx

let testHandler__v1 (handlerReturn: byte array) : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.WriteBytesAsync (handlerReturn)

let testHandler__v2 (handlerReturn: byte array) : HttpHandler =
    fun (_ : HttpFunc) (ctx : HttpContext) ->
        ctx.OptimizedWriteBytesAsync (handlerReturn)

[<MemoryDiagnoser>]
type WriteBytesAsync() =
    
    [<Params (100, 500, 1000)>] 
    member val ListSize : int = 0 with get, set

    member self.handlerReturn : byte array =
        [ 0 .. self.ListSize ] |> List.map (fun _ -> "a") |> List.reduce (+) |> Encoding.UTF8.GetBytes

    [<Benchmark(Baseline = true)>]
    member self.V1 () =
        testHandler__v1 (self.handlerReturn) (mockHttpFunc) (mockCtx)

    [<Benchmark>]
    member self.V2 () =
        testHandler__v2 (self.handlerReturn) (mockHttpFunc) (mockCtx)

[<EntryPoint>]
let main (_args: string[]) : int =
    BenchmarkRunner.Run<WriteBytesAsync>() |> ignore
    0