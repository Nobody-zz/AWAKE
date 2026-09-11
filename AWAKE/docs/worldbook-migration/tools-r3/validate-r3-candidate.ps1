# R3 候选三向闭合校验器（PowerShell 版 validator）
# 检查项：结构闭合（claim 全被 span 引用、span 字段齐备、偏移与正文一致）、
#         同质性（同一 span 内 claim 的认识论层级/视角一致）、
#         状态门（needs_review，禁 approve/canon/compiled/published/grants/denies）、
#         防机械搬运（target_text 与 v1 源文 variants 的 10 字 shingle 重叠必须为 0）。
param(
    [Parameter(Mandatory=$true)][string]$CandidatePath,
    [string]$SourceZip = "D:\AWAKE-Archive\worldbook-v1_20260911.zip",
    [string]$SourceTitle  # v1 规则文件名中的标题，缺省取候选 title
)
$ErrorActionPreference = "Stop"
$fail = 0
function Fail($m) { $script:fail++; Write-Host "FAIL: $m" }
function Pass($m) { Write-Host "ok  : $m" }

$raw = [IO.File]::ReadAllText($CandidatePath, [Text.Encoding]::UTF8)
$c = $raw | ConvertFrom-Json
Pass "json_parse"

# 状态门
if ($c.status -eq "needs_review") { Pass "status=needs_review" } else { Fail "status=$($c.status)" }
$bad = @("approve","canon","compiled","published","runtime_injected","grants","denies")
foreach ($k in $bad) { if ($raw -match "`"$k`"`s*:\s*(true|[1-9])") { Fail "forbidden_marker:$k" } }
Pass "forbidden_markers_absent"

# claim 完整性
$claimIds = @{}
foreach ($cl in $c.claims) {
    if (-not $cl.claim_id -or -not $cl.epistemic_kind -or -not $cl.subject -or -not $cl.predicate -or $cl.source_origin_ids.Count -eq 0) {
        Fail "claim_incomplete:$($cl.claim_id)"; continue
    }
    if ($claimIds.ContainsKey($cl.claim_id)) { Fail "claim_duplicate:$($cl.claim_id)" }
    $claimIds[$cl.claim_id] = $cl
}
Pass "claims_complete ($($claimIds.Count) claims)"

# span 引用闭合 + 同质性
$referenced = @{}
$tierOf = @{ source_fact="fact"; relationship="fact"; interpretation="interp"; rumor="rumor"; unresolved="unresolved" }
foreach ($sp in $c.target_spans) {
    if ($sp.claim_ids.Count -eq 0) { Fail "span_empty:$($sp.sentence_id)"; continue }
    $tiers = @{}; $persp = @{}
    foreach ($cid in $sp.claim_ids) {
        if (-not $claimIds.ContainsKey($cid)) { Fail "span_unknown_claim:$($sp.sentence_id):$cid"; continue }
        $referenced[$cid] = $true
        $tiers[$tierOf[$claimIds[$cid].epistemic_kind]] = $true
        $persp[$claimIds[$cid].perspective] = $true
    }
    if ($tiers.Count -gt 1) { Fail "span_mixed_tiers:$($sp.sentence_id):[$($tiers.Keys -join ',')]" }
    if ($persp.Count -gt 1) { Fail "span_mixed_perspectives:$($sp.sentence_id):[$($persp.Keys -join ',')]" }
}
Pass "span_claim_refs_closed ($($c.target_spans.Count) spans)"

$uncovered = $claimIds.Keys | Where-Object { -not $referenced.ContainsKey($_) }
if ($uncovered) { Fail "claims_not_covered_by_any_span: $($uncovered -join ', ')" } else { Pass "all_claims_covered" }

# 偏移与正文一致（按 locator 段落重算）
$paras = $c.target_text -split "`n`n"
$offs = @(); $o = 0
foreach ($p in $paras) { $offs += ,@($o, $p.Length); $o += $p.Length + 2 }
foreach ($sp in $c.target_spans) {
    if ($sp.target_locator -notmatch 'paragraph/(\d+)') { Fail "bad_locator:$($sp.sentence_id)"; continue }
    $pi = [int]$Matches[1]
    $s = $offs[$pi][0]; $e = $s + $offs[$pi][1]
    if ($sp.text_start -lt $s -or $sp.text_end -gt $e) { Fail "span_out_of_paragraph:$($sp.sentence_id) ($($sp.text_start)..$($sp.text_end) vs $s..$e)" }
}
Pass "span_offsets_within_bounds"

# 防机械搬运：10 字 shingle 与 v1 源文重叠必须为 0
if (-not $SourceTitle) { $SourceTitle = $c.title }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($SourceZip)
try {
    $entry = $zip.Entries | Where-Object { $_.FullName -match "[/\\]rules[/\\]rule_$SourceTitle" } | Select-Object -First 1
    if (-not $entry) { Fail "source_entry_not_found:$SourceTitle" }
    else {
        $sr = New-Object IO.StreamReader($entry.Open(), [Text.Encoding]::UTF8)
        $src = ($sr.ReadToEnd() | ConvertFrom-Json); $sr.Close()
        $shingles = New-Object 'System.Collections.Generic.HashSet[string]'
        foreach ($v in $src.Variants) {
            $t = $v.Content -replace '\s',''
            for ($i = 0; $i -le $t.Length - 10; $i++) { [void]$shingles.Add($t.Substring($i, 10)) }
        }
        $tt = $c.target_text -replace '\s',''
        $hits = @()
        for ($i = 0; $i -le $tt.Length - 10; $i++) { $g = $tt.Substring($i, 10); if ($shingles.Contains($g)) { $hits += $g } }
        if ($hits.Count -gt 0) { Fail "verbatim_overlap_10gram: $($hits.Count) 处，例：$($hits[0])" }
        else { Pass "no_10gram_overlap_with_v1_source" }
    }
} finally { $zip.Dispose() }

if ($fail -eq 0) { Write-Host "`nVALIDATION PASS: $CandidatePath" } else { Write-Host "`nVALIDATION FAILED: $fail 项" }
exit ([math]::Min($fail,1))
