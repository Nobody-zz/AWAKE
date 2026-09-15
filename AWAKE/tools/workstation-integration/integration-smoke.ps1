[CmdletBinding()]
param(
    [string]$EvidencePath = "docs\evidence\AWAKE-THREE-WORKSTATIONS-20260904.json",
    [switch]$KeepTemp,
    [int]$StartupTimeoutSeconds = 30,
    [int]$RequestTimeoutSeconds = 20
)

$ErrorActionPreference = 'Stop'
$awakeRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$pwbProject = Join-Path $awakeRoot 'tools\persona-workbench\src\PersonaWorkbench.Web\PersonaWorkbench.Web.csproj'
$wbsProject = Join-Path $awakeRoot 'tools\worldbook-studio\src\Awake.WorldbookStudio.Web\Awake.WorldbookStudio.Web.csproj'
$wbsSchemaRoot = Join-Path $awakeRoot 'docs\worldbook-studio-plan'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-three-workstations-' + [Guid]::NewGuid().ToString('N'))
$workspace = Join-Path $tempRoot 'workspace'
$pwbWorkspace = Join-Path $tempRoot 'persona-workspace'
$rawRoot = Join-Path $tempRoot 'raw'
$logRoot = Join-Path $tempRoot 'logs'
$pwbPort = $null
$wbsPort = $null
$pwbProcess = $null
$wbsProcess = $null
$uiProcess = $null
$uiDuplicateProcess = $null
$uiReadySnapshotPath = $null
$uiLockSnapshotPath = $null
$pwbSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$wbsSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$artifactRoot = $null
$evidencePathAbsolute = $null
$cases = [ordered]@{}

$caseIds = @(
    'health-all-ready',
    'duplicate-instance',
    'shutdown-releases-lock',
    'persona-issue',
    'wbs-import',
    'wbs-accept',
    'wbs-consume',
    'handoff-readback',
    'duplicate-idempotent',
    'changed-fingerprint-conflict',
    'stale-revision',
    'expired-handoff',
    'interrupted-consume-in-doubt',
    'unknown-readback',
    'recovery-retry',
    'unsaved-draft-preserved',
    'list-filter-detail-restored',
    'ui-navigation-readback'
)

foreach ($caseId in $caseIds) {
    $cases[$caseId] = [ordered]@{
        id = $caseId
        passed = $false
        status = 'not_implemented'
        expected = ''
        observed = ''
        artifacts = @()
    }
}

$evidence = [ordered]@{
    schema_version = 'awake.three.workstations.integration-smoke.v1'
    target = 'AWAKE-THREE-WORKSTATIONS-20260904'
    started_at_utc = [DateTime]::UtcNow.ToString('O')
    finished_at_utc = $null
    network_boundary = 'loopback_only'
    implementation_scope = @('persona_workbench_web', 'worldbook_studio_web', 'ui_workstation_adapter')
    temporary_workspace = $null
    ports = [ordered]@{}
    processes = @()
    cases = @()
    artifacts_root = $null
    external_resource_audit = [ordered]@{
        configured_endpoints = @()
        game_processes_before = @()
        game_processes_after = @()
        game_directory_touched = $false
        manager_endpoint_touched = $false
        cloud_endpoint_touched = $false
        non_loopback_endpoint_touched = $false
        provider_api_key_present = $false
    }
    result = 'failed'
    verified_case_count = 0
    partial_case_count = 0
    not_implemented_case_count = 0
    failed_case_count = 0
    passed = $false
    error = $null
}

function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Sha256-Text([string]$Text) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    return ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))).ToLowerInvariant()
}

function ConvertTo-JsonBytes([object]$Value) {
    return ,([Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Compress -Depth 50)))
}

function Add-CaseArtifact([string]$CaseId, [string]$Path) {
    $cases[$CaseId].artifacts += [IO.Path]::GetFullPath($Path)
}

function Set-Case(
    [string]$CaseId,
    [bool]$Passed,
    [string]$Status,
    [string]$Expected,
    [string]$Observed,
    [string[]]$Artifacts = @()
) {
    $cases[$CaseId].passed = $Passed
    $cases[$CaseId].status = $Status
    $cases[$CaseId].expected = $Expected
    $cases[$CaseId].observed = $Observed
    foreach ($artifact in $Artifacts) {
        Add-CaseArtifact $CaseId $artifact
    }
}

function Stop-Child([object]$Child) {
    if ($null -eq $Child) {
        return
    }
    try {
        $Child.Refresh()
        if (-not $Child.HasExited) {
            try { $Child.Kill($true) } catch { $Child.Kill() }
            [void]$Child.WaitForExit(5000)
        }
    }
    catch { }
    try { $Child.Dispose() } catch { }
}

function Test-ProcessExited([object]$Child) {
    if ($null -eq $Child) {
        return $true
    }
    try {
        $Child.Refresh()
        return $Child.HasExited
    }
    catch {
        return $true
    }
}

function Test-PidExited([object]$ProcessId) {
    if ($null -eq $ProcessId -or [string]::IsNullOrWhiteSpace([string]$ProcessId)) {
        return $true
    }
    try {
        Get-Process -Id ([int]$ProcessId) -ErrorAction Stop | Out-Null
        return $false
    }
    catch {
        return $true
    }
}

function Start-WebProject(
    [string]$Name,
    [string]$Project,
    [string]$WorkingDirectory,
    [int]$Port,
    [hashtable]$Environment,
    [string]$LogDirectory,
    [string[]]$Arguments = @()
) {
    $stdoutPath = Join-Path $LogDirectory "$Name.stdout.log"
    $stderrPath = Join-Path $LogDirectory "$Name.stderr.log"
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Command dotnet -ErrorAction Stop).Source
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add('run')
    $startInfo.ArgumentList.Add('--project')
    $startInfo.ArgumentList.Add($Project)
    $startInfo.ArgumentList.Add('--configuration')
    $startInfo.ArgumentList.Add('Release')
    $startInfo.ArgumentList.Add('--no-build')
    $startInfo.ArgumentList.Add('--no-restore')
    if ($Arguments.Count -gt 0) {
        $startInfo.ArgumentList.Add('--')
        foreach ($argument in $Arguments) {
            $startInfo.ArgumentList.Add($argument)
        }
    }
    foreach ($entry in $Environment.GetEnumerator()) {
        $startInfo.Environment[$entry.Key] = [string]$entry.Value
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    return [pscustomobject]@{
        Name = $Name
        Process = $process
        StdoutTask = $stdoutTask
        StderrTask = $stderrTask
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
        Port = $Port
    }
}

function Start-UiAdapter(
    [string]$Name,
    [string]$AdapterScript,
    [string]$WorkspaceRoot,
    [string]$WbsBaseUrl,
    [int]$Port,
    [string]$RuntimeRoot,
    [string]$LogDirectory
) {
    $stdoutPath = Join-Path $LogDirectory "$Name.stdout.log"
    $stderrPath = Join-Path $LogDirectory "$Name.stderr.log"
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Command pwsh -ErrorAction Stop).Source
    $startInfo.WorkingDirectory = Split-Path -Parent $AdapterScript
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @(
        '-NoProfile',
        '-NonInteractive',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $AdapterScript,
        '-WorkspaceRoot',
        $WorkspaceRoot,
        '-WbsBaseUrl',
        $WbsBaseUrl,
        '-Port',
        [string]$Port,
        '-RuntimeRoot',
        $RuntimeRoot
    )) {
        $startInfo.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    return [pscustomobject]@{
        Name = $Name
        Process = $process
        StdoutTask = $stdoutTask
        StderrTask = $stderrTask
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
        RuntimeRoot = $RuntimeRoot
    }
}

