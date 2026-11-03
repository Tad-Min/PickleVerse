$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location (Join-Path $PSScriptRoot '..')
$privateRoot = Join-Path $PWD 'artifacts/account-multiplayer-ai-20261103/acceptance-private'
$state = Get-Content (Join-Path $privateRoot 'state.json') -Raw | ConvertFrom-Json
$report = Get-Content 'My project/Temp/dedicated-build.json' -Raw | ConvertFrom-Json
if ($state.worker -ne 'pkv-acceptance-worker-p18' -or $state.label -ne 'com.pickleverse.acceptance=20261103-p18') { throw 'Unexpected acceptance state.' }
if ($report.result -ne 'Succeeded' -or !$report.appUiPluginExcluded -or $report.serverHttp -ne 'AlwaysAllowed-internal-server') { throw 'A successful current headless build is required.' }
docker build -t pickleverse-game-server:20261103-p18 -f deploy/game-server/Dockerfile .
if ($LASTEXITCODE -ne 0) { throw 'Worker image build failed.' }
if (@(docker ps -a --format '{{.Names}}') -contains $state.worker) {
    $label = docker inspect --format '{{index .Config.Labels "com.pickleverse.acceptance"}}' $state.worker
    if ($label -ne '20261103-p18') { throw 'Unexpected worker container label.' }
    docker stop $state.worker | Out-Null
    docker rm $state.worker | Out-Null
}
docker run -d --name $state.worker --label $state.label --network $state.network --cpus 2 --memory 2g -p 127.0.0.1:3669:3669/udp --env-file (Join-Path $privateRoot 'worker.env') pickleverse-game-server:20261103-p18
if ($LASTEXITCODE -ne 0) { throw 'Worker did not start.' }
