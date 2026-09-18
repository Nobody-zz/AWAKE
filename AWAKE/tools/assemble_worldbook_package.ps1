# Assemble the repository-side Worldbook package form (2026-09-14 decision).
#
# Normative spec: docs/worldbook-studio-plan/RUNTIME-MAPPING-CONTRACT.md , section
# "Repository-side package form".
#
#     <ProjectRoot>/ModuleData/Worldbook/
#       manifest.json              awake.worldbook.registry.v1   <- the only entry point
#       packages/<slug>/           one universe package = one Studio compile output
#         manifest.json  runtime.json  index.json               (runtime only reads these 3)
#       persona_definitions/       left untouched by this script
#
# The runtime (src/WorldbookRuntime.cs LocateManifest) walks up from the assembly dir up to
# 6 levels looking for <ancestor>/ModuleData/Worldbook/manifest.json and accepts ONLY
# awake.worldbook.registry.v1 or awake.worldbook.v2 there. WorldbookPackageIntegrity reads
# just manifest.json + entrypoints.{runtime,index}, so report files stay tool-side.
#
# This script COPIES BYTES. It never re-serializes the package files: the three hashes in
# the manifest are canonical-JSON hashes recomputed at runtime, so a byte-identical copy is
# what keeps them valid.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,
    [string]$OutputRoot = '',
    [string]$Slug = '',
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot
$runtimeSchema = 'awake.worldbook.v2'
$registrySchema = 'awake.worldbook.registry.v1'
$requiredEntryFiles = @('runtime.json', 'index.json')

# Resolve against the POWERSHELL location, not [Environment]::CurrentDirectory.
# [IO.Path]::GetFullPath() uses the .NET process CWD, which Set-Location does NOT move
# (classic .NET/PowerShell split) -> a relative -PackageRoot silently resolved one level up.
function Get-FullPath([string]$Path) {
    if (-not [IO.Path]::IsPathRooted($Path)) {
        $Path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    }
    return [IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    [IO.File]::WriteAllText($Path, $Content, (New-Object Text.UTF8Encoding($false)))
}

function Read-Json([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "missing $Label : $Path" }
    try { return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json) }
    catch { throw "$Label is not valid JSON: $Path . $($_.Exception.Message)" }
}

function Get-HashValue($Hashes, [string]$Name, [string]$Label) {
    $value = [string]$Hashes.$Name
    if ($value -notmatch '^[0-9A-Fa-f]{64}$') { throw "$Label : bad or missing hash '$Name'" }
    return $value.ToLowerInvariant()
}

# ---------------------------------------------------------------- resolve paths

$packageRootFull = Get-FullPath $PackageRoot
if (-not (Test-Path -LiteralPath $packageRootFull -PathType Container)) { throw "PackageRoot is missing: $packageRootFull" }
$outputRootFull = if ([string]::IsNullOrWhiteSpace($OutputRoot)) { Get-FullPath (Join-Path $projectRoot 'ModuleData\Worldbook') } else { Get-FullPath $OutputRoot }

# ---------------------------------------------------------------- read + check the compiled package

$packageManifestPath = Join-Path $packageRootFull 'manifest.json'
$packageManifest = Read-Json $packageManifestPath 'package manifest'
if ([string]$packageManifest.schemaVersion -ne $runtimeSchema) { throw "package manifest schemaVersion must be $runtimeSchema (got '$($packageManifest.schemaVersion)')" }

$packageId = [string]$packageManifest.packageId
$version = [string]$packageManifest.version
$kind = [string]$packageManifest.kind
$worldId = [string]$packageManifest.worldId
if ([string]::IsNullOrWhiteSpace($packageId) -or [string]::IsNullOrWhiteSpace($version) -or
    [string]::IsNullOrWhiteSpace($kind) -or [string]::IsNullOrWhiteSpace($worldId)) {
    throw 'package manifest is missing one of packageId / version / kind / worldId'
}

$manifestHash = Get-HashValue $packageManifest.hashes 'manifestHash' 'package manifest'
$contentHash = Get-HashValue $packageManifest.hashes 'contentHash' 'package manifest'
$packageHash = Get-HashValue $packageManifest.hashes 'packageHash' 'package manifest'

$runtimeRelative = [string]$packageManifest.entrypoints.runtime
$indexRelative = [string]$packageManifest.entrypoints.index
if ([string]::IsNullOrWhiteSpace($runtimeRelative) -or [string]::IsNullOrWhiteSpace($indexRelative)) {
    throw 'package manifest entrypoints.runtime / entrypoints.index must both be set'
}
foreach ($relative in @($runtimeRelative, $indexRelative)) {
    if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)' -or $relative.Contains('\')) {
        throw "package manifest entrypoint path is not a safe relative path: $relative"
    }
}

if ([string]::IsNullOrWhiteSpace($Slug)) {
    $parts = $worldId.Split(':')
    $Slug = $parts[$parts.Count - 1]
}
if ($Slug -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw "slug is not a safe directory name: $Slug" }

# The assembled package holds the runtime three: the package manifest itself plus the two
# entrypoints declared in it. All three are copied BYTE-FOR-BYTE (see the header note).
$sourceFiles = @('manifest.json') + @($runtimeRelative, $indexRelative) | Sort-Object -Unique
foreach ($relative in $sourceFiles) {
    $path = Join-Path $packageRootFull ($relative.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "package file is missing: $relative" }
}

Write-Output "ASSEMBLE_SOURCE package=$packageId version=$version kind=$kind worldId=$worldId slug=$Slug"
Write-Output "ASSEMBLE_HASHES manifest=$manifestHash content=$contentHash package=$packageHash"

