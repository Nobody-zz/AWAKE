param(
    [Parameter(Mandatory = $true)][string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'persona-awake-joint.ps1')

$reportPathFull = $null
$checks = [Collections.Generic.List[object]]::new()

function Add-BridgeCheck([string]$Id, [bool]$Passed, [string]$Detail, [string]$Severity) {
    $script:checks.Add([ordered]@{
        id = $Id
        passed = $Passed
        severity = $Severity
        detail = $Detail
    })
}

function Find-BridgeMatches([object[]]$Files, [string]$Pattern) {
    $found = [Collections.Generic.List[object]]::new()
    foreach ($file in $Files) {
        $lines = Get-Content -LiteralPath $file.FullName
        for ($index = 0; $index -lt $lines.Count; $index++) {
            if ([string]$lines[$index] -match $Pattern) {
                $found.Add([ordered]@{
                    relativePath = Get-JointRelativePath $file.FullName $script:sourceRoot
                    line = $index + 1
                    text = ([string]$lines[$index]).Trim()
                })
            }
        }
    }
    return @($found)
}

try {
    $reportPathFull = Assert-JointOutputPath $ReportPath 'ReportPath'
    $script:sourceRoot = Join-Path $script:JointAwakeRoot 'src'
    if (-not (Test-Path -LiteralPath $script:sourceRoot -PathType Container)) {
        throw [DirectoryNotFoundException]::new('AWAKE src directory is missing.')
    }

    $sourceFiles = @(Get-ChildItem -LiteralPath $script:sourceRoot -Recurse -File -Filter '*.cs' | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj|dist|backup[^\\/]*)[\\/]'
    } | Sort-Object FullName)
    $sourceSnapshot = @(Get-JointProtectedPathSnapshot @($script:sourceRoot))
    $sourceTreeSha256 = Get-JointJsonProperty $sourceSnapshot[0] 'sha256'

    $workbenchMatches = @(Find-BridgeMatches $sourceFiles 'PersonaWorkbench|persona-workbench\.character\.v1|Get-JointCrosswalkBundle')
    $legacyPersonaMatches = @(Find-BridgeMatches $sourceFiles 'WorldbookRuntime\.Current|WorldbookService\.BuildPersona')
    $legacyKnowledgeMatches = @(Find-BridgeMatches $sourceFiles 'KnowledgeRuntime\.Current')
    $providerMatches = @(Find-BridgeMatches $sourceFiles 'PersonaRuntimeProvider\.BuildProjection')
    $contextSnapshotMatches = @(Find-BridgeMatches $sourceFiles '\bContextSnapshot\b')
    $runtimeBundleMatches = @(Find-BridgeMatches $sourceFiles '\bRuntimeBundle\b')

    $generatorPath = Join-Path $script:sourceRoot 'PersonaDslGenerator.cs'
    $generatorText = if (Test-Path -LiteralPath $generatorPath -PathType Leaf) { Get-Content -Raw -LiteralPath $generatorPath } else { '' }
    $fingerprintStart = $generatorText.IndexOf('internal static string ComputeFingerprint(', [StringComparison]::Ordinal)
    $fingerprintEnd = $generatorText.IndexOf('private static string BuildLegacyFallback(', [StringComparison]::Ordinal)
    $fingerprintRegion = if ($fingerprintStart -ge 0 -and $fingerprintEnd -gt $fingerprintStart) { $generatorText.Substring($fingerprintStart, $fingerprintEnd - $fingerprintStart) } else { '' }

    $runtimePath = Join-Path $script:sourceRoot 'WorldbookRuntime.cs'
    $runtimeText = if (Test-Path -LiteralPath $runtimePath -PathType Leaf) { Get-Content -Raw -LiteralPath $runtimePath } else { '' }
    $reloadStart = $runtimeText.IndexOf('internal static void Reload()', [StringComparison]::Ordinal)
    $reloadEnd = $runtimeText.IndexOf('private static string LocateManifest()', [StringComparison]::Ordinal)
    $reloadRegion = if ($reloadStart -ge 0 -and $reloadEnd -gt $reloadStart) { $runtimeText.Substring($reloadStart, $reloadEnd - $reloadStart) } else { '' }

    $overlayInvalidationMatches = @(Find-BridgeMatches @(
        (Get-Item -LiteralPath (Join-Path $script:sourceRoot 'WorldbookRuntime.cs')),
        (Get-Item -LiteralPath (Join-Path $script:sourceRoot 'WorldKnowledgeQueryService.cs')),
        (Get-Item -LiteralPath (Join-Path $script:sourceRoot 'WorldbookService.cs'))
    ) 'InvalidatePersona|ClearPersonaCache|_personaCache\.(Clear|Remove)')

    $personaPersistencePath = Join-Path $script:sourceRoot 'PersonaPersistenceModels.cs'
    $personaPersistenceText = if (Test-Path -LiteralPath $personaPersistencePath -PathType Leaf) { Get-Content -Raw -LiteralPath $personaPersistencePath } else { '' }
    $personaPersistenceMatches = @(Find-BridgeMatches @(Get-Item -LiteralPath $personaPersistencePath) '\b(PersonaPersistenceEnvelope|PersonaStorageKey|PersonaPersistenceValidator)\b')
    $personaPersistenceWiringMatches = @($personaPersistenceMatches | Where-Object { $_.relativePath -ne 'PersonaPersistenceModels.cs' })
    $storageContractPath = Join-Path $script:sourceRoot 'AwakeStorageContract.cs'
    $storageContractText = if (Test-Path -LiteralPath $storageContractPath -PathType Leaf) { Get-Content -Raw -LiteralPath $storageContractPath } else { '' }
    $knownSchemaStart = $storageContractText.IndexOf('internal static bool IsKnownSchema(', [StringComparison]::Ordinal)
    $knownSchemaEnd = $storageContractText.IndexOf('internal static string ExpectedSchema(', [StringComparison]::Ordinal)
    $knownSchemaRegion = if ($knownSchemaStart -ge 0 -and $knownSchemaEnd -gt $knownSchemaStart) { $storageContractText.Substring($knownSchemaStart, $knownSchemaEnd - $knownSchemaStart) } else { '' }
    $overlayPersistenceMatches = @(Find-BridgeMatches @(Get-Item -LiteralPath (Join-Path $script:sourceRoot 'AwakeTerminalBehavior.cs')) 'awake_worldbook_overlay_v1|awake_worldbook_activation_v1')

    Add-BridgeCheck 'workbench_runtime_boundary_absent' ($workbenchMatches.Count -eq 0) ('AWAKE source Workbench/crosswalk references=' + $workbenchMatches.Count) 'contract'
    Add-BridgeCheck 'legacy_persona_entry_absent' ($legacyPersonaMatches.Count -eq 0) ('Legacy Persona entry matches=' + $legacyPersonaMatches.Count) 'blocking'
    Add-BridgeCheck 'legacy_knowledge_entry_absent' ($legacyKnowledgeMatches.Count -eq 0) ('Legacy Knowledge fallback matches=' + $legacyKnowledgeMatches.Count) 'blocking'
    Add-BridgeCheck 'persona_runtime_provider_present' ($providerMatches.Count -gt 0) ('PersonaRuntimeProvider.BuildProjection matches=' + $providerMatches.Count) 'blocking'
    Add-BridgeCheck 'context_snapshot_present' ($contextSnapshotMatches.Count -gt 0) ('ContextSnapshot matches=' + $contextSnapshotMatches.Count) 'blocking'
    Add-BridgeCheck 'runtime_bundle_present' ($runtimeBundleMatches.Count -gt 0) ('RuntimeBundle matches=' + $runtimeBundleMatches.Count) 'blocking'
    Add-BridgeCheck 'fingerprint_includes_context_modes' ($fingerprintRegion -match '\bContextModes\b') 'Persona fingerprint includes ContextModes.' 'blocking'
    Add-BridgeCheck 'reload_preserves_last_known_good' (-not ($reloadRegion -match 'ShutdownCurrent\(\)\s*;[\s\S]*EnsureCreated\(\)')) 'Reload does not dispose the active bundle before replacement validation.' 'blocking'
    Add-BridgeCheck 'overlay_invalidates_persona_cache' ($overlayInvalidationMatches.Count -gt 0) ('Overlay/persona invalidation references=' + $overlayInvalidationMatches.Count) 'blocking'
    Add-BridgeCheck 'persona_persistence_contract_present' (($personaPersistenceText -match 'PersonaPersistenceEnvelope') -and ($personaPersistenceText -match 'PersonaStorageKey') -and ($personaPersistenceText -match 'PersonaPersistenceValidator')) ('Persona persistence model symbols=' + $personaPersistenceMatches.Count) 'blocking'
    Add-BridgeCheck 'persona_persistence_runtime_wired' ($personaPersistenceWiringMatches.Count -gt 0) ('Persona persistence external wiring references=' + $personaPersistenceWiringMatches.Count) 'blocking'
    Add-BridgeCheck 'persona_storage_schema_registered' (($knownSchemaRegion -match 'awake\.persona\.continuity\.v1') -and ($knownSchemaRegion -match 'awake\.persona\.override\.v1') -and ($knownSchemaRegion -match 'awake\.persona\.recovery\.v1')) 'Persona continuity/override/recovery schemas are registered in AwakeStorageContract.' 'blocking'
    Add-BridgeCheck 'overlay_persistence_path_observable' ($overlayPersistenceMatches.Count -gt 0) ('Overlay persistence key references=' + $overlayPersistenceMatches.Count) 'contract'

    $failedBlocking = @($checks | Where-Object { -not $_.passed -and $_.severity -eq 'blocking' })
    $observedErrors = @($failedBlocking | ForEach-Object {
        [pscustomobject]@{
            schemaVersion = 'awake.persona.adapter-error.v1'
            errorId = Get-JointStableHashId 'error' ('persona.runtime_bridge_check_failed|' + $_.id + '|' + $_.detail)
            code = 'persona.runtime_bridge_check_failed'
            stage = 'runtime_static'
            artifactId = 'runtime_bridge_static'
            path = '$.checks.' + $_.id
            detail = $_.detail
            retryable = $false
            kind = '__JOINT_REJECT__'
        }
    })
    $status = if ($failedBlocking.Count -eq 0) { 'pass' } else { 'reject' }
    $exitCode = if ($status -eq 'pass') { 0 } else { 10 }
    $report = [ordered]@{
        schemaVersion = 'awake.persona.runtime-bridge-static.v1'
        commandLine = 'pwsh -NoProfile -File "' + [IO.Path]::GetFullPath($PSCommandPath) + '" -ReportPath "' + $ReportPath + '"'
        normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
        sourceRoot = [IO.Path]::GetFullPath($script:sourceRoot)
        sourceFileCount = $sourceFiles.Count
        sourceTreeSha256 = $sourceTreeSha256
        status = $status
        exitCode = $exitCode
        interpretation = 'Static readiness audit only. A reject records the runtime bridge gaps that must be resolved in a separately approved Native/Storage batch; it does not claim a gameplay result.'
        checks = @($checks)
        matches = @()
        observedErrors = $observedErrors
    }
    Write-JointReport $report $reportPathFull
    exit $exitCode
} catch {
    $errorRecord = Convert-JointExceptionToError $_.Exception 'verify-runtime-bridge-static'
    $report = [ordered]@{
        schemaVersion = 'awake.persona.runtime-bridge-static.v1'
        commandLine = 'pwsh -NoProfile -File "' + [IO.Path]::GetFullPath($PSCommandPath) + '" -ReportPath "' + $ReportPath + '"'
        normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
        status = 'error'
        exitCode = 40
        interpretation = 'Static readiness audit failed to execute.'
        checks = @($checks)
        matches = @()
        observedErrors = @($errorRecord)
    }
    if ($null -ne $reportPathFull) { Write-JointReport $report $reportPathFull }
    exit 40
}
