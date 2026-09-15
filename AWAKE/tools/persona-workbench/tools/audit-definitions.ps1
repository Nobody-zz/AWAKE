# Validate materialized runtime definitions against the reader contract used by
# the game runtime (src/PersonaDataLoader.cs): unique ids, schemaVersion v1,
# valid status, resolvable bundles, well-formed tags. Also validates that every
# card tag passed compilation (no drift). Exit 0 = clean.
#
# Usage: .\audit-definitions.ps1 [-DefinitionsDir <path>] [-RegistryPath <path>]

[CmdletBinding()]
param(
    [string]$DefinitionsDir = '',
    [string]$RegistryPath = ''
)

$ErrorActionPreference = 'Stop'
$base = 'D:\AWAKE-Dev\AWAKE'
if (-not $DefinitionsDir) { $DefinitionsDir = Join-Path $base 'ModuleData\Worldbook\persona_definitions\definitions' }
if (-not $RegistryPath)   { $RegistryPath   = Join-Path $base 'ModuleData\Worldbook\persona_definitions\tag_registry.json' }

$registry = Get-Content -LiteralPath $RegistryPath -Raw -Encoding UTF8 | ConvertFrom-Json
$knownBundles = @{}
foreach ($b in @($registry.bundles)) { $knownBundles[[string]$b.id] = $true }
$knownTags = @{}
foreach ($t in @($registry.tags)) { $knownTags[[string]$t.id] = $true }

$errors = New-Object System.Collections.ArrayList
$ids = @{}
$files = Get-ChildItem -LiteralPath $DefinitionsDir -Filter '*.json' -File | Sort-Object Name
foreach ($f in $files) {
    $obj = $null
    try { $obj = Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json } catch { $null = $errors.Add("PARSE $($f.Name): $($_.Exception.Message)") ; continue }
    if ($obj -eq $null) { $null = $errors.Add("EMPTY $($f.Name)"); continue }

    $sv = [string]$obj.schemaVersion
    if ($sv -ne 'awake.persona.definition.v1') { $null = $errors.Add("SCHEMA $($f.Name): '$sv'") }

    $id = [string]$obj.id
    if ([string]::IsNullOrWhiteSpace($id)) { $null = $errors.Add("NOID $($f.Name)") }
    elseif ($ids.ContainsKey($id)) { $null = $errors.Add("DUPID $($f.Name): $id also used") }
    else { $ids[$id] = $true }

    $status = [string]$obj.status
    if ($status -notin @('approved','draft','disabled')) { $null = $errors.Add("STATUS $($f.Name): '$status'") }

    $scope = [string]$obj.scope
    $isCharacter = ($scope -eq 'character')
    if ($isCharacter -and [string]::IsNullOrWhiteSpace([string]$obj.characterId)) { $null = $errors.Add("NOCHARID $($f.Name)") }

    foreach ($t in @($obj.tags)) {
        $tid = [string]$t.id
        if ([string]::IsNullOrWhiteSpace($tid)) { if ($isCharacter) { $null = $errors.Add("TAGEMPTY $($f.Name)") } }
        elseif (-not $knownTags.ContainsKey($tid)) { $null = $errors.Add("TAGUNKNOWN $($f.Name): $tid") }
    }
    foreach ($bid in @($obj.bundles)) {
        if (-not $knownBundles.ContainsKey([string]$bid)) { $null = $errors.Add("BUNDLEUNKNOWN $($f.Name): $bid") }
    }
}

Write-Output ("DEFINITION_FILES=" + $files.Count)
Write-Output ("TAGS_IN_REGISTRY=" + $knownTags.Count + " BUNDLES_IN_REGISTRY=" + $knownBundles.Count)
if ($errors.Count -eq 0) {
    Write-Output "DEFINITIONS_VALID=1"
    exit 0
}
Write-Output "DEFINITIONS_VALID=0"
$errors | ForEach-Object { Write-Output ("  ERROR " + $_) }
exit 1