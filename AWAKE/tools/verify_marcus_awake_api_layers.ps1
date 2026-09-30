[CmdletBinding()]
param(
    [string]$ProjectRoot = '',
    [string]$OutputPath = '',
    [string]$LegacyBaselinePath = '',
    [string]$CurrentBaselinePath = '',
    [switch]$WriteCurrentBaseline
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = Split-Path -Parent $PSScriptRoot }
if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path $ProjectRoot 'docs\evidence\MARCUS-AWAKE-API-LAYERS-E1-20260829.json' }
if ([string]::IsNullOrWhiteSpace($LegacyBaselinePath)) { $LegacyBaselinePath = Join-Path $ProjectRoot 'docs\evidence\MARCUS-AWAKE-P3-API-SURFACE-BASELINE-20260826.json' }
if ([string]::IsNullOrWhiteSpace($CurrentBaselinePath)) { $CurrentBaselinePath = Join-Path $ProjectRoot 'docs\evidence\MARCUS-AWAKE-P3-API-SURFACE-BASELINE-CURRENT-20260829.json' }

$frameworkRoot = Join-Path $ProjectRoot 'framework\MarcusAwakeFramework'
$sourceRoot = Join-Path $frameworkRoot 'src'
$frameworkProject = Join-Path $frameworkRoot 'MarcusAwakeFramework.csproj'
$assemblyPath = Join-Path $frameworkRoot '_build_out\Release\MarcusAwakeFramework.dll'
$schemaRoot = Join-Path $ProjectRoot 'docs\evidence\schemas'
$evidenceSchemaPath = Join-Path $schemaRoot 'MARCUS-AWAKE-API-LAYERS-E1.schema.json'

