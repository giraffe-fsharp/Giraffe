cls

dotnet restore
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet build src/AspNetCore.Lambda
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet build tests/AspNetCore.Lambda.tests
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet test tests/AspNetCore.Lambda.tests
if %errorlevel% neq 0 exit /b %errorlevel%

dotnet pack src/AspNetCore.Lambda
if %errorlevel% neq 0 exit /b %errorlevel%