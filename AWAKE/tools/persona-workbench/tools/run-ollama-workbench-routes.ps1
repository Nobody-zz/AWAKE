param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [string]$RequestCapturePath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$workbenchPort = 51337
$workbenchBaseUrl = "http://127.0.0.1:$workbenchPort"
$ollamaTagsUrl = "http://127.0.0.1:11434/api/tags"
$providerEndpoint = "http://127.0.0.1:11434/v1/chat/completions"
$providerModel = "gpt-oss:20b"
$providerDigest = "17052f91a42e97930aa6e28a6c6c06a983e6a58dbb00434885a0cf5313e376f7"
Add-Type -AssemblyName System.Net.Http
$fixturePath = Join-Path $env:USERPROFILE ".codex\skills\persona-workbench-ollama-pretest\references\test-fixtures.md"
$utf8 = New-Object System.Text.UTF8Encoding($false)
$httpClient = New-Object System.Net.Http.HttpClient
$httpClient.Timeout = [TimeSpan]::FromSeconds(180)
$webProcess = $null
$startedHere = $false
$previousCapturePath = [Environment]::GetEnvironmentVariable('PWB_REQUEST_CAPTURE_PATH', 'Process')
$captureRequested = -not [string]::IsNullOrWhiteSpace($RequestCapturePath)
$capturePathFull = $null
$captureEnvironmentSet = $false
$cleanup = [ordered]@{ attempted = $false; captureDeletionAttempted = $false; captureDeleted = $false; stopped = $false; portFree = $false; error = $null }
$stageResults = New-Object System.Collections.Generic.List[object]
$fixtures = $null
$packageMetadata = $null
$ollamaEvidence = $null
$runnerError = $null
$overallVerdict = "IN_DOUBT"

