Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$validatorPath = Join-Path $projectRoot 'tools\validate-ai-link-evidence.ps1'
$manifestWriterPath = Join-Path $projectRoot 'tools\write-source-manifest.ps1'
$pwshPath = (Get-Command pwsh -ErrorAction Stop).Source
$utf8 = [Text.UTF8Encoding]::new($false)
$testRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('pwb-evidence-tests-' + [Guid]::NewGuid().ToString('N'))))
$failures = [Collections.Generic.List[string]]::new()

$modelName = 'gpt-oss:20b'
$modelDigest = '17052f91a42e97930aa6e28a6c6c06a983e6a58dbb00434885a0cf5313e376f7'
$shortFixture = [ordered]@{ bytes = 270; sha256 = '3B82F664F26F11862C7DA899668AAA0635461F5CC3FDA711A8D7622CBA5EB968' }
$longFixture = [ordered]@{ bytes = 3888; sha256 = '8234670F470A8F663D5436111B39CF10D97AE9AA1D123E6FF7C090561F93B752' }
$stageNames = @('expand-short', 'convert-short', 'expand-long', 'convert-long')

function Assert([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function HashBytes([byte[]]$bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToUpperInvariant() }
    finally { $sha.Dispose() }
}

function HashText([string]$text) { return HashBytes $utf8.GetBytes($text) }

function WriteUtf8([string]$path, [string]$text) { [IO.File]::WriteAllText($path, $text, $utf8) }

function ReadJson([string]$path) { return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -Depth 100 }

function WriteJson([string]$path, [object]$value) {
    WriteUtf8 $path ($value | ConvertTo-Json -Depth 100 -Compress)
}

function AssertUnder([string]$path, [string]$root) {
    $fullPath = [IO.Path]::GetFullPath($path)
    $rootPath = ([IO.Path]::GetFullPath($root)).TrimEnd('\', '/') + '\'
    Assert $fullPath.StartsWith($rootPath, [StringComparison]::OrdinalIgnoreCase) "Path escaped test root: $fullPath"
}

function NewSourceRoot([string]$path, [string]$variant) {
    $files = @{
        'src\PersonaWorkbench.Core\PersonaWorkbench.Core.csproj' = '<Project Sdk="Microsoft.NET.Sdk" />'
        'src\PersonaWorkbench.Core\Marker.cs' = "variant-$variant"
        'src\PersonaWorkbench.Web\PersonaWorkbench.Web.csproj' = '<Project Sdk="Microsoft.NET.Sdk" />'
        'src\PersonaWorkbench.Web\Marker.cs' = "web-$variant"
        'src\PersonaWorkbench.Web\wwwroot\index.html' = '<!doctype html><title>test</title>'
        'src\PersonaWorkbench.Launcher\Program.cs' = "launcher-$variant"
        'src\PersonaWorkbench.Launcher\app.manifest' = '<assembly xmlns="urn:schemas-microsoft-com:asm.v1" />'
        'package-free-preview.ps1' = "package-$variant"
        'start-free-preview.ps1' = "start-$variant"
        'stop-free-preview.ps1' = "stop-$variant"
        'launch-free-preview.vbs' = "launch-$variant"
        'stop-free-preview.vbs' = "stop-vbs-$variant"
        'README-FreePreview.md' = "readme-$variant"
        'PersonaWorkbench-使用说明.md' = "usage-$variant"
    }
    foreach ($entry in $files.GetEnumerator()) {
        $filePath = Join-Path $path $entry.Key
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $filePath) | Out-Null
        WriteUtf8 $filePath $entry.Value
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $path 'tools') | Out-Null
    Copy-Item -LiteralPath $manifestWriterPath -Destination (Join-Path $path 'tools\write-source-manifest.ps1') -Force
}

function WritePackageManifest([string]$packagePath) {
    $packageFull = [IO.Path]::GetFullPath($packagePath)
    $manifestPath = Join-Path $packageFull 'PACKAGE-MANIFEST.sha256.txt'
    $lines = foreach ($file in (Get-ChildItem -LiteralPath $packageFull -Recurse -File | Where-Object { $_.FullName -ine $manifestPath } | Sort-Object FullName)) {
        $relative = $file.FullName.Substring($packageFull.Length).TrimStart('\', '/')
        "$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToUpperInvariant())  $relative"
    }
    WriteUtf8 $manifestPath (($lines -join "`n") + "`n")
}

function NewPackage([string]$sourcePath) {
    $packagePath = Join-Path $sourcePath 'package'
    New-Item -ItemType Directory -Force -Path $packagePath | Out-Null
    & $pwshPath -NoLogo -NoProfile -File (Join-Path $sourcePath 'tools\write-source-manifest.ps1') -SourceRoot $sourcePath -Destination $packagePath | Out-Null
    Assert ($LASTEXITCODE -eq 0) "Source manifest writer failed for $sourcePath"
    [IO.File]::WriteAllBytes((Join-Path $packagePath 'PersonaWorkbench.Web.exe'), [byte[]](1, 2, 3, 4))
    WriteUtf8 (Join-Path $packagePath 'PACKAGE-VERSION.txt') 'test-package'
    WritePackageManifest $packagePath
    return $packagePath
}

function GetPackageMetadata([string]$packagePath) {
    $buildLines = @(Get-Content -LiteralPath (Join-Path $packagePath 'BUILD-ID.txt'))
    $buildId = ($buildLines | Where-Object { $_ -like 'BuildId=*' }).Substring(8)
    $sourceHash = ($buildLines | Where-Object { $_ -like 'SourceManifestHash=*' }).Substring(19)
    return [ordered]@{
        buildId = $buildId
        sourceManifestHash = $sourceHash
        packageManifestHash = (Get-FileHash -LiteralPath (Join-Path $packagePath 'PACKAGE-MANIFEST.sha256.txt') -Algorithm SHA256).Hash.ToUpperInvariant()
    }
}

function NewCaptureRecord([string]$stage, [string]$inputText, [int]$numCtx = 8192) {
    $operation = if ($stage.StartsWith('expand-', [StringComparison]::Ordinal)) { 'expand' } else { 'convert' }
    $systemPrompt = "system-$stage"
    $envelopeText = if ($operation -eq 'expand') {
        $envelopeObject = [ordered]@{ protocol = 'persona-expansion.v1'; sourceDescription = $inputText; direction = ''; focusPreset = 'balanced'; focusKeywords = @(); avoidTopics = @() }
        $envelopeObject | ConvertTo-Json -Depth 20 -Compress
    } else {
        $inputText
    }
    $payload = [ordered]@{
        model = $modelName
        messages = @(
            [ordered]@{ role = 'system'; content = $systemPrompt }
            [ordered]@{ role = 'user'; content = $envelopeText }
        )
        stream = $false
        think = 'low'
        options = [ordered]@{ num_ctx = $numCtx; num_predict = 3072; temperature = 0; seed = 42 }
    }
    $body = $payload | ConvertTo-Json -Depth 20 -Compress
    $bodyBytes = $utf8.GetBytes($body)
    $inputBytes = $utf8.GetBytes($inputText)
    return [ordered]@{
        schemaVersion = 'persona-workbench.request-capture.v1'
        stage = $stage
        operation = $operation
        protocol = 'ollama'
        endpointClass = 'loopback'
        model = $modelName
        input = [ordered]@{ source = 'test-input'; bytes = $inputBytes.Length; sha256 = (HashBytes $inputBytes) }
        safeHeaders = [ordered]@{ ContentType = 'application/json' }
        bodyBase64 = [Convert]::ToBase64String($bodyBytes)
        bodyBytes = $bodyBytes.Length
    }
}

function NewStageEvidence([object]$capture) {
    $body = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($capture.bodyBase64)) | ConvertFrom-Json -Depth 30
    $systemText = [string]$body.messages[0].content
    $envelopeText = [string]$body.messages[1].content
    $inputText = if ($capture.operation -eq 'expand') {
        $envelope = $envelopeText | ConvertFrom-Json -Depth 30
        [string]$envelope.sourceDescription
    } else {
        $envelopeText
    }
    $inputBytes = $utf8.GetBytes($inputText)
    return [ordered]@{
        stage = $capture.stage
        operation = $capture.operation
        input = [ordered]@{ source = 'test-input'; bytes = $inputBytes.Length; sha256 = (HashBytes $inputBytes) }
        protocol = $capture.protocol
        endpointClass = $capture.endpointClass
        model = $capture.model
        requestSha256 = (HashBytes ([Convert]::FromBase64String($capture.bodyBase64)))
        requestBytes = $capture.bodyBytes
        systemPromptBytes = $utf8.GetByteCount($systemText)
        envelopeBytes = $utf8.GetByteCount($envelopeText)
        promptSha256 = (HashText $systemText)
        numCtx = $body.options.num_ctx
        numPredict = $body.options.num_predict
        completionReason = 'stop'
        envelopeKind = 'ollama_native'
        usage = [ordered]@{ promptTokens = 10; completionTokens = 20; totalTokens = 30 }
        durationMs = 100
        transportVerdict = 'PASS'
        qualityVerdict = 'PASS'
        performanceVerdict = 'PASS'
        accepted = $true
        failureNoAcceptedPayload = $false
    }
}

function NewFixture {
    $caseRoot = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
    $candidateSource = Join-Path $caseRoot 'candidate-source'
    $baselineSource = Join-Path $caseRoot 'baseline-source'
    New-Item -ItemType Directory -Force -Path $candidateSource,$baselineSource | Out-Null
    NewSourceRoot $candidateSource 'candidate'
    NewSourceRoot $baselineSource 'baseline'
    $candidatePackage = NewPackage $candidateSource
    $baselinePackage = NewPackage $baselineSource
    $workerPath = Join-Path $caseRoot 'worker.json'
    $matrixPath = Join-Path $caseRoot 'matrix.json'
    $worker = [ordered]@{ schemaVersion = 'persona-workbench.worker.v1'; model = [ordered]@{ name = $modelName; digest = $modelDigest; thinkingLevel = 'low' }; fixtures = [ordered]@{ short = $shortFixture; long = $longFixture } }
    WriteJson $workerPath $worker
    WriteUtf8 $matrixPath '{"revision":"test-r1","atoms":[]}'
    $matrixHash = (Get-FileHash -LiteralPath $matrixPath -Algorithm SHA256).Hash.ToUpperInvariant()
    $workerHash = (Get-FileHash -LiteralPath $workerPath -Algorithm SHA256).Hash.ToUpperInvariant()
    $candidateCapturePath = Join-Path $caseRoot 'candidate-capture.jsonl'
    $baselineCapturePath = Join-Path $caseRoot 'baseline-capture.jsonl'
    $captures = foreach ($stage in $stageNames) {
        $input = if ($stage -match 'short') { 'short test input' } else { 'long test input' }
        NewCaptureRecord $stage $input
    }
    foreach ($capture in $captures) { WriteJson (Join-Path $caseRoot ($capture.stage + '.capture.json')) $capture }
    WriteUtf8 $candidateCapturePath (($captures | ForEach-Object { $_ | ConvertTo-Json -Depth 30 -Compress }) -join "`n" + "`n")
    WriteUtf8 $baselineCapturePath (($captures | ForEach-Object { $_ | ConvertTo-Json -Depth 30 -Compress }) -join "`n" + "`n")
    $candidateStages = @($captures | ForEach-Object { NewStageEvidence $_ })
    $baselineStages = @($captures | ForEach-Object { NewStageEvidence $_ })
    $candidateMeta = GetPackageMetadata $candidatePackage
    $baselineMeta = GetPackageMetadata $baselinePackage
    $candidateEvidencePath = Join-Path $caseRoot 'candidate-evidence.json'
    $baselineEvidencePath = Join-Path $caseRoot 'baseline-evidence.json'
    $base = [ordered]@{
        schemaVersion = 'persona-workbench.ai-link-evidence.v2'
        candidate = $null
        baseline = $null
        model = [ordered]@{ name = $modelName; digest = $modelDigest; thinkingLevel = 'low' }
        fixtures = [ordered]@{ short = $shortFixture; long = $longFixture }
        workerDiagnostic = [ordered]@{ reportSha256 = $workerHash; model = $modelName; modelDigest = $modelDigest; thinkingLevel = 'low' }
        matrix = [ordered]@{ revision = 'test-r1'; sha256 = $matrixHash }
        workbenchStages = $candidateStages
        verdicts = [ordered]@{ transport = 'PASS'; quality = 'PASS'; performance = 'PASS'; overall = 'PASS' }
        cleanup = [ordered]@{ state = 'pending'; captureDeletionAttempted = $false; captureDeleted = $false; processStopped = $false; portFree = $false; error = $null }
    }
    $candidateEvidence = $base | ConvertTo-Json -Depth 100 | ConvertFrom-Json -Depth 100
    $candidateEvidence.candidate = [PSCustomObject]$candidateMeta
    $candidateEvidence.workbenchStages = $candidateStages
    WriteJson $candidateEvidencePath $candidateEvidence
    $baselineEvidence = $base | ConvertTo-Json -Depth 100 | ConvertFrom-Json -Depth 100
    $baselineEvidence.baseline = [PSCustomObject]$baselineMeta
    $baselineEvidence.workbenchStages = $baselineStages
    WriteJson $baselineEvidencePath $baselineEvidence
    return [ordered]@{
        Root = $caseRoot
        Paths = [ordered]@{ WorkspaceRoot = $caseRoot; SourceRoot = $candidateSource; PackagePath = $candidatePackage; WorkerReportPath = $workerPath; MatrixPath = $matrixPath; EvidencePath = $candidateEvidencePath; BaselineSourceRoot = $baselineSource; BaselinePackagePath = $baselinePackage; BaselineWorkerReportPath = $workerPath; BaselineMatrixPath = $matrixPath; BaselinePath = $baselineEvidencePath; RequestCapturePath = $candidateCapturePath; BaselineRequestCapturePath = $baselineCapturePath }
    }
}

function InvokeValidator([object]$paths) {
    function QuotePowerShell([string]$value) { return "'$($value.Replace("'", "''"))'" }
    $scriptPath = Join-Path $paths.WorkspaceRoot "invoke-validator.ps1"
    $argumentNames = @("-WorkspaceRoot", "-SourceRoot", "-PackagePath", "-WorkerReportPath", "-MatrixPath", "-EvidencePath", "-BaselineSourceRoot", "-BaselinePackagePath", "-BaselineWorkerReportPath", "-BaselineMatrixPath", "-BaselinePath", "-RequestCapturePath", "-BaselineRequestCapturePath")
    $commandParts = @("& $(QuotePowerShell $validatorPath)")
    foreach ($argumentName in $argumentNames) { $key = $argumentName.Substring(1); $commandParts += @($argumentName, (QuotePowerShell ([string]$paths[$key]))) }
    $commandText = ($commandParts -join " ") + "`nexit `$LASTEXITCODE`n"
    WriteUtf8 $scriptPath $commandText
    $rawOutput = & $pwshPath -NoLogo -NoProfile -File $scriptPath 2>&1
    $exitCode = $LASTEXITCODE
    $output = $rawOutput | Out-String
    if ($exitCode -eq 1 -and [string]$output -match 'EVIDENCE_INPUT_INVALID') { $exitCode = 2 }
    return [ordered]@{ ExitCode = $exitCode; Output = $output }
}
function RunCase([string]$name, [int]$expectedExitCode, [scriptblock]$mutate) {
    $fixture = NewFixture
    try {
        & $mutate $fixture
        $result = InvokeValidator $fixture.Paths
        Assert ($result.ExitCode -eq $expectedExitCode) "$name expected exit $expectedExitCode, got $($result.ExitCode): $($result.Output)"
        Write-Output "PASS $name"
    }
    catch { $failures.Add("FAIL $name`: $($_.Exception.Message)") }
    finally {
        AssertUnder $fixture.Root $testRoot
        Remove-Item -LiteralPath $fixture.Root -Recurse -Force
    }
}

New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
try {
    RunCase 'valid evidence' 0 { param($fixture) }
    RunCase 'tampered evidence hash' 1 {
        param($fixture)
        $evidence = ReadJson $fixture.Paths.EvidencePath
        $evidence.candidate.packageManifestHash = ('0' * 64)
        WriteJson $fixture.Paths.EvidencePath $evidence
    }
    RunCase 'tampered request capture' 1 {
        param($fixture)
        $lines = @(Get-Content -LiteralPath $fixture.Paths.RequestCapturePath)
        $record = $lines[0] | ConvertFrom-Json -Depth 30
        $record.bodyBase64 = [Convert]::ToBase64String($utf8.GetBytes('tampered'))
        $lines[0] = $record | ConvertTo-Json -Depth 30 -Compress
        WriteUtf8 $fixture.Paths.RequestCapturePath (($lines -join "`n") + "`n")
    }
    RunCase 'tampered matrix' 1 {
        param($fixture)
        WriteUtf8 $fixture.Paths.MatrixPath '{"revision":"tampered","atoms":[1]}'
    }
    RunCase 'tampered package manifest' 1 {
        param($fixture)
        Add-Content -LiteralPath (Join-Path $fixture.Paths.PackagePath 'PACKAGE-MANIFEST.sha256.txt') -Value 'tampered'
    }
    RunCase 'missing stage capture' 1 {
        param($fixture)
        $lines = @(Get-Content -LiteralPath $fixture.Paths.RequestCapturePath)
        WriteUtf8 $fixture.Paths.RequestCapturePath (($lines[0..2] -join "`n") + "`n")
    }
    RunCase 'unknown envelope kind' 1 {
        param($fixture)
        $evidence = ReadJson $fixture.Paths.EvidencePath
        $evidence.workbenchStages[0].envelopeKind = 'invalid_kind'
        WriteJson $fixture.Paths.EvidencePath $evidence
    }
    RunCase 'inconsistent num_ctx' 1 {
        param($fixture)
        $evidence = ReadJson $fixture.Paths.EvidencePath
        $evidence.workbenchStages[0].numCtx = 16384
        WriteJson $fixture.Paths.EvidencePath $evidence
    }
    RunCase 'empty baseline capture' 1 {
        param($fixture)
        WriteUtf8 $fixture.Paths.BaselineRequestCapturePath ''
    }
    RunCase 'duplicate evidence key' 2 {
        param($fixture)
        $text = [IO.File]::ReadAllText($fixture.Paths.EvidencePath, $utf8)
        WriteUtf8 $fixture.Paths.EvidencePath ('{"schemaVersion":"duplicate",' + $text.Substring(1))
    }
    RunCase 'missing matrix path' 2 {
        param($fixture)
        $fixture.Paths.MatrixPath = Join-Path $fixture.Root 'missing-matrix.json'
    }
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        AssertUnder $testRoot ([IO.Path]::GetTempPath())
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output 'PASS validate-ai-link-evidence.tests'
exit 0
