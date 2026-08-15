$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src'
$enXml = Join-Path $root 'ModuleData\Languages\awake_strings.xml'
$cnXml = Join-Path $root 'ModuleData\Languages\CNs\awake_strings-zh-HANS.xml'
if (-not (Test-Path $src)) { throw "Source directory not found: $src" }

$sourceKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
Get-ChildItem -LiteralPath $src -Filter '*.cs' -File -Recurse | ForEach-Object {
    $text = Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
    foreach ($m in [regex]::Matches($text, '\{=(?<key>[A-Za-z0-9_.-]+)\}')) { [void]$sourceKeys.Add($m.Groups['key'].Value) }
    foreach ($m in [regex]::Matches($text, 'AwakeLocalization\.Resolve\(\s*"(?<key>[A-Za-z0-9_.-]+)"')) { [void]$sourceKeys.Add($m.Groups['key'].Value) }
    foreach ($m in [regex]::Matches($text, 'GameTexts\.FindText\(\s*"(?<key>[A-Za-z0-9_.-]+)"')) { [void]$sourceKeys.Add($m.Groups['key'].Value) }
}

function Get-XmlKeys([string]$path) {
    if (-not (Test-Path $path)) { throw "Language XML not found: $path" }
    [xml]$xml = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    foreach ($node in $xml.base.strings.string) { [void]$set.Add([string]$node.id) }
    return $set
}

$enKeys = Get-XmlKeys $enXml
$cnKeys = Get-XmlKeys $cnXml

$missingEn = @($sourceKeys | Where-Object { -not $enKeys.Contains($_) } | Sort-Object)
$missingCn = @($sourceKeys | Where-Object { -not $cnKeys.Contains($_) } | Sort-Object)
$enOnly = @($enKeys | Where-Object { -not $cnKeys.Contains($_) } | Sort-Object)
$cnOnly = @($cnKeys | Where-Object { -not $enKeys.Contains($_) } | Sort-Object)

$failed = $false
if ($missingEn.Count -gt 0) { $failed = $true; Write-Output "MISSING_EN $($missingEn -join ',')" }
if ($missingCn.Count -gt 0) { $failed = $true; Write-Output "MISSING_CN $($missingCn -join ',')" }
if ($enOnly.Count -gt 0) { $failed = $true; Write-Output "EN_ONLY $($enOnly -join ',')" }
if ($cnOnly.Count -gt 0) { $failed = $true; Write-Output "CN_ONLY $($cnOnly -join ',')" }
if (-not $failed) { Write-Output "LOCALIZATION_OK source=$($sourceKeys.Count) en=$($enKeys.Count) cn=$($cnKeys.Count)" }
exit $(if ($failed) { 1 } else { 0 })
