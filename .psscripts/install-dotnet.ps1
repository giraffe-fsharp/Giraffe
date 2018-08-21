# ----------------------------------------------------------
# Install script to check and download the correct .NET SDK
# ----------------------------------------------------------

function Test-IsWindows
{
    [environment]::OSVersion.Platform -ne "Unix"
}

function Invoke-Cmd ($cmd)
{
    Write-Host $cmd -ForegroundColor DarkCyan
    if (Test-IsWindows) { $cmd = "cmd.exe /C $cmd" }
    Invoke-Expression -Command $cmd
    if ($LastExitCode -ne 0) { Write-Error "An error occured when executing '$cmd'."; return }
}

function dotnet-version { Invoke-Cmd "dotnet --version" }

function Get-DesiredSdk
{
    Get-Content "global.json" | ConvertFrom-Json | % { $_.sdk.version.ToString() }
}

function Get-NetCoreSdk ($version)
{
    $os = if (Test-IsWindows) { "windows" } else { "linux" }

    $response = Invoke-WebRequest `
                    -Uri "https://www.microsoft.com/net/download/thank-you/dotnet-sdk-$version-$os-x64-binaries" `
                    -Method Get `
                    -MaximumRedirection 0 `

    $downloadLink =
        $response.Links `
            | Where-Object { $_.onclick -eq "recordManualDownload()" } `
            | Select-Object -Expand href

    $tempFile  = [System.IO.Path]::GetTempFileName()
    $webClient = New-Object System.Net.WebClient
    $webClient.DownloadFile($downloadLink, $tempFile)
    return $tempFile
}

function Install-NetCoreSdk ($sdkZipPath)
{
    $env:DOTNET_INSTALL_DIR = "$pwd\.dotnetsdk"
    New-Item $env:DOTNET_INSTALL_DIR -ItemType Directory -Force

    Add-Type -AssemblyName System.IO.Compression.FileSystem;
    [System.IO.Compression.ZipFile]::ExtractToDirectory($sdkZipPath, $env:DOTNET_INSTALL_DIR)
    $env:Path = "$env:DOTNET_INSTALL_DIR;$env:Path"
}

# ----------------------------------------------
# Install .NET Core SDK
# ----------------------------------------------

$ErrorActionPreference = "Stop"

# Rename the global.json before making the dotnet --version call
# This will prevent AppVeyor to fail because it might not find
# the desired SDK specified in the global.json
$globalJson = Get-Item "global.json"
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

$sdkZipPath = Get-NetCoreSdk $desiredSdk
Install-NetCoreSdk $sdkZipPath

Write-Host ".NET SDK installation complete." -ForegroundColor Green
dotnet-version