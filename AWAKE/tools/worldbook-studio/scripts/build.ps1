$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot\restore-offline.ps1"
if ($LASTEXITCODE -ne 0) { throw 'WB-BUILD-001: 还原失败。' }
Push-Location $root
try {
    dotnet build Awake.WorldbookStudio.slnx --no-restore --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'WB-BUILD-002: 构建失败。' }
} finally { Pop-Location }
