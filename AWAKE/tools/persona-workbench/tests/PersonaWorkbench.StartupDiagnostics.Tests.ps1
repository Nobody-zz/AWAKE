param([Parameter(Mandatory = $true)][string]$SourceRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$temp = Join-Path ([IO.Path]::GetTempPath()) ("pwb-startup-log-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $SourceRoot "start-free-preview.ps1") -Destination $temp
    $failed = $false
    try { & (Join-Path $temp "start-free-preview.ps1") -NoWindow | Out-Null } catch { $failed = $true }
    if (-not $failed) { throw "Missing executable scenario did not fail." }
    $stderr = Join-Path $temp ".runtime\server.stderr.log"
    $startup = Join-Path $temp ".runtime\startup.log"
    if (-not (Test-Path -LiteralPath $stderr)) { throw "Startup failure did not create server.stderr.log." }
    if (-not (Test-Path -LiteralPath $startup)) { throw "Startup failure did not create startup.log." }
    $content = Get-Content -LiteralPath $stderr -Raw
    $startupContent = Get-Content -LiteralPath $startup -Raw
    if ([string]::IsNullOrWhiteSpace($content) -or $content -notmatch "startup_error" -or $content -notmatch "PersonaWorkbench.Web.exe") {
        throw "Startup failure log does not contain the actionable missing-executable error."
    }
    if ($startupContent -notmatch "startup_begin" -or $startupContent -notmatch "startup_error") { throw "startup.log did not record the complete failed startup lifecycle." }
}
finally {
    if (Test-Path -LiteralPath $temp) { [IO.Directory]::Delete($temp, $true) }
}
Write-Output "Persona Workbench startup diagnostics test: PASS"
