param([Parameter(Mandatory = $true)][string]$PackagePath)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$windowsPowerShell = Join-Path $env:SystemRoot "System32\WindowsPowerShell\v1.0\powershell.exe"
if (-not (Test-Path -LiteralPath $windowsPowerShell)) { throw "Windows PowerShell 5.1 is unavailable." }
foreach ($scriptName in @("start-free-preview.ps1", "stop-free-preview.ps1")) {
    $scriptPath = Join-Path $PackagePath $scriptName
    $bytes = [IO.File]::ReadAllBytes($scriptPath)
    if ($bytes.Length -lt 3 -or $bytes[0] -ne 0xEF -or $bytes[1] -ne 0xBB -or $bytes[2] -ne 0xBF) { throw "$scriptName is not UTF-8 BOM encoded." }
    $env:PWB_PARSE_PATH = $scriptPath
    & $windowsPowerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command '$tokens=$null;$errors=$null;[System.Management.Automation.Language.Parser]::ParseFile($env:PWB_PARSE_PATH,[ref]$tokens,[ref]$errors)|Out-Null;if($errors.Count){$errors|ForEach-Object{Write-Error $_.Message};exit 1}'
    if ($LASTEXITCODE -ne 0) { throw "Windows PowerShell could not parse $scriptName." }
}
Remove-Item Env:PWB_PARSE_PATH -ErrorAction SilentlyContinue
Write-Output "Persona Workbench Windows PowerShell compatibility test: PASS"
