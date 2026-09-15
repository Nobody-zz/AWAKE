param(
    [string]$ScopePath = 'docs/persona-awake-joint-g3-a-scope.v1.json',
    [string]$ExpectedA0ApprovalSha256,
    [string]$ExpectedA0LeaseSha256,
    [Parameter(Mandatory = $true)][string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'persona-awake-joint.ps1')

function Read-VerifiedJson([string]$Path, [string]$Label) {
    $full = Assert-JointReadablePath $Path $script:JointAwakeRoot $Label
    return Read-JointJsonFile $full $Label
}

$checks = [Collections.Generic.List[object]]::new()
function Add-Check([string]$Id, [bool]$Passed, [string]$Detail) {
    $checks.Add([ordered]@{ id = $Id; passed = $Passed; detail = $Detail })
}

try {
    $null = Assert-JointOutputPath $ReportPath 'ReportPath'
    $scopeRecord = Read-VerifiedJson $ScopePath 'G3-A scope'
    $scope = $scopeRecord.Value
    $predecessor = $scope.predecessor
    $a0ScopePath = [string]$predecessor.a0ScopePath
    $a0ScopeExpectedHash = [string]$predecessor.a0ScopeSha256
    $a0LeaseId = [string]$predecessor.a0LeaseId
    $a0ScopeRecord = Read-VerifiedJson $a0ScopePath 'G3-A0 scope'
    $a0Scope = $a0ScopeRecord.Value
    Add-Check 'a0_scope_hash_bound' ($a0ScopeRecord.RawSha256 -eq $a0ScopeExpectedHash) ('expected=' + $a0ScopeExpectedHash + '; observed=' + $a0ScopeRecord.RawSha256)
    Add-Check 'a0_scope_identity' (([string]$a0Scope.gate -eq 'G3-A0') -and ([string]$a0Scope.taskId -eq 'PERSONA-AWAKE-JOINT-G3-A0-20260911')) 'A0 scope has the expected gate and task identity.'

    $approvalRecord = Read-VerifiedJson ([string]$a0Scope.authority.approvalRecord) 'G3-A0 approval'
    $leaseRecord = Read-VerifiedJson ([string]$a0Scope.authority.leaseRecord) 'G3-A0 lease'
    $approval = $approvalRecord.Value
    $lease = $leaseRecord.Value
    $approvalExpectedHash = if ([string]::IsNullOrWhiteSpace($ExpectedA0ApprovalSha256)) { [string]$predecessor.a0ApprovalSha256 } else { $ExpectedA0ApprovalSha256 }
    $leaseExpectedHash = if ([string]::IsNullOrWhiteSpace($ExpectedA0LeaseSha256)) { [string]$predecessor.a0LeaseSha256 } else { $ExpectedA0LeaseSha256 }
    Add-Check 'a0_authority_hashes_supplied' (($approvalExpectedHash -match '^[A-F0-9]{64}$') -and ($leaseExpectedHash -match '^[A-F0-9]{64}$')) 'A0 approval and lease raw SHA-256 values are explicit.'
    Add-Check 'a0_approval_record_hash_bound' ($approvalRecord.RawSha256 -eq $approvalExpectedHash) ('expected=' + $approvalExpectedHash + '; observed=' + $approvalRecord.RawSha256)
    Add-Check 'a0_lease_record_hash_bound' ($leaseRecord.RawSha256 -eq $leaseExpectedHash) ('expected=' + $leaseExpectedHash + '; observed=' + $leaseRecord.RawSha256)
    Add-Check 'a0_approval_identity_bound' (([string]$approval.scopeSha256 -eq $a0ScopeRecord.RawSha256) -and ([string]$approval.gate -eq 'G3-A0') -and ([string]$approval.taskId -eq [string]$a0Scope.taskId) -and ([string]$approval.batchId -eq [string]$a0Scope.batchId) -and ([string]$approval.scopePath -eq $a0ScopePath)) 'A0 approval binds the exact scope and identity.'
    Add-Check 'a0_approval_signed' (([string]$approval.recordStatus -eq 'approved') -and [bool]$approval.userSignoff.confirmed) 'A0 approval is approved and user-signed.'
    Add-Check 'a0_lease_released' (([string]$lease.recordStatus -eq 'released') -and ([string]$lease.status -eq 'released') -and ([string]$lease.leaseId -eq $a0LeaseId)) 'A0 lease is the declared released lease.'
    Add-Check 'a0_lease_identity_bound' (([string]$lease.scopeSha256 -eq $a0ScopeRecord.RawSha256) -and ([string]$lease.gate -eq 'G3-A0') -and ([string]$lease.taskId -eq [string]$a0Scope.taskId) -and ([string]$lease.batchId -eq [string]$a0Scope.batchId) -and ([string]$lease.scopePath -eq $a0ScopePath)) 'A0 released lease binds the exact scope and identity.'
    $scopeSet = @($a0Scope.writeSet | ForEach-Object { ([string]$_).Replace('\','/').ToLowerInvariant() } | Sort-Object)
    $leaseSet = @($lease.writeSet | ForEach-Object { ([string]$_).Replace('\','/').ToLowerInvariant() } | Sort-Object)
    Add-Check 'a0_lease_write_set_exact' (($scopeSet -join "`n") -eq ($leaseSet -join "`n")) 'A0 scope and released lease have the same normalized write set.'

    $failed = @($checks | Where-Object { -not $_.passed })
    $report = [ordered]@{
        schemaVersion = 'awake.persona.g3-a0-predecessor-report.v1'
        commandLine = $MyInvocation.Line
        normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
        scopeSha256 = $scopeRecord.RawSha256
        a0ScopeSha256 = $a0ScopeRecord.RawSha256
        status = if ($failed.Count -eq 0) { 'pass' } else { 'reject' }
        exitCode = if ($failed.Count -eq 0) { 0 } else { 10 }
        checks = @($checks)
    }
    Write-JointReport $report $ReportPath
    exit $report.exitCode
} catch {
    $report = [ordered]@{
        schemaVersion = 'awake.persona.g3-a0-predecessor-report.v1'
        commandLine = $MyInvocation.Line
        normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
        status = 'reject'
        exitCode = 10
        checks = @($checks)
        error = $_.Exception.Message
    }
    Write-JointReport $report $ReportPath
    exit 10
}
