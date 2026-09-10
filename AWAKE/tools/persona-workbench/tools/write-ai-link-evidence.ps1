param(
    [Parameter(Mandatory = $true)][ValidateSet('candidate', 'baseline')][string]$Side,
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$RunnerReportPath,
    [Parameter(Mandatory = $true)][string]$RequestCapturePath,
    [Parameter(Mandatory = $true)][string]$WorkerReportPath,
    [Parameter(Mandatory = $true)][string]$MatrixPath,
    [Parameter(Mandatory = $true)][string]$EvidencePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false)
$expectedStages = @('expand-short', 'convert-short', 'expand-long', 'convert-long')

function Full([string]$path) { return [IO.Path]::GetFullPath($path) }
function HashBytes([byte[]]$bytes) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}
function HashText([string]$text) { return HashBytes $utf8.GetBytes($text ?? '') }
function ReadKeyValue([string[]]$lines, [string]$key) {
    $line = $lines | Where-Object { $_ -like "$key=*" } | Select-Object -First 1
    if ($null -eq $line) { throw "BUILD-ID.txt is missing $key." }
    return $line.Substring($key.Length + 1)
}
function ReadJson([string]$path) { return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -Depth 100 }
function GetProperty([object]$value, [string]$name, [object]$fallback = $null) {
    if ($null -eq $value) { return $fallback }
    $property = $value.PSObject.Properties[$name]
    if ($null -eq $property) { return $fallback }
    return $property.Value
}
function ReadCaptures([string]$path) {
    $records = [Collections.Generic.List[object]]::new()
    foreach ($line in [IO.File]::ReadAllLines($path, $utf8)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        [void]$records.Add(($line | ConvertFrom-Json -Depth 100))
    }
    return @($records.ToArray())
}
function GetRoute([object]$runner, [string]$stage) {
    $route = @($runner.workbenchRoutes | Where-Object { $_.stage -eq $stage })
    if ($route.Count -ne 1) { throw "Runner report must contain one route for $stage." }
    return $route[0]
}
function NewStageEvidence([object]$capture, [object]$route) {
    $bodyBytes = [Convert]::FromBase64String([string]$capture.bodyBase64)
    $bodyText = $utf8.GetString($bodyBytes)
    $body = $bodyText | ConvertFrom-Json -Depth 100
    $messages = @($body.messages)
    $system = @($messages | Where-Object { $_.role -eq 'system' })
    $user = @($messages | Where-Object { $_.role -eq 'user' })
    if ($system.Count -ne 1 -or $user.Count -ne 1) { throw "Capture $($capture.stage) must contain one system and one user message." }
    $systemText = [string]$system[0].content
    $envelopeText = [string]$user[0].content
    $inputText = if ($capture.operation -eq 'expand') {
        $envelope = $envelopeText | ConvertFrom-Json -Depth 100
        [string](GetProperty $envelope 'sourceDescription' '')
    } else {
        $envelopeText
    }
    $inputBytes = $utf8.GetBytes($inputText)
    $usage = GetProperty $route 'usage'
    $accepted = [bool](GetProperty $route 'accepted' $false)
    return [ordered]@{
        stage = [string]$capture.stage
        operation = [string]$capture.operation
        input = [ordered]@{ source = [string](GetProperty $route 'inputSource' 'provider-envelope'); bytes = $inputBytes.Length; sha256 = HashBytes $inputBytes }
        protocol = [string]$capture.protocol
        endpointClass = [string]$capture.endpointClass
        model = [string]$capture.model
        requestSha256 = HashBytes $bodyBytes
        requestBytes = $bodyBytes.Length
        systemPromptBytes = $utf8.GetByteCount($systemText)
        envelopeBytes = $utf8.GetByteCount($envelopeText)
        promptSha256 = HashText $systemText
        numCtx = [int]$body.options.num_ctx
        numPredict = [int]$body.options.num_predict
        completionReason = if ($accepted) { 'stop' } else { $null }
        envelopeKind = 'ollama_native'
        usage = $usage
        durationMs = [double](GetProperty $route 'durationMs' 0)
        transportVerdict = if ($accepted) { 'PASS' } else { 'FAIL' }
        qualityVerdict = if ($accepted) { 'PASS' } else { 'FAIL' }
        performanceVerdict = if ($accepted) { 'PASS' } else { 'FAIL' }
        accepted = $accepted
        failureNoAcceptedPayload = [bool](GetProperty $route 'failureNoAcceptedPayload' (-not $accepted))
    }
}

