$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src'
$gui = Join-Path $root 'GUI'
$failed = $false
$bannedPatterns = @(
    '(?i)\balice\b',
    '(?i)\banimusforge\b',
    '(?i)\baiinfluence\b',
    '(?i)\blovehate\b',
    '(?i)\bslaanesh\b',
    '(?i)\bgoddess\b'
)

function Test-Banned([string]$text) {
    foreach ($pattern in $bannedPatterns) {
        if ([regex]::IsMatch($text, $pattern)) { return $true }
    }
    return $false
}

$files = @()
if (Test-Path $src) { $files += Get-ChildItem -LiteralPath $src -Filter '*.cs' -Recurse -File }
if (Test-Path $gui) { $files += Get-ChildItem -LiteralPath $gui -Recurse -File }

foreach ($file in $files) {
    $name = $file.Name
    if (Test-Banned $name) {
        Write-Output "ASSET_BOUNDARY_FAIL $($file.FullName) (filename)"
        $failed = $true
        continue
    }
    $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    if (Test-Banned $text) {
        Write-Output "ASSET_BOUNDARY_FAIL $($file.FullName)"
        $failed = $true
    }
}

if ($failed) {
    Write-Output 'ASSET_BOUNDARY_LINT_FAILED'
    exit 1
}
Write-Output "ASSET_BOUNDARY_OK files=$($files.Count)"
exit 0
