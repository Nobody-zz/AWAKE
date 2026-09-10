Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$packagedExe = Join-Path $root "PersonaWorkbench.Web.exe"
$developmentExe = Join-Path $root "src\PersonaWorkbench.Web\bin\Release\net10.0\PersonaWorkbench.Web.exe"
$projectFile = Join-Path $root "src\PersonaWorkbench.Web\PersonaWorkbench.Web.csproj"
$exe = if (Test-Path -LiteralPath $packagedExe -PathType Leaf) { $packagedExe } elseif (Test-Path -LiteralPath $projectFile -PathType Leaf) { $developmentExe } else { $packagedExe }
$pidFile = Join-Path $root ".runtime\persona-workbench.pid"

function Test-ExpectedProcess($candidate) {
    if (-not $candidate) { return $false }
    try {
        return [IO.Path]::GetFullPath($candidate.MainModule.FileName) -ieq [IO.Path]::GetFullPath($exe)
    }
    catch {
        return $false
    }
}

function Find-ExpectedProcesses() {
    return @(Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($exe)) -ErrorAction SilentlyContinue | Where-Object { Test-ExpectedProcess $_ })
}

$process = $null
if (Test-Path -LiteralPath $pidFile) {
    $targetPid = 0
    if ([int]::TryParse((Get-Content -LiteralPath $pidFile -Raw).Trim(), [ref]$targetPid)) {
        $candidate = Get-Process -Id $targetPid -ErrorAction SilentlyContinue
        if ($candidate) {
            if (-not (Test-ExpectedProcess $candidate)) {
                throw "PID $targetPid does not belong to the Persona Workbench executable."
            }
            $process = $candidate
        }
    }
}

if (-not $process) {
    $matching = @(Find-ExpectedProcesses)
    if ($matching.Count -gt 1) { throw "Multiple Persona Workbench processes are already running." }
    if ($matching.Count -eq 1) { $process = $matching[0] }
}

if (-not $process) {
    if (Test-Path -LiteralPath $pidFile) { [IO.File]::Delete($pidFile) }
    Write-Output "Persona Workbench process has already exited."
    exit 0
}

$process.Kill()
$process.WaitForExit(5000)
if (Test-Path -LiteralPath $pidFile) { [IO.File]::Delete($pidFile) }
Write-Output "Persona Workbench stopped (PID $($process.Id))."
