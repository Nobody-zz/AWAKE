param(
    [Parameter(Mandatory = $true)][string]$WorkspaceRoot,
    [Parameter(Mandatory = $true)][string]$SourceRoot,
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$WorkerReportPath,
    [Parameter(Mandatory = $true)][string]$MatrixPath,
    [Parameter(Mandatory = $true)][string]$EvidencePath,
    [Parameter(Mandatory = $true)][string]$BaselineSourceRoot,
    [Parameter(Mandatory = $true)][string]$BaselinePackagePath,
    [Parameter(Mandatory = $true)][string]$BaselineWorkerReportPath,
    [Parameter(Mandatory = $true)][string]$BaselineMatrixPath,
    [Parameter(Mandatory = $true)][string]$BaselinePath,
    [Parameter(Mandatory = $true)][string]$RequestCapturePath,
    [Parameter(Mandatory = $true)][string]$BaselineRequestCapturePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$schemaPath = Join-Path $PSScriptRoot 'persona-workbench-ai-link-evidence.v2.schema.json'
$utf8 = [Text.UTF8Encoding]::new($false)
$expectedStages = @('expand-short', 'convert-short', 'expand-long', 'convert-long')
$expectedModelName = 'gpt-oss:20b'
$expectedModelDigest = '17052f91a42e97930aa6e28a6c6c06a983e6a58dbb00434885a0cf5313e376f7'
$expectedFixtures = [ordered]@{
    short = [ordered]@{ bytes = 270; sha256 = '3B82F664F26F11862C7DA899668AAA0635461F5CC3FDA711A8D7622CBA5EB968' }
    long = [ordered]@{ bytes = 3888; sha256 = '8234670F470A8F663D5436111B39CF10D97AE9AA1D123E6FF7C090561F93B752' }
}

function ThrowInput([string]$message) { throw [ArgumentException]::new("__INPUT__ $message") }
function ThrowEvidence([string]$message) { throw [InvalidOperationException]::new("__EVIDENCE__ $message") }

function Full([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path) -or -not [IO.Path]::IsPathRooted($path)) { ThrowInput "Path must be absolute: $path" }
    try { return [IO.Path]::GetFullPath($path) } catch { ThrowInput "Path is invalid: $path" }
}

function AssertUnder([string]$path, [string]$root, [string]$label) {
    $fullPath = Full $path
    $rootPath = (Full $root).TrimEnd('\', '/') + '\'
    if (-not $fullPath.StartsWith($rootPath, [StringComparison]::OrdinalIgnoreCase)) { ThrowInput "$label is outside WorkspaceRoot: $fullPath" }
    return $fullPath
}

function AssertDirectory([string]$path, [string]$label) {
    if (-not (Test-Path -LiteralPath $path -PathType Container)) { ThrowInput "$label is missing: $path" }
}

function AssertFile([string]$path, [string]$label) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { ThrowInput "$label is missing: $path" }
}

function HashBytes([byte[]]$bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToUpperInvariant() }
    finally { $sha.Dispose() }
}

function HashFile([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant() }

function HashText([string]$text) { return HashBytes $utf8.GetBytes($text) }

function GetRequiredProperty([object]$object, [string]$name, [string]$label) {
    if ($null -eq $object -or $null -eq $object.PSObject.Properties[$name]) { ThrowEvidence "$label is missing $name" }
    return $object.PSObject.Properties[$name].Value
}

function GetStringProperty([object]$object, [string]$name, [string]$label) {
    $value = GetRequiredProperty $object $name $label
    if ($value -isnot [string]) { ThrowEvidence "$label.$name is not a string" }
    return [string]$value
}

function GetIntegerProperty([object]$object, [string]$name, [string]$label) {
    $value = GetRequiredProperty $object $name $label
    if ($value -isnot [int] -and $value -isnot [long]) { ThrowEvidence "$label.$name is not an integer" }
    return [int64]$value
}

function GetNumberProperty([object]$object, [string]$name, [string]$label) {
    $value = GetRequiredProperty $object $name $label
    if ($value -isnot [int] -and $value -isnot [long] -and $value -isnot [double] -and $value -isnot [decimal]) { ThrowEvidence "$label.$name is not numeric" }
    return [double]$value
}

function AssertNoDuplicateJsonProperties([byte[]]$bytes, [string]$label) {
    if (-not ('PersonaWorkbenchEvidenceJsonGuard' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class PersonaWorkbenchEvidenceJsonGuard
{
    public static void Validate(byte[] utf8Bytes)
    {
        JsonReaderOptions readerOptions = new JsonReaderOptions { MaxDepth = 100 };
        Utf8JsonReader reader = new Utf8JsonReader(utf8Bytes, readerOptions);
        Stack<HashSet<string>> objectScopes = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                objectScopes.Push(new HashSet<string>(StringComparer.Ordinal));
            }
            else if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (objectScopes.Count == 0 || !objectScopes.Peek().Add(reader.GetString() ?? string.Empty))
                {
                    throw new InvalidOperationException("duplicate JSON property");
                }
            }
            else if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (objectScopes.Count == 0) throw new InvalidOperationException("unbalanced JSON object");
                objectScopes.Pop();
            }
        }
    }
}
'@ -Language CSharp
    }
    try { [PersonaWorkbenchEvidenceJsonGuard]::Validate($bytes) } catch { ThrowInput "$label contains duplicate or invalid JSON properties: $($_.Exception.Message)" }
}

