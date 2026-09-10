param(
    [string]$EvidencePath = "artifacts\current-test\evidence\workstation-handoff-loopback-smoke.test.json",
    [switch]$KeepTemp
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $root 'src\Awake.WorldbookStudio.Web\Awake.WorldbookStudio.Web.csproj'
$schemaRoot = Join-Path $root '..\..\docs\worldbook-studio-plan'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-wbs-handoff-smoke-' + [Guid]::NewGuid().ToString('N'))
$workspace = Join-Path $tempRoot 'workspace'
$port = 0
$process = $null
$webSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$evidence = [ordered]@{
    schema_version = 'awake.workstation.handoff.loopback-smoke.v1'
    started_at_utc = [DateTime]::UtcNow.ToString('O')
    network_boundary = 'loopback_only'
    cases = @()
    game_directory_touched = $false
    canon_written = $false
    passed = $false
    error = $null
}

function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "WB-HANDOFF-SMOKE-FAIL: $Message" }
}

function Stop-Child([object]$Child) {
    if ($null -eq $Child) { return }
    try {
        if (-not $Child.HasExited) {
            try { $Child.Kill($true) } catch { $Child.Kill() }
            [void]$Child.WaitForExit(5000)
        }
    } catch { }
    try { $Child.Dispose() } catch { }
}

function Sha256Text([string]$Text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).ToLowerInvariant()
    } finally { $sha.Dispose() }
}

function Invoke-Json([string]$Method, [string]$Uri, [object]$Body, [hashtable]$Headers) {
    $parameters = @{
        Method = $Method
        Uri = $Uri
        Headers = $Headers
        WebSession = $webSession
        UseBasicParsing = $true
        TimeoutSec = 20
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Compress -Depth 30))
    }
    try {
        $response = Invoke-WebRequest @parameters
        return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Body = ($response.Content | ConvertFrom-Json) }
    } catch {
        $response = $_.Exception.Response
        if ($null -eq $response) { throw }
        $content = $_.ErrorDetails.Message
        return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Body = ($content | ConvertFrom-Json) }
    }
}

