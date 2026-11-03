$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location (Join-Path $PSScriptRoot '..')
$privateRoot = Join-Path $PWD 'artifacts/account-multiplayer-ai-20261103/acceptance-private'
$state = Get-Content (Join-Path $privateRoot 'state.json') -Raw | ConvertFrom-Json
$name = 'pkv-acceptance-shared-api-p18'
if ($state.label -ne 'com.pickleverse.acceptance=20261103-p18') { throw 'Unexpected acceptance state.' }
if (@(docker ps -a --format '{{.Names}}') -contains $name) { throw 'Shared acceptance API already exists.' }
$lines = Get-Content (Join-Path $privateRoot 'api.env') | ForEach-Object { if ($_ -like 'DedicatedServer__Enabled=*') { 'DedicatedServer__Enabled=false' } else { $_ } }
[IO.File]::WriteAllLines((Join-Path $privateRoot 'shared-api.env'), $lines)
docker run -d --name $name --label $state.label --network $state.network -p 127.0.0.1:28081:8080 --env-file (Join-Path $privateRoot 'shared-api.env') pickleverse-api:acceptance-p18
if ($LASTEXITCODE -ne 0) { throw 'Shared acceptance API did not start.' }
for ($index=0; $index -lt 4; $index++) {
    $fixture = Get-Content (Join-Path $privateRoot "player-$index.json") -Raw | ConvertFrom-Json
    $fixture.baseUrl = 'http://127.0.0.1:28081'
    [IO.File]::WriteAllText((Join-Path $privateRoot "shared-player-$index.json"), ($fixture | ConvertTo-Json))
}
