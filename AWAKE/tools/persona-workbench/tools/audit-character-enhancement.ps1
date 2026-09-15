# audit-character-enhancement.ps1  (V3 = 规范 v4「可靠版」)
# Persona 加强规范强门禁（内容级）：三张力轴 + 自由决策样本 + 条件化 + 破模版
#
# 定位：W6 四门禁之外的第五道「内容强校验」门禁。
#
# ── V3 修订（红队驱动，依据 docs/PERSONA-SPEC-RELIABILITY-CHARTER-20260913.md）──
# 红队 A1/A2/A3/A6/A8/A9 六项 BREAK 的整改：
#   · A1 自洽性：M3 禁绝对词、E2b 却必填绝对词 → 矛盾。改法=不再做「词级禁令」，
#        改为「底线词必须与条件词在窗口内共现」（无条件断言即违规）。矛盾消解。
#   · A2/R3 非枚举：E3 删裸单字（但/却/才），且不再独立判定，并入 E2b 的窗口共现。
#   · A3/R3 非枚举：E5 弃「动词族枚举」（实测 5 张卡换动词溜过），改判【结构】——
#        「问号 → 破折号 → 引号」问答框结构占样本 >=50% 即 FAIL。
#   · A8 落地：M5 的「处境」维度转为 O2 观测（不判生死，避免误伤「只在乎一件事」的合法角色）。
#   · A9 分离：原 M7 复读自检（判模型输出）移出本门禁，见 scenario8 观测脚本。
#   · A6/R6 边界：语义空洞/脱名可辨识不可机械化，移交盲评 Q1/Q2，本门禁不假装能测。
#
# 规则（V3 硬 FAIL）：
#   E1  写精不凑：selfClaimExamples 条数 3..8（下限防空；上限由 6 放宽至 8）；rules+rbs >= 3
#   E2  三轴声明：tensionAxes 三字段均非空；值可为 'none'（允许缺席，R5）
#   E2b 张力入运行时：物化丢弃 tensionAxes，故张力须落实进运行时字段。
#       判定 = (命中 >=1 底线词 或 显式声明 none) 且 该处与条件词在 70 字窗口内共现
#   E4  破例落点：contradictionDescription 非空（可写「无破例」声明）
#   E5  结构破模版：问答框结构占比 < 50%（结构判定，不依赖动词表）
# 告警（WARN，不判 FAIL）：
#   O2  处境覆盖：样本处境类 < 2 提示（合法性存疑者人工复核，不当 FAIL——M6 允许「只在乎一件事」）
#   O3  族属符号：单卡命中该王国族属符号 >= 4
#   O4  疑似纯台词：样本内引文占比 >= 0.7
#   O5  跨卡同句：core 10-gram（滑窗）或 examples 开头 8 字，同一串跨 >=3 张卡
#
# 用法：
#   .\audit-character-enhancement.ps1                        # 全量
#   .\audit-character-enhancement.ps1 -Cards 拉盖娅,那得娅    # 指定卡
#   .\audit-character-enhancement.ps1 -OutMd <path>
#   .\audit-character-enhancement.ps1 -CharRoot <dir>        # 指定卡目录（自测/回归用）
# 退出码：0 = 指定范围无 FAIL；1 = 有 FAIL

[CmdletBinding()]
param(
    [string[]]$Cards = @(),
    [string]$OutMd  = '',
    [string]$CharRoot = ''    # 注意：勿命名 $CharDir——PS 变量名不区分大小写，会与下面 $charDir 撞成同一变量
)
$ErrorActionPreference = 'Stop'
$expanded = New-Object System.Collections.ArrayList
foreach($c in $Cards){ foreach($part in ($c -split ',')){ $t=$part.Trim(); if($t){ [void]$expanded.Add($t) } } }
$Cards = @($expanded.ToArray())

$base    = 'D:\AWAKE-Dev\AWAKE'
$charDir = if ($CharRoot) { $CharRoot } else { Join-Path $base 'tools\persona-workbench\characters' }
if (-not $OutMd) { $OutMd = Join-Path $base 'docs\AUDIT-CHARACTER-ENHANCEMENT-20260913.md' }

