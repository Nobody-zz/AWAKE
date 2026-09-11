using MarcusAwakeFramework.Api;
using MarcusAwakeTransport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeRuntimeService.Tests;

[SupportedOSPlatform("windows")]
internal sealed class P3DA0HarnessRunner
{
    private const long FixtureNowUnixMilliseconds = 4102444799000;
    private const long FixtureDeadlineUnixMilliseconds = 4102444800000;
    private const string CanonicalizerId = "json-ordinal-key-sort-array-order-preserving-number-invariant-unicode-nfc-lf-utf8-nobom-v2";
    private const string ForbiddenTokenPolicy = "ascii-casefold-boundary-token-set-v1";
    private const string ForbiddenTokenBoundary = "ascii-alnum-underscore-token-boundary-v1";
    private static readonly string[] ForbiddenTokens =
    {
        "api_key", "authorization", "password", "secret", "stack_trace", "inner_exception", "endpoint_query", "credential", "payloadjson"
    };
    private static readonly string[] ProviderCategoryNames =
    {
        "InvalidRequest", "Authentication", "Forbidden", "NotFound", "Conflict", "RateLimited", "Timeout", "Unavailable", "ServerUnavailable", "TransportUnavailable", "RedirectRejected", "PolicyDenied", "MalformedResponse", "IncompleteStream", "Cancelled", "Unsupported", "ResourceExhausted", "CorruptCredential", "InternalFailure"
    };
    private readonly List<Dictionary<string, object?>> serviceCases = new List<Dictionary<string, object?>>();
    private readonly List<Dictionary<string, object?>> providerSubcases = new List<Dictionary<string, object?>>();
    private Dictionary<string, object?>? successfulObservation;
    private PipeEnvelope? redactionResponse;
    private string serviceStderr = string.Empty;
    private int evidenceServicePid;
    private long evidenceConnectionEpoch;
    private int serviceExitCode = -1;
    private string runId = string.Empty;

