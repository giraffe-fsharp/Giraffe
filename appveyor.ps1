
#-------------------------------
# Installation
#-------------------------------

Write-Host "Downloading latest .NET Core SDK..."

Invoke-WebRequest "https://go.microsoft.com/fwlink/?linkid=841686" -OutFile "dotnet-core-sdk.exe"

Write-Host "Installing .NET Core SDK..."

Invoke-Command -ScriptBlock { ./dotnet-core-sdk.exe /S /v/qn }

Write-Host "Installation succeeded." -ForegroundColor Green

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