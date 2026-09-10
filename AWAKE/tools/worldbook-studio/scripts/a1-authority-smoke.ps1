param(
    [switch]$Extended,
    [string]$EvidencePath
)

$ErrorActionPreference = 'Stop'

$invocationContract = 'pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\a1-authority-smoke.ps1 -Extended -EvidencePath <TEMP>'

$root = Split-Path -Parent $PSScriptRoot
$schemaRoot = Join-Path $root '..\..\docs\worldbook-studio-plan'
$cliDll = Join-Path $root 'src\Awake.WorldbookStudio.Cli\bin\Release\net10.0\worldbook-studio.dll'
$webDll = Join-Path $root 'src\Awake.WorldbookStudio.Web\bin\Release\net10.0\Awake.WorldbookStudio.Web.dll'
$schemaFixtureRoot = Join-Path $root 'artifacts\current-test\WorldbookStudio\schemas'
$workerPort = 0
$webPort = 0
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('awake-worldbook-studio-a1-' + [Guid]::NewGuid().ToString('N'))
$workspace = Join-Path $tempRoot 'workspace'
$localAppData = Join-Path $tempRoot 'LocalAppData'
$workerScript = Join-Path $tempRoot 'worker.ps1'
$workerReady = Join-Path $tempRoot 'worker.ready'
$secret = 'a1-worker-secret-20260823'
$workerProcess = $null
$webProcess = $null

function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Select-TestWebPort {
    if (-not [string]::IsNullOrWhiteSpace($env:AWAKE_WB_TEST_PORT)) { return [int]$env:AWAKE_WB_TEST_PORT }
    return Select-FreePort
}

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "A1 smoke failed: $Message" }
}

function Invoke-DotnetApp([string]$Dll, [string[]]$Arguments, [hashtable]$Environment) {
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = (Get-Command dotnet).Source
    $psi.WorkingDirectory = $root
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.ArgumentList.Add($Dll)
    foreach ($argument in $Arguments) { $psi.ArgumentList.Add($argument) }
    foreach ($entry in $Environment.GetEnumerator()) { $psi.Environment[$entry.Key] = [string]$entry.Value }
    $process = [System.Diagnostics.Process]::Start($psi)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    [pscustomobject]@{ ExitCode = $process.ExitCode; Stdout = $stdout; Stderr = $stderr }
}

function Start-DotnetApp([string]$Dll, [hashtable]$Environment) {
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = (Get-Command dotnet).Source
    $psi.WorkingDirectory = $root
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.ArgumentList.Add($Dll)
    foreach ($entry in $Environment.GetEnumerator()) { $psi.Environment[$entry.Key] = [string]$entry.Value }
    return [System.Diagnostics.Process]::Start($psi)
}

function Read-Json([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { throw 'JSON output was empty.' }
    return $Text | ConvertFrom-Json
}

function Invoke-WebJson([Microsoft.PowerShell.Commands.WebRequestSession]$Session, [string]$Method, [string]$Uri, [hashtable]$Headers, [string]$Body) {
    try {
        $response = Invoke-WebRequest -Method $Method -Uri $Uri -Headers $Headers -Body $Body -ContentType 'application/json' -WebSession $Session -UseBasicParsing -SkipHttpErrorCheck
        return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Body = $response.Content }
    }
    catch {
        throw "Web request failed: $Method $Uri - $($_.Exception.Message)"
    }
}

