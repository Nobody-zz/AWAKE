[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceRoot,
    [Parameter(Mandatory = $true)]
    [string]$WbsBaseUrl,
    [int]$Port = 0,
    [string]$RuntimeRoot = (Join-Path $PSScriptRoot '.runtime')
)

$ErrorActionPreference = 'Stop'
$adapter = Join-Path $PSScriptRoot 'UiWorkstation.Adapter.ps1'
if (-not (Test-Path -LiteralPath $adapter -PathType Leaf)) {
    throw 'WB-WORKSTATION-START-404: UI Workstation Adapter 不存在。'
}

$gameRoot = [IO.Path]::GetFullPath('D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord')
$runtimeFull = [IO.Path]::GetFullPath($RuntimeRoot)
$gamePrefix = $gameRoot.TrimEnd([char]'\', [char]'/') + [IO.Path]::DirectorySeparatorChar
if ($runtimeFull.StartsWith($gamePrefix, [StringComparison]::OrdinalIgnoreCase) -or
    [string]::Equals($runtimeFull, $gameRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'WB-WORKSTATION-GAME-403: UI Workstation 不得将运行时文件写入游戏目录。'
}

& $adapter `
    -WorkspaceRoot $WorkspaceRoot `
    -WbsBaseUrl $WbsBaseUrl `
    -Port $Port `
    -RuntimeRoot $RuntimeRoot
exit $LASTEXITCODE
