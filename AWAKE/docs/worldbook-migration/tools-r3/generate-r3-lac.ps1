# R3 候选生成器 — 拉科尼斯湖（target revision 3）
# 数据驱动的通用骨架：按 paragraph/sentence 定义 span，自动计算 text_start/text_end（\n\n 计 2 字符段间隔，段内句切分无间隔）。
# 产物写入归档区 r3-revision/rewrite-candidates/，不覆盖 r2。全程 needs_review。
param(
    [string]$OutDir = "D:\AWAKE-Archive\worldbook-migration-content\semantic-rewrite-batch-download-20260903\r3-revision\rewrite-candidates"
)
$ErrorActionPreference = "Stop"

$candidateId = "authoring_provisional.document.57455a7ba76bcab6ce45bca1"
$baseRevision = 2
$targetRevision = 3
$title = "拉科尼斯湖"

$paragraphs = @(
    "拉科尼斯湖地处卡拉迪亚北部，是被山地与森林环抱的大型内陆淡水湖；喀拉卡兹河与弥戎河从不同方向汇入，把它接进北部的内河航运网络。沿岸居民世代靠这片水吃饭：渔民下网，船工行船，码头工人扛包卸货；帝国出产的麦子和斯特吉亚的毛皮，也沿这条水路双向转运。",
    "这片湖最要紧的特性是冬季不冻。斯特吉亚一侧的河道入冬封冻、陆路被大雪隔断的时候，湖面上的船照样在走——来源反复强调这一点，把它当作湖区冬季运输价值的根据。",
    "在帝国（防务）叙述里，这条冬季水路被解释成北境防务和运兵上的优势：谁握着湖上的舰队，谁就握着冬天的主动权。应当说明，这是带着立场的战略判断，不是湖泊本身的地理事实。",
    "民间流传着湖水曾被鲜血染红的德律亚传说，老一辈人提起时往往沉默。这个故事没有留下直接证据，只能作为湖区战争记忆的一部分被讲述。",
    "至于某些黄昏浅滩上泛起的铁锈色，来源里的考察叙述给了一种自然解释：那更可能是浅水地带大量植物残骸常年腐解、析出的铁质沉淀成层所致，与血染湖水的传说只是巧合性对应——它是一种解释，不是定论。"
)

# span 定义：paragraph 索引 + 可选句切分（按"。"首次出现切两段）
$spanDefs = @(
    @{ para = 0; splitAtSentence = 1; claims = @("authoring_provisional.claim.0d2520893b3a115035a9a1a0", "authoring_provisional.claim.80fd2b2477dad8dd01375064"); origins = @("authoring_provisional.origin.6792cb2c1224889c505df1ba", "authoring_provisional.origin.9989debf022c34e94bddd43e", "authoring_provisional.origin.28a21f9e5db2488ea6a4bc5e", "authoring_provisional.origin.d35c759a5ef84e3997f96df0", "authoring_provisional.origin.bfc66570e6db11cb4b5768ec"); op = "rephrase" },
    @{ para = 0; splitAtSentence = 2; claims = @("authoring_provisional.claim.c4672cffe65c5125bb1d00e7", "authoring_provisional.claim.d2c7e2dc921eafdf047a31c8"); origins = @("authoring_provisional.origin.d5bd6d3382672ae542a1cc22", "authoring_provisional.origin.7ba74330155cd80ad446d57a"); op = "rephrase" },
    @{ para = 1; claims = @("authoring_provisional.claim.c9ff734b8dd6d7968fb1a954"); origins = @("authoring_provisional.origin.6792cb2c1224889c505df1ba", "authoring_provisional.origin.bfc66570e6db11cb4b5768ec", "authoring_provisional.origin.9989debf022c34e94bddd43e", "authoring_provisional.origin.28a21f9e5db2488ea6a4bc5e", "authoring_provisional.origin.d35c759a5ef84e3997f96df0"); op = "rephrase" },
    @{ para = 2; claims = @("authoring_provisional.claim.e1158ae544d37d2f31aeed53"); origins = @("authoring_provisional.origin.ce4011516c2d730bfe559bea"); op = "rephrase" },
    @{ para = 3; claims = @("authoring_provisional.claim.7707292541174752ca88df88"); origins = @("authoring_provisional.origin.6792cb2c1224889c505df1ba", "authoring_provisional.origin.9989debf022c34e94bddd43e", "authoring_provisional.origin.28a21f9e5db2488ea6a4bc5e", "authoring_provisional.origin.d35c759a5ef84e3997f96df0"); op = "rephrase" },
    @{ para = 4; claims = @("authoring_provisional.claim.c829e454ee3264a31fe6c451"); origins = @("authoring_provisional.origin.a5239cb238349326884324ff"); op = "rephrase" }
)