$minExamples = 3
$maxExamples = 8
$minRulesBehaviors = 3
$window = 70

# E2b 底线词（V3 不再因「绝对」而禁用；改为要求共现条件，故保留绝不一族）
$hardlineWords = @('绝不','绝不让','绝不容','绝不退','不容','宁可','宁死','断然','寸步不让','一石不让','一步不让','留不得')
# 条件 / 让步词（V3 已删裸单字 但/却/才）
$condWords     = @('若','但凡','除非','一旦','只要','假如','万一','纵使','即便','容我','可以改','就算','宁可')
# 显式「无底线 / 无不可让」声明（允许缺席）
$noneMarks     = @('无底线','无所固守','无不可让','什么都可让','并不固守','无不可舍','无一物不可让')
# E5 结构框（两条结构信号之任一即算命中，不依赖动词枚举）：
#   (a) 问号 → 破折号 → 可选引号：「…？——“台词”」
#   (b) 「会/要 + 如何/怎样/怎么」型问答框：「拔该会如何劝解…」（去掉引号也躲不过）
$frameRe = '([？?]\s*[—\-]{1,2}\s*[“"「]?)|(会\s*(如何|怎样|怎么)|要\s*(如何|怎样|怎么))'

# O2 处境类（代理词表）
$situations = [ordered]@{
    '利益交换' = @('钱','财','价','货','买卖','付','金','银','买','卖','酬','礼')
    '索取求助' = @('求','请','讨','借','索取','求助','托','赏')
    '质询审问' = @('质问','凭什么','交代','问','盘','审','责问')
    '战争武力' = @('战','兵','杀','攻','守','血','阵','军','箭','刃')
    '结盟投靠' = @('盟','投','依附','效忠','归附','结盟','臣服')
    '亲族婚嫁' = @('婚','娶','嫁','妻','夫','子','女','兄弟','父','母','族')
    '生死殉难' = @('死','殉','葬','丧','命','赴死')
    '背叛告发' = @('叛','告','背弃','骗','出卖','反水')
    '日常生计' = @('田','收','市','食','粮','伤','病','冬','饥')
}
# O3 族属符号
$symbols = [ordered]@{
    'khuzait'  = @('草原','烈马','雕弓','游牧','篝火','帐篷','马背','牧场','骑手')
    'aserai'   = @('绿洲','商路','骆驼','椰枣','苏丹','水井','队商','沙丘','集市')
    'sturgia'  = @('冰原','战斧','圆盾','雪原','寒林','河流','木船','冻土')
    'battania' = @('森林','猎人','雾','巨石','山峦','德鲁伊','林间','苔')
    'vlandia'  = @('骑士','重甲','长剑','誓约','封臣','铁骑','纹章')
    'empire'   = @('元老','紫袍','公民','帝国','礼仪','名分','军团')
}

function Get-Situation([string]$text){
    $best = '其他'; $bn = 0
    foreach($k in $situations.Keys){
        $n = 0
        foreach($w in $situations[$k]){ if($text.Contains($w)){ $n++ } }
        if($n -gt $bn){ $best = $k; $bn = $n }
    }
    return $best
}
function Get-SymbolWords([string]$kingdom){
    $k = ([string]$kingdom).ToLower()
    foreach($key in @('khuzait','aserai','sturgia','battania','vlandia')){
        if($k.Contains($key)){ return $symbols[$key] }
    }
    if($k.Contains('empire')){ return $symbols['empire'] }
    return @()
}
function Test-WindowCooccur([string]$text,[string[]]$hard,[string[]]$cond,[int]$win){
    foreach($h in $hard){
        $idx = 0
        while($true){
            $p = $text.IndexOf($h, $idx)
            if($p -lt 0){ break }
            $lo = [Math]::Max(0, $p - $win)
            $len = [Math]::Min($text.Length - $lo, $h.Length + 2*$win)
            $seg = $text.Substring($lo, $len)
            foreach($c in $cond){ if($seg.Contains($c)){ return $true } }
            $idx = $p + $h.Length
        }
    }
    return $false
}

