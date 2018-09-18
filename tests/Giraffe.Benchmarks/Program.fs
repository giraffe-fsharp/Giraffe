open BenchmarkDotNet.Attributes;
open BenchmarkDotNet.Running;
open Giraffe.GiraffeViewEngine
open System.Text
open System.Buffers

[<MemoryDiagnoser>]
type HtmlUtf8Benchmark() =

    let doc =
        div [] [
            div [ _class "top-bar" ]
                [ div [ _class "top-bar-left" ]
                    [ ul [ _class "dropdown menu"
                           _data "dropdown-menu" ]
                        [ li [ _class "menu-text" ]
                            [ rawText "Site Title" ]
                          li [ ]
                            [ a [ _href "#" ]
                                [ encodedText """One <script>alert("hello world")</script>""" ]
                              ul [ _class "menu vertical" ]
                                [ li [ ]
                                    [ a [ _href "#" ]
                                        [ rawText "One" ] ]
                                  li [ ]
                                    [ a [ _href "#" ]
                                        [ encodedText "Two" ] ]
                                  li [ ]
                                    [ a [ _href "#" ]
                                        [ rawText "Three" ] ] ] ]
                          li [ ]
                            [ a [ _href "#" ]
                                [ encodedText "Two" ] ]
                          li [ ]
                            [ a [ _href "#" ]
                                [ encodedText "Three" ] ] ] ]
                  div [ _class "top-bar-right" ]
                    [ ul [ _class "menu" ]
                        [ li [ ]
                            [ input [ _type "search"
                                      _placeholder "Search" ] ]
                          li [ ]
                            [ button [ _type "button"
                                       _class "button" ]
                                [ rawText "Search" ] ] ] ] ]
        ]

    let stringBuilder = new StringBuilder(16 * 1024)

    [<Benchmark( Baseline = true )>]
    member this.Default() =
        renderHtmlDocument doc |> Encoding.UTF8.GetBytes

    [<Benchmark>]
    member this.CachedStringBuilder() =
        ViewBuilder.buildHtmlDocument stringBuilder doc
        stringBuilder.ToString() |> Encoding.UTF8.GetBytes |> ignore
        stringBuilder.Clear();

    [<Benchmark>]
    member this.CachedStringBuilderPooledUtf8Array() =
        ViewBuilder.buildHtmlDocument stringBuilder doc
        let chars = ArrayPool<char>.Shared.Rent(stringBuilder.Length)
        stringBuilder.CopyTo(0, chars, 0, stringBuilder.Length)
        Encoding.UTF8.GetBytes(chars, 0, stringBuilder.Length) |> ignore
        ArrayPool<char>.Shared.Return(chars)
        stringBuilder.Clear()

[<EntryPoint>]
let main args =
    let asm = typeof<HtmlUtf8Benchmark>.Assembly
    BenchmarkSwitcher.FromAssembly(asm).Run(args) |> ignore
    0