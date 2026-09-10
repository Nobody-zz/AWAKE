param(
    [string]$Package,
    [string]$Zip,
    [ValidateSet('success', 'failure', 'real')]
    [string]$Browser = 'success',
    [ValidateSet('', 'clean-start', 'browser-success', 'browser-failure', 'stale-settings-missing', 'stale-settings-marker', 'duplicate-launch', 'graceful-shutdown')]
    [string]$Scenario = '',
    [string]$EvidencePath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($Scenario)) {
    $Scenario = switch ($Browser) {
        'failure' { 'browser-failure' }
        'real' { 'browser-success' }
        default { 'browser-success' }
    }
}
if ($Scenario -eq 'browser-failure') { $Browser = 'failure' }
elseif ($Scenario -in @('clean-start', 'browser-success', 'graceful-shutdown', 'stale-settings-missing', 'stale-settings-marker', 'duplicate-launch')) { $Browser = 'success' }

function Get-ByteHash([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) { return '' }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Get-PidState([int]$ProcessId) {
    if ($ProcessId -le 0) { return 'not_applicable' }
    try {
        $process = [System.Diagnostics.Process]::GetProcessById($ProcessId)
        try {
            if ($process.HasExited) { return 'not_found' }
            return 'alive'
        }
        finally { $process.Dispose() }
    }
    catch [System.ArgumentException] { return 'not_found' }
    catch [System.InvalidOperationException] { return 'not_found' }
    catch [System.ComponentModel.Win32Exception] { return 'access_denied' }
    catch { return 'access_denied' }
}

function Get-LauncherHttpEvents([string]$LocalAppData) {
    $events = @()
    $logDirectory = Join-Path $LocalAppData 'AWAKE\WorldbookStudio\logs'
    if (-not (Test-Path -LiteralPath $logDirectory -PathType Container)) { return $events }
    foreach ($file in @(Get-ChildItem -LiteralPath $logDirectory -Filter 'launcher-*.log' -File -ErrorAction SilentlyContinue)) {
        foreach ($line in @(Get-Content -LiteralPath $file.FullName -Encoding UTF8 -ErrorAction SilentlyContinue)) {
            try { $entry = $line | ConvertFrom-Json } catch { continue }
            $name = switch ([string]$entry.code) {
                'WB-HEALTH-OK' { 'health' }
                'WB-SHUTDOWN-ACCEPTED' { 'shutdown' }
                'WB-SHUTDOWN-REJECTED' { 'shutdown' }
                'WB-SHUTDOWN-TRANSPORT' { 'shutdown' }
                default { $null }
            }
            if ($null -ne $name) {
                $events += [ordered]@{ name = $name; status = $entry.exitCode; outcome = [string]$entry.code }
            }
        }
    }
    return $events
}

function Write-V2Evidence(
    [string]$ScenarioName,
    [object[]]$Actors,
    [object]$WebExitSnapshot,
    [string]$PostClosePidState,
    [string]$SettingsBefore,
    [string]$SettingsAfter,
    [object[]]$HttpEvents,
    [string]$PortState,
    [string]$Code,
    [Nullable[bool]]$BrowserOpened,
    [bool]$Ok) {
    $evidence = [ordered]@{
        schemaVersion = 'awake.worldbook.launcher-smoke.v2'
        scenario = $ScenarioName
        actors = @($Actors)
        webExitSnapshot = $WebExitSnapshot
        postClosePidState = $PostClosePidState
        settingsHashes = [ordered]@{ before = $SettingsBefore; after = $SettingsAfter }
        httpEvents = @($HttpEvents | Where-Object { $null -ne $_ })
        portState = $PortState
        code = $Code
        browserOpened = $BrowserOpened
        ok = $Ok
        evidenceDeadlineUtc = [DateTime]::UtcNow.ToString('O')
    }
    $json = $evidence | ConvertTo-Json -Depth 12
    if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) {
        $directory = Split-Path -Parent ([System.IO.Path]::GetFullPath($EvidencePath))
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
        $json | Set-Content -LiteralPath $EvidencePath -Encoding UTF8
    }
    Write-Output $json
}

