Option Explicit

Dim shell, fileSystem, scriptDirectory, executablePath, launcherPath, pwshPath, command
Set shell = CreateObject("WScript.Shell")
Set fileSystem = CreateObject("Scripting.FileSystemObject")

scriptDirectory = fileSystem.GetParentFolderName(WScript.ScriptFullName)
executablePath = fileSystem.BuildPath(scriptDirectory, "PersonaWorkbench.Launcher.exe")
If fileSystem.FileExists(executablePath) Then
    shell.Run Chr(34) & executablePath & Chr(34), 1, False
    WScript.Quit 0
End If

launcherPath = fileSystem.BuildPath(scriptDirectory, "start-free-preview.ps1")
pwshPath = shell.ExpandEnvironmentStrings("%ProgramFiles%\PowerShell\7\pwsh.exe")
If Not fileSystem.FileExists(pwshPath) Then
    pwshPath = shell.ExpandEnvironmentStrings("%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe")
End If

command = Chr(34) & pwshPath & Chr(34) & _
    " -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " & _
    Chr(34) & launcherPath & Chr(34)
shell.Run command, 0, False