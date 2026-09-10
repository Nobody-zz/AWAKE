param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
$generatorVersion = '1.0.0'
$normalizationVersion = 'json-canonical-v1'
$root = [IO.Path]::GetFullPath($Root)
$outputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) { Join-Path $root 'docs\mappings\persona-entity' } else { [IO.Path]::GetFullPath($OutputRoot) }
$familyPath = Join-Path $root 'docs\mappings\persona-family\persona-family-mapping.v1.json'
$gamePath = Join-Path $root 'docs\mappings\persona-game\persona-game-mapping.v1.json'
$schemaRoot = Join-Path $root 'docs\worldbook-studio-plan'
$utf8 = [Text.UTF8Encoding]::new($false, $true)

function Get-RawFile([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    [pscustomobject]@{ Bytes = $bytes; Sha256 = ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))); Text = $utf8.GetString($bytes) }
}

function ConvertTo-StableJson($Value) {
    $text = $Value | ConvertTo-Json -Depth 30 -Compress
    return ($text.Replace("`r`n", "`n").Replace("`r", "`n"))
}

function Write-StableJson([string]$Path, $Value) {
    $text = ConvertTo-StableJson $Value
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $text + "`n", $utf8)
}

function Add-Diagnostic($List, [string]$Code, [string]$Severity, [string]$Message, [string]$Path, [string]$Detail) {
    $List.Add([ordered]@{ code = $Code; severity = $Severity; message = $Message; path = $Path; detail = $Detail })
}

function Normalize-Code([string]$Code) {
    if ([string]::IsNullOrWhiteSpace($Code)) { return '' }
    return $Code.Trim().ToLowerInvariant()
}

function Get-Status($Rows) {
    $statuses = @($Rows | ForEach-Object { [string]$_.mapping_status } | Sort-Object -Unique)
    if ($statuses.Count -eq 1 -and $statuses[0] -eq 'native_exact') { return [ordered]@{ WorldSource = 'base_game'; Availability = 'installed'; MappingStatus = 'exact_base' } }
    if ($statuses.Count -eq 1 -and $statuses[0] -eq 'war_sails_runtime_exact') { return [ordered]@{ WorldSource = 'official_dlc'; Availability = 'not_installed'; MappingStatus = 'exact_official_dlc_not_installed' } }
    return [ordered]@{ WorldSource = 'unknown'; Availability = 'unknown'; MappingStatus = 'needs_review' }
}

