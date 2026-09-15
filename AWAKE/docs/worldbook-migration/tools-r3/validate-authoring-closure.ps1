# =====================================================================
# W6 闭合复验工具 — authoring 投影产物 ↔ R3 权威 claim 三向闭合校验
# ---------------------------------------------------------------------
# 校验目标（PLAN-WORLDBOOK-AUTHORING-PROJECTION-20260912 第 6 节修订 7）：
#   1. assertion ↔ claim 计数与 epistemic 映射一一对应
#   2. expression ↔ target span 覆盖（每条断言至少一条表达段）
#   3. layer / grants / scope 一致性 + 登记表引用合法性
# 同时把"口径待裁定/遗留未实现/跨线阻断"显式记为 OPEN 或 BLOCKED，不静默通过。
#   OPEN    = 本侧可直接完成的待裁定/待办项
#   BLOCKED = 经查不能由世界书侧单方面完成、依赖他线基线的项（须留证据，不降级为 PASS）
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
    [string]$EntityRegistryRoot = 'D:\AWAKE-Dev\AWAKE\docs\mappings\persona-entity\generations',
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
# 清单内两类锚点：lore（语义实体）与 game_anchored（游戏锚定实体，ID 权威来源为 persona-entity 登记表）
$loreIds        = @($loreReg.entities | Where-Object { $_.anchor_class -ne 'game_anchored' } | ForEach-Object { $_.entity_id })
$gameAnchorIds  = @($loreReg.entities | Where-Object { $_.anchor_class -eq 'game_anchored' } | ForEach-Object { $_.entity_id })

# persona-entity 登记表（generations 下最新一版）——game_anchored 锚点的真实性复核面
$registryIds = @()
$registryDebug = "未找到 entity-registry.v1.json（root=$EntityRegistryRoot）"
$entityRegFile = @(Get-ChildItem -Path $EntityRegistryRoot -Recurse -Filter 'entity-registry.v1.json' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending) | Select-Object -First 1
if ($null -ne $entityRegFile) {
    $entityReg = Read-Json $entityRegFile.FullName
    $registryIds = @($entityReg.entities | ForEach-Object { $_.entity_id })
    $registryDebug = "来源 $($entityRegFile.Directory.Name)/$($entityRegFile.Name)，$($registryIds.Count) 实体"
}

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

# C12 entity_ids 合法：lore 锚点须登记于本清单，game_anchored 锚点须同时存在于 persona-entity 登记表
# （2026-09-12 放宽：原口径『必须全部是 entity.lore.*』是德里亚特误分类的镜像，会把正确绑定误报 FAIL）
$badEntity = @()
foreach ($key in $perDoc.Keys) {
    $ids = @($perDoc[$key].entity_ids)
    if ($ids.Count -eq 0) { $badEntity += "$key entity_ids 为空"; continue }
    foreach ($id in $ids) {
        if ($loreIds -contains $id) { continue }
        if ($gameAnchorIds -contains $id) {
            if ($registryIds.Count -gt 0 -and $registryIds -notcontains $id) { $badEntity += "${key}:${id} 声明为 game_anchored 但不在 persona-entity 登记表" }
            continue
        }
        $badEntity += "${key}:${id} 不在清单（既非 lore 亦非 game_anchored）"
    }
}
$c12title = 'entity_ids 非空；lore 锚点登记于本清单、game_anchored 锚点存在于 persona-entity 登记表'
$c12detail = "lore=$($loreIds.Count) / game_anchored=$($gameAnchorIds.Count)；persona-entity $registryDebug；引用实体 $((($entityUsed | Sort-Object -Unique)) -join ', ')"
if ($badEntity.Count -eq 0) { Add-Check 'C12' $c12title 'PASS' $c12detail }
else { Add-Check 'C12' $c12title 'FAIL' ($badEntity -join '; ') }

