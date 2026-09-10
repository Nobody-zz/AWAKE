param(
    [Parameter(Mandatory = $true)][string]$InputReportPath,
    [Parameter(Mandatory = $true)][string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'persona-awake-joint.ps1')

$reportPathFull = $null
$assertions = [Collections.Generic.List[object]]::new()
$errors = [Collections.Generic.List[object]]::new()
$report = [ordered]@{
    schemaVersion = 'awake.persona.g3-s0-focused-evidence-verification.v1'
    commandLine = 'pwsh -NoProfile -File "' + [IO.Path]::GetFullPath($PSCommandPath) + '" -InputReportPath "' + $InputReportPath + '" -ReportPath "' + $ReportPath + '"'
    normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
    inputReportPath = $null
    inputReportSha256 = $null
    scopePath = $null
    scopeSha256 = $null
    status = 'error'
    exitCode = 40
    assertions = @()
    observedErrors = @()
}

function Add-FocusedAssertion([string]$Id, [bool]$Passed, [string]$Detail) {
    $script:assertions.Add([ordered]@{ id = $Id; passed = $Passed; detail = $Detail })
    if (-not $Passed) {
        $script:errors.Add([ordered]@{
            schemaVersion = 'awake.persona.adapter-error.v1'
            errorId = Get-JointStableHashId 'error' ('persona.g3_s0_focused_invalid|' + $Id + '|' + $Detail)
            code = 'persona.g3_s0_focused_invalid'
            stage = 'g3_s0_focused_evidence'
            artifactId = 'g3_s0_focused_evidence'
            path = '$.' + $Id
            detail = $Detail
            retryable = $false
        })
    }
}

function Get-String([object]$Value, [string]$Name) {
    $property = Get-JointJsonProperty $Value $Name
    if ($null -eq $property) { return '' }
    return [string]$property
}

function Test-IsoUtcTimestamp([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value) -or -not $Value.EndsWith('Z', [StringComparison]::Ordinal)) { return $false }
    try { [void][DateTimeOffset]::Parse($Value, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind); return $true } catch { return $false }
}

