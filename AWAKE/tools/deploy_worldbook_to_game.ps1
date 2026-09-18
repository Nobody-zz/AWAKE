# Deploy the repository-side Worldbook package into a Bannerlord module directory.
#
# Normative spec: docs/worldbook-studio-plan/RUNTIME-MAPPING-CONTRACT.md , section
# "Repository-side package form" (2026-09-14).
#
# Source (repository)                         Target (game module)
#   ModuleData/Worldbook/manifest.json    ->    ModuleData/Worldbook/manifest.json      (OVERWRITTEN)
#   ModuleData/Worldbook/packages/**      ->    ModuleData/Worldbook/packages/**        (added)
#
# Why not just run tools/sync_module.ps1: that is the real pipeline, but it currently stops earlier at
# Assert-EmbeddedRuntimeMatchesBuild (the dist\...\Runtime payload predates the latest framework
# build). Refreshing that is the main line's call, so this script deliberately does the ONE thing the
# worldbook line owns: put the registry + package data in place. It touches nothing else.
#
# What is deliberately NOT touched (all verified unread by src/):
#   * root runtime.json / index.json / migration_report.json -- leftovers of the old flat v2 pilot
#     package. After the registry lands, only entrypoints.{runtime,index} are read, so these are dead.
#   * the eight v1 content directories (rules/ personality_background/ unnamed_persona/ voice_mapping/
#     event_data/ debt/ dialogue_history/ compressed_memory/) -- the runtime rejects awake.worldbook.v1
#     (WB2-SCHEMA-UNSUPPORTED:entry) and nothing under src/ reads them.
#   * persona_definitions/ -- that is the persona line's asset and has its own approval gate.
#
# Bytes are copied, never re-serialized: the three hashes in the package manifest are canonical-JSON
# hashes recomputed at runtime, so a byte-identical copy is what keeps them valid.

