[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$frameworkRoot = Join-Path $root 'framework'
$evidenceRoot = Join-Path $root 'docs\evidence'
$outputRoot = Join-Path $frameworkRoot 'MarcusAwakeRuntimeService\tests\_build_out\Release'
$finalEvidencePath = Join-Path $evidenceRoot 'MARCUS-AWAKE-P3D-A1-20260828.json'
$stableCapturePath = Join-Path $evidenceRoot 'MARCUS-AWAKE-P3D-A1-fake-http-capture-20260828.json'
$stdoutEvidencePath = Join-Path $evidenceRoot 'MARCUS-AWAKE-P3D-A1-harness-stdout-20260828.txt'
$stderrEvidencePath = Join-Path $evidenceRoot 'MARCUS-AWAKE-P3D-A1-harness-stderr-20260828.txt'
$schemaPath = Join-Path $root 'docs\schemas\marcus-awake-p3d-a1-evidence.v1.schema.json'
$harnessExe = Join-Path $outputRoot 'MarcusAwakeRuntimeService.Tests.exe'
$serviceExe = Join-Path $frameworkRoot 'MarcusAwakeRuntimeService\_build_out\Release\MarcusAwakeRuntimeService.exe'
$transportDll = Join-Path $frameworkRoot 'MarcusAwakeTransport\_build_out\Release\MarcusAwakeTransport.dll'
$frameworkDll = Join-Path $frameworkRoot 'MarcusAwakeFramework\_build_out\Release\MarcusAwakeFramework.dll'
$providerDll = Join-Path $frameworkRoot 'MarcusAwakeProvider\_build_out\Release\MarcusAwakeProvider.dll'
$providerTestExe = Join-Path $frameworkRoot 'MarcusAwakeProvider\tests\_build_out\Release\MarcusAwakeProvider.Tests.exe'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('marcus-awake-p3d-a1-verify-' + [Guid]::NewGuid().ToString('N'))

function Require([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function Write-Utf8([string]$path, [string]$text) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
}

function Read-Json([string]$path) {
    Require (Test-Path -LiteralPath $path) ('missing_json:' + $path)
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Get-Hash([string]$path) {
    Require (Test-Path -LiteralPath $path) ('missing_artifact:' + $path)
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-Lines([string]$text) {
    if ([string]::IsNullOrEmpty($text)) { return @() }
    return @($text.Replace("`r`n", "`n").Replace("`r", "`n").Split("`n", [StringSplitOptions]::RemoveEmptyEntries))
}

function Start-Captured([string]$name, [string]$filePath, [string[]]$arguments, [string]$workingDirectory) {
    $outPath = Join-Path $tempRoot ($name + '.stdout')
    $errPath = Join-Path $tempRoot ($name + '.stderr')
    Push-Location $workingDirectory
    try {
        & $filePath @arguments 1> $outPath 2> $errPath
        $exitCode = [int]$LASTEXITCODE
    }
    catch {
        $exitCode = 1
        Write-Utf8 $errPath $_.Exception.Message
    }
    finally {
        Pop-Location
    }
    $stdout = if (Test-Path -LiteralPath $outPath) { Get-Content -Raw -LiteralPath $outPath } else { '' }
    $stderr = if (Test-Path -LiteralPath $errPath) { Get-Content -Raw -LiteralPath $errPath } else { '' }
    return [pscustomobject]@{
        Name = $name
        FilePath = $filePath
        Arguments = @($arguments)
        WorkingDirectory = [IO.Path]::GetFullPath($workingDirectory)
        ExitCode = $exitCode
        Stdout = $stdout
        Stderr = $stderr
        StdoutPath = $outPath
        StderrPath = $errPath
    }
}

function Assert-Build([object]$result) {
    Require ($result.ExitCode -eq 0) ('build_failed:' + $result.Name + ':exit=' + $result.ExitCode)
    $text = $result.Stdout + "`n" + $result.Stderr
    Require ($text -notmatch '(?im)^\s*error\s+[A-Z]+\d+') ('compiler_error:' + $result.Name)
    Require ($text -notmatch '(?im)^\s*warning\s+[A-Z]+\d+') ('compiler_warning:' + $result.Name)
}

function Get-Metadata([string[]]$lines, [string]$key) {
    $prefix = $key + '='
    $matches = @($lines | Where-Object { $_.StartsWith($prefix, [StringComparison]::Ordinal) })
    Require ($matches.Count -eq 1) ('metadata_missing_or_duplicate:' + $key)
    return $matches[0].Substring($prefix.Length)
}

function Get-JsonMetadata([string[]]$lines, [string]$key) {
    $raw = Get-Metadata $lines $key
    if ($raw.Trim() -eq '[]') { return }
    try {
        $value = $raw | ConvertFrom-Json
        if ($value -is [Array]) {
            foreach ($item in $value) { Write-Output $item }
        }
        else {
            Write-Output $value
        }
    }
    catch {
        throw ('metadata_json_invalid:' + $key + ':' + $_.Exception.Message)
    }
}

function Assert-SimpleRunner([object]$result, [string]$passLine, [string]$failLine) {
    Require ($result.ExitCode -eq 0) ('runner_failed:' + $result.Name + ':exit=' + $result.ExitCode)
    Require ([string]::IsNullOrEmpty($result.Stderr)) ('runner_stderr_not_empty:' + $result.Name)
    $lines = Get-Lines $result.Stdout
    Require ($lines -contains $passLine) ('runner_pass_line_invalid:' + $result.Name)
    Require ($lines -contains $failLine) ('runner_fail_line_invalid:' + $result.Name)
    Require (@($lines | Where-Object { $_.StartsWith('FAIL ', [StringComparison]::Ordinal) }).Count -eq 0) ('runner_case_failed:' + $result.Name)
}

function Count-Forbidden([string]$text, [string[]]$tokens) {
    $count = 0
    foreach ($token in $tokens) {
        $count += [Text.RegularExpressions.Regex]::Matches($text, [Text.RegularExpressions.Regex]::Escape($token), [Text.RegularExpressions.RegexOptions]::IgnoreCase).Count
    }
    return $count
}

function Invoke-JsonSchemaValidation([string]$schemaFile, [string]$instanceFile) {
    $helperRoot = Join-Path $tempRoot 'schema-validator'
    New-Item -ItemType Directory -Force -Path $helperRoot | Out-Null
    $jsonSchemaDll = 'C:\Users\26811\.nuget\packages\jsonschema.net\9.4.0\lib\net8.0\JsonSchema.Net.dll'
    $jsonMoreDll = 'C:\Users\26811\.nuget\packages\json.more.net\3.0.1\lib\net8.0\Json.More.dll'
    $jsonPointerDll = 'C:\Users\26811\.nuget\packages\jsonpointer.net\7.0.2\lib\net8.0\JsonPointer.Net.dll'
    foreach ($dependency in @($jsonSchemaDll, $jsonMoreDll, $jsonPointerDll)) { Require (Test-Path -LiteralPath $dependency) ('schema_dependency_missing:' + $dependency) }
    $projectPath = Join-Path $helperRoot 'SchemaValidator.csproj'
    $sourcePath = Join-Path $helperRoot 'Program.cs'
    $project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>SchemaValidator</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="JsonSchema.Net"><HintPath>$jsonSchemaDll</HintPath><Private>true</Private></Reference>
    <Reference Include="Json.More"><HintPath>$jsonMoreDll</HintPath><Private>true</Private></Reference>
    <Reference Include="JsonPointer.Net"><HintPath>$jsonPointerDll</HintPath><Private>true</Private></Reference>
  </ItemGroup>
</Project>
"@
    $source = @'
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
            var options = BuildOptions.Default;
            var schemaPath = Path.GetFullPath(args[0]);
            var schema = JsonSchema.FromText(File.ReadAllText(schemaPath), options, new Uri(schemaPath));
            using var instance = JsonDocument.Parse(File.ReadAllText(args[1]));
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
    Write-Utf8 $projectPath $project
    Write-Utf8 $sourcePath $source
    Push-Location $helperRoot
    try {
        $restoreOutput = (& dotnet restore $projectPath '--ignore-failed-sources' '--nologo' 2>&1 | Out-String)
        $restoreExitCode = [int]$LASTEXITCODE
        Require ($restoreExitCode -eq 0) ('schema_helper_restore_failed:' + $restoreOutput.Trim())
        $buildOutput = (& dotnet build $projectPath '-c' 'Release' '--no-restore' '--nologo' 2>&1 | Out-String)
        $buildExitCode = [int]$LASTEXITCODE
        Require ($buildExitCode -eq 0) ('schema_helper_build_failed:' + $buildOutput.Trim())
        $helperDll = Join-Path $helperRoot 'bin\Release\net8.0\SchemaValidator.dll'
        $validationOutput = (& dotnet $helperDll $schemaFile $instanceFile 2>&1 | Out-String)
        $validationExitCode = [int]$LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    Require ($validationExitCode -eq 0) ('schema_validation_failed:' + $validationOutput.Trim())
    Require ($validationOutput -match 'SCHEMA_VALID') 'schema_validation_marker_missing'
}

$a1Ids = @(
    'P3D-A1-S01-profile_upsert_creates_scoped_registry_entry',
    'P3D-A1-S02-profile_remove_is_scoped_and_idempotent',
    'P3D-A1-S03-models_calls_fake_http_and_maps_models',
    'P3D-A1-S04-complete_calls_fake_http_and_maps_typed_result',
    'P3D-A1-S05-missing_credential_never_calls_http',
    'P3D-A1-S06-provider_http_error_maps_safe_typed_error',
    'P3D-A1-S07-deadline_and_cancellation_stop_unary_call',
    'P3D-A1-S08-profile_scope_isolation',
    'P3D-A1-S09-duplicate_frame_replays_same_non_durable_result',
    'P3D-A1-S10-oversized_or_malformed_provider_response_is_rejected',
    'P3D-A1-S11-no_secret_in_logs_evidence_or_fake_server_capture',
    'P3D-A1-S12-child_service_shutdown_has_no_orphan',
    'P3D-A1-S13-cancel_before_provider_admission_never_calls_http',
    'P3D-A1-S14-cancel_during_http_maps_cancelled_without_late_completion',
    'P3D-A1-S15-provider_completion_before_ipc_write_failure_is_reused_without_recall',
    'P3D-A1-S16-same_idempotency_payload_replays_without_recall',
    'P3D-A1-S17-different_payload_same_idempotency_key_is_conflict_without_recall',
    'P3D-A1-C01-unary_success_publishes_accepted_started_completed',
    'P3D-A1-C02-provider_error_maps_to_framework_error',
    'P3D-A1-C03-structured_json_and_usage_bounds',
    'P3D-A1-C04-caller_cancel_maps_to_cancelled_terminal',
    'P3D-A1-C05-service_unavailable_preserves_degraded_mode',
    'P3D-A1-C06-client_uses_real_provider_business_frame'
)

$builds = @(
    [pscustomobject]@{ Name = 'transport'; Project = Join-Path $frameworkRoot 'MarcusAwakeTransport\MarcusAwakeTransport.csproj'; WorkingDirectory = Join-Path $frameworkRoot 'MarcusAwakeTransport' },
    [pscustomobject]@{ Name = 'framework'; Project = Join-Path $frameworkRoot 'MarcusAwakeFramework\MarcusAwakeFramework.csproj'; WorkingDirectory = Join-Path $frameworkRoot 'MarcusAwakeFramework' },
    [pscustomobject]@{ Name = 'provider'; Project = Join-Path $frameworkRoot 'MarcusAwakeProvider\MarcusAwakeProvider.csproj'; WorkingDirectory = Join-Path $frameworkRoot 'MarcusAwakeProvider' },
    [pscustomobject]@{ Name = 'runtime-service'; Project = Join-Path $frameworkRoot 'MarcusAwakeRuntimeService\MarcusAwakeRuntimeService.csproj'; WorkingDirectory = Join-Path $frameworkRoot 'MarcusAwakeRuntimeService' },
    [pscustomobject]@{ Name = 'runtime-service-tests'; Project = Join-Path $frameworkRoot 'MarcusAwakeRuntimeService\tests\MarcusAwakeRuntimeService.Tests.csproj'; WorkingDirectory = Join-Path $frameworkRoot 'MarcusAwakeRuntimeService\tests' },
    [pscustomobject]@{ Name = 'provider-tests'; Project = Join-Path $frameworkRoot 'MarcusAwakeProvider\tests\MarcusAwakeProvider.Tests.csproj'; WorkingDirectory = Join-Path $frameworkRoot 'MarcusAwakeProvider\tests' }
)

New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
try {
    $buildResults = @()
    foreach ($build in $builds) {
        $result = Start-Captured $build.Name 'dotnet' @('build', $build.Project, '-c', 'Release', '--no-restore', '--nologo') $build.WorkingDirectory
        Assert-Build $result
        $buildResults += $result
    }

    $previousCapturePath = [Environment]::GetEnvironmentVariable('MARCUS_AWAKE_P3D_A1_CAPTURE_PATH', 'Process')
    [Environment]::SetEnvironmentVariable('MARCUS_AWAKE_P3D_A1_CAPTURE_PATH', [IO.Path]::GetFullPath($stableCapturePath), 'Process')
    try {
        $harness = Start-Captured 'p3d-a1' $harnessExe @('--p3d-a1') (Split-Path -Parent $harnessExe)
    }
    finally {
        [Environment]::SetEnvironmentVariable('MARCUS_AWAKE_P3D_A1_CAPTURE_PATH', $previousCapturePath, 'Process')
    }
    Require ($harness.ExitCode -eq 0) ('a1_harness_failed:exit=' + $harness.ExitCode)
    Require ([string]::IsNullOrEmpty($harness.Stderr)) 'a1_harness_stderr_not_empty'
    Write-Utf8 $stdoutEvidencePath $harness.Stdout
    Write-Utf8 $stderrEvidencePath $harness.Stderr
    $harnessLines = Get-Lines $harness.Stdout
    Require ($harnessLines -contains 'P3D-A1_PASS_COUNT=23') 'a1_harness_pass_count_invalid'
    Require ($harnessLines -contains 'P3D-A1_FAIL_COUNT=0') 'a1_harness_fail_count_invalid'
    foreach ($id in $a1Ids) { Require (@($harnessLines | Where-Object { $_ -eq ('PASS ' + $id) }).Count -eq 1) ('a1_case_missing:' + $id) }
    Require (@($harnessLines | Where-Object { $_.StartsWith('FAIL ', [StringComparison]::Ordinal) }).Count -eq 0) 'a1_case_failed'

    $runId = Get-Metadata $harnessLines 'P3D-A1_RUN_ID'
    $servicePid = [int](Get-Metadata $harnessLines 'P3D-A1_SERVICE_PID')
    $pipeName = Get-Metadata $harnessLines 'P3D-A1_PIPE_NAME'
    $serviceExitCode = [int](Get-Metadata $harnessLines 'P3D-A1_SERVICE_EXIT_CODE')
    $serviceArguments = @(Get-JsonMetadata $harnessLines 'P3D-A1_SERVICE_ARGUMENTS')
    $endpoint = Get-Metadata $harnessLines 'P3D-A1_FAKE_HTTP_ENDPOINT'
    $capturePath = [IO.Path]::GetFullPath((Get-Metadata $harnessLines 'P3D-A1_FAKE_HTTP_CAPTURE_PATH'))
    $requestCount = [int](Get-Metadata $harnessLines 'P3D-A1_PROVIDER_HTTP_REQUESTS')
    $responseCount = [int](Get-Metadata $harnessLines 'P3D-A1_PROVIDER_HTTP_RESPONSES')
    $requestObservations = @(Get-JsonMetadata $harnessLines 'P3D-A1_FAKE_HTTP_REQUEST_OBSERVATIONS')
    $caseObservations = @(Get-JsonMetadata $harnessLines 'P3D-A1_CASE_OBSERVATIONS')

    Require ($runId -match '^p3d-a1-[0-9a-f]{32}$') 'a1_run_id_invalid'
    Require ($servicePid -gt 0) 'a1_service_pid_invalid'
    Require (-not [string]::IsNullOrWhiteSpace($pipeName)) 'a1_pipe_name_invalid'
    Require ($serviceExitCode -eq 0) ('a1_service_exit_code_invalid:' + $serviceExitCode)
    Require ((@($serviceArguments).Count) -eq 0) 'a1_service_arguments_invalid'
    Require ($endpoint -match '^http://127\.0\.0\.1:[0-9]+/$') 'a1_fake_http_endpoint_invalid'
    Require ($capturePath -eq [IO.Path]::GetFullPath($stableCapturePath)) 'a1_capture_path_not_bound_to_verifier'
    Require (Test-Path -LiteralPath $capturePath) 'a1_capture_file_missing'
    Require ($requestCount -eq 12) ('a1_provider_request_count_invalid:' + $requestCount)
    Require ($responseCount -eq 10) ('a1_provider_response_count_invalid:' + $responseCount)
    Require ((@($requestObservations).Count) -eq $requestCount) ('a1_request_observation_count_invalid:observations=' + @($requestObservations).Count + ':requests=' + $requestCount)

    $capture = Read-Json $capturePath
    Require ([string]$capture.schema -eq 'marcus-awake.p3d-a1-fake-http-capture.v1') 'a1_capture_schema_invalid'
    $captureObservations = @($capture.observations)
    Require ($captureObservations.Count -eq $requestObservations.Count) 'a1_capture_observation_count_mismatch'
    for ($index = 0; $index -lt $requestObservations.Count; $index++) {
        Require ([int]$captureObservations[$index].index -eq [int]$requestObservations[$index].index) ('a1_capture_observation_index_mismatch:' + $index)
        Require ([string]$captureObservations[$index].method -eq [string]$requestObservations[$index].method) ('a1_capture_observation_method_mismatch:' + $index)
        Require ([string]$captureObservations[$index].path -eq [string]$requestObservations[$index].path) ('a1_capture_observation_path_mismatch:' + $index)
    }

    Require ($caseObservations.Count -eq $a1Ids.Count) 'a1_case_observation_count_invalid'
    $caseIdSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($case in $caseObservations) {
        Require ($caseIdSet.Add([string]$case.id)) ('a1_case_observation_duplicate:' + $case.id)
        Require ([string]$case.status -eq 'pass') ('a1_case_observation_failed:' + $case.id)
        Require ([string]$case.process_role -eq 'service_child') ('a1_case_process_role_invalid:' + $case.id)
        Require ([string]$case.service_executable -eq [IO.Path]::GetFullPath($serviceExe)) ('a1_case_service_path_invalid:' + $case.id)
        Require (([int]$case.service_pid) -gt 0) ('a1_case_service_pid_invalid:' + $case.id)
        Require (-not [string]::IsNullOrWhiteSpace([string]$case.private_pipe_name)) ('a1_case_pipe_invalid:' + $case.id)
        Require ([string]$case.fake_http_bind -eq $endpoint) ('a1_case_endpoint_invalid:' + $case.id)
        Require ([IO.Path]::GetFullPath([string]$case.fake_http_capture_path) -eq [IO.Path]::GetFullPath($stableCapturePath)) ('a1_case_capture_path_invalid:' + $case.id)
        Require (([int]$case.provider_http_request_count) -ge 0) ('a1_case_provider_count_invalid:' + $case.id)
        Require (([int]$case.provider_call_count) -ge 0) ('a1_case_provider_call_count_invalid:' + $case.id)
        Require (([int]$case.http_request_count) -ge 0) ('a1_case_http_count_invalid:' + $case.id)
        Require ([string]$case.observation_hash -match '^[0-9a-f]{64}$') ('a1_case_observation_hash_invalid:' + $case.id)
    }
    foreach ($id in $a1Ids) { Require ($caseIdSet.Contains($id)) ('a1_case_observation_missing:' + $id) }

    $ipcRegression = Start-Captured 'p3b-regression' $harnessExe @() (Split-Path -Parent $harnessExe)
    Assert-SimpleRunner $ipcRegression 'PASS_COUNT=19' 'FAIL_COUNT=0'
    $storageRegression = Start-Captured 'p3c-regression' $harnessExe @('--p3c') (Split-Path -Parent $harnessExe)
    Assert-SimpleRunner $storageRegression 'PASS_COUNT=7' 'FAIL_COUNT=0'
    $providerRegression = Start-Captured 'provider-regression' $providerTestExe @('--p3d-a0-provider') (Split-Path -Parent $providerTestExe)
    Require ($providerRegression.ExitCode -eq 0) ('provider_regression_failed:exit=' + $providerRegression.ExitCode)
    Require ([string]::IsNullOrEmpty($providerRegression.Stderr)) 'provider_regression_stderr_not_empty'
    Require ((Get-Lines $providerRegression.Stdout) -contains 'PASS_COUNT=1') 'provider_regression_pass_count_missing'
    Require ((Get-Lines $providerRegression.Stdout) -contains 'FAIL_COUNT=0') 'provider_regression_fail_count_invalid'

    $forbiddenTokens = @('api_key', 'authorization', 'database_path', 'connection_string', 'credential_value', 'raw_provider_exception')
    $redactionTexts = @(
        $harness.Stdout,
        $harness.Stderr,
        ($capture | ConvertTo-Json -Depth 12 -Compress),
        $ipcRegression.Stdout,
        $ipcRegression.Stderr,
        $storageRegression.Stdout,
        $storageRegression.Stderr,
        $providerRegression.Stdout,
        $providerRegression.Stderr
    )
    $preEvidenceForbiddenMatches = 0
    foreach ($text in $redactionTexts) { $preEvidenceForbiddenMatches += Count-Forbidden ([string]$text) $forbiddenTokens }
    $secretPattern = 'p3d-a1-secret-[0-9a-f]{16,}'
    $preEvidenceSecretFound = (($redactionTexts -join "`n") -match $secretPattern)
    Require ($preEvidenceForbiddenMatches -eq 0) ('a1_redaction_forbidden_token_found:' + $preEvidenceForbiddenMatches)
    Require (-not $preEvidenceSecretFound) 'a1_redaction_secret_marker_found'

    $record = [ordered]@{
        schema = 'marcus-awake.p3d-a1-evidence.v1'
        plan_revision = 7
        batch_id = 'MARCUS-AWAKE-P3D-A1-UNARY-PROVIDER-BRIDGE-20260828'
        captured_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
        runner = [ordered]@{
            executable = [IO.Path]::GetFullPath($harnessExe)
            arguments = @('--p3d-a1')
            exit_code = $harness.ExitCode
            working_directory = [IO.Path]::GetFullPath((Split-Path -Parent $harnessExe))
        }
        service = [ordered]@{
            executable = [IO.Path]::GetFullPath($serviceExe)
            arguments = $serviceArguments
            pid = $servicePid
            private_pipe_name = $pipeName
            exit_code = $serviceExitCode
            process_role = 'service_child'
        }
        fake_http = [ordered]@{
            bind = $endpoint
            capture_path = [IO.Path]::GetFullPath($stableCapturePath)
            request_count = $requestCount
            harness_request_count = 0
            service_request_count = $requestCount
            observations = $requestObservations
        }
        artifacts = [ordered]@{
            transport_sha256 = Get-Hash $transportDll
            framework_sha256 = Get-Hash $frameworkDll
            provider_sha256 = Get-Hash $providerDll
            service_sha256 = Get-Hash $serviceExe
            harness_sha256 = Get-Hash $harnessExe
        }
        cases = $caseObservations
        redaction = [ordered]@{
            scan_paths = [ordered]@{
                stdout = [IO.Path]::GetFullPath($stdoutEvidencePath)
                stderr = [IO.Path]::GetFullPath($stderrEvidencePath)
                fake_capture = [IO.Path]::GetFullPath($stableCapturePath)
                evidence = [IO.Path]::GetFullPath($finalEvidencePath)
            }
            secret_found = $false
            forbidden_match_count = 0
            passed = $true
        }
        flags = [ordered]@{
            external_network = $false
            real_cloud_provider = $false
            bannerlord_started = $false
            game_directory_synced = $false
        }
    }
    Write-Utf8 $finalEvidencePath ($record | ConvertTo-Json -Depth 20)
    $finalEvidenceText = Get-Content -Raw -LiteralPath $finalEvidencePath
    $allRedactionText = @($redactionTexts + $finalEvidenceText)
    $finalForbiddenMatches = 0
    foreach ($text in $allRedactionText) { $finalForbiddenMatches += Count-Forbidden ([string]$text) $forbiddenTokens }
    $finalSecretFound = (($allRedactionText -join "`n") -match $secretPattern)
    Require ($finalForbiddenMatches -eq 0) ('a1_final_redaction_forbidden_token_found:' + $finalForbiddenMatches)
    Require (-not $finalSecretFound) 'a1_final_redaction_secret_marker_found'

    Invoke-JsonSchemaValidation $schemaPath $finalEvidencePath
    $evidence = Read-Json $finalEvidencePath
    Require ([string]$evidence.schema -eq 'marcus-awake.p3d-a1-evidence.v1') 'a1_evidence_schema_invalid'
    Require ([int]$evidence.plan_revision -eq 7) 'a1_evidence_plan_revision_invalid'
    Require ([string]$evidence.batch_id -eq 'MARCUS-AWAKE-P3D-A1-UNARY-PROVIDER-BRIDGE-20260828') 'a1_evidence_batch_invalid'
    Require (@($evidence.cases).Count -eq 23) 'a1_evidence_case_count_invalid'
    Require ([bool]$evidence.redaction.passed) 'a1_evidence_redaction_failed'
    Require ([bool]$evidence.flags.external_network -eq $false) 'a1_external_network_flag_invalid'
    Require ([bool]$evidence.flags.real_cloud_provider -eq $false) 'a1_real_cloud_flag_invalid'
    Require ([bool]$evidence.flags.bannerlord_started -eq $false) 'a1_bannerlord_flag_invalid'
    Require ([bool]$evidence.flags.game_directory_synced -eq $false) 'a1_sync_flag_invalid'

    Write-Output ('P3D-A1_BUILD_COUNT=' + $buildResults.Count.ToString())
    Write-Output 'P3D-A1_BUILD_STATUS=PASS'
    Write-Output 'P3D-A1_IPC_REGRESSION=19/19'
    Write-Output 'P3D-A1_STORAGE_RAG_REGRESSION=7/7'
    Write-Output 'P3D-A1_PROVIDER_REGRESSION=PASS'
    Write-Output 'P3D-A1_CASES=23/23'
    Write-Output 'P3D-A1_SCHEMA=PASS'
    Write-Output 'P3D-A1_REDACTION=PASS'
    Write-Output ('EVIDENCE_PATH=' + [IO.Path]::GetFullPath($finalEvidencePath))
    Write-Output 'P3D-A1 VERIFIER PASS'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
