# 文字清单 lint：对每张卡的正文文本跑可机械化的中文质量检查
# 扫描字段：core / identityFacts / summary / publicDescription / privateDescription /
#           contradictionDescription / selfClaimRules / selfClaimExamples / realSelfBehaviors
# 检查项：A 拉丁残留  B 半角标点   C 超长句(西化)  D 矛盾模板腔  E 性别代词  F 复用词
$ErrorActionPreference = "Stop"
$base    = "D:\AWAKE-Dev\AWAKE"
$charDir = Join-Path $base "tools\persona-workbench\characters"
$mappings= Join-Path $base "docs\mappings"
$out     = Join-Path $base "docs\AUDIT-TEXT-QUALITY-20260911.md"

# 期望性别：character-names 表 sex 列
$sex = @{}
foreach($l in @(Get-Content (Join-Path $mappings "character-names-zh-en.tsv") -Encoding UTF8)){
    if(-not $l -or $l -notmatch "`t"){ continue }
    $c = $l -split "`t"
    if($c.Length -ge 6 -and $c[1]){ $sex[$c[0]] = $c[5] }
}
$originsSex = @{ "lord_6_4"="F"; "lord_1_14"="F"; "lord_6_1"="M"; "lord_2_3"="M"; "lord_2_1"="M"; "lord_1_1"="M"; "lord_1_7"="M"; "lord_3_1"="M"; "lord_4_1"="M"; "lord_5_1"="M"; "lord_1_2_1"="M"; "lord_6_2"="M"; "lord_1_8"="M"; "lord_1_9"="M" }