[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$GameRoot = 'D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\ModuleData\Worldbook',
    [string]$BackupRoot = '',
    [switch]$ConfirmDeploy,
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot

# Resolve against the POWERSHELL location, not [Environment]::CurrentDirectory (Set-Location does not
# move the .NET process CWD, so a relative -SourceRoot would silently resolve one level up).
function Get-FullPath([string]$Path) {
    if (-not [IO.Path]::IsPathRooted($Path)) {
        $Path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    }
    return [IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Read-Json([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "missing $Label : $Path" }
    try { return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json) }
    catch { throw "$Label is not valid JSON: $Path . $($_.Exception.Message)" }
}

$registrySchema = 'awake.worldbook.registry.v1'
$packageSchema = 'awake.worldbook.v2'

if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $projectRoot 'ModuleData\Worldbook' }
$sourceRootFull = Get-FullPath $SourceRoot
$gameRootFull = Get-FullPath $GameRoot
if ([string]::IsNullOrWhiteSpace($BackupRoot)) {
    $BackupRoot = Join-Path $projectRoot ('tools\worldbook-studio\artifacts\game-dir-deploy-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$backupRootFull = Get-FullPath $BackupRoot

# ---------------------------------------------------------------- 1. source side

$sourceRegistryPath = Join-Path $sourceRootFull 'manifest.json'
$registry = Read-Json $sourceRegistryPath 'source registry manifest'
if ([string]$registry.schemaVersion -ne $registrySchema) {
    throw "source manifest schemaVersion must be $registrySchema (got '$($registry.schemaVersion)')"
}
$packages = @($registry.packages)
if ($packages.Count -lt 1) { throw 'source registry lists no packages' }

$payload = New-Object 'System.Collections.Generic.List[object]'
foreach ($package in $packages) {
    $packageId = [string]$package.packageId
    foreach ($field in @('packageId', 'version', 'kind', 'relativePath', 'packageHash')) {
        if ([string]::IsNullOrWhiteSpace([string]$package.$field)) { throw "registry entry is missing $field" }
    }
    $relative = ([string]$package.relativePath).Replace('/', '\')
    if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|\\)\.\.(\\|$)') {
        throw "registry relativePath is not a safe relative path: $($package.relativePath)"
    }
    $packageSourceRoot = Join-Path $sourceRootFull $relative
    $packageManifestPath = Join-Path $packageSourceRoot 'manifest.json'
    $packageManifest = Read-Json $packageManifestPath "package manifest for $packageId"
    if ([string]$packageManifest.schemaVersion -ne $packageSchema) {
        throw "package '$packageId' schemaVersion must be $packageSchema"
    }
    $files = @('manifest.json')
    foreach ($entrypoint in @('runtime', 'index')) {
        $entryRelative = ([string]$packageManifest.entrypoints.$entrypoint).Replace('/', '\')
        if ([string]::IsNullOrWhiteSpace($entryRelative) -or [IO.Path]::IsPathRooted($entryRelative) -or $entryRelative -match '(^|[\\/])\.\.([\\/]|$)') {
            throw "package '$packageId' entrypoints.$entrypoint is not a safe relative path"
        }
        $files += $entryRelative
    }
    foreach ($name in $files) {
        $path = Join-Path $packageSourceRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "package '$packageId' is missing $name" }
    }
    $payload.Add([ordered]@{
        packageId = $packageId
        relativePath = $relative
        sourceRoot = $packageSourceRoot
        files = $files
    })
}
Write-Output "DEPLOY_SOURCE root=$sourceRootFull registry=$($registry.registryId) packages=$($packages.Count)"
foreach ($item in $payload) { Write-Output "DEPLOY_PACKAGE $($item.packageId) -> $($item.relativePath) files=$($item.files -join ',')" }

# ---------------------------------------------------------------- 2. target side / guards

if (-not $ValidateOnly) {
    if (-not $ConfirmDeploy) { throw 'Game directory writes require -ConfirmDeploy.' }
    $running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^(Bannerlord|TaleWorlds)' })
    if ($running.Count -gt 0) { throw "Bannerlord is running: $($running | Select-Object ProcessName, Id | ConvertTo-Json -Compress)" }
    if (-not (Test-Path -LiteralPath (Split-Path -Parent (Split-Path -Parent $gameRootFull)))) {
        throw "game module directory looks wrong (no grandparent for '$gameRootFull')"
    }
}

$targetRegistryPath = Join-Path $gameRootFull 'manifest.json'

# ---------------------------------------------------------------- 3. validate-only

if ($ValidateOnly) {
    $targetRegistry = Read-Json $targetRegistryPath 'deployed registry manifest'
    if ([string]$targetRegistry.schemaVersion -ne $registrySchema) { throw "deployed manifest schemaVersion must be $registrySchema" }
    if ((Get-Sha256 $sourceRegistryPath) -ne (Get-Sha256 $targetRegistryPath)) { throw 'deployed registry differs from source' }
    foreach ($item in $payload) {
        $targetPackageRoot = Join-Path $gameRootFull $item.relativePath
        foreach ($name in $item.files) {
            $sourceFile = Join-Path $item.sourceRoot $name
            $targetFile = Join-Path $targetPackageRoot $name
            if (-not (Test-Path -LiteralPath $targetFile -PathType Leaf)) { throw "deployed file is missing: $($item.relativePath)/$name" }
            if ((Get-Sha256 $sourceFile) -ne (Get-Sha256 $targetFile)) { throw "deployed file differs from source: $($item.relativePath)/$name" }
        }
    }
    Write-Output "DEPLOY_VALIDATE_OK root=$gameRootFull packages=$($payload.Count)"
    return
}

# ---------------------------------------------------------------- 4. backup then copy

New-Item -ItemType Directory -Force -Path $backupRootFull | Out-Null
if (Test-Path -LiteralPath $targetRegistryPath -PathType Leaf) {
    Copy-Item -LiteralPath $targetRegistryPath -Destination (Join-Path $backupRootFull 'manifest.json.before') -Force
    Write-Output "DEPLOY_BACKUP manifest.json.before sha=$((Get-Sha256 (Join-Path $backupRootFull 'manifest.json.before')).Substring(0,16))"
} else {
    Write-Output 'DEPLOY_BACKUP no previous manifest.json (nothing to back up)'
}
foreach ($name in @('runtime.json', 'index.json', 'migration_report.json')) {
    $previous = Join-Path $gameRootFull $name
    if (Test-Path -LiteralPath $previous -PathType Leaf) {
        Copy-Item -LiteralPath $previous -Destination (Join-Path $backupRootFull ($name + '.before')) -Force
    }
}

New-Item -ItemType Directory -Force -Path $gameRootFull | Out-Null
Copy-Item -LiteralPath $sourceRegistryPath -Destination $targetRegistryPath -Force
if ((Get-Sha256 $sourceRegistryPath) -ne (Get-Sha256 $targetRegistryPath)) { throw 'registry copy verification failed' }
Write-Output "DEPLOY_COPY manifest.json sha=$((Get-Sha256 $targetRegistryPath).Substring(0,16))"

foreach ($item in $payload) {
    $targetPackageRoot = Join-Path $gameRootFull $item.relativePath
    New-Item -ItemType Directory -Force -Path $targetPackageRoot | Out-Null
    foreach ($name in $item.files) {
        $sourceFile = Join-Path $item.sourceRoot $name
        $targetFile = Join-Path $targetPackageRoot $name
        Copy-Item -LiteralPath $sourceFile -Destination $targetFile -Force
        if ((Get-Sha256 $sourceFile) -ne (Get-Sha256 $targetFile)) { throw "copy verification failed: $($item.relativePath)/$name" }
    }
    $deployed = @(Get-ChildItem -LiteralPath $targetPackageRoot -File -Recurse | ForEach-Object { $_.Name } | Sort-Object)
    $expected = @($item.files | ForEach-Object { Split-Path -Leaf $_ } | Sort-Object)
    if (($deployed -join ',') -ne ($expected -join ',')) {
        throw "deployed package must hold exactly $($expected -join ' + ') (found: $($deployed -join ', '))"
    }
    Write-Output "DEPLOY_COPY $($item.relativePath) files=$($deployed -join ',')"
}

# ---------------------------------------------------------------- 5. re-read what we wrote

$written = Read-Json $targetRegistryPath 'deployed registry manifest'
if ([string]$written.schemaVersion -ne $registrySchema) { throw 'deployed registry failed to round-trip' }
foreach ($item in $payload) {
    $found = @($written.packages | Where-Object { [string]$_.packageId -eq $item.packageId })
    if ($found.Count -ne 1) { throw "deployed registry must list $($item.packageId) exactly once (found $($found.Count))" }
}

# ---------------------------------------------------------------- 6. report what was left alone

$untouchedRootFiles = @()
foreach ($name in @('runtime.json', 'index.json', 'migration_report.json')) {
    if (Test-Path -LiteralPath (Join-Path $gameRootFull $name) -PathType Leaf) { $untouchedRootFiles += $name }
}
$legacyDirs = @()
foreach ($name in @('rules', 'personality_background', 'unnamed_persona', 'voice_mapping', 'event_data', 'debt', 'dialogue_history', 'compressed_memory')) {
    if (Test-Path -LiteralPath (Join-Path $gameRootFull $name) -PathType Container) { $legacyDirs += $name }
}
Write-Output ("DEPLOY_UNTOUCHED root_files=" + $(if ($untouchedRootFiles.Count) { $untouchedRootFiles -join ',' } else { 'none' }))

$legacyCount = 0
foreach ($name in $legacyDirs) {
    $legacyCount += @(Get-ChildItem -LiteralPath (Join-Path $gameRootFull $name) -Recurse -File -Force -ErrorAction SilentlyContinue).Count
}
Write-Output ("DEPLOY_UNTOUCHED v1_dirs=" + $(if ($legacyDirs.Count) { $legacyDirs -join ',' } else { 'none' }) + " files=$legacyCount (read by nothing under src/)")
$personaRoot = Join-Path $gameRootFull 'persona_definitions'
if (Test-Path -LiteralPath $personaRoot -PathType Container) {
    $personaCount = @(Get-ChildItem -LiteralPath (Join-Path $personaRoot 'definitions') -File -Filter *.json -ErrorAction SilentlyContinue).Count
    Write-Output "DEPLOY_UNTOUCHED persona_definitions definitions=$personaCount (deployed by the persona line, not by this script)"
}

# The runtime walks up from the assembly directory: <ancestor>/ModuleData/Worldbook/manifest.json,
# at most 6 levels. From <module>\bin\Win64_Shipping_Client that is 2 levels up.
Write-Output "DEPLOY_ENTRYPOINT $gameRootFull\manifest.json"
Write-Output "DEPLOY_BACKUP_ROOT $backupRootFull"
Write-Output "DEPLOY_OK root=$gameRootFull packages=$($payload.Count)"
