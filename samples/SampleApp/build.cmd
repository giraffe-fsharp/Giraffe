dotnet restore SampleApp 
dotnet restore SampleApp.Tests
dotnet build SampleApp.Tests
dotnet test SampleApp.Tests/SampleApp.Tests.fsproj