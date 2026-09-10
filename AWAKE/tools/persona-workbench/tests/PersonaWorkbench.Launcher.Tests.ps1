param([Parameter(Mandatory = $true)][string]$PackagePath)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$launcher = Join-Path $PackagePath "PersonaWorkbench.Launcher.exe"
if (-not (Test-Path -LiteralPath $launcher)) { throw "Launcher executable is missing: $launcher" }
$web = Join-Path $PackagePath "PersonaWorkbench.Web.exe"
if (-not (Test-Path -LiteralPath $web)) { throw "Web executable is missing: $web" }
if (Test-Path -LiteralPath (Join-Path $PackagePath "PersonaWorkbench.Web.runtimeconfig.json")) { throw "Package still requires an external .NET runtime." }
if (Test-Path -LiteralPath (Join-Path $PackagePath "PersonaWorkbench.Web.deps.json")) { throw "Package is not the expected single-file release." }
$smoke = Start-Process -FilePath $launcher -ArgumentList "--smoke-test" -WorkingDirectory $PackagePath -PassThru -Wait
if ($smoke.ExitCode -ne 0) { throw "Launcher package smoke failed: $($smoke.ExitCode)" }
$process = Start-Process -FilePath $launcher -WorkingDirectory $PackagePath -PassThru
try {
    $visible = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
        if ($process.HasExited) { throw "Launcher exited before showing its window." }
        if ($process.MainWindowHandle -ne 0 -and $process.MainWindowTitle -eq "Persona Workbench 启动器") { $visible = $true; break }
    }
    if (-not $visible) { throw "Launcher did not expose the expected visible window." }
    Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class PersonaLauncherWindowTest {
    public delegate bool EnumProc(IntPtr handle, IntPtr state);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr state);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr handle, StringBuilder text, int length);
}
"@
    $labels = New-Object Collections.Generic.List[string]
    $callback = [PersonaLauncherWindowTest+EnumProc]{
        param($handle, $state)
        $text = New-Object Text.StringBuilder 512
        [PersonaLauncherWindowTest]::GetWindowText($handle, $text, $text.Capacity) | Out-Null
        if ($text.Length -gt 0) { $labels.Add($text.ToString()) }
        return $true
    }
    [PersonaLauncherWindowTest]::EnumChildWindows($process.MainWindowHandle, $callback, [IntPtr]::Zero) | Out-Null
    if (-not ($labels -contains "启动并打开 Persona Workbench") -and -not ($labels -contains "打开 Persona Workbench")) {
        throw "Primary launch button is missing."
    }
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { throw "Launcher did not close normally." }
    }
}
Write-Output "Persona Workbench launcher tests: PASS"
