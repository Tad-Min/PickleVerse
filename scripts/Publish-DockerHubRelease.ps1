[CmdletBinding()]
param(
    [switch]$Publish,
    [switch]$SkipBuild,
    [ValidateRange(60, 7200)][int]$TimeoutSeconds = 1800
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Checked {
    param([string]$Command, [string[]]$Arguments)
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed (exit $LASTEXITCODE)." }
}

function Assert-CleanSource {
    $changes = @(Invoke-Checked git @('status', '--porcelain', '--untracked-files=all'))
    if ($changes.Count -gt 0) {
        throw 'Commit or stash all source changes before building/publishing. No files were staged or committed automatically.'
    }
}

$repositoryRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repositoryRoot
try {
    foreach ($tool in @('git', 'tar')) { $null = Get-Command $tool -ErrorAction Stop }
    Assert-CleanSource
    $sourceCommit = (Invoke-Checked git @('rev-parse', 'HEAD')).Trim()
    if ($sourceCommit -notmatch '^[0-9a-f]{40}$') { throw 'Expected a full Git commit.' }
    $releaseTag = "server-build-$sourceCommit"
    $packageDirectory = Join-Path $repositoryRoot "artifacts/native-server/packages/$sourceCommit"
    $null = New-Item -ItemType Directory -Force -Path $packageDirectory
    $archivePath = Join-Path $packageDirectory "PickleVerseServer-linux-$sourceCommit.tar.gz"
    $checksumPath = "$archivePath.sha256"
    $projectDirectory = Join-Path $repositoryRoot 'My project'
    $unityVersion = ((Get-Content "$projectDirectory/ProjectSettings/ProjectVersion.txt" |
        Where-Object { $_ -match '^m_EditorVersion: ' }) -replace '^m_EditorVersion: ', '').Trim()
    $protocolText = Get-Content "$projectDirectory/Assets/Scripts/Multiplayer/Services/MultiplayerSceneConfiguration.cs" -Raw
    $protocol = [int][regex]::Match($protocolText, 'ProtocolVersion\s*=\s*(\d+)').Groups[1].Value

    if ($Publish) {
        $null = Get-Command gh -ErrorAction Stop
        if ((Invoke-Checked git @('branch', '--show-current')).Trim() -ne 'main') {
            throw 'Publish from main. Merge the source first, then build that exact commit.'
        }
        $originUrl = (Invoke-Checked git @('remote', 'get-url', 'origin')).Trim()
        if ($originUrl -notmatch '^((https://github\.com/)|(git@github\.com:))(?<repo>[\w.-]+/[\w.-]+?)(\.git)?$') {
            throw 'Expected origin to be an HTTPS or SSH github.com repository.'
        }
        $githubRepository = $Matches['repo']
        Invoke-Checked gh @('auth', 'status', '--hostname', 'github.com')
        $existingRelease = & gh release view $releaseTag --repo $githubRepository --json isDraft 2>$null
        $hasDraft = $LASTEXITCODE -eq 0
        if ($hasDraft -and -not ($existingRelease | ConvertFrom-Json).isDraft) {
            throw 'This native release is already published. Use Actions re-run instead of replacing its binary.'
        }
    }

    if (-not $SkipBuild) {
        $null = Get-Command unity -ErrorAction Stop
        $response = (Invoke-Checked unity @('run', $projectDirectory, '--command', 'pickleverse_build_server',
            '--format', 'json', '--timeout', "$TimeoutSeconds", '--', '--commit', $sourceCommit)) -join "`n"
        $nativeDirectory = ($response | ConvertFrom-Json).data.result
        if (-not $nativeDirectory) { throw 'Unity did not return a native output directory.' }
        $nativeDirectory = [IO.Path]::GetFullPath($nativeDirectory)
        $allowedDirectory = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts/native-server/$sourceCommit")) + [IO.Path]::DirectorySeparatorChar
        if (-not $nativeDirectory.StartsWith($allowedDirectory, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Unity output is outside this commit build directory.'
        }
        Invoke-Checked tar @('-czf', $archivePath, '--exclude=*_BackUpThisFolder_ButDontShipItWithYourGame',
            '--exclude=.unity-pipeline*', '-C', $nativeDirectory, '.')
        $digest = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText($checksumPath, "$digest  $([IO.Path]::GetFileName($archivePath))`n", [Text.Encoding]::ASCII)
    }

    if (-not (Test-Path -LiteralPath $checksumPath)) { throw 'Native package/checksum missing. Run without -SkipBuild.' }
    $digest = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $expectedChecksum = "$digest  $([IO.Path]::GetFileName($archivePath))"
    if ((Get-Content -LiteralPath $checksumPath -Raw).Trim() -cne $expectedChecksum) { throw 'Native checksum mismatch.' }
    $buildInfo = ((Invoke-Checked tar @('-xOf', $archivePath, './build-info.json')) -join "`n") | ConvertFrom-Json
    if ($buildInfo.commit -cne $sourceCommit -or $buildInfo.unity -cne $unityVersion -or
        $buildInfo.protocol -ne $protocol -or $buildInfo.result -cne 'Succeeded' -or $buildInfo.errors -ne 0) {
        throw 'Native package metadata does not match this successful source build.'
    }
    Assert-CleanSource
    if ((Invoke-Checked git @('rev-parse', 'HEAD')).Trim() -cne $sourceCommit) { throw 'HEAD changed during build.' }
    Write-Host "Native package ready: $archivePath"
    if (-not $Publish) {
        Write-Host 'No push performed. Publish this package with: .\scripts\Publish-DockerHubRelease.ps1 -Publish -SkipBuild'
        return
    }

    Invoke-Checked git @('push', 'origin', 'HEAD:refs/heads/main')
    $remoteHead = (Invoke-Checked git @('ls-remote', 'origin', 'refs/heads/main')) -split '\s+'
    if ($remoteHead[0] -cne $sourceCommit) { throw 'Remote main moved. Rebuild the new main commit before publishing.' }
    $notesPath = Join-Path $packageDirectory 'notes.md'
    [IO.File]::WriteAllText($notesPath, "Native Linux Dedicated Server for commit $sourceCommit.`nUnity $unityVersion; protocol $protocol.`nDocker publication is validated separately in Actions.`n")
    if (-not $hasDraft) {
        Invoke-Checked gh @('release', 'create', $releaseTag, '--repo', $githubRepository, '--target', $sourceCommit,
            '--draft', '--prerelease', '--latest=false', '--title', "Native server $($sourceCommit.Substring(0, 12))", '--notes-file', $notesPath)
    }
    Invoke-Checked gh @('release', 'upload', $releaseTag, $archivePath, $checksumPath, '--repo', $githubRepository, '--clobber')
    # Publish only after both uploads succeeded, so Actions cannot download a partial release.
    Invoke-Checked gh @('release', 'edit', $releaseTag, '--repo', $githubRepository, '--draft=false', '--latest=false')
    Write-Host "Published diagnostic native archive $releaseTag. The push to main starts the current CI/CD workflow."
}
finally { Pop-Location }
