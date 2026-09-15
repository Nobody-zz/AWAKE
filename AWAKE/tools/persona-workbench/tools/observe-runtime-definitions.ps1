# Direction A "数据加载观察演示"
# 以只读方式从 ModuleData 复现 PersonaDataLoader 的加载路径，输出"运行时视角"观察面板：
#   - tag registry（39 tags / 4 bundles）
#   - 47 个 definition 逐张：characterId / role / scope / status / bundles(展开 tag 数) / tags(displayName)
#   - 解析 OK / 未知 tag / 未知 bundle / 未命中 bundle / 重复 id 汇总
# 不启动游戏。尽力镜像 src/PersonaDataLoader.cs 的 StringValue/StringList/ArrayValue 语义。
#
# Usage: .\observe-runtime-definitions.ps1 [-OutDir <dir>] [-ReportPath <path>]

[CmdletBinding()]
param(
    [string]$DataDir = '',
    [string]$ReportPath = ''
)

$ErrorActionPreference = 'Stop'
$base = 'D:\AWAKE-Dev\AWAKE'
if (-not $DataDir)   { $DataDir = Join-Path $base 'ModuleData\Worldbook\persona_definitions' }
if (-not $ReportPath){ $ReportPath = Join-Path $base 'docs\OBSERVE-RUNTIME-DEFINITIONS-latest.txt' }
$defDir   = Join-Path $DataDir 'definitions'
$regPath  = Join-Path $DataDir 'tag_registry.json'

$lines = New-Object System.Collections.ArrayList
function Line([string]$s = '') { $null = $lines.Add($s); Write-Output $s }

# --- helpers mirroring PersonaDataLoader ---
function Get-Str($obj, [string[]]$names) {
    foreach ($n in $names) { $t = $obj.$n; if ($null -ne $t -and $t.GetType().Name -ne 'Object[]') { return [string]$t } }
    return $null
}
function Get-StrArray($obj, [string[]]$names) {
    $out = New-Object System.Collections.ArrayList
    foreach ($n in $names) {
        $a = $obj.$n
        if ($a -is [System.Collections.IList]) {
            foreach ($it in $a) { if ($null -ne $it -and (-not [string]::IsNullOrWhiteSpace([string]$it))) { $null = $out.Add([string]$it) } }
            break
        }
    }
    return ,$out.ToArray()
}
function Get-ObjArray($obj, [string[]]$names) {
    foreach ($n in $names) { $a = $obj.$n; if ($a -is [System.Collections.IList]) { return $a } }
    return $null
}

# --- registry ---
$reg = Get-Content -LiteralPath $regPath -Raw -Encoding UTF8 | ConvertFrom-Json
$tagDisplay = @{}
foreach ($t in @($reg.tags)) { $tagDisplay[[string]$t.id] = [string]$t.displayName }
$bundleTags = @{}
foreach ($b in @($reg.bundles)) { $bundleTags[[string]$b.id] = [string[]](Get-StrArray $b @('tags')) }

$regBytes = [System.IO.File]::ReadAllBytes($regPath)
$regHash = [System.BitConverter]::ToString([System.Security.Cryptography.SHA256]::Create().ComputeHash($regBytes)).Replace('-','')

Line ''
Line ('========== AWAKE persona 运行时观察面板 ==========')
Line ('数据源: ' + $DataDir)
Line ('tag_registry SHA256: ' + $regHash)
Line ('registry.tags = ' + @($reg.tags).Count + '   registry.bundles = ' + @($reg.bundles).Count)
Line ''
Line ('== bundles (id -> 数量 + 标签) ==')
foreach ($b in @($reg.bundles)) {
    $bt = [string[]](Get-StrArray $b @('tags'))
    $disp = (($bt | ForEach-Object { if ($tagDisplay.ContainsKey($_)) { $tagDisplay[$_] } else { $_ } }) -join ', ')
    Line ('  ' + [string]$b.id + ' (' + @($bt).Count + '): ' + $disp)
}
Line ''
Line ('== definitions ==')
$files = Get-ChildItem -LiteralPath $defDir -Filter '*.json' -File | Sort-Object Name
Line ("文件数 = " + $files.Count)
Line ''

