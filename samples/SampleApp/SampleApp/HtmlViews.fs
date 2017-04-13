module SampleApp.HtmlViews

open Giraffe.HtmlEngine
open SampleApp.Models

let layout (content: HtmlNode list) =
    html [] [
        head [] [
            title [] (encodedText "Giraffe")
        ]
        body [] content
    ]

let partial () =
    p [] (encodedText "Some partial text.")

let personView (model : Person) =
    [
        div [] [
                h3 [] (sprintf "Hello, %s" model.Name |> encodedText)
            ]
        div [] [partial()]
    ] |> layout
  
let model() =
    System.DateTime.Now.ToString()

let staticView =
    p [] (model() |> rawText)

let dynamicView() =
    p [] (model() |> rawText)