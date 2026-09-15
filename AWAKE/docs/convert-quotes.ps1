# 批量把 selfClaimRules / selfClaimExamples 内的成对半角引号转成全角引号
# 处理两类：裸单引号 ' 与 转义双引号 \"（均只出现在字段内容中且成对）
$ErrorActionPreference = "Stop"
$base="D:\AWAKE-Dev\AWAKE"
$charDir=Join-Path $base "tools\persona-workbench\characters"
$targets = @($args)

function Convert-Quotes($text){
    # 仅匹配 "字段: [..." 区域里的裸单引号与转义双引号。
    # 用逐字符扫描区分 JSON 结构引号 vs 内容引号更稳妥。
    $out = New-Object System.Text.StringBuilder
    $inStr=$false; $esc=$false; $sq=$false; $dbl=$false
    foreach($ch in $text.ToCharArray()){
        $c=[string]$ch
        if($esc){ [void]$out.Append($c); $esc=$false; continue }
        if($c -eq '\'){ [void]$out.Append($c); $esc=$true; continue }
        if($c -eq '"'){
            if($inStr){ $dbl=(-not $dbl); if($dbl){[void]$out.Append('“')} else {[void]$out.Append('”')} }
            else { [void]$out.Append($c); $inStr=$true }
            continue
        }
        if($c -eq "'" -and $inStr -and -not $esc){
            $sq=(-not $sq); if($sq){[void]$out.Append('“')} else {[void]$out.Append('”')}
            continue
        }
        [void]$out.Append($c)
    }
    return $out.ToString()
}

$count=0
foreach($f in Get-ChildItem $charDir -Filter "*.persona.json"){
    $name=$f.BaseName
    if($targets.Count -gt 0 -and $name -notin $targets){ continue }
    $raw=[System.IO.File]::ReadAllText($f.FullName,[System.Text.UTF8Encoding]::new($false))
    $new=Convert-Quotes $raw
    if($new -ne $raw){
        [System.IO.File]::WriteAllText($f.FullName,$new,[System.Text.UTF8Encoding]::new($false))
        $count++
        Write-Host "converted: $name"
    }
}
Write-Host "done converted=$count"