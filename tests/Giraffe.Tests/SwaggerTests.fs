module Giraffe.SwaggerTests

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Xunit
open Xunit.Abstractions
open NSubstitute
open XmlViewEngine
open TokenRouter
open Giraffe.Tests.Asserts
open Giraffe
open Giraffe.XmlViewEngine
open Swagger
open System

let assertThat (cmp:bool) =
  Assert.True cmp

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``webapp is a simple route with verb `GET` returning text`` () =
  let webApp =
    <@ GET >=> route "/home" >=> text "Home." @>

  let ctx = analyze webApp AppAnalyzeRules.Default

  let exp = 
    { Verb="GET"
      Path="/home"
      Responses=
        [
          { StatusCode=200
            ContentType="text/plain"
            ModelType=(typeof<string>) }
        ]
    }
  let route = !ctx.Routes |> Seq.exactlyOne
  
  Assert.Equal(exp.Path, route.Path)
  Assert.Equal(exp.Verb, route.Verb)
  Assert.Equal(exp.Responses.[0], route.Responses.[0])
  


[<Fact>]
let ``webapp is a simple route with verb `POST` returning text`` () =
  let webApp =
    <@ POST >=> route "/home" >=> text "Home." @>

  let ctx = analyze webApp AppAnalyzeRules.Default

  let exp = 
    { Verb="POST"
      Path="/home"
      Responses=
        [
          { StatusCode=200
            ContentType="text/plain"
            ModelType=(typeof<string>) }
        ]
    }
  let route = !ctx.Routes |> Seq.exactlyOne
  
  Assert.Equal(exp.Path, route.Path)
  Assert.Equal(exp.Verb, route.Verb)
  Assert.Equal(exp.Responses.[0], route.Responses.[0])
  

[<Fact>]
let ``webapp is a simple route with verb `PUT` with a condition returning text`` () =
  let webApp =
    <@ 
        PUT >=> 
          route "/seconds" >=> 
            if DateTime.Now.Second % 2 = 0 then text "Seconds are odd" else  text "Seconds are even" 
    @>

  let ctx = analyze webApp AppAnalyzeRules.Default

  let exp = 
    { Verb="PUT"
      Path="/seconds"
      Responses=
        [
          { StatusCode=200
            ContentType="text/plain"
            ModelType=(typeof<string>) }
        ]
    }
  let route = !ctx.Routes |> Seq.exactlyOne
  
  Assert.Equal(exp.Path, route.Path)
  Assert.Equal(exp.Verb, route.Verb)
  Assert.Equal(exp.Responses.[0], route.Responses.[0])
  Assert.Equal(1, route.Responses.Length)
  