function Start-SmokeLauncher([string]$ResultPath, [string]$WorkspacePath, [string]$LocalAppData, [hashtable]$Overrides) {
    $exe = Join-Path $packageRoot 'Awake.WorldbookStudio.Launcher.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'WB-SMOKE-001: Launcher.exe 缺失。' }
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $exe
    $psi.WorkingDirectory = $packageRoot
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.Environment['AWAKE_WB_SMOKE'] = '1'
    $psi.Environment['AWAKE_WB_SMOKE_BROWSER'] = $Browser
    $psi.Environment['AWAKE_WB_SMOKE_WORKSPACE'] = if ($null -eq $WorkspacePath) { '' } else { $WorkspacePath }
    $psi.Environment['AWAKE_WB_SMOKE_RESULT'] = $ResultPath
    $psi.Environment['AWAKE_WB_SMOKE_LOCALAPPDATA'] = $LocalAppData
    $psi.Environment['AWAKE_WB_SMOKE_HOLD'] = ''
    $psi.Environment['AWAKE_WB_SMOKE_READY'] = ''
    $psi.Environment['AWAKE_WB_SMOKE_RELEASE'] = ''
    $psi.Environment['AWAKE_WB_PORT'] = [string]$smokePort
    $psi.Environment['AWAKE_WB_MUTEX_NAME'] = $smokeMutexName
    $psi.Environment['USERPROFILE'] = $tempRoot
    $psi.Environment['DOTNET_ROOT'] = ''
    $psi.Environment['DOTNET_ROOT(x86)'] = ''
    foreach ($key in $Overrides.Keys) { $psi.Environment[$key] = [string]$Overrides[$key] }
    return [System.Diagnostics.Process]::Start($psi)
}

function Wait-SmokeResult([System.Diagnostics.Process]$Process, [string]$ResultPath, [int]$TimeoutMs = 30000) {
    if (-not $Process.WaitForExit($TimeoutMs)) {
        try { $Process.Kill($true) } catch { }
        throw 'WB-SMOKE-002: Launcher 30 秒内未退出。'
    }
    if (-not (Test-Path -LiteralPath $ResultPath -PathType Leaf)) { throw 'WB-SMOKE-003: 未生成 smoke-result.json。' }
    return Get-Content -LiteralPath $ResultPath -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Invoke-StaleSettingsScenario([string]$ScenarioName) {
    $scenarioRoot = Join-Path $tempRoot $ScenarioName
    $documents = Join-Path $scenarioRoot 'Documents'
    $localAppData = Join-Path $scenarioRoot 'LocalAppData'
    $savedWorkspace = Join-Path $scenarioRoot 'SavedWorkspace'
    $settingsPath = Join-Path $localAppData 'AWAKE\WorldbookStudio\settings.json'
    $markerPath = Join-Path $savedWorkspace '.awake-worldbook-workspace.json'
    $resultPath = Join-Path $scenarioRoot 'smoke-result.json'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $settingsPath) | Out-Null
    if ($ScenarioName -eq 'stale-settings-marker') {
        New-Item -ItemType Directory -Force -Path $savedWorkspace | Out-Null
        '{"Version":99,"Product":"wrong"}' | Set-Content -LiteralPath $markerPath -Encoding UTF8
    }
    else {
        New-Item -ItemType Directory -Force -Path $documents | Out-Null
    }
    ([ordered]@{ Version = 1; Workspace = $savedWorkspace; Language = 'zh-CN' } | ConvertTo-Json) | Set-Content -LiteralPath $settingsPath -Encoding UTF8
    $settingsBefore = Get-ByteHash $settingsPath
    $markerBefore = Get-ByteHash $markerPath
    $process = Start-SmokeLauncher $resultPath $null $localAppData @{ 'AWAKE_WB_SMOKE_DOCUMENTS' = $documents }
    try {
        $result = Wait-SmokeResult $process $resultPath
        if ($ScenarioName -eq 'stale-settings-marker') {
            if ($process.ExitCode -ne 1 -or $result.ok -ne $false -or $result.code -ne 'WB-SETTINGS-RECOVER-422') { throw "WB-SMOKE-010: 损坏 marker 未被安全阻断，退出码 $($process.ExitCode)，状态 $($result.code)。" }
            if ((Get-ByteHash $settingsPath) -ne $settingsBefore -or (Get-ByteHash $markerPath) -ne $markerBefore) { throw 'WB-SMOKE-011: 损坏 marker 场景修改了原始 settings/marker。' }
        }
        else {
            $defaultWorkspace = Join-Path $documents 'AWAKE\WorldbookStudio'
            $defaultMarker = Join-Path $defaultWorkspace '.awake-worldbook-workspace.json'
            $settingsAfter = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($process.ExitCode -ne 0 -or $result.ok -ne $true -or $result.code -ne 'WB-SMOKE-PASS') { throw "WB-SMOKE-010: 缺失工作区没有自动恢复，退出码 $($process.ExitCode)，状态 $($result.code)。" }
            if (-not (Test-Path -LiteralPath $defaultMarker -PathType Leaf)) { throw 'WB-SMOKE-011: 默认工作区 marker 缺失。' }
            if ([System.IO.Path]::GetFullPath($settingsAfter.Workspace) -ne [System.IO.Path]::GetFullPath($defaultWorkspace)) { throw 'WB-SMOKE-012: settings 没有切换到默认工作区。' }
            $backups = @(Get-ChildItem -LiteralPath (Split-Path -Parent $settingsPath) -Filter 'settings.json.recovery-*.bak' -File)
            if ($backups.Count -ne 1 -or (Get-ByteHash $backups[0].FullName) -ne $settingsBefore) { throw 'WB-SMOKE-013: 原始 settings 恢复备份缺失或内容不一致。' }
        }
        $snapshot = [ordered]@{ pid = [int]$result.processId; exitCode = $result.observedExitCode; exitedObserved = [bool]$result.exited }
        Write-V2Evidence $ScenarioName @([ordered]@{ role = 'launcher'; pid = $process.Id; exitCode = $process.ExitCode }) $snapshot 'not_applicable' $settingsBefore (Get-ByteHash $settingsPath) (Get-LauncherHttpEvents $localAppData) 'not_applicable' $result.code $false $true
    }
    finally { $process.Dispose(); try { Remove-Item -LiteralPath $scenarioRoot -Recurse -Force } catch { } }
}

