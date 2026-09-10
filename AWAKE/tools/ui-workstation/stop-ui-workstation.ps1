[CmdletBinding()]
param(
    [string]$RuntimeRoot = (Join-Path $PSScriptRoot '.runtime'),
    [int]$TimeoutSec = 10
)

$ErrorActionPreference = 'Stop'
$runtime = [IO.Path]::GetFullPath($RuntimeRoot)
$gameRoot = [IO.Path]::GetFullPath('D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord')
$gamePrefix = $gameRoot.TrimEnd([char]'\', [char]'/') + [IO.Path]::DirectorySeparatorChar
if ($runtime.StartsWith($gamePrefix, [StringComparison]::OrdinalIgnoreCase) -or
    [string]::Equals($runtime, $gameRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'WB-WORKSTATION-GAME-403: UI Workstation 不得操作游戏目录中的运行时文件。'
}
$readyPath = Join-Path $runtime 'ready.json'
if (-not (Test-Path -LiteralPath $readyPath -PathType Leaf)) {
    throw 'WB-WORKSTATION-STOP-404: 未发现运行中的 UI Workstation。'
}

$ready = Get-Content -Raw -LiteralPath $readyPath | ConvertFrom-Json -Depth 10
if ([string]::IsNullOrWhiteSpace([string]$ready.address) -or
    [string]::IsNullOrWhiteSpace([string]$ready.instance_id)) {
    throw 'WB-WORKSTATION-STOP-400: ready.json 缺少 address 或 instance_id。'
}

$body = @{ instance_id = [string]$ready.instance_id } | ConvertTo-Json -Compress
$response = Invoke-RestMethod `
    -Uri "$([string]$ready.address.TrimEnd('/'))/shutdown" `
    -Method Post `
    -ContentType 'application/json' `
    -Body $body `
    -TimeoutSec $TimeoutSec

if ([string]$response.state -ne 'stopping') {
    throw 'WB-WORKSTATION-STOP-422: UI Workstation 未接受停止请求。'
}

$response | ConvertTo-Json -Compress
