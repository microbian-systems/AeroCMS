#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pushes all .nupkg files from build/nupkgs/ to nuget.org.
.DESCRIPTION
    Pushes packages to the NuGet.org gallery. Requires a NuGet API key.
    The key can be provided via the -ApiKey parameter, or via the
    NUGET_API_KEY_AeroCMS or NUGET_API_KEY environment variable (for local publishing).
.PARAMETER ApiKey
    NuGet API key to use for publishing.
    If not provided, falls back to GITHUB_API_KEY_AeroCMS, then NUGET_API_KEY.
.PARAMETER PackageIdPrefix
    Package ID prefix to publish. Defaults to Aero.Cms. so stale or unrelated
    packages in build/nupkgs are not pushed with the CMS key.
.EXAMPLE
    # Local: uses $env:GITHUB_API_KEY_AeroCMS or $env:NUGET_API_KEY
    ./build/nuget-publish.ps1

    # CI (Trusted Publishing): pass OIDC temp key
    ./build/nuget-publish.ps1 -ApiKey "${{ steps.login.outputs.NUGET_API_KEY }}"
#>

param(
    [string]$ApiKey,
    [string]$PackageIdPrefix = "Aero.Cms."
)

$RepoRoot = Resolve-Path "$PSScriptRoot/.."

# --- Resolve API key: explicit param takes priority, then env var ---
if ($ApiKey) {
    Write-Host "Using provided -ApiKey parameter." -ForegroundColor Gray
} elseif (-not [string]::IsNullOrWhiteSpace($env:NUGET_API_KEY_AeroCMS)) {
    $ApiKey = $env:NUGET_API_KEY_AeroCMS
    Write-Host "Using NUGET_API_KEY_AeroCMS environment variable." -ForegroundColor Gray
} elseif (-not [string]::IsNullOrWhiteSpace($env:NUGET_API_KEY)) {
    $ApiKey = $env:NUGET_API_KEY
    Write-Host "Using NUGET_API_KEY environment variable." -ForegroundColor Gray
} else {
    Write-Host "No API key provided. Set NUGET_API_KEY_AeroCMS or NUGET_API_KEY, or pass -ApiKey." -ForegroundColor Red
    exit 1
}

$nupkgs = Get-ChildItem "$RepoRoot/build/nupkgs/*.nupkg" -ErrorAction SilentlyContinue |
    Where-Object { $_.BaseName -like "$PackageIdPrefix*" }
if (-not $nupkgs) {
    Write-Host "No .nupkg files matching '$PackageIdPrefix*' found in build/nupkgs/. Run ./build/nuget-pack.ps1 first." -ForegroundColor Yellow
    exit 1
}

# --- Push primary packages ---
$failed = 0
Write-Host "Pushing $($nupkgs.Count) packages matching '$PackageIdPrefix*' to nuget.org..." -ForegroundColor Cyan
foreach ($nupkg in $nupkgs) {
    Write-Host "  $($nupkg.Name)..." -ForegroundColor Gray
    dotnet nuget push $nupkg.FullName --source https://api.nuget.org/v3/index.json --api-key "$ApiKey" --skip-duplicate
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED: $($nupkg.Name)" -ForegroundColor Red
        $failed++
    }
}

if ($failed -eq 0) {
    Write-Host "All packages published successfully." -ForegroundColor Green
} else {
    Write-Host "$failed package(s) failed." -ForegroundColor Red
    exit 1
}
