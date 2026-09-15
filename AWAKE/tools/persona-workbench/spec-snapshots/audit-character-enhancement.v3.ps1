# audit-character-enhancement.ps1  (V2)
# Persona 加强规范强门禁（内容级）：三张力轴 + 自由决策样本 + 条件化 + 破模版
#
# 定位：W6 四门禁之外的第五道「内容强校验」门禁。
# V2 弃用的旧做法：8 类决策结果 + 锚词穷举 + 统一「如何回应…」问答框 —— 那套会被当成
#       模板套用到全量卡，造成新的脸谱化（接受=点头/拒绝=摇头…）。
# V2 的思路：不查表，给模型三根「张力轴」让他即兴；selfClaimExamples 改成自由叙事片段，
#       模型吸收的是它的价值排序，而不是背一段现成对白（断开小模型整句复刻的回路）。
#
# 规则（V2，每条可机械化判定）：
#   E1  写精不凑：selfClaimExamples 条数 3..6（下限防空、上限防堆砌）；rules+rbs 合计 >= 3
#   E2  三轴齐备：tensionAxes 存在且 hardLine/negotiable/breachSwitch 三字段均非空（结构）
#   E2b 张力入运行时：物化会丢弃 tensionAxes，故其张力必须落实进运行时字段
#       （selfClaimRules+realSelfBehaviors+selfClaimExamples+contradictionDescription），
#       判定 = 该文本同时命中 >=1 硬底线词 与 >=1 可让/条件词
#   E3  条件化去铁板：运行时文本命中 >=1 条件词（若/但凡/除非/一旦…）
#   E4  破例落点：contradictionDescription 非空（承载「哪根弦会被现实压力拨断」）
#   E5  异构破模版：selfClaimExamples 不得套用统一问答框。V3 抓「(如何|怎样|怎么)+动词」动词族，
#       占比 >= 50% 即 FAIL（旧版只认『如何回应』四字全等，换成"如何评价/怎样"即可绕过——实测 43/76 卡如此溜过）；
#       -StrictExact 时再要求 >=1 条为纯散文/决断叙事（体内无收尾引号『」』）
#   M1W 疑似金句（WARN，不判 FAIL）：任一样本内引文占比 >= 0.7，提示"基本是台词、缺自主叙事"
#
# 用法：
#   .\audit-character-enhancement.ps1                           # 全量
#   .\audit-character-enhancement.ps1 -Cards 拉盖娅,那得娅      # 指定卡
#   .\audit-character-enhancement.ps1 -StrictExact              # 追加「自由散文」门槛
#
# 退出码：0 = 指定范围全通过；1 = 有 FAIL

[CmdletBinding()]
param(
    [string[]]$Cards = @(),
    [switch]$StrictExact,
    [string]$OutMd  = ''
)
$ErrorActionPreference = 'Stop'
# -File 传参不会把 "A,B" 自动拆数组，手动按逗号展开
$expanded = New-Object System.Collections.ArrayList
foreach($c in $Cards){ foreach($part in ($c -split ',')){ $t=$part.Trim(); if($t){ [void]$expanded.Add($t) } } }
$Cards = @($expanded.ToArray())

$base    = 'D:\AWAKE-Dev\AWAKE'
$charDir = Join-Path $base 'tools\persona-workbench\characters'
if (-not $OutMd) { $OutMd = Join-Path $base 'docs\AUDIT-CHARACTER-ENHANCEMENT-20260913.md' }

# 条数窗口（写精不凑）
$minExamples = 3
$maxExamples = 6
$minRulesBehaviors = 3

# E2b/E3 词表
$hardlineWords = @('绝不','绝不让','不容','绝不容','宁可','宁死','断然','寸步不让','一石不让','留不得','绝不退')
$condWords     = @('若','但凡','除非','一旦','只要','假如','万一','但','却','才','容我','可以改')

