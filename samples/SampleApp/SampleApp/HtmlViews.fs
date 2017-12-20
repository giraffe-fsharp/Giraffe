module SampleApp.HtmlViews

open Giraffe.GiraffeViewEngine
open SampleApp.Models

let layout (content: XmlNode list) =
    html [] [
        head [] [
            title []  [ encodedText "Giraffe" ]
        ]
        body [] content
    ]

let partial () =
    p [] [ encodedText "Some partial text." ]

let personView (model : Person) =
    [
        div [] [
                h3 [] [ sprintf "Hello, %s" model.Name |> encodedText ]
            ]
        div [] [partial()]
    ] |> layout