function Get-RelativePath([string]$Path) {
    return [IO.Path]::GetRelativePath($root, $Path).Replace('\', '/')
}

$familyInput = Get-RawFile $familyPath
$gameInput = Get-RawFile $gamePath
$familyDocument = $familyInput.Text | ConvertFrom-Json -Depth 50
$gameDocument = $gameInput.Text | ConvertFrom-Json -Depth 50
$familyRows = @($familyDocument.rows)
$gameRows = @($gameDocument.rows)
$gameByPerson = @{}
foreach ($row in $gameRows) { if (-not [string]::IsNullOrWhiteSpace([string]$row.hero_id)) { $gameByPerson[[string]$row.hero_id] = $row } }

$diagnostics = [Collections.Generic.List[object]]::new()
$heroes = [Collections.Generic.List[object]]::new()
$clans = [Collections.Generic.List[object]]::new()
$heroGroups = $familyRows | Group-Object { Normalize-Code ([string]$_.person_code) }
foreach ($group in $heroGroups) {
    $code = [string]$group.Name
    if ([string]::IsNullOrWhiteSpace($code)) { Add-Diagnostic $diagnostics 'WB-ENTITY-HERO-001' 'error' '人物代码不能为空。' 'rows' ''; continue }
    if ($group.Count -gt 1) { Add-Diagnostic $diagnostics 'WB-ENTITY-HERO-002' 'error' '发现重复人物代码，已保留第一条并标记待复核。' "hero.$code" ($group.Count.ToString()) }
    $row = @($group.Group | Sort-Object source_file | Select-Object -First 1)[0]
    $familyCode = Normalize-Code ([string]$row.family_code)
    $status = Get-Status @($row)
    $gameRow = $gameByPerson[$code]
    $hasNativeGameBinding = $null -ne $gameRow -and [bool]$gameRow.native_hero_exists -and -not [string]::IsNullOrWhiteSpace([string]$gameRow.faction_id)
    if ($hasNativeGameBinding -and (Normalize-Code ([string]$gameRow.faction_id)) -ne $familyCode) { Add-Diagnostic $diagnostics 'WB-ENTITY-HERO-003' 'error' '人物所属家族在两份映射中不一致。' "hero.$code" "family=$($row.family_code);game=$($gameRow.faction_id)"; $status = [ordered]@{ WorldSource = 'unknown'; Availability = 'unknown'; MappingStatus = 'needs_review' } }
    $hero = [ordered]@{
        entity_id = "entity.hero.$code"
        kind = 'hero'
        display_name_zh = [string]$row.person_name_zh
        world_source = $status.WorldSource
        runtime_availability = $status.Availability
        mapping_status = $status.MappingStatus
        name_source = if ([string]::IsNullOrWhiteSpace([string]$row.person_name_zh)) { 'unknown' } else { 'mapping_report' }
        hero_code = $code
        family_entity_id = if ([string]::IsNullOrWhiteSpace($familyCode)) { $null } else { "entity.clan.$familyCode" }
        family_name_zh = if ([string]::IsNullOrWhiteSpace([string]$row.family_name_zh)) { $null } else { [string]$row.family_name_zh }
        related_codes = [ordered]@{ clan = $familyCode; kingdom = [string]$row.native_kingdom_id; culture = [string]$row.native_culture_id; home_settlement = [string]$row.native_home_settlement_id }
        source = [ordered]@{ mapping_status = [string]$row.mapping_status; source_policy = [string]$row.source_policy }
    }
    if ([string]::IsNullOrWhiteSpace($hero.display_name_zh)) { Add-Diagnostic $diagnostics 'WB-ENTITY-NAME-001' 'error' '人物缺少中文名称，不能作为普通作者选项。' "hero.$code" '' }
    $heroes.Add($hero)
}

$clanGroups = $familyRows | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.family_code) } | Group-Object { Normalize-Code ([string]$_.family_code) }
foreach ($group in $clanGroups) {
    $code = [string]$group.Name
    $rows = @($group.Group | Sort-Object person_code)
    $status = Get-Status $rows
    $names = @($rows | ForEach-Object { [string]$_.family_name_zh } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
    if ($names.Count -gt 1) { Add-Diagnostic $diagnostics 'WB-ENTITY-CLAN-001' 'error' '同一家族代码对应多个中文名称，不能静默选择。' "clan.$code" ($names -join '|'); $status = [ordered]@{ WorldSource = 'unknown'; Availability = 'unknown'; MappingStatus = 'needs_review' } }
    $first = $rows[0]
    $related = [ordered]@{}
    foreach ($field in @(@('kingdom','native_kingdom_id'), @('culture','native_culture_id'), @('home_settlement','native_home_settlement_id'))) {
        $values = @($rows | ForEach-Object { [string]$_.($field[1]) } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
        if ($values.Count -gt 1) { Add-Diagnostic $diagnostics 'WB-ENTITY-CLAN-002' 'warning' '家族关联代码在人物映射中存在多个值。' "clan.$code" "$($field[0])=$($values -join '|')" }
        $related[$field[0]] = if ($values.Count -gt 0) { $values[0] } else { $null }
    }
    $clan = [ordered]@{
        entity_id = "entity.clan.$code"
        kind = 'clan'
        display_name_zh = if ($names.Count -gt 0) { $names[0] } else { $null }
        world_source = $status.WorldSource
        runtime_availability = $status.Availability
        mapping_status = $status.MappingStatus
        name_source = if ($names.Count -gt 0) { 'mapping_report' } else { 'unknown' }
        clan_code = $code
        member_entity_ids = @($rows | ForEach-Object { "entity.hero.$(Normalize-Code ([string]$_.person_code))" } | Sort-Object -Unique)
        related_codes = $related
        source = [ordered]@{ mapping_statuses = @($rows | ForEach-Object { [string]$_.mapping_status } | Sort-Object -Unique); source_policy = [string]$first.source_policy }
    }
    if ([string]::IsNullOrWhiteSpace($clan.display_name_zh)) {
        if ($status.WorldSource -eq 'official_dlc' -and $status.Availability -eq 'not_installed') {
            Add-Diagnostic $diagnostics 'WB-ENTITY-NAME-002' 'warning' '官方 DLC 家族当前没有可用的中文名称，暂不作为普通作者选择项。' "clan.$code" 'War Sails 未安装；名称待补充。'
        } else {
            Add-Diagnostic $diagnostics 'WB-ENTITY-NAME-002' 'error' '基础游戏家族缺少中文名称，不能作为普通作者选项。' "clan.$code" ''
        }
    }
    $clans.Add($clan)
}

$entities = @($heroes + $clans | Sort-Object kind, entity_id)
$schemaFiles = @('entity-registry.v1.schema.json','entity-registry-manifest.v1.schema.json','entity-registry-diagnostics.v1.schema.json','entity-registry-pointer.v1.schema.json') | ForEach-Object { Join-Path $schemaRoot $_ } | Where-Object { Test-Path -LiteralPath $_ }
$generatorSourceHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToUpperInvariant()
$schemaHashes = @($schemaFiles | Sort-Object | ForEach-Object { [ordered]@{ path = Get-RelativePath $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToUpperInvariant(); length = (Get-Item -LiteralPath $_).Length } })
$inputHashes = @([ordered]@{ path = Get-RelativePath $familyPath; sha256 = $familyInput.Sha256; length = $familyInput.Bytes.Length; generated_at_utc = [string]$familyDocument.generated_at_utc }, [ordered]@{ path = Get-RelativePath $gamePath; sha256 = $gameInput.Sha256; length = $gameInput.Bytes.Length; generated_at_utc = [string]$gameDocument.generated_at_utc })
$buildMaterial = "$generatorVersion|$generatorSourceHash|$normalizationVersion|$(ConvertTo-StableJson $inputHashes)|$(ConvertTo-StableJson $schemaHashes)"
$buildHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($buildMaterial))).ToLowerInvariant()
$buildId = "b1-$buildHash"
$generationRoot = Join-Path $outputRoot 'generations'
$stagingRoot = Join-Path $outputRoot "staging-$buildId"
$generationPath = Join-Path $generationRoot $buildId
$pointerPath = Join-Path $outputRoot 'current-pointer.v1.json'

