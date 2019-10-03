module Giraffe.Tests.FormatExpressionTests

open System
open Xunit
open Giraffe.FormatExpressions

// ---------------------------------
// Positive Tests
// ---------------------------------

[<Fact>]
let ``Simple matching format string returns correct tuple`` () =
    tryMatchInputExact "/foo/%s/bar/%c/%b/test/%i" false "/foo/john/bar/M/true/test/123"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (s1 : string,
                c1 : char,
                b1 : bool,
                i1 : int) ->
            Assert.Equal("john", s1)
            Assert.Equal('M', c1)
            Assert.True(b1)
            Assert.Equal(123, i1)

[<Fact>]
let ``Format string with escaped "%" returns correct tuple`` () =
    tryMatchInputExact "/foo/%%s/%%%s/%c/%b/test/%i" false "/foo/%s/%bar/M/true/test/123"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (s1 : string,
                c1 : char,
                b1 : bool,
                i1 : int) ->
            Assert.Equal("bar", s1)
            Assert.Equal('M', c1)
            Assert.True(b1)
            Assert.Equal(123, i1)

[<Fact>]
let ``Format string with regex symbols returns correct tuple`` () =
    tryMatchInputExact "/foo/(.+)/%s/bar/%d/(.+)" false "/foo/(.+)/!£$%^&*(/bar/-345/(.+)"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (s1 : string,
                d1 : int64) ->
            Assert.Equal("!£$%^&*(", s1)
            Assert.Equal(-345L, d1)

[<Fact>]
let ``Format string with single "%s" matches a single string`` () =
    tryMatchInputExact "%s" false "hello world !!"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (s : string) -> Assert.Equal("hello world !!", s)

[<Fact>]
let ``Format string with single "%b" matches "true"`` () =
    tryMatchInputExact "%b" false "true"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b1 : bool) -> Assert.True(b1)

[<Fact>]
let ``Format string with single "%b" matches "false"`` () =
    tryMatchInputExact "%b" false "false"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.False(b)

[<Fact>]
let ``Format string with single "%b" matches "TRUE"`` () =
    tryMatchInputExact "%b" false "TRUE"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.True(b)

[<Fact>]
let ``Format string with single "%b" matches "FALSE"`` () =
    tryMatchInputExact "%b" false "FALSE"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.False(b)

[<Fact>]
let ``Format string with single "%b" matches "True"`` () =
    tryMatchInputExact "%b" false "True"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.True(b)

[<Fact>]
let ``Format string with single "%b" matches "False"`` () =
    tryMatchInputExact "%b" false "False"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.False(b)

[<Fact>]
let ``Format string with single "%b" matches "tRuE"`` () =
    tryMatchInputExact "%b" false "tRuE"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.True(b)

[<Fact>]
let ``Format string with single "%i" matches "0"`` () =
    tryMatchInputExact "%i" false "0"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (i : int) -> Assert.Equal(0, i)

[<Fact>]
let ``Format string with single "%i" matches int32 min value`` () =
    tryMatchInputExact "%i" false "-2147483648"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (i : int) -> Assert.Equal(Int32.MinValue, i)

[<Fact>]
let ``Format string with single "%i" matches int32 max value`` () =
    tryMatchInputExact "%i" false "2147483647"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (i : int) -> Assert.Equal(Int32.MaxValue, i)

[<Fact>]
let ``Format string with single "%d" matches "0"`` () =
    tryMatchInputExact "%d" false "0"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (d : int64) -> Assert.Equal(0L, d)

[<Fact>]
let ``Format string with single "%d" matches int64 min value`` () =
    tryMatchInputExact "%d" false "-9223372036854775808"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (d : int64) -> Assert.Equal(Int64.MinValue, d)

[<Fact>]
let ``Format string with single "%d" matches int64 max value`` () =
    tryMatchInputExact "%d" false "9223372036854775807"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (d : int64) -> Assert.Equal(Int64.MaxValue, d)

[<Fact>]
let ``Format string with single "%f" matches "0.0"`` () =
    tryMatchInputExact "%f" false "0.0"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (f : float) -> Assert.Equal(0.0, f)

