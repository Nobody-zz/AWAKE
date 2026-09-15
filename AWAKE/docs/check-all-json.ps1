# 全量 JSON 有效性校验：遍历所有 .persona.json，逐个 Parse，报告坏文件
$ErrorActionPreference = "Stop"
$dir = "D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters"
$files = Get-ChildItem $dir -Filter "*.persona.json"
$bad = New-Object System.Collections.Generic.List[string]
foreach ($f in $files) {
    try {
        $null = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        $bad.Add($f.BaseName + " :: " + $_.Exception.Message.Split("`n")[0])
    }
}
Write-Output ("checked=" + $files.Count + " broken=" + $bad.Count)
foreach ($b in $bad) { Write-Output ("BROKEN: " + $b) }
if ($bad.Count -eq 0) { Write-Output "ALL JSON VALID" }