$claims = @(
    @{ claim_id = "authoring_provisional.claim.0d2520893b3a115035a9a1a0"; epistemic_kind = "source_fact"; subject = "拉科尼斯湖"; predicate = "位于"; object = "卡拉迪亚北部的封闭型内陆淡水湖"; perspective = "neutral"; time_scope = "historical_or_current_unknown"; confidence = "medium"; source_origin_ids = @("authoring_provisional.origin.6792cb2c1224889c505df1ba", "authoring_provisional.origin.9989debf022c34e94bddd43e", "authoring_provisional.origin.28a21f9e5db2488ea6a4bc5e", "authoring_provisional.origin.d35c759a5ef84e3997f96df0"); polarity = "affirmed"; revision_note = "r2 保留（r3-decision: keep）" },
    @{ claim_id = "authoring_provisional.claim.80fd2b2477dad8dd01375064"; epistemic_kind = "relationship"; subject = "弥戎河与喀拉卡兹河"; predicate = "汇入并连接"; object = "拉科尼斯湖及北部水运网络"; perspective = "neutral"; time_scope = "historical_or_current_unknown"; confidence = "medium"; source_origin_ids = @("authoring_provisional.origin.bfc66570e6db11cb4b5768ec", "authoring_provisional.origin.9989debf022c34e94bddd43e", "authoring_provisional.origin.28a21f9e5db2488ea6a4bc5e", "authoring_provisional.origin.d35c759a5ef84e3997f96df0"); polarity = "affirmed"; revision_note = "r2 保留（r3-decision: keep）" },
    @{ claim_id = "authoring_provisional.claim.c4672cffe65c5125bb1d00e7"; epistemic_kind = "relationship"; subject = "沿岸居民"; predicate = "依靠湖区谋生"; object = "渔业、船运与码头劳作"; perspective = "neutral"; time_scope = "historical_or_current_unknown"; confidence = "medium"; source_origin_ids = @("authoring_provisional.origin.d5bd6d3382672ae542a1cc22"); polarity = "affirmed"; revision_note = "r3 新增（lac-01，decision=execute）" },
    @{ claim_id = "authoring_provisional.claim.d2c7e2dc921eafdf047a31c8"; epistemic_kind = "relationship"; subject = "帝国麦子与斯特吉亚毛皮"; predicate = "沿湖区水路转运"; object = "湖区南北双向贸易路线"; perspective = "neutral"; time_scope = "historical_or_current_unknown"; confidence = "medium"; source_origin_ids = @("authoring_provisional.origin.7ba74330155cd80ad446d57a"); polarity = "affirmed"; revision_note = "r3 新增（lac-02，decision=execute；仅路线层面，不写规模）" },
    @{ claim_id = "authoring_provisional.claim.c9ff734b8dd6d7968fb1a954"; epistemic_kind = "source_fact"; subject = "拉科尼斯湖"; predicate = "在冬季仍可通航"; object = "陆路受冰雪阻断时仍具运输价值"; perspective = "neutral"; time_scope = "historical_or_current_unknown"; confidence = "medium"; source_origin_ids = @("authoring_provisional.origin.6792cb2c1224889c505df1ba", "authoring_provisional.origin.bfc66570e6db11cb4b5768ec", "authoring_provisional.origin.9989debf022c34e94bddd43e", "authoring_provisional.origin.28a21f9e5db2488ea6a4bc5e", "authoring_provisional.origin.d35c759a5ef84e3997f96df0"); polarity = "affirmed"; revision_note = "r2 保留（r3-decision: keep）" },
    @{ claim_id = "authoring_provisional.claim.e1158ae544d37d2f31aeed53"; epistemic_kind = "interpretation"; subject = "帝国（防务）叙述"; predicate = "将冬季航运解释为"; object = "北境防务与运兵优势（控制湖区舰队者掌握冬季战略主动）"; perspective = "imperial"; time_scope = "source_bound_view"; confidence = "medium"; source_origin_ids = @("authoring_provisional.origin.ce4011516c2d730bfe559bea"); polarity = "affirmed"; revision_note = "r3 新增（lac-04，decision=execute_with_refinement；主体定为帝国（防务）叙述，取代 r2 claim.702611）" },
    @{ claim_id = "authoring_provisional.claim.7707292541174752ca88df88"; epistemic_kind = "rumor"; subject = "湖水变红的德律亚传说"; predicate = "被叙述为"; object = "湖区战争记忆的一部分；无直接证据"; perspective = "local_oral_tradition"; time_scope = "historical_unknown"; confidence = "low"; source_origin_ids = @("authoring_provisional.origin.6792cb2c1224889c505df1ba", "authoring_provisional.origin.9989debf022c34e94bddd43e", "authoring_provisional.origin.28a21f9e5db2488ea6a4bc5e", "authoring_provisional.origin.d35c759a5ef84e3997f96df0"); polarity = "affirmed"; revision_note = "r2 保留（r3-decision: keep）" },
    @{ claim_id = "authoring_provisional.claim.c829e454ee3264a31fe6c451"; epistemic_kind = "interpretation"; subject = "浅滩铁锈色沉积"; predicate = "被考察叙述解释为"; object = "可能由有机质大量腐烂形成的铁质氧化层自然沉积（与血染传说巧合性对应；保留原文'可能'级别）"; perspective = "survey_narrative"; time_scope = "source_bound_view"; confidence = "medium"; source_origin_ids = @("authoring_provisional.origin.a5239cb238349326884324ff"); polarity = "affirmed"; revision_note = "r3 新增（lac-03，decision=execute_with_refinement）" }
)

