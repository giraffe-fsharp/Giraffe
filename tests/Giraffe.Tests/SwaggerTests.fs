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

let assertMapEquals (m1:Map<'k,'v>) (m2:Map<'k,'v>) =
  Assert.Equal(m1.Count, m2.Count)
  for kv in m1 do
    Assert.Equal(box kv.Value, box m2.[kv.Key])

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
      Parameters=Map.empty
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
      Parameters=Map.empty
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
      Parameters=Map.empty
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
      Parameters=Map.empty
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
      Parameters=Map.empty
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
      Parameters=Map.empty
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
let ``webapp is a simple routef with verb `GET` returning text and handler inner quotation`` () =
  let webApp =
    <@ GET >=> routef "/hello/%s" (fun name -> text "Home.") @>
  let ctx = analyze webApp AppAnalyzeRules.Default
  let exp = 
    { Verb = "GET"
      Path = "/hello/%s"
      Parameters = Map [ "arg0", typeof<string> ]
      Responses =
        [
          { StatusCode = 200
            ContentType = "text/plain"
            ModelType = typeof<string> }
        ]
    }
  let route = !ctx.Routes |> Seq.exactlyOne
  
  Assert.Equal(exp.Path, route.Path)
  Assert.Equal(exp.Verb, route.Verb)
  Assert.Equal(exp.Responses.[0], route.Responses.[0])
  assertMapEquals exp.Parameters route.Parameters
  
[<Fact>]
let ``routef with verb `GET` and args [int, string, float] returning text and handler inner quotation`` () =
  let webApp =
    <@ GET >=> routef "/hello/%d/%s/%f" (fun (age, name, price) -> text "Home.") @>
  let ctx = analyze webApp AppAnalyzeRules.Default
  let exp = 
    { Verb = "GET"
      Path = "/hello/%d/%s/%f"
      Parameters = Map
                    [ "arg0", typeof<int> 
                      "arg1", typeof<string>
                      "arg2", typeof<float> ]
      Responses =
        [
          { StatusCode = 200
            ContentType = "text/plain"
            ModelType = typeof<string> }
        ]
    }
  let route = !ctx.Routes |> Seq.exactlyOne
  
  Assert.Equal(exp.Path, route.Path)
  Assert.Equal(exp.Verb, route.Verb)
  Assert.Equal(exp.Responses.[0], route.Responses.[0])
  
  assertMapEquals exp.Parameters route.Parameters

  
[<Fact>]
let ``app contains 1 route and 1 routef in GET`` () =
  let webApp =
    <@
      choose [ 
        GET >=> 
          choose [
              routef "/hello/%d/%s/%f" (fun (age, name, price) -> text "Home.")
              route "/home" >=> text "Home." 
            ]
      ] @>
    
  //failwithf "webapp %A" webApp
    
  let ctx = analyze webApp AppAnalyzeRules.Default
  let exp =
    [
     { Verb = "GET"
       Path = "/hello/%d/%s/%f"
       Parameters = Map
                     [ "arg0", typeof<int> 
                       "arg1", typeof<string>
                       "arg2", typeof<float> ]
       Responses =
         [
           { StatusCode = 200
             ContentType = "text/plain"
             ModelType = typeof<string> }
         ]
     }
     { Verb="GET"
       Path="/home"
       Parameters=Map.empty
       Responses=
         [
           { StatusCode=200
             ContentType="text/plain"
             ModelType=(typeof<string>) }
         ]
     }
    ]
  let routes = !ctx.Routes
  Assert.Equal(exp.Length, routes.Length)
  for route in routes do
    exp |> List.contains route |> assertThat

[<Fact>]
let ``app contains 1 route and 1 routef in GET and POST`` () =
  let webApp =
    <@
      choose [ 
        GET >=> routef "/hello/%d/%s/%f" (fun (age, name, price) -> text "Home.")
        POST >=> route "/home" >=> text "Home."
      ] @>
    
  let ctx = analyze webApp AppAnalyzeRules.Default
  let exp =
    [
     { Verb = "GET"
       Path = "/hello/%d/%s/%f"
       Parameters = Map
                     [ "arg0", typeof<int> 
                       "arg1", typeof<string>
                       "arg2", typeof<float> ]
       Responses =
         [
           { StatusCode = 200
             ContentType = "text/plain"
             ModelType = typeof<string> }
         ]
     }
     { Verb="POST"
       Path="/home"
       Parameters=Map.empty
       Responses=
         [
           { StatusCode=200
             ContentType="text/plain"
             ModelType=(typeof<string>) }
         ]
     }
    ]
  let routes = !ctx.Routes
  
  Assert.Equal(routes.Length, exp.Length)
  for route in routes do
    exp |> List.contains route |> assertThat
  
