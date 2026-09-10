param([string]$AwakeRoot = (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent))
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
function Fail([string]$message){throw "FAIL PersonaCrosswalkClosureTests: $message"}
function Assert([bool]$condition,[string]$message){if(-not $condition){Fail $message}}
function Read([string]$path){Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -Depth 100}
function Validate([object]$crosswalk,[string]$registryHash){
    $errors=[Collections.Generic.List[string]]::new(); $vocab=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal); foreach($property in 'rootFields','tagIds','axisIds','flagIds','facetStrengthIds','legacyFields','provenanceFields'){foreach($value in @($crosswalk.sourceVocabulary.$property)){[void]$vocab.Add([string]$value)}}
    $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal); foreach($row in @($crosswalk.rows)){if(-not $ids.Add([string]$row.sourceId)){$errors.Add('duplicate source vocabulary row')};${lookup} = ([string]$row.sourceId) -replace '^(tags|facetStrengths)\.',''; if(-not $vocab.Contains($lookup)){$errors.Add('unknown source vocabulary row: '+$row.sourceId)};if($row.registrySha256 -ne $registryHash){$errors.Add('registry digest drift: '+$row.sourceId)};if($row.status -in @('unmapped','reject') -and $row.lossPolicy -ne 'preserve_only'){$errors.Add('unmapped/reject row is not preserve_only: '+$row.sourceId)}}
    foreach($required in @($crosswalk.sourceVocabulary.rootFields)){if(-not $ids.Contains($required)){$errors.Add('missing root vocabulary row: '+$required)}}
    return @($errors | Select-Object -Unique)
}
$crosswalkPath=Join-Path $AwakeRoot 'docs\persona-contract\persona-workbench-to-awake.crosswalk.v1.json';$registryPath=Join-Path $AwakeRoot 'ModuleData\Worldbook\persona_definitions\tag_registry.json';$crosswalk=Read $crosswalkPath;$registryHash=(Get-FileHash -LiteralPath $registryPath -Algorithm SHA256).Hash.ToUpperInvariant();${errors}=@(Validate $crosswalk $registryHash);Assert ($errors.Count -eq 0) ('actual crosswalk is not closed: '+($errors -join '; '))
$temp=Join-Path $env:TEMP ('pwb-crosswalk-fixture-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $temp|Out-Null
try {
    $unknown=Read $crosswalkPath;$unknown.sourceVocabulary.rootFields += 'fixture.unknown';Assert (@(Validate $unknown $registryHash).Count -gt 0) 'unknown vocabulary must fail closed'
    $missing=Read $crosswalkPath;$missing.rows=@($missing.rows|Where-Object {$_.sourceId -ne 'id'});Assert (@(Validate $missing $registryHash).Count -gt 0) 'missing vocabulary row must fail closed'
    $duplicate=Read $crosswalkPath;$duplicate.rows=@($duplicate.rows)+$duplicate.rows[0];Assert (@(Validate $duplicate $registryHash).Count -gt 0) 'duplicate vocabulary row must fail closed'
    $drift=Read $crosswalkPath;$drift.rows[0].registrySha256='0'*64;Assert (@(Validate $drift $registryHash).Count -gt 0) 'registry digest drift must fail closed'
    $preserve=@($crosswalk.rows|Where-Object {$_.lossPolicy -eq 'preserve_only'});Assert ($preserve.Count -gt 0) 'fixture must contain preserve_only rows';Assert (@($crosswalk.rows|Where-Object {$_.status -in @('unmapped','reject')}).Count -gt 0) 'fixture must contain unmapped/reject rows'
    Write-Output 'PASS PersonaCrosswalkClosureTests';Write-Output "PreserveOnly=$($preserve.Count)";Write-Output "Rows=$(@($crosswalk.rows).Count)"
} finally { $resolved=(Resolve-Path -LiteralPath $temp).Path; if($resolved -like "$env:TEMP*"){Remove-Item -LiteralPath $resolved -Recurse -Force} }





