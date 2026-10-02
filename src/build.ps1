param(
    [string]$Python = "python",
    [switch]$SkipInstall,
    [switch]$FreshConfig,
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
$StageRoot = Join-Path $BuildRoot "dist"
$StagedOutput = Join-Path $StageRoot "client"
$BackupOutput = Join-Path $BuildRoot "previous-client"
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

    Write-Host "Preparing the new build..."
    if (Test-Path $BuildDir) {
        Remove-Item $BuildDir -Recurse -Force
    }
    if (Test-Path $StageRoot) {
        Remove-Item $StageRoot -Recurse -Force
    }

    Write-Host "Building GameLauncherBot..."
    & $VenvPython -m PyInstaller --clean --noconfirm --distpath $StageRoot --workpath $BuildDir $SpecFile
    if ($LASTEXITCODE -ne 0) {
        throw "PyInstaller build failed."
    }

    $ConfigDir = Join-Path $StagedOutput "configs"
    $IconDir = Join-Path $ConfigDir "ico"
    $ClassesDir = Join-Path $ConfigDir "classes"
    $AccountsDir = Join-Path $StagedOutput "accounts"
    New-Item -ItemType Directory -Force $ConfigDir | Out-Null
    New-Item -ItemType Directory -Force $IconDir | Out-Null
    New-Item -ItemType Directory -Force $ClassesDir | Out-Null
    New-Item -ItemType Directory -Force $AccountsDir | Out-Null

    Copy-Item (Join-Path $SourceDir "configs\config.ini") (Join-Path $ConfigDir "config.ini") -Force
    Copy-Item (Join-Path $SourceDir "configs\ico\app.png") (Join-Path $IconDir "app.png") -Force
    Copy-Item (Join-Path $SourceDir "configs\ico\app.ico") (Join-Path $IconDir "app.ico") -Force
    Copy-Item (Join-Path $SourceDir "configs\classes\*") $ClassesDir -Force
    Copy-Item (Join-Path $SourceDir "accounts\accounts.ini") (Join-Path $AccountsDir "accounts.ini") -Force
    Copy-Item (Join-Path $SourceDir "version.json") (Join-Path $StagedOutput "version.json") -Force

    if (-not $FreshConfig -and (Test-Path $OutputDir)) {
        & $VenvPython -c "from src.update_support import preserve_user_data; import sys; preserve_user_data(sys.argv[1], sys.argv[2])" $OutputDir $StagedOutput
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to preserve local settings and accounts."
        }
    }

    $Executable = Join-Path $StagedOutput "GameLauncherBot.exe"
    $RequiredFiles = @(
        $Executable,
        (Join-Path $StagedOutput "Updater.exe"),
        (Join-Path $StagedOutput "MemoryCleaner.exe"),
        (Join-Path $StagedOutput "version.json"),
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

    if (Test-Path $BackupOutput) {
        throw "Previous build backup still exists: $BackupOutput"
    }
    if (Test-Path $OutputDir) {
        Move-Item $OutputDir $BackupOutput
    }
    try {
        Move-Item $StagedOutput $OutputDir
    }
    catch {
        if (Test-Path $BackupOutput) {
            Move-Item $BackupOutput $OutputDir
        }
        throw
    }
    if (Test-Path $BackupOutput) {
        try {
            Remove-Item $BackupOutput -Recurse -Force
        }
        catch {
            Write-Warning "Cannot remove previous build backup: $BackupOutput"
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
        if (Test-Path $StageRoot) {
            Remove-Item $StageRoot -Recurse -Force
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
