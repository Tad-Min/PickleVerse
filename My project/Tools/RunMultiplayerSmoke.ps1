param(
    [ValidateSet('singles','solos4','duoSolos','duos2','custom1','custom2','custom12','editorGuest','duoControls','customAdmin','partyDeparture','customDeparture')][string]$Case='singles',
    [ValidatePattern('^[A-Za-z0-9]{1,12}$')][string]$RunId=(Get-Date -Format 'HHmmss'),
    [switch]$Lifecycle,
    [switch]$Reconnect,
    [ValidateSet('','hostLeaves','clientLeaves','cancelConnect')][string]$Behavior=''
)
$projectRoot=Split-Path $PSScriptRoot -Parent
$executable=Join-Path $projectRoot 'Builds/Multiplayer/PickleVerse.exe'
if(-not (Test-Path -LiteralPath $executable)) { throw 'Build Windows chưa tồn tại.' }
$receipt="Temp/MultiplayerSmoke/$Case-$RunId"
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'Temp/MultiplayerSmoke') -Force | Out-Null
$specs=@(switch($Case) {
    'singles' { @(@('singles','S1',''),@('singles','S2','')) }
    'solos4' { @(@('doubles','S1',''),@('doubles','S2',''),@('doubles','S3',''),@('doubles','S4','')) }
    'duoSolos' { @(@('duoHost','D1',''),@('duoGuest','D2',''),@('doubles','S1',''),@('doubles','S2','')) }
    'duos2' { @(@('duoHost','D1','A'),@('duoGuest','D2','A'),@('duoHost','D3','B'),@('duoGuest','D4','B')) }
    'custom1' { @(@('customHost','C1',''),@('customGuest','C2','')) }
    'custom2' { @(@('customHost','C1',''),@('customGuest','C2',''),@('customGuest','C3',''),@('customGuest','C4','')) }
    'custom12' { @(@('customHost','C1',''),@('customGuest','C2',''),@('customGuest','C3','')) }
    'editorGuest' { ,@('customGuest','E2','') }
    'duoControls' { @(@('duoControlHost','D1',''),@('duoControlGuest','D2','')) }
    'customAdmin' { @(@('customAdminHost','A1',''),@('customAdminGuest','A2','')) }
    'partyDeparture' { @(@('departurePartyHost','H1',''),@('departurePartyGuest','G2','')) }
    'customDeparture' { @(@('departureCustomHost','H1',''),@('departureCustomGuest','G2','')) }
})
$started=@()
foreach($spec in $specs) {
    $profile="P$RunId$($spec[1])"
    $sharedReceipt="$receipt$($spec[2])"
    $mode=if($Case -in @('custom2','custom12')){'2'}else{'1'}
    $arguments="-batchmode -nographics -pkvCase $($spec[0]) -pkvProfile $profile -pkvMode $mode -pkvReceipt $sharedReceipt -logFile $receipt.$profile.log"
    if($Lifecycle) { $arguments+=' -pkvLifecycle' }
    if($Case -eq 'custom12') { $arguments+=' -pkvCount 3' }
    if($Reconnect) { $arguments+=' -pkvReconnect' }
    if($Behavior) { $arguments+=" -pkvBehavior $Behavior -pkvRole $($spec[1])" }
    if($Behavior -eq 'cancelConnect') { $arguments+=' -pkvSlowCourt' }
    $process=Start-Process -FilePath $executable -WorkingDirectory $projectRoot -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $started+= [pscustomobject]@{Pid=$process.Id;Profile=$profile;Case=$spec[0];Receipt=$sharedReceipt;Executable=$executable}
}
$started | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot "$receipt.processes.json")
$started | Format-Table Pid,Profile,Case,Receipt