$packageFull = Full $PackagePath
$runnerFull = Full $RunnerReportPath
$captureFull = Full $RequestCapturePath
$workerFull = Full $WorkerReportPath
$matrixFull = Full $MatrixPath
$evidenceFull = Full $EvidencePath
if (-not (Test-Path -LiteralPath $packageFull -PathType Container)) { throw "Evidence package is missing: $packageFull" }
foreach ($path in @($runnerFull, $captureFull, $workerFull, $matrixFull)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Evidence input is missing: $path" }
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidenceFull) | Out-Null

$runner = ReadJson $runnerFull
$worker = ReadJson $workerFull
$matrix = ReadJson $matrixFull
$captures = ReadCaptures $captureFull
if ($captures.Count -ne 4) { throw "Capture must contain exactly four records." }
for ($index = 0; $index -lt 4; $index++) {
    if ([string]$captures[$index].stage -ne $expectedStages[$index]) { throw 'Capture stage order differs from the fixed contract.' }
}

$buildLines = @(Get-Content -LiteralPath (Join-Path $packageFull 'BUILD-ID.txt'))
$buildId = ReadKeyValue $buildLines 'BuildId'
$sourceManifestHash = ReadKeyValue $buildLines 'SourceManifestHash'
$packageManifestPath = Join-Path $packageFull 'PACKAGE-MANIFEST.sha256.txt'
$packageManifestHash = (Get-FileHash -LiteralPath $packageManifestPath -Algorithm SHA256).Hash.ToUpperInvariant()
$stages = [Collections.Generic.List[object]]::new()
for ($index = 0; $index -lt 4; $index++) {
    [void]$stages.Add((NewStageEvidence $captures[$index] (GetRoute $runner $expectedStages[$index])))
}

$build = [ordered]@{ buildId = $buildId; sourceManifestHash = $sourceManifestHash; packageManifestHash = $packageManifestHash }
$evidence = [ordered]@{
    schemaVersion = 'persona-workbench.ai-link-evidence.v2'
    candidate = if ($Side -eq 'candidate') { $build } else { $null }
    baseline = if ($Side -eq 'baseline') { $build } else { $null }
    model = [ordered]@{ name = [string]$runner.model.name; digest = [string]$runner.model.digest; thinkingLevel = 'low' }
    fixtures = $runner.fixtures
    workerDiagnostic = [ordered]@{
        reportSha256 = (Get-FileHash -LiteralPath $workerFull -Algorithm SHA256).Hash.ToUpperInvariant()
        model = [string]$worker.model.name
        modelDigest = [string]$worker.model.digest
        thinkingLevel = [string]$worker.model.thinkingLevel
    }
    matrix = [ordered]@{ revision = [string]$matrix.revision; sha256 = (Get-FileHash -LiteralPath $matrixFull -Algorithm SHA256).Hash.ToUpperInvariant() }
    workbenchStages = @($stages.ToArray())
    verdicts = [ordered]@{
        transport = if (@($stages | Where-Object { $_.transportVerdict -ne 'PASS' }).Count -eq 0) { 'PASS' } else { 'FAIL' }
        quality = if (@($stages | Where-Object { $_.qualityVerdict -ne 'PASS' }).Count -eq 0) { 'PASS' } else { 'FAIL' }
        performance = if (@($stages | Where-Object { $_.performanceVerdict -ne 'PASS' }).Count -eq 0) { 'PASS' } else { 'FAIL' }
        overall = if (@($stages | Where-Object { -not $_.accepted }).Count -eq 0) { 'PASS' } else { 'FAIL' }
    }
    cleanup = [ordered]@{ state = 'pending'; captureDeletionAttempted = $false; captureDeleted = $false; processStopped = $false; portFree = $false; error = $null }
}
[IO.File]::WriteAllText($evidenceFull, ($evidence | ConvertTo-Json -Depth 100), $utf8)
Write-Output "PASS evidence written: $evidenceFull"
