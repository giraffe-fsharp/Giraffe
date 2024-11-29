# Response Caching App

The purpose of this sample is to show how one can configure the Giraffe server to use the ASP.NET [response caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/response?view=aspnetcore-7.0) feature. Notice that we're leveraging the middlewares which are offered by Giraffe.

You can find their documentation here: [Giraffe Docs - Response Caching](https://giraffe.wiki/docs#response-caching).

+ Update november/2024: Adding a [file server](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files#usefileserver-for-default-documents) feature. You can use it checking the endpoint `http://localhost:5000/assets/main.css` after starting the web server.

## How to test

First, start the server at the terminal using:

```bash
# Assuming that you're at the top level of this repository
dotnet run --project samples/ResponseCachingApp/

# It will start the server listening to port 5000
```

Now, you can use the `test-run.fsx` script:

```bash
dotnet fsi samples/ResponseCachingApp/test-run.fsx
```

And the expected result:

```bash
# -----------------------------------
# Testing the /cached/not endpoint

# Sending request GET http://localhost:5000/cached/not ...
# 200 (OK) (GET http://localhost:5000/cached/not)
# Hello World -> DateTime: 8/30/2023 5:06:00 PM

# Sending request GET http://localhost:5000/cached/not ...
# 200 (OK) (GET http://localhost:5000/cached/not)
# Hello World -> DateTime: 8/30/2023 5:06:06 PM

# Sending request GET http://localhost:5000/cached/not ...
# 200 (OK) (GET http://localhost:5000/cached/not)
# Hello World -> DateTime: 8/30/2023 5:06:12 PM

# Sending request GET http://localhost:5000/cached/not ...
# 200 (OK) (GET http://localhost:5000/cached/not)
# Hello World -> DateTime: 8/30/2023 5:06:18 PM

# Sending request GET http://localhost:5000/cached/not ...
# 200 (OK) (GET http://localhost:5000/cached/not)
# Hello World -> DateTime: 8/30/2023 5:06:24 PM

# The time it took to finish:
# 30.47 seconds

# -----------------------------------
# Testing the /cached/public endpoint

# Sending request GET http://localhost:5000/cached/public ...
# 200 (OK) (GET http://localhost:5000/cached/public)
# Hello World -> DateTime: 8/30/2023 5:06:30 PM

# Sending request GET http://localhost:5000/cached/public ...
# 200 (OK) (GET http://localhost:5000/cached/public)
# Hello World -> DateTime: 8/30/2023 5:06:30 PM

# Sending request GET http://localhost:5000/cached/public ...
# 200 (OK) (GET http://localhost:5000/cached/public)
# Hello World -> DateTime: 8/30/2023 5:06:30 PM

# Sending request GET http://localhost:5000/cached/public ...
# 200 (OK) (GET http://localhost:5000/cached/public)
# Hello World -> DateTime: 8/30/2023 5:06:30 PM

# Sending request GET http://localhost:5000/cached/public ...
# 200 (OK) (GET http://localhost:5000/cached/public)
# Hello World -> DateTime: 8/30/2023 5:06:30 PM

# The time it took to finish:
# 10.29 seconds

# -----------------------------------
# Testing the /cached/private endpoint

# Sending request GET http://localhost:5000/cached/private ...
# 200 (OK) (GET http://localhost:5000/cached/private)
# Hello World -> DateTime: 8/30/2023 5:06:40 PM

# Sending request GET http://localhost:5000/cached/private ...
# 200 (OK) (GET http://localhost:5000/cached/private)
# Hello World -> DateTime: 8/30/2023 5:06:46 PM

# Sending request GET http://localhost:5000/cached/private ...
# 200 (OK) (GET http://localhost:5000/cached/private)
# Hello World -> DateTime: 8/30/2023 5:06:53 PM

# Sending request GET http://localhost:5000/cached/private ...
# 200 (OK) (GET http://localhost:5000/cached/private)
# Hello World -> DateTime: 8/30/2023 5:06:59 PM

# Sending request GET http://localhost:5000/cached/private ...
# 200 (OK) (GET http://localhost:5000/cached/private)
# Hello World -> DateTime: 8/30/2023 5:07:05 PM

# The time it took to finish:
# 30.37 seconds

# -----------------------------------
# Testing the /cached/vary/not endpoint

# Sending request GET http://localhost:5000/cached/vary/not?query1=a&query2=b ...
# 200 (OK) (GET http://localhost:5000/cached/vary/not?query1=a&query2=b)
# Parameters: query1 a query2 b -> DateTime: 8/30/2023 5:07:11 PM

# Sending request GET http://localhost:5000/cached/vary/not?query1=a&query2=b ...
# 200 (OK) (GET http://localhost:5000/cached/vary/not?query1=a&query2=b)
# Parameters: query1 a query2 b -> DateTime: 8/30/2023 5:07:11 PM

# Sending request GET http://localhost:5000/cached/vary/not?query1=c&query2=d ...
# 200 (OK) (GET http://localhost:5000/cached/vary/not?query1=c&query2=d)
# Parameters: query1 a query2 b -> DateTime: 8/30/2023 5:07:11 PM

# Sending request GET http://localhost:5000/cached/vary/not?query1=c&query2=d ...
# 200 (OK) (GET http://localhost:5000/cached/vary/not?query1=c&query2=d)
# Parameters: query1 a query2 b -> DateTime: 8/30/2023 5:07:11 PM

# The time it took to finish:
# 9.22 seconds

# -----------------------------------
# Testing the /cached/vary/yes endpoint

# Sending request GET http://localhost:5000/cached/vary/yes?query1=a&query2=b ...
# 200 (OK) (GET http://localhost:5000/cached/vary/yes?query1=a&query2=b)
# Parameters: query1 a query2 b -> DateTime: 8/30/2023 5:07:20 PM

# Sending request GET http://localhost:5000/cached/vary/yes?query1=a&query2=b ...
# 200 (OK) (GET http://localhost:5000/cached/vary/yes?query1=a&query2=b)
# Parameters: query1 a query2 b -> DateTime: 8/30/2023 5:07:20 PM

# Sending request GET http://localhost:5000/cached/vary/yes?query1=c&query2=d ...
# 200 (OK) (GET http://localhost:5000/cached/vary/yes?query1=c&query2=d)
# Parameters: query1 c query2 d -> DateTime: 8/30/2023 5:07:27 PM

# Sending request GET http://localhost:5000/cached/vary/yes?query1=c&query2=d ...
# 200 (OK) (GET http://localhost:5000/cached/vary/yes?query1=c&query2=d)
# Parameters: query1 c query2 d -> DateTime: 8/30/2023 5:07:27 PM

# The time it took to finish:
# 14.22 seconds
```

Notice that at this example, the cache worked properly only for the `/cached/public` and `/cached/vary/yes` endpoints, as expected. You can read the documentation presented before to understand why.

One last information, notice that the server will inform whenever the response was cached or not, just check the logs.

For example:

* If the response was cached: `The response has been cached.` and `Serving response from cache.`;
* If the response was not cached: `The response could not be cached for this request.` and `No cached response available for this request.`.