try {
    $reportPathFull = Assert-JointOutputPath $ReportPath 'ReportPath'
    $scopePath = Assert-JointReadablePath (Join-Path $script:JointAwakeRoot 'docs\persona-awake-joint-g3-s0-scope.v1.json') $script:JointAwakeRoot 'G3-S0 scope'
    $scopeRead = Read-JointJsonFile $scopePath 'G3-S0 scope'
    $scope = $scopeRead.Value
    $report.scopePath = $scopePath
    $report.scopeSha256 = $scopeRead.RawSha256

    $declaredEvidence = Get-JointJsonProperty $scope 'requiredEvidence'
    $declaredRelative = Get-String $declaredEvidence 'focusedReadinessReport'
    $declaredTraceRelative = Get-String $declaredEvidence 'focusedReadinessTrace'
    if ([string]::IsNullOrWhiteSpace($declaredRelative)) { Throw-JointReject 'persona.g3_s0_focused_path_missing' 'g3_s0_focused_evidence' '$.requiredEvidence.focusedReadinessReport' 'Scope does not declare the focused readiness report.' }
    if ([string]::IsNullOrWhiteSpace($declaredTraceRelative)) { Throw-JointReject 'persona.g3_s0_focused_path_missing' 'g3_s0_focused_evidence' '$.requiredEvidence.focusedReadinessTrace' 'Scope does not declare the focused readiness trace.' }
    $inputFull = Assert-JointReadablePath (Get-JointFullPath $InputReportPath) $script:JointWorkspaceRoot 'focused readiness input'
    $report.inputReportPath = $inputFull
    $report.inputReportSha256 = Get-JointHashFile $inputFull
    $declaredFull = Get-JointFullPath $declaredRelative $script:JointAwakeRoot
    Add-FocusedAssertion 'focused_report_path_bound' ([StringComparer]::OrdinalIgnoreCase.Equals($inputFull, $declaredFull)) 'Focused readiness report path must match the scope declaration.'

    $inputRead = Read-JointJsonFile $inputFull 'focused readiness report'
    $input = $inputRead.Value
    $inputShapeValid = $true
    try {
        Assert-JointExactFields $input @('schemaVersion','taskId','batchId','gate','scopeRevision','scopeSha256','status','exitCode','execution','tracePath','traceSha256','sourceBindings','sideEffects') @('schemaVersion','taskId','batchId','gate','scopeRevision','scopeSha256','status','exitCode','execution','tracePath','traceSha256','sourceBindings','sideEffects') '$.focusedReport'
        Assert-JointExactFields (Get-JointJsonProperty $input 'execution') @('commandLine','processExitCode','traceGeneratedAt') @('commandLine','processExitCode','traceGeneratedAt') '$.focusedReport.execution'
        Assert-JointExactFields (Get-JointJsonProperty $input 'sideEffects') @('launchGame','syncGameDirectory','mutateFrozenCandidate','publish') @('launchGame','syncGameDirectory','mutateFrozenCandidate','publish') '$.focusedReport.sideEffects'
        foreach ($binding in @((Get-JointJsonProperty $input 'sourceBindings'))) { Assert-JointExactFields $binding @('path','sha256') @('path','sha256') '$.focusedReport.sourceBindings[]' }
    } catch { $inputShapeValid = $false }
    Add-FocusedAssertion 'focused_report_exact_shape' $inputShapeValid 'Focused report must reject unknown or missing fields.'
    $traceRelative = Get-String $input 'tracePath'
    $traceSha256 = Get-String $input 'traceSha256'
    $traceFull = Get-JointFullPath $declaredTraceRelative $script:JointAwakeRoot
    Add-FocusedAssertion 'focused_trace_path_bound' (($traceRelative -eq $declaredTraceRelative) -and (Test-Path -LiteralPath $traceFull -PathType Leaf)) 'Focused report must point to the declared raw trace.'
    Add-FocusedAssertion 'focused_trace_hash_bound' (($traceSha256 -match '^[A-F0-9]{64}$') -and ((Get-JointHashFile $traceFull) -eq $traceSha256)) 'Focused report must bind the raw trace bytes by SHA-256.'
    $traceRead = Read-JointJsonFile $traceFull 'focused readiness trace'
    $trace = $traceRead.Value
    $traceShapeValid = $true
    try {
        Assert-JointExactFields $trace @('schemaVersion','taskId','batchId','gate','scopeRevision','scopeSha256','commandLine','processExitCode','sourceBindings','events','sideEffects') @('schemaVersion','taskId','batchId','gate','scopeRevision','scopeSha256','commandLine','processExitCode','sourceBindings','events','sideEffects') '$.focusedTrace'
        Assert-JointExactFields (Get-JointJsonProperty $trace 'sideEffects') @('launchGame','syncGameDirectory','mutateFrozenCandidate','publish') @('launchGame','syncGameDirectory','mutateFrozenCandidate','publish') '$.focusedTrace.sideEffects'
        foreach ($binding in @((Get-JointJsonProperty $trace 'sourceBindings'))) { Assert-JointExactFields $binding @('path','sha256') @('path','sha256') '$.focusedTrace.sourceBindings[]' }
        foreach ($event in @((Get-JointJsonProperty $trace 'events'))) { Assert-JointExactFields $event @('id','status','observed') @('id','status','observed') '$.focusedTrace.events[]' }
    } catch { $traceShapeValid = $false }
    Add-FocusedAssertion 'focused_trace_exact_shape' $traceShapeValid 'Raw focused trace must reject unknown or missing fields.'
    Add-FocusedAssertion 'schema_version' ((Get-String $input 'schemaVersion') -eq 'awake.persona.g3-s0-focused-report.v1') 'Focused report schema is exact.'
    Add-FocusedAssertion 'task_identity' ((Get-String $input 'taskId') -eq 'PERSONA-AWAKE-JOINT-G3-S0-20260824' -and (Get-String $input 'batchId') -eq 'persona-awake-joint-g3-s0-storage-readiness-20260824' -and (Get-String $input 'gate') -eq 'G3-S0') 'Focused report task, batch and gate identity are exact.'
    Add-FocusedAssertion 'scope_binding' (([int](Get-JointJsonProperty $input 'scopeRevision') -eq 2) -and ((Get-String $input 'scopeSha256') -eq $report.scopeSha256)) 'Focused report binds to the current scope revision and raw scope hash.'
    $execution = Get-JointJsonProperty $input 'execution'
    Add-FocusedAssertion 'positive_status_exit' ((Get-String $input 'status') -eq 'pass' -and [int](Get-JointJsonProperty $input 'exitCode') -eq 0 -and [int](Get-JointJsonProperty $execution 'processExitCode') -eq 0 -and (Test-IsoUtcTimestamp (Get-String $execution 'traceGeneratedAt'))) 'Focused evidence must be an explicit report/process pass/0 result with an ISO UTC timestamp.'

    $sideEffects = Get-JointJsonProperty $input 'sideEffects'
    Add-FocusedAssertion 'side_effects_disabled' ((Get-JointJsonProperty $sideEffects 'launchGame') -eq $false -and (Get-JointJsonProperty $sideEffects 'syncGameDirectory') -eq $false -and (Get-JointJsonProperty $sideEffects 'mutateFrozenCandidate') -eq $false -and (Get-JointJsonProperty $sideEffects 'publish') -eq $false) 'Focused evidence must prove no game, sync, frozen-root or publish side effects.'

    $traceIdentity = ((Get-String $trace 'schemaVersion') -eq 'awake.persona.g3-s0-focused-trace.v1' -and (Get-String $trace 'taskId') -eq 'PERSONA-AWAKE-JOINT-G3-S0-20260824' -and (Get-String $trace 'batchId') -eq 'persona-awake-joint-g3-s0-storage-readiness-20260824' -and (Get-String $trace 'gate') -eq 'G3-S0' -and [int](Get-JointJsonProperty $trace 'scopeRevision') -eq 2 -and (Get-String $trace 'scopeSha256') -eq $report.scopeSha256 -and [int](Get-JointJsonProperty $trace 'processExitCode') -eq 0)
    Add-FocusedAssertion 'trace_identity_and_process' $traceIdentity 'Raw trace identity, scope binding and process exit code are exact.'

    $expectedSourcePaths = @('_houkai_merge/AWAKE/src/AwakeStorageContract.cs','_houkai_merge/AWAKE/src/AiTaskConstants.cs','_houkai_merge/AWAKE/src/WorldStateStore.cs','_houkai_merge/AWAKE/src/AwakeRuntime.cs','_houkai_merge/AWAKE/src/PersonaPersistenceModels.cs','_houkai_merge/AWAKE/src/AwakeTerminalBehavior.cs','_houkai_merge/AWAKE/src/WorldbookRuntime.cs')
    $traceBindings = @((Get-JointJsonProperty $trace 'sourceBindings'))
    $reportBindings = @((Get-JointJsonProperty $input 'sourceBindings'))
    $sourceBindingsValid = $true
    foreach ($sourcePath in $expectedSourcePaths) {
        $sourceFull = Get-JointFullPath $sourcePath
        $actualHash = Get-JointHashFile $sourceFull
        $traceMatch = @($traceBindings | Where-Object { (Get-String $_ 'path') -eq $sourcePath -and (Get-String $_ 'sha256') -eq $actualHash }).Count -eq 1
        $reportMatch = @($reportBindings | Where-Object { (Get-String $_ 'path') -eq $sourcePath -and (Get-String $_ 'sha256') -eq $actualHash }).Count -eq 1
        $sourceBindingsValid = $sourceBindingsValid -and $traceMatch -and $reportMatch
    }
    Add-FocusedAssertion 'source_hash_bindings' $sourceBindingsValid 'Focused report and raw trace must bind all relevant source files to current SHA-256 values.'
    $traceBindingPaths = @($traceBindings | ForEach-Object { Get-String $_ 'path' })
    $reportBindingPaths = @($reportBindings | ForEach-Object { Get-String $_ 'path' })
    Add-FocusedAssertion 'source_binding_set_exact' ((@($traceBindingPaths | Sort-Object) -join "`n") -eq (@($expectedSourcePaths | Sort-Object) -join "`n") -and (@($reportBindingPaths | Sort-Object) -join "`n") -eq (@($expectedSourcePaths | Sort-Object) -join "`n")) 'Focused report and trace must contain exactly the declared source binding set.'

    $storageContractText = [IO.File]::ReadAllText((Get-JointFullPath '_houkai_merge/AWAKE/src/AwakeStorageContract.cs'), $script:JointUtf8)
    $taskConstantsText = [IO.File]::ReadAllText((Get-JointFullPath '_houkai_merge/AWAKE/src/AiTaskConstants.cs'), $script:JointUtf8)
    $worldStateText = [IO.File]::ReadAllText((Get-JointFullPath '_houkai_merge/AWAKE/src/WorldStateStore.cs'), $script:JointUtf8)
    $awakeRuntimeText = [IO.File]::ReadAllText((Get-JointFullPath '_houkai_merge/AWAKE/src/AwakeRuntime.cs'), $script:JointUtf8)
    $terminalText = [IO.File]::ReadAllText((Get-JointFullPath '_houkai_merge/AWAKE/src/AwakeTerminalBehavior.cs'), $script:JointUtf8)
    $worldbookText = [IO.File]::ReadAllText((Get-JointFullPath '_houkai_merge/AWAKE/src/WorldbookRuntime.cs'), $script:JointUtf8)
    $personaSourceValid = $storageContractText.Contains('awake.persona.continuity.v1', [StringComparison]::Ordinal) -and $storageContractText.Contains('awake.persona.override.v1', [StringComparison]::Ordinal) -and $storageContractText.Contains('awake.persona.recovery.v1', [StringComparison]::Ordinal) -and $taskConstantsText.Contains('awake.persona.state', [StringComparison]::Ordinal) -and $worldStateText.Contains('PersonaContinuity', [StringComparison]::Ordinal) -and $worldStateText.Contains('PersonaOverride', [StringComparison]::Ordinal) -and $worldStateText.Contains('PersonaRecovery', [StringComparison]::Ordinal) -and $awakeRuntimeText.Contains('EnsureWorldStateReadyAsync', [StringComparison]::Ordinal)
    Add-FocusedAssertion 'persona_storage_source_characterization' $personaSourceValid 'Focused evidence must bind the actual Persona Storage registration and readiness source paths.'
    $worldbookSourceValid = $terminalText.Contains('SyncData("awake_worldbook_overlay_v1"', [StringComparison]::Ordinal) -and $terminalText.Contains('SyncData("awake_worldbook_activation_v1"', [StringComparison]::Ordinal) -and $terminalText.Contains('ExportOverlayJson', [StringComparison]::Ordinal) -and $terminalText.Contains('ImportOverlayJson', [StringComparison]::Ordinal) -and $terminalText.Contains('ExportActivationJson', [StringComparison]::Ordinal) -and $terminalText.Contains('ImportActivationJson', [StringComparison]::Ordinal) -and $worldbookText.Contains('awake.worldbook.campaign-activation.v1', [StringComparison]::Ordinal) -and $worldbookText.Contains('ImportOverlayJson', [StringComparison]::Ordinal) -and $worldbookText.Contains('ExportOverlayJson', [StringComparison]::Ordinal) -and $worldbookText.Contains('ImportActivationJson', [StringComparison]::Ordinal) -and $worldbookText.Contains('ExportActivationJson', [StringComparison]::Ordinal)
    Add-FocusedAssertion 'worldbook_source_characterization' $worldbookSourceValid 'Focused evidence must verify the actual SyncData/import/export source implementation.'

    $events = @((Get-JointJsonProperty $trace 'events'))
    $expectedEventIds = @('namespace_owner_unique','typed_schema_registry','partial_open_no_owner','existing_owner_required_missing_no_use','retry_after_failure','worldbook_syncdata_characterization','worldbook_schema_separation')
    $observedEventIds = @($events | ForEach-Object { Get-String $_ 'id' })
    $eventStatusValid = @($events | Where-Object { (Get-String $_ 'status') -ne 'observed' -or $null -eq (Get-JointJsonProperty $_ 'observed') }).Count -eq 0
    $eventSetExact = $events.Count -eq $expectedEventIds.Count -and (@($observedEventIds | Sort-Object) -join "`n") -eq (@($expectedEventIds | Sort-Object) -join "`n") -and $eventStatusValid
    Add-FocusedAssertion 'trace_event_set_exact' $eventSetExact 'Raw trace must contain exactly the seven known event IDs with no duplicates or unknown IDs.'
    $eventById = @{}
    foreach ($event in $events) { $eventById[(Get-String $event 'id')] = Get-JointJsonProperty $event 'observed' }

    $namespaceObserved = $eventById['namespace_owner_unique']
    Add-FocusedAssertion 'namespace_owner_unique' ((Get-String $namespaceObserved 'namespaceId') -eq 'awake.persona.state' -and [int](Get-JointJsonProperty $namespaceObserved 'ownerCount') -eq 1 -and (Get-JointJsonProperty $namespaceObserved 'ready') -eq $true -and (Get-JointJsonProperty $namespaceObserved 'published') -eq $true) 'Namespace event independently proves one ready published owner.'

    $expectedSchemaIds = @('awake.persona.continuity.v1','awake.persona.override.v1','awake.persona.recovery.v1')
    $typedObserved = $eventById['typed_schema_registry']
    $typedObservedIds = @((Get-JointJsonProperty $typedObserved 'schemaIds') | ForEach-Object { [string]$_ })
    Add-FocusedAssertion 'typed_schema_registry' ((@($typedObservedIds | Sort-Object) -join "`n") -eq (@($expectedSchemaIds | Sort-Object) -join "`n") -and (Get-String $typedObserved 'namespaceId') -eq 'awake.persona.state' -and (Get-String $typedObserved 'owner') -eq 'PersonaStorageOwner') 'Typed schema event independently proves the exact schema set and owner.'

    $partialObserved = $eventById['partial_open_no_owner']
    Add-FocusedAssertion 'partial_open_no_owner' ((Get-String $partialObserved 'result') -eq 'failed' -and [int](Get-JointJsonProperty $partialObserved 'openedNamespaceCount') -lt [int](Get-JointJsonProperty $partialObserved 'requiredNamespaceCount') -and [int](Get-JointJsonProperty $partialObserved 'publishedOwnerCount') -eq 0 -and [int](Get-JointJsonProperty $partialObserved 'stateWriteCount') -eq 0) 'Partial namespace open independently proves failed atomic staging with no published owner or write.'

    $existingObserved = $eventById['existing_owner_required_missing_no_use']
    Add-FocusedAssertion 'existing_owner_required_missing_no_use' ([int](Get-JointJsonProperty $existingObserved 'preexistingOwnerCount') -eq 1 -and (Get-JointJsonProperty $existingObserved 'requiredNamespaceMissing') -eq $true -and (Get-JointJsonProperty $existingObserved 'ownerUsed') -eq $false -and [int](Get-JointJsonProperty $existingObserved 'publishedOwnerCount') -eq 0 -and [int](Get-JointJsonProperty $existingObserved 'stateWriteCount') -eq 0) 'Existing-owner failure independently proves the stale owner is not reused.'

    $retryObserved = $eventById['retry_after_failure']
    $attempts = @((Get-JointJsonProperty $retryObserved 'attempts'))
    Add-FocusedAssertion 'retry_after_failure' ($attempts.Count -eq 2 -and (Get-String $attempts[0] 'result') -eq 'failed_no_owner' -and (Get-String $attempts[1] 'result') -eq 'ready' -and [int](Get-JointJsonProperty $retryObserved 'finalOwnerCount') -eq 1) 'Retry event independently proves first failure is clean and second attempt becomes ready.'

    $worldbookObserved = $eventById['worldbook_syncdata_characterization']
    $worldbookBindings = @((Get-JointJsonProperty $worldbookObserved 'bindings'))
    $worldbookExpected = @(
        [ordered]@{ key = 'awake_worldbook_overlay_v1'; schemaId = 'awake.worldbook.overlay.v1'; path = 'WorldbookRuntime.ImportOverlayJson/ExportOverlayJson' },
        [ordered]@{ key = 'awake_worldbook_activation_v1'; schemaId = 'awake.worldbook.campaign-activation.v1'; path = 'WorldbookRuntime.ImportActivationJson/ExportActivationJson' }
    )
    $worldbookValid = $worldbookBindings.Count -eq 2
    foreach ($expected in $worldbookExpected) { $worldbookValid = $worldbookValid -and (@($worldbookBindings | Where-Object { (Get-String $_ 'key') -eq $expected.key -and (Get-String $_ 'schemaId') -eq $expected.schemaId -and (Get-String $_ 'path') -eq $expected.path -and (Get-String $_ 'owner') -eq 'AwakeTerminalBehavior' }).Count -eq 1) }
    Add-FocusedAssertion 'worldbook_syncdata_characterization' $worldbookValid 'Raw trace must contain both observed Worldbook SyncData mappings.'

    $separationObserved = $eventById['worldbook_schema_separation']
    $personaIds = @((Get-JointJsonProperty $separationObserved 'personaSchemaIds') | ForEach-Object { [string]$_ })
    $worldbookIds = @((Get-JointJsonProperty $separationObserved 'worldbookSchemaIds') | ForEach-Object { [string]$_ })
    Add-FocusedAssertion 'worldbook_schema_separation' ((Get-String $separationObserved 'personaNamespace') -eq 'awake.persona.state' -and [int](Get-JointJsonProperty $separationObserved 'intersectionCount') -eq 0 -and (@($personaIds | Where-Object { $worldbookIds -contains $_ }).Count -eq 0)) 'Raw trace independently proves Persona and Worldbook schema sets are disjoint.'

    if ($errors.Count -gt 0) {
        $report.status = 'reject'
        $report.exitCode = 10
        $report.observedErrors = @($errors)
    } else {
        $report.status = 'pass'
        $report.exitCode = 0
    }
} catch {
    $missingInput = $_.Exception -is [System.IO.FileNotFoundException] -or $_.Exception.Message -match '(?i)focused readiness input is missing'
    $blockedInput = $_.Exception.Message -like '__JOINT_BLOCKED__*'
    if ($missingInput) {
        $errorRecord = [pscustomobject]@{
            schemaVersion = 'awake.persona.adapter-error.v1'
            errorId = Get-JointStableHashId 'error' ('persona.g3_s0_focused_missing|' + $_.Exception.Message)
            code = 'persona.g3_s0_focused_missing'
            stage = 'g3_s0_focused_evidence'
            artifactId = 'g3_s0_focused_evidence'
            path = '$.requiredEvidence.focusedReadinessReport'
            detail = 'Focused readiness report is not present yet; positive evidence is required after S0 implementation.'
            retryable = $false
        }
        $report.status = 'blocked'
        $report.exitCode = 20
    } elseif ($blockedInput) {
        $errorRecord = Convert-JointExceptionToError $_.Exception 'verify-g3-s0-focused-evidence'
        $report.status = 'blocked'
        $report.exitCode = 20
    } else {
        $errorRecord = Convert-JointExceptionToError $_.Exception 'verify-g3-s0-focused-evidence'
        $report.status = 'error'
        $report.exitCode = 40
    }
    $report.observedErrors = @($errorRecord)
} finally {
    $report.assertions = @($assertions)
    if ($null -ne $reportPathFull) { Write-JointReport $report $reportPathFull }
}

exit ([int]$report.exitCode)
