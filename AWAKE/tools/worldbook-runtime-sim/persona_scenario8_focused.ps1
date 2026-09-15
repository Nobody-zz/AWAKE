[CmdletBinding()]
param(
    [string]$Model = 'qwen2.5:latest',
    [string]$Out = (Join-Path ([System.IO.Path]::GetTempPath()) 'awake-persona-scenario8.json'),
    [string]$CharactersDir = 'D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters',
    # 指定卡（中文名或卡文件名基名，逗号分隔）；留空 = 默认拉盖娅+那得娅两张基准卡
    [string[]]$Cards = @(),
    # 复读判定阈值：10 字滑窗落在卡内样本的比例 >= 此值即记复读（吸收 mid-adapt，默认 0.30）
    [double]$OverlapThreshold = 0.30
)

# 聚焦单卡（或多张指定卡）的 8 场景人物口吻测试。
# 与 MONCHUG-SCENARIO-8 同构：真实 persona DSL（force-approved 物化）+ 真实 NpcPromptTemplate + 本机 Ollama。
# 校验项：anyReplyRepeatedSlogan —— 回答是否整句/长串复读 selfClaimExamples（>=10 字的逐字窗口命中即记为复读）。
# 不写回源码；候选卡在临时目录物化，零污染。

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $root 'tools\worldbook-runtime-sim\WorldbookRuntimeSim.csproj'
$materializer = Join-Path $root 'tools\persona-workbench\tools\materialize-definitions.ps1'
$registry = Join-Path $root 'ModuleData\Worldbook\persona_definitions\tag_registry.json'
$templatePath = Join-Path $root 'src\Prompts\NpcPromptTemplate.cs'
$ollama = 'http://127.0.0.1:11434/api/chat'

# 目标卡：默认两张基准卡（拉盖娅 lord_1_14 / 那得娅 lord_1_34）。
# 传 -Cards 时按中文名或卡文件名匹配，heroId 一律从同基名 .origins.json 读（唯一 ID 源，不手写）。
if ($Cards.Count -gt 0) {
    $expanded = New-Object System.Collections.ArrayList
    foreach ($c in $Cards) { foreach ($part in ($c -split ',')) { $t = $part.Trim(); if ($t) { [void]$expanded.Add($t) } } }
    $Targets = @()
    foreach ($nm in $expanded) {
        if ($nm -like '*.persona.json') { $cardFile = $nm }
        else {
            $hit = Get-ChildItem -LiteralPath $CharactersDir -Filter '*.persona.json' |
                Where-Object { $_.Name -eq ($nm + '.persona.json') -or $_.BaseName.Split('_')[0] -eq $nm } |
                Select-Object -First 1
            if (-not $hit) { throw "未找到卡：$nm" }
            $cardFile = $hit.Name
        }
        $oBase = $cardFile -replace '\.persona\.json$', ''
        $oSide = Join-Path $CharactersDir ($oBase + '.origins.json')
        if (-not (Test-Path -LiteralPath $oSide)) { throw "缺 origins：$oSide" }
        $oJson = Get-Content -LiteralPath $oSide -Raw -Encoding UTF8 | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace([string]$oJson.heroId)) { throw "origins 无 heroId：$oSide" }
        $Targets += [pscustomobject]@{ CardFile = $cardFile; HeroId = [string]$oJson.heroId }
    }
} else {
    $Targets = @(
        [pscustomobject]@{ CardFile = '拉盖娅_rhagaea_pethros_empire_s.persona.json'; HeroId = 'lord_1_14' },
        [pscustomobject]@{ CardFile = '那得娅_nadea_comnos_empire_w.persona.json'; HeroId = 'lord_1_34' }
    )
}

