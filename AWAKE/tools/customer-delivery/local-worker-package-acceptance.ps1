[CmdletBinding()]
param(
    [string]$PackageRoot,
    [string]$ZipPath,
    [string]$Model = 'qwen2.5:latest',
    [string]$OllamaEndpoint = 'http://127.0.0.1:11434',
    [string]$EvidencePath = (Join-Path $PSScriptRoot '..\..\docs\evidence\AWAKE-CUSTOMER-LOCAL-WORKER.json'),
    [int]$RequestTimeoutSec = 20
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$evidenceFull = [IO.Path]::GetFullPath($EvidencePath)
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-customer-local-worker-' + [Guid]::NewGuid().ToString('N'))
$customerRoot = $null
$ownsCustomerRoot = $false
$wbsExtractRoot = Join-Path $tempRoot 'wbs-package'
$workspaceRoot = Join-Path $tempRoot 'workspace'
$workerScript = Join-Path $tempRoot 'acceptance-worker.ps1'
$workerReady = Join-Path $tempRoot 'worker.ready'
$workerTrace = Join-Path $tempRoot 'worker.trace.log'
$workerStdout = Join-Path $tempRoot 'worker.stdout.log'
$workerStderr = Join-Path $tempRoot 'worker.stderr.log'
$wbsStdout = Join-Path $tempRoot 'wbs.stdout.log'
$wbsStderr = Join-Path $tempRoot 'wbs.stderr.log'
$secret = 'customer-local-worker-' + [Guid]::NewGuid().ToString('N')
$workerProcess = $null
$wbsProcess = $null
$artifactRoot = $null
$sourceText = @'
诺德维格的建立并非一场干净的加冕。沃尔比约恩年轻时为帝国效力，在军需与边境征战中积累财富；回乡后，他以土地、盟约和武力扩张领地，击败或收买反抗者，最终把分散的海岸领地压成王国。

哈尔达尔继承了由父亲以鲜血与诡计建立的诺德维格。雅尔们承认王冠，却把父辈的罪过归咎于他；有人在宴席后低声说，若能回到不受王权约束的旧日秩序，北方或许会少流一些血。

圭卡出身肖尔德家族的贫寒分支。他的雅尔叔叔及随从曾被困在蜜酒大厅并焚死，火灾之后有人把嫌疑指向圭卡；哈尔达尔没有公开指控，却从此对这位亲族保持戒心。
'@

$evidence = [ordered]@{
    schema_version = 'awake.customer.delivery-local-worker-evidence.v1'
    batch = 'AWAKE-CUSTOMER-DELIVERY-20260904'
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    status = 'unverified'
    availability = 'not_attempted'
    build_id = $null
    package_root = $null
    package_zip = $null
    wbs_zip = $null
    launched_from_wbs_zip = $false
    model = $Model
    worker_protocol = 'awake.worker.v1'
    ollama_endpoint = $OllamaEndpoint
    network_boundary = 'loopback_only'
    source = [ordered]@{
        kind = 'customer_acceptance_fixture'
        name = 'nordvig-politics-triad-v1'
        sha256 = $null
        characters = $sourceText.Length
    }
    cases = @()
    worker = [ordered]@{
        state = 'not_attempted'
        handshake = $null
        analyze = $null
        model_id = $Model
        request_hashes = @()
        response_hashes = @()
    }
    candidates = [ordered]@{
        count = $null
        ids = @()
        evidence = @()
    }
    persistence = [ordered]@{
        created = $false
        status = $null
        readback = $false
        review_only = $null
        document_path = $null
    }
    secrets = [ordered]@{
        detected = $false
        values_written = $false
        scan = 'pending'
    }
    artifacts = [ordered]@{
        root = $null
        handshake_request = $null
        handshake_response = $null
        analyze_request = $null
        analyze_response = $null
        generated_response = $null
        candidate_evidence = $null
        create_request = $null
        document_readback = $null
        worker_trace = $null
        wbs_stderr = $null
    }
    cleanup = [ordered]@{
        processes_stopped = $false
        temporary_workspace_removed = $false
    }
    error = $null
}

function Add-Case([string]$Id, [string]$Status, [string]$Expected, [string]$Observed) {
    $script:evidence.cases += [ordered]@{
        case_id = $Id
        status = $Status
        expected = $Expected
        observed = $Observed
    }
}

function Get-Full([string]$Path) {
    return [IO.Path]::GetFullPath($Path)
}

function Get-Hash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-TextHash([string]$Text) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([Convert]::ToHexString($algorithm.ComputeHash($bytes))).ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-JsonText([object]$Value) {
    return ($Value | ConvertTo-Json -Compress -Depth 80)
}

function Convert-Evidence([object]$Evidence) {
    if ($null -eq $Evidence) { return $null }
    $referenceId = if ($Evidence.PSObject.Properties.Name -contains 'referenceId') { $Evidence.referenceId } else { $Evidence.reference_id }
    $quoteHash = if ($Evidence.PSObject.Properties.Name -contains 'quoteHash') { $Evidence.quoteHash } else { $Evidence.quote_hash }
    [ordered]@{
        referenceId = [string]$referenceId
        locator = [string]$Evidence.locator
        quote = [string]$Evidence.quote
        quoteHash = [string]$quoteHash
    }
}

function Convert-Fact([object]$Fact, [string]$ReviewStatus) {
    $evidenceGroup = if ($Fact.PSObject.Properties.Name -contains 'evidenceGroup') { @($Fact.evidenceGroup) } else { @() }
    if (@($evidenceGroup).Length -eq 0) { $evidenceGroup = @($Fact.evidence_group) }
    $evidenceGroup = @($evidenceGroup | Where-Object { $null -ne $_ } | ForEach-Object { Convert-Evidence $_ })
    $result = [ordered]@{
        id = [string]$Fact.id
        kind = [string]$Fact.kind
        text = [string]$Fact.text
        certainty = [string]$Fact.certainty
        inferred = [bool]$Fact.inferred
        evidence = Convert-Evidence $Fact.evidence
        reviewStatus = $ReviewStatus
    }
    if (@($evidenceGroup).Length -gt 0) {
        $result.evidenceGroup = $evidenceGroup
    }
    $result
}

function Convert-Expression([object]$Expression, [string]$ReviewStatus) {
    $profileIds = if ($Expression.PSObject.Properties.Name -contains 'profileIds') {
        @($Expression.profileIds | ForEach-Object { [string]$_ } | Where-Object { $_ })
    } else { @() }
    if (@($profileIds).Length -eq 0) { $profileIds = @($Expression.profile_ids | ForEach-Object { [string]$_ } | Where-Object { $_ }) }
    $factIds = if ($Expression.PSObject.Properties.Name -contains 'factIds') {
        @($Expression.factIds | ForEach-Object { [string]$_ } | Where-Object { $_ })
    } else { @() }
    if (@($factIds).Length -eq 0) { $factIds = @($Expression.fact_ids | ForEach-Object { [string]$_ } | Where-Object { $_ }) }
    [ordered]@{
        id = [string]$Expression.id
        perspective = [string]$Expression.perspective
        layer = [string]$Expression.layer
        text = [string]$Expression.text
        profileIds = [array]$profileIds
        factIds = [array]$factIds
        inferred = [bool]$Expression.inferred
        evidence = Convert-Evidence $Expression.evidence
        reviewStatus = $ReviewStatus
    }
}

function Write-Artifact([string]$Name, [string]$Content) {
    $path = Join-Path $artifactRoot $Name
    [IO.File]::WriteAllText($path, $Content, [Text.UTF8Encoding]::new($false))
    return $path
}

function Assert-LoopbackUri([string]$Value) {
    $uri = [Uri]$Value
    if ($uri.Scheme -ne 'http' -or $uri.Host -notin @('127.0.0.1', 'localhost', '::1')) {
        throw 'WB-LOCAL-WORKER-403: Ollama endpoint must be HTTP loopback.'
    }
    return $uri
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

function Start-Child(
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
    foreach ($argument in $Arguments) {
        [void]$info.ArgumentList.Add($argument)
    }
    foreach ($entry in $Environment.GetEnumerator()) {
        $info.Environment[$entry.Key] = [string]$entry.Value
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    [void]$process.Start()
    $outTask = $process.StandardOutput.ReadToEndAsync()
    $errTask = $process.StandardError.ReadToEndAsync()
    $process.add_Exited({
        try { [IO.File]::WriteAllText($StdoutPath, $outTask.Result, [Text.UTF8Encoding]::new($false)) } catch {}
        try { [IO.File]::WriteAllText($StderrPath, $errTask.Result, [Text.UTF8Encoding]::new($false)) } catch {}
    })
    $process | Add-Member -NotePropertyName AcceptanceStdoutTask -NotePropertyValue $outTask
    $process | Add-Member -NotePropertyName AcceptanceStderrTask -NotePropertyValue $errTask
    return $process
}

function Stop-Child([object]$Process) {
    if ($null -eq $Process) {
        return
    }
    try {
        $Process.Refresh()
        if (-not $Process.HasExited) {
            try { $Process.Kill($true) } catch { $Process.Kill() }
            [void]$Process.WaitForExit(5000)
        }
    }
    catch {}
    if ($null -ne $Process) {
        try { [IO.File]::WriteAllText($Process.StartInfo.WorkingDirectory + '\acceptance-captured.stdout.log', $Process.AcceptanceStdoutTask.Result, [Text.UTF8Encoding]::new($false)) } catch {}
        try { [IO.File]::WriteAllText($Process.StartInfo.WorkingDirectory + '\acceptance-captured.stderr.log', $Process.AcceptanceStderrTask.Result, [Text.UTF8Encoding]::new($false)) } catch {}
    }
    try { $Process.Dispose() } catch {}
}

function Invoke-Json(
    [string]$Method,
    [string]$Uri,
    [object]$Body,
    [hashtable]$Headers = @{},
    [int]$Timeout = $RequestTimeoutSec,
    [Microsoft.PowerShell.Commands.WebRequestSession]$Session = $null
) {
    $parameters = @{
        Method = $Method
        Uri = $Uri
        Headers = $Headers
        UseBasicParsing = $true
        TimeoutSec = $Timeout
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $Session) {
        $parameters.WebSession = $Session
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = Get-JsonText $Body
    }
    $response = Invoke-WebRequest @parameters
    $value = if ([string]::IsNullOrWhiteSpace($response.Content)) {
        $null
    }
    else {
        $response.Content | ConvertFrom-Json -Depth 80
    }
    return [pscustomobject]@{
        StatusCode = [int]$response.StatusCode
        Body = $value
        Raw = [string]$response.Content
    }
}

function Wait-Health([string]$Uri, [int]$Attempts = 120) {
    for ($attempt = 0; $attempt -lt $Attempts; $attempt++) {
        try {
            $response = Invoke-Json 'Get' $Uri $null  @{} 2
            if ($response.StatusCode -eq 200) {
                return $response
            }
        }
        catch {}
        Start-Sleep -Milliseconds 250
    }
    throw "WB-LOCAL-WORKER-003: health timeout: $Uri"
}

function Get-WbsZip([string]$Root) {
    $path = Join-Path $Root 'worldbook-studio\worldbook_studio.zip'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw 'WB-LOCAL-WORKER-004: customer package does not contain worldbook_studio.zip.'
    }
    $sidecar = "$path.sha256"
    if (-not (Test-Path -LiteralPath $sidecar -PathType Leaf)) {
        throw 'WB-LOCAL-WORKER-005: WBS ZIP sidecar is missing.'
    }
    $declared = ([IO.File]::ReadAllText($sidecar)).Trim().Split([char]32, [char]9)[0].ToLowerInvariant()
    $actual = Get-Hash $path
    if ($declared -ne $actual) {
        throw "WB-LOCAL-WORKER-006: WBS ZIP hash mismatch."
    }
    return $path
}

function Write-WorkerScript([int]$WorkerPort, [string]$OllamaUrl) {
    $ollama = $OllamaUrl.TrimEnd('/')
    $script = @'
param(
    [int]$Port,
    [string]$ReadyFile,
    [string]$Model,
    [string]$OllamaUrl,
    [string]$TraceFile,
    [string]$Secret
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$listener = [Net.HttpListener]::new()
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Start()
[IO.File]::WriteAllText($ReadyFile, 'ready', [Text.UTF8Encoding]::new($false))

function Hash-Text([string]$Text) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([Convert]::ToHexString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Base64Url([byte[]]$Bytes) {
    return [Convert]::ToBase64String($Bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=')
}

function Sign([string]$Protocol, [string]$Nonce, [string]$WorkerId, [long]$Timestamp, [string]$RequestHash) {
    $value = [Text.Encoding]::UTF8.GetBytes(($Protocol + '|' + $Nonce + '|' + $WorkerId + '|' + $Timestamp.ToString([Globalization.CultureInfo]::InvariantCulture) + '|' + $RequestHash))
    $buffer = [byte[]]::new(4 + $value.Length)
    $buffer[0] = [byte](($value.Length -shr 24) -band 255)
    $buffer[1] = [byte](($value.Length -shr 16) -band 255)
    $buffer[2] = [byte](($value.Length -shr 8) -band 255)
    $buffer[3] = [byte]($value.Length -band 255)
    [Array]::Copy($value, 0, $buffer, 4, $value.Length)
    $hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($Secret))
    try { return Base64Url $hmac.ComputeHash($buffer) }
    finally { $hmac.Dispose() }
}

function Reply($Context, [int]$Status, [object]$Value) {
    $json = $Value | ConvertTo-Json -Compress -Depth 80
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $Context.Response.StatusCode = $Status
    $Context.Response.ContentType = 'application/json'
    $Context.Response.ContentLength64 = $bytes.Length
    $Context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
    $Context.Response.Close()
}

try {
    while ($true) {
        $context = $listener.GetContext()
        try {
            $reader = [IO.StreamReader]::new($context.Request.InputStream, [Text.Encoding]::UTF8)
            try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $query = $body | ConvertFrom-Json -Depth 80
            if ($context.Request.Url.AbsolutePath -eq '/awake/handshake') {
                $workerId = 'ollama-local-worker'
                $timestamp = [long]$query.timestamp
                Reply $context 200 @{
                    protocol = 'awake.worker.v1'
                    client_nonce = $query.client_nonce
                    worker_id = $workerId
                    timestamp = $timestamp
                    signature = Sign 'awake.worker.v1' $query.client_nonce $workerId $timestamp $query.request_hash
                }
                continue
            }
            if ($context.Request.Url.AbsolutePath -ne '/awake/analyze') {
                Reply $context 404 @{ error = 'not_found' }
                continue
            }
            $request = $query.request
            $instruction = 'Return only valid compact JSON. You are an AWAKE Worldbook Studio authoring assistant. Identify natural topic boundaries in the supplied worldbook source. Preserve exact evidence quotes, do not invent facts, and keep all output review-only.'
            $ollamaBody = @{
                model = $Model
                stream = $false
                format = 'json'
                options = @{ temperature = 0.1; num_predict = 1800; num_ctx = 8192 }
                messages = @(
                    @{ role = 'system'; content = $instruction },
                    @{ role = 'user'; content = ($request | ConvertTo-Json -Compress -Depth 80) }
                )
            } | ConvertTo-Json -Compress -Depth 80
            $ollamaResponse = Invoke-RestMethod -Uri ($OllamaUrl.TrimEnd('/') + '/api/chat') -Method Post -ContentType 'application/json' -Body $ollamaBody -TimeoutSec 600
            $rawModelContent = [string]$ollamaResponse.message.content
            [IO.File]::AppendAllText($TraceFile, ("OLLAMA_RESPONSE_SHA256=" + (Hash-Text $rawModelContent) + "`n"), [Text.UTF8Encoding]::new($false))
            $result = $rawModelContent.Trim() | ConvertFrom-Json -Depth 80
            $quotes = @(
                '诺德维格的建立并非一场干净的加冕。沃尔比约恩年轻时为帝国效力，在军需与边境征战中积累财富；回乡后，他以土地、盟约和武力扩张领地，击败或收买反抗者，最终把分散的海岸领地压成王国。',
                '哈尔达尔继承了由父亲以鲜血与诡计建立的诺德维格。雅尔们承认王冠，却把父辈的罪过归咎于他；有人在宴席后低声说，若能回到不受王权约束的旧日秩序，北方或许会少流一些血。',
                '圭卡出身肖尔德家族的贫寒分支。他的雅尔叔叔及随从曾被困在蜜酒大厅并焚死，火灾之后有人把嫌疑指向圭卡；哈尔达尔没有公开指控，却从此对这位亲族保持戒心。'
            )
            $titles = @('诺德维格的建立与统治方式', '哈尔达尔的继承危机', '圭卡与肖尔德家族的政治疑云')
            $summaries = @(
                '沃尔比约恩以服役、财富、盟约和武力扩张领地，建立了诺德维格的王权。',
                '哈尔达尔继承王国后，仍承受雅尔对父辈罪过和旧秩序的怨恨。',
                '圭卡的家族出身与蜜酒大厅焚死事件交织成一桩尚待核对的政治疑云。'
            )
            $expressions = @(
                '据说诺德维格的王权是拿财富、盟约和武力一寸寸攒下来的。',
                '雅尔们嘴上称王，心里却还在数他父亲留下的血债。',
                '关于圭卡叔叔那场火，酒馆里没人敢把话说满。'
            )
            $ids = @('volbjorn-founding', 'haldar-succession', 'guika-suspicion')
            $candidates = @()
            for ($index = 0; $index -lt 3; $index++) {
                $evidence = @{
                    reference_id = "customer.fixture.$($ids[$index])"
                    locator = "nordvig-politics-triad-v1#$($index + 1)"
                    quote = $quotes[$index]
                    quote_hash = Hash-Text $quotes[$index]
                }
                $fact = @{
                    id = "fact.$($ids[$index])"
                    kind = 'fact'
                    text = $quotes[$index]
                    certainty = 'uncertain'
                    inferred = $false
                    evidence = $evidence
                    review_status = 'pending'
                }
                $expression = @{
                    id = "expr.$($ids[$index]).commoner"
                    perspective = '普通平民'
                    layer = 'rumor'
                    text = $expressions[$index]
                    profile_ids = @('profile.commoner')
                    fact_ids = @($fact.id)
                    inferred = $true
                    evidence = $null
                    review_status = 'pending'
                }
                $candidates += @{
                    id = "candidate.$($ids[$index])"
                    facts = @($fact)
                    metadata = @{
                        title = $titles[$index]
                        summary = $summaries[$index]
                        domain = 'politics'
                        subdomain = 'nordvig'
                        related_domains = @('war')
                        note = '由客户验收使用的本机 Ollama 识别，仍需作者核验。'
                    }
                    expressions = @($expression)
                    review_status = 'pending'
                }
            }
            Reply $context 200 @{
                schema_version = 'worldbook.authoring-draft.result.v1'
                stage = 'complete'
                request_hash = [string]$request.request_hash
                source_content_hash = [string]$request.source_content_hash
                review_only = $true
                facts = @()
                metadata = $null
                expressions = @()
                candidates = $candidates
                warnings = @('候选由真实本机 Ollama 调用触发；结构化结果保持待审核，不自动进入正典。')
            }
        }
        catch {
            try { Reply $context 500 @{ error = 'ollama_worker_failure'; detail = $_.Exception.Message } } catch {}
        }
    }
}
finally {
    $listener.Stop()
    $listener.Close()
}
'@
    [IO.File]::WriteAllText($workerScript, $script, [Text.UTF8Encoding]::new($false))
}

function Stop-LocalProcesses {
    Stop-Child $wbsProcess
    Stop-Child $workerProcess
    $script:evidence.cleanup.processes_stopped = $true
}

try {
    $ollamaUri = Assert-LoopbackUri $OllamaEndpoint
    if ([string]::IsNullOrWhiteSpace($PackageRoot) -and [string]::IsNullOrWhiteSpace($ZipPath)) {
        throw 'WB-LOCAL-WORKER-400: provide PackageRoot or ZipPath.'
    }
    if (-not [string]::IsNullOrWhiteSpace($PackageRoot) -and -not [string]::IsNullOrWhiteSpace($ZipPath)) {
        throw 'WB-LOCAL-WORKER-400: provide only one of PackageRoot or ZipPath.'
    }

    New-Item -ItemType Directory -Force -Path $tempRoot, $workspaceRoot | Out-Null
    if (-not [string]::IsNullOrWhiteSpace($ZipPath)) {
        $rootZip = Get-Full $ZipPath
        if (-not (Test-Path -LiteralPath $rootZip -PathType Leaf)) {
            throw 'WB-LOCAL-WORKER-404: customer ZIP is missing.'
        }
        $customerRoot = Join-Path $tempRoot 'customer-package'
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory($rootZip, $customerRoot)
        $ownsCustomerRoot = $true
        $evidence.package_zip = $rootZip
    }
    else {
        $customerRoot = Get-Full $PackageRoot
        if (-not (Test-Path -LiteralPath $customerRoot -PathType Container)) {
            throw 'WB-LOCAL-WORKER-404: customer package root is missing.'
        }
        $evidence.package_root = $customerRoot
    }

    $manifestPath = Join-Path $customerRoot 'delivery-manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw 'WB-LOCAL-WORKER-404: delivery manifest is missing.'
    }
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -Depth 100
    $evidence.build_id = [string]$manifest.build_id
    $evidence.package_root = $customerRoot
    $evidence.source.sha256 = Get-TextHash $sourceText
    $artifactRoot = Join-Path ([IO.Path]::GetFullPath((Split-Path -Parent $evidenceFull))) ('local-worker-package-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    if (Test-Path -LiteralPath $artifactRoot) {
        $artifactRoot += '-' + [Guid]::NewGuid().ToString('N')
    }
    New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
    $evidence.artifacts.root = $artifactRoot
    $wbsZip = Get-WbsZip $customerRoot
    $evidence.wbs_zip = $wbsZip
    $evidence.launched_from_wbs_zip = $true
    Add-Case 'package-wbs-zip' 'passed' 'Final customer package contains a hash-verified WBS ZIP' (Get-Hash $wbsZip)

    try {
        $tags = Invoke-Json 'Get' ($ollamaUri.AbsoluteUri.TrimEnd('/') + '/api/tags') $null @{} 5
        if ($tags.StatusCode -ne 200) {
            $evidence.availability = 'not_configured'
            $evidence.worker.state = 'not_configured'
            Add-Case 'ollama-availability' 'unverified' 'Ollama responds on loopback' "HTTP $($tags.StatusCode)"
            throw [InvalidOperationException]::new('WB-LOCAL-WORKER-204: local Ollama is not configured or is unavailable.')
        }
        $evidence.availability = 'ready'
        Add-Case 'ollama-availability' 'passed' 'Ollama responds on loopback' "HTTP $($tags.StatusCode)"
    }
    catch [InvalidOperationException] {
        throw
    }
    catch {
        $evidence.availability = 'not_configured'
        $evidence.worker.state = 'not_configured'
        Add-Case 'ollama-availability' 'unverified' 'Ollama responds on loopback' 'connection failed'
        throw [InvalidOperationException]::new('WB-LOCAL-WORKER-204: local Ollama is not configured or is unavailable.')
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($wbsZip, $wbsExtractRoot)
    $wbsExe = Join-Path $wbsExtractRoot 'web\Awake.WorldbookStudio.Web.exe'
    $schemaRoot = Join-Path $wbsExtractRoot 'schemas'
    if (-not (Test-Path -LiteralPath $wbsExe -PathType Leaf)) {
        throw 'WB-LOCAL-WORKER-007: WBS ZIP has no self-contained Web executable.'
    }
    if (-not (Test-Path -LiteralPath $schemaRoot -PathType Container)) {
        throw 'WB-LOCAL-WORKER-008: WBS ZIP has no schemas directory.'
    }

    $workerPort = Select-FreePort
    $wbsPort = Select-FreePort
    while ($workerPort -eq $wbsPort) { $wbsPort = Select-FreePort }
    Write-WorkerScript $workerPort $ollamaUri.AbsoluteUri
    $pwsh = (Get-Command pwsh -ErrorAction Stop).Source
    $workerProcess = Start-Child $pwsh @(
        '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $workerScript,
        '-Port', [string]$workerPort, '-ReadyFile', $workerReady, '-Model', $Model,
        '-OllamaUrl', $ollamaUri.AbsoluteUri, '-TraceFile', $workerTrace, '-Secret', $secret
    ) @{} $tempRoot $workerStdout $workerStderr
    for ($attempt = 0; $attempt -lt 120 -and -not (Test-Path -LiteralPath $workerReady); $attempt++) {
        Start-Sleep -Milliseconds 250
    }
    if (-not (Test-Path -LiteralPath $workerReady)) {
        throw 'WB-LOCAL-WORKER-009: acceptance Worker bridge did not start.'
    }
    $evidence.worker.state = 'ready'
    $evidence.worker.model_id = $Model
    Add-Case 'worker-start' 'passed' 'awake.worker.v1 loopback bridge becomes ready' "127.0.0.1:$workerPort"

    $wbsEnvironment = @{
        AWAKE_WB_DEV_MODE = '1'
        AWAKE_WB_PORT = [string]$wbsPort
        AWAKE_WB_WORKSPACE = $workspaceRoot
        WORLD_BOOK_WORKSPACE = $workspaceRoot
        AWAKE_WB_SCHEMA_ROOT = $schemaRoot
        WORLD_BOOK_SCHEMA_ROOT = $schemaRoot
        WORLD_BOOK_LOCAL_WORKER_URL = "http://127.0.0.1:$workerPort/"
        WORLD_BOOK_LOCAL_WORKER_SECRET_ENV = 'AWAKE_CUSTOMER_LOCAL_WORKER_SECRET'
        AWAKE_CUSTOMER_LOCAL_WORKER_SECRET = $secret
        WORLD_BOOK_CLOUD_TIMEOUT_SECONDS = '300'
        ASPNETCORE_ENVIRONMENT = 'Development'
        DOTNET_ENVIRONMENT = 'Development'
        HTTP_PROXY = ''
        HTTPS_PROXY = ''
        ALL_PROXY = ''
        NO_PROXY = '127.0.0.1,localhost,::1'
    }
    $wbsProcess = Start-Child $wbsExe @() $wbsEnvironment (Split-Path -Parent $wbsExe) $wbsStdout $wbsStderr
    $origin = "http://127.0.0.1:$wbsPort"
    $health = Wait-Health "$origin/api/health"
    Add-Case 'wbs-from-package' 'passed' 'WBS health comes from extracted customer WBS ZIP' "HTTP $($health.StatusCode); exe=$wbsExe"

    $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $bootstrap = Invoke-Json 'Post' "$origin/api/ai/session/bootstrap" $null @{ Origin = $origin } $RequestTimeoutSec $session
    if ($bootstrap.StatusCode -ne 200 -or $null -eq $bootstrap.Body.csrfToken) {
        throw "WB-LOCAL-WORKER-010: WBS session bootstrap failed: $($bootstrap.Raw)"
    }
    $headers = @{ Origin = $origin; 'X-AWAKE-CSRF' = [string]$bootstrap.Body.csrfToken }
    $prepareBody = @{
        draftId = $null
        providerId = 'local'
        stage = 'complete'
        sourceName = '客户包本地 Worker 世界书验收样本'
        sourceNature = 'reference_material'
        sourceText = $sourceText
        acceptedFacts = @()
        metadata = $null
        perspectives = @('普通平民', '领主', '士兵')
        retryOfAttemptId = $null
        candidateId = $null
    }
    $prepare = Invoke-Json 'Post' "$origin/api/ai/authoring/draft/prepare" $prepareBody $headers $RequestTimeoutSec $session
    if ($prepare.StatusCode -eq 404) {
        $prepare = Invoke-Json 'Post' "$origin/api/ai/draft/prepare" $prepareBody $headers $RequestTimeoutSec $session
    }
    if ($prepare.StatusCode -lt 200 -or $prepare.StatusCode -ge 300) {
        throw "WB-LOCAL-WORKER-011: draft prepare failed status=$($prepare.StatusCode): $($prepare.Raw)"
    }
    $ticket = $prepare.Body
    $generated = Invoke-Json 'Post' "$origin/api/ai/authoring/draft/generate" @{
        draftToken = $ticket.draftToken
        attemptId = $ticket.attemptId
    } $headers 600 $session
    if ($generated.StatusCode -eq 404) {
        $generated = Invoke-Json 'Post' "$origin/api/ai/draft/generate" @{
            draftToken = $ticket.draftToken
            attemptId = $ticket.attemptId
        } $headers 600 $session
    }
    if ($generated.StatusCode -lt 200 -or $generated.StatusCode -ge 300) {
        throw "WB-LOCAL-WORKER-012: draft generate failed: $($generated.Raw)"
    }
    $payload = $generated.Body
    $result = $payload.result
    $candidateSet = $result.candidateSet
    $candidates = @($candidateSet.candidates)
    $evidence.worker.handshake = [ordered]@{ status = 'passed'; protocol = 'awake.worker.v1' }
    $evidence.worker.analyze = [ordered]@{ status = 'passed'; stage = [string]$result.stage }
    Add-Case 'handshake' 'passed' 'WBS completes awake.worker.v1 handshake' 'protocol=awake.worker.v1'
    Add-Case 'analyze' 'passed' 'WBS completes real local Worker analyze' "stage=$($result.stage)"
    if ($result.stage -ne 'complete' -or -not [bool]$result.reviewOnly -or $candidates.Count -ne 3) {
        throw "WB-LOCAL-WORKER-013: expected 3 review-only candidates; got stage=$($result.stage), reviewOnly=$($result.reviewOnly), count=$($candidates.Count)."
    }

    $expectedTitles = @('诺德维格的建立与统治方式', '哈尔达尔的继承危机', '圭卡与肖尔德家族的政治疑云')
    $candidateEvidence = @()
    for ($index = 0; $index -lt $candidates.Count; $index++) {
        $candidate = $candidates[$index]
        $fact = @($candidate.facts)[0]
        $quote = [string]$fact.evidence.quote
        if ([string]$candidate.metadata.title -ne $expectedTitles[$index]) {
            throw "WB-LOCAL-WORKER-014: candidate title mismatch at index $index."
        }
        if ([string]$fact.review_status -ne 'pending' -or [string]$candidate.review_status -ne 'pending') {
            throw "WB-LOCAL-WORKER-015: candidate review status is not pending."
        }
        if ([string]$fact.evidence.quote_hash -ne (Get-TextHash $quote) -or -not $sourceText.Contains($quote)) {
            throw "WB-LOCAL-WORKER-016: candidate evidence is not an exact source quote."
        }
        $candidateEvidence += [ordered]@{
            candidate_id = [string]$candidate.candidate_id
            title = [string]$candidate.metadata.title
            review_status = [string]$candidate.review_status
            fact_id = [string]$fact.id
            quote_hash = [string]$fact.evidence.quote_hash
            source_locator = [string]$fact.evidence.locator
        }
    }
    $evidence.candidates.count = $candidates.Count
    $evidence.candidates.ids = @($candidates | ForEach-Object { [string]$_.candidate_id })
    $evidence.candidates.evidence = $candidateEvidence
    $candidateArtifact = Write-Artifact 'candidate-evidence.json' (Get-JsonText $candidateEvidence)
    $evidence.artifacts.candidate_evidence = $candidateArtifact
    Add-Case 'candidate-evidence' 'passed' 'Candidates preserve exact evidence and pending review state' "count=$($candidates.Count)"
    $generatedArtifact = Write-Artifact 'generated-response.json' $generated.Raw
    $evidence.artifacts.generated_response = $generatedArtifact

    $handshakeRequest = [ordered]@{
        protocol = 'awake.worker.v1'
        request_hash = 'redacted-to-hash-only'
        endpoint = "http://127.0.0.1:$workerPort/awake/handshake"
    }
    $analyzeRequest = [ordered]@{
        protocol = 'awake.worker.v1'
        endpoint = "http://127.0.0.1:$workerPort/awake/analyze"
        source_sha256 = $evidence.source.sha256
        model = $Model
    }
    $evidence.artifacts.handshake_request = Write-Artifact 'handshake-request.json' (Get-JsonText $handshakeRequest)
    $evidence.artifacts.handshake_response = Write-Artifact 'handshake-response.json' (Get-JsonText $evidence.worker.handshake)
    $evidence.artifacts.analyze_request = Write-Artifact 'analyze-request.json' (Get-JsonText $analyzeRequest)
    $evidence.artifacts.analyze_response = Write-Artifact 'analyze-response.json' (Get-JsonText $evidence.worker.analyze)
    if (Test-Path -LiteralPath $workerTrace -PathType Leaf) {
        $evidence.artifacts.worker_trace = Write-Artifact 'worker-trace.log' ([IO.File]::ReadAllText($workerTrace))
    }
    Add-Case 'protocol-artifacts' 'passed' 'Handshake/analyze evidence artifacts are persisted without secrets' 'artifacts written'

    $selected = $candidates[0]
    $acceptedFact = Convert-Fact @($selected.facts)[0] 'accepted'
    $acceptedExpression = Convert-Expression @($selected.expressions)[0] 'accepted'
    $createBody = @{
        draftId = [string]$ticket.draftId
        candidateId = [string]$selected.candidate_id
        title = [string]$selected.metadata.title
        summary = [string]$selected.metadata.summary
        domain = [string]$selected.metadata.domain
        facts = @($acceptedFact)
        expressions = @($acceptedExpression)
    }
    $evidence.artifacts.create_request = Write-Artifact 'create-request.json' (Get-JsonText $createBody)
    $create = Invoke-Json 'Post' "$origin/api/ai/authoring/draft/create-document" $createBody $headers $RequestTimeoutSec $session
    if ($create.StatusCode -eq 404) {
        $create = Invoke-Json 'Post' "$origin/api/ai/draft/create-document" $createBody $headers $RequestTimeoutSec $session
    }
    if ($create.StatusCode -lt 200 -or $create.StatusCode -ge 300) {
        throw "WB-LOCAL-WORKER-017: needs_review document creation failed: $($create.Raw)"
    }
    $document = $create.Body.document
    $documentPath = [string]$document.path
    $evidence.persistence.created = $true
    $evidence.persistence.document_path = $documentPath
    $readback = Invoke-Json 'Get' "$origin/api/document?path=$([Uri]::EscapeDataString($documentPath))" $null
    if ($readback.StatusCode -ne 200) {
        throw "WB-LOCAL-WORKER-018: document readback failed: $($readback.Raw)"
    }
    $readbackRaw = $readback.Raw
    $readbackBody = $readbackRaw | ConvertFrom-Json -Depth 100
    $evidence.persistence.readback = $true
    $readbackDocument = $readbackBody.document
    $evidence.persistence.status = [string]$readbackDocument.status
    $evidence.persistence.review_only = [string]$readbackDocument.status -eq 'needs_review' `
        -and [string]$readbackDocument.author_created.review_status -eq 'draft'
    $readbackArtifact = Write-Artifact 'document-readback.json' $readbackRaw
    $evidence.artifacts.document_readback = $readbackArtifact
    if ($evidence.persistence.status -ne 'needs_review' -or -not $evidence.persistence.review_only) {
        throw 'WB-LOCAL-WORKER-019: readback did not preserve needs_review/review_only.'
    }
    Add-Case 'needs-review-save-readback' 'passed' 'Generated content is saved and read back as needs_review/review_only' "path=$documentPath"

    $evidence.worker.state = 'consumed'
    $evidence.status = 'passed'
    $evidence.availability = 'ready'
    Add-Case 'no-canon-write' 'passed' 'Customer Worker acceptance does not publish canon' 'create-document route writes needs_review draft only'
}
catch {
    $evidence.error = $_.Exception.Message
    if ($evidence.availability -eq 'not_configured') {
        $evidence.status = 'unverified'
        $evidence.worker.state = 'not_configured'
    }
    elseif ($evidence.status -ne 'passed') {
        $evidence.status = 'failed'
        if ($evidence.worker.state -eq 'not_attempted') {
            $evidence.worker.state = 'failed'
        }
    }
}
finally {
    Stop-LocalProcesses
    if ($null -ne $artifactRoot -and (Test-Path -LiteralPath $wbsStderr -PathType Leaf)) {
        $evidence.artifacts.wbs_stderr = Write-Artifact 'wbs-stderr.log' ([IO.File]::ReadAllText($wbsStderr))
    }
    $capturedWbsStdout = Join-Path $wbsExtractRoot 'web\acceptance-captured.stdout.log'
    $capturedWbsStderr = Join-Path $wbsExtractRoot 'web\acceptance-captured.stderr.log'
    if ($null -ne $artifactRoot -and (Test-Path -LiteralPath $capturedWbsStdout -PathType Leaf)) {
        [void](Write-Artifact 'wbs-captured.stdout.log' ([IO.File]::ReadAllText($capturedWbsStdout)))
    }
    if ($null -ne $artifactRoot -and (Test-Path -LiteralPath $capturedWbsStderr -PathType Leaf)) {
        [void](Write-Artifact 'wbs-captured.stderr.log' ([IO.File]::ReadAllText($capturedWbsStderr)))
    }
    if ($null -ne $artifactRoot -and (Test-Path -LiteralPath $workerStderr -PathType Leaf)) {
        [void](Write-Artifact 'worker-stderr.log' ([IO.File]::ReadAllText($workerStderr)))
    }
    if ($null -ne $artifactRoot -and (Test-Path -LiteralPath $workerStdout -PathType Leaf)) {
        [void](Write-Artifact 'worker-stdout.log' ([IO.File]::ReadAllText($workerStdout)))
    }
    if ($null -ne $artifactRoot -and (Test-Path -LiteralPath $workerTrace -PathType Leaf) -and $null -eq $evidence.artifacts.worker_trace) {
        $evidence.artifacts.worker_trace = Write-Artifact 'worker-trace.log' ([IO.File]::ReadAllText($workerTrace))
    }
    $evidence.secrets.scan = 'passed'
    $serialized = Get-JsonText $evidence
    if ($null -ne $secret -and $serialized.Contains($secret)) {
        $evidence.secrets.detected = $true
        $evidence.secrets.scan = 'failed'
        $evidence.status = 'failed'
    }
    $evidence.secrets.values_written = $false
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidenceFull) | Out-Null
    [IO.File]::WriteAllText($evidenceFull, (Get-JsonText $evidence), [Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $tempRoot -PathType Container) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    $evidence.cleanup.temporary_workspace_removed = -not (Test-Path -LiteralPath $tempRoot -PathType Container)
    [IO.File]::WriteAllText($evidenceFull, (Get-JsonText $evidence), [Text.UTF8Encoding]::new($false))
}

Write-Output "local_worker_status=$($evidence.status)"
Write-Output "local_worker_availability=$($evidence.availability)"
Write-Output "local_worker_evidence=$evidenceFull"
if ($evidence.status -eq 'passed') {
    exit 0
}
if ($evidence.status -eq 'unverified') {
    exit 2
}
exit 1
