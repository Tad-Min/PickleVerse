param([string]$CatalogPath = "$PSScriptRoot/../src/Seed/UnityCatalog.json")
$ErrorActionPreference = 'Stop'
$catalog = Get-Content -Raw -LiteralPath $CatalogPath | ConvertFrom-Json
function Sql([string]$value) { return "'" + $value.Replace("'", "''") + "'" }
function JsonSql($value) { return (Sql ($value | ConvertTo-Json -Depth 30 -Compress)) + '::jsonb' }
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('-- BEGIN UNITY GAME CONTENT SEED')
$lines.Add('-- Exported from Unity Resources/Progression Config, Shop/Shop Catalog and TrainingCurriculum.')
$lines.Add('BEGIN;')
foreach($currency in @('TOKENPAD','ELITE TOKEN')) {
    $lines.Add("INSERT INTO currencies(currencyname,description) VALUES ($(Sql $currency),$(Sql $currency)) ON CONFLICT(currencyname) DO NOTHING;")
}
foreach($definition in $catalog.definitions) {
    $reward = JsonSql $definition.reward
    if($definition.scope -eq 'Achievement') {
        $lines.Add("INSERT INTO achievements(achievementcode,achievementname,description,type,target,reward) VALUES ($(Sql $definition.id),$(Sql $definition.title),$(Sql $definition.title),$(Sql $definition.metric),$($definition.target),$reward) ON CONFLICT(achievementcode) DO UPDATE SET achievementname=EXCLUDED.achievementname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward;")
    } else {
        $lines.Add("INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ($(Sql $definition.id),$(Sql $definition.title),$(Sql $definition.title),$(Sql $definition.scope),$($definition.target),$reward,$(JsonSql $definition)) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;")
    }
}
foreach($cosmetic in $catalog.cosmetics) {
    $currency = if($cosmetic.tokenpadPrice -gt 0 -or $cosmetic.id -eq 'paddle.default') {'TOKENPAD'} else {'ELITE TOKEN'}
    $price = if($currency -eq 'TOKENPAD') {$cosmetic.tokenpadPrice} else {$cosmetic.elitePrice}
    $lines.Add("INSERT INTO cosmetics(currencyid,itemcode,cosmeticname,description,price,type,config) SELECT currencyid,$(Sql $cosmetic.id),$(Sql $cosmetic.title),$(Sql $cosmetic.description),$price,'Paddle',$(JsonSql $cosmetic) FROM currencies WHERE currencyname=$(Sql $currency) ON CONFLICT(itemcode) DO UPDATE SET currencyid=EXCLUDED.currencyid,cosmeticname=EXCLUDED.cosmeticname,description=EXCLUDED.description,price=EXCLUDED.price,type=EXCLUDED.type,config=EXCLUDED.config;")
    foreach($offer in @(@{name='TOKENPAD';price=$cosmetic.tokenpadPrice},@{name='ELITE TOKEN';price=$cosmetic.elitePrice})) {
        if($offer.price -gt 0 -or ($cosmetic.id -eq 'paddle.default' -and $offer.name -eq 'TOKENPAD')) {
            $lines.Add("INSERT INTO cosmeticprices(cosmeticid,currencyid,price) SELECT c.cosmeticid,u.currencyid,$($offer.price) FROM cosmetics c CROSS JOIN currencies u WHERE c.itemcode=$(Sql $cosmetic.id) AND u.currencyname=$(Sql $offer.name) ON CONFLICT(cosmeticid,currencyid) DO UPDATE SET price=EXCLUDED.price;")
        }
    }
}
$order = 0
foreach($lesson in $catalog.lessons) {
    $order++
    $reward = JsonSql @{coins=$lesson.reward;gems=0;xp=$lesson.xp}
    $lines.Add("INSERT INTO techniques(techniquename,description) VALUES ($(Sql $lesson.skill),$(Sql $lesson.title)) ON CONFLICT(techniquename) DO NOTHING;")
    $lines.Add("INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ($(Sql $lesson.id),$(Sql $lesson.title),$(Sql $lesson.explanation),$(Sql $lesson.skill),$($lesson.required),$reward,$order,$(JsonSql $lesson)) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;")
    $lines.Add("INSERT INTO drills(code,techniqueid,drillname,description) SELECT $(Sql $lesson.id),techniqueid,$(Sql $lesson.title),$(Sql $lesson.coaching) FROM techniques WHERE techniquename=$(Sql $lesson.skill) ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;")
    $lines.Add("INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,$($lesson.required) FROM lessons l CROSS JOIN drills d WHERE l.code=$(Sql $lesson.id) AND d.code=$(Sql $lesson.id) ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;")
}
$settings = @{winXp=$catalog.winXp;lossXp=$catalog.lossXp;dailyBonus=$catalog.dailyBonus;weeklyBonus=$catalog.weeklyBonus;matchRewards=$catalog.matchRewards}
$lines.Add("INSERT INTO gamesettings(key,value) VALUES ('progression',$(JsonSql $settings)) ON CONFLICT(key) DO UPDATE SET value=EXCLUDED.value;")
$lines.Add("INSERT INTO playercosmetics(playerid,cosmeticid,equipped) SELECT p.userid,c.cosmeticid,NOT EXISTS(SELECT 1 FROM playercosmetics pc WHERE pc.playerid=p.userid AND pc.equipped) FROM playerprofiles p CROSS JOIN cosmetics c WHERE c.itemcode='paddle.default' ON CONFLICT(playerid,cosmeticid) DO NOTHING;")
$lines.Add('COMMIT;')
$lines.Add('-- END UNITY GAME CONTENT SEED')
$root = Split-Path $PSScriptRoot -Parent
$database = Join-Path $root 'src/Database.sql'
$original = [System.IO.File]::ReadAllText($database)
$marker = '-- BEGIN ONLINE GAME SCHEMA'
$index = $original.IndexOf($marker,[StringComparison]::Ordinal)
if($index -ge 0) { $original=$original.Substring(0,$index).TrimEnd()+"`n" }
$migration = [System.IO.File]::ReadAllText((Join-Path $root 'src/GameOnlineSetup.sql'))
$output = $original+"`n$marker`n"+$migration+"`n-- END ONLINE GAME SCHEMA`n`n"+($lines -join "`n")+"`n"
[System.IO.File]::WriteAllText($database,$output,[System.Text.UTF8Encoding]::new($false))
Write-Output "Seed generated: 10 achievements, 17 missions, 3 cosmetics, 12 lessons/drills."
