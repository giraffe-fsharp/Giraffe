# ----------------------------------------------
# Build script
# ----------------------------------------------

param
(
    [switch] $Release,
    [switch] $ExcludeTests,
    [switch] $Pack,
    [switch] $ClearOnly
)

# ----------------------------------------------
# Main
# ----------------------------------------------

$ErrorActionPreference = "Stop"

Import-module "$PSScriptRoot/.psscripts/build-functions.ps1" -Force

Write-BuildHeader "Starting Giraffe build script"

if ($ClearOnly.IsPresent)
{
    Remove-OldBuildArtifacts
    return
}

$giraffe               = "./src/Giraffe/Giraffe.fsproj"
$giraffeTests          = "./tests/Giraffe.Tests/Giraffe.Tests.fsproj"

$version = Get-ProjectVersion $giraffe
Update-AppVeyorBuildVersion $version

if (Test-IsAppVeyorBuildTriggeredByGitTag)
{
    $gitTag = Get-AppVeyorGitTag
    Test-CompareVersions $version $gitTag
}

Write-DotnetCoreVersions

Remove-OldBuildArtifacts

$configuration = if ($Release.IsPresent) { "Release" } else { "Debug" }

Write-Host "Building Giraffe..." -ForegroundColor Magenta
dotnet-build   $giraffe "-c $configuration"

if (!$ExcludeTests.IsPresent -and !$Run.IsPresent)
{
    Write-Host "Building and running tests..." -ForegroundColor Magenta

    dotnet-build $giraffeTests
    dotnet-test  $giraffeTests
}

if ($Pack.IsPresent)
{
    Write-Host "Packaging Giraffe NuGet package..." -ForegroundColor Magenta

    dotnet-pack $giraffe "-c $configuration"
}

Write-SuccessFooter "Giraffe build completed successfully!"