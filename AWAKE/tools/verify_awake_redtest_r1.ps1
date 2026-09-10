[CmdletBinding()]
param(
    [string]$ProjectRoot = '',
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent $PSScriptRoot
}
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $ProjectRoot 'docs\evidence\AWAKE-REDTEST-R1-STATIC-20260908.json'
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)

$buildId = 'awake-20260903-awake-runtime-repair-004'
$sourceRoot = Join-Path $ProjectRoot 'src'
$testSource = Join-Path (Split-Path -Parent $ProjectRoot) 'AWAKE.Tests\Program.cs'
$constantsPath = Join-Path $sourceRoot 'AwakeConstants.cs'
$buildPath = Join-Path $ProjectRoot '_build_out\1.3.15\Release\Awake.dll'

function Read-Text([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "missing_file:$Path"
    }
    return Get-Content -Raw -LiteralPath $Path
}

function Add-Check(
    [System.Collections.Generic.List[object]]$List,
    [string]$Id,
    [string]$Status,
    [string]$Severity,
    [string]$Message,
    [string[]]$Evidence,
    [string]$NextAction
) {
    $List.Add([ordered]@{
        id = $Id
        status = $Status
        severity = $Severity
        message = $Message
        evidence = @($Evidence)
        next_action = $NextAction
    })
}

$sourceFiles = @{
    constants = Read-Text $constantsPath
    submodule = Read-Text (Join-Path $sourceRoot 'SubModule.cs')
    runtime = Read-Text (Join-Path $sourceRoot 'AwakeRuntime.cs')
    host = Read-Text (Join-Path $sourceRoot 'AwakeHostComposition.cs')
    npcVm = Read-Text (Join-Path $sourceRoot 'NpcDialogueVM.cs')
    gold = Read-Text (Join-Path $sourceRoot 'AwakeGoldSettlementService.cs')
    event = Read-Text (Join-Path $sourceRoot 'AwakeEventEngine.cs')
    permission = Read-Text (Join-Path $sourceRoot 'PermissionGate.cs')
    config = Read-Text (Join-Path $sourceRoot 'AwakeConfig.cs')
    persona = Read-Text (Join-Path $sourceRoot 'PersonaPersistenceModels.cs')
    behavior = (Read-Text (Join-Path $sourceRoot 'AwakeEventBehavior.cs')) +
        (Read-Text (Join-Path $sourceRoot 'AwakeEncounterBehavior.cs')) +
        (Read-Text (Join-Path $sourceRoot 'AwakeTerminalBehavior.cs'))
    npcUi = (Read-Text (Join-Path $sourceRoot 'NpcDialogueVM.cs')) +
        (Read-Text (Join-Path $sourceRoot 'AwakeMessengerVM.cs'))
    overlay = (Read-Text (Join-Path $sourceRoot 'NpcDialogueOverlay.cs')) +
        (Read-Text (Join-Path $sourceRoot 'AwakeMessengerOverlay.cs')) +
        (Read-Text (Join-Path $sourceRoot 'WorldEventInboxOverlay.cs')) +
        (Read-Text (Join-Path $sourceRoot 'WeeklyReportBrowserOverlay.cs'))
}
$tests = Read-Text $testSource
$checks = [System.Collections.Generic.List[object]]::new()

if ($sourceFiles.constants.Contains($buildId)) {
    Add-Check $checks 'identity.build_id' 'PASS' 'E1' "当前源码包含 BuildId $buildId。" @('src\AwakeConstants.cs') '无'
} else {
    Add-Check $checks 'identity.build_id' 'FAIL' 'P1' '当前源码未包含目标 BuildId。' @('src\AwakeConstants.cs') '停止红测，先恢复候选身份。'
}