# ── O5 v2 预扫描：跨卡同句（WARN）────────────────────────────────────────────
# 通道 A：core 全文（去所有空白）的 10-gram 滑窗   ← 第一人称独白，作者腔最易沉淀处
# 通道 B：selfClaimExamples 每条开头 8 字（去空白、去前导「（」）
# 命中阈值：同一串出现在 >= 3 张不同卡。
# 折叠：按命中卡集（frozenset 等价物）分组，锚卡 = 组内 Ordinal 序最小的卡名，
#       在锚卡 core 里把相邻命中的 10-gram 合并成片段，取最长者（等价 o5_algo.py）。
# 为何不扫 identityFacts / 三个 description：那几处是第三人称设定摘要体，
#       本就应模板化（全库 22 张共用同一句），扫了全是误报。
$o5NCore    = 10
$o5NEx      = 8
$o5Min      = 3
$o5MaxCards = 6
$o5Ordinal  = [System.StringComparer]::Ordinal
$o5EdgeRe   = '^[，。、；：！？…—·,.!?;:（）()「」『』《》〈〉<>"''’‘“”]+|[，。、；：！？…—·,.!?;:（）()「」『』《》〈〉<>"''’‘“”]+$'

function ConvertTo-O5Norm([string]$s){ if($null -eq $s){ return '' } return ($s -replace '\s','') }
function ConvertTo-O5Trim([string]$s){ if($null -eq $s){ return '' } return ($s -replace $o5EdgeRe,'') }

function Get-O5Span([string]$text, $gramSet, [int]$n){
    $idx = New-Object System.Collections.Generic.List[int]
    for($i=0; $i -le $text.Length-$n; $i++){
        if($gramSet.Contains($text.Substring($i,$n))){ $idx.Add($i) }
    }
    if($idx.Count -eq 0){ return '' }
    $idx.Sort()
    $bestS=$idx[0]; $bestE=$idx[0]+$n; $cs=$idx[0]; $ce=$idx[0]+$n
    for($k=1; $k -lt $idx.Count; $k++){
        $p = $idx[$k]
        if($p -le $ce){ if(($p+$n) -gt $ce){ $ce = $p+$n } }
        else {
            if(($ce-$cs) -gt ($bestE-$bestS)){ $bestS=$cs; $bestE=$ce }
            $cs=$p; $ce=$p+$n
        }
    }
    if(($ce-$cs) -gt ($bestE-$bestS)){ $bestS=$cs; $bestE=$ce }
    return $text.Substring($bestS, $bestE-$bestS)
}

$o5Core     = @{}
$o5Ex       = @{}
$o5CoreText = @{}
foreach($pf in (Get-ChildItem -Path $charDir -Filter '*.persona.json')){
    $nm = (($pf.BaseName -split '_')[0])
    try {
        $pj = Get-Content $pf.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $core = ConvertTo-O5Norm ([string]$pj.core)
        $o5CoreText[$nm] = $core
        if($core.Length -ge $o5NCore){
            $seen = New-Object 'System.Collections.Generic.HashSet[string]'
            for($i=0; $i -le $core.Length - $o5NCore; $i++){ [void]$seen.Add($core.Substring($i,$o5NCore)) }
            foreach($g in $seen){
                if(-not $o5Core.ContainsKey($g)){ $o5Core[$g] = New-Object 'System.Collections.Generic.HashSet[string]' }
                [void]$o5Core[$g].Add($nm)
            }
        }
        foreach($pe in @($pj.selfClaimExamples)){
            $b = (ConvertTo-O5Norm ([string]$pe)).TrimStart('（')
            if($b.Length -ge $o5NEx){
                $k = $b.Substring(0,$o5NEx)
                if(-not $o5Ex.ContainsKey($k)){ $o5Ex[$k] = New-Object 'System.Collections.Generic.HashSet[string]' }
                [void]$o5Ex[$k].Add($nm)
            }
        }
    } catch {}
}

