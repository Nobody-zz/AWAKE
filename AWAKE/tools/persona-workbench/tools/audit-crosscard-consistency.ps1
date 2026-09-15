# 跨卡一致性 / 专名覆盖审计（2026-09-12）
# 基准 persona-names-zh-en.tsv：核对 46 卡正文实际使用的氏族/人名/专名是否都被权威表覆盖。
#  A) 每卡采用哪些官方氏族/人名（确认官方名被采用）
#  B) 疑似未收录专名候选：先剥离全部权威名，再抓"X氏族/X部族/X族长"，供人工核。
$ErrorActionPreference = "Stop"
$base    = "D:\AWAKE-Dev\AWAKE"
$charDir = Join-Path $base "tools\persona-workbench\characters"
$mapping = Join-Path $base "docs\mappings\persona-names-zh-en.tsv"
$out     = Join-Path $base "docs\AUDIT-CROSSCARD-20260912.md"

$zh2en   = @{}
$zhType  = @{}
foreach($l in (Get-Content $mapping -Encoding UTF8)){
    if(-not $l -or $l -notmatch "`t"){ continue }
    $c = $l -split "`t"
    if($c.Length -ge 4 -and $c[2] -and $c[3]){
        if(-not $zh2en.ContainsKey($c[3])){ $zh2en[$c[3]] = $c[2] }
        if(-not $zhType.ContainsKey($c[3])){ $zhType[$c[3]] = $c[1] }
    }
}
$clanZh = @($zhType.GetEnumerator() | Where-Object { $_.Value -eq "clan" } | ForEach-Object { $_.Key })
$heroZh = @($zhType.GetEnumerator() | Where-Object { $_.Value -eq "hero" } | ForEach-Object { $_.Key })
$allZhS = $zh2en.Keys

function Get-CardText($j){
    $parts = New-Object System.Collections.Generic.List[string]
    foreach($p in $j.PSObject.Properties.Name){
        $v = $j.$p
        if($v -is [string]){ $parts.Add($v) }
        elseif($v -is [System.Collections.IEnumerable]){
            foreach($e in $v){ if($e -is [string]){ $parts.Add($e) } }
        }
    }
    return ($parts -join " ")
}

$cards = Get-ChildItem $charDir -Filter "*.persona.json"
$md = New-Object System.Collections.Generic.List[string]
$md.Add("# 跨卡一致性 / 专名覆盖审计（2026-09-12）")
$md.Add("")
$md.Add(("基准：docs/mappings/persona-names-zh-en.tsv（hero {0} 个 / clan {1} 个）" -f $heroZh.Count, $clanZh.Count))

# A
$md.Add("")
$md.Add("## A. 各卡采用的官方氏族 / 人名")
$md.Add("| 卡 | 官方氏族名 | 官方人名 |")
$md.Add("|---|---|---|")
foreach($f in $cards){
    $j  = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $txt = Get-CardText $j
    $usedClan = @($clanZh | Where-Object { $txt -match [regex]::Escape($_) })
    $usedHero = @($heroZh | Where-Object { $txt -match [regex]::Escape($_) })
    $md.Add(("| " + $f.BaseName + " | " + (($usedClan | Sort-Object -Unique) -join "／") + " | " + (($usedHero | Sort-Object -Unique) -join "／") + " |"))
}

# B
$md.Add("")
$md.Add("## B. 疑似未收录专名候选（正文有、权威表无，供人工核）")
$stop = @("一位","这个","那个","自家","整个","王室","王族","统治","豪门","老牌","本族","本部","所在","王权","大礼","世袭","王国内","他族")
$foundAny = $false
$pat = '([\u4e00-\u9fff]{1,4})(?:氏族|部族|族长|部长的|一部|世家)'
foreach($f in $cards){
    $j   = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $txt = Get-CardText $j
    $masked = $txt
    foreach($zh in $allZhS){ $masked = $masked.Replace($zh, "") }
    $hit = @{}
    foreach($mt in [regex]::Matches($masked,$pat)){
        $pre = $mt.Groups[1].Value
        if($pre.Length -lt 2 -or ($stop -contains $pre)){ continue }
        $known = $false
        foreach($zh in $allZhS){
            if($pre.StartsWith($zh) -or $zh.StartsWith($pre)){ $known = $true; break }
        }
        if($known){ continue }
        $hit[$pre] = $mt.Value
    }
    if($hit.Count){
        $foundAny = $true
        foreach($k in ($hit.Keys | Sort-Object)){ $md.Add(("- 候选 [**" + $k + "**]（" + $f.BaseName + "） 上下文:「" + $hit[$k] + "」")) }
    }
}
if(-not $foundAny){ $md.Add("（无候选：权威表已覆盖全部正文氏族/人名专名）") }

[System.IO.File]::WriteAllText($out, ([string]::Join("`n", $md.ToArray())), (New-Object System.Text.UTF8Encoding($true)))
Write-Host ("DONE cards=" + $cards.Count + "  report=" + $out)