# C16 【BLOCKED】语义锚点（entity.lore.*）在现行 RuntimePackageCompiler 下不可编译
# 依据：CanonicalEntityRef 对非 hero/clan/settlement 的 kind 抛 WB-DOC-003（RuntimePackageCompiler.cs:253），
# BuildEntry 对每档每个 entity_id 无条件调用（同文件 147）；测试 EditorContent.Tests/Program.cs:96-104 钉死该语义。
# 属 Studio C# 侧改动，本侧不实施；显式记为 BLOCKED，不静默通过。
$loreAnchorDocs = @($perDoc.Keys | Where-Object { @($perDoc[$_].entity_ids | Where-Object { $loreIds -contains $_ }).Count -gt 0 })
$c16 = "含 entity.lore.* 锚点的档案 $($loreAnchorDocs.Count)/$($perDoc.Keys.Count) 档（$($loreAnchorDocs -join ', ')）；"
$c16 += "RuntimePackageCompiler.CanonicalEntityRef（Core/RuntimePackageCompiler.cs:253）对 parts[1] 非 hero/clan/settlement 抛 WB-DOC-003，BuildEntry 无条件调用（同文件 147），故这些档案 Compile() 必失败。"
$c16 += " 证据三条：① 源码白名单；② 无条件调用点；③ tests/Awake.WorldbookStudio.EditorContent.Tests/Program.cs:96-104（entity.settlement.town_v5 通过 / entity.bogus.thing 抛 WB-DOC-003）。"
$c16 += " 与 ENTRY-LINKAGE-MAP 第 26 行『无需改 C#』矛盾：该结论只对锚点名称查找（LookupEntityAnchorNames，容错）成立，对引用规范化（CanonicalEntityRef，白名单）不成立，合并 lore 分区解决不了。"
$c16 += " 未暴露原因：W6 走 schema 结构校验，未执行 Compile()（投影批 scope 已排除 compile/export/publish）。"
$c16 += " 归属 Studio 侧（改 RuntimePackageCompiler + 测试夹具），处置选项见 LORE-ENTITY-REGISTER corrections_20260912.compile_blocker_lore_kind。"
Add-Check 'C16' '语义锚点可编译性（entity.lore.* vs 编译器 kind 白名单）' 'BLOCKED' $c16

# C13 【BLOCKED】W4 独立审查修订 P1-3：place_cluster → fallback_referral_ids 接线
$referralIds = @($referralReg.referrals | ForEach-Object { $_.id })
$clusterReferrals = @($referralIds | Where-Object { $_ -like 'referral.cluster.*' })
$totalFallback = 0
foreach ($key in $yamlText.Keys) { $totalFallback += ([regex]::Matches($yamlText[$key], 'fallback_referral_ids')).Count }
if ($clusterReferrals.Count -gt 0 -and $totalFallback -ge $totalExpressions) {
    Add-Check 'C13' 'W4 P1-3：place_cluster → fallback_referral_ids 接线' 'PASS' "referral.cluster.*=$($clusterReferrals.Count) 条；fallback 覆盖 $totalFallback/$totalExpressions"
} else {
    $c13 = "未实施，经查为跨线阻断项，不能由世界书侧单方面完成："
    $c13 += " ① 校验器 Core/Application.cs:820-821 对每条 fallback_referral_ids 强制命中登记表（WB-REFERRAL-001）、且目标须 publicly_askable（WB-REFERRAL-002）——故必须先扩 referral-registry；"
    $c13 += " ② 该登记表被 Studio golden 测试钉死为冻结输入：tests/Awake.WorldbookStudio.Tests/Program.cs:743 断言其文件 SHA-256 == 6E17075F…（失败信息 'referral registry golden hash drifted'），tests/fixtures/a3-1-authoring-template-golden.v1.json 与 a3-2/a3-3 golden 夹具内嵌 version 1.0.0 + 该哈希；"
    $c13 += " ③ 改哈希还会经 Application.cs:742（WB-REGISTRY-001）触发全部 12 档 registry_bindings 重绑；"
    $c13 += " ④ golden 依 A3.1/A3.2 计划约定『不得由当前实现运行时重生成、须独立人工确认』，且属 Studio C# 测试代码——本投影计划第 2 节『明确不做』已排除。"
    $c13 += " 结论：归 Studio 侧联合批次（改登记表 + 重签 golden + 12 档重绑）。世界书侧待接线交付物已备：projection/CLUSTER-REFERRAL-MAP-20260912.json（5 簇 / 12 条 referral.cluster.* / 逐档 fallback 映射，由 C15 独立校验）。"
    $c13 += " 当前实测：referral 登记表 $($referralIds.Count) 条通用 referral、无 referral.cluster.*；12 档 fallback 覆盖 0/$totalExpressions。"
    Add-Check 'C13' 'W4 P1-3：place_cluster → fallback_referral_ids 接线' 'BLOCKED' $c13
}

# C14 【已裁定】表达式计数口径：维持 40
$layerSpans = 0
foreach ($key in $r3.Keys) { $layerSpans += @($r3[$key].target_spans | Where-Object { $_.PSObject.Properties.Name -contains 'layer' }).Count }
$RuledExpressions = 40
if ($totalExpressions -eq $RuledExpressions) {
    Add-Check 'C14' "expression 计数口径已裁定：维持 $RuledExpressions" 'PASS' "实测 $totalExpressions = R3 带 layer 标注 target_span $layerSpans 条（即第 6 节修订 7 的计划口径 $ExpectedExpressionsPlan）+ 中立内核补挂 summary 档 $($totalExpressions - $layerSpans) 条。裁定理由：表达段是『可达性载体』而非文案副本（summary 档文本与断言正文逐字一致）——中立内核若无表达段则对任何身份不可达，分层失去地基。authoring.v1 schema 下 40 与 16 均合法（expressions 必填但无 minItems）。裁定记录：PLAN 第 8.1 节 / PROJECTION-MANIFEST.corrections_20260912.expression_count_ruling；第 6 节修订 7 原写『预期 16』保留不改写。"
} else {
    Add-Check 'C14' "expression 计数口径已裁定：维持 $RuledExpressions" 'FAIL' "实测 $totalExpressions，与裁定值 $RuledExpressions 不一致，须重新裁定。"
}

