#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Packs all Aero CMS library projects into NuGet packages.
.DESCRIPTION
    Builds all library projects in Release mode and produces .nupkg files
    in the build/nupkgs/ directory.
.PARAMETER VersionPrefix
    Override the version prefix (e.g. "1.2.0"). When set, overrides the
    VersionPrefix in Directory.Build.props. Used by the release workflow
    to set the version from the git tag.
.PARAMETER VersionSuffix
    Optional SemVer 2.0 suffix (e.g. "alpha.1", "rc.1", "preview").
    When set, packages are versioned as <base-version>-<suffix>.
    Default: "alpha" (produces 0.0.6-alpha).
    Ignored when -Stable is used.
.PARAMETER Stable
    Produces stable (release) packages with no suffix.
    Overrides both -VersionSuffix and the default VersionSuffix in
    Directory.Build.props, producing e.g. 0.0.6 instead of 0.0.6-alpha.
.PARAMETER OutputDir
    Output directory for nupkg files. Default: build/nupkgs.
.PARAMETER Configuration
    Build configuration. Default: Release.
.EXAMPLE
    # Preview: produces 0.0.6-alpha
    ./build/nuget-pack.ps1

    # Preview with custom suffix: produces 0.0.6-rc.1
    ./build/nuget-pack.ps1 -VersionSuffix "rc.1"

    # Stable release: produces 0.0.6
    ./build/nuget-pack.ps1 -Stable

    # Tag-based release: overrides version from git tag
    ./build/nuget-pack.ps1 -Stable -VersionPrefix "1.2.0"
#>

param(
    [string]$VersionPrefix = "",
    [string]$VersionSuffix = "alpha",
    [switch]$Stable,
    [string]$OutputDir = "",
    [string]$Configuration = "Release"
)

$RepoRoot = Resolve-Path "$PSScriptRoot/.."
$OutputDir = $(if ($OutputDir) { $OutputDir } else { "$RepoRoot/build/nupkgs" })

Write-Host "=== Aero CMS NuGet Pack Script ===" -ForegroundColor Cyan
Write-Host "Repo:     $RepoRoot" -ForegroundColor Gray
Write-Host "Output:   $OutputDir" -ForegroundColor Gray
Write-Host "Config:   $Configuration" -ForegroundColor Gray

$versionArgs = @()
if ($VersionPrefix) {
    Write-Host "Prefix:   $VersionPrefix (override from tag)" -ForegroundColor Green
    $versionArgs += "-p:VersionPrefix=$VersionPrefix"
}
if ($Stable) {
    Write-Host "Version:  stable (no suffix)" -ForegroundColor Green
    $versionArgs += "-p:VersionSuffix="  # Override Directory.Build.props to empty
} else {
    Write-Host "Suffix:   $VersionSuffix" -ForegroundColor Gray
    if ($VersionSuffix) {
        $versionArgs += "-p:VersionSuffix=$VersionSuffix"
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$libProjects = @(
    "$RepoRoot/src/Aero.Cms.Abstractions"
    "$RepoRoot/src/Aero.Cms.Contracts"
    "$RepoRoot/src/Aero.Cms.CookiePolicy"
    "$RepoRoot/src/Aero.Cms.Core"
    "$RepoRoot/src/Aero.Cms.Core.Abstractions"
    "$RepoRoot/src/Aero.Cms.Core.Entities"
    "$RepoRoot/src/Aero.Cms.Data"
    "$RepoRoot/src/Aero.Cms.Db.Marten"
    "$RepoRoot/src/Aero.Cms.Db.Polecat"
    "$RepoRoot/src/Aero.Cms.Hosting.Abstractions"
    "$RepoRoot/src/Aero.Cms.Hosting.Defaults"
    "$RepoRoot/src/Aero.Cms.Html"
    "$RepoRoot/src/Aero.Cms.Jobs"
    "$RepoRoot/src/Aero.Cms.Marten.Identity"
    "$RepoRoot/src/Aero.Cms.Modules.Abstraction"
    "$RepoRoot/src/Aero.Cms.Client"
    "$RepoRoot/src/Aero.Cms.ServiceDefaults"
    "$RepoRoot/src/Aero.Cms.Services"
    "$RepoRoot/src/Aero.Cms.Shared"
    "$RepoRoot/src/Aero.Cms.Shared.Models"
    "$RepoRoot/src/Aero.Cms.SourceGenerators"
    "$RepoRoot/src/Aero.Cms.UI"
    "$RepoRoot/src/Aero.Cms.Web.Bootstrap"
    "$RepoRoot/src/Aero.Cms.Web.Core"

    "$RepoRoot/src/Aero.AppServer"
)

# The default hosting catalog has ordinary package dependencies on its explicitly
# selected modules. Pack every module package so the facade/default packages can
# be restored from this output directory on a clean machine.
$moduleProjects = Get-ChildItem "$RepoRoot/src" -Directory |
    Where-Object {
        $_.Name.StartsWith("Aero.Cms.Modules.", [StringComparison]::Ordinal) -or
        $_.Name.StartsWith("Aero.Cms.Banners", [StringComparison]::Ordinal) -or
        $_.Name.StartsWith("Aero.Cms.CookiePolicy", [StringComparison]::Ordinal)
    } |
    Select-Object -ExpandProperty FullName

$packageProjects = @($libProjects + $moduleProjects | Sort-Object -Unique)

$failed = @()

foreach ($proj in $packageProjects) {
    $csproj = Get-ChildItem "$proj/*.csproj" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $csproj) {
        Write-Host "WARN: Project not found, skipping: $proj" -ForegroundColor Yellow
        continue
    }

    $projName = (Get-Item $csproj).BaseName
    Write-Host "  Packing: $projName..." -ForegroundColor Cyan
    $packArgs = @(
        "pack", $csproj,
        "-c", $Configuration,
        "-o", $OutputDir
    )

    # Analyzer-only packages place their assembly under analyzers/ instead of lib/.
    # Aero.Cms.SourceGenerators therefore opts out of a separate symbol package;
    # forcing one here produces an empty .snupkg and fails with NU5017.
    if ($projName -ne "Aero.Cms.SourceGenerators") {
        $packArgs += @(
            "--include-symbols",
            "-p:IncludeSymbols=true",
            "-p:SymbolPackageFormat=snupkg"
        )
    }

    if ($projName -eq "Aero.Cms.Client") {
        $packArgs += @(
            "-m:1",
            "-p:BuildInParallel=false",
            "-p:UseSharedCompilation=false",
            "-p:NodeReuse=false"
        )
    }

    $output = dotnet @packArgs @versionArgs 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED: $(Split-Path $proj -Leaf)" -ForegroundColor Red
        $failed += $proj
        $output | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkRed }
    }
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
$count = (Get-ChildItem "$OutputDir/*.nupkg" -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike '*.snupkg' }).Count
Write-Host "Packages created: $count" -ForegroundColor Green
Write-Host "Location: $OutputDir" -ForegroundColor Green

if ($failed.Count -gt 0) {
    Write-Host "Failed: $($failed.Count)" -ForegroundColor Red
    exit 1
}
