param([Parameter(Mandatory=$true)][string]$MatchId)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$reports=@()
foreach($file in @('unity-account-fixture.json','photon-account-fixture.json')) {
    $fixture=Get-Content -LiteralPath (Join-Path $taskRoot "artifacts/$file") -Raw | ConvertFrom-Json
    $login=Invoke-RestMethod -Method Post -Uri http://localhost:8080/api/Auth/login -ContentType application/json -Body (@{email=$fixture.email;password=$fixture.password}|ConvertTo-Json)
    $headers=@{Authorization="Bearer $($login.accessToken)"}
    $route="http://localhost:8080/api/Game/replays/$MatchId"
    $metadata=Invoke-RestMethod -Uri $route -Headers $headers
    $upload=Invoke-RestMethod -Uri "$route/chunks/0" -Headers $headers
    $bytes=[Convert]::FromBase64String($upload.data)
    $memory=[IO.MemoryStream]::new($bytes)
    $gzip=[IO.Compression.GZipStream]::new($memory,[IO.Compression.CompressionMode]::Decompress)
    $reader=[IO.StreamReader]::new($gzip)
    try {$chunk=$reader.ReadToEnd()|ConvertFrom-Json} finally {$reader.Dispose();$gzip.Dispose();$memory.Dispose()}
    $reports+=@{playerMatches=([Guid]$metadata.playerId -eq [Guid]$fixture.userId);complete=$metadata.complete;chunks=$metadata.chunks.Count;actors=$metadata.actors.Count;frames=$chunk.frames.Count;posePresent=($chunk.frames[0].actors[0].bones.Count -gt 0)}
}
$passed=@($reports|Where-Object{-not $_.playerMatches -or -not $_.complete -or $_.chunks -lt 1 -or $_.actors -ne 2 -or $_.frames -lt 1 -or -not $_.posePresent}).Count -eq 0
@{matchId=$MatchId;players=$reports;passed=$passed}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $taskRoot 'artifacts/verification/photon-replay-check.json') -Encoding utf8
if(-not $passed){throw 'Photon replay round-trip failed.'}
Write-Output 'PASS: both Photon clients restored compressed replay frames and bone poses through the authenticated API/S3.'
