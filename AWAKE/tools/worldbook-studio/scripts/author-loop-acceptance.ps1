param(
    [string]$EvidencePath = '..\..\docs\evidence\WORLDBOOKSTUDIO-AUTHOR-LOOP-ACCEPTANCE.json'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$package = Join-Path $root 'artifacts\current-test\WorldbookStudio'
$webExe = Join-Path $package 'web\Awake.WorldbookStudio.Web.exe'
if (-not (Test-Path -LiteralPath $webExe)) { throw '缺少打包版 Web，请先运行 scripts\package.ps1。' }
$evidenceFile = [IO.Path]::GetFullPath((Join-Path $root $EvidencePath))
$generation = Get-Content -LiteralPath (Join-Path $root '_tmp\pravend-real-worker-evidence.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $generation.document_created -or [string]::IsNullOrWhiteSpace($generation.workspace)) {
    throw '请先运行 _tmp\real-worker-pravend.ps1 -KeepWorkspace，生成一份可继续编辑的待审档案。'
}
$workspace = [string]$generation.workspace
$documentPath = [string]$generation.document_path

$steps = New-Object System.Collections.Generic.List[object]
$notes = New-Object System.Collections.Generic.List[string]
$result = $null
function Step([string]$id, [scriptblock]$body) {
    $start = Get-Date
    try {
        $detail = & $body
        $steps.Add([ordered]@{ id = $id; passed = $true; seconds = [Math]::Round(((Get-Date) - $start).TotalSeconds, 2); detail = $detail })
        Write-Output ('PASS ' + $id)
    }
    catch {
        $steps.Add([ordered]@{ id = $id; passed = $false; seconds = [Math]::Round(((Get-Date) - $start).TotalSeconds, 2); detail = $_.Exception.Message })
        Write-Output ('FAIL ' + $id + ' :: ' + $_.Exception.Message)
        throw
    }
}
function Select-FreePort { $l = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0); try { $l.Start(); return ([Net.IPEndPoint]$l.LocalEndpoint).Port } finally { $l.Stop() } }
function Invoke-Json([string]$method, [string]$uri, $session, [hashtable]$headers, $body) {
    (Invoke-WebRequest -Method $method -Uri $uri -WebSession $session -Headers $headers -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress -Depth 40) -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 120)
}

