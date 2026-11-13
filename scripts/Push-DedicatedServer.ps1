<#
.SYNOPSIS
    Builds Linux Dedicated Server with local Unity Editor and pushes Docker image to Docker Hub.

.DESCRIPTION
    Solution 1:
    1. Calls Unity CLI (using local license and cached Library) to build Dedicated Server (Linux x86_64).
    2. Builds Docker image from deploy/game-server/Dockerfile.
    3. Pushes image to Docker Hub (tadminm/pickelverse:server-latest, :server, :server-<commit>).

.PARAMETER SkipBuild
    Skips Unity build step and uses the latest existing build in artifacts/.

.PARAMETER NoPush
    Only builds the Docker image locally, does not push to Docker Hub.

.PARAMETER Tag
    Adds an optional custom tag (e.g. -Tag "v1.0.0").

.PARAMETER ImageRepo
    Docker Hub repository name (default: tadminm/pickelverse).
#>

[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$NoPush,
    [string]$Tag = "",
    [string]$ImageRepo = "tadminm/pickelverse",
    [int]$TimeoutSeconds = 1800
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# 1. Check required tools
foreach ($cmd in @('git', 'docker')) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        throw "Required tool '$cmd' not found. Please install and add to PATH."
    }
}

# Verify Docker daemon is running
try {
    $null = docker info --format '{{.ServerVersion}}' 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Docker daemon is not running." }
} catch {
    throw "Cannot connect to Docker daemon. Please start Docker Desktop before running this script."
}

$repoRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
Push-Location $repoRoot
try {
    # 2. Get Git commit
    $fullCommit = (git rev-parse HEAD).Trim()
    $shortCommit = (git rev-parse --short HEAD).Trim()
    if ($fullCommit -notmatch '^[0-9a-f]{40}$') {
        throw "Could not obtain a valid 40-character Git commit."
    }

    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host " PickleVerse Dedicated Server Publisher (Local Build & Push)" -ForegroundColor Cyan
    Write-Host " Commit : $fullCommit ($shortCommit)" -ForegroundColor Cyan
    Write-Host " Target : $ImageRepo" -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Cyan

    $buildDir = ""
    if (-not $SkipBuild) {
        if (-not (Get-Command unity -ErrorAction SilentlyContinue)) {
            throw "Unity CLI not found in PATH. Please ensure Unity CLI is installed."
        }
        Write-Host "`n[1/3] Calling Unity Editor to build Dedicated Server (Linux x86_64)..." -ForegroundColor Yellow
        $projectDir = Join-Path $repoRoot 'My project'
        
        $outputLines = & unity run $projectDir --command pickleverse_build_server --format json --timeout "$TimeoutSeconds" -- --commit $fullCommit
        if ($LASTEXITCODE -ne 0) {
            throw "Unity build failed (exit code $LASTEXITCODE)."
        }
        
        $responseJson = ($outputLines -join "`n") | ConvertFrom-Json
        $buildDir = $responseJson.data.result
        if (-not $buildDir -or -not (Test-Path -LiteralPath $buildDir)) {
            throw "Unity did not return a valid build directory: $($outputLines -join "`n")"
        }
        Write-Host "  -> Unity build succeeded at: $buildDir" -ForegroundColor Green
    } else {
        Write-Host "`n[1/3] Skipping Unity build (-SkipBuild). Locating existing server build..." -ForegroundColor Yellow
        $candidateDirs = Get-ChildItem (Join-Path $repoRoot "artifacts/native-server/$fullCommit") -Directory -ErrorAction SilentlyContinue
        if (-not $candidateDirs) {
            $candidateDirs = Get-ChildItem (Join-Path $repoRoot "artifacts") -Recurse -Filter "PickleVerseServer" -ErrorAction SilentlyContinue | ForEach-Object { $_.Directory }
        }
        if (-not $candidateDirs) {
            throw "No existing server build found. Run without -SkipBuild."
        }
        $buildDir = $candidateDirs[-1].FullName
        Write-Host "  -> Using existing server build: $buildDir" -ForegroundColor Green
    }

    # Relative path from repo root for Docker build context
    $uriRoot = New-Object Uri (($repoRoot.TrimEnd('\') + '\'))
    $uriBuild = New-Object Uri (($buildDir.TrimEnd('\') + '\'))
    $relPath = [Uri]::UnescapeDataString($uriRoot.MakeRelativeUri($uriBuild).ToString()).Replace('\', '/').TrimEnd('/')

    # 3. Tags
    $tags = @(
        "$ImageRepo`:server-latest",
        "$ImageRepo`:server",
        "$ImageRepo`:server-$shortCommit"
    )
    if ($Tag) {
        $tags += "$ImageRepo`:server-$Tag"
    }

    $dockerTagArgs = @()
    foreach ($t in $tags) {
        $dockerTagArgs += @('-t', $t)
    }

    Write-Host "`n[2/3] Building Dedicated Server Docker image..." -ForegroundColor Yellow
    Write-Host "  Context Path : ." -ForegroundColor DarkGray
    Write-Host "  Dockerfile   : deploy/game-server/Dockerfile" -ForegroundColor DarkGray
    Write-Host "  Build Path   : $relPath" -ForegroundColor DarkGray
    foreach ($t in $tags) {
        Write-Host "  Tag          : $t" -ForegroundColor DarkGreen
    }

    & docker build -f deploy/game-server/Dockerfile --build-arg "SERVER_BUILD_PATH=$relPath" @dockerTagArgs .
    if ($LASTEXITCODE -ne 0) {
        throw "Docker build failed."
    }
    Write-Host "  -> Docker build succeeded!" -ForegroundColor Green

    # 4. Push to Docker Hub
    if (-not $NoPush) {
        Write-Host "`n[3/3] Pushing images to Docker Hub ($ImageRepo)..." -ForegroundColor Yellow
        foreach ($t in $tags) {
            Write-Host "  Pushing $t ..." -ForegroundColor Cyan
            & docker push $t
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to push $t to Docker Hub. Please check 'docker login'."
            }
        }
        Write-Host "`n==> Successfully pushed Dedicated Server images to Docker Hub!" -ForegroundColor Green
    } else {
        Write-Host "`n[3/3] Skipping Docker push (-NoPush)." -ForegroundColor Yellow
    }

    Write-Host "`nTo deploy a matching release of API, AI, web and Dedicated Server, see Doc/CI_CD_DockerHub.md." -ForegroundColor Cyan
}
finally {
    Pop-Location
}
