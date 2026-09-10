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
    schemaVersion = 'awake.persona.g3-plan-verification.v1'
    commandLine = 'pwsh -NoProfile -File "' + [IO.Path]::GetFullPath($PSCommandPath) + '" -ReportPath "' + $ReportPath + '"'
    normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
    status = 'error'
    exitCode = 40
    taskGraph = 'G3-S0 -> G3-A -> G3-B -> G3-C -> G4'
    checkedPaths = @()
    planStatus = $null
    reviewStatus = $null
    scopeStatus = $null
    scopeSchemaVersion = $null
    assertions = @()
    observedErrors = @()
}

function Add-G3PlanAssertion([string]$Id, [bool]$Passed, [string]$Detail) {
    $script:assertions.Add([ordered]@{
        id = $Id
        passed = $Passed
        detail = $Detail
    })
    if (-not $Passed) {
        $script:errors.Add([pscustomobject]@{
            schemaVersion = 'awake.persona.adapter-error.v1'
            errorId = Get-JointStableHashId 'error' ('persona.g3_plan_invalid|' + $Id + '|' + $Detail)
            code = 'persona.g3_plan_invalid'
            stage = 'plan_verification'
            artifactId = 'g3_plan'
            path = '$.' + $Id
            detail = $Detail
            retryable = $false
        })
    }
}