# C15 待接线交付物 CLUSTER-REFERRAL-MAP 与 R3 place_cluster 一致（校验本侧交付物本身）
$mapPath = Join-Path $ProjectionDir 'CLUSTER-REFERRAL-MAP-20260912.json'
$badMap = @(); $mapEntries = @(); $mapClusters = 0
if (-not (Test-Path $mapPath)) { $badMap += 'CLUSTER-REFERRAL-MAP-20260912.json 缺失' }
else {
    $map = Read-Json $mapPath
    $mapEntries = @($map.cluster_referrals)
    $fb = $map.fallback_by_doc
    $mapClusters = @($mapEntries | ForEach-Object { $_.place_cluster } | Sort-Object -Unique).Count
    if ($mapEntries.Count -ne $docFiles.Count) { $badMap += "map referral 数 $($mapEntries.Count) 不等于档数 $($docFiles.Count)" }
    $ids = @($mapEntries | ForEach-Object { $_.referral_id })
    if (($ids | Sort-Object -Unique).Count -ne $ids.Count) { $badMap += 'referral_id 有重复' }
    if ($ids.Count -ne ($ids | Sort-Object -Unique).Count) { $badMap += 'referral_id 去重后数量不符' }
    foreach ($e in $mapEntries) {
        if ($e.referral_id -notmatch '^referral\.cluster\.[a-z0-9]+(?:[._-][a-z0-9]+)*$') { $badMap += "命名不合规: $($e.referral_id)"; continue }
        if (-not $r3.Contains($e.target_doc_key)) { $badMap += "target_doc_key 不在 R3: $($e.target_doc_key)"; continue }
        if ($r3[$e.target_doc_key].place_cluster -ne $e.place_cluster) { $badMap += "簇归属与 R3 不符: $($e.target_doc_key)" }
        if ($e.publicly_askable -ne $true) { $badMap += "publicly_askable 非 true: $($e.referral_id)" }
    }
    foreach ($key in $r3.Keys) {
        $cl = $r3[$key].place_cluster
        $expect = @($mapEntries | Where-Object { $_.place_cluster -eq $cl -and $_.target_doc_key -ne $key } | ForEach-Object { $_.referral_id } | Sort-Object)
        $got = @()
        $prop = $fb.PSObject.Properties[$key]
        if ($prop) { $got = @($prop.Value | Sort-Object) }
        if (($expect -join ',') -ne ($got -join ',')) { $badMap += "${key} fallback 映射不符（期望 $($expect -join '/')，实得 $($got -join '/')）" }
    }
}
if ($badMap.Count -eq 0) {
    Add-Check 'C15' '待接线交付物 CLUSTER-REFERRAL-MAP 与 R3 place_cluster 一致' 'PASS' "$mapClusters 簇 / $($mapEntries.Count) 条 referral.cluster.* / 逐档 fallback 映射与同簇兄弟档严格相等（status=prepared_not_wired，未接线）"
} else {
    Add-Check 'C15' '待接线交付物 CLUSTER-REFERRAL-MAP 与 R3 place_cluster 一致' 'FAIL' ($badMap -join '; ')
}

# ---------- 证据落盘 ----------
$fail = @($checks | Where-Object { $_.status -eq 'FAIL' })
$open = @($checks | Where-Object { $_.status -eq 'OPEN' })
$blocked = @($checks | Where-Object { $_.status -eq 'BLOCKED' })
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
        blocked = $blocked.Count
        blocking = @($fail | ForEach-Object { "$($_.id) $($_.title)" })
        open_items = @($open | ForEach-Object { "$($_.id) $($_.title)" })
        blocked_items = @($blocked | ForEach-Object { "$($_.id) $($_.title)" })
        verdict = if ($fail.Count -eq 0 -and $open.Count -eq 0 -and $blocked.Count -eq 0) { 'CLOSED' }
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
    $color = switch ($c.status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } 'BLOCKED' { 'Magenta' } default { 'Yellow' } }
    Write-Host ("[{0}] {1,-4} {2}" -f $c.status, $c.id, $c.title) -ForegroundColor $color
    Write-Host ("        {0}" -f $c.detail)
}
Write-Host ''
Write-Host ("assertion={0}  expression={1}  grants={2}  档数={3}" -f $totalAssertions, $totalExpressions, $totalGrants, $docFiles.Count)
Write-Host ("VERDICT = {0}  (PASS {1} / FAIL {2} / OPEN {3} / BLOCKED {4})" -f $evidence.summary.verdict, $evidence.summary.pass, $fail.Count, $open.Count, $blocked.Count) -ForegroundColor Cyan
Write-Host ("evidence -> {0}" -f $OutFile)
if ($fail.Count -gt 0) { exit 1 }
exit 0
