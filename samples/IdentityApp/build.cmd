dotnet restore IdentityApp
dotnet restore IdentityApp.Tests
dotnet build IdentityApp.Tests
dotnet test IdentityApp.Tests/IdentityApp.Tests.fsproj