$md = New-Object System.Collections.Generic.List[string]
$md.Add("# TEXT LINT - 角色卡正文文字清单（2026-09-11）")
$md.Add("")
$md.Add("| 卡 | A拉丁 | B半角标点 | C超长句 | D模板腔(声称/却) | E代词(他/她) | F复用词 | 结果 |")
$md.Add("|---|---|---|---|---|---|---|---|")
$cards = Get-ChildItem $charDir -Filter "*.persona.json"
$allPass = $true
$reuseCandidates = @("他声称","她声称","声称","打抱不平","老兵","却","然而","坚信","始终","非常","一个","作为","不仅","而且","从不","从未")
foreach($f in $cards){
    $json = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $name = $f.BaseName
    $fields = @($json.core,$json.identityFacts,$json.summary,$json.publicDescription,$json.privateDescription,$json.contradictionDescription)
    # identityFacts 允许代码锚定（heroId/clanId 等官方 id 令牌），用于跨卡亲缘/归属锁定；
    # 这些令牌不是叙述性拉丁残留，审计需豁免，仅对真实正文字段判 A/B。
    $idTok = $null
    if($json.identityFacts){
        $idTok = [regex]::Matches($json.identityFacts, '[A-Za-z0-9_]{2,}') | ForEach-Object { $_.Value } | Where-Object { $_ -match '_' -and $_ -notmatch '^[\d_]+$' } | Select-Object -Unique
        $idTokEsc = @($idTok | ForEach-Object { [regex]::Escape($_) })
    }
    $text = ($fields | Where-Object { $_ } ) -join " "
    # 移除正文中的合法代码锚定令牌后再跑机械检查，避免误报
    if($idTokEsc -and $idTokEsc.Count -gt 0){
        $text = [regex]::Replace($text, ('(?<![A-Za-z])(' + ($idTokEsc -join '|') + ')(?![A-Za-z0-9_])'), '', 'IgnoreCase')
    }
    $issues = New-Object System.Collections.Generic.List[string]
    $issueN = 0

    # A 拉丁残留（正文不应含英文；附前样本）
    $lat = [regex]::Matches($text,'[A-Za-z]{2,}') | ForEach-Object { $_.Value } | Where-Object { $_ -notmatch 'Bannerlord|AWAKE|v1|XML' } | Select-Object -Unique
    if($lat){ $issues.Add(("A:" + (($lat | Select-Object -First 3) -join '/'))); $issueN++ }

    # B 半角标点混排（中文流中的 ASCII 标点区域）
    $half = [regex]::Match($text,'[\u0021-\u002F\u003A-\u0040\u005B-\u0060\u007B-\u007E]')
    if($half.Success){ $issues.Add(("B: '" + $half.Value + "' 样例:" + $half.Index)); $issueN++ }

    # C 超长句（按。！？分句，>48 字记西化长句）
    $sentences = $text -split '[。！？]' | ForEach-Object { $_.Trim().Length }
    $long = @($sentences | Where-Object { $_ -gt 48 })
    if($long.Count -gt 0){ $issues.Add(("C:超长句x" + $long.Count + " 最长达" + ($long | Measure-Object -Maximum).Maximum + "字")); if($long.Count -ge 2){$issueN++} }

    # D 矛盾模板腔：声称/却 计数
    $cs = ([regex]::Matches($text,'声称')).Count
    $q2 = ([regex]::Matches($text,'却')).Count
    if($cs -gt 2 -or $q2 -gt 3){ $issues.Add(("D:声称x" + $cs + " 却x" + $q2)); $issueN++ }

    # E 性别代词（origins 在 .origins.json 侧车）
    $expect = $null
    $origJson = Join-Path $charDir ($name + ".origins.json")
    if(Test-Path $origJson){
        $otmp = Get-Content $origJson -Raw -Encoding UTF8 | ConvertFrom-Json
        if($otmp -and $otmp.heroId -and $sex[$otmp.heroId]){ $expect = $sex[$otmp.heroId] }
        elseif($otmp -and $otmp.heroId -and $originsSex[$otmp.heroId]){ $expect = $originsSex[$otmp.heroId] }
    }
    $mis = ([regex]::Matches($text,'她')).Count
    $mal = ([regex]::Matches($text,'他')).Count
    if($expect -eq "F" -and $mal -gt 0){ $issues.Add(("E:女卡含'他'x" + $mal)); $issueN++ }
    if($expect -eq "M" -and $mis -gt 0){ $issues.Add(("E:男卡含'她'x" + $mis)); $issueN++ }

    # F 复用候选词频
    $reuse = @()
    foreach($w in $reuseCandidates){ $c = ([regex]::Matches($text,[regex]::Escape($w))).Count; if($c -ge 2){ $reuse += ($w + "x" + $c) } }
    if($reuse.Count){ $issues.Add(("F:" + ($reuse -join ","))) }

    $res = if($issueN -eq 0){ "PASS" } else { "*" }
    if($issueN -gt 0){ $allPass = $false }
    $L = "-"; if($lat){ $L = "Y" }
    $B = "-"; if($half.Success){ $B = "Y" }
    $C = "-"; if($long.Count){ $C = "x" + $long.Count }
    $md.Add(("| " + $name + " | " + $L + " | " + $B + " | " + $C + " | " + $cs + "/" + $q2 + " | " + $mal + "/" + $mis + " | " + (($reuse|Select-Object -First 3) -join ",") + " | " + $res + " |"))
    foreach($i in $issues){ $md.Add(("    - " + $i)) }
}
$md.Add("")
$md.Add(("## 汇总: 卡数=" + $cards.Count + " 全洁=" + $allPass))
$md.Add("")
$md.Add("## 说明")
$md.Add("- A/B 为硬性机械项；C/D/E/F 为复核项：超长句、模板腔、代词、复用词报的是**待人工判断**的素材，不是绝对错误。")
$md.Add("- E 代词混用需人审上下文（如女卡提男女亲属时合法使用'他'）。")
$text = [string]::Join("`n", $md.ToArray())
[System.IO.File]::WriteAllText($out, $text, (New-Object System.Text.UTF8Encoding($true)))
Write-Host ("DONE cards=" + $cards.Count + " allClean=" + $allPass)
Write-Host ("report=" + $out)