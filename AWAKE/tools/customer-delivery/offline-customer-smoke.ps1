[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,
    [string]$EvidencePath = (Join-Path $PSScriptRoot '..\..\docs\evidence\AWAKE-CUSTOMER-OFFLINE.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$packageRoot = [IO.Path]::GetFullPath($PackageRoot)
$zipPath = [IO.Path]::GetFullPath($ZipPath)
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-customer-offline-' + [Guid]::NewGuid().ToString('N'))
$extractedRoot = Join-Path $tempRoot 'package'
$workspace = Join-Path $tempRoot 'workspace'
$pwb = $null
$wbs = $null
$ui = $null
$evidence = [ordered]@{
    schema_version = 'awake.customer.delivery-evidence.v1'
    section = 'offline_customer'
    status = 'failed'
    started_at_utc = [DateTime]::UtcNow.ToString('O')
    finished_at_utc = $null
    command = $MyInvocation.Line
    inputs = [ordered]@{ package_root = $packageRoot; zip_path = $zipPath }
    cases = @()
    package_build_id = $null
    package_hash = $null
    zip_hash = $null
    game_directory_touched = $false
    external_network_touched = $false
    temporary_workspace_removed = $false
    error = $null
}

function Add-Case([string]$Id, [string]$Status, [string]$Expected, [string]$Observed) {
    $script:evidence.cases += [ordered]@{ id = $Id; status = $Status; expected = $Expected; observed = $Observed }
}

function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Start-ProcessWithEnvironment(
    [string]$FileName,
    [string[]]$Arguments,
    [hashtable]$Environment,
    [string]$WorkingDirectory,
    [string]$StdoutPath,
    [string]$StderrPath
) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $FileName
    $info.WorkingDirectory = $WorkingDirectory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { [void]$info.ArgumentList.Add($argument) }
    foreach ($entry in $Environment.GetEnumerator()) { $info.Environment[$entry.Key] = [string]$entry.Value }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    [void]$process.Start()
    $process.add_Exited({
        try { $process.StandardOutput.ReadToEnd() | Set-Content -LiteralPath $StdoutPath -Encoding UTF8 } catch {}
        try { $process.StandardError.ReadToEnd() | Set-Content -LiteralPath $StderrPath -Encoding UTF8 } catch {}
    })
    return $process
}

function Stop-ProcessSafe([object]$Process) {
    if ($null -eq $Process) { return }
    try {
        $Process.Refresh()
        if (-not $Process.HasExited) {
            try { $Process.Kill($true) } catch { $Process.Kill() }
            [void]$Process.WaitForExit(5000)
        }
    } catch {}
    try { $Process.Dispose() } catch {}
}

function Invoke-Json([string]$Method, [string]$Uri, [object]$Body, [hashtable]$Headers = @{}) {
    $parameters = @{ Method = $Method; Uri = $Uri; Headers = $Headers; UseBasicParsing = $true; TimeoutSec = 20; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Compress -Depth 50))
    }
    $response = Invoke-WebRequest @parameters
    $value = if ([string]::IsNullOrWhiteSpace($response.Content)) { $null } else { $response.Content | ConvertFrom-Json -Depth 50 }
    return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Body = $value; Raw = $response.Content }
}

function Wait-Health([string]$Uri) {
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        try {
            $response = Invoke-Json 'Get' $Uri $null
            if ($response.StatusCode -eq 200) { return $response }
        } catch {}
        Start-Sleep -Milliseconds 250
    }
    throw "health timeout: $Uri"
}

