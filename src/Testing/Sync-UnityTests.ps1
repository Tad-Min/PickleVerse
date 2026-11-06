$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../My project')).Path
$assets = (Resolve-Path -LiteralPath (Join-Path $project 'Assets')).Path
$source = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'Unity/EditMode')).Path
$target = Join-Path $assets 'PickleVerse.Testing.Generated'
$markerName = '.pickleverse-testing-generated'
$marker = Join-Path $target $markerName

if ($assets -ne (Join-Path $project 'Assets') -or ((Get-Item -LiteralPath $assets).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Không xác minh được thư mục Assets của PickleVerse.'
}
if (Test-Path -LiteralPath $target) {
    $resolved = Get-Item -LiteralPath $target -Force
    if ($resolved.FullName -ne $target -or !$resolved.PSIsContainer -or
        ($resolved.Attributes -band [IO.FileAttributes]::ReparsePoint) -or !(Test-Path -LiteralPath $marker -PathType Leaf)) {
        throw 'Thư mục đích đã tồn tại và không phải bản sao test được tạo bởi script.'
    }
    $linked = Get-ChildItem -LiteralPath $target -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }
    if ($linked) { throw 'Thư mục test sinh tự động chứa đường dẫn liên kết.' }
    Remove-Item -LiteralPath $target -Recurse -Force
}
New-Item -ItemType Directory -Path $target | Out-Null
New-Item -ItemType File -Path $marker | Out-Null
Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $target -Recurse
}
Copy-Item -LiteralPath ($source + '.meta') -Destination ($target + '.meta') -Force
Write-Output 'Đã đồng bộ test Unity. Mở Window > General > Test Runner > EditMode để chạy.'
