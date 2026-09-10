param(
    [string]$EvidencePath = "artifacts\current-test\evidence\launcher-tests.v2.json",
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'tests\Awake.WorldbookStudio.Launcher.Tests\Awake.WorldbookStudio.Launcher.Tests.csproj'
$evidence = if ([System.IO.Path]::IsPathRooted($EvidencePath)) { [System.IO.Path]::GetFullPath($EvidencePath) } else { [System.IO.Path]::GetFullPath((Join-Path $root $EvidencePath)) }
$output = @()
$exitCode = 1
$previousPackage = $env:AWAKE_WB_TEST_PACKAGE
$previousPort = $env:AWAKE_WB_PORT
$previousMutex = $env:AWAKE_WB_MUTEX_NAME

function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

$env:AWAKE_WB_PORT = [string](Select-FreePort)
$env:AWAKE_WB_MUTEX_NAME = "Local\AWAKE.WorldbookStudio.Launcher.Tests.$([Guid]::NewGuid().ToString('N'))"
if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $defaultPackage = Join-Path $root 'artifacts\current-test\WorldbookStudio'
    if (Test-Path -LiteralPath $defaultPackage -PathType Container) { $PackagePath = $defaultPackage }
}
if (-not [string]::IsNullOrWhiteSpace($PackagePath)) {
    $env:AWAKE_WB_TEST_PACKAGE = if ([System.IO.Path]::IsPathRooted($PackagePath)) { [System.IO.Path]::GetFullPath($PackagePath) } else { [System.IO.Path]::GetFullPath((Join-Path $root $PackagePath)) }
}

Push-Location $root
try {
    & dotnet build $project --configuration Release --no-restore 2>&1 | Out-String | ForEach-Object { $output += $_ }
    if ($LASTEXITCODE -ne 0) { throw 'WB-LAUNCHER-TEST-001: Launcher test host 构建失败。' }
    $output += @(& dotnet run --project $project --configuration Release --no-restore 2>&1)
    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
    if (-not [string]::IsNullOrWhiteSpace($PackagePath)) { $env:AWAKE_WB_TEST_PACKAGE = $previousPackage }
    if ($null -eq $previousPort) { Remove-Item Env:AWAKE_WB_PORT -ErrorAction SilentlyContinue }
    else { $env:AWAKE_WB_PORT = $previousPort }
    if ($null -eq $previousMutex) { Remove-Item Env:AWAKE_WB_MUTEX_NAME -ErrorAction SilentlyContinue }
    else { $env:AWAKE_WB_MUTEX_NAME = $previousMutex }
}

$passLines = @($output | Where-Object { $_ -match '^PASS:' })
$result = [ordered]@{
    schemaVersion = 'awake.worldbook.launcher-tests.v2'
    ok = $exitCode -eq 0
    exitCode = $exitCode
    passCount = $passLines.Count
    output = @($output)
    generatedAtUtc = [DateTime]::UtcNow.ToString('O')
}
$directory = Split-Path -Parent $evidence
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidence -Encoding UTF8
if ($exitCode -ne 0) { throw 'WB-LAUNCHER-TEST-002: Launcher seam tests failed.' }
Write-Output "PASS: Launcher tests ($($passLines.Count))"
Write-Output "EVIDENCE: $evidence"