# 通用 8 场景（不绑定库赛特；贵族/王廷中立设定，两卡通吃）
$Scenarios = @(
    @{ Id = '1_普通闲聊'; identity = '只身赶路、路过此地的异邦行商'; scene = '治所厅堂，日影西斜。'; turn = '我打南边来，听说这一带的秩序乱得厉害。您这儿可还安稳？' },
    @{ Id = '2_利益交换'; identity = '手握一支骑甲的地方豪强'; scene = '会客偏厅，只有两名随从。'; turn = '我带来一千第纳尔的诚意和两百副甲，想换您这边一个说得响的名号，往后我的人马在您地界上走动，不受盘查。' },
    @{ Id = '3_玩家履约'; identity = '同你立过血契的盟友'; scene = '战事方歇，营地一角。'; turn = '约定的粮道我守住了，侧翼也没让人摸上来。敢问这一仗打完，我在这儿的位子可还稳？' },
    @{ Id = '4_玩家失约'; identity = '许过重诺的盟友'; scene = '厅堂，气氛沉闷。'; turn = '路上变故，说好的军资迟了半月。这也是我身不由己——能否再宽限些日子？' },
    @{ Id = '5_公开场合'; identity = '当众问询的部曲首领'; scene = '众人环伺的会盟，号角方歇。'; turn = '我们为你流的血够多了。当着众人，今日你先把酬劳的分配说明白，再谈下一桩事。' },
    @{ Id = '6_私下场合'; identity = '你最信任的一员旧将'; scene = '深夜灯下，只有两人。'; turn = '恕我直言。这么硬撑下去，底下的人心已经换了两茬，我怕的不是外头的刀，是咱们自己先散了。' },
    @{ Id = '7_资源压力'; identity = '掌着内务账目的执事'; scene = '秋末，粮仓见底。'; turn = '账我算清了：再撑下去，这个冬天要么减每人的口粮，要么先抚恤阵亡的家眷、把那几项开销收一收。您先定一样。' },
    @{ Id = '8_核心价值挑战'; identity = '族中德高望重、敢直谏的长者'; scene = '四下无人，一位老者单独求见。'; turn = '你争来的这些名声与权势，不该拿底下活人的命去填。收一收手，给治下的人一口安稳气，才算对得起先头人留下的根。' }
)

function Invoke-Sim([string[]]$Arguments) {
    $lines = @(& dotnet run --project $project -c Release --no-build -- @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "模拟器失败（$LASTEXITCODE）：$($lines -join [Environment]::NewLine)" }
    return ($lines -join [Environment]::NewLine)
}

function Get-NpcTemplate {
    $raw = Get-Content -LiteralPath $templatePath -Raw -Encoding UTF8
    $match = [regex]::Match($raw, 'TemplateText\s*=\s*@"(.*?)";\s*\r?\n\s*internal const string OutputSchemaJson', [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) { throw "无法从源码提取 NPC TemplateText：$templatePath" }
    return $match.Groups[1].Value.Replace('""', '"')
}

function Build-Prompt([string]$Template, [string]$Dsl, [string]$Identity, [string]$DisplayName, [string]$Scene, [string]$PlayerTurn) {
    $evidence = '【世界书检索结果（按身份门控）】（本次没有检索到任何世界书知识）'
    $values = @{
        npc_identity = $DisplayName
        persona_dsl = $Dsl
        retrieved_knowledge = $evidence
        npc_memory = '（无共同经历，玩家是新来的外乡人。）'
        npc_state = '戒备'
        dialogue_history = '（无）'
        player_known = '玩家身份如上所述。'
        scene = $Scene
        opening_hint = ''
        player_turn = $PlayerTurn
        npc_id = '"hero_scenario8"'
        dialogue_action_mode = 'chat：本轮只进行普通交谈；不得输出 command。'
    }
    $result = $Template
    foreach ($key in $values.Keys) { $result = $result.Replace('{{' + $key + '}}', [string]$values[$key]) }
    return $result
}

function Invoke-Ollama([string]$Prompt) {
    $body = @{
        model = $Model
        messages = @(@{ role = 'user'; content = $Prompt })
        stream = $false
        options = @{ temperature = 0.7; num_predict = 600 }
    } | ConvertTo-Json -Depth 10 -Compress
    $started = [DateTime]::UtcNow
    $response = Invoke-RestMethod -Uri $ollama -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json' -TimeoutSec 300
    [pscustomobject]@{ Content = [string]$response.message.content; Seconds = ([DateTime]::UtcNow - $started).TotalSeconds }
}

function Parse-Reply([string]$Content) {
    if ([string]::IsNullOrWhiteSpace($Content)) { return $null }
    $text = $Content.Trim()
    if ($text.StartsWith('```')) { $text = $text -replace '^```[^\r\n]*\r?\n?', ''; $text = $text -replace '`+$', '' }
    try {
        $value = $text | ConvertFrom-Json
        if ($null -ne $value.reply -and -not [string]::IsNullOrWhiteSpace([string]$value.reply)) { return $value }
    } catch { }
    $m = [regex]::Match($text, '"reply"\s*:\s*"((?:[^"\\]|\\.)*)"')
    if ($m.Success) {
        $r = $m.Groups[1].Value -replace '\\n', "`n" -replace '\\"', '"' -replace '\\\\', '\'
        if (-not [string]::IsNullOrWhiteSpace($r)) { return [pscustomobject]@{ reply = $r; mood = ''; command = $null } }
    }
    return $null
}

# 复读检测（V2）：算回答的 10 字滑窗落在卡内样本中的比例，按比例判定。
# 旧版只判"任意 10 字命中"的布尔值，会让 mid-adapt（模型改抄卡内句子、overlap 0.3~0.6）放行
# —— 蒙楚格 S2 实测 overlap=0.54 仍被 trae 判过。V2 用比例分级，>= Threshold 即记复读。
function Get-RepeatAnalysis {
    param([string]$Reply, [string[]]$Examples, [double]$Threshold = 0.30)
    $empty = [pscustomobject]@{ ratio = 0.0; hits = 0; total = 0; exactHit = $false; repeated = $false }
    if ([string]::IsNullOrWhiteSpace($Reply)) { return $empty }
    $rep = ($Reply -replace '\s+', '')
    if ($rep.Length -lt 10) { return $empty }
    $cleans = @($Examples | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } | ForEach-Object { ($_ -replace '\s+', '') } | Where-Object { $_.Length -ge 10 })
    if ($cleans.Count -eq 0) { return $empty }
    $total = $rep.Length - 10 + 1
    $hits = 0
    for ($i = 0; $i -lt $total; $i++) {
        $win = $rep.Substring($i, 10)
        foreach ($c in $cleans) { if ($c.Contains($win)) { $hits++; break } }
    }
    $ratio = [Math]::Round($hits / $total, 2)
    return [pscustomobject]@{ ratio = $ratio; hits = $hits; total = $total; exactHit = ($hits -gt 0); repeated = ($ratio -ge $Threshold) }
}

