param(
    [string]$EnglishDirectory = 'C:\Users\26811\Downloads\战帆英文',
    [string]$ChineseDirectory = 'C:\Users\26811\Downloads\战帆中文',
    [string]$OutputDirectory = 'C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\mappings\war-sails-reference'
)

$ErrorActionPreference = 'Stop'

foreach ($path in @($EnglishDirectory, $ChineseDirectory)) {
    if (-not (Test-Path -LiteralPath $path -PathType Container)) { throw "Reference directory missing: $path" }
}

function Read-StringFile([string]$Path) {
    $document = [xml](Get-Content -LiteralPath $Path -Raw -Encoding UTF8)
    $rows = @{}
    foreach ($node in $document.SelectNodes('//string')) {
        $id = $node.GetAttribute('id')
        if ([string]::IsNullOrWhiteSpace($id)) { continue }
        $rows[$id] = $node.GetAttribute('text')
    }
    return $rows
}

function Get-ReferenceRows([string]$Stem, [string]$Category) {
    $englishPath = Join-Path $EnglishDirectory "$Stem.xml"
    $chinesePath = Join-Path $ChineseDirectory "$Stem-zho-CN.xml"
    if (-not (Test-Path -LiteralPath $englishPath)) { return @() }
    $english = Read-StringFile $englishPath
    $chinese = if (Test-Path -LiteralPath $chinesePath) { Read-StringFile $chinesePath } else { @{} }
    $rows = foreach ($id in ($english.Keys | Sort-Object)) {
        [ordered]@{
            source_mod = 'war_sails'
            reference_status = 'extension_reference'
            category = $Category
            source_file_english = [IO.Path]::GetFileName($englishPath)
            source_file_chinese = if (Test-Path -LiteralPath $chinesePath) { [IO.Path]::GetFileName($chinesePath) } else { $null }
            string_id = $id
            english_text = $english[$id]
            chinese_text = if ($chinese.ContainsKey($id)) { $chinese[$id] } else { $null }
        }
    }
    return @($rows)
}

$groups = @(
    @{ Stem = 'std_heroes_xml'; Category = 'heroes' },
    @{ Stem = 'std_clans_xml'; Category = 'clans' },
    @{ Stem = 'std_kingdoms_xml'; Category = 'kingdoms' },
    @{ Stem = 'std_settlements_xml'; Category = 'settlements' },
    @{ Stem = 'std_naval_characters_xml'; Category = 'naval_characters' },
    @{ Stem = 'std_naval_lords_xml'; Category = 'naval_lords' }
)

$allRows = @()
foreach ($group in $groups) { $allRows += Get-ReferenceRows $group.Stem $group.Category }

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory 'war-sails-reference-string-map.v1.json'
$csvPath = Join-Path $OutputDirectory 'war-sails-reference-string-map.v1.csv'
$readmePath = Join-Path $OutputDirectory 'README.md'

$summary = [ordered]@{
    schema_version = 'awake.war-sails-reference-map.v1'
    generated_at_utc = [DateTime]::UtcNow.ToString('O')
    source_mod = 'war_sails'
    reference_status = 'extension_reference'
    source_policy = 'English and Chinese extracted string tables only; no object relation is inferred from prose.'
    input_directories = [ordered]@{ english = $EnglishDirectory; chinese = $ChineseDirectory }
    counts = [ordered]@{ total = $allRows.Count }
    rows = $allRows
}
foreach ($group in $groups) { $summary.counts[$group.Category] = @($allRows | Where-Object category -eq $group.Category).Count }

$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$allRows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
@"
# 战帆参考索引

- Schema: awake.war-sails-reference-map.v1
- 来源：$EnglishDirectory 与 $ChineseDirectory
- 定位：战帆扩展参考，不是当前 AWAKE 卡拉迪亚正典，也不是当前游戏对象关系权威。
- 提取范围：人物、家族、王国、聚落、海军角色、海军领主的字符串 ID 及中英文显示文本。
- 未执行：根据描述文本推断 HeroId → ClanId → KingdomId，也未把新增对象写入默认游戏映射。

文件：

- war-sails-reference-string-map.v1.json：机器可读完整索引。
- war-sails-reference-string-map.v1.csv：内容编辑者可筛选查看的表格。

使用规则：

1. 当前 Bannerlord XML 优先用于运行对象代码和关系绑定。
2. 战帆索引仅用于识别扩展对象、补充翻译和提出人工审查候选。
3. 任何候选进入 AWAKE 世界书前，必须由开发者决定其所属世界观、正典状态和适用内容包。
"@ | Set-Content -LiteralPath $readmePath -Encoding UTF8

Write-Output "JSON: $jsonPath"
Write-Output "CSV: $csvPath"
Write-Output "COUNTS: $($summary.counts | ConvertTo-Json -Compress)"
