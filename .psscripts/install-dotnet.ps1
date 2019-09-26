# ----------------------------------------------
# Install .NET Core SDK
# ----------------------------------------------

param
(
    [string] $SdkVersions
)

$ErrorActionPreference = "Stop"

Import-module "$PSScriptRoot\build-functions.ps1" -Force

# Get desired SDKs from argument
$desiredSDKs = $SdkVersions.Split(",") | % { $_.Trim() }

foreach($desiredSDK in $desiredSDKs)
{
    Write-Host "Attempting to download and install the .NET SDK $desiredSDK..."
    $sdkZipPath = Get-NetCoreSdkFromWeb $desiredSDK
    Install-NetCoreSdkFromArchive $sdkZipPath
}

Write-Host ".NET SDK installations complete." -ForegroundColor Green
dotnet-info
dotnet-version