if (Test-Path -LiteralPath $buildPath -PathType Leaf) {
    $hash = (Get-FileHash -LiteralPath $buildPath -Algorithm SHA256).Hash.ToUpperInvariant()
    $expected = 'B25A5A4F1F7E95D7182BBD48FDC41A894E8366366440ECA4D27F2582AF74C17E'
    $status = if ($hash -eq $expected) { 'PASS' } else { 'FAIL' }
    $severity = if ($status -eq 'PASS') { 'E1' } else { 'P1' }
    Add-Check $checks 'identity.awake_dll_hash' $status $severity "Awake.dll SHA-256=$hash。" @('_build_out\1.3.15\Release\Awake.dll') '哈希不一致时禁止继续归属当前候选。'
} else {
    Add-Check $checks 'identity.awake_dll_hash' 'NOT_ATTEMPTED' 'E1' '当前 Release DLL 不存在。' @('_build_out\1.3.15\Release\Awake.dll') '先执行当前候选 Release 构建。'
}

$addBehaviorCount = ([regex]::Matches($sourceFiles.submodule, 'campaignStarter\.AddBehavior\(')).Count
$hasBehaviorGate = $sourceFiles.submodule -match '(?i)(contains|already|registered|dedup|registration.*gate|behavior.*exists)'
if ($addBehaviorCount -eq 3 -and -not $hasBehaviorGate) {
    Add-Check $checks 'lifecycle.behavior_registration_dedup' 'RISK' 'P1' 'OnGameStart 直接添加三个 Behavior，未发现本体级重复注册门。' @('src\SubModule.cs') '执行同一 CampaignGameStarter 二次 OnGameStart 红测；若复现重复处理，另立 P1 修复批次。'
} else {
    Add-Check $checks 'lifecycle.behavior_registration_dedup' 'PASS' 'E1' '源码存在行为注册防护迹象，需实机确认调用序列。' @('src\SubModule.cs') '仍需当前 BuildId 实机日志。'
}

if ($sourceFiles.npcVm -match 'SendAsync\(text,\s*CancellationToken\.None\)') {
    Add-Check $checks 'ui.dialogue_close_cancellation' 'RISK' 'P1' 'NPC 对话 VM 将发送请求传入 CancellationToken.None，关闭后的 VM 级取消未被证明。' @('src\NpcDialogueVM.cs') '执行发送后立即关闭、换人、重开和延迟返回红测。'
} else {
    Add-Check $checks 'ui.dialogue_close_cancellation' 'PASS' 'E1' '未发现已知 CancellationToken.None 发送模式。' @('src\NpcDialogueVM.cs') '仍需实机晚到结果红测。'
}

$drainMatches = ([regex]::Matches($sourceFiles.npcUi, 'while\s*\(\s*.*TryDrainUiEvent')).Count
$drainBudget = $sourceFiles.npcUi -match '(?i)(max.*event|event.*budget|drain.*limit|per.?frame.*limit)'
if ($drainMatches -gt 0 -and -not $drainBudget) {
    Add-Check $checks 'ui.tick_drain_budget' 'RISK' 'P1' 'UI 事件使用无上限 while 排空，未发现每帧预算。' @('src\NpcDialogueVM.cs','src\AwakeMessengerVM.cs') '执行高频 stream delta 红测并记录单帧 drain 数量与输入延迟。'
} else {
    Add-Check $checks 'ui.tick_drain_budget' 'PASS' 'E1' '源码存在 drain 限制迹象。' @('src\NpcDialogueVM.cs','src\AwakeMessengerVM.cs') '仍需实机帧时间证据。'
}

if ($sourceFiles.persona -match 'class\s+PersonaPersistence' -and $sourceFiles.behavior -notmatch 'PersonaPersistence|PersonaRecovery|SyncData.*persona') {
    Add-Check $checks 'save.persona_syncdata_wiring' 'BLOCKED' 'P0' 'Persona persistence 模型存在，但未发现 Bannerlord Behavior/SyncData 接线。' @('src\PersonaPersistenceModels.cs','src\AwakeEventBehavior.cs','src\AwakeEncounterBehavior.cs','src\AwakeTerminalBehavior.cs') '建立独立 Persona persistence/projection 接线批次后再执行 E5。'
} else {
    Add-Check $checks 'save.persona_syncdata_wiring' 'PASS' 'E1' '发现 Persona persistence 接线迹象。' @('src\PersonaPersistenceModels.cs') '仍需 E5 存档证据。'
}

