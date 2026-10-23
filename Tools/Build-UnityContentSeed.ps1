param([string]$CatalogPath = "$PSScriptRoot/../src/Seed/UnityCatalog.json")
$ErrorActionPreference = 'Stop'
$catalog = Get-Content -Raw -LiteralPath $CatalogPath | ConvertFrom-Json
function Sql([string]$value) { return "'" + $value.Replace("'", "''") + "'" }
function JsonSql($value) { return (Sql ($value | ConvertTo-Json -Depth 30 -Compress)) + '::jsonb' }
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('-- BEGIN UNITY GAME CONTENT SEED')
$lines.Add('-- Exported from Unity Resources/Progression Config, Shop/Shop Catalog and TrainingCurriculum.')
foreach($currency in @('TOKENPAD','ELITE TOKEN')) {
    $lines.Add("INSERT INTO Currencies(CurrencyName,Description) VALUES ($(Sql $currency),$(Sql $currency)) ON CONFLICT(CurrencyName) DO NOTHING;")
}
foreach($definition in $catalog.definitions) {
    $reward = JsonSql $definition.reward
    if($definition.scope -eq 'Achievement') {
        $lines.Add("INSERT INTO Achievements(AchievementCode,AchievementName,Description,Type,Target,Reward) VALUES ($(Sql $definition.id),$(Sql $definition.title),$(Sql $definition.title),$(Sql $definition.metric),$($definition.target),$reward) ON CONFLICT(AchievementCode) DO UPDATE SET AchievementName=EXCLUDED.AchievementName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward;")
    } else {
        $lines.Add("INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ($(Sql $definition.id),$(Sql $definition.title),$(Sql $definition.title),$(Sql $definition.scope),$($definition.target),$reward,$(JsonSql $definition)) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;")
    }
}
foreach($cosmetic in $catalog.cosmetics) {
    $currency = if($cosmetic.tokenpadPrice -gt 0 -or $cosmetic.id -eq 'paddle.default') {'TOKENPAD'} else {'ELITE TOKEN'}
    $price = if($currency -eq 'TOKENPAD') {$cosmetic.tokenpadPrice} else {$cosmetic.elitePrice}
    $lines.Add("INSERT INTO Cosmetics(CurrencyId,ItemCode,CosmeticName,Description,Price,Type,Config) SELECT CurrencyId,$(Sql $cosmetic.id),$(Sql $cosmetic.title),$(Sql $cosmetic.description),$price,'Paddle',$(JsonSql $cosmetic) FROM Currencies WHERE CurrencyName=$(Sql $currency) ON CONFLICT(ItemCode) DO UPDATE SET CurrencyId=EXCLUDED.CurrencyId,CosmeticName=EXCLUDED.CosmeticName,Description=EXCLUDED.Description,Price=EXCLUDED.Price,Type=EXCLUDED.Type,Config=EXCLUDED.Config;")
    foreach($offer in @(@{name='TOKENPAD';price=$cosmetic.tokenpadPrice},@{name='ELITE TOKEN';price=$cosmetic.elitePrice})) {
        if($offer.price -gt 0 -or ($cosmetic.id -eq 'paddle.default' -and $offer.name -eq 'TOKENPAD')) {
            $lines.Add("INSERT INTO CosmeticPrices(CosmeticId,CurrencyId,Price) SELECT c.CosmeticId,u.CurrencyId,$($offer.price) FROM Cosmetics c CROSS JOIN Currencies u WHERE c.ItemCode=$(Sql $cosmetic.id) AND u.CurrencyName=$(Sql $offer.name) ON CONFLICT(CosmeticId,CurrencyId) DO UPDATE SET Price=EXCLUDED.Price;")
        }
    }
}
$order = 0
foreach($lesson in $catalog.lessons) {
    $order++
    $reward = JsonSql @{coins=$lesson.reward;gems=0;xp=$lesson.xp}
    $lines.Add("INSERT INTO Techniques(TechniqueName,Description) VALUES ($(Sql $lesson.skill),$(Sql $lesson.title)) ON CONFLICT(TechniqueName) DO NOTHING;")
    $lines.Add("INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ($(Sql $lesson.id),$(Sql $lesson.title),$(Sql $lesson.explanation),$(Sql $lesson.skill),$($lesson.required),$reward,$order,$(JsonSql $lesson)) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;")
    $lines.Add("INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT $(Sql $lesson.id),TechniqueId,$(Sql $lesson.title),$(Sql $lesson.coaching) FROM Techniques WHERE TechniqueName=$(Sql $lesson.skill) ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;")
    $lines.Add("INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,$($lesson.required) FROM Lessons l CROSS JOIN Drills d WHERE l.Code=$(Sql $lesson.id) AND d.Code=$(Sql $lesson.id) ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;")
}
$lines.Add("INSERT INTO PlayerCosmetics(PlayerId,CosmeticId,Equipped) SELECT p.UserId,c.CosmeticId,NOT EXISTS(SELECT 1 FROM PlayerCosmetics pc WHERE pc.PlayerId=p.UserId AND pc.Equipped) FROM PlayerProfiles p CROSS JOIN Cosmetics c WHERE c.ItemCode='paddle.default' ON CONFLICT(PlayerId,CosmeticId) DO NOTHING;")
$lines.Add('-- END UNITY GAME CONTENT SEED')
$root = Split-Path $PSScriptRoot -Parent
$database = Join-Path $root 'src/Database.sql'
$original = [System.IO.File]::ReadAllText($database)
$startMarker = '-- BEGIN UNITY GAME CONTENT SEED'
$endMarker = '-- END UNITY GAME CONTENT SEED'
$start = $original.IndexOf($startMarker,[StringComparison]::Ordinal)
$end = $original.IndexOf($endMarker,[StringComparison]::Ordinal)
if($start -lt 0 -or $end -lt $start -or
    $original.IndexOf($startMarker,$start+$startMarker.Length,[StringComparison]::Ordinal) -ge 0 -or
    $original.IndexOf($endMarker,$end+$endMarker.Length,[StringComparison]::Ordinal) -ge 0) {
    throw 'Database.sql must contain exactly one complete Unity seed block.'
}
$output = $original.Substring(0,$start)+($lines -join "`n")+$original.Substring($end+$endMarker.Length)
[System.IO.File]::WriteAllText($database,$output,[System.Text.UTF8Encoding]::new($false))
Write-Output "Seed generated: 10 achievements, 17 missions, 3 cosmetics, 12 lessons/drills."
