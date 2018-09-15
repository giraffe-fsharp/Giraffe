# ----------------------------------------------
# Install .NET Core SDK
# ----------------------------------------------

$ErrorActionPreference = "Stop"

Import-module "$PSScriptRoot\build-functions.ps1" -Force

# Rename the global.json before making the dotnet --version call
# This will prevent AppVeyor to fail because it might not find
# the desired SDK specified in the global.json
$globalJson = Get-Item "$PSScriptRoot\..\global.json"
Rename-Item -Path $globalJson.FullName -NewName "global.json.bak" -Force

# Get the current .NET Core SDK version
$currentSdk = dotnet-version

# After we established the current installed .NET SDK we can put the global.json back
Rename-Item -Path ($globalJson.FullName + ".bak") -NewName "global.json" -Force

$desiredSdk = Get-DesiredSdk

if ($desiredSdk -eq $currentSdk)
{
    Write-Host "The current .NET SDK matches the project's desired .NET SDK: $desiredSDK" -ForegroundColor Green
    return
}

Write-Host "The current .NET SDK ($currentSdk) doesn't match the project's desired .NET SDK ($desiredSdk)." -ForegroundColor Yellow
Write-Host "Attempting to download and install the correct .NET SDK..."

$sdkZipPath = Get-NetCoreSdkFromWeb $desiredSdk
Install-NetCoreSdk $sdkZipPath

Write-Host ".NET SDK installation complete." -ForegroundColor Green
dotnet-version