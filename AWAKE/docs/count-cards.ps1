$d = "D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters"
$cards = @(Get-ChildItem $d -Filter "*.persona.json")
Write-Output ("persona cards = " + $cards.Count)
$king = @{}
foreach ($c in $cards) {
    $parts = $c.BaseName -split '_'
    $k = $parts[$parts.Count - 1]
    if (-not $king.ContainsKey($k)) { $king[$k] = 0 }
    $king[$k]++
}
$king.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Output ($_.Name + " = " + $_.Value) }