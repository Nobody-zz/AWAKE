param(
    [Parameter(Mandatory = $true)][string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'persona-awake-joint.ps1')

$reportPathFull = $null
$assertions = [Collections.Generic.List[object]]::new()
$report = [ordered]@{
    schemaVersion = 'awake.persona.native-prerequisite.v1'
    commandLine = 'pwsh -NoProfile -File "' + [IO.Path]::GetFullPath($PSCommandPath) + '" -ReportPath "' + $ReportPath + '"'
    normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
    status = 'error'
    exitCode = 40
    planPath = $null
    checkpointPath = $null
    g3PlanPath = $null
    g3ReviewLogPath = $null
    scopeManifestPath = $null
    scopeVerificationPath = $null
    focusedEvidencePath = $null
    focusedEvidenceVerificationPath = $null
    focusedEvidenceStatus = $null
    focusedEvidenceExitCode = $null
    scopeReportExitCode = $null
    focusedReportExitCode = $null
    storageEvidencePaths = @()
    checkpointStatus = $null
    g3PlanStatus = $null
    g3ReviewStatus = $null
    g3ReviewVerdict = $null
    legacyReviewVerdict = $null
    scopeStatus = $null
    scopeApprovalVerdict = $null
    scopeUserSignoff = $false
    scopeLeaseStatus = $null
    scopeLeaseOwner = $null
    scopeLeaseId = $null
    scopeVerificationExitCode = $null
    reviewVerdict = $null
    executionLease = $null
    leaseOwner = $null
    leaseId = $null
    exactApprovedScope = $false
    uniqueActiveLease = $false
    disjointWriteSet = $false
    storageReady = $false
    assertions = @()
    observedErrors = @()
}

try {
    $reportPathFull = Assert-JointOutputPath $ReportPath 'ReportPath'
    $planPath = Join-Path $script:JointAwakeRoot 'docs\PLAN-AWAKE-NativeState-Knowledge-MultiBatch-20260823.md'
    $checkpointPath = Join-Path $script:JointAwakeRoot 'docs\checkpoints\AWAKE-NATIVE-KNOWLEDGE-BOUNDARY-20260823-checkpoint.md'
    $g3PlanPath = Join-Path $script:JointAwakeRoot 'docs\PLAN-PersonaWorkbench-AWAKE-Joint-G3-EXECUTION-GATE-20260824.md'
    $g3ReviewLogPath = Join-Path $script:JointAwakeRoot 'docs\PLAN-PersonaWorkbench-AWAKE-Joint-G3-EXECUTION-GATE-REVIEW-LOG-20260824.md'
    $scopeManifestPath = Join-Path $script:JointAwakeRoot 'docs\persona-awake-joint-g3-s0-scope.v1.json'
    $scopeVerificationPath = Join-Path (Get-JointArtifactRoot) '.verify-native-g3-s0-scope.json'
    $storageEvidencePaths = @(
        (Join-Path $script:JointAwakeRoot 'docs\Awake-StorageAndMemory-Verification-20260816.md'),
        (Join-Path $script:JointAwakeRoot 'docs\AWAKE-NativeState-Adapter-Spec-v1.md')
    )
    $report.planPath = [IO.Path]::GetFullPath($planPath)
    $report.checkpointPath = [IO.Path]::GetFullPath($checkpointPath)
    $report.g3PlanPath = [IO.Path]::GetFullPath($g3PlanPath)
    $report.g3ReviewLogPath = [IO.Path]::GetFullPath($g3ReviewLogPath)
    $report.scopeManifestPath = [IO.Path]::GetFullPath($scopeManifestPath)
    $report.scopeVerificationPath = [IO.Path]::GetFullPath($scopeVerificationPath)
    $report.storageEvidencePaths = @($storageEvidencePaths | ForEach-Object { [IO.Path]::GetFullPath($_) })
    if (-not (Test-Path -LiteralPath $scopeManifestPath -PathType Leaf)) { throw [System.IO.FileNotFoundException]::new('G3-S0 scope manifest is missing.', $scopeManifestPath) }
    $planText = if (Test-Path -LiteralPath $planPath -PathType Leaf) { [IO.File]::ReadAllText($planPath, $script:JointUtf8) } else { '' }
    $checkpointText = if (Test-Path -LiteralPath $checkpointPath -PathType Leaf) { [IO.File]::ReadAllText($checkpointPath, $script:JointUtf8) } else { '' }
    $g3PlanText = if (Test-Path -LiteralPath $g3PlanPath -PathType Leaf) { [IO.File]::ReadAllText($g3PlanPath, $script:JointUtf8) } else { '' }
    $g3ReviewText = if (Test-Path -LiteralPath $g3ReviewLogPath -PathType Leaf) { [IO.File]::ReadAllText($g3ReviewLogPath, $script:JointUtf8) } else { '' }
    $scopeRead = Read-JointJsonFile $scopeManifestPath 'G3-S0 scope manifest'
    $scope = $scopeRead.Value
    $requiredEvidence = Get-JointJsonProperty $scope 'requiredEvidence'
    $focusedEvidenceRelative = [string](Get-JointJsonProperty $requiredEvidence 'focusedReadinessReport')
    if ([string]::IsNullOrWhiteSpace($focusedEvidenceRelative)) { throw [System.IO.InvalidDataException]::new('G3-S0 focused readiness report is not declared.') }
    $focusedEvidencePath = Get-JointFullPath $focusedEvidenceRelative $script:JointAwakeRoot
    $focusedEvidenceVerificationPath = Join-Path (Get-JointArtifactRoot) '.verify-native-g3-s0-focused-evidence.json'
    $report.focusedEvidencePath = [IO.Path]::GetFullPath($focusedEvidencePath)
    $report.focusedEvidenceVerificationPath = [IO.Path]::GetFullPath($focusedEvidenceVerificationPath)
    $report.legacyReviewVerdict = if ($planText -match '(?m)^\s*VERDICT:\s*APPROVED\s*$') { 'APPROVED' } elseif ($planText -match 'LOCKED_AFTER_GRILL') { 'LOCKED_AFTER_GRILL' } else { 'UNKNOWN' }
    $g3PlanStatusMatch = [Regex]::Match($g3PlanText, '(?m)^> 状态：`([^`]+)`')
    $g3ReviewStatusMatch = [Regex]::Match($g3ReviewText, '(?m)^- 状态：`([^`]+)`')
    $report.g3PlanStatus = if ($g3PlanStatusMatch.Success) { $g3PlanStatusMatch.Groups[1].Value } else { 'UNKNOWN' }
    $report.g3ReviewStatus = if ($g3ReviewStatusMatch.Success) { $g3ReviewStatusMatch.Groups[1].Value } else { 'UNKNOWN' }
    $report.g3ReviewVerdict = if ($g3ReviewText -match '(?m)^\s*VERDICT:\s*APPROVED\s*$') { 'APPROVED' } elseif ($g3ReviewText -match '(?m)^\s*VERDICT:\s*REVISE\s*$') { 'REVISE' } else { 'UNKNOWN' }
    $report.reviewVerdict = $report.g3ReviewVerdict
    $scopeVerifier = Join-Path $script:JointToolRoot 'verify-g3-s0-scope.ps1'
    $null = & pwsh -NoProfile -File $scopeVerifier -ReportPath $scopeVerificationPath
    $report.scopeVerificationExitCode = [int]$LASTEXITCODE
    if (-not (Test-Path -LiteralPath $scopeVerificationPath -PathType Leaf)) { throw [System.IO.FileNotFoundException]::new('G3-S0 scope verification report is missing.', $scopeVerificationPath) }
    $scopeReport = (Read-JointJsonFile $scopeVerificationPath 'G3-S0 scope verification').Value
    $report.scopeReportExitCode = [int](Get-JointJsonProperty $scopeReport 'exitCode')
    $report.scopeStatus = [string](Get-JointJsonProperty $scopeReport 'scopeStatus')
    $report.scopeApprovalVerdict = [string](Get-JointJsonProperty $scopeReport 'approvalVerdict')
    $report.scopeUserSignoff = (Get-JointJsonProperty $scopeReport 'userSignoff') -eq $true
    $report.scopeLeaseStatus = [string](Get-JointJsonProperty $scopeReport 'leaseStatus')
    $report.scopeLeaseOwner = [string](Get-JointJsonProperty $scopeReport 'leaseOwner')
    $report.scopeLeaseId = [string](Get-JointJsonProperty $scopeReport 'leaseId')
    $statusMatch = [Regex]::Match($checkpointText, '(?m)^- `status`:\s*`([^`]+)`')
    $report.checkpointStatus = if ($statusMatch.Success) { $statusMatch.Groups[1].Value } else { 'UNKNOWN' }
    $report.executionLease = $report.scopeLeaseStatus
    $report.leaseOwner = $report.scopeLeaseOwner
    $report.leaseId = $report.scopeLeaseId
    $scopeReportStatus = [string](Get-JointJsonProperty $scopeReport 'status')
    $null = & pwsh -NoProfile -File (Join-Path $script:JointToolRoot 'verify-g3-s0-focused-evidence.ps1') -InputReportPath $focusedEvidencePath -ReportPath $focusedEvidenceVerificationPath
    $report.focusedEvidenceExitCode = [int]$LASTEXITCODE
    if (-not (Test-Path -LiteralPath $focusedEvidenceVerificationPath -PathType Leaf)) { throw [System.IO.FileNotFoundException]::new('G3-S0 focused evidence verification report is missing.', $focusedEvidenceVerificationPath) }
    $focusedReport = (Read-JointJsonFile $focusedEvidenceVerificationPath 'G3-S0 focused evidence verification').Value
    $report.focusedEvidenceStatus = [string](Get-JointJsonProperty $focusedReport 'status')
    $report.focusedReportExitCode = [int](Get-JointJsonProperty $focusedReport 'exitCode')
    $scopeSubprocessPassed = $report.scopeVerificationExitCode -eq 0 -and $report.scopeReportExitCode -eq 0
    $focusedSubprocessPassed = $report.focusedEvidenceExitCode -eq 0 -and $report.focusedReportExitCode -eq 0
    $report.exactApprovedScope = ($scopeSubprocessPassed -and ($scopeReportStatus -eq 'pass') -and ($report.scopeApprovalVerdict -eq 'APPROVED') -and $report.scopeUserSignoff -and ((Get-JointJsonProperty $scopeReport 'identityBound') -eq $true))
    $report.uniqueActiveLease = ($scopeSubprocessPassed -and ($scopeReportStatus -eq 'pass') -and ($report.scopeLeaseStatus -eq 'active') -and ((Get-JointJsonProperty $scopeReport 'activeLeaseRecordCount') -eq 1) -and -not [string]::IsNullOrWhiteSpace($report.scopeLeaseOwner) -and -not [string]::IsNullOrWhiteSpace($report.scopeLeaseId))
    $report.disjointWriteSet = $scopeSubprocessPassed -and $scopeReportStatus -eq 'pass' -and (Get-JointJsonProperty $scopeReport 'writeSetDisjoint') -eq $true
    $report.storageReady = ($focusedSubprocessPassed -and $report.focusedEvidenceStatus -eq 'pass')
    Add-JointAssertion $assertions 'native_exact_approval_scope' ([bool]$report.exactApprovedScope) 'Requires an approved Persona G3 scope; legacy B1 Knowledge approval is not sufficient.'
    Add-JointAssertion $assertions 'native_unique_active_lease' ([bool]$report.uniqueActiveLease) 'Requires a unique active lease with owner and ID.'
    Add-JointAssertion $assertions 'native_disjoint_write_set' ([bool]$report.disjointWriteSet) 'The Native lease must exclude the isolated joint-tool write set.'
    Add-JointAssertion $assertions 'storage_readiness_evidence' ([bool]$report.storageReady) 'Storage readiness evidence must be explicit before shared persistence fixtures.'
    if ($report.exactApprovedScope -and $report.uniqueActiveLease -and $report.disjointWriteSet -and $report.storageReady) {
        $report.status = 'pass'
        $report.exitCode = 0
        $report.observedErrors = @()
    } elseif ($scopeReportStatus -eq 'blocked' -or $report.focusedEvidenceStatus -eq 'blocked' -or $report.executionLease -in @('none','NONE','null','') -or -not $scopeSubprocessPassed -or -not $focusedSubprocessPassed) {
        $report.status = 'blocked'
        $report.exitCode = 20
        $report.observedErrors = @([pscustomobject]@{ code = 'persona.native_prerequisite_blocked'; detail = 'G3-S0 exact approval/active lease or scope-bound focused readiness evidence is missing; legacy B1 checkpoint is not sufficient.' })
    } else {
        $report.status = 'not_attempted'
        $report.exitCode = 30
        $report.observedErrors = @([pscustomobject]@{ code = 'persona.native_prerequisite_not_ready'; detail = 'Native prerequisite evidence is incomplete.' })
    }
} catch {
    $errorRecord = Convert-JointExceptionToError $_.Exception 'verify-native-prerequisite'
    $report.status = 'error'
    $report.exitCode = 40
    $report.observedErrors = @($errorRecord)
} finally {
    $report.assertions = @($assertions)
    if ($null -ne $reportPathFull) { Write-JointReport $report $reportPathFull }
}

exit ([int]$report.exitCode)