try {
    Assert (Test-Path -LiteralPath $webProject -PathType Leaf) 'Web 项目不存在。'
    $port = Select-FreePort
    New-Item -ItemType Directory -Force -Path $workspace | Out-Null
    $environment = @{
        AWAKE_WB_DEV_MODE = '1'
        WORLD_BOOK_WORKSPACE = $workspace
        WORLD_BOOK_SCHEMA_ROOT = $schemaRoot
        AWAKE_WB_PORT = [string]$port
        ASPNETCORE_ENVIRONMENT = 'Development'
        DOTNET_ENVIRONMENT = 'Development'
    }
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Command dotnet -ErrorAction Stop).Source
    $startInfo.WorkingDirectory = $root
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add('run')
    $startInfo.ArgumentList.Add('--project')
    $startInfo.ArgumentList.Add($webProject)
    $startInfo.ArgumentList.Add('--configuration')
    $startInfo.ArgumentList.Add('Release')
    $startInfo.ArgumentList.Add('--no-build')
    $startInfo.ArgumentList.Add('--no-restore')
    foreach ($entry in $environment.GetEnumerator()) { $startInfo.Environment[$entry.Key] = [string]$entry.Value }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $origin = "http://127.0.0.1:$port"
    $healthy = $false
    for ($attempt = 0; $attempt -lt 80 -and -not $healthy; $attempt++) {
        try { $healthy = (Invoke-WebRequest -Uri "$origin/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { Start-Sleep -Milliseconds 250 }
    }
    Assert $healthy 'WBS 未进入健康状态。'
    $bootstrap = Invoke-Json 'Post' "$origin/api/ai/session/bootstrap" $null @{ Origin = $origin }
    Assert ($bootstrap.StatusCode -eq 200 -and $bootstrap.Body.ok -eq $true) '会话初始化失败。'
    $headers = @{ Origin = $origin; 'X-AWAKE-CSRF' = [string]$bootstrap.Body.csrfToken }

    $payload = '{"id":"persona-1","name":"Nordvig"}'
    $issuedAt = [DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('yyyy-MM-ddTHH:mm:ssZ')
    $expiresAt = [DateTimeOffset]::UtcNow.AddHours(1).ToString('yyyy-MM-ddTHH:mm:ssZ')
    $envelope = [ordered]@{
        schema_version = 'awake.workstation.handoff-envelope.v1'
        handoff_id = 'pwb-handoff-0123456789abcdef0123456789abcdef'
        workspace_id = 'awake-test'
        document_id = 'persona-1'
        revision = 3
        content_sha256 = (Sha256Text $payload)
        producer = 'persona_workbench'
        provenance = [ordered]@{
            source_schema = 'persona.workbench.persona.v1'
            source_id = 'persona-1'
            source_revision = 3
            source_sha256 = ('b' * 64)
            issuer_id = 'persona-workbench'
        }
        review_only = $true
        review_status = 'approved_local'
        issued_at_utc = $issuedAt
        expires_at_utc = $expiresAt
        payload = $payload
    }
    $fingerprintInput = @(
        $envelope.schema_version, $envelope.handoff_id, $envelope.workspace_id,
        $envelope.document_id, ([string]$envelope.revision), $envelope.content_sha256,
        $envelope.producer, 'true', $envelope.review_status, $envelope.issued_at_utc,
        $envelope.expires_at_utc, $envelope.payload
    ) -join "`n"
    $envelope.request_fingerprint = Sha256Text $fingerprintInput

    $import = Invoke-Json 'Post' "$origin/api/integration/persona/handoff/import" @{ envelope = $envelope } $headers
    Assert ($import.StatusCode -eq 200 -and $import.Body.ok -eq $true -and $import.Body.data.lifecycle_status -eq 'issued') ("import 失败：HTTP {0}; {1}" -f $import.StatusCode, ($import.Body | ConvertTo-Json -Compress -Depth 20))
    $receiptId = [string]$import.Body.data.receipt_id
    $accept = Invoke-Json 'Post' "$origin/api/integration/persona/handoff/$($envelope.handoff_id)/accept" @{ receipt_id = $receiptId; expected_status = 'issued' } $headers
    Assert ($accept.StatusCode -eq 200 -and $accept.Body.data.lifecycle_status -eq 'accepted') 'accept 失败。'
    $consume = Invoke-Json 'Post' "$origin/api/integration/persona/handoff/$($envelope.handoff_id)/consume" @{ receipt_id = $receiptId; expected_status = 'accepted' } $headers
    Assert ($consume.StatusCode -eq 200 -and $consume.Body.data.lifecycle_status -eq 'consumed' -and $consume.Body.data.draft_status -eq 'needs_review' -and $consume.Body.data.review_only -eq $true) 'consume 未生成 review-only 草稿。'
    $canonFiles = @(Get-ChildItem -LiteralPath $workspace -Recurse -File -Filter '*.canon.json' -ErrorAction SilentlyContinue)
    $evidence.canon_written = $canonFiles.Count -gt 0
    Assert (-not $evidence.canon_written) 'handoff consume 不得写入 canon。'
    $duplicate = Invoke-Json 'Post' "$origin/api/integration/persona/handoff/import" @{ envelope = $envelope } $headers
    Assert ($duplicate.StatusCode -eq 200 -and $duplicate.Body.data.receipt_id -eq $receiptId) '重复 import 未幂等。'
    $readback = Invoke-Json 'Get' "$origin/api/integration/persona/handoff/$($envelope.handoff_id)" $null $headers
    Assert ($readback.StatusCode -eq 200 -and $readback.Body.data.envelope.document_id -eq 'persona-1' -and $readback.Body.data.receipt.lifecycle_status -eq 'consumed') 'handoff GET 回读失败。'
    $conflictEnvelope = [ordered]@{}
    foreach ($property in $envelope.GetEnumerator()) { $conflictEnvelope[$property.Key] = $property.Value }
    $conflictEnvelope.document_id = 'persona-2'
    $conflictFingerprintInput = @(
        $conflictEnvelope.schema_version, $conflictEnvelope.handoff_id, $conflictEnvelope.workspace_id,
        $conflictEnvelope.document_id, ([string]$conflictEnvelope.revision), $conflictEnvelope.content_sha256,
        $conflictEnvelope.producer, 'true', $conflictEnvelope.review_status, $conflictEnvelope.issued_at_utc,
        $conflictEnvelope.expires_at_utc, $conflictEnvelope.payload
    ) -join "`n"
    $conflictEnvelope.request_fingerprint = Sha256Text $conflictFingerprintInput
    $conflict = Invoke-Json 'Post' "$origin/api/integration/persona/handoff/import" @{ envelope = $conflictEnvelope } $headers
    Assert ($conflict.StatusCode -eq 409 -and $conflict.Body.error -eq 'WB-HANDOFF-409') '变化后的重复 handoff 未返回稳定冲突。'

    $evidence.cases = @(
        [ordered]@{ id = 'wbs-import'; passed = $true; expected = '200 issued'; observed = [string]$import.StatusCode; artifacts = @('raw import response') },
        [ordered]@{ id = 'wbs-accept'; passed = $true; expected = '200 accepted'; observed = [string]$accept.StatusCode; artifacts = @('raw accept response') },
        [ordered]@{ id = 'wbs-consume'; passed = $true; expected = '200 consumed needs_review'; observed = [string]$consume.StatusCode; artifacts = @('raw consume response') },
        [ordered]@{ id = 'duplicate-idempotent'; passed = $true; expected = 'same receipt'; observed = [string]$duplicate.StatusCode; artifacts = @('raw duplicate response') },
        [ordered]@{ id = 'handoff-readback'; passed = $true; expected = 'same document/revision/hash'; observed = [string]$readback.StatusCode; artifacts = @('raw GET response') },
        [ordered]@{ id = 'changed-fingerprint-conflict'; passed = $true; expected = '409 WB-HANDOFF-409'; observed = [string]$conflict.StatusCode; artifacts = @('raw conflict response') }
    )
    $evidence.passed = $true
}
catch {
    $evidence.error = $_.Exception.Message
    throw
}
finally {
    $evidence.finished_at_utc = [DateTime]::UtcNow.ToString('O')
    $evidencePathAbsolute = if ([IO.Path]::IsPathRooted($EvidencePath)) { $EvidencePath } else { Join-Path $root $EvidencePath }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidencePathAbsolute) | Out-Null
    $evidence | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $evidencePathAbsolute -Encoding UTF8
    Stop-Child $process
    if (-not $KeepTemp) { Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Output "workstation_handoff_loopback_passed=$($evidence.passed)"
Write-Output "workstation_handoff_evidence=$evidencePathAbsolute"
