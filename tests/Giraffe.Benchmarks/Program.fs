open BenchmarkDotNet.Attributes;
open BenchmarkDotNet.Running;
open Giraffe.GiraffeViewEngine
open System.Text
open System.Buffers
open System

[<AutoOpen>]
module Caching =

    let DefaultCapacity = 8 * 1024
    let MaxBuilderSize = DefaultCapacity * 8
    
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

    [<Benchmark( Baseline = true )>]
    member this.String() = 
        renderHtmlDocument doc |> Encoding.UTF8.GetBytes

    [<Benchmark>]
    member this.Cached() = 
        let sb = StringBuilderCache.Get()
        renderHtmlDocument' sb doc
        sb.ToString() |> Encoding.UTF8.GetBytes |> ignore
        StringBuilderCache.Release sb

    [<Benchmark>]
    member this.CachedAndPooled() = 
        let sb = StringBuilderCache.Get()
        renderHtmlDocument' sb doc
        let chars = ArrayPool<char>.Shared.Rent(sb.Length)
        sb.CopyTo(0, chars, 0, sb.Length) 
        Encoding.UTF8.GetBytes(chars, 0, sb.Length) |> ignore
        ArrayPool<char>.Shared.Return(chars)
        StringBuilderCache.Release sb

[<EntryPoint>]
let main args =
    let asm = typeof<HtmlUtf8Benchmark>.Assembly
    BenchmarkSwitcher.FromAssembly(asm).Run(args) |> ignore
    0

