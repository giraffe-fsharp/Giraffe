# ----------------------------------------------
# Build script
# ----------------------------------------------

param
(
    [switch] $Release,
    [switch] $ExcludeTests,
    [switch] $ExcludeSamples,
    [switch] $Pack,
    [switch] $Run,
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
$identityApp           = "./samples/IdentityApp/IdentityApp/IdentityApp.fsproj"
$jwtApp                = "./samples/JwtApp/JwtApp/JwtApp.fsproj"
$sampleApp             = "./samples/SampleApp/SampleApp/SampleApp.fsproj"
$sampleAppTests        = "./samples/SampleApp/SampleApp.Tests/SampleApp.Tests.fsproj"

$version = Get-ProjectVersion $giraffe
Update-AppVeyorBuildVersion $version

if (Test-IsAppVeyorBuildTriggeredByGitTag)
{
    $gitTag = Get-AppVeyorGitTag
    Test-CompareVersions $giraffe $gitTag
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

if (!$ExcludeSamples.IsPresent -and !$Run.IsPresent)
{
    Write-Host "Building and testing samples..." -ForegroundColor Magenta

    dotnet-build   $identityApp
    dotnet-build   $jwtApp
    dotnet-build   $sampleApp

    dotnet-build   $sampleAppTests
    dotnet-test    $sampleAppTests
}

if ($Run.IsPresent)
{
    Write-Host "Launching sample application..." -ForegroundColor Magenta
    dotnet-build   $sampleApp
    dotnet-run     $sampleApp
}

if ($Pack.IsPresent)
{
    Write-Host "Packaging Giraffe NuGet package..." -ForegroundColor Magenta

    dotnet-pack $giraffe "-c $configuration"
}

Write-SuccessFooter "Giraffe build completed successfully!"