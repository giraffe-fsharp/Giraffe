module Giraffe.Tests.DateTimeTests

open System
open Xunit
open Giraffe

// ---------------------------------
// DateTime Tests
// ---------------------------------

[<Fact>]
let ``DateTime.ToHtmlString() produces a RFC822 formatted string`` () =
    let htmlString = (new DateTime(2019, 01, 01, 0, 0, 0, 0, DateTimeKind.Utc)).ToHtmlString()
    Assert.Equal("Tue, 01 Jan 2019 00:00:00 GMT", htmlString)

[<Fact>]
let ``DateTime.ToIsoString() produces a RFC3339 formatted string`` () =
    let isoString = (new DateTime(2019, 01, 01, 0, 0, 0, 0, DateTimeKind.Utc)).ToIsoString()
    Assert.Equal("2019-01-01T00:00:00.0000000Z", isoString)
    
[<Fact>]
let ``DateTimeOffset.ToHtmlString() produces a RFC822 formatted string`` () =
    let htmlString = (new DateTimeOffset(2019, 01, 01, 0, 0, 0, 0, TimeSpan.Zero)).ToHtmlString()
    Assert.Equal("Tue, 01 Jan 2019 00:00:00 GMT", htmlString)
    
[<Fact>]
let ``DateTimeOffset.ToIsoString() produces a RFC3339 formatted string`` () =
    let isoString = (new DateTimeOffset(2019, 01, 01, 0, 0, 0, 0, TimeSpan.Zero)).ToIsoString()
    Assert.Equal("2019-01-01T00:00:00.0000000+00:00", isoString)
    