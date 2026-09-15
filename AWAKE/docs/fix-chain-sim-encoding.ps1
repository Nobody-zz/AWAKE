$ErrorActionPreference = "Stop"
$p = "D:\AWAKE-Dev\AWAKE\tools\worldbook-runtime-sim\ai_chain_sim.ps1"
$curl = [System.Text.UTF8Encoding]::new($false)
$utf8Bom = [System.Text.UTF8Encoding]::new($true)
$text = [System.IO.File]::ReadAllText($p, $curl)

$i = 0
foreach ($ln in ($text -split "`r?`n")) {
    $i++
    if ($ln -match 'pwsh') { Write-Output ("L" + $i + ": " + $ln.Trim()) }
}

$newText = $text -replace '& pwsh\b', '& powershell'
$changed = ($newText -ne $text)
[System.IO.File]::WriteAllText($p, $newText, $utf8Bom)
Write-Output ("lenBEFORE=" + $text.Length + " lenAFTER=" + $newText.Length + " changed=" + $changed + " BOMwritten=True")