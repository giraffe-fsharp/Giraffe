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

// ---------------------------------
// Helper functions
// ---------------------------------

let assertThat (cmp:bool) =
  Assert.True cmp

// ---------------------------------
// Test Types
// ---------------------------------

type Dummy =
    {
        Foo : string
        Bar : string
        Age : int
    }


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
  
[<Fact>]
let ``webapp is a simple route with verb `DELETE` with a condition returning text or json`` () =
  let webApp =
    <@ 
        DELETE >=> 
          route "/seconds" >=> 
            if DateTime.Now.Second % 2 = 0 
            then text "Seconds are odd" 
            else json { Foo = "foo"; Bar = "bar"; Age = 32 } 
    @>

  let ctx = analyze webApp AppAnalyzeRules.Default

  let exp = 
    { Verb="DELETE"
      Path="/seconds"
      Responses=
        [
          { StatusCode=200
            ContentType="application/json"
            ModelType=(typeof<Dummy>) }
          
          { StatusCode=200
            ContentType="text/plain"
            ModelType=(typeof<string>) }
        ]
    }
  let route = !ctx.Routes |> Seq.exactlyOne
  
  let ss = sprintf "%A" webApp
  printfn "ss: %s" ss
  
  Assert.Equal(exp.Path, route.Path)
  Assert.Equal(exp.Verb, route.Verb)
  Assert.Equal(exp.Responses.[0], route.Responses.[0])
  Assert.Equal(2, route.Responses.Length)
  
[<Fact>]
let ``webapp is a simple route with verb `POST` with a more complex condition returning text or json`` () =

  let externalValue = "Bonjour"

  let webApp =
    <@ 
        POST >=> 
          route "/swagger/is/cool" >=> 
            if DateTime.Now.Second % 2 = 0 
            then
              if externalValue.Equals "bonjour" then text "Bonjour! Seconds are odd"
              elif externalValue.Equals "au revoir" then text "Bye! Seconds are odd"
              else text "Seconds are odd"
            else json { Foo = "foo"; Bar = "bar"; Age = 32 } 
    @>

  let ctx = analyze webApp AppAnalyzeRules.Default

  let exp = 
    { Verb="POST"
      Path="/swagger/is/cool"
      Responses=
        [
          { StatusCode=200
            ContentType="application/json"
            ModelType=(typeof<Dummy>) }
          
          { StatusCode=200
            ContentType="text/plain"
            ModelType=(typeof<string>) }
        ]
    }
  let route = !ctx.Routes |> Seq.exactlyOne
  
  let ss = sprintf "%A" webApp
  printfn "ss: %s" ss
  
  Assert.Equal(exp.Path, route.Path)
  Assert.Equal(exp.Verb, route.Verb)
  Assert.Equal(exp.Responses.[0], route.Responses.[0])
  Assert.Equal(2, route.Responses.Length)
  Assert.Equal(0, (!ctx.ArgTypes).Length)


[<Fact>]
let ``webapp is a simple routeCi with verb `GET` returning text`` () =
  let webApp =
    <@ GET >=> routeCi "/home" >=> text "Home." @>

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

