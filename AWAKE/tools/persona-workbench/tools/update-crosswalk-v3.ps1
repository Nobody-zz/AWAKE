# ============================================================
# update-crosswalk-v3.ps1
# Incrementally update crosswalk for v3 tag vocabulary
# ONLY adds/updates what's needed; preserves all existing encodings
# ============================================================

$ErrorActionPreference = "Stop"

$base = "D:\AWAKE-Dev\AWAKE"
$crosswalkPath = Join-Path $base "docs\persona-contract\persona-workbench-to-awake.crosswalk.v1.json"
$registryPath  = Join-Path $base "ModuleData\Worldbook\persona_definitions\tag_registry.json"

# --- Load JSON (use .NET API for PS5 compatibility with UTF-8 no-BOM) ---
$utf8 = New-Object System.Text.UTF8Encoding $false
$crosswalkRaw = [System.IO.File]::ReadAllText($crosswalkPath, $utf8)
$registryRaw  = [System.IO.File]::ReadAllText($registryPath, $utf8)
$json = $crosswalkRaw | ConvertFrom-Json
$registry = $registryRaw | ConvertFrom-Json

# --- Compute new registry hash ---
$newHash = (Get-FileHash -Path $registryPath -Algorithm SHA256).Hash.ToUpperInvariant()
Write-Host "New registry hash: $newHash"

# --- Build complete tag ID list (39 tags from registry) ---
$allTagIds = $registry.tags | ForEach-Object { $_.id }
Write-Host "Registry tag count: $($allTagIds.Count)"

# --- Build complete facet ID list (all tags except trigger category) ---
$facetTagIds = $registry.tags | Where-Object { $_.category -ne "trigger" } | ForEach-Object { $_.id }
Write-Host "Facet-eligible tag count: $($facetTagIds.Count)"

# --- Update sourceVocabulary ---
$json.sourceVocabulary.tagIds = $allTagIds
# Add facetIds property (Force = add or replace)
$json.sourceVocabulary | Add-Member -NotePropertyName facetIds -NotePropertyValue $facetTagIds -Force
Write-Host "Updated sourceVocabulary: tagIds=$($allTagIds.Count), facetIds=$($facetTagIds.Count)"

# --- Update targetRegistry ---
$json.targetRegistry.sha256 = $newHash
Write-Host "Updated targetRegistry.sha256"

# --- Update registrySha256 on all existing rows ---
$updatedRows = 0
foreach ($row in $json.rows) {
    if ($row.PSObject.Properties.Name -contains "registrySha256") {
        $row.registrySha256 = $newHash
        $updatedRows++
    }
}
Write-Host "Updated registrySha256 on $updatedRows existing rows"

# --- Determine which tag rows already exist ---
$existingTagSources = @{}
foreach ($row in $json.rows) {
    if ($row.sourceKind -eq "tag") {
        $existingTagSources[$row.sourceId] = $true
    }
}

# --- Add new tag rows (presence_preserve_only) ---
$newTagCount = 0
foreach ($tagId in $allTagIds) {
    $sourceId = "tags.$tagId"
    if ($existingTagSources.ContainsKey($sourceId)) { continue }

    $newRow = [PSCustomObject]@{
        sourceId       = $sourceId
        sourceKind     = "tag"
        targetField    = "tagSelectors"
        targetId       = $tagId
        mappingKind    = "direct_tag"
        valueEncoding  = "presence_preserve_only"
        lossPolicy     = "preserve"
        provenancePath = "/tags"
        registrySha256 = $newHash
        status         = "preserve_only"
    }
    $json.rows += $newRow
    $newTagCount++
    Write-Host "  ADD tag row: $sourceId"
}
Write-Host "Added $newTagCount new tag rows"

# --- Determine which facet rows already exist (by sourceId prefix) ---
$existingFacetSources = @{}
foreach ($row in $json.rows) {
    if ($row.sourceId -like "facetStrengths.*") {
        $existingFacetSources[$row.sourceId] = $true
    }
}

# --- Add new facet rows (facet_preserve_only, sourceKind = facet_strength) ---
$newFacetCount = 0
foreach ($tagId in $facetTagIds) {
    $sourceId = "facetStrengths.$tagId"
    if ($existingFacetSources.ContainsKey($sourceId)) { continue }

    $newRow = [PSCustomObject]@{
        sourceId       = $sourceId
        sourceKind     = "facet_strength"
        targetField    = "tagSelectors"
        targetId       = $tagId
        mappingKind    = "facet_to_selector"
        valueEncoding  = "facet_preserve_only"
        lossPolicy     = "preserve"
        provenancePath = "/facetStrengths"
        registrySha256 = $newHash
        status         = "preserve_only"
    }
    $json.rows += $newRow
    $newFacetCount++
    Write-Host "  ADD facet row: $sourceId"
}
Write-Host "Added $newFacetCount new facet rows"

# --- Remove boundary tag rows (boundary.no_empty_promises, boundary.public_humiliation) if present ---
# These are now global constraints, not per-persona tags
$preRemoveCount = $json.rows.Count
$json.rows = @($json.rows | Where-Object {
    -not ($_.sourceKind -eq "tag" -and $_.sourceId -like "tags.boundary.*")
})
$removedBoundary = $preRemoveCount - $json.rows.Count
if ($removedBoundary -gt 0) {
    Write-Host "Removed $removedBoundary boundary tag rows"
}

# --- Also remove boundary flag rows (flag_no_empty_promises, flag_public_humiliation) ---
$preRemoveCount = $json.rows.Count
$json.rows = @($json.rows | Where-Object {
    -not ($_.sourceId -like "flags.*_empty_promises" -or $_.sourceId -like "flags.*public_humiliation")
})
$removedFlags = $preRemoveCount - $json.rows.Count
if ($removedFlags -gt 0) {
    Write-Host "Removed $removedFlags boundary flag rows"
}

# --- Save as UTF-8 without BOM ---
$jsonStr = $json | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($crosswalkPath, $jsonStr, (New-Object System.Text.UTF8Encoding $false))

Write-Host ""
Write-Host "=== Crosswalk Update Summary ==="
Write-Host "Rows with updated sha256: $updatedRows"
Write-Host "New tag rows added:        $newTagCount"
Write-Host "New facet rows added:      $newFacetCount"
Write-Host "Boundary rows removed:     $($removedBoundary + $removedFlags)"
Write-Host "Total rows now:            $($json.rows.Count)"
Write-Host "Value encodings preserved: $($json.valueEncodings.Count)"
