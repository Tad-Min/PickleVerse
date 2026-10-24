[CmdletBinding()]
param(
    [string]$Container = 'pickleverse_postgres',
    [string]$Database = 'pickleverse',
    [string]$User = 'sa',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../UnityWork/CatalogTextRepair'),
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)
$source = Join-Path $PSScriptRoot '../src/Database.sql'
$updates = [System.Collections.Generic.List[string]]::new()
$counts = @{}

function SqlLiteral([string]$Value) { return "'" + $Value.Replace("'", "''") + "'" }
function IsCorrupt([string]$Expression) {
    return "(position('?' in $Expression) > 0 OR position(chr(65533) in $Expression) > 0)"
}
function AddRepair([string]$Table, [string]$KeyColumn, [string]$Key,
    [System.Collections.IDictionary]$Text, [System.Collections.IDictionary]$JsonText) {
    $assignments = [System.Collections.Generic.List[string]]::new()
    $conditions = [System.Collections.Generic.List[string]]::new()
    foreach ($column in $Text.Keys) {
        $value = [string]$Text[$column]
        if ($value -match '[?\uFFFD]') { throw "Corrupt canonical text in $Table/$Key/$column" }
        $bad = IsCorrupt $column
        $conditions.Add($bad)
        $assignments.Add("$column = CASE WHEN $bad THEN $(SqlLiteral $value) ELSE $column END")
    }
    if ($JsonText.Count -gt 0) {
        $expression = 'config'
        foreach ($field in $JsonText.Keys) {
            $value = [string]$JsonText[$field]
            if ($value -match '[?\uFFFD]') { throw "Corrupt canonical JSON in $Table/$Key/$field" }
            $bad = IsCorrupt "(config->>$(SqlLiteral $field))"
            $conditions.Add($bad)
            $expression += " || (CASE WHEN $bad THEN jsonb_build_object($(SqlLiteral $field), $(SqlLiteral $value)::text) ELSE '{}'::jsonb END)"
        }
        $assignments.Add("config = $expression")
    }
    $updates.Add("UPDATE $Table SET $($assignments -join ', ') WHERE $KeyColumn = $(SqlLiteral $Key) AND ($($conditions -join ' OR '));")
    if (!$counts.ContainsKey($Table)) { $counts[$Table] = 0 }
    $counts[$Table]++
}

# Read canonical SQL as bytes/UTF-8. Never pipe Vietnamese text through a shell's native encoding.
foreach ($line in [System.IO.File]::ReadAllLines($source, $utf8)) {
    if ($line -notmatch '^INSERT INTO (Achievements|Missions|Lessons|Drills|Cosmetics|Techniques)\(') { continue }
    $table = $Matches[1]
    $values = @([regex]::Matches($line, "'(?:[^']|'')*'") | ForEach-Object {
        $_.Value.Substring(1, $_.Value.Length - 2).Replace("''", "'")
    })
    switch ($table) {
        'Achievements' {
            if ($values.Count -ne 5) { throw 'Unexpected achievement seed format.' }
            AddRepair $table 'AchievementCode' $values[0] ([ordered]@{AchievementName=$values[1]; Description=$values[2]}) @{}
        }
        'Missions' {
            if ($values.Count -ne 6) { throw 'Unexpected mission seed format.' }
            $config = $values[5] | ConvertFrom-Json
            AddRepair $table 'Code' $values[0] ([ordered]@{MissionName=$values[1]; Description=$values[2]}) ([ordered]@{title=$config.title})
        }
        'Lessons' {
            if ($values.Count -ne 6) { throw 'Unexpected lesson seed format.' }
            $config = $values[5] | ConvertFrom-Json
            AddRepair $table 'Code' $values[0] ([ordered]@{LessonName=$values[1]; Description=$values[2]}) ([ordered]@{title=$config.title; explanation=$config.explanation; coaching=$config.coaching})
        }
        'Drills' {
            if ($values.Count -ne 4) { throw 'Unexpected drill seed format.' }
            AddRepair $table 'Code' $values[0] ([ordered]@{DrillName=$values[1]; Description=$values[2]}) @{}
        }
        'Techniques' {
            if ($values.Count -ne 2) { throw 'Unexpected technique seed format.' }
            AddRepair $table 'TechniqueName' $values[0] ([ordered]@{Description=$values[1]}) @{}
        }
        'Cosmetics' {
            if ($values.Count -ne 6) { throw 'Unexpected cosmetic seed format.' }
            $config = $values[4] | ConvertFrom-Json
            AddRepair $table 'ItemCode' $values[0] ([ordered]@{CosmeticName=$values[1]; Description=$values[2]}) ([ordered]@{title=$config.title; description=$config.description; collection=$config.collection})
        }
    }
}
if ($counts.Count -ne 6 -or $updates.Count -lt 50) { throw 'Canonical catalog seed is incomplete; no database changes applied.' }

$directory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($directory) | Out-Null
$stamp = [guid]::NewGuid().ToString('N')
$sqlPath = Join-Path $directory "repair-$stamp.sql"
$backupPath = Join-Path $directory "before-$stamp.sql"
$sql = "\set ON_ERROR_STOP on`nSET client_encoding TO 'UTF8';`nSET standard_conforming_strings TO on;`nBEGIN;`n" + ($updates -join "`n") + "`nCOMMIT;`n"
[System.IO.File]::WriteAllText($sqlPath, $sql, $utf8)
Write-Output "Prepared $($updates.Count) catalog text repairs from src/Database.sql: $sqlPath"
Write-Output 'Only corrupted seeded text is replaced. Existing gameplay config and player data are preserved.'
if (!$Apply) { Write-Output 'Preview only. Pass -Apply to back up the catalog tables and execute the transaction.'; return }

$remoteSql = "/tmp/pkv-catalog-$stamp.sql"
$remoteBackup = "/tmp/pkv-catalog-$stamp-backup.sql"
try {
    $dumpArguments = @('exec', $Container, 'pg_dump', '-U', $User, '-d', $Database,
        '--data-only', '--column-inserts', '--no-owner', '--no-privileges', '--file', $remoteBackup)
    foreach ($table in $counts.Keys) { $dumpArguments += "--table=public.$($table.ToLowerInvariant())" }
    & docker @dumpArguments
    if ($LASTEXITCODE -ne 0) { throw 'Catalog backup failed; repair was not started.' }
    & docker cp "${Container}:$remoteBackup" $backupPath
    if ($LASTEXITCODE -ne 0) { throw 'Cannot save catalog backup; repair was not started.' }
    & docker cp $sqlPath "${Container}:$remoteSql"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot copy UTF-8 repair script.' }
    & docker exec $Container psql -U $User -d $Database -X --set=ON_ERROR_STOP=1 --file $remoteSql
    if ($LASTEXITCODE -ne 0) { throw 'Repair failed; its transaction was rolled back.' }
    Write-Output "Catalog text repaired. Backup: $backupPath"
}
finally {
    & docker exec $Container rm -f -- $remoteSql $remoteBackup
}
