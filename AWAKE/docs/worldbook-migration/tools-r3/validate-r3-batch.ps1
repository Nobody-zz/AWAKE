# R3 拆分版批量校验器
# 对 r3-revision/documents/*.r3.json 逐档校验：
#   结构闭合（claim 全被 span 引用，metadata_claims 豁免）、span 标记唯一且偏移一致、
#   同质性（同 span 认识论层级/视角一致）、layer 规则（仅 interpretation/rumor 层可标）、
#   状态门（needs_review，禁 approve/canon/…）、防机械搬运（与 v1 源文 10 字 shingle 零重叠）。
param(
    [string]$DocDir = "D:\AWAKE-Archive\worldbook-migration-content\semantic-rewrite-batch-download-20260903\r3-revision\documents",
    [string]$SourceZip = "D:\AWAKE-Archive\worldbook-v1_20260911.zip"
)
$ErrorActionPreference = "Stop"
$script:totalFail = 0
$tierOf = @{ source_fact='fact'; relationship='fact'; interpretation='interp'; rumor='rumor'; unresolved='unresolved'; state='fact' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($SourceZip)
$shingleCache = @{}

function Get-Shingles([string]$title) {
    if ($shingleCache.ContainsKey($title)) { return $shingleCache[$title] }
    $entry = $zip.Entries | Where-Object { $_.FullName -match "[/\\]rules[/\\]rule_$title" } | Select-Object -First 1
    $set = New-Object 'System.Collections.Generic.HashSet[string]'
    if ($entry) {
        $sr = New-Object IO.StreamReader($entry.Open(), [Text.Encoding]::UTF8)
        $src = $sr.ReadToEnd() | ConvertFrom-Json; $sr.Close()
        foreach ($v in $src.Variants) {
            $t = $v.Content -replace '\s',''
            for ($i = 0; $i -le $t.Length - 10; $i++) { [void]$set.Add($t.Substring($i, 10)) }
        }
    }
    $shingleCache[$title] = $set
    return $set
}

Get-ChildItem "$DocDir\*.r3.json" | Sort-Object Name | ForEach-Object {
    $file = $_.Name
    $fail = 0
    function Fail($m) { $script:fail++; Write-Host "    FAIL: $m" }
    function Pass($m) { Write-Host "    ok  : $m" }

    $c = [IO.File]::ReadAllText($_.FullName, [Text.Encoding]::UTF8) | ConvertFrom-Json
    $n = $c.title -split '·' | Select-Object -First 1
    Write-Host "  [$file] $n"

    if ($c.status -eq 'needs_review') { Pass 'status=needs_review' } else { Fail "status=$($c.status)" }
    foreach ($k in @('approve','canon','compiled','published','runtime_injected','grants','denies')) {
        if ($c.PSObject.Properties.Name -contains $k) { Fail "forbidden_field:$k" }
    }

    $claims = @{}
    foreach ($cl in $c.claims) {
        if (-not $cl.claim_id -or -not $cl.subject -or $cl.source_origin_ids.Count -eq 0) { Fail "claim_incomplete:$($cl.claim_id)"; continue }
        if ($claims.ContainsKey($cl.claim_id)) { Fail "claim_duplicate:$($cl.claim_id)" }
        $claims[$cl.claim_id] = $cl
    }
    $metaIds = @($c.metadata_claims)
    foreach ($m in $metaIds) { if (-not $claims.ContainsKey($m)) { Fail "metadata_unknown:$m" } }
    Pass "claims=$($claims.Count) metadata=$($metaIds.Count)"

    $referenced = @{}
    foreach ($sp in $c.target_spans) {
        if ($sp.claim_ids.Count -eq 0) { Fail "span_empty:$($sp.sentence_id)"; continue }
        $tiers = @{}; $persp = @{}
        foreach ($cid in $sp.claim_ids) {
            if (-not $claims.ContainsKey($cid)) { Fail "span_unknown_claim:$($sp.sentence_id)"; continue }
            $referenced[$cid] = $true
            $tiers[$tierOf[$claims[$cid].epistemic_kind]] = $true
            $persp[$claims[$cid].perspective] = $true
        }
        if ($tiers.Count -gt 1) { Fail "span_mixed_tiers:$($sp.sentence_id):[$($tiers.Keys -join ',')]" }
        if ($persp.Count -gt 1) { Fail "span_mixed_perspectives:$($sp.sentence_id):[$($persp.Keys -join ',')]" }
        # layer 规则：仅 interp/rumor 层标 layer；纯 fact/relation 中性段不得标
        if ($sp.PSObject.Properties.Name -contains 'layer') {
            $perspVals = @($sp.claim_ids | ForEach-Object { $claims[$_].perspective } | Sort-Object -Unique)
            if ($tiers.Count -eq 1 -and $tiers['fact'] -and $perspVals.Count -eq 1 -and $perspVals[0] -eq 'neutral') { Fail "layer_on_neutral_fact_span:$($sp.sentence_id)" }
        }
    }
    $uncovered = $claims.Keys | Where-Object { -not $referenced.ContainsKey($_) -and $metaIds -notcontains $_ }
    if ($uncovered) { Fail "claims_uncovered: $($uncovered -join ', ')" } else { Pass "closure_ok ($($c.target_spans.Count) spans)" }

    # 标记回验：text_start/end 与正文中的唯一标记一致
    $ok = $true
    foreach ($sp in $c.target_spans) {
        $seg = $c.target_text.Substring($sp.text_start, $sp.text_end - $sp.text_start)
        if ($c.target_text.IndexOf($seg) -ne $c.target_text.LastIndexOf($seg) -and $seg.Length -ge 8) { Fail "marker_not_unique:$($sp.sentence_id)"; $ok = $false }
    }
    if ($ok) { Pass 'span_offsets_unique' }

    # 防搬运
    $sh = Get-Shingles $n
    $tt = $c.target_text -replace '\s',''
    $hits = 0; $first = ''
    for ($i = 0; $i -le $tt.Length - 10; $i++) { $g = $tt.Substring($i, 10); if ($sh.Contains($g)) { $hits++; if (-not $first) { $first = $g } } }
    if (-not $sh.Count) { Fail "source_not_found:$n" }
    elseif ($hits -gt 0) { Fail "verbatim_overlap_10gram: $hits 处，例：$first" }
    else { Pass 'no_10gram_overlap' }

    $script:totalFail += $fail
    if ($fail -eq 0) { Write-Host "    => PASS" } else { Write-Host "    => FAILED ($fail)" }
}
$zip.Dispose()
Write-Host ""
if ($script:totalFail -eq 0) { Write-Host 'BATCH VALIDATION: ALL PASS' } else { Write-Host "BATCH VALIDATION: $script:totalFail FAILURES" }
exit ([math]::Min($script:totalFail, 1))