[<Fact>]
let ``Format string with single "%f" matches "0.5"`` () =
    tryMatchInputExact "%f" false "0.5"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (f : float) -> Assert.Equal(0.5, f)

[<Fact>]
let ``Format string with single "%f" matches "100500.7895"`` () =
    tryMatchInputExact "%f" false "100500.7895"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (f : float) -> Assert.Equal(100500.7895, f)

[<Fact>]
let ``Format string with single "%f" matches "-45.342"`` () =
    tryMatchInputExact "%f" false "-45.342"
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (f : float) -> Assert.Equal(-45.342, f)

[<Fact>]
let ``Format string with single "%O" matches "00000000-0000-0000-0000-000000000000"`` () =
    tryMatchInputExact "%O" false "00000000-0000-0000-0000-000000000000"
    |> function
        | None   -> assertFail "Format failed to match input."
        | Some g -> Assert.Equal(Guid.Empty, g)

[<Fact>]
let ``Format string with single "%O" matches "FE9CFE19-35D4-4EDC-9A95-5D38C4D579BD"`` () =
    tryMatchInputExact "%O" false "FE9CFE19-35D4-4EDC-9A95-5D38C4D579BD"
    |> function
        | None   -> assertFail "Format failed to match input."
        | Some g -> Assert.Equal(Guid("FE9CFE19-35D4-4EDC-9A95-5D38C4D579BD"), g)

[<Fact>]
let ``Format string with single "%O" matches "00000000000000000000000000000000"`` () =
    tryMatchInputExact "%O" false "00000000000000000000000000000000"
    |> function
        | None   -> assertFail "Format failed to match input."
        | Some g -> Assert.Equal(Guid.Empty, g)

[<Fact>]
let ``Format string with single "%O" matches "FE9CFE1935D44EDC9A955D38C4D579BD"`` () =
    tryMatchInputExact "%O" false "FE9CFE1935D44EDC9A955D38C4D579BD"
    |> function
        | None   -> assertFail "Format failed to match input."
        | Some g -> Assert.Equal(Guid("FE9CFE19-35D4-4EDC-9A95-5D38C4D579BD"), g)

[<Fact>]
let ``Format string with single "%O" matches "Xy0MVKupFES9NpmZ9TiHcw"`` () =
    tryMatchInputExact "%O" false "Xy0MVKupFES9NpmZ9TiHcw"
    |> function
        | None   -> assertFail "Format failed to match input."
        | Some g -> Assert.Equal(Guid("540c2d5f-a9ab-4414-bd36-9999f5388773"), g)

[<Fact>]
let ``Format string with single "%u" matches "FOwfPLe6waQ"`` () =
    tryMatchInputExact "%u" false "FOwfPLe6waQ"
    |> function
        | None    -> assertFail "Format failed to match input."
        | Some id -> Assert.Equal(1507614320903242148UL, id)

[<Fact>]
let ``Format string with "%s" matches url encoded string`` () =
    tryMatchInputExact "/encode/%s" false "/encode/a%2fb%2Bc.d%2Ce"
    |> function
        | None              -> assertFail "Format failed to match input."
        | Some (s : string) -> Assert.Equal("a/b%2Bc.d%2Ce", s)

// ---------------------------------
// Negative Tests
// ---------------------------------

[<Fact>]
let ``Format string with single "%s" doesn't matches empty string`` () =
    tryMatchInputExact "%s" false ""
    |> function
        | None -> ()
        | Some _ -> assertFail "Should not have matched string"

[<Fact>]
let ``Format string with single "%i" doesn't match int32 max value + 1`` () =
    tryMatchInputExact "%i" false "2147483648"
    |> function
        | None -> ()
        | Some _ -> assertFail "Should not have matched string"

[<Fact>]
let ``Format string with single "%f" doesn't match "0"`` () =
    tryMatchInputExact "%f" false "0"
    |> function
        | None -> ()
        | Some _ -> assertFail "Should not have matched string"