function Full([string]$path) { return [IO.Path]::GetFullPath($path) }
function Relative([string]$path, [string]$base) { return $path.Substring($base.Length).TrimStart([char]92, [char]47).Replace([char]47, [char]92) }
function GetBytesHash([byte[]]$bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToUpperInvariant() }
    finally { $sha.Dispose() }
}
function GetTextHash([string]$text) {
    if ($null -eq $text) { $text = [string]::Empty }
    return GetBytesHash ($utf8.GetBytes($text))
}
function ReadKeyValue([string[]]$lines, [string]$key) {
    $line = $lines | Where-Object { $_ -like "$key=*" } | Select-Object -First 1
    if ($null -eq $line) { throw "Package metadata is missing $key." }
    return $line.Substring($key.Length + 1)
}
function ParseJson([string]$body) {
    if ([string]::IsNullOrWhiteSpace($body)) { return $null }
    try { return $body | ConvertFrom-Json } catch { return $null }
}
function GetUsage([object]$payload) {
    if ($null -eq $payload -or $null -eq $payload.usage) { return $null }
    return [ordered]@{
        promptTokens = $payload.usage.promptTokens
        completionTokens = $payload.usage.completionTokens
        totalTokens = $payload.usage.totalTokens
    }
}
function InvokeHttp([string]$method, [string]$uri, [string]$body, [hashtable]$headers) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $request = [System.Net.Http.HttpRequestMessage]::new()
    $response = $null
    try {
        $request.Method = if ($method -eq 'POST') { [System.Net.Http.HttpMethod]::Post } else { [System.Net.Http.HttpMethod]::Get }
        $request.RequestUri = [Uri]$uri
        if ($method -eq 'POST' -and $null -ne $body) { $request.Content = [System.Net.Http.StringContent]::new($body, [Text.Encoding]::UTF8, 'application/json') }
        foreach ($headerName in ($headers.Keys | Sort-Object)) { [void]$request.Headers.TryAddWithoutValidation($headerName, [string]$headers[$headerName]) }
        $response = $httpClient.SendAsync($request).GetAwaiter().GetResult()
        $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $timer.Stop()
        return [ordered]@{
            status = [int]$response.StatusCode
            body = $responseBody
            durationMs = [Math]::Round($timer.Elapsed.TotalMilliseconds, 1)
            transportError = $null
        }
    } catch {
        $timer.Stop()
        return [ordered]@{
            status = $null
            body = ''
            durationMs = [Math]::Round($timer.Elapsed.TotalMilliseconds, 1)
            transportError = $_.Exception.Message
        }
    } finally {
        if ($null -ne $response) { $response.Dispose() }
        $request.Dispose()
    }
}
function GetPortOwnerId([int]$port) {
    $connections = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
    if ($connections.Count -eq 0) { return $null }
    if ($connections.Count -gt 1) { throw "PORT_OWNER_MISMATCH: multiple listeners own port $port ($($connections -join ', '))." }
    return [int]$connections[0]
}
function GetProcessExecutablePath([int]$processId) {
    $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $processId" -ErrorAction SilentlyContinue
    if ($null -eq $processInfo) { return $null }
    return $processInfo.ExecutablePath
}
function WaitForWorkbench([string]$url, [int]$timeoutSeconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)
    do {
        $health = InvokeHttp 'GET' $url $null @{}
        if ($health.status -ge 200 -and $health.status -lt 500) { return }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'WORKBENCH_START_FAILED: timed out waiting for the named package.'
}
function ReadFixtures() {
    if (-not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) { throw "FIXTURE_MISSING: $fixturePath" }
    $fixtureText = [IO.File]::ReadAllText($fixturePath)
    $shortMatch = [regex]::Match($fixtureText, '(?s)## Short Fixture\s*\r?\n\r?\n```text\r?\n(?<value>.*?)\r?\n```')
    $longMatch = [regex]::Match($fixtureText, '(?s)## Long Fixture\s*\r?\n\r?\n```text\r?\n(?<value>.*?)\r?\n```')
    if (-not $shortMatch.Success -or -not $longMatch.Success) { throw 'FIXTURE_MISSING: could not parse fixed short/long fixtures.' }
    $shortText = $shortMatch.Groups['value'].Value
    $longText = $longMatch.Groups['value'].Value
    $shortBytes = $utf8.GetBytes($shortText)
    $longBytes = $utf8.GetBytes($longText)
    $shortHash = GetBytesHash $shortBytes
    $longHash = GetBytesHash $longBytes
    if ($shortBytes.Length -ne 270 -or $shortHash -ne '3B82F664F26F11862C7DA899668AAA0635461F5CC3FDA711A8D7622CBA5EB968') { throw "FIXTURE_DRIFT: short bytes=$($shortBytes.Length), sha256=$shortHash" }
    if ($longBytes.Length -ne 3888 -or $longHash -ne '8234670F470A8F663D5436111B39CF10D97AE9AA1D123E6FF7C090561F93B752') { throw "FIXTURE_DRIFT: long bytes=$($longBytes.Length), sha256=$longHash" }
    return [ordered]@{
        short = [ordered]@{ text = $shortText; bytes = $shortBytes.Length; sha256 = $shortHash }
        long = [ordered]@{ text = $longText; bytes = $longBytes.Length; sha256 = $longHash }
    }
}
function ReadPackageMetadata([string]$packageFull) {
    $buildIdPath = Join-Path $packageFull 'BUILD-ID.txt'
    $sourceManifestPath = Join-Path $packageFull 'BUILD-SOURCE-MANIFEST.sha256.txt'
    $packageManifestPath = Join-Path $packageFull 'PACKAGE-MANIFEST.sha256.txt'
    foreach ($requiredPath in @($buildIdPath, $sourceManifestPath, $packageManifestPath)) { if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) { throw "PACKAGE_MISSING: package metadata is missing $requiredPath" } }
    $buildLines = @(Get-Content -LiteralPath $buildIdPath)
    $sourceHash = ReadKeyValue $buildLines 'SourceManifestHash'
    $buildId = ReadKeyValue $buildLines 'BuildId'
    $packageManifestHash = (Get-FileHash -LiteralPath $packageManifestPath -Algorithm SHA256).Hash.ToUpperInvariant()
    $executablePath = Join-Path $packageFull 'PersonaWorkbench.Web.exe'
    if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) { throw "PACKAGE_MISSING: $executablePath" }
    $executableHash = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash.ToUpperInvariant()
    $staticLines = @(Get-Content -LiteralPath $sourceManifestPath | Where-Object { $_ -match '  src\\PersonaWorkbench.Web\\wwwroot\\' })
    $staticHash = GetTextHash (($staticLines -join "`n") + "`n")
    return [ordered]@{
        path = $packageFull
        buildId = $buildId
        sourceManifestHash = $sourceHash
        packageManifestHash = $packageManifestHash
        executableHash = $executableHash
        wwwrootSourceHash = $staticHash
    }
}
function GetOllamaEvidence() {
    $response = InvokeHttp 'GET' $ollamaTagsUrl $null @{}
    if ($null -ne $response.transportError) { throw "IN_DOUBT: Ollama health request failed: $($response.transportError)" }
    if ($response.status -ne 200) { throw "IN_DOUBT: Ollama health returned HTTP $($response.status)." }
    $payload = ParseJson $response.body
    if ($null -eq $payload) { throw 'IN_DOUBT: Ollama health response was invalid JSON.' }
    $model = @($payload.models | Where-Object { $_.name -eq $providerModel } | Select-Object -First 1)
    if ($model.Count -eq 0 -or $model[0].digest -ne $providerDigest) { throw "MODEL_DIGEST_MISMATCH: expected $providerModel/$providerDigest." }
    return [ordered]@{ status = 'PASS'; model = $providerModel; digest = $providerDigest; durationMs = $response.durationMs }
}
function GetOutputEvidence([string]$text) {
    if ($null -eq $text) { return [ordered]@{ length = 0; sha256 = $null } }
    return [ordered]@{ length = $text.Length; sha256 = (GetTextHash $text) }
}
function InvokeExpansion([string]$fixtureName, [object]$fixture, [string]$sessionToken, [string]$csrfToken) {
    $requestBody = [ordered]@{
        endpoint = $providerEndpoint
        model = $providerModel
        providerProtocol = 'ollama'
        description = $fixture.text
        controls = [ordered]@{
            direction = ''
            focusPreset = 'balanced'
            focusKeywords = @()
            avoidTopics = @()
        }
    }
    $requestJson = $requestBody | ConvertTo-Json -Depth 8 -Compress
    $inputEvidence = [ordered]@{ source = $fixtureName + '-fixture'; bytes = $fixture.bytes; sha256 = $fixture.sha256 }
    $stageName = 'expand-' + $fixtureName
    $headers = @{ 'X-Pwb-Session' = $sessionToken; 'X-Pwb-Csrf' = $csrfToken }
    if ($captureRequested) { $headers['X-Pwb-Diagnostic-Stage'] = $stageName }
    $response = InvokeHttp 'POST' ($workbenchBaseUrl + '/api/provider/expand-description') $requestJson $headers
    $payload = ParseJson $response.body
    $jsonValid = $null -ne $payload
    $acceptedText = ''
    $errorCode = ''
    if ($jsonValid) {
        $acceptedText = [string]$payload.expandedText
        $errorCode = [string]$payload.errorCode
    }
    $passed = $null -eq $response.transportError -and $response.status -eq 200 -and $jsonValid -and -not [string]::IsNullOrWhiteSpace($acceptedText)
    $stageResult = 'FAIL'
    if ($passed) { $stageResult = 'PASS' } elseif ($null -ne $response.transportError) { $stageResult = 'IN_DOUBT' }
    $errorMessage = $errorCode
    if ($null -ne $response.transportError) { $errorMessage = $response.transportError } elseif (-not $jsonValid) { $errorMessage = 'response.json_invalid' }
    $acceptedOutput = $null
    if ($passed) { $acceptedOutput = $acceptedText }
    return [ordered]@{
        stage = $stageName
        inputSource = $inputEvidence.source
        transport = 'Workbench'
        result = $stageResult
        error = $errorMessage
        httpStatus = $response.status
        durationMs = $response.durationMs
        input = $inputEvidence
        output = (GetOutputEvidence $acceptedText)
        rawResponseBytes = $utf8.GetByteCount($response.body)
        rawResponseSha256 = (GetTextHash $response.body)
        jsonValid = $jsonValid
        usage = (GetUsage $payload)
        failureNoAcceptedPayload = -not $passed -and [string]::IsNullOrWhiteSpace($acceptedText)
        requestHash = (GetTextHash $requestJson)
        accepted = $passed
        expandedText = $acceptedOutput
    }
}
function InvokeDsl([string]$fixtureName, [object]$fixture, [string]$inputSource, [string]$sourceText, [string]$sessionToken, [string]$csrfToken) {
    $requestBody = [ordered]@{
        endpoint = $providerEndpoint
        model = $providerModel
        providerProtocol = 'ollama'
        sourceText = $sourceText
        localId = 'free.ollama.' + $fixtureName
        localDisplayName = '阿芙洛狄忒'
    }
    $requestJson = $requestBody | ConvertTo-Json -Depth 8 -Compress
    $inputBytes = $utf8.GetBytes($sourceText)
    $inputEvidence = [ordered]@{ source = $inputSource; bytes = $inputBytes.Length; sha256 = (GetBytesHash $inputBytes) }
    $stageName = 'convert-' + $fixtureName
    $headers = @{ 'X-Pwb-Session' = $sessionToken; 'X-Pwb-Csrf' = $csrfToken }
    if ($captureRequested) { $headers['X-Pwb-Diagnostic-Stage'] = $stageName }
    $response = InvokeHttp 'POST' ($workbenchBaseUrl + '/api/provider/convert-to-dsl') $requestJson $headers
    $payload = ParseJson $response.body
    $jsonValid = $null -ne $payload
    $dsl = ''
    $errorCode = ''
    if ($jsonValid) {
        $dsl = [string]$payload.dsl
        $errorCode = [string]$payload.errorCode
    }
    $draftPresent = $jsonValid -and $null -ne $payload.draft
    $passed = $null -eq $response.transportError -and $response.status -eq 200 -and $jsonValid -and -not [string]::IsNullOrWhiteSpace($dsl) -and $draftPresent
    $stageResult = 'FAIL'
    if ($passed) { $stageResult = 'PASS' } elseif ($null -ne $response.transportError) { $stageResult = 'IN_DOUBT' }
    $errorMessage = $errorCode
    if ($null -ne $response.transportError) { $errorMessage = $response.transportError } elseif (-not $jsonValid) { $errorMessage = 'response.json_invalid' }
    $acceptedOutput = $null
    if ($passed) { $acceptedOutput = $dsl }
    return [ordered]@{
        stage = $stageName
        inputSource = $inputSource
        transport = 'Workbench'
        result = $stageResult
        error = $errorMessage
        httpStatus = $response.status
        durationMs = $response.durationMs
        input = $inputEvidence
        output = (GetOutputEvidence $dsl)
        rawResponseBytes = $utf8.GetByteCount($response.body)
        rawResponseSha256 = (GetTextHash $response.body)
        jsonValid = $jsonValid
        draftPresent = $draftPresent
        usage = (GetUsage $payload)
        failureNoAcceptedPayload = -not $passed -and ([string]::IsNullOrWhiteSpace($dsl) -or -not $draftPresent)
        requestHash = (GetTextHash $requestJson)
        accepted = $passed
        dsl = $acceptedOutput
    }
}

