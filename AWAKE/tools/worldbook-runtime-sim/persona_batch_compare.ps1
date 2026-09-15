[CmdletBinding()]
param(
    [string]$Model = 'qwen2.5:latest',
    [string]$Out = (Join-Path ([System.IO.Path]::GetTempPath()) 'awake-persona-batch-sim.json'),
    [switch]$CommandMatrix
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
    if ($LASTEXITCODE -ne 0) { throw "模拟器失败（$LASTEXITCODE）：$($lines -join [Environment]::NewLine)" }
    return ($lines -join [Environment]::NewLine)
}

function Get-NpcTemplate {
    $raw = Get-Content -LiteralPath $templatePath -Raw -Encoding UTF8
    $match = [regex]::Match($raw, 'TemplateText\s*=\s*@"(.*?)";\s*\r?\n\s*internal const string OutputSchemaJson', [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) { throw "无法从源码提取 NPC TemplateText：$templatePath" }
    return $match.Groups[1].Value.Replace('""', '"')
}

function Build-Prompt([string]$Template, [string]$Dsl, [string]$Identity, [string]$Knowledge, [string]$ReportText, [string]$PlayerTurn, [string]$History) {
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
        dialogue_history = $History
        player_known = '玩家是刚进入此地的外乡人。'
        scene = '巴旦尼亚边境的一处石砌厅堂，傍晚。'
        opening_hint = ''
        player_turn = $PlayerTurn
        npc_id = '"hero_batch_sim"'
        dialogue_action_mode = if ($PlayerTurn -like '*愿意为*') { 'negotiation：玩家正在明确提出行动或条件；只有明确接受且关系确实改变时才可提出 command。' } else { 'chat：本轮只进行普通交谈；不得输出 command。' }
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
    try {
        $value = $Content.Trim() | ConvertFrom-Json
        if ($null -ne $value.reply -and -not [string]::IsNullOrWhiteSpace([string]$value.reply)) { return $value }
    } catch { }
    return $null
}

if (-not (Test-Path -LiteralPath $manifest) -or -not (Test-Path -LiteralPath $cards) -or -not (Test-Path -LiteralPath $materializer) -or -not (Test-Path -LiteralPath $registry)) {
    throw '缺少当前可用的世界书、角色卡或物化工具。'
}

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('awake-persona-batch-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null
try {
    $generatedDefs = Join-Path $temp 'definitions'
    $materialize = @(& pwsh -NoProfile -ExecutionPolicy Bypass -File $materializer -CharactersDir $cards -RegistryPath $registry -OutDir $generatedDefs 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "角色卡物化失败：$($materialize -join [Environment]::NewLine)" }

    $retrievalPath = Join-Path $temp 'retrieval.json'
    Invoke-Sim @($manifest, $retrievalPath, '领主', 'profile.noble') | Out-Null
    $retrieval = Get-Content -LiteralPath $retrievalPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $hit = @($retrieval | Where-Object { $_.identity -eq 'profile.noble' -and $_.question -eq '领主' }) | Select-Object -First 1
    if ($null -eq $hit) { throw '找不到固定贵族/领主世界书用例。' }

    $contextPath = Join-Path $temp 'context.json'
    Invoke-Sim @('full-context', $contextPath) | Out-Null
    $context = Get-Content -LiteralPath $contextPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $template = Get-NpcTemplate
    $promptCases = if ($CommandMatrix) {
        @(
            [pscustomobject]@{ Id = 'neutral'; PlayerTurn = '你认为最近的动静会影响这里吗？'; History = '玩家：先说说最近边境的动静。' },
            [pscustomobject]@{ Id = 'relationship'; PlayerTurn = '我现在就提供一千第纳尔军费和五十名弓手，换取你公开承认我为巴旦尼亚的盟友。若你同意这项交换，请明确接受并提交关系变化；若不同意，请直接拒绝。'; History = '玩家：我愿意帮你守住边境。' }
        )
    } else {
        @([pscustomobject]@{ Id = 'neutral'; PlayerTurn = '你认为最近的动静会影响这里吗？'; History = '玩家：先说说最近边境的动静。' })
    }

    $candidateRows = @()
    foreach ($cardFile in Get-ChildItem -LiteralPath $cards -File -Filter '*.persona.json' | Sort-Object Name) {
        $card = Get-Content -LiteralPath $cardFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $base = $cardFile.Name -replace '\.persona\.json$',''
        $sidePath = Join-Path $cards ($base + '.origins.json')
        if (-not (Test-Path -LiteralPath $sidePath)) { continue }
        $side = Get-Content -LiteralPath $sidePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $candidateRows += [pscustomobject]@{ File = $cardFile.Name; DisplayName = [string]$card.displayName; HeroId = [string]$side.heroId; KingdomId = [string]$side.kingdomId; Tags = @($card.tags) }
    }
    $selected = @($candidateRows | Group-Object KingdomId | Sort-Object Name | ForEach-Object { $_.Group | Sort-Object HeroId | Select-Object -First 1 })
    if ($selected.Count -lt 8) { throw "代表性王国数量不足，实际只有 $($selected.Count) 个。" }

    $cases = @()
    $dslHashes = @()
    foreach ($candidate in $selected) {
        $dslPath = Join-Path $temp ($candidate.HeroId + '.dsl.txt')
        Invoke-Sim @('persona', $generatedDefs, $registry, $candidate.HeroId, $dslPath, '8000', '--force-approved') | Out-Null
        $dsl = Get-Content -LiteralPath $dslPath -Raw -Encoding UTF8
        $dslHash = [BitConverter]::ToString(([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($dsl)))).Replace('-', '').ToLowerInvariant()
        $dslHashes += $dslHash
        foreach ($scenario in $promptCases) {
            $prompt = Build-Prompt $template $dsl $candidate.DisplayName ([string]$hit.text) ([string]$context.reportText) $scenario.PlayerTurn $scenario.History
            $promptHash = [BitConverter]::ToString(([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($prompt)))).Replace('-', '').ToLowerInvariant()
            $entry = [ordered]@{
                scenario = $scenario.Id; heroId = $candidate.HeroId; displayName = $candidate.DisplayName; kingdomId = $candidate.KingdomId
                tagCount = $candidate.Tags.Count; dslSha256 = $dslHash; dslBytes = [Text.Encoding]::UTF8.GetByteCount($dsl)
                promptSha256 = $promptHash; gateState = [string]$hit.state; knowledgeBytes = [Text.Encoding]::UTF8.GetByteCount([string]$hit.text)
                playerTurn = $scenario.PlayerTurn
            }
            try {
                $modelResult = Invoke-Ollama $prompt
                $parsed = Parse-Reply $modelResult.Content
                $entry.elapsedSeconds = [Math]::Round($modelResult.Seconds, 3)
                $entry.outputStatus = if ($null -ne $parsed) { 'ok' } else { 'invalid_json_or_reply' }
                if ($null -ne $parsed) {
                    $entry.reply = [string]$parsed.reply; $entry.mood = [string]$parsed.mood; $entry.hasCommand = $null -ne $parsed.command
                } else { $entry.rawReply = ([string]$modelResult.Content).Substring(0, [Math]::Min(1000, ([string]$modelResult.Content).Length)) }
            } catch {
                $entry.outputStatus = 'in_doubt'; $entry.error = $_.Exception.Message
            }
            $cases += [pscustomobject]$entry
        }
    }

    $structured = @($cases | Where-Object { $_.outputStatus -ne 'ok' }).Count -eq 0
    $neutralCases = @($cases | Where-Object { $_.scenario -eq 'neutral' })
    $relationshipCases = @($cases | Where-Object { $_.scenario -eq 'relationship' })
    $neutralCommands = @($neutralCases | Where-Object { $_.hasCommand -eq $true }).Count
    $relationshipCommands = @($relationshipCases | Where-Object { $_.hasCommand -eq $true }).Count
    $result = [ordered]@{
        schemaVersion = 'awake.persona.batch-simulation.v1'; status = if ($structured) { 'pass' } else { 'in_doubt' }; model = $Model; ollamaEndpoint = $ollama
        selection = [ordered]@{ strategy = 'one candidate per kingdomId'; count = $selected.Count; scenarios = @($promptCases.Id); sameWorldbookProfile = 'profile.noble' }
        sources = [ordered]@{ personaCandidates = $cards; personaMaterializer = $materializer; worldbookManifest = $manifest; promptTemplate = $templatePath }
        deterministic = [ordered]@{ reportSchema = [string]$context.report.schemaVersion; reportSourceFactCount = @($context.facts).Count; gateState = [string]$hit.state; knowledgeBytes = [Text.Encoding]::UTF8.GetByteCount([string]$hit.text); uniqueDslCount = @($dslHashes | Sort-Object -Unique).Count; allDslDistinct = @($dslHashes | Sort-Object -Unique).Count -eq $selected.Count; allOutputsStructured = $structured; neutralCommandCount = $neutralCommands; relationshipCommandCount = $relationshipCommands; neutralCommandRate = if ($neutralCases.Count -eq 0) { 0 } else { [Math]::Round($neutralCommands / $neutralCases.Count, 3) }; relationshipCommandRate = if ($relationshipCases.Count -eq 0) { 0 } else { [Math]::Round($relationshipCommands / $relationshipCases.Count, 3) }; commandPolicyObservation = if ($CommandMatrix -and $neutralCommands -gt 0) { 'neutral_prompt_emitted_command' } else { 'not_observed' } }
        cases = $cases
        limitations = @('固定事实 fixture，不是游戏实时战役回调。', '候选卡在临时物化后使用 force-approved，仅用于离线观察，不改变卡片状态。', '模型回答的文学质量仍需人工判断；pass 只表示链路和结构化输出通过。')
    }
    $outPath = [System.IO.Path]::GetFullPath($Out)
    $parent = Split-Path -Parent $outPath
    if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $outPath -Encoding UTF8
    [pscustomobject]@{ status = $result.status; out = $outPath; selected = $selected.Count; uniqueDsl = $result.deterministic.uniqueDslCount; allOutputsStructured = $structured } | ConvertTo-Json -Depth 5 -Compress
    if ($result.status -ne 'pass') { exit 2 }
} finally {
    if (Test-Path -LiteralPath $temp) { [System.IO.Directory]::Delete($temp, $true) }
}
