param(
    [Parameter(Mandatory = $true)][string]$ScopePath,
    [Parameter(Mandatory = $true)][string]$ApprovalPath,
    [Parameter(Mandatory = $true)][string]$LeasePath,
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [string]$DisjointScopePath,
    [string]$DependentScopePath,
    [string]$StaticReportPath,
    [string]$PredecessorApprovalPath,
    [string]$PredecessorLeasePath,
    [string]$CompletionPath,
    [string]$CompletionSchemaPath,
    [switch]$AllowUserException
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'persona-awake-joint.ps1')

function Normalize-G3ScopePath([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { throw 'Scope writeSet contains an empty path.' }
    return ($Value.Replace('\\','/').Trim()).ToLowerInvariant()
}

function Read-G3Json([string]$Path, [string]$Label) {
    $full = Assert-JointReadablePath $Path $script:JointWorkspaceRoot $Label
    return (Read-JointJsonFile $full $Label).Value
}

$checks = [Collections.Generic.List[object]]::new()
function Add-G3AuthorityCheck([string]$Id, [bool]$Passed, [string]$Detail) {
    $checks.Add([ordered]@{ id = $Id; passed = $Passed; detail = $Detail })
}

try {
    $reportFull = Assert-JointOutputPath $ReportPath 'ReportPath'
    $scopeFull = Assert-JointReadablePath $ScopePath $script:JointWorkspaceRoot 'ScopePath'
    $leasePathFull = Assert-JointReadablePath ([IO.Path]::GetFullPath($LeasePath)) $script:JointWorkspaceRoot 'LeasePath'
    $scope = Read-G3Json $scopeFull 'scope'
    $approval = Read-G3Json $ApprovalPath 'approval'
    $lease = Read-G3Json $LeasePath 'lease'
    $scopeHash = (Get-FileHash -LiteralPath $scopeFull -Algorithm SHA256).Hash
    $scopeWriteSet = @($scope.writeSet | ForEach-Object { Normalize-G3ScopePath ([string]$_) })
    $leaseWriteSet = @($lease.writeSet | ForEach-Object { Normalize-G3ScopePath ([string]$_) })
    $duplicateScopePaths = @($scopeWriteSet | Group-Object | Where-Object Count -gt 1)
    Add-G3AuthorityCheck 'scope_has_unique_write_set' ($scopeWriteSet.Count -gt 0 -and $duplicateScopePaths.Count -eq 0) ('writeSet=' + $scopeWriteSet.Count + '; duplicate=' + $duplicateScopePaths.Count)
    Add-G3AuthorityCheck 'approval_binds_scope_hash' ([string]$approval.scopeSha256 -eq $scopeHash) ('approval=' + $approval.scopeSha256 + '; observed=' + $scopeHash)
    Add-G3AuthorityCheck 'lease_binds_scope_hash' ([string]$lease.scopeSha256 -eq $scopeHash) ('lease=' + $lease.scopeSha256 + '; observed=' + $scopeHash)
    Add-G3AuthorityCheck 'authority_identity_matches' (([string]$approval.taskId -eq [string]$scope.taskId) -and ([string]$lease.taskId -eq [string]$scope.taskId) -and ([string]$approval.gate -eq [string]$scope.gate) -and ([string]$lease.gate -eq [string]$scope.gate)) 'taskId and gate agree.'
    $approvalAccepted = ([string]$approval.recordStatus -eq 'approved') -or ($AllowUserException -and [string]$approval.recordStatus -eq 'approved_by_user_exception' -and [bool]$approval.userSignoff.confirmed)
    Add-G3AuthorityCheck 'approval_status_accepted' $approvalAccepted ('recordStatus=' + $approval.recordStatus + '; exceptionAllowed=' + $AllowUserException)
    $completionMode = -not [string]::IsNullOrWhiteSpace($CompletionPath)
    $leaseUsable = (([string]$lease.status -eq 'active') -and ([string]$lease.recordStatus -eq 'active')) -or ($completionMode -and ([string]$lease.status -eq 'released') -and ([string]$lease.recordStatus -eq 'released'))
    Add-G3AuthorityCheck 'lease_is_active_or_completion_released' $leaseUsable ('status=' + $lease.status + '; recordStatus=' + $lease.recordStatus + '; completionMode=' + $completionMode)
    Add-G3AuthorityCheck 'lease_write_set_exact' ((@($scopeWriteSet | Sort-Object) -join "`n") -eq (@($leaseWriteSet | Sort-Object) -join "`n")) 'Normalized scope and lease write sets match exactly.'
    if (-not [string]::IsNullOrWhiteSpace($DisjointScopePath)) {
        $other = Read-G3Json $DisjointScopePath 'DisjointScopePath'
        $otherPaths = @($other.writeSet | ForEach-Object { Normalize-G3ScopePath ([string]$_) })
        $overlap = @($scopeWriteSet | Where-Object { $otherPaths -contains $_ })
        Add-G3AuthorityCheck 'write_set_disjoint' ($overlap.Count -eq 0) ('overlap=' + ($overlap -join ','))
    }
    if (-not [string]::IsNullOrWhiteSpace($DependentScopePath)) {
        $dependent = Read-G3Json $DependentScopePath 'DependentScopePath'
        $matrixRelativePath = [string]$dependent.checkMatrixPath
        $matrixExpectedHash = [string]$dependent.checkMatrixSha256
        $matrixFullPath = Assert-JointReadablePath (Join-Path $script:JointAwakeRoot $matrixRelativePath) $script:JointAwakeRoot 'checkMatrixPath'
        $matrix = Read-G3Json $matrixFullPath 'check matrix'
        $matrixActualHash = (Get-FileHash -LiteralPath $matrixFullPath -Algorithm SHA256).Hash
        Add-G3AuthorityCheck 'dependent_matrix_hash_bound' ($matrixExpectedHash -match '^[A-F0-9]{64}$' -and $matrixExpectedHash -eq $matrixActualHash) ('expected=' + $matrixExpectedHash + '; observed=' + $matrixActualHash)
        $matrixChecks = @($matrix.checks)
        $expectedFailedIds = @($matrix.expectedFailedCheckIds | ForEach-Object { [string]$_ })
        $blockingExpectedIds = @($matrixChecks | Where-Object { ([string]$_.layer -eq 'G3-A') -and ([string]$_.expectedDisposition -eq 'reject_until_implemented') -and [bool]$_.countsTowardA0Blocking } | ForEach-Object { [string]$_.id })
        $g3bBlocking = @($matrixChecks | Where-Object { ([string]$_.layer -eq 'G3-B') -and [bool]$_.countsTowardA0Blocking })
        Add-G3AuthorityCheck 'dependent_matrix_expected_failures_exact' ((@($expectedFailedIds | Sort-Object) -join "`n") -eq (@($blockingExpectedIds | Sort-Object) -join "`n")) ('expected=' + ($expectedFailedIds -join ',') + '; blocking=' + ($blockingExpectedIds -join ','))
        Add-G3AuthorityCheck 'dependent_matrix_excludes_g3b_blocking' ($g3bBlocking.Count -eq 0) ('G3-B blocking checks=' + $g3bBlocking.Count)
        if (-not [string]::IsNullOrWhiteSpace($StaticReportPath)) {
            $staticReport = Read-G3Json $StaticReportPath 'StaticReportPath'
            $deferredStaticIds = @($matrixChecks | Where-Object { ([string]$_.layer -eq 'G3-B') -and -not [bool]$_.countsTowardA0Blocking } | ForEach-Object { [string]$_.staticCheckId })
            $actualStaticFailures = @($staticReport.checks | Where-Object { -not [bool]$_.passed -and $deferredStaticIds -notcontains [string]$_.id } | ForEach-Object { [string]$_.id })
            $expectedStaticFailures = @($matrixChecks | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.staticCheckId) -and (([string]$_.layer -eq 'G3-A') -or ([string]$_.expectedDisposition -eq 'legacy_tool_false_negative')) } | ForEach-Object { [string]$_.staticCheckId })
            Add-G3AuthorityCheck 'dependent_static_report_failed_ids_exact' ((@($actualStaticFailures | Sort-Object) -join "`n") -eq (@($expectedStaticFailures | Sort-Object) -join "`n")) ('actual=' + ($actualStaticFailures -join ',') + '; expected=' + ($expectedStaticFailures -join ','))
            Add-G3AuthorityCheck 'dependent_static_report_status_exact' (([string]$staticReport.status -eq [string]$matrix.expectedStatus) -and ([int]$staticReport.exitCode -eq [int]$matrix.expectedExitCode)) ('status=' + $staticReport.status + '; exitCode=' + $staticReport.exitCode)
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($PredecessorApprovalPath) -or -not [string]::IsNullOrWhiteSpace($PredecessorLeasePath)) {
        if ([string]::IsNullOrWhiteSpace($PredecessorApprovalPath) -or [string]::IsNullOrWhiteSpace($PredecessorLeasePath)) { throw 'Both predecessor authority paths are required.' }
        $predecessor = $scope.predecessor
        if ($null -eq $predecessor) { throw 'Scope predecessor is required when predecessor authority paths are supplied.' }
        $predecessorScopeFull = Assert-JointReadablePath (Join-Path $script:JointAwakeRoot ([string]$predecessor.scopePath)) $script:JointAwakeRoot 'predecessor.scopePath'
        $predecessorHash = (Get-FileHash -LiteralPath $predecessorScopeFull -Algorithm SHA256).Hash
        $predecessorApproval = Read-G3Json $PredecessorApprovalPath 'predecessor approval'
        $predecessorLease = Read-G3Json $PredecessorLeasePath 'predecessor lease'
        Add-G3AuthorityCheck 'predecessor_scope_hash_bound' ([string]$predecessor.scopeSha256 -eq $predecessorHash) ('expected=' + $predecessor.scopeSha256 + '; observed=' + $predecessorHash)
        Add-G3AuthorityCheck 'predecessor_approval_bound' (([string]$predecessorApproval.scopeSha256 -eq $predecessorHash) -and ([string]$predecessorApproval.gate -eq 'G3-G0')) 'Predecessor approval binds the predecessor scope.'
        Add-G3AuthorityCheck 'predecessor_user_signoff' ([bool]$predecessorApproval.userSignoff.confirmed) 'Predecessor user signoff is confirmed.'
        Add-G3AuthorityCheck 'predecessor_lease_released' (([string]$predecessorLease.leaseId -eq [string]$predecessor.leaseId) -and ([string]$predecessorLease.status -eq 'released') -and ([string]$predecessorLease.recordStatus -eq 'released')) 'Predecessor lease is the declared released lease.'
    }
    if (-not [string]::IsNullOrWhiteSpace($CompletionPath)) {
        if ([string]::IsNullOrWhiteSpace($CompletionSchemaPath)) { throw 'CompletionSchemaPath is required with CompletionPath.' }
        $completion = Read-G3Json $CompletionPath 'CompletionPath'
        $completionSchema = Read-G3Json $CompletionSchemaPath 'CompletionSchemaPath'
        $completionObject = $completion | ConvertTo-Json -Compress -Depth 20 | ConvertFrom-Json
        $completionObject.PSObject.Properties.Remove('recordSha256')
        $canonical = $completionObject | ConvertTo-Json -Compress -Depth 20
        $digest = [Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))
        $completionHash = (($digest | ForEach-Object ToString x2) -join '').ToUpperInvariant()
        Add-G3AuthorityCheck 'completion_schema_domain' ([string]$completionSchema.digestDomain -eq 'canonical_utf8_json_without_recordSha256_property') 'Completion digest domain is fixed.'
        Add-G3AuthorityCheck 'completion_record_hash' ([string]$completion.recordSha256 -eq $completionHash) ('expected=' + $completion.recordSha256 + '; observed=' + $completionHash)
        $currentVerifierHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
        Add-G3AuthorityCheck 'completion_verifier_hash' ([string]$completion.verifierSha256 -eq $currentVerifierHash) ('expected=' + $completion.verifierSha256 + '; observed=' + $currentVerifierHash)
        Add-G3AuthorityCheck 'completion_status' ([string]$completion.recordStatus -eq 'complete') ('status=' + $completion.recordStatus)
        Add-G3AuthorityCheck 'completion_scope_binding' ([string]$completion.scopeSha256 -eq $scopeHash) 'Completion binds the current scope.'
        Add-G3AuthorityCheck 'completion_lease_released' (([string]$completion.releasedLeaseId -eq [string]$lease.leaseId) -and ([string]$lease.status -eq 'released')) 'Completion binds a released lease.'
        $leaseHash = (Get-FileHash -LiteralPath $leasePathFull -Algorithm SHA256).Hash
        Add-G3AuthorityCheck 'completion_lease_hash' ([string]$completion.leaseSha256 -eq $leaseHash) ('expected=' + $completion.leaseSha256 + '; observed=' + $leaseHash)
    }
    $failed = @($checks | Where-Object { -not $_.passed })
    $report = [ordered]@{ schemaVersion='awake.persona.g3-scope-authority-report.v1'; commandLine=$MyInvocation.Line; normalizedCwd=[IO.Path]::GetFullPath((Get-Location).Path); scopeSha256=$scopeHash; status=if($failed.Count -eq 0){'pass'}else{'reject'}; exitCode=if($failed.Count -eq 0){0}else{10}; checks=@($checks) }
    Write-JointReport $report $reportFull
    exit $report.exitCode
} catch {
    throw
}
