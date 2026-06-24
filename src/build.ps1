param(
    [string]$Python = "python",
    [switch]$SkipInstall,
    [switch]$NoPause
)

$ErrorActionPreference = "Stop"
$SourceDir = $PSScriptRoot
$ProjectRoot = Split-Path -Parent $SourceDir
$VenvDir = Join-Path $SourceDir ".venv"
$VenvPython = Join-Path $VenvDir "Scripts\python.exe"
$RequirementsFile = Join-Path $SourceDir "requirements-dev.txt"
$TestsDir = Join-Path $SourceDir "tests"
$OutputDir = Join-Path $ProjectRoot "client"
$BuildRoot = Join-Path $ProjectRoot "build"
$BuildDir = Join-Path $BuildRoot "GameLauncherBot"
$SpecFile = Join-Path $SourceDir "app.spec"
$BuildError = $null
$PythonCacheDirs = @(
    (Join-Path $ProjectRoot "__pycache__"),
    (Join-Path $SourceDir "__pycache__"),
    (Join-Path $TestsDir "__pycache__")
)

Push-Location $ProjectRoot
try {
    if (-not (Test-Path $VenvPython)) {
        Write-Host "Creating Python virtual environment..."
        & $Python -m venv $VenvDir
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create the virtual environment."
        }
    }

    if (-not $SkipInstall) {
        Write-Host "Installing build dependencies..."
        & $VenvPython -m pip install --upgrade pip
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to upgrade pip."
        }

        & $VenvPython -m pip install -r $RequirementsFile
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install dependencies."
        }
    }

    Write-Host "Running tests..."
    & $VenvPython -m unittest discover -s $TestsDir -v
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed."
    }

    Write-Host "Removing the previous build..."
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
    }
    if (Test-Path $BuildDir) {
        Remove-Item $BuildDir -Recurse -Force
    }

    Write-Host "Building GameLauncherBot..."
    & $VenvPython -m PyInstaller --clean --noconfirm --distpath $ProjectRoot --workpath $BuildDir $SpecFile
    if ($LASTEXITCODE -ne 0) {
        throw "PyInstaller build failed."
    }

    $ConfigDir = Join-Path $OutputDir "configs"
    $IconDir = Join-Path $ConfigDir "ico"
    $ClassesDir = Join-Path $ConfigDir "classes"
    $AccountsDir = Join-Path $OutputDir "accounts"
    New-Item -ItemType Directory -Force $ConfigDir | Out-Null
    New-Item -ItemType Directory -Force $IconDir | Out-Null
    New-Item -ItemType Directory -Force $ClassesDir | Out-Null
    New-Item -ItemType Directory -Force $AccountsDir | Out-Null

    Copy-Item (Join-Path $SourceDir "configs\config.ini") (Join-Path $ConfigDir "config.ini") -Force
    Copy-Item (Join-Path $SourceDir "configs\ico\app.png") (Join-Path $IconDir "app.png") -Force
    Copy-Item (Join-Path $SourceDir "configs\ico\app.ico") (Join-Path $IconDir "app.ico") -Force
    Copy-Item (Join-Path $SourceDir "configs\classes\*") $ClassesDir -Force
    Copy-Item (Join-Path $SourceDir "accounts\accounts.ini") (Join-Path $AccountsDir "accounts.ini") -Force

    $Executable = Join-Path $OutputDir "GameLauncherBot.exe"
    $RequiredFiles = @(
        $Executable,
        (Join-Path $ConfigDir "config.ini"),
        (Join-Path $IconDir "app.png"),
        (Join-Path $IconDir "app.ico"),
        (Join-Path $ClassesDir "luk.png"),
        (Join-Path $AccountsDir "accounts.ini")
    )
    foreach ($RequiredFile in $RequiredFiles) {
        if (-not (Test-Path $RequiredFile)) {
            throw "Build completed without required file: $RequiredFile"
        }
    }

    Write-Host "Build completed: $OutputDir" -ForegroundColor Green
}
catch {
    $BuildError = $_
    Write-Host "BUILD FAILED: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Pop-Location

    Write-Host "Cleaning temporary build files..."
    try {
        if (Test-Path $BuildDir) {
            Remove-Item $BuildDir -Recurse -Force
        }
        if ((Test-Path $BuildRoot) -and -not (Get-ChildItem $BuildRoot -Force)) {
            Remove-Item $BuildRoot -Force
        }
        foreach ($CacheDir in $PythonCacheDirs) {
            if (Test-Path $CacheDir) {
                Remove-Item $CacheDir -Recurse -Force
            }
        }
    }
    catch {
        Write-Warning "Cannot remove some temporary files: $($_.Exception.Message)"
    }
}

if (-not $NoPause) {
    Read-Host "Press Enter to close"
}

if ($BuildError) {
    throw $BuildError
}
