param([string]$BaseUrl='http://localhost:8080')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$fixture=Get-Content -LiteralPath (Join-Path $repo 'artifacts/unity-account-fixture.json') -Raw | ConvertFrom-Json
if($fixture.email -notlike 'unity-smoke-*@example.invalid'){throw 'This check requires the disposable Unity fixture.'}
$login=Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/Auth/login" -ContentType 'application/json' -Body (@{email=$fixture.email;password=$fixture.password}|ConvertTo-Json -Compress)
$headers=@{Authorization='Bearer '+$login.accessToken}
function Api([string]$method,[string]$path,$body=$null) {
    $args=@{Method=$method;Uri="$BaseUrl/api/Game/$path";Headers=$headers}
    if($null -ne $body){$args.ContentType='application/json';$args.Body=ConvertTo-Json -InputObject $body -Depth 30 -Compress}
    Invoke-RestMethod @args
}
$state=Api POST 'match/bot/start' @{requestId=[guid]::NewGuid().ToString('N');kind='BotPractice'}
$result=$state.player.pendingMatch
$metadata=@{version=1;sampleRate=30;matchId=$result.id;playerId=$state.player.id;actors=@(
    @{id=$state.player.id;name='Verification';team=0;visualId='Male';bones=@('Root')},
    @{id='BOT';name='Bot';team=1;visualId='Female';bones=@('Root')})}
$saved=Api PUT "replays/$($result.id)" $metadata
$pose=@{root=@(0,0,0,0,0,0,1,1,1,1);bones=@(0,0,0,0,0,0,1,1,1,1)}
$chunk=@{frames=@(@{time=0;ball=@(0,1,0);actors=@($pose,$pose)});events=@()}
$bytes=[Text.Encoding]::UTF8.GetBytes(($chunk|ConvertTo-Json -Depth 20 -Compress))
$buffer=[IO.MemoryStream]::new()
$gzip=[IO.Compression.GZipStream]::new($buffer,[IO.Compression.CompressionLevel]::Fastest,$true)
$gzip.Write($bytes,0,$bytes.Length);$gzip.Dispose()
$data=[Convert]::ToBase64String($buffer.ToArray());$buffer.Dispose()
$info=Api PUT "replays/$($result.id)/chunks/0" @{data=$data}
$retry=Api PUT "replays/$($result.id)/chunks/0" @{data=$data}
$fetched=Api GET "replays/$($result.id)/chunks/0"
if($fetched.data -ne $data -or $info.file -ne $retry.file){throw 'S3 replay round-trip or idempotency failed.'}
$result.outcome='Lost';$result.localForfeit=$true
$finished=Api POST 'match/bot/complete' @{requestId=[guid]::NewGuid().ToString('N');result=$result}
$metadata.complete=$false
$saved=Api PUT "replays/$($result.id)" $metadata
$lessons=Invoke-RestMethod -Uri "$BaseUrl/api/Training/lessons" -Headers $headers
if($lessons.Count -ne 12){throw 'Legacy lesson route is missing Unity lessons.'}
$report=@{passed=$true;replayStorageRoundTrip=$true;replayRetrySameChunk=$true;matchRecorded=($finished.player.history.id -contains $result.id);lessonCount=$lessons.Count;matchId=$result.id}
$report|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $repo 'artifacts/verification/docker-online-check.json') -Encoding utf8
$report|ConvertTo-Json -Compress
