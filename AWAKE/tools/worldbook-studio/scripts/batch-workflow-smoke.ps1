param(
    [string]$EvidencePath,
    [switch]$KeepTemp
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $root 'src\Awake.WorldbookStudio.Web\Awake.WorldbookStudio.Web.csproj'
$schemaRoot = Join-Path $root '..\..\docs\worldbook-studio-plan'
$webPort = if ($env:AWAKE_WB_TEST_PORT) { [int]$env:AWAKE_WB_TEST_PORT } else { 0 }
$workerPort = 0
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-worldbook-batch-smoke-' + [Guid]::NewGuid().ToString('N'))
$workspace = Join-Path $tempRoot 'workspace'
$workerScript = Join-Path $tempRoot 'fake-worker.ps1'
$workerReady = Join-Path $tempRoot 'worker.ready'
$workerTrace = Join-Path $tempRoot 'worker-trace.log'
$webLog = Join-Path $tempRoot 'web.log'
$webErrorLog = Join-Path $tempRoot 'web-error.log'
$workerLog = Join-Path $tempRoot 'worker.log'
$workerErrorLog = Join-Path $tempRoot 'worker-error.log'
$secret = 'awake-batch-smoke-secret'
$workerProcess = $null
$webProcess = $null
$startedAt = [DateTime]::UtcNow
$evidence = [ordered]@{
    schema_version = 'awake.worldbook.batch-authoring-smoke.v1'
    started_at_utc = $startedAt.ToString('O')
    finished_at_utc = $null
    cases = @()
    network_boundary = 'loopback_only'
    game_directory_touched = $false
    generated_documents = @()
    passed = $false
    error = $null
}

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "WB-BATCH-SMOKE-FAIL: $Message" }
}

function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

if ($webPort -eq 0) { $webPort = Select-FreePort }

function Start-ChildProcess([string]$FilePath, [string[]]$ArgumentList, [hashtable]$Environment, [string]$WorkingDirectory, [string]$StdoutPath, [string]$StderrPath) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = (($ArgumentList | ForEach-Object { '"' + $_.Trim('"').Replace('"', '\"') + '"' }) -join ' ')
    foreach ($entry in $Environment.GetEnumerator()) { $startInfo.Environment[$entry.Key] = [string]$entry.Value }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.add_Exited({
        try { [IO.File]::WriteAllText($StdoutPath, $stdoutTask.Result) } catch { }
        try { [IO.File]::WriteAllText($StderrPath, $stderrTask.Result) } catch { }
    })
    return $process
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

function Read-Json([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { throw 'WB-BATCH-SMOKE-JSON: 响应为空。' }
    return $Text | ConvertFrom-Json
}

function Invoke-Multipart(
    [Microsoft.PowerShell.Commands.WebRequestSession]$Session,
    [string]$Uri,
    [hashtable]$Headers,
    [string]$RelativePath,
    [string]$DisplayName,
    [string]$FilePath) {
    Add-Type -AssemblyName System.Net.Http
    $cookieContainer = [Net.CookieContainer]::new()
    foreach ($cookie in $Session.Cookies.GetCookies([Uri]$Uri)) {
        $cookieContainer.Add([Uri]$Uri, $cookie)
    }
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.CookieContainer = $cookieContainer
    $client = [System.Net.Http.HttpClient]::new($handler)
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, $Uri)
    try {
        foreach ($key in $Headers.Keys) { [void]$request.Headers.TryAddWithoutValidation($key, [string]$Headers[$key]) }
        $multipart = [System.Net.Http.MultipartFormDataContent]::new()
        try {
            $multipart.Add([System.Net.Http.StringContent]::new($RelativePath, [Text.Encoding]::UTF8), 'relative_path')
            $multipart.Add([System.Net.Http.StringContent]::new($DisplayName, [Text.Encoding]::UTF8), 'display_name')
            $stream = [IO.File]::OpenRead($FilePath)
            try {
                $fileContent = [System.Net.Http.StreamContent]::new($stream)
                $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new('application/octet-stream')
                $multipart.Add($fileContent, 'file', [IO.Path]::GetFileName($FilePath))
                $request.Content = $multipart
                $response = $client.SendAsync($request).GetAwaiter().GetResult()
            }
            finally { $stream.Dispose() }
        }
        finally { $multipart.Dispose() }
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Content = $content }
    }
    finally {
        $request.Dispose()
        $client.Dispose()
        $handler.Dispose()
    }
}

