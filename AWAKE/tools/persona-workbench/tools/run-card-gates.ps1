# run-card-gates.ps1 - 角色卡七道门禁的一键复检入口
#
# 位置：W9「完成定义」的批量执行器。改完一批卡后跑这一个命令，即知有没有退步。
# 前四道门禁均为全量扫描（76 卡纯文本解析，秒级），无需指定卡。
#
# 用法：
#   .\run-card-gates.ps1                 # 四道静态门禁 + 编译
#   .\run-card-gates.ps1 -SkipCompile    # 只要四道静态门禁（快）
#
# 退出码：0 = 全绿；1 = 有门禁红。
#
# 注意：第 6 道（8 场景复读自检）与第 7 道（运行时物化）不在本脚本内——
#       前者要本机 Ollama、后者要真实编译链，属重型验证，另按 W9 单独跑。

[CmdletBinding()]
param(
    [switch]$SkipCompile
)

$ErrorActionPreference = 'Continue'
$base  = 'D:\AWAKE-Dev\AWAKE'
$tools = Join-Path $base 'tools\persona-workbench\tools'

$gates = @(
    [pscustomobject]@{ Name = '1-schema';        Path = (Join-Path $tools 'audit-character-schema.ps1') },
    [pscustomobject]@{ Name = '2-affiliations';  Path = (Join-Path $tools 'audit-character-affiliations.ps1') },
    [pscustomobject]@{ Name = '3-text';          Path = (Join-Path $tools 'audit-character-text.ps1') },
    [pscustomobject]@{ Name = '4-enhancement';   Path = (Join-Path $tools 'audit-character-enhancement.ps1') }
)
if (-not $SkipCompile) {
    $gates += [pscustomobject]@{ Name = '5-compile'; Path = (Join-Path $tools 'compile-verify.ps1') }
}

$results = @()
foreach ($g in $gates) {
    if (-not (Test-Path -LiteralPath $g.Path)) {
        $results += [pscustomobject]@{ Gate = $g.Name; Exit = -1; Pass = $false; Seconds = 0; Note = 'missing script' }
        continue
    }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & powershell -NoProfile -ExecutionPolicy Bypass -File $g.Path *>&1 | Out-Null
    $code = $LASTEXITCODE
    $sw.Stop()
    $results += [pscustomobject]@{
        Gate    = $g.Name
        Exit    = $code
        Pass    = ($code -eq 0)
        Seconds = [Math]::Round($sw.Elapsed.TotalSeconds, 1)
        Note    = ''
    }
}

$allPass = (@($results | Where-Object { -not $_.Pass }).Count -eq 0)
Write-Host '=== 角色卡门禁复检（W9） ==='
foreach ($r in $results) {
    Write-Host ("{0,-16} {1,-6} {2,6}s  {3}" -f $r.Gate, $(if ($r.Pass) { 'PASS' } else { 'FAIL' }), $r.Seconds, $r.Note)
}
Write-Host ("OVERALL: " + $(if ($allPass) { 'PASS' } else { 'FAIL' }))
Write-Host '（第6道 8场景自检 / 第7道 物化 需另跑；见 AUTHORING-GUIDELINES W9）'
if (-not $allPass) { exit 1 }