$packageFull = Full $PackagePath
$reportFull = Full $ReportPath
$capturePathFull = if ($captureRequested) { Full $RequestCapturePath } else { $null }
$workspaceRoot = Full (Join-Path $PSScriptRoot '..')
$reportParent = Split-Path -Parent $reportFull
if (-not (Test-Path -LiteralPath $packageFull -PathType Container)) { $runnerError = 'PACKAGE_MISSING: package directory is missing.' }
elseif (-not (Test-Path -LiteralPath (Join-Path $packageFull 'PersonaWorkbench.Web.exe') -PathType Leaf)) { $runnerError = 'PACKAGE_MISSING: package executable is missing.' }
elseif (-not $reportFull.StartsWith(($workspaceRoot.TrimEnd([char]92, [char]47) + [char]92), [StringComparison]::OrdinalIgnoreCase)) { $runnerError = 'REPORT_PATH_INVALID: report must be under the PersonaWorkbench workspace.' }
else {
    New-Item -ItemType Directory -Force -Path $reportParent | Out-Null
    try {
        if ($captureRequested) {
            if (-not $capturePathFull.StartsWith(($workspaceRoot.TrimEnd([char]92, [char]47) + [char]92), [StringComparison]::OrdinalIgnoreCase)) { throw 'CAPTURE_PATH_INVALID: capture must be under the PersonaWorkbench workspace.' }
            if (Test-Path -LiteralPath $capturePathFull) { throw "CAPTURE_PATH_EXISTS: capture path must be new: $capturePathFull" }
            $captureParent = Split-Path -Parent $capturePathFull
            if (-not [string]::IsNullOrWhiteSpace($captureParent)) { New-Item -ItemType Directory -Force -Path $captureParent | Out-Null }
        }
        $fixtures = ReadFixtures
        $packageMetadata = ReadPackageMetadata $packageFull
        $ollamaEvidence = GetOllamaEvidence
        $ownerId = GetPortOwnerId $workbenchPort
        $executablePath = Full (Join-Path $packageFull 'PersonaWorkbench.Web.exe')
        if ($null -ne $ownerId) {
            if ($captureRequested) { throw "PORT_OWNER_CAPTURE_MISMATCH: cannot attach capture to an already-running Workbench process." }
            $ownerPath = GetProcessExecutablePath $ownerId
            if ($null -eq $ownerPath -or (Full $ownerPath) -ine $executablePath) { throw "PORT_OWNER_MISMATCH: port $workbenchPort is owned by $ownerPath, expected $executablePath." }
            $webProcess = Get-Process -Id $ownerId -ErrorAction Stop
        } else {
            if ($captureRequested) {
                [Environment]::SetEnvironmentVariable('PWB_REQUEST_CAPTURE_PATH', $capturePathFull, 'Process')
                $captureEnvironmentSet = $true
            }
            $webProcess = Start-Process -FilePath $executablePath -ArgumentList '--no-browser' -WorkingDirectory $packageFull -PassThru -WindowStyle Hidden
            $startedHere = $true
        }
        WaitForWorkbench ($workbenchBaseUrl + '/') 45
        $postOwnerId = GetPortOwnerId $workbenchPort
        if ($null -eq $postOwnerId -or $postOwnerId -ne $webProcess.Id) { throw 'WORKBENCH_START_FAILED: named package does not own the listening port after startup.' }
        $launch = InvokeHttp 'POST' ($workbenchBaseUrl + '/api/session/launch-token') $null @{}
        if ($launch.status -ne 200 -or $null -ne $launch.transportError) { throw "IN_DOUBT: launch-token failed with HTTP $($launch.status): $($launch.transportError)" }
        $launchPayload = ParseJson $launch.body
        if ($null -eq $launchPayload -or [string]::IsNullOrWhiteSpace($launchPayload.token)) { throw 'IN_DOUBT: launch-token response was invalid.' }
        $bootstrapJson = (@{ token = $launchPayload.token } | ConvertTo-Json -Compress)
        $bootstrap = InvokeHttp 'POST' ($workbenchBaseUrl + '/api/session/bootstrap') $bootstrapJson @{}
        if ($bootstrap.status -ne 200 -or $null -ne $bootstrap.transportError) { throw "IN_DOUBT: session bootstrap failed with HTTP $($bootstrap.status): $($bootstrap.transportError)" }
        $grant = ParseJson $bootstrap.body
        if ($null -eq $grant -or [string]::IsNullOrWhiteSpace($grant.sessionToken) -or [string]::IsNullOrWhiteSpace($grant.csrfToken)) { throw 'IN_DOUBT: session bootstrap response was invalid.' }

        foreach ($fixtureName in @('short', 'long')) {
            $fixture = $fixtures[$fixtureName]
            $expansion = InvokeExpansion $fixtureName $fixture $grant.sessionToken $grant.csrfToken
            [void]$stageResults.Add($expansion)
            $conversionSource = if ($expansion.accepted) { $expansion.expandedText } else { $fixture.text }
            $conversionSourceName = if ($expansion.accepted) { 'expand-' + $fixtureName + '-output' } else { $fixtureName + '-fixture-fallback' }
            $conversion = InvokeDsl $fixtureName $fixture $conversionSourceName $conversionSource $grant.sessionToken $grant.csrfToken
            [void]$stageResults.Add($conversion)
        }
        $failedStage = @($stageResults | Where-Object { $_.result -ne 'PASS' })
        $overallVerdict = if ($failedStage.Count -eq 0) { 'PASS' } elseif (@($failedStage | Where-Object { $_.result -eq 'IN_DOUBT' }).Count -gt 0) { 'IN_DOUBT' } else { 'FAIL' }
    } catch {
        $runnerError = $_.Exception.Message
        if ($runnerError -like 'IN_DOUBT:*' -or $runnerError -like '*START_FAILED*' -or $runnerError -like 'MODEL_DIGEST_MISMATCH:*' -or $runnerError -like 'FIXTURE_*:*') { $overallVerdict = 'IN_DOUBT' } else { $overallVerdict = 'FAIL' }
    } finally {
        $cleanup.attempted = $true
        if ($null -ne $webProcess) {
            try {
                $currentPath = GetProcessExecutablePath $webProcess.Id
                if ($null -ne $currentPath -and (Full $currentPath) -ieq (Full (Join-Path $packageFull 'PersonaWorkbench.Web.exe'))) {
                    if (-not $webProcess.HasExited) { $webProcess.Kill(); [void]$webProcess.WaitForExit(10000) }
                    $cleanup.stopped = $true
                } else { $cleanup.error = 'cleanup refused to stop a process whose executable path no longer matches the package.' }
            } catch { $cleanup.error = $_.Exception.Message }
        }
        Start-Sleep -Milliseconds 250
        $cleanup.portFree = $null -eq (GetPortOwnerId $workbenchPort)
        if ($captureEnvironmentSet) {
            [Environment]::SetEnvironmentVariable('PWB_REQUEST_CAPTURE_PATH', $previousCapturePath, 'Process')
            $captureEnvironmentSet = $false
        }
    }
}

