module Giraffe.XmlViewEngineTests

open System
open Xunit
open Giraffe.XmlViewEngine

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
        a [ "href", "http://example.org"; "target", "_blank" ] [ encodedText "Example" ]
    let html = renderXmlNode anchor
    Assert.Equal("<a href=\"http://example.org\" target=\"_blank\">Example</a>", html)

[<Fact>]
let ``Nested content should render correctly`` () =
    let nested =
        div [] [
            comment "this is a test"
            h1 [] [ encodedText "Header" ]
            p [] [
                EncodedText "Lorem "
                strong [] [ encodedText "Ipsum" ]
                RawText " dollar"
        ] ]
    let html = 
        nested
        |> renderXmlNode
        |> removeNewLines
    Assert.Equal("<div><!-- this is a test --><h1>Header</h1><p>Lorem <strong>Ipsum</strong> dollar</p></div>", html)

[<Fact>]
let ``Void tag in XML should be self closing tag`` () =
    let unary =  br [] |> renderXmlNode
    Assert.Equal("<br />", unary)

[<Fact>]
let ``Void tag in HTML should be unary tag`` () =
    let unary =  br [] |> renderHtmlNode
    Assert.Equal("<br>", unary)