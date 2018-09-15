module Giraffe.Tests.FormatExpressionTests

open System
open Xunit
open Giraffe.FormatExpressions

// ---------------------------------
// Positive Tests
// ---------------------------------

[<Fact>]
let ``Simple matching format string returns correct tuple`` () =
    tryMatchInput "/foo/%s/bar/%c/%b/test/%i" "/foo/john/bar/M/true/test/123" false
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
    tryMatchInput "/foo/%%s/%%%s/%c/%b/test/%i" "/foo/%s/%bar/M/true/test/123" false
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
    tryMatchInput "/foo/(.+)/%s/bar/%d/(.+)" "/foo/(.+)/!£$%^&*(/bar/-345/(.+)" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (s1 : string,
                d1 : int64) ->
            Assert.Equal("!£$%^&*(", s1)
            Assert.Equal(-345L, d1)

[<Fact>]
let ``Format string with single "%s" matches a single string`` () =
    tryMatchInput "%s" "hello world !!" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (s : string) -> Assert.Equal("hello world !!", s)

[<Fact>]
let ``Format string with single "%b" matches "true"`` () =
    tryMatchInput "%b" "true" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b1 : bool) -> Assert.True(b1)

[<Fact>]
let ``Format string with single "%b" matches "false"`` () =
    tryMatchInput "%b" "false" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.False(b)

[<Fact>]
let ``Format string with single "%b" matches "TRUE"`` () =
    tryMatchInput "%b" "TRUE" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.True(b)

[<Fact>]
let ``Format string with single "%b" matches "FALSE"`` () =
    tryMatchInput "%b" "FALSE" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.False(b)

[<Fact>]
let ``Format string with single "%b" matches "True"`` () =
    tryMatchInput "%b" "True" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.True(b)

[<Fact>]
let ``Format string with single "%b" matches "False"`` () =
    tryMatchInput "%b" "False" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.False(b)

[<Fact>]
let ``Format string with single "%b" matches "tRuE"`` () =
    tryMatchInput "%b" "tRuE" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (b : bool) -> Assert.True(b)

[<Fact>]
let ``Format string with single "%i" matches "0"`` () =
    tryMatchInput "%i" "0" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (i : int) -> Assert.Equal(0, i)

[<Fact>]
let ``Format string with single "%i" matches int32 min value`` () =
    tryMatchInput "%i" "-2147483648" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (i : int) -> Assert.Equal(Int32.MinValue, i)

[<Fact>]
let ``Format string with single "%i" matches int32 max value`` () =
    tryMatchInput "%i" "2147483647" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (i : int) -> Assert.Equal(Int32.MaxValue, i)

[<Fact>]
let ``Format string with single "%d" matches "0"`` () =
    tryMatchInput "%d" "0" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (d : int64) -> Assert.Equal(0L, d)

[<Fact>]
let ``Format string with single "%d" matches int64 min value`` () =
    tryMatchInput "%d" "-9223372036854775808" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (d : int64) -> Assert.Equal(Int64.MinValue, d)

[<Fact>]
let ``Format string with single "%d" matches int64 max value`` () =
    tryMatchInput "%d" "9223372036854775807" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (d : int64) -> Assert.Equal(Int64.MaxValue, d)

[<Fact>]
let ``Format string with single "%f" matches "0.0"`` () =
    tryMatchInput "%f" "0.0" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (f : float) -> Assert.Equal(0.0, f)

[<Fact>]
let ``Format string with single "%f" matches "0.5"`` () =
    tryMatchInput "%f" "0.5" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (f : float) -> Assert.Equal(0.5, f)

[<Fact>]
let ``Format string with single "%f" matches "100500.7895"`` () =
    tryMatchInput "%f" "100500.7895" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (f : float) -> Assert.Equal(100500.7895, f)

[<Fact>]
let ``Format string with single "%f" matches "-45.342"`` () =
    tryMatchInput "%f" "-45.342" false
    |> function
        | None -> assertFail "Format failed to match input."
        | Some (f : float) -> Assert.Equal(-45.342, f)

[<Fact>]
let ``Format string with single "%O" matches "00000000-0000-0000-0000-000000000000"`` () =
    tryMatchInput "%O" "00000000-0000-0000-0000-000000000000" false
    |> function
        | None   -> assertFail "Format failed to match input."
        | Some g -> Assert.Equal(Guid.Empty, g)

[<Fact>]
let ``Format string with single "%O" matches "FE9CFE19-35D4-4EDC-9A95-5D38C4D579BD"`` () =
    tryMatchInput "%O" "FE9CFE19-35D4-4EDC-9A95-5D38C4D579BD" false
    |> function
        | None   -> assertFail "Format failed to match input."
        | Some g -> Assert.Equal(Guid("FE9CFE19-35D4-4EDC-9A95-5D38C4D579BD"), g)

[<Fact>]
let ``Format string with single "%O" matches "00000000000000000000000000000000"`` () =
    tryMatchInput "%O" "00000000000000000000000000000000" false
    |> function
        | None   -> assertFail "Format failed to match input."
        | Some g -> Assert.Equal(Guid.Empty, g)

[<Fact>]
let ``Format string with single "%O" matches "FE9CFE1935D44EDC9A955D38C4D579BD"`` () =
    tryMatchInput "%O" "FE9CFE1935D44EDC9A955D38C4D579BD" false
    |> function
        | None   -> assertFail "Format failed to match input."
        | Some g -> Assert.Equal(Guid("FE9CFE19-35D4-4EDC-9A95-5D38C4D579BD"), g)

[<Fact>]
let ``Format string with single "%O" matches "Xy0MVKupFES9NpmZ9TiHcw"`` () =
    tryMatchInput "%O" "Xy0MVKupFES9NpmZ9TiHcw" false
    |> function
        | None   -> assertFail "Format failed to match input."
        | Some g -> Assert.Equal(Guid("540c2d5f-a9ab-4414-bd36-9999f5388773"), g)

[<Fact>]
let ``Format string with single "%u" matches "FOwfPLe6waQ"`` () =
    tryMatchInput "%u" "FOwfPLe6waQ" false
    |> function
        | None    -> assertFail "Format failed to match input."
        | Some id -> Assert.Equal(1507614320903242148UL, id)

[<Fact>]
let ``Format string with "%s" matches url encoded string`` () =
    tryMatchInput "/encode/%s" "/encode/a%2fb%2Bc.d%2Ce" false
    |> function
        | None              -> assertFail "Format failed to match input."
        | Some (s : string) -> Assert.Equal("a/b%2Bc.d%2Ce", s)

// ---------------------------------
// Negative Tests
// ---------------------------------

[<Fact>]
let ``Format string with single "%s" doesn't matches empty string`` () =
    tryMatchInput "%s" "" false
    |> function
        | None -> ()
        | Some _ -> assertFail "Should not have matched string"

[<Fact>]
let ``Format string with single "%i" doesn't match int32 max value + 1`` () =
    tryMatchInput "%i" "2147483648" false
    |> function
        | None -> ()
        | Some _ -> assertFail "Should not have matched string"

[<Fact>]
let ``Format string with single "%f" doesn't match "0"`` () =
    tryMatchInput "%f" "0" false
    |> function
        | None -> ()
        | Some _ -> assertFail "Should not have matched string"