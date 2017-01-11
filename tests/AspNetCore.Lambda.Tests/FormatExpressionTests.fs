module AspNetCore.Lambda.FormatExpressionTests

open System
open Xunit
open AspNetCore.Lambda.FormatExpressions

let assertFail msg = Assert.True(false, msg)

[<Fact>]
let ``Simple matching format string returns correct tuple`` () =
    tryMatchInput "/foo/%s/bar/%c/%b/test/%i" "/foo/john/bar/M/true/test/123"
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
    tryMatchInput "/foo/%%s/%%%s/%c/%b/test/%i" "/foo/%s/%bar/M/true/test/123"
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

[<Fact>]
let ``Format string with single "%s" matches a single string`` () =
    tryMatchInput "%s" "hello world !!"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (s : string) -> Assert.Equal("hello world !!", s)

[<Theory>]
let ``Format string with single "%b" matches case insensitive bool values`` () =
    tryMatchInput "%b" "true"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b1 : bool) -> Assert.True(b1)

    tryMatchInput "%b" "false"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.False(b)

    tryMatchInput "%b" "TRUE"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.True(b)

    tryMatchInput "%b" "FALSE"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.False(b)

    tryMatchInput "%b" "True"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.True(b)

    tryMatchInput "%b" "False"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.False(b)

[<Theory>]
let ``Format string with single "%i" matches any valid int32 value`` () =
    tryMatchInput "%i" "0"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (i : int) -> Assert.Equal(0, i)
        
    tryMatchInput "%i" "-2147483648"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (i : int) -> Assert.Equal(Int32.MinValue, i)
        
    tryMatchInput "%i" "2147483647"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (i : int) -> Assert.Equal(Int32.MaxValue, i)

[<Theory>]
let ``Format string with single "%d" matches any valid int64 value`` () =
    tryMatchInput "%d" "0"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (i : int64) -> Assert.Equal(0L, i)
        
    tryMatchInput "%d" "-9223372036854775808"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (i : int64) -> Assert.Equal(Int64.MinValue, i)
        
    tryMatchInput "%d" "9223372036854775807"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (i : int64) -> Assert.Equal(Int64.MaxValue, i)