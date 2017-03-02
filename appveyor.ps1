
#-------------------------------
# Update AppVeyor Build version
#-------------------------------

Write-Host "Parsing project file..."

[xml]$proj = Get-Content -Path "./src/Giraffe/Giraffe.fsproj"

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