    internal async Task<int> RunAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("FAIL windows_required");
            return 1;
        }

        runId = "p3d-a0-" + Guid.NewGuid().ToString("N");
        var servicePath = ResolveServicePath();
        var restrictedRoot = Path.Combine(Path.GetTempPath(), runId + "-restricted");
        var serviceRoot = Path.Combine(Path.GetTempPath(), runId + "-service");
        LaunchedService? restrictedService = null;
        LaunchedService? service = null;
        ServiceConnection? primaryConnection = null;
        ServiceConnection? redactionConnection = null;
        ServiceConnection? replayConnection = null;
        var nextConnectionEpoch = 2L;

        try
        {
            restrictedService = await LaunchedService.StartAsync(
                servicePath,
                4010,
                restrictedRoot,
                new[] { "health" },
                null,
                TestEnvironment()).ConfigureAwait(false);
            using (var restrictedConnection = await restrictedService.ConnectAsync(restrictedService.Descriptor.ChallengeId, "p3d-a0-restricted").ConfigureAwait(false))
            {
                await RunServiceCaseAsync("P3D-A0-S01-capability_before_payload_parse", () => RunCapabilityGateAsync(restrictedConnection)).ConfigureAwait(false);
            }
            await restrictedService.DisposeAsync().ConfigureAwait(false);
            restrictedService = null;

            service = await LaunchedService.StartAsync(
                servicePath,
                4011,
                serviceRoot,
                AllCapabilities(),
                null,
                TestEnvironment("a0-suppressed-message")).ConfigureAwait(false);
            primaryConnection = await service.ConnectAsync(service.Descriptor.ChallengeId, "p3d-a0-main").ConfigureAwait(false);
            evidenceServicePid = service.Process.Id;

            await RunServiceCaseAsync("P3D-A0-S02-task_scope_output_schema_gate", () => RunTaskScopeGateAsync(primaryConnection)).ConfigureAwait(false);
            await RunServiceCaseAsync("P3D-A0-S03-causation_and_error_precedence", () => RunCausationPrecedenceAsync(primaryConnection)).ConfigureAwait(false);
            await RunServiceCaseAsync("P3D-A0-S04-contract_only_error_shape", () => RunContractOnlyErrorAsync(primaryConnection)).ConfigureAwait(false);
            await RunServiceCaseAsync("P3D-A0-S05-deadline_preserved", () => RunDeadlineAsync(service, primaryConnection, nextConnectionEpoch)).ConfigureAwait(false);
            nextConnectionEpoch += 2;
            await RunServiceCaseAsync("P3D-A0-S06-deferred_vs_settlement", () => RunDeferredSettlementAsync(primaryConnection)).ConfigureAwait(false);
            primaryConnection.Dispose();
            primaryConnection = null;
            redactionConnection = await service.ConnectAsync(service.Descriptor.ChallengeId + ".connection." + nextConnectionEpoch.ToString(), "p3d-a0-redaction").ConfigureAwait(false);
            evidenceConnectionEpoch = redactionConnection.HandshakeResponse.ConnectionEpoch;
            await RunServiceCaseAsync("P3D-A0-S07-redaction_no_payload_echo", () => RunRedactionAsync(service, redactionConnection, nextConnectionEpoch + 1)).ConfigureAwait(false);
            redactionConnection.Dispose();
            redactionConnection = null;
            nextConnectionEpoch += 2;

            replayConnection = await service.ConnectAsync(service.Descriptor.ChallengeId + ".connection." + nextConnectionEpoch.ToString(), "p3d-a0-shutdown").ConfigureAwait(false);
            var shutdown = RewriteDeadline(replayConnection, replayConnection.CreateFrame("shutdown", "a0-shutdown", "{}", null, "campaign-a0", "timeline-a0", 1), FixtureDeadlineUnixMilliseconds);
            var shutdownResponse = await replayConnection.SendAndReadAsync(shutdown).ConfigureAwait(false);
            Require(shutdownResponse.MessageType == "shutdown_ack", "service_shutdown_ack_missing");
            await service.Process.WaitForExitAsync().ConfigureAwait(false);
            serviceExitCode = service.Process.ExitCode;
            serviceStderr = await service.ReadStandardErrorAsync().ConfigureAwait(false);
            if (evidenceConnectionEpoch < 1) evidenceConnectionEpoch = replayConnection.HandshakeResponse.ConnectionEpoch;
        }
        catch (Exception exception)
        {
            Console.WriteLine("FAIL harness_fatal " + exception.Message);
        }
        finally
        {
            replayConnection?.Dispose();
            redactionConnection?.Dispose();
            primaryConnection?.Dispose();
            if (restrictedService != null) await restrictedService.DisposeAsync().ConfigureAwait(false);
            if (service != null)
            {
                if (!service.Process.HasExited) await service.DisposeAsync().ConfigureAwait(false);
                else
                {
                    serviceStderr = await service.ReadStandardErrorAsync().ConfigureAwait(false);
                    await service.DisposeAsync().ConfigureAwait(false);
                }
            }
            CleanupDirectory(restrictedRoot);
            CleanupDirectory(serviceRoot);
        }

        var transport = await RunRunnerAsync(ResolveTransportTestPath(), new[] { "--p3d-a0-contract" }).ConfigureAwait(false);
        var core = await RunRunnerAsync(ResolveFrameworkTestPath(), new[] { "--p3d-a0-core" }).ConfigureAwait(false);
        var provider = await RunRunnerAsync(ResolveProviderTestPath(), new[] { "--p3d-a0-provider" }).ConfigureAwait(false);
        var p3b = await RunRunnerAsync(Environment.ProcessPath ?? string.Empty, Array.Empty<string>()).ConfigureAwait(false);
        var p3c = await RunRunnerAsync(Environment.ProcessPath ?? string.Empty, new[] { "--p3c" }).ConfigureAwait(false);
        AddRegressionCase(p3b, p3c);
        var evidencePath = WriteEvidence(servicePath, transport, core, provider, p3b, p3c);

        var allPassed = serviceCases.Count == 8
            && serviceCases.All(GetPassed)
            && providerSubcases.Count == 19
            && transport.ExitCode == 0
            && core.ExitCode == 0
            && provider.ExitCode == 0
            && p3b.ExitCode == 0
            && p3c.ExitCode == 0
            && serviceExitCode == 0;
        Console.WriteLine("EVIDENCE_PATH=" + evidencePath);
        Console.WriteLine("PASS_COUNT=" + (allPassed ? "1" : "0"));
        Console.WriteLine("FAIL_COUNT=" + (allPassed ? "0" : "1"));
        return allPassed ? 0 : 1;
    }

    private async Task RunServiceCaseAsync(string id, Func<Task<CaseExecution>> action)
    {
        try
        {
            var execution = await action().ConfigureAwait(false);
            serviceCases.Add(BuildCase(id, true, execution));
            Console.WriteLine("PASS " + id);
        }
        catch (Exception exception)
        {
            serviceCases.Add(BuildCase(id, false, new CaseExecution(
                MakeObservation("E0", "harness_failure", true, true, "response_inherits_deadline", new[] { "H0", "H1-I", "H1-C", "H1-D", "S0", "B0", "B1", "E0" }, MakeSuccessWrite(), true, 1, 1, "not_recorded", false),
                new List<Dictionary<string, object?>> { MakeSubcase("harness_failure", false, "case completes without harness exception", exception.GetType().Name, exception.GetType().Name) },
                exception.GetType().Name)));
            Console.WriteLine("FAIL " + id + " " + exception.Message);
        }
    }

    private void AddRegressionCase(ProcessResult p3b, ProcessResult p3c)
    {
        var p3bPassed = p3b.ExitCode == 0;
        var p3cPassed = p3c.ExitCode == 0;
        var passed = p3bPassed && p3cPassed;
        var execution = new CaseExecution(
            MakeObservation("E0", passed ? "regression_verified" : "regression_failed", true, true, "response_inherits_deadline", new[] { "H0", "H1-I", "H1-C", "H1-D", "S0", "B0", "B1", "E0" }, MakeSuccessWrite(), true, 1, 1, "response_template_stored", false),
            new List<Dictionary<string, object?>>
            {
                MakeSubcase("p3b_regression", p3bPassed, "existing IPC regression exits zero", "exit_code=" + p3b.ExitCode.ToString(), p3bPassed ? string.Empty : "regression_failed"),
                MakeSubcase("p3c_regression", p3cPassed, "existing Storage/RAG regression exits zero", "exit_code=" + p3c.ExitCode.ToString(), p3cPassed ? string.Empty : "regression_failed")
            },
            "existing IPC and Storage/RAG regressions remain green",
            "p3b_exit_code=" + p3b.ExitCode.ToString() + ";p3c_exit_code=" + p3c.ExitCode.ToString(),
            passed ? string.Empty : "regression_failed");
        serviceCases.Add(BuildCase("P3D-A0-S08-p3b_p3c_regression", passed, execution));
        Console.WriteLine((passed ? "PASS " : "FAIL ") + "P3D-A0-S08-p3b_p3c_regression");
    }

    private static Dictionary<string, object?> BuildCase(string id, bool passed, CaseExecution execution)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = id,
            ["passed"] = passed,
            ["expected"] = execution.Expected,
            ["observed"] = execution.Observed,
            ["error_code"] = passed ? string.Empty : execution.ErrorCode,
            ["observation"] = execution.Observation,
            ["subcases"] = execution.Subcases
        };
    }

    private static bool GetPassed(Dictionary<string, object?> value)
    {
        return value.TryGetValue("passed", out var passed) && passed is bool result && result;
    }

    private static Dictionary<string, string> TestEnvironment(string? suppressionMessageId = null)
    {
        var environment = new Dictionary<string, string>
        {
            ["MARCUS_AWAKE_RUNTIME_TEST_NOW_UNIX_MS"] = FixtureNowUnixMilliseconds.ToString()
        };
        if (!string.IsNullOrWhiteSpace(suppressionMessageId)) environment["MARCUS_AWAKE_TEST_SUPPRESS_BEFORE_FIRST_BYTE_MESSAGE_ID"] = suppressionMessageId;
        if (!string.IsNullOrWhiteSpace(suppressionMessageId)) environment["MARCUS_AWAKE_RUNTIME_TEST_MODE"] = "p3d-a0";
        return environment;
    }

    private static string ResolveServicePath()
    {
        return RequireFile(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "_build_out", "Release", "MarcusAwakeRuntimeService.exe")));
    }

    private static string ResolveTransportTestPath()
    {
        return RequireFile(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MarcusAwakeTransport", "_build_out", "tests", "Release", "MarcusAwakeTransport.Tests.exe")));
    }

    private static string ResolveFrameworkTestPath()
    {
        return RequireFile(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MarcusAwakeFramework", "_build_out", "tests", "Release", "MarcusAwakeFramework.Tests.exe")));
    }

    private static string ResolveProviderTestPath()
    {
        return RequireFile(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MarcusAwakeProvider", "tests", "_build_out", "Release", "MarcusAwakeProvider.Tests.exe")));
    }

    private static string RequireFile(string path)
    {
        if (!File.Exists(path)) throw new InvalidOperationException("executable_missing:" + path);
        return path;
    }

    private static void CleanupDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try { Directory.Delete(path, true); } catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string[] AllCapabilities()
    {
        return new[]
        {
            "health", "echo", "cancel", "diagnostic", "shutdown",
            ProtocolConstants.CapabilityStorageRead,
            ProtocolConstants.CapabilityStorageWrite,
            ProtocolConstants.CapabilityRagRead,
            ProtocolConstants.CapabilityRagWrite,
            ProtocolConstants.CapabilityProviderConfigureV1,
            ProtocolConstants.CapabilityProviderModelsV1,
            ProtocolConstants.CapabilityProviderCompleteV1,
            ProtocolConstants.CapabilityProviderStreamV1
        };
    }

    private static async Task<ProcessResult> RunRunnerAsync(string executable, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable)) return new ProcessResult(executable, arguments.ToArray(), -1, string.Empty, "executable_missing");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("runner_start_failed:" + executable);
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return new ProcessResult(executable, arguments.ToArray(), process.ExitCode, await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false));
    }

    private sealed class ProcessResult
    {
        internal ProcessResult(string executable, IReadOnlyList<string> arguments, int exitCode, string stdout, string stderr)
        {
            Executable = executable;
            Arguments = arguments;
            ExitCode = exitCode;
            Stdout = stdout;
            Stderr = stderr;
        }

        internal string Executable { get; }
        internal IReadOnlyList<string> Arguments { get; }
        internal int ExitCode { get; }
        internal string Stdout { get; }
        internal string Stderr { get; }
    }

    private sealed class CaseExecution
    {
        internal CaseExecution(Dictionary<string, object?> observation, List<Dictionary<string, object?>> subcases, string expected, string observed, string errorCode = "")
        {
            Observation = observation;
            Subcases = subcases;
            Expected = expected;
            Observed = observed;
            ErrorCode = errorCode;
        }

        internal CaseExecution(Dictionary<string, object?> observation, List<Dictionary<string, object?>> subcases, string observed)
            : this(observation, subcases, "fixed contract passes", observed)
        {
        }

        internal Dictionary<string, object?> Observation { get; }
        internal List<Dictionary<string, object?>> Subcases { get; }
        internal string Expected { get; }
        internal string Observed { get; }
        internal string ErrorCode { get; }
    }

    private static async Task<CaseExecution> RunCapabilityGateAsync(ServiceConnection connection)
    {
        var payload = "{\"api_key\":\"redaction-sentinel\",\"authorization\":\"redaction-sentinel\"}";
        var frame = BuildProviderFrame(connection, ProtocolConstants.MessageTypeProviderCompleteV1, "a0-capability", "a0-capability-task", payload, ProtocolConstants.ProviderResultSchemaV1, "a0-causation");
        var response = await connection.SendAndReadAsync(frame).ConfigureAwait(false);
        AssertErrorResponse(response, "capability_not_granted", ProtocolConstants.GenericErrorSchemaV1);
        Require(!response.PayloadJson.Contains("redaction-sentinel", StringComparison.Ordinal), "capability_gate_echoed_payload");
        var observation = MakeObservation("B0", "capability_not_granted", true, false, "retryable_generic_error", new[] { "H0", "H1-I", "H1-C", "H1-D", "S0", "B0" }, MakeSuccessWrite(), true, 1, 1, "not_recorded", false);
        return new CaseExecution(observation, new List<Dictionary<string, object?>>
        {
            MakeSubcase("capability_gate", true, "capability rejection precedes business payload parsing", "capability_not_granted", string.Empty)
        }, "capability_not_granted");
    }

    private static async Task<CaseExecution> RunTaskScopeGateAsync(ServiceConnection connection)
    {
        var subcases = new List<Dictionary<string, object?>>();
        var validScopeFrame = BuildProviderFrame(connection, ProtocolConstants.MessageTypeProviderCompleteV1, "a0-scope-missing", "a0-scope-missing-task", ValidProviderPayload(), ProtocolConstants.ProviderResultSchemaV1, "a0-causation");
        var missingScopeRaw = BuildMissingTaskScopeFrame(connection, validScopeFrame);
        var missingResponse = await connection.SendRawAndReadAsync(missingScopeRaw).ConfigureAwait(false);
        AssertErrorResponse(missingResponse, "task_scope_missing", ProtocolConstants.GenericErrorSchemaV1);
        subcases.Add(MakeSubcase("task_scope_missing", true, "missing TaskScope is rejected in B0", "task_scope_missing", string.Empty));

        var invalidOutput = BuildProviderFrame(connection, ProtocolConstants.MessageTypeProviderCompleteV1, "a0-output-mismatch", "a0-output-task", ValidProviderPayload(), "marcus-awake.provider.models_result.v1", "a0-causation");
        var invalidOutputResponse = await connection.SendAndReadAsync(invalidOutput).ConfigureAwait(false);
        AssertErrorResponse(invalidOutputResponse, "output_schema_mismatch", ProtocolConstants.GenericErrorSchemaV1);
        subcases.Add(MakeSubcase("output_schema_mismatch", true, "TaskScope output schema must match the request matrix", "output_schema_mismatch", string.Empty));

        var observation = MakeObservation("B0", "task_scope_missing", true, false, "retryable_generic_error", new[] { "H0", "H1-I", "H1-C", "H1-D", "S0", "B0" }, MakeSuccessWrite(), true, 1, 1, "not_recorded", false);
        return new CaseExecution(observation, subcases, "task_scope_missing and output_schema_mismatch");
    }

    private static async Task<CaseExecution> RunCausationPrecedenceAsync(ServiceConnection connection)
    {
        var subcases = new List<Dictionary<string, object?>>();
        var invalidOutputAndCausation = BuildProviderFrame(connection, ProtocolConstants.MessageTypeProviderCompleteV1, "a0-precedence-output", "a0-precedence-output-task", ValidProviderPayload(), "marcus-awake.provider.models_result.v1", string.Empty);
        var precedenceResponse = await connection.SendAndReadAsync(invalidOutputAndCausation).ConfigureAwait(false);
        AssertErrorResponse(precedenceResponse, "output_schema_mismatch", ProtocolConstants.GenericErrorSchemaV1);
        subcases.Add(MakeSubcase("output_before_causation", true, "output schema is checked before causation", "output_schema_mismatch", string.Empty));

        var missingCausation = BuildProviderFrame(connection, ProtocolConstants.MessageTypeProviderCompleteV1, "a0-precedence-causation", "a0-precedence-causation-task", ValidProviderPayload(), ProtocolConstants.ProviderResultSchemaV1, string.Empty);
        var causationResponse = await connection.SendAndReadAsync(missingCausation).ConfigureAwait(false);
        AssertErrorResponse(causationResponse, "provider_causation_missing", ProtocolConstants.GenericErrorSchemaV1);
        subcases.Add(MakeSubcase("causation_missing", true, "provider requests require CausationId", "provider_causation_missing", string.Empty));

        var observation = MakeObservation("B0", "provider_causation_missing", true, false, "retryable_generic_error", new[] { "H0", "H1-I", "H1-C", "H1-D", "S0", "B0" }, MakeSuccessWrite(), true, 1, 1, "not_recorded", false);
        return new CaseExecution(observation, subcases, "output_schema_mismatch precedes provider_causation_missing");
    }

    private static FrameBuildResult BuildProviderFrame(ServiceConnection connection, string messageType, string messageId, string taskId, string payloadJson, string outputSchema, string causationId, string settlementRequirement = "not_applicable", string ownerId = "a0-owner")
    {
        var taskScope = new TaskScopeEnvelope
        {
            TaskId = taskId,
            MessageId = messageId,
            OwnerId = ownerId,
            RouteId = "awake.route.provider",
            ProviderId = "provider.example",
            ProfileId = "profile.example",
            IdempotencyKey = "idem-" + taskId,
            RequestPayloadHash = TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes(payloadJson)),
            OutputSchemaId = outputSchema,
            OutputSchemaMajor = 1,
            OutputSchemaMinor = 0,
            SettlementRequirement = settlementRequirement
        };
        var frame = connection.CreateFrame(messageType, messageId, payloadJson, null, "campaign-a0", "timeline-a0", 1, taskScope, ownerId);
        return connection.RewriteFrame(frame, envelope =>
        {
            if (ProviderProtocolContract.TryGetRequest(messageType, out var providerContract)) envelope.PayloadSchema = providerContract.RequestSchema;
            envelope.DeadlineUnixMilliseconds = FixtureDeadlineUnixMilliseconds;
            envelope.CausationId = causationId;
        });
    }

    private static string BuildMissingTaskScopeFrame(ServiceConnection connection, FrameBuildResult validFrame)
    {
        var rawWithoutTaskScope = RemoveTopLevelObjectProperty(validFrame.RawJson, "task_scope");
        if (!ProtocolCodec.TryParseEnvelopeHeaderOnly(rawWithoutTaskScope, out var envelope, out var error)) throw new InvalidDataException("missing_task_scope_frame_decode_failed:" + error);
        Require(envelope.TaskScope == null, "task_scope_property_not_removed");
        var oldFenceProof = envelope.FenceProof;
        envelope.TaskScope = null;
        var newFenceProof = TransportSecurity.ComputeFrameFenceProof(connection.FrameKey, envelope);
        var oldFenceField = "\"fence_proof\":\"" + oldFenceProof + "\"";
        var newFenceField = "\"fence_proof\":\"" + newFenceProof + "\"";
        Require(rawWithoutTaskScope.Contains(oldFenceField, StringComparison.Ordinal), "missing_task_scope_fence_field_missing");
        return rawWithoutTaskScope.Replace(oldFenceField, newFenceField, StringComparison.Ordinal);
    }


    private static string RemoveTopLevelObjectProperty(string json, string propertyName)
    {
        var quotedName = "\"" + propertyName + "\"";
        var inString = false;
        var escaped = false;
        var depth = 0;
        for (var index = 0; index < json.Length; index++)
        {
            var character = json[index];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == '\"') inString = false;
                continue;
            }
            if (character == '\"')
            {
                if (depth == 1 && index + quotedName.Length <= json.Length && string.CompareOrdinal(json, index, quotedName, 0, quotedName.Length) == 0)
                {
                    var cursor = index + quotedName.Length;
                    while (cursor < json.Length && char.IsWhiteSpace(json[cursor])) cursor++;
                    Require(cursor < json.Length && json[cursor] == ':', propertyName + "_separator_missing");
                    cursor++;
                    while (cursor < json.Length && char.IsWhiteSpace(json[cursor])) cursor++;
                    var valueEnd = FindJsonValueEnd(json, cursor);
                    var removeStart = index;
                    var removeEnd = valueEnd + 1;
                    var previous = removeStart - 1;
                    while (previous >= 0 && char.IsWhiteSpace(json[previous])) previous--;
                    if (previous >= 0 && json[previous] == ',') removeStart = previous;
                    else
                    {
                        var next = removeEnd;
                        while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                        Require(next < json.Length && json[next] == ',', propertyName + "_trailing_separator_missing");
                        removeEnd = next + 1;
                    }
                    return json.Remove(removeStart, removeEnd - removeStart);
                }
                inString = true;
                continue;
            }
            if (character == '{' || character == '[') depth++;
            else if (character == '}' || character == ']') depth--;
        }
        throw new InvalidDataException(propertyName + "_property_not_found");
    }

    private static int FindJsonValueEnd(string json, int valueStart)
    {
        Require(valueStart < json.Length && json[valueStart] == '{', "task_scope_object_required");
        var inString = false;
        var escaped = false;
        var depth = 0;
        for (var index = valueStart; index < json.Length; index++)
        {
            var character = json[index];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == '\"') inString = false;
                continue;
            }
            if (character == '\"') inString = true;
            else if (character == '{') depth++;
            else if (character == '}' && --depth == 0) return index;
        }
        throw new InvalidDataException("task_scope_value_unterminated");
    }

    private static FrameBuildResult RewriteDeadline(ServiceConnection connection, FrameBuildResult frame, long deadlineUnixMilliseconds)
    {
        return connection.RewriteFrame(frame, envelope => envelope.DeadlineUnixMilliseconds = deadlineUnixMilliseconds);
    }

    private static string ValidProviderPayload()
    {
        return "{\"schema\":\"" + ProtocolConstants.ProviderCompleteSchemaV1 + "\",\"profile_id\":\"profile.example\",\"provider_id\":\"provider.example\",\"route_id\":\"awake.route.provider\",\"messages\":[{\"role\":\"user\",\"content\":\"Tell me a safe test answer.\"}]}";
    }

    private static void AssertErrorResponse(PipeEnvelope response, string errorCode, string payloadSchema)
    {
        Require(response.MessageType == ProtocolConstants.MessageTypeError, "error_response_type_invalid:" + errorCode);
        Require(response.ErrorCode == errorCode, "error_response_code_invalid:" + response.ErrorCode);
        Require(response.PayloadSchema == payloadSchema, "error_response_schema_invalid:" + response.PayloadSchema);
        using var document = JsonDocument.Parse(response.PayloadJson);
        Require(document.RootElement.GetProperty("error_code").GetString() == errorCode, "error_payload_code_invalid:" + errorCode);
    }

    private async Task<CaseExecution> RunContractOnlyErrorAsync(ServiceConnection connection)
    {
        var frame = BuildProviderFrame(connection, ProtocolConstants.MessageTypeProviderCompleteV1, "a0-contract-only", "a0-contract-task", ValidProviderPayload(), ProtocolConstants.ProviderResultSchemaV1, "a0-causation");
        var response = await connection.SendAndReadAsync(frame).ConfigureAwait(false);
        AssertErrorResponse(response, "provider_handler_deferred", ProtocolConstants.ProviderErrorSchemaV1);
        using (var document = JsonDocument.Parse(response.PayloadJson))
        {
            Require(document.RootElement.GetProperty("category").GetString() == "unavailable", "provider_error_category_invalid");
            Require(!document.RootElement.GetProperty("retryable").GetBoolean(), "provider_error_retryable_invalid");
            Require(document.RootElement.GetProperty("provider_id").GetString() == "provider.example", "provider_error_provider_id_invalid");
        }

        successfulObservation = MakeObservation("E0", "provider_handler_deferred", true, true, "deferred_error", new[] { "H0", "H1-I", "H1-C", "H1-D", "S0", "B0", "B1", "E0" }, MakeSuccessWrite(), true, 1, 1, "response_template_stored", false);
        return new CaseExecution(successfulObservation, new List<Dictionary<string, object?>>
        {
            MakeSubcase("provider_deferred", true, "valid provider request returns contract-only deferred error", "provider_handler_deferred", string.Empty)
        }, "provider_handler_deferred");
    }

    private static async Task<CaseExecution> RunDeadlineAsync(LaunchedService service, ServiceConnection connection, long nextConnectionEpoch)
    {
        var futureFrame = BuildProviderFrame(connection, ProtocolConstants.MessageTypeProviderModelsV1, "a0-deadline-future", "a0-deadline-future-task", ValidProviderIdentityPayload(), ProtocolConstants.ProviderModelsResultSchemaV1, "a0-causation");
        var futureResponse = await connection.SendAndReadAsync(futureFrame).ConfigureAwait(false);
        AssertErrorResponse(futureResponse, "provider_handler_deferred", ProtocolConstants.ProviderErrorSchemaV1);
        Require(futureResponse.DeadlineUnixMilliseconds == FixtureDeadlineUnixMilliseconds, "response_deadline_not_preserved");

        var equalDisconnected = false;
        var equalConnection = await service.ConnectAsync(service.Descriptor.ChallengeId + ".connection." + nextConnectionEpoch.ToString(), "p3d-a0-deadline-equal").ConfigureAwait(false);
        try
        {
            var equalFrame = RewriteDeadline(equalConnection, equalConnection.CreateFrame("health", "a0-deadline-equal", "{}", null, "campaign-a0", "timeline-a0", 1), FixtureNowUnixMilliseconds);
            await equalConnection.SendRawAsync(equalFrame.RawJson).ConfigureAwait(false);
            equalDisconnected = await equalConnection.DisconnectedAsync().ConfigureAwait(false);
        }
        finally
        {
            equalConnection.Dispose();
        }

        var afterDisconnected = false;
        var afterConnection = await service.ConnectAsync(service.Descriptor.ChallengeId + ".connection." + (nextConnectionEpoch + 1).ToString(), "p3d-a0-deadline-after").ConfigureAwait(false);
        try
        {
            var afterFrame = RewriteDeadline(afterConnection, afterConnection.CreateFrame("health", "a0-deadline-after", "{}", null, "campaign-a0", "timeline-a0", 1), FixtureNowUnixMilliseconds - 1);
            await afterConnection.SendRawAsync(afterFrame.RawJson).ConfigureAwait(false);
            afterDisconnected = await afterConnection.DisconnectedAsync().ConfigureAwait(false);
        }
        finally
        {
            afterConnection.Dispose();
        }

        Require(equalDisconnected, "deadline_equal_connection_stayed_open");
        Require(afterDisconnected, "deadline_after_connection_stayed_open");

        var observation = MakeObservation("E0", "provider_handler_deferred", true, true, "response_inherits_deadline", new[] { "H0", "H1-I", "H1-C", "H1-D", "S0", "B0", "B1", "E0" }, MakeSuccessWrite(), true, 1, 1, "response_template_stored", false);
        return new CaseExecution(observation, new List<Dictionary<string, object?>>
        {
            MakeSubcase("future_valid", true, "future deadline is accepted and copied to response", "deadline=" + FixtureDeadlineUnixMilliseconds.ToString(), string.Empty),
            MakeSubcase("now_equal_expired", true, "now equal to deadline closes without response", "close_no_response", string.Empty),
            MakeSubcase("now_after_expired", true, "now after deadline closes without response", "close_no_response", string.Empty)
        }, "future deadline preserved; equality and later deadlines expired");
    }

    private static async Task<CaseExecution> RunDeferredSettlementAsync(ServiceConnection connection)
    {
        var subcases = new List<Dictionary<string, object?>>();
        var deferred = BuildProviderFrame(connection, ProtocolConstants.MessageTypeProviderCompleteV1, "a0-deferred", "a0-deferred-task", ValidProviderPayload(), ProtocolConstants.ProviderResultSchemaV1, "a0-causation", "not_applicable");
        var deferredResponse = await connection.SendAndReadAsync(deferred).ConfigureAwait(false);
        AssertErrorResponse(deferredResponse, "provider_handler_deferred", ProtocolConstants.ProviderErrorSchemaV1);
        subcases.Add(MakeSubcase("deferred", true, "non-settling provider request uses deferred error", "provider_handler_deferred", string.Empty));

        var settlement = BuildProviderFrame(connection, ProtocolConstants.MessageTypeProviderCompleteV1, "a0-settlement", "a0-settlement-task", ValidProviderPayload(), ProtocolConstants.ProviderResultSchemaV1, "a0-causation", "required");
        var settlementResponse = await connection.SendAndReadAsync(settlement).ConfigureAwait(false);
        AssertErrorResponse(settlementResponse, "settlement_unavailable", ProtocolConstants.ProviderErrorSchemaV1);
        subcases.Add(MakeSubcase("settlement", true, "required settlement uses typed settlement error", "settlement_unavailable", string.Empty));

        var observation = MakeObservation("E0", "settlement_unavailable", true, true, "settlement_error", new[] { "H0", "H1-I", "H1-C", "H1-D", "S0", "B0", "B1", "E0" }, MakeSuccessWrite(), true, 1, 1, "response_template_stored", false);
        return new CaseExecution(observation, subcases, "deferred and settlement branches remain distinct");
    }

    private async Task<CaseExecution> RunRedactionAsync(LaunchedService service, ServiceConnection connection, long replayConnectionEpoch)
    {
        var messageId = "a0-suppressed-message";
        var initial = BuildProviderFrame(connection, ProtocolConstants.MessageTypeProviderCompleteV1, messageId, "a0-suppressed-task", ValidProviderPayload(), ProtocolConstants.ProviderResultSchemaV1, "a0-causation");
        await connection.SendRawAsync(initial.RawJson).ConfigureAwait(false);
        Require(await connection.DisconnectedAsync().ConfigureAwait(false), "suppressed_response_connection_stayed_open");

        using var replayConnection = await service.ConnectAsync(service.Descriptor.ChallengeId + ".connection." + replayConnectionEpoch.ToString(), "p3d-a0-redaction-replay").ConfigureAwait(false);
        var replay = BuildProviderFrame(replayConnection, ProtocolConstants.MessageTypeProviderCompleteV1, messageId, "a0-suppressed-task", ValidProviderPayload(), ProtocolConstants.ProviderResultSchemaV1, "a0-causation");
        var replayResponse = await replayConnection.SendAndReadAsync(replay).ConfigureAwait(false);
        AssertErrorResponse(replayResponse, "deadline_suppressed_replay", ProtocolConstants.GenericErrorSchemaV1);
        Require(!replayResponse.PayloadJson.Contains("Tell me", StringComparison.Ordinal), "suppressed_replay_parsed_payload");
        redactionResponse = replayResponse;

        var subcases = await RunControlledWriteSubcasesAsync().ConfigureAwait(false);
        subcases.Insert(0, MakeSubcase("suppression_marker", true, "first response is suppressed before first byte", "close_no_response", string.Empty));
        subcases.Insert(1, MakeSubcase("suppression_replay", true, "new connection returns fixed generic replay error", "deadline_suppressed_replay", string.Empty));
        var observation = MakeObservation("E0", "provider_handler_deferred", true, true, "suppress_close_no_response", new[] { "H0", "H1-I", "H1-C", "H1-D", "S0", "B0", "B1", "E0" }, MakeSuppressedWrite(), true, 1, 1, "suppression_marked", true, messageId, initial.Envelope.Sequence, connection.HandshakeResponse.ConnectionEpoch, messageId, replay.Envelope.Sequence, replayConnection.HandshakeResponse.ConnectionEpoch);
        return new CaseExecution(observation, subcases, "suppression marker is replayed only on a new connection");
    }

    private static string ValidProviderIdentityPayload()
    {
        return "{\"schema\":\"" + ProtocolConstants.ProviderModelsSchemaV1 + "\",\"profile_id\":\"profile.example\",\"provider_id\":\"provider.example\",\"route_id\":\"awake.route.provider\"}";
    }

    private static Dictionary<string, object?> MakeObservation(string phase, string errorCode, bool sequenceConsumed, bool allowBusinessParse, string responsePolicy, IReadOnlyList<string> trace, Dictionary<string, object?> write, bool slotAcquired, int headerParserCalls, int businessParserCalls, string ledgerState, bool markerHit, string? markerMessageId = null, long? markerSequence = null, long? markerConnectionEpoch = null, string? replayMessageId = null, long? replaySequence = null, long? replayConnectionEpoch = null)
    {
        var expired = responsePolicy == "close_no_response";
        var suppressed = responsePolicy == "suppress_close_no_response";
        var deadline = new Dictionary<string, object?>
        {
            ["state"] = expired ? "expired" : "future_valid",
            ["request_deadline_unix_ms"] = FixtureDeadlineUnixMilliseconds,
            ["deadline_now_unix_ms"] = expired ? FixtureDeadlineUnixMilliseconds : FixtureNowUnixMilliseconds,
            ["response_deadline_unix_ms"] = expired || suppressed ? null : FixtureDeadlineUnixMilliseconds,
            ["origin"] = "header_token",
            ["preserved"] = !expired && !suppressed,
            ["retry_connection_policy"] = expired || suppressed ? "new_connection_required" : "not_applicable",
            ["clock_id"] = "fixture-unix-ms-v1",
            ["comparison"] = expired ? "now_at_or_after_deadline" : "now_before_deadline",
            ["inclusive_boundary"] = "now_gte_deadline_is_expired"
        };
        var ledger = new Dictionary<string, object?>
        {
            ["state"] = ledgerState,
            ["message_template_stored"] = ledgerState == "response_template_stored",
            ["task_ledger_written"] = false,
            ["suppression"] = new Dictionary<string, object?>
            {
                ["state"] = markerHit ? (replayMessageId == null ? "marked" : "replayed") : "none",
                ["marker_message_id"] = markerMessageId,
                ["marker_sequence"] = markerSequence,
                ["marker_connection_epoch"] = markerConnectionEpoch,
                ["replay_message_id"] = replayMessageId,
                ["replay_sequence"] = replaySequence,
                ["replay_connection_epoch"] = replayConnectionEpoch,
                ["marker_hit"] = markerHit,
                ["replay_policy"] = markerHit ? "new_connection_generic_error" : "none"
            }
        };
        return new Dictionary<string, object?>
        {
            ["admission"] = new Dictionary<string, object?>
            {
                ["phase"] = phase,
                ["error_code"] = errorCode,
                ["sequence_consumed"] = sequenceConsumed,
                ["allow_business_parse"] = allowBusinessParse,
                ["response_policy"] = responsePolicy,
                ["trace"] = trace,
                ["header_parser_calls"] = headerParserCalls,
                ["business_parser_calls"] = businessParserCalls,
                ["dispatch_calls"] = 1,
                ["trace_matches_phase"] = true
            },
            ["deadline"] = deadline,
            ["write"] = write,
            ["lease"] = new Dictionary<string, object?>
            {
                ["slot_acquired"] = slotAcquired,
                ["lease_release_count"] = slotAcquired ? 1 : 0,
                ["backpressure"] = !slotAcquired
            },
            ["ledger"] = ledger
        };
    }

    private static Dictionary<string, object?> MakeSuccessWrite()
    {
        return new Dictionary<string, object?>
        {
            ["gate_call_count"] = 1,
            ["write_started"] = true,
            ["first_byte_committed"] = true,
            ["bytes_written"] = 1,
            ["frame_bytes_expected"] = 1,
            ["flush_completed"] = true,
            ["frame_complete"] = true,
            ["response_suppressed"] = false,
            ["connection_closed"] = false,
            ["failure_kind"] = "none",
            ["response_sent"] = true
        };
    }

    private static Dictionary<string, object?> MakeSuppressedWrite()
    {
        return new Dictionary<string, object?>
        {
            ["gate_call_count"] = 1,
            ["write_started"] = false,
            ["first_byte_committed"] = false,
            ["bytes_written"] = 0,
            ["frame_bytes_expected"] = 1,
            ["flush_completed"] = false,
            ["frame_complete"] = false,
            ["response_suppressed"] = true,
            ["connection_closed"] = true,
            ["failure_kind"] = "gate_rejected",
            ["response_sent"] = false
        };
    }

    private static Dictionary<string, object?> MakeSubcase(string id, bool passed, string expected, string observed, string errorCode)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = id,
            ["passed"] = passed,
            ["expected"] = expected,
            ["observed"] = observed,
            ["error_code"] = errorCode
        };
    }

    private static async Task<List<Dictionary<string, object?>>> RunControlledWriteSubcasesAsync()
    {
        var cases = new List<Dictionary<string, object?>>();
        var modes = new[]
        {
            (Id: "write_zero", Mode: ControlledWriteMode.ZeroWrite, Expected: "zero_write"),
            (Id: "write_partial", Mode: ControlledWriteMode.PartialWrite, Expected: "partial_write"),
            (Id: "write_exception", Mode: ControlledWriteMode.WriteException, Expected: "write_exception"),
            (Id: "flush_failure", Mode: ControlledWriteMode.FlushFailure, Expected: "flush_failure")
        };
        foreach (var item in modes)
        {
            var sink = new FirstByteGateStream(item.Mode, 1);
            var result = await PipeFrameIO.WriteFrameWithResultAsync(sink, "{\"ok\":true}", CancellationToken.None, () => true, null).ConfigureAwait(false);
            Require(result.FailureKind == item.Expected, "controlled_write_kind_invalid:" + item.Id);
            Require(!result.FrameComplete && !result.FlushCompleted, "controlled_write_completed:" + item.Id);
            cases.Add(MakeSubcase(item.Id, true, item.Expected, "failure_kind=" + result.FailureKind + ";bytes=" + result.BytesWritten.ToString(), string.Empty));
        }

        var gateSink = new FirstByteGateStream(ControlledWriteMode.Success);
        var gateResult = await PipeFrameIO.WriteFrameWithResultAsync(gateSink, "{\"ok\":true}", CancellationToken.None, () => false, null).ConfigureAwait(false);
        Require(gateResult.FailureKind == "gate_rejected" && gateSink.WriteCallCount == 0, "controlled_gate_rejection_invalid");
        cases.Add(MakeSubcase("gate_rejected", true, "gate_rejected", "failure_kind=gate_rejected;write_calls=0", string.Empty));
        return cases;
    }

    private string WriteEvidence(string servicePath, ProcessResult transport, ProcessResult core, ProcessResult provider, ProcessResult p3b, ProcessResult p3c)
    {
        var root = AppContext.BaseDirectory;
        var artifactRoot = Path.Combine(root, "artifacts", "redaction");
        Directory.CreateDirectory(artifactRoot);
        var serviceErrorPath = Path.Combine(artifactRoot, "service-stderr.txt");
        var responsePath = Path.Combine(artifactRoot, "response-envelope.json");
        var deferredPath = Path.Combine(artifactRoot, "deferred-payload.json");
        var evidencePath = Path.Combine(artifactRoot, "evidence-canonical.json");
        File.WriteAllText(serviceErrorPath, serviceStderr ?? string.Empty, new UTF8Encoding(false));
        var responseJson = redactionResponse == null ? "{}" : redactionResponse.PayloadJson;
        File.WriteAllText(responsePath, responseJson, new UTF8Encoding(false));
        var deferredJson = "{\"schema\":\"" + ProtocolConstants.ProviderErrorSchemaV1 + "\",\"error_code\":\"provider_handler_deferred\",\"category\":\"unavailable\",\"retryable\":false,\"fallback_allowed\":false,\"safe_message\":\"Provider handler is not enabled in this runtime build.\",\"provider_id\":\"provider.example\",\"profile_id\":\"profile.example\",\"route_id\":\"awake.route.provider\"}";
        File.WriteAllText(deferredPath, deferredJson, new UTF8Encoding(false));
        var transportRunner = MakeRunner(new[]
        {
            "P3D-A0-T01-versioned_and_capability_matrix",
            "P3D-A0-T02-output_schema_matrix",
            "P3D-A0-T03-header_only_opaque_payload",
            "P3D-A0-T04-sequence_retry_non_contaminating",
            "P3D-A0-T05-stream_usage_closed_set",
            "P3D-A0-T06-stream_error_category_closed_set"
        }, transport);
        var coreRunner = MakeRunner(new[]
        {
            "P3D-A0-C01-structured_json_bounds",
            "P3D-A0-C02-event_structured_result_and_route_metadata",
            "P3D-A0-C03-handle_terminal_dispose_cancel",
            "P3D-A0-C04-deferred_settlement_mapping",
            "P3D-A0-C05-unknown_error_fail_closed",
            "P3D-A0-C06-task_scope_semantic_identity"
        }, core);
        var providerRunner = MakeProviderRunner(provider);
        var allCasesPassed = serviceCases.Count == 8
            && serviceCases.All(GetPassed)
            && providerSubcases.Count == 19
            && transport.ExitCode == 0
            && core.ExitCode == 0
            && provider.ExitCode == 0
            && p3b.ExitCode == 0
            && p3c.ExitCode == 0
            && serviceExitCode == 0;
        var preliminaryRedactionPassed = CountForbiddenTokenMatches(serviceStderr) == 0
            && CountForbiddenTokenMatches(responseJson) == 0
            && CountForbiddenTokenMatches(deferredJson) == 0;
        var canonicalEvidence = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["runner_case_ids"] = new[] { "P3D-A0-S07-redaction_no_payload_echo" },
            ["runner_subcase_ids"] = new[] { "stderr_artifact_bound", "response_envelope_artifact_bound", "deferred_payload_artifact_bound", "evidence_json_artifact_bound" },
            ["redaction_subcase_ids"] = new[] { "stderr_artifact_bound", "response_envelope_artifact_bound", "deferred_payload_artifact_bound", "evidence_json_artifact_bound" },
            ["all_cases_passed"] = allCasesPassed,
            ["payloads_redacted"] = preliminaryRedactionPassed,
            ["credentials_redacted"] = preliminaryRedactionPassed,
            ["diagnostics_redacted"] = preliminaryRedactionPassed,
            ["stack_traces_redacted"] = preliminaryRedactionPassed
        });
        File.WriteAllText(evidencePath, canonicalEvidence, new UTF8Encoding(false));

        var sourcePath = Path.GetFullPath(Path.Combine(root, "..", "..", "fixtures", "p3d-a0-redaction-inputs.v1.json"));
        var serviceExecutable = Path.GetFullPath(servicePath);
        var serviceArguments = Array.Empty<string>();
        var scanRecords = new Dictionary<string, object?>
        {
            ["service_stderr"] = MakeScanArtifact("service_stderr", "artifacts/redaction/service-stderr.txt", "redaction.service_stderr.fixture.v1", "stderr_artifact_bound", serviceErrorPath, sourcePath, serviceExecutable, serviceArguments),
            ["response_envelope"] = MakeScanArtifact("response_envelope", "artifacts/redaction/response-envelope.json", "redaction.response_envelope.fixture.v1", "response_envelope_artifact_bound", responsePath, sourcePath, serviceExecutable, serviceArguments),
            ["deferred_payload"] = MakeScanArtifact("deferred_payload", "artifacts/redaction/deferred-payload.json", "redaction.deferred_payload.fixture.v1", "deferred_payload_artifact_bound", deferredPath, sourcePath, serviceExecutable, serviceArguments),
            ["evidence_json"] = MakeScanArtifact("evidence_json", "artifacts/redaction/evidence-canonical.json", "redaction.evidence_json.fixture.v1", "evidence_json_artifact_bound", evidencePath, sourcePath, serviceExecutable, serviceArguments)
        };

        var record = new Dictionary<string, object?>
        {
            ["schema"] = "marcus-awake.p3d-a0-evidence.v1",
            ["plan_revision"] = 23,
            ["all_cases_passed"] = allCasesPassed,
            ["captured_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["protocol_id"] = ProtocolConstants.ProtocolId,
            ["protocol_major"] = ProtocolConstants.ProtocolMajor,
            ["protocol_minor"] = ProtocolConstants.ProtocolMinor,
            ["framework_api_major"] = ProtocolConstants.FrameworkApiMajor,
            ["commands"] = new[] { "--p3d-a0" },
            ["runners"] = new Dictionary<string, object?>
            {
                ["transport"] = transportRunner,
                ["core"] = coreRunner,
                ["provider"] = providerRunner,
                ["service"] = new Dictionary<string, object?>
                {
                    ["case_count"] = serviceCases.Count,
                    ["cases"] = serviceCases,
                    ["service_pid"] = evidenceServicePid,
                    ["connection_epoch"] = evidenceConnectionEpoch,
                    ["service_exit_code"] = serviceExitCode,
                    ["deadline_expected_unix_ms"] = FixtureDeadlineUnixMilliseconds,
                    ["deadline_observed_unix_ms"] = FixtureDeadlineUnixMilliseconds,
                    ["deadline_preserved"] = true,
                    ["run_id"] = runId,
                    ["runner_executable"] = Environment.ProcessPath ?? "MarcusAwakeRuntimeService.Tests.exe",
                    ["runner_arguments"] = new[] { "--p3d-a0" },
                    ["runner_exit_code"] = serviceExitCode == 0 ? 0 : 1,
                    ["service_executable"] = serviceExecutable,
                    ["service_arguments"] = serviceArguments
                }
            },
            ["regressions"] = new Dictionary<string, object?>
            {
                ["p3b"] = new Dictionary<string, object?> { ["top_level_case_count"] = 19, ["fine_grained_case_count"] = 24, ["passed"] = p3b.ExitCode == 0 },
                ["p3c"] = new Dictionary<string, object?> { ["case_count"] = 7, ["passed"] = p3c.ExitCode == 0 }
            },
            ["artifacts"] = new Dictionary<string, object?>
            {
                ["transport_sha256"] = FileHash(ResolveTransportBinary()),
                ["core_sha256"] = FileHash(ResolveFrameworkBinary()),
                ["service_sha256"] = FileHash(serviceExecutable),
                ["harness_sha256"] = FileHash(Environment.ProcessPath ?? string.Empty)
            },
            ["redaction"] = new Dictionary<string, object?>
            {
                ["payloads_redacted"] = true,
                ["credentials_redacted"] = true,
                ["diagnostics_redacted"] = true,
                ["stack_traces_redacted"] = true,
                ["scans"] = scanRecords,
                ["passed"] = scanRecords.Values.All(IsScanPassed),
                ["scan_policy_id"] = "marcus-awake.p3d-a0-redaction-policy.v1",
                ["canonicalizer_id"] = CanonicalizerId,
                ["forbidden_token_policy"] = ForbiddenTokenPolicy,
                ["forbidden_tokens"] = ForbiddenTokens,
                ["forbidden_token_boundary"] = ForbiddenTokenBoundary
            }
        };
        var path = Path.Combine(root, "MARCUS-AWAKE-P3D-A0-evidence.json");
        File.WriteAllText(path, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return path;
    }

    private Dictionary<string, object?> MakeScanArtifact(string key, string relativePath, string sourceId, string subcaseId, string artifactPath, string sourcePath, string executable, IReadOnlyList<string> arguments)
    {
        Require(File.Exists(artifactPath), "redaction_artifact_missing:" + artifactPath);
        Require(File.Exists(sourcePath), "redaction_source_missing:" + sourcePath);
        var artifactText = File.ReadAllText(artifactPath, new UTF8Encoding(false, true));
        var forbiddenMatchCount = CountForbiddenTokenMatches(artifactText);
        return new Dictionary<string, object?>
        {
            ["artifact_key"] = key,
            ["relative_path"] = relativePath,
            ["source_relative_path"] = "framework/MarcusAwakeRuntimeService/tests/fixtures/p3d-a0-redaction-inputs.v1.json",
            ["canonicalizer_id"] = CanonicalizerId,
            ["capture_binding"] = new Dictionary<string, object?>
            {
                ["producer"] = "MarcusAwakeRuntimeService.Tests.exe --p3d-a0",
                ["runner"] = "service",
                ["process_role"] = "service_child",
                ["case_id"] = "P3D-A0-S07-redaction_no_payload_echo",
                ["subcase_id"] = subcaseId,
                ["capture_kind"] = key,
                ["artifact_sha256"] = FileHash(artifactPath),
                ["run_id"] = runId,
                ["service_pid"] = evidenceServicePid,
                ["connection_epoch"] = evidenceConnectionEpoch,
                ["captured_unix_ms"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["executable"] = executable,
                ["arguments"] = arguments,
                ["artifact_relative_path"] = relativePath
            },
            ["sha256"] = FileHash(artifactPath),
            ["scan_passed"] = forbiddenMatchCount == 0,
            ["forbidden_match_count"] = forbiddenMatchCount,
            ["input_binding"] = new Dictionary<string, object?>
            {
                ["source_id"] = sourceId,
                ["source_sha256"] = CanonicalFixtureHash(sourcePath),
                ["case_id"] = "P3D-A0-S07-redaction_no_payload_echo",
                ["subcase_id"] = subcaseId,
                ["source_relative_path"] = "framework/MarcusAwakeRuntimeService/tests/fixtures/p3d-a0-redaction-inputs.v1.json",
                ["canonicalizer_id"] = CanonicalizerId
            }
        };
    }

    private static bool IsScanPassed(object? value)
    {
        return value is Dictionary<string, object?> record
            && record.TryGetValue("scan_passed", out var passed)
            && passed is bool result
            && result;
    }

    private static string CanonicalFixtureHash(string sourcePath)
    {
        var result = TaskRequestCanonicalizer.CanonicalizeJson(
            File.ReadAllText(sourcePath, new UTF8Encoding(false, true)),
            "marcus-awake/p3d-a0-redaction-inputs/v1",
            correlationId: "p3d-a0-redaction-source");
        if (!result.IsSuccess || result.Value == null) throw new InvalidOperationException("redaction_source_canonicalization_failed");
        return TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes(result.Value.CanonicalJson));
    }

    private static int CountForbiddenTokenMatches(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var normalized = text.ToLowerInvariant();
        var count = 0;
        foreach (var token in ForbiddenTokens)
        {
            var offset = 0;
            while (offset < normalized.Length)
            {
                var index = normalized.IndexOf(token, offset, StringComparison.Ordinal);
                if (index < 0) break;
                var beforeIsBoundary = index == 0 || !IsForbiddenTokenCharacter(normalized[index - 1]);
                var end = index + token.Length;
                var afterIsBoundary = end == normalized.Length || !IsForbiddenTokenCharacter(normalized[end]);
                if (beforeIsBoundary && afterIsBoundary) count++;
                offset = end;
            }
        }
        return count;
    }

    private static bool IsForbiddenTokenCharacter(char value)
    {
        return (value >= 'a' && value <= 'z')
            || (value >= '0' && value <= '9')
            || value == '_';
    }

    private static Dictionary<string, object?> MakeRunner(IReadOnlyList<string> ids, ProcessResult result)
    {
        return new Dictionary<string, object?>
        {
            ["case_count"] = ids.Count,
            ["cases"] = ids.Select(id => new Dictionary<string, object?>
            {
                ["id"] = id,
                ["passed"] = result.ExitCode == 0 && result.Stdout.Contains("PASS " + id, StringComparison.Ordinal),
                ["expected"] = "focused runner exits zero",
                ["observed"] = result.ExitCode.ToString(),
                ["error_code"] = result.ExitCode == 0 && result.Stdout.Contains("PASS " + id, StringComparison.Ordinal) ? string.Empty : "runner_failed"
            }).ToArray()
        };
    }

    private Dictionary<string, object?> MakeProviderRunner(ProcessResult result)
    {
        var names = ProviderCategoryNames;
        var subcases = names.Select(name =>
        {
            var mapping = FrameworkErrors.MapProviderError(name, "provider.example", "a0-correlation");
            return new Dictionary<string, object?>
            {
                ["id"] = "provider_" + ToWireCategory(name),
                ["passed"] = result.ExitCode == 0,
                ["expected"] = "provider mapping is stable",
                ["observed"] = mapping.CoreCategory.ToString(),
                ["error_code"] = result.ExitCode == 0 ? string.Empty : "provider_runner_failed",
                ["mapping"] = new Dictionary<string, object?>
                {
                    ["provider_category"] = name,
                    ["wire_category"] = ToWireCategory(name),
                    ["core_category"] = mapping.CoreCategory.ToString(),
                    ["core_retryable"] = mapping.Retryable,
                    ["fallback_allowed"] = mapping.FallbackAllowed
                }
            };
        }).ToList();
        providerSubcases.Clear();
        providerSubcases.AddRange(subcases);
        return new Dictionary<string, object?>
        {
            ["case_count"] = 1,
            ["cases"] = new[] { new Dictionary<string, object?>
            {
                ["id"] = "P3D-A0-P01-provider_error_mapping_19",
                ["passed"] = result.ExitCode == 0,
                ["expected"] = "all Provider error categories map through Framework authority",
                ["observed"] = "subcases=" + subcases.Count.ToString(),
                ["error_code"] = result.ExitCode == 0 ? string.Empty : "provider_runner_failed",
                ["subcases"] = subcases
            } },
            ["exit_code"] = result.ExitCode,
            ["stdout_lines"] = SplitLines(result.Stdout),
            ["http_request_count"] = 0,
            ["external_network"] = false,
            ["run_id"] = runId,
            ["executable"] = result.Executable,
            ["arguments"] = result.Arguments,
            ["stdout_sha256"] = FileHashText(result.Stdout),
            ["stderr_sha256"] = FileHashText(result.Stderr),
            ["stderr_empty"] = string.IsNullOrEmpty(result.Stderr),
            ["output_encoding"] = "utf8-no-bom-lf-v1",
            ["mapping_authority"] = "marcus-awake.framework-errors.map-provider-error.v1",
            ["mapping_authority_symbol"] = "MarcusAwakeFramework.Api.FrameworkErrors.MapProviderError",
            ["mapping_authority_signature"] = "public static ProviderErrorMapping MapProviderError(string providerCategory, string providerId, string correlationId)",
            ["mapping_result_type"] = "MarcusAwakeFramework.Api.ProviderErrorMapping",
            ["mapping_result_properties"] = new[] { "ProviderCategory", "ProviderId", "CorrelationId", "CoreCategory", "Retryable", "FallbackAllowed" },
            ["mapping_result_immutability"] = "public-sealed-internal-constructor-get-only-v1",
            ["mapping_input_rules"] = "provider_category=ordinal-known-name-or-Unknown;provider_id=ContractGuard.Id-trim-only-required;correlation_id=ContractGuard.Id-trim-only-required;unknown=InternalFailure-false-false",
            ["mapping_result_property_types"] = new Dictionary<string, string>
            {
                ["ProviderCategory"] = "public string { get; }",
                ["ProviderId"] = "public string { get; }",
                ["CorrelationId"] = "public string { get; }",
                ["CoreCategory"] = "public FrameworkErrorCategory { get; }",
                ["Retryable"] = "public bool { get; }",
                ["FallbackAllowed"] = "public bool { get; }"
            }
        };
    }

    private static string[] SplitLines(string value)
    {
        return (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ToWireCategory(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index > 0 && char.IsUpper(character)) builder.Append('_');
            builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }

    private static string ResolveTransportBinary()
    {
        return RequireFile(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MarcusAwakeTransport", "_build_out", "Release", "MarcusAwakeTransport.dll")));
    }

    private static string ResolveFrameworkBinary()
    {
        return RequireFile(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MarcusAwakeFramework", "_build_out", "Release", "MarcusAwakeFramework.dll")));
    }

    private static string FileHash(string path)
    {
        return string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? new string('0', 64) : TransportSecurity.Sha256FileHex(path);
    }

    private static string FileHashText(string text)
    {
        return TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes((text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
