param([Parameter(Mandatory = $true)][string]$PackagePath)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$url = "http://127.0.0.1:51337/"
$web = Join-Path $PackagePath "PersonaWorkbench.Web.exe"
if (-not (Test-Path -LiteralPath $web)) { throw "Web executable is missing: $web" }
if (Test-Path -LiteralPath (Join-Path $PackagePath "PersonaWorkbench.Web.runtimeconfig.json")) { throw "Package still requires an external .NET runtime." }
if (Test-Path -LiteralPath (Join-Path $PackagePath "PersonaWorkbench.Web.deps.json")) { throw "Package is not the expected single-file release." }
try { Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 1 | Out-Null; throw "Port 51337 must be stopped before self-contained test." } catch { if ($_.Exception.Message -eq "Port 51337 must be stopped before self-contained test.") { throw } }
$oldDotnetRoot = $env:DOTNET_ROOT
$oldDotnetRootX64 = $env:DOTNET_ROOT_X64
$oldMultilevelLookup = $env:DOTNET_MULTILEVEL_LOOKUP
$process = $null
try {
    $env:DOTNET_ROOT = "Z:\missing-dotnet-runtime"
    $env:DOTNET_ROOT_X64 = "Z:\missing-dotnet-runtime"
    $env:DOTNET_MULTILEVEL_LOOKUP = "0"
    $process = Start-Process -FilePath $web -ArgumentList "--no-browser" -WorkingDirectory $PackagePath -PassThru -WindowStyle Hidden
    $ready = $false
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        Start-Sleep -Milliseconds 250
        if ($process.HasExited) { throw "Self-contained executable exited with code $($process.ExitCode)." }
        try { if ((Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 1).StatusCode -eq 200) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw "Self-contained executable did not become ready without a global .NET runtime." }
}
finally {
    if ($process -and -not $process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
    $env:DOTNET_ROOT = $oldDotnetRoot
    $env:DOTNET_ROOT_X64 = $oldDotnetRootX64
    $env:DOTNET_MULTILEVEL_LOOKUP = $oldMultilevelLookup
}
Write-Output "Persona Workbench self-contained runtime test: PASS"
