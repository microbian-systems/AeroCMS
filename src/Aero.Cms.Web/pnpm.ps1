# Ensure script stops on error
$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting dev dependency setup..." -ForegroundColor Cyan

# Check if pnpm is installed
if (-not (Get-Command pnpm -ErrorAction SilentlyContinue)) {
    Write-Host "📦 pnpm not found. Installing globally via npm..." -ForegroundColor Yellow
    
    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        Write-Error "❌ npm is not installed. Please install Node.js first."
        exit 1
    }

    npm install -g pnpm
}

# Initialize package.json if not present
if (-not (Test-Path "package.json")) {
    Write-Host "📄 No package.json found. Initializing project..." -ForegroundColor Yellow
    pnpm init -y
}

# Define dependencies (deduplicated)
$devDependencies = @(
    "alpinejs@latest",
    "htmx.org@latest",
    "gsap@latest",
    "swiper@latest",
    "animxyz@latest",
    "preact@latest",
    "lit@latest",
    "rxjs@latest",
    "simple-parallax-js@latest",
    "reapptor@latest"
)

Write-Host "📥 Installing dev dependencies..." -ForegroundColor Cyan

# Install as dev dependencies
pnpm add -D $devDependencies

Write-Host "✅ Installation complete!" -ForegroundColor Green