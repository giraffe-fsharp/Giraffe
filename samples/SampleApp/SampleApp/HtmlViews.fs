module SampleApp.HtmlViews

open Giraffe.GiraffeViewEngine
open Giraffe.GiraffeViewEngine.Attributes
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
        div [``class`` "container"] [
                h3 [``title_attr`` "Some title attribute"] [ sprintf "Hello, %s" model.Name |> encodedText ]
                a [href "https://github.com/giraffe-fsharp/Giraffe"] [encodedText "Github"]
            ]
        div [] [partial()]
    ] |> layout