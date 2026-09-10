param(
    [string]$EvidencePath,
    [int]$WorkerPort = 0,
    [switch]$KeepTemp
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $root 'src\Awake.WorldbookStudio.Web\Awake.WorldbookStudio.Web.csproj'
$schemaRoot = Join-Path $root '..\..\docs\worldbook-studio-plan'
$webPort = if ($env:AWAKE_WB_TEST_PORT) { [int]$env:AWAKE_WB_TEST_PORT } else { 0 }
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-worldbook-draft-smoke-' + [Guid]::NewGuid().ToString('N'))
$workspace = Join-Path $tempRoot 'workspace'
$workerScript = Join-Path $tempRoot 'fake-worker.ps1'
$workerReady = Join-Path $tempRoot 'worker.ready'
$workerTrace = Join-Path $tempRoot 'worker-trace.log'
$webLog = Join-Path $tempRoot 'web.log'
$webErrorLog = Join-Path $tempRoot 'web-error.log'
$workerLog = Join-Path $tempRoot 'worker.log'
$workerErrorLog = Join-Path $tempRoot 'worker-error.log'
$secret = 'awake-draft-smoke-secret'
$workerProcess = $null
$webProcess = $null
$startedAt = [DateTime]::UtcNow
$evidence = [ordered]@{
    schema_version = 'awake.worldbook.authoring-draft-smoke.v1'
    started_at_utc = $startedAt.ToString('O')
    finished_at_utc = $null
    cases = @()
    network_boundary = 'loopback_only'
    game_directory_touched = $false
    source_auto_saved = $false
    passed = $false
    error = $null
}

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "WB-DRAFT-SMOKE-FAIL: $Message" }
}

function Sha256Text([string]$Text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
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
    $startInfo.Arguments = (($ArgumentList | ForEach-Object {
        '"' + $_.Trim('"').Replace('"', '\"') + '"'
    }) -join ' ')
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
    }
    catch { }
    try { $Child.Dispose() } catch { }
}

