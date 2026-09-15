param(
    [string]$PersonaDirectory = 'D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\PlayerExports\卡拉迪亚编年史\personality_background',
    [string]$GameModulesRoot = 'D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules',
    [string]$WarSailsEnglishDirectory = 'C:\Users\26811\Downloads\战帆英文',
    [string]$WarSailsChineseDirectory = 'C:\Users\26811\Downloads\战帆中文',
    [string]$WarSailsHeroesObjectPath = 'C:\Users\26811\Downloads\heroes.xml',
    [string]$WarSailsClansChineseOverridePath = 'C:\Users\26811\Downloads\std_clans_xml-zho-CN.xml',
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\mappings\persona-family' }

function Get-AttributeValue($Node, [string]$Name) {
    if ($null -eq $Node) { return $null }
    $attribute = $Node.Attributes[$Name]
    if ($null -eq $attribute) { return $null }
    return $attribute.Value
}

function Remove-Prefix([string]$Value, [string]$Prefix) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    if ($Value.StartsWith($Prefix, [StringComparison]::Ordinal)) { return $Value.Substring($Prefix.Length) }
    return $Value
}

function Read-StringFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return @{} }
    $document = [xml](Get-Content -LiteralPath $Path -Raw -Encoding UTF8)
    $rows = @{}
    foreach ($node in $document.SelectNodes('//string')) {
        $id = $node.GetAttribute('id')
        if ([string]::IsNullOrWhiteSpace($id)) { continue }
        $rows[$id] = $node.GetAttribute('text')
    }
    return $rows
}

function Parse-LocalizedAttribute([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return [ordered]@{ key = $null; fallback = $null } }
    $match = [regex]::Match($Value, '^\{=([^}]+)\}(.*)$')
    if ($match.Success) { return [ordered]@{ key = $match.Groups[1].Value; fallback = $match.Groups[2].Value } }
    return [ordered]@{ key = $null; fallback = $Value }
}

function Get-FamilyRoot([string]$HeroId) {
    $match = [regex]::Match($HeroId, '^(?:dead_)?(lord_7_\d+)')
    if ($match.Success) { return $match.Groups[1].Value }
    return $null
}

$heroesPath = Join-Path $GameModulesRoot 'Sandbox\ModuleData\heroes.xml'
$clansPath = Join-Path $GameModulesRoot 'Sandbox\ModuleData\spclans.xml'
$kingdomsPath = Join-Path $GameModulesRoot 'Sandbox\ModuleData\spkingdoms.xml'
$nativeClanEnglishPath = Join-Path $GameModulesRoot 'SandBox\ModuleData\Languages\std_spclans_xml.xml'
$nativeClanChinesePath = Join-Path $GameModulesRoot 'SandBox\ModuleData\Languages\CNs\std_spclans_xml-zho-CN.xml'
$warSailsLordsEnglishPath = Join-Path $WarSailsEnglishDirectory 'std_naval_lords_xml.xml'
$warSailsLordsChinesePath = Join-Path $WarSailsChineseDirectory 'std_naval_lords_xml-zho-CN.xml'
$warSailsHeroesEnglishPath = Join-Path $WarSailsEnglishDirectory 'std_heroes_xml.xml'
$warSailsHeroesChinesePath = Join-Path $WarSailsChineseDirectory 'std_heroes_xml-zho-CN.xml'
$warSailsClansEnglishPath = Join-Path $WarSailsEnglishDirectory 'std_clans_xml.xml'
$warSailsClansChinesePath = if ([string]::IsNullOrWhiteSpace($WarSailsClansChineseOverridePath)) { Join-Path $WarSailsChineseDirectory 'std_clans_xml-zho-CN.xml' } else { $WarSailsClansChineseOverridePath }

