# JWT Application

A sample application showing how to use a JSON Web Token (JWT) for 
authentication in ASP.NET Core with Giraffe.

This sample expects tokens from Google, but any JWT provider will work as long
as you configure the JwtBearerOptions accordingly.

In order to test this sample application, obtain a token from any web site
that uses Google Sign-In. Then, when you call the endpoints provided in this
sample, make sure you add the following HTTP header to your request:

```
Authorization=Bearer eyJhb...cSDPA
```

Replace the actual token with your own.
