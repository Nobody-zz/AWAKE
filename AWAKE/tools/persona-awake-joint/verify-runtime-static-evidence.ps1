param(
    [Parameter(Mandatory = $true)][string]$InputReportPath,
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [ValidateSet('auto', 'reject', 'pass')][string]$Case = 'auto'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'persona-awake-joint.ps1')

$reportPathFull = $null
$assertions = [Collections.Generic.List[object]]::new()
$errors = [Collections.Generic.List[object]]::new()
$expectedChecks = [ordered]@{
    workbench_runtime_boundary_absent = 'contract'
    legacy_persona_entry_absent = 'blocking'
    legacy_knowledge_entry_absent = 'blocking'
    persona_runtime_provider_present = 'blocking'
    context_snapshot_present = 'blocking'
    runtime_bundle_present = 'blocking'
    fingerprint_includes_context_modes = 'blocking'
    reload_preserves_last_known_good = 'blocking'
    overlay_invalidates_persona_cache = 'blocking'
    persona_persistence_contract_present = 'blocking'
    persona_persistence_runtime_wired = 'blocking'
    persona_storage_schema_registered = 'blocking'
    overlay_persistence_path_observable = 'contract'
}
$report = [ordered]@{
    schemaVersion = 'awake.persona.runtime-bridge-evidence.v1'
    commandLine = 'pwsh -NoProfile -File "' + [IO.Path]::GetFullPath($PSCommandPath) + '" -InputReportPath "' + $InputReportPath + '" -ReportPath "' + $ReportPath + '" -Case "' + $Case + '"'
    normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
    caseMode = $Case
    inputReportPath = $null
    inputReportSha256 = $null
    targetStatus = $null
    targetExitCode = $null
    checkCount = 0
    failedBlockingCheckCount = 0
    observedErrorCount = 0
    status = 'error'
    exitCode = 40
    interpretation = 'Evidence-contract verification only. A pass means the runtime-static report is internally consistent; it does not mean the runtime bridge is ready.'
    assertions = @()
    observedErrors = @()
}

function Add-EvidenceAssertion([string]$Id, [bool]$Passed, [string]$Detail) {
    $script:assertions.Add([ordered]@{
        id = $Id
        passed = $Passed
        detail = $Detail
    })
    if (-not $Passed) {
        $script:errors.Add([pscustomobject]@{
            schemaVersion = 'awake.persona.adapter-error.v1'
            errorId = Get-JointStableHashId 'error' ('persona.runtime_static_evidence_invalid|' + $Id + '|' + $Detail)
            code = 'persona.runtime_static_evidence_invalid'
            stage = 'runtime_static_evidence'
            artifactId = 'runtime_static_evidence'
            path = '$.' + $Id
            detail = $Detail
            retryable = $false
            kind = '__JOINT_REJECT__'
        })
    }
}

function Get-ObjectProperty([object]$Value, [string]$Name) {
    return Get-JointJsonProperty $Value $Name
}

function Test-StableId([string]$Value) {
    return -not [string]::IsNullOrWhiteSpace($Value) -and $Value -match '^[a-z0-9_]+(?:\.[a-z0-9_]+)*$'
}

