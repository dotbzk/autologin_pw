param(
    [ValidateSet("dist", "portable")]
    [string]$Target = "dist",
    [switch]$SkipInstall,
    [switch]$NoPause
)

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
$BuildError = $null

function Assert-Command($Name, $InstallHint) {
    $Command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $Command) {
        throw "$Name is not installed or not available in PATH. $InstallHint"
    }
    return $Command
}

Push-Location $ProjectDir
try {
    Assert-Command "node" "Install Node.js LTS from https://nodejs.org/ or run: winget install OpenJS.NodeJS.LTS"
    Assert-Command "npm" "Install Node.js LTS from https://nodejs.org/ or run: winget install OpenJS.NodeJS.LTS"

    Write-Host "Node version:"
    & node --version
    if ($LASTEXITCODE -ne 0) {
        throw "node --version failed."
    }

    Write-Host "npm version:"
    & npm --version
    if ($LASTEXITCODE -ne 0) {
        throw "npm --version failed."
    }

    if (-not $SkipInstall) {
        if (Test-Path (Join-Path $ProjectDir "package-lock.json")) {
            Write-Host "Installing dependencies with npm ci..."
            & npm ci
        }
        else {
            Write-Host "Installing dependencies with npm install..."
            & npm install
        }
        if ($LASTEXITCODE -ne 0) {
            throw "Dependency installation failed."
        }
    }

    $ScriptName = if ($Target -eq "portable") { "dist:portable" } else { "dist" }
    Write-Host "Building Site Profiles Client ($ScriptName)..."
    & npm run $ScriptName
    if ($LASTEXITCODE -ne 0) {
        throw "Electron build failed."
    }

    $DistDir = Join-Path $ProjectDir "dist"
    if (-not (Test-Path $DistDir)) {
        throw "Build completed without dist directory: $DistDir"
    }

    Write-Host "Build completed: $DistDir" -ForegroundColor Green
}
catch {
    $BuildError = $_
    Write-Host "BUILD FAILED: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Pop-Location
}

if (-not $NoPause) {
    Read-Host "Press Enter to close"
}

if ($BuildError) {
    throw $BuildError
}
