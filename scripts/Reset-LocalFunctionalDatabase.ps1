$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$taskDb = (docker inspect pickleverse_postgres | ConvertFrom-Json)[0]
$taskApi = (docker inspect pickleverse_api | ConvertFrom-Json)[0]
$taskRedis = (docker inspect pickleverse_redis | ConvertFrom-Json)[0]
if ($LASTEXITCODE -ne 0) { throw 'Không đọc được stack development local.' }
function Read-TaskEnvironment($taskContainer) {
    $taskValues = @{}
    foreach ($taskItem in $taskContainer.Config.Env) { $taskPair = $taskItem.Split('=', 2); $taskValues[$taskPair[0]] = $taskPair[1] }
    return $taskValues
}
$taskDbEnv = Read-TaskEnvironment $taskDb
$taskApiEnv = Read-TaskEnvironment $taskApi
$taskPort = $taskDb.NetworkSettings.Ports.'5432/tcp'[0]
$taskVolume = @($taskDb.Mounts | Where-Object Destination -eq '/var/lib/postgresql/data')
if ($taskDb.Config.Image -ne 'postgres:16-alpine' -or $taskDbEnv.POSTGRES_DB -ne 'pickleverse' -or
    $taskDbEnv.POSTGRES_USER -ne 'sa' -or $taskPort.HostPort -ne '15432' -or $taskPort.HostIp -ne '127.0.0.1' -or
    $taskVolume.Count -ne 1 -or $taskVolume[0].Name -ne 'pickleverse_dev_postgres_data' -or
    $taskApiEnv.ASPNETCORE_ENVIRONMENT -ne 'Development' -or
    $taskApiEnv.ConnectionStrings__DefaultConnection -notmatch '^Host=postgres;Port=5432;Database=pickleverse;Username=sa;') {
    throw 'Instance/API/volume không khớp development local đã được người dùng cho phép reset.'
}
foreach ($taskContainer in @($taskDb, $taskApi, $taskRedis)) {
    if ($taskContainer.Config.Labels.'com.docker.compose.project' -ne 'pickleverse' -or
        $taskContainer.Config.Labels.'com.docker.compose.project.working_dir' -ne (Get-Location).Path) {
        throw 'Compose project hoặc workspace không khớp.'
    }
}
$taskSource = @($taskDb.Mounts | Where-Object { $_.Type -eq 'bind' -and $_.Destination -eq '/docker-entrypoint-initdb.d/01_init.sql' })
if ($taskSource.Count -ne 1 -or $taskSource[0].Source -ne [IO.Path]::GetFullPath((Join-Path (Get-Location) 'src/Database.sql'))) {
    throw 'Schema bind mount không khớp src/Database.sql.'
}
$taskPrefix = (Get-Content src/Backend/API/appsettings.json -Raw | ConvertFrom-Json).Redis.KeyPrefix
if ($taskPrefix -ne 'pickleverse:') { throw 'Cần đối chiếu lại Redis prefix trước khi dọn cache.' }
Write-Output 'Đã xác minh localhost:15432, pickleverse_postgres/pickleverse, volume pickleverse_dev_postgres_data và API Development.'
docker compose stop backend-api
if ($LASTEXITCODE -ne 0) { throw 'Không dừng được API cũ.' }
$taskBackup = Join-Path (Get-Location) ('artifacts/functional-completion-20261106/before-reset-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.dump')
docker exec pickleverse_postgres pg_dump -U sa -d pickleverse -Fc -f /tmp/pkv-functional-backup.dump
if ($LASTEXITCODE -ne 0) { throw 'Không tạo được backup development.' }
docker cp pickleverse_postgres:/tmp/pkv-functional-backup.dump $taskBackup
if ($LASTEXITCODE -ne 0) { throw 'Không lưu được backup vào workspace.' }
docker exec pickleverse_postgres psql -U sa -d postgres -v ON_ERROR_STOP=1 -c 'DROP DATABASE pickleverse WITH (FORCE)'
if ($LASTEXITCODE -ne 0) { throw 'Không tạo lại được database development.' }
docker exec pickleverse_postgres psql -U sa -d postgres -v ON_ERROR_STOP=1 -c 'CREATE DATABASE pickleverse OWNER sa'
if ($LASTEXITCODE -ne 0) { throw 'Không tạo được database development.' }
docker exec -e 'PGOPTIONS=-c pickleverse.seed_coaching_demo=on' pickleverse_postgres psql -U sa -d pickleverse -v ON_ERROR_STOP=1 -f /docker-entrypoint-initdb.d/01_init.sql |
    Set-Content artifacts/functional-completion-20261106/local-schema.log -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Không khởi tạo được schema; API vẫn dừng để tránh chạy sai model.' }
$taskKeys = @(docker exec pickleverse_redis redis-cli --scan --pattern ($taskPrefix + '*'))
if ($LASTEXITCODE -ne 0) { throw 'Không đọc được cache PickleVerse.' }
foreach ($taskKey in $taskKeys) {
    if (-not $taskKey.StartsWith($taskPrefix, [StringComparison]::Ordinal)) { throw 'Redis trả key ngoài prefix dự án.' }
    docker exec pickleverse_redis redis-cli UNLINK $taskKey | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Không dọn được cache PickleVerse cũ.' }
}
Write-Output "Đã khởi tạo schema/seed và dọn $($taskKeys.Count) cache key thuộc PickleVerse. Backup: $taskBackup"
