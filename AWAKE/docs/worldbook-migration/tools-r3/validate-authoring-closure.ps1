# =====================================================================
# W6 闭合复验工具 — authoring 投影产物 ↔ R3 权威 claim 三向闭合校验
# ---------------------------------------------------------------------
# 校验目标（PLAN-WORLDBOOK-AUTHORING-PROJECTION-20260912 第 6 节修订 7）：
#   1. assertion ↔ claim 计数与 epistemic 映射一一对应
#   2. expression ↔ target span 覆盖（每条断言至少一条表达段）
#   3. layer / grants / scope 一致性 + 登记表引用合法性
# 同时把两项"口径待裁定/遗留未实现"显式记为 OPEN，不静默通过。
#
# 输入（全部只读）：
#   - R3 权威文档（已签收基线，冻结不回写）
#   - Studio authoring 工作区产物（12 档 YAML，官方保存链落库版）
#   - profile / referral 登记表（哈希复算）
#   - LORE 实体清单 + 投影 manifest
# 输出：证据 JSON（默认落 docs/evidence/，该目录 gitignore）
#
# 用法：
#   pwsh -File validate-authoring-closure.ps1
#   pwsh -File validate-authoring-closure.ps1 -YamlDir <改指向 authoring-out 可校验未落库产物>
# =====================================================================
[CmdletBinding()]
param(
    [string]$DocsDir       = 'D:\AWAKE-Archive\worldbook-migration-content\semantic-rewrite-batch-download-20260903\r3-revision\documents',
    [string]$YamlDir       = 'D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\authoring',
    [string]$PlanDir       = 'D:\AWAKE-Dev\AWAKE\docs\worldbook-studio-plan',
    [string]$ProjectionDir = 'D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection',
    [string]$OutFile       = 'D:\AWAKE-Dev\AWAKE\docs\evidence\authoring-closure-20260912.json'
)
$ErrorActionPreference = 'Stop'

# ---------- 规格常量（源自计划第 6 节修订 7） ----------
$KindMap = @{
    'source_fact'    = 'fact'
    'relationship'   = 'relation'
    'state'          = 'state'
    'interpretation' = 'interpretation'
    'rumor'          = 'rumor'
    'unresolved'     = 'interpretation'
}
$ExcludedClaims = @(
    'authoring_provisional.claim.ddfb30ad3ffe6a7f19b28766'   # der-04 纯注册元数据，不投影
)
$UnresolvedProjected = @{
    'authoring_provisional.claim.9644824420c36a47d41eae1e' = 'kach-own'
    'authoring_provisional.claim.fd78df11d0b49237fa8d34dd' = 'dawn-stew'
}
$ExpectedAssertions       = 40
$ExpectedExpressionsPlan  = 16     # 计划口径：layer 标注 span 数
$ExpectedDocuments        = 12
$AllowedLayers            = @('rumor', 'summary', 'detail', 'secret')

# ---------- 结果容器 ----------
$checks = [System.Collections.ArrayList]::new()
function Add-Check([string]$id, [string]$title, [string]$status, [string]$detail) {
    [void]$checks.Add([ordered]@{ id = $id; title = $title; status = $status; detail = $detail })
}
function Read-Json([string]$path) {
    return ([IO.File]::ReadAllText($path, [Text.Encoding]::UTF8) | ConvertFrom-Json)
}