$md = New-Object System.Collections.Generic.List[string]
$md.Add('# PERSONA ENHANCEMENT LINT - 加强规范强校验 V2（三张力轴 / 自由样本 / 条件化 / 破模版）')
$md.Add('')
$md.Add('> 范围：' + $(if($Cards.Count){ '指定卡：' + ($Cards -join '、') } else { '全量' }))
$md.Add('> 判定：E1/E2/E2b/E3/E4/E5 硬 FAIL；V2 弃用 8 类锚词与统一问答框。' + $(if($StrictExact){'（strict：要求>=1条纯散文片段）'}else{''}))
$md.Add('')
$md.Add('| 卡 | 样本数 | E1条数 | E2三轴 | E2b入运行时 | E3条件化 | E4破例 | E5模版 | 结果 |')
$md.Add('|---|---|---|---|---|---|---|---|---|')

$selNames = @{}
foreach($f in Get-ChildItem $charDir -Filter '*.persona.json'){
    $selNames[($f.BaseName -split '_')[0]] = $f.BaseName
}

function Test-Match([string]$text, [string[]]$keys){
    foreach($k in $keys){ if($text.Contains($k)){ return $true } }
    return $false
}
function Test-HitCount([string]$text, [string[]]$keys){
    foreach($k in $keys){ if($text.Contains($k)){ return 1 } }
    return 0
}