$storageKeyMethod = [regex]::Match($sourceFiles.persona, '(?s)internal static bool TryBuild\(.*?\n\s*}\s*\n}\s*internal static class PersonaPersistenceValidator')
if ($storageKeyMethod.Success -and $storageKeyMethod.Value -notmatch 'timeline\.SaveId') {
    Add-Check $checks 'save.persona_storage_key_save_id' 'RISK' 'P1' 'PersonaStorageKey.TryBuild 未将 SaveId 纳入键组成，跨存档隔离仍未由键本身证明。' @('src\PersonaPersistenceModels.cs') '建立 SaveId 不同而其余身份相同的离线碰撞测试；修复需另立存档契约批次。'
} else {
    Add-Check $checks 'save.persona_storage_key_save_id' 'PASS' 'E1' 'Persona 存储键包含 SaveId，仍需 E5 读档证据。' @('src\PersonaPersistenceModels.cs') '执行跨存档隔离红测。'
}

if ($sourceFiles.config -notmatch '(?i)Config\.json') {
    Add-Check $checks 'config.json_fallback' 'RISK' 'P1' 'AwakeConfig 未发现 Config.json 回退读取路径。' @('src\AwakeConfig.cs') '确认是否仍要求 Config.json 回退；若要求，另立配置修复批次。'
} else {
    Add-Check $checks 'config.json_fallback' 'PASS' 'E1' 'AwakeConfig 包含 Config.json 相关路径。' @('src\AwakeConfig.cs') '执行 MCM/回退优先级红测。'
}

if ($sourceFiles.gold -match '(?i)\(\s*int\s*\)\s*args\[\s*"amount"\s*\]') {
    $adapter = Read-Text (Join-Path $sourceRoot 'AwakeWorldCommandAdapters.cs')
    $adapterValidates = $adapter -match '(?i)class\s+AwakeGiveGoldAdapter[\s\S]*?static bool Validate[\s\S]*?amountToken\.Type\s*!=\s*JTokenType\.Integer[\s\S]*?amount\s*<\s*1'
    if ($adapterValidates) {
        Add-Check $checks 'settlement.gold_amount_boundary' 'RISK' 'P1' '命令适配器有 amount 类型/范围校验，但内部 TryQueue 仍直接强转，防护依赖调用方不变量。' @('src\AwakeWorldCommandAdapters.cs','src\AwakeGoldSettlementService.cs') '分别执行适配器入口边界矩阵和内部绕过调用测试；若内部入口不可安全调用，另立最小结算修复批次。'
    } else {
        Add-Check $checks 'settlement.gold_amount_boundary' 'RISK' 'P1' '金钱入口直接转换 amount，未证明缺失、负数、零值和溢出均安全拒绝。' @('src\AwakeGoldSettlementService.cs','src\AwakeWorldCommandAdapters.cs') '执行金额边界矩阵；失败后另立最小结算修复批次。'
    }
} else {
    Add-Check $checks 'settlement.gold_amount_boundary' 'PASS' 'E1' '未发现已知直接转换模式。' @('src\AwakeGoldSettlementService.cs') '仍需实机金币观察。'
}

if ($sourceFiles.permission -notmatch '(?i)(CancelAfter|Task\.Delay|timeout|deadline)') {
    Add-Check $checks 'provider.permission_timeout' 'RISK' 'P1' 'PermissionGate 未发现本地统一 timeout/deadline 策略。' @('src\PermissionGate.cs') '执行长时间不返回的 fake Provider 红测并确认 fail-closed。'
} else {
    Add-Check $checks 'provider.permission_timeout' 'PASS' 'E1' 'PermissionGate 存在 timeout/deadline 相关处理。' @('src\PermissionGate.cs') '仍需执行 timeout 红测。'
}

