$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location (Join-Path $PSScriptRoot '..')
$privateRoot = Join-Path $PWD 'artifacts/account-multiplayer-ai-20261103/acceptance-private'
$state = Get-Content (Join-Path $privateRoot 'state.json') -Raw | ConvertFrom-Json
if ($state.api -ne 'pkv-acceptance-api-p18' -or $state.label -ne 'com.pickleverse.acceptance=20261103-p18') { throw 'Unexpected acceptance state.' }
$label = docker inspect --format '{{index .Config.Labels "com.pickleverse.acceptance"}}' $state.api
if ($label -ne '20261103-p18') { throw 'Unexpected API container label.' }
docker build -t pickleverse-api:20261103-p18 -f src/Backend/API/Dockerfile .
if ($LASTEXITCODE -ne 0) { throw 'API image build failed.' }
docker build -t pickleverse-api:acceptance-p18 -f (Join-Path $privateRoot 'Dockerfile') $privateRoot
if ($LASTEXITCODE -ne 0) { throw 'Acceptance trust-store image build failed.' }
docker stop $state.api | Out-Null
docker rm $state.api | Out-Null
docker run -d --name $state.api --label $state.label --network $state.network -p 127.0.0.1:28080:8080 --env-file (Join-Path $privateRoot 'api.env') pickleverse-api:acceptance-p18
if ($LASTEXITCODE -ne 0) { throw 'Acceptance API did not start.' }
