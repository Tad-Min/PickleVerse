param(
    [switch]$Doubles,
    [switch]$Reconnect,
    [switch]$ServerLoss,
    [switch]$Shared,
    [switch]$PublicSearch,
    [switch]$PartyPairs,
    [string]$BuildPath,
    [string]$FixturePath,
    [string]$OutputPath,
    [int]$FixtureOffset = 0,
    [int]$HoldSeconds = 0,
    [uri]$BaseUrl
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$root = Join-Path $PWD 'artifacts/account-multiplayer-ai-20261103'
$privateRoot = Join-Path $root 'acceptance-private'
$exe = Join-Path $root 'windows-client/PickleVerse.exe'
if ($BuildPath) { $exe = (Resolve-Path -LiteralPath $BuildPath).Path }
if ($FixturePath) { $privateRoot = (Resolve-Path -LiteralPath $FixturePath).Path }
if ($OutputPath) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    $root = (Resolve-Path -LiteralPath $OutputPath).Path
}
if ($BaseUrl -and (!$BaseUrl.IsAbsoluteUri -or !$BaseUrl.IsLoopback -or $BaseUrl.Scheme -notin @('http','https'))) {
    throw 'The acceptance API override must be a loopback HTTP(S) endpoint.'
}
$mode = if ($Doubles) { '2v2' } else { '1v1' }
if ($Shared) { $mode = 'shared-' + $mode }
if ($PublicSearch) { $mode = 'public-' + $mode }
if ($PartyPairs) {
    if (!$PublicSearch -or !$Doubles) { throw 'PartyPairs uses public doubles matchmaking.' }
    $mode = 'party-' + $mode
}
$count = if ($Doubles) { 4 } else { 2 }
$room = Join-Path $(if ($OutputPath) { $root } else { $privateRoot }) "room-$mode.txt"
if (Test-Path -LiteralPath $room) { throw 'Archive the previous run and remove its temporary room file first.' }
for ($index=0; $index -lt $count; $index++) {
    $report = Join-Path $root "game-$mode-$index.json"
    if (Test-Path -LiteralPath $report) { throw 'Archive previous reports before starting another run.' }
}
for ($index=0; $index -lt $count; $index++) {
    $role = if ($index -eq 0) { 'host' } else { 'guest' }
    $slot = $index + $FixtureOffset
    $fixtureName = if ($Shared) { "shared-player-$slot.json" } else { "player-$slot.json" }
    $fixture = Join-Path $privateRoot $fixtureName
    if (!(Test-Path -LiteralPath $fixture)) { throw 'Prepare the isolated game players first.' }
    if ($BaseUrl) {
        $local = Get-Content -Raw -LiteralPath $fixture | ConvertFrom-Json
        $local | Add-Member -NotePropertyName baseUrl -NotePropertyValue $BaseUrl.AbsoluteUri -Force
        $fixture = Join-Path $privateRoot "$mode-player-$slot.local.json"
        [IO.File]::WriteAllText($fixture, (ConvertTo-Json -InputObject $local -Compress))
    }
    $assignmentRoom = $room
    if ($PartyPairs) {
        $team = [Math]::Floor($index / 2)
        $assignmentRoom = [IO.Path]::ChangeExtension($room, "$team.txt")
        if ($index % 2 -eq 0 -and (Test-Path -LiteralPath $assignmentRoom)) { throw 'Archive the previous party code.' }
    }
    $arguments = @('-batchmode','-nographics','-logFile',('"'+(Join-Path $root "game-$mode-$index.log")+'"'),
        '-pkvPhotonSmoke',('"'+$fixture+'"'),('"'+(Join-Path $root "game-$mode-$index.json")+'"'),('"'+$assignmentRoom+'"'),$role,'-pkvNetworkDiagnostics')
    if ($Doubles) { $arguments += '-pkvDoubles' }
    if ($PublicSearch) { $arguments += '-pkvPublicSearch' }
    if ($PartyPairs) { $arguments += $(if ($index % 2 -eq 0) { '-pkvPartyPair' } else { '-pkvPartyFollower' }) }
    if ($HoldSeconds -gt 0) { $arguments += @('-pkvHoldCourtSeconds', "$HoldSeconds") }
    if ($Reconnect) { $arguments += '-pkvReconnectCheck' }
    if ($ServerLoss) { $arguments += '-pkvServerLossCheck' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    [IO.File]::WriteAllText((Join-Path $root "game-$mode-$index.pid"),$process.Id)
    Write-Output "Started $mode test client $index, process $($process.Id)."
}
