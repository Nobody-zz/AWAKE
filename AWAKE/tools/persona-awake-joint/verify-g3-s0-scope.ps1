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
    schemaVersion = 'awake.persona.g3-s0-scope-verification.v1'
    commandLine = 'pwsh -NoProfile -File "' + [IO.Path]::GetFullPath($PSCommandPath) + '" -ReportPath "' + $ReportPath + '"'
    normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
    scopePath = $null
    scopeSchemaVersion = $null
    taskId = $null
    batchId = $null
    gate = $null
    scopeRevision = $null
    scopeStatus = $null
    scopeSha256 = $null
    approvalRecordPath = $null
    leaseRecordPath = $null
    approvalRecordStatus = $null
    leaseRecordStatus = $null
    authorityDetached = $false
    approvalScopeSha256 = $null
    leaseScopeSha256 = $null
    approvalVerdict = $null
    userSignoff = $false
    reviewerId = $null
    reviewEventId = $null
    reviewSource = $null
    reviewLogSha256 = $null
    reviewLogPath = $null
    leaseStatus = $null
    leaseOwner = $null
    leaseId = $null
    activeLeaseRecordCount = 0
    foreignActiveLeaseRecordCount = 0
    identityBound = $false
    worldbookPathBoundary = $false
    writeSetExact = $false
    writeSetDisjoint = $false
    sideEffectsDisabled = $false
    leaseSideEffectsDisabled = $false
    typedSchemaRegistryComplete = $false
    worldbookCompatibilityPreserved = $false
    personaStorageBoundary = $false
    status = 'error'
    exitCode = 40
    assertions = @()
    observedErrors = @()
}

