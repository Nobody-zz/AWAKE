# Direction A (B-3): demote axis + facetStrengths from emitting runtime selectors.
# Every axis/facet value becomes preserve_only (authoring-only, never runtime).
# Tag-based presence_emit rows remain the sole runtime selector source.
# idempotent; also prints the new crosswalk SHA-256 (for the embedded-resource pin).

$ErrorActionPreference = 'Stop'
$path = 'D:\AWAKE-Dev\AWAKE\docs\persona-contract\persona-workbench-to-awake.crosswalk.v1.json'
$j = Get-Content -Raw -Encoding UTF8 $path | ConvertFrom-Json

$ids = @('facet_mapped','axis_pragmatic_positive','axis_measured_positive','axis_indirect_negative','axis_conditional_positive','axis_unmapped_preserve_only')

$changed = 0
foreach ($ve in $j.valueEncodings) {
    if ($ve.id -notin $ids) { continue }
    if ($ve.actions -eq $null) { continue }
    $props = @($ve.actions.PSObject.Properties | Select-Object -ExpandProperty Name)
    foreach ($key in $props) {
        if ($key -eq '0') { continue }
        $ve.actions.$key = [ordered]@{ action = 'preserve_only' }
        $changed++
    }
}

$json = $j | ConvertTo-Json -Depth 40
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($path, $json, $utf8NoBom)

$bytes = [System.Text.Encoding]::UTF8.GetBytes((Get-Content -Raw -Encoding UTF8 $path))
$shaObj = [System.Security.Cryptography.SHA256]::Create()
try { $hashBytes = $shaObj.ComputeHash($bytes) } finally { $shaObj.Dispose() }
$sha = [System.BitConverter]::ToString($hashBytes).Replace('-','')
Write-Output ("CROSSWALK_ACTIONS_DEMOTED=" + $changed)
Write-Output ("CROSSWALK_SHA256=" + $sha)