function Invoke-Request([Microsoft.PowerShell.Commands.WebRequestSession]$Session, [string]$Method, [string]$Uri, [hashtable]$Headers, [object]$Body) {
    $requestHeaders = @{}
    if ($null -ne $Headers) { foreach ($key in $Headers.Keys) { $requestHeaders[$key] = $Headers[$key] } }
    if ($Method -eq 'GET') { [void]$requestHeaders.Remove('Origin') }
    $parameters = @{ WebSession = $Session; Method = $Method; Uri = $Uri; Headers = $requestHeaders; UseBasicParsing = $true; TimeoutSec = 30 }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $json = $Body | ConvertTo-Json -Compress -Depth 40
        $parameters.Body = [Text.Encoding]::UTF8.GetBytes($json)
    }
    try { return Invoke-WebRequest @parameters }
    catch {
        $response = $_.Exception.Response
        if ($null -eq $response) { throw }
        $content = $_.ErrorDetails.Message
        return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Content = $content }
    }
}

function Invoke-Json([Microsoft.PowerShell.Commands.WebRequestSession]$Session, [string]$Method, [string]$Uri, [hashtable]$Headers, [object]$Body) {
    $response = Invoke-Request $Session $Method $Uri $Headers $Body
    Assert ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) "HTTP $($response.StatusCode) $Method ${Uri}: $($response.Content)"
    return Read-Json $response.Content
}

function Get-Field($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($property) { return $property.Value }
    $snake = [Regex]::Replace($Name, '(?<!^)[A-Z]', { param($match) '_' + $match.Value.ToLowerInvariant() })
    $property = $Object.PSObject.Properties[$snake]
    if ($property) { return $property.Value }
    return $null
}

function New-Case([string]$Id, [hashtable]$Values = @{}) {
    $result = [ordered]@{ id = $Id; passed = $true }
    foreach ($key in $Values.Keys) { $result[$key] = $Values[$key] }
    return $result
}