$charCount = 0; $roleCount = 0
$idsSeen = @{}
$unknownTags = @{}
$unknownBundles = @{}
$unbundled = @()
$dupeIds = @()
$heroSet = @{}
$totalTagUses = 0

foreach ($f in $files) {
    $obj = $null
    try { $obj = Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json } catch { Line ('  [PARSE-FAIL] ' + $f.Name + ' :: ' + $_.Exception.Message); continue }
    $id = Get-Str $obj @('id','Id'); if (-not $id) { $id = $f.BaseName }
    if ($idsSeen.ContainsKey($id)) { $dupeIds += $id } else { $idsSeen[$id] = $true }

    $charId = Get-Str $obj @('characterId','CharacterId'); if (-not $charId) { $charId = '' }
    $role   = Get-Str $obj @('role','Role'); if (-not $role) { $role = '' }
    $scope  = Get-Str $obj @('scope','Scope'); if (-not $scope) { $scope = 'character' }
    $status = Get-Str $obj @('status','Status'); if (-not $status) { $status = 'draft' }
    if ($scope -eq 'character') { $charCount++ } else { $roleCount++ }
    if ($charId) { $heroSet[$charId] = $true }

    $tags = Get-ObjArray $obj @('tags','Tags')
    $tagUses = @()
    $tagIds = @()
    foreach ($tu in @($tags)) { if ($tu -is [System.Management.Automation.PSCustomObject]) { $tid = Get-Str $tu @('id','Id'); if ($tid) { $tagIds += $tid } } }
    foreach ($tid in $tagIds) { if (-not $tagDisplay.ContainsKey($tid)) { $unknownTags[$tid] = $true } }
    $totalTagUses += @($tagIds).Count

    $bundles = [string[]](Get-StrArray $obj @('bundles','Bundles'))
    $bundleInfo = @()
    foreach ($bid in $bundles) {
        if (-not $bundleTags.ContainsKey($bid)) { $unknownBundles[$bid] = $true; $bundleInfo += ($bid + '(?!registry)') }
        else { $bundleInfo += ($bid + '(' + @($bundleTags[$bid]).Count + ')') }
    }
    if ($scope -eq 'character' -and @($bundles).Count -eq 0) {
        # character definitions SHOULD carry a bundle under Direction A
        $unbundled += $f.Name
    }

    $disp = ($tagIds | ForEach-Object { if ($tagDisplay.ContainsKey($_)) { $tagDisplay[$_] } else { $_ } }) -join ', '
    $flags = ''
    if (@($bundleInfo).Count -gt 0) { $flags = '  bundles=[' + ($bundleInfo -join ', ') + ']' }
    Line ('  [' + $scope + '] ' + $f.Name + '  id=' + $id)
    Line ('      characterId=' + $charId + '  role=' + $role + '  status=' + $status + $flags)
    Line ('      tags(' + @($tagIds).Count + '): ' + $disp)
    Line ''
}

Line ('== 汇总 ==')
Line ('definition 总数   = ' + $files.Count)
Line ('  character 级    = ' + $charCount)
Line ('  role/其它级     = ' + $roleCount)
Line ('去重 characterId(heroId) 覆盖 = ' + $heroSet.Count)
Line ('tag 引用总量      = ' + $totalTagUses)
Line ('未知 tag (registry 无) = ' + $unknownTags.Count + ($(if ($unknownTags.Count){': ' + (($unknownTags.Keys -join ','))} else {''})))
Line ('未知 bundle = ' + $unknownBundles.Count + ($(if ($unknownBundles.Count){': ' + (($unknownBundles.Keys -join ','))} else {''})))
Line ('重复 definition id = ' + $dupeIds.Count + ($(if ($dupeIds.Count){': ' + (($dupeIds -join ','))} else {''})))
Line ('character 级未命中任何 bundle = ' + $unbundled.Count + ($(if ($unbundled.Count){': ' + (($unbundled -join ','))} else {''})))

# write report
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($ReportPath, ($lines -join "`r`n"), $utf8NoBom)
Write-Output ''
Write-Output ('REPORT=' + $ReportPath)