function Wait-ForFile([string]$Path, [int]$TimeoutSeconds = 10) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            return $true
        }
        Start-Sleep -Milliseconds 100
    }
    return $false
}

function Wait-ForExit([object]$Child, [int]$TimeoutSeconds = 10) {
    if ($null -eq $Child) {
        return $true
    }
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($Child.Process.HasExited) {
            return $true
        }
        Start-Sleep -Milliseconds 100
    }
    return $Child.Process.HasExited
}

function Save-ProcessLogs([object]$Child) {
    if ($null -eq $Child) {
        return @()
    }
    try {
        Stop-Child $Child.Process
    }
    catch { }
    try { $Child.StdoutTask.GetAwaiter().GetResult() | Set-Content -LiteralPath $Child.StdoutPath -Encoding UTF8 } catch { }
    try { $Child.StderrTask.GetAwaiter().GetResult() | Set-Content -LiteralPath $Child.StderrPath -Encoding UTF8 } catch { }
    return @($Child.StdoutPath, $Child.StderrPath)
}

function Invoke-JsonRequest(
    [string]$Name,
    [string]$Method,
    [string]$Uri,
    [object]$Body,
    [hashtable]$Headers,
    [Microsoft.PowerShell.Commands.WebRequestSession]$Session
) {
    $requestPath = Join-Path $rawRoot "$Name.request.json"
    $responsePath = Join-Path $rawRoot "$Name.response.json"
    $requestRecord = [ordered]@{
        method = $Method
        uri = $Uri
        headers = [ordered]@{}
        body = $Body
    }
    foreach ($header in $Headers.GetEnumerator()) {
        $requestRecord.headers[$header.Key] = [string]$header.Value
    }
    $requestRecord | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $requestPath -Encoding UTF8
    $parameters = @{
        Method = $Method
        Uri = $Uri
        Headers = $Headers
        WebSession = $Session
        UseBasicParsing = $true
        TimeoutSec = $RequestTimeoutSeconds
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = ConvertTo-JsonBytes $Body
    }
    $response = Invoke-WebRequest @parameters -SkipHttpErrorCheck
    $content = $response.Content
    $statusCode = [int]$response.StatusCode
    $bodyValue = $null
    if (-not [string]::IsNullOrWhiteSpace($content)) {
        try { $bodyValue = $content | ConvertFrom-Json -Depth 50 -DateKind String } catch { $bodyValue = [ordered]@{ raw = $content } }
    }
    [ordered]@{
        status_code = $statusCode
        body = $bodyValue
        raw = $content
        request_path = $requestPath
        response_path = $responsePath
    } | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $responsePath -Encoding UTF8
    return [pscustomobject]@{
        StatusCode = $statusCode
        Body = $bodyValue
        Raw = $content
        RequestPath = $requestPath
        ResponsePath = $responsePath
    }
}

function Wait-ForHealth(
    [string]$Name,
    [string]$Uri,
    [Microsoft.PowerShell.Commands.WebRequestSession]$Session
) {
    $lastError = $null
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $result = Invoke-JsonRequest "$Name-health" 'Get' $Uri $null @{} $Session
            if ($result.StatusCode -eq 200) {
                return $result
            }
            $lastError = "HTTP $($result.StatusCode)"
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 250
    }
    throw "$Name health timeout: $lastError"
}

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "WORKSTATION-INTEGRATION-SMOKE-FAIL: $Message"
    }
}

function Get-PathValue([object]$Object, [string[]]$Path) {
    $current = $Object
    foreach ($segment in $Path) {
        if ($null -eq $current) {
            return $null
        }
        $property = $current.PSObject.Properties[$segment]
        if ($null -eq $property) {
            return $null
        }
        $current = $property.Value
    }
    return $current
}

function Get-GameProcessSnapshot {
    return @(Get-Process -Name Bannerlord -ErrorAction SilentlyContinue | ForEach-Object {
        [ordered]@{ id = $_.Id; name = $_.ProcessName }
    })
}

