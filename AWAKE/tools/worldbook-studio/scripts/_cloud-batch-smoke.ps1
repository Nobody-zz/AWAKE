param(
    [string]$PackagePath = (Join-Path $PSScriptRoot '..\artifacts\current-test-pending-20260829-r7\WorldbookStudio'),
    [int]$MaxOutputTokens = 3000,
    [switch]$TwoUnits
)

$ErrorActionPreference = 'Stop'
$package = [IO.Path]::GetFullPath($PackagePath)
$web = Join-Path $package 'web\Awake.WorldbookStudio.Web.exe'
$schema = Join-Path $PSScriptRoot '..\..\docs\worldbook-studio-plan'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('awake-cloud-smoke-' + [Guid]::NewGuid().ToString('N'))
$workspace = Join-Path $temp 'workspace'
$log = Join-Path $temp 'web.log'
$err = Join-Path $temp 'web.err.log'
New-Item -ItemType Directory -Force -Path $workspace | Out-Null
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$environment = @{
    AWAKE_WB_DEV_MODE = '1'
    WORLD_BOOK_WORKSPACE = $workspace
    WORLD_BOOK_SCHEMA_ROOT = [IO.Path]::GetFullPath($schema)
    AWAKE_WB_PORT = [string]$port
    ASPNETCORE_ENVIRONMENT = 'Production'
    DOTNET_ENVIRONMENT = 'Production'
}
$process = $null

function Read-Json([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) { throw '响应为空。' }
    $text | ConvertFrom-Json
}

function Invoke-Api([string]$method, [string]$uri, [hashtable]$headers, [object]$body) {
    $parameters = @{
        Method = $method
        Uri = $uri
        Headers = $headers
        WebSession = $script:session
        UseBasicParsing = $true
        TimeoutSec = 120
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $body | ConvertTo-Json -Compress -Depth 40
    }
    $response = Invoke-WebRequest @parameters
    $json = $null
    try { $json = Read-Json $response.Content } catch { }
    [pscustomobject]@{
        Status = [int]$response.StatusCode
        Json = $json
        Raw = $response.Content
    }
}

