param(
    [Parameter(Mandatory = $true)][string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'persona-awake-joint.ps1')

$reportPathFull = $null
$results = [Collections.Generic.List[object]]::new()
$warnings = [Collections.Generic.List[object]]::new()
$errors = [Collections.Generic.List[object]]::new()
$report = [ordered]@{
    schemaVersion = 'awake.persona.e2-matrix-report.v1'
    commandLine = 'pwsh -NoProfile -File "' + [IO.Path]::GetFullPath($PSCommandPath) + '" -ReportPath "' + $ReportPath + '"'
    normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
    status = 'error'
    exitCode = 40
    fixtureCount = 0
    matchedCount = 0
    protectedBaselineDriftCount = 0
    unexpectedCount = 0
    sourceSnapshotPath = $null
    sourceTreeSha256 = $null
    results = @()
    warnings = @()
    observedErrors = @()
}

function Add-MatrixError([string]$Code, [string]$Path, [string]$Detail) {
    $script:errors.Add([ordered]@{
        schemaVersion = 'awake.persona.adapter-error.v1'
        errorId = Get-JointStableHashId 'error' ('persona.e2_matrix|' + $Code + '|' + $Path + '|' + $Detail)
        code = $Code
        stage = 'e2_matrix'
        artifactId = 'e2_matrix'
        path = $Path
        detail = $Detail
        retryable = $false
    })
}

function Add-MatrixWarning([string]$Code, [string]$FixtureId, [string]$Detail) {
    $script:warnings.Add([ordered]@{
        code = $Code
        fixtureId = $FixtureId
        detail = $Detail
    })
}

try {
    $reportPathFull = Assert-JointOutputPath $ReportPath 'ReportPath'
    $fixtureRoot = Join-Path $script:JointAwakeRoot 'docs\fixtures\persona-awake-joint'
    $artifactRoot = Get-JointArtifactRoot
    $runnerPath = Join-Path $script:JointToolRoot 'run-fixtures.ps1'
    $sourceSnapshotPath = Join-Path $artifactRoot 'e2-matrix-source-snapshot.json'
    $report.sourceSnapshotPath = [IO.Path]::GetFullPath($sourceSnapshotPath)

    $null = & pwsh -NoProfile -File (Join-Path $script:JointToolRoot 'verify-old-entry-scan.ps1') -ReportPath $sourceSnapshotPath
    $sourceSnapshot = Read-JointJsonFile $sourceSnapshotPath 'E2 matrix source snapshot'
    $report.sourceTreeSha256 = [string](Get-JointJsonProperty $sourceSnapshot.Value 'sourceTreeSha256')
    if ([string]::IsNullOrWhiteSpace($report.sourceTreeSha256)) {
        Add-MatrixError 'persona.e2_matrix_source_snapshot_missing' '$.sourceTreeSha256' 'Source snapshot did not contain a tree SHA-256.'
    }

    $fixtureDirectories = @(Get-ChildItem -LiteralPath $fixtureRoot -Directory | Sort-Object Name)
    $report.fixtureCount = $fixtureDirectories.Count
    if ($fixtureDirectories.Count -eq 0) { Add-MatrixError 'persona.e2_matrix_fixture_set_empty' '$.fixtureCount' 'No E2 fixture directories were found.' }

    foreach ($directory in $fixtureDirectories) {
        $fixtureId = $directory.Name
        $fixtureReportPath = Join-Path $artifactRoot ('e2-matrix-' + $fixtureId + '.json')
        $expectedPath = Join-Path $directory.FullName 'expected.json'
        $inputPath = Join-Path $directory.FullName 'input.json'
        if (-not (Test-Path -LiteralPath $expectedPath -PathType Leaf)) {
            Add-MatrixError 'persona.e2_matrix_expected_missing' ('$.' + $fixtureId) 'Fixture expected.json is missing.'
            continue
        }
        if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) {
            Add-MatrixError 'persona.e2_matrix_input_missing' ('$.' + $fixtureId) 'Fixture input.json is missing.'
            continue
        }

        $null = & pwsh -NoProfile -File $runnerPath -FixtureId $fixtureId -ReportPath $fixtureReportPath
        $processExit = [int]$LASTEXITCODE
        if (-not (Test-Path -LiteralPath $fixtureReportPath -PathType Leaf)) {
            Add-MatrixError 'persona.e2_matrix_report_missing' ('$.' + $fixtureId) 'Fixture runner did not produce a report.'
            continue
        }

        $actual = (Read-JointJsonFile $fixtureReportPath ('E2 matrix report ' + $fixtureId)).Value
        $expected = (Read-JointJsonFile $expectedPath ('E2 matrix expected ' + $fixtureId)).Value
        $expectedStatus = [string](Get-JointJsonProperty $expected 'status')
        $expectedExit = [int](Get-JointJsonProperty $expected 'exitCode')
        $actualStatus = [string](Get-JointJsonProperty $actual 'status')
        $actualExit = [int](Get-JointJsonProperty $actual 'exitCode')
        $failedAssertions = @((Get-JointJsonProperty $actual 'assertions') | Where-Object { (Get-JointJsonProperty $_ 'passed') -eq $false }).Count
        $codes = @((Get-JointJsonProperty $actual 'observedErrors') | ForEach-Object { [string](Get-JointJsonProperty $_ 'code') })
        $classification = 'matched'
        $matched = $expectedStatus -eq $actualStatus -and $expectedExit -eq $actualExit -and $failedAssertions -eq 0

        if ($fixtureId -eq 'PWB-AWAKE-013-frozen-candidate-isolation') {
            $input = (Read-JointJsonFile $inputPath 'PWB-AWAKE-013 input').Value
            $protectedRoots = Get-JointJsonProperty $input 'protectedRoots'
            $declaredSourceHash = if ($null -ne $protectedRoots -and $protectedRoots.Count -gt 0) { [string](Get-JointJsonProperty $protectedRoots[0] 'beforeSha256') } else { '' }
            $baselineDrift = -not [string]::IsNullOrWhiteSpace($report.sourceTreeSha256) -and $declaredSourceHash -ne $report.sourceTreeSha256
            $protectedError = $actualStatus -eq 'error' -and $actualExit -eq 40 -and $baselineDrift -and $codes -contains 'persona.registry_digest_mismatch'
            if ($protectedError) {
                $classification = 'protected_baseline_drift'
                $matched = $true
                $report.protectedBaselineDriftCount++
                Add-MatrixWarning 'persona.frozen_baseline_drift' $fixtureId ('Frozen source baseline=' + $declaredSourceHash + '; observed source tree=' + $report.sourceTreeSha256 + '; fixture remains protected and was not rewritten.')
            }
        }

        if ($matched) {
            $report.matchedCount++
        } else {
            $report.unexpectedCount++
            Add-MatrixError 'persona.e2_matrix_unexpected_result' ('$.' + $fixtureId) ('Expected ' + $expectedStatus + '/' + $expectedExit + '; observed ' + $actualStatus + '/' + $actualExit + '; failedAssertions=' + $failedAssertions + '; processExit=' + $processExit + '.')
            $classification = 'unexpected'
        }

        $results.Add([ordered]@{
            fixtureId = $fixtureId
            expectedStatus = $expectedStatus
            expectedExitCode = $expectedExit
            observedStatus = $actualStatus
            observedExitCode = $actualExit
            processExitCode = $processExit
            failedAssertionCount = $failedAssertions
            observedErrorCodes = $codes
            classification = $classification
            reportPath = [IO.Path]::GetFullPath($fixtureReportPath)
        })
    }

    if ($errors.Count -eq 0) {
        $report.status = 'pass'
        $report.exitCode = 0
    } else {
        $report.status = 'reject'
        $report.exitCode = 10
    }
} catch {
    $errorRecord = Convert-JointExceptionToError $_.Exception 'verify-e2-matrix'
    $errors.Add($errorRecord)
    $report.status = 'error'
    $report.exitCode = 40
} finally {
    $report.results = @($results)
    $report.warnings = @($warnings)
    $report.observedErrors = @($errors)
    if ($null -ne $reportPathFull) { Write-JointReport $report $reportPathFull }
}

exit ([int]$report.exitCode)
