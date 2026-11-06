param([string]$Check = 'auth', [switch]$Build)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$container = (docker inspect pickleverse_postgres | ConvertFrom-Json)[0]
$values = @{}
foreach ($item in $container.Config.Env) { $pair = $item.Split('=', 2); $values[$pair[0]] = $pair[1] }
if ($container.Config.Image -ne 'postgres:16-alpine' -or $values.POSTGRES_DB -ne 'pickleverse' -or $values.POSTGRES_USER -ne 'sa') {
    throw 'Development instance does not match the authorized project.'
}
$binding = $container.NetworkSettings.Ports.'5432/tcp'[0]
if ($binding.HostIp -notin @('127.0.0.1', '0.0.0.0')) { throw 'PostgreSQL is not published on the local host.' }
$env:PKV_TEST_POSTGRES = "Host=127.0.0.1;Port=$($binding.HostPort);Database=pickleverse;Username=$($values.POSTGRES_USER);Password=$($values.POSTGRES_PASSWORD);Maximum Pool Size=40"
$env:PKV_TEST_REDIS = '127.0.0.1:16379,abortConnect=false'
if ($Build) {
    & dotnet build src/Testing/Backend/IntegrationChecks.csproj -v quiet
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
$checks = @{
    auth = ''; schema = '--check-schema'; recovery = '--check-password-recovery'; social = '--check-online-social';
    invitations = '--check-room-invitations'; hybrid = '--check-hybrid'; multiplayer = '--check-online-multiplayer';
    catalog = '--check-catalog-text'; participant = '--check-normalized-participants'; match_columns = '--check-match-columns'; dedicated = '--check-dedicated'
}
if (!$checks.ContainsKey($Check)) { throw 'Unknown check.' }
$arguments = @('run', '--project', 'src/Testing/Backend', '--no-build', '--')
if ($checks[$Check]) { $arguments += $checks[$Check] }
& dotnet @arguments 2>&1 | Tee-Object -FilePath "artifacts/account-multiplayer-ai-20261103/check-$Check.log"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
