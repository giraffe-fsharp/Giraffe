# ----------------------------------------------
# Generic functions
# ----------------------------------------------

function Test-IsMonoInstalled
{
    <#
        .DESCRIPTION
        Checks to see whether the current environment has the Mono framework installed.

        .EXAMPLE
        if (Test-IsMonoInstalled) { Write-Host "Mono is available." }
    #>

    try
    {
        $result = Invoke-Cmd "mono --version" -Silent
        return $result.StartsWith("Mono JIT compiler version")
    }
    catch { return false }
}

function Get-UbuntuVersion
{
    <#
        .DESCRIPTION
        Gets the Ubuntu version.

        .EXAMPLE
        $ubuntuVersion = Get-UbuntuVersion
    #>

    $version = Invoke-Cmd "lsb_release -r -s" -Silent
    return $version
}

function Invoke-UnsafeCmd (
    [string] $Cmd,
    [switch] $Silent)
{
    <#
        .DESCRIPTION
        Runs a shell or bash command, but doesn't throw an error if the command didn't exit with 0.

        .PARAMETER cmd
        The command to be executed.

        .EXAMPLE
        Invoke-Cmd -Cmd "dotnet new classlib"

        .NOTES
        Use this PowerShell command to execute any CLI commands which might not exit with 0 on a success.
    #>

    if (!($Silent.IsPresent)) { Write-Host $cmd -ForegroundColor DarkCyan }
    if ($IsWindows) { $cmd = "cmd.exe /C $cmd" }
    Invoke-Expression -Command $cmd
}

function Invoke-Cmd (
    [string] $Cmd,
    [switch] $Silent)
{
    <#
        .DESCRIPTION
        Runs a shell or bash command and throws an error if the command didn't exit with 0.

        .PARAMETER cmd
        The command to be executed.

        .EXAMPLE
        Invoke-Cmd -Cmd "dotnet new classlib"

        .NOTES
        Use this PowerShell command to execute any dotnet CLI commands in order to ensure that they behave the same way in the case of an error across different environments (Windows, OSX and Linux).
    #>

    if ($Silent.IsPresent) { Invoke-UnsafeCmd $cmd -Silent } else { Invoke-UnsafeCmd $cmd }
    if ($LastExitCode -ne 0) {
        Write-Host "An error occured when executing '$Cmd'."
        Write-Error "An error occured when executing '$Cmd'."
        return
    }
}

function Remove-OldBuildArtifacts
{
    <#
        .DESCRIPTION
        Deletes all the bin and obj folders from the current- and all sub directories.
    #>

    Write-Host "Deleting old build artifacts..." -ForegroundColor Magenta

    Get-ChildItem -Include "bin", "obj" -Recurse -Directory `
    | ForEach-Object {
        Write-Host "Removing folder $_" -ForegroundColor DarkGray
        Remove-Item $_ -Recurse -Force }
}

function Get-ProjectVersion ($projFile)
{
    <#
        .DESCRIPTION
        Gets the <Version> value of a .NET Core *.csproj, *.fsproj or *.vbproj file.

        .PARAMETER cmd
        The relative or absolute path to the .NET Core project file.
    #>

    [xml]$xml = Get-Content $projFile
    [string] $version = $xml.Project.PropertyGroup.Version
    $version
}

function Get-NuspecVersion ($nuspecFile)
{
    <#
        .DESCRIPTION
        Gets the <version> value of a .nuspec file.

        .PARAMETER cmd
        The relative or absolute path to the .nuspec file.
    #>

    [xml] $xml = Get-Content $nuspecFile
    [string] $version = $xml.package.metadata.version
    $version
}

function Test-CompareVersions ($version, [string]$gitTag)
{
    Write-Host "Matching version against git tag..." -ForegroundColor Magenta
    Write-Host "Project version: $version" -ForegroundColor Cyan
    Write-Host "Git tag version: $gitTag" -ForegroundColor Cyan

    if (!$gitTag.EndsWith($version))
    {
        Write-Error "Version and Git tag do not match."
    }
}

function Add-ToPathVariable ($path)
{
    if ($IsWindows)
    {
        $updatedPath = "$path;$env:Path"
        [Environment]::SetEnvironmentVariable("Path", $updatedPath, "Process")
        [Environment]::SetEnvironmentVariable("Path", $updatedPath, "User")
        [Environment]::SetEnvironmentVariable("Path", $updatedPath, "Machine")
    }
    else
    {
        $updatedPath = "$path`:$env:PATH"
        [Environment]::SetEnvironmentVariable("PATH", $updatedPath, "Process")
        [Environment]::SetEnvironmentVariable("PATH", $updatedPath, "User")
        [Environment]::SetEnvironmentVariable("PATH", $updatedPath, "Machine")
    }
}

function Get-ProjectName ($proj)
{
	[System.IO.Path]::GetFileNameWithoutExtension($proj)
}

