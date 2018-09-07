module Giraffe.Tests.GiraffeViewEngineTests

open Xunit
open Giraffe.GiraffeViewEngine

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
        a [ attr "href" "http://example.org";  attr "target" "_blank" ] [ encodedText "Example" ]
    let html = renderXmlNode anchor
    Assert.Equal("<a href=\"http://example.org\" target=\"_blank\">Example</a>", html)

[<Fact>]
let ``Script should contain src, lang and async`` () =
    let scriptFile =
        script [ attr "src" "http://example.org/example.js";  attr "lang" "javascript"; flag "async" ] []
    let html = renderXmlNode scriptFile
    Assert.Equal("<script src=\"http://example.org/example.js\" lang=\"javascript\" async></script>", html)

[<Fact>]
let ``Nested content should render correctly`` () =
    let nested =
        div [] [
            comment "this is a test"
            h1 [] [ encodedText "Header" ]
            p [] [
                rawText "Lorem "
                strong [] [ encodedText "Ipsum" ]
                encodedText " dollar"
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
