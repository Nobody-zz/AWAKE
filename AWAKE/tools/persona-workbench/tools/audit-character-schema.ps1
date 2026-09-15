# Character v1 schema lint: loop over characters/*.persona.json, run contract rules.
$ErrorActionPreference = "Stop"
$base    = "D:\AWAKE-Dev\AWAKE"
$charDir = Join-Path $base "tools\persona-workbench\characters"
$out     = Join-Path $base "docs\AUDIT-CHARACTER-SCHEMA-20260911.md"

$topFields = @(
  "schemaVersion","id","displayName","origins","core","identityFacts","summary",
  "sourceDescription","publicDescription","privateDescription","contradictionDescription",
  "selfClaimRules","realSelfBehaviors","selfClaimExamples","tensionAxes","tags",
  "facetStrengths","traitProfile","expressionProfile","behaviorProfile","reactionProfile",
  "commitmentProfile","status","sourcePackId","templateVersion"
)
$numAxes = @("traitProfile","expressionProfile","behaviorProfile")
$reactionStr = @("sensitiveConditions","conditionalResponses")
$commitStr   = @("priorityOrder","protectedValues","applicableScope","exceptionCost","breachResponse")
$statusEnum = @("draft","translated","review","approved")
$kingEnum   = @("empire","empire_w","empire_s","sturgia","aserai","vlandia","battania","khuzait")

function Test-IntRange($obj, $min, $max, $strKeys, [ref]$errs){
    if($null -eq $obj){ return }
    foreach($k in $obj.PSObject.Properties.Name){
        if($strKeys -contains $k){ continue }
        $v = $obj.$k
        if($v -isnot [int] -and -not ($v -is [long])){ $errs.Value.Add("[$k] should be integer, got $($v.GetType().Name)"); continue }
        $iv = [int]$v
        if($iv -lt $min -or $iv -gt $max){ $errs.Value.Add("[$k]=$iv out of range [$min,$max]") }
    }
}

$md = New-Object System.Collections.Generic.List[string]
$md.Add("# PERSONA-SCHEMA LINT - character.v1 契约校验（2026-09-11）")
$md.Add("")
$md.Add("| 卡 | schemaVersion | id有值/合法 | required | origins | 数值轴 | 枚举 | 多余顶层键 | 结果 |")
$md.Add("|---|---|---|---|---|---|---|---|---|")
$cards = Get-ChildItem $charDir -Filter "*.persona.json"
$allPass = $true
foreach($f in $cards){
    $errs = New-Object System.Collections.Generic.List[string]
    $name = $f.Name -replace '\.persona\.json$',''
    $json = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json

    if($json.schemaVersion -ne "persona-workbench.character.v1"){ $errs.Add("schemaVersion=$($json.schemaVersion)") }

    # required
    $req = @("schemaVersion","id","displayName","core","identityFacts","summary","sourceDescription","status","sourcePackId","templateVersion")
    foreach($r in $req){ if($null -eq $json.$r){ $errs.Add("missing required: $r") } }

    # id
    $idOk = $false
    if($json.id){ if($json.id -match "^calradia\.[a-z0-9_]+(\.[a-z0-9_]+){1,2}$"){ $idOk = $true } else { $errs.Add("id pattern: $($json.id)") } }

    # origins (sidecar)
    $origOk = $false
    $sidePath = Join-Path $charDir ($name + ".origins.json")
    $side = $null
    if(Test-Path -LiteralPath $sidePath -PathType Leaf){ $side = Get-Content -LiteralPath $sidePath -Raw -Encoding UTF8 | ConvertFrom-Json }
    if($null -eq $side){ $errs.Add("origins sidecar missing: $($name).origins.json") }
    else {
        if($side.heroId){ if($side.heroId -match "^(lord|dead_lord)_[0-9A-Za-z_]+$"){ $origOk=$true } else { $errs.Add("origins.heroId pattern: $($side.heroId)") } }
        else { $errs.Add("origins.heroId missing") }
        if($null -ne $side.kingdomId -and -not ($kingEnum -contains $side.kingdomId)){ $errs.Add("origins.kingdomId=$($side.kingdomId) not in enum") }
    }

    # 作者侧草稿（降级为可选，不入门禁）：范围越界/缺字段仅软建议
    $warns = New-Object System.Collections.Generic.List[string]
    foreach($ax in $numAxes){ if($json.$ax){ Test-IntRange $json.$ax -3 3 @() ([ref]$warns) } }
    if($json.tensionAxes){
        foreach($axKey in @('hardLine','negotiable','breachSwitch')){
            $v = [string]$json.tensionAxes.$axKey
            if([string]::IsNullOrWhiteSpace($v)){ $warns.Add("tensionAxes.$axKey 空") }
        }
    }
    if($json.reactionProfile){ Test-IntRange $json.reactionProfile -3 3 $reactionStr ([ref]$warns) }
    if($json.commitmentProfile){ Test-IntRange $json.commitmentProfile -3 3 $commitStr ([ref]$warns) }
    if($json.facetStrengths){ Test-IntRange $json.facetStrengths 0 5 @() ([ref]$warns) }

    # enums
    if($json.status -and -not ($statusEnum -contains $json.status)){ $errs.Add("status=$($json.status) not in enum") }

    # array-of-string fields dangling
    foreach($af in @("selfClaimRules","realSelfBehaviors","selfClaimExamples","tags")){
        if($json.$af -is [array]){ foreach($e in $json.$af){ if($e -isnot [string]){ $errs.Add("$af item not string") } } }
    }

    # extra top-level keys
    $extra = @()
    foreach($k in $json.PSObject.Properties.Name){ if($topFields -notcontains $k){ $extra += $k } }

    $nIssues = $errs.Count; if($extra.Count -gt 0){ $nIssues++ }
    if($nIssues -gt 0){ $allPass = $false }
    $res = if($nIssues -eq 0){ "PASS" } else { "FAIL($nIssues)" }
    $v = [string]::Join(";", $errs.ToArray())
    if($extra.Count){ $v += "  EXTRA: " + [string]::Join(",", $extra) }
    $wv = [string]::Join(";", $warns.ToArray())
    $md.Add(("| " + $name + " | " + $json.schemaVersion + " | " + $idOk + " | " + ($null -ne $json.core) + " | " + $origOk + " | issues=" + $errs.Count + " | " + $json.status + " | " + $extra.Count + " | " + $res + " |"))
    if($v){ $md.Add(("  - " + $v)) }
    if($wv){ $md.Add(("  - WARN: " + $wv)) }
}
$md.Add("")
$md.Add(("## 汇总: 卡数=" + $cards.Count + "  全部通过=" + $allPass))
$text = [string]::Join("`n", $md.ToArray())
[System.IO.File]::WriteAllText($out, $text, (New-Object System.Text.UTF8Encoding($true)))
Write-Host ("DONE cards=" + $cards.Count + " allPass=" + $allPass)
Write-Host ("report=" + $out)
if(-not $allPass){ exit 1 }