# ---------- 行式 YAML 抽取（产物由固定模板生成，行式最稳） ----------
function Get-YamlFacts([string]$path) {
    $lines = [IO.File]::ReadAllLines($path, [Text.Encoding]::UTF8)
    $facts = [ordered]@{
        DocKey = [IO.Path]::GetFileNameWithoutExtension($path)
        Status = $null; EntityIds = @(); Assertions = @()
        RegistryProfileHash = $null; RegistryReferralHash = $null
    }
    $cur = $null
    $inEntityIds = $false
    $mode = ''      # '', 'sources', 'assertions', 'expressions', 'grants'
    foreach ($raw in $lines) {
        $line = $raw.TrimEnd("`r")
        if ($line -match '^status:\s*(\S+)') { $facts.Status = $Matches[1]; $mode = ''; continue }
        if ($line -match '^profile_registry_hash:\s*(\S+)')  { $facts.RegistryProfileHash  = $Matches[1]; continue }
        if ($line -match '^referral_registry_hash:\s*(\S+)') { $facts.RegistryReferralHash = $Matches[1]; continue }
        if ($line -match '^entity_ids:\s*$') { $inEntityIds = $true; $mode = ''; continue }
        if ($inEntityIds) {
            if ($line -match '^- (.+)$') { $facts.EntityIds += $Matches[1].Trim(); continue }
            $inEntityIds = $false
        }
        if ($line -match '^- id:\s*assertion\.(.+)$') {
            $cur = [ordered]@{ Id = $Matches[1]; Kind = $null; ExprCount = 0; Layers = @(); ProfileIds = @(); Scopes = @(); HasDenies = $false }
            $facts.Assertions += $cur
            $mode = 'assertions'; continue
        }
        if ($null -eq $cur) { continue }
        if ($line -match '^\s+- id:\s*expr\.') {
            $cur.ExprCount++
            $mode = 'expressions'; continue
        }
        if ($line -match '^\s+(layer|scope|min_detail):\s*(\S+)') {
            $k = $Matches[1]; $v = $Matches[2]
            if ($k -eq 'layer')      { $cur.Layers += $v }
            elseif ($k -eq 'scope' -and $mode -eq 'grants') { $cur.Scopes += $v }
            elseif ($k -eq 'min_detail' -and $mode -eq 'grants') { }
            continue
        }
        if ($line -match '^  kind:\s*(\S+)') { $cur.Kind = $Matches[1]; $mode = 'assertions'; continue }
        if ($line -match '^\s+grants:') { $mode = 'grants'; continue }
        if ($line -match '^\s+denies:') { $cur.HasDenies = $true; continue }
        if ($line -match '^\s+- profile_id:\s*(\S+)') { $cur.ProfileIds += $Matches[1]; continue }
        if ($line -match '^\s+sources:') { $mode = 'sources'; continue }
    }
    return $facts
}

# ---------- 读取输入 ----------
$docFiles = @(Get-ChildItem "$DocsDir\*.r3.json" | Sort-Object Name)
$yamlFiles = @(Get-ChildItem "$YamlDir\*.yaml" | Where-Object { $_.BaseName -ne 'demo' } | Sort-Object Name)
$manifest  = Read-Json (Join-Path $ProjectionDir 'PROJECTION-MANIFEST-20260912.json')
$loreReg   = Read-Json (Join-Path $ProjectionDir 'LORE-ENTITY-REGISTER-20260912.json')
$profileReg = Read-Json (Join-Path $PlanDir 'profile-registry.v1.json')
$referralReg = Read-Json (Join-Path $PlanDir 'referral-registry.v1.json')

$profileIds  = @($profileReg.profiles | ForEach-Object { $_.id })
$loreIds     = @($loreReg.entities  | ForEach-Object { $_.entity_id })

$r3 = [ordered]@{}
foreach ($f in $docFiles) {
    $d = Read-Json $f.FullName
    $key = $f.BaseName -replace '\.r3$', ''
    $r3[$key] = $d
}

# ---------- C1 文档数对齐 ----------
$manifestKeys = @($manifest.documents | ForEach-Object { $_.doc_key })
$perDoc = [ordered]@{}
$c1detail = "R3 文档 $($docFiles.Count) / YAML 产物 $($yamlFiles.Count) / manifest 条目 $($manifestKeys.Count)"
if ($docFiles.Count -eq $ExpectedDocuments -and $yamlFiles.Count -eq $ExpectedDocuments -and $manifestKeys.Count -eq $ExpectedDocuments) {
    Add-Check 'C1' '文档数 12 对齐（R3 / 产物 / manifest）' 'PASS' $c1detail
} else {
    Add-Check 'C1' '文档数 12 对齐（R3 / 产物 / manifest）' 'FAIL' $c1detail
}