function Invoke-DuplicateLaunchScenario {
    $scenarioRoot = Join-Path $tempRoot 'duplicate-launch'
    $localAppData = Join-Path $scenarioRoot 'LocalAppData'
    $workspace = Join-Path $scenarioRoot 'Workspace'
    $readyPath = Join-Path $scenarioRoot 'ready.json'
    $releasePath = Join-Path $scenarioRoot 'release.signal'
    $primaryResultPath = Join-Path $scenarioRoot 'primary-result.json'
    $secondaryResultPath = Join-Path $scenarioRoot 'secondary-result.json'
    $thirdResultPath = Join-Path $scenarioRoot 'third-result.json'
    New-Item -ItemType Directory -Force -Path $scenarioRoot, $workspace | Out-Null
    $primary = $null
    $secondary = $null
    $third = $null
    try {
        $primary = Start-SmokeLauncher $primaryResultPath $workspace $localAppData @{
            AWAKE_WB_SMOKE_HOLD = '1'
            AWAKE_WB_SMOKE_READY = $readyPath
            AWAKE_WB_SMOKE_RELEASE = $releasePath
        }
        $readyDeadline = [DateTime]::UtcNow.AddSeconds(30)
        while (-not (Test-Path -LiteralPath $readyPath -PathType Leaf) -and [DateTime]::UtcNow -lt $readyDeadline) { Start-Sleep -Milliseconds 25 }
        if (-not (Test-Path -LiteralPath $readyPath -PathType Leaf)) { throw 'WB-SMOKE-020: primary launcher 未写入 ready.json。' }
        $ready = Get-Content -LiteralPath $readyPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ([int]$ready.webProcessId -le 0 -or (Get-PidState ([int]$ready.webProcessId)) -ne 'alive') { throw 'WB-SMOKE-021: primary Web 没有保持存活。' }

        $secondary = Start-SmokeLauncher $secondaryResultPath $workspace $localAppData @{}
        $secondaryResult = Wait-SmokeResult $secondary $secondaryResultPath
        if ($secondary.ExitCode -ne 1 -or $secondaryResult.code -ne 'WB-INSTANCE-409' -or $secondaryResult.ok -ne $false) { throw 'WB-SMOKE-022: secondary 没有得到 WB-INSTANCE-409。' }
        if ([int]$secondaryResult.processId -ne 0) { throw 'WB-SMOKE-023: secondary 启动了第二个 Web PID。' }
        if (Test-Path -LiteralPath $releasePath) { Remove-Item -LiteralPath $releasePath -Force }
        New-Item -ItemType File -Path $releasePath | Out-Null
        if (-not $primary.WaitForExit(30000)) { try { $primary.Kill($true) } catch { }; throw 'WB-SMOKE-024: primary 未在 release 后退出。' }
        $pidState = Get-PidState ([int]$ready.webProcessId)
        $pidDeadline = [DateTime]::UtcNow.AddSeconds(5)
        while ($pidState -eq 'alive' -and [DateTime]::UtcNow -lt $pidDeadline) { Start-Sleep -Milliseconds 50; $pidState = Get-PidState ([int]$ready.webProcessId) }
        if ($pidState -ne 'not_found') { throw "WB-SMOKE-025: primary Web PID 在关闭后仍存活（状态 $pidState）。" }

        $third = Start-SmokeLauncher $thirdResultPath $workspace $localAppData @{}
        $thirdResult = Wait-SmokeResult $third $thirdResultPath
        if ($third.ExitCode -ne 0 -or $thirdResult.ok -ne $true -or $thirdResult.code -ne 'WB-SMOKE-PASS') { throw 'WB-SMOKE-026: mutex 释放后第三 Launcher 未成功启动。' }
        $primaryResult = Get-Content -LiteralPath $primaryResultPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $snapshot = [ordered]@{ pid = [int]$ready.webProcessId; exitCode = $primaryResult.observedExitCode; exitedObserved = [bool]$primaryResult.exited }
        Write-V2Evidence 'duplicate-launch' @(
            [ordered]@{ role = 'launcher-primary'; pid = [int]$ready.launcherProcessId; exitCode = $primary.ExitCode },
            [ordered]@{ role = 'launcher-secondary'; pid = $secondary.Id; exitCode = $secondary.ExitCode },
            [ordered]@{ role = 'launcher-third'; pid = $third.Id; exitCode = $third.ExitCode }
        ) $snapshot 'not_found' (Get-ByteHash (Join-Path $localAppData 'AWAKE\WorldbookStudio\settings.json')) (Get-ByteHash (Join-Path $localAppData 'AWAKE\WorldbookStudio\settings.json')) (Get-LauncherHttpEvents $localAppData) 'freed' 'WB-INSTANCE-409' $null $true
    }
    finally {
        foreach ($process in @($third, $secondary, $primary)) {
            if ($process) { try { if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit(2000) } } catch { }; $process.Dispose() }
        }
        try { Remove-Item -LiteralPath $scenarioRoot -Recurse -Force } catch { }
    }
}
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("awake-worldbook-studio-smoke-" + [Guid]::NewGuid().ToString('N'))
$smokePort = Select-FreePort
$smokeMutexName = "Local\AWAKE.WorldbookStudio.Smoke.$([Guid]::NewGuid().ToString('N'))"
$packageRoot = $null
$extracted = $false
if (-not [string]::IsNullOrWhiteSpace($Zip)) {
    $extractRoot = Join-Path $tempRoot 'package'
    New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
    Expand-Archive -LiteralPath ([System.IO.Path]::GetFullPath($Zip)) -DestinationPath $extractRoot -Force
    $packageRoot = $extractRoot
    $extracted = $true
}
elseif (-not [string]::IsNullOrWhiteSpace($Package)) {
    $packageRoot = if ([System.IO.Path]::IsPathRooted($Package)) { [System.IO.Path]::GetFullPath($Package) } else { [System.IO.Path]::GetFullPath((Join-Path $root $Package)) }
}
else { throw 'WB-SMOKE-000: 必须提供 Package 或 Zip。' }

