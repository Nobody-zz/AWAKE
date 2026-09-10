[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,
    [Parameter(Mandatory = $true)]
    [string]$EvidencePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packageRoot = [IO.Path]::GetFullPath($PackageRoot)
$evidenceFull = [IO.Path]::GetFullPath($EvidencePath)
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-customer-authority-export-' + [Guid]::NewGuid().ToString('N'))
$wbsRoot = Join-Path $tempRoot 'wbs'
$workspace = Join-Path $tempRoot 'workspace'
$wbsZip = Join-Path $packageRoot 'worldbook-studio\worldbook_studio.zip'
$fixture = Join-Path $wbsRoot 'schemas\fixture-valid-accepted-variant.yaml'
$sourceFixture = Join-Path $wbsRoot 'schemas\source-fixture-demo.yaml'
$sourceTextFixture = Join-Path $wbsRoot 'schemas\source-demo.txt'
$documentPath = 'authoring/politics/variant.yaml'
$wbsProcess = $null
$stdoutPath = Join-Path $tempRoot 'wbs.stdout.log'
$stderrPath = Join-Path $tempRoot 'wbs.stderr.log'
$stdoutTask = $null
$stderrTask = $null
$evidence = [ordered]@{
    schema_version = 'awake.customer.authority-export-evidence.v1'
    status = 'failed'
    package_root = $packageRoot
    cases = @()
    game_directory_touched = $false
    external_network_touched = $false
    temporary_workspace_removed = $false
    error = $null
}

function Add-Case([string]$Id, [string]$Status, [string]$Observed) {
    $script:evidence.cases += [ordered]@{
        id = $Id
        status = $Status
        observed = $Observed
    }
}

function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Stop-Child([object]$Process) {
    if ($null -eq $Process) { return }
    try {
        if (-not $Process.HasExited) {
            try { $Process.Kill($true) } catch { $Process.Kill() }
            [void]$Process.WaitForExit(5000)
        }
    }
    catch {}
    try { $Process.Dispose() } catch {}
}

function Invoke-Json([string]$Method, [string]$Uri, [object]$Body) {
    $parameters = @{
        Method = $Method
        Uri = $Uri
        UseBasicParsing = $true
        SkipHttpErrorCheck = $true
        TimeoutSec = 20
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Compress -Depth 80
    }
    $response = Invoke-WebRequest @parameters
    $body = if ([string]::IsNullOrWhiteSpace($response.Content)) { $null } else { $response.Content | ConvertFrom-Json -Depth 100 }
    [pscustomobject]@{
        StatusCode = [int]$response.StatusCode
        Body = $body
        Raw = [string]$response.Content
    }
}

function Assert-Ok([object]$Response, [string]$CaseId) {
    if ($Response.StatusCode -lt 200 -or $Response.StatusCode -ge 300 -or $null -eq $Response.Body -or $Response.Body.ok -ne $true) {
        throw "WB-CUSTOMER-AUTHORITY-017: $CaseId failed HTTP $($Response.StatusCode): $($Response.Raw)"
    }
}

try {
    if (-not (Test-Path -LiteralPath $wbsZip -PathType Leaf)) {
        throw 'WB-CUSTOMER-AUTHORITY-001: customer package WBS ZIP is missing.'
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    New-Item -ItemType Directory -Force -Path $wbsRoot, $workspace | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($wbsZip, $wbsRoot)
    if (-not (Test-Path -LiteralPath $fixture -PathType Leaf)) {
        throw 'WB-CUSTOMER-AUTHORITY-002: accepted fixture is missing from customer WBS package.'
    }
    $targetDocument = Join-Path $workspace $documentPath.Replace('/', '\')
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetDocument) | Out-Null
    Copy-Item -LiteralPath $fixture -Destination $targetDocument -Force
    $sourceDirectory = Join-Path $workspace 'authoring\sources'
    New-Item -ItemType Directory -Force -Path $sourceDirectory | Out-Null
    Copy-Item -LiteralPath $sourceFixture -Destination (Join-Path $sourceDirectory 'source-demo.yaml') -Force
    Copy-Item -LiteralPath $sourceTextFixture -Destination (Join-Path $sourceDirectory 'source-demo.txt') -Force
    Add-Case 'package-fixture' 'passed' 'accepted variant copied into isolated customer workspace'

    $schemaRoot = Join-Path $wbsRoot 'schemas'
    $wbsExe = Join-Path $wbsRoot 'web\Awake.WorldbookStudio.Web.exe'
    if (-not (Test-Path -LiteralPath $wbsExe -PathType Leaf)) {
        throw 'WB-CUSTOMER-AUTHORITY-003: customer WBS executable is missing.'
    }
    $port = Select-FreePort
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $wbsExe
    $startInfo.WorkingDirectory = Split-Path -Parent $wbsExe
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Environment['AWAKE_WB_DEV_MODE'] = '1'
    $startInfo.Environment['AWAKE_WB_PORT'] = [string]$port
    $startInfo.Environment['AWAKE_WB_WORKSPACE'] = $workspace
    $startInfo.Environment['WORLD_BOOK_WORKSPACE'] = $workspace
    $startInfo.Environment['AWAKE_WB_SCHEMA_ROOT'] = $schemaRoot
    $startInfo.Environment['WORLD_BOOK_SCHEMA_ROOT'] = $schemaRoot
    $startInfo.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $startInfo.Environment['DOTNET_ENVIRONMENT'] = 'Development'
    $startInfo.Environment['HTTP_PROXY'] = ''
    $startInfo.Environment['HTTPS_PROXY'] = ''
    $startInfo.Environment['ALL_PROXY'] = ''
    $startInfo.Environment['NO_PROXY'] = '127.0.0.1,localhost,::1'
    $wbsProcess = [Diagnostics.Process]::new()
    $wbsProcess.StartInfo = $startInfo
    [void]$wbsProcess.Start()
    $stdoutTask = $wbsProcess.StandardOutput.ReadToEndAsync()
    $stderrTask = $wbsProcess.StandardError.ReadToEndAsync()
    $origin = "http://127.0.0.1:$port"
    $healthy = $false
    for ($attempt = 0; $attempt -lt 120 -and -not $healthy; $attempt++) {
        try { $healthy = (Invoke-Json 'Get' "$origin/api/health" $null).StatusCode -eq 200 } catch {}
        if (-not $healthy) { Start-Sleep -Milliseconds 250 }
    }
    if (-not $healthy) { throw 'WB-CUSTOMER-AUTHORITY-004: customer WBS did not become healthy.' }
    Add-Case 'package-health' 'passed' "HTTP 200; package=$wbsExe"

    $register = Invoke-Json 'Post' "$origin/api/ai/authoring/documents/register" @{
        operationId = 'customer.export.register'
        path = $documentPath
    }
    Assert-Ok $register 'register'
    $documentId = [string]$register.Body.document_revision.documentId
    Add-Case 'register' 'passed' "document_id=$documentId"

    $selection = Invoke-Json 'Post' "$origin/api/ai/authoring/selections" @{
        operationId = 'customer.export.selection'
        documentIds = @($documentId)
    }
    Assert-Ok $selection 'selection'
    $selectionId = [string]$selection.Body.selection.selectionId
    Add-Case 'selection' 'passed' "selection_id=$selectionId"

    $approval = Invoke-Json 'Post' "$origin/api/ai/authoring/selections/$([Uri]::EscapeDataString($selectionId))/approve" @{
        operationId = 'customer.export.approval'
    }
    Assert-Ok $approval 'approval'
    $approvalId = [string]$approval.Body.approval_proof.approvalId
    Add-Case 'approval' 'passed' "approval_id=$approvalId"

    $compileProof = Invoke-Json 'Post' "$origin/api/ai/authoring/compile-proof" @{
        operationId = 'customer.export.compile'
        approvalId = $approvalId
        contentTier = 'base'
    }
    Assert-Ok $compileProof 'compile-proof'
    $compileProofId = [string]$compileProof.Body.compile_proof.compileProofId
    Add-Case 'compile-proof' 'passed' "compile_proof_id=$compileProofId"

    $outputRelative = 'export/WorldbookV2'
    $export = Invoke-Json 'Post' "$origin/api/ai/authoring/export-staging" @{
        operationId = 'customer.export.staging'
        compileProofId = $compileProofId
    }
    Assert-Ok $export 'export-staging'
    $stagingRelative = [string]$export.Body.staging
    $stagingRoot = Join-Path $workspace $stagingRelative.Replace('/', '\')
    if (-not (Test-Path -LiteralPath (Join-Path $stagingRoot 'manifest.json') -PathType Leaf)) {
        throw 'WB-CUSTOMER-AUTHORITY-018: staging manifest is missing.'
    }
    if (Test-Path -LiteralPath (Join-Path $workspace 'export\WorldbookV2\current.json') -PathType Leaf) {
        throw 'WB-CUSTOMER-AUTHORITY-019: staging export changed current pointer.'
    }
    Add-Case 'export-staging' 'passed' "staging=$stagingRelative; current_pointer_unchanged=true"
    $evidence.status = 'passed'
}
catch {
    $evidence.error = $_.Exception.Message
}
finally {
    Stop-Child $wbsProcess
    try { [IO.File]::WriteAllText($stdoutPath, $stdoutTask.Result, [Text.UTF8Encoding]::new($false)) } catch {}
    try { [IO.File]::WriteAllText($stderrPath, $stderrTask.Result, [Text.UTF8Encoding]::new($false)) } catch {}
    if (Test-Path -LiteralPath $stdoutPath -PathType Leaf) {
        $evidence.server_stdout = [IO.File]::ReadAllText($stdoutPath)
    }
    if (Test-Path -LiteralPath $stderrPath -PathType Leaf) {
        $evidence.server_stderr = [IO.File]::ReadAllText($stderrPath)
    }
    if (Test-Path -LiteralPath $tempRoot -PathType Container) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    $evidence.temporary_workspace_removed = -not (Test-Path -LiteralPath $tempRoot -PathType Container)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidenceFull) | Out-Null
    $evidence | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $evidenceFull -Encoding UTF8
}

Write-Output "customer_authority_export_status=$($evidence.status)"
Write-Output "customer_authority_export_evidence=$evidenceFull"
if ($evidence.status -ne 'passed') { exit 1 }
exit 0
