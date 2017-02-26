cls

dotnet --version

dotnet restore src/AspNetCore.Lambda
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet build src/AspNetCore.Lambda
if %errorlevel% neq 0 exit /b %errorlevel%


dotnet restore tests/AspNetCore.Lambda.Tests
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet build tests/AspNetCore.Lambda.Tests
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet test tests/AspNetCore.Lambda.Tests/AspNetCore.Lambda.Tests.fsproj
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


dotnet pack src/AspNetCore.Lambda -c Release
if %errorlevel% neq 0 exit /b %errorlevel%