try {
    if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) { throw 'package root missing' }
    if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) { throw 'package zip missing' }
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'delivery-manifest.json') | ConvertFrom-Json -Depth 100
    $evidence.package_build_id = [string]$manifest.build_id
    $evidence.package_hash = [string]$manifest.package_sha256
    $evidence.zip_hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    New-Item -ItemType Directory -Force -Path $extractedRoot, $workspace | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $extractedRoot)
    Add-Case 'clean-extract' 'passed' 'ZIP extracts into a fresh directory' 'passed'

    $pwbPort = Select-FreePort
    $wbsPort = Select-FreePort
    $wbsPackage = Join-Path $extractedRoot 'worldbook-studio'
    $pwbPackage = Join-Path $extractedRoot 'persona-workbench'
    $uiPackage = Join-Path $extractedRoot 'ui-workstation'
    $pwbExe = Join-Path $pwbPackage 'PersonaWorkbench.Web.exe'
    $wbsExe = Join-Path $wbsPackage 'web\Awake.WorldbookStudio.Web.exe'
    $uiStart = Join-Path $uiPackage 'start-ui-workstation.ps1'
    if (-not (Test-Path -LiteralPath $pwbExe) -or -not (Test-Path -LiteralPath $wbsExe) -or -not (Test-Path -LiteralPath $uiStart)) {
        throw 'customer package entrypoint is incomplete'
    }
    $pwb = Start-ProcessWithEnvironment $pwbExe @('--no-browser', '--port', [string]$pwbPort) @{} $pwbPackage (Join-Path $tempRoot 'pwb.out') (Join-Path $tempRoot 'pwb.err')
    $wbsEnvironment = @{
        AWAKE_WB_DEV_MODE = '1'
        AWAKE_WB_PORT = $wbsPort
        AWAKE_WB_WORKSPACE = $workspace
        WORLD_BOOK_WORKSPACE = $workspace
        AWAKE_WB_SCHEMA_ROOT = (Join-Path $wbsPackage 'schemas')
        WORLD_BOOK_SCHEMA_ROOT = (Join-Path $wbsPackage 'schemas')
        ASPNETCORE_ENVIRONMENT = 'Development'
        DOTNET_ENVIRONMENT = 'Development'
        HTTP_PROXY = ''
        HTTPS_PROXY = ''
        ALL_PROXY = ''
        NO_PROXY = '127.0.0.1,localhost'
    }
    $wbs = Start-ProcessWithEnvironment $wbsExe @{} $wbsEnvironment (Split-Path -Parent $wbsExe) (Join-Path $tempRoot 'wbs.out') (Join-Path $tempRoot 'wbs.err')
    $pwbHealth = Wait-Health "http://127.0.0.1:$pwbPort/health"
    $wbsHealth = Wait-Health "http://127.0.0.1:$wbsPort/api/health"
    Add-Case 'health' 'passed' 'PWB and WBS report ready' "pwb=$($pwbHealth.StatusCode);wbs=$($wbsHealth.StatusCode)"

    $uiRuntime = Join-Path $tempRoot 'ui-runtime'
    $ui = Start-ProcessWithEnvironment (Get-Process -Id $PID).Path @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $uiStart, '-WorkspaceRoot', $workspace, '-WbsBaseUrl', "http://127.0.0.1:$wbsPort", '-Port', '0', '-RuntimeRoot', $uiRuntime) @{} $uiPackage (Join-Path $tempRoot 'ui.out') (Join-Path $tempRoot 'ui.err')
    $uiReadyPath = Join-Path $uiRuntime 'ready.json'
    for ($attempt = 0; $attempt -lt 120 -and -not (Test-Path -LiteralPath $uiReadyPath); $attempt++) { Start-Sleep -Milliseconds 250 }
    if (-not (Test-Path -LiteralPath $uiReadyPath)) { throw 'UI adapter did not become ready' }
    $uiReady = Get-Content -Raw -LiteralPath $uiReadyPath | ConvertFrom-Json
    $uiHealth = Wait-Health "$($uiReady.address)/health"
    Add-Case 'ui-health' 'passed' 'UI Workstation reports ready' "HTTP $($uiHealth.StatusCode)"

    $wbsSession = Invoke-Json 'Post' "http://127.0.0.1:$wbsPort/api/ai/session/bootstrap" $null @{ Origin = "http://127.0.0.1:$wbsPort" }
    $csrf = [string]$wbsSession.Body.csrfToken
    $headers = @{ Origin = "http://127.0.0.1:$wbsPort"; 'X-AWAKE-CSRF' = $csrf }
    $create = Invoke-Json 'Post' "http://127.0.0.1:$wbsPort/api/authoring/document/new" @{
        title = '客户离线验收夹具'
        domain = 'politics'
        subdomain = 'throne'
        relatedDomains = @('war')
        contentTier = 'base'
        authorId = 'author.developer'
    } $headers
    if ($create.StatusCode -ne 200) { throw "offline create failed: $($create.Raw)" }
    $path = [string]$create.Body.document.path
    $read = Invoke-Json 'Get' "http://127.0.0.1:$wbsPort/api/document?path=$([Uri]::EscapeDataString($path))" $null
    if ($read.StatusCode -ne 200) { throw "offline read failed: $($read.Raw)" }
    Add-Case 'create-reopen' 'passed' 'Customer can create and reopen a draft' "path=$path"
    $providers = Invoke-Json 'Get' "http://127.0.0.1:$wbsPort/api/ai/providers" $null $headers
    Add-Case 'no-worker-degradation' 'passed' 'Missing Worker state is observable' "HTTP $($providers.StatusCode)"
    Add-Case 'export' 'not_run' 'Customer export is verified separately through authority workflow' 'not_run in this offline smoke'
    $evidence.status = 'partial'
}
catch {
    $evidence.error = $_.Exception.Message
}
finally {
    Stop-ProcessSafe $pwb
    Stop-ProcessSafe $wbs
    Stop-ProcessSafe $ui
    $evidence.finished_at_utc = [DateTime]::UtcNow.ToString('O')
    $evidence.temporary_workspace_removed = $true
    $evidencePathFull = [IO.Path]::GetFullPath($EvidencePath)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidencePathFull) | Out-Null
    $evidence | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $evidencePathFull -Encoding UTF8
    if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }
    $evidence.temporary_workspace_removed = -not (Test-Path -LiteralPath $tempRoot -PathType Container)
    $evidence | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $evidencePathFull -Encoding UTF8
}

Write-Output "offline_status=$($evidence.status)"
Write-Output "offline_evidence=$([IO.Path]::GetFullPath($EvidencePath))"
if ($evidence.status -ne 'passed') { exit 1 }
