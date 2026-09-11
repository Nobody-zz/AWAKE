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
internal sealed class P3DA2HarnessRunner
{
    private const string ProfileId = "profile.p3d-a2";
    private const string RouteId = "route.p3d-a2";
    private const string PrimaryProviderId = "provider.primary";
    private const string FallbackProviderId = "provider.fallback";
    private const string OwnerId = "p3d-a2-harness";
    private const string CampaignId = "campaign-p3d-a2";
    private const string TimelineId = "timeline-p3d-a2";

    private readonly List<Dictionary<string, object?>> caseObservations = new List<Dictionary<string, object?>>();
    private int passed;
    private int failed;

    internal async Task<int> RunAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("FAIL windows_required");
            return 1;
        }

        var runId = "p3d-a2-" + Guid.NewGuid().ToString("N");
        var runRoot = Path.Combine(Path.GetTempPath(), runId);
        var dataRoot = Path.Combine(runRoot, "runtime");
        var credentialRoot = Path.Combine(runRoot, "credentials");
        var providerAssemblyPath = ResolveProviderAssemblyPath();
        var servicePath = ResolveServicePath();
        var secret = "p3d-a2-secret-" + Guid.NewGuid().ToString("N");
        LaunchedService? service = null;
        ServiceConnection? connection = null;
        P3DA2FakeProviderHttpServer? primaryServer = null;
        P3DA2FakeProviderHttpServer? fallbackServer = null;
        P3DA2FakeProviderHttpServer? ollamaServer = null;

        try
        {
            Directory.CreateDirectory(runRoot);
            primaryServer = new P3DA2FakeProviderHttpServer();
            fallbackServer = new P3DA2FakeProviderHttpServer();
            ollamaServer = new P3DA2FakeProviderHttpServer();
            await SeedCredentialAsync(providerAssemblyPath, credentialRoot, "credential.p3d_a2", secret).ConfigureAwait(false);
            service = await LaunchedService.StartAsync(
                servicePath,
                4201,
                dataRoot,
                AllCapabilities(),
                environmentOverrides: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MARCUS_AWAKE_PROVIDER_ASSEMBLY_PATH"] = providerAssemblyPath,
                    ["MARCUS_AWAKE_RUNTIME_CREDENTIAL_ROOT"] = credentialRoot,
                    ["MARCUS_AWAKE_RUNTIME_TEST_MODE"] = "p3d-a2",
                    ["MARCUS_AWAKE_TEST_LATE_STREAM_EVENT_MESSAGE_ID"] = "a2-s14"
                }).ConfigureAwait(false);
            connection = await service.ConnectAsync(service.Descriptor.ChallengeId, "p3d-a2-session", AllCapabilities()).ConfigureAwait(false);

            await UpsertProfileAsync(connection, ProfileId, PrimaryProviderId, RouteId, "openai_compatible", primaryServer.Port, true).ConfigureAwait(false);
            await UpsertProfileAsync(connection, ProfileId, FallbackProviderId, RouteId, "openai_compatible", fallbackServer.Port, true).ConfigureAwait(false);
            await UpsertProfileAsync(connection, "profile.ollama", "provider.ollama", "route.ollama", "ollama", ollamaServer.Port, false).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S01", "normal_sse_stream", async () =>
            {
                primaryServer.Enqueue(P3DA2StreamPlan.Sse(OpenAiDelta("hello")));
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s01", "task-a2-s01", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s01")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "text_delta", "completed");
                AssertSingleTerminal(transcript);
                AssertProviderSequence(transcript, PrimaryProviderId, PrimaryProviderId, PrimaryProviderId);
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S02", "sse_usage_update", async () =>
            {
                primaryServer.Enqueue(P3DA2StreamPlan.Sse(OpenAiDelta("usage"), OpenAiUsage(2, 3)));
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s02", "task-a2-s02", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s02")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "text_delta", "usage_update", "completed");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames.Any(item => item.EventKind == "usage_update" && item.InputTokens == 2 && item.OutputTokens == 3), "usage_update_not_projected");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S03", "first_byte_fallback", async () =>
            {
                var primaryBefore = primaryServer.RequestCount;
                var fallbackBefore = fallbackServer.RequestCount;
                primaryServer.Enqueue(P3DA2StreamPlan.Status(503));
                fallbackServer.Enqueue(P3DA2StreamPlan.Sse(OpenAiDelta("fallback")));
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s03", "task-a2-s03", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s03")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "route_changed", "text_delta", "completed");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[1].FromProviderId == PrimaryProviderId && transcript.Frames[1].ToProviderId == FallbackProviderId, "fallback_route_change_invalid");
                Require(primaryServer.RequestCount == primaryBefore + 1 && fallbackServer.RequestCount == fallbackBefore + 1, "fallback_request_count_invalid");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S04", "non_retryable_authentication_failure", async () =>
            {
                var primaryBefore = primaryServer.RequestCount;
                var fallbackBefore = fallbackServer.RequestCount;
                primaryServer.Enqueue(P3DA2StreamPlan.Status(401));
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s04", "task-a2-s04", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s04")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "failed");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[1].ErrorCategory == "authentication", "authentication_category_invalid");
                Require(primaryServer.RequestCount == primaryBefore + 1 && fallbackServer.RequestCount == fallbackBefore, "authentication_fallback_called");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S05", "visible_text_disables_fallback", async () =>
            {
                var fallbackBefore = fallbackServer.RequestCount;
                primaryServer.Enqueue(P3DA2StreamPlan.SseWithoutDone(OpenAiDelta("partial")));
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s05", "task-a2-s05", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s05")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "text_delta", "failed");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[2].ErrorCategory == "incomplete_stream", "incomplete_stream_category_invalid");
                Require(fallbackServer.RequestCount == fallbackBefore, "visible_text_fallback_called");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S06", "fallback_with_usage", async () =>
            {
                primaryServer.Enqueue(P3DA2StreamPlan.Status(503));
                fallbackServer.Enqueue(P3DA2StreamPlan.Sse(OpenAiDelta("fallback-usage"), OpenAiUsage(4, 5)));
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s06", "task-a2-s06", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s06")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "route_changed", "text_delta", "usage_update", "completed");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[1].FromProviderId == PrimaryProviderId && transcript.Frames[1].ToProviderId == FallbackProviderId, "fallback_usage_route_change_invalid");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S07", "player_cancellation_emits_single_cancelled_terminal", async () =>
            {
                using var control = await ConnectControlAsync(service, connection.SessionId).ConfigureAwait(false);
                primaryServer.Enqueue(P3DA2StreamPlan.SseBlockedAfterFirst(OpenAiDelta("cancel"), OpenAiDelta("never")));
                var target = CreateStreamFrame(connection, "a2-s07", "task-a2-s07", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s07");
                await connection.SendRawAsync(target.RawJson).ConfigureAwait(false);
                var transcript = new StreamTranscript(target.Envelope.MessageId);
                transcript.Frames.Add(await ReadStreamFrameAsync(connection, target).ConfigureAwait(false));
                transcript.Frames.Add(await ReadStreamFrameAsync(connection, target).ConfigureAwait(false));
                var cancel = await control.SendAndReadAsync(CreateControlCancelFrame(control, target, "a2-s07-cancel")).ConfigureAwait(false);
                AssertCancelRequested(cancel);
                transcript.Frames.Add(await ReadStreamFrameAsync(connection, target).ConfigureAwait(false));
                AssertKinds(transcript, "started", "text_delta", "cancelled");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[2].ErrorCode == "request.cancelled", "cancelled_error_code_invalid");
                Require(transcript.Frames[2].ErrorCategory == "cancelled", "cancelled_error_category_invalid");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S08", "provider_timeout_without_next_candidate", async () =>
            {
                await UpsertProfileAsync(connection, "profile.timeout", "provider.timeout", "route.timeout", "openai_compatible", primaryServer.Port, true).ConfigureAwait(false);
                primaryServer.Enqueue(P3DA2StreamPlan.Status(408));
                var before = primaryServer.RequestCount;
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s08", "task-a2-s08", "profile.timeout", "provider.timeout", "route.timeout", "idem-a2-s08")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "failed");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[1].ErrorCategory == "timeout", "timeout_category_invalid");
                Require(primaryServer.RequestCount == before + 1, "timeout_called_unexpected_candidate");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S09", "eof_is_incomplete", async () =>
            {
                primaryServer.Enqueue(P3DA2StreamPlan.SseWithoutDone(OpenAiDelta("eof")));
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s09", "task-a2-s09", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s09")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "text_delta", "failed");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[2].ErrorCategory == "incomplete_stream", "eof_error_category_invalid");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S10", "malformed_sse_frame", async () =>
            {
                primaryServer.Enqueue(P3DA2StreamPlan.SseRaw("not-json"));
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s10", "task-a2-s10", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s10")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "failed");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[1].ErrorCategory == "malformed_response", "malformed_error_category_invalid");
                Require(!transcript.Frames[1].SafeMessage.Contains("not-json", StringComparison.Ordinal), "malformed_payload_leaked");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S11", "frame_budget_exhaustion", async () =>
            {
                var frames = new List<string>();
                for (var index = 0; index < 200; index++) frames.Add(OpenAiDelta("x"));
                primaryServer.Enqueue(P3DA2StreamPlan.Sse(frames.ToArray()));
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s11", "task-a2-s11", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s11", frameCount: 125, textDeltas: 200), 150).ConfigureAwait(false);
                Require(transcript.Frames.Count == 125, "frame_budget_transcript_count_invalid:" + transcript.Frames.Count.ToString());
                Require(transcript.Frames.Count(item => item.EventKind == "text_delta") == 123, "frame_budget_text_count_invalid");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[124].ErrorCategory == "resource_exhausted", "frame_budget_error_category_invalid");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S12", "complete_stream_replay", async () =>
            {
                primaryServer.Enqueue(P3DA2StreamPlan.Sse(OpenAiDelta("replay")));
                var first = CreateStreamFrame(connection, "a2-s12-first", "task-a2-s12-first", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s12");
                var firstTranscript = await ExecuteStreamAsync(connection, first).ConfigureAwait(false);
                var before = primaryServer.RequestCount;
                var replay = CreateStreamFrame(connection, "a2-s12-replay", "task-a2-s12-replay", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s12");
                var replayTranscript = await ExecuteStreamAsync(connection, replay).ConfigureAwait(false);
                AssertKinds(replayTranscript, "started", "text_delta", "completed");
                AssertSingleTerminal(replayTranscript);
                Require(primaryServer.RequestCount == before, "replay_recalled_provider");
                Require(replayTranscript.Frames.Count == firstTranscript.Frames.Count, "replay_frame_count_invalid");
                Require(replayTranscript.Frames[replayTranscript.Frames.Count - 1].OutcomeKind == ProtocolConstants.OutcomeTerminalReplay, "replay_terminal_outcome_invalid");
                Require(replayTranscript.Frames[replayTranscript.Frames.Count - 1].TaskScopeMessageId == replay.Envelope.MessageId, "replay_task_scope_not_rebound");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S13a", "no_candidate_fail_closed", async () =>
            {
                var before = primaryServer.RequestCount + fallbackServer.RequestCount;
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s13a", "task-a2-s13a", "profile.empty", "provider.empty", "route.empty", "idem-a2-s13a")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "failed");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[1].ErrorCode == "route_no_candidate", "no_candidate_error_invalid");
                Require(primaryServer.RequestCount + fallbackServer.RequestCount == before, "no_candidate_called_provider");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S13b", "missing_anchor_fail_closed", async () =>
            {
                await UpsertProfileAsync(connection, "profile.anchor", "provider.anchor-fallback", "route.anchor", "openai_compatible", fallbackServer.Port, true).ConfigureAwait(false);
                var before = fallbackServer.RequestCount;
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s13b", "task-a2-s13b", "profile.anchor", "provider.anchor-primary", "route.anchor", "idem-a2-s13b")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "failed");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[1].ErrorCode == "route_anchor_unavailable", "missing_anchor_error_invalid");
                Require(fallbackServer.RequestCount == before, "missing_anchor_called_fallback");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S14", "late_event_after_terminal_is_rejected", async () =>
            {
                primaryServer.Enqueue(P3DA2StreamPlan.Sse(OpenAiDelta("done")));
                var target = CreateStreamFrame(connection, "a2-s14", "task-a2-s14", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s14");
                var transcript = await ExecuteStreamAsync(connection, target).ConfigureAwait(false);
                AssertKinds(transcript, "started", "text_delta", "completed");
                AssertSingleTerminal(transcript);
                var lateFrameObserved = false;
                try
                {
                    await ReadStreamFrameAsync(connection, target, 750).ConfigureAwait(false);
                    lateFrameObserved = true;
                }
                catch (OperationCanceledException)
                {
                }

                Require(!lateFrameObserved, "late_stream_event_forwarded");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S15", "ipc_disconnect_aborts_working_ledger", async () =>
            {
                using var disconnectConnection = await service.ConnectNextAsync("p3d-a2-disconnect", AllCapabilities()).ConfigureAwait(false);
                await UpsertProfileAsync(disconnectConnection, ProfileId, PrimaryProviderId, RouteId, "openai_compatible", primaryServer.Port, true).ConfigureAwait(false);
                primaryServer.Enqueue(P3DA2StreamPlan.SseBlockedAfterFirst(OpenAiDelta("disconnect")));
                var target = CreateStreamFrame(disconnectConnection, "a2-s15", "task-a2-s15", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s15");
                await disconnectConnection.SendRawAsync(target.RawJson).ConfigureAwait(false);
                var started = await ReadStreamFrameAsync(disconnectConnection, target).ConfigureAwait(false);
                var delta = await ReadStreamFrameAsync(disconnectConnection, target).ConfigureAwait(false);
                Require(started.EventKind == "started" && delta.EventKind == "text_delta", "disconnect_prefix_invalid");
                var before = primaryServer.RequestCount;
                disconnectConnection.Dispose();
                await Task.Delay(400).ConfigureAwait(false);
                using var reconnect = await service.ConnectNextAsync("p3d-a2-reconnect", AllCapabilities()).ConfigureAwait(false);
                await UpsertProfileAsync(reconnect, ProfileId, PrimaryProviderId, RouteId, "openai_compatible", primaryServer.Port, true).ConfigureAwait(false);
                primaryServer.Enqueue(P3DA2StreamPlan.Sse(OpenAiDelta("after-disconnect")));
                var retry = CreateStreamFrame(reconnect, "a2-s15-retry", "task-a2-s15-retry", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s15");
                var retryTranscript = await ExecuteStreamAsync(reconnect, retry).ConfigureAwait(false);
                AssertKinds(retryTranscript, "started", "text_delta", "completed");
                Require(primaryServer.RequestCount == before + 1, "aborted_ledger_replayed_instead_of_recalled");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S16a", "route_change_contract_rejection", () => RunContractNegativeCaseAsync("route_changed", "stream_route_change_invalid")).ConfigureAwait(false);
            await RunCaseAsync("P3D-A2-S16b", "stream_sequence_contract_rejection", () => RunContractNegativeCaseAsync("sequence", "stream_sequence_invalid")).ConfigureAwait(false);
            await RunCaseAsync("P3D-A2-S16c", "provider_identity_contract_rejection", () => RunContractNegativeCaseAsync("provider", "stream_provider_mismatch")).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S16d", "structured_json_invalid_is_fail_closed", async () =>
            {
                primaryServer.Enqueue(P3DA2StreamPlan.Sse(OpenAiDelta("{\"answer\":"), OpenAiDelta("oops")));
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s16d", "task-a2-s16d", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s16d", responseSchemaJson: "{\"type\":\"object\"}")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "failed");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[1].ErrorCategory == "malformed_response", "structured_invalid_category_invalid");
                Require(!transcript.Frames[1].SafeMessage.Contains("oops", StringComparison.Ordinal), "structured_invalid_payload_leaked");
            }).ConfigureAwait(false);

            await RunCaseAsync("P3D-A2-S17", "structured_json_stream_projection", async () =>
            {
                primaryServer.Enqueue(P3DA2StreamPlan.Sse(OpenAiDelta("{\"answer\":"), OpenAiDelta("\"ok\"}")));
                var transcript = await ExecuteStreamAsync(connection, CreateStreamFrame(connection, "a2-s17", "task-a2-s17", ProfileId, PrimaryProviderId, RouteId, "idem-a2-s17", responseSchemaJson: "{\"type\":\"object\"}")).ConfigureAwait(false);
                AssertKinds(transcript, "started", "completed");
                AssertSingleTerminal(transcript);
                Require(transcript.Frames[1].StructuredJson == "{\"answer\":\"ok\"}", "structured_json_not_canonicalized");
                Require(string.IsNullOrEmpty(transcript.Frames[1].Text), "structured_json_leaked_as_text");
            }).ConfigureAwait(false);

            await ShutdownAsync(service, connection).ConfigureAwait(false);
            connection = null;
            service = null;
            WriteEvidence(runId, servicePath, primaryServer, fallbackServer, ollamaServer);
            Console.WriteLine("P3D-A2-SERVICE " + passed.ToString() + "/" + (passed + failed).ToString() + " PASS");
            return failed == 0 ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL p3d-a2:" + exception.GetType().Name + ":" + exception.Message);
            return 1;
        }
        finally
        {
            connection?.Dispose();
            if (service != null) await service.DisposeAsync().ConfigureAwait(false);
            if (primaryServer != null) await primaryServer.DisposeAsync().ConfigureAwait(false);
            if (fallbackServer != null) await fallbackServer.DisposeAsync().ConfigureAwait(false);
            if (ollamaServer != null) await ollamaServer.DisposeAsync().ConfigureAwait(false);
            TryDelete(runRoot);
        }
    }

    private async Task RunCaseAsync(string id, string name, Func<Task> action)
    {
        Console.WriteLine("CASE " + id + " " + name + " START");
        var observation = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["name"] = name,
            ["status"] = "pass",
            ["surface"] = "runtime_service_ipc"
        };
        try
        {
            await action().ConfigureAwait(false);
            passed++;
            Console.WriteLine("CASE " + id + " " + name + " PASS");
        }
        catch (Exception exception)
        {
            failed++;
            observation["status"] = "fail";
            observation["error_type"] = exception.GetType().Name;
            observation["error"] = exception.Message;
            Console.Error.WriteLine("CASE " + id + " " + name + " FAIL " + exception.GetType().Name + ":" + exception.Message);
        }

        caseObservations.Add(observation);
    }

    private async Task<StreamTranscript> ExecuteStreamAsync(ServiceConnection connection, FrameBuildResult request, int maximumFrames = 150)
    {
        await connection.SendRawAsync(request.RawJson).ConfigureAwait(false);
        var transcript = new StreamTranscript(request.Envelope.MessageId);
        for (var index = 0; index < maximumFrames; index++)
        {
            var frame = await ReadStreamFrameAsync(connection, request).ConfigureAwait(false);
            transcript.Frames.Add(frame);
            Console.WriteLine("TRANSCRIPT " + request.Envelope.MessageId + " W=" + frame.StreamSequence.ToString() + " I=" + frame.IpcSequence.ToString() + " E=" + frame.EventIndex.ToString() + " F=" + frame.FrameworkSequence.ToString() + " active=" + frame.ActiveProviderId + " kind=" + frame.EventKind + " terminal=" + (frame.IsTerminal ? "1" : "0") + " error=" + frame.ErrorCode + "/" + frame.ErrorCategory);
            if (frame.IsTerminal) return transcript;
        }

        throw new TimeoutException("stream_terminal_not_observed");
    }

    private static async Task<StreamFrameObservation> ReadStreamFrameAsync(ServiceConnection connection, FrameBuildResult request, int timeoutMilliseconds = 5000)
    {
        var response = await connection.ReadResponseAsync(item => item.MessageId == request.Envelope.MessageId, timeoutMilliseconds).ConfigureAwait(false);
        Require(response.MessageType == ProtocolConstants.MessageTypeProviderStreamEvent, "stream_response_message_type_invalid:" + response.MessageType);
        Require(response.PayloadSchema == ProtocolConstants.ProviderStreamEventSchemaV1, "stream_response_schema_invalid:" + response.PayloadSchema);
        using var document = JsonDocument.Parse(response.PayloadJson);
        var root = document.RootElement;
        var eventKind = root.GetProperty("event_kind").GetString() ?? string.Empty;
        var providerId = root.GetProperty("provider_id").GetString() ?? string.Empty;
        var modelId = root.GetProperty("model_id").GetString() ?? string.Empty;
        var streamSequence = root.GetProperty("stream_sequence").GetInt64();
        var text = root.TryGetProperty("text", out var textProperty) ? textProperty.GetString() ?? string.Empty : string.Empty;
        var fromProviderId = root.TryGetProperty("from_provider_id", out var fromProperty) ? fromProperty.GetString() ?? string.Empty : string.Empty;
        var toProviderId = root.TryGetProperty("to_provider_id", out var toProperty) ? toProperty.GetString() ?? string.Empty : string.Empty;
        var structuredJson = root.TryGetProperty("structured_json", out var structuredProperty) ? structuredProperty.GetString() ?? string.Empty : string.Empty;
        var errorCode = string.Empty;
        var errorCategory = string.Empty;
        var safeMessage = string.Empty;
        if (root.TryGetProperty("error", out var errorProperty))
        {
            errorCode = errorProperty.GetProperty("error_code").GetString() ?? string.Empty;
            errorCategory = errorProperty.GetProperty("category").GetString() ?? string.Empty;
            safeMessage = errorProperty.GetProperty("safe_message").GetString() ?? string.Empty;
        }

        var isTerminal = eventKind == "completed" || eventKind == "cancelled" || eventKind == "failed";
        return new StreamFrameObservation(
            response.Sequence,
            response.EventIndex,
            streamSequence,
            isTerminal ? streamSequence + 1 : streamSequence + 1,
            response.OutcomeKind,
            response.TaskScope?.MessageId ?? string.Empty,
            eventKind,
            providerId,
            providerId,
            fromProviderId,
            toProviderId,
            modelId,
            text,
            structuredJson,
            errorCode,
            errorCategory,
            safeMessage,
            isTerminal,
            ReadUsage(root, "input_tokens"),
            ReadUsage(root, "output_tokens"));
    }

    private static int? ReadUsage(JsonElement root, string name)
    {
        return root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : null;
    }

    private async Task UpsertProfileAsync(ServiceConnection connection, string profileId, string providerId, string routeId, string kind, int port, bool isCloud)
    {
        var messageId = "upsert-" + profileId + "-" + providerId + "-" + Guid.NewGuid().ToString("N");
        var payload = ProfilePayload(profileId, providerId, routeId, kind, port, isCloud);
        var frame = CreateProviderFrame(connection, ProtocolConstants.MessageTypeProviderProfileUpsertV1, messageId, "task-" + messageId, payload, "idem-" + messageId, ProtocolConstants.ProviderProfileResultSchemaV1, profileId, providerId, routeId);
        var response = await connection.SendAndReadAsync(frame).ConfigureAwait(false);
        Require(response.MessageType == ProtocolConstants.MessageTypeProviderProfileResult, "profile_upsert_message_type_invalid");
        Require(response.ErrorCode == string.Empty, "profile_upsert_failed:" + response.ErrorCode);
    }

    private static FrameBuildResult CreateStreamFrame(ServiceConnection connection, string messageId, string taskId, string profileId, string providerId, string routeId, string idempotencyKey, int frameCount = 16, int outputBytes = 262144, int tokens = 1024, int textDeltas = 256, string? responseSchemaJson = null)
    {
        var payload = new StringBuilder();
        payload.Append("{\"schema\":").Append(JsonSerializer.Serialize(ProtocolConstants.ProviderStreamSchemaV1));
        payload.Append(",\"profile_id\":").Append(JsonSerializer.Serialize(profileId));
        payload.Append(",\"provider_id\":").Append(JsonSerializer.Serialize(providerId));
        payload.Append(",\"route_id\":").Append(JsonSerializer.Serialize(routeId));
        payload.Append(",\"messages\":[{\"role\":\"user\",\"content\":\"p3d-a2\"}]\n");
        payload.Append(",\"resource_budget\":{\"frame_count\":").Append(frameCount.ToString());
        payload.Append(",\"output_bytes\":").Append(outputBytes.ToString());
        payload.Append(",\"tokens\":").Append(tokens.ToString());
        payload.Append(",\"text_deltas\":").Append(textDeltas.ToString()).Append('}');
        if (responseSchemaJson != null) payload.Append(",\"response_schema_json\":").Append(JsonSerializer.Serialize(responseSchemaJson));
        payload.Append('}');
        return CreateProviderFrame(connection, ProtocolConstants.MessageTypeProviderStreamV1, messageId, taskId, payload.ToString(), idempotencyKey, ProtocolConstants.ProviderStreamEventSchemaV1, profileId, providerId, routeId);
    }

    private static FrameBuildResult CreateProviderFrame(ServiceConnection connection, string messageType, string messageId, string taskId, string payload, string idempotencyKey, string outputSchema, string profileId, string providerId, string routeId)
    {
        var canonicalPayload = CanonicalizePayload(payload);
        var payloadHash = TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes(canonicalPayload));
        var taskScope = new TaskScopeEnvelope
        {
            TaskId = taskId,
            MessageId = messageId,
            OwnerId = OwnerId,
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
        var frame = connection.CreateFrame(messageType, messageId, canonicalPayload, null, CampaignId, TimelineId, 1, taskScope, OwnerId);
        frame.Envelope.PayloadSchema = messageType switch
        {
            ProtocolConstants.MessageTypeProviderProfileUpsertV1 => ProtocolConstants.ProviderProfileUpsertSchemaV1,
            ProtocolConstants.MessageTypeProviderStreamV1 => ProtocolConstants.ProviderStreamSchemaV1,
            _ => throw new InvalidOperationException("provider_message_type_invalid")
        };
        frame.Envelope.CausationId = "cause-" + messageId;
        frame.Envelope.FenceProof = TransportSecurity.ComputeFrameFenceProof(connection.FrameKey, frame.Envelope);
        return new FrameBuildResult(frame.Envelope, ProtocolCodec.SerializeEnvelope(frame.Envelope));
    }

    private static string ProfilePayload(string profileId, string providerId, string routeId, string kind, int port, bool isCloud)
    {
        var baseUrl = kind == "ollama" ? "http://127.0.0.1:" + port.ToString() + "/" : "http://127.0.0.1:" + port.ToString() + "/v1/";
        var payload = new StringBuilder();
        payload.Append("{\"schema\":").Append(JsonSerializer.Serialize(ProtocolConstants.ProviderProfileUpsertSchemaV1));
        payload.Append(",\"profile_id\":").Append(JsonSerializer.Serialize(profileId));
        payload.Append(",\"provider_id\":").Append(JsonSerializer.Serialize(providerId));
        payload.Append(",\"route_id\":").Append(JsonSerializer.Serialize(routeId));
        payload.Append(",\"provider_kind\":").Append(JsonSerializer.Serialize(kind));
        payload.Append(",\"base_url\":").Append(JsonSerializer.Serialize(baseUrl));
        payload.Append(",\"default_model\":\"fake-model\"" );
        if (isCloud) payload.Append(",\"credential_reference\":\"credential.p3d_a2\"");
        payload.Append(",\"is_cloud\":").Append(isCloud ? "true" : "false").Append('}');
        return payload.ToString();
    }

    private static string[] AllCapabilities()
    {
        return new[]
        {
            ProtocolConstants.MessageTypeHealth,
            ProtocolConstants.MessageTypeEcho,
            ProtocolConstants.MessageTypeCancel,
            ProtocolConstants.MessageTypeDiagnostic,
            ProtocolConstants.MessageTypeShutdown,
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

    private static Task RunContractNegativeCaseAsync(string kind, string expectedError)
    {
        var machine = new ProviderStreamStateMachine("contract", "provider.primary", "profile.p3d-a2");
        var started = machine.Accept("started", 1, "provider.primary");
        Require(started.IsAccepted, "contract_started_rejected");
        ProviderStreamDecision decision;
        if (kind == "route_changed") decision = machine.Accept("route_changed", 2, "provider.primary", "provider.primary", "provider.primary");
        else if (kind == "sequence") decision = machine.Accept("text_delta", 3, "provider.primary", hasText: true);
        else decision = machine.Accept("text_delta", 2, "provider.other", hasText: true);
        Require(!decision.IsAccepted && decision.ErrorCode == expectedError, "contract_error_invalid:" + decision.ErrorCode);
        return Task.CompletedTask;
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

    private static void AssertKinds(StreamTranscript transcript, params string[] expected)
    {
        Require(transcript.Frames.Count == expected.Length, "stream_kind_count_invalid:" + transcript.Frames.Count.ToString());
        for (var index = 0; index < expected.Length; index++) Require(transcript.Frames[index].EventKind == expected[index], "stream_kind_invalid_at_" + index.ToString() + ":" + transcript.Frames[index].EventKind);
    }

    private static void AssertProviderSequence(StreamTranscript transcript, params string[] expected)
    {
        Require(transcript.Frames.Count == expected.Length, "provider_sequence_count_invalid");
        for (var index = 0; index < expected.Length; index++) Require(transcript.Frames[index].ProviderId == expected[index], "provider_sequence_invalid_at_" + index.ToString());
    }

    private static void AssertSingleTerminal(StreamTranscript transcript)
    {
        Require(transcript.Frames.Count(item => item.IsTerminal) == 1, "terminal_count_invalid");
        for (var index = 1; index < transcript.Frames.Count; index++) Require(transcript.Frames[index].StreamSequence == transcript.Frames[index - 1].StreamSequence + 1, "stream_sequence_not_contiguous");
        for (var index = 0; index < transcript.Frames.Count; index++) Require(transcript.Frames[index].EventIndex == transcript.Frames[index].StreamSequence, "event_index_mismatch");
    }

    private static async Task ShutdownAsync(LaunchedService service, ServiceConnection connection)
    {
        try
        {
            var shutdown = connection.CreateFrame(ProtocolConstants.MessageTypeShutdown, "a2-shutdown", "{}", null, CampaignId, TimelineId, 1);
            await connection.SendRawAsync(shutdown.RawJson).ConfigureAwait(false);
            await connection.ReadResponseAsync(item => item.MessageId == shutdown.Envelope.MessageId).ConfigureAwait(false);
        }
        catch
        {
        }

        connection.Dispose();
        await service.DisposeAsync().ConfigureAwait(false);
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
            Require(result?.GetType().GetProperty("IsSuccess")?.GetValue(result) is bool success && success, "credential_seed_failed");
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

    private void WriteEvidence(string runId, string servicePath, P3DA2FakeProviderHttpServer primary, P3DA2FakeProviderHttpServer fallback, P3DA2FakeProviderHttpServer ollama)
    {
        var configured = Environment.GetEnvironmentVariable("MARCUS_AWAKE_P3D_A2_EVIDENCE_PATH");
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "evidence", "MARCUS-AWAKE-P3D-A2-20260828.json"))
            : Path.GetFullPath(configured);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var evidence = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = "marcus-awake/p3d-a2-evidence.v1",
            ["plan_revision"] = 8,
            ["run_id"] = runId,
            ["evidence_level"] = "offline_e2",
            ["service_executable"] = servicePath,
            ["external_network"] = false,
            ["real_cloud_provider"] = false,
            ["bannerlord_started"] = false,
            ["game_directory_synced"] = false,
            ["case_count"] = passed + failed,
            ["passed"] = passed,
            ["failed"] = failed,
            ["cases"] = caseObservations,
            ["fake_http"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["primary_requests"] = primary.RequestCount,
                ["fallback_requests"] = fallback.RequestCount,
                ["ollama_requests"] = ollama.RequestCount,
                ["primary_observations"] = primary.Observations,
                ["fallback_observations"] = fallback.Observations,
                ["ollama_observations"] = ollama.Observations
            }
        };
        File.WriteAllText(path, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        Console.WriteLine("EVIDENCE_PATH=" + path);
    }

    private static string OpenAiDelta(string text)
    {
        return "{\"model\":\"fake-model\",\"choices\":[{\"delta\":{\"content\":" + JsonSerializer.Serialize(text) + "}}]}";
    }

    private static string OpenAiUsage(int inputTokens, int outputTokens)
    {
        return "{\"model\":\"fake-model\",\"choices\":[],\"usage\":{\"prompt_tokens\":" + inputTokens.ToString() + ",\"completion_tokens\":" + outputTokens.ToString() + "}}";
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

    private sealed class StreamTranscript
    {
        internal StreamTranscript(string messageId)
        {
            MessageId = messageId;
        }

        internal string MessageId { get; }
        internal List<StreamFrameObservation> Frames { get; } = new List<StreamFrameObservation>();
    }

    private sealed class StreamFrameObservation
    {
        internal StreamFrameObservation(long ipcSequence, long eventIndex, long streamSequence, long frameworkSequence, string outcomeKind, string taskScopeMessageId, string eventKind, string providerId, string activeProviderId, string fromProviderId, string toProviderId, string modelId, string text, string structuredJson, string errorCode, string errorCategory, string safeMessage, bool isTerminal, int? inputTokens, int? outputTokens)
        {
            IpcSequence = ipcSequence;
            EventIndex = eventIndex;
            StreamSequence = streamSequence;
            FrameworkSequence = frameworkSequence;
            OutcomeKind = outcomeKind;
            TaskScopeMessageId = taskScopeMessageId;
            EventKind = eventKind;
            ProviderId = providerId;
            ActiveProviderId = activeProviderId;
            FromProviderId = fromProviderId;
            ToProviderId = toProviderId;
            ModelId = modelId;
            Text = text;
            StructuredJson = structuredJson;
            ErrorCode = errorCode;
            ErrorCategory = errorCategory;
            SafeMessage = safeMessage;
            IsTerminal = isTerminal;
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
        }

        internal long IpcSequence { get; }
        internal long EventIndex { get; }
        internal long StreamSequence { get; }
        internal long FrameworkSequence { get; }
        internal string OutcomeKind { get; }
        internal string TaskScopeMessageId { get; }
        internal string EventKind { get; }
        internal string ProviderId { get; }
        internal string ActiveProviderId { get; }
        internal string FromProviderId { get; }
        internal string ToProviderId { get; }
        internal string ModelId { get; }
        internal string Text { get; }
        internal string StructuredJson { get; }
        internal string ErrorCode { get; }
        internal string ErrorCategory { get; }
        internal string SafeMessage { get; }
        internal bool IsTerminal { get; }
        internal int? InputTokens { get; }
        internal int? OutputTokens { get; }
    }
}

internal sealed class P3DA2StreamPlan
{
    private P3DA2StreamPlan(int statusCode, string? contentType, IReadOnlyList<string> frames, bool includeDone, int closeAfterFrames, bool blockAfterFirstFrame)
    {
        StatusCode = statusCode;
        ContentType = contentType;
        Frames = frames;
        IncludeDone = includeDone;
        CloseAfterFrames = closeAfterFrames;
        BlockAfterFirstFrame = blockAfterFirstFrame;
    }

    internal int StatusCode { get; }
    internal string? ContentType { get; }
    internal IReadOnlyList<string> Frames { get; }
    internal bool IncludeDone { get; }
    internal int CloseAfterFrames { get; }
    internal bool BlockAfterFirstFrame { get; }

    internal static P3DA2StreamPlan Status(int statusCode) => new P3DA2StreamPlan(statusCode, "application/json", Array.Empty<string>(), false, 0, false);
    internal static P3DA2StreamPlan Sse(params string[] frames) => new P3DA2StreamPlan(200, "text/event-stream", frames, true, 0, false);
    internal static P3DA2StreamPlan SseWithoutDone(params string[] frames) => new P3DA2StreamPlan(200, "text/event-stream", frames, false, 0, false);
    internal static P3DA2StreamPlan SseRaw(params string[] frames) => new P3DA2StreamPlan(200, "text/event-stream", frames, false, 0, false);
    internal static P3DA2StreamPlan SseBlockedAfterFirst(params string[] frames) => new P3DA2StreamPlan(200, "text/event-stream", frames, false, 1, true);
}

internal sealed class P3DA2FakeProviderHttpServer : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private readonly Task acceptLoop;
    private readonly object sync = new object();
    private readonly Queue<P3DA2StreamPlan> plans = new Queue<P3DA2StreamPlan>();
    private readonly List<Dictionary<string, object?>> observations = new List<Dictionary<string, object?>>();
    private int requestCount;

    internal P3DA2FakeProviderHttpServer()
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        acceptLoop = AcceptLoopAsync();
    }

    internal int Port { get; }
    internal int RequestCount => Volatile.Read(ref requestCount);
    internal IReadOnlyList<Dictionary<string, object?>> Observations
    {
        get
        {
            lock (sync) return observations.Select(item => new Dictionary<string, object?>(item, StringComparer.Ordinal)).ToList().AsReadOnly();
        }
    }

    internal void Enqueue(P3DA2StreamPlan plan)
    {
        lock (sync) plans.Enqueue(plan);
    }

    private async Task AcceptLoopAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                _ = HandleClientAsync(client);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (cancellation.IsCancellationRequested) break;
            }
            finally
            {
                if (client == null) await Task.Yield();
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                var request = await ReadRequestAsync(client.GetStream(), timeout.Token).ConfigureAwait(false);
                Interlocked.Increment(ref requestCount);
                var plan = DequeuePlan();
                lock (sync)
                {
                    observations.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["method"] = request.Method,
                        ["path"] = request.Path,
                        ["status"] = plan.StatusCode,
                        ["content_type"] = plan.ContentType ?? string.Empty
                    });
                }

                if (plan.StatusCode != 200)
                {
                    await WriteResponseAsync(client.GetStream(), plan.StatusCode, "application/json", "{}", timeout.Token).ConfigureAwait(false);
                    return;
                }

                var body = BuildBody(plan);
                var bytes = Encoding.UTF8.GetBytes(body);
                var header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: " + plan.ContentType + "\r\nContent-Length: " + bytes.Length.ToString() + "\r\nConnection: close\r\n\r\n");
                var stream = client.GetStream();
                await stream.WriteAsync(header, 0, header.Length, timeout.Token).ConfigureAwait(false);
                await stream.FlushAsync(timeout.Token).ConfigureAwait(false);
                var chunks = BuildChunks(plan);
                for (var index = 0; index < chunks.Count; index++)
                {
                    var chunk = Encoding.UTF8.GetBytes(chunks[index]);
                    await stream.WriteAsync(chunk, 0, chunk.Length, timeout.Token).ConfigureAwait(false);
                    await stream.FlushAsync(timeout.Token).ConfigureAwait(false);
                    if (plan.BlockAfterFirstFrame && index == 0)
                    {
                        await WaitForClientCloseAsync(stream, timeout.Token).ConfigureAwait(false);
                        return;
                    }

                    if (plan.CloseAfterFrames > 0 && index + 1 >= plan.CloseAfterFrames) return;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
        }
    }

    private P3DA2StreamPlan DequeuePlan()
    {
        lock (sync) return plans.Count == 0 ? P3DA2StreamPlan.Status(500) : plans.Dequeue();
    }

    private static string BuildBody(P3DA2StreamPlan plan)
    {
        if (plan.ContentType == "application/json") return "{}";
        return string.Concat(BuildChunks(plan));
    }

    private static List<string> BuildChunks(P3DA2StreamPlan plan)
    {
        var chunks = new List<string>();
        for (var index = 0; index < plan.Frames.Count; index++)
        {
            if (plan.ContentType == "text/event-stream") chunks.Add("data: " + plan.Frames[index] + "\r\n\r\n");
            else chunks.Add(plan.Frames[index] + "\n");
        }

        if (plan.IncludeDone) chunks.Add("data: [DONE]\r\n\r\n");
        return chunks;
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int statusCode, string contentType, string body, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var reason = statusCode == 401 ? "Unauthorized" : statusCode == 408 ? "Request Timeout" : statusCode == 503 ? "Service Unavailable" : "Error";
        var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + statusCode.ToString() + " " + reason + "\r\nContent-Type: " + contentType + "\r\nContent-Length: " + bytes.Length.ToString() + "\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForClientCloseAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (read == 0) return;
            }
        }
        catch (IOException)
        {
        }
    }

    private static async Task<P3DA2HttpRequest> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
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
                    var header = Encoding.ASCII.GetString(data.ToArray(), 0, headerEnd);
                    foreach (var line in header.Split(new[] { "\r\n" }, StringSplitOptions.None))
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) && int.TryParse(line.Substring("Content-Length:".Length).Trim(), out var value)) contentLength = Math.Min(value, 1_048_576);
                    }
                }
            }
        }

        var headerText = Encoding.ASCII.GetString(data.ToArray(), 0, headerEnd);
        var requestLine = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None)[0].Split(' ');
        return new P3DA2HttpRequest(requestLine.Length > 1 ? requestLine[0] : string.Empty, requestLine.Length > 1 ? requestLine[1] : string.Empty);
    }

    private static int FindHeaderEnd(List<byte> data)
    {
        for (var index = 0; index <= data.Count - 4; index++)
        {
            if (data[index] == 13 && data[index + 1] == 10 && data[index + 2] == 13 && data[index + 3] == 10) return index;
        }

        return -1;
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
}

internal sealed class P3DA2HttpRequest
{
    internal P3DA2HttpRequest(string method, string path)
    {
        Method = method;
        Path = path;
    }

    internal string Method { get; }
    internal string Path { get; }
}