function Add-ScopeAssertion([string]$Id, [bool]$Passed, [string]$Detail) {
    $script:assertions.Add([ordered]@{ id = $Id; passed = $Passed; detail = $Detail })
    if (-not $Passed) {
        $script:errors.Add([ordered]@{
            schemaVersion = 'awake.persona.adapter-error.v1'
            errorId = Get-JointStableHashId 'error' ('persona.g3_s0_scope_invalid|' + $Id + '|' + $Detail)
            code = 'persona.g3_s0_scope_invalid'
            stage = 'g3_s0_scope'
            artifactId = 'g3_s0_scope'
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
    try {
        [void][DateTimeOffset]::Parse($Value, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
        return $true
    } catch {
        return $false
    }
}

try {
    $reportPathFull = Assert-JointOutputPath $ReportPath 'ReportPath'
    $scopePath = Assert-JointReadablePath (Join-Path $script:JointAwakeRoot 'docs\persona-awake-joint-g3-s0-scope.v1.json') $script:JointAwakeRoot 'G3-S0 scope'
    $scopeRead = Read-JointJsonFile $scopePath 'G3-S0 scope'
    $scope = $scopeRead.Value
    $report.scopePath = $scopePath
    $report.scopeSchemaVersion = Get-String $scope 'schemaVersion'
    $report.taskId = Get-String $scope 'taskId'
    $report.batchId = Get-String $scope 'batchId'
    $report.gate = Get-String $scope 'gate'
    $report.scopeRevision = [int](Get-JointJsonProperty $scope 'scopeRevision')
    $report.scopeStatus = Get-String $scope 'status'
    $report.scopeSha256 = $scopeRead.RawSha256
    $authority = Get-JointJsonProperty $scope 'authority'
    $approvalRecordRelative = Get-String $authority 'approvalRecord'
    $leaseRecordRelative = Get-String $authority 'leaseRecord'
    $approval = $null
    $lease = $null
    if ([string]::IsNullOrWhiteSpace($approvalRecordRelative)) {
        Add-ScopeAssertion 'approval_record_path' $false 'Detached approval record path is required.'
    } else {
        $approvalPath = Assert-JointReadablePath (Join-Path $script:JointAwakeRoot $approvalRecordRelative) $script:JointAwakeRoot 'G3-S0 approval record'
        $report.approvalRecordPath = $approvalPath
        $approval = (Read-JointJsonFile $approvalPath 'G3-S0 approval record').Value
    }
    if ([string]::IsNullOrWhiteSpace($leaseRecordRelative)) {
        Add-ScopeAssertion 'lease_record_path' $false 'Detached lease record path is required.'
    } else {
        $leasePath = Assert-JointReadablePath (Join-Path $script:JointAwakeRoot $leaseRecordRelative) $script:JointAwakeRoot 'G3-S0 lease record'
        $report.leaseRecordPath = $leasePath
        $lease = (Read-JointJsonFile $leasePath 'G3-S0 lease record').Value
    }
    $sideEffects = Get-JointJsonProperty $scope 'sideEffects'
    $approvalReview = Get-JointJsonProperty $approval 'review'
    $approvalSignoff = Get-JointJsonProperty $approval 'userSignoff'
    $approvalShapeValid = $true
    $leaseShapeValid = $true
    try {
        Assert-JointExactFields $approval @('schemaVersion','recordStatus','taskId','batchId','gate','scopeRevision','scopePath','scopeSha256','review','userSignoff') @('schemaVersion','recordStatus','taskId','batchId','gate','scopeRevision','scopePath','scopeSha256','review','userSignoff') '$.approvalRecord'
        Assert-JointExactFields $approvalReview @('verdict','eventId','reviewerId','reviewSource','reviewLogPath','reviewLogSha256','reviewedAt','reviewedScopeRevision','supersedesVerdict') @('verdict','eventId','reviewerId','reviewSource','reviewLogPath','reviewLogSha256','reviewedAt','reviewedScopeRevision','supersedesVerdict') '$.approvalRecord.review'
        Assert-JointExactFields $approvalSignoff @('confirmed','actorId','confirmedAt','confirmedScopeRevision') @('confirmed','actorId','confirmedAt','confirmedScopeRevision') '$.approvalRecord.userSignoff'
    } catch { $approvalShapeValid = $false }
    try {
        $leaseSideEffectsObject = Get-JointJsonProperty $lease 'sideEffects'
        Assert-JointExactFields $lease @('schemaVersion','recordStatus','taskId','batchId','gate','scopeRevision','scopePath','scopeSha256','status','owner','leaseId','writeSet','acquiredAt','releasedAt','sideEffects') @('schemaVersion','recordStatus','taskId','batchId','gate','scopeRevision','scopePath','scopeSha256','status','owner','leaseId','writeSet','acquiredAt','releasedAt','sideEffects') '$.leaseRecord'
        Assert-JointExactFields $leaseSideEffectsObject @('launchGame','syncGameDirectory','mutateFrozenCandidate','publish') @('launchGame','syncGameDirectory','mutateFrozenCandidate','publish') '$.leaseRecord.sideEffects'
    } catch { $leaseShapeValid = $false }
    $report.approvalRecordStatus = Get-String $approval 'recordStatus'
    $report.approvalVerdict = Get-String $approvalReview 'verdict'
    $report.userSignoff = (Get-JointJsonProperty $approvalSignoff 'confirmed') -eq $true
    $report.reviewerId = Get-String $approvalReview 'reviewerId'
    $report.reviewEventId = Get-String $approvalReview 'eventId'
    $report.reviewSource = Get-String $approvalReview 'reviewSource'
    $report.reviewLogPath = Get-String $approvalReview 'reviewLogPath'
    $report.reviewLogSha256 = Get-String $approvalReview 'reviewLogSha256'
    $report.approvalScopeSha256 = Get-String $approval 'scopeSha256'
    $report.leaseRecordStatus = Get-String $lease 'recordStatus'
    $report.leaseScopeSha256 = Get-String $lease 'scopeSha256'
    $report.leaseStatus = Get-String $lease 'status'
    $report.leaseOwner = Get-String $lease 'owner'
    $report.leaseId = Get-String $lease 'leaseId'

    $expectedWriteSet = @(
        '_houkai_merge/AWAKE/src/AwakeStorageContract.cs',
        '_houkai_merge/AWAKE/src/AiTaskConstants.cs',
        '_houkai_merge/AWAKE/src/WorldStateStore.cs',
        '_houkai_merge/AWAKE/src/AwakeRuntime.cs',
        '_houkai_merge/AWAKE.Tests/Program.cs',
        '_houkai_merge/AWAKE.Tests/AwakeTestFakes.cs'
    )
    $writeSet = @((Get-JointJsonProperty $scope 'writeSet') | ForEach-Object { [string]$_ })
    $excludedWriteSet = @((Get-JointJsonProperty $scope 'excludedWriteSet') | ForEach-Object { [string]$_ })
    $report.writeSetExact = (@($writeSet | Sort-Object) -join "`n") -eq (@($expectedWriteSet | Sort-Object) -join "`n")
    $writeSetOverlaps = @($writeSet | Where-Object { $excludedWriteSet -contains $_ })
    $report.writeSetDisjoint = ($writeSetOverlaps.Count -eq 0) -and -not ($writeSet -contains '_houkai_merge/AWAKE/tools/persona-awake-joint/*')
    $sideEffectNames = @('launchGame','syncGameDirectory','mutateFrozenCandidate','publish')
    $enabledSideEffects = @($sideEffectNames | Where-Object { (Get-JointJsonProperty $sideEffects $_) -ne $false })
    $report.sideEffectsDisabled = $enabledSideEffects.Count -eq 0

    $expectedScopeRelative = 'docs/persona-awake-joint-g3-s0-scope.v1.json'
    $expectedApprovalRelative = 'docs/persona-awake-joint-g3-s0-approval.v1.json'
    $expectedLeaseRelative = 'docs/persona-awake-joint-g3-s0-lease.v1.json'
    $approvalScopePath = Get-String $approval 'scopePath'
    $leaseScopePath = Get-String $lease 'scopePath'
    $authorityPaths = @($approvalRecordRelative, $leaseRecordRelative) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $report.authorityDetached = (($approvalRecordRelative -eq $expectedApprovalRelative) -and ($leaseRecordRelative -eq $expectedLeaseRelative) -and (@($authorityPaths | Select-Object -Unique).Count -eq 2) -and ($authorityPaths -notcontains $expectedScopeRelative) -and -not (@($writeSet | Where-Object { $authorityPaths -contains $_ }).Count -gt 0))
    $authorityHashesMatch = $report.scopeSha256 -eq $report.approvalScopeSha256 -and $report.scopeSha256 -eq $report.leaseScopeSha256

    $approvalTaskId = Get-String $approval 'taskId'
    $approvalBatchId = Get-String $approval 'batchId'
    $approvalGate = Get-String $approval 'gate'
    $approvalRevision = [int](Get-JointJsonProperty $approval 'scopeRevision')
    $leaseTaskId = Get-String $lease 'taskId'
    $leaseBatchId = Get-String $lease 'batchId'
    $leaseGate = Get-String $lease 'gate'
    $leaseRevision = [int](Get-JointJsonProperty $lease 'scopeRevision')
    $reviewedScopeRevision = [int](Get-JointJsonProperty $approvalReview 'reviewedScopeRevision')
    $confirmedScopeRevision = [int](Get-JointJsonProperty $approvalSignoff 'confirmedScopeRevision')
    $approvalRecordSchema = Get-String $approval 'schemaVersion'
    $leaseRecordSchema = Get-String $lease 'schemaVersion'
    $report.identityBound = (($approvalRecordSchema -eq 'awake.persona.g3-s0-approval.v1') -and ($leaseRecordSchema -eq 'awake.persona.g3-s0-lease.v1') -and ($approvalTaskId -eq $report.taskId) -and ($approvalBatchId -eq $report.batchId) -and ($approvalGate -eq $report.gate) -and ($approvalRevision -eq $report.scopeRevision) -and ($leaseTaskId -eq $report.taskId) -and ($leaseBatchId -eq $report.batchId) -and ($leaseGate -eq $report.gate) -and ($leaseRevision -eq $report.scopeRevision) -and ($reviewedScopeRevision -eq $report.scopeRevision) -and ($confirmedScopeRevision -eq $report.scopeRevision) -and ($approvalScopePath -eq $expectedScopeRelative) -and ($leaseScopePath -eq $expectedScopeRelative) -and ($report.reviewLogPath -eq 'docs/PLAN-PersonaWorkbench-AWAKE-Joint-G3-EXECUTION-GATE-REVIEW-LOG-20260824.md') -and ($report.reviewSource -eq 'independent_read_only_subagent') -and ($report.reviewEventId -match '^g3-s0-review-r[0-9]+-20260824$'))

    $leaseDirectory = Join-Path $script:JointAwakeRoot 'docs'
    $leaseRecords = @(Get-ChildItem -LiteralPath $leaseDirectory -Filter 'persona-awake-joint-g3-s0-lease*.v1.json' -File)
    $activeLeaseCount = 0
    foreach ($leaseFile in $leaseRecords) {
        try {
            $leaseCandidate = (Read-JointJsonFile $leaseFile.FullName ('lease registry candidate ' + $leaseFile.Name)).Value
            if ((Get-String $leaseCandidate 'status') -eq 'active') {
                $candidateMatches = ((Get-String $leaseCandidate 'taskId') -eq $report.taskId -and (Get-String $leaseCandidate 'batchId') -eq $report.batchId -and (Get-String $leaseCandidate 'gate') -eq $report.gate -and [int](Get-JointJsonProperty $leaseCandidate 'scopeRevision') -eq $report.scopeRevision -and (Get-String $leaseCandidate 'scopeSha256') -eq $report.scopeSha256)
                if ($candidateMatches) { $activeLeaseCount++ } else { $report.foreignActiveLeaseRecordCount++ }
            }
        } catch {
            Add-ScopeAssertion 'lease_registry_parse' $false ('Lease registry candidate is not valid JSON: ' + $leaseFile.Name)
        }
    }
    $report.activeLeaseRecordCount = $activeLeaseCount

    $typedSchemas = @((Get-JointJsonProperty $scope 'typedSchemas'))
    $expectedTypedSchemas = @(
        [ordered]@{ schemaId = 'awake.persona.continuity.v1'; typeName = 'PersonaPersistenceEnvelope'; worldStateKind = 'PersonaContinuity'; namespaceId = 'awake.persona.state'; owner = 'PersonaStorageOwner'; writeBoundary = 'G3-B-only' },
        [ordered]@{ schemaId = 'awake.persona.override.v1'; typeName = 'PersonaPersistenceEnvelope'; worldStateKind = 'PersonaOverride'; namespaceId = 'awake.persona.state'; owner = 'PersonaStorageOwner'; writeBoundary = 'G3-B-only' },
        [ordered]@{ schemaId = 'awake.persona.recovery.v1'; typeName = 'PersonaRecoveryRecord'; worldStateKind = 'PersonaRecovery'; namespaceId = 'awake.persona.state'; owner = 'PersonaStorageOwner'; writeBoundary = 'G3-B-only' }
    )
    $report.typedSchemaRegistryComplete = $typedSchemas.Count -eq $expectedTypedSchemas.Count
    foreach ($expected in $expectedTypedSchemas) {
        $match = @($typedSchemas | Where-Object {
            (Get-String $_ 'schemaId') -eq $expected.schemaId -and
            (Get-String $_ 'typeName') -eq $expected.typeName -and
            (Get-String $_ 'worldStateKind') -eq $expected.worldStateKind -and
            (Get-String $_ 'namespaceId') -eq $expected.namespaceId -and
            (Get-String $_ 'owner') -eq $expected.owner -and
            (Get-String $_ 'writeBoundary') -eq $expected.writeBoundary
        }).Count -eq 1
        $report.typedSchemaRegistryComplete = $report.typedSchemaRegistryComplete -and $match
    }

    $worldbookBoundaries = @((Get-JointJsonProperty (Get-JointJsonProperty $scope 'compatibilityBoundaries') 'worldbookSyncData'))
    $expectedWorldbookBoundaries = @(
        [ordered]@{ syncKey = 'awake_worldbook_overlay_v1'; schemaId = 'awake.worldbook.overlay.v1'; owner = 'AwakeTerminalBehavior'; compatibility = 'preserve-existing-key-and-path' },
        [ordered]@{ syncKey = 'awake_worldbook_activation_v1'; schemaId = 'awake.worldbook.campaign-activation.v1'; owner = 'AwakeTerminalBehavior'; compatibility = 'preserve-existing-key-and-path' }
    )
    $report.worldbookCompatibilityPreserved = $worldbookBoundaries.Count -eq $expectedWorldbookBoundaries.Count
    foreach ($expected in $expectedWorldbookBoundaries) {
        $expectedWorldbookPath = if ($expected.syncKey -eq 'awake_worldbook_overlay_v1') { 'WorldbookRuntime.ImportOverlayJson/ExportOverlayJson' } else { 'WorldbookRuntime.ImportActivationJson/ExportActivationJson' }
        $match = @($worldbookBoundaries | Where-Object {
            (Get-String $_ 'syncKey') -eq $expected.syncKey -and
            (Get-String $_ 'schemaId') -eq $expected.schemaId -and
            (Get-String $_ 'owner') -eq $expected.owner -and
            (Get-String $_ 'compatibility') -eq $expected.compatibility -and
            (Get-String $_ 'path') -eq $expectedWorldbookPath
        }).Count -eq 1
        $report.worldbookCompatibilityPreserved = $report.worldbookCompatibilityPreserved -and $match
    }
    $personaStorage = Get-JointJsonProperty (Get-JointJsonProperty $scope 'compatibilityBoundaries') 'personaStorage'
    $personaSchemaIds = @((Get-JointJsonProperty $personaStorage 'stateSchemaIds') | ForEach-Object { [string]$_ })
    $worldbookSchemaIds = @($expectedWorldbookBoundaries | ForEach-Object { [string]$_.schemaId })
    $report.personaStorageBoundary = ($report.typedSchemaRegistryComplete -and (Get-String $personaStorage 'namespaceId') -eq 'awake.persona.state' -and (Get-String $personaStorage 'owner') -eq 'PersonaStorageOwner' -and (@($personaSchemaIds | Sort-Object) -join "`n") -eq (@($expectedTypedSchemas | ForEach-Object { $_.schemaId } | Sort-Object) -join "`n") -and (@($personaSchemaIds | Where-Object { $worldbookSchemaIds -contains $_ }).Count -eq 0))

    $reviewLogPathFull = $null
    $reviewLogText = ''
    if (-not [string]::IsNullOrWhiteSpace($report.reviewLogPath)) {
        $reviewLogPathFull = Assert-JointReadablePath (Join-Path $script:JointAwakeRoot $report.reviewLogPath) $script:JointAwakeRoot 'G3-S0 review log'
        $reviewLogText = [IO.File]::ReadAllText($reviewLogPathFull, $script:JointUtf8)
    }
    $reviewedAt = Get-String $approvalReview 'reviewedAt'
    $confirmedAt = Get-String $approvalSignoff 'confirmedAt'
    $reviewLogHashValid = ($report.reviewLogSha256 -match '^[A-F0-9]{64}$') -and ($null -ne $reviewLogPathFull) -and ((Get-JointHashFile $reviewLogPathFull) -eq $report.reviewLogSha256)
    $reviewEventPresent = $reviewLogText.Contains('reviewEventId：`' + $report.reviewEventId + '`', [StringComparison]::Ordinal)
    $verdictLines = @([Regex]::Matches($reviewLogText, '(?m)^\s*VERDICT:\s*(APPROVED|REVISE)\s*$') | ForEach-Object { $_.Groups[1].Value })
    $reviewEvidenceValid = (($report.approvalVerdict -eq 'APPROVED') -and (-not [string]::IsNullOrWhiteSpace($report.reviewerId)) -and (Test-IsoUtcTimestamp $reviewedAt) -and $reviewLogHashValid -and $reviewEventPresent -and $verdictLines.Count -gt 0 -and $verdictLines[-1] -eq 'APPROVED')
    $approvalRecordValid = (($report.approvalRecordStatus -eq 'approved') -and $report.identityBound -and $approvalScopePath -eq $expectedScopeRelative -and $reviewEvidenceValid -and $report.userSignoff -and (-not [string]::IsNullOrWhiteSpace((Get-String $approvalSignoff 'actorId'))) -and (Test-IsoUtcTimestamp $confirmedAt))
    $leaseWriteSet = @((Get-JointJsonProperty $lease 'writeSet') | ForEach-Object { [string]$_ })
    $leaseWriteSetExact = (@($leaseWriteSet | Sort-Object) -join "`n") -eq (@($expectedWriteSet | Sort-Object) -join "`n")
    $leaseSideEffects = Get-JointJsonProperty $lease 'sideEffects'
    $leaseSideEffectNames = @('launchGame','syncGameDirectory','mutateFrozenCandidate','publish')
    $leaseSideEffectsEnabled = @($leaseSideEffectNames | Where-Object { (Get-JointJsonProperty $leaseSideEffects $_) -ne $false })
    $report.leaseSideEffectsDisabled = $leaseSideEffectsEnabled.Count -eq 0
    $leaseAcquiredAt = Get-String $lease 'acquiredAt'
    $leaseReleasedAt = Get-String $lease 'releasedAt'
    $leaseValid = (($report.leaseRecordStatus -eq 'active') -and ($report.leaseStatus -eq 'active') -and ($report.activeLeaseRecordCount -eq 1) -and ($report.foreignActiveLeaseRecordCount -eq 0) -and $report.identityBound -and (-not [string]::IsNullOrWhiteSpace($report.leaseOwner)) -and (-not [string]::IsNullOrWhiteSpace($report.leaseId)) -and (Test-IsoUtcTimestamp $leaseAcquiredAt) -and [string]::IsNullOrWhiteSpace($leaseReleasedAt) -and ($leaseScopePath -eq $expectedScopeRelative) -and $leaseWriteSetExact -and $report.leaseSideEffectsDisabled)

    Add-ScopeAssertion 'schema_version' ($report.scopeSchemaVersion -eq 'awake.persona.g3-s0-scope.v1') ('Observed=' + $report.scopeSchemaVersion)
    Add-ScopeAssertion 'task_identity' ($report.taskId -eq 'PERSONA-AWAKE-JOINT-G3-S0-20260824' -and $report.batchId -eq 'persona-awake-joint-g3-s0-storage-readiness-20260824') ('taskId=' + $report.taskId + '; batchId=' + $report.batchId)
    Add-ScopeAssertion 'gate_identity' ($report.gate -eq 'G3-S0') ('Observed=' + $report.gate)
    Add-ScopeAssertion 'write_set_exact' ([bool]$report.writeSetExact) 'The declared S0 write set must match the locked six-file proposal.'
    Add-ScopeAssertion 'write_set_disjoint' ([bool]$report.writeSetDisjoint) 'The S0 write set must exclude runtime Persona, persistence, game, candidate and joint-tool paths.'
    Add-ScopeAssertion 'side_effects_disabled' ([bool]$report.sideEffectsDisabled) 'S0 must not launch, sync, publish or mutate a frozen candidate.'
    Add-ScopeAssertion 'scope_status_supported' ($report.scopeStatus -in @('pending_approval','approved')) ('Observed scope status=' + $report.scopeStatus)
    Add-ScopeAssertion 'detached_authority_paths' ([bool]$report.authorityDetached) 'Approval and lease records must be detached from the scope manifest and declared write set.'
    Add-ScopeAssertion 'scope_hash_binding' ([bool]$authorityHashesMatch) 'Detached approval and lease records must bind to the exact raw scope bytes.'
    Add-ScopeAssertion 'authority_identity_binding' ([bool]$report.identityBound) 'Approval and lease records must match task, batch, gate, revision, paths and review log.'
    Add-ScopeAssertion 'unique_active_lease_registry' ($report.activeLeaseRecordCount -le 1) ('Active G3-S0 lease records observed=' + $report.activeLeaseRecordCount)
    Add-ScopeAssertion 'foreign_active_lease_registry' ($report.foreignActiveLeaseRecordCount -eq 0) ('Foreign active G3-S0 lease records observed=' + $report.foreignActiveLeaseRecordCount)
    Add-ScopeAssertion 'typed_schema_registry_complete' ([bool]$report.typedSchemaRegistryComplete) 'The three Persona schemas, types, WorldStateKind values and owner boundary must be exact.'
    Add-ScopeAssertion 'worldbook_compatibility_contract' ([bool]$report.worldbookCompatibilityPreserved) 'Worldbook SyncData keys and storage schema IDs must remain separate and explicit.'
    Add-ScopeAssertion 'persona_storage_boundary' ([bool]$report.personaStorageBoundary) 'Persona Storage namespace, owner and schema set must be exact and disjoint from Worldbook SyncData schemas.'
    Add-ScopeAssertion 'approval_record_shape' (-not [string]::IsNullOrWhiteSpace($report.approvalRecordStatus) -and $null -ne $approvalReview -and $null -ne $approvalSignoff) 'Detached approval record has explicit review and user-signoff fields.'
    Add-ScopeAssertion 'approval_record_exact_shape' ([bool]$approvalShapeValid) 'Approval record and nested review/signoff objects must reject unknown or missing fields.'
    Add-ScopeAssertion 'lease_record_shape' (-not [string]::IsNullOrWhiteSpace($report.leaseRecordStatus) -and $leaseWriteSet.Count -eq 6) 'Detached lease record has explicit status and six-file write set.'
    Add-ScopeAssertion 'lease_record_exact_shape' ([bool]$leaseShapeValid) 'Lease record and side-effect object must reject unknown or missing fields.'
    Add-ScopeAssertion 'lease_side_effects_disabled' ([bool]$report.leaseSideEffectsDisabled) 'The active lease must not grant game, sync, frozen-root or publish side effects.'

    if ($errors.Count -gt 0) {
        $report.status = 'reject'
        $report.exitCode = 10
        $report.observedErrors = @($errors)
    } elseif (-not $approvalRecordValid -or -not $leaseValid) {
        $report.status = 'blocked'
        $report.exitCode = 20
        $report.observedErrors = @([ordered]@{
            schemaVersion = 'awake.persona.adapter-error.v1'
            errorId = Get-JointStableHashId 'error' ('persona.g3_s0_scope_pending|' + $report.approvalVerdict + '|' + $report.leaseStatus)
            code = 'persona.g3_s0_scope_pending'
            stage = 'g3_s0_scope'
            artifactId = 'g3_s0_scope'
            path = '$'
            detail = 'Detached independent approval, user signoff and active disjoint lease are not all present; the scope manifest is not self-authorizing.'
            retryable = $false
        })
    } else {
        $report.status = 'pass'
        $report.exitCode = 0
    }
} catch {
    $report.status = 'error'
    $report.exitCode = 40
    $report.observedErrors = @((Convert-JointExceptionToError $_.Exception 'verify-g3-s0-scope'))
} finally {
    $report.assertions = @($assertions)
    if ($null -ne $reportPathFull) { Write-JointReport $report $reportPathFull }
}

exit ([int]$report.exitCode)
