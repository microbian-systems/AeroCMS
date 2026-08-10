#!/usr/bin/env pwsh

param(
    [switch]$SkipPack
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path "$PSScriptRoot/../.."
$localFeed = "$repoRoot/artifacts/hosting-packages"
$packageCache = "$repoRoot/artifacts/hosting-package-cache-$PID"
$publishDirectory = "$repoRoot/artifacts/external-csharp-publish-$PID"
$nugetConfig = "$PSScriptRoot/NuGet.config"

if (-not $SkipPack) {
    & "$repoRoot/build/nuget-pack.ps1" -OutputDir $localFeed
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$consumerProjects = @(
    "$PSScriptRoot/CSharpHost.Client/CSharpHost.Client.csproj"
    "$PSScriptRoot/CSharpHost/CSharpHost.csproj"
    "$PSScriptRoot/FSharpHost.Client/FSharpHost.Client.csproj"
    "$PSScriptRoot/FSharpHost/FSharpHost.fsproj"
)

foreach ($consumerProject in $consumerProjects) {
    dotnet restore $consumerProject `
        --configfile $nugetConfig `
        --packages $packageCache `
        --force `
        --no-cache
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet build $consumerProject `
        -c Release `
        --no-restore `
        -p:RestorePackagesPath=$packageCache
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

dotnet publish "$PSScriptRoot/CSharpHost/CSharpHost.csproj" `
    -c Release `
    --no-restore `
    -p:RestorePackagesPath=$packageCache `
    -p:PublishDir=$publishDirectory
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$manifest = Get-ChildItem $publishDirectory -Filter "*.staticwebassets.endpoints.json" |
    Select-Object -First 1
if (-not $manifest) {
    throw "The package-restored C# host did not publish a static-web-asset endpoint manifest."
}

$manifestText = Get-Content $manifest.FullName -Raw
if (-not $manifestText.Contains("_content/Aero.Cms.UI") -or
    -not $manifestText.Contains("_content/Aero.Cms.Client") -or
    $manifestText.Contains("_content/Aero.Cms.Web/")) {
    throw "The published static-web-asset manifest does not match the reusable UI/client package boundary."
}

if (-not (Test-Path "$publishDirectory/runtimes/win-x64/native/surreal_surrealkv.dll")) {
    throw "The package-restored host publish output is missing the embedded database native runtime asset."
}

if (-not (Test-Path "$publishDirectory/wwwroot/_framework/dotnet.js") -or
    -not (Get-ChildItem "$publishDirectory/wwwroot/_framework" -Filter "CSharpHost.Client.*.wasm" | Select-Object -First 1) -or
    -not $manifestText.Contains("CSharpHost.Client") -or
    -not $manifestText.Contains("_framework/dotnet.js")) {
    throw "The package-restored C# host publish output is missing the consumer-owned WebAssembly runtime or client assembly."
}

Write-Host "External C# and F# package consumers verified." -ForegroundColor Green