if ($Scenario -eq 'stale-settings-missing' -or $Scenario -eq 'stale-settings-marker') {
    Invoke-StaleSettingsScenario $Scenario
    exit 0
}
if ($Scenario -eq 'duplicate-launch') {
    Invoke-DuplicateLaunchScenario
    exit 0
}

$documents = Join-Path $tempRoot 'Documents'
$localAppData = Join-Path $tempRoot 'LocalAppData'
$workspace = Join-Path $documents 'AWAKE\WorldbookStudio'
$resultPath = Join-Path $tempRoot 'smoke-result.json'
New-Item -ItemType Directory -Force -Path $documents, $localAppData, $workspace | Out-Null

$exe = Join-Path $packageRoot 'Awake.WorldbookStudio.Launcher.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'WB-SMOKE-001: Launcher.exe 缺失。' }
$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.FileName = $exe
$psi.WorkingDirectory = $packageRoot
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
$psi.Environment['AWAKE_WB_SMOKE'] = '1'
$psi.Environment['AWAKE_WB_SMOKE_BROWSER'] = $Browser
$psi.Environment['AWAKE_WB_SMOKE_WORKSPACE'] = $workspace
$psi.Environment['AWAKE_WB_SMOKE_RESULT'] = $resultPath
$psi.Environment['AWAKE_WB_SMOKE_LOCALAPPDATA'] = $localAppData
$psi.Environment['AWAKE_WB_PORT'] = [string]$smokePort
$psi.Environment['AWAKE_WB_MUTEX_NAME'] = $smokeMutexName
$psi.Environment['USERPROFILE'] = $tempRoot
$psi.Environment['DOTNET_ROOT'] = ''
$psi.Environment['DOTNET_ROOT(x86)'] = ''
$process = [System.Diagnostics.Process]::Start($psi)
try {
    if (-not $process.WaitForExit(30000)) { $process.Kill($true); throw 'WB-SMOKE-002: Launcher 30 秒内未退出。' }
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw 'WB-SMOKE-003: 未生成 smoke-result.json。' }
    $result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $expectedCode = if ($Scenario -eq 'browser-failure') { 'WB-BROWSER-OPEN-FAILED' } else { 'WB-SMOKE-PASS' }
    if ($process.ExitCode -ne 0 -or $result.ok -ne $true -or $result.exited -ne $true -or $result.code -ne $expectedCode) { throw "WB-SMOKE-004: smoke 失败，退出码 $($process.ExitCode)，状态 $($result.code)。" }
    if (($Scenario -ne 'browser-failure' -and $result.browserOpened -ne $true) -or ($Scenario -eq 'browser-failure' -and $result.browserOpened -ne $false)) { throw 'WB-SMOKE-005: 浏览器 fallback 状态不正确。' }
    if ($result.workspaceHash -eq [string]::Empty) { throw 'WB-SMOKE-006: workspace 身份缺失。' }
    if ([System.IO.Path]::GetFullPath($workspace).StartsWith($packageRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'WB-SMOKE-007: workspace 落入发行包。' }
    $httpEvents = Get-LauncherHttpEvents $localAppData
    if ($Scenario -eq 'graceful-shutdown' -and -not (@($httpEvents | Where-Object { $_.outcome -eq 'WB-SHUTDOWN-ACCEPTED' }).Count -gt 0)) { throw 'WB-SMOKE-008: graceful shutdown 没有留下 accepted 证据。' }
    $snapshot = [ordered]@{ pid = [int]$result.processId; exitCode = $result.observedExitCode; exitedObserved = [bool]$result.exited }
    Write-V2Evidence $Scenario @([ordered]@{ role = 'launcher'; pid = $process.Id; exitCode = $process.ExitCode }, [ordered]@{ role = 'web'; pid = [int]$result.processId; exitCode = $process.ExitCode }) $snapshot 'not_found' '' '' $httpEvents 'freed' $result.code ([bool]$result.browserOpened) $true
    Write-Output "PASS: Worldbook Studio smoke ($Browser) $packageRoot"
    Write-Output (Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8)
}
finally {
    if ($process) { $process.Dispose() }
    try { Remove-Item -LiteralPath $tempRoot -Recurse -Force } catch { }
}