function ReadJsonFile([string]$path, [string]$label, [switch]$EvidenceSchema) {
    $bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -eq 0) { ThrowInput "$label is empty" }
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { ThrowInput "$label must be UTF-8 without BOM" }
    AssertNoDuplicateJsonProperties $bytes $label
    $text = $utf8.GetString($bytes)
    try { $value = $text | ConvertFrom-Json -Depth 100 -ErrorAction Stop } catch { ThrowInput "$label is not valid JSON: $($_.Exception.Message)" }
    if ($EvidenceSchema) {
        try { $schemaValid = Test-Json -LiteralPath $path -SchemaFile $schemaPath -ErrorAction Stop } catch { ThrowEvidence "$label does not satisfy evidence schema: $($_.Exception.Message)" }
        if (-not $schemaValid) { ThrowEvidence "$label does not satisfy evidence schema" }
    }
    return $value
}

function ReadKeyValue([string[]]$lines, [string]$key, [string]$label) {
    $line = $lines | Where-Object { $_ -like "$key=*" } | Select-Object -First 1
    if ($null -eq $line) { ThrowEvidence "$label is missing $key" }
    return $line.Substring($key.Length + 1)
}

function Relative([string]$path, [string]$base) { return $path.Substring($base.Length).TrimStart('\', '/').Replace('/', '\') }

function AddSourceFile([Collections.Generic.List[string]]$files, [string]$relativePath, [string]$sourceRoot) {
    $fullPath = Join-Path $sourceRoot $relativePath
    AssertFile $fullPath "source manifest input $relativePath"
    [void]$files.Add((Full $fullPath))
}

function AddSourceFiles([Collections.Generic.List[string]]$files, [string]$directoryRelativePath, [string]$sourceRoot, [string]$filter, [switch]$AllFiles) {
    $directory = Join-Path $sourceRoot $directoryRelativePath
    AssertDirectory $directory "source manifest directory $directoryRelativePath"
    $items = if ($AllFiles) { Get-ChildItem -LiteralPath $directory -Recurse -File } else { Get-ChildItem -LiteralPath $directory -Recurse -File -Filter $filter }
    foreach ($item in ($items | Sort-Object FullName)) {
        $relative = Relative $item.FullName (Full $sourceRoot)
        if ($relative -match '(^|[\\/])(bin|obj|artifacts|tests|backup[^\\/]*|[^\\/]*_backup_[^\\/]*)([\\/]|$)') { continue }
        [void]$files.Add((Full $item.FullName))
    }
}

function GetSourceManifestLines([string]$sourceRoot) {
    $files = [Collections.Generic.List[string]]::new()
    AddSourceFiles $files 'src\PersonaWorkbench.Core' $sourceRoot '*.cs'
    AddSourceFile $files 'src\PersonaWorkbench.Core\PersonaWorkbench.Core.csproj' $sourceRoot
    AddSourceFiles $files 'src\PersonaWorkbench.Web' $sourceRoot '*.cs'
    AddSourceFile $files 'src\PersonaWorkbench.Web\PersonaWorkbench.Web.csproj' $sourceRoot
    AddSourceFiles $files 'src\PersonaWorkbench.Web\wwwroot' $sourceRoot '' -AllFiles
    AddSourceFile $files 'src\PersonaWorkbench.Launcher\Program.cs' $sourceRoot
    AddSourceFile $files 'src\PersonaWorkbench.Launcher\app.manifest' $sourceRoot
    AddSourceFile $files 'package-free-preview.ps1' $sourceRoot
    AddSourceFile $files 'tools\write-source-manifest.ps1' $sourceRoot
    AddSourceFile $files 'start-free-preview.ps1' $sourceRoot
    AddSourceFile $files 'stop-free-preview.ps1' $sourceRoot
    AddSourceFile $files 'launch-free-preview.vbs' $sourceRoot
    AddSourceFile $files 'stop-free-preview.vbs' $sourceRoot
    AddSourceFile $files 'README-FreePreview.md' $sourceRoot
    $usage = Get-ChildItem -LiteralPath $sourceRoot -File | Where-Object { $_.Name -like 'PersonaWorkbench-*.md' -and $_.Name -ne 'README-FreePreview.md' } | Sort-Object Name | Select-Object -First 1
    if ($null -eq $usage) { ThrowEvidence 'Persona Workbench usage document is missing' }
    [void]$files.Add((Full $usage.FullName))
    $uniqueFiles = $files | Sort-Object -Unique
    return @($uniqueFiles | ForEach-Object {
        $relative = Relative $_ (Full $sourceRoot)
        "$((HashFile $_))  $relative"
    } | Sort-Object)
}

function ValidateBuildAndManifests([string]$sourceRoot, [string]$packagePath, [object]$evidenceBuild, [string]$label) {
    $sourceRootFull = Full $sourceRoot
    $packageFull = Full $packagePath
    $packagePrefix = $sourceRootFull.TrimEnd('\', '/') + '\'
    if (-not $packageFull.StartsWith($packagePrefix, [StringComparison]::OrdinalIgnoreCase)) { ThrowEvidence "$label package is outside its source root" }
    $sourceManifestPath = Join-Path $packageFull 'BUILD-SOURCE-MANIFEST.sha256.txt'
    $buildIdPath = Join-Path $packageFull 'BUILD-ID.txt'
    $packageManifestPath = Join-Path $packageFull 'PACKAGE-MANIFEST.sha256.txt'
    AssertFile $sourceManifestPath "$label source manifest"
    AssertFile $buildIdPath "$label build metadata"
    AssertFile $packageManifestPath "$label package manifest"
    $expectedLines = @(GetSourceManifestLines $sourceRootFull)
    $actualLines = @(Get-Content -LiteralPath $sourceManifestPath)
    if ($actualLines.Count -ne $expectedLines.Count) { ThrowEvidence "$label source manifest line count differs" }
    for ($index = 0; $index -lt $expectedLines.Count; $index++) { if ($actualLines[$index] -ne $expectedLines[$index]) { ThrowEvidence "$label source manifest differs at line $($index + 1)" } }
    $sourceManifestText = ($expectedLines -join "`n") + "`n"
    $sourceManifestHash = HashText $sourceManifestText
    $buildLines = @(Get-Content -LiteralPath $buildIdPath)
    $buildId = ReadKeyValue $buildLines 'BuildId' "$label BUILD-ID.txt"
    $recordedSourceHash = ReadKeyValue $buildLines 'SourceManifestHash' "$label BUILD-ID.txt"
    if ($recordedSourceHash -ne $sourceManifestHash) { ThrowEvidence "$label source hash does not match BUILD-ID.txt" }
    if ($buildId -notmatch '^PWB-[0-9]{8}-[0-9]{6}Z-[0-9A-F]{12}$') { ThrowEvidence "$label BuildId format is invalid" }
    if ($buildId.Substring($buildId.Length - 12) -ne $sourceManifestHash.Substring(0, 12)) { ThrowEvidence "$label BuildId is not bound to source hash" }
    $manifestRelativePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in @(Get-Content -LiteralPath $packageManifestPath)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^([0-9A-Fa-f]{64})  (.+)$') { ThrowEvidence "$label package manifest line is invalid" }
        $relative = $matches[2].Replace('/', '\')
        $candidate = Full (Join-Path $packageFull $relative)
        $packagePrefix = $packageFull.TrimEnd('\', '/') + '\'
        if (-not $candidate.StartsWith($packagePrefix, [StringComparison]::OrdinalIgnoreCase)) { ThrowEvidence "$label package manifest escapes package" }
        AssertFile $candidate "$label package manifest entry $relative"
        $actualHash = HashFile $candidate
        if ($actualHash -ne $matches[1].ToUpperInvariant()) { ThrowEvidence "$label package hash differs for $relative" }
        [void]$manifestRelativePaths.Add($relative)
    }
    foreach ($file in (Get-ChildItem -LiteralPath $packageFull -Recurse -File | Where-Object { $_.FullName -ine $packageManifestPath })) {
        $relative = Relative $file.FullName $packageFull
        if (-not $manifestRelativePaths.Contains($relative)) { ThrowEvidence "$label package manifest does not cover $relative" }
    }
    $packageManifestHash = HashFile $packageManifestPath
    $evidenceBuildId = GetStringProperty $evidenceBuild 'buildId' "$label evidence build"
    $evidenceSourceHash = GetStringProperty $evidenceBuild 'sourceManifestHash' "$label evidence build"
    $evidencePackageHash = GetStringProperty $evidenceBuild 'packageManifestHash' "$label evidence build"
    if ($evidenceBuildId -ne $buildId -or $evidenceSourceHash -ne $sourceManifestHash -or $evidencePackageHash -ne $packageManifestHash) { ThrowEvidence "$label evidence build metadata does not match package" }
    return [ordered]@{ buildId = $buildId; sourceManifestHash = $sourceManifestHash; packageManifestHash = $packageManifestHash }
}

function AssertFixture([object]$actual, [object]$expected, [string]$label) {
    if ((GetIntegerProperty $actual 'bytes' $label) -ne $expected.bytes -or (GetStringProperty $actual 'sha256' $label) -ne $expected.sha256) { ThrowEvidence "$label does not match fixed fixture" }
}

function ValidateCommonEvidence([object]$evidence, [string]$workerReportPath, [string]$matrixPath, [string]$label) {
    $model = GetRequiredProperty $evidence 'model' $label
    if ((GetStringProperty $model 'name' "$label.model") -ne $expectedModelName -or (GetStringProperty $model 'digest' "$label.model") -ne $expectedModelDigest -or (GetStringProperty $model 'thinkingLevel' "$label.model") -ne 'low') { ThrowEvidence "$label model does not match fixed local model" }
    $fixtures = GetRequiredProperty $evidence 'fixtures' $label
    AssertFixture (GetRequiredProperty $fixtures 'short' "$label.fixtures") $expectedFixtures.short "$label.fixtures.short"
    AssertFixture (GetRequiredProperty $fixtures 'long' "$label.fixtures") $expectedFixtures.long "$label.fixtures.long"
    $worker = ReadJsonFile $workerReportPath "$label Worker report"
    $workerHash = HashFile $workerReportPath
    $workerEvidence = GetRequiredProperty $evidence 'workerDiagnostic' $label
    if ((GetStringProperty $workerEvidence 'reportSha256' "$label.workerDiagnostic") -ne $workerHash) { ThrowEvidence "$label Worker report hash differs" }
    if ((GetStringProperty $workerEvidence 'model' "$label.workerDiagnostic") -ne $expectedModelName -or (GetStringProperty $workerEvidence 'modelDigest' "$label.workerDiagnostic") -ne $expectedModelDigest -or (GetStringProperty $workerEvidence 'thinkingLevel' "$label.workerDiagnostic") -ne 'low') { ThrowEvidence "$label Worker model metadata differs" }
    $workerModel = GetRequiredProperty $worker 'model' "$label Worker report"
    if ((GetStringProperty $workerModel 'name' "$label Worker report.model") -ne $expectedModelName -or (GetStringProperty $workerModel 'digest' "$label Worker report.model") -ne $expectedModelDigest -or (GetStringProperty $workerModel 'thinkingLevel' "$label Worker report.model") -ne 'low') { ThrowEvidence "$label Worker report model differs" }
    $workerFixtures = GetRequiredProperty $worker 'fixtures' "$label Worker report"
    AssertFixture (GetRequiredProperty $workerFixtures 'short' "$label Worker report.fixtures") $expectedFixtures.short "$label Worker report.fixtures.short"
    AssertFixture (GetRequiredProperty $workerFixtures 'long' "$label Worker report.fixtures") $expectedFixtures.long "$label Worker report.fixtures.long"
    $matrix = ReadJsonFile $matrixPath "$label matrix"
    $matrixEvidence = GetRequiredProperty $evidence 'matrix' $label
    if ((GetStringProperty $matrixEvidence 'sha256' "$label.matrix") -ne (HashFile $matrixPath)) { ThrowEvidence "$label matrix hash differs" }
    if ((GetStringProperty $matrixEvidence 'revision' "$label.matrix") -ne (GetStringProperty $matrix 'revision' "$label matrix")) { ThrowEvidence "$label matrix revision differs" }
}

function AssertCaptureProperties([object]$capture, [string]$label) {
    $allowed = @('schemaVersion', 'stage', 'operation', 'protocol', 'endpointClass', 'model', 'input', 'safeHeaders', 'bodyBase64', 'bodyBytes')
    $actual = @($capture.PSObject.Properties.Name)
    $unknown = @($actual | Where-Object { $_ -notin $allowed })
    if ($unknown.Count -gt 0) { ThrowInput "$label contains unknown capture fields: $($unknown -join ',')" }
    foreach ($field in $allowed) { if ($null -eq $capture.PSObject.Properties[$field]) { ThrowInput "$label is missing $field" } }
    if ((GetStringProperty $capture 'schemaVersion' $label) -ne 'persona-workbench.request-capture.v1') { ThrowInput "$label schemaVersion is invalid" }
    try { $bodyBytes = [Convert]::FromBase64String((GetStringProperty $capture 'bodyBase64' $label)) } catch { ThrowInput "$label bodyBase64 is invalid" }
    if ((GetIntegerProperty $capture 'bodyBytes' $label) -ne $bodyBytes.Length) { ThrowEvidence "$label bodyBytes differs from bodyBase64" }
    $headers = GetRequiredProperty $capture 'safeHeaders' $label
    foreach ($header in @($headers.PSObject.Properties.Name)) { if ($header -match '(?i)authorization|api.?key|secret') { ThrowInput "$label contains a sensitive header" } }
    return $bodyBytes
}

function ReadCapture([string]$path, [string]$label) {
    $records = [Collections.Generic.List[object]]::new()
    foreach ($line in [IO.File]::ReadAllLines($path, $utf8)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $bytes = $utf8.GetBytes($line)
        AssertNoDuplicateJsonProperties $bytes $label
        try { $record = $line | ConvertFrom-Json -Depth 100 -ErrorAction Stop } catch { ThrowInput "$label contains invalid JSON: $($_.Exception.Message)" }
        [void](AssertCaptureProperties $record $label)
        [void]$records.Add($record)
    }
    return @($records.ToArray())
}

function ValidateStage([object]$stage, [object]$capture, [object]$topModel, [string]$label) {
    $stageName = GetStringProperty $stage 'stage' $label
    $captureStage = GetStringProperty $capture 'stage' "$label.capture"
    if ($stageName -ne $captureStage) { ThrowEvidence "$label stage name differs from capture" }
    $operation = GetStringProperty $stage 'operation' $label
    $expectedOperation = if ($stageName.StartsWith('expand-', [StringComparison]::Ordinal)) { 'expand' } else { 'convert' }
    if ($operation -ne $expectedOperation -or (GetStringProperty $capture 'operation' "$label.capture") -ne $operation) { ThrowEvidence "$label operation differs" }
    $bodyBytes = AssertCaptureProperties $capture "$label.capture"
    $bodyText = $utf8.GetString($bodyBytes)
    AssertNoDuplicateJsonProperties $bodyBytes "$label provider body"
    try { $body = $bodyText | ConvertFrom-Json -Depth 100 -ErrorAction Stop } catch { ThrowInput "$label provider body is invalid JSON" }
    $messages = GetRequiredProperty $body 'messages' "$label provider body"
    if (@($messages).Count -ne 2) { ThrowInput "$label provider body must have exactly two messages" }
    $systemMessages = @($messages | Where-Object { $_.role -eq 'system' })
    $userMessages = @($messages | Where-Object { $_.role -eq 'user' })
    if ($systemMessages.Count -ne 1 -or $userMessages.Count -ne 1) { ThrowInput "$label provider body must have one system and one user message" }
    $systemText = GetStringProperty $systemMessages[0] 'content' "$label system message"
    $envelopeText = GetStringProperty $userMessages[0] 'content' "$label user message"
    if ($operation -eq 'expand') {
        try { $envelope = $envelopeText | ConvertFrom-Json -Depth 100 -ErrorAction Stop } catch { ThrowInput "$label user envelope is invalid JSON" }
        $inputText = GetStringProperty $envelope 'sourceDescription' "$label user envelope"
    } else {
        $inputText = $envelopeText
    }
    $inputBytes = $utf8.GetBytes($inputText)
    $input = GetRequiredProperty $stage 'input' $label
    if ((GetIntegerProperty $input 'bytes' "$label.input") -ne $inputBytes.Length -or (GetStringProperty $input 'sha256' "$label.input") -ne (HashBytes $inputBytes)) { ThrowEvidence "$label input hash/bytes differ from provider envelope" }
    $captureInput = GetRequiredProperty $capture 'input' "$label.capture"
    if ((GetIntegerProperty $captureInput 'bytes' "$label.capture.input") -ne $inputBytes.Length -or (GetStringProperty $captureInput 'sha256' "$label.capture.input") -ne (HashBytes $inputBytes)) { ThrowEvidence "$label capture input hash/bytes differ" }
    if ((GetStringProperty $stage 'protocol' $label) -ne (GetStringProperty $capture 'protocol' "$label.capture") -or (GetStringProperty $stage 'endpointClass' $label) -ne (GetStringProperty $capture 'endpointClass' "$label.capture") -or (GetStringProperty $stage 'model' $label) -ne (GetStringProperty $capture 'model' "$label.capture") -or (GetStringProperty $stage 'model' $label) -ne (GetStringProperty $topModel 'name' 'model')) { ThrowEvidence "$label request metadata differs" }
    if ((GetStringProperty $body 'model' "$label provider body") -ne (GetStringProperty $topModel 'name' 'model')) { ThrowEvidence "$label provider body model differs" }
    $requestHash = HashBytes $bodyBytes
    if ((GetStringProperty $stage 'requestSha256' $label) -ne $requestHash -or (GetIntegerProperty $stage 'requestBytes' $label) -ne $bodyBytes.Length) { ThrowEvidence "$label request hash/bytes differ" }
    if ((GetIntegerProperty $stage 'systemPromptBytes' $label) -ne $utf8.GetByteCount($systemText) -or (GetIntegerProperty $stage 'envelopeBytes' $label) -ne $utf8.GetByteCount($envelopeText) -or (GetStringProperty $stage 'promptSha256' $label) -ne (HashText $systemText)) { ThrowEvidence "$label prompt/envelope byte accounting differs" }
    $options = GetRequiredProperty $body 'options' "$label provider body"
    $numCtx = GetIntegerProperty $options 'num_ctx' "$label provider body.options"
    $numPredict = GetIntegerProperty $options 'num_predict' "$label provider body.options"
    if ((GetIntegerProperty $stage 'numCtx' $label) -ne $numCtx -or (GetIntegerProperty $stage 'numPredict' $label) -ne $numPredict) { ThrowEvidence "$label request budget differs" }
    $protocol = GetStringProperty $stage 'protocol' $label
    $kind = GetStringProperty $stage 'envelopeKind' $label
    $expectedKind = if ($protocol -eq 'ollama') { 'ollama_native' } else { 'openai_compatible' }
    if ($kind -eq 'unknown' -and (GetRequiredProperty $stage 'accepted' $label)) { ThrowEvidence "$label accepted stage cannot have unknown envelope kind" }
    if ($kind -ne 'unknown' -and $kind -ne $expectedKind) { ThrowEvidence "$label envelope kind differs from protocol" }
    $accepted = [bool](GetRequiredProperty $stage 'accepted' $label)
    $failureNoPayload = [bool](GetRequiredProperty $stage 'failureNoAcceptedPayload' $label)
    $reasonProperty = $stage.PSObject.Properties['completionReason']
    $completionReason = if ($null -eq $reasonProperty -or $null -eq $reasonProperty.Value) { $null } else { [string]$reasonProperty.Value }
    if ($accepted -and [string]::IsNullOrWhiteSpace($completionReason)) { ThrowEvidence "$label accepted stage has no completion reason" }
    if ($accepted -and $completionReason -eq 'length') { ThrowEvidence "$label accepted stage reports length truncation" }
    if ($accepted -and $failureNoPayload) { ThrowEvidence "$label accepted stage reports no accepted payload" }
    $usageProperty = $stage.PSObject.Properties['usage']
    if ($null -ne $usageProperty -and $null -ne $usageProperty.Value) {
        $usage = $usageProperty.Value
        $promptTokens = GetIntegerProperty $usage 'promptTokens' "$label.usage"
        $completionTokens = GetIntegerProperty $usage 'completionTokens' "$label.usage"
        $totalTokens = GetIntegerProperty $usage 'totalTokens' "$label.usage"
        if ($promptTokens + $completionTokens -ne $totalTokens) { ThrowEvidence "$label usage totals differ" }
    }
    if (-not $accepted) { ThrowEvidence "$label did not produce an accepted result" }
    foreach ($verdictName in @('transportVerdict', 'qualityVerdict', 'performanceVerdict')) { if ((GetStringProperty $stage $verdictName $label) -ne 'PASS') { ThrowEvidence "$label has non-PASS $verdictName" } }
    return [ordered]@{ stage = $stageName; operation = $operation; protocol = $protocol; endpointClass = (GetStringProperty $stage 'endpointClass' $label); model = (GetStringProperty $stage 'model' $label); envelopeKind = $kind; durationMs = (GetNumberProperty $stage 'durationMs' $label) }
}

function ValidateSide([object]$evidence, [string]$sourceRoot, [string]$packagePath, [string]$workerPath, [string]$matrixPath, [string]$capturePath, [string]$side) {
    $sideBuild = GetRequiredProperty $evidence $side 'evidence'
    if ($null -eq $sideBuild) { ThrowEvidence "evidence.$side is null" }
    $otherSide = if ($side -eq 'candidate') { 'baseline' } else { 'candidate' }
    if ($null -ne (GetRequiredProperty $evidence $otherSide 'evidence')) { ThrowEvidence "evidence cannot contain both candidate and baseline builds" }
    $metadata = ValidateBuildAndManifests $sourceRoot $packagePath $sideBuild $side
    ValidateCommonEvidence $evidence $workerPath $matrixPath $side
    $topModel = GetRequiredProperty $evidence 'model' $side
    $stages = @((GetRequiredProperty $evidence 'workbenchStages' $side))
    $captures = @(ReadCapture $capturePath "$side capture")
    if ($stages.Count -ne 4 -or $captures.Count -ne 4) { ThrowEvidence "$side must contain exactly four stages and four captures" }
    for ($index = 0; $index -lt 4; $index++) {
        if ((GetStringProperty $stages[$index] 'stage' "$side stage $index") -ne $expectedStages[$index]) { ThrowEvidence "$side stage order differs" }
        if ((GetStringProperty $captures[$index] 'stage' "$side capture $index") -ne $expectedStages[$index]) { ThrowEvidence "$side capture order differs" }
    }
    $stageEvidence = [Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt 4; $index++) { [void]$stageEvidence.Add((ValidateStage $stages[$index] $captures[$index] $topModel "$side.$($expectedStages[$index])")) }
    $verdicts = GetRequiredProperty $evidence 'verdicts' $side
    foreach ($verdictName in @('transport', 'quality', 'performance', 'overall')) { if ((GetStringProperty $verdicts $verdictName "$side.verdicts") -ne 'PASS') { ThrowEvidence "$side has non-PASS $verdictName verdict" } }
    return [ordered]@{ metadata = $metadata; stages = @($stageEvidence) }
}

function CompareSides([object]$candidate, [object]$baseline) {
    for ($index = 0; $index -lt 4; $index++) {
        $candidateStage = $candidate.stages[$index]
        $baselineStage = $baseline.stages[$index]
        foreach ($field in @('stage', 'operation', 'protocol', 'endpointClass', 'model', 'envelopeKind')) {
            if ($candidateStage[$field] -ne $baselineStage[$field]) { ThrowEvidence "candidate/baseline $field differs at $($candidateStage.stage)" }
        }
        if ($candidateStage.durationMs -gt ($baselineStage.durationMs * 2)) { ThrowEvidence "candidate duration exceeds baseline 2x at $($candidateStage.stage)" }
    }
}

try {
    $workspaceFull = Full $WorkspaceRoot
    AssertDirectory $workspaceFull 'WorkspaceRoot'
    $sourceFull = AssertUnder $SourceRoot $workspaceFull 'SourceRoot'
    $packageFull = AssertUnder $PackagePath $workspaceFull 'PackagePath'
    $workerFull = AssertUnder $WorkerReportPath $workspaceFull 'WorkerReportPath'
    $matrixFull = AssertUnder $MatrixPath $workspaceFull 'MatrixPath'
    $evidenceFull = AssertUnder $EvidencePath $workspaceFull 'EvidencePath'
    $baselineSourceFull = AssertUnder $BaselineSourceRoot $workspaceFull 'BaselineSourceRoot'
    $baselinePackageFull = AssertUnder $BaselinePackagePath $workspaceFull 'BaselinePackagePath'
    $baselineWorkerFull = AssertUnder $BaselineWorkerReportPath $workspaceFull 'BaselineWorkerReportPath'
    $baselineMatrixFull = AssertUnder $BaselineMatrixPath $workspaceFull 'BaselineMatrixPath'
    $baselineEvidenceFull = AssertUnder $BaselinePath $workspaceFull 'BaselinePath'
    $captureFull = AssertUnder $RequestCapturePath $workspaceFull 'RequestCapturePath'
    $baselineCaptureFull = AssertUnder $BaselineRequestCapturePath $workspaceFull 'BaselineRequestCapturePath'
    AssertDirectory $sourceFull 'SourceRoot'
    AssertDirectory $baselineSourceFull 'BaselineSourceRoot'
    AssertDirectory $packageFull 'PackagePath'
    AssertDirectory $baselinePackageFull 'BaselinePackagePath'
    foreach ($path in @($workerFull, $matrixFull, $evidenceFull, $baselineWorkerFull, $baselineMatrixFull, $baselineEvidenceFull, $captureFull, $baselineCaptureFull)) { AssertFile $path 'validator input' }
    if ($packageFull -eq $baselinePackageFull -or $evidenceFull -eq $baselineEvidenceFull -or $captureFull -eq $baselineCaptureFull) { ThrowInput 'candidate and baseline package/evidence/capture paths must be distinct' }
    if (-not (Test-Path -LiteralPath $schemaPath -PathType Leaf)) { ThrowInput "Evidence schema is missing: $schemaPath" }
    try { [void](Get-Content -LiteralPath $schemaPath -Raw | ConvertFrom-Json -Depth 100 -ErrorAction Stop) } catch { ThrowInput "Evidence schema is not valid JSON" }
    $candidateEvidence = ReadJsonFile $evidenceFull 'candidate evidence' -EvidenceSchema
    $baselineEvidence = ReadJsonFile $baselineEvidenceFull 'baseline evidence' -EvidenceSchema
    $candidate = ValidateSide $candidateEvidence $sourceFull $packageFull $workerFull $matrixFull $captureFull 'candidate'
    $baseline = ValidateSide $baselineEvidence $baselineSourceFull $baselinePackageFull $baselineWorkerFull $baselineMatrixFull $baselineCaptureFull 'baseline'
    CompareSides $candidate $baseline
    Write-Output 'PASS persona-workbench.ai-link-evidence.v2'
    Write-Output "CandidateBuildId=$($candidate.metadata.buildId)"
    Write-Output "BaselineBuildId=$($baseline.metadata.buildId)"
    exit 0
}
catch [ArgumentException] {
    Write-Error ("EVIDENCE_INPUT_INVALID: " + $_.Exception.Message.Replace('__INPUT__ ', ''))
    exit 2
}
catch [InvalidOperationException] {
    Write-Error ("EVIDENCE_INVALID: " + $_.Exception.Message.Replace('__EVIDENCE__ ', ''))
    exit 1
}
catch {
    $message = $_.Exception.Message
    if ($message.StartsWith('__INPUT__ ', [StringComparison]::Ordinal)) {
        Write-Error ("EVIDENCE_INPUT_INVALID: " + $message.Substring(10))
        exit 2
    }
    if ($message.StartsWith('__EVIDENCE__ ', [StringComparison]::Ordinal)) {
        Write-Error ("EVIDENCE_INVALID: " + $message.Substring(13))
        exit 1
    }
    Write-Error "EVIDENCE_INPUT_INVALID: $message"
    exit 2
}
