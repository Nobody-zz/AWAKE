param([switch]$Locked = $true)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet restore Awake.WorldbookStudio.slnx --configfile NuGet.Config --locked-mode --force-evaluate --ignore-failed-sources
    if ($LASTEXITCODE -ne 0) { throw 'WB-RESTORE-001: 离线还原失败。' }
} finally { Pop-Location }