try {
    $reportPathFull = Assert-JointOutputPath $ReportPath 'ReportPath'
    $paths = [ordered]@{
        g3Plan = Join-Path $script:JointAwakeRoot 'docs\PLAN-PersonaWorkbench-AWAKE-Joint-G3-EXECUTION-GATE-20260824.md'
        reviewLog = Join-Path $script:JointAwakeRoot 'docs\PLAN-PersonaWorkbench-AWAKE-Joint-G3-EXECUTION-GATE-REVIEW-LOG-20260824.md'
        nextGate = Join-Path $script:JointAwakeRoot 'docs\PLAN-PersonaWorkbench-AWAKE-Joint-NEXT-GATE-20260824.md'
        architecture = Join-Path $script:JointAwakeRoot 'docs\PLAN-PersonaWorkbench-AWAKE-Joint-ARCHITECTURE-MATRIX-20260824.md'
        evidenceMatrix = Join-Path $script:JointAwakeRoot 'docs\PLAN-PersonaWorkbench-AWAKE-Joint-REQUIREMENT-EVIDENCE-MATRIX-20260824.md'
        g3S0Scope = Join-Path $script:JointAwakeRoot 'docs\persona-awake-joint-g3-s0-scope.v1.json'
    }
    $report.checkedPaths = @($paths.Values | ForEach-Object { [IO.Path]::GetFullPath($_) })
    foreach ($path in $paths.Values) {
        Add-G3PlanAssertion ('file_present:' + [IO.Path]::GetFileName($path)) (Test-Path -LiteralPath $path -PathType Leaf) ('Path=' + [IO.Path]::GetFullPath($path))
    }

    $planText = [IO.File]::ReadAllText($paths.g3Plan, $script:JointUtf8)
    $reviewText = [IO.File]::ReadAllText($paths.reviewLog, $script:JointUtf8)
    $nextGateText = [IO.File]::ReadAllText($paths.nextGate, $script:JointUtf8)
    $architectureText = [IO.File]::ReadAllText($paths.architecture, $script:JointUtf8)
    $evidenceMatrixText = [IO.File]::ReadAllText($paths.evidenceMatrix, $script:JointUtf8)
    $scope = (Read-JointJsonFile (Assert-JointReadablePath $paths.g3S0Scope $script:JointAwakeRoot 'G3-S0 scope') 'G3-S0 scope').Value
    $report.scopeSchemaVersion = [string](Get-JointJsonProperty $scope 'schemaVersion')
    $report.scopeStatus = [string](Get-JointJsonProperty $scope 'status')
    $planStatusMatch = [Regex]::Match($planText, '(?m)^> 状态：`([^`]+)`')
    $reviewStatusMatch = [Regex]::Match($reviewText, '(?m)^- 状态：`([^`]+)`')
    $report.planStatus = if ($planStatusMatch.Success) { $planStatusMatch.Groups[1].Value } else { 'UNKNOWN' }
    $report.reviewStatus = if ($reviewStatusMatch.Success) { $reviewStatusMatch.Groups[1].Value } else { 'UNKNOWN' }

    $headings = [ordered]@{
        g3_s0 = '## 4. G3-S0：Storage readiness contract'
        g3_a = '## 5. G3-A：Native Persona runtime projection'
        g3_b = '## 6. G3-B：Persona persistence'
        g3_c = '## 7. G3-C：Offline integrated contract'
    }
    $positions = [ordered]@{}
    foreach ($key in $headings.Keys) {
        $position = $planText.IndexOf($headings[$key], [StringComparison]::Ordinal)
        $positions[$key] = $position
        Add-G3PlanAssertion ('heading_present:' + $key) ($position -ge 0) ('Heading=' + $headings[$key] + '; position=' + $position)
    }
    $ordered = $positions.g3_s0 -ge 0 -and $positions.g3_a -gt $positions.g3_s0 -and $positions.g3_b -gt $positions.g3_a -and $positions.g3_c -gt $positions.g3_b
    Add-G3PlanAssertion 'heading_order' $ordered ('G3-S0=' + $positions.g3_s0 + '; G3-A=' + $positions.g3_a + '; G3-B=' + $positions.g3_b + '; G3-C=' + $positions.g3_c)

    Add-G3PlanAssertion 'task_graph_exact' $planText.Contains('G3-S0 -> G3-A -> G3-B -> G3-C -> G4', [StringComparison]::Ordinal) 'The machine-checkable serial task graph is present.'
    Add-G3PlanAssertion 'plan_blocked_status' ($report.planStatus -eq 'PREPARED / BLOCKED_PENDING_SCOPE_AND_LEASE') ('Current plan status=' + $report.planStatus + '; runtime implementation remains unauthorized.')
    Add-G3PlanAssertion 'review_pending_status' ($report.reviewStatus -eq 'PREPARED / REVIEW_PENDING') ('Current review status=' + $report.reviewStatus + '; an independent verdict is still required.')
    Add-G3PlanAssertion 'no_vague_runtime_filenames' (-not $planText.Contains('文件名在 lease 签发时冻结', [StringComparison]::Ordinal)) 'Runtime write set uses concrete file names.'
    Add-G3PlanAssertion 's0_separate_from_persistence' ($planText.Contains('`G3-S0` 只证明', [StringComparison]::Ordinal) -and $planText.Contains('完整存档连续性仍只属于 G3-B', [StringComparison]::Ordinal)) 'Storage readiness is not conflated with persistence completion.'
    Add-G3PlanAssertion 'next_gate_mentions_s0' $nextGateText.Contains('G3-S0：Storage readiness contract', [StringComparison]::Ordinal) 'Next-Gate includes the readiness batch.'
    Add-G3PlanAssertion 'architecture_mentions_s0' $architectureText.Contains('### G3-S0：Storage readiness contract', [StringComparison]::Ordinal) 'Architecture matrix includes the readiness batch.'
    Add-G3PlanAssertion 'evidence_matrix_current' ($evidenceMatrixText.Contains('G3-S0 pending / G3-A blocked / G3-B blocked / G4 not_attempted', [StringComparison]::Ordinal) -and $evidenceMatrixText.Contains('联合目标证据矩阵', [StringComparison]::Ordinal)) 'Requirement/evidence matrix records the current gate state.'
    Add-G3PlanAssertion 's0_scope_schema' ($report.scopeSchemaVersion -eq 'awake.persona.g3-s0-scope.v1') ('Observed scope schema=' + $report.scopeSchemaVersion)
    Add-G3PlanAssertion 's0_scope_identity' (([string](Get-JointJsonProperty $scope 'gate') -eq 'G3-S0') -and ([string](Get-JointJsonProperty $scope 'taskId') -eq 'PERSONA-AWAKE-JOINT-G3-S0-20260824')) 'G3-S0 scope manifest identity is pinned.'
    Add-G3PlanAssertion 's0_scope_revision' ([int](Get-JointJsonProperty $scope 'scopeRevision') -eq 2) 'G3-S0 scope revision is pinned to the reviewed revision.'
    Add-G3PlanAssertion 's0_scope_state_supported' ($report.scopeStatus -in @('pending_approval','approved')) ('Observed scope status=' + $report.scopeStatus)
    $scopeWriteSet = @((Get-JointJsonProperty $scope 'writeSet') | ForEach-Object { [string]$_ })
    Add-G3PlanAssertion 's0_scope_write_set_present' ($scopeWriteSet.Count -eq 6 -and -not ($scopeWriteSet -contains '_houkai_merge/AWAKE/tools/persona-awake-joint/*')) 'G3-S0 scope declares six source/test files and excludes the isolated tooling set.'
    $authority = Get-JointJsonProperty $scope 'authority'
    Add-G3PlanAssertion 's0_detached_authority_paths' (([string](Get-JointJsonProperty $authority 'approvalRecord') -eq 'docs/persona-awake-joint-g3-s0-approval.v1.json') -and ([string](Get-JointJsonProperty $authority 'leaseRecord') -eq 'docs/persona-awake-joint-g3-s0-lease.v1.json') -and ([string](Get-JointJsonProperty $authority 'scopeDigest') -eq 'raw_utf8_sha256_of_this_manifest')) 'G3-S0 approval and lease authority are detached and hash-bound.'
    $typedSchemaIds = @((Get-JointJsonProperty $scope 'typedSchemas') | ForEach-Object { [string](Get-JointJsonProperty $_ 'schemaId') })
    Add-G3PlanAssertion 's0_typed_schema_ids_locked' ((@($typedSchemaIds | Sort-Object) -join "`n") -eq (@('awake.persona.continuity.v1','awake.persona.override.v1','awake.persona.recovery.v1' | Sort-Object) -join "`n")) 'G3-S0 locks the three Persona typed schema IDs.'
    $worldbookSyncKeys = @((Get-JointJsonProperty (Get-JointJsonProperty $scope 'compatibilityBoundaries') 'worldbookSyncData') | ForEach-Object { [string](Get-JointJsonProperty $_ 'syncKey') })
    Add-G3PlanAssertion 's0_worldbook_syncdata_keys_locked' ((@($worldbookSyncKeys | Sort-Object) -join "`n") -eq (@('awake_worldbook_overlay_v1','awake_worldbook_activation_v1' | Sort-Object) -join "`n")) 'G3-S0 preserves the two existing Worldbook SyncData keys as a separate compatibility boundary.'
    $acceptanceIds = @((Get-JointJsonProperty $scope 'acceptanceIds') | ForEach-Object { [string]$_ })
    Add-G3PlanAssertion 's0_acceptance_ids_complete' ((@('G3-S0-001-persona-namespace-ready','G3-S0-002-schema-registry-complete','G3-S0-003-readiness-failure-atomic-no-owner','G3-S0-004-readiness-retry-after-failure','G3-S0-005-worldbook-syncdata-characterization','G3-S0-006-worldbook-compatibility-preserved' | Where-Object { $acceptanceIds -notcontains $_ }).Count -eq 0)) 'G3-S0 acceptance IDs cover readiness failure, retry and Worldbook characterization.'
    $requiredEvidence = Get-JointJsonProperty $scope 'requiredEvidence'
    Add-G3PlanAssertion 's0_focused_evidence_contract' (([string](Get-JointJsonProperty $requiredEvidence 'focusedReadinessReport') -eq 'tools/persona-awake-joint/artifacts/g3-s0-readiness-focused.json') -and ([string](Get-JointJsonProperty $requiredEvidence 'focusedReadinessReportSchema') -eq 'awake.persona.g3-s0-focused-report.v1') -and ([string](Get-JointJsonProperty $requiredEvidence 'focusedReadinessTrace') -eq 'tools/persona-awake-joint/artifacts/g3-s0-readiness-trace.json') -and ([string](Get-JointJsonProperty $requiredEvidence 'focusedReadinessTraceSchema') -eq 'awake.persona.g3-s0-focused-trace.v1') -and ([int](Get-JointJsonProperty $requiredEvidence 'focusedReadinessExitCode') -eq 0)) 'G3-S0 has a scope-bound positive focused report plus raw trace contract.'

    $requiredWritePaths = @(
        '_houkai_merge/AWAKE/src/AwakeStorageContract.cs',
        '_houkai_merge/AWAKE/src/AiTaskConstants.cs',
        '_houkai_merge/AWAKE/src/WorldStateStore.cs',
        '_houkai_merge/AWAKE/src/AwakeRuntime.cs',
        '_houkai_merge/AWAKE/src/NpcDialogueService.cs',
        '_houkai_merge/AWAKE/src/WorldbookRuntime.cs',
        '_houkai_merge/AWAKE/src/PersonaRuntimeModels.cs',
        '_houkai_merge/AWAKE/src/PersonaRuntimeProvider.cs',
        '_houkai_merge/AWAKE/src/PersonaRuntimeBundleLoader.cs',
        '_houkai_merge/AWAKE/src/PersonaPersistenceModels.cs',
        '_houkai_merge/AWAKE/src/ProbeExtension.cs'
    )
    foreach ($requiredPath in $requiredWritePaths) {
        Add-G3PlanAssertion ('write_path_present:' + ([IO.Path]::GetFileName($requiredPath))) $planText.Contains($requiredPath, [StringComparison]::Ordinal) ('Path=' + $requiredPath)
    }

    Add-G3PlanAssertion 'protected_runtime_boundary' ($planText.Contains('不启动 Bannerlord', [StringComparison]::Ordinal) -and $planText.Contains('不修改 frozen candidate', [StringComparison]::Ordinal)) 'Game launch and frozen candidate remain excluded.'
    Add-G3PlanAssertion 'next_gate_sequence' $nextGateText.Contains('G3-S0 -> G3-A -> G3-B -> G3-C -> G4', [StringComparison]::Ordinal) 'Next-Gate records the corrected serial order.'
    Add-G3PlanAssertion 'architecture_sequence' $architectureText.Contains('G3-S0 -> G3-A -> G3-B -> G3-C -> G4', [StringComparison]::Ordinal) 'Architecture matrix records the corrected serial order.'

    if ($errors.Count -eq 0) {
        $report.status = 'pass'
        $report.exitCode = 0
    } else {
        $report.status = 'reject'
        $report.exitCode = 10
        $report.observedErrors = @($errors)
    }
} catch {
    $errorRecord = Convert-JointExceptionToError $_.Exception 'verify-g3-plan'
    $report.status = 'error'
    $report.exitCode = 40
    $report.observedErrors = @($errorRecord)
} finally {
    $report.assertions = @($assertions)
    if ($null -ne $reportPathFull) { Write-JointReport $report $reportPathFull }
}

exit ([int]$report.exitCode)