$allPass = $true
$targets = Get-ChildItem $charDir -Filter '*.persona.json' | Sort-Object Name
foreach($f in $targets){
    $baseName = $f.BaseName
    $zh = ($baseName -split '_')[0]
    if($Cards.Count -gt 0 -and -not ($Cards -contains $zh)){ continue }
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $errs = New-Object System.Collections.Generic.List[string]

    $exs   = @($j.selfClaimExamples)
    $rules = @($j.selfClaimRules)
    $rbs   = @($j.realSelfBehaviors)
    $contra= [string]$j.contradictionDescription
    $runtimeText = ($rules + $rbs + $exs + @($contra)) -join ' '

    # E1 写精不凑
    if($exs.Count -lt $minExamples){ $errs.Add("E1:样本x$($exs.Count) < $minExamples") }
    if($exs.Count -gt $maxExamples){ $errs.Add("E1:样本x$($exs.Count) > $maxExamples（写精不凑）") }
    if(($rules.Count + $rbs.Count) -lt $minRulesBehaviors){ $errs.Add("E1:rules+rbs=$($rules.Count+$rbs.Count) < $minRulesBehaviors") }

    # E2 三轴齐备（结构）
    $tAx = $j.tensionAxes
    $tOk = $false
    if($null -ne $tAx){
        $allNonEmpty = $true
        foreach($k in @('hardLine','negotiable','breachSwitch')){
            if([string]::IsNullOrWhiteSpace([string]$tAx.$k)){ $allNonEmpty = $false }
        }
        if($allNonEmpty){ $tOk = $true }
    }
    if(-not $tOk){ $errs.Add("E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填") }

    # E2b 张力入运行时（硬底线 ∩ 可让条件）
    $hasHard = Test-HitCount $runtimeText $hardlineWords
    $hasCond = Test-HitCount $runtimeText $condWords
    $e2bOk = ($hasHard -eq 1 -and $hasCond -eq 1)
    if(-not $e2bOk){
        $why = @()
        if($hasHard -eq 0){ $why += '无硬底线词' }
        if($hasCond -eq 0){ $why += '无可让/条件词' }
        $errs.Add("E2b:运行时缺张力（" + ($why -join '；') + "）")
    }

    # E3 条件化去铁板
    if($hasCond -eq 0){ $errs.Add("E3:运行时无条件化信号词（若/但凡/除非/一旦…）") }

    # E4 破例落点
    if([string]::IsNullOrWhiteSpace($contra)){ $errs.Add("E4:contradictionDescription 为空（缺破例落点）") }

    # E5 异构破模版（V3：问答框动词族 + 占比判定）
    # 旧版只认『如何回应』四字全等，换"如何评价/如何看待/怎样"即绕过（实测 43/76 卡如此溜过）。
    # 新版抓「(如何|怎样|怎么)+ 动词」的问答框，并按占比判 FAIL，堵住换动词的绕过路径。
    $qaFrameRe = '(如何|怎样|怎么)\s*(回应|应对|面对|看待|评价|处理|答复|回答|表态|反驳|拒绝|答应|选择|取舍|看|想)'
    $qaHits = @($exs | Where-Object { [string]$_ -match $qaFrameRe }).Count
    $frameHits = $qaHits
    if($exs.Count -ge 2 -and $qaHits -eq $exs.Count){ $errs.Add("E5:全部样本套用统一问答框（$qaHits/$($exs.Count)），未脱模版") }
    elseif($exs.Count -ge 2 -and ($qaHits / $exs.Count) -ge 0.5){ $errs.Add("E5:过半样本套用问答框（$qaHits/$($exs.Count)），未脱模版") }
    $starts = @{}
    foreach($e in $exs){
        $b = ([string]$e).Trim().TrimStart('（')
        if($b.Length -ge 8){ $s = $b.Substring(0,8); if($starts.ContainsKey($s)){$starts[$s]++}else{$starts[$s]=1} }
    }
    $dupes = @($starts.GetEnumerator() | Where-Object { $_.Value -ge 2 })
    $modelWarn = ''
    if($dupes.Count -gt 0){ $modelWarn = ' E5WARN:开头雷同 ' + ($dupes.Name -join '/') }
    if($exs.Count -ge 2 -and $qaHits -gt 0 -and ($qaHits / $exs.Count) -lt 0.5){ $modelWarn += (' E5WARN:含问答框 ' + $qaHits + '/' + $exs.Count) }
    # M1 疑似纯台词样本（WARN，仅提示不改判定）：样本内引文占比 >= 0.7 视为"基本是台词、缺自主叙事"
    $qRateMax = 0.0
    foreach($e in $exs){
        $s = [string]$e
        $qm = [regex]::Matches($s, '[\u2018\u201c\u300c]([^\u2019\u201d\u300d]{2,40})[\u2019\u201d\u300d]')
        if($qm.Count -gt 0){
            $qt = (($qm | ForEach-Object { $_.Groups[1].Value }) -join '')
            $r = [double]$qt.Length / [Math]::Max(1, $s.Length)
            if($r -gt $qRateMax){ $qRateMax = $r }
        }
    }
    if($qRateMax -ge 0.7){ $modelWarn += (' M1WARN:疑似纯台词样本(引文占比 ' + [Math]::Round($qRateMax,2) + ')') }
    if($StrictExact){
        $proseHits = @($exs | Where-Object { -not $_.Contains('」') }).Count
        if($proseHits -lt 1){ $errs.Add("E5:strict 需 >=1 条纯散文/决断叙事（体内无收尾引号）") }
    }

    $res = if($errs.Count -eq 0){ 'PASS' } else { 'FAIL('+$errs.Count+')' }
    if($errs.Count -gt 0){ $allPass = $false }
    $md.Add(("| $zh | $($exs.Count) | " + $(if($exs.Count -ge $minExamples -and $exs.Count -le $maxExamples){'Y'}else{'-'}) + " | " + $(if($tOk){'Y'}else{'-'}) + " | " + $(if($e2bOk){'Y'}else{'-'}) + " | " + $(if($hasCond){'Y'}else{'-'}) + " | " + $(if([string]::IsNullOrWhiteSpace($contra)){'-'}else{'Y'}) + " | " + $(if($exs.Count -ge 2 -and ($qaHits / $exs.Count) -ge 0.5){'Y'}else{'-'}) + " | $res |"))
    foreach($e in $errs){ $md.Add(("  - "+$e)) }
    if($modelWarn){ $md.Add(("  - "+$modelWarn)) }
}
$md.Add('')
$md.Add(('## 汇总: ' + $(if($Cards.Count){'范围卡已核对'}else{'全量'}) + '  全部通过=' + $allPass))
$md.Add('')
$md.Add('## 判定说明')
$md.Add('- E1/E2/E2b/E3/E4/E5 为硬 FAIL。E2b 是本门禁的关键：tensionAxes 只是作者侧清单，物化会丢弃它，故张力必须落实进运行时字段才算数。')
$md.Add('- V2 不查 8 类、不凑锚词；模板化的「如何回应…」问答框直接被 E5 记为 FAIL。')
$md.Add('- 用 -StrictExact 追加「>=1 条纯散文/决断叙事」的门槛，进一步断开背诵台词回路。')

$text = [string]::Join("`n", $md.ToArray())
[System.IO.File]::WriteAllText($OutMd, $text, (New-Object System.Text.UTF8Encoding($true)))
Write-Host ("DONE allPass=" + $allPass)
Write-Host ("report=" + $OutMd)
if(-not $allPass){ exit 1 }