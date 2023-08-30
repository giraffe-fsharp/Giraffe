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

# Hello World -> DateTime: 8/30/2023 9:10:01 AM
# Hello World -> DateTime: 8/30/2023 9:10:07 AM
# Hello World -> DateTime: 8/30/2023 9:10:13 AM
# Hello World -> DateTime: 8/30/2023 9:10:19 AM
# Hello World -> DateTime: 8/30/2023 9:10:25 AM

# real	0m30,110s
# user	0m0,034s
# sys	0m0,067s
# -----------------------------------
# Testing the /cached/public endpoint

# Hello World -> DateTime: 8/30/2023 9:10:31 AM
# Hello World -> DateTime: 8/30/2023 9:10:31 AM
# Hello World -> DateTime: 8/30/2023 9:10:31 AM
# Hello World -> DateTime: 8/30/2023 9:10:31 AM
# Hello World -> DateTime: 8/30/2023 9:10:31 AM

# real	0m10,116s
# user	0m0,043s
# sys	0m0,060s
# -----------------------------------
# Testing the /cached/private endpoint

# Hello World -> DateTime: 8/30/2023 9:10:41 AM
# Hello World -> DateTime: 8/30/2023 9:10:47 AM
# Hello World -> DateTime: 8/30/2023 9:10:53 AM
# Hello World -> DateTime: 8/30/2023 9:10:59 AM
# Hello World -> DateTime: 8/30/2023 9:11:05 AM

# real	0m30,144s
# user	0m0,031s
# sys	0m0,082s
# -----------------------------------
# Testing the /cached/vary/not endpoint

# Parameters: query1 a query2 b -> DateTime: 8/30/2023 9:11:11 AM
# Parameters: query1 a query2 b -> DateTime: 8/30/2023 9:11:11 AM
# Parameters: query1 a query2 b -> DateTime: 8/30/2023 9:11:11 AM
# Parameters: query1 a query2 b -> DateTime: 8/30/2023 9:11:11 AM

# real	0m9,109s
# user	0m0,052s
# sys	0m0,053s
# -----------------------------------
# Testing the /cached/vary/yes endpoint

# Parameters: query1 a query2 b -> DateTime: 8/30/2023 9:11:21 AM
# Parameters: query1 a query2 b -> DateTime: 8/30/2023 9:11:21 AM
# Parameters: query1 c query2 d -> DateTime: 8/30/2023 9:11:28 AM
# Parameters: query1 c query2 d -> DateTime: 8/30/2023 9:11:28 AM

# real	0m14,105s
# user	0m0,043s
# sys	0m0,056s
```

Notice that at this example, the cache worked properly only for the `/cached/public` and `/cached/vary/yes` endpoints, as expected. You can read the documentation presented before to understand why.

One last information, notice that the server will inform whenever the response was cached or not, just check the logs.