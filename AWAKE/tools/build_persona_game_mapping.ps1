param(
    [string]$PersonaDirectory = 'D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\PlayerExports\卡拉迪亚编年史\personality_background',
    [string]$GameModulesRoot = 'D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules',
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\mappings\persona-game' }

function Remove-Prefix([string]$Value, [string]$Prefix) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    if ($Value.StartsWith($Prefix, [StringComparison]::Ordinal)) { return $Value.Substring($Prefix.Length) }
    return $Value
}

function Get-AttributeValue($Node, [string]$Name) {
    if ($null -eq $Node) { return $null }
    $attribute = $Node.Attributes[$Name]
    if ($null -eq $attribute) { return $null }
    return $attribute.Value
}

$heroesPath = Join-Path $GameModulesRoot 'Sandbox\ModuleData\heroes.xml'
$clansPath = Join-Path $GameModulesRoot 'Sandbox\ModuleData\spclans.xml'
$kingdomsPath = Join-Path $GameModulesRoot 'Sandbox\ModuleData\spkingdoms.xml'
foreach ($path in @($heroesPath, $clansPath, $kingdomsPath, $PersonaDirectory)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Mapping input missing: $path" }
}

$heroes = [xml](Get-Content -LiteralPath $heroesPath -Raw -Encoding UTF8)
$clans = [xml](Get-Content -LiteralPath $clansPath -Raw -Encoding UTF8)
$kingdoms = [xml](Get-Content -LiteralPath $kingdomsPath -Raw -Encoding UTF8)

$heroById = @{}
foreach ($hero in $heroes.SelectNodes('//Hero')) {
    $id = Get-AttributeValue $hero 'id'
    if ([string]::IsNullOrWhiteSpace($id)) { continue }
    $heroById[$id] = [ordered]@{
        hero_id = $id
        faction_id = Remove-Prefix (Get-AttributeValue $hero 'faction') 'Faction.'
        father_id = Remove-Prefix (Get-AttributeValue $hero 'father') 'Hero.'
        mother_id = Remove-Prefix (Get-AttributeValue $hero 'mother') 'Hero.'
        spouse_id = Remove-Prefix (Get-AttributeValue $hero 'spouse') 'Hero.'
        alive_in_xml = ((Get-AttributeValue $hero 'alive') -ne 'false')
    }
}

$clanById = @{}
foreach ($clan in $clans.SelectNodes('//Faction')) {
    $id = Get-AttributeValue $clan 'id'
    if ([string]::IsNullOrWhiteSpace($id)) { continue }
    $clanById[$id] = [ordered]@{
        clan_id = $id
        clan_name_key = Get-AttributeValue $clan 'name'
        kingdom_id = Remove-Prefix (Get-AttributeValue $clan 'super_faction') 'Kingdom.'
        culture_id = Remove-Prefix (Get-AttributeValue $clan 'culture') 'Culture.'
        owner_hero_id = Remove-Prefix (Get-AttributeValue $clan 'owner') 'Hero.'
        home_settlement_id = Remove-Prefix (Get-AttributeValue $clan 'initial_home_settlement') 'Settlement.'
        is_noble = ((Get-AttributeValue $clan 'is_noble') -eq 'true')
    }
}

$kingdomById = @{}
foreach ($kingdom in $kingdoms.SelectNodes('//Kingdom')) {
    $id = Get-AttributeValue $kingdom 'id'
    if ([string]::IsNullOrWhiteSpace($id)) { continue }
    $kingdomById[$id] = [ordered]@{
        kingdom_id = $id
        kingdom_name_key = Get-AttributeValue $kingdom 'name'
        culture_id = Remove-Prefix (Get-AttributeValue $kingdom 'culture') 'Culture.'
        owner_hero_id = Remove-Prefix (Get-AttributeValue $kingdom 'owner') 'Hero.'
    }
}

