$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projects = @(
    'src/Aero.Cms.Abstractions/Aero.Cms.Abstractions.csproj'
    'src/Aero.Cms.Contracts/Aero.Cms.Contracts.csproj'
    'src/Aero.Cms.Html/Aero.Cms.Html.csproj'
    'src/Aero.Cms.Core/Aero.Cms.Core.csproj'
    'src/Aero.Cms.Web.Core/Aero.Cms.Web.Core.csproj'
    'src/Aero.Cms.Web.Bootstrap/Aero.Cms.Web.Bootstrap.csproj'
    'src/Aero.Cms.Modules.Commerce/Aero.Cms.Modules.Commerce.csproj'
)

Push-Location $repositoryRoot
try {
    foreach ($project in $projects) {
        & dotnet build $project `
            -c Release `
            -m:1 `
            -p:BuildInParallel=false `
            -p:UseSharedCompilation=false `
            -p:NuGetAudit=false `
            --disable-build-servers
        if ($LASTEXITCODE -ne 0) {
            throw "API input build failed: $project"
        }
    }

    Push-Location $PSScriptRoot
    try {
        & docfx metadata docfx.json --logLevel Warning
        if ($LASTEXITCODE -ne 0) {
            throw 'DocFX metadata generation failed.'
        }

        & node sanitize-api.mjs
        if ($LASTEXITCODE -ne 0) {
            throw 'DocFX metadata sanitization failed.'
        }

        & docfx build docfx.json --logLevel Warning
        if ($LASTEXITCODE -ne 0) {
            throw 'DocFX API build failed.'
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}
