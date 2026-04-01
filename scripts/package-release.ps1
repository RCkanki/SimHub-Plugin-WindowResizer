#Requires -Version 5.1
<#
.SYNOPSIS
  Builds WindowResizer in Release and zips artifacts for GitHub Releases.
.NOTES
  Requires SimHub at -SimHubRoot (default: ${env:ProgramFiles(x86)}\SimHub) so
  HintPath references in WindowResizer.csproj resolve.
#>
param(
    [string] $SimHubRoot = "${env:ProgramFiles(x86)}\SimHub",
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $repoRoot "WindowResizer.csproj"

if (-not (Test-Path $csproj)) {
    Write-Error "WindowResizer.csproj not found at $csproj"
}

$pluginDll = Join-Path $SimHubRoot "SimHub.Plugins.dll"
if (-not (Test-Path $pluginDll)) {
    Write-Error "SimHub not found at '$SimHubRoot' (missing SimHub.Plugins.dll). Install SimHub or pass -SimHubRoot."
}

[xml] $proj = Get-Content -Raw $csproj
$version = @($proj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ }) | Select-Object -First 1
if (-not $version) { $version = "0.0.0" }

Push-Location $repoRoot
try {
    dotnet build $csproj -c $Configuration --no-incremental
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $outDir = Join-Path $repoRoot "bin\$Configuration\net48"
    $dll = Join-Path $outDir "WindowResizer.dll"
    if (-not (Test-Path $dll)) {
        Write-Error "Build output not found: $dll"
    }

    $artifacts = Join-Path $repoRoot "artifacts"
    New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
    $zipName = "WindowResizer-v$version.zip"
    $zipPath = Join-Path $artifacts $zipName

    $staging = Join-Path $artifacts "staging-$version"
    if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
    New-Item -ItemType Directory -Force -Path $staging | Out-Null

    Copy-Item $dll (Join-Path $staging "WindowResizer.dll")
    Copy-Item (Join-Path $repoRoot "LICENSE") $staging
    Copy-Item (Join-Path $repoRoot "CHANGELOG.md") $staging
    Copy-Item (Join-Path $repoRoot "docs\INSTALL.txt") $staging

    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath
    Remove-Item -Recurse -Force $staging

    Write-Host "Created $zipPath"
    $hash = Get-FileHash $zipPath -Algorithm SHA256
    Write-Host "SHA256: $($hash.Hash)"
}
finally {
    Pop-Location
}