try {
    if (-not (Test-Path -LiteralPath $web -PathType Leaf)) { throw "找不到 Web：$web" }
    $process = Start-Process -FilePath $web -WorkingDirectory (Join-Path $package 'web') -Environment $environment -RedirectStandardOutput $log -RedirectStandardError $err -WindowStyle Hidden -PassThru
    $origin = "http://127.0.0.1:$port"
    $healthy = $false
    for ($attempt = 0; $attempt -lt 120 -and -not $healthy; $attempt++) {
        try { $healthy = (Invoke-WebRequest -Uri "$origin/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 }
        catch { Start-Sleep -Milliseconds 250 }
    }
    if (-not $healthy) { throw 'Web 未进入健康状态。' }
    Write-Output "HEALTH PASS port=$port"

    $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $bootstrap = Invoke-Api 'Post' "$origin/api/ai/session/bootstrap" @{ Origin = $origin } $null
    if ($bootstrap.Status -ne 200 -or $bootstrap.Json.ok -ne $true) { throw "会话初始化失败：$($bootstrap.Raw)" }
    $headers = @{ Origin = $origin; 'X-AWAKE-CSRF' = [string]$bootstrap.Json.csrfToken }
    $providers = Invoke-Api 'Get' "$origin/api/ai/providers" $headers $null
    Write-Output 'PROVIDERS:'
    $providers.Json | ConvertTo-Json -Depth 10
    $cloud = @($providers.Json.providers) | Where-Object { $_.providerId -eq 'cloud' } | Select-Object -First 1
    if ($null -eq $cloud -or $cloud.state -ne 'configured') { throw '云端 Provider 未配置。' }

    $sourcePath = Join-Path $temp 'one.md'
    $sourceText = if ($TwoUnits) {
        "西帝国位于帝国西部。`n`n西部军团长期在边境征募士兵。"
    } else {
        "西帝国位于帝国西部，西部军团长期在边境征募士兵。"
    }
    [IO.File]::WriteAllText($sourcePath, $sourceText, [Text.UTF8Encoding]::new($false))
    $scan = Invoke-WebRequest -Method Post -Uri "$origin/api/ai/authoring/batch/scan" -WebSession $session -Headers $headers -Form @{
        relative_path = 'references/one.md'
        display_name = '最小真实云端资料'
        file = Get-Item -LiteralPath $sourcePath
    } -UseBasicParsing -TimeoutSec 120 -SkipHttpErrorCheck
    if ($scan.StatusCode -lt 200 -or $scan.StatusCode -ge 300) { throw "扫描失败：$($scan.Content)" }
    $scanData = (Read-Json $scan.Content).data
    Write-Output 'SCAN DATA:'
    $scanData | ConvertTo-Json -Depth 12
    if ($MaxOutputTokens -lt 128 -or $MaxOutputTokens -gt 8000) { throw 'MaxOutputTokens 必须在 128 到 8000 之间。' }
    $model = [ordered]@{ model = [string]$cloud.model; temperature = 0; max_output_tokens = $MaxOutputTokens; reasoning_effort = 'low' }
    $create = Invoke-Api 'Post' "$origin/api/ai/authoring/batch/create" $headers ([ordered]@{
        scan_id = [string]$scanData.scan_id
        expected_scan_hash = [string]$scanData.scan_hash
        pipeline_revision = 'batch-authoring.v1'
        provider_id = 'cloud'
        model_parameters = $model
        idempotency_key = ('c' * 64)
    })
    if ($create.Status -lt 200 -or $create.Status -ge 300) { throw "创建批次失败：$($create.Raw)" }
    $manifest = $create.Json.data
    $batchId = [string]$manifest.batch_id
    $itemIds = @($manifest.item_ids | ForEach-Object { [string]$_ })
    Write-Output "CREATE PASS batch=$batchId items=$($itemIds.Count)"

    $consent = Invoke-Api 'Post' "$origin/api/ai/authoring/batch/$batchId/consent" $headers ([ordered]@{
        expected_revision = [int]$manifest.revision
        provider_id = 'cloud'
        scope = 'facts_and_metadata'
        model_parameters = $model
        send_scope = 'all_snapshots'
        authorized_item_ids = $itemIds
    })
    if ($consent.Status -lt 200 -or $consent.Status -ge 300) { throw "授权失败：$($consent.Raw)" }
    $token = [string]$consent.Json.data.consent_token
    $consentRevision = [int]$consent.Json.data.consent.revision
    Write-Output 'CONSENT PASS'

    $start = Invoke-Api 'Post' "$origin/api/ai/authoring/batch/$batchId/start" $headers ([ordered]@{
        expected_revision = [int]$manifest.revision
        consent_token = $token
        claim_generation = [int]$manifest.claim_generation
        consent_revision = $consentRevision
        stage = 'facts'
        target_item_ids = $itemIds
    })
    if ($start.Status -lt 200 -or $start.Status -ge 300) { throw "启动提取失败：$($start.Raw)" }
    Write-Output 'START PASS'

    $detail = Invoke-Api 'Get' "$origin/api/ai/authoring/batch/$batchId/items/$($itemIds[0])" $headers $null
    Write-Output 'DETAIL:'
    $detail.Json | ConvertTo-Json -Depth 30
    $report = Invoke-Api 'Get' "$origin/api/ai/authoring/batch/$batchId/report" $headers $null
    Write-Output 'REPORT:'
    $report.Json | ConvertTo-Json -Depth 20
    $item = $detail.Json.data.item
    if ([string]$item.status -eq 'failed') { throw "云端提取失败：code=$($item.lastErrorCode) message=$($item.lastErrorMessage)" }
    if ([string]$item.status -ne 'facts_review') { throw "提取未进入 facts_review：status=$($item.status)" }
    Write-Output 'CLOUD_BATCH_SMOKE_PASS'
}
catch {
    Write-Output "CLOUD_BATCH_SMOKE_FAIL: $($_.Exception.Message)"
    if ($null -ne $process) {
        Write-Output 'WEB_STDOUT:'
        Get-Content -LiteralPath $log -Tail 50 -ErrorAction SilentlyContinue
        Write-Output 'WEB_STDERR:'
        Get-Content -LiteralPath $err -Tail 50 -ErrorAction SilentlyContinue
    }
    exit 1
}
finally {
    if ($null -ne $process) {
        try { if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit(5000) } } catch { }
        try { $process.Dispose() } catch { }
    }
    try { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue } catch { }
}