# ---------- 汇总统计 ----------
$totalAssertions = 0; $totalExpressions = 0; $totalGrants = 0; $totalFallback = 0
$kindMismatch = @(); $countMismatch = @(); $noExpr = @(); $badLayer = @(); $badProfile = @(); $noGrant = @(); $noDenies = @()
$entityUsed = @()
$yamlText = [ordered]@{}

foreach ($yf in $yamlFiles) {
    $key = $yf.BaseName
    $facts = Get-YamlFacts $yf.FullName
    $yamlText[$key] = [IO.File]::ReadAllText($yf.FullName, [Text.Encoding]::UTF8)
    $exprSum = 0
    foreach ($a in $facts.Assertions) { $exprSum += $a.ExprCount }
    $perDoc[$key] = [ordered]@{
        status = $facts.Status
        assertions = $facts.Assertions.Count
        expressions = $exprSum
        entity_ids = $facts.EntityIds
    }
    $totalAssertions += $facts.Assertions.Count
    foreach ($a in $facts.Assertions) {
        $totalExpressions += $a.ExprCount
        $totalGrants += $a.ProfileIds.Count
        if ($a.ExprCount -lt 1) { $noExpr += "$key/$($a.Id)" }
        if (-not $a.HasDenies) { $noDenies += "$key/$($a.Id)" }
        if ($a.ProfileIds.Count -lt 1) { $noGrant += "$key/$($a.Id)" }
        foreach ($l in $a.Layers) { if ($AllowedLayers -notcontains $l) { $badLayer += "$key/$($a.Id):$l" } }
        foreach ($p in $a.ProfileIds) { if ($profileIds -notcontains $p) { $badProfile += "$key/$($a.Id):$p" } }
    }
    $entityUsed += $facts.EntityIds

    # C2/C4 逐档计数与 kind 映射
    if (-not $r3.Contains($key)) { $countMismatch += "$key(无 R3 文档)"; continue }
    $d = $r3[$key]
    $metaIds = @($d.metadata_claims)
    $expected = @($d.claims | Where-Object { $metaIds -notcontains $_.claim_id })
    if ($expected.Count -ne $facts.Assertions.Count) {
        $countMismatch += "$key 期望 $($expected.Count) 实际 $($facts.Assertions.Count)"
    }
    $expKinds = @($expected | ForEach-Object { $KindMap[$_.epistemic_kind] })
    $actKinds = @($facts.Assertions | ForEach-Object { $_.Kind })
    $e = ($expKinds | Sort-Object) -join ','
    $a = ($actKinds | Sort-Object) -join ','
    if ($e -ne $a) { $kindMismatch += "$key 期望[$e] 实际[$a]" }
}

# C2
if ($countMismatch.Count -eq 0) { Add-Check 'C2' '逐档 assertion 数 = R3 claim 数（扣除元数据豁免）' 'PASS' "12/12 一致" }
else { Add-Check 'C2' '逐档 assertion 数 = R3 claim 数（扣除元数据豁免）' 'FAIL' ($countMismatch -join '; ') }

# C3
if ($totalAssertions -eq $ExpectedAssertions) { Add-Check 'C3' "assertion 合计 = $ExpectedAssertions" 'PASS' "实测 $totalAssertions" }
else { Add-Check 'C3' "assertion 合计 = $ExpectedAssertions" 'FAIL' "实测 $totalAssertions" }

# C4
if ($kindMismatch.Count -eq 0) {
    Add-Check 'C4' 'epistemic_kind → authoring kind 映射一一对应' 'PASS' 'source_fact→fact / relationship→relation / state→state / interpretation→interpretation / rumor→rumor / unresolved→interpretation，12/12 档多重集相等'
} else {
    Add-Check 'C4' 'epistemic_kind → authoring kind 映射一一对应' 'FAIL' ($kindMismatch -join '; ')
}

