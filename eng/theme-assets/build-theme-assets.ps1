[CmdletBinding()]
param(
    [ValidateSet('Build', 'Watch')]
    [string] $Mode = 'Build'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$tailwindVersion = '4.3.3'
$releaseBaseUri = "https://github.com/tailwindlabs/tailwindcss/releases/download/v$tailwindVersion"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$toolRoot = Join-Path $repoRoot ".tools\tailwindcss\$tailwindVersion"

$assets = @{
    'linux-arm64'      = @{ Name = 'tailwindcss-linux-arm64';      Sha256 = '55fd0b241214eff3de1e8ee4f22796662f2d2e7a49bcfca7477cfd0bac398195' }
    'linux-arm64-musl' = @{ Name = 'tailwindcss-linux-arm64-musl'; Sha256 = '71ea4be79c9de9827545682df3e040053fb535d37c71ed2cfdedf9385a0868e0' }
    'linux-x64'        = @{ Name = 'tailwindcss-linux-x64';        Sha256 = 'dc61b3ac6b8c9ca874c0cc4c57b2409791a64c5540404ca5f5367360babc313a' }
    'linux-x64-musl'   = @{ Name = 'tailwindcss-linux-x64-musl';   Sha256 = 'a04d34ceacc8f52cbe8920ad846cdeb61d3d0021dba32db0d1f77c9d9fad7a6c' }
    'macos-arm64'      = @{ Name = 'tailwindcss-macos-arm64';      Sha256 = 'cdf646702987a743464dff4d9c60fd4480d1c1e73dd819a9a67f1078815dce9d' }
    'macos-x64'        = @{ Name = 'tailwindcss-macos-x64';        Sha256 = '7922e0953f2110c05976e3bf58f14e643d90427575e766b7d433f5f80cbee7e1' }
    'windows-x64'      = @{ Name = 'tailwindcss-windows-x64.exe';  Sha256 = 'e0e260ce048014e9268f6237ff18f8ccf02cef521cbd0ae04e82c2cdf7aa3955' }
}

function Get-TailwindPlatformKey {
    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()

    if ($IsWindows) {
        return "windows-$architecture"
    }

    if ($IsMacOS) {
        return "macos-$architecture"
    }

    if ($IsLinux) {
        $isMusl = Test-Path '/etc/alpine-release'
        return if ($isMusl) { "linux-$architecture-musl" } else { "linux-$architecture" }
    }

    throw "Tailwind standalone CLI is not configured for this operating system."
}

function Get-VerifiedTailwindTool {
    $platformKey = Get-TailwindPlatformKey
    if (-not $assets.ContainsKey($platformKey)) {
        throw "Tailwind standalone CLI v$tailwindVersion has no pinned Aero CMS asset for '$platformKey'."
    }

    $asset = $assets[$platformKey]
    $toolPath = Join-Path $toolRoot $asset.Name
    New-Item -ItemType Directory -Force -Path $toolRoot | Out-Null

    if (-not (Test-Path -LiteralPath $toolPath)) {
        $downloadPath = "$toolPath.download"
        try {
            Invoke-WebRequest -Uri "$releaseBaseUri/$($asset.Name)" -OutFile $downloadPath
            Move-Item -Force -LiteralPath $downloadPath -Destination $toolPath
        }
        finally {
            if (Test-Path -LiteralPath $downloadPath) {
                Remove-Item -Force -LiteralPath $downloadPath
            }
        }
    }

    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $toolPath).Hash.ToLowerInvariant()
    if ($actualHash -ne $asset.Sha256) {
        throw "Tailwind standalone CLI checksum mismatch for '$toolPath'. Expected $($asset.Sha256), received $actualHash. Delete the file and retry."
    }

    if (-not $IsWindows) {
        & chmod '+x' $toolPath
        if ($LASTEXITCODE -ne 0) {
            throw "Could not mark the Tailwind standalone CLI executable."
        }
    }

    return $toolPath
}

$compilations = @(
    @{
        Name = 'Aero CMS web and manager styles'
        Input = Join-Path $repoRoot 'src\Aero.Cms.Web\Styles\aero.tailwind.css'
        Output = Join-Path $repoRoot 'src\Aero.Cms.Web\wwwroot\css\aero.generated.css'
    },
    @{
        Name = 'Aero CMS docs styles'
        Input = Join-Path $repoRoot 'src\Aero.Cms.Modules.Docs\Styles\docs.tailwind.css'
        Output = Join-Path $repoRoot 'src\Aero.Cms.Modules.Docs\wwwroot\css\docs.generated.css'
    }
)

$tool = Get-VerifiedTailwindTool

foreach ($compilation in $compilations) {
    if (-not (Test-Path -LiteralPath $compilation.Input)) {
        throw "Tailwind input is missing: $($compilation.Input)"
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $compilation.Output) | Out-Null
}

if ($Mode -eq 'Build') {
    foreach ($compilation in $compilations) {
        Write-Host "Compiling $($compilation.Name)..."
        & $tool '-i' $compilation.Input '-o' $compilation.Output '--minify'
        if ($LASTEXITCODE -ne 0) {
            throw "Tailwind compilation failed for $($compilation.Name)."
        }

        if (-not (Test-Path -LiteralPath $compilation.Output)) {
            throw "Tailwind reported success but did not create $($compilation.Output)."
        }

        # Tailwind avoids rewriting byte-identical output. Stamp successful outputs so
        # MSBuild's incremental missing/stale gate records that every input was checked.
        (Get-Item -LiteralPath $compilation.Output).LastWriteTimeUtc = [DateTime]::UtcNow
    }

    exit 0
}

Write-Host "Watching trusted Tailwind sources. Press Ctrl+C to stop."
$processes = @()
try {
    foreach ($compilation in $compilations) {
        # Start-Process joins ArgumentList values into one command line, so paths
        # must retain explicit quoting when a checkout contains spaces.
        $arguments = @('-i', ('"{0}"' -f $compilation.Input), '-o', ('"{0}"' -f $compilation.Output), '--minify', '--watch')
        $processes += Start-Process -FilePath $tool -ArgumentList $arguments -NoNewWindow -PassThru
    }

    Wait-Process -Id $processes.Id
}
finally {
    foreach ($process in $processes) {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id
        }
    }
}
