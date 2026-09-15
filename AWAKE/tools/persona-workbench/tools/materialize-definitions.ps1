# Requires: powershell (PS5 compatible). No external deps.
# Materialize each character card (persona.json + .origins.json sidecar) into a
# runtime persona definition (awake.persona.definition.v1), Direction-A model:
#   - keep narrative text + tags (as PersonaTagUse) + best-fit bundle
#   - DROP axis / facetStrengths (runtime model has no observations)
# Bundle assignment: max Jaccard overlap between the card's tag set and each
# registry bundle's tag set; empty when overlap is zero.
#
# Usage:
#   .\materialize-definitions.ps1
#   .\materialize-definitions.ps1 -CharactersDir <path> -RegistryPath <path> -OutDir <path>

[CmdletBinding()]
param(
    [string]$CharactersDir = '',
    [string]$RegistryPath = '',
    [string]$OutDir = ''
)

$ErrorActionPreference = 'Stop'

$base     = 'D:\AWAKE-Dev\AWAKE'
$toolsRoot = Join-Path $base 'tools\persona-workbench'
if (-not $CharactersDir) { $CharactersDir = Join-Path $toolsRoot 'characters' }
if (-not $RegistryPath)  { $RegistryPath  = Join-Path $base 'ModuleData\Worldbook\persona_definitions\tag_registry.json' }
if (-not $OutDir)        { $OutDir        = Join-Path $base 'ModuleData\Worldbook\persona_definitions\definitions' }

if (-not (Test-Path -LiteralPath $CharactersDir -PathType Container)) { throw "CharactersDir not found: $CharactersDir" }
if (-not (Test-Path -LiteralPath $RegistryPath -PathType Leaf))       { throw "RegistryPath not found: $RegistryPath" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
# 全量再生成：清掉旧 definition 产物，避免历史命名方案残留造成 DUPID。
# 保留非 *.definition.json 文件（如 hero_default.json 兜底模板）。
Get-ChildItem -LiteralPath $OutDir -Filter '*.definition.json' -File | Remove-Item -Force

$utf8NoBom = New-Object System.Text.UTF8Encoding $false

# --- load registry bundles for best-fit assignment ---
$registry = Get-Content -LiteralPath $RegistryPath -Raw -Encoding UTF8 | ConvertFrom-Json
$bundles = @()
foreach ($b in @($registry.bundles)) {
    $bundles += [PSCustomObject]@{ id = [string]$b.id; tags = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase) }
    foreach ($t in @($b.tags)) { $bundles[$bundles.Count - 1].tags.Add([string]$t) | Out-Null }
}

function Get-BestBundleId([string[]]$cardTags) {
    $bestId = $null
    $bestScore = 0.0
    $cardSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($t in $cardTags) { if (-not [string]::IsNullOrWhiteSpace($t)) { $cardSet.Add([string]$t) | Out-Null } }
    if ($cardSet.Count -eq 0) { return $null }
    foreach ($b in $bundles) {
        $inter = 0
        foreach ($t in $b.tags) { if ($cardSet.Contains($t)) { $inter++ } }
        if ($inter -eq 0) { continue }
        $union = $cardSet.Count + $b.tags.Count - $inter
        if ($union -eq 0) { continue }
        $score = [double]$inter / [double]$union
        if ($score -gt $bestScore) { $bestScore = $score; $bestId = $b.id }
    }
    return $bestId
}

$count = 0
$cardFiles = Get-ChildItem -LiteralPath $CharactersDir -Filter '*.persona.json' -File | Sort-Object Name
foreach ($cardFile in $cardFiles) {
    $card = Get-Content -LiteralPath $cardFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $baseName = $cardFile.Name -replace '\.persona\.json$',''

    $heroId = $null
    $sidecarPath = Join-Path $CharactersDir ($baseName + '.origins.json')
    if (Test-Path -LiteralPath $sidecarPath -PathType Leaf) {
        $side = Get-Content -LiteralPath $sidecarPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $heroId = [string]$side.heroId
    }
    if ([string]::IsNullOrWhiteSpace($heroId)) { $heroId = $baseName }

    $cardTags = @()
    foreach ($t in @($card.tags)) { $cardTags += [string]$t }
    $tagUses = New-Object System.Collections.ArrayList
    foreach ($t in $cardTags) { if (-not [string]::IsNullOrWhiteSpace($t)) { $null = $tagUses.Add([ordered]@{ id = [string]$t }) } }
    $bundleId = Get-BestBundleId $cardTags
    $bundlesOut = @()
    if ($bundleId) { $bundlesOut = @($bundleId) }

    function Get-NonEmptyStrings($list) {
    $out = New-Object System.Collections.ArrayList
    foreach ($it in @($list)) { if ($null -ne $it -and (-not [string]::IsNullOrWhiteSpace([string]$it))) { $null = $out.Add([string]$it) } }
    return $out
}

function Get-NonEmptyStrings($list) {
    $out = New-Object System.Collections.ArrayList
    foreach ($it in @($list)) { if ($null -ne $it -and (-not [string]::IsNullOrWhiteSpace([string]$it))) { $null = $out.Add([string]$it) } }
    return $out
}

$definitionId = 'awake.persona.character.' + ([string]$card.id) + '.v1'

    $def = [ordered]@{
        schemaVersion   = 'awake.persona.definition.v1'
        id              = $definitionId
        characterId     = $heroId
        role            = 'hero'
        sourcePackId    = [string]$card.sourcePackId
        templateVersion = [string]$card.templateVersion
        status          = [string]$card.status
        priority        = 10
        scope           = 'character'
        core            = [string]$card.core
        identityFacts   = [string]$card.identityFacts
        summary         = [string]$card.summary
        publicDescription   = [string]$card.publicDescription
        privateDescription  = [string]$card.privateDescription
        contradictionDescription = [string]$card.contradictionDescription
        selfClaimRules      = Get-NonEmptyStrings $card.selfClaimRules
        realSelfBehaviors   = Get-NonEmptyStrings $card.realSelfBehaviors
        selfClaimExamples   = Get-NonEmptyStrings $card.selfClaimExamples
        tags            = $tagUses
        bundles         = $bundlesOut
        experiences     = @()
        materialization = [ordered]@{
            sourceFile  = $cardFile.Name
            sourceSchema = 'persona-workbench.character.v1'
            dropAxisFacet = $true
            bundleAssignment = $(if ($bundleId) { 'jaccard_best_fit' } else { 'none' })
        }
    }

    $dest = Join-Path $OutDir ($baseName + '.definition.json')
    $json = $def | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText($dest, $json, $utf8NoBom)
    $count++
}

Write-Output ("DEFINITIONS_GENERATED=" + $count)
Write-Output ("OUT_DIR=" + $OutDir)