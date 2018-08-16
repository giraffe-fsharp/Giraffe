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

$desiredSdk = Get-DesiredSdk
$currentSdk = dotnet-version

if ($desiredSdk -eq $currentSdk)
{
    Write-Host "The current .NET SDK matches the project's desired .NET SDK: $desiredSDK" -ForegroundColor Green
    return
}

Write-Host "The current .NET SDK ($currentSdk) doesn't match the project's desired .NET SDK ($desiredSdk)." -ForegroundColor Yellow
Write-Host "Attempting to download and install the correct .NET SDK..."

# Download .NET Core SDK and add to PATH
# https://download.microsoft.com/download/9/D/2/9D2354BE-778B-42D6-BA4F-3CEF489A4FDE/dotnet-sdk-2.1.400-win-x64.zip
$url = "https://www.microsoft.com/net/download/thank-you/dotnet-sdk-$desiredSdk-windows-x64-binaries"
$env:DOTNET_INSTALL_DIR = "$pwd\.dotnetsdk"
New-Item $env:DOTNET_INSTALL_DIR -ItemType Directory -Force
$tempFile = [System.IO.Path]::GetTempFileName()
(New-Object System.Net.WebClient).DownloadFile($url, $tempFile)

Add-Type -AssemblyName System.IO.Compression.FileSystem;
[System.IO.Compression.ZipFile]::ExtractToDirectory($tempFile, $env:DOTNET_INSTALL_DIR)
$env:Path = "$env:DOTNET_INSTALL_DIR;$env:Path"

Write-Host ".NET SDK installation complete." -ForegroundColor Green
dotnet-version