if ($sourceFiles.event -match 'awake_event_effect_applied' -and $sourceFiles.event -notmatch 'if\s*\(\s*!?result\.IsSuccess') {
    Add-Check $checks 'settlement.event_failure_observation' 'RISK' 'P2' '事件效果日志记录 ok，但未发现基于 IsSuccess 的明确成功/失败分支。' @('src\AwakeEventEngine.cs') '注入关系命令失败，确认不得记录成功结算。'
} else {
    Add-Check $checks 'settlement.event_failure_observation' 'PASS' 'E1' '发现事件结果分支或未匹配已知风险模式。' @('src\AwakeEventEngine.cs') '仍需 fake failure 红测。'
}

if ($sourceFiles.event -notmatch '(?i)crime' -and $sourceFiles.gold -notmatch '(?i)crime') {
    Add-Check $checks 'settlement.crime_path' 'BLOCKED' 'P1' '当前源码未发现独立犯罪效果入口、命令或结算路径。' @('src') '若版本宣称支持犯罪，必须另立功能批次；否则标记 NOT_IMPLEMENTED/NOT_ENABLED。'
} else {
    Add-Check $checks 'settlement.crime_path' 'PASS' 'E1' '发现犯罪相关路径字样，需进一步确认闭环。' @('src') '执行犯罪入口→结算红测。'
}

$coverage = @(
    @{ id = 'coverage.storage_fail_closed'; method = 'RunPersistenceSettlementTruthSmokeAsync'; required = @('FailSet','must not report success') },
    @{ id = 'coverage.relationship_duplicate'; method = 'RunRelationshipCommandSmoke'; required = @('duplicate','DuplicateCount') },
    @{ id = 'coverage.gold_adapter_boundaries'; method = 'RunR1GoldAdapterBoundarySmoke'; required = @('missingAmount','overflowAmount','amount"] = 100001') },
    @{ id = 'coverage.persona_model_only'; method = 'RunPersonaPersistenceSmoke'; required = @('PersonaPersistenceValidator','save_commit_without_anchor') },
    @{ id = 'coverage.session_stale'; method = 'RunB1NativeStateSmokeAsync'; required = @('stale-session','current-session') }
)
foreach ($item in $coverage) {
    $methodPresent = $tests -match [regex]::Escape($item.method)
    $missing = @($item.required | Where-Object { $tests -notmatch [regex]::Escape($_) })
    if ($methodPresent -and $missing.Count -eq 0) {
        $status = if ($item.id -eq 'coverage.persona_model_only') { 'PARTIAL' } else { 'PASS' }
        $severity = if ($status -eq 'PARTIAL') { 'P1' } else { 'E2' }
        Add-Check $checks $item.id $status $severity "现有测试包含指定覆盖标记：$($item.method)。" @('..\AWAKE.Tests\Program.cs') '不把该测试扩大解释为 E4/E5。'
    } else {
        Add-Check $checks $item.id 'NOT_ATTEMPTED' 'E2' "未找到完整覆盖：$($item.method)。" @('..\AWAKE.Tests\Program.cs') '补充或明确标记对应离线负路径。'
    }
}

$summary = [ordered]@{}
foreach ($status in @('PASS','PARTIAL','RISK','BLOCKED','NOT_ATTEMPTED','FAIL')) {
    $summary[$status] = @($checks | Where-Object { $_.status -eq $status }).Count
}

$report = [ordered]@{
    schema_version = 'awake.redtest.r1-static-gate.v1'
    generated_at_utc = [DateTime]::UtcNow.ToString('o')
    target = 'AWAKE-REDTEST-PREP-20260908'
    build_id = $buildId
    mode = 'offline_static_source_and_test_audit'
    game_started = $false
    game_directory_synchronized = $false
    real_provider_accessed = $false
    summary = $summary
    checks = @($checks)
    evidence_boundary = 'This gate reports source/test coverage and known blockers. It does not prove Bannerlord E4/E5.'
    next_action = 'Execute existing offline negative-path tests where harnesses permit; do not synchronize or launch Bannerlord until explicit authorization.'
}

[IO.Directory]::CreateDirectory((Split-Path -Parent $OutputPath)) | Out-Null
$report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $OutputPath -Encoding utf8
$report | ConvertTo-Json -Depth 8
