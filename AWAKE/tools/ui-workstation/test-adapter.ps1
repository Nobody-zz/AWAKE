$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'UiWorkstation.Adapter.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('awake-ui-adapter-' + [Guid]::NewGuid().ToString('N'))
$workspace = Join-Path $root 'workspace'
$runtime = Join-Path $root 'runtime'
$output = Join-Path $root 'adapter.out'
$errorPath = Join-Path $root 'adapter.err'
New-Item -ItemType Directory -Force -Path $workspace | Out-Null
$process = $null
try {
    $pwsh = (Get-Process -Id $PID).Path
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$script`" -WorkspaceRoot `"$workspace`" -WbsBaseUrl `"http://127.0.0.1:5444`" -Port 0 -RuntimeRoot `"$runtime`""
    $process = Start-Process -FilePath $pwsh -WindowStyle Hidden -PassThru -RedirectStandardOutput $output -RedirectStandardError $errorPath -ArgumentList $arguments
    $readyPath = Join-Path $runtime 'ready.json'
    $ready = $null
    for ($index = 0; $index -lt 200 -and $null -eq $ready; $index++) {
        Start-Sleep -Milliseconds 100
        if (Test-Path -LiteralPath $readyPath) { $ready = Get-Content -Raw -LiteralPath $readyPath | ConvertFrom-Json }
    }
    if ($null -eq $ready) {
        $stdout = if (Test-Path -LiteralPath $output) { Get-Content -Raw -LiteralPath $output } else { '' }
        $stderr = if (Test-Path -LiteralPath $errorPath) { Get-Content -Raw -LiteralPath $errorPath } else { '' }
        throw "adapter did not write ready.json; stdout=$stdout; stderr=$stderr"
    }
    $health = Invoke-RestMethod -Uri "$($ready.address)/health" -TimeoutSec 5
    if ($health.state -ne 'ready' -or $health.workstation_id -ne 'ui_workstation') { throw 'health contract mismatch' }
    $shutdown = Invoke-RestMethod -Uri "$($ready.address)/shutdown" -Method Post -ContentType 'application/json' -Body (@{ instance_id = $ready.instance_id } | ConvertTo-Json) -TimeoutSec 5
    if ($shutdown.state -ne 'stopping') { throw 'shutdown contract mismatch' }
    $process.WaitForExit(5000) | Out-Null
    if (-not $process.HasExited) { throw 'adapter process did not exit' }
    if (Test-Path -LiteralPath $readyPath) { throw 'ready file remained' }
    Write-Output "UI ADAPTER SMOKE PASS port=$($ready.port) instance=$($ready.instance_id)"
}
finally {
    if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
}
