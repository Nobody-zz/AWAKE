# 构建 character-names 全量中英对照表（XmlDocument 版）
$ErrorActionPreference = "Stop"
$game = "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules"
$lordsXml = Join-Path $game "SandBox\ModuleData\lords.xml"
$lordsCn  = Join-Path $game "SandBox\ModuleData\Languages\CNs\std_lords_xml-zho-CN.xml"
$out      = "D:\AWAKE-Dev\AWAKE\docs\mappings\character-names-zh-en.tsv"

$doc = New-Object System.Xml.XmlDocument
$doc.Load($lordsXml)
$nodes = $doc.SelectNodes("//NPCCharacter")

$cnMap = @{}
foreach($line in (Get-Content $lordsCn -Encoding UTF8)){
    $m = [regex]::Match($line, '<string\s+id="([^"]+)"\s+text="([^"]*)"')
    if($m.Success -and -not $cnMap.ContainsKey($m.Groups[1].Value)){
        $cnMap[$m.Groups[1].Value] = $m.Groups[2].Value
    }
}

$placeholder = "[no-fixed-zh: generated]"
$rows = New-Object System.Collections.Generic.List[string]
$rows.Add("hero_id	english	chinese	source	culture	sex	occupation	name_key	has_latin_in_cn	same_as_english")
$missingCn = 0; $total = 0
foreach($n in $nodes){
    $id = $n.GetAttribute("id")
    if(-not $id){ continue }
    $nm = $n.GetAttribute("name")
    $key = ""; $en = ""
    if($nm -match '^\{=([^}]+)\}(.*)$'){ $key = $Matches[1]; $en = $Matches[2].Trim() }
    elseif($nm){ $en = $nm.Trim() }
    if(-not $en){ continue }
    $total++
    if($key -and $cnMap.ContainsKey($key)){
        $cn = $cnMap[$key] -replace "[`t`r`n]", " "
        $src = "SandBox/std_lords"
    } else {
        $cn = $placeholder; $missingCn++; $src = "(no-cn-override: generated)"
    }
    if($cn -ne $placeholder){ $cn = $cn -replace "\s+", " " }
    $culture = $n.GetAttribute("culture") -replace "^Culture\.", ""
    $sex = if($n.GetAttribute("is_female") -eq "true"){ "F" } else { "M" }
    $occ = $n.GetAttribute("occupation"); if(-not $occ){ $occ = "Lord" }
    $haslatin = if($cn -match "[A-Za-z]"){ "True" } else { "False" }
    $same = if($cn -eq $en){ "True" } else { "False" }
    $row = "{0}`t{1}`t{2}`t{3}`t{4}`t{5}`t{6}`t{7}`t{8}`t{9}" -f $id,$en,$cn,$src,$culture,$sex,$occ,$key,$haslatin,$same
    $rows.Add($row)
}
$rows | Set-Content -Path $out -Encoding UTF8
Write-Host "TOTAL=$total MISSING_CN=$missingCn OUT=$out"