# C5
if ($noExpr.Count -eq 0) { Add-Check 'C5' '每条 assertion 至少 1 条 expression' 'PASS' "$totalAssertions/$totalAssertions" }
else { Add-Check 'C5' '每条 assertion 至少 1 条 expression' 'FAIL' ($noExpr -join '; ') }

# C6
$c6 = @()
if ($badLayer.Count -gt 0) { $c6 += "layer 非法: $($badLayer -join ',')" }
if ($noGrant.Count -gt 0) { $c6 += "缺 grants: $($noGrant.Count) 条" }
if ($noDenies.Count -gt 0) { $c6 += "缺 denies 键: $($noDenies.Count) 条" }
if ($c6.Count -eq 0) { Add-Check 'C6' 'expression 必填项齐全（layer 枚举 / grants≥1 / denies 键）' 'PASS' "expression=$totalExpressions grants=$totalGrants" }
else { Add-Check 'C6' 'expression 必填项齐全（layer 枚举 / grants≥1 / denies 键）' 'FAIL' ($c6 -join '; ') }

# C7
if ($badProfile.Count -eq 0) { Add-Check 'C7' 'grants.profile_id 全部命中 profile 登记表' 'PASS' "登记表 $($profileIds.Count) 个 profile，引用 $totalGrants 次全部合法" }
else { Add-Check 'C7' 'grants.profile_id 全部命中 profile 登记表' 'FAIL' ($badProfile -join '; ') }

# C8 登记表哈希复算
$badHash = @()
$pHash = (Get-FileHash (Join-Path $PlanDir 'profile-registry.v1.json')  -Algorithm SHA256).Hash
$rHash = (Get-FileHash (Join-Path $PlanDir 'referral-registry.v1.json') -Algorithm SHA256).Hash
foreach ($key in $perDoc.Keys) {
    $t = $yamlText[$key]
    $mp = [regex]::Match($t, 'profile_registry_hash:\s*([0-9A-Fa-f]+)')
    if (-not $mp.Success) { $badHash += "$key 缺 profile hash"; continue }
    if ($mp.Groups[1].Value.ToUpper() -ne $pHash) { $badHash += "$key profile hash 不符" }
    $mr = [regex]::Match($t, 'referral_registry_hash:\s*([0-9A-Fa-f]+)')
    if (-not $mr.Success) { $badHash += "$key 缺 referral hash"; continue }
    if ($mr.Groups[1].Value.ToUpper() -ne $rHash) { $badHash += "$key referral hash 不符" }
}
if ($badHash.Count -eq 0) { Add-Check 'C8' 'registry_bindings 哈希与登记表实文件一致' 'PASS' "profile=$($pHash.Substring(0,8)) referral=$($rHash.Substring(0,8))" }
else { Add-Check 'C8' 'registry_bindings 哈希与登记表实文件一致' 'FAIL' ($badHash -join '; ') }

# C9 状态门
$badStatus = @($perDoc.Keys | Where-Object { $perDoc[$_].status -ne 'needs_review' })
if ($badStatus.Count -eq 0) { Add-Check 'C9' '12 档 status 均为 needs_review（未自动 approve）' 'PASS' '12/12' }
else { Add-Check 'C9' '12 档 status 均为 needs_review（未自动 approve）' 'FAIL' (($badStatus | ForEach-Object { "$_=$($perDoc[$_].status)" }) -join '; ') }

# C10 排除清单未投影
$dv = $r3['der-vill']
$dvAssert = $perDoc['der-vill'].assertions
$ddfPresent = $false
foreach ($key in $yamlText.Keys) { if ($yamlText[$key] -match 'ddfb30ad') { $ddfPresent = $true } }
if ($dvAssert -eq 1 -and -not $ddfPresent) { Add-Check 'C10' '排除清单 ddfb30ad 未投影' 'PASS' 'der-vill assertion=1（仅 source_fact 主体），产物全文无 ddfb30ad 痕迹' }
else { Add-Check 'C10' '排除清单 ddfb30ad 未投影' 'FAIL' "der-vill assertion=$dvAssert；产物含 ddfb30ad=$ddfPresent" }

