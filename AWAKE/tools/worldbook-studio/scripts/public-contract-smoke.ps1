$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $root 'src\Awake.WorldbookStudio.Web\Awake.WorldbookStudio.Web.csproj'
$projectDirectory = Split-Path -Parent $webProject
$projectName = Split-Path -Leaf $webProject
$schemaRoot = [IO.Path]::GetFullPath((Join-Path $root '..\..\docs\worldbook-studio-plan'))
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-public-contract-' + [Guid]::NewGuid().ToString('N'))
$workspaceRoot = Join-Path $tempRoot 'workspace'
$logPath = Join-Path $tempRoot 'web.log'
$errPath = Join-Path $tempRoot 'web.err.log'
New-Item -ItemType Directory -Force -Path $workspaceRoot | Out-Null
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$old = @{
    dev = $env:AWAKE_WB_DEV_MODE
    port = $env:AWAKE_WB_PORT
    workspace = $env:WORLD_BOOK_WORKSPACE
    schema = $env:WORLD_BOOK_SCHEMA_ROOT
}
$process = $null
$passed = $false
try {
    $env:AWAKE_WB_DEV_MODE = '1'
    $env:AWAKE_WB_PORT = [string]$port
    $env:WORLD_BOOK_WORKSPACE = $workspaceRoot
    $env:WORLD_BOOK_SCHEMA_ROOT = $schemaRoot
    $process = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', $projectName, '--no-build', '--configuration', 'Release') -WorkingDirectory $projectDirectory -WindowStyle Hidden -RedirectStandardOutput $logPath -RedirectStandardError $errPath -PassThru
    $health = $null
    for($attempt = 0; $attempt -lt 60; $attempt++) {
        Start-Sleep -Milliseconds 250
        if($process.HasExited) { throw "web process exited early: $($process.ExitCode)" }
        try {
            $health = Invoke-WebRequest -Uri "http://127.0.0.1:$port/health" -UseBasicParsing -TimeoutSec 2
            if($health.StatusCode -eq 200) { break }
        } catch { }
    }
    if($null -eq $health -or $health.StatusCode -ne 200) { throw 'web health endpoint did not become ready' }
    try {
        $response = Invoke-WebRequest -Uri "http://127.0.0.1:$port/api/ai/batch/test/start" -Method Post -UseBasicParsing -TimeoutSec 5
    }
    catch {
        $errorResponse = $_.Exception.Response
        if ($null -eq $errorResponse) { throw }
        $content = $_.ErrorDetails.Message
        $response = [pscustomobject]@{
            StatusCode = [int]$errorResponse.StatusCode
            Content = $content
            Headers = $errorResponse.Headers
        }
    }
    if($response.StatusCode -ne 410) { throw "retired route status was $($response.StatusCode)" }
    $body = $response.Content | ConvertFrom-Json
    if($body.error -ne 'WB-AUTHORITY-LEGACY-410') { throw 'retired route error code changed' }
    if($body.side_effect -ne 'none') { throw 'retired route reported a side effect' }
    $correlation = ($response.Headers.GetValues('X-AWAKE-Correlation-Id') | Select-Object -First 1)
    if([string]::IsNullOrWhiteSpace($correlation) -or $body.correlation_id -ne $correlation) { throw "retired route correlation projection is inconsistent body=$($body.correlation_id) header=$correlation headers=$($response.Headers | Out-String)" }
    $passed = $true
    Write-Output 'PUBLIC CONTRACT SMOKE: PASS'
}
finally {
    if($null -ne $process) {
        try {
            if(-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
            [void]$process.WaitForExit(5000)
        } catch { }
        try { $process.Dispose() } catch { }
    }
    if($null -eq $old.dev) { Remove-Item Env:AWAKE_WB_DEV_MODE -ErrorAction SilentlyContinue } else { $env:AWAKE_WB_DEV_MODE = $old.dev }
    if($null -eq $old.port) { Remove-Item Env:AWAKE_WB_PORT -ErrorAction SilentlyContinue } else { $env:AWAKE_WB_PORT = $old.port }
    if($null -eq $old.workspace) { Remove-Item Env:WORLD_BOOK_WORKSPACE -ErrorAction SilentlyContinue } else { $env:WORLD_BOOK_WORKSPACE = $old.workspace }
    if($null -eq $old.schema) { Remove-Item Env:WORLD_BOOK_SCHEMA_ROOT -ErrorAction SilentlyContinue } else { $env:WORLD_BOOK_SCHEMA_ROOT = $old.schema }
    if(-not $passed) { Write-Output "public contract smoke logs: $logPath / $errPath" }
    $tempFull = [IO.Path]::GetFullPath($tempRoot)
    $tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if($passed -and $tempFull.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $tempFull)) {
        try { Remove-Item -LiteralPath $tempFull -Recurse -Force -ErrorAction Stop } catch { Write-Output "public contract smoke temp cleanup deferred: $tempFull" }
    }
}
