namespace SampleApp

open Giraffe.Html
open SampleApp.Models

module Html =

    let layout (content: Node list) =
        html [] [
            head [] [
                title [] (textContent "Giraffe")
            ]
            body [] content
        ]

    let partial () =
        p [] (textContent "Some partial text.")
    
    let person model =
        [
            div [] [
                    h3 [] (sprintf "Hello, %s" model.Name |> textContent)
                ]
            div [] [partial()]
        ] |> layout
  
    