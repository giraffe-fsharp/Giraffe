
#-------------------------------
# Installation
#-------------------------------


$url = "https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x64.latest.zip"
$env:DOTNET_INSTALL_DIR = "$pwd\.dotnetsdk"
mkdir $env:DOTNET_INSTALL_DIR -Force | Out-Null
$tempFile = [System.IO.Path]::GetTempFileName()
(New-Object System.Net.WebClient).DownloadFile($url, $tempFile)
Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::ExtractToDirectory($tempFile, $env:DOTNET_INSTALL_DIR)
$env:Path = "$env:DOTNET_INSTALL_DIR;$env:Path"


#Write-Host "Downloading latest .NET Core SDK..."

#(New-Object System.Net.WebClient).DownloadFile("https://download.microsoft.com/download/5/F/E/5FEB7E95-C643-48D5-8329-9D2C63676CE8/dotnet-dev-win-x64.1.0.0-rc4-004771.exe","dotnet-core-sdk.exe")
#Invoke-WebRequest "https://go.microsoft.com/fwlink/?linkid=841695" -OutFile "dotnet-core-sdk.exe"

#Write-Host "Installing .NET Core SDK..."

#Invoke-Command -ScriptBlock { ./dotnet-core-sdk.exe /S /v/qn }

#./dotnet-core-sdk.exe /install /quiet /norestart

#Write-Host "Installation succeeded." -ForegroundColor Green

#-------------------------------
# Update AppVeyor Build version
#-------------------------------

Write-Host "Parsing project file..."

[xml]$proj = Get-Content -Path "./src/AspNetCore.Lambda/AspNetCore.Lambda.fsproj"

$versionPrefix = $proj.Project.PropertyGroup.VersionPrefix

Write-Host "Version prefix: $versionPrefix"

$version = "$versionPrefix-$env:APPVEYOR_BUILD_NUMBER"

Write-Host "Updating AppVeyor build version to $version."

Update-AppveyorBuild -Version $version

#-------------------------------
# Update AppVeyor Build version
#-------------------------------

Write-Host "Launching build.cmd..."

./build.cmd

if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode)  }