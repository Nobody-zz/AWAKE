[CmdletBinding()]
param(
    [string]$Root = '',
    [switch]$SkipReferenceHashCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Root)) { $Root = $PSScriptRoot }
$rootPath = [IO.Path]::GetFullPath($Root)

$manifestPath = Join-Path $rootPath 'import-manifest.v1.json'
$fixturePath = Join-Path $rootPath 'fixtures\fixture-state-ids.v1.json'
$prefabPath = Join-Path $rootPath 'assets\GauntletUILab.baseline.xml'

$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$referenceHashes = [System.Collections.Generic.List[object]]::new()

function Require([bool]$condition, [string]$failure) {
    if (-not $condition) { $failures.Add($failure) }
}

function Get-LabRelativePath([string]$basePath, [string]$fullPath) {
    $base = $basePath.TrimEnd('\', '/')
    $full = [IO.Path]::GetFullPath($fullPath)
    if ($full.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) {
        $relative = $full.Substring($base.Length).TrimStart('\', '/')
    }
    else {
        $relative = $full
    }
    return $relative.Replace('\', '/')
}

function Test-IgnoredPath([string]$relativePath) {
    foreach ($segment in $relativePath.Split('/')) {
        if ($segment -ieq 'bin' -or $segment -ieq 'obj' -or $segment -ieq '__pycache__') { return $true }
    }
    if ($relativePath.EndsWith('.pyc', [StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($relativePath.StartsWith('preview/out/', [StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($relativePath.StartsWith('out/', [StringComparison]::OrdinalIgnoreCase)) { return $true }
    return $false
}

function Remove-CodeComments([string]$text) {
    $stripped = [regex]::Replace($text, '/\*[\s\S]*?\*/', '')
    $stripped = [regex]::Replace($stripped, '//[^\r\n]*', '')
    return $stripped
}

$forbiddenExtensions = @('.dll', '.pdb', '.exe', '.so', '.dylib', '.zip', '.7z', '.nupkg', '.tpac', '.rar', '.msi')
$forbiddenNameTokens = @('GauntletUILabHost', 'GauntletUILabShared', 'UiLabRuntime', 'AnimusForge', 'LoveHate', 'Houkai', 'MarcusAINpc')
$forbiddenSourceTokens = @('TaleWorlds', 'NpcDialogueService', 'NpcDialogueOverlay', 'NpcDialogueVM', 'Campaign', 'AwakeLog', 'HttpClient', 'System.Net', 'Microsoft.Data.Sqlite', 'Newtonsoft')
$forbiddenPrefabTokens = @('(?i)AnimusForge', '(?i)LoveHate', '(?i)Houkai')
$legacySourceTokens = @('GauntletUILabHost', 'GauntletUILabShared', 'UiLabRuntime')

$alwaysAllowed = @('import-manifest.v1.json', 'verify-import.ps1', '.gitignore')

# ------------------------------------------------------------------ 1. parse

$manifest = $null
$fixtures = $null
$prefab = $null
$parsed = $false

try {
    $manifest = Get-Content -Raw -LiteralPath $manifestPath -Encoding UTF8 | ConvertFrom-Json
    $fixtures = Get-Content -Raw -LiteralPath $fixturePath -Encoding UTF8 | ConvertFrom-Json
    [xml]$prefab = Get-Content -Raw -LiteralPath $prefabPath -Encoding UTF8
    $parsed = $true
}
catch {
    $failures.Add('parse_failed:' + $_.Exception.Message)
}

# ------------------------------------------------------------------ 2. manifest / fixture / prefab contract

if ($parsed) {
    Require ($manifest.schema -eq 'awake.ui-lab.import.v1') 'unexpected_manifest_schema'
    Require ($fixtures.schema -eq 'awake.ui-lab.fixture-states.v1') 'unexpected_fixture_schema'
    Require ($fixtures.states -contains 'dialogue.command-confirmation') 'confirmation_fixture_missing'
    Require ($fixtures.states -contains 'dialogue.command-rejected') 'rejection_fixture_missing'

    $prefabAsset = @($manifest.assets | Where-Object { $_.id -eq 'gauntlet-ui-lab-prefab-baseline' })
    Require ($prefabAsset.Count -eq 1) 'prefab_manifest_entry_missing'
    if ($prefabAsset.Count -eq 1) {
        $actualHash = (Get-FileHash -LiteralPath $prefabPath -Algorithm SHA256).Hash
        Require ($actualHash -eq $prefabAsset[0].sourceSha256) 'prefab_hash_drift'
    }

    $xmlText = Get-Content -Raw -LiteralPath $prefabPath -Encoding UTF8
    foreach ($token in $forbiddenPrefabTokens) {
        Require ($xmlText -notmatch $token) ('legacy_mod_token_found:' + $token)
    }

    $detailPanel = $prefab.SelectSingleNode("//Widget[@Id='DetailPanel']")
    $detailClip = $prefab.SelectSingleNode("//Widget[@Id='DetailClip']")
    $backButton = $prefab.SelectSingleNode("//ButtonWidget[@Id='BackButton']")
    Require ($null -ne $detailPanel -and $null -ne $detailClip -and $null -ne $backButton) 'required_prefab_nodes_missing'
    if ($null -ne $detailClip -and $null -ne $backButton) {
        Require (-not $detailClip.SelectNodes(".//ButtonWidget[@Id='BackButton']").Count) 'back_button_inside_detail_clip'
    }

    Require ($manifest.hostIdentity.movieId -eq 'AwakeUiLab') 'host_identity_movie_id_unexpected'
    Require ($manifest.hostIdentity.movieId -ne $manifest.hostIdentity.reservedProductionMovieId) 'host_identity_reuses_production_movie'
}

# ------------------------------------------------------------------ 3. recursive whitelist + category rejection

$scannedFileCount = 0
$whitelisted = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

if ($parsed) {
    foreach ($entry in @($manifest.whitelist.files)) {
        if (-not [string]::IsNullOrWhiteSpace($entry)) {
            [void]$whitelisted.Add(([string]$entry).Replace('\', '/'))
        }
    }
    foreach ($entry in $alwaysAllowed) { [void]$whitelisted.Add($entry) }
    Require ($whitelisted.Count -ge 1) 'whitelist_empty'
}

foreach ($file in @(Get-ChildItem -LiteralPath $rootPath -Recurse -File -Force -ErrorAction SilentlyContinue)) {
    $relative = Get-LabRelativePath $rootPath $file.FullName
    if (Test-IgnoredPath $relative) { continue }
    $scannedFileCount++

    if ($parsed -and -not $whitelisted.Contains($relative)) {
        $failures.Add('unexpected_file:' + $relative)
    }

    $extension = $file.Extension.ToLowerInvariant()
    if ($forbiddenExtensions -contains $extension) {
        $failures.Add('forbidden_binary_or_package:' + $relative)
    }

    if ($file.Name -ieq 'SubModule.xml') {
        $failures.Add('legacy_module_entry_found:' + $relative)
    }

    foreach ($token in $forbiddenNameTokens) {
        if ($relative.IndexOf($token, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $failures.Add('legacy_name_in_path:' + $relative + ':' + $token)
        }
    }
}

Require ($scannedFileCount -ge 1) 'lab_root_scan_empty'

if ($parsed) {
    foreach ($entry in $whitelisted) {
        if (Test-IgnoredPath $entry) { continue }
        $full = Join-Path $rootPath ($entry.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $full)) {
            $failures.Add('whitelisted_file_missing:' + $entry)
        }
    }
}

# ------------------------------------------------------------------ 4. fixture source dependency scan (no game / production services)

$sourceFilesScanned = 0
foreach ($directoryName in @('src', 'tests')) {
    $directory = Join-Path $rootPath $directoryName
    if (-not (Test-Path -LiteralPath $directory)) { continue }
    foreach ($file in @(Get-ChildItem -LiteralPath $directory -Recurse -File -Force -Filter '*.cs' -ErrorAction SilentlyContinue)) {
        $relative = Get-LabRelativePath $rootPath $file.FullName
        if (Test-IgnoredPath $relative) { continue }
        $sourceFilesScanned++

        $code = Remove-CodeComments (Get-Content -Raw -LiteralPath $file.FullName -Encoding UTF8)
        foreach ($token in $forbiddenSourceTokens) {
            if ($code.IndexOf($token, [StringComparison]::Ordinal) -ge 0) {
                $failures.Add('forbidden_source_dependency:' + $relative + ':' + $token)
            }
        }
        foreach ($token in $legacySourceTokens) {
            if ($code.IndexOf($token, [StringComparison]::Ordinal) -ge 0) {
                $failures.Add('legacy_source_token:' + $relative + ':' + $token)
            }
        }
    }
}

if ($parsed) {
    Require ($sourceFilesScanned -ge 1) 'fixture_source_scan_empty'
}

# ------------------------------------------------------------------ 5. historical reference hashes (reference_only)

if ($parsed -and -not $SkipReferenceHashCheck) {
    $referenceAssets = @($manifest.referenceAssets)
    Require ($referenceAssets.Count -ge 2) 'reference_assets_incomplete'
    foreach ($reference in $referenceAssets) {
        $sourcePath = [string]$reference.sourcePath
        $expected = [string]$reference.sourceSha256
        $entry = [ordered]@{
            id = [string]$reference.id
            sourcePath = $sourcePath
            expectedSha256 = $expected
            status = 'unknown'
            actualSha256 = $null
        }

        if ([string]::IsNullOrWhiteSpace($sourcePath) -or -not (Test-Path -LiteralPath $sourcePath)) {
            $entry.status = 'skipped_unreachable'
            $warnings.Add('reference_source_unreachable:' + $reference.id)
        }
        else {
            $actual = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
            $entry.actualSha256 = $actual
            if ($actual -eq $expected) {
                $entry.status = 'matched'
            }
            else {
                $entry.status = 'drift'
                $failures.Add('reference_hash_drift:' + $reference.id)
            }
        }
        $referenceHashes.Add([pscustomobject]$entry)
    }
}

# ------------------------------------------------------------------ output

$checks = @(
    'manifest',
    'fixture-states',
    'prefab-hash',
    'legacy-token-exclusion',
    'detail-hierarchy',
    'host-identity',
    'recursive-whitelist',
    'forbidden-binary-package-submodule',
    'legacy-name-rejection',
    'fixture-source-dependency-scan',
    'reference-source-hash'
)

[ordered]@{
    schema = 'awake.ui-lab.import-verification.v1'
    status = if ($failures.Count -eq 0) { 'passed' } else { 'failed' }
    root = $rootPath
    checks = $checks
    scannedFiles = $scannedFileCount
    sourceFilesScanned = $sourceFilesScanned
    referenceHashes = @($referenceHashes)
    warnings = @($warnings)
    failures = @($failures)
} | ConvertTo-Json -Depth 6

if ($failures.Count -ne 0) { exit 1 }
