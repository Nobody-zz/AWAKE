[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Command,
    [Parameter(Mandatory = $true)][int]$ExpectExit,
    [string]$ExpectPattern,
    [string]$ForbidPattern,
    [string]$Label = 'gate'
)

# ASCII-only on purpose: no BOM here, and non-ASCII bytes would make
# Windows PowerShell 5.1 misparse this file. Keep it ASCII.

$ErrorActionPreference = 'Continue'

$outFile = Join-Path $env:TEMP ("assert-gate-" + [guid]::NewGuid().ToString('N') + ".txt")

# Run in a CHILD powershell so we get a clean exit code, and Tee to a file so
# output survives a throw inside the command (a plain assignment would be lost).
$inner = $Command + " *>&1 | Tee-Object -FilePath '" + $outFile + "'"
& powershell -NoProfile -ExecutionPolicy Bypass -Command $inner | Out-Null
$actual = $LASTEXITCODE
if ($null -eq $actual) { $actual = 0 }

$text = ''
if (Test-Path -LiteralPath $outFile) { $text = [IO.File]::ReadAllText($outFile) }

$problems = @()
if ($actual -ne $ExpectExit) {
    $problems += ("exit code: expected {0}, got {1}" -f $ExpectExit, $actual)
}
if (-not [string]::IsNullOrWhiteSpace($ExpectPattern)) {
    if ($text -notmatch [regex]::Escape($ExpectPattern)) {
        $problems += ("missing expected pattern: " + $ExpectPattern)
    }
}
if (-not [string]::IsNullOrWhiteSpace($ForbidPattern)) {
    if ($text -match [regex]::Escape($ForbidPattern)) {
        $problems += ("found forbidden pattern: " + $ForbidPattern)
    }
}

Write-Output ("--- assert-gate [{0}] ---" -f $Label)
Write-Output ("command      : " + $Command)
Write-Output ("expected exit: " + $ExpectExit)
Write-Output ("actual exit  : " + $actual)
if ($ExpectPattern) { Write-Output ("expect patt  : " + $ExpectPattern + "  -> " + $(if ($text -match [regex]::Escape($ExpectPattern)) { 'found' } else { 'NOT found' })) }
if ($ForbidPattern) { Write-Output ("forbid patt  : " + $ForbidPattern + "  -> " + $(if ($text -match [regex]::Escape($ForbidPattern)) { 'FOUND (bad)' } else { 'absent (ok)' })) }
Write-Output ("output file  : " + $outFile)

if ($problems.Count -gt 0) {
    Write-Output ""
    Write-Output "ASSERT_FAIL"
    foreach ($p in $problems) { Write-Output ("  - " + $p) }
    Write-Output ""
    Write-Output "--- last 15 lines of output ---"
    $lines = @($text -split "`r?`n" | Where-Object { $_.Trim() -ne '' })
    $tail = if ($lines.Count -gt 15) { $lines[($lines.Count - 15)..($lines.Count - 1)] } else { $lines }
    foreach ($l in $tail) { Write-Output ("  " + $l) }
    exit 1
}

Write-Output ""
Write-Output "ASSERT_PASS"
exit 0
