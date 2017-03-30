cls

dotnet --version

dotnet restore src/Giraffe
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet build src/Giraffe
if %errorlevel% neq 0 exit /b %errorlevel%


dotnet restore tests/Giraffe.Tests
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet build tests/Giraffe.Tests
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet test tests/Giraffe.Tests/Giraffe.Tests.fsproj
if %errorlevel% neq 0 exit /b %errorlevel%


dotnet restore samples/SampleApp/SampleApp
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet build samples/SampleApp/SampleApp
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet restore samples/SampleApp/SampleApp.Tests
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet build samples/SampleApp/SampleApp.Tests
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet test samples/SampleApp/SampleApp.Tests/SampleApp.Tests.fsproj
if %errorlevel% neq 0 exit /b %errorlevel%


dotnet pack src/Giraffe -c Release
if %errorlevel% neq 0 exit /b %errorlevel%

nuget pack template/giraffe-template.nuspec
if %errorlevel% neq 0 exit /b %errorlevel%