function Get-RelativePath([string]$BasePath, [string]$Path) {
    $base = [IO.Path]::GetFullPath($BasePath).TrimEnd('\')
    $full = [IO.Path]::GetFullPath($Path)
    if ($full.Equals($base, [StringComparison]::OrdinalIgnoreCase)) { return '' }
    $prefix = $base + '\'
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Path escaped root: $full" }
    return $full.Substring($prefix.Length).Replace('\', '/')
}

function Get-TypeName([Type]$Type) {
    if ($null -eq $Type) { return '' }
    if ($Type.IsGenericType) {
        $name = $Type.GetGenericTypeDefinition().FullName
        $name = $name.Substring(0, $name.IndexOf('`'))
        return $name + '<' + (($Type.GetGenericArguments() | ForEach-Object { Get-TypeName $_ }) -join ',') + '>'
    }
    return $Type.FullName
}

function Get-ApiSurface([string]$Path) {
    $assembly = [Reflection.Assembly]::LoadFrom($Path)
    $types = @()
    foreach ($type in ($assembly.GetExportedTypes() | Sort-Object FullName)) {
        $methods = @($type.GetMethods([Reflection.BindingFlags]'Public,Instance,Static,DeclaredOnly') | Sort-Object Name, MetadataToken | ForEach-Object {
            [ordered]@{
                name = $_.Name
                return_type = Get-TypeName $_.ReturnType
                parameters = @($_.GetParameters() | ForEach-Object { [ordered]@{ name = $_.Name; type = Get-TypeName $_.ParameterType } })
            }
        })
        $properties = @($type.GetProperties([Reflection.BindingFlags]'Public,Instance,Static,DeclaredOnly') | Sort-Object Name | ForEach-Object {
            [ordered]@{ name = $_.Name; type = Get-TypeName $_.PropertyType; can_read = $_.CanRead; can_write = $_.CanWrite }
        })
        $fields = @($type.GetFields([Reflection.BindingFlags]'Public,Instance,Static,DeclaredOnly') | Sort-Object Name | ForEach-Object {
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

function Write-Utf8Json([string]$Path, [object]$Value) {
    $directory = Split-Path -Parent ([IO.Path]::GetFullPath($Path))
    if (-not [string]::IsNullOrWhiteSpace($directory)) { [IO.Directory]::CreateDirectory($directory) | Out-Null }
    $json = $Value | ConvertTo-Json -Depth 30
    [IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "JSON file does not exist: $Path" }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-ExceptionMessage([object]$ErrorRecord) {
    $exception = $ErrorRecord.Exception
    if ($null -eq $exception) { return [string]$ErrorRecord }
    while ($null -ne $exception.InnerException) { $exception = $exception.InnerException }
    return $exception.Message
}

function Get-JsonSchemaDependencySets {
    $sets = @()
    $toolOutputRoots = @(
        (Join-Path $ProjectRoot 'tools\worldbook-studio\src\Awake.WorldbookStudio.Cli\bin\Release\net10.0'),
        (Join-Path $ProjectRoot 'tools\worldbook-studio\tests\Awake.WorldbookStudio.Tests\bin\Release\net10.0')
    )
    foreach ($root in $toolOutputRoots) {
        $sets += [ordered]@{
            name = $root
            target_framework = 'net10.0'
            more = Join-Path $root 'Json.More.dll'
            pointer = Join-Path $root 'JsonPointer.Net.dll'
            schema = Join-Path $root 'JsonSchema.Net.dll'
            humanizer = Join-Path $root 'Humanizer.dll'
        }
    }

    $nugetRoots = @()
    if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) { $nugetRoots += $env:NUGET_PACKAGES }
    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) { $nugetRoots += Join-Path $env:USERPROFILE '.nuget\packages' }
    foreach ($root in ($nugetRoots | Select-Object -Unique)) {
        foreach ($targetFramework in @('net9.0', 'net8.0')) {
            $schemaRoot = Join-Path $root ('jsonschema.net\9.4.0\lib\' + $targetFramework)
            $morePath = Join-Path $root ('json.more.net\3.0.1\lib\' + $targetFramework + '\Json.More.dll')
            $pointerPath = Join-Path $root ('jsonpointer.net\7.0.2\lib\' + $targetFramework + '\JsonPointer.Net.dll')
            $schemaPath = Join-Path $schemaRoot 'JsonSchema.Net.dll'
            $humanizerPath = Join-Path $root ('humanizer.core\3.0.10\lib\' + $targetFramework + '\Humanizer.dll')
            $sets += [ordered]@{
                name = $schemaRoot
                target_framework = $targetFramework
                more = $morePath
                pointer = $pointerPath
                schema = $schemaPath
                humanizer = $humanizerPath
            }
        }
    }
    return @($sets)
}

function Test-EvidenceAgainstSchema([string]$SchemaPath, [string]$EvidencePath) {
    $result = [ordered]@{ schema_exists = $false; evidence_parsed = $false; valid = $false; errors = @() }
    if (-not (Test-Path -LiteralPath $SchemaPath -PathType Leaf)) {
        $result.errors = @('Schema file does not exist: ' + [IO.Path]::GetFullPath($SchemaPath))
        return $result
    }
    $result.schema_exists = $true

    $helperRoot = $null
    $locationPushed = $false
    try {
        [string]$schemaText = [IO.File]::ReadAllText($SchemaPath)
        $null = $schemaText | ConvertFrom-Json
        [string]$evidenceText = [IO.File]::ReadAllText($EvidencePath)
        $null = $evidenceText | ConvertFrom-Json
        $result.evidence_parsed = $true

        $dependencySet = $null
        $checkedCandidates = @()
        foreach ($candidate in @(Get-JsonSchemaDependencySets)) {
            $checkedCandidates += [string]$candidate.name
            $dependencyPaths = @($candidate.more, $candidate.pointer, $candidate.schema, $candidate.humanizer)
            if (@($dependencyPaths | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }).Count -eq 0) {
                $dependencySet = $candidate
                break
            }
        }
        if ($null -eq $dependencySet) {
            throw ('No offline JsonSchema.Net dependency set was found. Checked: ' + ($checkedCandidates -join '; '))
        }

        $helperRoot = Join-Path ([IO.Path]::GetTempPath()) ('marcus-awake-schema-validator-' + [guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($helperRoot) | Out-Null
        $projectPath = Join-Path $helperRoot 'SchemaValidator.csproj'
        $sourcePath = Join-Path $helperRoot 'Program.cs'
        $helperProject = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$($dependencySet.target_framework)</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>SchemaValidator</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="JsonSchema.Net"><HintPath>$($dependencySet.schema)</HintPath><Private>true</Private></Reference>
    <Reference Include="Json.More"><HintPath>$($dependencySet.more)</HintPath><Private>true</Private></Reference>
    <Reference Include="JsonPointer.Net"><HintPath>$($dependencySet.pointer)</HintPath><Private>true</Private></Reference>
    <Reference Include="Humanizer"><HintPath>$($dependencySet.humanizer)</HintPath><Private>true</Private></Reference>
  </ItemGroup>
</Project>
"@
        $helperSource = @'
using System;
using System.IO;
using System.Text.Json;
using Json.Schema;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("SCHEMA_ARGUMENTS_INVALID");
            return 2;
        }

        try
        {
            var schemaPath = Path.GetFullPath(args[0]);
            var instancePath = Path.GetFullPath(args[1]);
            var schema = JsonSchema.FromText(File.ReadAllText(schemaPath), BuildOptions.Default, new Uri(schemaPath));
            using var instance = JsonDocument.Parse(File.ReadAllText(instancePath));
            var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (result.IsValid)
            {
                Console.WriteLine("SCHEMA_VALID");
                return 0;
            }

            Console.Error.WriteLine("SCHEMA_INVALID");
            Console.Error.WriteLine(result.ToString());
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("SCHEMA_EXECUTION_FAILED:" + exception.GetType().Name + ":" + exception.Message);
            return 1;
        }
    }
}
'@
        [IO.File]::WriteAllText($projectPath, $helperProject, [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText($sourcePath, $helperSource, [Text.UTF8Encoding]::new($false))

        $restoreOutput = @()
        $buildOutput = @()
        $validationOutput = @()
        $validationExitCode = 1
        Push-Location $helperRoot
        $locationPushed = $true
        try {
            $restoreOutput = @(& dotnet restore $projectPath '--ignore-failed-sources' '--nologo' 2>&1 | ForEach-Object { $_.ToString() })
            $restoreExitCode = [int]$LASTEXITCODE
            if ($restoreExitCode -ne 0) { throw ('JsonSchema.Net helper restore failed (' + $restoreExitCode + '): ' + (($restoreOutput -join ' ').Trim())) }
            $buildOutput = @(& dotnet build $projectPath '-c' 'Release' '--no-restore' '--nologo' 2>&1 | ForEach-Object { $_.ToString() })
            $buildExitCode = [int]$LASTEXITCODE
            if ($buildExitCode -ne 0) { throw ('JsonSchema.Net helper build failed (' + $buildExitCode + '): ' + (($buildOutput -join ' ').Trim())) }
            $helperDll = Join-Path $helperRoot ('bin\Release\' + $dependencySet.target_framework + '\SchemaValidator.dll')
            $validationOutput = @(& dotnet $helperDll ([IO.Path]::GetFullPath($SchemaPath)) ([IO.Path]::GetFullPath($EvidencePath)) 2>&1 | ForEach-Object { $_.ToString() })
            $validationExitCode = [int]$LASTEXITCODE
        }
        finally {
            if ($locationPushed) { Pop-Location; $locationPushed = $false }
        }

        $diagnostics = @($validationOutput | ForEach-Object { $_.ToString().Trim() } | Where-Object { $_ -ne '' } | Select-Object -First 20)
        $result.valid = ($validationExitCode -eq 0 -and @($diagnostics | Where-Object { $_ -eq 'SCHEMA_VALID' }).Count -gt 0)
        if (-not $result.valid) {
            if ($diagnostics.Count -eq 0) { $diagnostics = @('JsonSchema.Net helper exited with code ' + $validationExitCode + ' without a diagnostic.') }
            $result.errors = $diagnostics
        }
    } catch {
        $result.errors = @((Get-ExceptionMessage $_))
    } finally {
        if ($locationPushed) { Pop-Location; $locationPushed = $false }
        if ($null -ne $helperRoot -and (Test-Path -LiteralPath $helperRoot -PathType Container)) {
            $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
            $helperRootFull = [IO.Path]::GetFullPath($helperRoot)
            if ($helperRootFull.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
                Remove-Item -LiteralPath $helperRootFull -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
    return $result
}

function Invoke-Build {
    $output = @(& dotnet build $frameworkProject -c Release --no-restore --nologo 2>&1 | ForEach-Object { $_.ToString() })
    $exitCode = $LASTEXITCODE
    return [ordered]@{ command = "dotnet build $frameworkProject -c Release --no-restore --nologo"; exit_code = $exitCode; output = $output }
}

function Find-SourceFile([string]$TypeName) {
    $shortName = ($TypeName.Split('.')[-1]) -replace '`.*$', ''
    foreach ($file in (Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.cs' | Sort-Object FullName)) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $typePattern = "public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+|readonly\s+|ref\s+)?(?:class|interface|struct|enum)\s+$([Regex]::Escape($shortName))\b"
        $delegatePattern = "public\s+delegate\s+.*\s+$([Regex]::Escape($shortName))\s*\("
        if ($text -match $typePattern -or $text -match $delegatePattern) { return Get-RelativePath $sourceRoot $file.FullName }
    }
    return ''
}

function Get-ApiPhase([string]$TypeName, [string]$SourceFile, [System.Collections.Generic.HashSet[string]]$LegacyNames) {
    if ($LegacyNames.Contains($TypeName)) { return 'P3A-approved' }
    $shortName = ($TypeName.Split('.')[-1]) -replace '`.*$', ''
    if ($shortName -in @('RuntimeServiceClient', 'RuntimeServiceClientOptions')) { return 'P3B-approved' }
    if ($SourceFile -eq 'ProviderRuntimeApi.cs' -or $shortName -in @('AiTaskHandle', 'ProviderErrorMapping')) { return 'P3D-approved' }
    if ($SourceFile -match '^Compat/(Embedding|Rerank|Sql|Timeline)' -or $shortName -eq 'ProviderTaskRequest') { return 'P3C-approved' }
    if ($SourceFile -match '^(Compat/|FullApiTypes\.cs$|ApiCompatibilityTypes\.cs$|ApiCompatibilityExtensions\.cs$|ServiceOverrides\.cs$)') { return 'P5-approved' }
    return 'unclassified'
}

$build = Invoke-Build
$assemblyExists = Test-Path -LiteralPath $assemblyPath -PathType Leaf
$assemblyHash = if ($assemblyExists) { (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash } else { '' }
$currentSurface = if ($assemblyExists) { @(Get-ApiSurface $assemblyPath) } else { @() }

$legacy = Read-Json $LegacyBaselinePath
$legacyNames = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($type in @($legacy.types)) { [void]$legacyNames.Add([string]$type.full_name) }
$currentNames = @($currentSurface | ForEach-Object { $_.full_name })
$missingLegacy = @($legacyNames | Where-Object { -not ($currentNames -contains $_) } | Sort-Object)
$addedNames = @($currentNames | Where-Object { -not $legacyNames.Contains($_) } | Sort-Object)

$phaseRecords = @()
foreach ($type in $currentSurface) {
    $sourceFile = Find-SourceFile $type.full_name
    $phaseRecords += [ordered]@{
        type = $type.full_name
        source_file = $sourceFile
        phase = Get-ApiPhase $type.full_name $sourceFile $legacyNames
        legacy_baseline = $legacyNames.Contains($type.full_name)
    }
}
$unclassified = @($phaseRecords | Where-Object { $_['phase'] -eq 'unclassified' } | ForEach-Object { $_['type'] } | Sort-Object)
$phaseCounts = [ordered]@{}
foreach ($record in $phaseRecords) {
    $phase = [string]$record['phase']
    if (-not $phaseCounts.Contains($phase)) { $phaseCounts[$phase] = 0 }
    $phaseCounts[$phase]++
}

$currentBaselineMatch = $false
if ($WriteCurrentBaseline -and $assemblyExists) {
    # D-10：公共 API 面变更后，用当前程序集重建基线，避免手写漂移。
    Write-Utf8Json $CurrentBaselinePath ([ordered]@{ types = @($currentSurface) })
}
if ((Test-Path -LiteralPath $CurrentBaselinePath -PathType Leaf) -and $assemblyExists) {
    $currentBaseline = Read-Json $CurrentBaselinePath
    $actualJson = (@{ types = $currentSurface } | ConvertTo-Json -Depth 30 -Compress)
    $expectedJson = (@{ types = @($currentBaseline.types) } | ConvertTo-Json -Depth 30 -Compress)
    $currentBaselineMatch = [StringComparer]::Ordinal.Equals($actualJson, $expectedJson)
}

$forbiddenPatterns = [ordered]@{
    http_client = 'HttpClient|System\.Net\.Http'
    sockets = 'System\.Net\.Sockets|SocketAsyncEventArgs'
    named_pipe = 'NamedPipe'
    sqlite = 'SQLite|Microsoft\.Data\.Sqlite|System\.Data\.SQLite'
    process = 'System\.Diagnostics\.Process|\bProcess\s*\.'
    file_io = '\b(File|Directory)\s*\.'
    environment_secret = "Environment\.GetEnvironmentVariable\s*\(\s*['""](?=[^'""]*(KEY|SECRET|TOKEN|PASSWORD|CREDENTIAL))"
    blocking_wait = '\.Wait\s*\(|GetAwaiter\(\)\s*\.\s*GetResult\s*\(|\b(?:task|[A-Za-z_][A-Za-z0-9_]*Task)\s*\.\s*Result\b'
    wall_clock = 'DateTime(Offset)?\.(UtcNow|Now)'
    bannerlord_reference = 'using\s+(TaleWorlds|Bannerlord)|TaleWorlds\.'
}
$sourceFindings = @()
foreach ($file in (Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.cs' | Sort-Object FullName)) {
    $relative = Get-RelativePath $sourceRoot $file.FullName
    $text = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($entry in $forbiddenPatterns.GetEnumerator()) {
        if ($text -match $entry.Value) { $sourceFindings += [ordered]@{ file = $relative; rule = $entry.Key } }
    }
}
$allowedArchitectureFindings = @(
    [ordered]@{ file = 'RuntimeServiceClient.cs'; rule = 'named_pipe'; reason = 'Approved Framework-to-Runtime Service IPC boundary.' },
    [ordered]@{ file = 'RuntimeServiceClient.cs'; rule = 'process'; reason = 'Approved Runtime Service process supervision.' },
    [ordered]@{ file = 'RuntimeServiceClient.cs'; rule = 'file_io'; reason = 'Approved packaged Runtime Service path resolution.' },
    [ordered]@{ file = 'RuntimeServiceClient.cs'; rule = 'blocking_wait'; reason = 'Synchronous IDisposable teardown joins startup and drain tasks to prevent orphaned Runtime Service work.' },
    [ordered]@{ file = 'RuntimeServiceClient.cs'; rule = 'wall_clock'; reason = 'Required transport deadline enforcement.' },
    [ordered]@{ file = 'ApiCompatibilityExtensions.cs'; rule = 'wall_clock'; reason = 'Timestamp for unavailable compatibility health projection.' },
    [ordered]@{ file = 'HostApi.cs'; rule = 'wall_clock'; reason = 'Session lifecycle deadline clock.' },
    [ordered]@{ file = 'RequestContext.cs'; rule = 'wall_clock'; reason = 'Request deadline expiry check.' }
)
$unexpectedSourceFindings = @($sourceFindings | Where-Object {
    $finding = $_
    -not (@($allowedArchitectureFindings | Where-Object { $_.file -eq $finding.file -and $_.rule -eq $finding.rule }).Count -gt 0)
})

$assemblyFindings = @()
if ($assemblyExists) {
    $assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
    foreach ($reference in $assembly.GetReferencedAssemblies()) {
        if ($reference.Name -match 'MarcusAIFramework|System\.Net\.Http|SQLite|TaleWorlds|Bannerlord') {
            $assemblyFindings += [ordered]@{ kind = 'assembly_reference'; value = $reference.FullName }
        }
    }
}

$publicApiFindings = @()
if ($assemblyExists) {
    foreach ($type in $currentSurface) {
        $memberTypes = @($type.methods | ForEach-Object { $_.return_type; $_.parameters | ForEach-Object type }) + @($type.properties | ForEach-Object type) + @($type.fields | ForEach-Object type)
        foreach ($memberType in $memberTypes) {
            if ($memberType -match 'System\.Diagnostics\.Process|System\.IO\.Pipes|System\.Net\.Http\.HttpClient|Microsoft\.Data\.Sqlite|System\.Data\.SQLite') {
                $publicApiFindings += [ordered]@{ type = $type.full_name; member_type = $memberType }
            }
        }
    }
}

$parseResults = @()
foreach ($schemaName in @('MARCUS-AWAKE-P3A-E1.schema.json', 'MARCUS-AWAKE-P3A-E2.schema.json')) {
    $schemaPath = Join-Path $schemaRoot $schemaName
    try { Read-Json $schemaPath | Out-Null; $parseResults += [ordered]@{ file = Get-RelativePath $ProjectRoot $schemaPath; parsed = $true; error = '' } }
    catch { $parseResults += [ordered]@{ file = Get-RelativePath $ProjectRoot $schemaPath; parsed = $false; error = $_.Exception.Message } }
}

$schemaPass = @($parseResults | Where-Object { -not $_.parsed }).Count -eq 0
$forbiddenPass = ($unexpectedSourceFindings.Count -eq 0 -and $assemblyFindings.Count -eq 0 -and $publicApiFindings.Count -eq 0)
$pass = ($build.exit_code -eq 0 -and $assemblyExists -and $legacyNames.Count -gt 0 -and $missingLegacy.Count -eq 0 -and $currentBaselineMatch -and $unclassified.Count -eq 0 -and $schemaPass -and $forbiddenPass)

$evidence = [ordered]@{
    schema_version = 'marcus-awake/api-layers-evidence/v1'
    evidence_level = 'E1'
    batch_id = 'MARCUS-AWAKE-API-LAYERS-20260829'
    commands = @($build)
    legacy_baseline = [ordered]@{ path = Get-RelativePath $ProjectRoot $LegacyBaselinePath; type_count = $legacyNames.Count; missing_types = $missingLegacy; preserved = ($missingLegacy.Count -eq 0) }
    current_baseline = [ordered]@{ path = Get-RelativePath $ProjectRoot $CurrentBaselinePath; type_count = $currentNames.Count; added_type_count = $addedNames.Count; match = $currentBaselineMatch }
    assembly = [ordered]@{ path = Get-RelativePath $ProjectRoot $assemblyPath; sha256 = $assemblyHash; version = if ($assemblyExists) { [Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version.ToString() } else { '' } }
    phase_counts = $phaseCounts
    api_phase_records = $phaseRecords
    unclassified_types = $unclassified
    parse_results = $parseResults
    forbidden_scan = [ordered]@{ source_findings = $sourceFindings; allowed_architecture_findings = $allowedArchitectureFindings; unexpected_source_findings = $unexpectedSourceFindings; assembly_findings = $assemblyFindings; public_api_findings = $publicApiFindings; passed = $forbiddenPass }
    pass = $pass
}
Write-Utf8Json $OutputPath $evidence
$schemaValidation = Test-EvidenceAgainstSchema $evidenceSchemaPath $OutputPath
$status = if ($pass -and $schemaValidation.valid) { 'PASS' } else { 'FAIL' }
Write-Output ("MARCUS-AWAKE-API-LAYERS-E1 " + $status + " build=" + $build.exit_code + " legacy_missing=" + $missingLegacy.Count + " current_api=" + $currentNames.Count + " current_baseline=" + $currentBaselineMatch + " unclassified=" + $unclassified.Count + " forbidden=" + $forbiddenPass + " evidence_json=" + $schemaValidation.evidence_parsed + " schema_validation=" + $schemaValidation.valid)
if (-not $schemaValidation.schema_exists) { Write-Error ("MARCUS-AWAKE-API-LAYERS-E1 schema validation failed: " + ($schemaValidation.errors -join ' | ')); exit 1 }
if (-not $schemaValidation.evidence_parsed) { Write-Error ("MARCUS-AWAKE-API-LAYERS-E1 evidence JSON parse failed: " + ($schemaValidation.errors -join ' | ')); exit 1 }
if (-not $schemaValidation.valid) { Write-Error ("MARCUS-AWAKE-API-LAYERS-E1 schema validation failed: " + ($schemaValidation.errors -join ' | ')); exit 1 }
if (-not $pass) { exit 1 }
exit 0
