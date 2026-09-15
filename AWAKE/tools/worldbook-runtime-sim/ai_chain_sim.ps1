[CmdletBinding()]
param(
    [string]$Model = 'qwen2.5:latest',
    [string]$Out = (Join-Path ([System.IO.Path]::GetTempPath()) 'awake-ai-chain-sim.json')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $root 'tools\worldbook-runtime-sim\WorldbookRuntimeSim.csproj'
$manifest = Join-Path $root 'release\awake-worldbook-pilot\manifest.json'
$cards = Join-Path $root 'tools\persona-workbench\characters'
$materializer = Join-Path $root 'tools\persona-workbench\tools\materialize-definitions.ps1'
$registry = Join-Path $root 'ModuleData\Worldbook\persona_definitions\tag_registry.json'
$templatePath = Join-Path $root 'src\Prompts\NpcPromptTemplate.cs'
$ollama = 'http://127.0.0.1:11434/api/chat'

function Invoke-Sim([string[]]$Arguments) {
    $lines = @(& dotnet run --project $project -c Release --no-build -- @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "模拟器失败（$LASTEXITCODE）：`n$($lines -join [Environment]::NewLine)"
    }
    return ($lines -join [Environment]::NewLine)
}

function Get-NpcTemplate {
    $raw = Get-Content -LiteralPath $templatePath -Raw -Encoding UTF8
    $match = [regex]::Match($raw, 'TemplateText\s*=\s*@"(.*?)";\s*\r?\n\s*internal const string OutputSchemaJson', [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) { throw "无法从源码提取 NPC TemplateText：$templatePath" }
    return $match.Groups[1].Value.Replace('""', '"')
}

function Build-Prompt([string]$Template, [string]$Dsl, [string]$Identity, [string]$Knowledge, [string]$ReportText) {
    $evidence = "【本周动态（来自真实 WeeklyReportService 的结构化报告）】`n$ReportText`n`n"
    if ([string]::IsNullOrWhiteSpace($Knowledge)) {
        $evidence += '【世界书检索结果（按身份门控）】`n（本次没有检索到任何世界书知识）'
    } else {
        $evidence += "【世界书检索结果（按身份门控）】`n$Knowledge"
    }
    $values = @{
        npc_identity = $Identity
        persona_dsl = $Dsl
        retrieved_knowledge = $evidence
        npc_memory = '玩家刚才问过边境动静；这不是事实来源。'
        npc_state = '戒备'
        dialogue_history = '玩家：先说说最近边境的动静。'
        player_known = '玩家是刚进入此地的外乡人。'
        scene = '巴旦尼亚边境的一处石砌厅堂，傍晚。'
        opening_hint = ''
        player_turn = '你认为最近的动静会影响这里吗？'
        npc_id = '"hero_sim_caladog"'
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
    $bytes = [Text.Encoding]::UTF8.GetBytes($body)
    $started = [DateTime]::UtcNow
    $response = Invoke-RestMethod -Uri $ollama -Method Post -Body $bytes -ContentType 'application/json' -TimeoutSec 300
    return [pscustomobject]@{ Content = [string]$response.message.content; Seconds = ([DateTime]::UtcNow - $started).TotalSeconds }
}

function Parse-Reply([string]$Content) {
    if ([string]::IsNullOrWhiteSpace($Content)) { return $null }
    $candidates = @($Content.Trim())
    $fenced = [regex]::Match($Content, '```(?:json)?\s*(.*?)```', [Text.RegularExpressions.RegexOptions]::Singleline -bor [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($fenced.Success) { $candidates += $fenced.Groups[1].Value.Trim() }
    foreach ($candidate in $candidates) {
        try {
            $value = $candidate | ConvertFrom-Json
            if ($null -ne $value.reply -and -not [string]::IsNullOrWhiteSpace([string]$value.reply)) { return $value }
        } catch { }
    }
    return $null
}

if (-not (Test-Path -LiteralPath $manifest) -or -not (Test-Path -LiteralPath $registry) -or -not (Test-Path -LiteralPath $cards) -or -not (Test-Path -LiteralPath $materializer)) {
    throw '缺少当前可用的世界书或人物卡输入。'
}

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('awake-ai-chain-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $contextPath = Join-Path $temp 'context.json'
    $retrievalPath = Join-Path $temp 'retrieval.json'
    $dslPath = Join-Path $temp 'caladog.dsl.txt'
    $generatedDefs = Join-Path $temp 'definitions'
    $materializeLines = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $materializer -CharactersDir $cards -RegistryPath $registry -OutDir $generatedDefs 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "角色卡临时物化失败：$($materializeLines -join [Environment]::NewLine)" }
    $contextStdout = Invoke-Sim @('full-context', $contextPath)
    $retrievalStdout = Invoke-Sim @($manifest, $retrievalPath, '领主,收成', 'profile.noble,profile.soldier')
    $personaStdout = Invoke-Sim @('persona', $generatedDefs, $registry, 'lord_5_1', $dslPath, '8000', '--force-approved')
    $context = Get-Content -LiteralPath $contextPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $retrieval = Get-Content -LiteralPath $retrievalPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $dsl = Get-Content -LiteralPath $dslPath -Raw -Encoding UTF8
    $template = Get-NpcTemplate

    $cases = @(
        [pscustomobject]@{ Label = 'noble:领主'; Identity = '贵族'; Profile = 'profile.noble'; Question = '领主'; Expected = 'known_or_partial' },
        [pscustomobject]@{ Label = 'noble:收成'; Identity = '贵族'; Profile = 'profile.noble'; Question = '收成'; Expected = 'not_found' }
    )
    $caseResults = @()
    $inDoubt = $false
    foreach ($case in $cases) {
        $hit = @($retrieval | Where-Object { $_.identity -eq $case.Profile -and $_.question -eq $case.Question }) | Select-Object -First 1
        if ($null -eq $hit) { throw "找不到组合测试用例：$($case.Label)" }
        $prompt = Build-Prompt $template $dsl $case.Identity ([string]$hit.text) ([string]$context.reportText)
        $promptHash = [BitConverter]::ToString(([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($prompt)))).Replace('-', '').ToLowerInvariant()
        $entry = [ordered]@{
            case = $case.Label; identity = $case.Identity; question = $case.Question
            gateState = [string]$hit.state; knowledgeBytes = [Text.Encoding]::UTF8.GetByteCount([string]$hit.text)
            expectedGate = $case.Expected; promptSha256 = $promptHash; promptBytes = [Text.Encoding]::UTF8.GetByteCount($prompt)
        }
        if ([string]$hit.state -notin @('known', 'partial') -or [string]::IsNullOrWhiteSpace([string]$hit.text)) {
            # 与 NpcDialogueService.BuildPromptInputAsync 保持一致：没有可用知识时，
            # 直接走受控回复，不把“无资料”交给模型自行发挥。
            $entry.replySource = 'knowledge_direct_fallback'
            $entry.outputStatus = 'direct'
            $entry.reply = '这件事我没听说过。'
            $entry.mood = '茫然'
            $entry.hasCommand = $false
            $entry.aiCalled = $false
        } else {
            try {
                $replyResult = Invoke-Ollama $prompt
                $parsed = Parse-Reply $replyResult.Content
                $entry.replySource = 'ollama'
                $entry.aiCalled = $true
                $entry.elapsedSeconds = [Math]::Round($replyResult.Seconds, 3)
                $entry.rawReply = ([string]$replyResult.Content).Substring(0, [Math]::Min(4000, ([string]$replyResult.Content).Length))
                $entry.outputStatus = if ($null -ne $parsed) { 'ok' } else { 'invalid_json_or_reply' }
                if ($null -ne $parsed) {
                    $entry.reply = [string]$parsed.reply
                    $entry.mood = [string]$parsed.mood
                    $entry.hasCommand = $null -ne $parsed.command
                } else { $inDoubt = $true }
            } catch {
                $entry.outputStatus = 'in_doubt'; $entry.error = $_.Exception.Message; $inDoubt = $true
            }
        }
        $caseResults += [pscustomobject]$entry
    }

    $checks = [ordered]@{
        reportV2Validated = ($context.validation.reportV2 -eq $true)
        personaDslPresent = (-not [string]::IsNullOrWhiteSpace($dsl))
        positiveKnowledgeReturned = [bool](@($caseResults | Where-Object { $_.case -eq 'noble:领主' -and $_.knowledgeBytes -gt 0 -and $_.gateState -in @('known', 'partial') }).Count -gt 0)
        negativeKnowledgeBlocked = [bool](@($caseResults | Where-Object { $_.case -eq 'noble:收成' -and $_.knowledgeBytes -eq 0 -and $_.gateState -eq 'not_found' }).Count -gt 0)
        allRepliesUsable = [bool](@($caseResults | Where-Object { $_.outputStatus -notin @('ok', 'direct') }).Count -eq 0)
    }
    $status = if ($inDoubt) { 'in_doubt' } elseif (@($checks.Values | Where-Object { $_ -ne $true }).Count -eq 0) { 'pass' } else { 'fail' }
    $result = [ordered]@{
        schemaVersion = 'awake.ai.simulation-result.v1'; status = $status; model = $Model; ollamaEndpoint = $ollama
        sources = [ordered]@{ runtimeSimulator = $project; worldbookManifest = $manifest; personaCandidates = $cards; personaMaterializer = $materializer; promptTemplate = $templatePath }
        deterministic = [ordered]@{
            contextCommand = @($contextStdout -split "`r?`n" | Select-Object -Last 1)
            retrievalCommand = @($retrievalStdout -split "`r?`n" | Select-Object -Last 1)
            personaCommand = @($personaStdout -split "`r?`n" | Select-Object -Last 1)
            reportSchema = [string]$context.report.schemaVersion; reportSourceFactCount = @($context.facts).Count
            personaDslBytes = [Text.Encoding]::UTF8.GetByteCount($dsl); checks = $checks
        }
        cases = $caseResults
        limitations = @('不启动 Bannerlord，不执行真实战役回调、存档或 UI。', '周报事实使用真实 WeeklyReportService，但事实本身是固定测试 fixture。', '模型输出只用于观察提示词和门控效果，不会写回世界书、人物卡或源码。')
    }
    $outPath = [System.IO.Path]::GetFullPath($Out)
    $outParent = Split-Path -Parent $outPath
    if ($outParent) { New-Item -ItemType Directory -Path $outParent -Force | Out-Null }
    $result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $outPath -Encoding UTF8
    [pscustomobject]@{ status = $status; out = $outPath; checks = $checks } | ConvertTo-Json -Depth 5 -Compress
    if ($status -ne 'pass') { exit 2 }
} finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
