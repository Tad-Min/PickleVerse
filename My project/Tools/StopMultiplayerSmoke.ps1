param([Parameter(Mandatory)][string]$ProcessFile,[string]$Profile='')
$projectRoot=Split-Path $PSScriptRoot -Parent
$recordPath=[IO.Path]::GetFullPath((Join-Path $projectRoot $ProcessFile))
if(-not $recordPath.StartsWith((Join-Path $projectRoot 'Temp/MultiplayerSmoke'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Process file phải nằm trong Temp/MultiplayerSmoke.' }
$expectedExecutable=Join-Path $projectRoot 'Builds/Multiplayer/PickleVerse.exe'
foreach($entry in (Get-Content -LiteralPath $recordPath | ConvertFrom-Json)) {
    if($Profile -and $entry.Profile -ne $Profile) { continue }
    $process=Get-Process -Id $entry.Pid -ErrorAction SilentlyContinue
    if($process -and $process.Path -eq $expectedExecutable -and $entry.Executable -eq $expectedExecutable) {
        $process.Kill()
        "Stopped $($entry.Pid) $($entry.Profile)"
    }
}
