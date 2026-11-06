param([string[]]$Check = @('status-enums', 'schema'), [switch]$Build, [switch]$ProvisionCoachingPilot)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$taskContainer = (docker inspect pickleverse_postgres | ConvertFrom-Json)[0]
if ($LASTEXITCODE -ne 0) { throw 'Không đọc được PostgreSQL development.' }
$taskValues = @{}
foreach ($taskItem in $taskContainer.Config.Env) { $taskPair = $taskItem.Split('=', 2); $taskValues[$taskPair[0]] = $taskPair[1] }
$taskBinding = $taskContainer.NetworkSettings.Ports.'5432/tcp'[0]
if ($taskContainer.Config.Image -ne 'postgres:16-alpine' -or $taskValues.POSTGRES_DB -ne 'pickleverse' -or
    $taskValues.POSTGRES_USER -ne 'sa' -or $taskBinding.HostPort -ne '15432' -or $taskBinding.HostIp -notin @('127.0.0.1', '0.0.0.0')) {
    throw 'Instance không khớp PostgreSQL development PickleVerse đã được cho phép.'
}
$env:PKV_TEST_POSTGRES = "Host=127.0.0.1;Port=$($taskBinding.HostPort);Database=pickleverse;Username=$($taskValues.POSTGRES_USER);Password=$($taskValues.POSTGRES_PASSWORD);Maximum Pool Size=40"
$env:PKV_TEST_REDIS = '127.0.0.1:16379,abortConnect=false'
$taskLogRoot = Join-Path (Get-Location) 'artifacts/functional-completion-20261106'
New-Item -ItemType Directory -Path $taskLogRoot -Force | Out-Null
if ($Build) {
    & dotnet build src/Testing/Backend/IntegrationChecks.csproj --disable-build-servers -m:1 -v quiet
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
if ($ProvisionCoachingPilot) {
    $taskApi = (docker inspect pickleverse_api | ConvertFrom-Json)[0]
    if ($LASTEXITCODE -ne 0 -or $taskBinding.HostIp -ne '127.0.0.1' -or
        $taskContainer.Config.Labels.'com.docker.compose.project' -ne 'pickleverse' -or
        $taskApi.Config.Labels.'com.docker.compose.project' -ne 'pickleverse' -or
        $taskApi.Config.Labels.'com.docker.compose.project.working_dir' -ne (Get-Location).Path -or
        'ASPNETCORE_ENVIRONMENT=Development' -notin $taskApi.Config.Env) {
        throw 'Chỉ cấp tài khoản thu thử trên stack development local đã xác minh.'
    }
    & dotnet run --project src/Testing/Backend --no-build -- '--provision-local-coaching-pilot'
    exit $LASTEXITCODE
}
foreach ($taskCheck in $Check) {
    if ($taskCheck -notmatch '^[a-z-]+$') { throw 'Tên bộ kiểm tra không hợp lệ.' }
    & dotnet run --project src/Testing/Backend --no-build -- "--check-$taskCheck" 2>&1 |
        Tee-Object -FilePath (Join-Path $taskLogRoot "check-$taskCheck.log")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
