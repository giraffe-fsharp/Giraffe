# ----------------------------------------------
# Helper script to find NuGet package upgrades
# ----------------------------------------------

function Get-NuGetPackageInfo ($nugetPackageId, $currentVersion)
{
    $url    = "https://api-v2v3search-0.nuget.org/query?q=$nugetPackageId&prerelease=false"
    $result = Invoke-RestMethod -Uri $url -Method Get
    $latestVersion = $result.data[0].version
    $upgradeAvailable = !$latestVersion.StartsWith($currentVersion.Replace("*", ""))

    [PSCustomObject]@{
        PackageName      = $nugetPackageId;
        Current          = $currentVersion;
        Latest           = $latestVersion;
        UpgradeAvailable = $upgradeAvailable
    }
}

filter Highlight-Upgrades
{
    $lines = $_.Split([Environment]::NewLine)
    foreach ($line in $lines) {
        if ($line.Trim().EndsWith("True")) {
            Write-Host $line -ForegroundColor DarkGreen
        } else {
            Write-Host $line -ForegroundColor Gray
        }
    }
}

Write-Host ""
Write-Host ""
Write-Host "--------------------------------------------------"
Write-Host " Scanning all projects for NuGet package upgrades "
Write-Host "--------------------------------------------------"
Write-Host ""

$projects = Get-ChildItem "$PSScriptRoot\..\**\*.*proj" -Recurse | % { $_.FullName }

foreach ($project in $projects)
{
    $projName = Split-Path $project -Leaf
    Write-Host $projName -ForegroundColor Magenta

    [xml]$proj = Get-Content $project
    $references = $proj.Project.ItemGroup.PackageReference | Where-Object { $_.Include -ne $null }
    $packages = @()

    foreach ($reference in $references)
    {
        $id      = $reference.Include
        $version = $reference.Version
        $packages += Get-NuGetPackageInfo $id $version
    }

    $packages `
        | Format-Table -Property PackageName, Current, Latest, UpgradeAvailable -AutoSize `
        | Out-String `
        | Highlight-Upgrades
}