# ---- 计算 span 偏移 ----
$fullText = ($paragraphs -join "`n`n")
$paraOffsets = @()
$off = 0
foreach ($p in $paragraphs) { $paraOffsets += ,@($off, $p.Length); $off += $p.Length + 2 }

$spans = @()
$sid = 0
foreach ($sd in $spanDefs) {
    $po = $paraOffsets[$sd.para]
    if ($sd.ContainsKey("splitAtSentence")) {
        $p = $paragraphs[$sd.para]
        $idx = $p.IndexOf("。")
        if ($idx -lt 0) { throw "paragraph $($sd.para): no sentence boundary" }
        if ($sd.splitAtSentence -eq 1) { $s = $po[0]; $e = $po[0] + $idx + 1 } else { $s = $po[0] + $idx + 1; $e = $po[0] + $p.Length }
        $sname = "p$($sd.para)-s$($sd.splitAtSentence)"
    } else {
        $s = $po[0]; $e = $po[0] + $po[1]
        $sname = "p$($sd.para)"
    }
    $spans += [ordered]@{
        target_locator = "#/target_text/paragraph/$($sd.para)"
        claim_ids = $sd.claims
        source_origin_ids = $sd.origins
        operation = $sd.op
        text_start = $s
        text_end = $e
        sentence_id = $sname
        unsupported_additions = @()
        inference_type = "none"
    }
    $sid++
}

$out = [ordered]@{
    schema_version = "awake.worldbook.migration.semantic-rewrite-candidate.v1"
    batch_id = "semantic-rewrite.download-20260903"
    revision_line = "r3"
    base_revision = $baseRevision
    target_revision = $targetRevision
    source_snapshot_id = "semantic-pilot30.download-20260903"
    candidate_id = $candidateId
    status = "needs_review"
    title = $title
    domain = "geography"
    content_tier = "pending"
    decision_record = "SEMANTIC-R3-DECISION-RECORD-20260911"
    claims = $claims
    target_text = $fullText
    target_spans = $spans
    review = @{ understood_rewrite = "pending"; reviewer = "author.pending"; review_state = "needs_review"; rejection_reason = $null }
    source_snapshot_sha256 = "629a24fc633c035aa83d369b54d35cd3a893eb429d5fcf501e2e2ff0c717f129"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$json = $out | ConvertTo-Json -Depth 8
$path = Join-Path $OutDir ("$candidateId.r3.json")
[IO.File]::WriteAllText($path, $json, (New-Object Text.UTF8Encoding $true))
Write-Host "WROTE $path"
Write-Host "text_length=$($fullText.Length) spans=$($spans.Count) claims=$($claims.Count)"