function Group-O5ByCardset($inv){
    $bySet = @{}
    foreach($g in $inv.Keys){
        $h = $inv[$g]
        if($h.Count -ge $o5Min){
            $cl = @($h); [Array]::Sort($cl, $o5Ordinal)
            $key = ($cl -join '|')
            if(-not $bySet.ContainsKey($key)){
                $bySet[$key] = [pscustomobject]@{ cards=$cl; grams=(New-Object 'System.Collections.Generic.HashSet[string]') }
            }
            [void]$bySet[$key].grams.Add($g)
        }
    }
    return $bySet
}
$o5CoreBySet = Group-O5ByCardset $o5Core
$o5ExBySet   = Group-O5ByCardset $o5Ex

$md = New-Object System.Collections.Generic.List[string]
$md.Add('# PERSONA ENHANCEMENT LINT — 加强规范强校验 V3（规范 v4 可靠版）')
$md.Add('')
$md.Add('> 范围：' + $(if($Cards.Count){ '指定卡：' + ($Cards -join '、') } else { '全量' }))
$md.Add('> 卡目录：' + $charDir)
$md.Add('> 硬 FAIL：E1 / E2 / E2b / E4 / E5（结构框）。告警：O2 处境 / O3 符号 / O4 纯台词 / O5 跨卡同句。')
$md.Add('> 本版依据：docs/PERSONA-SPEC-RELIABILITY-CHARTER-20260913.md（红队 A1/A2/A3/A6/A8/A9 整改）。')
$md.Add('')
$md.Add('| 卡 | 样本数 | E1 | E2三轴 | E2b张力 | E4破例 | E5结构框 | 结果 |')
$md.Add('|---|---|---|---|---|---|---|---|')

