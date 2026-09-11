using MarcusAwakeFramework.Api;
using MarcusAwakeTransport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeRuntimeService.Tests;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal sealed class P3DA1HarnessRunner
{
    private static readonly string[] ProviderCapabilities =
    {
        ProtocolConstants.CapabilityProviderConfigureV1,
        ProtocolConstants.CapabilityProviderModelsV1,
        ProtocolConstants.CapabilityProviderCompleteV1
    };

    private int passed;
    private int failed;
    private string runId = string.Empty;
    private int primaryServicePid;
    private string primaryPipeName = string.Empty;
    private string currentServiceExecutable = string.Empty;
    private int currentServicePid;
    private string currentPipeName = string.Empty;
    private IReadOnlyList<string> currentServiceArguments = Array.Empty<string>();
    private IReadOnlyList<string> primaryServiceArguments = Array.Empty<string>();
    private int primaryServiceExitCode;
    private string evidenceFakeHttpCapturePath = string.Empty;
    private FakeProviderHttpServer? evidenceFakeServer;
    private readonly List<Dictionary<string, object?>> caseObservations = new List<Dictionary<string, object?>>();

    internal async Task<int> RunAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("FAIL windows_required");
            return 1;
        }

        runId = "p3d-a1-" + Guid.NewGuid().ToString("N");
        var configuredCapturePath = Environment.GetEnvironmentVariable("MARCUS_AWAKE_P3D_A1_CAPTURE_PATH");
        evidenceFakeHttpCapturePath = string.IsNullOrWhiteSpace(configuredCapturePath)
            ? Path.Combine(Path.GetTempPath(), runId + "-fake-http-capture.json")
            : Path.GetFullPath(configuredCapturePath);
        var runRoot = Path.Combine(Path.GetTempPath(), runId);
        var dataRoot = Path.Combine(runRoot, "runtime");
        var credentialRoot = Path.Combine(runRoot, "credentials");
        var providerAssemblyPath = ResolveProviderAssemblyPath();
        var servicePath = ResolveServicePath();
        var secret = "p3d-a1-secret-" + Guid.NewGuid().ToString("N");
        LaunchedService? service = null;
        ServiceConnection? connection = null;
        FakeProviderHttpServer? fakeServer = null;
        try
        {
            Directory.CreateDirectory(runRoot);
            fakeServer = new FakeProviderHttpServer();
            evidenceFakeServer = fakeServer;
            await SeedCredentialAsync(providerAssemblyPath, credentialRoot, "credential.p3d_a1", secret).ConfigureAwait(false);
            service = await LaunchedService.StartAsync(
                servicePath,
                4101,
                dataRoot,
                AllCapabilities(),
                environmentOverrides: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MARCUS_AWAKE_PROVIDER_ASSEMBLY_PATH"] = providerAssemblyPath,
                    ["MARCUS_AWAKE_RUNTIME_CREDENTIAL_ROOT"] = credentialRoot
                }).ConfigureAwait(false);
            primaryServicePid = service.Process.Id;
            primaryPipeName = service.Descriptor.PipeName;
            currentServiceExecutable = servicePath;
            currentServicePid = primaryServicePid;
            currentPipeName = primaryPipeName;
            currentServiceArguments = service.CommandLineArguments;
            primaryServiceArguments = service.CommandLineArguments;
            connection = await service.ConnectAsync(service.Descriptor.ChallengeId, "p3d-a1-session", AllCapabilities()).ConfigureAwait(false);

            await RunCaseAsync("P3D-A1-S01-profile_upsert_creates_scoped_registry_entry", async () =>
            {
                var response = await connection.SendAndReadAsync(CreateProviderFrame(
                    connection,
                    ProtocolConstants.MessageTypeProviderProfileUpsertV1,
                    "a1-upsert",
                    "a1-upsert-task",
                    ProfilePayload(fakeServer.Port),
                    "a1-upsert-idem",
                    ProtocolConstants.ProviderProfileResultSchemaV1)).ConfigureAwait(false);
                AssertSuccess(response, "provider_profile_result", ProtocolConstants.ProviderProfileResultSchemaV1);
                Require(GetString(response, "status") == "ready", "profile_upsert_status_invalid");
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S03-models_calls_fake_http_and_maps_models", async () =>
            {
                var response = await connection.SendAndReadAsync(CreateProviderFrame(
                    connection,
                    ProtocolConstants.MessageTypeProviderModelsV1,
                    "a1-models",
                    "a1-models-task",
                    ScopePayload(ProtocolConstants.ProviderModelsSchemaV1),
                    "a1-models-idem",
                    ProtocolConstants.ProviderModelsResultSchemaV1)).ConfigureAwait(false);
                AssertSuccess(response, "provider_models_result", ProtocolConstants.ProviderModelsResultSchemaV1);
                Require(GetArrayLength(response, "models") == 1, "models_count_invalid");
                Require(GetArrayItemString(response, "models", 0, "id") == "fake-model", "model_id_invalid");
                Require(fakeServer.RequestCount == 1, "models_http_count_invalid");
            }).ConfigureAwait(false);
            FrameBuildResult completionFrame = CreateProviderFrame(
                connection,
                ProtocolConstants.MessageTypeProviderCompleteV1,
                "a1-complete",
                "a1-complete-task",
                CompletionPayload(),
                "a1-complete-idem",
                ProtocolConstants.ProviderResultSchemaV1);
            await RunCaseAsync("P3D-A1-S04-complete_calls_fake_http_and_maps_typed_result", async () =>
            {
                var response = await connection.SendAndReadAsync(completionFrame).ConfigureAwait(false);
                AssertSuccess(response, "provider_result", ProtocolConstants.ProviderResultSchemaV1);
                Require(GetString(response, "model_id") == "fake-model", "completion_model_invalid");
                Require(GetString(response, "content") == "fake reply", "completion_content_invalid");
                Require(GetInt32(response, "usage", "input_tokens") == 2, "completion_input_usage_invalid");
                Require(GetInt32(response, "usage", "output_tokens") == 3, "completion_output_usage_invalid");
                Require(fakeServer.RequestCount == 2, "completion_http_count_invalid");
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S09-duplicate_frame_replays_same_non_durable_result", async () =>
            {
                var response = await connection.SendRawAndReadAsync(completionFrame.RawJson).ConfigureAwait(false);
                AssertSuccess(response, "provider_result", ProtocolConstants.ProviderResultSchemaV1);
                Require(fakeServer.RequestCount == 2, "duplicate_recalled_provider");
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S16-same_idempotency_payload_replays_without_recall", async () =>
            {
                var replayFrame = CreateProviderFrame(
                    connection,
                    ProtocolConstants.MessageTypeProviderCompleteV1,
                    "a1-idempotency-replay",
                    "a1-idempotency-replay-task",
                    CompletionPayload(),
                    "a1-complete-idem",
                    ProtocolConstants.ProviderResultSchemaV1);
                var response = await connection.SendAndReadAsync(replayFrame).ConfigureAwait(false);
                AssertSuccess(response, "provider_result", ProtocolConstants.ProviderResultSchemaV1);
                Require(GetString(response, "content") == "fake reply", "idempotency_replay_content_invalid");
                Require(fakeServer.RequestCount == 2, "same_idempotency_recalled_provider");
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S17-different_payload_same_idempotency_key_is_conflict_without_recall", async () =>
            {
                var conflictFrame = CreateProviderFrame(
                    connection,
                    ProtocolConstants.MessageTypeProviderCompleteV1,
                    "a1-idempotency-conflict",
                    "a1-idempotency-conflict-task",
                    CompletionPayload("say something else"),
                    "a1-complete-idem",
                    ProtocolConstants.ProviderResultSchemaV1);
                var response = await connection.SendAndReadAsync(conflictFrame).ConfigureAwait(false);
                AssertError(response, "provider.idempotency_conflict", "conflict");
                Require(fakeServer.RequestCount == 2, "conflicting_idempotency_called_provider");
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S05-missing_credential_never_calls_http", async () =>
            {
                const string profileId = "profile.missing-credential";
                const string providerId = "provider.missing-credential";
                const string routeId = "route.missing-credential";
                var before = fakeServer.RequestCount;
                var profile = await connection.SendAndReadAsync(CreateProviderFrame(
                    connection,
                    ProtocolConstants.MessageTypeProviderProfileUpsertV1,
                    "a1-missing-profile",
                    "a1-missing-profile-task",
                    ProfilePayload(fakeServer.Port, profileId, providerId, routeId, "credential.missing", true),
                    "a1-missing-profile-idem",
                    ProtocolConstants.ProviderProfileResultSchemaV1,
                    profileId,
                    providerId,
                    routeId)).ConfigureAwait(false);
                AssertSuccess(profile, "provider_profile_result", ProtocolConstants.ProviderProfileResultSchemaV1);
                var response = await connection.SendAndReadAsync(CreateProviderFrame(
                    connection,
                    ProtocolConstants.MessageTypeProviderModelsV1,
                    "a1-missing-models",
                    "a1-missing-models-task",
                    ScopePayload(ProtocolConstants.ProviderModelsSchemaV1, profileId, providerId, routeId),
                    "a1-missing-models-idem",
                    ProtocolConstants.ProviderModelsResultSchemaV1,
                    profileId,
                    providerId,
                    routeId)).ConfigureAwait(false);
                AssertError(response, "credential.not_found", "not_found");
                Require(fakeServer.RequestCount == before, "missing_credential_called_provider");
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S06-provider_http_error_maps_safe_typed_error", async () =>
            {
                var before = fakeServer.RequestCount;
                fakeServer.ResponseStatusCode = 401;
                try
                {
                    var response = await connection.SendAndReadAsync(CreateProviderFrame(
                        connection,
                        ProtocolConstants.MessageTypeProviderCompleteV1,
                        "a1-http-error",
                        "a1-http-error-task",
                        CompletionPayload("auth failure"),
                        "a1-http-error-idem",
                        ProtocolConstants.ProviderResultSchemaV1)).ConfigureAwait(false);
                    AssertError(response, "http.authentication_failed", "authentication");
                    Require(fakeServer.RequestCount == before + 1, "http_error_request_count_invalid");
                    Require(!response.PayloadJson.Contains(secret, StringComparison.Ordinal), "http_error_secret_echoed");
                }
                finally
                {
                    fakeServer.ResponseStatusCode = 200;
                }
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S08-profile_scope_isolation", async () =>
            {
                using var isolatedConnection = await service.ConnectNextAsync("p3d-a1-isolated-" + Guid.NewGuid().ToString("N"), AllCapabilities()).ConfigureAwait(false);
                var before = fakeServer.RequestCount;
                var response = await isolatedConnection.SendAndReadAsync(CreateProviderFrame(
                    isolatedConnection,
                    ProtocolConstants.MessageTypeProviderModelsV1,
                    "a1-isolated-models",
                    "a1-isolated-models-task",
                    ScopePayload(ProtocolConstants.ProviderModelsSchemaV1),
                    "a1-isolated-models-idem",
                    ProtocolConstants.ProviderModelsResultSchemaV1)).ConfigureAwait(false);
                AssertError(response, "profile.not_found", "not_found");
                Require(fakeServer.RequestCount == before, "isolated_scope_called_provider");
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S10-oversized_or_malformed_provider_response_is_rejected", async () =>
            {
                var malformedBefore = fakeServer.RequestCount;
                try
                {
                    fakeServer.ResponseBodyOverride = "{\"invalid\":true}";
                    var malformed = await connection.SendAndReadAsync(CreateProviderFrame(
                        connection,
                        ProtocolConstants.MessageTypeProviderCompleteV1,
                        "a1-malformed-response",
                        "a1-malformed-response-task",
                        CompletionPayload("malformed"),
                        "a1-malformed-response-idem",
                        ProtocolConstants.ProviderResultSchemaV1)).ConfigureAwait(false);
                    AssertError(malformed, "response.completion_invalid", "malformed_response");
                    Require(fakeServer.RequestCount == malformedBefore + 1, "malformed_response_request_count_invalid");

                    fakeServer.ResponseBodyOverride = new string('x', 2_000_000);
                    var oversized = await connection.SendAndReadAsync(CreateProviderFrame(
                        connection,
                        ProtocolConstants.MessageTypeProviderCompleteV1,
                        "a1-oversized-response",
                        "a1-oversized-response-task",
                        CompletionPayload("oversized"),
                        "a1-oversized-response-idem",
                        ProtocolConstants.ProviderResultSchemaV1)).ConfigureAwait(false);
                    AssertError(oversized, "response.too_large", "resource_exhausted");
                }
                finally
                {
                    fakeServer.ResponseBodyOverride = string.Empty;
                }
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S11-no_secret_in_logs_evidence_or_fake_server_capture", async () =>
            {
                Require(!fakeServer.LastRequestText.Contains(secret, StringComparison.Ordinal), "fake_server_capture_contains_secret");
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S13-cancel_before_provider_admission_never_calls_http", async () =>
            {
                var target = CreateProviderFrame(
                    connection,
                    ProtocolConstants.MessageTypeProviderCompleteV1,
                    "a1-cancel-before-admission",
                    "a1-cancel-before-admission-task",
                    CompletionPayload("cancel before admission"),
                    "a1-cancel-before-admission-idem",
                    ProtocolConstants.ProviderResultSchemaV1);
                using var control = await ConnectControlAsync(service, connection.SessionId).ConfigureAwait(false);
                var cancel = await control.SendAndReadAsync(CreateControlCancelFrame(control, target, "a1-cancel-before-admission-control")).ConfigureAwait(false);
                AssertCancelRequested(cancel);
                var before = fakeServer.RequestCount;
                var response = await connection.SendAndReadAsync(target).ConfigureAwait(false);
                AssertError(response, "request.cancelled", "cancelled");
                Require(fakeServer.RequestCount == before, "pre_admission_cancel_called_provider");
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S14-cancel_during_http_maps_cancelled_without_late_completion", async () =>
            {
                var target = CreateProviderFrame(
                    connection,
                    ProtocolConstants.MessageTypeProviderCompleteV1,
                    "a1-cancel-during-http",
                    "a1-cancel-during-http-task",
                    CompletionPayload("cancel during http"),
                    "a1-cancel-during-http-idem",
                    ProtocolConstants.ProviderResultSchemaV1);
                using var control = await ConnectControlAsync(service, connection.SessionId).ConfigureAwait(false);
                fakeServer.DelayMilliseconds = 1000;
                try
                {
                    var requestCountBefore = fakeServer.RequestCount;
                    var beforeResponses = fakeServer.ResponseCount;
                    await connection.SendRawAsync(target.RawJson).ConfigureAwait(false);
                    await fakeServer.WaitForRequestCountAsync(requestCountBefore + 1, 5000).ConfigureAwait(false);
                    var cancel = await control.SendAndReadAsync(CreateControlCancelFrame(control, target, "a1-cancel-during-http-control")).ConfigureAwait(false);
                    AssertCancelRequested(cancel);
                    var response = await connection.ReadResponseAsync(item => item.MessageId == target.Envelope.MessageId).ConfigureAwait(false);
                    AssertError(response, "request.cancelled", "cancelled");
                    await Task.Delay(1200).ConfigureAwait(false);
                    Require(fakeServer.ResponseCount == beforeResponses, "cancel_during_http_late_provider_response");
                }
                finally
                {
                    fakeServer.DelayMilliseconds = 0;
                }
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S02-profile_remove_is_scoped_and_idempotent", async () =>
            {
                var response = await connection.SendAndReadAsync(CreateProviderFrame(
                    connection,
                    ProtocolConstants.MessageTypeProviderProfileRemoveV1,
                    "a1-remove",
                    "a1-remove-task",
                    ScopePayload(ProtocolConstants.ProviderProfileRemoveSchemaV1),
                    "a1-remove-idem",
                    ProtocolConstants.ProviderProfileResultSchemaV1)).ConfigureAwait(false);
                AssertSuccess(response, "provider_profile_result", ProtocolConstants.ProviderProfileResultSchemaV1);
                Require(GetString(response, "status") == "removed", "profile_remove_status_invalid");
                var removedAgain = await connection.SendAndReadAsync(CreateProviderFrame(
                    connection,
                    ProtocolConstants.MessageTypeProviderProfileRemoveV1,
                    "a1-remove-again",
                    "a1-remove-again-task",
                    ScopePayload(ProtocolConstants.ProviderProfileRemoveSchemaV1),
                    "a1-remove-again-idem",
                    ProtocolConstants.ProviderProfileResultSchemaV1)).ConfigureAwait(false);
                AssertSuccess(removedAgain, "provider_profile_result", ProtocolConstants.ProviderProfileResultSchemaV1);
                Require(GetString(removedAgain, "status") == "removed", "profile_remove_repeat_status_invalid");
            }).ConfigureAwait(false);
            connection.Dispose();
            connection = null;
            await service.RequestGracefulShutdownAsync("p3d-a1-primary-shutdown").ConfigureAwait(false);
            await service.DisposeAsync().ConfigureAwait(false);
            primaryServiceExitCode = service.FinalExitCode;
            service = null;
            await RunCaseAsync("P3D-A1-S07-deadline_and_cancellation_stop_unary_call", async () =>
            {
                await RunDeadlineAdmissionCaseAsync(servicePath, providerAssemblyPath, credentialRoot, dataRoot, fakeServer).ConfigureAwait(false);
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S12-child_service_shutdown_has_no_orphan", async () =>
            {
                await RunChildShutdownCaseAsync(servicePath, providerAssemblyPath, credentialRoot, dataRoot).ConfigureAwait(false);
            }).ConfigureAwait(false);
            await RunCaseAsync("P3D-A1-S15-provider_completion_before_ipc_write_failure_is_reused_without_recall", async () =>
            {
                await RunWriteFailureReplayCaseAsync(servicePath, providerAssemblyPath, credentialRoot, dataRoot, fakeServer).ConfigureAwait(false);
            }).ConfigureAwait(false);
            await RunFrameworkClientCasesAsync(servicePath, providerAssemblyPath, credentialRoot, dataRoot, fakeServer, secret).ConfigureAwait(false);
            WriteFakeHttpCapture(fakeServer);
            Console.WriteLine("P3D-A1_RUN_ID=" + runId);
            Console.WriteLine("P3D-A1_SERVICE_PID=" + primaryServicePid.ToString());
            Console.WriteLine("P3D-A1_PIPE_NAME=" + primaryPipeName);
            Console.WriteLine("P3D-A1_SERVICE_EXIT_CODE=" + primaryServiceExitCode.ToString());
            Console.WriteLine("P3D-A1_SERVICE_ARGUMENTS=" + JsonSerializer.Serialize(primaryServiceArguments));
            Console.WriteLine("P3D-A1_FAKE_HTTP_ENDPOINT=http://127.0.0.1:" + fakeServer.Port.ToString() + "/");
            Console.WriteLine("P3D-A1_FAKE_HTTP_CAPTURE_PATH=" + evidenceFakeHttpCapturePath);
            Console.WriteLine("P3D-A1_PROVIDER_HTTP_REQUESTS=" + fakeServer.RequestCount.ToString());
            Console.WriteLine("P3D-A1_PROVIDER_HTTP_RESPONSES=" + fakeServer.ResponseCount.ToString());
            Console.WriteLine("P3D-A1_FAKE_HTTP_REQUEST_OBSERVATIONS=" + JsonSerializer.Serialize(fakeServer.GetBoundedRequestObservations()));
            Console.WriteLine("P3D-A1_CASE_OBSERVATIONS=" + JsonSerializer.Serialize(caseObservations));
        }
        catch (Exception exception)
        {
            Console.WriteLine("FAIL P3D-A1 harness_fatal " + exception.Message);
            failed++;
        }
        finally
        {
            connection?.Dispose();
            if (service != null) await service.DisposeAsync().ConfigureAwait(false);
            if (fakeServer != null) await fakeServer.DisposeAsync().ConfigureAwait(false);
            TryDelete(runRoot);
        }

        Console.WriteLine("P3D-A1_PASS_COUNT=" + passed.ToString());
        Console.WriteLine("P3D-A1_FAIL_COUNT=" + failed.ToString());
        return failed == 0 ? 0 : 1;
    }

    private async Task RunFrameworkClientCasesAsync(string servicePath, string providerAssemblyPath, string credentialRoot, string dataRoot, FakeProviderHttpServer fakeServer, string secret)
    {
        var oldProviderAssemblyPath = Environment.GetEnvironmentVariable("MARCUS_AWAKE_PROVIDER_ASSEMBLY_PATH");
        var oldCredentialRoot = Environment.GetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_CREDENTIAL_ROOT");
        var oldDataRoot = Environment.GetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_DATA_ROOT");
        Environment.SetEnvironmentVariable("MARCUS_AWAKE_PROVIDER_ASSEMBLY_PATH", providerAssemblyPath);
        Environment.SetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_CREDENTIAL_ROOT", credentialRoot);
        Environment.SetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_DATA_ROOT", Path.Combine(dataRoot, "framework-client"));

        var sessionCoordinator = new SessionCoordinator();
        var session = new SessionRef("campaign-p3d-a1-client", "timeline-p3d-a1-client", "session-p3d-a1-client");
        var leaseResult = sessionCoordinator.BeginSession(session);
        Require(leaseResult.IsSuccess, "framework_client_session_start_failed");
        var context = new RequestContext(new ExtensionId("p3d-a1-framework-client"), leaseResult.Value, "p3d-a1-framework-client-correlation", DateTimeOffset.UtcNow.AddMinutes(1));
        var clientOptions = new RuntimeServiceClientOptions(servicePath: servicePath, requestedCapabilities: new[]
        {
            ProtocolConstants.MessageTypeHealth,
            ProtocolConstants.MessageTypeCancel,
            ProtocolConstants.CapabilityProviderConfigureV1,
            ProtocolConstants.CapabilityProviderModelsV1,
            ProtocolConstants.CapabilityProviderCompleteV1
        });
        var client = new RuntimeServiceClient(clientOptions);
        using var operationCancellation = new CancellationTokenSource();
        try
        {
            var started = await client.StartAsync(new RuntimeServiceStartRequest(ProtocolConstants.ServiceId, new ApiVersion(2, 0), ProtocolConstants.MaxFrameBytes), context, operationCancellation.Token).ConfigureAwait(false);
            Require(started.IsSuccess && started.Value.IsReady, "framework_client_start_failed");
            Require(client.SupportsAiBusinessFrames, "framework_client_provider_capability_missing");
            CaptureFrameworkServiceProvenance(client);
            var profile = new ProviderProfileRequest("profile.p3d-a1", "provider.p3d-a1", "route.p3d-a1", "openai_compatible", "http://127.0.0.1:" + fakeServer.Port.ToString() + "/v1/", "fake-model", "credential.p3d_a1", true);
            var profileResult = await client.UpsertProfileAsync(profile, context, operationCancellation.Token).ConfigureAwait(false);
            Require(profileResult.IsSuccess && profileResult.Value.Status == "ready", "framework_client_profile_failed:" + DescribeError(profileResult.Error));

            await RunCaseAsync("P3D-A1-C01-unary_success_publishes_accepted_started_completed", async () =>
            {
                var request = new AiTaskRequest("client-completion-task", "client-completion-message", "route.p3d-a1", "provider.p3d-a1", "profile.p3d-a1", "{\"messages\":[{\"role\":\"user\",\"content\":\"say hello\"}]}", new SchemaRef(ProtocolConstants.ProviderResultSchemaV1, new ApiVersion(1, 0)), "public", DateTimeOffset.UtcNow.AddSeconds(30), false, new RuntimeResourceBudget(32768, 65536, 2048, 256), "client-completion-idempotency");
                var submitted = await client.SubmitAsync(request, context, operationCancellation.Token).ConfigureAwait(false);
                Require(submitted.IsSuccess, "framework_client_submit_failed:" + (submitted.Error == null ? string.Empty : submitted.Error.Code));
                var handle = submitted.Value;
                for (var attempt = 0; attempt < 100 && handle.Snapshot().All(item => item.Kind != AiTaskEventKind.Completed && item.Kind != AiTaskEventKind.Failed && item.Kind != AiTaskEventKind.Cancelled); attempt++) await Task.Delay(10).ConfigureAwait(false);
                var events = handle.Snapshot();
                Require(events.Count == 5, "framework_client_event_count_invalid:" + events.Count.ToString());
                Require(events[0].Kind == AiTaskEventKind.Accepted && events[1].Kind == AiTaskEventKind.Started && events[2].Kind == AiTaskEventKind.TextDelta && events[3].Kind == AiTaskEventKind.UsageUpdate && events[4].Kind == AiTaskEventKind.Completed, "framework_client_event_sequence_invalid:" + DescribeEvents(events));
                Require(events[2].Text == "fake reply" && events[4].Text == string.Empty && events[4].ResolvedModel == "fake-model", "framework_client_completion_result_invalid");
                handle.Dispose();
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A1-C02-provider_error_maps_to_framework_error", async () =>
            {
                fakeServer.ResponseStatusCode = 401;
                try
                {
                    var request = new AiTaskRequest("client-provider-error-task", "client-provider-error-message", "route.p3d-a1", "provider.p3d-a1", "profile.p3d-a1", "{\"messages\":[{\"role\":\"user\",\"content\":\"trigger provider error\"}]}", new SchemaRef(ProtocolConstants.ProviderResultSchemaV1, new ApiVersion(1, 0)), "public", DateTimeOffset.UtcNow.AddSeconds(30), false, new RuntimeResourceBudget(32768, 65536, 2048, 256), "client-provider-error-idempotency");
                    var submitted = await client.SubmitAsync(request, context, operationCancellation.Token).ConfigureAwait(false);
                    Require(submitted.IsSuccess, "framework_client_provider_error_submit_failed:" + DescribeError(submitted.Error));
                    var handle = submitted.Value;
                    try
                    {
                        for (var attempt = 0; attempt < 100 && handle.Snapshot().All(item => item.Kind != AiTaskEventKind.Completed && item.Kind != AiTaskEventKind.Failed && item.Kind != AiTaskEventKind.Cancelled); attempt++) await Task.Delay(10).ConfigureAwait(false);
                        var events = handle.Snapshot();
                        Require(events.Count == 3, "framework_client_provider_error_event_count_invalid:" + events.Count.ToString() + ":" + DescribeEvents(events));
                        Require(events[0].Kind == AiTaskEventKind.Accepted && events[1].Kind == AiTaskEventKind.Started && events[2].Kind == AiTaskEventKind.Failed, "framework_client_provider_error_event_sequence_invalid:" + DescribeEvents(events));
                        Require(events[2].Error != null && events[2].Error.Code == "provider.http.authentication_failed", "framework_client_provider_error_code_invalid:" + DescribeError(events[2].Error));
                        Require(events[2].Error.Category == FrameworkErrorCategory.Denied, "framework_client_provider_error_category_invalid:" + events[2].Error.Category.ToString());
                        Require(events.All(item => item.Kind != AiTaskEventKind.Completed), "framework_client_provider_error_late_completion");
                    }
                    finally
                    {
                        handle.Dispose();
                    }
                }
                finally
                {
                    fakeServer.ResponseStatusCode = 200;
                }
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A1-C03-structured_json_and_usage_bounds", async () =>
            {
                fakeServer.ResponseBodyOverride = "{\"id\":\"fake-structured\",\"model\":\"fake-model\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"answer\\\":\\\"ok\\\"}\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":2,\"completion_tokens\":3}}";
                try
                {
                    var request = new AiTaskRequest("client-structured-task", "client-structured-message", "route.p3d-a1", "provider.p3d-a1", "profile.p3d-a1", "{\"messages\":[{\"role\":\"user\",\"content\":\"return structured\"}],\"response_schema_json\":\"{\\\"type\\\":\\\"object\\\",\\\"properties\\\":{\\\"answer\\\":{\\\"type\\\":\\\"string\\\"}}}\"}", new SchemaRef(ProtocolConstants.ProviderResultSchemaV1, new ApiVersion(1, 0)), "public", DateTimeOffset.UtcNow.AddSeconds(30), false, new RuntimeResourceBudget(32768, 65536, 2048, 256), "client-structured-idempotency");
                    var submitted = await client.SubmitAsync(request, context, operationCancellation.Token).ConfigureAwait(false);
                    Require(submitted.IsSuccess, "framework_client_structured_submit_failed:" + DescribeError(submitted.Error));
                    var handle = submitted.Value;
                    try
                    {
                        for (var attempt = 0; attempt < 100 && handle.Snapshot().All(item => item.Kind != AiTaskEventKind.Completed && item.Kind != AiTaskEventKind.Failed && item.Kind != AiTaskEventKind.Cancelled); attempt++) await Task.Delay(10).ConfigureAwait(false);
                        var events = handle.Snapshot();
                        Require(events.Count == 5, "framework_client_structured_event_count_invalid:" + events.Count.ToString() + ":" + DescribeEvents(events));
                        var usage = events.Single(item => item.Kind == AiTaskEventKind.UsageUpdate);
                        var completed = events.Single(item => item.Kind == AiTaskEventKind.Completed);
                        Require(usage.InputTokens == 2 && usage.OutputTokens == 3, "framework_client_usage_invalid:" + usage.InputTokens.ToString() + ":" + usage.OutputTokens.ToString());
                        Require(completed.StructuredJson == "{\"answer\":\"ok\"}", "framework_client_structured_json_invalid:" + completed.StructuredJson);
                        Require(completed.InputTokens == 2 && completed.OutputTokens == 3, "framework_client_completed_usage_invalid");
                        Require(events.All(item => item.Kind != AiTaskEventKind.Failed && item.Kind != AiTaskEventKind.Cancelled), "framework_client_structured_terminal_invalid:" + DescribeEvents(events));
                    }
                    finally
                    {
                        handle.Dispose();
                    }
                }
                finally
                {
                    fakeServer.ResponseBodyOverride = string.Empty;
                }
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A1-C06-client_uses_real_provider_business_frame", async () =>
            {
                var requestCountBefore = fakeServer.RequestCount;
                var request = new AiTaskRequest("client-real-provider-task", "client-real-provider-message", "route.p3d-a1", "provider.p3d-a1", "profile.p3d-a1", "{\"messages\":[{\"role\":\"user\",\"content\":\"inspect business frame\"}]}", new SchemaRef(ProtocolConstants.ProviderResultSchemaV1, new ApiVersion(1, 0)), "public", DateTimeOffset.UtcNow.AddSeconds(30), false, new RuntimeResourceBudget(32768, 65536, 2048, 256), "client-real-provider-idempotency");
                var submitted = await client.SubmitAsync(request, context, operationCancellation.Token).ConfigureAwait(false);
                Require(submitted.IsSuccess, "framework_client_real_provider_submit_failed:" + DescribeError(submitted.Error));
                var handle = submitted.Value;
                try
                {
                    await fakeServer.WaitForRequestCountAsync(requestCountBefore + 1, 5000).ConfigureAwait(false);
                    for (var attempt = 0; attempt < 100 && handle.Snapshot().All(item => item.Kind != AiTaskEventKind.Completed && item.Kind != AiTaskEventKind.Failed && item.Kind != AiTaskEventKind.Cancelled); attempt++) await Task.Delay(10).ConfigureAwait(false);
                    var events = handle.Snapshot();
                    Require(events.Any(item => item.Kind == AiTaskEventKind.Completed), "framework_client_real_provider_not_completed:" + DescribeEvents(events));
                    Require(fakeServer.LastRequestText.StartsWith("POST /v1/chat/completions ", StringComparison.Ordinal), "framework_client_provider_path_invalid");
                    Require(fakeServer.LastRequestText.Contains("\"model\":\"fake-model\"", StringComparison.Ordinal), "framework_client_provider_model_missing");
                    Require(fakeServer.LastRequestText.Contains("\"messages\"", StringComparison.Ordinal), "framework_client_provider_messages_missing");
                }
                finally
                {
                    handle.Dispose();
                }
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A1-C04-caller_cancel_maps_to_cancelled_terminal", async () =>
            {
                var requestCountBefore = fakeServer.RequestCount;
                var responseCountBefore = fakeServer.ResponseCount;
                fakeServer.DelayMilliseconds = 1000;
                try
                {
                    var request = new AiTaskRequest("client-cancel-task", "client-cancel-message", "route.p3d-a1", "provider.p3d-a1", "profile.p3d-a1", "{\"messages\":[{\"role\":\"user\",\"content\":\"cancel me\"}]}", new SchemaRef(ProtocolConstants.ProviderResultSchemaV1, new ApiVersion(1, 0)), "public", DateTimeOffset.UtcNow.AddSeconds(30), false, new RuntimeResourceBudget(32768, 65536, 2048, 256), "client-cancel-idempotency");
                    var submitted = await client.SubmitAsync(request, context, operationCancellation.Token).ConfigureAwait(false);
                    Require(submitted.IsSuccess, "framework_client_cancel_submit_failed:" + DescribeError(submitted.Error));
                    var handle = submitted.Value;
                    try
                    {
                        await fakeServer.WaitForRequestCountAsync(requestCountBefore + 1, 5000).ConfigureAwait(false);
                        using var cancelTimeout = new CancellationTokenSource(5000);
                        var cancelled = await handle.CancelAsync(cancelTimeout.Token).ConfigureAwait(false);
                        Require(cancelled.IsSuccess && cancelled.Value, "framework_client_cancel_request_failed:" + DescribeError(cancelled.Error));
                        for (var attempt = 0; attempt < 100 && handle.Snapshot().All(item => item.Kind != AiTaskEventKind.Completed && item.Kind != AiTaskEventKind.Failed && item.Kind != AiTaskEventKind.Cancelled); attempt++) await Task.Delay(10).ConfigureAwait(false);
                        var events = handle.Snapshot();
                        Require(events.Count == 3, "framework_client_cancel_event_count_invalid:" + events.Count.ToString() + ":" + DescribeEvents(events));
                        Require(events[0].Kind == AiTaskEventKind.Accepted && events[1].Kind == AiTaskEventKind.Started && events[2].Kind == AiTaskEventKind.Cancelled, "framework_client_cancel_event_sequence_invalid:" + DescribeEvents(events));
                        Require(events.All(item => item.Kind != AiTaskEventKind.Completed), "framework_client_cancel_late_completion");
                        await Task.Delay(1500).ConfigureAwait(false);
                        Require(fakeServer.ResponseCount == responseCountBefore, "framework_client_provider_completed_after_cancel");
                    }
                    finally
                    {
                        handle.Dispose();
                    }
                }
                finally
                {
                    fakeServer.DelayMilliseconds = 0;
                }
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A1-C05-service_unavailable_preserves_degraded_mode", async () =>
            {
                var missingServicePath = Path.Combine(Path.GetDirectoryName(servicePath)!, "missing-p3d-a1-service.exe");
                using var unavailableClient = new RuntimeServiceClient(new RuntimeServiceClientOptions(servicePath: missingServicePath, requestedCapabilities: new[]
                {
                    ProtocolConstants.MessageTypeHealth,
                    ProtocolConstants.MessageTypeCancel,
                    ProtocolConstants.CapabilityProviderCompleteV1
                }));
                var unavailableStart = await unavailableClient.StartAsync(new RuntimeServiceStartRequest(ProtocolConstants.ServiceId, new ApiVersion(2, 0), ProtocolConstants.MaxFrameBytes), context, operationCancellation.Token).ConfigureAwait(false);
                Require(!unavailableStart.IsSuccess, "framework_client_unavailable_start_succeeded");
                Require(unavailableStart.Error != null && unavailableStart.Error.Category == FrameworkErrorCategory.Unavailable, "framework_client_unavailable_error_invalid:" + DescribeError(unavailableStart.Error));
                Require(!unavailableClient.SupportsAiBusinessFrames, "framework_client_unavailable_exposed_business_capability");
            }).ConfigureAwait(false);
        }
        finally
        {
            client.Dispose();
            Environment.SetEnvironmentVariable("MARCUS_AWAKE_PROVIDER_ASSEMBLY_PATH", oldProviderAssemblyPath);
            Environment.SetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_CREDENTIAL_ROOT", oldCredentialRoot);
            Environment.SetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_DATA_ROOT", oldDataRoot);
        }
    }

    private async Task RunCaseAsync(string name, Func<Task> test)
    {
        var requestCountBefore = evidenceFakeServer == null ? 0 : evidenceFakeServer.RequestCount;
        var responseCountBefore = evidenceFakeServer == null ? 0 : evidenceFakeServer.ResponseCount;
        try
        {
            await test().ConfigureAwait(false);
            Console.WriteLine("PASS " + name);
            passed++;
            RecordCaseObservation(name, true, requestCountBefore, responseCountBefore, string.Empty);
        }
        catch (Exception exception)
        {
            Console.WriteLine("FAIL " + name + " " + exception.GetType().Name);
            failed++;
            RecordCaseObservation(name, false, requestCountBefore, responseCountBefore, exception.GetType().Name);
        }
    }

    private void RecordCaseObservation(string id, bool passedCase, int requestCountBefore, int responseCountBefore, string errorType)
    {
        var requestCountAfter = evidenceFakeServer == null ? requestCountBefore : evidenceFakeServer.RequestCount;
        var responseCountAfter = evidenceFakeServer == null ? responseCountBefore : evidenceFakeServer.ResponseCount;
        var protocol = DescribeCase(id);
        var serviceExecutable = string.IsNullOrWhiteSpace(currentServiceExecutable) ? ResolveServicePath() : currentServiceExecutable;
        var servicePid = currentServicePid > 0 ? currentServicePid : primaryServicePid;
        var pipeName = string.IsNullOrWhiteSpace(currentPipeName) ? primaryPipeName : currentPipeName;
        var capturePath = "pending:p3d-a1-fake-http-capture";
        var observation = string.Join("|", new[]
        {
            id,
            passedCase ? "pass" : "fail",
            requestCountBefore.ToString(),
            requestCountAfter.ToString(),
            responseCountBefore.ToString(),
            responseCountAfter.ToString(),
            protocol.RequestMessageType,
            protocol.ResponseMessageType,
            protocol.LedgerStateBefore,
            protocol.LedgerStateAfter,
            protocol.CancelObserved ? "cancelled" : "not_cancelled",
            protocol.WriteOutcome,
            protocol.ReplayOutcome
        });
        caseObservations.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["status"] = passedCase ? "pass" : "fail",
            ["process_role"] = "service_child",
            ["runner_executable"] = Environment.ProcessPath ?? "MarcusAwakeRuntimeService.Tests.exe",
            ["runner_arguments"] = new[] { "--p3d-a1" },
            ["service_executable"] = serviceExecutable,
            ["service_arguments"] = currentServiceArguments.ToArray(),
            ["service_pid"] = servicePid,
            ["private_pipe_name"] = pipeName,
            ["fake_http_bind"] = evidenceFakeServer == null ? "http://127.0.0.1:0/" : "http://127.0.0.1:" + evidenceFakeServer.Port.ToString() + "/",
            ["fake_http_capture_path"] = string.IsNullOrWhiteSpace(evidenceFakeHttpCapturePath) ? capturePath : evidenceFakeHttpCapturePath,
            ["provider_http_request_count"] = requestCountAfter,
            ["request_message_type"] = protocol.RequestMessageType,
            ["response_message_type"] = protocol.ResponseMessageType,
            ["provider_call_count"] = Math.Max(0, requestCountAfter - requestCountBefore),
            ["http_request_count"] = Math.Max(0, requestCountAfter - requestCountBefore),
            ["ledger_state_before"] = protocol.LedgerStateBefore,
            ["ledger_state_after"] = protocol.LedgerStateAfter,
            ["cancel_observed"] = protocol.CancelObserved,
            ["write_outcome"] = protocol.WriteOutcome,
            ["replay_outcome"] = protocol.ReplayOutcome,
            ["observation_hash"] = TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes(observation)),
        });
    }

    private void WriteFakeHttpCapture(FakeProviderHttpServer fakeServer)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = "marcus-awake.p3d-a1-fake-http-capture.v1",
            ["observations"] = fakeServer.GetBoundedRequestObservations().ToArray()
        };
        var captureDirectory = Path.GetDirectoryName(evidenceFakeHttpCapturePath);
        if (!string.IsNullOrWhiteSpace(captureDirectory)) Directory.CreateDirectory(captureDirectory);
        File.WriteAllText(evidenceFakeHttpCapturePath, JsonSerializer.Serialize(payload), new UTF8Encoding(false));
    }

    private static (string RequestMessageType, string ResponseMessageType, string LedgerStateBefore, string LedgerStateAfter, bool CancelObserved, string WriteOutcome, string ReplayOutcome) DescribeCase(string id)
    {
        if (id.StartsWith("P3D-A1-S01-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderProfileUpsertV1, ProtocolConstants.MessageTypeProviderProfileResult, "none", "response_published", false, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-S02-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderProfileRemoveV1, ProtocolConstants.MessageTypeProviderProfileResult, "admitted", "response_published", false, "frame_complete", "idempotent_remove");
        if (id.StartsWith("P3D-A1-S03-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderModelsV1, ProtocolConstants.MessageTypeProviderModelsResult, "none", "response_published", false, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-S04-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeProviderResult, "none", "response_published", false, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-S05-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderModelsV1, ProtocolConstants.MessageTypeError, "none", "failed", false, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-S06-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeError, "provider_in_flight", "failed", false, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-S07-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderModelsV1, ProtocolConstants.MessageTypeError, "none", "rejected", false, "no_response_closed", "none");
        if (id.StartsWith("P3D-A1-S08-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderModelsV1, ProtocolConstants.MessageTypeError, "none", "rejected", false, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-S09-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeProviderResult, "response_published", "terminal_replay", false, "frame_complete", "same_result_no_recall");
        if (id.StartsWith("P3D-A1-S10-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeError, "provider_in_flight", "failed", false, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-S11-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeProviderResult, "captured", "redaction_verified", false, "not_applicable", "none");
        if (id.StartsWith("P3D-A1-S12-", StringComparison.Ordinal)) return ("shutdown", "shutdown_ack", "admitted", "stopped", false, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-S13-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeError, "admitted", "cancelled", true, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-S14-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeError, "provider_in_flight", "cancelled", true, "frame_complete", "late_completion_rejected");
        if (id.StartsWith("P3D-A1-S15-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeProviderResult, "provider_in_flight", "terminal_replay", false, "first_byte_suppressed", "same_result_no_recall");
        if (id.StartsWith("P3D-A1-S16-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeProviderResult, "response_published", "terminal_replay", false, "frame_complete", "same_result_no_recall");
        if (id.StartsWith("P3D-A1-S17-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeError, "response_published", "failed", false, "frame_complete", "conflict_no_recall");
        if (id.StartsWith("P3D-A1-C01-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeProviderResult, "none", "response_published", false, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-C02-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeError, "provider_in_flight", "failed", false, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-C03-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeProviderResult, "none", "response_published", false, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-C04-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeError, "provider_in_flight", "cancelled", true, "frame_complete", "none");
        if (id.StartsWith("P3D-A1-C05-", StringComparison.Ordinal)) return ("runtime.start", ProtocolConstants.MessageTypeError, "none", "unavailable", false, "no_response", "none");
        if (id.StartsWith("P3D-A1-C06-", StringComparison.Ordinal)) return (ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.MessageTypeProviderResult, "none", "response_published", false, "frame_complete", "none");
        return ("unknown", "unknown", "unknown", "unknown", false, "unknown", "unknown");
    }

    private void CaptureFrameworkServiceProvenance(RuntimeServiceClient client)
    {
        var processField = typeof(RuntimeServiceClient).GetField("process", BindingFlags.Instance | BindingFlags.NonPublic);
        if (processField?.GetValue(client) is Process process && !process.HasExited) currentServicePid = process.Id;
        var connectionField = typeof(RuntimeServiceClient).GetField("connection", BindingFlags.Instance | BindingFlags.NonPublic);
        var clientConnection = connectionField?.GetValue(client);
        var descriptorField = clientConnection?.GetType().GetField("descriptor", BindingFlags.Instance | BindingFlags.NonPublic);
        var descriptor = descriptorField?.GetValue(clientConnection);
        var pipeProperty = descriptor?.GetType().GetProperty("PipeName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (pipeProperty?.GetValue(descriptor) is string pipeName && !string.IsNullOrWhiteSpace(pipeName)) currentPipeName = pipeName;
        currentServiceExecutable = string.IsNullOrWhiteSpace(currentServiceExecutable) ? ResolveServicePath() : currentServiceExecutable;
        currentServiceArguments = Array.Empty<string>();
    }

    private static async Task<ServiceConnection> ConnectControlAsync(LaunchedService service, string targetSessionId)
    {
        var controlSessionId = targetSessionId + ".cancel." + Guid.NewGuid().ToString("N");
        return await service.ConnectNextAsync(controlSessionId, new[]
        {
            ProtocolConstants.MessageTypeHealth,
            ProtocolConstants.MessageTypeCancel
        }).ConfigureAwait(false);
    }

    private static FrameBuildResult CreateControlCancelFrame(ServiceConnection controlConnection, FrameBuildResult target, string messageId)
    {
        if (target.Envelope.TaskScope == null) throw new InvalidOperationException("cancel_target_scope_missing");
        var payload = "{\"schema\":\"marcus-awake.cancel.v1\",\"task_id\":" + JsonSerializer.Serialize(target.Envelope.TaskScope.TaskId) + ",\"target_session_id\":" + JsonSerializer.Serialize(target.Envelope.SessionId) + "}";
        var frame = controlConnection.CreateFrame(
            ProtocolConstants.MessageTypeCancel,
            messageId,
            payload,
            null,
            target.Envelope.CampaignGuid,
            target.Envelope.TimelineId,
            target.Envelope.SessionGeneration,
            CloneTaskScope(target.Envelope.TaskScope),
            target.Envelope.OwnerId);
        frame.Envelope.TaskScope!.MessageId = messageId;
        frame.Envelope.CausationId = "cause-" + messageId;
        frame.Envelope.FenceProof = TransportSecurity.ComputeFrameFenceProof(controlConnection.FrameKey, frame.Envelope);
        return new FrameBuildResult(frame.Envelope, ProtocolCodec.SerializeEnvelope(frame.Envelope));
    }

    private static TaskScopeEnvelope CloneTaskScope(TaskScopeEnvelope source)
    {
        return new TaskScopeEnvelope
        {
            TaskId = source.TaskId,
            MessageId = source.MessageId,
            OwnerId = source.OwnerId,
            RouteId = source.RouteId,
            ProviderId = source.ProviderId,
            ProfileId = source.ProfileId,
            IdempotencyKey = source.IdempotencyKey,
            RequestPayloadHash = source.RequestPayloadHash,
            OutputSchemaId = source.OutputSchemaId,
            OutputSchemaMajor = source.OutputSchemaMajor,
            OutputSchemaMinor = source.OutputSchemaMinor,
            SettlementRequirement = source.SettlementRequirement
        };
    }

    private static void AssertCancelRequested(PipeEnvelope response)
    {
        Require(response.MessageType == ProtocolConstants.MessageTypeCancelAck, "cancel_response_type_invalid");
        Require(response.PayloadSchema == "marcus-awake.cancel.v1", "cancel_response_schema_invalid");
        using var document = JsonDocument.Parse(response.PayloadJson);
        Require(document.RootElement.GetProperty("status").GetString() == "cancel_requested", "cancel_status_invalid");
    }

    private async Task RunDeadlineAdmissionCaseAsync(string servicePath, string providerAssemblyPath, string credentialRoot, string dataRoot, FakeProviderHttpServer fakeServer)
    {
        var service = await LaunchedService.StartAsync(
            servicePath,
            4104,
            Path.Combine(dataRoot, "deadline"),
            AllCapabilities(),
            environmentOverrides: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MARCUS_AWAKE_PROVIDER_ASSEMBLY_PATH"] = providerAssemblyPath,
                ["MARCUS_AWAKE_RUNTIME_CREDENTIAL_ROOT"] = credentialRoot
            }).ConfigureAwait(false);
        currentServiceExecutable = servicePath;
        currentServicePid = service.Process.Id;
        currentPipeName = service.Descriptor.PipeName;
        currentServiceArguments = service.CommandLineArguments;
        var connection = await service.ConnectAsync(service.Descriptor.ChallengeId, "p3d-a1-deadline-session", AllCapabilities()).ConfigureAwait(false);
        try
        {
            var before = fakeServer.RequestCount;
            var expiredFrame = CreateProviderFrame(
                connection,
                ProtocolConstants.MessageTypeProviderModelsV1,
                "a1-expired-models",
                "a1-expired-models-task",
                ScopePayload(ProtocolConstants.ProviderModelsSchemaV1),
                "a1-expired-models-idem",
                ProtocolConstants.ProviderModelsResultSchemaV1);
            var rewritten = connection.RewriteFrame(expiredFrame, envelope => envelope.DeadlineUnixMilliseconds = DateTimeOffset.UtcNow.AddMilliseconds(-100).ToUnixTimeMilliseconds());
            await connection.SendRawAsync(rewritten.RawJson).ConfigureAwait(false);
            Require(await connection.DisconnectedAsync().ConfigureAwait(false), "expired_deadline_connection_not_closed");
            Require(fakeServer.RequestCount == before, "expired_deadline_called_provider");
        }
        finally
        {
            connection.Dispose();
            await service.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task RunChildShutdownCaseAsync(string servicePath, string providerAssemblyPath, string credentialRoot, string dataRoot)
    {
        var service = await LaunchedService.StartAsync(
            servicePath,
            4102,
            Path.Combine(dataRoot, "shutdown"),
            AllCapabilities(),
            environmentOverrides: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MARCUS_AWAKE_PROVIDER_ASSEMBLY_PATH"] = providerAssemblyPath,
                ["MARCUS_AWAKE_RUNTIME_CREDENTIAL_ROOT"] = credentialRoot
            }).ConfigureAwait(false);
        var processId = service.Process.Id;
        currentServiceExecutable = servicePath;
        currentServicePid = processId;
        currentPipeName = service.Descriptor.PipeName;
        currentServiceArguments = service.CommandLineArguments;
        var connection = await service.ConnectAsync(service.Descriptor.ChallengeId, "p3d-a1-shutdown-session", AllCapabilities()).ConfigureAwait(false);
        try
        {
            var shutdown = connection.CreateFrame("shutdown", "a1-shutdown", "{}", null, "campaign-p3d-a1", "timeline-p3d-a1", 1);
            var response = await connection.SendAndReadAsync(shutdown).ConfigureAwait(false);
            Require(response.MessageType == "shutdown_ack", "shutdown_ack_missing");
        }
        finally
        {
            connection.Dispose();
        }

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!service.Process.HasExited && DateTime.UtcNow < deadline) await Task.Delay(25).ConfigureAwait(false);
        var exited = service.Process.HasExited;
        await service.DisposeAsync().ConfigureAwait(false);
        Require(exited, "service_shutdown_did_not_exit");
        try
        {
            using var residual = Process.GetProcessById(processId);
            Require(residual.HasExited, "service_shutdown_left_orphan");
        }
        catch (ArgumentException)
        {
        }
    }

    private async Task RunWriteFailureReplayCaseAsync(string servicePath, string providerAssemblyPath, string credentialRoot, string dataRoot, FakeProviderHttpServer fakeServer)
    {
        const string suppressedMessageId = "a1-write-suppressed";
        var service = await LaunchedService.StartAsync(
            servicePath,
            4103,
            Path.Combine(dataRoot, "write-replay"),
            AllCapabilities(),
            environmentOverrides: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MARCUS_AWAKE_RUNTIME_TEST_MODE"] = "p3d-a1",
                ["MARCUS_AWAKE_TEST_SUPPRESS_BEFORE_FIRST_BYTE_MESSAGE_ID"] = suppressedMessageId,
                ["MARCUS_AWAKE_PROVIDER_ASSEMBLY_PATH"] = providerAssemblyPath,
                ["MARCUS_AWAKE_RUNTIME_CREDENTIAL_ROOT"] = credentialRoot
            }).ConfigureAwait(false);
        const string sessionId = "p3d-a1-write-replay-session";
        ServiceConnection? connection = await service.ConnectAsync(service.Descriptor.ChallengeId, sessionId, AllCapabilities()).ConfigureAwait(false);
        currentServiceExecutable = servicePath;
        currentServicePid = service.Process.Id;
        currentPipeName = service.Descriptor.PipeName;
        currentServiceArguments = service.CommandLineArguments;
        try
        {
            var profile = await connection.SendAndReadAsync(CreateProviderFrame(
                connection,
                ProtocolConstants.MessageTypeProviderProfileUpsertV1,
                "a1-write-profile",
                "a1-write-profile-task",
                ProfilePayload(fakeServer.Port),
                "a1-write-profile-idem",
                ProtocolConstants.ProviderProfileResultSchemaV1)).ConfigureAwait(false);
            AssertSuccess(profile, "provider_profile_result", ProtocolConstants.ProviderProfileResultSchemaV1);

            var beforeRequests = fakeServer.RequestCount;
            var beforeResponses = fakeServer.ResponseCount;
            var first = CreateProviderFrame(
                connection,
                ProtocolConstants.MessageTypeProviderCompleteV1,
                suppressedMessageId,
                "a1-write-replay-task",
                CompletionPayload("write failure replay"),
                "a1-write-replay-idem",
                ProtocolConstants.ProviderResultSchemaV1);
            await connection.SendRawAsync(first.RawJson).ConfigureAwait(false);
            await fakeServer.WaitForRequestCountAsync(beforeRequests + 1, 5000).ConfigureAwait(false);
            await fakeServer.WaitForResponseCountAsync(beforeResponses + 1, 5000).ConfigureAwait(false);
            Require(await connection.DisconnectedAsync().ConfigureAwait(false), "write_failure_replay_connection_stayed_open");
            connection.Dispose();
            connection = null;

            connection = await service.ConnectNextAsync(sessionId, AllCapabilities()).ConfigureAwait(false);
            PipeEnvelope? response = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                var replay = CreateProviderFrame(
                    connection,
                    ProtocolConstants.MessageTypeProviderCompleteV1,
                    "a1-write-replay-retry-" + attempt.ToString(),
                    "a1-write-replay-task",
                    CompletionPayload("write failure replay"),
                    "a1-write-replay-idem",
                    ProtocolConstants.ProviderResultSchemaV1);
                response = await connection.SendAndReadAsync(replay).ConfigureAwait(false);
                if (response.MessageType == "provider_result") break;
                Require(response.MessageType == ProtocolConstants.MessageTypeError && response.ErrorCode == "provider.task_in_progress", "write_replay_unexpected_intermediate_response");
                await Task.Delay(25).ConfigureAwait(false);
            }
            Require(response != null, "write_replay_response_missing");
            AssertSuccess(response!, "provider_result", ProtocolConstants.ProviderResultSchemaV1);
            Require(GetString(response!, "content") == "fake reply", "write_replay_content_invalid");
            Require(fakeServer.RequestCount == beforeRequests + 1, "write_replay_recalled_provider");
        }
        finally
        {
            connection?.Dispose();
            await service.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static FrameBuildResult CreateProviderFrame(
        ServiceConnection connection,
        string messageType,
        string messageId,
        string taskId,
        string payload,
        string idempotencyKey,
        string outputSchema,
        string profileId = "profile.p3d-a1",
        string providerId = "provider.p3d-a1",
        string routeId = "route.p3d-a1",
        string campaignGuid = "campaign-p3d-a1",
        string timelineId = "timeline-p3d-a1",
        string ownerId = "p3d-a1-harness",
        long sessionGeneration = 1)
    {
        var canonicalPayload = CanonicalizePayload(payload);
        var payloadHash = TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes(canonicalPayload));
        var taskScope = new TaskScopeEnvelope
        {
            TaskId = taskId,
            MessageId = messageId,
            OwnerId = ownerId,
            RouteId = routeId,
            ProviderId = providerId,
            ProfileId = profileId,
            IdempotencyKey = idempotencyKey,
            RequestPayloadHash = payloadHash,
            OutputSchemaId = outputSchema,
            OutputSchemaMajor = 1,
            OutputSchemaMinor = 0,
            SettlementRequirement = "not_applicable"
        };
        var frame = connection.CreateFrame(messageType, messageId, canonicalPayload, null, campaignGuid, timelineId, sessionGeneration, taskScope, ownerId);
        frame.Envelope.PayloadSchema = messageType switch
        {
            ProtocolConstants.MessageTypeProviderProfileUpsertV1 => ProtocolConstants.ProviderProfileUpsertSchemaV1,
            ProtocolConstants.MessageTypeProviderProfileRemoveV1 => ProtocolConstants.ProviderProfileRemoveSchemaV1,
            ProtocolConstants.MessageTypeProviderModelsV1 => ProtocolConstants.ProviderModelsSchemaV1,
            ProtocolConstants.MessageTypeProviderCompleteV1 => ProtocolConstants.ProviderCompleteSchemaV1,
            _ => throw new InvalidOperationException("provider_message_type_invalid")
        };
        frame.Envelope.CausationId = "cause-" + messageId;
        frame.Envelope.FenceProof = TransportSecurity.ComputeFrameFenceProof(connection.FrameKey, frame.Envelope);
        return new FrameBuildResult(frame.Envelope, ProtocolCodec.SerializeEnvelope(frame.Envelope));
    }

    private static string ProfilePayload(int port, string profileId = "profile.p3d-a1", string providerId = "provider.p3d-a1", string routeId = "route.p3d-a1", string credentialReference = "credential.p3d_a1", bool isCloud = true)
    {
        return "{\"schema\":\"" + ProtocolConstants.ProviderProfileUpsertSchemaV1 + "\",\"profile_id\":" + JsonSerializer.Serialize(profileId) + ",\"provider_id\":" + JsonSerializer.Serialize(providerId) + ",\"route_id\":" + JsonSerializer.Serialize(routeId) + ",\"provider_kind\":\"openai_compatible\",\"base_url\":\"http://127.0.0.1:" + port.ToString() + "/v1/\",\"default_model\":\"fake-model\",\"credential_reference\":" + JsonSerializer.Serialize(credentialReference) + ",\"is_cloud\":" + (isCloud ? "true" : "false") + "}";
    }

    private static string ScopePayload(string schema, string profileId = "profile.p3d-a1", string providerId = "provider.p3d-a1", string routeId = "route.p3d-a1")
    {
        return "{\"schema\":\"" + schema + "\",\"profile_id\":" + JsonSerializer.Serialize(profileId) + ",\"provider_id\":" + JsonSerializer.Serialize(providerId) + ",\"route_id\":" + JsonSerializer.Serialize(routeId) + "}";
    }

    private static string CompletionPayload(string content = "say hello", string profileId = "profile.p3d-a1", string providerId = "provider.p3d-a1", string routeId = "route.p3d-a1")
    {
        return "{\"schema\":\"" + ProtocolConstants.ProviderCompleteSchemaV1 + "\",\"profile_id\":" + JsonSerializer.Serialize(profileId) + ",\"provider_id\":" + JsonSerializer.Serialize(providerId) + ",\"route_id\":" + JsonSerializer.Serialize(routeId) + ",\"messages\":[{\"role\":\"user\",\"content\":" + JsonSerializer.Serialize(content) + "}]}";
    }

    private static string[] AllCapabilities()
    {
        var values = new List<string>
        {
            ProtocolConstants.MessageTypeHealth,
            ProtocolConstants.MessageTypeEcho,
            ProtocolConstants.MessageTypeCancel,
            ProtocolConstants.MessageTypeDiagnostic,
            ProtocolConstants.MessageTypeShutdown,
            ProtocolConstants.CapabilityStorageRead,
            ProtocolConstants.CapabilityStorageWrite,
            ProtocolConstants.CapabilityRagRead,
            ProtocolConstants.CapabilityRagWrite
        };
        values.AddRange(ProviderCapabilities);
        values.Add(ProtocolConstants.CapabilityProviderStreamV1);
        return values.ToArray();
    }

    private static void AssertSuccess(PipeEnvelope response, string messageType, string schema)
    {
        Require(response.MessageType == messageType, "response_message_type_invalid:" + response.MessageType + ":" + response.PayloadSchema + ":" + response.ErrorCode + ":" + response.PayloadJson);
        Require(response.PayloadSchema == schema, "response_schema_invalid:" + response.PayloadSchema + ":" + response.PayloadJson);
        Require(response.ErrorCode == string.Empty, "success_response_contains_error:" + response.ErrorCode + ":" + response.PayloadJson);
    }

    private static void AssertError(PipeEnvelope response, string errorCode, string category)
    {
        Require(response.MessageType == ProtocolConstants.MessageTypeError, "error_message_type_invalid:" + response.MessageType + ":" + response.ErrorCode + ":" + response.PayloadJson);
        Require(response.PayloadSchema == ProtocolConstants.ProviderErrorSchemaV1, "error_schema_invalid:" + response.PayloadSchema + ":" + response.PayloadJson);
        using var document = JsonDocument.Parse(response.PayloadJson);
        var actualErrorCode = document.RootElement.GetProperty("error_code").GetString() ?? string.Empty;
        var actualCategory = document.RootElement.GetProperty("category").GetString() ?? string.Empty;
        Require(actualErrorCode == errorCode, "error_code_invalid:" + actualErrorCode + ":expected=" + errorCode);
        Require(actualCategory == category, "error_category_invalid:" + actualCategory + ":expected=" + category);
    }

    private static string GetString(PipeEnvelope response, string propertyName)
    {
        using var document = JsonDocument.Parse(response.PayloadJson);
        return document.RootElement.GetProperty(propertyName).GetString() ?? string.Empty;
    }

    private static int GetInt32(PipeEnvelope response, string objectProperty, string propertyName)
    {
        using var document = JsonDocument.Parse(response.PayloadJson);
        return document.RootElement.GetProperty(objectProperty).GetProperty(propertyName).GetInt32();
    }

    private static int GetArrayLength(PipeEnvelope response, string propertyName)
    {
        using var document = JsonDocument.Parse(response.PayloadJson);
        return document.RootElement.GetProperty(propertyName).GetArrayLength();
    }

    private static string GetArrayItemString(PipeEnvelope response, string arrayProperty, int index, string propertyName)
    {
        using var document = JsonDocument.Parse(response.PayloadJson);
        return document.RootElement.GetProperty(arrayProperty)[index].GetProperty(propertyName).GetString() ?? string.Empty;
    }

    private static async Task SeedCredentialAsync(string providerAssemblyPath, string credentialRoot, string reference, string secret)
    {
        var assembly = Assembly.LoadFrom(providerAssemblyPath);
        var storeType = assembly.GetType("MarcusAwakeProvider.ProtectedFileCredentialStore", true)!;
        var modeType = assembly.GetType("MarcusAwakeProvider.CredentialProtectionMode", true)!;
        var credentialType = assembly.GetType("MarcusAwakeProvider.ApiKeyCredential", true)!;
        var mode = Enum.Parse(modeType, "PlatformPreferred", false);
        var store = Activator.CreateInstance(storeType, new object?[] { credentialRoot, mode })!;
        var credential = Activator.CreateInstance(credentialType, new object?[] { reference, secret })!;
        try
        {
            var saveMethod = storeType.GetMethod("SaveAsync", new[] { credentialType, typeof(CancellationToken) })!;
            var saveTask = (Task)saveMethod.Invoke(store, new object?[] { credential, CancellationToken.None })!;
            await saveTask.ConfigureAwait(false);
            var result = saveTask.GetType().GetProperty("Result")?.GetValue(saveTask);
            var isSuccess = result?.GetType().GetProperty("IsSuccess")?.GetValue(result);
            Require(isSuccess is bool success && success, "credential_seed_failed");
        }
        finally
        {
            (credential as IDisposable)?.Dispose();
            (store as IDisposable)?.Dispose();
        }
    }

    private static string ResolveProviderAssemblyPath()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MarcusAwakeProvider", "_build_out", "Release", "MarcusAwakeProvider.dll"));
        if (!File.Exists(path)) throw new InvalidOperationException("provider_assembly_missing:" + path);
        return path;
    }

    private static string ResolveServicePath()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "_build_out", "Release", "MarcusAwakeRuntimeService.exe"));
        if (!File.Exists(path)) throw new InvalidOperationException("service_executable_missing:" + path);
        return path;
    }

    private static string CanonicalizePayload(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson, new JsonDocumentOptions { MaxDepth = ProtocolConstants.MaxJsonDepth, AllowTrailingCommas = false });
        return document.RootElement.GetRawText();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch
        {
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string DescribeError(FrameworkError? error)
    {
        if (error == null) return "none";
        if (error.Details != null && error.Details.TryGetValue("protocol_detail", out var protocolDetail)) return error.Code + "/" + error.Category + "/" + protocolDetail;
        if (error.Details != null && error.Details.TryGetValue("failure_type", out var failureType)) return error.Code + "/" + error.Category + "/" + failureType;
        return error.Code + "/" + error.Category;
    }

    private static string DescribeEvents(IReadOnlyList<AiTaskEvent> events)
    {
        if (events == null) return "none";
        var values = new List<string>();
        for (var index = 0; index < events.Count; index++)
        {
            var item = events[index];
            values.Add(item.Kind + "#" + item.Sequence + (item.Error == null ? string.Empty : ":" + DescribeError(item.Error)));
        }
        return string.Join(",", values);
    }
}

internal sealed class FakeProviderHttpServer : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private readonly Task acceptLoop;
    private int requestCount;
    private int responseCount;
    private int delayMilliseconds;
    private int responseStatusCode = 200;
    private string responseBodyOverride = string.Empty;
    private bool dropResponse;
    private readonly object captureSync = new object();
    private readonly List<string> capturedRequests = new List<string>();

    internal FakeProviderHttpServer()
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        acceptLoop = AcceptLoopAsync();
    }

    internal int Port { get; }
    internal int RequestCount => Volatile.Read(ref requestCount);
    internal int ResponseCount => Volatile.Read(ref responseCount);
    internal int DelayMilliseconds
    {
        get => Volatile.Read(ref delayMilliseconds);
        set => Volatile.Write(ref delayMilliseconds, value);
    }

    internal int ResponseStatusCode
    {
        get => Volatile.Read(ref responseStatusCode);
        set => Volatile.Write(ref responseStatusCode, value);
    }

    internal string ResponseBodyOverride
    {
        get
        {
            lock (captureSync) return responseBodyOverride;
        }
        set
        {
            lock (captureSync) responseBodyOverride = value ?? string.Empty;
        }
    }

    internal bool DropResponse
    {
        get
        {
            lock (captureSync) return dropResponse;
        }
        set
        {
            lock (captureSync) dropResponse = value;
        }
    }

    internal string LastRequestText
    {
        get
        {
            lock (captureSync) return capturedRequests.Count == 0 ? string.Empty : capturedRequests[capturedRequests.Count - 1];
        }
    }

    internal IReadOnlyList<Dictionary<string, object?>> GetBoundedRequestObservations()
    {
        lock (captureSync)
        {
            var observations = new List<Dictionary<string, object?>>();
            var count = Math.Min(capturedRequests.Count, 64);
            for (var index = 0; index < count; index++)
            {
                var firstLine = capturedRequests[index].Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n', StringSplitOptions.None)[0];
                var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                observations.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["index"] = index,
                    ["method"] = parts.Length > 0 ? parts[0] : string.Empty,
                    ["path"] = parts.Length > 1 ? parts[1] : string.Empty
                });
            }

            return observations;
        }
    }

    internal async Task WaitForRequestCountAsync(int expectedCount, int timeoutMilliseconds)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(timeoutMilliseconds / 1000.0 * Stopwatch.Frequency);
        while (RequestCount < expectedCount)
        {
            if (Stopwatch.GetTimestamp() >= deadline) throw new TimeoutException("fake_provider_request_timeout");
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    internal async Task WaitForResponseCountAsync(int expectedCount, int timeoutMilliseconds)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(timeoutMilliseconds / 1000.0 * Stopwatch.Frequency);
        while (ResponseCount < expectedCount)
        {
            if (Stopwatch.GetTimestamp() >= deadline) throw new TimeoutException("fake_provider_response_timeout");
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellation.Token).ConfigureAwait(false);
                _ = HandleClientAsync(client);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            try
            {
                using var timeout = new CancellationTokenSource(5000);
                var request = await ReadRequestAsync(stream, timeout.Token).ConfigureAwait(false);
                Interlocked.Increment(ref requestCount);
                lock (captureSync) capturedRequests.Add(RedactRequest(request.RawText));
                var delay = DelayMilliseconds;
                if (delay > 0) await Task.Delay(delay, timeout.Token).ConfigureAwait(false);
                if (DropResponse) return;
                var overrideBody = ResponseBodyOverride;
                var body = string.IsNullOrWhiteSpace(overrideBody)
                    ? request.Path.EndsWith("/models", StringComparison.Ordinal)
                        ? "{\"data\":[{\"id\":\"fake-model\",\"name\":\"Fake Model\"}]}"
                        : "{\"id\":\"fake-completion\",\"model\":\"fake-model\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"fake reply\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":2,\"completion_tokens\":3}}"
                    : overrideBody;
                var bytes = Encoding.UTF8.GetBytes(body);
                var statusCode = ResponseStatusCode;
                var statusText = statusCode == 200 ? "OK" : statusCode == 401 ? "Unauthorized" : statusCode == 403 ? "Forbidden" : statusCode == 404 ? "Not Found" : statusCode == 429 ? "Too Many Requests" : statusCode >= 500 ? "Server Error" : "Error";
                var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + statusCode.ToString() + " " + statusText + "\r\nContent-Type: application/json\r\nContent-Length: " + bytes.Length.ToString() + "\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, 0, header.Length, timeout.Token).ConfigureAwait(false);
                await stream.WriteAsync(bytes, 0, bytes.Length, timeout.Token).ConfigureAwait(false);
                await stream.FlushAsync(timeout.Token).ConfigureAwait(false);
                Interlocked.Increment(ref responseCount);
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
        }
    }

    private static async Task<FakeRequest> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var data = new List<byte>();
        var buffer = new byte[4096];
        var headerEnd = -1;
        var contentLength = 0;
        while (headerEnd < 0 || data.Count < headerEnd + 4 + contentLength)
        {
            var count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (count == 0) throw new EndOfStreamException();
            for (var index = 0; index < count; index++) data.Add(buffer[index]);
            if (headerEnd < 0)
            {
                headerEnd = FindHeaderEnd(data);
                if (headerEnd >= 0)
                {
                    var headerText = Encoding.ASCII.GetString(data.ToArray(), 0, headerEnd);
                    foreach (var line in headerText.Split(new[] { "\r\n" }, StringSplitOptions.None))
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) && int.TryParse(line.Substring("Content-Length:".Length).Trim(), out var value)) contentLength = value;
                    }
                }
            }
        }

        var header = Encoding.ASCII.GetString(data.ToArray(), 0, headerEnd);
        var requestLine = header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0].Split(' ');
        return new FakeRequest(requestLine.Length > 1 ? requestLine[0] : string.Empty, requestLine.Length > 1 ? requestLine[1] : string.Empty, Encoding.UTF8.GetString(data.ToArray()));
    }

    private static int FindHeaderEnd(List<byte> data)
    {
        for (var index = 0; index <= data.Count - 4; index++)
        {
            if (data[index] == 13 && data[index + 1] == 10 && data[index + 2] == 13 && data[index + 3] == 10) return index;
        }

        return -1;
    }

    private static string RedactRequest(string rawText)
    {
        var lines = rawText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)
                || lines[index].StartsWith("x-api-key:", StringComparison.OrdinalIgnoreCase))
            {
                var separator = lines[index].IndexOf(':');
                lines[index] = lines[index].Substring(0, separator + 1) + " <redacted>";
            }
        }

        return string.Join("\n", lines);
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        listener.Stop();
        try
        {
            await acceptLoop.ConfigureAwait(false);
        }
        catch
        {
        }
        cancellation.Dispose();
    }

    private sealed class FakeRequest
    {
        internal FakeRequest(string method, string path, string rawText)
        {
            Method = method;
            Path = path;
            RawText = rawText;
        }

        internal string Method { get; }
        internal string Path { get; }
        internal string RawText { get; }
    }
}
