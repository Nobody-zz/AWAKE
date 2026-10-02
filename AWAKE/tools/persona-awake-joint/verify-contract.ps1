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
    schemaVersion = 'awake.persona.contract-verification.v1'
    commandLine = 'pwsh -NoProfile -File "' + [IO.Path]::GetFullPath($PSCommandPath) + '" -ReportPath "' + $ReportPath + '"'
    normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
    status = 'error'
    exitCode = 40
    assertions = @()
    observedErrors = @()
    contractLockSha256 = $null
    checkedPaths = @()
}

try {
    $reportPathFull = Assert-JointOutputPath $ReportPath 'ReportPath'
    $contractPath = Join-Path $script:JointAwakeRoot 'docs\PLAN-PersonaWorkbench-AWAKE-Joint-CONTRACT-LOCK-20260823.md'
    $planPath = Join-Path $script:JointAwakeRoot 'docs\PLAN-PersonaWorkbench-AWAKE-Joint-20260823.md'
    if (-not (Test-Path -LiteralPath $contractPath -PathType Leaf)) { throw [FileNotFoundException]::new('Contract-Lock is missing.', $contractPath) }
    if (-not (Test-Path -LiteralPath $planPath -PathType Leaf)) { throw [FileNotFoundException]::new('Joint plan is missing.', $planPath) }
    $contractText = [IO.File]::ReadAllText($contractPath, $script:JointUtf8)
    $report.contractLockSha256 = Get-JointHashFile $contractPath
    $report.checkedPaths = @($contractPath, $planPath)
    $requiredCommands = @(
        'pwsh -NoProfile -File tools/persona-awake-joint/run-fixtures.ps1 -FixtureId <id> -ReportPath <path>',
        'pwsh -NoProfile -File tools/persona-awake-joint/verify-contract.ps1 -ReportPath <path>',
        'pwsh -NoProfile -File tools/persona-awake-joint/verify-old-entry-scan.ps1 -ReportPath <path>',
        'pwsh -NoProfile -File tools/persona-awake-joint/verify-candidate-ledger.ps1 -ReportPath <path>',
        'pwsh -NoProfile -File tools/persona-awake-joint/verify-runtime-bridge-static.ps1 -ReportPath <path>',
        'pwsh -NoProfile -File tools/persona-awake-joint/verify-runtime-static-evidence.ps1 -InputReportPath <path> -ReportPath <path> -Case auto',
        'pwsh -NoProfile -File tools/persona-awake-joint/verify-native-prerequisite.ps1 -ReportPath <path>',
        'pwsh -NoProfile -File tools/persona-awake-joint/verify-g3-s0-scope.ps1 -ReportPath <path>',
        'pwsh -NoProfile -File tools/persona-awake-joint/verify-g3-s0-focused-evidence.ps1 -InputReportPath <path> -ReportPath <path>',
        'pwsh -NoProfile -File tools/persona-awake-joint/verify-g3-plan.ps1 -ReportPath <path>',
        'pwsh -NoProfile -File tools/persona-awake-joint/verify-e2-matrix.ps1 -ReportPath <path>'
    )
    foreach ($command in $requiredCommands) {
        $present = $contractText.Contains($command, [StringComparison]::Ordinal)
        $commandName = ([regex]::Match($command, 'tools/persona-awake-joint/([^ ]+)')).Groups[1].Value
        Add-JointAssertion $assertions ('command_contract:' + $commandName) $present 'Contract-Lock command line is present.'
        if (-not $present) { $errors.Add([pscustomobject]@{ code = 'persona.contract_command_missing'; detail = $command }) }
    }
    $requiredToolFiles = @('persona-awake-joint.ps1','run-fixtures.ps1','verify-contract.ps1','verify-old-entry-scan.ps1','verify-candidate-ledger.ps1','verify-runtime-bridge-static.ps1','verify-runtime-static-evidence.ps1','verify-native-prerequisite.ps1','verify-g3-s0-scope.ps1','verify-g3-s0-focused-evidence.ps1','verify-g3-scope-authority.ps1','verify-g3-plan.ps1','verify-e2-matrix.ps1')
    foreach ($fileName in $requiredToolFiles) {
        $path = Join-Path $script:JointToolRoot $fileName
        $present = Test-Path -LiteralPath $path -PathType Leaf
        Add-JointAssertion $assertions ('tool_file:' + $fileName) $present 'Required offline tool file is present.'
        if (-not $present) { $errors.Add([pscustomobject]@{ code = 'persona.tool_file_missing'; detail = $path }) }
    }
    $schemaDirectory = Join-Path $script:JointAwakeRoot 'docs\persona-contract'
    $requiredContractFiles = @(
        'awake.persona.authoring.v2.schema.json',
        'awake.persona.selection.v1.schema.json',
        'awake.persona.export.v1.schema.json',
        'awake.persona.definition.v1.schema.json',
        'awake.persona.tags.v1.schema.json',
        'awake.persona.fixture-report.v1.schema.json',
        'persona-workbench-to-awake.crosswalk.v1.json',
        'mapping-report.v1.schema.json',
        'adapter-error.v1.schema.json',
        'awake.persona.g3-s0-focused-report.v1.schema.json',
        'awake.persona.g3-s0-focused-trace.v1.schema.json',
        'awake.persona.g3-s0-approval.v1.schema.json',
        'awake.persona.g3-s0-lease.v1.schema.json'
    )
    $missingContractFiles = [Collections.Generic.List[string]]::new()
    foreach ($fileName in $requiredContractFiles) {
        $path = Join-Path $schemaDirectory $fileName
        $present = Test-Path -LiteralPath $path -PathType Leaf
        Add-JointAssertion $assertions ('contract_artifact:' + $fileName) $present 'Contract artifact is present.'
        if (-not $present) { $missingContractFiles.Add($path) }
    }
    $runtimeStaticPassFixture = Join-Path $script:JointAwakeRoot 'docs\fixtures\persona-awake-joint\runtime-static-pass-evidence.json'
    $runtimeStaticPassFixturePresent = Test-Path -LiteralPath $runtimeStaticPassFixture -PathType Leaf
    Add-JointAssertion $assertions 'runtime_static_pass_fixture' $runtimeStaticPassFixturePresent 'Synthetic pass fixture is present for the evidence verifier.'
    if (-not $runtimeStaticPassFixturePresent) { $missingContractFiles.Add($runtimeStaticPassFixture) }
    $g3S0ScopeManifest = Join-Path $script:JointAwakeRoot 'docs\persona-awake-joint-g3-s0-scope.v1.json'
    $g3S0ScopeManifestPresent = Test-Path -LiteralPath $g3S0ScopeManifest -PathType Leaf
    Add-JointAssertion $assertions 'g3_s0_scope_manifest' $g3S0ScopeManifestPresent 'G3-S0 exact-scope manifest is present.'
    if (-not $g3S0ScopeManifestPresent) { $missingContractFiles.Add($g3S0ScopeManifest) }
    foreach ($authorityFileName in @('persona-awake-joint-g3-s0-approval.v1.json','persona-awake-joint-g3-s0-lease.v1.json')) {
        $authorityPath = Join-Path $script:JointAwakeRoot ('docs\' + $authorityFileName)
        $authorityPresent = Test-Path -LiteralPath $authorityPath -PathType Leaf
        Add-JointAssertion $assertions ('g3_s0_authority:' + $authorityFileName) $authorityPresent 'Detached G3-S0 authority record is present.'
        if (-not $authorityPresent) { $missingContractFiles.Add($authorityPath) }
    }
    $writeSetMarker = 'AWAKE/tools/persona-awake-joint/*'
    $writeSetPresent = $contractText.Contains($writeSetMarker, [StringComparison]::Ordinal)
    Add-JointAssertion $assertions 'isolated_tool_write_set' $writeSetPresent 'Contract-Lock names the isolated tool write set.'
    if (-not $writeSetPresent) { $errors.Add([pscustomobject]@{ code = 'persona.write_set_missing'; detail = $writeSetMarker }) }
    $forbiddenMarkers = @('src','ModuleData','dist, game','frozen roots')
    foreach ($marker in $forbiddenMarkers) {
        $present = $contractText.Contains($marker, [StringComparison]::Ordinal)
        Add-JointAssertion $assertions ('protected_boundary:' + $marker) $present 'Contract-Lock records the protected boundary.'
        if (-not $present) { $errors.Add([pscustomobject]@{ code = 'persona.protected_boundary_missing'; detail = $marker }) }
    }
    if ($errors.Count -eq 0 -and $missingContractFiles.Count -eq 0) {
        $runtimeStaticEvidenceVerifier = Join-Path $script:JointToolRoot 'verify-runtime-static-evidence.ps1'
        $runtimeStaticEvidenceOutput = Join-Path $script:JointToolRoot 'artifacts\.verify-contract-runtime-static-evidence.json'
        $null = & pwsh -NoProfile -File $runtimeStaticEvidenceVerifier -InputReportPath $runtimeStaticPassFixture -ReportPath $runtimeStaticEvidenceOutput -Case pass
        $runtimeStaticEvidenceExitCode = $LASTEXITCODE
        $runtimeStaticEvidenceStatus = $null
        if (Test-Path -LiteralPath $runtimeStaticEvidenceOutput -PathType Leaf) {
            $runtimeStaticEvidenceReport = Read-JointJsonFile $runtimeStaticEvidenceOutput 'runtime static evidence verification report'
            $runtimeStaticEvidenceStatus = [string](Get-JointJsonProperty $runtimeStaticEvidenceReport.Value 'status')
        }
        $runtimeStaticEvidencePassed = $runtimeStaticEvidenceExitCode -eq 0 -and $runtimeStaticEvidenceStatus -eq 'pass'
        Add-JointAssertion $assertions 'runtime_static_evidence_executes' $runtimeStaticEvidencePassed ('status=' + [string]$runtimeStaticEvidenceStatus + '; exitCode=' + [string]$runtimeStaticEvidenceExitCode)
        if (-not $runtimeStaticEvidencePassed) {
            $errors.Add([pscustomobject]@{
                schemaVersion = 'awake.persona.adapter-error.v1'
                errorId = Get-JointStableHashId 'error' ('persona.runtime_static_evidence_invalid|verify-contract|' + $runtimeStaticEvidenceExitCode + '|' + $runtimeStaticEvidenceStatus)
                code = 'persona.runtime_static_evidence_invalid'
                stage = 'runtime_static_evidence'
                artifactId = 'verify_contract'
                path = '$.runtimeStaticEvidence'
                detail = 'Contract verifier could not establish pass/0 for the synthetic runtime-static evidence case.'
                retryable = $false
            })
        }
    }
    if ($errors.Count -gt 0) {
        $report.status = 'reject'
        $report.exitCode = 10
        $report.observedErrors = @($errors)
    } elseif ($missingContractFiles.Count -gt 0) {
        $report.status = 'not_attempted'
        $report.exitCode = 30
        $report.observedErrors = @([pscustomobject]@{ code = 'persona.contract_artifacts_not_created'; detail = ($missingContractFiles -join '; ') })
    } else {
        $report.status = 'pass'
        $report.exitCode = 0
    }
} catch {
    $errorRecord = Convert-JointExceptionToError $_.Exception 'verify-contract'
    $report.status = 'error'
    $report.exitCode = 40
    $report.observedErrors = @($errorRecord)
} finally {
    $report.assertions = @($assertions)
    if ($null -ne $reportPathFull) {
        Write-JointReport $report $reportPathFull
    } else {
        # The report path is validated before anything else, so a rejected path
        # leaves no report on disk. Without this branch the tool would exit 40 in
        # total silence -- which is how a relative -ReportPath was mistaken for a
        # broken chain. The reason goes to stderr; the report is deliberately NOT
        # written, because writing outside the tool root is what the guard forbids.
        $reason = 'report path was not resolved.'
        if ($report.observedErrors.Count -gt 0) { $reason = [string]$report.observedErrors[0].detail }
        [Console]::Error.WriteLine('verify-contract: ' + [string]$report.status + '/' + [string]$report.exitCode + ' :: ' + $reason)
        [Console]::Error.WriteLine('verify-contract: -ReportPath must resolve under ' + $script:JointToolRoot + '; pass an absolute path.')
    }
}

exit ([int]$report.exitCode)
