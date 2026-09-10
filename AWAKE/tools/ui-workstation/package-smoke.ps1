[CmdletBinding()]
param(
    [string]$PackageRoot = (Join-Path $PSScriptRoot 'artifacts\smoke')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PSScriptRoot)
$packageOutput = [IO.Path]::GetFullPath($PackageRoot)
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('awake-ui-package-smoke-' + [Guid]::NewGuid().ToString('N'))
$workspace = Join-Path $scratch 'workspace'
$runtime = Join-Path $scratch 'runtime'
$extracted = Join-Path $scratch 'extracted'
$process = $null

function Get-Hash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-Equal([object]$Actual, [object]$Expected, [string]$Message) {
    if ([string]$Actual -ne [string]$Expected) {
        throw "WB-UI-SMOKE-422: $Message (actual=$Actual expected=$Expected)"
    }
}

try {
    New-Item -ItemType Directory -Force -Path $scratch, $workspace, $extracted | Out-Null
    if (Test-Path -LiteralPath $packageOutput) {
        Remove-Item -LiteralPath $packageOutput -Recurse -Force
    }

    & (Join-Path $root 'package-ui-workstation.ps1') -OutputRoot $packageOutput -BuildId 'smoke-20260904-0001'

    $package = Get-ChildItem -LiteralPath $packageOutput -Directory | Select-Object -First 1
    $zip = Get-ChildItem -LiteralPath $packageOutput -Filter '*.zip' -File | Select-Object -First 1
    if ($null -eq $package -or $null -eq $zip) { throw 'WB-UI-SMOKE-404: 包目录或 ZIP 缺失。' }
    $sidecar = $zip.FullName + '.sha256'
    if (-not (Test-Path -LiteralPath $sidecar -PathType Leaf)) { throw 'WB-UI-SMOKE-404: ZIP sidecar 缺失。' }

    $manifestPath = Join-Path $package.FullName 'manifest.json'
    $sumPath = Join-Path $package.FullName 'SHA256SUMS.txt'
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -Depth 20
    Assert-Equal $manifest.entrypoint 'start-ui-workstation.ps1' '客户入口不正确'
    Assert-Equal $manifest.stop_entrypoint 'stop-ui-workstation.ps1' '停止入口不正确'
    Assert-Equal $manifest.game_directory_access 'forbidden' '游戏目录策略不正确'
    foreach ($entry in @($manifest.files)) {
        Assert-Equal (Get-Hash (Join-Path $package.FullName $entry.path)) $entry.sha256 "manifest 哈希不匹配：$($entry.path)"
    }
    $sumLines = Get-Content -LiteralPath $sumPath
    foreach ($line in $sumLines) {
        $parts = $line -split '\s{2}', 2
        if ($parts.Count -ne 2) { throw "WB-UI-SMOKE-422: SHA256SUMS 行格式无效：$line" }
        Assert-Equal (Get-Hash (Join-Path $package.FullName $parts[1])) $parts[0].ToLowerInvariant() "SHA256SUMS 不匹配：$($parts[1])"
    }

    Expand-Archive -LiteralPath $zip.FullName -DestinationPath $extracted -Force
    $extractedPackage = $extracted
    if (-not (Test-Path -LiteralPath (Join-Path $extractedPackage 'start-ui-workstation.ps1') -PathType Leaf)) {
        throw 'WB-UI-SMOKE-404: ZIP 未包含客户启动入口。'
    }
    Assert-Equal (Get-Hash $zip.FullName) ((Get-Content -Raw -LiteralPath $sidecar).Trim().Split(' ')[0]) 'ZIP sidecar 不匹配'

    New-Item -ItemType Directory -Force -Path $workspace | Out-Null
    $pwsh = (Get-Process -Id $PID).Path
    $start = Join-Path $extractedPackage 'start-ui-workstation.ps1'
    $stdout = Join-Path $scratch 'ui.stdout.log'
    $stderr = Join-Path $scratch 'ui.stderr.log'
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$start`" -WorkspaceRoot `"$workspace`" -WbsBaseUrl `"http://127.0.0.1:1`" -Port 0 -RuntimeRoot `"$runtime`""
    $process = Start-Process -FilePath $pwsh -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr -ArgumentList $arguments
    $readyPath = Join-Path $runtime 'ready.json'
    $ready = $null
    for ($index = 0; $index -lt 150 -and $null -eq $ready; $index++) {
        Start-Sleep -Milliseconds 100
        if (Test-Path -LiteralPath $readyPath -PathType Leaf) {
            $ready = Get-Content -Raw -LiteralPath $readyPath | ConvertFrom-Json -Depth 10
        }
    }
    if ($null -eq $ready) { throw 'WB-UI-SMOKE-504: 启动后未生成 ready.json。' }
    $health = Invoke-RestMethod -Uri "$($ready.address)/health" -TimeoutSec 5
    Assert-Equal $health.state 'ready' 'health 状态不正确'
    Assert-Equal $health.workstation_id 'ui_workstation' 'workstation_id 不正确'

    $stop = Join-Path $extractedPackage 'stop-ui-workstation.ps1'
    $stopOutput = & $pwsh -NoProfile -ExecutionPolicy Bypass -File $stop -RuntimeRoot $runtime
    $stopPayload = $stopOutput | ConvertFrom-Json -Depth 10
    Assert-Equal $stopPayload.state 'stopping' '停止响应不正确'
    if (-not $process.WaitForExit(5000)) { throw 'WB-UI-SMOKE-504: UI Workstation 未退出。' }
    if (Test-Path -LiteralPath $readyPath) { throw 'WB-UI-SMOKE-422: ready.json 未清理。' }

    Write-Output "UI PACKAGE SMOKE PASS build=$($manifest.build_id) zip=$($zip.Name)"
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $scratch -PathType Container) {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $packageOutput -PathType Container) {
        Remove-Item -LiteralPath $packageOutput -Recurse -Force -ErrorAction SilentlyContinue
    }
}
