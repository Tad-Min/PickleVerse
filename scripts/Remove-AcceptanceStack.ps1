$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location (Join-Path $PSScriptRoot '..')
$privateRoot = [IO.Path]::GetFullPath((Join-Path $PWD 'artifacts/account-multiplayer-ai-20261103/acceptance-private'))
$state = Get-Content (Join-Path $privateRoot 'state.json') -Raw | ConvertFrom-Json
if ($state.database -notmatch '^pkv_acceptance_[a-f0-9]{32}$' -or $state.label -ne 'com.pickleverse.acceptance=20261103-p18') { throw 'Unexpected acceptance state.' }
$suffix = $state.database.Substring('pkv_acceptance_'.Length)
if ($state.bucket -ne ('pkv-acceptance-' + $suffix.Substring(0,12))) { throw 'Unexpected acceptance bucket.' }
$postgres = (docker inspect pickleverse_postgres | ConvertFrom-Json)[0]
$pg = @{}; foreach ($entry in $postgres.Config.Env) { $pair = $entry.Split('=',2); $pg[$pair[0]] = $pair[1] }
if ($postgres.Config.Image -ne 'postgres:16-alpine' -or $pg.POSTGRES_DB -ne 'pickleverse' -or $pg.POSTGRES_USER -ne 'sa') { throw 'Wrong development PostgreSQL instance.' }
$expected = @('pkv-acceptance-worker-p18','pkv-acceptance-shared-api-p18','pkv-acceptance-api-p18','pkv-acceptance-mail-p18')
$existing = @(docker ps -a --format '{{.Names}}')
foreach ($name in $expected) {
    if ($existing -notcontains $name) { continue }
    $label = docker inspect --format '{{index .Config.Labels "com.pickleverse.acceptance"}}' $name
    if ($label -ne '20261103-p18') { throw "Refusing to remove a container without the expected label: $name" }
}
foreach ($name in $expected) {
    if ($existing -notcontains $name) { continue }
    docker stop $name | Out-Null
    docker rm $name | Out-Null
}
$binding = (docker inspect pickleverse_s3 | ConvertFrom-Json)[0].NetworkSettings.Ports.'9000/tcp'[0]
if ($binding.HostPort -ne '19000' -or $binding.HostIp -notin @('127.0.0.1','0.0.0.0')) { throw 'Unexpected local S3 endpoint.' }
dotnet run --project src/Testing/Backend -- --cleanup-acceptance-bucket
if ($LASTEXITCODE -ne 0) { throw 'Isolated S3 cleanup failed.' }
$s3Cleanup = Get-Content 'artifacts/account-multiplayer-ai-20261103/s3-cleanup.json' -Raw | ConvertFrom-Json
if (!$s3Cleanup.bucketDeleted) {
    $storage = (docker inspect pickleverse_s3 | ConvertFrom-Json)[0]
    $mount = @($storage.Mounts | Where-Object { $_.Destination -eq '/data' -and $_.Name -eq 'pickleverse_dev_s3_data' -and $_.Type -eq 'volume' })
    if ($storage.Config.Image -notmatch '^ghcr\.io/shyim/local-s3(:|@)' -or $mount.Count -ne 1 -or !$s3Cleanup.empty -or $s3Cleanup.bucket -ne $state.bucket) { throw 'Refusing an unverified storage directory cleanup.' }
    # Non-recursive: refuses to remove anything if files remain or the target is a symlink.
    docker exec pickleverse_s3 rmdir -- "/data/$($state.bucket)"
    if ($LASTEXITCODE -ne 0) { throw 'Isolated bucket directory is not empty; no recursive deletion attempted.' }
    $s3Cleanup.bucketDeleted = $true
    $s3Cleanup | Add-Member -NotePropertyName method -NotePropertyValue 'verified-empty-directory' -Force
    [IO.File]::WriteAllText((Join-Path $PWD 'artifacts/account-multiplayer-ai-20261103/s3-cleanup.json'), ($s3Cleanup | ConvertTo-Json))
}
docker exec pickleverse_postgres psql -U sa -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE $($state.database) WITH (FORCE);" | Out-Null
$keys = @(docker exec pickleverse_redis redis-cli --scan --pattern "pickleverse:acceptance:$suffix`:*") | Where-Object { $_ }
foreach ($key in $keys) {
    if (!$key.StartsWith("pickleverse:acceptance:$suffix`:")) { throw 'Unexpected Redis key.' }
    docker exec pickleverse_redis redis-cli UNLINK $key | Out-Null
}
foreach ($path in Get-ChildItem -LiteralPath $privateRoot -File) {
    if ($path.Directory.FullName -ne $privateRoot) { throw 'Unexpected private file path.' }
    Remove-Item -LiteralPath $path.FullName
}
if (@(Get-ChildItem -LiteralPath $privateRoot -Force).Count -eq 0) { Remove-Item -LiteralPath $privateRoot }
Write-Output 'Removed labelled acceptance containers, isolated database, Redis prefix, S3 bucket and private test credentials.'
