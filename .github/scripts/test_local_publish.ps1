# Exercise the real publisher with stubbed Git/Unity/GitHub commands and real tar/checksums.
# No network, account, Unity build or registry publication is performed.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('pkv-publish-check-' + [guid]::NewGuid().ToString('N'))
$testCommit = 'a' * 40
$global:PkvPublishTestState = @{
    Calls = [Collections.Generic.List[string]]::new()
    Dirty = $false
    Published = $false
    Native = Join-Path $fixtureRoot "artifacts/native-server/$testCommit/fixture"
}
function git {
    $global:LASTEXITCODE = 0
    $argv = @($args)
    $global:PkvPublishTestState.Calls.Add('git ' + ($argv -join ' '))
    switch ($argv[0]) {
        status { if ($global:PkvPublishTestState.Dirty) { ' M changed.cs' } }
        'rev-parse' { 'a' * 40 }
        branch { 'main' }
        remote { 'git@github.com:test/pickleverse.git' }
        'ls-remote' { ('a' * 40) + "`trefs/heads/main" }
        push { }
        default { throw "Unexpected mock git: $argv" }
    }
}
function unity {
    $global:LASTEXITCODE = 0
    $global:PkvPublishTestState.Calls.Add('unity ' + ($args -join ' '))
    @{ data = @{ result = $global:PkvPublishTestState.Native } } | ConvertTo-Json -Compress
}
function gh {
    $global:LASTEXITCODE = 0
    $global:PkvPublishTestState.Calls.Add('gh ' + ($args -join ' '))
    if ($args[0] -eq 'release' -and $args[1] -eq 'view') {
        if ($global:PkvPublishTestState.Published) { '{"isDraft":false}' }
        else { $global:LASTEXITCODE = 1 }
    }
}
function Assert-That([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Refused([scriptblock]$Action, [string]$MessagePattern) {
    $caught = $false
    try { & $Action }
    catch { if ($_.Exception.Message -notlike $MessagePattern) { throw }; $caught = $true }
    Assert-That $caught "Expected refusal: $MessagePattern"
}
try {
    foreach ($path in @('scripts', 'My project/ProjectSettings', 'My project/Assets/Scripts/Multiplayer/Services')) {
        $null = New-Item -ItemType Directory -Force -Path (Join-Path $fixtureRoot $path)
    }
    # Native is already absolute: create that directory independently on both OS families.
    $null = New-Item -ItemType Directory -Force -Path $global:PkvPublishTestState.Native
    $publisher = Join-Path $fixtureRoot 'scripts/Publish-DockerHubRelease.ps1'
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'scripts/Publish-DockerHubRelease.ps1') -Destination $publisher
    foreach ($relative in @('My project/ProjectSettings/ProjectVersion.txt', 'My project/Assets/Scripts/Multiplayer/Services/MultiplayerSceneConfiguration.cs')) {
        Copy-Item -LiteralPath (Join-Path $sourceRoot $relative) -Destination (Join-Path $fixtureRoot $relative)
    }
    $unityVersion = [regex]::Match((Get-Content (Join-Path $fixtureRoot 'My project/ProjectSettings/ProjectVersion.txt') -Raw), 'm_EditorVersion: (\S+)').Groups[1].Value
    $protocol = [int][regex]::Match((Get-Content (Join-Path $fixtureRoot 'My project/Assets/Scripts/Multiplayer/Services/MultiplayerSceneConfiguration.cs') -Raw), 'ProtocolVersion\s*=\s*(\d+)').Groups[1].Value
    $native = $global:PkvPublishTestState.Native
    @{ commit = $testCommit; unity = $unityVersion; protocol = $protocol; result = 'Succeeded'; errors = 0 } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $native 'build-info.json')
    'fixture' | Set-Content -LiteralPath (Join-Path $native 'PickleVerseServer')

    $global:PkvPublishTestState.Dirty = $true
    Assert-Refused { & $publisher -Publish } 'Commit or stash*'
    Assert-That (-not ($global:PkvPublishTestState.Calls -match '^(unity|gh|git push) ')) 'Dirty source reached a side effect.'
    $global:PkvPublishTestState.Dirty = $false
    $global:PkvPublishTestState.Calls.Clear()
    & $publisher -Publish
    $calls = $global:PkvPublishTestState.Calls
    $buildIndex = $calls.FindIndex([Predicate[string]] { param($s) $s.StartsWith('unity ') })
    $pushIndex = $calls.FindIndex([Predicate[string]] { param($s) $s.StartsWith('git push ') })
    $uploadIndex = $calls.FindIndex([Predicate[string]] { param($s) $s.StartsWith('gh release upload ') })
    $publishIndex = $calls.FindIndex([Predicate[string]] { param($s) $s.StartsWith('gh release edit ') })
    Assert-That ($buildIndex -ge 0 -and $pushIndex -gt $buildIndex -and $uploadIndex -gt $pushIndex -and $publishIndex -gt $uploadIndex) 'Build/push/upload/publish order is wrong.'
    Assert-That (@($calls | Where-Object { $_ -like 'gh release create *--draft*' }).Count -eq 1) 'Release must begin as a draft.'

    $calls.Clear()
    & $publisher -Publish -SkipBuild
    Assert-That (-not ($calls -match '^unity ')) 'SkipBuild invoked Unity.'
    $global:PkvPublishTestState.Published = $true
    $calls.Clear()
    Assert-Refused { & $publisher -Publish -SkipBuild } 'This native release is already published*'
    Assert-That (-not ($calls -match '^(git push|gh release upload|gh release edit) ')) 'Published binary was replaced.'

    $global:PkvPublishTestState.Published = $false
    $checksum = Join-Path $fixtureRoot "artifacts/native-server/packages/$testCommit/PickleVerseServer-linux-$testCommit.tar.gz.sha256"
    'invalid' | Set-Content -LiteralPath $checksum
    $calls.Clear()
    Assert-Refused { & $publisher -Publish -SkipBuild } 'Native checksum mismatch*'
    Assert-That (-not ($calls -match '^git push ')) 'Corrupt package reached git push.'
    Write-Output 'PASS: dirty source, build/push/upload order, draft publication, SkipBuild, published-release protection and checksum guard.'
}
finally {
    Remove-Variable PkvPublishTestState -Scope Global
    $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
    $allowedRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolvedFixture.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedFixture).StartsWith('pkv-publish-check-')) {
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
