param(
    [Parameter(Mandatory = $true)][string]$FixtureId,
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [string]$FixtureRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'persona-awake-joint.ps1')

$normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
$reportPathFull = $null
$report = Get-JointReportBase $FixtureId ('pwsh -NoProfile -File "' + [IO.Path]::GetFullPath($PSCommandPath) + '" -FixtureId "' + $FixtureId + '" -ReportPath "' + $ReportPath + '"') $normalizedCwd
$assertions = [Collections.Generic.List[object]]::new()
$observedErrors = [Collections.Generic.List[object]]::new()
$outputPath = $null
$outputBeforeHash = $null
$expectedStatus = ''
$fixtureDirectory = ''
$cleanupPaths = [Collections.Generic.List[string]]::new()
$adapted = $null

function Assert-JointNativeFixturePrerequisite([object]$InputObject) {
    $path = '$.nativePrerequisite'
    if (-not (Test-JointJsonProperty $InputObject 'nativePrerequisite')) { Throw-JointBlocked 'persona.native_lease_required' 'prerequisite' $path 'Native prerequisite evidence is missing.' }
    $native = Get-JointJsonProperty $InputObject 'nativePrerequisite'
    if (-not (Test-JointJsonObject $native)) { Throw-JointBlocked 'persona.native_lease_required' 'prerequisite' $path 'Native prerequisite evidence must be an object.' }
    $lease = Get-JointOptionalString $native 'executionLease' $path
    if ([string]::IsNullOrWhiteSpace($lease) -or $lease -in @('none','NONE','null')) { Throw-JointBlocked 'persona.native_lease_required' 'prerequisite' ($path + '.executionLease') 'Native exact-scope approval and one active disjoint lease are absent.' }
    $owner = Get-JointOptionalString $native 'owner' $path
    if ([string]::IsNullOrWhiteSpace($owner) -or $owner -in @('none','NONE','null')) { Throw-JointBlocked 'persona.native_lease_required' 'prerequisite' ($path + '.owner') 'Native lease owner is missing.' }
    $scopeApproved = (Test-JointJsonProperty $native 'exactScopeApproved') -and ((Get-JointJsonProperty $native 'exactScopeApproved') -is [bool]) -and [bool](Get-JointJsonProperty $native 'exactScopeApproved')
    if (-not $scopeApproved) { Throw-JointBlocked 'persona.native_lease_required' 'prerequisite' ($path + '.exactScopeApproved') 'Native exact-scope approval is absent.' }
    $writeSetDisjoint = (Test-JointJsonProperty $native 'writeSetDisjoint') -and ((Get-JointJsonProperty $native 'writeSetDisjoint') -is [bool]) -and [bool](Get-JointJsonProperty $native 'writeSetDisjoint')
    if (-not $writeSetDisjoint) { Throw-JointBlocked 'persona.native_lease_required' 'prerequisite' ($path + '.writeSetDisjoint') 'Native lease write set is not proven disjoint from the isolated tool set.' }
    $planStatus = Get-JointOptionalString $native 'planStatus' $path
    if ($planStatus -eq 'approved_for_b1_only') { Throw-JointBlocked 'persona.native_lease_required' 'prerequisite' ($path + '.planStatus') 'B1-only approval does not authorize shared runtime fixture execution.' }
}

function Assert-JointStorageFixturePrerequisite([object]$InputObject) {
    $path = '$.storagePrerequisite'
    if (-not (Test-JointJsonProperty $InputObject 'storagePrerequisite')) { Throw-JointBlocked 'persona.storage_lease_required' 'prerequisite' $path 'Storage prerequisite evidence is missing.' }
    $storage = Get-JointJsonProperty $InputObject 'storagePrerequisite'
    if (-not (Test-JointJsonObject $storage)) { Throw-JointBlocked 'persona.storage_lease_required' 'prerequisite' $path 'Storage prerequisite evidence must be an object.' }
    $owner = Get-JointOptionalString $storage 'owner' $path
    if ([string]::IsNullOrWhiteSpace($owner) -or $owner -in @('none','NONE','null')) { Throw-JointBlocked 'persona.storage_lease_required' 'prerequisite' ($path + '.owner') 'Storage owner and persistence lease are not available; save/load/branch/restart cannot be claimed.' }
    $lease = Get-JointOptionalString $storage 'lease' $path
    if ([string]::IsNullOrWhiteSpace($lease) -or $lease -in @('none','NONE','null')) { Throw-JointBlocked 'persona.storage_lease_required' 'prerequisite' ($path + '.lease') 'Storage persistence lease is not available; save/load/branch/restart cannot be claimed.' }
    $ready = (Test-JointJsonProperty $storage 'ready') -and ((Get-JointJsonProperty $storage 'ready') -is [bool]) -and [bool](Get-JointJsonProperty $storage 'ready')
    if (-not $ready) { Throw-JointBlocked 'persona.storage_lease_required' 'prerequisite' ($path + '.ready') 'Storage readiness evidence is absent; save/load/branch/restart cannot be claimed.' }
    Assert-JointNativeFixturePrerequisite $InputObject
}

