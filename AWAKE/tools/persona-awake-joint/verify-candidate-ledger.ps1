param(
    [Parameter(Mandatory = $true)][string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'persona-awake-joint.ps1')

$reportPathFull = $null
$assertions = [Collections.Generic.List[object]]::new()
$errors = [Collections.Generic.List[object]]::new()
$report = [ordered]@{
    schemaVersion = 'awake.persona.candidate-ledger-verification.v1'
    commandLine = 'pwsh -NoProfile -File "' + [IO.Path]::GetFullPath($PSCommandPath) + '" -ReportPath "' + $ReportPath + '"'
    normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
    status = 'error'
    exitCode = 40
    canonicalPath = $null
    draftPath = $null
    canonicalSha256 = $null
    draftSha256 = $null
    byteIdentical = $false
    ledgerStatus = $null
    activeCandidateId = $null
    evidenceCeiling = $null
    promotionBlocked = $true
    assertions = @()
    observedErrors = @()
}

try {
    $reportPathFull = Assert-JointOutputPath $ReportPath 'ReportPath'
    $canonicalPath = Join-Path $script:JointAwakeRoot 'docs\persona-awake-candidate-ledger.v1.json'
    $draftPath = Join-Path $script:JointAwakeRoot 'docs\persona-awake-candidate-ledger.v1.draft.json'
    $report.canonicalPath = [IO.Path]::GetFullPath($canonicalPath)
    $report.draftPath = [IO.Path]::GetFullPath($draftPath)
    if (-not (Test-Path -LiteralPath $canonicalPath -PathType Leaf)) { throw [FileNotFoundException]::new('Canonical candidate ledger is missing.', $canonicalPath) }
    if (-not (Test-Path -LiteralPath $draftPath -PathType Leaf)) { throw [FileNotFoundException]::new('Draft candidate ledger is missing.', $draftPath) }
    $canonicalBytes = [IO.File]::ReadAllBytes($canonicalPath)
    $draftBytes = [IO.File]::ReadAllBytes($draftPath)
    $report.canonicalSha256 = Get-JointHashBytes $canonicalBytes
    $report.draftSha256 = Get-JointHashBytes $draftBytes
    $byteIdentical = $canonicalBytes.Length -eq $draftBytes.Length -and [Convert]::ToBase64String($canonicalBytes) -ceq [Convert]::ToBase64String($draftBytes)
    $report.byteIdentical = $byteIdentical
    Add-JointAssertion $assertions 'canonical_and_draft_byte_identical' $byteIdentical 'The verifier compares original bytes and never edits either ledger.'
    $canonical = Read-JointJsonFile $canonicalPath 'canonical candidate ledger'
    $draft = Read-JointJsonFile $draftPath 'draft candidate ledger'
    Assert-JointExactFields $canonical.Value @('schemaVersion','ledgerId','status','activeCandidateId','evidenceCeiling','candidates','reconciliationRules') @('schemaVersion','ledgerId','status','activeCandidateId','evidenceCeiling','candidates','reconciliationRules') '$.canonical'
    Assert-JointExactFields $draft.Value @('schemaVersion','ledgerId','status','activeCandidateId','evidenceCeiling','candidates','reconciliationRules') @('schemaVersion','ledgerId','status','activeCandidateId','evidenceCeiling','candidates','reconciliationRules') '$.draft'
    $schema = Get-JointRequiredString $canonical.Value 'schemaVersion' '$.canonical'
    Add-JointAssertion $assertions 'ledger_schema_valid' ($schema -eq 'awake.candidate-ledger.v1') 'Ledger schema is awake.candidate-ledger.v1.'
    if ($schema -ne 'awake.candidate-ledger.v1') { $errors.Add([pscustomobject]@{ code = 'persona.ledger_schema_invalid'; detail = $schema }) }
    $report.ledgerStatus = Get-JointRequiredString $canonical.Value 'status' '$.canonical'
    $report.activeCandidateId = Get-JointJsonProperty $canonical.Value 'activeCandidateId'
    $report.evidenceCeiling = Get-JointRequiredString $canonical.Value 'evidenceCeiling' '$.canonical'
    $statusOkay = $report.ledgerStatus -eq 'needs_reconcile'
    $activeOkay = $null -eq $report.activeCandidateId
    $ceilingOkay = $report.evidenceCeiling -eq 'E2'
    Add-JointAssertion $assertions 'ledger_status_needs_reconcile' $statusOkay 'Current ledger must remain needs_reconcile.'
    Add-JointAssertion $assertions 'ledger_active_candidate_null' $activeOkay 'No active candidate is allowed while the ledger is unreconciled.'
    Add-JointAssertion $assertions 'ledger_evidence_ceiling_e2' $ceilingOkay 'Current evidence ceiling must remain E2.'
    if (-not $statusOkay) { $errors.Add([pscustomobject]@{ code = 'persona.ledger_status_unexpected'; detail = [string]$report.ledgerStatus }) }
    if (-not $activeOkay) { $errors.Add([pscustomobject]@{ code = 'persona.ledger_active_candidate_unexpected'; detail = [string]$report.activeCandidateId }) }
    if (-not $ceilingOkay) { $errors.Add([pscustomobject]@{ code = 'persona.ledger_evidence_ceiling_unexpected'; detail = [string]$report.evidenceCeiling }) }
    if (-not $byteIdentical) { $errors.Add([pscustomobject]@{ code = 'persona.ledger_draft_mismatch'; detail = 'Canonical and draft bytes differ.' }) }
    if ($errors.Count -gt 0) {
        $report.status = 'reject'
        $report.exitCode = 10
        $report.observedErrors = @($errors)
    } else {
        $report.status = 'pass'
        $report.exitCode = 0
        $report.observedErrors = @()
    }
} catch {
    $errorRecord = Convert-JointExceptionToError $_.Exception 'verify-candidate-ledger'
    $report.status = 'error'
    $report.exitCode = 40
    $report.observedErrors = @($errorRecord)
} finally {
    $report.assertions = @($assertions)
    if ($null -ne $reportPathFull) { Write-JointReport $report $reportPathFull }
}

exit ([int]$report.exitCode)
