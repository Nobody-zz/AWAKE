# 角色卡 × BannerlordSage 库 一致性核验入口（只读）
# 核对 characters/*.persona.json 的 origins.heroId / kingdomId / clanId
# 与 BannerlordSage 本地库中 hero 的实际归属是否一致。
# 用法:  .\audit-character-refs.ps1     (退出码 0=一致 / 1=有缺失或不一致 / 2=依赖缺失)
# DB 路径可用环境变量 BANNERSAGE_DB 覆盖，默认指向本机 BannerlordSage 库。
$ErrorActionPreference = 'Stop'

$bun = (Get-Command bun -ErrorAction SilentlyContinue).Source
if (-not $bun) {
    $cand = Join-Path $env:USERPROFILE '.bun\bin\bun.exe'
    if (-not (Test-Path $cand)) { Write-Error 'bun 未找到，请先安装 bun 或将其加入 PATH'; exit 1 }
    $bun = $cand
}

$scriptDir = $PSScriptRoot
& $bun run (Join-Path $scriptDir 'audit-character-refs.ts')
exit $LASTEXITCODE