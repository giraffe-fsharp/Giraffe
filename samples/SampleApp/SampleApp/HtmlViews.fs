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
        div [_class "container"] [
                h3 [_title "Some title attribute"] [ sprintf "Hello, %s" model.Name |> encodedText ]
                a [_href "https://github.com/giraffe-fsharp/Giraffe"] [encodedText "Github"]
            ]
        div [] [partial()]
    ] |> layout