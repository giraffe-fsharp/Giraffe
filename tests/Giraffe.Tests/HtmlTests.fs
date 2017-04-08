module Giraffe.HtmlTests

open Giraffe.Html
open Xunit
open System

let removeNewLines (html:string):string =
    html.Replace(Environment.NewLine, String.Empty)

[<Fact>]
let ``single html root should compile`` () =
    let htmlNode = html [] []
    let html = renderHtmlDocument htmlNode |> removeNewLines
    Assert.Equal("<!DOCTYPE html><html></html>", html)

[<Fact>]
let ``anchor should contain href, target and content`` () =
    let anchor = a ["href", "https://github.com/dustinmoris/Giraffe"; "target", "_blank"] (textContent "github")
    let html = htmlToString anchor
    Assert.Equal("""<a href="https://github.com/dustinmoris/Giraffe" target="_blank">github</a>""", html)

[<Fact>]
let ``nested content should render correctly`` () =
    let nested = div [] [
                    h1 [] (textContent "Header")
                    p [] [
                        Text "Lorem "
                        strong [] (textContent "Ipsum")
                        Text " dollar"
                    ]
                ]
    let html = htmlToString nested |> removeNewLines
    Assert.Equal("<div><h1>Header</h1><p>Lorem <strong>Ipsum</strong> dollar</p></div>", html)

[<Fact>]
let ``void tag should be unary tag`` () =
    let unary =  br [] |> htmlToString
    Assert.Equal("<br>", unary)