function Read-Json([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { throw 'WB-DRAFT-SMOKE-JSON: 响应为空。' }
    return $Text | ConvertFrom-Json
}

function Invoke-Json([Microsoft.PowerShell.Commands.WebRequestSession]$Session, [string]$Method, [string]$Uri, [hashtable]$Headers, [object]$Body) {
    $parameters = @{
        WebSession = $Session
        Method = $Method
        Uri = $Uri
        Headers = $Headers
        UseBasicParsing = $true
        TimeoutSec = 20
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $json = $Body | ConvertTo-Json -Compress -Depth 30
        $parameters.Body = [Text.Encoding]::UTF8.GetBytes($json)
    }
    if ($KeepTemp -and $null -ne $Body) {
        [IO.File]::AppendAllText((Join-Path $tempRoot 'http-request-trace.log'), "$Method $Uri`n$json`n")
    }
    try {
        return Invoke-WebRequest @parameters
    }
    catch {
        $response = $_.Exception.Response
        $responseBody = $_.ErrorDetails.Message
        if ([string]::IsNullOrWhiteSpace($responseBody)) { $responseBody = $null }
        throw "WB-DRAFT-SMOKE-HTTP: $Method $Uri failed: $($_.Exception.Message); body=$responseBody"
    }
}

function Copy-Evidence($Evidence) {
    if ($null -eq $Evidence) { return $null }
    $referenceId = [string]$Evidence.referenceId
    if ([string]::IsNullOrWhiteSpace($referenceId)) { $referenceId = [string]$Evidence.reference_id }
    $locator = [string]$Evidence.locator
    if ([string]::IsNullOrWhiteSpace($locator)) { $locator = [string]$Evidence.source_locator }
    $quote = [string]$Evidence.quote
    if ([string]::IsNullOrWhiteSpace($quote)) { $quote = [string]$Evidence.source_span }
    $quoteHash = [string]$Evidence.quoteHash
    if ([string]::IsNullOrWhiteSpace($quoteHash)) { $quoteHash = [string]$Evidence.quote_hash }
    return [ordered]@{
        referenceId = [string]$referenceId
        locator = [string]$locator
        quote = [string]$quote
        quoteHash = [string]$quoteHash
    }
}

function Copy-Fact($Fact, [string]$ReviewStatus) {
    $evidenceGroup = @($Fact.evidenceGroup | Where-Object { $null -ne $_ })
    if ($evidenceGroup.Count -eq 0) { $evidenceGroup = @($Fact.evidence_group | Where-Object { $null -ne $_ }) }
    $result = [ordered]@{
        id = [string]$Fact.id
        kind = [string]$Fact.kind
        text = [string]$Fact.text
        certainty = [string]$Fact.certainty
        inferred = [bool]$Fact.inferred
        evidence = Copy-Evidence $Fact.evidence
        reviewStatus = $ReviewStatus
    }
    if ($evidenceGroup.Count -gt 0) { $result.evidenceGroup = @($evidenceGroup | ForEach-Object { Copy-Evidence $_ } | Where-Object { $null -ne $_ }) }
    return $result
}

function Copy-Expression($Expression, [string]$ReviewStatus) {
    $profileIds = @($Expression.profileIds | ForEach-Object { [string]$_ } | Where-Object { $_ })
    if ($profileIds.Count -eq 0) { $profileIds = @($Expression.profile_ids | ForEach-Object { [string]$_ } | Where-Object { $_ }) }
    $factIds = @($Expression.factIds | ForEach-Object { [string]$_ } | Where-Object { $_ })
    if ($factIds.Count -eq 0) { $factIds = @($Expression.fact_ids | ForEach-Object { [string]$_ } | Where-Object { $_ }) }
    return [ordered]@{
        id = [string]$Expression.id
        perspective = [string]$Expression.perspective
        layer = [string]$Expression.layer
        text = [string]$Expression.text
        profileIds = [array]$profileIds
        factIds = [array]$factIds
        inferred = [bool]$Expression.inferred
        evidence = Copy-Evidence $Expression.evidence
        reviewStatus = $ReviewStatus
    }
}

try {
    Assert (Test-Path -LiteralPath $webProject -PathType Leaf) 'Web 项目文件不存在。'
    Assert (Test-Path -LiteralPath $schemaRoot -PathType Container) 'Studio schema 目录不存在。'
    if ($WorkerPort -le 0) {
        do { $WorkerPort = Select-FreePort } while ($WorkerPort -eq $webPort)
    }
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
    $buffer[0] = [byte](($length -shr 24) -band 0xff)
    $buffer[1] = [byte](($length -shr 16) -band 0xff)
    $buffer[2] = [byte](($length -shr 8) -band 0xff)
    $buffer[3] = [byte]($length -band 0xff)
    [Array]::Copy($payload, 0, $buffer, 4, $payload.Length)
    $hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($Secret))
    try { Base64Url $hmac.ComputeHash($buffer) } finally { $hmac.Dispose() }
}
function Reply($Context, [int]$Status, $Value) {
    $json = $Value | ConvertTo-Json -Compress -Depth 30
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
            $request = if ([string]::IsNullOrWhiteSpace($body)) { $null } else { $body | ConvertFrom-Json }
            [IO.File]::AppendAllText($TraceFile, "REQUEST $($context.Request.Url.AbsolutePath)`n$body`n")
            if ($context.Request.Url.AbsolutePath -eq '/awake/handshake') {
                $protocol = 'awake.worker.v1'
                $workerId = 'draft-smoke-worker'
                $timestamp = [long]$request.timestamp
                Reply $context 200 ([ordered]@{ protocol = $protocol; client_nonce = $request.client_nonce; worker_id = $workerId; timestamp = $timestamp; signature = (Signature $protocol $request.client_nonce $workerId $timestamp $request.request_hash) })
                continue
            }
            if ($context.Request.Url.AbsolutePath -ne '/awake/analyze') { Reply $context 404 ([ordered]@{ error = 'not found' }); continue }
            $wire = $request.request
            $stage = [string]$wire.stage
            $generationPass = [string]$wire.generation_pass
            if ($stage -eq 'complete' -and $generationPass -eq 'pass_a') {
                $origins = @($wire.source_origins)
                if ($origins.Count -lt 2) { Reply $context 422 ([ordered]@{ error = 'pass_a requires two source origins' }); continue }
                $originOne = $origins[0]
                $originTwo = $origins[1]
                $factOne = [ordered]@{
                    id = 'fact.quick.volbjorn'; kind = 'fact'; text = [string]$originOne.quote; certainty = 'confirmed'; inferred = $false
                    evidence = [ordered]@{ reference_id = [string]$originOne.id; locator = [string]$originOne.locator; quote = [string]$originOne.quote; quote_hash = [string]$originOne.quote_hash }
                    review_status = 'pending'
                }
                $factTwo = [ordered]@{
                    id = 'fact.quick.haldar'; kind = 'fact'; text = [string]$originTwo.quote; certainty = 'confirmed'; inferred = $false
                    evidence = [ordered]@{ reference_id = [string]$originTwo.id; locator = [string]$originTwo.locator; quote = [string]$originTwo.quote; quote_hash = [string]$originTwo.quote_hash }
                    review_status = 'pending'
                }
                $propositionOne = [ordered]@{
                    id = 'proposition.quick.volbjorn'; subject = '沃尔比约恩'; predicate = '扩张'; object = '土地'
                    epistemic_kind = 'fact'; perspective = 'unknown'; time_scope = 'historical'; polarity = 'affirmed'
                    source_origin_ids = @([string]$originOne.id); confidence = 'confirmed'; unresolved = @()
                }
                $propositionTwo = [ordered]@{
                    id = 'proposition.quick.haldar'; subject = '哈尔达尔'; predicate = '面对'; object = '雅尔不满'
                    epistemic_kind = 'fact'; perspective = 'unknown'; time_scope = 'historical'; polarity = 'affirmed'
                    source_origin_ids = @([string]$originTwo.id); confidence = 'confirmed'; unresolved = @()
                }
                $claimOne = [ordered]@{
                    id = 'claim.quick.volbjorn'; proposition_id = $propositionOne.id; text = [string]$originOne.quote
                    source_origin_ids = @([string]$originOne.id); review_status = 'pending'
                }
                $claimTwo = [ordered]@{
                    id = 'claim.quick.haldar'; proposition_id = $propositionTwo.id; text = [string]$originTwo.quote
                    source_origin_ids = @([string]$originTwo.id); review_status = 'pending'
                }
                $targetOne = [ordered]@{
                    id = 'target.quick.volbjorn'; text = [string]$originOne.quote; claim_ids = @([string]$claimOne.id)
                    source_origin_ids = @([string]$originOne.id); operation = 'preserve'; review_state = 'pending'
                }
                $targetTwo = [ordered]@{
                    id = 'target.quick.haldar'; text = [string]$originTwo.quote; claim_ids = @([string]$claimTwo.id)
                    source_origin_ids = @([string]$originTwo.id); operation = 'preserve'; review_state = 'pending'
                }
                $result = [ordered]@{
                    schema_version = 'worldbook.authoring-draft.result.v1'; stage = 'complete'
                    request_hash = [string]$wire.request_hash; source_content_hash = [string]$wire.source_content_hash; review_only = $true
                    facts = @($factOne, $factTwo); metadata = $null; expressions = @(); candidates = $null; warnings = @(); unresolved = @()
                    coverage = [ordered]@{ mode = 'quick_authoring'; status = 'partial'; heuristic = $true; not_semantic_migration_proof = $true; source_proposition_count = 2 }
                    target_spans = @($targetOne, $targetTwo); propositions = @($propositionOne, $propositionTwo); claims = @($claimOne, $claimTwo)
                }
                [IO.File]::AppendAllText($TraceFile, "RESPONSE $($context.Request.Url.AbsolutePath)`n$($result | ConvertTo-Json -Compress -Depth 30)`n")
                Reply $context 200 $result
                continue
            }
            if ($stage -eq 'complete' -and $generationPass -eq 'pass_b') {
                $packet = $wire.semantic_packet
                $packetFacts = @($packet.facts)
                $packetExpressions = @($packet.expressions)
                $packetPropositions = @($packet.propositions)
                $packetClaims = @($packet.claims)
                $packetTargets = @($packet.target_spans)
                $packetOrigins = @($wire.source_origins)
                if ($packetFacts.Count -lt 2 -or $packetPropositions.Count -lt 2 -or $packetClaims.Count -lt 2 -or $packetTargets.Count -lt 2) {
                    Reply $context 422 ([ordered]@{ error = 'pass_b requires the frozen semantic packet' })
                    continue
                }
                $candidateOne = [ordered]@{
                    id = 'candidate.quick.volbjorn'; facts = @($packetFacts[0])
                    metadata = [ordered]@{ title = '诺德维格的建立'; summary = '沃尔比约恩的扩张经历。'; domain = 'politics'; subdomain = 'founding'; related_domains = @('war'); note = '待审核。' }
                    expressions = @($packetExpressions | Where-Object { $_.fact_ids -contains $packetFacts[0].id })
                    segmentation_reason_codes = @('entity_focus_changed')
                    source_spans = @($packetOrigins[0]); target_spans = @($packetTargets[0]); propositions = @($packetPropositions[0]); claims = @($packetClaims[0]); unresolved = @()
                    coverage = [ordered]@{ mode = 'quick_authoring'; status = 'partial'; heuristic = $true; not_semantic_migration_proof = $true }
                    review_status = 'pending'
                }
                $candidateTwo = [ordered]@{
                    id = 'candidate.quick.haldar'; facts = @($packetFacts[1])
                    metadata = [ordered]@{ title = '哈尔达尔的继承危机'; summary = '哈尔达尔继位后的雅尔不满。'; domain = 'politics'; subdomain = 'succession'; related_domains = @('war'); note = '待审核。' }
                    expressions = @($packetExpressions | Where-Object { $_.fact_ids -contains $packetFacts[1].id })
                    segmentation_reason_codes = @('entity_focus_changed')
                    source_spans = @($packetOrigins[1]); target_spans = @($packetTargets[1]); propositions = @($packetPropositions[1]); claims = @($packetClaims[1]); unresolved = @()
                    coverage = [ordered]@{ mode = 'quick_authoring'; status = 'partial'; heuristic = $true; not_semantic_migration_proof = $true }
                    review_status = 'pending'
                }
                $result = [ordered]@{
                    schema_version = 'worldbook.authoring-draft.result.v1'; stage = 'complete'
                    request_hash = [string]$wire.request_hash; source_content_hash = [string]$wire.source_content_hash; review_only = $true
                    facts = @(); metadata = $null; expressions = @(); candidates = @($candidateOne, $candidateTwo); warnings = @(); unresolved = @()
                    coverage = [ordered]@{ mode = 'quick_authoring'; pipeline = 'pass_a_gate_a_pass_b_gate_b'; status = 'partial'; heuristic = $true; not_semantic_migration_proof = $true }
                    target_spans = @($packetTargets); propositions = @($packetPropositions); claims = @($packetClaims)
                }
                [IO.File]::AppendAllText($TraceFile, "RESPONSE $($context.Request.Url.AbsolutePath)`n$($result | ConvertTo-Json -Compress -Depth 30)`n")
                Reply $context 200 $result
                continue
            }
            $factQuote = '西部土地以葱郁的橡树林地为主。'
            $fact = [ordered]@{
                id = 'fact.draft-smoke.1'; kind = 'fact'; text = $factQuote; certainty = 'confirmed'; inferred = $false
                evidence = [ordered]@{ reference_id = 'smoke-reference'; locator = '第 1 段'; quote = $factQuote; quote_hash = (Sha256Text $factQuote) }
                review_status = 'pending'
            }
            $metadata = [ordered]@{
                title = '西部土地与军事传统'; summary = '西部土地以橡树林地为主，并形成了鲜明的军事传统。'; domain = 'geography'; subdomain = $null; related_domains = @('war'); note = 'Smoke 仅用于验证流程，不属于正典。'
            }
            $expression = [ordered]@{
                id = 'expression.draft-smoke.1'; perspective = '普通平民'; layer = 'summary'; text = '西边多是橡树林地，军团里服役是许多人谋生的路子。'; profile_ids = @('profile.commoner'); fact_ids = @('fact.draft-smoke.1'); inferred = $false; evidence = $null; review_status = 'pending'
            }
            $facts = [object[]]@()
            $expressions = [object[]]@()
            $metadataResult = $null
            $candidates = $null
            if ($stage -eq 'facts') { $facts = [object[]]@($fact) }
            if ($stage -eq 'metadata') { $metadataResult = $metadata }
            if ($stage -eq 'expressions') { $expressions = [object[]]@($expression) }
            if ($stage -eq 'complete') {
                $firstQuote = '沃尔比约恩年轻时为帝国效力，回乡后以财富和武力扩张土地。'
                $secondQuote = '哈尔达尔继承诺德维格后，雅尔们仍把父辈的罪过归咎于他。'
                $secondEvidenceQuote = '有人渴望回到不受王权约束的旧日秩序。'
                $firstFact = [ordered]@{
                    id = 'fact.volbjorn.founding'; kind = 'fact'; text = $firstQuote; certainty = 'confirmed'; inferred = $false
                    evidence = [ordered]@{ reference_id = 'rule.volbjorn'; locator = '沃尔比约恩段落'; quote = $firstQuote; quote_hash = (Sha256Text $firstQuote) }
                    review_status = 'pending'
                }
                $secondFact = [ordered]@{
                    id = 'fact.haldar.succession'; kind = 'fact'; text = $secondQuote; certainty = 'confirmed'; inferred = $false
                    evidence = [ordered]@{ reference_id = 'rule.haldar'; locator = '哈尔达尔段落'; quote = $secondQuote; quote_hash = (Sha256Text $secondQuote) }
                    evidence_group = @(
                        [ordered]@{ reference_id = 'rule.haldar'; locator = '哈尔达尔段落'; quote = $secondQuote; quote_hash = (Sha256Text $secondQuote) },
                        [ordered]@{ reference_id = 'rule.haldar'; locator = '旧秩序段落'; quote = $secondEvidenceQuote; quote_hash = (Sha256Text $secondEvidenceQuote) }
                    )
                    review_status = 'pending'
                }
                $firstExpression = [ordered]@{
                    id = 'expression.volbjorn.commoner'; perspective = '普通平民'; layer = 'summary'; text = '老王的国土是拿钱袋和斧头一寸寸攒下来的。'; profile_ids = @('profile.commoner'); fact_ids = @('fact.volbjorn.founding'); inferred = $false; evidence = $null; review_status = 'pending'
                }
                $secondExpression = [ordered]@{
                    id = 'expression.haldar.commoner'; perspective = '普通平民'; layer = 'rumor'; text = '雅尔们嘴上称王，心里却还在数他父亲欠下的血债。'; profile_ids = @('profile.commoner'); fact_ids = @('fact.haldar.succession'); inferred = $false; evidence = $null; review_status = 'pending'
                }
                $candidates = @(
                    [ordered]@{
                        id = 'candidate.volbjorn'; facts = @($firstFact)
                        metadata = [ordered]@{ title = '诺德维格的建立'; summary = '沃尔比约恩建立王权的方式。'; domain = 'politics'; subdomain = 'founding'; related_domains = @('war'); note = '待审核。' }
                        expressions = @($firstExpression)
                        propositions = @([ordered]@{ id = 'proposition.volbjorn'; subject = '沃尔比约恩'; predicate = '建立'; object = '诺德维格王权'; epistemic_kind = 'fact'; perspective = 'unknown'; time_scope = 'historical'; polarity = 'affirmed'; source_origin_ids = @('rule.volbjorn'); confidence = 'confirmed'; unresolved = @() })
                        claims = @([ordered]@{ id = 'claim.volbjorn'; proposition_id = 'proposition.volbjorn'; text = $firstQuote; source_origin_ids = @('rule.volbjorn'); review_status = 'pending' })
                        target_spans = @([ordered]@{ id = 'target.volbjorn'; text = $firstQuote; claim_ids = @('claim.volbjorn'); source_origin_ids = @('rule.volbjorn'); operation = 'preserve'; review_state = 'pending' })
                        review_status = 'pending'
                    },
                    [ordered]@{
                        id = 'candidate.haldar'; facts = @($secondFact)
                        metadata = [ordered]@{ title = '哈尔达尔的继承危机'; summary = '哈尔达尔继位后面对雅尔不满。'; domain = 'politics'; subdomain = 'succession'; related_domains = @('war'); note = '待审核。' }
                        expressions = @($secondExpression)
                        propositions = @([ordered]@{ id = 'proposition.haldar'; subject = '哈尔达尔'; predicate = '面对'; object = '雅尔不满'; epistemic_kind = 'fact'; perspective = 'unknown'; time_scope = 'historical'; polarity = 'affirmed'; source_origin_ids = @('rule.haldar'); confidence = 'confirmed'; unresolved = @() })
                        claims = @([ordered]@{ id = 'claim.haldar'; proposition_id = 'proposition.haldar'; text = $secondQuote; source_origin_ids = @('rule.haldar'); review_status = 'pending' })
                        target_spans = @([ordered]@{ id = 'target.haldar'; text = $secondQuote; claim_ids = @('claim.haldar'); source_origin_ids = @('rule.haldar'); operation = 'preserve'; review_state = 'pending' })
                        review_status = 'pending'
                    }
                )
            }
            $result = [ordered]@{
                schema_version = 'worldbook.authoring-draft.result.v1'; stage = $stage; request_hash = [string]$wire.request_hash; source_content_hash = [string]$wire.source_content_hash; review_only = $true
                facts = $facts
                metadata = $metadataResult
                expressions = $expressions
                candidates = $candidates
                warnings = @()
            }
            [IO.File]::AppendAllText($TraceFile, "RESPONSE $($context.Request.Url.AbsolutePath)`n$($result | ConvertTo-Json -Compress -Depth 30)`n")
            Reply $context 200 $result
        }
        catch { try { Reply $context 500 ([ordered]@{ error = 'fake worker failure' }) } catch { } }
    }
}
finally { $listener.Stop(); $listener.Close() }
'@
    [IO.File]::WriteAllText($workerScript, $worker, [Text.UTF8Encoding]::new($false))
    $pwsh = (Get-Command pwsh -ErrorAction Stop).Source
    $workerChild = Start-ChildProcess $pwsh @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $workerScript, '-Port', $WorkerPort, '-Secret', $secret, '-ReadyFile', $workerReady, '-TraceFile', $workerTrace) @{} $tempRoot $workerLog $workerErrorLog
    $workerProcess = $workerChild
    for ($attempt = 0; $attempt -lt 60 -and -not (Test-Path -LiteralPath $workerReady); $attempt++) { Start-Sleep -Milliseconds 100 }
    Assert (Test-Path -LiteralPath $workerReady) '假 Worker 未启动。'

    $webEnvironment = @{
        AWAKE_WB_DEV_MODE = '1'
        WORLD_BOOK_WORKSPACE = $workspace
        WORLD_BOOK_SCHEMA_ROOT = $schemaRoot
        WORLD_BOOK_LOCAL_WORKER_URL = "http://127.0.0.1:$WorkerPort/"
        WORLD_BOOK_LOCAL_WORKER_SECRET_ENV = 'AWAKE_DRAFT_SMOKE_SECRET'
        AWAKE_DRAFT_SMOKE_SECRET = $secret
        AWAKE_WB_PORT = [string]$webPort
        ASPNETCORE_ENVIRONMENT = 'Development'
        DOTNET_ENVIRONMENT = 'Development'
    }
    $webChild = Start-ChildProcess (Get-Command dotnet -ErrorAction Stop).Source @('run', '--project', ('"' + $webProject + '"'), '--configuration', 'Release', '--no-build', '--no-restore') $webEnvironment $root $webLog $webErrorLog
    $webProcess = $webChild
    $origin = "http://127.0.0.1:$webPort"
    $healthy = $false
    for ($attempt = 0; $attempt -lt 80 -and -not $healthy; $attempt++) {
        try { $healthy = (Invoke-WebRequest -Uri "$origin/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { Start-Sleep -Milliseconds 250 }
    }
    Assert $healthy 'Web Studio 未能启动到健康状态。'

    $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $bootstrapResponse = Invoke-Json $session 'Post' "$origin/api/ai/session/bootstrap" @{ Origin = $origin } $null
    $bootstrap = Read-Json $bootstrapResponse.Content
    Assert ($bootstrap.ok -eq $true -and -not [string]::IsNullOrWhiteSpace($bootstrap.csrfToken)) 'AI 会话初始化失败。'
    $headers = @{ Origin = $origin; 'X-AWAKE-CSRF' = [string]$bootstrap.csrfToken }
    $sourceText = '首批进入大陆的卡拉德人被记为流亡者；西部土地以葱郁的橡树林地为主。'

    $factsPrepare = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/prepare" $headers ([ordered]@{ draftId = $null; providerId = 'local'; stage = 'facts'; sourceName = 'Smoke 参考资料'; sourceNature = 'under_review'; sourceText = $sourceText; acceptedFacts = @(); metadata = $null; perspectives = @('普通平民') })
    $factsTicket = Read-Json $factsPrepare.Content
    $factsResponse = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/generate" $headers ([ordered]@{ draftToken = $factsTicket.draftToken; attemptId = $factsTicket.attemptId })
    $factsPayload = Read-Json $factsResponse.Content
    $factsResult = $factsPayload.result
    if ($KeepTemp) { [IO.File]::AppendAllText((Join-Path $tempRoot 'http-response-trace.log'), "FACTS`n$($factsResponse.Content)`n") }
    Assert ($factsResult.facts.Count -eq 1 -and $factsResult.reviewOnly -eq $true) '客观事实阶段未返回可审核草稿。'
    $acceptedFacts = @((Copy-Fact $factsResult.facts[0] 'accepted'))

    $metadataPrepare = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/prepare" $headers ([ordered]@{ draftId = $factsTicket.draftId; providerId = 'local'; stage = 'metadata'; sourceName = 'Smoke 参考资料'; sourceNature = 'under_review'; sourceText = $sourceText; acceptedFacts = $acceptedFacts; metadata = $null; perspectives = @('普通平民') })
    $metadataTicket = Read-Json $metadataPrepare.Content
    $metadataResponse = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/generate" $headers ([ordered]@{ draftToken = $metadataTicket.draftToken; attemptId = $metadataTicket.attemptId })
    $metadataResult = (Read-Json $metadataResponse.Content).result
    Assert ($metadataResult.metadata.title -eq '西部土地与军事传统') '简介阶段未返回标题候选。'
    $metadata = [ordered]@{ title = [string]$metadataResult.metadata.title; summary = [string]$metadataResult.metadata.summary; domain = [string]$metadataResult.metadata.domain; subdomain = $null; relatedDomains = @('war'); note = [string]$metadataResult.metadata.note }

    $expressionPrepare = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/prepare" $headers ([ordered]@{ draftId = $factsTicket.draftId; providerId = 'local'; stage = 'expressions'; sourceName = 'Smoke 参考资料'; sourceNature = 'under_review'; sourceText = $sourceText; acceptedFacts = $acceptedFacts; metadata = $metadata; perspectives = @('普通平民') })
    $expressionTicket = Read-Json $expressionPrepare.Content
    $expressionResponse = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/generate" $headers ([ordered]@{ draftToken = $expressionTicket.draftToken; attemptId = $expressionTicket.attemptId })
    if ($KeepTemp) { Write-Output "EXPRESSION_RESPONSE=$($expressionResponse.Content)" }
    $expressionResult = (Read-Json $expressionResponse.Content).result
    Assert ($expressionResult.expressions.Count -eq 1 -and $expressionResult.expressions[0].profileIds[0] -eq 'profile.commoner') '身份表达阶段未返回身份绑定表达。'
    $acceptedExpressions = @((Copy-Expression $expressionResult.expressions[0] 'accepted'))
    $resumeResponse = Invoke-Json $session 'Get' "$origin/api/ai/authoring/draft/$($factsTicket.draftId)" $headers $null
    $resume = Read-Json $resumeResponse.Content
    Assert ($resume.ok -eq $true -and $resume.result.reviewOnly -eq $true -and $resume.result.stage -eq 'expressions') 'Draft 续接读取没有返回当前待审核结果。'
    Assert ($resume.result.candidateSet.candidates.Count -eq 1) 'Draft 续接读取没有返回当前候选集。'

    $createPayload = [ordered]@{ draftId = $factsTicket.draftId; title = $metadata.title; summary = $metadata.summary; domain = 'geography'; contentTier = 'base'; facts = $acceptedFacts; expressions = $acceptedExpressions }
    if ($KeepTemp) { Write-Output "CREATE_PAYLOAD=$($createPayload | ConvertTo-Json -Compress -Depth 30)" }
    $createResponse = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/create-document" $headers $createPayload
    $created = Read-Json $createResponse.Content
    Assert ($created.ok -eq $true -and -not [string]::IsNullOrWhiteSpace($created.document.path)) '草稿档案没有创建。'
    $createdPath = [string]$created.document.path
    $duplicateResponse = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/create-document" $headers $createPayload
    $duplicate = Read-Json $duplicateResponse.Content
    Assert ($duplicate.ok -eq $true -and [string]$duplicate.document.path -eq $createdPath) '重复提交没有返回同一份草稿档案。'
    $editorResponse = Invoke-WebRequest -Uri "$origin/api/editor-document?path=$([Uri]::EscapeDataString($createdPath))" -UseBasicParsing -TimeoutSec 20
    Assert ($editorResponse.StatusCode -eq 200) '创建后无法回读作者表单模型。'
    $editorModel = Read-Json $editorResponse.Content
    $readbackFact = [string]$editorModel.model.assertions[0].text
    $readbackExpression = [string]$editorModel.model.assertions[0].expressions[0].text
    Assert ($editorModel.model.status -eq 'needs_review') '创建后的档案没有保持待复核状态。'
    Assert ($readbackFact -eq [string]$acceptedFacts[0].text) '创建后回读的客观事实与已采纳内容不一致。'
    Assert ($readbackExpression -eq [string]$acceptedExpressions[0].text) '创建后回读的 NPC 表达与已采纳内容不一致。'
    Assert (-not (Get-ChildItem -LiteralPath (Join-Path $workspace 'authoring\sources') -Recurse -File -ErrorAction SilentlyContinue)) '参考资料被意外自动写入来源目录。'
    $documentCountBeforeForgery = @(Get-ChildItem -LiteralPath (Join-Path $workspace 'authoring\geography') -Filter '*.yaml' -File).Count
    $forgedPayload = Read-Json (($createPayload | ConvertTo-Json -Compress -Depth 30) | ConvertFrom-Json | ConvertTo-Json -Compress -Depth 30)
    $forgedPayload.facts[0].evidence.quote = '伪造的原文依据'
    $forgedJson = $forgedPayload | ConvertTo-Json -Compress -Depth 30
    $forgedResponse = Invoke-WebRequest -Method Post -Uri "$origin/api/ai/authoring/draft/create-document" -WebSession $session -Headers $headers -ContentType 'application/json' -Body ([Text.Encoding]::UTF8.GetBytes($forgedJson)) -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 20
    $forgedResult = Read-Json $forgedResponse.Content
    Assert ($forgedResponse.StatusCode -eq 422 -and $forgedResult.error -eq 'WB-AI-DRAFT-422') "伪造原文依据没有被 HTTP 边界拒绝。status=$($forgedResponse.StatusCode) body=$($forgedResponse.Content)"
    $documentCountAfterForgery = @(Get-ChildItem -LiteralPath (Join-Path $workspace 'authoring\geography') -Filter '*.yaml' -File).Count
    Assert ($documentCountAfterForgery -eq $documentCountBeforeForgery) '伪造提交意外创建了新的档案文件。'

    $multiSourceText = '沃尔比约恩年轻时为帝国效力，回乡后以财富和武力扩张土地。哈尔达尔继承诺德维格后，雅尔们仍把父辈的罪过归咎于他。有人渴望回到不受王权约束的旧日秩序。'
    $multiPrepare = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/prepare" $headers ([ordered]@{ draftId = $null; providerId = 'local'; stage = 'complete'; sourceName = '诺德维格政治资料'; sourceNature = 'reference_material'; sourceText = $multiSourceText; acceptedFacts = @(); metadata = $null; perspectives = @('普通平民') })
    $multiTicket = Read-Json $multiPrepare.Content
    $multiResponse = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/generate" $headers ([ordered]@{ draftToken = $multiTicket.draftToken; attemptId = $multiTicket.attemptId })
    $multiResult = (Read-Json $multiResponse.Content).result
    $multiCandidates = @($multiResult.candidateSet.candidates)
    Assert ($multiCandidates.Count -eq 2) '完整生成没有返回两个独立候选。'
    $reviewDecisionResponse = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/review-decision" $headers ([ordered]@{
        draftId = $multiTicket.draftId; operation = 'merge'
        candidateIds = @($multiCandidates[0].candidate_id, $multiCandidates[1].candidate_id)
    })
    $reviewDecision = Read-Json $reviewDecisionResponse.Content
    Assert ($reviewDecision.ok -eq $true -and $reviewDecision.decision.status -eq 'proposed' -and $reviewDecision.decision.materialized_candidates.Count -eq 1 -and [int]$reviewDecision.decisionCount -eq 1) 'merge review decision was not persisted with a derived review candidate.'
    Assert (@($reviewDecision.reviewProjection.candidates).Count -eq 3) 'merge review response did not expose the authoritative candidate set.'
    Assert (@($reviewDecision.reviewProjection.candidates | Where-Object { $_.review_status -eq 'superseded' }).Count -eq 2) 'merge review response did not mark source candidates superseded.'
    Assert (@($reviewDecision.reviewProjection.candidates | Where-Object {
        $_.PSObject.Properties.Name -contains 'segmentation_reason_codes' -and
        $_.PSObject.Properties.Name -contains 'reason_codes' -and
        $_.PSObject.Properties.Name -contains 'source_spans'
    }).Count -eq 3) 'merge review projection dropped candidate explanation fields.'
    $selectedCandidate = @($reviewDecision.candidateSet.candidates | Where-Object { $_.review_status -eq 'pending' })[0]
    Assert ($null -ne $selectedCandidate) 'merge review did not expose an active derived candidate.'
    $reviewReadbackResponse = Invoke-Json $session 'Get' "$origin/api/ai/authoring/draft/review-projection/$($multiTicket.draftId)" $headers $null
    $reviewReadback = Read-Json $reviewReadbackResponse.Content
    Assert ($reviewReadback.ok -eq $true -and @($reviewReadback.reviewProjection.candidates).Count -eq 3 -and @($reviewReadback.reviewProjection.candidates | Where-Object { $_.review_status -eq 'superseded' }).Count -eq 2) 'review projection readback did not return the persisted authoritative state.'
    $badDecisionResponse = Invoke-WebRequest -Method Post -Uri "$origin/api/ai/authoring/draft/review-decision" -WebSession $session -Headers $headers -ContentType 'application/json' -Body (([ordered]@{
        draftId = $multiTicket.draftId; operation = 'reorder'; candidateIds = @($multiCandidates[0].candidate_id, $multiCandidates[1].candidate_id); orderedCandidateIds = @($multiCandidates[0].candidate_id)
    } | ConvertTo-Json -Compress -Depth 20)) -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 20
    $badDecision = Read-Json $badDecisionResponse.Content
    Assert ([int]$badDecisionResponse.StatusCode -eq 409 -and $badDecision.error -eq 'WB-AI-REVIEW-CANDIDATE-409') 'invalid review decision was not rejected.'
    $selectedFacts = @($selectedCandidate.facts | ForEach-Object { Copy-Fact $_ 'accepted' })
    $selectedExpressions = @($selectedCandidate.expressions | ForEach-Object { Copy-Expression $_ 'accepted' })
    $multiCreatePayload = [ordered]@{
        draftId = $multiTicket.draftId
        candidateId = [string]$selectedCandidate.candidate_id
        title = [string]$selectedCandidate.metadata.title
        summary = [string]$selectedCandidate.metadata.summary
        domain = [string]$selectedCandidate.metadata.domain
        contentTier = 'base'
        subdomain = 'succession'
        relatedDomains = @('war')
        facts = $selectedFacts
        expressions = $selectedExpressions
    }
    if ($KeepTemp) { Write-Output "MULTI_CREATE_PAYLOAD=$($multiCreatePayload | ConvertTo-Json -Compress -Depth 30)" }
    $multiCreateResponse = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/create-document" $headers $multiCreatePayload
    $multiCreated = Read-Json $multiCreateResponse.Content
    $multiCreatedPath = [string]$multiCreated.document.path
    Assert (-not [string]::IsNullOrWhiteSpace($multiCreatedPath)) '选择第二候选后没有创建作者草稿。'
    $multiEditorResponse = Invoke-WebRequest -Uri "$origin/api/editor-document?path=$([Uri]::EscapeDataString($multiCreatedPath))" -UseBasicParsing -TimeoutSec 20
    $multiEditorModel = Read-Json $multiEditorResponse.Content
    Assert ([string]$multiEditorModel.model.assertions[0].text -eq [string]$selectedFacts[0].text) '建档正文没有绑定第二候选事实。'
    Assert ([string]$multiEditorModel.model.assertions[0].expressions[0].text -eq [string]$selectedExpressions[0].text) '建档表达没有绑定第二候选表达。'
    $multiDocumentContent = [IO.File]::ReadAllText((Join-Path $workspace $multiCreatedPath))
    Assert ($multiDocumentContent.Contains('candidate_fingerprint:', [StringComparison]::Ordinal)) '建档 provenance 没有保存最终投影 fingerprint。'
    Assert (-not $multiDocumentContent.Contains([string]$selectedCandidate.fingerprint, [StringComparison]::Ordinal)) '建档 provenance 错误复用了未采纳的第二候选 fingerprint。'
    Assert (-not $multiDocumentContent.Contains([string]$multiCandidates[0].fingerprint, [StringComparison]::Ordinal)) '建档 provenance 错误保存了第一候选 fingerprint。'
    Assert ($multiDocumentContent.Contains([string]$selectedFacts[0].evidence.quote, [StringComparison]::Ordinal)) '建档 provenance 没有保存第二候选证据。'
    Assert ($selectedFacts.Count -ge 2 -and $multiDocumentContent.Contains([string]$selectedFacts[1].text, [StringComparison]::Ordinal)) '合并候选建档没有保留全部事实。'
    $switchPayload = [ordered]@{
        draftId = $multiTicket.draftId
        candidateId = [string]$multiCandidates[0].candidate_id
        title = [string]$multiCandidates[0].metadata.title
         summary = [string]$multiCandidates[0].metadata.summary
         domain = [string]$multiCandidates[0].metadata.domain
         contentTier = 'base'
         facts = @($multiCandidates[0].facts | ForEach-Object { Copy-Fact $_ 'accepted' })
        expressions = @($multiCandidates[0].expressions | ForEach-Object { Copy-Expression $_ 'accepted' })
    }
    $switchJson = $switchPayload | ConvertTo-Json -Compress -Depth 30
    $switchResponse = Invoke-WebRequest -Method Post -Uri "$origin/api/ai/authoring/draft/create-document" -WebSession $session -Headers $headers -ContentType 'application/json' -Body ([Text.Encoding]::UTF8.GetBytes($switchJson)) -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 20
    $switchResult = Read-Json $switchResponse.Content
    Assert ([int]$switchResponse.StatusCode -eq 409 -and $switchResult.error -eq 'WB-AI-DRAFT-CANDIDATE-409') '切换候选后不应静默返回旧文档。'

    $quickPrepare = Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/prepare" $headers ([ordered]@{
        draftId = $null
        providerId = 'local'
        stage = 'complete'
        mode = 'quick_authoring'
        authoringGoal = '整理诺德维格继承冲突档案'
        userInstruction = '保留各方立场，不要把流言写成确定事实。'
        requestedDomain = 'politics'
        requestedSubdomain = 'succession'
        requestedAudience = @('普通玩家')
        requestedPerspectives = @('普通平民')
        styleConstraints = @('简洁的中世纪编年史体')
        mustPreserve = @('继承冲突')
        mustNotInvent = @('新人物', '新年份', '正式 entity ID')
        requestedContentTier = 'base'
        sourceName = 'Quick Authoring Smoke'
        sourceNature = 'reference_material'
        sourceText = $multiSourceText
        acceptedFacts = @()
        metadata = $null
        perspectives = @('普通平民')
    })
    $quickTicket = Read-Json $quickPrepare.Content
    $quickResult = (Read-Json ((Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/generate" $headers ([ordered]@{
        draftToken = $quickTicket.draftToken
        attemptId = $quickTicket.attemptId
    })).Content)).result
    Assert ($quickResult.reviewOnly -eq $true -and $quickResult.candidateSet.candidates.Count -eq 2) 'Quick Authoring 未返回待审核候选集。'
    Assert ($quickResult.candidateSet.candidates[0].propositions.Count -eq 1 -and $quickResult.candidateSet.candidates[0].claims.Count -eq 1 -and $quickResult.candidateSet.candidates[0].target_spans.Count -eq 1) 'Quick Authoring 语义链路未闭合。'
    Assert (-not @($quickResult.unresolved | Where-Object { $_.blocking -eq $true })) 'Quick Authoring 合法语义图却产生阻断项。'
    $quickCandidate = $quickResult.candidateSet.candidates[1]
    $quickCreatePayload = [ordered]@{
        draftId = $quickTicket.draftId
        candidateId = [string]$quickCandidate.candidate_id
        title = [string]$quickCandidate.metadata.title
        summary = [string]$quickCandidate.metadata.summary
        domain = [string]$quickCandidate.metadata.domain
        contentTier = 'base'
        subdomain = [string]$quickCandidate.metadata.subdomain
        relatedDomains = @('war')
        facts = @($quickCandidate.facts | ForEach-Object { Copy-Fact $_ 'accepted' })
        expressions = @($quickCandidate.expressions | ForEach-Object { Copy-Expression $_ 'accepted' })
    }
    $quickCreated = Read-Json (Invoke-Json $session 'Post' "$origin/api/ai/authoring/draft/create-document" $headers $quickCreatePayload).Content
    $quickCreatedPath = [string]$quickCreated.document.path
    Assert (-not [string]::IsNullOrWhiteSpace($quickCreatedPath)) 'Quick Authoring 未能创建待审核档案。'
    $quickDocumentContent = [IO.File]::ReadAllText((Join-Path $workspace $quickCreatedPath))
    Assert ($quickDocumentContent.Contains('propositions:', [StringComparison]::Ordinal) -and $quickDocumentContent.Contains('claims:', [StringComparison]::Ordinal) -and $quickDocumentContent.Contains('target_spans:', [StringComparison]::Ordinal)) 'Quick Authoring provenance 未保存语义链路。'

    $evidence.cases = @(
        [ordered]@{ id = 'session-bootstrap'; passed = $true }
        [ordered]@{ id = 'facts-generate-and-review'; passed = $true; fact_count = [int]$factsResult.facts.Count }
        [ordered]@{ id = 'metadata-generate'; passed = $true }
        [ordered]@{ id = 'expressions-generate-and-review'; passed = $true; expression_count = [int]$expressionResult.expressions.Count }
        [ordered]@{ id = 'draft-session-resume-read'; passed = $true; stage = [string]$resume.result.stage; candidate_count = [int]$resume.result.candidateSet.candidates.Count }
        [ordered]@{ id = 'create-document'; passed = $true; path = $createdPath }
        [ordered]@{ id = 'duplicate-create-idempotent'; passed = $true; same_path = $true }
        [ordered]@{ id = 'editor-document-readback'; passed = $true; status = [string]$editorModel.model.status; fact_matches = $true; expression_matches = $true }
        [ordered]@{ id = 'source-not-auto-saved'; passed = $true }
        [ordered]@{ id = 'forged-evidence-rejected'; passed = $true; status = [int]$forgedResponse.StatusCode; document_count_unchanged = $true }
        [ordered]@{ id = 'candidate-two-http-create-readback'; passed = $true; candidate_count = $multiCandidates.Count; selected_candidate_id = [string]$selectedCandidate.candidate_id; path = $multiCreatedPath; fingerprint_matches = $true; content_matches = $true; evidence_matches = $true }
        [ordered]@{ id = 'review-decision-ledger'; passed = $true; operation = 'merge'; decision_count = [int]$reviewDecision.decisionCount; invalid_reorder_rejected = $true }
        [ordered]@{ id = 'candidate-switch-cannot-return-old-document'; passed = $true; status = [int]$switchResponse.StatusCode; error = [string]$switchResult.error }
        [ordered]@{ id = 'quick-authoring-semantic-http-closure'; passed = $true; candidate_count = [int]$quickResult.candidateSet.candidates.Count; proposition_count = [int]$quickCandidate.propositions.Count; claim_count = [int]$quickCandidate.claims.Count; target_span_count = [int]$quickCandidate.target_spans.Count; path = $quickCreatedPath }
    )
    $evidence.source_auto_saved = $false
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
        $evidence | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
    }
    Stop-Child $webProcess
    Stop-Child $workerProcess
    if (-not $KeepTemp) {
        if ($evidence.passed -ne $true) {
            Write-Output "DRAFT_SMOKE_TEMP=$tempRoot"
        } else {
            try { if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force } } catch { }
        }
    }
}

$evidence | ConvertTo-Json -Depth 30
if (-not $evidence.passed) { exit 1 }
