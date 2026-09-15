# 归属与译名稽查：对 characters/*.persona.json 全量审计
$ErrorActionPreference = "Stop"
$base = "D:\AWAKE-Dev\AWAKE"
$charDir = Join-Path $base "tools\persona-workbench\characters"
$mappings = Join-Path $base "docs\mappings"
$out  = Join-Path $base "docs\AUDIT-PERSONA-AFFILIATIONS-20260911.md"

# ---------- 1) 数据加载 ----------
$charMap = @{}
foreach($l in @(Get-Content (Join-Path $mappings "character-names-zh-en.tsv") -Encoding UTF8)){
    if(-not $l -or $l -notmatch "`t"){ continue }
    $c = $l -split "`t"
    if($c[0] -and $c[0] -ne "hero_id"){ $charMap[$c[0]] = $c }
}
$perEn2Zh = @{}
foreach($l in @(Get-Content (Join-Path $mappings "persona-names-zh-en.tsv") -Encoding UTF8)){
    if(-not $l -or $l -notmatch "`t"){ continue }
    $c = $l -split "`t"
    if($c[2] -and $c[3]){ $perEn2Zh[$c[2]] = $c[3] }
}
$kingOwner = @{}
foreach($l in @(Get-Content (Join-Path $mappings "kingdom-names-zh-en.tsv") -Encoding UTF8)){
    if(-not $l -or $l -notmatch "`t"){ continue }
    $c = $l -split "`t"
    if($c.Length -ge 6 -and $c[5] -and $c[5] -ne "-"){ $kingOwner[$c[5]] = $c[0] }
}

# ---------- 2) ruler 判定表（kingdom-names 的 owner_hero → kingdom_id） ----------
$kingCn = @{ "empire"="北帝国";"empire_w"="西帝国";"empire_s"="南帝国";"sturgia"="斯特吉亚";"aserai"="阿塞莱";"vlandia"="瓦兰迪亚";"battania"="巴旦尼亚";"khuzait"="库赛特" }

# ---------- 3) 全量遍历审计：从 .origins.json 侧车读 heroId/kingdomId ----------
$md = New-Object System.Collections.Generic.List[string]
$md.Add("# AUDIT - PersonaWorkbench 归属与译名稽查（全量，origin sidecar）")
$md.Add('| 卡 | heroId | 官方EN | 官方中文 | 卡displayName | 译名 | 王国段 | 游戏内归属 |')
$md.Add('|---|---|---|---|---|---|---|---|')
$fail = 0; $rows = 0; $noSide = 0
$cards = Get-ChildItem $charDir -Filter "*.persona.json" | Sort-Object Name
foreach($f in $cards){
    $card = $f.Name -replace '\.persona\.json$',''   # 中英并用名，如 温吉德_unqin_aserai
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    # heroId/kingdomId 优先取 .origins.json 侧车
    $sidePath = Join-Path $charDir ($card + ".origins.json")
    $hero = ''; $kSeg = ''; $srcFlag = '(no-sidecar)'
    if(Test-Path -LiteralPath $sidePath -PathType Leaf){
        $side = Get-Content -LiteralPath $sidePath -Raw -Encoding UTF8 | ConvertFrom-Json
        if($side.heroId){ $hero = [string]$side.heroId; $srcFlag = '(sidecar)' }
        if($side.kingdomId){ $kSeg = [string]$side.kingdomId }
    }
    # 兜底：卡内嵌 origins（兼容性保留）
    if(-not $hero -and $j.PSObject.Properties.Name -contains 'origins' -and $j.origins -and $j.origins.heroId){
        $hero = [string]$j.origins.heroId
        if(-not $kSeg -and $j.origins.kingdomId){ $kSeg = [string]$j.origins.kingdomId }
        $srcFlag = '(origins-embedded)'
    }
    if(-not $hero){ $md.Add(("| " + $card + " | !! 缺 heroId（无 sidecar 且无内嵌 origins） |")); $noSide++; continue }
    $en = "-"; $zh = "-"; $srcZh = "-"
    if($charMap.ContainsKey($hero)){
        $c = $charMap[$hero]
        $en = $c[1]; $zh = $c[2]
        if($zh -match 'no-fixed-zh'){ $zh = "(生成名)"; $srcZh = "std_lords无覆盖" } else { $srcZh = "std_lords" }
    }
    if($zh -eq "(生成名)" -and $perEn2Zh.ContainsKey($en)){ $zh = $perEn2Zh[$en]; $srcZh = "persona-names(正文)" }
    if($zh -eq $j.displayName){ $nameOk = "==" } else { $nameOk = "XX 卡=" + $j.displayName + " 官方=" + $zh; $fail++ }
    $gameK = $null
    if($kingOwner.ContainsKey($hero)){ $gameK = $kingOwner[$hero] }
    if($gameK){
        $kRes = $kingCn[$gameK] + " [ruler]"
        if($kSeg -and $gameK -ne $kSeg){ $kRes += " !! 与王国段 $kSeg 冲突"; $fail++ }
    } else {
        if($kSeg -and $kingCn.ContainsKey($kSeg)){ $kRes = "非ruler家族(" + $kingCn[$kSeg] + "内)，归属按王国段" }
        else{ $kRes = "非ruler / 王国段未知(" + $kSeg + ")" }
    }
    $md.Add(("| " + $card + $srcFlag + " | " + $hero + " | " + $en + " | " + $zh + "(" + $srcZh + ") | " + $j.displayName + " | " + $nameOk + " | " + $kSeg + " | " + $kRes + " |"))
    $rows++
}
$md.Add('')
$md.Add('## 结论')
$md.Add('')
$md.Add(("- 审计卡数：" + $rows + "（全量 personalities）；无 sidecar/内嵌 origins 无法定位 hero 计 " + $noSide + "。"))
$md.Add(("- 译名/归属不一致（含冲突）计 " + $fail + " 处，见上表逐行。"))
$md.Add('- heroId/kingdomId 来源：.origins.json 侧车（sidecar），卡 JSON 不内嵌；ruler 判定按 kingdom-names 的 owner_hero。')
$md | Set-Content -Path $out -Encoding UTF8
Write-Host ("DONE rows=" + $rows + " issues=" + $fail + " noSide=" + $noSide)
Write-Host ("report=" + $out)