function Invoke-JointNonMigrationFixture([object]$InputObject, [string]$FixtureId, [string]$ArtifactRoot, [Collections.Generic.List[string]]$CleanupPaths, [Collections.Generic.List[object]]$Assertions) {
    $operation = Get-JointRequiredString $InputObject 'operation' '$'
    if ($operation -eq 'validate-export-plan') {
        if ((Test-JointJsonProperty $InputObject 'exportState') -and ((Get-JointRequiredString $InputObject 'exportState' '$') -eq 'partial')) { Throw-JointReject 'persona.partial_export' 'export-plan' '$.exportState' 'Partial export cannot be promoted or overwrite last-known-good.' }
        $candidates = if (Test-JointJsonProperty $InputObject 'exportCandidates') { Get-JointJsonProperty $InputObject 'exportCandidates' } else { @() }
        if (-not (Test-JointJsonArray $candidates)) { Throw-JointReject 'persona.schema_invalid' 'export-plan' '$.exportCandidates' 'exportCandidates must be an array.' }
        $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $index = 0
        foreach ($candidate in $candidates) {
            if (-not (Test-JointJsonObject $candidate)) { Throw-JointReject 'persona.schema_invalid' 'export-plan' ('$.exportCandidates[' + $index + ']') 'Export candidate must be an object.' }
            $candidateId = Get-JointRequiredString $candidate 'id' ('$.exportCandidates[' + $index + ']')
            if (-not $ids.Add($candidateId)) { Throw-JointReject 'persona.duplicate_id' 'export-plan' ('$.exportCandidates[' + $index + '].id') 'Duplicate stable export/definition ID is not idempotent-safe.' }
            $index++
        }
        Add-JointAssertion $Assertions 'export_candidate_ids_unique' $true 'Export candidate IDs are unique.'
        $pathAttempts = if (Test-JointJsonProperty $InputObject 'pathAttempts') { Get-JointJsonProperty $InputObject 'pathAttempts' } else { @() }
        if (-not (Test-JointJsonArray $pathAttempts)) { Throw-JointReject 'persona.schema_invalid' 'path-guard' '$.pathAttempts' 'pathAttempts must be an array.' }
        $pathIndex = 0
        foreach ($attempt in $pathAttempts) {
            if (-not (Test-JointJsonObject $attempt)) { Throw-JointReject 'persona.schema_invalid' 'path-guard' ('$.pathAttempts[' + $pathIndex + ']') 'Path attempt must be an object.' }
            $writePath = Get-JointRequiredString $attempt 'writePath' ('$.pathAttempts[' + $pathIndex + ']')
            if ((Test-JointJsonProperty $attempt 'reparse') -and [bool](Get-JointJsonProperty $attempt 'reparse')) { Throw-JointReject 'persona.reparse_path' 'path-guard' ('$.pathAttempts[' + $pathIndex + '].writePath') 'Junction/reparse resolution escapes the isolated candidate root.' }
            [void](Assert-JointOutputPath $writePath ('pathAttempts[' + $pathIndex + ']'))
            $pathIndex++
        }
        Add-JointAssertion $Assertions 'path_guard_checked' $true 'All requested export paths stayed within the isolated tool write set.'
        return [pscustomobject]@{ Mode = 'pass'; ObservedWarnings = @() }
    }
    if ($operation -eq 'reload-runtime-bundle') {
        $current = Get-JointJsonProperty $InputObject 'currentBundle'
        $candidate = Get-JointJsonProperty $InputObject 'candidateReload'
        if (-not (Test-JointJsonObject $current) -or -not (Test-JointJsonObject $candidate)) { Throw-JointReject 'persona.schema_invalid' 'reload' '$' 'Runtime reload fixture requires currentBundle and candidateReload objects.' }
        $validationError = Get-JointOptionalString $candidate 'validationError' '$.candidateReload'
        if ([string]::IsNullOrWhiteSpace($validationError)) { Throw-JointReject 'persona.registry_digest_mismatch' 'reload' '$.candidateReload' 'Candidate reload did not expose a validation failure.' }
        Add-JointAssertion $Assertions 'bad_reload_rejected' $true ('Candidate reload rejected before activation: ' + $validationError)
        Add-JointAssertion $Assertions 'old_runtime_bundle_unchanged' $true ('Published bundle remains ' + (Get-JointRequiredString $current 'bundleId' '$.currentBundle'))
        Add-JointAssertion $Assertions 'old_activation_metadata_unchanged' $true 'No runtime activation write was attempted.'
        Add-JointAssertion $Assertions 'single_reference_remains_published' $true 'Only the last-known-good bundle remains selected.'
        return [pscustomobject]@{ Mode = 'pass'; ObservedWarnings = @('persona.registry_digest_mismatch') }
    }
    if ($operation -eq 'load-runtime-entry') {
        $manifestPath = if (Test-JointJsonProperty $InputObject 'runtimeManifest') { '$.runtimeManifest' } else { '$.runtimeEntry' }
        $manifest = if (Test-JointJsonProperty $InputObject 'runtimeManifest') { Get-JointJsonProperty $InputObject 'runtimeManifest' } else { Get-JointJsonProperty $InputObject 'runtimeEntry' }
        if (-not (Test-JointJsonObject $manifest)) { Throw-JointReject 'persona.schema_invalid' 'runtime-load' $manifestPath 'Runtime entry must be an object.' }
        $schema = Get-JointRequiredString $manifest 'schemaVersion' $manifestPath
        if ($schema -like '*v1') { Throw-JointReject 'persona.runtime_legacy_schema_rejected' 'runtime' ($manifestPath + '.schemaVersion') 'Legacy worldbook v1 is rejected; no automatic v1-to-v2 runtime migration exists.' }
        Throw-JointReject 'persona.schema_version_unsupported' 'runtime-load' ($manifestPath + '.schemaVersion') 'Runtime schema is not accepted by this offline bridge.'
    }
    if ($operation -eq 'frozen-candidate-isolation') {
        $protectedValues = if (Test-JointJsonProperty $InputObject 'protectedRoots') { Get-JointJsonProperty $InputObject 'protectedRoots' } else { @() }
        if (-not (Test-JointJsonArray $protectedValues) -or $protectedValues.Count -eq 0) { Throw-JointReject 'persona.path_protected' 'isolation' '$.protectedRoots' 'Frozen-candidate fixture requires protected roots.' }
        $protectedEntries = [Collections.Generic.List[object]]::new()
        foreach ($protectedValue in $protectedValues) {
            if (-not (Test-JointJsonObject $protectedValue)) { Throw-JointReject 'persona.schema_invalid' 'isolation' '$.protectedRoots' 'Protected root must be an object.' }
            Assert-JointExactFields $protectedValue @('id','path','beforeSha256','afterSha256') @('id','path','beforeSha256','afterSha256') '$.protectedRoots'
            $protectedId = Get-JointRequiredString $protectedValue 'id' '$.protectedRoots'
            Assert-JointStableId $protectedId '$.protectedRoots.id'
            $protectedPathText = Get-JointRequiredString $protectedValue 'path' '$.protectedRoots'
            $protectedPath = Get-JointFullPath $protectedPathText
            if (Test-JointReparsePath $protectedPath) { Throw-JointReject 'persona.path_protected' 'isolation' '$.protectedRoots.path' 'Protected root traverses a reparse point.' }
            $protectedEntries.Add([pscustomobject]@{
                Id = $protectedId
                DeclaredBeforeSha256 = Get-JointRequiredSha256 $protectedValue 'beforeSha256' '$.protectedRoots'
                DeclaredAfterSha256 = Get-JointRequiredSha256 $protectedValue 'afterSha256' '$.protectedRoots'
                FullPath = $protectedPath
            })
        }
        $isolatedSiblingText = Get-JointRequiredString $InputObject 'isolatedSibling' '$'
        $isolatedSiblingPath = Get-JointFullPath $isolatedSiblingText
        if (Test-JointReparsePath $isolatedSiblingPath) { Throw-JointReject 'persona.path_protected' 'isolation' '$.isolatedSibling' 'Isolated sibling traverses a reparse point.' }
        $allowedSiblingRoot = Join-Path $script:JointWorkspaceRoot 'temp\persona-awake-joint'
        if (-not (Test-JointPathUnder $isolatedSiblingPath $allowedSiblingRoot -AllowEqual:$false)) { Throw-JointReject 'persona.path_protected' 'isolation' '$.isolatedSibling' 'Isolated sibling must remain under the dedicated temporary sibling root.' }
        foreach ($entry in $protectedEntries) {
            if ((Test-JointPathUnder $isolatedSiblingPath $entry.FullPath -AllowEqual:$true) -or (Test-JointPathUnder $entry.FullPath $isolatedSiblingPath -AllowEqual:$true)) { Throw-JointReject 'persona.path_protected' 'isolation' '$.isolatedSibling' 'Isolated sibling overlaps or contains a protected root.' }
        }
        $attemptValues = if (Test-JointJsonProperty $InputObject 'attempts') { Get-JointJsonProperty $InputObject 'attempts' } else { @() }
        if (-not (Test-JointJsonArray $attemptValues) -or $attemptValues.Count -eq 0) { Throw-JointReject 'persona.schema_invalid' 'isolation' '$.attempts' 'Frozen-candidate fixture requires isolation attempts.' }
        $attemptIndex = 0
        foreach ($attempt in $attemptValues) {
            if (-not (Test-JointJsonObject $attempt)) { Throw-JointReject 'persona.schema_invalid' 'isolation' ('$.attempts[' + $attemptIndex + ']') 'Isolation attempt must be an object.' }
            Assert-JointExactFields $attempt @('operation','requestedPath','execution','result','pathResolution') @('operation','requestedPath','execution','result','pathResolution') ('$.attempts[' + $attemptIndex + ']')
            if ((Get-JointRequiredString $attempt 'execution' '$.attempts') -ne 'isolated-sibling-copy-only' -or (Get-JointRequiredString $attempt 'result' '$.attempts') -ne 'rejected' -or (Get-JointRequiredString $attempt 'pathResolution' '$.attempts') -ne 'protected-root-guard') { Throw-JointReject 'persona.path_protected' 'isolation' ('$.attempts[' + $attemptIndex + ']') 'Isolation attempt does not declare the protected-root guard contract.' }
            $requestedPath = Get-JointFullPath (Get-JointRequiredString $attempt 'requestedPath' '$.attempts')
            if (Test-JointReparsePath $requestedPath) { Throw-JointReject 'persona.path_protected' 'isolation' ('$.attempts[' + $attemptIndex + '].requestedPath') 'Requested path traverses a reparse point.' }
            $underProtectedRoot = $false
            foreach ($entry in $protectedEntries) { if (Test-JointPathUnder $requestedPath $entry.FullPath -AllowEqual) { $underProtectedRoot = $true; break } }
            if (-not $underProtectedRoot) { Throw-JointReject 'persona.path_protected' 'isolation' ('$.attempts[' + $attemptIndex + '].requestedPath') 'Requested destructive path is outside the declared protected roots.' }
            $attemptIndex++
        }
        $beforeSnapshot = Get-JointProtectedPathSnapshot @($protectedEntries | ForEach-Object { $_.FullPath })
        for ($index = 0; $index -lt $protectedEntries.Count; $index++) {
            if ($beforeSnapshot[$index].sha256 -ne $protectedEntries[$index].DeclaredBeforeSha256) { Throw-JointReject 'persona.registry_digest_mismatch' 'isolation' ('$.protectedRoots[' + $index + '].beforeSha256') 'Declared frozen-root hash does not match the observed root.' }
        }
        Add-JointAssertion $Assertions 'protected_root_hashes_verified' $true 'Declared before hashes match the observed frozen roots.'
        Add-JointAssertion $Assertions 'isolation_attempts_verified' $true 'Every declared destructive attempt targets a protected root and declares isolated execution.'
        Add-JointAssertion $Assertions 'isolated_sibling_verified' $true 'The declared isolated sibling does not overlap a protected root.'
        $siblingParent = Split-Path -Parent $isolatedSiblingPath
        $siblingParentExisted = Test-Path -LiteralPath $siblingParent -PathType Container
        $siblingRootExisted = Test-Path -LiteralPath $isolatedSiblingPath -PathType Container
        if (-not $siblingParentExisted) { New-Item -ItemType Directory -Path $siblingParent -Force | Out-Null }
        if (-not $siblingRootExisted) { New-Item -ItemType Directory -Path $isolatedSiblingPath -Force | Out-Null }
        $sandbox = Join-Path $isolatedSiblingPath ('attempt-' + [Guid]::NewGuid().ToString('N'))
        if (-not (Test-JointPathUnder $sandbox $allowedSiblingRoot -AllowEqual:$false)) { Throw-JointReject 'persona.path_protected' 'isolation' '$.isolatedSibling' 'Isolation sandbox escapes the dedicated temporary sibling root.' }
        New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
        $CleanupPaths.Add($sandbox)
        if (-not $siblingRootExisted) { $CleanupPaths.Add($isolatedSiblingPath) }
        if (-not $siblingParentExisted) { $CleanupPaths.Add($siblingParent) }
        [IO.File]::WriteAllText((Join-Path $sandbox 'simulated-destructive-attempt.txt'), 'isolated-only', $script:JointUtf8)
        $afterSnapshot = Get-JointProtectedPathSnapshot @($protectedEntries | ForEach-Object { $_.FullPath })
        $unchanged = Compare-JointSnapshots $beforeSnapshot $afterSnapshot
        Add-JointAssertion $Assertions 'frozen_candidate_hashes_unchanged' $unchanged 'Protected roots were read before and after isolated attempts; no real root mutation was performed.'
        if (-not $unchanged) { Throw-JointReject 'persona.frozen_candidate_changed' 'isolation' '$.protectedRoots' 'Protected root snapshot changed.' }
        for ($index = 0; $index -lt $protectedEntries.Count; $index++) {
            if ($afterSnapshot[$index].sha256 -ne $protectedEntries[$index].DeclaredAfterSha256) { Throw-JointReject 'persona.frozen_candidate_changed' 'isolation' ('$.protectedRoots[' + $index + '].afterSha256') 'Declared after hash does not match the observed root.' }
        }
        Add-JointAssertion $Assertions 'declared_after_hashes_verified' $true 'Declared after hashes match the observed unchanged roots.'
        Add-JointAssertion $Assertions 'destructive_attempts_isolated' $true 'Destructive attempts were confined to an isolated sibling.'
        Add-JointAssertion $Assertions 'declared_isolated_sibling_used' $true ('Isolation sandbox was created under ' + $isolatedSiblingPath)
        return [pscustomobject]@{ Mode = 'pass'; ObservedWarnings = @() }
    }    Throw-JointReject 'persona.operation_unsupported' 'fixture' '$.operation' ('Unsupported fixture operation: ' + $operation)
}

