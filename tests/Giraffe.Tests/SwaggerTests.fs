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

let assertThat (cmp:bool) =
  Assert.True cmp

// ---------------------------------
// Tests
// ---------------------------------

[<Fact>]
let ``webapp is a simple route returning text`` () =
  let webApp =
    <@ GET >=> route "/home" >=> text "Home." @>

  let ctx = AnalyzeContext.Empty
  analyze webApp ctx HttpMethods.Get
  let routes = !ctx.Routes
  let expRoutes = 
    [ "GET", "/home" ]
  assertThat (routes = expRoutes)

