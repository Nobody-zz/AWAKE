# Batch migrate persona cards to v3 vocabulary and sidecar layout.
# - Moves origins to *.origins.json sidecar
# - Fixes near-name drift (trait.caution -> trait.cautious, etc.)
# - Removes boundary tags (moved to global constraints)
# - Maps clear near-synonyms to registry equivalents
# - Drops low-frequency niche tags (meaning already in text descriptions)
#
# Usage: .\migrate-cards-v3.ps1 [-WhatIf]
#   -WhatIf : print planned changes without writing files

param(
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$base    = "D:\AWAKE-Dev\AWAKE"
$charDir = Join-Path $base "tools\persona-workbench\characters"

# Build tag migration map programmatically (PS5 hashtable parser is fussy with dotted keys + comments)
$tagMap = @{}

# Near-name drift
$tagMap.Add("trait.caution", "trait.cautious")
$tagMap.Add("trait.pride", "trait.proud")
$tagMap.Add("trait.tradition", "trait.traditional")
$tagMap.Add("trait.courage", "trait.courageous")

# Wrong category
$tagMap.Add("trait.direct", "expression.direct")
$tagMap.Add("trait.private", "trait.reserved")

# Near-synonyms -> merge
$tagMap.Add("expression.evasive", "expression.indirect")
$tagMap.Add("expression.reserved", "trait.reserved")
$tagMap.Add("expression.warm_to_allies", "expression.warm")
$tagMap.Add("expression.stoic", "expression.measured")
$tagMap.Add("behavior.tests_commitment", "behavior.tests_loyalty")
$tagMap.Add("behavior.guards_home", "behavior.protects_inner_circle")
$tagMap.Add("behavior.misrepresents", "trait.deceitful")
$tagMap.Add("behavior.seeks_power", "trait.ambitious")
$tagMap.Add("behavior.retaliates", $null)
$tagMap.Add("trigger.clan_safety", "trigger.family_safety")
$tagMap.Add("trigger.threat_to_house_honor", "trigger.reputation_challenge")

# Boundary tags -> all removed (2 moved to global constraints, others are per-card text)
$tagMap.Add("boundary.no_instant_submission", $null)
$tagMap.Add("boundary.no_modern_psychology", $null)
$tagMap.Add("boundary.no_direct_confrontation", $null)
$tagMap.Add("boundary.no_grand_ambition", $null)
$tagMap.Add("boundary.no_intrigue", $null)
$tagMap.Add("boundary.no_servility", $null)

# Low-frequency niche tags -> drop (text descriptions already cover them)
$tagMap.Add("trait.caution_for_family", $null)
$tagMap.Add("trait.hedonistic", $null)
$tagMap.Add("trait.wild", $null)
$tagMap.Add("trait.will", $null)
$tagMap.Add("expression.fierce", $null)
$tagMap.Add("expression.high_energy", $null)
$tagMap.Add("expression.informal", $null)
$tagMap.Add("expression.ingratiating", $null)
$tagMap.Add("trigger.being_put_on_pedestal", $null)
$tagMap.Add("trigger.duty_or_responsibility", $null)
$tagMap.Add("trigger.fear_of_decline", $null)

# Facet keys use the same mapping
$facetMap = $tagMap

$cards = Get-ChildItem $charDir -Filter "*.persona.json"
$changed = 0
$sidecars = 0
$tagMoves = 0
$tagDrops = 0
$facetMoves = 0
$facetDrops = 0

foreach ($f in $cards) {
    $baseName = $f.BaseName
    $name = $baseName -replace '\.persona$', ''
    $json = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $modified = $false

    # 1. Origins -> sidecar
    if ($json.PSObject.Properties.Name -contains "origins") {
        $origins = $json.origins
        $sidecarPath = Join-Path $charDir ($name + ".origins.json")
        if (-not $WhatIf) {
            $origins | ConvertTo-Json -Depth 10 | Set-Content -Path $sidecarPath -Encoding UTF8 -NoNewline
        }
        $json.PSObject.Properties.Remove("origins")
        $sidecars++
        $modified = $true
    }

    # 2. Tags array migration
    if ($json.tags) {
        $newTags = New-Object System.Collections.Generic.List[string]
        foreach ($t in $json.tags) {
            if ($tagMap.ContainsKey($t)) {
                $target = $tagMap[$t]
                if ($null -eq $target) {
                    Write-Host ("  DROP tag: " + $name + " / " + $t)
                    $tagDrops++
                    $modified = $true
                } elseif ($newTags -notcontains $target) {
                    Write-Host ("  MAP  tag: " + $name + " / " + $t + " -> " + $target)
                    $newTags.Add($target)
                    $tagMoves++
                    $modified = $true
                } else {
                    Write-Host ("  DROP dup tag: " + $name + " / " + $t + " (already have " + $target + ")")
                    $tagDrops++
                    $modified = $true
                }
            } else {
                # Keep as-is, but check for duplicates (may have been added by an earlier mapping)
                if ($newTags -notcontains $t) {
                    $newTags.Add($t)
                } else {
                    Write-Host ("  DROP dup tag: " + $name + " / " + $t + " (already present from mapping)")
                    $tagDrops++
                    $modified = $true
                }
            }
        }
        $json.tags = $newTags.ToArray()
    }

    # 3. FacetStrengths migration
    if ($json.facetStrengths) {
        $newFacets = @{}
        foreach ($prop in $json.facetStrengths.PSObject.Properties) {
            $k = $prop.Name
            $v = $prop.Value
            if ($facetMap.ContainsKey($k)) {
                $target = $facetMap[$k]
                if ($null -eq $target) {
                    Write-Host ("  DROP facet: " + $name + " / " + $k)
                    $facetDrops++
                    $modified = $true
                } else {
                    $isTrigger = $target.StartsWith("trigger.")
                    $isBoundary = $target.StartsWith("boundary.")
                    if ($isTrigger -or $isBoundary) {
                        Write-Host ("  DROP facet (unsupported category): " + $name + " / " + $k + " -> " + $target)
                        $facetDrops++
                        $modified = $true
                    } elseif ($newFacets.ContainsKey($target)) {
                        $newFacets[$target] = [Math]::Max([int]$newFacets[$target], [int]$v)
                        Write-Host ("  MERGE facet: " + $name + " / " + $k + " -> " + $target + " (kept max)")
                        $facetMoves++
                        $modified = $true
                    } else {
                        Write-Host ("  MAP  facet: " + $name + " / " + $k + " -> " + $target)
                        $newFacets[$target] = $v
                        $facetMoves++
                        $modified = $true
                    }
                }
            } else {
                $newFacets[$k] = $v
            }
        }
        $json.facetStrengths = [pscustomobject]$newFacets
    }

    if ($modified) {
        $changed++
        if (-not $WhatIf) {
            $json | ConvertTo-Json -Depth 10 | Set-Content -Path $f.FullName -Encoding UTF8 -NoNewline
        }
    }
}

Write-Host ""
Write-Host "=== Migration Summary ==="
Write-Host ("Cards processed: " + $cards.Count)
Write-Host ("Cards modified:  " + $changed)
Write-Host ("Sidecars created: " + $sidecars)
Write-Host ("Tags mapped:     " + $tagMoves)
Write-Host ("Tags dropped:    " + $tagDrops)
Write-Host ("Facets mapped:   " + $facetMoves)
Write-Host ("Facets dropped:  " + $facetDrops)
if ($WhatIf) { Write-Host "(WhatIf mode - no files written)" }