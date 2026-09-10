[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$frameworkRoot = Join-Path $root 'framework'
$evidencePath = Join-Path $frameworkRoot 'MarcusAwakeRuntimeService\tests\_build_out\Release\MARCUS-AWAKE-P3D-A0-evidence.json'
$schemaPath = Join-Path $root 'docs\schemas\marcus-awake-p3d-a0-evidence.v1.schema.json'
$redactionInputPath = Join-Path $frameworkRoot 'MarcusAwakeRuntimeService\tests\fixtures\p3d-a0-redaction-inputs.v1.json'
$transportDll = Join-Path $frameworkRoot 'MarcusAwakeTransport\_build_out\Release\MarcusAwakeTransport.dll'
$coreDll = Join-Path $frameworkRoot 'MarcusAwakeFramework\_build_out\Release\MarcusAwakeFramework.dll'
$serviceExe = Join-Path $frameworkRoot 'MarcusAwakeRuntimeService\_build_out\Release\MarcusAwakeRuntimeService.exe'
$harnessExe = Join-Path $frameworkRoot 'MarcusAwakeRuntimeService\tests\_build_out\Release\MarcusAwakeRuntimeService.Tests.exe'
$transportTestExe = Join-Path $frameworkRoot 'MarcusAwakeTransport\_build_out\tests\Release\MarcusAwakeTransport.Tests.exe'
$coreTestExe = Join-Path $frameworkRoot 'MarcusAwakeFramework\_build_out\tests\Release\MarcusAwakeFramework.Tests.exe'
$providerTestExe = Join-Path $frameworkRoot 'MarcusAwakeProvider\tests\_build_out\Release\MarcusAwakeProvider.Tests.exe'
$serviceTestProject = Join-Path $frameworkRoot 'MarcusAwakeRuntimeService\tests\MarcusAwakeRuntimeService.Tests.csproj'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('marcus-awake-p3d-a0-verify-' + [Guid]::NewGuid().ToString('N'))

function Require([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function Read-Json([string]$path) {
    Require (Test-Path -LiteralPath $path) ('missing_file:' + $path)
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
        [IO.File]::WriteAllText($errPath, $_.Exception.Message)
    }
    finally {
        Pop-Location
    }
    $stdout = if (Test-Path -LiteralPath $outPath) { Get-Content -Raw -LiteralPath $outPath } else { '' }
    $stderr = if (Test-Path -LiteralPath $errPath) { Get-Content -Raw -LiteralPath $errPath } else { '' }
    return [pscustomobject]@{
        Name = $name
        FilePath = [IO.Path]::GetFullPath($filePath)
        Arguments = @($arguments)
        WorkingDirectory = [IO.Path]::GetFullPath($workingDirectory)
        ExitCode = $exitCode
        Stdout = $stdout
        Stderr = $stderr
    }
}

function Assert-Build([object]$result) {
    Require ($result.ExitCode -eq 0) ('build_failed:' + $result.Name + ':exit=' + $result.ExitCode)
    $text = $result.Stdout + "`n" + $result.Stderr
    Require ($text -notmatch '(?im)^\s*error\s+[A-Z]+\d+') ('compiler_error:' + $result.Name)
    Require ($text -notmatch '(?im)^\s*warning\s+[A-Z]+\d+') ('compiler_warning:' + $result.Name)
}

function Assert-Runner([object]$result, [string[]]$ids, [int]$count) {
    Require ($result.ExitCode -eq 0) ('runner_failed:' + $result.Name + ':exit=' + $result.ExitCode)
    $lines = Get-Lines $result.Stdout
    Require ($lines -contains ('PASS_COUNT=' + $count)) ('runner_pass_count_invalid:' + $result.Name)
    Require ($lines -contains 'FAIL_COUNT=0') ('runner_fail_count_invalid:' + $result.Name)
    foreach ($id in $ids) { Require ($lines -contains ('PASS ' + $id)) ('runner_case_missing:' + $result.Name + ':' + $id) }
}

function Assert-ExactRunner([object]$result, [string[]]$expectedLines) {
    Require ($result.ExitCode -eq 0) ('runner_failed:' + $result.Name + ':exit=' + $result.ExitCode)
    $actual = Get-Lines $result.Stdout
    Require (($actual -join "`n") -eq ($expectedLines -join "`n")) ('runner_stdout_invalid:' + $result.Name)
    Require ([string]::IsNullOrEmpty($result.Stderr)) ('runner_stderr_not_empty:' + $result.Name)
}

function Assert-CaseSet([object]$runner, [string[]]$ids, [string]$name) {
    $cases = @($runner.cases)
    Require ([int]$runner.case_count -eq $ids.Count) ($name + '_case_count')
    Require ($cases.Count -eq $ids.Count) ($name + '_case_array_count')
    $actual = @($cases | ForEach-Object { [string]$_.id })
    Require (($actual | Sort-Object -Unique).Count -eq $ids.Count) ($name + '_duplicate_case_id')
    foreach ($id in $ids) { Require ($actual -contains $id) ($name + '_missing_case:' + $id) }
    foreach ($case in $cases) { Require ([bool]$case.passed) ($name + '_case_failed:' + $case.id) }
}

function Assert-SubcaseSet([object]$case, [string[]]$ids, [string]$name) {
    $subcases = @($case.subcases)
    $actual = @($subcases | ForEach-Object { [string]$_.id })
    Require (($actual | Sort-Object -Unique).Count -eq $ids.Count) ($name + '_duplicate_or_count')
    foreach ($id in $ids) { Require ($actual -contains $id) ($name + '_missing_subcase:' + $id) }
    foreach ($subcase in $subcases) { Require ([bool]$subcase.passed) ($name + '_subcase_failed:' + $subcase.id) }
}

function Get-Case([object]$runner, [string]$id) {
    $case = @($runner.cases | Where-Object { $_.id -eq $id })
    Require ($case.Count -eq 1) ('case_not_unique:' + $id)
    return $case[0]
}

function Assert-Mapping([object]$mapping, [string]$providerCategory, [string]$wireCategory, [string]$coreCategory, [bool]$retryable, [bool]$fallbackAllowed) {
    Require ([string]$mapping.provider_category -eq $providerCategory) ('mapping_provider_category:' + $providerCategory)
    Require ([string]$mapping.wire_category -eq $wireCategory) ('mapping_wire_category:' + $providerCategory)
    Require ([string]$mapping.core_category -eq $coreCategory) ('mapping_core_category:' + $providerCategory)
    Require ([bool]$mapping.core_retryable -eq $retryable) ('mapping_retryable:' + $providerCategory)
    Require ([bool]$mapping.fallback_allowed -eq $fallbackAllowed) ('mapping_fallback:' + $providerCategory)
}

function Convert-ToWire([string]$value) {
    $builder = New-Object System.Text.StringBuilder
    for ($index = 0; $index -lt $value.Length; $index++) {
        $character = $value[$index]
        if ($index -gt 0 -and [char]::IsUpper($character)) { [void]$builder.Append('_') }
        [void]$builder.Append([char]::ToLowerInvariant($character))
    }
    return $builder.ToString()
}

function Get-CanonicalJson([string]$path) {
    $assembly = [Reflection.Assembly]::LoadFrom($coreDll)
    $type = $assembly.GetType('MarcusAwakeFramework.Api.TaskRequestCanonicalizer', $true)
    $method = $type.GetMethod('CanonicalizeJson', [Reflection.BindingFlags]'Public,Static')
    Require ($null -ne $method) 'canonicalizer_method_missing'
    $raw = [IO.File]::ReadAllText($path, [Text.UTF8Encoding]::new($false, $true))
    $result = $method.Invoke($null, @($raw, 'marcus-awake/p3d-a0-redaction-inputs/v1', $null, 'p3d-a0-redaction-source'))
    Require ([bool]$result.IsSuccess) 'redaction_source_canonicalization_failed'
    return [string]$result.Value.CanonicalJson
}

function Count-Forbidden([string]$text, [string[]]$tokens) {
    if ([string]::IsNullOrEmpty($text)) { return 0 }
    $normalized = $text.ToLowerInvariant()
    $count = 0
    foreach ($token in $tokens) {
        $offset = 0
        while ($offset -lt $normalized.Length) {
            $index = $normalized.IndexOf($token, $offset, [StringComparison]::Ordinal)
            if ($index -lt 0) { break }
            $end = $index + $token.Length
            $before = $index -eq 0 -or $normalized[$index - 1] -notmatch '[a-z0-9_]'
            $after = $end -eq $normalized.Length -or $normalized[$end] -notmatch '[a-z0-9_]'
            if ($before -and $after) { $count++ }
            $offset = $end
        }
    }
    return $count
}

New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
try {
    $builds = @(
        [pscustomobject]@{ Name = 'transport'; Project = Join-Path $frameworkRoot 'MarcusAwakeTransport\MarcusAwakeTransport.csproj' },
        [pscustomobject]@{ Name = 'framework'; Project = Join-Path $frameworkRoot 'MarcusAwakeFramework\MarcusAwakeFramework.csproj' },
        [pscustomobject]@{ Name = 'runtime-service'; Project = Join-Path $frameworkRoot 'MarcusAwakeRuntimeService\MarcusAwakeRuntimeService.csproj' },
        [pscustomobject]@{ Name = 'transport-tests'; Project = Join-Path $frameworkRoot 'MarcusAwakeTransport\tests\MarcusAwakeTransport.Tests.csproj' },
        [pscustomobject]@{ Name = 'framework-tests'; Project = Join-Path $frameworkRoot 'MarcusAwakeFramework\tests\MarcusAwakeFramework.Tests.csproj' },
        [pscustomobject]@{ Name = 'runtime-service-tests'; Project = $serviceTestProject },
        [pscustomobject]@{ Name = 'provider-tests'; Project = Join-Path $frameworkRoot 'MarcusAwakeProvider\tests\MarcusAwakeProvider.Tests.csproj' }
    )
    foreach ($build in $builds) {
        $result = Start-Captured ('build-' + $build.Name) 'dotnet' @('build', ('"' + $build.Project + '"'), '-c', 'Release', '--no-restore') $root
        Assert-Build $result
        Write-Output ('PASS BUILD ' + $build.Name)
    }

    $transportIds = @('P3D-A0-T01-versioned_and_capability_matrix','P3D-A0-T02-output_schema_matrix','P3D-A0-T03-header_only_opaque_payload','P3D-A0-T04-sequence_retry_non_contaminating','P3D-A0-T05-stream_usage_closed_set','P3D-A0-T06-stream_error_category_closed_set')
    $coreIds = @('P3D-A0-C01-structured_json_bounds','P3D-A0-C02-event_structured_result_and_route_metadata','P3D-A0-C03-handle_terminal_dispose_cancel','P3D-A0-C04-deferred_settlement_mapping','P3D-A0-C05-unknown_error_fail_closed','P3D-A0-C06-task_scope_semantic_identity')
    $p3bIds = @('P3B-01 service_start_descriptor_pipe','P3B-03 legacy_pipe_rejected','P3B-03 wrong_protocol_rejected','P3B-03 wrong_service_instance_rejected','P3B-03 wrong_sid_rejected','P3B-03 wrong_parent_rejected','P3B-03 wrong_nonce_rejected','P3B-02 valid_handshake_and_health','P3B-04 echo_response','P3B-05 checksum_and_length_rejected','P3B-05 duplicate_and_deep_json_rejected','P3B-05 oversized_frame_rejected','P3B-06 duplicate_and_conflicting_replay','P3B-06 sequence_gap_rejected','P3B-08 cancellation_and_terminal_replay','P3B-03 replayed_challenge_rejected','P3B-07 reconnect_new_epoch','P3B-08 disconnect_does_not_orphan','P3B-09 parent_exit_orphan_cleanup')
    $serviceIds = @('P3D-A0-S01-capability_before_payload_parse','P3D-A0-S02-task_scope_output_schema_gate','P3D-A0-S03-causation_and_error_precedence','P3D-A0-S04-contract_only_error_shape','P3D-A0-S05-deadline_preserved','P3D-A0-S06-deferred_vs_settlement','P3D-A0-S07-redaction_no_payload_echo','P3D-A0-S08-p3b_p3c_regression')
    $providerIds = @('provider_invalid_request','provider_authentication','provider_forbidden','provider_not_found','provider_conflict','provider_rate_limited','provider_timeout','provider_unavailable','provider_server_unavailable','provider_transport_unavailable','provider_redirect_rejected','provider_policy_denied','provider_malformed_response','provider_incomplete_stream','provider_cancelled','provider_unsupported','provider_resource_exhausted','provider_corrupt_credential','provider_internal_failure')

    $transportRun = Start-Captured 'transport-contract' $transportTestExe @('--p3d-a0-contract') (Split-Path -Parent $transportTestExe)
    Assert-Runner $transportRun $transportIds 6
    $coreRun = Start-Captured 'framework-core' $coreTestExe @('--p3d-a0-core') (Split-Path -Parent $coreTestExe)
    Assert-Runner $coreRun $coreIds 6
    $serviceBaselineRun = Start-Captured 'service-baseline' $harnessExe @() (Split-Path -Parent $harnessExe)
    Assert-Runner $serviceBaselineRun $p3bIds 19
    $serviceStorageRun = Start-Captured 'service-storage' $harnessExe @('--p3c') (Split-Path -Parent $harnessExe)
    Require ($serviceStorageRun.ExitCode -eq 0) ('service_storage_runner_failed:' + $serviceStorageRun.ExitCode)
    Require ((Get-Lines $serviceStorageRun.Stdout) -contains 'PASS_COUNT=7') 'service_storage_pass_count_invalid'
    Require ((Get-Lines $serviceStorageRun.Stdout) -contains 'FAIL_COUNT=0') 'service_storage_fail_count_invalid'
    $providerRun = Start-Captured 'provider-contract' $providerTestExe @('--p3d-a0-provider') (Split-Path -Parent $providerTestExe)
    Assert-ExactRunner $providerRun @('PASS_COUNT=1','FAIL_COUNT=0','HTTP_REQUEST_COUNT=0','EXTERNAL_NETWORK=false')
    $a0Run = Start-Captured 'service-a0' $harnessExe @('--p3d-a0') (Split-Path -Parent $harnessExe)
    Require ($a0Run.ExitCode -eq 0) ('a0_runner_failed:' + $a0Run.ExitCode)
    Require ((Get-Lines $a0Run.Stdout) -contains 'PASS_COUNT=1') 'a0_pass_count_invalid'
    Require ((Get-Lines $a0Run.Stdout) -contains 'FAIL_COUNT=0') 'a0_fail_count_invalid'

    $evidence = Read-Json $evidencePath
    Require ([string]$evidence.schema -eq 'marcus-awake.p3d-a0-evidence.v1') 'evidence_schema_invalid'
    Require (Test-Path -LiteralPath $schemaPath) 'evidence_schema_file_missing'
    $schemaDocument = Read-Json $schemaPath
    Require ([string]$schemaDocument.'$id' -eq 'marcus-awake/p3d-a0-evidence.v1') 'evidence_schema_id_invalid'
    Require ([int]$evidence.plan_revision -eq 23) 'evidence_plan_revision_invalid'
    Assert-CaseSet $evidence.runners.transport $transportIds 'transport'
    Assert-CaseSet $evidence.runners.core $coreIds 'core'
    Assert-CaseSet $evidence.runners.service $serviceIds 'service'
    $providerCase = Get-Case $evidence.runners.provider 'P3D-A0-P01-provider_error_mapping_19'
    Require ([int]$evidence.runners.provider.case_count -eq 1) 'provider_case_count_invalid'
    Require ([bool]$providerCase.passed) 'provider_case_failed'
    Assert-SubcaseSet $providerCase $providerIds 'provider'
    foreach ($subcase in @($providerCase.subcases)) {
        $mapping = $subcase.mapping
        $name = [string]$mapping.provider_category
        $wire = [string]$mapping.wire_category
        $core = [string]$mapping.core_category
        $retry = [bool]$mapping.core_retryable
        $fallback = [bool]$mapping.fallback_allowed
        $expectedCore = switch ($name) {
            'InvalidRequest' { 'InvalidRequest' }
            'Authentication' { 'Denied' }
            'Forbidden' { 'Denied' }
            'NotFound' { 'NotFound' }
            'Conflict' { 'Conflict' }
            'RateLimited' { 'RateLimited' }
            'Timeout' { 'Timeout' }
            'Unavailable' { 'Unavailable' }
            'ServerUnavailable' { 'Unavailable' }
            'TransportUnavailable' { 'Unavailable' }
            'RedirectRejected' { 'Denied' }
            'PolicyDenied' { 'Denied' }
            'MalformedResponse' { 'ProviderFailure' }
            'IncompleteStream' { 'ProviderFailure' }
            'Cancelled' { 'Cancelled' }
            'Unsupported' { 'Unsupported' }
            'ResourceExhausted' { 'ResourceExhausted' }
            'CorruptCredential' { 'RecoveryRequired' }
            'InternalFailure' { 'InternalFailure' }
            default { throw ('unknown_provider_category:' + $name) }
        }
        $expectedRetry = $name -in @('RateLimited','Unavailable','ServerUnavailable','TransportUnavailable')
        $expectedFallback = $name -in @('RateLimited','ServerUnavailable','TransportUnavailable')
        $expectedWire = Convert-ToWire $name
        Assert-Mapping $mapping $name $expectedWire $expectedCore $expectedRetry $expectedFallback
    }
    Require ([int]$evidence.runners.provider.exit_code -eq 0) 'provider_evidence_exit_invalid'
    Require ([string]$evidence.runners.provider.executable -like '*MarcusAwakeProvider.Tests.exe') 'provider_executable_invalid'
    Require ((@($evidence.runners.provider.arguments) -join '|') -eq '--p3d-a0-provider') 'provider_arguments_invalid'
    Require ([int]$evidence.runners.provider.http_request_count -eq 0) 'provider_http_count_invalid'
    Require (-not [bool]$evidence.runners.provider.external_network) 'provider_network_flag_invalid'
    Require ([bool]$evidence.runners.provider.stderr_empty) 'provider_stderr_not_empty'

    Require ([int]$evidence.runners.service.case_count -eq 8) 'service_evidence_case_count_invalid'
    Require ([int]$evidence.runners.service.service_exit_code -eq 0) 'service_exit_invalid'
    Require ([int]$evidence.runners.service.runner_exit_code -eq 0) 'service_runner_exit_invalid'
    Require ([int64]$evidence.runners.service.deadline_expected_unix_ms -eq 4102444800000) 'deadline_expected_invalid'
    Require ([int64]$evidence.runners.service.deadline_observed_unix_ms -eq 4102444800000) 'deadline_observed_invalid'
    Require ([bool]$evidence.runners.service.deadline_preserved) 'deadline_not_preserved'
    Require ([string]$evidence.runners.service.runner_executable -like '*MarcusAwakeRuntimeService.Tests.exe') 'service_runner_executable_invalid'
    Require ((@($evidence.runners.service.runner_arguments) -join '|') -eq '--p3d-a0') 'service_runner_arguments_invalid'
    Require ((@($evidence.runners.service.service_arguments).Count) -eq 0) 'service_arguments_invalid'

    Require ([int]$evidence.regressions.p3b.top_level_case_count -eq 19) 'regression_p3b_case_count_invalid'
    Require ([int]$evidence.regressions.p3b.fine_grained_case_count -eq 24) 'regression_p3b_fine_count_invalid'
    Require ([bool]$evidence.regressions.p3b.passed) 'regression_p3b_failed'
    Require ([int]$evidence.regressions.p3c.case_count -eq 7) 'regression_p3c_case_count_invalid'
    Require ([bool]$evidence.regressions.p3c.passed) 'regression_p3c_failed'

    $actualHashes = [ordered]@{
        transport_sha256 = Get-Hash $transportDll
        core_sha256 = Get-Hash $coreDll
        service_sha256 = Get-Hash $serviceExe
        harness_sha256 = Get-Hash $harnessExe
    }
    foreach ($key in $actualHashes.Keys) { Require ([string]$evidence.artifacts.$key -eq $actualHashes[$key]) ('artifact_hash_mismatch:' + $key) }

    $redactionInput = Read-Json $redactionInputPath
    Require ([string]$redactionInput.schema -eq 'marcus-awake.p3d-a0-redaction-inputs.v1') 'redaction_input_schema_invalid'
    $canonical = Get-CanonicalJson $redactionInputPath
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $sourceHash = [BitConverter]::ToString($sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
    Require ($sourceHash -eq 'b37ca2ae7b4ecb2425e1a1ebc80c93a5beae470afbdb107a0d9ada4fa388d2e8') 'redaction_source_hash_invalid'
    $tokens = @($redactionInput.forbidden_tokens)
    Require ((@($evidence.redaction.forbidden_tokens) -join '|') -eq ($tokens -join '|')) 'redaction_token_policy_changed'
    Require ([string]$evidence.redaction.scan_policy_id -eq 'marcus-awake.p3d-a0-redaction-policy.v1') 'redaction_policy_invalid'
    Require ([string]$evidence.redaction.canonicalizer_id -eq [string]$redactionInput.canonicalizer) 'redaction_canonicalizer_invalid'
    foreach ($property in @('service_stderr','response_envelope','deferred_payload','evidence_json')) {
        $scan = $evidence.redaction.scans.$property
        Require ([bool]$scan.scan_passed) ('redaction_scan_failed:' + $property)
        Require ([int]$scan.forbidden_match_count -eq 0) ('redaction_match_count:' + $property)
        $artifactPath = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $evidencePath) ([string]$scan.relative_path)))
        $actualHash = Get-Hash $artifactPath
        Require ([string]$scan.sha256 -eq $actualHash) ('redaction_hash_mismatch:' + $property)
        Require ([string]$scan.capture_binding.artifact_sha256 -eq $actualHash) ('redaction_capture_hash_mismatch:' + $property)
        Require ([string]$scan.input_binding.source_sha256 -eq $sourceHash) ('redaction_source_binding_mismatch:' + $property)
        Require ([string]$scan.capture_binding.run_id -eq [string]$evidence.runners.service.run_id) ('redaction_run_binding_mismatch:' + $property)
        Require ([string]$scan.capture_binding.executable -eq [string]$evidence.runners.service.service_executable) ('redaction_service_binding_mismatch:' + $property)
        Require ((@($scan.capture_binding.arguments).Count) -eq 0) ('redaction_arguments_invalid:' + $property)
        $text = [IO.File]::ReadAllText($artifactPath, [Text.UTF8Encoding]::new($false, $true))
        Require ((Count-Forbidden $text $tokens) -eq 0) ('redaction_forbidden_token_found:' + $property)
    }
    Require ([bool]$evidence.redaction.payloads_redacted) 'payload_redaction_false'
    Require ([bool]$evidence.redaction.credentials_redacted) 'credential_redaction_false'
    Require ([bool]$evidence.redaction.diagnostics_redacted) 'diagnostic_redaction_false'
    Require ([bool]$evidence.redaction.stack_traces_redacted) 'stack_redaction_false'
    Require ([bool]$evidence.redaction.passed) 'redaction_summary_failed'

    $allCases = @($evidence.runners.transport.cases + $evidence.runners.core.cases + $evidence.runners.provider.cases + $evidence.runners.service.cases)
    $recomputed = $allCases.Count -gt 0 -and @($allCases | Where-Object { -not [bool]$_.passed }).Count -eq 0 -and [bool]$evidence.regressions.p3b.passed -and [bool]$evidence.regressions.p3c.passed -and [bool]$evidence.redaction.passed
    Require ([bool]$evidence.all_cases_passed -eq $recomputed) 'evidence_aggregate_mismatch'
    Require ([bool]$evidence.all_cases_passed) 'evidence_not_passed'
    Write-Output 'P3D-A0 VERIFIER PASS'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
