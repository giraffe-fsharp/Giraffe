open System
open BenchmarkDotNet.Attributes;
open BenchmarkDotNet.Running;
open Giraffe.GiraffeViewEngine
open System.Text

let private DefaultCapacity = 16 * 1024
let private MaxBuilderSize = DefaultCapacity * 3

type MemoryStreamCache = 

    [<ThreadStatic>]
    [<DefaultValue>]
    static val mutable private instance: StringBuilder

    static member Get() = MemoryStreamCache.Get(DefaultCapacity)
    static member Get(capacity:int) = 
        
        if capacity <= MaxBuilderSize then
            let ms = MemoryStreamCache.instance;
            let capacity = max capacity DefaultCapacity
            
            if ms <> null && capacity <= ms.Capacity then
                MemoryStreamCache.instance <- null;
                ms.Clear()
            else
                new StringBuilder(capacity)
        else
            new StringBuilder(capacity)

    static member Release(ms:StringBuilder) = 
        if ms.Capacity <= MaxBuilderSize then
            MemoryStreamCache.instance <- ms

[<MemoryDiagnoser>]
type HtmlBench() =

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
    member this.RenderHtmlOriginal() = 
        renderHtmlDocument doc

    [<Benchmark>]
    member this.RenderHtmlStatefull() = 
        let sb = new StringBuilder()
        StatefullRendering.renderHtmlDocument sb doc
        sb.ToString() |> ignore

    [<Benchmark>]
    member this.RenderHtmlStatefullCached() = 
        let sb = MemoryStreamCache.Get()
        StatefullRendering.renderHtmlDocument sb doc
        sb.ToString() |> ignore
        MemoryStreamCache.Release sb

    [<Benchmark>]
    member this.RenderHtmlStatefullCachedNoCopy() = 
        let sb = MemoryStreamCache.Get()
        StatefullRendering.renderHtmlDocument sb doc
        MemoryStreamCache.Release sb

[<EntryPoint>]
let main args =
    let asm = typeof<HtmlBench>.Assembly
    BenchmarkSwitcher.FromAssembly(asm).Run(args) |> ignore
    0

