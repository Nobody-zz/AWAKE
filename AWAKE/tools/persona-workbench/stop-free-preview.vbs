Option Explicit
Dim shell, fso, root, script
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
root = fso.GetParentFolderName(WScript.ScriptFullName)
script = root & "\stop-free-preview.ps1"
If fso.FileExists(script) Then
    shell.Run "powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File """ & script & """", 0, True
End If