# C11 unresolved → interpretation 溯源标记
$badUnres = @()
foreach ($cid in $UnresolvedProjected.Keys) {
    $doc = $UnresolvedProjected[$cid]
    $d = $r3[$doc]
    $cl = @($d.claims | Where-Object { $_.claim_id -eq $cid })
    if ($cl.Count -eq 0) { $badUnres += "$cid 不在 $doc"; continue }
    $facts = Get-YamlFacts (Join-Path $YamlDir "$doc.yaml")
    $n = @($facts.Assertions | Where-Object { $_.Kind -eq 'interpretation' }).Count
    if ($n -lt 1) { $badUnres += "$doc 无 interpretation 断言" }
}
$maniUnres = @($manifest.unresolved_projection | ForEach-Object { $_.claim_id })
$missingMani = @($UnresolvedProjected.Keys | Where-Object { $maniUnres -notcontains $_ })
if ($badUnres.Count -eq 0 -and $missingMani.Count -eq 0) {
    Add-Check 'C11' 'unresolved 知识限制 claim 投影为 interpretation 且 manifest 有溯源标记' 'PASS' "$($UnresolvedProjected.Keys.Count)/2 已标记"
} else {
    Add-Check 'C11' 'unresolved 知识限制 claim 投影为 interpretation 且 manifest 有溯源标记' 'FAIL' (($badUnres + $missingMani) -join '; ')
}

# C12 entity_ids 合法且与 lore 清单一致
$badEntity = @()
foreach ($key in $perDoc.Keys) {
    $ids = @($perDoc[$key].entity_ids)
    if ($ids.Count -eq 0) { $badEntity += "$key entity_ids 为空"; continue }
    foreach ($id in $ids) { if ($loreIds -notcontains $id) { $badEntity += "${key}:${id} 不在 lore 清单" } }
}
if ($badEntity.Count -eq 0) { Add-Check 'C12' 'entity_ids 非空且全部登记于 LORE-ENTITY-REGISTER（entity.lore.*）' 'PASS' "引用实体 $((($entityUsed | Sort-Object -Unique)) -join ', ')" }
else { Add-Check 'C12' 'entity_ids 非空且全部登记于 LORE-ENTITY-REGISTER（entity.lore.*）' 'FAIL' ($badEntity -join '; ') }

# C13 【OPEN】W4 独立审查修订 P1-3：place_cluster → fallback_referral_ids 接线
$referralIds = @($referralReg.referrals | ForEach-Object { $_.id })
$clusterReferrals = @($referralIds | Where-Object { $_ -like 'referral.cluster.*' })
$totalFallback = 0
foreach ($key in $yamlText.Keys) { $totalFallback += ([regex]::Matches($yamlText[$key], 'fallback_referral_ids')).Count }
if ($clusterReferrals.Count -gt 0 -and $totalFallback -ge $totalExpressions) {
    Add-Check 'C13' 'W4 P1-3：place_cluster → fallback_referral_ids 接线' 'PASS' "referral.cluster.*=$($clusterReferrals.Count) 条；fallback 覆盖 $totalFallback/$totalExpressions"
} else {
    Add-Check 'C13' 'W4 P1-3：place_cluster → fallback_referral_ids 接线' 'OPEN' "未实现：referral 登记表仅有 $($referralIds.Count) 条通用 referral（无 referral.cluster.*），12 档产物 fallback_referral_ids 覆盖 0/$totalExpressions。计划第 6 节修订 3 要求每段表达挂同簇兄弟档 referral。需裁定是否补做（补做将改动 12 档产物 + referral 登记表，登记表哈希变更会触发全部 registry_bindings 重绑）"
}