if (-not (Test-Path -LiteralPath $CharactersDir)) { throw "CharactersDir not found: $CharactersDir" }
$template = Get-NpcTemplate

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('awake-scenario8-' + [guid]::NewGuid().ToString('N'))
try {
    # 1) 只物化目标卡到临时目录
    $miniDir = Join-Path $temp 'characters'
    $defsDir = Join-Path $temp 'definitions'
    New-Item -ItemType Directory -Path $miniDir -Force | Out-Null
    foreach ($t in $Targets) {
        Copy-Item -LiteralPath (Join-Path $CharactersDir $t.CardFile) -Destination (Join-Path $miniDir $t.CardFile) -Force
        $base = $t.CardFile -replace '\.persona\.json$', ''
        $sidePath = Join-Path $CharactersDir ($base + '.origins.json')
        if (Test-Path -LiteralPath $sidePath -PathType Leaf) { Copy-Item -LiteralPath $sidePath -Destination (Join-Path $miniDir ($base + '.origins.json')) -Force }
    }
    $mat = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $materializer -CharactersDir $miniDir -RegistryPath $registry -OutDir $defsDir 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "物化失败：$($mat -join [Environment]::NewLine)" }

    $allCases = @()
    foreach ($t in $Targets) {
        $card = Get-Content -LiteralPath (Join-Path $CharactersDir $t.CardFile) -Raw -Encoding UTF8 | ConvertFrom-Json
        $displayName = [string]$card.displayName
        $examples = @($card.selfClaimExamples | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } | ForEach-Object { [string]$_ })

        $dslPath = Join-Path $temp ($t.HeroId + '.dsl.txt')
        Invoke-Sim @('persona', $defsDir, $registry, $t.HeroId, $dslPath, '8000', '--force-approved') | Out-Null
        $dsl = Get-Content -LiteralPath $dslPath -Raw -Encoding UTF8
        $dslHash = [BitConverter]::ToString(([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($dsl)))).Replace('-', '').ToLowerInvariant()

        $anyRepeat = $false
        foreach ($sc in $Scenarios) {
            $prompt = Build-Prompt $template $dsl ([string]$sc.identity) $displayName ([string]$sc.scene) ([string]$sc.turn)
            $entry = [ordered]@{
                scenario = $sc.Id; identity = $sc.identity; scene = $sc.scene; player_turn = $sc.turn
                heroId = $t.HeroId; displayName = $displayName; dslSha256 = $dslHash
            }
            try {
                $mr = Invoke-Ollama $prompt
                $parsed = Parse-Reply $mr.Content
                $entry.elapsedSeconds = [Math]::Round($mr.Seconds, 3)
                if ($null -ne $parsed) {
                    $entry.outputStatus = 'ok'
                    $entry.reply = [string]$parsed.reply
                    $entry.mood = [string]$parsed.mood
                    $entry.hasCommand = $null -ne $parsed.command
                    $ra = Get-RepeatAnalysis -Reply ([string]$parsed.reply) -Examples $examples -Threshold $OverlapThreshold
                    $entry.overlapRatio = $ra.ratio
                    $entry.exactSloganHit = $ra.exactHit
                    $entry.repeatedSlogan = $ra.repeated
                    if ($entry.repeatedSlogan) { $anyRepeat = $true }
                } else {
                    $entry.outputStatus = 'invalid_json_or_reply'
                    $entry.repeatedSlogan = $null
                    $entry.rawTail = ([string]$mr.Content).Substring(0, [Math]::Min(800, ([string]$mr.Content).Length))
                }
            } catch {
                $entry.outputStatus = 'in_doubt'; $entry.error = $_.Exception.Message; $entry.repeatedSlogan = $null
            }
            $allCases += [pscustomobject]$entry
        }
        $heroCases = @($allCases | Where-Object { $_.heroId -eq $t.HeroId })
        $usable = @($heroCases | Where-Object { $_.outputStatus -eq 'ok' }).Count
        $repeatCount = @($heroCases | Where-Object { $_.repeatedSlogan -eq $true }).Count
        $result = [ordered]@{
            schemaVersion = 'awake.persona.scenario-test.v1'
            status = if ($heroCases.Count -gt 0 -and $usable -eq $Scenarios.Count -and -not $anyRepeat) { 'pass' } elseif ($heroCases.Count -gt 0 -and $usable -eq $Scenarios.Count) { 'repetition_detected' } else { 'in_doubt' }
            model = $Model; heroId = $t.HeroId; heroName = $displayName
            cardSource = (Join-Path $CharactersDir $t.CardFile)
            runtimeFields = @('core','identityFacts','summary','publicDescription','privateDescription','contradictionDescription','selfClaimRules','realSelfBehaviors','selfClaimExamples','tags')
            checks = [ordered]@{
                dslGenerated = $true; scenariosRun = $heroCases.Count; usableReplies = $usable; allUsable = ($usable -eq $Scenarios.Count)
                anyReplyRepeatedSlogan = ($repeatCount -gt 0)
                overlapThreshold = $OverlapThreshold
                maxOverlapRatio = [Math]::Round([double](@($heroCases | Where-Object { $null -ne $_.overlapRatio } | Measure-Object -Property overlapRatio -Maximum).Maximum), 2)
                midOrHighOverlapCount = @($heroCases | Where-Object { $null -ne $_.overlapRatio -and $_.overlapRatio -ge 0.30 }).Count
                anyExactSloganHit = (@($heroCases | Where-Object { $_.exactSloganHit -eq $true }).Count -gt 0)
                anyCommandInChatMode = (@($heroCases | Where-Object { $_.hasCommand -eq $true }).Count -gt 0)
                anyError = (@($heroCases | Where-Object { $null -ne $_.error }).Count -gt 0)
            }
            cases = $heroCases
            promptTemplate = $templatePath; materializer = $materializer
        }
        $outPath = [System.IO.Path]::GetFullPath($Out)
        $dir = [System.IO.Path]::GetDirectoryName($outPath)
        if ($dir) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        $baseOut = $t.HeroId + '-scenario8.json'
        $result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $dir $baseOut) -Encoding UTF8
        $repFlag = if ($anyRepeat) { 'REPEAT_DETECTED' } else { 'CLEAN' }
        Write-Output ("{0} {1} hero={2} usable={3}/{4} {5}" -f $t.HeroId, $result.status, $displayName, $usable, $Scenarios.Count, $repFlag)
        if ($usable -ne $Scenarios.Count) { throw "英雄 $($t.HeroId) 未跑满 8 场景可用回答。" }
    }
} finally {
    if (Test-Path -LiteralPath $temp) { [System.IO.Directory]::Delete($temp, $true) }
}