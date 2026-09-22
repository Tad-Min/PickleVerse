# Build-time only. Requires PowerShell 7. Never called by the Unity player.
[CmdletBinding()]
param(
    [string]$ProjectId = $env:GOOGLE_CLOUD_PROJECT,
    [switch]$DryRun,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Use PowerShell 7 (pwsh) for UTF-8 Vietnamese text.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$voiceDirectory = Join-Path $projectRoot 'Assets/OfflineCoach/Voice'
$phrases = [ordered]@{
    InsufficientData = 'Chưa thấy rõ động tác. Hãy giữ vai, hông và tay trong khung hình.'
    StraightKnees = 'Thử chùng gối nhẹ ở tư thế chuẩn bị.'
    ShortFinish = 'Thử hoàn tất động tác vung vợt.'
    Recorded = 'Đã ghi nhận cú đánh. Tiếp tục giữ nhịp nhé.'
}
if ($DryRun) {
    $phrases.GetEnumerator() | ForEach-Object { [pscustomobject]@{File=($_.Key + '.wav'); Voice='vi-VN-Standard-A'; Text=$_.Value} }
    return
}
# Only these four fixed sentences leave the machine. No player inputs are read.
$headers = @{}
$accessToken = $null
try {
    if ($env:GOOGLE_API_KEY) {
        $headers['x-goog-api-key'] = $env:GOOGLE_API_KEY
    } else {
        if (-not (Get-Command gcloud -ErrorAction SilentlyContinue)) { throw 'Configure Google Cloud CLI and ADC locally; do not paste credentials into chat or the project.' }
        $accessToken = (& gcloud auth application-default print-access-token 2>$null)
        if ($LASTEXITCODE -ne 0 -or -not $accessToken) { throw 'ADC unavailable. Run gcloud auth application-default login locally.' }
        $headers.Authorization = 'Bearer ' + ($accessToken -join '').Trim()
        if (-not $ProjectId) { throw 'Set GOOGLE_CLOUD_PROJECT or pass -ProjectId for ADC quota billing.' }
        $headers['x-goog-user-project'] = $ProjectId
    }
    [void][System.IO.Directory]::CreateDirectory($voiceDirectory)
    foreach ($phrase in $phrases.GetEnumerator()) {
        $outputPath = Join-Path $voiceDirectory ($phrase.Key + '.wav')
        if ((Test-Path -LiteralPath $outputPath) -and -not $Force) { Write-Output ("Keep existing: " + $phrase.Key); continue }
        $body = @{
            input = @{text=$phrase.Value}
            voice = @{languageCode='vi-VN'; name='vi-VN-Standard-A'; ssmlGender='FEMALE'}
            audioConfig = @{audioEncoding='LINEAR16'; sampleRateHertz=24000; speakingRate=1.0}
        } | ConvertTo-Json -Depth 4
        try {
            $response = Invoke-RestMethod -Uri 'https://texttospeech.googleapis.com/v1/text:synthesize' -Method Post -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($body))
        } catch {
            # Do not echo a request/headers or the credential-bearing exception.
            throw ('Google TTS failed for ' + $phrase.Key + '. Check TTS API, billing and local credential permissions.')
        }
        $bytes = [Convert]::FromBase64String($response.audioContent)
        if ($bytes.Length -lt 44 -or [Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne 'RIFF' -or [Text.Encoding]::ASCII.GetString($bytes, 8, 4) -ne 'WAVE') {
            throw 'TTS did not return a valid WAV container.'
        }
        [System.IO.File]::WriteAllBytes($outputPath, $bytes)
        Write-Output ('Generated ' + $phrase.Key + '.wav')
    }
    Write-Output 'In Unity: PickleVerse > Coach > Install or refresh in Training. Listen to all four clips before accepting voice completion.'
} finally {
    $accessToken = $null
    $headers.Clear()
}

