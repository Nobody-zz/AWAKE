param(
    [ValidateSet('All', 'AuthorityGate')]
    [string]$Suite = 'All'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if ($Suite -eq 'AuthorityGate') {
    $authorityProject = Join-Path $root 'tests\Awake.WorldbookStudio.AuthorityGate.Tests\Awake.WorldbookStudio.AuthorityGate.Tests.csproj'
    $authorityEvidencePath = Join-Path $root 'artifacts\current-test\evidence\authority-gate.test.json'
    if (-not (Test-Path -LiteralPath $authorityProject -PathType Leaf)) { throw 'WB-TEST-040: AuthorityGate test project missing.' }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $authorityEvidencePath) | Out-Null
    Push-Location $root
    try {
        & dotnet build $authorityProject --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'WB-TEST-041: AuthorityGate build failed.' }
        $env:AWAKE_WB_AUTHORITY_EVIDENCE_PATH = $authorityEvidencePath
        & dotnet run --project $authorityProject --no-build --configuration Release
        if ($LASTEXITCODE -ne 0) { throw 'WB-TEST-042: AuthorityGate suite failed.' }
    }
    finally { Pop-Location }
    if (-not (Test-Path -LiteralPath $authorityEvidencePath -PathType Leaf)) { throw 'WB-TEST-043: AuthorityGate evidence missing.' }
    $authorityEvidence = Get-Content -LiteralPath $authorityEvidencePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($authorityEvidence.authority_gate_passed -ne $true) { throw 'WB-TEST-044: AuthorityGate evidence did not pass.' }
    Write-Output 'authority_gate_passed=true'
    Write-Output "authority_gate_evidence=$authorityEvidencePath"
    return
}

$node = "C:\Users\26811\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe"
function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}
$env:AWAKE_WB_TEST_PORT = [string](Select-FreePort)
& $node (Join-Path $root "tests\frontend\editor-session.test.js")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $node (Join-Path $root "tests\frontend\editor-safety.test.js")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $node (Join-Path $root "tests\frontend\draft-race.test.js")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $node (Join-Path $root "tests\frontend\draft-dom-state.test.js")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $node (Join-Path $root "tests\frontend\batch-race.test.js")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $node (Join-Path $root "tests\frontend\editor-content.test.js")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $node (Join-Path $root "tests\frontend\customer-closure.test.js")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& "$PSScriptRoot\build.ps1"
Push-Location $root
try {
    dotnet run --project tests\Awake.WorldbookStudio.Tests\Awake.WorldbookStudio.Tests.csproj --no-build --configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run --project tests\Awake.WorldbookStudio.EditorContent.Tests\Awake.WorldbookStudio.EditorContent.Tests.csproj --no-build --configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run --project tests\Awake.WorldbookStudio.BatchTests\Awake.WorldbookStudio.BatchTests.csproj --no-build --configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run --project tests\Awake.WorldbookStudio.Draft.Tests\Awake.WorldbookStudio.Draft.Tests.csproj --no-build --configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run --project tests\Awake.WorldbookStudio.Workstation.Tests\Awake.WorldbookStudio.Workstation.Tests.csproj --no-build --configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & "$PSScriptRoot\draft-workflow-smoke.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & "$PSScriptRoot\workstation-handoff-loopback-smoke.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & "$PSScriptRoot\authoring-save-smoke.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& "$PSScriptRoot\public-contract-smoke.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $batchEvidencePath = Join-Path $root 'artifacts\current-test\evidence\batch-workflow-smoke.test.json'
    & "$PSScriptRoot\batch-workflow-smoke.ps1" -EvidencePath $batchEvidencePath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally { Pop-Location }
