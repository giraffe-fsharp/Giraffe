# ----------------------------------------------
# Build script
# ----------------------------------------------

param
(
    [switch] $Release,
    [switch] $ExcludeRazor,
    [switch] $ExcludeDotLiquid,
    [switch] $ExcludeTests,
    [switch] $ExcludeSamples,
    [switch] $Pack,
    [switch] $Run,
    [switch] $OnlyNetStandard
)

$ErrorActionPreference = "Stop"

# ----------------------------------------------
# Helper functions
# ----------------------------------------------

function Invoke-Cmd ($cmd)
{
    Write-Host $cmd -ForegroundColor DarkCyan
    if ([environment]::OSVersion.Platform -eq "Unix")
    {
        Invoke-Expression -Command $cmd
    }
    else
    {
        $command = "cmd.exe /C $cmd"
        Invoke-Expression -Command $command
        if ($LastExitCode -ne 0) { Write-Error "An error occured when executing '$cmd'."; return }
    }
}

function Write-DotnetVersion
{
    $dotnetVersion = Invoke-Cmd "dotnet --version"
    Write-Host ".NET Core runtime version: $dotnetVersion" -ForegroundColor Cyan
}

function dotnet-restore ($project, $argv) { Invoke-Cmd "dotnet restore $project $argv" }
function dotnet-build   ($project, $argv) { Invoke-Cmd "dotnet build $project $argv" }
function dotnet-run     ($project, $argv) { Invoke-Cmd "dotnet run --project $project $argv" }
function dotnet-test    ($project, $argv) { Invoke-Cmd "dotnet test $project $argv" }
function dotnet-pack    ($project, $argv) { Invoke-Cmd "dotnet pack $project $argv" }

function Test-Version ($project)
{
    if ($env:APPVEYOR_REPO_TAG -eq $true)
    {
        Write-Host "Matching version against git tag..." -ForegroundColor Magenta

        [xml] $xml = Get-Content $project
        [string] $version = $xml.Project.PropertyGroup.Version
        [string] $gitTag  = $env:APPVEYOR_REPO_TAG_NAME

        Write-Host "Project version: $version" -ForegroundColor Cyan
        Write-Host "Git tag version: $gitTag" -ForegroundColor Cyan

        if (!$gitTag.EndsWith($version))
        {
            Write-Error "Version and Git tag do not match."
        }
    }
}

function Update-AppVeyorBuildVersion ($project)
{
    if ($env:APPVEYOR -eq $true)
    {
        Write-Host "Updating AppVeyor build version..." -ForegroundColor Magenta

        [xml]$xml = Get-Content $project
        $version = $xml.Project.PropertyGroup.Version
        $buildVersion = "$version-$env:APPVEYOR_BUILD_NUMBER"
        Write-Host "Setting AppVeyor build version to $buildVersion."
        Update-AppveyorBuild -Version $buildVersion
    }
}

function Remove-OldBuildArtifacts
{
    Write-Host "Deleting old build artifacts..." -ForegroundColor Magenta

    Get-ChildItem -Include "bin", "obj" -Recurse -Directory `
    | ForEach-Object { 
        Write-Host "Removing folder $_" -ForegroundColor DarkGray
        Remove-Item $_ -Recurse -Force }
}

# ----------------------------------------------
# Main
# ----------------------------------------------

$giraffe          = ".\src\Giraffe\Giraffe.fsproj"
$giraffeRazor     = ".\src\Giraffe.Razor\Giraffe.Razor.fsproj"
$giraffeDotLiquid = ".\src\Giraffe.DotLiquid\Giraffe.DotLiquid.fsproj"
$giraffeTests     = ".\tests\Giraffe.Tests\Giraffe.Tests.fsproj"
$sampleApp        = ".\samples\SampleApp\SampleApp\SampleApp.fsproj"
$sampleAppTests   = ".\samples\SampleApp\SampleApp.Tests\SampleApp.Tests.fsproj"

Update-AppVeyorBuildVersion $giraffe
Test-Version $giraffe
Write-DotnetVersion
Remove-OldBuildArtifacts

$configuration = if ($Release.IsPresent) { "Release" } else { "Debug" }
$framework     = if ($OnlyNetStandard.IsPresent) { "-f netstandard1.6" } else { "" }

Write-Host "Building Giraffe..." -ForegroundColor Magenta
dotnet-restore $giraffe
dotnet-build   $giraffe "-c $configuration $framework"

if (!$ExcludeRazor.IsPresent)
{
    Write-Host "Building Giraffe.Razor..." -ForegroundColor Magenta
    dotnet-restore $giraffeRazor
    dotnet-build   $giraffeRazor "-c $configuration $framework"
}

if (!$ExcludeDotLiquid.IsPresent)
{
    Write-Host "Building Giraffe.DotLiquid..." -ForegroundColor Magenta
    dotnet-restore $giraffeDotLiquid
    dotnet-build   $giraffeDotLiquid "-c $configuration $framework"
}

if (!$ExcludeTests.IsPresent -and !$Run.IsPresent)
{
    Write-Host "Building and running tests..." -ForegroundColor Magenta
    dotnet-restore $giraffeTests
    dotnet-build   $giraffeTests
    dotnet-test    $giraffeTests
}

if (!$ExcludeSamples.IsPresent -and !$Run.IsPresent)
{
    Write-Host "Building and testing samples..." -ForegroundColor Magenta 

    dotnet-restore $sampleApp
    dotnet-build   $sampleApp
    
    dotnet-restore $sampleAppTests
    dotnet-build   $sampleAppTests
    dotnet-test    $sampleAppTests
}

if ($Run.IsPresent)
{
    Write-Host "Launching sample application..." -ForegroundColor Magenta
    dotnet-restore $sampleApp
    dotnet-build   $sampleApp
    dotnet-run     $sampleApp
}

if ($Pack.IsPresent)
{
    Write-Host "Packaging all NuGet packages..." -ForegroundColor Magenta

    dotnet-pack $giraffe "-c $configuration"

    if (!$ExcludeRazor.IsPresent) { dotnet-pack $giraffeRazor "-c $configuration" }    
    if (!$ExcludeDotLiquid.IsPresent) { dotnet-pack $giraffeDotLiquid "-c $configuration" }

    Invoke-Cmd "nuget pack template/giraffe-template.nuspec"
}