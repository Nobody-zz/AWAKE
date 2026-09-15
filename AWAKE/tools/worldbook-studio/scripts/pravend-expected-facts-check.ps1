param(
    [string]$ClusterPath = '',
    [string]$EvidencePath = ''
)
$ErrorActionPreference = 'Stop'
$studio = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ClusterPath)) { $ClusterPath = Join-Path $studio 'tests\fixtures\official-reference\pravend-cluster' }
if ([string]::IsNullOrWhiteSpace($EvidencePath)) { $EvidencePath = Join-Path $studio '_tmp\pravend-real-worker-evidence.json' }
$cluster = $ClusterPath
$expected = Get-Content -LiteralPath (Join-Path $cluster 'expected-facts.v1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$evidence = Get-Content -LiteralPath $EvidencePath -Raw -Encoding UTF8 | ConvertFrom-Json
$source = Get-Content -LiteralPath (Join-Path $cluster $expected.source_extract) -Raw -Encoding UTF8

$generatedFacts = @(); $generatedCandidates = @($evidence.candidates)
foreach ($c in $generatedCandidates) { foreach ($f in @($c.facts)) { $generatedFacts += $f } }
$allText = (@($generatedFacts | ForEach-Object { [string]$_.text }) -join '|')

$pass = 0; $fail = 0
function Check([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { Write-Output ('  PASS  ' + $name) } else { Write-Output ('  FAIL  ' + $name + ' :: ' + $detail) }
    if ($ok) { $script:pass++ } else { $script:fail++ }
}

Write-Output '=== 期望事实覆盖 ==='
foreach ($ef in $expected.expected_facts) {
    $hit = $null
    foreach ($f in $generatedFacts) {
        # 以逐字引文为准（复述文本仅作回退）
        foreach ($field in @([string]$f.evidence.quote, [string]$f.text)) {
            if ([string]::IsNullOrWhiteSpace($field)) { continue }
            $allMarkers = $true
            foreach ($mk in $ef.markers) { if (-not $field.Contains([string]$mk)) { $allMarkers = $false; break } }
            if ($allMarkers) { $hit = $f; break }
        }
        if ($hit) { break }
    }
    if (-not $hit) {
        # 退化匹配：所有标记在整批文本中同时出现
        $union = $true
        foreach ($mk in $ef.markers) { if (-not $allText.Contains([string]$mk)) { $union = $false; break } }
        Check $ef.id $false ('未在单条事实中命中标记 ' + ($ef.markers -join '+') + '（跨批出现=' + $union + '）')
        continue
    }
    $kindOk = ([string]$hit.kind -eq [string]$ef.kind)
    Check $ef.id $true ('匹配事实文本，kind=' + $hit.kind + '（期望 ' + $ef.kind + '，' + $(if ($kindOk) { '一致' } else { '不一致' }) + '）')
}

Write-Output ''
Write-Output '=== 禁止项 ==='
$yearHits = @($generatedFacts | Where-Object { [string]$_.text -match $expected.forbidden.year_pattern })
Check 'no-invented-year' ($yearHits.Count -eq 0) ('命中 ' + $yearHits.Count + ' 条')
$idHits = @($generatedFacts | Where-Object { [string]$_.text -match $expected.forbidden.formal_entity_id })
Check 'no-formal-entity-id' ($idHits.Count -eq 0) ('命中 ' + $idHits.Count + ' 条')
$warHits = @($generatedFacts | Where-Object { [string]$_.text -match $expected.forbidden.war_marker })
Check 'no-invented-war' ($warHits.Count -eq 0) ('命中 ' + $warHits.Count + ' 条')
$absentHits = @($generatedFacts | Where-Object { $t = [string]$_.text; @($expected.forbidden.place_absent_in_source | Where-Object { $t.Contains([string]$_) }).Count -gt 0 })
Check 'no-fregian' ($absentHits.Count -eq 0) ('命中 ' + $absentHits.Count + ' 条')

Write-Output ''
Write-Output '=== 状态与证据 ==='
Check 'review-only' ([bool]$evidence.passed) '生成未通过'
$pendingFacts = @($generatedFacts | Where-Object { [string]$_.review_status -ne 'pending' })
Check 'facts-pending' ($pendingFacts.Count -eq 0) ('非 pending: ' + $pendingFacts.Count)
$pendingCands = @($generatedCandidates | Where-Object { [string]$_.review_status -ne 'pending' })
Check 'candidates-pending' ($pendingCands.Count -eq 0) ('非 pending: ' + $pendingCands.Count)
$badQuotes = @($generatedFacts | Where-Object { -not $source.Contains([string]$_.evidence.quote) })
Check 'quotes-verbatim' ($badQuotes.Count -eq 0) ('不可定位: ' + $badQuotes.Count)
$quoteEqualText = @($generatedFacts | Where-Object { [string]$_.evidence.quote -eq [string]$_.text })
Check 'text-is-restatement-not-copy' ($quoteEqualText.Count -eq 0) ('照抄原句: ' + $quoteEqualText.Count + ' 条')
$exprCount = 0; foreach ($c in $generatedCandidates) { $exprCount += @($c.expressions).Count }
Check 'no-placeholder-expressions' ($exprCount -eq $expected.requirements.expressions_expected_when_perspectives_empty) ('表达数=' + $exprCount)

Write-Output ''
Write-Output '=== 地点锚点 ==='
$anchorIds = @()
foreach ($c in $generatedCandidates) { $anchorIds += @($c.metadata.entity_ids) }
$anchorIds += @($evidence.document_entity_ids)
$anchorIds = @($anchorIds | Where-Object { $_ } | Select-Object -Unique)
foreach ($need in @($expected.expected_anchors.must_appear_somewhere)) {
    Check ('anchor-present:' + $need) ($anchorIds -contains [string]$need) ('未出现，现有=[' + ($anchorIds -join ',') + ']')
}
if ($expected.expected_anchors.must_exist_in_registry) {
    $mappingRoot = Join-Path $studio '..\..\docs\mappings\persona-entity'
    $mappingRoot = [IO.Path]::GetFullPath($mappingRoot)
    try {
        $ptr = Get-Content -LiteralPath (Join-Path $mappingRoot 'current-pointer.v1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $genDir = Join-Path $mappingRoot ($ptr.generation_relative_path -replace '/', '\')
        $reg = Get-Content -LiteralPath (Join-Path $genDir 'entity-registry.v1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $known = @{}; foreach ($e in $reg.entities) { $known[[string]$e.entity_id] = $true }
        $unknown = @($anchorIds | Where-Object { -not $known.ContainsKey([string]$_) })
        Check 'anchors-exist-in-mapping' ($unknown.Count -eq 0) ('未登记锚点=[' + ($unknown -join ',') + ']')
    }
    catch {
        Check 'anchors-exist-in-mapping' $false ('无法读取地点映射：' + $_.Exception.Message)
    }
}

$aliasMap = $expected.expected_anchors.alias_map
if ($aliasMap) {
    foreach ($aliasName in $aliasMap.PSObject.Properties.Name) {
        $targetAnchor = [string]$aliasMap.$aliasName
        $mentioning = @($generatedCandidates | Where-Object {
            $text = (@($_.facts) | ForEach-Object { [string]$_.text + ' ' + [string]$_.evidence.quote }) -join ' '
            $text.Contains($aliasName)
        })
        if ($mentioning.Count -eq 0) {
            Check ('alias-anchor:' + $aliasName) $true '本批未提及，跳过'
            continue
        }
        $missing = @($mentioning | Where-Object { @($_.metadata.entity_ids) -notcontains $targetAnchor })
        Check ('alias-anchor:' + $aliasName) ($missing.Count -eq 0) ('提及但缺锚点=' + $missing.Count + '/' + $mentioning.Count)
    }
}

Write-Output ''
Write-Output ("汇总: PASS=" + $pass + " FAIL=" + $fail)
if ($fail -gt 0) { exit 1 }
