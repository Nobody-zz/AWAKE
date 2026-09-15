# 检查 selfClaimRules / selfClaimExamples 两个数组字段中半角引号的成对情况
$ErrorActionPreference = "Stop"
$base="D:\AWAKE-Dev\AWAKE"
$charDir=Join-Path $base "tools\persona-workbench\characters"
$lines=New-Object System.Collections.Generic.List[string]

foreach($f in Get-ChildItem $charDir -Filter "*.persona.json"){
    $json=Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $name=$f.BaseName
    foreach($field in @("selfClaimRules","selfClaimExamples")){
        $arr=$json.($field)
        if(-not $arr){ continue }
        foreach($item in $arr){
            $single=([regex]::Matches($item,"'")).Count
            $double=([regex]::Matches($item,'"')).Count
            if($single -eq 0 -and $double -eq 0){ continue }
            $flag=""
            if(($single % 2) -ne 0){ $flag+="单引号奇数!" }
            if(($double % 2) -ne 0){ $flag+="双引号奇数!" }
            $s=$item
            if($s.Length -gt 30){ $s=$s.Substring(0,30)+"..." }
            $lines.Add(("[$name][$field] s=$single d=$double $flag :: $s"))
        }
    }
}
$out=Join-Path $base "docs\lint-quote-check.txt"
[string]::Join("`n",$lines.ToArray()) | Set-Content $out -Encoding UTF8
Write-Host ("rows="+$lines.Count+" -> "+$out)