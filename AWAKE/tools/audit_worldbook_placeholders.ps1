$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$rulesDir = Join-Path $root 'ModuleData\Worldbook\rules'

if (-not (Test-Path $rulesDir)) {
    Write-Output "WORLDBOOK_PLACEHOLDER_FAIL rules_dir_missing=$rulesDir"
    exit 1
}

$files = Get-ChildItem -LiteralPath $rulesDir -Filter '*.json' -File
$chinesePlaceholder = '某某|哪哪|待补|待定|TODO|TBD|XXX|xxx|XX|占位'
$singleLetter = '(?<![A-Za-z])[A-Z](?![A-Za-z])'
$flagged = 0
$ruleCount = 0

foreach ($file in $files) {
    try {
        $json = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        Write-Output "WORLDBOOK_PLACEHOLDER_FAIL parse=$($file.Name) error=$($_.Exception.Message)"
        $flagged++
        continue
    }

    $ruleId = [string]$json.id
    $ruleCount++
    $mappingSources = @()
    if ($json.textMappings) {
        $mappingSources = @($json.textMappings | ForEach-Object { [string]$_.sourceText })
    }

    $contentValues = @()
    if (-not [string]::IsNullOrWhiteSpace([string]$json.content)) {
        $contentValues += [string]$json.content
    }
    if ($json.variants) {
        foreach ($variant in $json.variants) {
            if (-not [string]::IsNullOrWhiteSpace([string]$variant.content)) {
                $contentValues += [string]$variant.content
            }
        }
    }

    foreach ($content in $contentValues) {
        if ([regex]::IsMatch($content, $chinesePlaceholder)) {
            Write-Output "WORLDBOOK_PLACEHOLDER_FAIL file=$($file.Name) rule=$ruleId text=$content"
            $flagged++
        }
        foreach ($match in [regex]::Matches($content, $singleLetter)) {
            if ($mappingSources -notcontains $match.Value) {
                Write-Output "WORLDBOOK_PLACEHOLDER_FAIL file=$($file.Name) rule=$ruleId token=$($match.Value) text=$content"
                $flagged++
            }
        }
    }

    if ($json.textMappings) {
        foreach ($mapping in $json.textMappings) {
            $kind = [string]$mapping.kind
            $target = [string]$mapping.targetId
            $isBoundOrStatus = $kind -like 'bound_*' -or $kind -like 'status|*'
            if (-not $isBoundOrStatus -and [string]::IsNullOrWhiteSpace($target)) {
                Write-Output "WORLDBOOK_PLACEHOLDER_FAIL file=$($file.Name) rule=$ruleId mapping_kind=$kind missing_target=1"
                $flagged++
            }
        }
    }
}

if ($flagged -gt 0) {
    Write-Output "WORLDBOOK_PLACEHOLDER_AUDIT files=$($files.Count) rules=$ruleCount flagged=$flagged"
    exit 1
}

Write-Output "WORLDBOOK_PLACEHOLDER_OK files=$($files.Count) rules=$ruleCount"
exit 0
