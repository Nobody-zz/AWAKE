# 精准定位文字审计问题：把每个问题映射回具体正文字段，区分叙述性违规与合法锚定
$ErrorActionPreference = "Stop"
$base    = "D:\AWAKE-Dev\AWAKE"
$charDir = Join-Path $base "tools\persona-workbench\characters"

$lines = New-Object System.Collections.Generic.List[string]
$narrFields = @("core","summary","publicDescription","privateDescription","contradictionDescription","selfClaimRules","selfClaimExamples","realSelfBehaviors")
$idFields   = @("identityFacts")

$latinRe = [regex]::new('[A-Za-z]{2,}')
$halfRe  = [regex]::new('[\u0021-\u002F\u003A-\u0040\u005B-\u0060\u007B-\u007E]')
$hardRe  = [regex]::new("[A-Za-z]{2,}|[\u0021-\u002F\u003A-\u0040\u005B-\u0060\u007B-\u007E]")
$wl = @('Bannerlord','AWAKE','v1','XML')

foreach($f in Get-ChildItem $charDir -Filter "*.persona.json"){
    $json = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $name = $f.BaseName

    foreach($field in ($narrFields + $idFields)){
        $val = $json.($field)
        if(-not $val){ continue }

        $latList = @( $latinRe.Matches($val) | ForEach-Object { $_.Value } | Where-Object { $_ -notin $wl } | Select-Object -Unique )
        $half = $halfRe.Match($val)

        if($latList.Count -eq 0 -and -not $half.Success){ continue }

        $isId = ($idFields -contains $field)
        $kind = "叙述字段"
        if($isId){ $kind = "identityFacts(含锚定=OK)" }

        # 抽一段含问题的行作为样本
        $snip = ""
        foreach($seg in ($val -split "`n")){
            if($hardRe.IsMatch($seg)){
                $s = $seg.Trim()
                if($s.Length -gt 44){ $s = $s.Substring(0,44) + "…" }
                $snip = " ｜《" + $s + "》"
                break
            }
        }

        $latS = ""
        if($latList.Count){ $latS = "拉丁:" + (($latList | Select-Object -First 4) -join "/") }
        $halfS = ""
        if($half.Success){ $halfS = "半角:[" + $half.Value + "]" }

        $lines.Add(("[$kind][$name] $field :: $latS $halfS$snip"))
    }
}

$out = Join-Path $base "docs\lint-precise-diagnose.txt"
[string]::Join("`n",$lines.ToArray()) | Set-Content $out -Encoding UTF8
Write-Host ("diagnosed=" + $lines.Count + " -> " + $out)