# Response Caching App

The purpose of this sample is to show how one can configure the Giraffe server to use the ASP.NET [response caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/response?view=aspnetcore-7.0) feature. Notice that we're leveraging the middlewares which are offered by Giraffe.

You can find their documentation here: [Giraffe Docs - Response Caching](https://giraffe.wiki/docs#response-caching).

## How to test

First, start the server at the terminal using:

```bash
# Assuming that you're at the top level of this repository
dotnet run --project samples/ResponseCachingApp/

# It will start the server listening to port 5000
```

Now, you can use the `test-run.sh` script (Linux):

```bash
# Add execution permission to the script
chmod +x samples/ResponseCachingApp/test-run.sh

./samples/ResponseCachingApp/test-run.sh
```

And the expected result:

```bash
# -----------------------------------
# Testing the /cached/not endpoint

# Hello World -> DateTime: 8/29/2023 8:00:28 PM
# Hello World -> DateTime: 8/29/2023 8:00:34 PM
# Hello World -> DateTime: 8/29/2023 8:00:40 PM
# Hello World -> DateTime: 8/29/2023 8:00:46 PM
# Hello World -> DateTime: 8/29/2023 8:00:52 PM

# real	0m30,126s
# user	0m0,063s
# sys	0m0,052s
# -----------------------------------
# Testing the /cached/public endpoint

# Hello World -> DateTime: 8/29/2023 8:00:58 PM
# Hello World -> DateTime: 8/29/2023 8:00:58 PM
# Hello World -> DateTime: 8/29/2023 8:00:58 PM
# Hello World -> DateTime: 8/29/2023 8:00:58 PM
# Hello World -> DateTime: 8/29/2023 8:00:58 PM

# real	0m10,072s
# user	0m0,025s
# sys	0m0,040s
# -----------------------------------
# Testing the /cached/private endpoint

# Hello World -> DateTime: 8/29/2023 8:01:09 PM
# Hello World -> DateTime: 8/29/2023 8:01:15 PM
# Hello World -> DateTime: 8/29/2023 8:01:21 PM
# Hello World -> DateTime: 8/29/2023 8:01:27 PM
# Hello World -> DateTime: 8/29/2023 8:01:33 PM

# real	0m30,120s
# user	0m0,052s
# sys	0m0,060s
```

Notice that at this example, the cache worked only for the `/cached/public` endpoint, as expected. You can read the documentation presented before to understand why.

One last information, notice that the server will inform whenever the response was cached or not, just check the logs.