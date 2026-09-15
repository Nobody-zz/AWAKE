# BannerlordSage 本地数据源 — person-workbench 取证入口
# 只读查询 BannerlordSage 建立的 bannerlord.db(SQLite)。
# 用法:  .\bannersage-query.ps1 <kind> <arg>   (kind: hero/clan/kingdom/culture/settlement/localize/search)
# DB 路径可用环境变量 BANNERSAGE_DB 覆盖，默认指向本机 BannerlordSage 库。
$ErrorActionPreference = 'Stop'

$bun = (Get-Command bun -ErrorAction SilentlyContinue).Source
if (-not $bun) {
    $cand = Join-Path $env:USERPROFILE '.bun\bin\bun.exe'
    if (-not (Test-Path $cand)) { Write-Error 'bun 未找到，请先安装 bun 或将其加入 PATH'; exit 1 }
    $bun = $cand
}

$scriptDir = $PSScriptRoot
& $bun run (Join-Path $scriptDir 'bannersage-query.ts') @args