try {
    $reportPathFull = Assert-JointOutputPath $ReportPath 'ReportPath'
    $inputReportPathFull = Get-JointFullPath $InputReportPath
    $allowedInputRoots = @(
        $script:JointToolRoot,
        (Join-Path $script:JointAwakeRoot 'docs\fixtures\persona-awake-joint')
    )
    $inputPathAllowed = @($allowedInputRoots | Where-Object {
        Test-JointPathUnder $inputReportPathFull $_ -AllowEqual:$false
    }).Count -gt 0
    if (-not $inputPathAllowed) {
        Throw-JointReject 'persona.path_protected' 'path' 'InputReportPath' 'Input report must remain under the isolated tool root or persona fixture root.'
    }
    $inputReportPathFull = Assert-JointReadablePath $inputReportPathFull $script:JointWorkspaceRoot 'InputReportPath'
    if ([StringComparer]::OrdinalIgnoreCase.Equals($inputReportPathFull, $reportPathFull)) {
        Throw-JointReject 'persona.path_protected' 'path' 'ReportPath' 'Evidence report must not overwrite the input report.'
    }
    $parsed = Read-JointJsonFile $inputReportPathFull 'runtime static report'
    $target = $parsed.Value
    $report.inputReportPath = $parsed.FullPath
    $report.inputReportSha256 = $parsed.RawSha256

    $schemaVersion = [string](Get-ObjectProperty $target 'schemaVersion')
    Add-EvidenceAssertion 'schema_version' ($schemaVersion -eq 'awake.persona.runtime-bridge-static.v1') ('Observed=' + $schemaVersion)

    $targetStatus = [string](Get-ObjectProperty $target 'status')
    $targetExitCodeValue = Get-ObjectProperty $target 'exitCode'
    $report.targetStatus = $targetStatus
    $report.targetExitCode = $targetExitCodeValue
    $statusSupported = $targetStatus -in @('pass', 'reject')
    Add-EvidenceAssertion 'status_supported' $statusSupported ('Observed=' + $targetStatus)
    if (-not $statusSupported) {
        Throw-JointNotAttempted 'persona.runtime_static_case_not_supported' 'runtime_static_evidence' '$.status' 'Only pass and reject runtime-static reports are evidence cases.'
    }
    $exitCodeIsInteger = $targetExitCodeValue -is [int] -or $targetExitCodeValue -is [long] -or $targetExitCodeValue -is [short]
    Add-EvidenceAssertion 'exit_code_integer' $exitCodeIsInteger ('Observed=' + [string]$targetExitCodeValue)

    $checksValue = Get-ObjectProperty $target 'checks'
    $checksAreArray = Test-JointJsonArray $checksValue
    Add-EvidenceAssertion 'checks_array' $checksAreArray ('Observed array=' + $checksAreArray)
    $checkRecords = [Collections.Generic.List[object]]::new()
    $checkIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    if ($checksAreArray) {
        $checkIndex = 0
        foreach ($check in @($checksValue)) {
            $checkObject = Test-JointJsonObject $check
            if (-not $checkObject) {
                Add-EvidenceAssertion ('check_object:' + $checkIndex) $false 'A checks entry is not an object.'
                $checkIndex++
                continue
            }
            $id = [string](Get-ObjectProperty $check 'id')
            $passedValue = Get-ObjectProperty $check 'passed'
            $severity = [string](Get-ObjectProperty $check 'severity')
            $detail = [string](Get-ObjectProperty $check 'detail')
            $idValid = Test-StableId $id
            Add-EvidenceAssertion ('check_id_present:' + $checkIndex) $idValid 'Every check must have a stable non-empty id.'
            if ($idValid) {
                Add-EvidenceAssertion ('check_id_unique:' + $id) $checkIds.Add($id) ('Duplicate check id=' + $id)
            }
            $passedValid = $passedValue -is [bool]
            Add-EvidenceAssertion ('check_passed_bool:' + $checkIndex) $passedValid ('Check=' + $id)
            $severityValid = $severity -in @('contract', 'blocking')
            Add-EvidenceAssertion ('check_severity:' + $checkIndex) $severityValid ('Check=' + $id + '; severity=' + $severity)
            $knownCheck = $idValid -and $expectedChecks.Contains($id)
            Add-EvidenceAssertion ('check_known:' + $checkIndex) $knownCheck ('Check=' + $id)
            if ($idValid -and $passedValid -and $severityValid) {
                $checkRecords.Add([pscustomobject]@{
                    id = $id
                    passed = [bool]$passedValue
                    severity = $severity
                    detail = $detail
                })
            }
            $checkIndex++
        }
    }
    $report.checkCount = if ($checksAreArray) { $checksValue.Count } else { 0 }
    Add-EvidenceAssertion 'check_count_matches_expected' ($report.checkCount -eq $expectedChecks.Count) ('Expected=' + $expectedChecks.Count + '; observed=' + $report.checkCount)
    foreach ($expectedId in $expectedChecks.Keys) {
        $matches = @($checkRecords | Where-Object { $_.id -eq $expectedId })
        Add-EvidenceAssertion ('required_check_present:' + $expectedId) ($matches.Count -eq 1) ('Check=' + $expectedId + '; matches=' + $matches.Count)
        if ($matches.Count -eq 1) {
            Add-EvidenceAssertion ('required_check_severity:' + $expectedId) ($matches[0].severity -eq $expectedChecks[$expectedId]) ('Check=' + $expectedId + '; expected=' + $expectedChecks[$expectedId] + '; observed=' + $matches[0].severity)
        }
    }
    $failedBlockingChecks = [Collections.Generic.List[object]]::new()
    foreach ($check in $checkRecords) {
        if ($expectedChecks.Contains($check.id) -and -not $check.passed -and $check.severity -eq 'blocking') {
            $failedBlockingChecks.Add($check)
        }
    }
    $report.failedBlockingCheckCount = $failedBlockingChecks.Count

    $errorsValue = Get-ObjectProperty $target 'observedErrors'
    $errorsAreArray = Test-JointJsonArray $errorsValue
    Add-EvidenceAssertion 'observed_errors_array' $errorsAreArray ('Observed array=' + $errorsAreArray)
    $observedErrors = [Collections.Generic.List[object]]::new()
    if ($errorsAreArray) {
        foreach ($observedError in $errorsValue) {
            $observedErrors.Add($observedError)
        }
    }
    $report.observedErrorCount = $observedErrors.Count
    $errorPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $errorIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $failedByPath = @{}
    foreach ($check in $failedBlockingChecks) {
        $failedByPath['$.checks.' + $check.id] = $check
    }

    $observedErrorIndex = 0
    foreach ($observedError in $observedErrors) {
        $errorObject = Test-JointJsonObject $observedError
        if (-not $errorObject) {
            Add-EvidenceAssertion ('observed_error_object:' + $observedErrorIndex) $false 'An observedErrors entry is not an object.'
            $observedErrorIndex++
            continue
        }
        $schema = [string](Get-ObjectProperty $observedError 'schemaVersion')
        $code = [string](Get-ObjectProperty $observedError 'code')
        $stage = [string](Get-ObjectProperty $observedError 'stage')
        $artifactId = [string](Get-ObjectProperty $observedError 'artifactId')
        $path = [string](Get-ObjectProperty $observedError 'path')
        $detail = [string](Get-ObjectProperty $observedError 'detail')
        $retryable = Get-ObjectProperty $observedError 'retryable'
        $errorId = [string](Get-ObjectProperty $observedError 'errorId')
        Add-EvidenceAssertion ('observed_error_schema:' + $observedErrorIndex) ($schema -eq 'awake.persona.adapter-error.v1') ('Path=' + $path)
        Add-EvidenceAssertion ('observed_error_code:' + $observedErrorIndex) ($code -eq 'persona.runtime_bridge_check_failed') ('Path=' + $path + '; code=' + $code)
        Add-EvidenceAssertion ('observed_error_stage:' + $observedErrorIndex) ($stage -eq 'runtime_static') ('Path=' + $path + '; stage=' + $stage)
        Add-EvidenceAssertion ('observed_error_artifact:' + $observedErrorIndex) (Test-StableId $artifactId) ('Path=' + $path + '; artifactId=' + $artifactId)
        Add-EvidenceAssertion ('observed_error_path:' + $observedErrorIndex) (-not [string]::IsNullOrWhiteSpace($path)) ('Observed error path is required.')
        Add-EvidenceAssertion ('observed_error_path_unique:' + $observedErrorIndex) $errorPaths.Add($path) ('Duplicate path=' + $path)
        Add-EvidenceAssertion ('observed_error_id:' + $observedErrorIndex) (Test-StableId $errorId) ('Path=' + $path + '; errorId=' + $errorId)
        Add-EvidenceAssertion ('observed_error_id_unique:' + $observedErrorIndex) $errorIds.Add($errorId) ('Duplicate errorId=' + $errorId)
        Add-EvidenceAssertion ('observed_error_detail:' + $observedErrorIndex) (-not [string]::IsNullOrWhiteSpace($detail)) ('Path=' + $path)
        Add-EvidenceAssertion ('observed_error_retryable:' + $observedErrorIndex) ($retryable -is [bool]) ('Path=' + $path)
        $matchingCheck = $null
        if ($failedByPath.ContainsKey($path)) { $matchingCheck = $failedByPath[$path] }
        Add-EvidenceAssertion ('observed_error_matches_failed_check:' + $observedErrorIndex) ($null -ne $matchingCheck) ('Path=' + $path)
        $observedErrorIndex++
    }

    Add-EvidenceAssertion 'failed_check_error_count' ($observedErrors.Count -eq $failedBlockingChecks.Count) ('Failed blocking checks=' + $failedBlockingChecks.Count + '; observed errors=' + $observedErrors.Count)
    foreach ($check in $failedBlockingChecks) {
        $path = '$.checks.' + $check.id
        $matchingErrors = @($observedErrors | Where-Object { [string](Get-ObjectProperty $_ 'path') -eq $path })
        Add-EvidenceAssertion ('failed_check_has_error:' + $check.id) ($matchingErrors.Count -eq 1) ('Path=' + $path + '; matches=' + $matchingErrors.Count)
    }
    $expectedStatus = if ($failedBlockingChecks.Count -gt 0) { 'reject' } else { 'pass' }
    $expectedExitCode = if ($failedBlockingChecks.Count -gt 0) { 10 } else { 0 }
    Add-EvidenceAssertion 'case_matches_target' ($Case -eq 'auto' -or $Case -eq $expectedStatus) ('Case=' + $Case + '; expected=' + $expectedStatus)
    Add-EvidenceAssertion 'status_matches_failed_checks' ($targetStatus -eq $expectedStatus) ('Expected=' + $expectedStatus + '; observed=' + $targetStatus)
    Add-EvidenceAssertion 'exit_code_matches_status' ($exitCodeIsInteger -and [int]$targetExitCodeValue -eq $expectedExitCode) ('Expected=' + $expectedExitCode + '; observed=' + [string]$targetExitCodeValue)
    if ($targetStatus -eq 'pass') {
        Add-EvidenceAssertion 'pass_has_no_observed_errors' ($observedErrors.Count -eq 0) ('Observed errors=' + $observedErrors.Count)
    }

    if ($errors.Count -eq 0) {
        $report.status = 'pass'
        $report.exitCode = 0
    } else {
        $report.status = 'reject'
        $report.exitCode = 10
        $report.observedErrors = @($errors)
    }
} catch {
    $errorRecord = Convert-JointExceptionToError $_.Exception 'runtime-static-evidence'
    $report.observedErrors = @($errorRecord)
    if ($_.Exception.Message.StartsWith('__JOINT_NOT_ATTEMPTED__|', [StringComparison]::Ordinal)) {
        $report.status = 'not_attempted'
        $report.exitCode = 30
    } elseif ($_.Exception.Message.StartsWith('__JOINT_REJECT__|', [StringComparison]::Ordinal)) {
        $report.status = 'reject'
        $report.exitCode = 10
    } else {
        $report.status = 'error'
        $report.exitCode = 40
    }
} finally {
    $report.assertions = @($assertions)
    if ($null -ne $reportPathFull) { Write-JointReport $report $reportPathFull }
}

exit ([int]$report.exitCode)
