param(
    [string]$PersonaMapPath = 'C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\mappings\persona-game\persona-game-mapping.v1.json',
    [string]$WarSailsMapPath = 'C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\mappings\war-sails-reference\war-sails-reference-string-map.v1.json',
    [string]$OutputDirectory = 'C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\mappings\persona-game'
)

$ErrorActionPreference = 'Stop'
$personaMap = Get-Content -Raw -LiteralPath $PersonaMapPath -Encoding UTF8 | ConvertFrom-Json
$warSailsMap = Get-Content -Raw -LiteralPath $WarSailsMapPath -Encoding UTF8 | ConvertFrom-Json

$rows = foreach ($persona in @($personaMap.rows | Where-Object match_status -eq 'missing_native_hero')) {
    $hits = @($warSailsMap.rows | Where-Object {
        $name = $persona.display_name_from_filename
        $name -and (($_.chinese_text -like "*$name*") -or ($_.english_text -match [regex]::Escape($name)))
    })
    $status = if ($hits.Count -eq 0) { 'needs_manual_review' } elseif (@($hits | Where-Object category -eq 'heroes').Count -gt 0) { 'war_sails_reference_candidate' } else { 'war_sails_name_or_description_hit_only' }
    [ordered]@{
        hero_id = $persona.hero_id
        display_name_from_filename = $persona.display_name_from_filename
        current_game_status = 'missing_native_hero'
        war_sails_reference_status = $status
        matching_reference_count = $hits.Count
        matching_categories = (($hits | Select-Object -ExpandProperty category -Unique) -join ',')
        matching_string_ids = (($hits | Select-Object -ExpandProperty string_id) -join ',')
        warning = 'Reference hit does not prove HeroId, ClanId or KingdomId relation.'
    }
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$csvPath = Join-Path $OutputDirectory 'persona-game-war-sails-candidate-audit.v1.csv'
$mdPath = Join-Path $OutputDirectory 'persona-game-war-sails-candidate-audit.v1.md'
$rows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8

$candidateCount = @($rows | Where-Object war_sails_reference_status -eq 'war_sails_reference_candidate').Count
$manualCount = @($rows | Where-Object war_sails_reference_status -eq 'needs_manual_review').Count
$hitOnlyCount = @($rows | Where-Object war_sails_reference_status -eq 'war_sails_name_or_description_hit_only').Count
@"
# AF 缺漏人物与战帆参考审计

- AF 当前游戏未匹配人物：$($rows.Count)
- 战帆人物表直接命中候选：$candidateCount
- 只有海军领主/描述文本命中：$hitOnlyCount
- 完全没有命中：$manualCount

判定规则：

- war_sails_reference_candidate：战帆 heroes 字符串表出现同名或相关中文名，可作为人工核对候选。
- war_sails_name_or_description_hit_only：只在战帆海军领主或描述文本中出现，不足以证明稳定人物对象关系。
- needs_manual_review：战帆索引未找到同名参考。

重要限制：

1. 本表只帮助定位参考资料，不补写当前游戏的 HeroId、ClanId、KingdomId。
2. 战帆扩展对象可能属于独立模组命名空间，不能自动进入默认 AWAKE 卡拉迪亚映射。
3. `lord_7_*` 与 `dead_lord_7_*` 的共同编号模式不能单独证明家族关系。

机器表：persona-game-war-sails-candidate-audit.v1.csv
"@ | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Output "AUDIT: $csvPath"
Write-Output "REPORT: $mdPath"