$port = Select-FreePort
$env:AWAKE_WB_DEV_MODE = '1'
$env:WORLD_BOOK_WORKSPACE = $workspace
$env:WORLD_BOOK_SCHEMA_ROOT = (Join-Path $package 'schemas')
$env:AWAKE_WB_PORT = [string]$port
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$web = Start-Process -FilePath $webExe -WorkingDirectory (Join-Path $package 'web') -WindowStyle Hidden -PassThru
$origin = "http://127.0.0.1:$port"
try {
    $healthy = $false
    for ($i = 0; $i -lt 120 -and -not $healthy; $i++) {
        try { $healthy = (Invoke-WebRequest "$origin/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { Start-Sleep -Milliseconds 250 }
    }
    if (-not $healthy) { throw '打包版工作室未能启动。' }

    $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $bootstrap = Invoke-WebRequest "$origin/api/ai/session/bootstrap" -Method Post -UseBasicParsing -WebSession $session -Headers @{ Origin = $origin }
    $csrf = ($bootstrap.Content | ConvertFrom-Json).csrfToken
    $headers = @{ Origin = $origin; 'X-AWAKE-CSRF' = $csrf }

    $model = $null
    Step 'author-opens-ai-document' {
        $response = Invoke-RestMethod -Uri ("$origin/api/editor-document?path=" + [Uri]::EscapeDataString($documentPath)) -TimeoutSec 30
        $script:model = $response.model
        if ($model.status -ne 'needs_review') { throw ('草稿状态不是 needs_review：' + $model.status) }
        $eraKey = [string]$model.era.key
        $subdomain = [string]$model.subdomain
        $assertionCount = @($model.assertions).Count
        if (-not $eraKey) { throw '作者表单没有显示时期(era)。' }
        $script:notes.Add(('作者表单字段：时期=' + $eraKey + '，二级主题=' + ($(if ($subdomain) { $subdomain } else { '（空，需要作者选择）' })) + '，事实数=' + $assertionCount))
        if (-not $subdomain) { $notes.Add('体验问题：AI 建档后二级主题为空，作者必须手动补选。') }
        return ('era=' + $eraKey + ' subdomain=' + $subdomain + ' assertions=' + $assertionCount)
    }

    $raw = $null
    Step 'author-loads-raw-document' {
        $script:raw = Invoke-RestMethod -Uri ("$origin/api/document?path=" + [Uri]::EscapeDataString($documentPath)) -TimeoutSec 30
        if (-not $raw.content) { throw '原始文档内容为空。' }
        return ('revision=' + [int]$raw.document.revision + ' hash=' + [string]$raw.report.inputHash)
    }

    Step 'author-saves-edited-document' {
        $beforeRevision = [int]$raw.document.revision
        $beforeHash = [string]$raw.report.inputHash
        $content = [string]$raw.content
        if ($content -match 'certainty: unknown') { $edited = [regex]::Replace($content, 'certainty: unknown', 'certainty: bounded', 1); $expect = 'certainty: bounded' }
        elseif ($content -match 'certainty: bounded') { $edited = [regex]::Replace($content, 'certainty: bounded', 'certainty: unknown', 1); $expect = 'certainty: unknown' }
        else { throw '文档里没有可编辑的 certainty 字段。' }
        $saved = Invoke-Json 'Post' "$origin/api/authoring/save-authoring" $session $headers @{ path = $documentPath; content = $edited; sourceHash = $beforeHash; revision = $beforeRevision }
        if ($saved.StatusCode -ne 200) { throw ('保存失败：' + $saved.StatusCode + ' ' + $saved.Content) }
        $body = $saved.Content | ConvertFrom-Json
        if (-not $body.ok -or [int]$body.revision -ne ($beforeRevision + 1)) { throw ('revision 未推进：' + [string]$body.revision) }
        $after = Invoke-RestMethod -Uri ("$origin/api/document?path=" + [Uri]::EscapeDataString($documentPath)) -TimeoutSec 30
        if ([string]$after.content -notmatch [regex]::Escape($expect)) { throw '保存后的文档没有保留作者的编辑。' }
        if ([string]$after.document.era.key -ne [string]$model.era.key) { throw '保存后时期(era)发生变化。' }
        return ('revision ' + $beforeRevision + ' -> ' + [int]$body.revision)
    }

    Step 'author-validation-passes' {
        $current = Invoke-RestMethod -Uri ("$origin/api/document?path=" + [Uri]::EscapeDataString($documentPath)) -TimeoutSec 30
        $hash = [string]$current.report.inputHash
        $revision = [int]$current.document.revision
        $uri = "$origin/api/validate?path=" + [Uri]::EscapeDataString($documentPath) + '&sourceHash=' + [Uri]::EscapeDataString($hash) + '&revision=' + $revision
        $report = Invoke-Json 'Post' $uri $session $headers @{}
        if ($report.StatusCode -ne 200) { throw ('校验请求失败：' + $report.StatusCode + ' ' + $report.Content) }
        $payload = $report.Content | ConvertFrom-Json
        $diagnostics = @($payload.Diagnostics)
        $errors = @($diagnostics | Where-Object { [string]$_.Severity -eq 'error' })
        if (-not $payload.Valid -or $errors.Count -gt 0) { throw ('校验未通过：Valid=' + $payload.Valid + ' errors=' + $errors.Count) }
        return ('Valid=true；诊断=' + $diagnostics.Count + '；revision=' + $revision)
    }

    $failed = @($steps | Where-Object { -not $_.passed }).Count
    $result = [ordered]@{
        schema_version = 'awake.worldbook.author-loop-acceptance.v1'
        finished_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
        workspace = $workspace
        document_path = $documentPath
        steps = $steps
        notes = $notes
        passed = ($failed -eq 0)
    }
}
finally {
    try { if (-not $web.HasExited) { $web.Kill($true) } } catch { }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidenceFile) | Out-Null
    if ($null -eq $result) {
        $result = [ordered]@{
            schema_version = 'awake.worldbook.author-loop-acceptance.v1'
            finished_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
            workspace = $workspace
            document_path = $documentPath
            steps = $steps
            notes = $notes
            passed = $false
        }
    }
    ($result | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $evidenceFile -Encoding UTF8
    Write-Output ("evidence=" + $evidenceFile)
}