foreach ($path in @($PersonaDirectory, $heroesPath, $clansPath, $kingdomsPath, $warSailsLordsEnglishPath, $warSailsLordsChinesePath, $warSailsClansEnglishPath, $warSailsClansChinesePath, $WarSailsHeroesObjectPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Mapping input missing: $path" }
}

$heroes = [xml](Get-Content -LiteralPath $heroesPath -Raw -Encoding UTF8)
$warSailsHeroesObject = [xml](Get-Content -LiteralPath $WarSailsHeroesObjectPath -Raw -Encoding UTF8)
$clans = [xml](Get-Content -LiteralPath $clansPath -Raw -Encoding UTF8)
$kingdoms = [xml](Get-Content -LiteralPath $kingdomsPath -Raw -Encoding UTF8)
$nativeClanEnglish = Read-StringFile $nativeClanEnglishPath
$nativeClanChinese = Read-StringFile $nativeClanChinesePath
$warSailsLordsEnglish = Read-StringFile $warSailsLordsEnglishPath
$warSailsLordsChinese = Read-StringFile $warSailsLordsChinesePath
$warSailsHeroesEnglish = Read-StringFile $warSailsHeroesEnglishPath
$warSailsHeroesChinese = Read-StringFile $warSailsHeroesChinesePath
$warSailsClansEnglish = Read-StringFile $warSailsClansEnglishPath
$warSailsClansChinese = Read-StringFile $warSailsClansChinesePath

$heroById = @{}
foreach ($hero in $heroes.SelectNodes('//Hero')) {
    $id = Get-AttributeValue $hero 'id'
    if ([string]::IsNullOrWhiteSpace($id)) { continue }
    $heroById[$id] = [ordered]@{
        hero_id = $id
        faction_id = Remove-Prefix (Get-AttributeValue $hero 'faction') 'Faction.'
        alive_in_xml = ((Get-AttributeValue $hero 'alive') -ne 'false')
    }
}

$warSailsHeroById = @{}
foreach ($hero in $warSailsHeroesObject.SelectNodes('//Hero')) {
    $id = Get-AttributeValue $hero 'id'
    if ([string]::IsNullOrWhiteSpace($id)) { continue }
    $text = Parse-LocalizedAttribute (Get-AttributeValue $hero 'text')
    $warSailsHeroById[$id] = [ordered]@{
        hero_id = $id
        faction_id = Remove-Prefix (Get-AttributeValue $hero 'faction') 'Faction.'
        spouse_id = Remove-Prefix (Get-AttributeValue $hero 'spouse') 'Hero.'
        father_id = Remove-Prefix (Get-AttributeValue $hero 'father') 'Hero.'
        mother_id = Remove-Prefix (Get-AttributeValue $hero 'mother') 'Hero.'
        text_string_id = $text.key
        text_fallback = $text.fallback
    }
}

$clanById = @{}
$clanNameKeyByName = @{}
foreach ($clan in $clans.SelectNodes('//Faction')) {
    $id = Get-AttributeValue $clan 'id'
    if ([string]::IsNullOrWhiteSpace($id)) { continue }
    $name = Parse-LocalizedAttribute (Get-AttributeValue $clan 'name')
    $clanById[$id] = [ordered]@{
        clan_id = $id
        clan_name_key = $name.key
        clan_name_fallback = $name.fallback
        kingdom_id = Remove-Prefix (Get-AttributeValue $clan 'super_faction') 'Kingdom.'
        culture_id = Remove-Prefix (Get-AttributeValue $clan 'culture') 'Culture.'
        home_settlement_id = Remove-Prefix (Get-AttributeValue $clan 'initial_home_settlement') 'Settlement.'
    }
    if ($name.key) { $clanNameKeyByName[$name.fallback] = $name.key }
}

$kingdomById = @{}
foreach ($kingdom in $kingdoms.SelectNodes('//Kingdom')) {
    $id = Get-AttributeValue $kingdom 'id'
    if ([string]::IsNullOrWhiteSpace($id)) { continue }
    $name = Parse-LocalizedAttribute (Get-AttributeValue $kingdom 'name')
    $kingdomById[$id] = [ordered]@{ kingdom_id = $id; kingdom_name_key = $name.key; kingdom_name_fallback = $name.fallback }
}

$warSailsClanIds = @(@($warSailsClansEnglish.Keys) + @($warSailsClansChinese.Keys) | Sort-Object -Unique)
$warSailsClanRows = foreach ($id in $warSailsClanIds) {
    [ordered]@{
        source_mod = 'war_sails'
        reference_status = 'extension_reference'
        family_name_en = if ($warSailsClansEnglish.ContainsKey($id)) { $warSailsClansEnglish[$id] } else { $null }
        family_name_zh = if ($warSailsClansChinese.ContainsKey($id)) { $warSailsClansChinese[$id] } else { $null }
        family_name_string_id = $id
        runtime_clan_id = $null
        note = if (-not $warSailsClansEnglish.ContainsKey($id)) { 'Chinese language-table key only; English counterpart and War Sails spclans object definition were not present in supplied files.' } else { 'Language-table key only; War Sails spclans object definition was not present in supplied files.' }
    }
}

$warSailsFamilyByZh = @{}
foreach ($row in $warSailsClanRows) { if ($row.family_name_zh) { $warSailsFamilyByZh[$row.family_name_zh] = $row } }

$warSailsBiographyRows = foreach ($id in ($warSailsHeroesEnglish.Keys | Sort-Object)) {
    [ordered]@{
        biography_string_id = $id
        english_text = $warSailsHeroesEnglish[$id]
        chinese_text = if ($warSailsHeroesChinese.ContainsKey($id)) { $warSailsHeroesChinese[$id] } else { $null }
    }
}

$rows = foreach ($file in Get-ChildItem -LiteralPath $PersonaDirectory -File -Filter '*.json' | Sort-Object Name) {
    $parts = $file.BaseName -split '__', 2
    $heroId = $parts[0]
    $displayName = if ($parts.Count -gt 1) { $parts[1] } else { $null }
    $hero = $heroById[$heroId]
    $warSailsHero = $warSailsHeroById[$heroId]
    $nativeClan = if ($hero) { $clanById[$hero.faction_id] } else { $null }
    $nativeKingdom = if ($nativeClan) { $kingdomById[$nativeClan.kingdom_id] } else { $null }
    $nativeClanEnglishName = if ($nativeClan -and $nativeClan.clan_name_key -and $nativeClanEnglish.ContainsKey($nativeClan.clan_name_key)) { $nativeClanEnglish[$nativeClan.clan_name_key] } else { if ($nativeClan) { $nativeClan.clan_name_fallback } else { $null } }
    $nativeClanChineseName = if ($nativeClan -and $nativeClan.clan_name_key -and $nativeClanChinese.ContainsKey($nativeClan.clan_name_key)) { $nativeClanChinese[$nativeClan.clan_name_key] } else { $null }
    $warSailsNameId = @($warSailsLordsChinese.Keys | Where-Object { $warSailsLordsChinese[$_] -eq $displayName } | Select-Object -First 1)
    $warSailsBiography = @($warSailsBiographyRows | Where-Object { $_.chinese_text -like "*$displayName*" -or $_.english_text -like "*$displayName*" } | Select-Object -First 1)
    $warSailsFamilyName = $null
    $warSailsFamilyStatus = 'not_available'
    $warSailsFamilyEvidence = $null
    if ($displayName -eq '阿丝葛莎' -and $warSailsFamilyByZh.ContainsKey('奥特尔')) {
        $warSailsFamilyName = '奥特尔'
        $warSailsFamilyStatus = 'direct_biography_reference'
        $warSailsFamilyEvidence = 'std_heroes_xml: biography names Orthling/奥特尔 together with Asgotha/阿丝葛莎'
    } elseif ($displayName -eq '圭卡' -and $warSailsFamilyByZh.ContainsKey('肖尔德')) {
        $warSailsFamilyName = '肖尔德'
        $warSailsFamilyStatus = 'direct_biography_reference'
        $warSailsFamilyEvidence = 'std_heroes_xml: biography describes Grykka/圭卡 as an offshoot of Kjoldings/肖尔德'
    } elseif ($displayName -eq '哈尔达尔' -and $warSailsFamilyByZh.ContainsKey('许尔夫')) {
        $warSailsFamilyName = '许尔夫'
        $warSailsFamilyStatus = 'contextual_candidate'
        $warSailsFamilyEvidence = 'std_heroes_xml: biography describes the Nordvyg under Skylfing/许尔夫 rule; not a direct Hero faction binding'
    }
    $warSailsFamily = if ($warSailsFamilyName -and $warSailsFamilyByZh.ContainsKey($warSailsFamilyName)) { $warSailsFamilyByZh[$warSailsFamilyName] } else { $null }
    $mappingStatus = if ($hero -and $nativeClan -and $nativeKingdom) { 'native_exact' } elseif ($warSailsHero -and $warSailsHero.faction_id) { 'war_sails_runtime_exact' } elseif ($warSailsFamilyStatus -in @('direct_biography_reference','contextual_candidate')) { 'war_sails_person_and_family_reference' } elseif ($warSailsNameId.Count -gt 0 -or $warSailsBiography.Count -gt 0) { 'war_sails_person_reference_only' } else { 'unresolved' }
    [ordered]@{
        person_name_zh = $displayName
        person_code = $heroId
        family_name_zh = if ($nativeClanChineseName) { $nativeClanChineseName } else { $warSailsFamilyName }
        family_code = if ($nativeClan) { $nativeClan.clan_id } elseif ($warSailsHero) { $warSailsHero.faction_id } else { $null }
        family_code_type = if ($nativeClan) { 'runtime_clan_id_current_game_heroes_xml' } elseif ($warSailsHero -and $warSailsHero.faction_id) { 'runtime_clan_id_war_sails_heroes_xml' } else { $null }
        family_name_en = if ($nativeClanEnglishName) { $nativeClanEnglishName } else { if ($warSailsFamily) { $warSailsFamily.family_name_en } else { $null } }
        family_name_string_id = if ($warSailsFamily) { $warSailsFamily.family_name_string_id } else { $null }
        family_group_code = Get-FamilyRoot $heroId
        native_hero_exists = ($null -ne $hero)
        native_clan_exists = ($null -ne $nativeClan)
        war_sails_hero_exists = ($null -ne $warSailsHero)
        war_sails_family_code = if ($warSailsHero) { $warSailsHero.faction_id } else { $null }
        war_sails_family_code_type = if ($warSailsHero -and $warSailsHero.faction_id) { 'runtime_clan_id_war_sails_heroes_xml' } else { $null }
        war_sails_family_code_evidence = if ($warSailsHero -and $warSailsHero.faction_id) { 'heroes.xml: Hero.faction' } else { $null }
        war_sails_hero_spouse_id = if ($warSailsHero) { $warSailsHero.spouse_id } else { $null }
        war_sails_hero_father_id = if ($warSailsHero) { $warSailsHero.father_id } else { $null }
        war_sails_hero_mother_id = if ($warSailsHero) { $warSailsHero.mother_id } else { $null }
        war_sails_hero_text_string_id = if ($warSailsHero) { $warSailsHero.text_string_id } else { $null }
        native_clan_id = if ($nativeClan) { $nativeClan.clan_id } else { $null }
        native_kingdom_id = if ($nativeClan) { $nativeClan.kingdom_id } else { $null }
        native_culture_id = if ($nativeClan) { $nativeClan.culture_id } else { $null }
        native_home_settlement_id = if ($nativeClan) { $nativeClan.home_settlement_id } else { $null }
        war_sails_name_string_id = if ($warSailsNameId.Count -gt 0) { $warSailsNameId[0] } else { $null }
        war_sails_biography_string_id = if ($warSailsBiography.Count -gt 0) { $warSailsBiography[0].biography_string_id } else { $null }
        war_sails_family_status = $warSailsFamilyStatus
        war_sails_family_evidence = $warSailsFamilyEvidence
        mapping_status = $mappingStatus
        source_policy = if ($nativeClan) { 'current Bannerlord heroes.xml -> spclans.xml' } elseif ($warSailsHero -and $warSailsHero.faction_id) { 'War Sails heroes.xml: Hero.faction' } else { 'AF filename + War Sails language tables; no runtime ClanId inferred' }
    }
}

$rows = @($rows)
$rowsForCsv = @($rows | ForEach-Object { [pscustomobject]$_ })
$warSailsClanRowsForCsv = @($warSailsClanRows | ForEach-Object { [pscustomobject]$_ })
$personaHeroIds = @{}
foreach ($row in $rows) { $personaHeroIds[$row.person_code] = $true }
$warSailsHeroesNotInAfRows = @(
    foreach ($heroId in ($warSailsHeroById.Keys | Where-Object { $_ -match '^(?:dead_)?lord_7_' } | Sort-Object)) {
        if ($personaHeroIds.ContainsKey($heroId)) { continue }
        $warSailsHero = $warSailsHeroById[$heroId]
        [ordered]@{
            source_mod = 'war_sails'
            reference_status = 'object_xml_not_in_af_persona_directory'
            hero_id = $heroId
            family_code = $warSailsHero.faction_id
            family_code_type = 'runtime_clan_id_war_sails_heroes_xml'
            spouse_id = $warSailsHero.spouse_id
            father_id = $warSailsHero.father_id
            mother_id = $warSailsHero.mother_id
            text_string_id = $warSailsHero.text_string_id
            note = 'War Sails heroes.xml contains this Hero, but the AF persona directory has no matching file.'
        }
    }
)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory 'persona-family-mapping.v1.json'
$csvPath = Join-Path $OutputDirectory 'persona-family-mapping.v1.csv'
$simpleCsvPath = Join-Path $OutputDirectory 'persona-family-simple.v1.csv'
$warSailsSimpleCsvPath = Join-Path $OutputDirectory 'war-sails-person-family-simple.v1.csv'
$warSailsClanPath = Join-Path $OutputDirectory 'war-sails-family-reference.v1.csv'
$warSailsHeroesMissingPath = Join-Path $OutputDirectory 'war-sails-heroes-not-in-af.v1.csv'
$readmePath = Join-Path $OutputDirectory 'README.md'

$summary = [ordered]@{
    schema_version = 'awake.persona-family-mapping.v1'
    generated_at_utc = [DateTime]::UtcNow.ToString('O')
    source_policy = 'Current game heroes.xml -> spclans.xml is authoritative for current runtime clan IDs. War Sails heroes.xml supplies direct Hero.faction family IDs for War Sails personas; War Sails language tables supply names/StringIds and textual evidence only.'
    counts = [ordered]@{
        total_persons = $rows.Count
        native_exact = @($rows | Where-Object mapping_status -eq 'native_exact').Count
        war_sails_runtime_exact = @($rows | Where-Object mapping_status -eq 'war_sails_runtime_exact').Count
        war_sails_person_and_family_reference = @($rows | Where-Object mapping_status -eq 'war_sails_person_and_family_reference').Count
        war_sails_person_reference_only = @($rows | Where-Object mapping_status -eq 'war_sails_person_reference_only').Count
        unresolved = @($rows | Where-Object mapping_status -eq 'unresolved').Count
        war_sails_heroes_not_in_af = $warSailsHeroesNotInAfRows.Count
    }
    input_hashes = [ordered]@{
        heroes_xml_sha256 = (Get-FileHash -LiteralPath $heroesPath -Algorithm SHA256).Hash.ToLowerInvariant()
        war_sails_heroes_object_sha256 = (Get-FileHash -LiteralPath $WarSailsHeroesObjectPath -Algorithm SHA256).Hash.ToLowerInvariant()
        spclans_xml_sha256 = (Get-FileHash -LiteralPath $clansPath -Algorithm SHA256).Hash.ToLowerInvariant()
        spkingdoms_xml_sha256 = (Get-FileHash -LiteralPath $kingdomsPath -Algorithm SHA256).Hash.ToLowerInvariant()
        war_sails_naval_lords_sha256 = (Get-FileHash -LiteralPath $warSailsLordsEnglishPath -Algorithm SHA256).Hash.ToLowerInvariant()
        war_sails_clans_sha256 = (Get-FileHash -LiteralPath $warSailsClansEnglishPath -Algorithm SHA256).Hash.ToLowerInvariant()
        war_sails_clans_chinese_sha256 = (Get-FileHash -LiteralPath $warSailsClansChinesePath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    rows = $rows
}

$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$rowsForCsv | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
$rowsForCsv | Select-Object person_name_zh,person_code,family_name_zh,family_code,family_code_type,mapping_status | Export-Csv -LiteralPath $simpleCsvPath -NoTypeInformation -Encoding UTF8
$rowsForCsv | Where-Object { $_.person_code -match '^(dead_)?lord_7_' } | Select-Object person_name_zh,person_code,family_name_zh,family_code,family_code_type,family_group_code,family_name_string_id,war_sails_name_string_id,war_sails_family_status,mapping_status | Export-Csv -LiteralPath $warSailsSimpleCsvPath -NoTypeInformation -Encoding UTF8
$warSailsClanRowsForCsv | Export-Csv -LiteralPath $warSailsClanPath -NoTypeInformation -Encoding UTF8
$warSailsHeroesNotInAfRows | ForEach-Object { [pscustomobject]$_ } | Export-Csv -LiteralPath $warSailsHeroesMissingPath -NoTypeInformation -Encoding UTF8
@"
# 人物—家族映射

- Schema: awake.persona-family-mapping.v1
- 总人物：$($rows.Count)
- 当前游戏 XML 精确绑定：$(@($rows | Where-Object mapping_status -eq 'native_exact').Count)
- 战帆对象 XML 精确绑定：$(@($rows | Where-Object mapping_status -eq 'war_sails_runtime_exact').Count)
- 战帆人物与家族文本参考：$(@($rows | Where-Object mapping_status -eq 'war_sails_person_and_family_reference').Count)
- 只有战帆人物姓名键参考：$(@($rows | Where-Object mapping_status -eq 'war_sails_person_reference_only').Count)
- 尚未解析：$(@($rows | Where-Object mapping_status -eq 'unresolved').Count)
- 战帆对象 XML 中但 AF 没有对应人物文件：$($warSailsHeroesNotInAfRows.Count)

主表字段：

- person_name_zh：人物中文名
- person_code：人物代码，例如 lord_1_1、lord_7_1
- family_name_zh：家族中文名
- family_code：优先填写当前游戏真实运行时 ClanId；当前游戏没有时，填写战帆 `heroes.xml` 的 `Hero.faction` 运行时 ClanId
- family_code_type：标记运行时家族代码来自当前游戏对象 XML 还是战帆对象 XML
- family_name_string_id：战帆家族本地化键，不等于运行时 ClanId
- family_group_code：从 lord_7_* 文件名层级提取的家族组根代码，仅作参考，不等于 ClanId
- war_sails_name_string_id：战帆具名人物姓名键
- war_sails_family_code：战帆 `heroes.xml` 直接提供的 `Hero.faction`，可用于战帆人物视角过滤
- mapping_status：证据等级和映射状态

边界：

1. 当前游戏 `heroes.xml -> spclans.xml` 是当前游戏人物—家族代码的权威。
2. 新增战帆 `heroes.xml` 是对象数据；其中 `Hero.id` 与 `Hero.faction` 可直接提供战帆人物和运行时家族代码。
3. `std_clans_xml-zho-CN.xml` 只是中文本地化表。它能提供家族中文名和 StringId，但没有说明哪个 StringId 对应哪个 `clan_nord_*`，因此不按文件顺序猜配。
4. family_name_zh 为空不代表人物没有家族，而是代表目前没有足够证据把中文家族名绑定到该运行时家族代码。
5. `war-sails-heroes-not-in-af.v1.csv` 列出战帆对象 XML 有、AF 人物目录没有的额外人物。

输出：

- `persona-family-mapping.v1.csv`：内容编辑者可查看的人物—家族表。
- `persona-family-simple.v1.csv`：只保留人物、人物代码、家族、家族代码和状态的简表。
- `war-sails-person-family-simple.v1.csv`：只列出 `lord_7_*` / `dead_lord_7_*` 战帆参考人物。
- `persona-family-mapping.v1.json`：机器读取的完整映射和输入哈希。
- `war-sails-family-reference.v1.csv`：战帆家族名称键参考表。
- `war-sails-heroes-not-in-af.v1.csv`：战帆对象 XML 中未进入 AF 人物目录的额外人物。
"@ | Set-Content -LiteralPath $readmePath -Encoding UTF8

Write-Output "JSON: $jsonPath"
Write-Output "CSV: $csvPath"
Write-Output "SIMPLE_CSV: $simpleCsvPath"
Write-Output "WAR_SAILS_SIMPLE_CSV: $warSailsSimpleCsvPath"
Write-Output "WAR_SAILS_CLANS: $warSailsClanPath"
Write-Output "WAR_SAILS_HEROES_NOT_IN_AF: $warSailsHeroesMissingPath"
Write-Output "COUNTS: $($summary.counts | ConvertTo-Json -Compress)"