$allPass = $true
foreach($f in (Get-ChildItem $charDir -Filter '*.persona.json' | Sort-Object Name)){
    $baseName = $f.BaseName
    $zh = ($baseName -split '_')[0]
    if($Cards.Count -gt 0 -and -not ($Cards -contains $zh)){ continue }
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $errs = New-Object System.Collections.Generic.List[string]
    $warns = New-Object System.Collections.Generic.List[string]

    $exs   = @($j.selfClaimExamples)
    $rules = @($j.selfClaimRules)
    $rbs   = @($j.realSelfBehaviors)
    $contra= [string]$j.contradictionDescription
    $runtimeText = (($rules + $rbs + $exs + @($contra)) | ForEach-Object { [string]$_ }) -join ' '

    # E1
    if($exs.Count -lt $minExamples){ $errs.Add("E1:样本x$($exs.Count) < $minExamples") }
    if($exs.Count -gt $maxExamples){ $errs.Add("E1:样本x$($exs.Count) > $maxExamples") }
    if(($rules.Count + $rbs.Count) -lt $minRulesBehaviors){ $errs.Add("E1:rules+rbs=$($rules.Count+$rbs.Count) < $minRulesBehaviors") }

    # E2 三轴声明（值可为 none）
    $tAx = $j.tensionAxes
    $tOk = $false
    if($null -ne $tAx){
        $allNonEmpty = $true
        foreach($k in @('hardLine','negotiable','breachSwitch')){
            if([string]::IsNullOrWhiteSpace([string]$tAx.$k)){ $allNonEmpty = $false }
        }
        if($allNonEmpty){ $tOk = $true }
    }
    if(-not $tOk){ $errs.Add("E2:tensionAxes 缺失或三轴未尽填（可填 none，但须三键皆非空）") }
    elseif($null -ne $tAx){
        # V3：允许缺席，但不得三轴全 none（否则 none 成为「什么都不写」的逃逸口，红队 B4）
        $noneCnt = 0
        foreach($k in @('hardLine','negotiable','breachSwitch')){
            if((([string]$tAx.$k).Trim().ToLower()) -eq 'none'){ $noneCnt++ }
        }
        if($noneCnt -eq 3){ $errs.Add("E2:三轴不得全为 none（允许缺席，但人格须至少一轴有实质）") }
    }

    # E2b 底线 ∩ 条件（窗口共现），或显式 none
    $hasHard = $false
    foreach($w in $hardlineWords){ if($runtimeText.Contains($w)){ $hasHard = $true; break } }
    $hasNone = $false
    foreach($w in $noneMarks){ if($runtimeText.Contains($w)){ $hasNone = $true; break } }
    $hasCond = $false
    foreach($w in $condWords){ if($runtimeText.Contains($w)){ $hasCond = $true; break } }
    $bound = Test-WindowCooccur $runtimeText $hardlineWords $condWords $window
    $noneBound = Test-WindowCooccur $runtimeText $noneMarks $condWords $window
    $e2bOk = ($hasNone -and $noneBound) -or ($hasHard -and $bound)
    if(-not $e2bOk){
        $why = @()
        if(-not ($hasHard -or $hasNone)){ $why += '无底线词且未声明 none' }
        if(-not $hasCond){ $why += '无条件/让步词' }
        elseif(-not ($bound -or $noneBound)){ $why += '底线未与条件在 ' + $window + ' 字窗口内共现（疑似无条件绝对断言）' }
        $errs.Add("E2b:张力未入运行时（" + ($why -join '；') + "）")
    }

    # E4 破例落点
    if([string]::IsNullOrWhiteSpace($contra)){ $errs.Add("E4:contradictionDescription 为空（缺破例落点，可写「无破例」）") }

    # E5 结构框占比（结构判定，替代动词枚举）
    $frameHits = 0
    foreach($e in $exs){ if(([string]$e) -match $frameRe){ $frameHits++ } }
    if($exs.Count -ge 2 -and ($frameHits / $exs.Count) -ge 0.5){
        $errs.Add("E5:问答框结构占样本 $frameHits/$($exs.Count)（>=50%），未脱模版")
    }
    # E5b：模板可搬家——rules 里同样不得成片套用问答框结构（v4.1 收口 R5-C4）
    $ruleFrames = 0
    foreach($r in $rules){ if(([string]$r) -match $frameRe){ $ruleFrames++ } }
    if($rules.Count -ge 2 -and ($ruleFrames / $rules.Count) -ge 0.5){
        $errs.Add("E5:问答框结构占 rules $ruleFrames/$($rules.Count)（模板搬家到 rules）")
    }

    # O2 处境覆盖
    $sitSet = @{}
    foreach($e in $exs){ $s = Get-Situation ([string]$e); if($s -ne '其他'){ $sitSet[$s] = 1 } }
    if($exs.Count -ge 2 -and $sitSet.Count -lt 2){ $warns.Add('O2:样本处境类<2（提示，非 FAIL）') }

    # O3 族属符号
    $of = $f.FullName -replace '\.persona\.json$', '.origins.json'
    $kingdom = ''
    if(Test-Path $of){ try { $kingdom = [string](Get-Content $of -Raw -Encoding UTF8 | ConvertFrom-Json).kingdomId } catch {} }
    $sym = Get-SymbolWords $kingdom
    $symHits = 0
    foreach($w in $sym){ if($runtimeText.Contains($w)){ $symHits++ } }
    if($symHits -ge 4){ $warns.Add("O3:命中族属符号 $symHits 个（提示）") }

    # O4 疑似纯台词
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
    if($qRateMax -ge 0.7){ $warns.Add('O4:疑似纯台词样本(引文占比 ' + [Math]::Round($qRateMax,2) + ')') }

    # O5 跨卡同句（v2：core 10-gram 滑窗 + examples 开头 8 字）
    $o5Items = New-Object System.Collections.Generic.List[object]
    foreach($key in $o5CoreBySet.Keys){
        $grp = $o5CoreBySet[$key]
        if(-not ($grp.cards -contains $zh)){ continue }
        $anchor = $grp.cards[0]
        $atext  = if($o5CoreText.ContainsKey($anchor)){ $o5CoreText[$anchor] } else { '' }
        $span   = if($atext){ ConvertTo-O5Trim (Get-O5Span $atext $grp.grams $o5NCore) } else { '' }
        if(-not $span){ $span = (@($grp.grams) | Sort-Object -CaseSensitive)[0] }
        $cl     = $grp.cards
        $shown  = (@($cl | Select-Object -First $o5MaxCards) -join '、') + $(if($cl.Count -gt $o5MaxCards){'…'}else{''})
        $msg    = 'O5:core 与其它卡同句「' + $span + '」（跨 ' + $cl.Count + ' 张：' + $shown + '）'
        $o5Items.Add([pscustomobject]@{ n=$cl.Count; ch='core'; span=$span; msg=$msg })
    }
    foreach($key in $o5ExBySet.Keys){
        $grp = $o5ExBySet[$key]
        if(-not ($grp.cards -contains $zh)){ continue }
        $cl    = $grp.cards
        $shown = (@($cl | Select-Object -First $o5MaxCards) -join '、') + $(if($cl.Count -gt $o5MaxCards){'…'}else{''})
        $msg   = 'O5:examples 开头雷同「' + $key + '」（跨 ' + $cl.Count + ' 张：' + $shown + '）'
        $o5Items.Add([pscustomobject]@{ n=$cl.Count; ch='examples'; span=$key; msg=$msg })
    }
    foreach($it in ($o5Items | Sort-Object @{Expression={$_.n};Descending=$true}, @{Expression={$_.ch}}, @{Expression={$_.span}})){
        $warns.Add($it.msg)
    }

    $res = if($errs.Count -eq 0){ 'PASS' } else { 'FAIL('+$errs.Count+')' }
    if($errs.Count -gt 0){ $allPass = $false }
    $md.Add(("| $zh | $($exs.Count) | " +
        $(if($exs.Count -ge $minExamples -and $exs.Count -le $maxExamples){'Y'}else{'-'}) + " | " +
        $(if($tOk){'Y'}else{'-'}) + " | " +
        $(if($e2bOk){'Y'}else{'-'}) + " | " +
        $(if([string]::IsNullOrWhiteSpace($contra)){'-'}else{'Y'}) + " | " +
        $(if($exs.Count -ge 2 -and ($frameHits / $exs.Count) -ge 0.5){'Y'}else{'-'}) + " | $res |"))
    foreach($e in $errs){ $md.Add(("  - "+$e)) }
    foreach($w in $warns){ $md.Add(("  - "+$w)) }
}
$md.Add('')
$md.Add('## 汇总: ' + $(if($Cards.Count){'范围卡已核对'}else{'全量'}) + '  全部通过=' + $allPass)
$md.Add('')
$md.Add('## 判定说明')
$md.Add('- 硬 FAIL：E1 条数 / E2 三轴声明 / E2b 张力入运行时（窗口共现）/ E4 破例落点 / E5 结构框占比。')
$md.Add('- E5 为结构判定（问号→破折号→引号），不依赖动词表——红队实测动词枚举有 5 张卡漏网。')
$md.Add('- O2/O3/O4/O5 为观测告警，不作生死：语义级反脸谱（脱名可辨识）交盲评 Q1/Q2，本门禁不假装能测。')
$md.Add('- O5 = 跨卡同句（v2）：core 全文 10-gram 滑窗 + examples 开头 8 字，同一串跨 >=3 张卡即告警，')
$md.Add('  按命中卡集折成片段（锚卡=组内 Ordinal 最小者）。只覆盖 core 与 examples；identityFacts 与')
$md.Add('  三个 description 属第三人称设定摘要体、本就应同构，不在扫描范围（否则全是误报）。')

$text = [string]::Join("`n", $md.ToArray())
[System.IO.File]::WriteAllText($OutMd, $text, (New-Object System.Text.UTF8Encoding($true)))
Write-Host ("DONE allPass=" + $allPass)
Write-Host ("report=" + $OutMd)
if(-not $allPass){ exit 1 }