$reportPackage = if ($null -ne $packageMetadata) { $packageMetadata } else { [ordered]@{ path = $packageFull } }
$reportFixtures = $null
if ($null -ne $fixtures) {
    $reportFixtures = [ordered]@{
        short = [ordered]@{ bytes = $fixtures.short.bytes; sha256 = $fixtures.short.sha256 }
        long = [ordered]@{ bytes = $fixtures.long.bytes; sha256 = $fixtures.long.sha256 }
    }
}
$reportRoutes = @($stageResults | ForEach-Object { $_ })
$report = [ordered]@{
    schemaVersion = 'persona-workbench.ollama-workbench-routes.v1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    verdict = $overallVerdict
    error = $runnerError
    model = [ordered]@{ name = $providerModel; digest = $providerDigest; endpoint = $providerEndpoint; thinkingLevel = 'low' }
    package = $reportPackage
    fixtures = $reportFixtures
    workerDiagnostic = [ordered]@{ required = $true; executedBy = 'persona-workbench-ollama-pretest'; thinkingLevel = 'low'; result = 'not-run-by-this-runner' }
    requestCapture = [ordered]@{ requested = $captureRequested; path = $capturePathFull; exists = $captureRequested -and (Test-Path -LiteralPath $capturePathFull -PathType Leaf); sha256 = if ($captureRequested -and (Test-Path -LiteralPath $capturePathFull -PathType Leaf)) { (Get-FileHash -LiteralPath $capturePathFull -Algorithm SHA256).Hash.ToUpperInvariant() } else { $null } }
    workbenchRoutes = $reportRoutes
    cleanup = $cleanup
}
[IO.File]::WriteAllText($reportFull, ($report | ConvertTo-Json -Depth 16), (New-Object System.Text.UTF8Encoding($false)))
if ($overallVerdict -ne 'PASS') {
    $errorMessage = "Workbench route verdict: $overallVerdict"
    if ($null -ne $runnerError) { $errorMessage = $runnerError }
    Write-Error $errorMessage
    exit 1
}
Write-Output "PASS Workbench Ollama routes; report=$reportFull"