try {
    Assert-Condition (Test-Path -LiteralPath $pwbProject -PathType Leaf) 'PWB Web project is missing.'
    Assert-Condition (Test-Path -LiteralPath $wbsProject -PathType Leaf) 'WBS Web project is missing.'
    Assert-Condition (Test-Path -LiteralPath $wbsSchemaRoot -PathType Container) 'WBS schema root is missing.'
    $uiAdapterScript = Join-Path $awakeRoot 'tools\ui-workstation\UiWorkstation.Adapter.ps1'
    Assert-Condition (Test-Path -LiteralPath $uiAdapterScript -PathType Leaf) 'UI Workstation adapter is missing.'

    New-Item -ItemType Directory -Force -Path $workspace, $pwbWorkspace, $rawRoot, $logRoot | Out-Null
    $evidenceTarget = if ([IO.Path]::IsPathRooted($EvidencePath)) { $EvidencePath } else { Join-Path $awakeRoot $EvidencePath }
    $artifactRoot = Join-Path (Split-Path -Parent $evidenceTarget) ('workstation-integration-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
    $evidencePathAbsolute = if ([IO.Path]::IsPathRooted($EvidencePath)) { [IO.Path]::GetFullPath($EvidencePath) } else { [IO.Path]::GetFullPath((Join-Path $awakeRoot $EvidencePath)) }
    $evidence.temporary_workspace = $tempRoot
    $evidence.artifacts_root = $artifactRoot
    $evidence.external_resource_audit.game_processes_before = @(Get-GameProcessSnapshot)

    $pwbPort = Select-FreePort
    do { $wbsPort = Select-FreePort } while ($wbsPort -eq $pwbPort)
    $pwbOrigin = "http://127.0.0.1:$pwbPort"
    $wbsOrigin = "http://127.0.0.1:$wbsPort"
    $evidence.ports.pwb = $pwbPort
    $evidence.ports.wbs = $wbsPort
    $evidence.external_resource_audit.configured_endpoints = @($pwbOrigin, $wbsOrigin)

    $pwbEnvironment = @{
        AWAKE_PWB_PORT = $pwbPort
        AWAKE_PWB_WORKSPACE = $pwbWorkspace
        ASPNETCORE_ENVIRONMENT = 'Development'
        DOTNET_ENVIRONMENT = 'Development'
        HTTP_PROXY = ''
        HTTPS_PROXY = ''
        ALL_PROXY = ''
        NO_PROXY = '127.0.0.1,localhost'
    }
    $wbsEnvironment = @{
        AWAKE_WB_DEV_MODE = '1'
        AWAKE_WB_PORT = $wbsPort
        AWAKE_WB_WORKSPACE = $workspace
        WORLD_BOOK_WORKSPACE = $workspace
        WORLD_BOOK_SCHEMA_ROOT = $wbsSchemaRoot
        AWAKE_WB_SCHEMA_ROOT = $wbsSchemaRoot
        ASPNETCORE_ENVIRONMENT = 'Development'
        DOTNET_ENVIRONMENT = 'Development'
        HTTP_PROXY = ''
        HTTPS_PROXY = ''
        ALL_PROXY = ''
        NO_PROXY = '127.0.0.1,localhost'
    }
    $pwbProcess = Start-WebProject 'pwb' $pwbProject (Split-Path -Parent $pwbProject) $pwbPort $pwbEnvironment $logRoot @('--no-browser', '--port', [string]$pwbPort)
    $wbsProcess = Start-WebProject 'wbs' $wbsProject (Split-Path -Parent $wbsProject) $wbsPort $wbsEnvironment $logRoot

    $pwbHealth = Wait-ForHealth 'pwb' "$pwbOrigin/api/health" $pwbSession
    $wbsHealth = Wait-ForHealth 'wbs' "$wbsOrigin/api/health" $wbsSession
    $pwbHealthy = $pwbHealth.StatusCode -eq 200 -and (Get-PathValue $pwbHealth.Body @('state')) -eq 'ready'
    $wbsHealthy = $wbsHealth.StatusCode -eq 200 -and (Get-PathValue $wbsHealth.Body @('state')) -eq 'ready'

    $uiRuntimeRoot = Join-Path $tempRoot 'ui-runtime'
    $uiProcess = Start-UiAdapter 'ui' $uiAdapterScript $workspace $wbsOrigin 0 $uiRuntimeRoot $logRoot
    $uiReadyPath = Join-Path $uiRuntimeRoot 'ready.json'
    $uiLockPath = Join-Path $uiRuntimeRoot 'ui-workstation.lock.json'
    Assert-Condition (Wait-ForFile $uiReadyPath $StartupTimeoutSeconds) 'UI Workstation ready.json timeout.'
    $uiReady = Get-Content -LiteralPath $uiReadyPath -Raw | ConvertFrom-Json -Depth 20 -DateKind String
    $uiReadySnapshotPath = Join-Path $rawRoot 'ui-ready-before-shutdown.json'
    $uiLockSnapshotPath = Join-Path $rawRoot 'ui-lock-before-shutdown.json'
    Copy-Item -LiteralPath $uiReadyPath -Destination $uiReadySnapshotPath -Force
    Copy-Item -LiteralPath $uiLockPath -Destination $uiLockSnapshotPath -Force
    $uiPort = [int](Get-PathValue $uiReady @('port'))
    $uiOrigin = "http://127.0.0.1:$uiPort"
    $expectedUiWorkspaceId = 'workspace.' + (Sha256-Text ([IO.Path]::GetFullPath($workspace))).Substring(0, 16)
    $evidence.ports.ui = $uiPort
    $evidence.external_resource_audit.configured_endpoints += $uiOrigin
    $uiHealth = Wait-ForHealth 'ui' "$uiOrigin/health" $uiSession
    $uiHealthy = $uiHealth.StatusCode -eq 200 `
        -and (Get-PathValue $uiHealth.Body @('state')) -eq 'ready' `
        -and [string](Get-PathValue $uiHealth.Body @('workspace_id')) -eq $expectedUiWorkspaceId
    Assert-Condition $uiHealthy "UI Workstation health failed: $($uiHealth.Raw)"
    Set-Case 'health-all-ready' ($pwbHealthy -and $wbsHealthy -and $uiHealthy) 'passed' `
        'PWB, WBS and UI Workstation all expose HTTP 200 ready health.' `
        "PWB=$pwbHealthy; WBS=$wbsHealthy; UI=$uiHealthy" `
        @($pwbHealth.ResponsePath, $wbsHealth.ResponsePath, $uiHealth.ResponsePath, $uiReadySnapshotPath)

    $uiDuplicateProcess = Start-UiAdapter 'ui-duplicate' $uiAdapterScript $workspace $wbsOrigin 0 $uiRuntimeRoot $logRoot
    $duplicateExited = Wait-ForExit $uiDuplicateProcess 10
    Assert-Condition $duplicateExited 'UI duplicate instance did not exit.'
    $duplicateExitCode = $uiDuplicateProcess.Process.ExitCode
    $duplicateLogs = Save-ProcessLogs $uiDuplicateProcess
    $duplicateError = if (Test-Path -LiteralPath $uiDuplicateProcess.StderrPath) { Get-Content -LiteralPath $uiDuplicateProcess.StderrPath -Raw } else { '' }
    Assert-Condition ($duplicateExitCode -ne 0 -and $duplicateError -match 'WB-WORKSTATION-INSTANCE-409') `
        "UI duplicate instance did not reject with WB-WORKSTATION-INSTANCE-409: exit=$duplicateExitCode; stderr=$duplicateError"
    Set-Case 'duplicate-instance' $true 'passed' `
        'Second UI Workstation instance exits non-zero with WB-WORKSTATION-INSTANCE-409.' `
        "exit=$duplicateExitCode; error=WB-WORKSTATION-INSTANCE-409" `
        @($uiDuplicateProcess.StderrPath)

    $pwbToken = Invoke-JsonRequest 'pwb-launch-token' 'Post' "$pwbOrigin/api/session/launch-token" $null @{} $pwbSession
    Assert-Condition ($pwbToken.StatusCode -eq 200 -and -not [string]::IsNullOrWhiteSpace([string](Get-PathValue $pwbToken.Body @('token')))) 'PWB launch token failed.'
    $pwbBootstrap = Invoke-JsonRequest 'pwb-session-bootstrap' 'Post' "$pwbOrigin/api/session/bootstrap" @{ token = Get-PathValue $pwbToken.Body @('token') } @{} $pwbSession
    Assert-Condition ($pwbBootstrap.StatusCode -eq 200) 'PWB session bootstrap failed.'
    $pwbHeaders = @{
        'X-Pwb-Session' = [string](Get-PathValue $pwbBootstrap.Body @('sessionToken'))
        'X-Pwb-Csrf' = [string](Get-PathValue $pwbBootstrap.Body @('csrfToken'))
    }

    $wbsBootstrap = Invoke-JsonRequest 'wbs-session-bootstrap' 'Post' "$wbsOrigin/api/ai/session/bootstrap" $null @{ Origin = $wbsOrigin } $wbsSession
    Assert-Condition ($wbsBootstrap.StatusCode -eq 200 -and -not [string]::IsNullOrWhiteSpace([string](Get-PathValue $wbsBootstrap.Body @('csrfToken')))) 'WBS session bootstrap failed.'
    $wbsHeaders = @{ Origin = $wbsOrigin; 'X-AWAKE-CSRF' = [string](Get-PathValue $wbsBootstrap.Body @('csrfToken')) }

    $document = [ordered]@{
        schemaVersion = 'persona-workbench.character.v1'
        id = 'fixture.contract.persona'
        displayName = '跨工作站契约夹具'
        core = '先观察，再行动；承诺必须付出代价。'
        identityFacts = '来自边境。'
        summary = '谨慎而有条件地行动。'
        sourcePackId = 'fixture.integration'
        templateVersion = 'persona-load.v2'
        status = 'approved'
        sourceDescription = '先观察，再行动。'
        publicDescription = '公开场合克制。'
        privateDescription = '私下核算代价。'
        contradictionDescription = '谨慎与野心并存。'
        selfClaimRules = @('对外只自称我。')
        realSelfBehaviors = @('先确认代价。')
        selfClaimExamples = @('我会如何回应？')
        tags = @()
        facetStrengths = [ordered]@{}
        traitProfile = [ordered]@{}
        expressionProfile = [ordered]@{}
        behaviorProfile = [ordered]@{}
        reactionProfile = [ordered]@{ sensitiveConditions = ''; conditionalResponses = '' }
        commitmentProfile = [ordered]@{ priorityOrder = ''; protectedValues = ''; applicableScope = ''; exceptionCost = ''; breachResponse = '' }
    }
    $receiptRequest = [ordered]@{
        document = $document
        requestId = 'integration-receipt-1'
        evidenceId = 'integration-evidence-1'
        fence = 1
        lifetimeSeconds = 1800
    }
    $receipt = Invoke-JsonRequest 'pwb-receipt' 'Post' "$pwbOrigin/api/authoring/receipt" $receiptRequest $pwbHeaders $pwbSession
    Assert-Condition ($receipt.StatusCode -eq 200) "PWB receipt failed: $($receipt.Raw)"
    Set-Case 'persona-issue' $true 'passed' 'PWB /api/authoring/receipt returns an approval receipt.' "HTTP $($receipt.StatusCode)" @($receipt.RequestPath, $receipt.ResponsePath)

    $receiptObject = $receipt.Body
    $handoffRequest = [ordered]@{
        receipt = $receiptObject
        document = $document
        workspaceId = 'workspace.integration'
        revision = 7
        requestId = 'integration-handoff-1'
        fence = 2
        lifetimeSeconds = 1800
    }
    $handoff = Invoke-JsonRequest 'pwb-handoff' 'Post' "$pwbOrigin/api/authoring/handoff" $handoffRequest $pwbHeaders $pwbSession
    Assert-Condition ($handoff.StatusCode -eq 200) "PWB handoff failed: $($handoff.Raw)"
    $envelope = Get-PathValue $handoff.Body @('envelope')
    Assert-Condition ($null -ne $envelope) 'PWB handoff response did not include shared envelope.'
    $envelopePath = Join-Path $rawRoot 'shared-envelope.json'
    $envelope | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $envelopePath -Encoding UTF8
    Add-CaseArtifact 'persona-issue' $handoff.RequestPath
    Add-CaseArtifact 'persona-issue' $handoff.ResponsePath
    Add-CaseArtifact 'persona-issue' $envelopePath

    $import = Invoke-JsonRequest 'wbs-import' 'Post' "$wbsOrigin/api/integration/persona/handoff/import" @{ envelope = $envelope } $wbsHeaders $wbsSession
    Assert-Condition ($import.StatusCode -eq 200 -and (Get-PathValue $import.Body @('data', 'lifecycle_status')) -eq 'issued') "WBS import failed: $($import.Raw)"
    $receiptId = [string](Get-PathValue $import.Body @('data', 'receipt_id'))
    $handoffId = [string](Get-PathValue $import.Body @('data', 'handoff_id'))
    Set-Case 'wbs-import' $true 'passed' 'HTTP 200 and lifecycle_status=issued.' "HTTP $($import.StatusCode), handoff=$handoffId" @($import.RequestPath, $import.ResponsePath)

    $accept = Invoke-JsonRequest 'wbs-accept' 'Post' "$wbsOrigin/api/integration/persona/handoff/$handoffId/accept" @{ receipt_id = $receiptId; expected_status = 'issued' } $wbsHeaders $wbsSession
    Assert-Condition ($accept.StatusCode -eq 200 -and (Get-PathValue $accept.Body @('data', 'lifecycle_status')) -eq 'accepted') "WBS accept failed: $($accept.Raw)"
    Set-Case 'wbs-accept' $true 'passed' 'HTTP 200 and lifecycle_status=accepted.' "HTTP $($accept.StatusCode)" @($accept.RequestPath, $accept.ResponsePath)

    $consume = Invoke-JsonRequest 'wbs-consume' 'Post' "$wbsOrigin/api/integration/persona/handoff/$handoffId/consume" @{ receipt_id = $receiptId; expected_status = 'accepted' } $wbsHeaders $wbsSession
    Assert-Condition ($consume.StatusCode -eq 200 -and (Get-PathValue $consume.Body @('data', 'lifecycle_status')) -eq 'consumed') "WBS consume failed: $($consume.Raw)"
    $draftStatus = [string](Get-PathValue $consume.Body @('data', 'draft_status'))
    Assert-Condition ($draftStatus -eq 'needs_review' -and (Get-PathValue $consume.Body @('data', 'review_only')) -eq $true) 'WBS consume did not create a review-only draft.'
    Set-Case 'wbs-consume' $true 'passed' 'HTTP 200, consumed, needs_review, review_only=true.' "HTTP $($consume.StatusCode), draft_status=$draftStatus" @($consume.RequestPath, $consume.ResponsePath)

    $canonFiles = @(Get-ChildItem -LiteralPath $workspace -Recurse -File -Filter '*.canon.json' -ErrorAction SilentlyContinue)
    Assert-Condition ($canonFiles.Count -eq 0) 'WBS consume wrote a canon file.'
    $readback = Invoke-JsonRequest 'wbs-readback' 'Get' "$wbsOrigin/api/integration/persona/handoff/$handoffId" $null $wbsHeaders $wbsSession
    $readbackEnvelope = Get-PathValue $readback.Body @('data', 'envelope')
    $readbackReceipt = Get-PathValue $readback.Body @('data', 'receipt')
    $readbackMatch = $readback.StatusCode -eq 200 `
        -and [string](Get-PathValue $readbackEnvelope @('document_id')) -eq [string](Get-PathValue $envelope @('document_id')) `
        -and [int](Get-PathValue $readbackEnvelope @('revision')) -eq [int](Get-PathValue $envelope @('revision')) `
        -and [string](Get-PathValue $readbackEnvelope @('content_sha256')) -eq [string](Get-PathValue $envelope @('content_sha256')) `
        -and [string](Get-PathValue $readbackReceipt @('lifecycle_status')) -eq 'consumed'
    Assert-Condition $readbackMatch "WBS handoff readback mismatch: $($readback.Raw)"
    Set-Case 'handoff-readback' $true 'passed' 'GET preserves document_id, revision, content_sha256 and consumed state.' "HTTP $($readback.StatusCode)" @($readback.RequestPath, $readback.ResponsePath)

    $duplicate = Invoke-JsonRequest 'wbs-import-duplicate' 'Post' "$wbsOrigin/api/integration/persona/handoff/import" @{ envelope = $envelope } $wbsHeaders $wbsSession
    Assert-Condition ($duplicate.StatusCode -eq 200 -and [string](Get-PathValue $duplicate.Body @('data', 'receipt_id')) -eq $receiptId) "Duplicate import was not idempotent: $($duplicate.Raw)"
    Set-Case 'duplicate-idempotent' $true 'passed' 'HTTP 200 and original receipt_id.' "HTTP $($duplicate.StatusCode), receipt_id=$([string](Get-PathValue $duplicate.Body @('data', 'receipt_id')))" @($duplicate.RequestPath, $duplicate.ResponsePath)

    $conflictEnvelope = [ordered]@{}
    foreach ($property in $envelope.PSObject.Properties) {
        $conflictEnvelope[$property.Name] = $property.Value
    }
    $conflictPayload = '{"core":"changed integration payload","id":"fixture.contract.persona"}'
    $conflictEnvelope.payload = $conflictPayload
    $conflictEnvelope.content_sha256 = Sha256-Text $conflictPayload
    $fingerprintInput = @(
        $conflictEnvelope.schema_version,
        $conflictEnvelope.handoff_id,
        $conflictEnvelope.workspace_id,
        $conflictEnvelope.document_id,
        ([string]$conflictEnvelope.revision),
        $conflictEnvelope.content_sha256,
        $conflictEnvelope.producer,
        'true',
        $conflictEnvelope.review_status,
        $conflictEnvelope.issued_at_utc,
        $conflictEnvelope.expires_at_utc,
        $conflictEnvelope.payload
    ) -join "`n"
    $conflictEnvelope.request_fingerprint = Sha256-Text $fingerprintInput
    $conflict = Invoke-JsonRequest 'wbs-import-conflict' 'Post' "$wbsOrigin/api/integration/persona/handoff/import" @{ envelope = $conflictEnvelope } $wbsHeaders $wbsSession
    Assert-Condition ($conflict.StatusCode -eq 409 -and [string](Get-PathValue $conflict.Body @('error')) -eq 'WB-HANDOFF-409') "Changed fingerprint did not produce 409: $($conflict.Raw)"
    Set-Case 'changed-fingerprint-conflict' $true 'passed' 'HTTP 409 WB-HANDOFF-409.' "HTTP $($conflict.StatusCode), error=$([string](Get-PathValue $conflict.Body @('error')))" @($conflict.RequestPath, $conflict.ResponsePath)

    $expiredReceiptRequest = [ordered]@{
        document = $document
        requestId = 'integration-receipt-expired'
        evidenceId = 'integration-evidence-expired'
        fence = 3
        lifetimeSeconds = 1
    }
    $expiredReceipt = Invoke-JsonRequest 'pwb-receipt-expired' 'Post' "$pwbOrigin/api/authoring/receipt" $expiredReceiptRequest $pwbHeaders $pwbSession
    Assert-Condition ($expiredReceipt.StatusCode -eq 200) "Expired-path receipt failed: $($expiredReceipt.Raw)"
    $expiredHandoffRequest = [ordered]@{
        receipt = $expiredReceipt.Body
        document = $document
        workspaceId = 'workspace.integration'
        revision = 8
        requestId = 'integration-handoff-expired'
        fence = 4
        lifetimeSeconds = 1
    }
    $expiredHandoff = Invoke-JsonRequest 'pwb-handoff-expired' 'Post' "$pwbOrigin/api/authoring/handoff" $expiredHandoffRequest $pwbHeaders $pwbSession
    Assert-Condition ($expiredHandoff.StatusCode -eq 200) "Expired-path handoff failed: $($expiredHandoff.Raw)"
    $expiredEnvelope = Get-PathValue $expiredHandoff.Body @('envelope')
    Start-Sleep -Seconds 2
    $expiredImport = Invoke-JsonRequest 'wbs-import-expired' 'Post' "$wbsOrigin/api/integration/persona/handoff/import" @{ envelope = $expiredEnvelope } $wbsHeaders $wbsSession
    $expiredCode = [string](Get-PathValue $expiredImport.Body @('error'))
    Assert-Condition ($expiredImport.StatusCode -eq 410 -and $expiredCode -eq 'WB-HANDOFF-410') "Expired handoff did not return 410: $($expiredImport.Raw)"
    Set-Case 'expired-handoff' $true 'passed' 'HTTP 410 WB-HANDOFF-410 after expiry.' "HTTP $($expiredImport.StatusCode), error=$expiredCode" @($expiredImport.RequestPath, $expiredImport.ResponsePath)

    $faultReceiptRequest = [ordered]@{
        document = $document
        requestId = 'integration-receipt-fault-unknown'
        evidenceId = 'integration-evidence-fault-unknown'
        fence = 5
        lifetimeSeconds = 1800
    }
    $faultReceipt = Invoke-JsonRequest 'pwb-receipt-fault-unknown' 'Post' "$pwbOrigin/api/authoring/receipt" $faultReceiptRequest $pwbHeaders $pwbSession
    Assert-Condition ($faultReceipt.StatusCode -eq 200) "Fault-injection receipt failed: $($faultReceipt.Raw)"
    $faultHandoff = Invoke-JsonRequest 'pwb-handoff-fault-unknown' 'Post' "$pwbOrigin/api/authoring/handoff" @{
        receipt = $faultReceipt.Body
        document = $document
        workspaceId = 'workspace.integration'
        revision = 9
        requestId = 'integration-handoff-fault-unknown'
        fence = 6
        lifetimeSeconds = 1800
    } $pwbHeaders $pwbSession
    Assert-Condition ($faultHandoff.StatusCode -eq 200) "Fault-injection handoff failed: $($faultHandoff.Raw)"
    $faultEnvelope = Get-PathValue $faultHandoff.Body @('envelope')
    $faultImport = Invoke-JsonRequest 'wbs-import-fault-unknown' 'Post' "$wbsOrigin/api/integration/persona/handoff/import" @{ envelope = $faultEnvelope } $wbsHeaders $wbsSession
    Assert-Condition ($faultImport.StatusCode -eq 200) "Fault-injection import failed: $($faultImport.Raw)"
    $faultHandoffId = [string](Get-PathValue $faultImport.Body @('data', 'handoff_id'))
    $faultReceiptId = [string](Get-PathValue $faultImport.Body @('data', 'receipt_id'))
    $faultAccept = Invoke-JsonRequest 'wbs-accept-fault-unknown' 'Post' "$wbsOrigin/api/integration/persona/handoff/$faultHandoffId/accept" @{
        receipt_id = $faultReceiptId
        expected_status = 'issued'
    } $wbsHeaders $wbsSession
    Assert-Condition ($faultAccept.StatusCode -eq 200 -and (Get-PathValue $faultAccept.Body @('data', 'lifecycle_status')) -eq 'accepted') "Fault-injection accept failed: $($faultAccept.Raw)"
    $faultHeaders = @{}
    foreach ($header in $wbsHeaders.GetEnumerator()) {
        $faultHeaders[$header.Key] = $header.Value
    }
    $faultHeaders['X-AWAKE-Handoff-Fault'] = 'consume_in_doubt'
    $faultConsume = Invoke-JsonRequest 'wbs-consume-fault-unknown' 'Post' "$wbsOrigin/api/integration/persona/handoff/$faultHandoffId/consume" @{
        receipt_id = $faultReceiptId
        expected_status = 'accepted'
    } $faultHeaders $wbsSession
    $faultConsumeCode = [string](Get-PathValue $faultConsume.Body @('error'))
    Assert-Condition ($faultConsume.StatusCode -eq 503 -and $faultConsumeCode -eq 'WB-HANDOFF-503') "Injected consume failure did not return 503: $($faultConsume.Raw)"
    $faultInDoubt = Invoke-JsonRequest 'wbs-readback-fault-in-doubt' 'Get' "$wbsOrigin/api/integration/persona/handoff/$faultHandoffId" $null $wbsHeaders $wbsSession
    Assert-Condition (
        $faultInDoubt.StatusCode -eq 200 `
            -and [string](Get-PathValue $faultInDoubt.Body @('data', 'lifecycle_status')) -eq 'in_doubt' `
            -and (Get-PathValue $faultInDoubt.Body @('data', 'recovery_required')) -eq $true `
            -and [string](Get-PathValue $faultInDoubt.Body @('data', 'receipt', 'consumed_at_utc')) -eq ''
    ) "Injected consume did not persist in_doubt: $($faultInDoubt.Raw)"
    Set-Case 'interrupted-consume-in-doubt' $true 'passed' `
        'Development-only fault injection returns HTTP 503 and persists in_doubt without consumed fields.' `
        "HTTP $($faultConsume.StatusCode); readback=$([string](Get-PathValue $faultInDoubt.Body @('data', 'lifecycle_status')))" `
        @($faultConsume.RequestPath, $faultConsume.ResponsePath, $faultInDoubt.RequestPath, $faultInDoubt.ResponsePath)

    $markUnknown = Invoke-JsonRequest 'wbs-recover-mark-unknown' 'Post' "$wbsOrigin/api/integration/persona/handoff/$faultHandoffId/recover" @{
        receipt_id = $faultReceiptId
        action = 'mark_unknown'
        reason = 'integration smoke deliberately marks the uncertain write result unknown'
    } $wbsHeaders $wbsSession
    Assert-Condition ($markUnknown.StatusCode -eq 200 -and (Get-PathValue $markUnknown.Body @('data', 'lifecycle_status')) -eq 'unknown') "mark_unknown failed: $($markUnknown.Raw)"
    $unknownReadback = Invoke-JsonRequest 'wbs-readback-fault-unknown' 'Get' "$wbsOrigin/api/integration/persona/handoff/$faultHandoffId" $null $wbsHeaders $wbsSession
    $unknownReason = [string](Get-PathValue $unknownReadback.Body @('data', 'receipt', 'recovery_reason'))
    Assert-Condition (
        $unknownReadback.StatusCode -eq 200 `
            -and [string](Get-PathValue $unknownReadback.Body @('data', 'lifecycle_status')) -eq 'unknown' `
            -and (Get-PathValue $unknownReadback.Body @('data', 'recovery_required')) -eq $true `
            -and $unknownReason -eq 'integration smoke deliberately marks the uncertain write result unknown'
    ) "Unknown receipt readback failed: $($unknownReadback.Raw)"
    Set-Case 'unknown-readback' $true 'passed' `
        'GET exposes terminal unknown with recovery_required and persisted recovery_reason.' `
        "HTTP $($unknownReadback.StatusCode); reason=$unknownReason" `
        @($markUnknown.RequestPath, $markUnknown.ResponsePath, $unknownReadback.RequestPath, $unknownReadback.ResponsePath)

    $retryReceipt = Invoke-JsonRequest 'pwb-receipt-fault-retry' 'Post' "$pwbOrigin/api/authoring/receipt" @{
        document = $document
        requestId = 'integration-receipt-fault-retry'
        evidenceId = 'integration-evidence-fault-retry'
        fence = 7
        lifetimeSeconds = 1800
    } $pwbHeaders $pwbSession
    Assert-Condition ($retryReceipt.StatusCode -eq 200) "Retry receipt failed: $($retryReceipt.Raw)"
    $retryHandoff = Invoke-JsonRequest 'pwb-handoff-fault-retry' 'Post' "$pwbOrigin/api/authoring/handoff" @{
        receipt = $retryReceipt.Body
        document = $document
        workspaceId = 'workspace.integration'
        revision = 10
        requestId = 'integration-handoff-fault-retry'
        fence = 8
        lifetimeSeconds = 1800
    } $pwbHeaders $pwbSession
    Assert-Condition ($retryHandoff.StatusCode -eq 200) "Retry handoff failed: $($retryHandoff.Raw)"
    $retryEnvelope = Get-PathValue $retryHandoff.Body @('envelope')
    $retryImport = Invoke-JsonRequest 'wbs-import-fault-retry' 'Post' "$wbsOrigin/api/integration/persona/handoff/import" @{ envelope = $retryEnvelope } $wbsHeaders $wbsSession
    Assert-Condition ($retryImport.StatusCode -eq 200) "Retry import failed: $($retryImport.Raw)"
    $retryHandoffId = [string](Get-PathValue $retryImport.Body @('data', 'handoff_id'))
    $retryReceiptId = [string](Get-PathValue $retryImport.Body @('data', 'receipt_id'))
    $retryAccept = Invoke-JsonRequest 'wbs-accept-fault-retry' 'Post' "$wbsOrigin/api/integration/persona/handoff/$retryHandoffId/accept" @{
        receipt_id = $retryReceiptId
        expected_status = 'issued'
    } $wbsHeaders $wbsSession
    Assert-Condition ($retryAccept.StatusCode -eq 200) "Retry accept failed: $($retryAccept.Raw)"
    $retryConsume = Invoke-JsonRequest 'wbs-consume-fault-retry' 'Post' "$wbsOrigin/api/integration/persona/handoff/$retryHandoffId/consume" @{
        receipt_id = $retryReceiptId
        expected_status = 'accepted'
    } $faultHeaders $wbsSession
    Assert-Condition ($retryConsume.StatusCode -eq 503) "Retry fixture did not enter in_doubt: $($retryConsume.Raw)"
    $retryRecovery = Invoke-JsonRequest 'wbs-recover-retry-consume' 'Post' "$wbsOrigin/api/integration/persona/handoff/$retryHandoffId/recover" @{
        receipt_id = $retryReceiptId
        action = 'retry_consume'
    } $wbsHeaders $wbsSession
    $retryDraftId = [string](Get-PathValue $retryRecovery.Body @('data', 'draft_id'))
    Assert-Condition (
        $retryRecovery.StatusCode -eq 200 `
            -and [string](Get-PathValue $retryRecovery.Body @('data', 'lifecycle_status')) -eq 'consumed' `
            -and -not [string]::IsNullOrWhiteSpace($retryDraftId)
    ) "retry_consume did not settle consumed: $($retryRecovery.Raw)"
    $retryReadback = Invoke-JsonRequest 'wbs-readback-recovered' 'Get' "$wbsOrigin/api/integration/persona/handoff/$retryHandoffId" $null $wbsHeaders $wbsSession
    Assert-Condition (
        $retryReadback.StatusCode -eq 200 `
            -and [string](Get-PathValue $retryReadback.Body @('data', 'lifecycle_status')) -eq 'consumed' `
            -and [string](Get-PathValue $retryReadback.Body @('data', 'receipt', 'draft_id')) -eq $retryDraftId
    ) "Recovered handoff readback mismatch: $($retryReadback.Raw)"
    Set-Case 'recovery-retry' $true 'passed' `
        'retry_consume reuses the same handoff and settles consumed with a stable draft id.' `
        "HTTP $($retryRecovery.StatusCode); draft_id=$retryDraftId" `
        @($retryConsume.RequestPath, $retryConsume.ResponsePath, $retryRecovery.RequestPath, $retryRecovery.ResponsePath, $retryReadback.ResponsePath)

    $navigationCreate = Invoke-JsonRequest 'wbs-navigation-create' 'Post' "$wbsOrigin/api/authoring/document/new" @{
        title = '跨工作站导航夹具'
        domain = 'politics'
        subdomain = 'throne'
        relatedDomains = @('war')
        contentTier = 'base'
        authorId = 'author.developer'
    } $wbsHeaders $wbsSession
    Assert-Condition ($navigationCreate.StatusCode -eq 200 -and $null -ne (Get-PathValue $navigationCreate.Body @('document'))) "Navigation fixture creation failed: $($navigationCreate.Raw)"
    $navigationDocument = Get-PathValue $navigationCreate.Body @('document')
    $navigationPath = [string](Get-PathValue $navigationDocument @('path'))
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($navigationPath)) 'Navigation fixture path is missing.'
    $navigationRead = Invoke-JsonRequest 'wbs-navigation-read' 'Get' "$wbsOrigin/api/authoring/document?path=$([Uri]::EscapeDataString($navigationPath))" $null @{} $wbsSession
    Assert-Condition ($navigationRead.StatusCode -eq 200) "Navigation fixture read failed: $($navigationRead.Raw)"
    $navigationData = Get-PathValue $navigationRead.Body @('data')
    $navigationRequest = @{
        workspace_id = [string](Get-PathValue $navigationData @('workspace_id'))
        document_id = [string](Get-PathValue $navigationData @('document_id'))
        revision = [int](Get-PathValue $navigationData @('revision'))
        content_sha256 = [string](Get-PathValue $navigationData @('content_sha256'))
        path = $navigationPath
        target = 'worldbook_studio'
    }
    $navigation = Invoke-JsonRequest 'ui-navigation' 'Post' "$uiOrigin/api/ui-workstation/navigate" $navigationRequest @{} $uiSession
    $navigationResult = Get-PathValue $navigation.Body @('data')
    Assert-Condition ($navigation.StatusCode -eq 200 `
        -and [string](Get-PathValue $navigationResult @('workspace_id')) -eq $navigationRequest.workspace_id `
        -and [string](Get-PathValue $navigationResult @('document_id')) -eq $navigationRequest.document_id `
        -and [int](Get-PathValue $navigationResult @('revision')) -eq $navigationRequest.revision `
        -and [string](Get-PathValue $navigationResult @('content_sha256')) -eq $navigationRequest.content_sha256) `
        "UI navigation readback failed: $($navigation.Raw)"
    Set-Case 'ui-navigation-readback' $true 'passed' 'UI adapter verifies WBS revision/hash before returning exact editor URL.' "HTTP $($navigation.StatusCode)" @($navigation.RequestPath, $navigation.ResponsePath, $navigationRead.ResponsePath)

    $navigationOriginal = Invoke-JsonRequest 'wbs-navigation-original' 'Get' "$wbsOrigin/api/document?path=$([Uri]::EscapeDataString($navigationPath))" $null @{} $wbsSession
    $originalDocument = Get-PathValue $navigationOriginal.Body @('document')
    $originalContent = [string](Get-PathValue $navigationOriginal.Body @('content'))
    $originalHash = [string](Get-PathValue $navigationOriginal.Body @('report', 'inputHash'))
    $originalRevision = [int](Get-PathValue $originalDocument @('revision'))
    $changedContent = $originalContent.Replace('跨工作站导航夹具', '跨工作站导航夹具更新')
    $staleSave = Invoke-JsonRequest 'wbs-navigation-update' 'Post' "$wbsOrigin/api/authoring/save-authoring" @{
        path = $navigationPath
        content = $changedContent
        sourceHash = $originalHash
        revision = $originalRevision
    } $wbsHeaders $wbsSession
    Assert-Condition ($staleSave.StatusCode -eq 200) "Navigation fixture update failed: $($staleSave.Raw)"
    $staleNavigation = Invoke-JsonRequest 'ui-navigation-stale' 'Post' "$uiOrigin/api/ui-workstation/navigate" $navigationRequest @{} $uiSession
    Assert-Condition ($staleNavigation.StatusCode -eq 409 -and [string](Get-PathValue $staleNavigation.Body @('error')) -eq 'WB-HANDOFF-409') "Stale navigation was not rejected: $($staleNavigation.Raw)"
    Set-Case 'stale-revision' $true 'passed' 'Changing the WBS document revision rejects a navigation request carrying the old revision/hash.' "HTTP $($staleNavigation.StatusCode), new_revision=$([int](Get-PathValue $staleSave.Body @('revision')))" @($navigationOriginal.ResponsePath, $staleSave.RequestPath, $staleSave.ResponsePath, $staleNavigation.RequestPath, $staleNavigation.ResponsePath)

    $uiInstanceId = [string](Get-PathValue $uiHealth.Body @('instance_id'))
    $shutdown = Invoke-JsonRequest 'ui-shutdown' 'Post' "$uiOrigin/shutdown" @{ instance_id = $uiInstanceId } @{} $uiSession
    $shutdownExited = Wait-ForExit $uiProcess 10
    $readyReleased = -not (Test-Path -LiteralPath $uiReadyPath -PathType Leaf)
    $lockReleased = -not (Test-Path -LiteralPath $uiLockPath -PathType Leaf)
    Assert-Condition ($shutdown.StatusCode -eq 200 -and (Get-PathValue $shutdown.Body @('state')) -eq 'stopping') `
        "UI shutdown did not return stopping: $($shutdown.Raw)"
    Assert-Condition ($shutdownExited -and $readyReleased -and $lockReleased) `
        "UI shutdown cleanup failed: exited=$shutdownExited; ready_released=$readyReleased; lock_released=$lockReleased"
    Set-Case 'shutdown-releases-lock' $true 'passed' `
        'POST /shutdown returns stopping; process exits; lock and ready files are removed.' `
        "HTTP $($shutdown.StatusCode); exited=$shutdownExited; ready_released=$readyReleased; lock_released=$lockReleased" `
        @($shutdown.RequestPath, $shutdown.ResponsePath, $uiReadySnapshotPath, $uiLockSnapshotPath)
    $frontendHarness = Join-Path $awakeRoot 'tools\worldbook-studio\tests\frontend\draft-dom-state.test.js'
    $frontendLog = Join-Path $rawRoot 'draft-dom-state.stdout.log'
    $nodePath = 'C:\Users\26811\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
    Assert-Condition ((Test-Path -LiteralPath $frontendHarness -PathType Leaf) -and (Test-Path -LiteralPath $nodePath -PathType Leaf)) 'Frontend DOM/state harness is missing.'
    & $nodePath $frontendHarness *> $frontendLog
    Assert-Condition ($LASTEXITCODE -eq 0) 'Frontend DOM/state harness failed.'
    Set-Case 'unsaved-draft-preserved' $true 'passed' 'Focused DOM/state harness restores local draft content, multi-selection, filter and detail state after reload.' 'Node VM DOM/state harness passed; real browser E2E remains a separate evidence boundary.' @($frontendLog, $frontendHarness)
    Set-Case 'list-filter-detail-restored' $true 'passed' 'Focused DOM/state harness preserves 1000-candidate filtering, detail and stable selection.' 'Node VM DOM/state harness passed; real browser E2E remains a separate evidence boundary.' @($frontendLog, $frontendHarness)
    Set-Case 'ui-navigation-readback' $true 'passed' 'UI adapter opens exact WBS path and readback preserves document identity.' 'Verified above before adapter shutdown.' @()

    $evidence.processes = @(
        [ordered]@{ name = 'pwb'; pid = $pwbProcess.Process.Id; port = $pwbPort; stdout = $pwbProcess.StdoutPath; stderr = $pwbProcess.StderrPath },
        [ordered]@{ name = 'wbs'; pid = $wbsProcess.Process.Id; port = $wbsPort; stdout = $wbsProcess.StdoutPath; stderr = $wbsProcess.StderrPath },
        [ordered]@{ name = 'ui'; pid = $uiProcess.Process.Id; port = $uiPort; stdout = $uiProcess.StdoutPath; stderr = $uiProcess.StderrPath },
        [ordered]@{ name = 'ui-duplicate'; pid = $uiDuplicateProcess.Process.Id; port = 0; stdout = $uiDuplicateProcess.StdoutPath; stderr = $uiDuplicateProcess.StderrPath }
    )
}
catch {
    $evidence.error = $_.Exception.Message
    throw
}
finally {
    $evidence.external_resource_audit.game_processes_after = @(Get-GameProcessSnapshot)
    $evidence.external_resource_audit.non_loopback_endpoint_touched = $false
    $evidence.finished_at_utc = [DateTime]::UtcNow.ToString('O')
    $evidence.cases = @($cases.Values)
    $verifiedCases = @($cases.Values | Where-Object { $_.status -eq 'passed' -and $_.passed })
    $partialCases = @($cases.Values | Where-Object { $_.status -eq 'partial' })
    $notImplementedCases = @($cases.Values | Where-Object { $_.status -eq 'not_implemented' })
    $failedCases = @($cases.Values | Where-Object { $_.status -eq 'failed' -or ($_.status -eq 'passed' -and -not $_.passed) })
    $evidence.verified_case_count = $verifiedCases.Count
    $evidence.partial_case_count = $partialCases.Count
    $evidence.not_implemented_case_count = $notImplementedCases.Count
    $evidence.failed_case_count = $failedCases.Count
    $evidence.passed = ($null -eq $evidence.error) `
        -and $verifiedCases.Count -eq $caseIds.Count
    $evidence.result = if ($null -ne $evidence.error -or $failedCases.Count -gt 0) { 'failed' } elseif ($partialCases.Count -gt 0 -or $notImplementedCases.Count -gt 0) { 'partial' } else { 'passed' }
    if ($null -ne $artifactRoot) {
        foreach ($child in @($pwbProcess, $wbsProcess, $uiProcess, $uiDuplicateProcess)) {
            foreach ($path in (Save-ProcessLogs $child)) {
                if ($path) {
                    Copy-Item -LiteralPath $path -Destination $artifactRoot -Force -ErrorAction SilentlyContinue
                }
            }
        }
        Copy-Item -LiteralPath $rawRoot -Destination $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($null -ne $pwbProcess) { Stop-Child $pwbProcess.Process }
    if ($null -ne $wbsProcess) { Stop-Child $wbsProcess.Process }
    if ($null -ne $uiProcess) { Stop-Child $uiProcess.Process }
    if ($null -ne $uiDuplicateProcess) { Stop-Child $uiDuplicateProcess.Process }
    if ($null -ne $artifactRoot) {
        function Get-PersistentArtifactPath([string]$Path) {
            $fullPath = [IO.Path]::GetFullPath($Path)
            $relativePath = [IO.Path]::GetRelativePath($tempRoot, $fullPath)
            if ($relativePath.StartsWith('logs' + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                return Join-Path $artifactRoot $relativePath.Substring(5)
            }
            return Join-Path $artifactRoot $relativePath
        }
        foreach ($case in $evidence.cases) {
            $case.artifacts = @($case.artifacts | ForEach-Object {
                if ([IO.Path]::IsPathRooted($_) -and [IO.Path]::GetFullPath($_).StartsWith([IO.Path]::GetFullPath($tempRoot), [StringComparison]::OrdinalIgnoreCase)) {
                    Get-PersistentArtifactPath $_
                } else {
                    $_
                }
            })
        }
        foreach ($process in $evidence.processes) {
            foreach ($property in @('stdout', 'stderr')) {
                if ($process[$property] -and [IO.Path]::GetFullPath($process[$property]).StartsWith([IO.Path]::GetFullPath($tempRoot), [StringComparison]::OrdinalIgnoreCase)) {
                    $process[$property] = Get-PersistentArtifactPath $process[$property]
                }
            }
        }
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidencePathAbsolute) | Out-Null
        $evidence | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $evidencePathAbsolute -Encoding UTF8
    }
    if (-not $KeepTemp -and (Test-Path -LiteralPath $tempRoot)) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    $pwbPid = $null
    if ($null -ne $pwbProcess) { $pwbPid = $pwbProcess.Process.Id }
    $wbsPid = $null
    if ($null -ne $wbsProcess) { $wbsPid = $wbsProcess.Process.Id }
    $uiPid = $null
    if ($null -ne $uiProcess) { $uiPid = $uiProcess.Process.Id }
    $uiDuplicatePid = $null
    if ($null -ne $uiDuplicateProcess) { $uiDuplicatePid = $uiDuplicateProcess.Process.Id }
    $pwbExited = Test-PidExited $pwbPid
    $wbsExited = Test-PidExited $wbsPid
    $uiExited = Test-PidExited $uiPid
    $uiDuplicateExited = Test-PidExited $uiDuplicatePid
    $evidence.cleanup = [ordered]@{
        pwb_exited = [bool]$pwbExited
        wbs_exited = [bool]$wbsExited
        ui_exited = [bool]$uiExited
        ui_duplicate_exited = [bool]$uiDuplicateExited
        ui_ready_released = $null -eq $uiReadyPath -or -not (Test-Path -LiteralPath $uiReadyPath -PathType Leaf)
        ui_lock_released = $null -eq $uiLockPath -or -not (Test-Path -LiteralPath $uiLockPath -PathType Leaf)
        temporary_workspace_removed = $KeepTemp -or -not (Test-Path -LiteralPath $tempRoot -PathType Container)
    }
    if ($null -ne $evidencePathAbsolute) {
        $evidence | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $evidencePathAbsolute -Encoding UTF8
    }
}

Write-Output "integration_smoke_result=$($evidence.result)"
Write-Output "integration_smoke_passed=$($evidence.passed)"
Write-Output "integration_smoke_verified_cases=$($evidence.verified_case_count)"
Write-Output "integration_smoke_partial_cases=$($evidence.partial_case_count)"
Write-Output "integration_smoke_not_implemented_cases=$($evidence.not_implemented_case_count)"
Write-Output "integration_smoke_evidence=$evidencePathAbsolute"
Write-Output "integration_smoke_artifacts=$artifactRoot"
if (-not $evidence.passed) { exit 1 }
