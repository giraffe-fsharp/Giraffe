module AspNetCore.Lambda.FormatExpressionTests

open System
open Xunit
open AspNetCore.Lambda.FormatExpressions

let assertFail msg = Assert.True(false, msg)

[<Fact>]
let ``Simple matching format string returns correct tuple`` () =
    tryMatchInput "/foo/%s/bar/%c/%b/test/%d" "/foo/john/bar/M/true/test/123"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (s1 : string, 
                c1 : char,
                b1 : bool,
                d1 : int) ->
            Assert.Equal("john", s1)
            Assert.Equal('M', c1)
            Assert.True(b1)
            Assert.Equal(123, d1)

[<Fact>]
let ``Format string with escaped "%" returns correct tuple`` () =
    tryMatchInput "/foo/%%s/%%%s/%c/%b/test/%d" "/foo/%s/%bar/M/true/test/123"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (s1 : string, 
                c1 : char,
                b1 : bool,
                d1 : int) ->
            Assert.Equal("bar", s1)
            Assert.Equal('M', c1)
            Assert.True(b1)
            Assert.Equal(123, d1)