# ----------------------------------------------
# .NET Core functions
# ----------------------------------------------

function Get-TargetFrameworks ($projFile)
{
    <#
        .DESCRIPTION
        Returns all target frameworks set up inside a specific .NET Core project file.

        .PARAMETER projFile
        The full or relative path to a .NET Core project file (*.csproj, *.fsproj, *.vbproj).

        .EXAMPLE
        Get-TargetFrameworks "MyProject.csproj"

        .NOTES
        This function will always return an array of target frameworks, even if only a single target framework was found in the project file.
    #>

    [xml]$proj = Get-Content $projFile

    if ($null -ne $proj.Project.PropertyGroup.TargetFrameworks) {
        ($proj.Project.PropertyGroup.TargetFrameworks).Split(";")
    }
    else { @($proj.Project.PropertyGroup.TargetFramework) }
}

function Get-NetCoreTargetFrameworks ($projFile)
{
    <#
        .DESCRIPTION
        Returns a single .NET Core framework which could be found among all configured target frameworks of a given .NET Core project file.

        .PARAMETER projFile
        The full or relative path to a .NET Core project file (*.csproj, *.fsproj, *.vbproj).

        .EXAMPLE
        Get-NetCoreTargetFrameworks "MyProject.csproj"

        .NOTES
        This function will always return the only netstandard*/netcoreapp* target framework which is set up as a target framework.
    #>

    Get-TargetFrameworks $projFile | Where-Object { $_ -like "netstandard*" -or $_ -like "netcoreapp*" }
}

