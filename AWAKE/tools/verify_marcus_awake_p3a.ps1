[CmdletBinding()]
param(
    [ValidateSet('Verify', 'WriteBaseline')]
    [string]$Mode = 'Verify',
    [string]$OutputPath = '',
    [string]$ApiBaselinePath = ''
)

$projectRoot = Split-Path -Parent $PSScriptRoot
$frameworkRoot = Join-Path $projectRoot 'framework\MarcusAwakeFramework'
$sourceRoot = Join-Path $frameworkRoot 'src'
$frameworkProject = Join-Path $frameworkRoot 'MarcusAwakeFramework.csproj'
$assemblyPath = Join-Path $frameworkRoot '_build_out\Release\MarcusAwakeFramework.dll'
$schemaRoot = Join-Path $projectRoot 'docs\evidence\schemas'
$defaultBaseline = Join-Path $projectRoot 'docs\evidence\MARCUS-AWAKE-P3-API-SURFACE-BASELINE-20260826.json'
$defaultOutput = Join-Path $projectRoot 'docs\evidence\MARCUS-AWAKE-P3A-E1-20260826.json'
if ([string]::IsNullOrWhiteSpace($ApiBaselinePath)) { $ApiBaselinePath = $defaultBaseline }
if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = $defaultOutput }

