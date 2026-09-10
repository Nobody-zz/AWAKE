param([Parameter(Mandatory = $true)][string]$PackagePath)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$start = Join-Path $PackagePath "start-free-preview.ps1"
$stop = Join-Path $PackagePath "stop-free-preview.ps1"
& $start | Out-Null
$pidFile = Join-Path $PackagePath ".runtime\persona-workbench.pid"
if (-not (Test-Path -LiteralPath $pidFile)) { throw "Start script did not create a PID file." }
Remove-Item -LiteralPath $pidFile -Force
& $stop | Out-Null
if (Get-Process -Name "PersonaWorkbench.Web" -ErrorAction SilentlyContinue | Where-Object { $_.MainModule.FileName -ieq (Join-Path $PackagePath "PersonaWorkbench.Web.exe") }) { throw "Stop script left the package service running." }
Write-Output "Persona Workbench stop script tests: PASS"
