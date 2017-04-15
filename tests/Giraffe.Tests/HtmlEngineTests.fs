module Giraffe.HtmlEngineTests

open System
open Xunit
open Giraffe.HtmlEngine

let removeNewLines (html:string):string =
    html.Replace(Environment.NewLine, String.Empty)

[<Fact>]
let ``Single html root should compile`` () =
    let doc  = html [] []
    let html =
        doc 
        |> renderHtmlDocument
        |> removeNewLines
    Assert.Equal("<!DOCTYPE html><html></html>", html)

[<Fact>]
let ``Anchor should contain href, target and content`` () =
    let anchor =
        a [ "href", "http://example.org"; "target", "_blank" ] (encodedText "Example")
    let html = nodeToHtmlString anchor
    Assert.Equal("<a href=\"http://example.org\" target=\"_blank\">Example</a>", html)

[<Fact>]
let ``Nested content should render correctly`` () =
    let nested =
        div [] [
            h1 [] (encodedText "Header")
            p [] [
                EncodedText "Lorem "
                strong [] (encodedText "Ipsum")
                RawText " dollar"
        ] ]
    let html = 
        nested
        |> nodeToHtmlString
        |> removeNewLines
    Assert.Equal("<div><h1>Header</h1><p>Lorem <strong>Ipsum</strong> dollar</p></div>", html)

[<Fact>]
let ``Void tag should be unary tag`` () =
    let unary =  br [] |> nodeToHtmlString
    Assert.Equal("<br>", unary)