try {
    Assert (Test-Path -LiteralPath $webProject -PathType Leaf) 'Web 项目文件不存在。'
    Assert (Test-Path -LiteralPath $schemaRoot -PathType Container) 'Studio schema 目录不存在。'
    $workerPort = Select-FreePort
    New-Item -ItemType Directory -Force -Path $tempRoot, $workspace | Out-Null

    $worker = @'
param([int]$Port, [string]$Secret, [string]$ReadyFile, [string]$TraceFile)
$ErrorActionPreference = 'Stop'
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Start()
[IO.File]::WriteAllText($ReadyFile, 'ready')
function Base64Url([byte[]]$Bytes) { [Convert]::ToBase64String($Bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=') }
function Sha256Text([string]$Text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).ToLowerInvariant() }
    finally { $sha.Dispose() }
}
function Signature([string]$Protocol, [string]$Nonce, [string]$WorkerId, [long]$Timestamp, [string]$RequestHash) {
    $payload = [Text.Encoding]::UTF8.GetBytes(($Protocol + '|' + $Nonce + '|' + $WorkerId + '|' + $Timestamp.ToString([Globalization.CultureInfo]::InvariantCulture) + '|' + $RequestHash))
    $buffer = [byte[]]::new(4 + $payload.Length)
    $length = $payload.Length
    $buffer[0] = [byte](($length -shr 24) -band 0xff); $buffer[1] = [byte](($length -shr 16) -band 0xff); $buffer[2] = [byte](($length -shr 8) -band 0xff); $buffer[3] = [byte]($length -band 0xff)
    [Array]::Copy($payload, 0, $buffer, 4, $payload.Length)
    $hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($Secret))
    try { Base64Url $hmac.ComputeHash($buffer) } finally { $hmac.Dispose() }
}
function Reply($Context, [int]$Status, $Value) {
    $bytes = [Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Compress -Depth 40))
    $Context.Response.StatusCode = $Status; $Context.Response.ContentType = 'application/json'; $Context.Response.ContentLength64 = $bytes.Length
    $Context.Response.OutputStream.Write($bytes, 0, $bytes.Length); $Context.Response.Close()
}
try {
    while ($true) {
        $context = $listener.GetContext()
        try {
            $reader = [IO.StreamReader]::new($context.Request.InputStream, $context.Request.ContentEncoding)
            try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $request = if ([string]::IsNullOrWhiteSpace($body)) { $null } else { $body | ConvertFrom-Json }
            [IO.File]::AppendAllText($TraceFile, "REQUEST $($context.Request.Url.AbsolutePath)`n$body`n")
            if ($context.Request.Url.AbsolutePath -eq '/awake/handshake') {
                $protocol = 'awake.worker.v1'; $workerId = 'batch-smoke-worker'; $timestamp = [long]$request.timestamp
                Reply $context 200 ([ordered]@{ protocol = $protocol; client_nonce = $request.client_nonce; worker_id = $workerId; timestamp = $timestamp; signature = (Signature $protocol $request.client_nonce $workerId $timestamp $request.request_hash) })
                continue
            }
            if ($context.Request.Url.AbsolutePath -ne '/awake/analyze') { Reply $context 404 ([ordered]@{ error = 'not found' }); continue }
            $wire = $request.request; $stage = [string]$wire.stage
            $quote = '西帝国西部有葱郁温暖的橡树林地。'
            $fact = [ordered]@{ id = 'fact.batch-smoke.1'; kind = 'geography'; text = $quote; certainty = 'confirmed'; inferred = $false; evidence = [ordered]@{ reference_id = 'source.batch-smoke'; locator = 'unit'; quote = $quote; quote_hash = (Sha256Text $quote) }; review_status = 'pending' }
            $metadata = [ordered]@{ title = '西帝国西部地貌'; summary = '西帝国西部以葱郁温暖的橡树林地为主要地貌。'; domain = 'geography'; subdomain = '地貌'; related_domains = @(); note = '' }
            $result = [ordered]@{ schema_version = 'worldbook.authoring-draft.result.v1'; stage = $stage; request_hash = [string]$wire.request_hash; source_content_hash = [string]$wire.source_content_hash; review_only = $true; facts = @(); metadata = $null; expressions = @(); warnings = @() }
            if ($stage -eq 'facts') { $result.facts = @($fact) }
            if ($stage -eq 'metadata') { $result.metadata = $metadata }
            Reply $context 200 $result
        } catch { try { Reply $context 500 ([ordered]@{ error = 'fake worker failure' }) } catch { } }
    }
} finally { $listener.Stop(); $listener.Close() }
'@
    [IO.File]::WriteAllText($workerScript, $worker, [Text.UTF8Encoding]::new($false))
    $pwsh = (Get-Command pwsh -ErrorAction Stop).Source
    $workerProcess = Start-ChildProcess $pwsh @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $workerScript, '-Port', $workerPort, '-Secret', $secret, '-ReadyFile', $workerReady, '-TraceFile', $workerTrace) @{} $tempRoot $workerLog $workerErrorLog
    for ($attempt = 0; $attempt -lt 60 -and -not (Test-Path -LiteralPath $workerReady); $attempt++) { Start-Sleep -Milliseconds 100 }
    Assert (Test-Path -LiteralPath $workerReady) '假 Worker 未启动。'

    $webEnvironment = @{
        AWAKE_WB_DEV_MODE = '1'; WORLD_BOOK_WORKSPACE = $workspace; WORLD_BOOK_SCHEMA_ROOT = $schemaRoot
        WORLD_BOOK_LOCAL_WORKER_URL = "http://127.0.0.1:$workerPort/"; WORLD_BOOK_LOCAL_WORKER_SECRET_ENV = 'AWAKE_BATCH_SMOKE_SECRET'
        AWAKE_BATCH_SMOKE_SECRET = $secret; AWAKE_WB_PORT = [string]$webPort; ASPNETCORE_ENVIRONMENT = 'Development'; DOTNET_ENVIRONMENT = 'Development'
    }
    $webProcess = Start-ChildProcess (Get-Command dotnet -ErrorAction Stop).Source @('run', '--project', ('"' + $webProject + '"'), '--configuration', 'Release', '--no-build', '--no-restore') $webEnvironment $root $webLog $webErrorLog
    $origin = "http://127.0.0.1:$webPort"
    $healthy = $false
    for ($attempt = 0; $attempt -lt 100 -and -not $healthy; $attempt++) { try { $healthy = (Invoke-WebRequest -Uri "$origin/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { Start-Sleep -Milliseconds 250 } }
    Assert $healthy 'Web Studio 未能启动到健康状态。'

    $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $bootstrap = Invoke-Json $session 'Post' "$origin/api/ai/session/bootstrap" @{ Origin = $origin } $null
    Assert ($bootstrap.ok -eq $true -and -not [string]::IsNullOrWhiteSpace($bootstrap.csrfToken)) 'AI 会话初始化失败。'
    $headers = @{ Origin = $origin; 'X-AWAKE-CSRF' = [string]$bootstrap.csrfToken }
    $unauthorized = Invoke-Request $session 'Post' "$origin/api/ai/authoring/batch/scan" @{} $null
    Assert ($unauthorized.StatusCode -eq 403) "缺少 Origin/CSRF 的写请求未被拒绝：$($unauthorized.StatusCode)"
    $evidence.cases += New-Case 'csrf-origin-rejection'

    $sourcePath = Join-Path $tempRoot 'reference.md'
    $sourceText = "西帝国西部有葱郁温暖的橡树林地。`n`n军团服役是当地平民脱贫的重要途径。"
    [IO.File]::WriteAllText($sourcePath, $sourceText, [Text.UTF8Encoding]::new($false))
    $scanResponse = Invoke-Multipart $session "$origin/api/ai/authoring/batch/scan" $headers 'references/empire.md' '帝国参考资料' $sourcePath
    Assert ($scanResponse.StatusCode -eq 200) "资料扫描失败：$($scanResponse.StatusCode) $($scanResponse.Content)"
    $scanEnvelope = Read-Json $scanResponse.Content
    $scan = $scanEnvelope.data
    $scanId = [string](Get-Field $scan 'scanId')
    $scanHash = [string](Get-Field $scan 'scanHash')
    $snapshotId = [string]$scan.snapshot_ids[0]
    Assert (-not [string]::IsNullOrWhiteSpace($scanId) -and $scan.snapshot_ids.Count -gt 0 -and $scanHash -match '^[A-Fa-f0-9]{64}$') '扫描结果缺少可创建批次的快照或哈希。'
    $evidence.cases += New-Case 'multipart-scan' @{ scan_id = $scanId; snapshot_count = [int]$scan.snapshot_ids.Count }

    $prebatchUnitsRoot = Join-Path $workspace "prebatches\$scanId\sources\$snapshotId.units"
    $prebatchUnitFile = Get-ChildItem -LiteralPath $prebatchUnitsRoot -Filter '*.json' -File | Select-Object -First 1
    Assert ($null -ne $prebatchUnitFile) '扫描没有生成可读取的 prebatch source unit。'
    $prebatchUnitId = [IO.Path]::GetFileNameWithoutExtension($prebatchUnitFile.Name)
    $prebatch = Invoke-Json $session 'Get' "$origin/api/ai/authoring/batch/prebatches/$scanId/sources/$snapshotId/units/$prebatchUnitId" $headers $null
    Assert ([string](Get-Field $prebatch.data 'sourceScope') -eq 'prebatch') 'prebatch source unit 没有返回公开来源范围。'
    $evidence.cases += New-Case 'prebatch-source-unit' @{ source_unit_id = $prebatchUnitId }

    $modelParameters = [ordered]@{ model = 'fixture-model'; temperature = 0.2; max_output_tokens = 512; reasoning_effort = 'low' }
    $createBody = [ordered]@{ scan_id = $scanId; expected_scan_hash = $scanHash; pipeline_revision = 'batch-authoring.v1'; provider_id = 'local'; model_parameters = $modelParameters; idempotency_key = ('a' * 64) }
    $created = Invoke-Json $session 'Post' "$origin/api/ai/authoring/batch/create" $headers $createBody
    $manifest = $created.data
    $batchId = [string](Get-Field $manifest 'batchId')
    $itemIds = @($manifest.item_ids | ForEach-Object { [string]$_ })
    Assert (-not [string]::IsNullOrWhiteSpace($batchId) -and $itemIds.Count -gt 0) '批次创建没有返回项目。'
    $duplicate = Invoke-Json $session 'Post' "$origin/api/ai/authoring/batch/create" $headers $createBody
    Assert ([string](Get-Field $duplicate.data 'batchId') -eq $batchId) '相同 idempotency key 没有返回同一个批次。'
    $evidence.cases += New-Case 'create-idempotent' @{ batch_id = $batchId; item_count = $itemIds.Count }

    $publicManifest = Invoke-Json $session 'Get' "$origin/api/ai/authoring/batch/$batchId" $headers $null
    Assert ($null -eq (Get-Field $publicManifest.data 'ownerId') -and $null -eq (Get-Field $publicManifest.data 'providerFingerprint')) '批次公开 manifest 泄露了内部字段。'
    $sourceUnit = Invoke-Json $session 'Get' "$origin/api/ai/authoring/batch/$batchId/sources/$snapshotId/units/$prebatchUnitId" $headers $null
    Assert ([string](Get-Field $sourceUnit.data 'sourceScope') -eq 'batch') '批次 source unit 没有返回公开来源范围。'
    $evidence.cases += New-Case 'batch-and-source-read'

    $consentBody = [ordered]@{ expected_revision = [int](Get-Field $manifest 'revision'); provider_id = 'local'; scope = 'facts_and_metadata'; model_parameters = $modelParameters; send_scope = 'all_snapshots'; authorized_item_ids = $itemIds }
    $consent = Invoke-Json $session 'Post' "$origin/api/ai/authoring/batch/$batchId/consent" $headers $consentBody
    $consentToken = [string](Get-Field $consent.data 'consentToken')
    $consentRevision = [int](Get-Field $consent.data.consent 'revision')
    Assert (-not [string]::IsNullOrWhiteSpace($consentToken)) '事实和元数据 consent 没有返回 token。'
    $evidence.cases += New-Case 'facts-and-metadata-consent'

    $startFactsBody = [ordered]@{ expected_revision = [int](Get-Field $manifest 'revision'); consent_token = $consentToken; claim_generation = [int](Get-Field $manifest 'claimGeneration'); consent_revision = $consentRevision; stage = 'facts'; target_item_ids = $itemIds }
    $startedFacts = Invoke-Json $session 'Post' "$origin/api/ai/authoring/batch/$batchId/start" $headers $startFactsBody
    $factsManifest = $startedFacts.data
    $itemDetail = Invoke-Json $session 'Get' "$origin/api/ai/authoring/batch/$batchId/items/$($itemIds[0])" $headers $null
    $facts = @($itemDetail.data.facts)
    $itemEvidence = @($itemDetail.data.evidence)
    $factIds = @($facts | ForEach-Object { [string](Get-Field $_ 'factId') })
    Assert ($facts.Count -gt 0 -and $factIds.Count -eq $facts.Count -and [string](Get-Field $itemDetail.data.item 'status') -eq 'facts_review') '事实阶段没有进入可审核状态。'
    Assert ($itemEvidence.Count -gt 0) '事实阶段没有返回原文证据。'
    $evidence.cases += New-Case 'facts-extraction-and-detail' @{ fact_count = $facts.Count }

    $reviewBody = [ordered]@{ expected_item_revision = [int](Get-Field $itemDetail.data.item 'revision'); fact_ids = $factIds; risk_level = 'green'; review_status = 'accepted'; fact_decisions = @($factIds | ForEach-Object { [ordered]@{ fact_id = $_; decision = 'accept' } }) }
    $review = Invoke-Json $session 'Post' "$origin/api/ai/authoring/batch/$batchId/items/$($itemIds[0])/review" $headers $reviewBody
    Assert ([string](Get-Field $review.data 'reviewStatus') -eq 'accepted') '事实审核没有进入 accepted。'
    $afterReviewManifest = (Invoke-Json $session 'Get' "$origin/api/ai/authoring/batch/$batchId" $headers $null).data
    $afterReviewItem = (Invoke-Json $session 'Get' "$origin/api/ai/authoring/batch/$batchId/items/$($itemIds[0])" $headers $null).data.item
    Assert ([string](Get-Field $afterReviewItem 'status') -eq 'metadata_pending') '接受事实后没有进入 metadata_pending。'
    $evidence.cases += New-Case 'facts-review'

    $metadataTargetItemIds = @($itemIds[0])
    $startMetadataBody = [ordered]@{ expected_revision = [int](Get-Field $afterReviewManifest 'revision'); consent_token = $consentToken; claim_generation = [int](Get-Field $afterReviewManifest 'claimGeneration'); consent_revision = $consentRevision; stage = 'metadata'; target_item_ids = $metadataTargetItemIds }
    $startedMetadata = Invoke-Json $session 'Post' "$origin/api/ai/authoring/batch/$batchId/start" $headers $startMetadataBody
    $metadataManifest = $startedMetadata.data
    $metadataDetail = Invoke-Json $session 'Get' "$origin/api/ai/authoring/batch/$batchId/items/$($itemIds[0])" $headers $null
    $metadataResults = @($metadataDetail.data.metadata_results)
    $metadata = $metadataResults | Select-Object -First 1
    $readyItem = $metadataDetail.data.item
    Assert ($metadataResults.Count -eq 1 -and [string](Get-Field $readyItem 'status') -eq 'ready_to_create') '元数据阶段没有生成可建档候选。'
    Assert ((Get-Field $metadata 'reviewOnly') -eq $true) '元数据结果没有保持 review-only。'
    $evidence.cases += New-Case 'metadata-extraction' @{ metadata_count = $metadataResults.Count }

    $selection = [ordered]@{
        title = [string](Get-Field $metadata 'title')
        summary = [string](Get-Field $metadata 'summary')
        domain = [string](Get-Field $metadata 'domain')
        subdomain = [string](Get-Field $metadata 'subdomain')
        metadata_selection_hash = [string](Get-Field $metadata 'metadataSelectionHash')
    }
    Assert ($selection.title -and $selection.summary -and $selection.domain -and $selection.subdomain -and $selection.metadata_selection_hash -match '^[A-Fa-f0-9]{64}$') '元数据候选缺少建档所需字段。'
    $createDocumentsBody = [ordered]@{ expected_revision = [int](Get-Field $metadataManifest 'revision'); create_mode = 'needs_review'; items = @([ordered]@{ item_id = $itemIds[0]; expected_item_revision = [int](Get-Field $readyItem 'revision'); fact_ids = $factIds; metadata_selection = $selection }) }
    $createdDocuments = Invoke-Json $session 'Post' "$origin/api/ai/authoring/batch/$batchId/create-documents" $headers $createDocumentsBody
    $createdItemResult = @($createdDocuments.data.items) | Select-Object -First 1
    Assert ([string](Get-Field $createdItemResult 'status') -eq 'created' -and -not [string]::IsNullOrWhiteSpace([string](Get-Field $createdItemResult 'documentId'))) '批量建档没有生成 needs_review 草稿。'
    $evidence.generated_documents += [string](Get-Field $createdItemResult 'documentId')
    $createdManifest = (Invoke-Json $session 'Get' "$origin/api/ai/authoring/batch/$batchId" $headers $null).data
    $createdItem = (Invoke-Json $session 'Get' "$origin/api/ai/authoring/batch/$batchId/items/$($itemIds[0])" $headers $null).data.item
    Assert ([string](Get-Field $createdItem 'status') -eq 'created' -and [string](Get-Field $createdItem 'documentId')) '建档后项目没有进入 created。'
    $evidence.cases += New-Case 'create-needs-review-document'

    $duplicateDocumentsBody = [ordered]@{ expected_revision = [int](Get-Field $createdManifest 'revision'); create_mode = 'needs_review'; items = @([ordered]@{ item_id = $itemIds[0]; expected_item_revision = [int](Get-Field $createdItem 'revision'); fact_ids = $factIds; metadata_selection = $selection }) }
    $duplicateDocuments = Invoke-Json $session 'Post' "$origin/api/ai/authoring/batch/$batchId/create-documents" $headers $duplicateDocumentsBody
    $duplicateItemResult = @($duplicateDocuments.data.items) | Select-Object -First 1
    Assert ([string](Get-Field $duplicateItemResult 'status') -eq 'already_exists') '重复批量建档没有返回 already_exists。'
    $report = Invoke-Json $session 'Get' "$origin/api/ai/authoring/batch/$batchId/report" $headers $null
    foreach ($countName in @('total','queued','running','review_pending','ready_to_create','created','failed','unknown_result')) { Assert ($null -ne $report.data.counts.$countName) "报告缺少固定计数字段 $countName。" }
    Assert ($null -eq $report.data.counts.skipped -and $null -eq $report.data.counts.cancelled) '报告暴露了未定义的 skipped/cancelled 计数。'
    $evidence.cases += New-Case 'duplicate-create-and-report'

    $authoringFiles = @(Get-ChildItem -LiteralPath (Join-Path $workspace 'authoring') -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[\\/]authoring[\\/](session-state|draft-state)[\\/]' })
    Assert ($authoringFiles.Count -gt 0) '工作区没有生成作者档案。'
    foreach ($file in $authoringFiles) {
        $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        Assert ($content -match 'needs_review') "生成档案没有 needs_review 状态：$($file.Name)"
        Assert ($content -notmatch '(?ms)^\s*expressions\s*:\s*\r?\n\s*-\s*' -and $content -notmatch '(?m)^\s*(profile_id|hero_id|family_id)\s*:') "批量 V1 意外生成了表达或人物/家族/身份绑定：$($file.Name)"
    }
    $evidence.cases += New-Case 'needs-review-and-no-bindings' @{ document_count = $authoringFiles.Count }

    $controlScanResponse = Invoke-Multipart $session "$origin/api/ai/authoring/batch/scan" $headers 'references/control.md' '控制路径参考资料' $sourcePath
    Assert ($controlScanResponse.StatusCode -eq 200) "控制路径扫描失败：$($controlScanResponse.StatusCode) $($controlScanResponse.Content)"
    $controlScan = (Read-Json $controlScanResponse.Content).data
    $controlScanId = [string](Get-Field $controlScan 'scanId')
    $controlScanHash = [string](Get-Field $controlScan 'scanHash')
    Assert ($controlScanId -and $controlScanHash -match '^[A-Fa-f0-9]{64}$') '控制路径扫描缺少可创建批次的哈希。'
    $controlBody = [ordered]@{ scan_id = $controlScanId; expected_scan_hash = $controlScanHash; pipeline_revision = 'batch-authoring.v1'; provider_id = 'local'; model_parameters = $modelParameters; idempotency_key = ('b' * 64) }
    $controlCreated = Invoke-Json $session 'Post' "$origin/api/ai/authoring/batch/create" $headers $controlBody
    $controlManifest = $controlCreated.data
    $controlBatchId = [string](Get-Field $controlManifest 'batchId')
    $controlItemId = [string]$controlManifest.item_ids[0]
    $paused = Invoke-Json $session 'Post' "$origin/api/ai/authoring/batch/$controlBatchId/pause" $headers ([ordered]@{ expected_revision = [int](Get-Field $controlManifest 'revision') })
    Assert ([string](Get-Field $paused.data 'status') -eq 'paused') '暂停路由没有进入 paused。'
    $cancelled = Invoke-Json $session 'Post' "$origin/api/ai/authoring/batch/$controlBatchId/cancel" $headers ([ordered]@{ expected_revision = [int](Get-Field $paused.data 'revision') })
    Assert ([string](Get-Field $cancelled.data 'status') -eq 'cancelled') '取消路由没有进入 cancelled。'
    $cancelledItem = (Invoke-Json $session 'Get' "$origin/api/ai/authoring/batch/$controlBatchId/items/$controlItemId" $headers $null).data.item
    Assert ([string](Get-Field $cancelledItem 'status') -eq 'skipped') '取消中的项目没有使用公开 skipped 状态。'
    $claimed = Invoke-Json $session 'Post' "$origin/api/ai/authoring/batch/$controlBatchId/claim" $headers ([ordered]@{ expected_revision = [int](Get-Field $cancelled.data 'revision'); claim_reason = 'manual_reclaim' })
    Assert ((Get-Field $claimed.data 'claimGeneration') -ge 1) 'claim 路由没有递增 claim_generation。'
    $retryResponse = Invoke-Request $session 'Post' "$origin/api/ai/authoring/batch/$controlBatchId/items/$controlItemId/retry" $headers ([ordered]@{ expected_item_revision = [int](Get-Field $cancelledItem 'revision'); manual_confirmation = $true; stage = 'facts' })
    Assert ($retryResponse.StatusCode -in @(409, 422)) '不满足条件的 retry 路由没有返回契约错误。'
    $evidence.cases += New-Case 'pause-cancel-claim-retry' @{ retry_status = [int]$retryResponse.StatusCode }

    $batchFiles = @(Get-ChildItem -LiteralPath $workspace -Recurse -File)
    $moduleFiles = @($batchFiles | Where-Object { $_.FullName -match '[\\/]Modules[\\/]' })
    Assert ($moduleFiles.Count -eq 0) 'Smoke 触碰了游戏 Modules 目录。'
    $evidence.passed = $true
}
catch {
    $evidence.error = $_.Exception.Message + " temp_root=$tempRoot"
    throw
}
finally {
    $evidence.finished_at_utc = [DateTime]::UtcNow.ToString('O')
    if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) {
        $evidenceDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($EvidencePath))
        New-Item -ItemType Directory -Force -Path $evidenceDirectory | Out-Null
        $evidence | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
    }
    Stop-Child $webProcess
    Stop-Child $workerProcess
    if (-not $KeepTemp) { try { if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force } } catch { } }
}

$evidence | ConvertTo-Json -Depth 40
if (-not $evidence.passed) { exit 1 }
