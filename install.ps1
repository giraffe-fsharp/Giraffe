Write-Host "Downloading latest .NET Core SDK..."

Invoke-WebRequest "https://go.microsoft.com/fwlink/?linkid=841686" -OutFile "dotnet-core-sdk.exe"

Write-Host "Installing .NET Core SDK..."

Invoke-Command -ScriptBlock { ./dotnet-core-sdk.exe /S /v/qn }

Write-Host "Installation succeeded." -ForegroundColor Green