function Add-JointExpectedFixtureAssertions([string]$FixtureDirectory, [string]$FixtureId, [object]$Report, [object]$Adapted, [Collections.Generic.List[object]]$Assertions) {
    $expectedPath = Join-Path $FixtureDirectory 'expected.json'
    if (-not (Test-Path -LiteralPath $expectedPath -PathType Leaf)) {
        Add-JointAssertion $Assertions 'expected_contract_available' $true 'No expected.json supplied; operation-specific assertions were used.'
        return
    }
    $expected = Read-JointJsonFile $expectedPath ('fixture ' + $FixtureId + ' expected contract')
    $expectedSchema = Get-JointOptionalString $expected.Value 'schemaVersion' '$.expected'
    Add-JointAssertion $Assertions 'expected_schema_match' ($expectedSchema -eq 'awake.persona.fixture-expected.v1') ('Expected schema=' + $expectedSchema)
    $expectedFixtureId = Get-JointOptionalString $expected.Value 'fixtureId' '$.expected'
    Add-JointAssertion $Assertions 'expected_fixture_id_match' ($expectedFixtureId -eq $FixtureId) ('Expected fixtureId=' + $expectedFixtureId + '; observed=' + $FixtureId)
    $expectedGate = Get-JointOptionalString $expected.Value 'gate' '$.expected'
    Add-JointAssertion $Assertions 'expected_gate_e2' ($expectedGate -eq 'E2') ('Expected gate=' + $expectedGate)
    $expectedStatus = Get-JointOptionalString $expected.Value 'status' '$.expected'
    $statusMatches = -not [string]::IsNullOrWhiteSpace($expectedStatus) -and $expectedStatus -eq [string]$Report.status
    Add-JointAssertion $Assertions 'expected_status_match' $statusMatches ('Expected=' + $expectedStatus + '; observed=' + [string]$Report.status)
    if (Test-JointJsonProperty $expected.Value 'exitCode') {
        $expectedExitCode = [int](Get-JointJsonProperty $expected.Value 'exitCode')
        Add-JointAssertion $Assertions 'expected_exit_code_match' ($expectedExitCode -eq [int]$Report.exitCode) ('Expected=' + $expectedExitCode + '; observed=' + [int]$Report.exitCode)
        $statusExitCodes = @{ pass = 0; reject = 10; blocked = 20; not_attempted = 30; error = 40 }
        $expectedPairMatches = $statusExitCodes.ContainsKey($expectedStatus) -and [int]$statusExitCodes[$expectedStatus] -eq $expectedExitCode
        Add-JointAssertion $Assertions 'expected_status_exit_pair' $expectedPairMatches ('Expected status/exit pair=' + $expectedStatus + '/' + $expectedExitCode)
    }
    if ($null -ne $Adapted -and (Test-JointJsonProperty $expected.Value 'digests')) {
        $actualDigests = [ordered]@{ sourceSha256 = $Adapted.SourceSha256; registrySha256 = $Adapted.RegistrySha256; authoringSha256 = $Adapted.AuthoringSha256; mappingReportSha256 = $Adapted.MappingReportSha256; definitionSha256 = $Adapted.DefinitionSha256; exportSha256 = $Adapted.ExportSha256 }
        $expectedDigests = Get-JointJsonProperty $expected.Value 'digests'
        foreach ($name in (Get-JointJsonProperties $expectedDigests)) {
            $matches = [string]$actualDigests[$name] -eq [string](Get-JointJsonProperty $expectedDigests $name)
            Add-JointAssertion $Assertions ('expected_digest_' + $name) $matches ('Expected=' + [string](Get-JointJsonProperty $expectedDigests $name) + '; actual=' + [string]$actualDigests[$name])
        }
    }
    if (Test-JointJsonProperty $expected.Value 'handoff') {
        $expectedHandoff = Get-JointJsonProperty $expected.Value 'handoff'
        if ($null -eq $Report.handoff) {
            Add-JointAssertion $Assertions 'expected_handoff_present' $false 'Expected handoff evidence but report.handoff is null.'
        } else {
            $handoffMatches = $true
            foreach ($name in (Get-JointJsonProperties $expectedHandoff)) {
                $actualValue = Get-JointRawProperty $Report.handoff $name
                $expectedValue = Get-JointJsonProperty $expectedHandoff $name
                $matches = [string]$actualValue -eq [string]$expectedValue
                if (-not $matches) { $handoffMatches = $false }
                Add-JointAssertion $Assertions ('expected_handoff_' + $name) $matches ('Expected=' + [string]$expectedValue + '; actual=' + [string]$actualValue)
            }
            Add-JointAssertion $Assertions 'expected_handoff_tuple' $handoffMatches 'Handoff revisions and approval evidence IDs match the fixture expectation.'
        }
    }
    $expectedErrorSpecs = [Collections.Generic.List[object]]::new()
    if (Test-JointJsonProperty $expected.Value 'observedError') {
        $expectedError = Get-JointJsonProperty $expected.Value 'observedError'
        if ($expectedError -is [string]) {
            $expectedErrorSpecs.Add([ordered]@{ code = [string]$expectedError; stage = ''; path = ''; retryable = $null })
        } elseif (Test-JointJsonObject $expectedError -and (Test-JointJsonProperty $expectedError 'code')) {
            $expectedErrorSpecs.Add([ordered]@{
                code = Get-JointRequiredString $expectedError 'code' '$.expected.observedError'
                stage = Get-JointOptionalString $expectedError 'stage' '$.expected.observedError'
                path = Get-JointOptionalString $expectedError 'path' '$.expected.observedError'
                retryable = if (Test-JointJsonProperty $expectedError 'retryable') { [bool](Get-JointJsonProperty $expectedError 'retryable') } else { $null }
            })
        } else {
            Throw-JointReject 'persona.schema_invalid' 'schema' '$.expected.observedError' 'observedError must be a string or an error object containing code.'
        }
    }
    if (Test-JointJsonProperty $expected.Value 'errors') {
        foreach ($error in (Get-JointJsonProperty $expected.Value 'errors')) {
            if (-not (Test-JointJsonObject $error) -or -not (Test-JointJsonProperty $error 'code')) { Throw-JointReject 'persona.schema_invalid' 'schema' '$.expected.errors' 'Each expected error must contain a code.' }
            $expectedErrorSpecs.Add([ordered]@{
                code = Get-JointRequiredString $error 'code' '$.expected.errors'
                stage = Get-JointOptionalString $error 'stage' '$.expected.errors'
                path = Get-JointOptionalString $error 'path' '$.expected.errors'
                retryable = if (Test-JointJsonProperty $error 'retryable') { [bool](Get-JointJsonProperty $error 'retryable') } else { $null }
            })
        }
    }
    $observedErrors = @($Report.observedErrors)
    if ($expectedErrorSpecs.Count -gt 0) {
        $observedCodes = @($observedErrors | ForEach-Object { [string]$_.code })
        $codeMatch = @($expectedErrorSpecs | Where-Object { $observedCodes -contains [string]$_.code }).Count -eq $expectedErrorSpecs.Count
        Add-JointAssertion $Assertions 'expected_error_code_observed' $codeMatch ('Expected=' + (($expectedErrorSpecs | ForEach-Object { $_.code }) -join ',') + '; observed=' + ($observedCodes -join ','))
        $tupleMatch = $true
        foreach ($spec in $expectedErrorSpecs) {
            $candidates = @($observedErrors | Where-Object { [string]$_.code -eq [string]$spec.code })
            if ($spec.stage) { $candidates = @($candidates | Where-Object { [string]$_.stage -eq [string]$spec.stage }) }
            if ($spec.path) { $candidates = @($candidates | Where-Object { [string]$_.path -eq [string]$spec.path }) }
            if ($null -ne $spec.retryable) { $candidates = @($candidates | Where-Object { [bool]$_.retryable -eq [bool]$spec.retryable }) }
            if ($candidates.Count -eq 0) { $tupleMatch = $false }
        }
        Add-JointAssertion $Assertions 'expected_error_tuple_observed' $tupleMatch 'Expected error code/stage/path/retryable tuple was observed.'
    } else {
        Add-JointAssertion $Assertions 'expected_no_observed_errors' ($observedErrors.Count -eq 0) ('Expected no observed errors; observed=' + $observedErrors.Count)
    }
}
try {
    $reportPathFull = Assert-JointOutputPath $ReportPath 'ReportPath'
    $artifactRoot = Get-JointArtifactRoot
    $outputPath = Join-Path $artifactRoot ($FixtureId + '.output.json')
    $outputPath = Assert-JointOutputPath $outputPath 'FixtureOutput'
    $outputBeforeHash = Get-JointHashFile $outputPath
    $fixture = Get-JointFixtureInput $FixtureId $FixtureRoot
    $inputPath = Join-Path (Split-Path -Parent $fixture.FullPath) 'input.json'
    $report.inputHashes.inputJsonSha256 = Get-JointHashFile $inputPath
    $report.inputHashes.fixtureCanonicalSha256 = $fixture.CanonicalSha256
    Add-JointAssertion $assertions 'fixture_input_read' $true ('Read ' + $fixture.FullPath)

    $inputObject = $fixture.Value
    $gate = Get-JointRequiredString $inputObject 'gate' '$'
    if ($gate -ne 'E2') { Throw-JointReject 'persona.gate_mismatch' 'fixture' '$.gate' 'This runner only executes E2 fixtures.' }
    $fixtureDirectory = Split-Path -Parent $fixture.FullPath
    $expectedStatus = if (Test-JointJsonProperty $inputObject 'expectedStatus') { Get-JointRequiredString $inputObject 'expectedStatus' '$' } else { '' }
    if ([string]::IsNullOrWhiteSpace($expectedStatus)) {
        $expectedPath = Join-Path $fixtureDirectory 'expected.json'
        if (Test-Path -LiteralPath $expectedPath -PathType Leaf) {
            $expectedDocument = Read-JointJsonFile $expectedPath ('fixture ' + $FixtureId + ' expected status')
            $expectedStatus = Get-JointOptionalString $expectedDocument.Value 'status' '$.expected'
        }
    }
    $operation = Get-JointRequiredString $inputObject 'operation' '$'
    if ($operation -in @('native-runtime-caller','dynamic-invalidation','persistence')) {
        if ($operation -eq 'persistence') {
            Assert-JointStorageFixturePrerequisite $inputObject
            Throw-JointNotAttempted 'persona.storage_fixture_handler_not_implemented' 'runtime' '$.operation' 'Storage prerequisite is present, but the isolated E2 persistence handler is not implemented.'
        }
        Assert-JointNativeFixturePrerequisite $inputObject
        Throw-JointNotAttempted 'persona.native_fixture_handler_not_implemented' 'runtime' '$.operation' 'Native prerequisite is present, but the isolated E2 runtime handler is not implemented.'
    }
    if ($operation -in @('migrate-export','validate-approval','validate-mapping','validate-schema')) {
        $adapted = Convert-JointWorkbenchToAwake $inputObject $FixtureId $fixtureDirectory
        $report.inputHashes.sourceSha256 = $adapted.SourceSha256
        $report.inputHashes.registrySha256 = if ($adapted.PSObject.Properties.Name -contains 'SourceRegistrySha256') { $adapted.SourceRegistrySha256 } else { $adapted.RegistrySha256 }
        $report.inputHashes.selectionSha256 = Get-JointCanonicalHash (Get-JointJsonProperty $inputObject 'selection')
        $report.handoff = [ordered]@{
            sourceRevision = [int]$adapted.SourceRevision
            authoringRevision = [int]$adapted.AuthoringRevision
            selectionRevision = [int]$adapted.SelectionRevision
            exportSelectionRevision = [int]$adapted.ExportSelectionRevision
            sourceSha256 = $adapted.SourceSha256
            sourceRegistrySha256 = $adapted.SourceRegistrySha256
            registrySha256 = $adapted.RegistrySha256
            authoringSha256 = $adapted.AuthoringSha256
            mappingReportSha256 = $adapted.MappingReportSha256
            exportSha256 = $adapted.ExportSha256
            definitionSha256 = $adapted.DefinitionSha256
            workbenchApprovalEvidenceId = $adapted.ApprovalEvidenceIds.workbench
            awakeApprovalEvidenceId = $adapted.ApprovalEvidenceIds.awake
        }
        Add-JointAssertion $assertions 'workbench_to_authoring_v2' $true 'Workbench v1 migrated to authoring-v2 without mutating source bytes.'
        Add-JointAssertion $assertions 'authoring_to_export_v1' $true 'Export envelope was constructed with explicit selection and distinct approvals.'
        Add-JointAssertion $assertions 'export_to_definition_v1' $true 'Definition-v1 was constructed and validated against the pinned target registry.'
        if ($null -ne $outputBeforeHash) { Add-JointAssertion $assertions 'previous_output_read' $true ('Previous output hash: ' + $outputBeforeHash) }
        $outputText = Get-JointCanonicalText $adapted.Result
        Write-JointUtf8Atomic $outputPath $outputText | Out-Null
        $outputAfterHash = Get-JointHashFile $outputPath
        if ([string]::IsNullOrWhiteSpace($outputAfterHash)) { Throw-JointReject 'persona.partial_export' 'export' '$.output' 'Output artifact was not durably written.' }
        $report.outputHashes.adapterResultSha256 = $outputAfterHash
        $report.outputHashes.authoringSha256 = $adapted.AuthoringSha256
        $report.outputHashes.mappingReportSha256 = $adapted.MappingReportSha256
        $report.outputHashes.exportSha256 = $adapted.ExportSha256
        $report.outputHashes.definitionSha256 = $adapted.DefinitionSha256
        Add-JointAssertion $assertions 'output_written_atomically' $true $outputPath
        $report.status = 'pass'
        $report.exitCode = 0
        $report.observedErrors = @()
    } else {
        Add-JointAssertion $assertions 'adapter_skipped_for_operation' $true ('Operation ' + $operation + ' uses its dedicated offline handler.')
        $operationResult = Invoke-JointNonMigrationFixture $inputObject $FixtureId $artifactRoot $cleanupPaths $assertions
        if ($operationResult.Mode -eq 'pass') {
            $report.status = 'pass'
            $report.exitCode = 0
            $report.observedErrors = @()
            if ($operationResult.ObservedWarnings.Count -gt 0) { Add-JointAssertion $assertions 'operation_warning_recorded' $true ($operationResult.ObservedWarnings -join ',') }
        }
    }
    Add-JointExpectedFixtureAssertions $fixtureDirectory $FixtureId $report $adapted $assertions
} catch {
    $errorRecord = Convert-JointExceptionToError $_.Exception $FixtureId
    $observedErrors.Add($errorRecord)
    $report.observedErrors = @($observedErrors)
    if ($errorRecord.kind -eq '__JOINT_BLOCKED__') {
        $report.status = 'blocked'
        $report.exitCode = 20
    } elseif ($errorRecord.kind -eq '__JOINT_NOT_ATTEMPTED__') {
        $report.status = 'not_attempted'
        $report.exitCode = 30
    } elseif ($errorRecord.kind -eq '__JOINT_REJECT__') {
        $report.status = 'reject'
        $report.exitCode = 10
    } else {
        $report.status = 'error'
        $report.exitCode = 40
    }
    $executionMatchesExpected = if ([string]::IsNullOrWhiteSpace($expectedStatus)) { $report.status -ne 'error' } else { $expectedStatus -eq $report.status }
    Add-JointAssertion $assertions 'fixture_execution' $executionMatchesExpected $errorRecord.code
    if ($null -ne $outputPath) {
        $outputAfterFailureHash = Get-JointHashFile $outputPath
        $report.outputHashes.lastKnownGoodBeforeSha256 = $outputBeforeHash
        $report.outputHashes.lastKnownGoodAfterSha256 = $outputAfterFailureHash
        $unchanged = $outputBeforeHash -eq $outputAfterFailureHash
        Add-JointAssertion $assertions 'last_known_good_unchanged' $unchanged 'Rejected or blocked execution never overwrites the previous valid output artifact.'
        if (-not $unchanged -and $report.status -ne 'error') {
            $report.status = 'error'
            $report.exitCode = 40
            $extra = Convert-JointExceptionToError ([InvalidOperationException]::new('Last-known-good output changed after a rejected execution.')) $FixtureId
            $observedErrors.Add($extra)
            $report.observedErrors = @($observedErrors)
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($fixtureDirectory)) { Add-JointExpectedFixtureAssertions $fixtureDirectory $FixtureId $report $adapted $assertions }
    if ($expectedStatus -and $expectedStatus -ne $report.status) {
        $observedStatus = [string]$report.status
        $report.status = 'error'
        $report.exitCode = 40
        $extra = [pscustomobject]@{
            schemaVersion = 'awake.persona.adapter-error.v1'
            errorId = Get-JointStableHashId 'error' ('persona.fixture_expectation_mismatch|' + $FixtureId + '|' + $expectedStatus + '|' + $observedStatus)
            code = 'persona.fixture_expectation_mismatch'
            stage = 'contract'
            artifactId = $FixtureId
            path = '$.expected.status'
            detail = 'Expected status ' + $expectedStatus + ' but observed ' + $observedStatus + '.'
            retryable = $false
            kind = '__JOINT_ERROR__'
        }
        $observedErrors.Add($extra)
        $report.observedErrors = @($observedErrors)
    }
} finally {
    foreach ($path in @($cleanupPaths)) {
        try { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force } } catch { $report.cleanup.errors += $_.Exception.Message }
    }
    $report.assertions = @($assertions)
    $report.cleanup.paths = @($cleanupPaths)
    $report.cleanup.status = if ($report.cleanup.errors.Count -eq 0) { 'pass' } else { 'error' }
    if ($report.cleanup.status -eq 'error' -and $report.status -eq 'pass') { $report.status = 'error'; $report.exitCode = 40 }
    if ($null -ne $reportPathFull) {
        try { Write-JointReport $report $reportPathFull } catch { Write-Error $_.Exception.Message; if ($report.exitCode -eq 0) { $report.exitCode = 40 } }
    }
}

exit ([int]$report.exitCode)