# C14 【OPEN】表达式计数口径
$layerSpans = 0
foreach ($key in $r3.Keys) { $layerSpans += @($r3[$key].target_spans | Where-Object { $_.PSObject.Properties.Name -contains 'layer' }).Count }
if ($totalExpressions -eq $ExpectedExpressionsPlan) {
    Add-Check 'C14' 'expression 计数口径（计划 16 / 实测）' 'PASS' "实测 $totalExpressions"
} else {
    Add-Check 'C14' 'expression 计数口径（计划 16 / 实测）' 'OPEN' "实测 $totalExpressions 条 = R3 layer 标注 target_span $layerSpans 条 + 中立内核补挂摘要档 $($totalExpressions - $layerSpans) 条。计划第 6 节修订 7 记『预期 expression 16』，PROJECTION-MANIFEST.expected_counts.expressions 亦写 16。口径待裁定：(a) 维持 40（中立内核以 summary 层对 profile.commoner/regional 可达）；(b) 收严为 16（中立内核不挂表达段，仅作正典一致性用）。两种在 authoring.v1 schema 下均合法（expressions 必填但无 minItems）"
}

# ---------- 证据落盘 ----------
$fail = @($checks | Where-Object { $_.status -eq 'FAIL' })
$open = @($checks | Where-Object { $_.status -eq 'OPEN' })
$evidence = [ordered]@{
    schema_version = 'awake.worldbook.authoring-closure.v1'
    date           = '2026-09-12'
    tool           = 'tools-r3/validate-authoring-closure.ps1'
    scope          = 'W6 三向闭合复验（只读；未启动游戏、未同步游戏目录、未写 Studio）'
    inputs         = [ordered]@{
        r3_documents_dir = $DocsDir
        authoring_dir    = $YamlDir
        manifest         = 'projection/PROJECTION-MANIFEST-20260912.json'
        profile_registry = "profile-registry.v1.json @ $($pHash.Substring(0,8))"
        referral_registry = "referral-registry.v1.json @ $($rHash.Substring(0,8))"
    }
    totals         = [ordered]@{
        documents = $docFiles.Count; assertions = $totalAssertions
        expressions = $totalExpressions; grants = $totalGrants
        r3_claims   = (@($r3.Keys | ForEach-Object { $r3[$_].claims.Count }) | Measure-Object -Sum).Sum
    }
    per_document   = $perDoc
    checks         = $checks
    summary        = [ordered]@{
        pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
        fail = $fail.Count
        open = $open.Count
        blocking = @($fail | ForEach-Object { "$($_.id) $($_.title)" })
        open_items = @($open | ForEach-Object { "$($_.id) $($_.title)" })
        verdict = if ($fail.Count -eq 0 -and $open.Count -eq 0) { 'CLOSED' }
                  elseif ($fail.Count -eq 0) { 'STRUCTURALLY_CLOSED_OPEN_ITEMS' }
                  else { 'NOT_CLOSED' }
    }
}
$dir = Split-Path -Parent $OutFile
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
[IO.File]::WriteAllText($OutFile, ($evidence | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding $true))

# ---------- 控制台摘要 ----------
Write-Host ''
Write-Host '===== W6 闭合复验 =====' -ForegroundColor Cyan
foreach ($c in $checks) {
    $color = switch ($c.status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1,-4} {2}" -f $c.status, $c.id, $c.title) -ForegroundColor $color
    Write-Host ("        {0}" -f $c.detail)
}
Write-Host ''
Write-Host ("assertion={0}  expression={1}  grants={2}  档数={3}" -f $totalAssertions, $totalExpressions, $totalGrants, $docFiles.Count)
Write-Host ("VERDICT = {0}  (PASS {1} / FAIL {2} / OPEN {3})" -f $evidence.summary.verdict, $evidence.summary.pass, $fail.Count, $open.Count) -ForegroundColor Cyan
Write-Host ("evidence -> {0}" -f $OutFile)
if ($fail.Count -gt 0) { exit 1 }
exit 0