function Invoke-ExtendedSmoke {
    param(
        [string]$Root,
        [string]$SchemaRoot,
        [string]$CliDll,
        [string]$WebDll,
        [string]$SchemaFixtureRoot,
        [string]$EvidencePath
    )

    $goldenPath = Join-Path $Root 'tests\fixtures\a4-cli-web-contract-golden.v1.json'
    $golden = Get-Content -LiteralPath $goldenPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $expectedCases = @($golden.smoke_cases)
    $requiredCaseIds = @('cli-invalid-provider', 'cli-consent-success', 'cli-analyze-success', 'cli-apply-success', 'cli-reject-success', 'cli-wrong-nonce', 'cli-wrong-cas', 'web-session-bootstrap', 'web-invalid-csrf', 'web-invalid-session', 'web-invalid-consent', 'web-invalid-provider', 'web-consent-success', 'web-analyze-success', 'web-apply-success', 'web-reject-success', 'web-wrong-nonce', 'web-wrong-cas')
    if ((@($expectedCases | ForEach-Object { $_.id }) -join '|') -cne ($requiredCaseIds -join '|')) { throw 'A4 smoke case manifest does not match the fixed 18-case order.' }
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('awake-worldbook-studio-a4-' + [Guid]::NewGuid().ToString('N'))
    $workspace = Join-Path $tempRoot 'workspace'
    $localAppData = Join-Path $tempRoot 'LocalAppData'
    $workerScript = Join-Path $tempRoot 'worker.ps1'
    $workerReady = Join-Path $tempRoot 'worker.ready'
    $workerPort = Select-FreePort
    $webPort = Select-TestWebPort
    while ($webPort -eq $workerPort) { $workerPort = Select-FreePort }
    $secret = 'a4-worker-secret-20260823'
    $workerProcess = $null
    $webProcess = $null
    $failed = $false
    $script:extendedSmokeFailed = $false
    $startedAt = [DateTimeOffset]::UtcNow
    $releaseArtifacts = @(
        [ordered]@{ role = 'core'; path = 'src\Awake.WorldbookStudio.Core\bin\Release\net10.0\Awake.WorldbookStudio.Core.dll'; sha256 = $null },
        [ordered]@{ role = 'cli'; path = 'src\Awake.WorldbookStudio.Cli\bin\Release\net10.0\worldbook-studio.dll'; sha256 = $null },
        [ordered]@{ role = 'web'; path = 'src\Awake.WorldbookStudio.Web\bin\Release\net10.0\Awake.WorldbookStudio.Web.dll'; sha256 = $null }
    )
    foreach ($artifact in $releaseArtifacts) {
        $artifactPath = Join-Path $Root $artifact.path
        if (Test-Path -LiteralPath $artifactPath -PathType Leaf) { $artifact.sha256 = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant() }
        else { $failed = $true }
    }
    $caseResults = @($expectedCases | ForEach-Object {
        [ordered]@{
            id = $_.id
            boundary = $_.boundary
            operation = $_.operation
            expected_exit_code = $_.expected_exit_code
            actual_exit_code = $null
            expected_http_status = $_.expected_http_status
            actual_http_status = $null
            expected_error_code = $_.expected_error_code
            actual_error_code = $null
            expected_safe_message = $_.expected_safe_message
            actual_safe_message = $null
            success_markers = @($_.success_markers)
            passed = $false
        }
    })
    $evidence = [ordered]@{
        schema_version = $golden.smoke_schema_version
        batch_id = 'worldbook-studio-a4-cli-web-20260823'
        started_at_utc = $startedAt.ToString('O')
        finished_at_utc = $null
        working_directory = $Root
        command = $invocationContract
        release_artifacts = $releaseArtifacts
        fake_worker_artifact_path = 'worker.ps1'
        fake_worker_artifact_sha256 = $null
        fake_worker_dll_sha256 = $null
        cases = $caseResults
        cleanup = [ordered]@{ web_process_stopped = $false; worker_process_stopped = $false; temp_workspace_removed = $false }
        game_directory_touched = $false
        real_provider = $false
        real_worker = $false
    }

    function Set-CaseResult([int]$Index, $Actual, [bool]$MarkersOk = $true) {
        $expected = $expectedCases[$Index]
        $case = $caseResults[$Index]
        $case.actual_exit_code = $Actual.ExitCode
        $case.actual_http_status = $Actual.HttpStatus
        $case.actual_error_code = $Actual.ErrorCode
        $case.actual_safe_message = $Actual.SafeMessage
        $markersSatisfied = ($case.success_markers.Count -eq 0) -or $Actual.MarkersOk
        $passed = ($case.expected_exit_code -eq $case.actual_exit_code) -and ($case.expected_http_status -eq $case.actual_http_status) -and ($case.expected_error_code -eq $case.actual_error_code) -and ($case.expected_safe_message -eq $case.actual_safe_message) -and $MarkersOk -and $markersSatisfied
        $case.passed = $passed
        if (-not $case.passed) { $script:extendedSmokeFailed = $true }
    }

    function Invoke-Case([int]$Index, [scriptblock]$Action) {
        try { Set-CaseResult $Index (& $Action) }
        catch {
            $script:extendedSmokeFailed = $true
            $case = $caseResults[$Index]
            $case.passed = $false
        }
    }

    function Assert-Markers($Json, $ExpectedMarkers) {
        foreach ($marker in @($ExpectedMarkers)) {
            if (-not $Json.PSObject.Properties.Name.Contains($marker)) { return $false }
        }
        return $true
    }

    function CliActual($Result, $Json, [bool]$MarkersOk) {
        $errorValue = if ($Json -and $Json.error) { [string]$Json.error } else { $null }
        [pscustomobject]@{
            ExitCode = $Result.ExitCode
            HttpStatus = $null
            ErrorCode = if ($errorValue) { $errorValue.Split(':', 2)[0] } else { $null }
            SafeMessage = $errorValue
            MarkersOk = $MarkersOk
        }
    }

    function WebActual($Response, $Json, [bool]$MarkersOk) {
        [pscustomobject]@{
            ExitCode = $null
            HttpStatus = $Response.StatusCode
            ErrorCode = if ($Json -and $Json.error) { [string]$Json.error } else { $null }
            SafeMessage = if ($Json -and $Json.message) { [string]$Json.message } else { $null }
            MarkersOk = $MarkersOk
        }
    }

    try {
        if (-not $EvidencePath) { throw 'A4 smoke requires -EvidencePath.' }
        New-Item -ItemType Directory -Force -Path (Join-Path $workspace 'authoring\sources'), $localAppData | Out-Null
        Copy-Item -LiteralPath (Join-Path $SchemaFixtureRoot 'fixture-valid-authoring-minimal.yaml') -Destination (Join-Path $workspace 'authoring\demo.yaml')
        Copy-Item -LiteralPath (Join-Path $SchemaFixtureRoot 'source-fixture-demo.yaml') -Destination (Join-Path $workspace 'authoring\sources\source-demo.yaml')
        Copy-Item -LiteralPath (Join-Path $SchemaFixtureRoot 'source-demo.txt') -Destination (Join-Path $workspace 'authoring\sources\source-demo.txt')
        $worker = @'
param([int]$Port, [string]$Secret, [string]$ReadyFile)
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Start()
[System.IO.File]::WriteAllText($ReadyFile, 'ready')
function Base64Url([byte[]]$Bytes) { return [Convert]::ToBase64String($Bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=') }
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
    try { return Base64Url $hmac.ComputeHash($buffer) } finally { $hmac.Dispose() }
}
try {
    while ($true) {
        $context = $listener.GetContext()
        try {
            $reader = [IO.StreamReader]::new($context.Request.InputStream, $context.Request.ContentEncoding)
            try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $request = if ([string]::IsNullOrWhiteSpace($body)) { $null } else { $body | ConvertFrom-Json }
            if ($context.Request.Url.AbsolutePath -eq '/awake/handshake') {
                $protocol = 'awake.worker.v1'
                $workerId = 'a4-worker'
                $timestamp = [long]$request.timestamp
                $response = @{ protocol = $protocol; client_nonce = $request.client_nonce; worker_id = $workerId; timestamp = $timestamp; signature = (Signature $protocol $request.client_nonce $workerId $timestamp $request.request_hash) } | ConvertTo-Json -Compress
                $status = 200
            }
            elseif ($context.Request.Url.AbsolutePath -eq '/awake/analyze') {
                $patch = @{ schema_version = 'knowledge-patch.v1'; operations = @(@{ op = 'replace'; path = '/title/zh-CN'; value = 'A4 smoke title' }) }
                $suggestion = @{ id = ('a4-suggestion-' + [Guid]::NewGuid().ToString('N')); kind = 'prose'; severity = 'info'; confidence = 0.5; title = 'A4 smoke'; reason = '只用于 A4 入口契约核验。'; candidate_text = 'A4 smoke candidate'; patch = $patch; review_only = $true }
                $response = @{ schema_version = 'assistance.result.v1'; request_hash = $request.request.request_hash; source_document_hash = $request.request.source_document_hash; suggestions = @($suggestion) } | ConvertTo-Json -Compress -Depth 20
                $status = 200
            }
            else { $response = '{"error":"not found"}'; $status = 404 }
            $bytes = [Text.Encoding]::UTF8.GetBytes($response)
            $context.Response.StatusCode = $status
            $context.Response.ContentType = 'application/json'
            $context.Response.ContentLength64 = $bytes.Length
            $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            $context.Response.Close()
        }
        catch { try { $context.Response.StatusCode = 500; $context.Response.Close() } catch { } }
    }
}
finally { $listener.Stop(); $listener.Close() }
'@
        [System.IO.File]::WriteAllText($workerScript, $worker, [Text.UTF8Encoding]::new($false))
        $evidence.fake_worker_artifact_sha256 = (Get-FileHash -LiteralPath $workerScript -Algorithm SHA256).Hash.ToLowerInvariant()
        $workerProcess = Start-Process -FilePath (Get-Command pwsh).Source -WindowStyle Hidden -PassThru -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $workerScript, '-Port', $workerPort, '-Secret', $secret, '-ReadyFile', $workerReady)
        for ($attempt = 0; $attempt -lt 50 -and -not (Test-Path -LiteralPath $workerReady); $attempt++) { Start-Sleep -Milliseconds 100 }
        if (-not (Test-Path -LiteralPath $workerReady)) { throw 'local worker did not become ready' }
        $providerEnvironment = @{
            WORLD_BOOK_LOCAL_WORKER_URL = "http://127.0.0.1:$workerPort"
            WORLD_BOOK_LOCAL_WORKER_SECRET_ENV = 'A4_WORKER_SECRET'
            A4_WORKER_SECRET = $secret
            LOCALAPPDATA = $localAppData
        }
        $cliBase = @('--workspace', $workspace, '--schema-root', $SchemaRoot)
        $cliConsent = Invoke-DotnetApp $CliDll (@('ai-consent-preview') + $cliBase + @('--path', 'authoring/demo.yaml', '--provider', 'local', '--analysis', 'prose', '--buffer-id', 'a4-cli-main')) $providerEnvironment
        $cliConsentJson = Read-Json $cliConsent.Stdout
        $cliAnalyze = Invoke-DotnetApp $CliDll (@('ai-analyze') + $cliBase + @('--path', 'authoring/demo.yaml', '--consent', $cliConsentJson.consent_token)) $providerEnvironment
        $cliAnalyzeJson = Read-Json $cliAnalyze.Stdout
        $cliSuggestion = $cliAnalyzeJson.suggestions[0]
        Invoke-Case 0 { $result = Invoke-DotnetApp $CliDll (@('ai-consent-preview') + $cliBase + @('--path', 'authoring/demo.yaml', '--provider', 'invalid', '--analysis', 'consistency')) $providerEnvironment; CliActual $result (Read-Json $result.Stdout) $false }
        Invoke-Case 1 { CliActual $cliConsent $cliConsentJson (Assert-Markers $cliConsentJson $caseResults[1].success_markers) }
        Invoke-Case 2 { CliActual $cliAnalyze $cliAnalyzeJson (Assert-Markers $cliAnalyzeJson $caseResults[2].success_markers) }
        Invoke-Case 3 { $result = Invoke-DotnetApp $CliDll (@('ai-apply') + $cliBase + @('--path', 'authoring/demo.yaml', '--consent', $cliConsentJson.consent_token, '--buffer-id', $cliConsentJson.buffer_id, '--suggestion', $cliSuggestion.id, '--apply-nonce', $cliSuggestion.apply_nonce)) $providerEnvironment; $json = Read-Json $result.Stdout; CliActual $result $json $false }
        $cliRejectConsent = Invoke-DotnetApp $CliDll (@('ai-consent-preview') + $cliBase + @('--path', 'authoring/demo.yaml', '--provider', 'local', '--analysis', 'prose', '--buffer-id', 'a4-cli-reject')) $providerEnvironment
        $cliRejectConsentJson = Read-Json $cliRejectConsent.Stdout
        $cliRejectAnalyze = Invoke-DotnetApp $CliDll (@('ai-analyze') + $cliBase + @('--path', 'authoring/demo.yaml', '--consent', $cliRejectConsentJson.consent_token)) $providerEnvironment
        $cliRejectAnalyzeJson = Read-Json $cliRejectAnalyze.Stdout
        $cliRejectSuggestion = $cliRejectAnalyzeJson.suggestions[0]
        Invoke-Case 4 { $result = Invoke-DotnetApp $CliDll (@('ai-reject') + $cliBase + @('--path', 'authoring/demo.yaml', '--consent', $cliRejectConsentJson.consent_token, '--buffer-id', $cliRejectConsentJson.buffer_id, '--suggestion', $cliRejectSuggestion.id, '--apply-nonce', $cliRejectSuggestion.apply_nonce)) $providerEnvironment; $json = Read-Json $result.Stdout; CliActual $result $json $false }
        $cliWrongConsent = Invoke-DotnetApp $CliDll (@('ai-consent-preview') + $cliBase + @('--path', 'authoring/demo.yaml', '--provider', 'local', '--analysis', 'prose', '--buffer-id', 'a4-cli-wrong')) $providerEnvironment
        $cliWrongConsentJson = Read-Json $cliWrongConsent.Stdout
        $cliWrongAnalyze = Invoke-DotnetApp $CliDll (@('ai-analyze') + $cliBase + @('--path', 'authoring/demo.yaml', '--consent', $cliWrongConsentJson.consent_token)) $providerEnvironment
        $cliWrongAnalyzeJson = Read-Json $cliWrongAnalyze.Stdout
        $cliWrongSuggestion = $cliWrongAnalyzeJson.suggestions[0]
        Invoke-Case 5 { $result = Invoke-DotnetApp $CliDll (@('ai-apply') + $cliBase + @('--path', 'authoring/demo.yaml', '--consent', $cliWrongConsentJson.consent_token, '--buffer-id', $cliWrongConsentJson.buffer_id, '--suggestion', $cliWrongSuggestion.id, '--apply-nonce', 'wrong-a4-nonce')) $providerEnvironment; $json = Read-Json $result.Stdout; CliActual $result $json $false }
        Invoke-Case 6 { $result = Invoke-DotnetApp $CliDll (@('ai-apply') + $cliBase + @('--path', 'authoring/demo.yaml', '--consent', $cliWrongConsentJson.consent_token, '--buffer-id', $cliWrongConsentJson.buffer_id, '--suggestion', 'missing-a4-suggestion', '--apply-nonce', $cliWrongSuggestion.apply_nonce)) $providerEnvironment; $json = Read-Json $result.Stdout; CliActual $result $json $false }

        $webEnvironment = $providerEnvironment.Clone()
        $webEnvironment['AWAKE_WB_DEV_MODE'] = '1'
        $webEnvironment['WORLD_BOOK_WORKSPACE'] = $workspace
        $webEnvironment['WORLD_BOOK_SCHEMA_ROOT'] = $SchemaRoot
        $webEnvironment['AWAKE_WB_PORT'] = [string]$webPort
        $webEnvironment['ASPNETCORE_ENVIRONMENT'] = 'Development'
        $webEnvironment['DOTNET_ENVIRONMENT'] = 'Development'
        $webProcess = Start-DotnetApp $WebDll $webEnvironment
        $health = $false
        for ($attempt = 0; $attempt -lt 60 -and -not $health; $attempt++) { try { $health = (Invoke-WebRequest -Uri "http://127.0.0.1:$webPort/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { Start-Sleep -Milliseconds 250 } }
        if (-not $health) { throw 'Web process did not become healthy' }
        $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
        $origin = "http://127.0.0.1:$webPort"
        $bootstrap = Invoke-WebJson $session 'Post' "$origin/api/ai/session/bootstrap" @{ Origin = $origin } ''
        $bootstrapJson = Read-Json $bootstrap.Body
        Invoke-Case 7 { WebActual $bootstrap $bootstrapJson (Assert-Markers $bootstrapJson $caseResults[7].success_markers) }
        $headers = @{ Origin = $origin; 'X-AWAKE-CSRF' = $bootstrapJson.csrfToken }
        Invoke-Case 8 { $badHeaders = @{ Origin = $origin; 'X-AWAKE-CSRF' = 'invalid-a4-csrf' }; $response = Invoke-WebJson $session 'Post' "$origin/api/ai/consent-preview" $badHeaders (@{ path = 'authoring/demo.yaml'; providerId = 'local'; analysis = 'consistency'; focus = 'general' } | ConvertTo-Json -Compress); $json = Read-Json $response.Body; WebActual $response $json $false }
        Invoke-Case 9 { $noSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new(); $response = Invoke-WebJson $noSession 'Post' "$origin/api/ai/consent-preview" @{ Origin = $origin; 'X-AWAKE-CSRF' = 'no-session' } (@{ path = 'authoring/demo.yaml'; providerId = 'local'; analysis = 'consistency'; focus = 'general' } | ConvertTo-Json -Compress); $json = Read-Json $response.Body; WebActual $response $json $false }
        Invoke-Case 10 { $response = Invoke-WebJson $session 'Post' "$origin/api/ai/analyze" $headers (@{ consentToken = 'invalid-a4-consent' } | ConvertTo-Json -Compress); $json = Read-Json $response.Body; WebActual $response $json $false }
        Invoke-Case 11 { $response = Invoke-WebJson $session 'Post' "$origin/api/ai/consent-preview" $headers (@{ path = 'authoring/demo.yaml'; providerId = 'invalid'; analysis = 'consistency'; focus = 'general' } | ConvertTo-Json -Compress); $json = Read-Json $response.Body; WebActual $response $json $false }
        $webConsent = Invoke-WebJson $session 'Post' "$origin/api/ai/consent-preview" $headers (@{ path = 'authoring/demo.yaml'; providerId = 'local'; analysis = 'prose'; focus = 'world_style' } | ConvertTo-Json -Compress)
        $webConsentJson = Read-Json $webConsent.Body
        Invoke-Case 12 { WebActual $webConsent $webConsentJson (Assert-Markers $webConsentJson $caseResults[12].success_markers) }
        $webAnalyze = Invoke-WebJson $session 'Post' "$origin/api/ai/analyze" $headers (@{ consentToken = $webConsentJson.consentToken } | ConvertTo-Json -Compress)
        $webAnalyzeJson = Read-Json $webAnalyze.Body
        $webSuggestion = $webAnalyzeJson.suggestions[0]
        Invoke-Case 13 { WebActual $webAnalyze $webAnalyzeJson (Assert-Markers $webAnalyzeJson $caseResults[13].success_markers) }
        Invoke-Case 14 {
            $response = Invoke-WebJson $session 'Post' "$origin/api/ai/authoring/apply" $headers (@{ path = 'authoring/demo.yaml'; bufferId = $webConsentJson.bufferId; suggestionId = $webSuggestion.id; applyNonce = $webSuggestion.applyNonce } | ConvertTo-Json -Compress)
            $json = Read-Json $response.Body
            WebActual $response $json (Assert-Markers $json $caseResults[14].success_markers)
            if ($json.canApplyToAuthorMode -ne $true -or $json.authorTarget.step -ne 0 -or $json.authorTarget.focusId -ne 'doc-title' -or $null -eq $json.editorDocument.model) { throw 'Web apply did not return the author-mode title projection.' }
        }
        $webRejectConsent = Invoke-WebJson $session 'Post' "$origin/api/ai/consent-preview" $headers (@{ path = 'authoring/demo.yaml'; providerId = 'local'; analysis = 'prose'; focus = 'world_style' } | ConvertTo-Json -Compress)
        $webRejectConsentJson = Read-Json $webRejectConsent.Body
        $webRejectAnalyze = Invoke-WebJson $session 'Post' "$origin/api/ai/analyze" $headers (@{ consentToken = $webRejectConsentJson.consentToken } | ConvertTo-Json -Compress)
        $webRejectAnalyzeJson = Read-Json $webRejectAnalyze.Body
        $webRejectSuggestion = $webRejectAnalyzeJson.suggestions[0]
        Invoke-Case 15 { $response = Invoke-WebJson $session 'Post' "$origin/api/ai/authoring/reject" $headers (@{ path = 'authoring/demo.yaml'; bufferId = $webRejectConsentJson.bufferId; suggestionId = $webRejectSuggestion.id; applyNonce = $webRejectSuggestion.applyNonce } | ConvertTo-Json -Compress); $json = Read-Json $response.Body; WebActual $response $json (Assert-Markers $json $caseResults[15].success_markers) }
        $webWrongConsent = Invoke-WebJson $session 'Post' "$origin/api/ai/consent-preview" $headers (@{ path = 'authoring/demo.yaml'; providerId = 'local'; analysis = 'prose'; focus = 'world_style' } | ConvertTo-Json -Compress)
        $webWrongConsentJson = Read-Json $webWrongConsent.Body
        $webWrongAnalyze = Invoke-WebJson $session 'Post' "$origin/api/ai/analyze" $headers (@{ consentToken = $webWrongConsentJson.consentToken } | ConvertTo-Json -Compress)
        $webWrongAnalyzeJson = Read-Json $webWrongAnalyze.Body
        $webWrongSuggestion = $webWrongAnalyzeJson.suggestions[0]
        Invoke-Case 16 { $response = Invoke-WebJson $session 'Post' "$origin/api/ai/authoring/apply" $headers (@{ path = 'authoring/demo.yaml'; bufferId = $webWrongConsentJson.bufferId; suggestionId = $webWrongSuggestion.id; applyNonce = 'wrong-a4-nonce' } | ConvertTo-Json -Compress); $json = Read-Json $response.Body; WebActual $response $json $false }
        Invoke-Case 17 { $response = Invoke-WebJson $session 'Post' "$origin/api/ai/authoring/apply" $headers (@{ path = 'authoring/demo.yaml'; bufferId = $webWrongConsentJson.bufferId; suggestionId = 'missing-a4-suggestion'; applyNonce = $webWrongSuggestion.applyNonce } | ConvertTo-Json -Compress); $json = Read-Json $response.Body; WebActual $response $json $false }
    }
    catch { $failed = $true }
    finally {
        if ($webProcess -and -not $webProcess.HasExited) { try { $webProcess.Kill($true); $webProcess.WaitForExit() } catch { } }
        if ($workerProcess -and -not $workerProcess.HasExited) { try { $workerProcess.Kill($true); $workerProcess.WaitForExit() } catch { } }
        $evidence.cleanup.web_process_stopped = (-not $webProcess -or $webProcess.HasExited)
        $evidence.cleanup.worker_process_stopped = (-not $workerProcess -or $workerProcess.HasExited)
        if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }
        $evidence.cleanup.temp_workspace_removed = -not (Test-Path -LiteralPath $tempRoot)
        $evidence.finished_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
        $failed = $failed -or $script:extendedSmokeFailed -or @($caseResults | Where-Object { -not $_.passed }).Count -gt 0
        $archiveDir = [System.IO.Path]::GetFullPath((Join-Path $Root '..\..\docs\evidence'))
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $EvidencePath), $archiveDir | Out-Null
        $json = ($evidence | ConvertTo-Json -Depth 30) -replace "`r`n", "`n"
        [System.IO.File]::WriteAllText($EvidencePath, $json, [Text.UTF8Encoding]::new($false))
        $archivePath = Join-Path $archiveDir 'WORLDBOOK-STUDIO-A4-CLI-WEB-20260823-smoke.json'
        [System.IO.File]::WriteAllText($archivePath, $json, [Text.UTF8Encoding]::new($false))
    }
    if ($failed) { Write-Error 'A4 extended smoke failed.'; return 1 }
    Write-Output 'PASS: Worldbook Studio A4 CLI/Web extended smoke'
    return 0
}

if ($Extended) {
    if ([string]::IsNullOrWhiteSpace($EvidencePath)) { Write-Error 'A4 extended smoke requires -EvidencePath.'; exit 1 }
    $extendedExitCode = Invoke-ExtendedSmoke -Root $root -SchemaRoot $schemaRoot -CliDll $cliDll -WebDll $webDll -SchemaFixtureRoot $schemaFixtureRoot -EvidencePath $EvidencePath
    exit $extendedExitCode
}

try {
    foreach ($path in @($cliDll, $webDll, (Join-Path $schemaRoot 'awake.worldbook.authoring.v1.schema.json'))) { Assert (Test-Path -LiteralPath $path -PathType Leaf) "missing build or schema file: $path" }
    New-Item -ItemType Directory -Force -Path (Join-Path $workspace 'authoring\sources'), $localAppData | Out-Null
    Copy-Item -LiteralPath (Join-Path $schemaFixtureRoot 'fixture-valid-minimal.yaml') -Destination (Join-Path $workspace 'authoring\demo.yaml')
    Copy-Item -LiteralPath (Join-Path $schemaFixtureRoot 'source-fixture-demo.yaml') -Destination (Join-Path $workspace 'authoring\sources\source-demo.yaml')
    Copy-Item -LiteralPath (Join-Path $schemaFixtureRoot 'source-demo.txt') -Destination (Join-Path $workspace 'authoring\sources\source-demo.txt')

    $workerPort = Select-FreePort
    $webPort = Select-TestWebPort
    while ($webPort -eq $workerPort) { $workerPort = Select-FreePort }
    $worker = @'
param([int]$Port, [string]$Secret, [string]$ReadyFile)
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Start()
[System.IO.File]::WriteAllText($ReadyFile, 'ready')
function Base64Url([byte[]]$Bytes) { return [Convert]::ToBase64String($Bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=') }
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
    try { return Base64Url $hmac.ComputeHash($buffer) } finally { $hmac.Dispose() }
}
try {
    while ($true) {
        $context = $listener.GetContext()
        try {
            $reader = [IO.StreamReader]::new($context.Request.InputStream, $context.Request.ContentEncoding)
            try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $request = if ([string]::IsNullOrWhiteSpace($body)) { $null } else { $body | ConvertFrom-Json }
            if ($context.Request.Url.AbsolutePath -eq '/awake/handshake') {
                $protocol = 'awake.worker.v1'
                $workerId = 'a1-worker'
                $timestamp = [long]$request.timestamp
                $response = @{ protocol = $protocol; client_nonce = $request.client_nonce; worker_id = $workerId; timestamp = $timestamp; signature = (Signature $protocol $request.client_nonce $workerId $timestamp $request.request_hash) } | ConvertTo-Json -Compress
                $status = 200
            }
            elseif ($context.Request.Url.AbsolutePath -eq '/awake/analyze') {
                $patch = @{ schema_version = 'knowledge-patch.v1'; operations = @() }
                $suggestion = @{ id = 'a1-suggestion'; kind = 'prose'; severity = 'info'; confidence = 0.5; title = 'A1 smoke'; reason = '只用于入口契约核验。'; candidate_text = 'A1 smoke candidate'; patch = $patch; review_only = $true }
                $response = @{ schema_version = 'assistance.result.v1'; request_hash = $request.request.request_hash; source_document_hash = $request.request.source_document_hash; suggestions = @($suggestion) } | ConvertTo-Json -Compress -Depth 20
                $status = 200
            }
            else {
                $response = '{"error":"not found"}'
                $status = 404
            }
            $bytes = [Text.Encoding]::UTF8.GetBytes($response)
            $context.Response.StatusCode = $status
            $context.Response.ContentType = 'application/json'
            $context.Response.ContentLength64 = $bytes.Length
            $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            $context.Response.Close()
        }
        catch {
            try { $context.Response.StatusCode = 500; $context.Response.Close() } catch { }
        }
    }
}
finally { $listener.Stop(); $listener.Close() }
'@
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    [System.IO.File]::WriteAllText($workerScript, $worker, [Text.UTF8Encoding]::new($false))
    $workerProcess = Start-Process -FilePath (Get-Command pwsh).Source -WindowStyle Hidden -PassThru -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $workerScript, '-Port', $workerPort, '-Secret', $secret, '-ReadyFile', $workerReady)
    for ($attempt = 0; $attempt -lt 50 -and -not (Test-Path -LiteralPath $workerReady); $attempt++) { Start-Sleep -Milliseconds 100 }
    Assert (Test-Path -LiteralPath $workerReady) 'local worker did not become ready'

    $providerEnvironment = @{
        WORLD_BOOK_LOCAL_WORKER_URL = "http://127.0.0.1:$workerPort"
        WORLD_BOOK_LOCAL_WORKER_SECRET_ENV = 'A1_WORKER_SECRET'
        A1_WORKER_SECRET = $secret
        LOCALAPPDATA = $localAppData
    }
    $cliBase = @('--workspace', $workspace, '--schema-root', $schemaRoot)
    $invalidCli = Invoke-DotnetApp $cliDll (@('ai-consent-preview') + $cliBase + @('--path', 'authoring/demo.yaml', '--provider', 'invalid', '--analysis', 'consistency')) $providerEnvironment
    $invalidCliJson = Read-Json $invalidCli.Stdout
    Assert ($invalidCli.ExitCode -eq 3 -and $invalidCliJson.error -like 'WB-AI-PROVIDER-400:*') 'CLI invalid Provider contract changed'

    $consentCli = Invoke-DotnetApp $cliDll (@('ai-consent-preview') + $cliBase + @('--path', 'authoring/demo.yaml', '--provider', 'local', '--analysis', 'prose', '--buffer-id', 'a1-cli-buffer')) $providerEnvironment
    Assert ($consentCli.ExitCode -eq 0) "CLI consent failed: $($consentCli.Stdout) $($consentCli.Stderr)"
    $consentCliJson = Read-Json $consentCli.Stdout
    $analyzeCli = Invoke-DotnetApp $cliDll (@('ai-analyze') + $cliBase + @('--path', 'authoring/demo.yaml', '--consent', $consentCliJson.consent_token)) $providerEnvironment
    Assert ($analyzeCli.ExitCode -eq 0) "CLI analyze failed: $($analyzeCli.Stdout) $($analyzeCli.Stderr)"
    $cliSuggestion = (Read-Json $analyzeCli.Stdout).suggestions[0]

    $webEnvironment = $providerEnvironment.Clone()
    $webEnvironment['AWAKE_WB_DEV_MODE'] = '1'
    $webEnvironment['WORLD_BOOK_WORKSPACE'] = $workspace
    $webEnvironment['WORLD_BOOK_SCHEMA_ROOT'] = $schemaRoot
    $webEnvironment['AWAKE_WB_PORT'] = [string]$webPort
    $webEnvironment['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $webEnvironment['DOTNET_ENVIRONMENT'] = 'Development'
    $webProcess = Start-DotnetApp $webDll $webEnvironment
    $health = $false
    for ($attempt = 0; $attempt -lt 60 -and -not $health; $attempt++) {
        try { $health = (Invoke-WebRequest -Uri "http://127.0.0.1:$webPort/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { Start-Sleep -Milliseconds 250 }
    }
    Assert $health 'Web process did not become healthy'
    $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $origin = "http://127.0.0.1:$webPort"
    $bootstrap = Invoke-WebJson $session 'Post' "$origin/api/ai/session/bootstrap" @{ Origin = $origin } ''
    Assert ($bootstrap.StatusCode -eq 200) 'Web session bootstrap failed'
    $bootstrapJson = Read-Json $bootstrap.Body
    $headers = @{ Origin = $origin; 'X-AWAKE-CSRF' = $bootstrapJson.csrfToken }
    $invalidWeb = Invoke-WebJson $session 'Post' "$origin/api/ai/consent-preview" $headers (@{ path = 'authoring/demo.yaml'; providerId = 'invalid'; analysis = 'consistency'; focus = 'general' } | ConvertTo-Json -Compress)
    $invalidWebJson = Read-Json $invalidWeb.Body
    Assert ($invalidWeb.StatusCode -eq 400 -and $invalidWebJson.error -eq 'WB-AI-PROVIDER-400') 'Web invalid Provider contract changed'
    $consentWeb = Invoke-WebJson $session 'Post' "$origin/api/ai/consent-preview" $headers (@{ path = 'authoring/demo.yaml'; providerId = 'local'; analysis = 'prose'; focus = 'world_style' } | ConvertTo-Json -Compress)
    Assert ($consentWeb.StatusCode -eq 200) "Web consent failed: $($consentWeb.Body)"
    $consentWebJson = Read-Json $consentWeb.Body
    $analyzeWeb = Invoke-WebJson $session 'Post' "$origin/api/ai/analyze" $headers (@{ consentToken = $consentWebJson.consentToken } | ConvertTo-Json -Compress)
    Assert ($analyzeWeb.StatusCode -eq 200) "Web analyze failed: $($analyzeWeb.Body)"
    $webSuggestion = (Read-Json $analyzeWeb.Body).suggestions[0]

    $wireGolden = Read-Json (Get-Content -LiteralPath (Join-Path $root 'tests\fixtures\a1-wire-golden.v1.json') -Raw -Encoding UTF8)
    $cliFields = @($cliSuggestion.PSObject.Properties.Name)
    $webFields = @($webSuggestion.PSObject.Properties.Name)
    Assert ((ConvertTo-Json $cliFields -Compress) -eq (ConvertTo-Json @($wireGolden.cli_fields) -Compress)) 'CLI Suggestion wire field order changed'
    Assert ((ConvertTo-Json $webFields -Compress) -eq (ConvertTo-Json @($wireGolden.web_fields) -Compress)) 'Web Suggestion wire field order changed'
    Assert ($cliSuggestion.id -eq $webSuggestion.id -and $cliSuggestion.kind -eq $webSuggestion.kind -and $cliSuggestion.candidate_text -eq $webSuggestion.candidateText -and $cliSuggestion.review_only -eq $webSuggestion.reviewOnly) 'CLI/Web Suggestion semantic fields diverged'
    Write-Output 'PASS: Worldbook Studio A1 CLI/Web authority smoke'
}
finally {
    if ($webProcess -and -not $webProcess.HasExited) { $webProcess.Kill($true); $webProcess.WaitForExit() }
    if ($workerProcess -and -not $workerProcess.HasExited) { $workerProcess.Kill($true); $workerProcess.WaitForExit() }
    if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