function Invoke-DotNetCli ($cmd, $proj, $argv)
{
    # Currently dotnet test does not work for net461 on Linux/Mac
    # See: https://github.com/Microsoft/vstest/issues/1318

    if((!($IsWindows) -and !(Test-IsMonoInstalled)) `
        -or (!($IsWindows) -and ($cmd -eq "test")))
    {
        $netCoreFrameworks = Get-NetCoreTargetFrameworks($proj)

        foreach($fw in $netCoreFrameworks) {
            $fwArgv = "-f $fw " + $argv
            Invoke-Cmd "dotnet $cmd $proj $fwArgv"
        }
    }
    else
    {
        Invoke-Cmd "dotnet $cmd $proj $argv"
    }
}

function dotnet-info                      { Invoke-Cmd "dotnet --info" -Silent }
function dotnet-version                   { Invoke-Cmd "dotnet --version" -Silent }
function dotnet-restore ($project, $argv) { Invoke-Cmd "dotnet restore $project $argv" }
function dotnet-build   ($project, $argv) { Invoke-DotNetCli -Cmd "build" -Proj $project -Argv $argv }
function dotnet-test    ($project, $argv) { Invoke-DotNetCli -Cmd "test"  -Proj $project -Argv $argv  }
function dotnet-run     ($project, $argv) { Invoke-Cmd "dotnet run --project $project $argv" }
function dotnet-pack    ($project, $argv) { Invoke-Cmd "dotnet pack $project $argv" }
function dotnet-publish ($project, $argv) { Invoke-Cmd "dotnet publish $project $argv" }

function Get-DotNetRuntimeVersion
{
    <#
        .DESCRIPTION
        Runs the dotnet --info command and extracts the .NET Core Runtime version number.

        .NOTES
        The .NET Core Runtime version can sometimes be useful for other dotnet CLI commands (e.g. dotnet xunit -fxversion ".NET Core Runtime version").
    #>

    $info = dotnet-info
    [System.Array]::Reverse($info)
    $version = $info | Where-Object { $_.Contains("Version")  } | Select-Object -First 1
    $version.Split(":")[1].Trim()
}

function Write-DotnetCoreVersions
{
    <#
        .DESCRIPTION
        Writes the .NET Core SDK and Runtime version to the current host.
    #>

    $sdkVersion     = dotnet-version
    $runtimeVersion = Get-DotNetRuntimeVersion
    Write-Host ".NET Core SDK version:      $sdkVersion" -ForegroundColor Cyan
    Write-Host ".NET Core Runtime version:  $runtimeVersion" -ForegroundColor Cyan
}

function Get-DesiredSdk
{
    <#
        .DESCRIPTION
        Gets the desired .NET Core SDK version from the global.json file.
    #>

    Get-Content "global.json" `
    | ConvertFrom-Json `
    | ForEach-Object { $_.sdk.version.ToString() }
}

function Get-NetCoreSdkFromWeb ($version)
{
    <#
        .DESCRIPTION
        Downloads the desired .NET Core SDK version from the internet and saves it under a temporary file name which will be returned by the function.

        .PARAMETER version
        The SDK version which should be downloaded.
    #>

    Write-Host "Downloading .NET Core SDK $version..."

    $os  =
        if ($IsWindows) { "windows" }
        elseif ($IsLinux) { "linux" }
        elseif ($IsMacOS) { "macos" }
        else { Write-Error "Unknown OS, which is not supported by .NET Core." }

    $ext = if ($IsWindows) { ".zip" } else { ".tar.gz" }

    $uri = "https://www.microsoft.com/net/download/thank-you/dotnet-sdk-$version-$os-x64-binaries"
    Write-Host "Finding download link..."

    $response = Invoke-WebRequest -Uri $uri

    $downloadLink =
        $response.Links `
            | Where-Object { $_.onclick -eq "recordManualDownload()" } `
            | Select-Object -Expand href

    Write-Host "Creating temporary file..."

    $tempFile  = [System.IO.Path]::GetTempFileName() + $ext

    $webClient = New-Object System.Net.WebClient
    $webClient.DownloadFile($downloadLink, $tempFile)

    Write-Host "Download finished. SDK has been saved to '$tempFile'."

    return $tempFile
}

function Install-NetCoreSdkFromArchive ($sdkPackagePath)
{
    <#
        .DESCRIPTION
        Extracts the zip archive which contains the .NET Core SDK and installs it in the current working directory under .dotnetsdk.

        .PARAMETER version
        The zip archive which contains the .NET Core SDK.
    #>

    if ($IsWindows)
    {
        $dotnetInstallDir = [System.IO.Path]::Combine($pwd, ".dotnetsdk")

        if (!(Test-Path $dotnetInstallDir))
        {
            New-Item $dotnetInstallDir -ItemType Directory -Force | Out-Null
            Write-Host "Created folder '$dotnetInstallDir'."
        }

        Expand-Archive -LiteralPath $sdkPackagePath -DestinationPath $dotnetInstallDir -Force
        Write-Host "Extracted '$sdkPackagePath' to folder '$dotnetInstallDir'."

        [Environment]::SetEnvironmentVariable("DOTNET_ROOT", $dotnetInstallDir, "Process")
        Write-Host "DOTNET_ROOT environment variable has been set to $dotnetInstallDir."
    }
    else
    {
        $dotnetInstallDir = "$env:HOME/.dotnetsdk"

        if (!(Test-Path $dotnetInstallDir))
        {
            Invoke-Cmd "mkdir -p $dotnetInstallDir"
            Write-Host "Created folder '$dotnetInstallDir'."
        }

        Invoke-Cmd "tar -xf $sdkPackagePath -C $dotnetInstallDir"
        Write-Host "Extracted '$sdkPackagePath' to folder '$dotnetInstallDir'."
    }

    Add-ToPathVariable $dotnetInstallDir
    Write-Host "Added '$dotnetInstallDir' to the PATH environment variable:"
    Write-Host $env:PATH
}

function Install-NetCoreSdkForUbuntu ($ubuntuVersion, $sdkVersion)
{
    Invoke-Cmd "wget -q https://packages.microsoft.com/config/ubuntu/$ubuntuVersion/packages-microsoft-prod.deb"
    Invoke-Cmd "sudo dpkg -i packages-microsoft-prod.deb"
    Invoke-Cmd "sudo apt-get install apt-transport-https"
    Invoke-Cmd "sudo apt-get update"
    Invoke-Cmd "sudo apt-get -y install dotnet-sdk-$sdkVersion"
}

# ----------------------------------------------
# AppVeyor functions
# ----------------------------------------------

function Test-IsAppVeyorBuild                  { return ($env:APPVEYOR -eq $true) }
function Test-IsAppVeyorBuildTriggeredByGitTag { return ($env:APPVEYOR_REPO_TAG -eq $true) }
function Get-AppVeyorGitTag                    { return $env:APPVEYOR_REPO_TAG_NAME }

function Update-AppVeyorBuildVersion ($version)
{
    if (Test-IsAppVeyorBuild)
    {
        Write-Host "Updating AppVeyor build version..." -ForegroundColor Magenta
        $buildVersion = "$version-$env:APPVEYOR_BUILD_NUMBER"
        Write-Host "Setting AppVeyor build version to $buildVersion."
        Update-AppveyorBuild -Version $buildVersion
    }
}

# ----------------------------------------------
# Host Writing functions
# ----------------------------------------------

function Write-BuildHeader ($projectTitle)
{
    $header = "  $projectTitle  ";
    $bar = ""
    for ($i = 0; $i -lt $header.Length; $i++) { $bar += "-" }

    Write-Host ""
    Write-Host $bar -ForegroundColor DarkYellow
    Write-Host $header -ForegroundColor DarkYellow
    Write-Host $bar -ForegroundColor DarkYellow
    Write-Host ""
}

function Write-SuccessFooter ($msg)
{
    $footer = "  $msg  ";
    $bar = ""
    for ($i = 0; $i -lt $footer.Length; $i++) { $bar += "-" }

    Write-Host ""
    Write-Host $bar -ForegroundColor Green
    Write-Host $footer -ForegroundColor Green
    Write-Host $bar -ForegroundColor Green
    Write-Host ""
}