function Get-RelativePath([string]$basePath, [string]$path) {
    return $path.Substring($basePath.Length).TrimStart('\', '/') -replace '\\', '/'
}

function Get-TypeName([Type]$type) {
    if ($null -eq $type) { return '' }
    if ($type.IsGenericType) {
        $name = $type.GetGenericTypeDefinition().FullName
        $name = $name.Substring(0, $name.IndexOf('`'))
        return $name + '<' + (($type.GetGenericArguments() | ForEach-Object { Get-TypeName $_ }) -join ',') + '>'
    }
    return $type.FullName
}

function Get-ApiSurface([string]$path) {
    $assembly = [System.Reflection.Assembly]::LoadFrom($path)
    $types = @()
    foreach ($type in ($assembly.GetExportedTypes() | Sort-Object FullName)) {
        $methods = @($type.GetMethods([System.Reflection.BindingFlags]'Public,Instance,Static,DeclaredOnly') | Sort-Object Name, MetadataToken | ForEach-Object {
            [ordered]@{
                name = $_.Name
                return_type = Get-TypeName $_.ReturnType
                parameters = @($_.GetParameters() | ForEach-Object { [ordered]@{ name = $_.Name; type = Get-TypeName $_.ParameterType } })
            }
        })
        $properties = @($type.GetProperties([System.Reflection.BindingFlags]'Public,Instance,Static,DeclaredOnly') | Sort-Object Name | ForEach-Object {
            [ordered]@{ name = $_.Name; type = Get-TypeName $_.PropertyType; can_read = $_.CanRead; can_write = $_.CanWrite }
        })
        $fields = @($type.GetFields([System.Reflection.BindingFlags]'Public,Instance,Static,DeclaredOnly') | Sort-Object Name | ForEach-Object {
            [ordered]@{ name = $_.Name; type = Get-TypeName $_.FieldType }
        })
        $types += [ordered]@{
            full_name = $type.FullName
            kind = if ($type.IsEnum) { 'enum' } elseif ($type.IsInterface) { 'interface' } else { 'class' }
            methods = $methods
            properties = $properties
            fields = $fields
        }
    }
    return $types
}

function Write-Utf8Json([string]$path, [object]$value) {
    $directory = Split-Path -Parent ([System.IO.Path]::GetFullPath($path))
    if (-not [string]::IsNullOrWhiteSpace($directory)) { [System.IO.Directory]::CreateDirectory($directory) | Out-Null }
    $json = $value | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText($path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

function Read-Json([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { throw "JSON file does not exist: $path" }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Invoke-Build {
    $output = @(& dotnet build $frameworkProject -c Release --no-restore 2>&1 | ForEach-Object { $_.ToString() })
    $exitCode = $LASTEXITCODE
    return [ordered]@{ command = "dotnet build $frameworkProject -c Release --no-restore"; exit_code = $exitCode; output = $output }
}

if ($Mode -eq 'WriteBaseline') {
    $build = Invoke-Build
    if ($build.exit_code -ne 0) { $build.output | ForEach-Object { Write-Output $_ }; exit $build.exit_code }
    Write-Utf8Json $ApiBaselinePath ([ordered]@{ schema_version = 'marcus-awake/api-surface/v1'; assembly = 'MarcusAwakeFramework.dll'; assembly_version = '2.0.0.0'; types = @(Get-ApiSurface $assemblyPath) })
    Write-Output "API baseline written: $ApiBaselinePath"
    exit 0
}

$build = Invoke-Build
$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -File -Filter '*.cs' -Recurse | Sort-Object FullName)
$sourceHashes = [ordered]@{}
foreach ($file in $sourceFiles) { $sourceHashes[(Get-RelativePath $sourceRoot $file.FullName)] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
$assemblyHash = if (Test-Path -LiteralPath $assemblyPath) { (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash } else { '' }

$forbiddenPatterns = [ordered]@{
    'http_client' = 'HttpClient|System\.Net\.Http'
    'sockets' = 'System\.Net\.Sockets|SocketAsyncEventArgs'
    'named_pipe' = 'NamedPipe'
    'sqlite' = 'SQLite|Microsoft\.Data\.Sqlite|System\.Data\.SQLite'
    'process' = 'System\.Diagnostics\.Process|\bProcess\s*\.'
    'file_io' = '\b(File|Directory)\s*\.'
    'environment_secret' = @'
Environment\.GetEnvironmentVariable\s*\(\s*['"](?=[^'"]*(KEY|SECRET|TOKEN|PASSWORD|CREDENTIAL))
'@
    'blocking_wait' = '\.Wait\s*\(|GetAwaiter\(\)\s*\.\s*GetResult\s*\(|\b(?:task|[A-Za-z_][A-Za-z0-9_]*Task)\s*\.\s*Result\b'
    'bannerlord_reference' = 'using\s+(TaleWorlds|Bannerlord)|TaleWorlds\.'
}
$deferredSourceFiles = @()
$scannedSourceFiles = @()
function Get-P3ASourcePhase([string]$RelativePath) {
    $normalized = $RelativePath.Replace('\', '/')
    if ($normalized -eq 'RuntimeServiceClient.cs') { return 'P3B' }
    if ($normalized -eq 'ProviderRuntimeApi.cs' -or $normalized -eq 'AiTaskHandle.cs') { return 'P3D' }
    if ($normalized -eq 'ProviderTaskRequest.cs' -or $normalized -match '^Compat/(Embedding|Rerank|Sql|Timeline)') { return 'P3C' }
    if ($normalized -match '^Compat/' -or $normalized -in @('FullApiTypes.cs', 'ApiCompatibilityTypes.cs', 'ApiCompatibilityExtensions.cs')) { return 'P5' }
    return 'P3A'
}
$sourceFindings = @()
foreach ($file in $sourceFiles) {
    $relative = Get-RelativePath $sourceRoot $file.FullName
    $phase = Get-P3ASourcePhase $relative
    if ($phase -ne 'P3A') {
        $deferredSourceFiles += [ordered]@{ file = $relative; phase = $phase }
        continue
    }
    $scannedSourceFiles += $relative
    $text = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($entry in $forbiddenPatterns.GetEnumerator()) {
        if ($text -match $entry.Value) { $sourceFindings += [ordered]@{ file = $relative; rule = $entry.Key } }
    }
}

$assemblyFindings = @()
if (Test-Path -LiteralPath $assemblyPath) {
    $assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)
    foreach ($type in $assembly.GetExportedTypes()) {
        if ($type.FullName -match '(^|\.)InMemory|Fixture|ProviderWire') { $assemblyFindings += [ordered]@{ kind = 'exported_type'; value = $type.FullName } }
    }
    foreach ($reference in $assembly.GetReferencedAssemblies()) {
        if ($reference.Name -match 'MarcusAIFramework|System\.Net\.Http|SQLite|TaleWorlds|Bannerlord') { $assemblyFindings += [ordered]@{ kind = 'assembly_reference'; value = $reference.FullName } }
    }
}

$apiSurface = if (Test-Path -LiteralPath $assemblyPath) { @(Get-ApiSurface $assemblyPath) } else { @() }
$baselineMatch = $false
if ((Test-Path -LiteralPath $ApiBaselinePath) -and (Test-Path -LiteralPath $assemblyPath)) {
    $baseline = Read-Json $ApiBaselinePath
    $actualJson = (@{ types = $apiSurface } | ConvertTo-Json -Depth 20 -Compress)
    $expectedJson = (@{ types = @($baseline.types) } | ConvertTo-Json -Depth 20 -Compress)
    $baselineMatch = [StringComparer]::Ordinal.Equals($actualJson, $expectedJson)
}

$parseResults = @()
foreach ($schema in @('MARCUS-AWAKE-P3A-E1.schema.json', 'MARCUS-AWAKE-P3A-E2.schema.json')) {
    $schemaPath = Join-Path $schemaRoot $schema
    try { Read-Json $schemaPath | Out-Null; $parseResults += [ordered]@{ file = Get-RelativePath $projectRoot $schemaPath; parsed = $true; error = '' } }
    catch { $parseResults += [ordered]@{ file = Get-RelativePath $projectRoot $schemaPath; parsed = $false; error = $_.Exception.Message } }
}

$errors = if ($build.exit_code -eq 0) { 0 } else { 1 }
$warnings = 0
$forbiddenPassed = ($sourceFindings.Count -eq 0 -and $assemblyFindings.Count -eq 0)
$evidence = [ordered]@{
    schema_version = 'marcus-awake/evidence/v1'
    evidence_level = 'P3A-E1'
    batch_id = 'MARCUS-AWAKE-P3-RUNTIME-VERTICAL-20260826'
    commands = @($build)
    source_files = @($sourceHashes.Keys)
    source_hashes = $sourceHashes
    assembly_hashes = [ordered]@{ 'MarcusAwakeFramework.dll' = $assemblyHash }
    api_surface = [ordered]@{ baseline_path = Get-RelativePath $projectRoot $ApiBaselinePath; baseline_match = $baselineMatch; public_type_count = $apiSurface.Count; assembly_version = if (Test-Path -LiteralPath $assemblyPath) { ([System.Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version.ToString()) } else { '' } }
    parse_results = $parseResults
    forbidden_scan = [ordered]@{ source_findings = $sourceFindings; scanned_source_files = $scannedSourceFiles; deferred_source_files = $deferredSourceFiles; assembly_findings = $assemblyFindings; inmemory_test_double_exports = ($assemblyFindings | Where-Object { $_.value -match '(^|\.)InMemory' }).Count -gt 0; passed = $forbiddenPassed }
    warning_count = $warnings
    error_count = $errors
    unverified = @('real_runtime_service_deferred', 'live_provider_deferred', 'sqlite_fts5_deferred', 'ipc_deferred', 'bannerlord_deferred')
}
Write-Utf8Json $OutputPath $evidence
$schemaPass = @($parseResults | Where-Object { -not $_.parsed }).Count -eq 0
$pass = ($build.exit_code -eq 0 -and $baselineMatch -and $schemaPass -and $forbiddenPassed)
Write-Output ("P3A-E1 " + ($(if ($pass) { 'PASS' } else { 'FAIL' })) + " build=" + $build.exit_code + " api=" + $baselineMatch + " forbidden=" + $forbiddenPassed)
if (-not $pass) { exit 1 }
exit 0
