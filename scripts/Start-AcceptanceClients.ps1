param([switch]$Doubles, [switch]$Reconnect, [switch]$ServerLoss, [switch]$Shared)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$root = Join-Path $PWD 'artifacts/account-multiplayer-ai-20261103'
$privateRoot = Join-Path $root 'acceptance-private'
$exe = Join-Path $root 'windows-client/PickleVerse.exe'
$mode = if ($Doubles) { '2v2' } else { '1v1' }
if ($Shared) { $mode = 'shared-' + $mode }
$count = if ($Doubles) { 4 } else { 2 }
$room = Join-Path $privateRoot "room-$mode.txt"
if (Test-Path -LiteralPath $room) { throw 'Archive the previous run and remove its temporary room file first.' }
for ($index=0; $index -lt $count; $index++) {
    $report = Join-Path $root "game-$mode-$index.json"
    if (Test-Path -LiteralPath $report) { throw 'Archive previous reports before starting another run.' }
}
for ($index=0; $index -lt $count; $index++) {
    $role = if ($index -eq 0) { 'host' } else { 'guest' }
    $fixtureName = if ($Shared) { "shared-player-$index.json" } else { "player-$index.json" }
    $fixture = Join-Path $privateRoot $fixtureName
    if (!(Test-Path -LiteralPath $fixture)) { throw 'Prepare the isolated game players first.' }
    $arguments = @('-batchmode','-nographics','-logFile',('"'+(Join-Path $root "game-$mode-$index.log")+'"'),
        '-pkvPhotonSmoke',('"'+$fixture+'"'),('"'+(Join-Path $root "game-$mode-$index.json")+'"'),('"'+$room+'"'),$role,'-pkvNetworkDiagnostics')
    if ($Doubles) { $arguments += '-pkvDoubles' }
    if ($Reconnect) { $arguments += '-pkvReconnectCheck' }
    if ($ServerLoss) { $arguments += '-pkvServerLossCheck' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    [IO.File]::WriteAllText((Join-Path $root "game-$mode-$index.pid"),$process.Id)
    Write-Output "Started $mode test client $index, process $($process.Id)."
}