$rows = foreach ($file in Get-ChildItem -LiteralPath $PersonaDirectory -File -Filter '*.json' | Sort-Object Name) {
    $parts = $file.BaseName -split '__', 2
    $heroId = $parts[0]
    $displayName = if ($parts.Count -gt 1) { $parts[1] } else { $null }
    $hero = $heroById[$heroId]
    $clan = if ($hero) { $clanById[$hero.faction_id] } else { $null }
    $kingdom = if ($clan) { $kingdomById[$clan.kingdom_id] } else { $null }
    [ordered]@{
        source_file = $file.Name
        source_path = $file.FullName
        display_name_from_filename = $displayName
        hero_id = $heroId
        native_hero_exists = ($null -ne $hero)
        faction_id = if ($hero) { $hero.faction_id } else { $null }
        clan_id = if ($clan) { $clan.clan_id } else { $null }
        clan_name_key = if ($clan) { $clan.clan_name_key } else { $null }
        kingdom_id = if ($clan) { $clan.kingdom_id } else { $null }
        kingdom_name_key = if ($kingdom) { $kingdom.kingdom_name_key } else { $null }
        culture_id = if ($clan) { $clan.culture_id } else { $null }
        home_settlement_id = if ($clan) { $clan.home_settlement_id } else { $null }
        clan_owner_hero_id = if ($clan) { $clan.owner_hero_id } else { $null }
        is_noble_clan = if ($clan) { $clan.is_noble } else { $null }
        match_status = if ($hero -and $clan -and $kingdom) { 'exact' } elseif ($hero) { 'hero_found_but_clan_or_kingdom_missing' } else { 'missing_native_hero' }
        binding_note = if ($hero -and $clan -and $kingdom) { 'AF filename -> current heroes.xml -> current spclans.xml -> current spkingdoms.xml' } else { 'AF filename retained; no current-game binding inferred' }
    }
}

$rows = @($rows)
$summary = [ordered]@{
    schema_version = 'awake.persona-game-mapping.v1'
    generated_at_utc = [DateTime]::UtcNow.ToString('O')
    game_modules_root = $GameModulesRoot
    persona_directory = $PersonaDirectory
    source_policy = 'Read AF filenames only; do not read personality_background JSON content.'
    authority = 'Current local Bannerlord module XML for hero, clan and kingdom relations.'
    counts = [ordered]@{
        persona_files = $rows.Count
        exact = @($rows | Where-Object match_status -eq 'exact').Count
        missing_native_hero = @($rows | Where-Object match_status -eq 'missing_native_hero').Count
        hero_found_but_clan_or_kingdom_missing = @($rows | Where-Object match_status -eq 'hero_found_but_clan_or_kingdom_missing').Count
    }
    input_hashes = [ordered]@{
        heroes_xml_sha256 = (Get-FileHash -LiteralPath $heroesPath -Algorithm SHA256).Hash.ToLowerInvariant()
        spclans_xml_sha256 = (Get-FileHash -LiteralPath $clansPath -Algorithm SHA256).Hash.ToLowerInvariant()
        spkingdoms_xml_sha256 = (Get-FileHash -LiteralPath $kingdomsPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    rows = $rows
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory 'persona-game-mapping.v1.json'
$csvPath = Join-Path $OutputDirectory 'persona-game-mapping.v1.csv'
$missingPath = Join-Path $OutputDirectory 'persona-game-mapping-missing-current-hero.v1.csv'
$summaryPath = Join-Path $OutputDirectory 'README.md'

$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$rows | Select-Object source_file,display_name_from_filename,hero_id,native_hero_exists,faction_id,clan_id,clan_name_key,kingdom_id,kingdom_name_key,culture_id,home_settlement_id,clan_owner_hero_id,is_noble_clan,match_status,binding_note | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
$rows | Where-Object match_status -ne 'exact' | Select-Object source_file,display_name_from_filename,hero_id,match_status | Export-Csv -LiteralPath $missingPath -NoTypeInformation -Encoding UTF8

@"
# Persona → Bannerlord mapping

- Schema: `awake.persona-game-mapping.v1`
- AF source: `$PersonaDirectory`
- Mapping policy: only AF filenames are read for persona names; AF JSON body is not treated as authority.
- Game authority: `heroes.xml` → `spclans.xml` → `spkingdoms.xml`.
- Persona files: $($rows.Count)
- Exact current-game bindings: $(@($rows | Where-Object match_status -eq 'exact').Count)
- Missing current Hero entries: $(@($rows | Where-Object match_status -eq 'missing_native_hero').Count)

Files:

- `persona-game-mapping.v1.json`: complete machine-readable mapping with provenance and hashes.
- `persona-game-mapping.v1.csv`: editor-friendly table.
- `persona-game-mapping-missing-current-hero.v1.csv`: AF names with no current `heroes.xml` entry; do not infer their clan automatically.

Important:

- `display_name_from_filename` is the Chinese name supplied by the AF filename.
- `hero_id` is the game Hero code when the current XML contains it.
- `clan_id` and `kingdom_id` are resolved only through current game XML.
- `clan_name_key` and `kingdom_name_key` are native localization keys, not translated display names.
- A future game/mod version requires regenerating this mapping and comparing input hashes.
"@ | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Output "MAPPING: $jsonPath"
Write-Output "CSV: $csvPath"
Write-Output "MISSING: $missingPath"
Write-Output "COUNTS: $($summary.counts | ConvertTo-Json -Compress)"
