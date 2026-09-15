# 统一 selfClaimRules/selfClaimExamples 内半角引号 -> 全角
# 逐行, 记录当前所在数组, 用全局计数器(lives at script scope)交替开闭引号
$ErrorActionPreference="Stop"
$base="D:\AWAKE-Dev\AWAKE"
$charDir=Join-Path $base "tools\persona-workbench\characters"
$targets=@($args)

$script:cRules=0
$script:cExS=0
$script:cExD=0

$done=New-Object System.Collections.Generic.List[string]

foreach($f in Get-ChildItem $charDir -Filter "*.persona.json"){
    $name=$f.BaseName
    if($targets.Count -gt 0 -and $name -notin $targets){ continue }
    $raw=[System.IO.File]::ReadAllText($f.FullName,[System.Text.UTF8Encoding]::new($false))
    $lines=$raw -split "`n"
    $mode=0
    for($k=0;$k -lt $lines.Count;$k++){
        $line=$lines[$k]
        if($line -match '^\s*"selfClaimRules"\s*:\s*\['){ $mode=1; continue }
        if($line -match '^\s*"selfClaimExamples"\s*:\s*\['){ $mode=2; continue }
        if($mode -ne 0 -and $line -match '^\s*\]'){ $mode=0; continue }
        $new=$line
        if($mode -eq 1 -and $line.Contains('\u0027')){
            $new=[regex]::Replace($line,'\\u0027',{
                $script:cRules++
                if(($script:cRules) % 2 -eq 1){'\u201c'} else {'\u201d'}
            })
        } elseif($mode -eq 2){
            if($line.Contains('\u0027')){
                $new=[regex]::Replace($new,'\\u0027',{
                    $script:cExS++
                    if(($script:cExS) % 2 -eq 1){'\u2018'} else {'\u2019'}
                })
            }
            if($new.Contains('\"')){
                $new=[regex]::Replace($new,'\\"',{
                    $script:cExD++
                    if(($script:cExD) % 2 -eq 1){'\u201c'} else {'\u201d'}
                })
            }
        }
        if($new -ne $line){ $lines[$k]=$new }
    }
    $newFull=[string]::Join("`n",$lines)
    if($newFull -ne $raw){
        [System.IO.File]::WriteAllText($f.FullName,$newFull,[System.Text.UTF8Encoding]::new($false))
        $done.Add($name)
        Write-Host "converted: $name"
    }
}
Write-Host ("done converted="+$done.Count)