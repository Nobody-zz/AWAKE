param([Parameter(Mandatory = $true)][string]$PackagePath)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$url = "http://127.0.0.1:51337/"
try { Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 1 | Out-Null; throw "Port 51337 must be stopped before launcher button test." } catch { if ($_.Exception.Message -eq "Port 51337 must be stopped before launcher button test.") { throw } }
$launcherPath = Join-Path $PackagePath "PersonaWorkbench.Launcher.exe"
$launcher = Start-Process -FilePath $launcherPath -WorkingDirectory $PackagePath -PassThru
try {
    for ($attempt = 0; $attempt -lt 150; $attempt++) { Start-Sleep -Milliseconds 100; $launcher.Refresh(); if ($launcher.HasExited) { throw "Launcher exited before showing its window." }; if ($launcher.MainWindowHandle -ne 0) { break } }
    if ($launcher.MainWindowHandle -eq 0) { throw "Launcher window is not visible." }
    Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class PersonaLauncherButtonStartTest {
    public delegate bool EnumProc(IntPtr handle, IntPtr state);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr state);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr handle, StringBuilder text, int length);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
}
"@
    $script:button = [IntPtr]::Zero
    $callback = [PersonaLauncherButtonStartTest+EnumProc]{
        param($handle, $state)
        $text = New-Object Text.StringBuilder 512
        [PersonaLauncherButtonStartTest]::GetWindowText($handle, $text, $text.Capacity) | Out-Null
        if ($text.ToString() -eq "启动并打开 Persona Workbench") { $script:button = $handle; return $false }
        return $true
    }
    [PersonaLauncherButtonStartTest]::EnumChildWindows($launcher.MainWindowHandle, $callback, [IntPtr]::Zero) | Out-Null
    if ($script:button -eq [IntPtr]::Zero) { throw "Primary launch button was not found." }
    [PersonaLauncherButtonStartTest]::SendMessage($script:button, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    $ready = $false
    for ($attempt = 0; $attempt -lt 150; $attempt++) {
        Start-Sleep -Milliseconds 200
        try { if ((Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 1).StatusCode -eq 200) { $ready = $true; break } } catch {}
    }
    if (-not $ready) { throw "Launcher button did not start the HTTP service." }
    Start-Sleep -Milliseconds 500
    $launcher.Refresh()
    if ($launcher.HasExited -or -not $launcher.Responding) { throw "Launcher became unresponsive after starting the service." }
}
finally {
    if (-not $launcher.HasExited) { $launcher.CloseMainWindow() | Out-Null; $launcher.WaitForExit(5000) | Out-Null }
    & (Join-Path $PackagePath "stop-free-preview.ps1") | Out-Null
}
Write-Output "Persona Workbench launcher button start test: PASS"
