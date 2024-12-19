# Rate Limiting Sample

This sample project shows how one can configure ASP.NET's built-in [rate limiting middleware](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-8.0).

Notice that this rate limiting configuration is very simple, and for real life scenarios you'll need to figure out what is the best strategy to use for your server.

To make it easier to test this project locally, and see the rate limiting middleware working, you can use the `rate-limiting-test.fsx` script:

```bash
# start the server
dotnet run .
# if you want to keep using the same terminal, just start this process in the background

# then, you can use this script to test the server, and confirm that the rate-limiting
# middleware is really working
dotnet fsi rate-limiting-test.fsx

# to run with the DEBUG flag active
dotnet fsi --define:DEBUG rate-limiting-test.fsx
```