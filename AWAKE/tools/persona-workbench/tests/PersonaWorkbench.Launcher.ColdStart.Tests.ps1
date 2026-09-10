param([Parameter(Mandatory = $true)][string]$PackagePath)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$url = "http://127.0.0.1:51337/"
$start = Join-Path $PackagePath "start-free-preview.ps1"
$stop = Join-Path $PackagePath "stop-free-preview.ps1"
try { Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 1 | Out-Null; throw "Port 51337 must be stopped before cold-start test." } catch { if ($_.Exception.Message -eq "Port 51337 must be stopped before cold-start test.") { throw } }
try {
    & $start -NoWindow | Out-Null
    $ready = $false
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        Start-Sleep -Milliseconds 250
        try { if ((Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 1).StatusCode -eq 200) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw "Packaged start script did not start the HTTP service." }
    $stdoutLog = Join-Path $PackagePath ".runtime\server.stdout.log"
    $stderrLog = Join-Path $PackagePath ".runtime\server.stderr.log"
    $startupLog = Join-Path $PackagePath ".runtime\startup.log"
    if (-not (Test-Path -LiteralPath $stdoutLog)) { throw "Startup did not create server.stdout.log." }
    if (-not (Test-Path -LiteralPath $stderrLog)) { throw "Startup did not create server.stderr.log." }
    if (-not (Test-Path -LiteralPath $startupLog)) { throw "Startup did not create startup.log." }
    if ((Get-Content -LiteralPath $startupLog -Raw) -notmatch "startup_ready") { throw "startup.log did not record startup_ready." }
    $pidFile = Join-Path $PackagePath ".runtime\persona-workbench.pid"
    if (-not (Test-Path -LiteralPath $pidFile)) { throw "Package PID file is missing." }
    $serviceId = [int](Get-Content -LiteralPath $pidFile -Raw)
    $service = Get-Process -Id $serviceId -ErrorAction Stop
    $expected = [IO.Path]::GetFullPath((Join-Path $PackagePath "PersonaWorkbench.Web.exe"))
    $actual = [IO.Path]::GetFullPath($service.MainModule.FileName)
    if ($actual -ine $expected) { throw "Unexpected service path: $actual" }
    [pscustomobject]@{ HttpStatus = 200; ServicePid = $serviceId; ServicePath = $actual }
}
finally {
    if (Test-Path -LiteralPath $stop) { & $stop | Out-Null }
}
