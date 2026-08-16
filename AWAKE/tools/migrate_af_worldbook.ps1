<#
.SYNOPSIS
Convert an AF-format worldbook directory into native AWAKE worldbook format.

.DESCRIPTION
The script reads an AnimusForge PlayerExports worldbook (e.g. 卡拉迪亚编年史),
normalizes PascalCase fields to AWAKE camelCase, writes a manifest with
sourceFormat=awake, and preserves AF variant selection by writing
variantSelection=af-best on every rule.

The source directory is not modified. Use -DryRun to inspect counts and a
sample before writing.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [Parameter(Mandatory = $true)]
    [string]$Destination,

    [string]$Id,

    [switch]$DryRun
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$sourceFull = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Source).Path)
$destFull = [System.IO.Path]::GetFullPath($Destination)

if ([System.String]::Equals($sourceFull, $destFull, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Destination must be different from source."
}
$prefix = $sourceFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if ($destFull.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Destination cannot be inside the source worldbook directory."
}

function Rename-Property {
    param(
        [object]$Object,
        [string]$SourceName,
        [string]$TargetName
    )
    if ($null -eq $Object) { return }
    $property = $Object.PSObject.Properties[$SourceName]
    if ($null -eq $property) { return }
    $value = $property.Value
    $Object.PSObject.Properties.Remove($SourceName)
    $Object | Add-Member -NotePropertyName $TargetName -NotePropertyValue $value -Force
}

function Remove-NullProperties {
    param([object]$Object)
    if ($null -eq $Object) { return }
    $names = @($Object.PSObject.Properties.Name)
    foreach ($name in $names) {
        if ($null -eq $Object.PSObject.Properties[$name].Value) {
            $Object.PSObject.Properties.Remove($name)
        }
    }
}

function Test-EmptyObject {
    param([object]$Object)
    return $null -ne $Object
        -and $Object -is [System.Management.Automation.PSCustomObject]
        -and @($Object.PSObject.Properties).Count -eq 0
}

function Convert-When {
    param([object]$When)
    if ($null -eq $When) { return $null }
    Rename-Property $When 'HeroIds' 'heroIds'
    Rename-Property $When 'CharacterIds' 'characterIds'
    Rename-Property $When 'Cultures' 'cultures'
    Rename-Property $When 'KingdomIds' 'kingdomIds'
    Rename-Property $When 'SettlementIds' 'settlementIds'
    Rename-Property $When 'Roles' 'roles'
    Rename-Property $When 'IdentityIds' 'identityIds'
    Rename-Property $When 'IsFemale' 'isFemale'
    Rename-Property $When 'IsClanLeader' 'isClanLeader'
    Rename-Property $When 'MinAge' 'minAge'
    Rename-Property $When 'MaxAge' 'maxAge'
    Rename-Property $When 'ContentTier' 'contentTier'
    Rename-Property $When 'SkillMin' 'skillMin'
    Remove-NullProperties $When
    return $When
}

function Convert-Variant {
    param([object]$Variant)
    if ($null -eq $Variant) { return $null }
    Rename-Property $Variant 'Priority' 'priority'
    Rename-Property $Variant 'Content' 'content'
    $when = $null
    if ($null -ne $Variant.PSObject.Properties['when']) {
        $when = $Variant.PSObject.Properties['when'].Value
    }
    if ($null -ne $when) {
        $convertedWhen = Convert-When $when
        if (Test-EmptyObject $convertedWhen) { $convertedWhen = $null }
        if ($null -ne $convertedWhen) {
            $Variant | Add-Member -NotePropertyName 'when' -NotePropertyValue $convertedWhen -Force
        }
    }
    Remove-NullProperties $Variant
    return $Variant
}

function Convert-TextMapping {
    param([object]$Mapping)
    if ($null -eq $Mapping) { return $null }
    Rename-Property $Mapping 'SourceText' 'sourceText'
    Rename-Property $Mapping 'Kind' 'kind'
    Rename-Property $Mapping 'TargetId' 'targetId'
    Rename-Property $Mapping 'AgeMin' 'ageMin'
    Rename-Property $Mapping 'AgeMax' 'ageMax'
    Rename-Property $Mapping 'EmptyValueText' 'emptyValueText'
    Rename-Property $Mapping 'TrueText' 'trueText'
    Rename-Property $Mapping 'FalseText' 'falseText'
    Remove-NullProperties $Mapping
    return $Mapping
}

function Convert-Rule {
    param(
        [object]$Rule,
        [string]$FallbackId
    )
    if ($null -eq $Rule) { return $null }
    Rename-Property $Rule 'Id' 'id'
    Rename-Property $Rule 'Keywords' 'keywords'
    Rename-Property $Rule 'RagShortTexts' 'ragShortTexts'
    Rename-Property $Rule 'SemanticPrototypes' 'semanticPrototypes'
    Rename-Property $Rule 'Priority' 'priority'
    Rename-Property $Rule 'Variants' 'variants'
    Rename-Property $Rule 'TextMappings' 'textMappings'

    if ($null -eq $Rule.PSObject.Properties['id'] -or [string]::IsNullOrWhiteSpace([string]$Rule.PSObject.Properties['id'].Value)) {
        $Rule | Add-Member -NotePropertyName 'id' -NotePropertyValue $FallbackId -Force
    }
    if ($null -eq $Rule.PSObject.Properties['kind']) {
        $Rule | Add-Member -NotePropertyName 'kind' -NotePropertyValue 'background' -Force
    }
    if ($null -eq $Rule.PSObject.Properties['scope']) {
        $Rule | Add-Member -NotePropertyName 'scope' -NotePropertyValue 'npc' -Force
    }
    if ($null -eq $Rule.PSObject.Properties['persistence']) {
        $Rule | Add-Member -NotePropertyName 'persistence' -NotePropertyValue 'persistent' -Force
    }
    if ($null -eq $Rule.PSObject.Properties['priority']) {
        $Rule | Add-Member -NotePropertyName 'priority' -NotePropertyValue 0 -Force
    }
    $Rule | Add-Member -NotePropertyName 'variantSelection' -NotePropertyValue 'af-best' -Force

    $when = $null
    if ($null -ne $Rule.PSObject.Properties['when']) {
        $when = $Rule.PSObject.Properties['when'].Value
    }
    if ($null -ne $when) {
        $convertedWhen = Convert-When $when
        if (Test-EmptyObject $convertedWhen) { $convertedWhen = $null }
        if ($null -ne $convertedWhen) {
            $Rule | Add-Member -NotePropertyName 'when' -NotePropertyValue $convertedWhen -Force
        }
    }

    $variants = $null
    if ($null -ne $Rule.PSObject.Properties['variants']) {
        $variants = $Rule.PSObject.Properties['variants'].Value
    }
    if ($null -ne $variants) {
        $converted = @()
        foreach ($variant in $variants) {
            $converted += Convert-Variant $variant
        }
        $Rule | Add-Member -NotePropertyName 'variants' -NotePropertyValue $converted -Force
    }

    $mappings = $null
    if ($null -ne $Rule.PSObject.Properties['textMappings']) {
        $mappings = $Rule.PSObject.Properties['textMappings'].Value
    }
    if ($null -ne $mappings) {
        $converted = @()
        foreach ($mapping in $mappings) {
            $converted += Convert-TextMapping $mapping
        }
        $Rule | Add-Member -NotePropertyName 'textMappings' -NotePropertyValue $converted -Force
    }

    Remove-NullProperties $Rule
    return $Rule
}

function Convert-Persona {
    param(
        [object]$Persona,
        [string]$FallbackId
    )
    if ($null -eq $Persona) { return $null }
    Rename-Property $Persona 'Personality' 'personality'
    Rename-Property $Persona 'Background' 'background'
    Rename-Property $Persona 'VoiceId' 'voiceId'
    Rename-Property $Persona 'CharacterId' 'characterId'
    Rename-Property $Persona 'CharacterObjectId' 'characterId'
    if ($null -eq $Persona.PSObject.Properties['characterId'] -or [string]::IsNullOrWhiteSpace([string]$Persona.PSObject.Properties['characterId'].Value)) {
        $Persona | Add-Member -NotePropertyName 'characterId' -NotePropertyValue $FallbackId -Force
    }
    Remove-NullProperties $Persona
    return $Persona
}

function Find-JsonDirectory {
    param(
        [string]$Base,
        [string[]]$Candidates
    )
    foreach ($candidate in $Candidates) {
        $path = if ([System.IO.Path]::IsPathRooted($candidate)) { $candidate } else { Join-Path $Base $candidate }
        if (-not (Test-Path -LiteralPath $path -PathType Container)) { continue }
        $files = @(Get-ChildItem -LiteralPath $path -Filter *.json -File -ErrorAction SilentlyContinue)
        if ($files.Count -gt 0) { return $path }
    }
    return $null
}

$rulesDir = Find-JsonDirectory $sourceFull @('knowledge\rules', 'rules')
if ($null -eq $rulesDir) {
    throw "No rules directory found under $sourceFull (expected knowledge\rules or rules)."
}
$personaDir = Find-JsonDirectory $sourceFull @('personality_background', 'personality_background\personality_background')
if ($null -eq $personaDir) {
    Write-Warning "No personality_background directory found; personas will be skipped."
}

$ruleFiles = @(Get-ChildItem -LiteralPath $rulesDir -Filter *.json -File)
$personaFiles = if ($null -eq $personaDir) { @() } else { @(Get-ChildItem -LiteralPath $personaDir -Filter *.json -File) }
$auxDirs = @(
    'unnamed_persona',
    'voice_mapping',
    'event_data',
    'debt',
    'dialogue_history',
    'compressed_memory'
)

$report = [ordered]@{
    source = $sourceFull
    destination = $destFull
    id = $Id
    ruleFiles = $ruleFiles.Count
    personaFiles = $personaFiles.Count
    warnings = @()
    sample = $null
}

if ($DryRun) {
    $sample = Convert-Rule (Get-Content -LiteralPath $ruleFiles[0].FullName -Encoding UTF8 -Raw | ConvertFrom-Json) ($ruleFiles[0].BaseName)
    $report.sample = $sample
    $report | ConvertTo-Json -Depth 30
    return
}

New-Item -ItemType Directory -Path (Join-Path $destFull 'rules') -Force | Out-Null
if ($null -ne $personaDir) {
    New-Item -ItemType Directory -Path (Join-Path $destFull 'personality_background') -Force | Out-Null
}

$ruleCount = 0
$personaCount = 0

foreach ($file in $ruleFiles) {
    $token = Get-Content -LiteralPath $file.FullName -Encoding UTF8 -Raw | ConvertFrom-Json
    $fallback = $file.BaseName
    $json = $null
    if ($token -is [System.Array]) {
        $converted = @()
        for ($i = 0; $i -lt $token.Count; $i++) {
            $converted += Convert-Rule $token[$i] ($fallback + '_' + $i)
        }
        $ruleCount += $converted.Count
        $json = ConvertTo-Json -InputObject $converted -Depth 50
    }
    else {
        $converted = Convert-Rule $token $fallback
        $ruleCount += 1
        $json = $converted | ConvertTo-Json -Depth 50
    }
    $outFile = Join-Path (Join-Path $destFull 'rules') ($file.Name)
    [System.IO.File]::WriteAllText($outFile, $json, $utf8NoBom)
}

foreach ($file in $personaFiles) {
    $token = Get-Content -LiteralPath $file.FullName -Encoding UTF8 -Raw | ConvertFrom-Json
    $fallback = $file.BaseName
    $json = $null
    if ($token -is [System.Array]) {
        $converted = @()
        for ($i = 0; $i -lt $token.Count; $i++) {
            $converted += Convert-Persona $token[$i] ($fallback + '_' + $i)
        }
        $personaCount += $converted.Count
        $json = ConvertTo-Json -InputObject $converted -Depth 50
    }
    else {
        $converted = Convert-Persona $token $fallback
        $personaCount += 1
        $json = $converted | ConvertTo-Json -Depth 50
    }
    $outFile = Join-Path (Join-Path $destFull 'personality_background') ($file.Name)
    [System.IO.File]::WriteAllText($outFile, $json, $utf8NoBom)
}

$manifest = [ordered]@{
    schemaVersion = 'awake.worldbook.v1'
    id = if ([string]::IsNullOrWhiteSpace($Id)) { 'awake.worldbook.' + [System.IO.Path]::GetFileName($sourceFull).ToLowerInvariant() } else { $Id }
    sourceFormat = 'awake'
    rulesDirectory = 'rules'
    personaDirectory = 'personality_background'
}

foreach ($dir in $auxDirs) {
    if (Test-Path -LiteralPath (Join-Path $sourceFull $dir) -PathType Container) {
        $manifest[$dir + 'Directory'] = $dir
    }
}

$manifestJson = $manifest | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText((Join-Path $destFull 'manifest.json'), $manifestJson, $utf8NoBom)

foreach ($dir in $auxDirs) {
    $srcDir = Join-Path $sourceFull $dir
    if (-not (Test-Path -LiteralPath $srcDir -PathType Container)) { continue }
    $dstDir = Join-Path $destFull $dir
    Copy-Item -LiteralPath $srcDir -Destination $dstDir -Recurse -Force
}

$report.id = $manifest['id']
$report.ruleCount = $ruleCount
$report.personaCount = $personaCount
$report.auxiliaryDirectories = @($auxDirs | Where-Object { Test-Path -LiteralPath (Join-Path $sourceFull $_) -PathType Container })
$reportJson = $report | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText((Join-Path $destFull 'migration_report.json'), $reportJson, $utf8NoBom)

Write-Host "Migrated $ruleCount rules and $personaCount personas from $sourceFull"
Write-Host "Output: $destFull"