if (Test-Path -LiteralPath $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
[IO.Directory]::CreateDirectory($stagingRoot) | Out-Null
$registry = [ordered]@{ schema_version = 'awake.persona-entity.registry.v1'; catalog_build_id = $buildId; counts = [ordered]@{ hero = $heroes.Count; clan = $clans.Count; hero_base_game = @($heroes | Where-Object {$_.mapping_status -eq 'exact_base'}).Count; hero_official_dlc_not_installed = @($heroes | Where-Object {$_.mapping_status -eq 'exact_official_dlc_not_installed'}).Count; clan_base_game = @($clans | Where-Object {$_.mapping_status -eq 'exact_base'}).Count; clan_official_dlc_not_installed = @($clans | Where-Object {$_.mapping_status -eq 'exact_official_dlc_not_installed'}).Count }; entities = $entities }
$registryPath = Join-Path $stagingRoot 'entity-registry.v1.json'
Write-StableJson $registryPath $registry
$diagnosticDocument = [ordered]@{ schema_version = 'awake.persona-entity.diagnostics.v1'; catalog_build_id = $buildId; valid = -not ($diagnostics | Where-Object { $_.severity -eq 'error' }); diagnostics = @($diagnostics); counts = [ordered]@{ total = $diagnostics.Count; errors = @($diagnostics | Where-Object {$_.severity -eq 'error'}).Count; warnings = @($diagnostics | Where-Object {$_.severity -eq 'warning'}).Count } }
$diagnosticsPath = Join-Path $stagingRoot 'entity-registry-diagnostics.v1.json'
Write-StableJson $diagnosticsPath $diagnosticDocument
$manifest = [ordered]@{ schema_version = 'awake.persona-entity.manifest.v1'; catalog_build_id = $buildId; generator_version = $generatorVersion; generator_source_sha256 = $generatorSourceHash; normalization_version = $normalizationVersion; input_files = $inputHashes; schema_files = $schemaHashes; registry_sha256 = (Get-FileHash -LiteralPath $registryPath -Algorithm SHA256).Hash.ToUpperInvariant(); diagnostics_sha256 = (Get-FileHash -LiteralPath $diagnosticsPath -Algorithm SHA256).Hash.ToUpperInvariant(); counts = $registry.counts }
$manifestPath = Join-Path $stagingRoot 'entity-registry-manifest.v1.json'
Write-StableJson $manifestPath $manifest
if (Test-Path -LiteralPath $generationPath) { Remove-Item -LiteralPath $generationPath -Recurse -Force }
[IO.Directory]::CreateDirectory($generationRoot) | Out-Null
Move-Item -LiteralPath $stagingRoot -Destination $generationPath
$pointer = [ordered]@{ schema_version = 'awake.persona-entity.pointer.v1'; catalog_build_id = $buildId; generation_relative_path = "generations/$buildId"; registry_sha256 = (Get-FileHash -LiteralPath (Join-Path $generationPath 'entity-registry.v1.json') -Algorithm SHA256).Hash.ToUpperInvariant(); manifest_sha256 = (Get-FileHash -LiteralPath (Join-Path $generationPath 'entity-registry-manifest.v1.json') -Algorithm SHA256).Hash.ToUpperInvariant(); diagnostics_sha256 = (Get-FileHash -LiteralPath (Join-Path $generationPath 'entity-registry-diagnostics.v1.json') -Algorithm SHA256).Hash.ToUpperInvariant() }
$pointerTemp = "$pointerPath.$([Guid]::NewGuid().ToString('N')).tmp"
Write-StableJson $pointerTemp $pointer
try {
    if (Test-Path -LiteralPath $pointerPath) {
        try {
            $pointerBackup = "$pointerPath.$([Guid]::NewGuid().ToString('N')).bak"
            [IO.File]::Replace($pointerTemp, $pointerPath, $pointerBackup, $true)
            if (Test-Path -LiteralPath $pointerBackup) { Remove-Item -LiteralPath $pointerBackup -Force }
        } catch [IO.IOException] {
            Move-Item -LiteralPath $pointerTemp -Destination $pointerPath -Force
        }
    } else {
        Move-Item -LiteralPath $pointerTemp -Destination $pointerPath
    }
} finally {
    if (Test-Path -LiteralPath $pointerTemp) { Remove-Item -LiteralPath $pointerTemp -Force }
    Get-ChildItem -LiteralPath $outputRoot -Filter 'current-pointer.v1.json.*.tmp' -File -ErrorAction SilentlyContinue | Remove-Item -Force
}
Write-Output "ENTITY_REGISTRY: $generationPath"
Write-Output "BUILD_ID: $buildId"
Write-Output "HEROES: $($heroes.Count)"
Write-Output "CLANS: $($clans.Count)"
Write-Output "DIAGNOSTICS: $($diagnostics.Count)"
