[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$InputDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$inputPath = [IO.Path]::GetFullPath($InputDirectory)
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $inputPath -PathType Container)) { throw "InputDirectory not found: $inputPath" }
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

$count = 0
foreach ($file in Get-ChildItem -LiteralPath $inputPath -Filter '*.json' -File) {
    $source = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    $characterId = [string]$source.characterId
    if ([string]::IsNullOrWhiteSpace($characterId)) { $characterId = [IO.Path]::GetFileNameWithoutExtension($file.Name) }
    $id = 'draft.legacy.' + ($characterId -replace '[^A-Za-z0-9_.:-]', '_')
    $draft = [ordered]@{
        schemaVersion = 'awake.persona.definition.v1'
        id = $id
        characterId = $characterId
        sourcePackId = 'legacy-migration'
        templateVersion = '1'
        status = 'draft'
        priority = 0
        scope = 'character'
        core = [string]$source.personality
        identityFacts = [string]$source.background
        tags = @()
        bundles = @()
        experiences = @()
        migration = [ordered]@{
            sourceFile = $file.Name
            sourceSchema = 'awake.worldbook.personality_background.v1'
            requiresHumanApproval = $true
        }
    }
    $destination = Join-Path $outputPath ($file.BaseName + '.persona.draft.json')
    if ((Test-Path -LiteralPath $destination) -and -not $Force) { continue }
    $json = $draft | ConvertTo-Json -Depth 10
    [IO.File]::WriteAllText($destination, $json, [Text.UTF8Encoding]::new($false))
    $count++
}

Write-Output "PERSONA_LEGACY_MIGRATION_DRAFTS=$count"