# ---------------------------------------------------------------- validate-only mode

$registryPath = Join-Path $outputRootFull 'manifest.json'
$packageOutDir = Join-Path (Join-Path $outputRootFull 'packages') $Slug

if ($ValidateOnly) {
    if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) { throw "registry is missing: $registryPath" }
    $registry = Read-Json $registryPath $registrySchema
    if ([string]$registry.schemaVersion -ne $registrySchema) { throw "registry schemaVersion must be $registrySchema" }
    $entry = @($registry.packages | Where-Object { [string]$_.packageId -eq $packageId })
    if ($entry.Count -ne 1) { throw "registry must list packageId $packageId exactly once (found $($entry.Count))" }
    if ([string]$entry[0].packageHash -ne $packageHash) { throw 'registry packageHash does not match the package manifest' }
    if ([string]$entry[0].manifestHash -ne $manifestHash) { throw 'registry manifestHash does not match the package manifest' }
    if ([string]$entry[0].contentHash -ne $contentHash) { throw 'registry contentHash does not match the package manifest' }
    if ([string]$entry[0].relativePath -ne "packages/$Slug") { throw "registry relativePath must be packages/$Slug" }
    if (-not (Test-Path -LiteralPath (Join-Path $packageOutDir 'manifest.json') -PathType Leaf)) { throw "assembled package manifest is missing: $packageOutDir" }
    foreach ($relative in $sourceFiles) {
        $sourcePath = Join-Path $packageRootFull ($relative.Replace('/', '\'))
        $targetPath = Join-Path $packageOutDir ($relative.Replace('/', '\'))
        if ((Get-Sha256 $sourcePath) -ne (Get-Sha256 $targetPath)) { throw "assembled file differs from source: $relative" }
    }
    Write-Output "ASSEMBLE_VALIDATE_OK root=$outputRootFull slug=$Slug"
    return
}

# ---------------------------------------------------------------- write
#
# Deliberately NOT destructive: we overwrite the three files and then ASSERT the shape.
# Stale extras are reported, never silently wiped -- a half-populated packages/ must be a
# loud failure, and one repository ships exactly one world package.

New-Item -ItemType Directory -Force -Path $outputRootFull | Out-Null
$packagesRoot = Join-Path $outputRootFull 'packages'
New-Item -ItemType Directory -Force -Path $packagesRoot | Out-Null
New-Item -ItemType Directory -Force -Path $packageOutDir | Out-Null

$copied = @()
foreach ($relative in $sourceFiles) {
    $sourcePath = Join-Path $packageRootFull ($relative.Replace('/', '\'))
    $targetPath = Join-Path $packageOutDir ($relative.Replace('/', '\'))
    $targetParent = Split-Path -Parent $targetPath
    if (-not (Test-Path -LiteralPath $targetParent)) { New-Item -ItemType Directory -Force -Path $targetParent | Out-Null }
    Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
    if ((Get-Sha256 $sourcePath) -ne (Get-Sha256 $targetPath)) { throw "copy verification failed: $relative" }
    $copied += $relative
}

$siblings = @(Get-ChildItem -LiteralPath $packagesRoot -Directory -Force |
    Where-Object { $_.Name -ne $Slug } | ForEach-Object { $_.Name } | Sort-Object)
if ($siblings.Count -gt 0) {
    throw "packages/ holds package directories that are not this world: $($siblings -join ', '). This script never deletes -- remove them by hand if they are stale."
}

$registryObject = [ordered]@{
    schemaVersion = $registrySchema
    registryId = 'awake:registry:installed'
    packages = @(
        [ordered]@{
            packageId = $packageId
            version = $version
            kind = $kind
            relativePath = "packages/$Slug"
            manifestHash = $manifestHash
            contentHash = $contentHash
            packageHash = $packageHash
            enabledByDefault = $true
        }
    )
}
Write-Utf8NoBom $registryPath (($registryObject | ConvertTo-Json -Depth 6) + [Environment]::NewLine)

# ---------------------------------------------------------------- re-read what we wrote

$writtenRegistry = Read-Json $registryPath 'registry'
if ([string]$writtenRegistry.schemaVersion -ne $registrySchema) { throw 'written registry failed to round-trip' }
$writtenEntry = @($writtenRegistry.packages)[0]
if ([string]$writtenEntry.packageHash -ne $packageHash) { throw 'written registry packageHash mismatch' }
if ([string]$writtenEntry.relativePath -ne "packages/$Slug") { throw 'written registry relativePath mismatch' }

$assembledFiles = @(Get-ChildItem -LiteralPath $packageOutDir -File -Recurse | ForEach-Object {
    $_.FullName.Substring($packageOutDir.Length + 1).Replace('\', '/')
} | Sort-Object)
$expectedFiles = @($script:requiredEntryFiles + @('manifest.json')) | Sort-Object
if (($assembledFiles -join ',') -ne ($expectedFiles -join ',')) {
    throw "assembled package must hold exactly manifest.json + $($requiredEntryFiles -join ' + ') (found: $($assembledFiles -join ', '))"
}
if ((Get-Sha256 (Join-Path $packageOutDir 'manifest.json')) -ne (Get-Sha256 $packageManifestPath)) {
    throw 'assembled package manifest differs from the compiled package manifest'
}

$personaRoot = Join-Path $outputRootFull 'persona_definitions'
Write-Output ("ASSEMBLE_PERSONA_DEFINITIONS " + $(if (Test-Path -LiteralPath $personaRoot -PathType Container) { "present (left untouched)" } else { "ABSENT" }))
Write-Output "ASSEMBLE_OK root=$outputRootFull slug=$Slug files=$($assembledFiles -join ',') registry=$registryPath"
