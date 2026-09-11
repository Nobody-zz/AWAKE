using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using MarcusAwakeTransport;

namespace MarcusAwakeFramework.Tests
{
    internal static class P3DA2FrameworkTests
    {
        internal static void Run()
        {
            var cases = new (string Id, Action RunCase)[]
            {
                ("P3D-A2-S01-stream-transcript-projection", StreamTranscriptProjection),
                ("P3D-A2-S02-stream-failure-and-cancelled", StreamFailureAndCancellation),
                ("P3D-A2-S03-unary-structured-fallback", UnaryStructuredFallback),
                ("P3D-A2-S04-budget-and-capability-negatives", BudgetAndCapabilityNegatives),
                ("P3D-A2-S05-wire-parser-negatives", WireParserNegatives),
                ("P3D-A2-S06-dynamic-terminal-race", DynamicTerminalRace)
            };

            for (var index = 0; index < cases.Length; index++)
            {
                Console.WriteLine("CASE " + cases[index].Id + " START");
                cases[index].RunCase();
                Console.WriteLine("CASE " + cases[index].Id + " PASS");
            }

            Console.WriteLine("P3D-A2-FRAMEWORK PASS " + cases.Length);
        }

        private static void StreamTranscriptProjection()
        {
            var request = CreateRequest("task.a2.stream", true, new RuntimeResourceBudget(8, 4096, 32, 4));
            var scope = new ProviderScopeRequest(request.ProfileId, request.ProviderId, request.RouteId);
            var frames = new FakeServiceIpc(new[]
            {
                new FakeIpcFrame(101, 1, "started", StreamEventJson(request, "provider.primary", "started", 1)),
                new FakeIpcFrame(102, 2, "route_changed", StreamEventJson(request, "provider.primary", "route_changed", 2, fromProviderId: "provider.primary", toProviderId: "provider.fallback")),
                new FakeIpcFrame(103, 3, "text_delta", StreamEventJson(request, "provider.fallback", "text_delta", 3, text: "answer")),
                new FakeIpcFrame(104, 4, "usage_update", StreamEventJson(request, "provider.fallback", "usage_update", 4, inputTokens: 3, outputTokens: 5)),
                new FakeIpcFrame(105, 5, "completed", StreamEventJson(request, "provider.fallback", "completed", 5, structuredJson: "{\"answer\":\"ok\"}"))
            }).ReadAll();
            var stateMachine = new ProviderStreamStateMachine(request.TaskId, request.ProviderId, request.ProfileId);
            var budget = new RuntimeServiceClient.RuntimeStreamBudgetTracker(request.Budget);
            var parsed = new List<ProviderStreamEventWire>();
            var expectedIpcSequence = 101L;

            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                AssertEx.Equal(expectedIpcSequence++, frame.IpcSequence, "fake_ipc_sequence_not_contiguous");
                AssertEx.Equal(ProtocolConstants.MessageTypeProviderStreamEvent, frame.MessageType, "fake_ipc_message_type_invalid");
                AssertEx.Equal(ProviderRuntimeSchema.StreamEvent, frame.PayloadSchema, "fake_ipc_payload_schema_invalid");
                AssertEx.Equal(frame.StreamSequence, frame.EventIndex, "fake_ipc_event_index_invalid");
                AssertEx.True(ProviderRuntimeWire.TryReadStreamEvent(frame.PayloadJson, scope, out var streamEvent, out var error), "stream_event_parse_failed:" + error);
                AssertEx.Equal(frame.EventKind, streamEvent.EventKind, "stream_event_kind_not_preserved");
                var terminal = streamEvent.EventKind == "completed" || streamEvent.EventKind == "cancelled" || streamEvent.EventKind == "failed";
                var decision = stateMachine.Accept(streamEvent.EventKind, streamEvent.StreamSequence, streamEvent.ProviderId, streamEvent.FromProviderId ?? string.Empty, streamEvent.ToProviderId ?? string.Empty, terminal, streamEvent.Text != null, streamEvent.Usage != null, streamEvent.Error != null, streamEvent.StructuredJson != null);
                AssertEx.True(decision.IsAccepted, "stream_state_machine_rejected:" + decision.ErrorCode);
                AssertEx.True(budget.Accept(streamEvent, out error), "stream_budget_rejected:" + error);
                parsed.Add(streamEvent);
            }

            AssertEx.Equal(ProviderStreamState.Terminal, stateMachine.State, "stream_did_not_reach_terminal");
            AssertEx.Equal(1, budget.TextDeltas, "stream_text_delta_count_invalid");
            AssertEx.Equal(3, budget.InputTokens, "stream_input_usage_invalid");
            AssertEx.Equal(5, budget.OutputTokens, "stream_output_usage_invalid");

            var handle = CreateStartedHandle(request);
            var projection = RuntimeServiceClient.ProjectStreamEvents(handle, request, parsed, "corr.a2.stream");
            AssertEx.True(projection.IsSuccess, "stream_framework_projection_failed:" + Describe(projection.Error));
            var events = handle.Snapshot();
            AssertEx.Equal(6, events.Count, "stream_framework_event_count_invalid");
            AssertEx.Equal(AiTaskEventKind.Accepted, events[0].Kind, "accepted_event_missing");
            AssertEx.Equal(AiTaskEventKind.Started, events[1].Kind, "started_event_missing");
            AssertEx.Equal(AiTaskEventKind.RouteChanged, events[2].Kind, "route_changed_event_missing");
            AssertEx.Equal("provider.primary", events[2].FromProviderId, "route_from_provider_invalid");
            AssertEx.Equal("provider.fallback", events[2].ToProviderId, "route_to_provider_invalid");
            AssertEx.Equal(AiTaskEventKind.TextDelta, events[3].Kind, "text_delta_event_missing");
            AssertEx.Equal("answer", events[3].Text, "text_delta_text_invalid");
            AssertEx.Equal(AiTaskEventKind.UsageUpdate, events[4].Kind, "usage_event_missing");
            AssertEx.Equal(3, events[4].InputTokens, "usage_input_tokens_invalid");
            AssertEx.Equal(5, events[4].OutputTokens, "usage_output_tokens_invalid");
            AssertEx.Equal(AiTaskEventKind.Completed, events[5].Kind, "completed_event_missing");
            AssertEx.Equal(string.Empty, events[5].Text, "completed_text_must_be_empty");
            AssertEx.Equal("{\"answer\":\"ok\"}", events[5].StructuredJson, "structured_result_not_preserved");
            AssertEx.Equal("provider.fallback", events[5].ActiveProviderId, "completed_provider_invalid");
            AssertEx.Equal(6L, events[5].Sequence, "dynamic_terminal_sequence_invalid");
        }

        private static void StreamFailureAndCancellation()
        {
            var request = CreateRequest("task.a2.failed", false);
            var failedHandle = CreateStartedHandle(request);
            var failedEvents = new List<ProviderStreamEventWire>
            {
                new ProviderStreamEventWire { EventKind = "started", StreamSequence = 1, ProviderId = request.ProviderId, ProfileId = request.ProfileId, RouteId = request.RouteId, ModelId = "model.a2" },
                new ProviderStreamEventWire { EventKind = "failed", StreamSequence = 2, ProviderId = request.ProviderId, ProfileId = request.ProfileId, RouteId = request.RouteId, ModelId = "model.a2", Error = new ProviderStreamErrorWire { ErrorCode = "provider.unavailable", Category = "unavailable", Retryable = true, SafeMessage = "temporary" } }
            };
            var failed = RuntimeServiceClient.ProjectStreamEvents(failedHandle, request, failedEvents, "corr.a2.failed");
            AssertEx.True(failed.IsSuccess, "failed_stream_projection_failed:" + Describe(failed.Error));
            var failedSnapshot = failedHandle.Snapshot();
            AssertEx.Equal(AiTaskEventKind.Failed, failedSnapshot[2].Kind, "failed_terminal_missing");
            AssertEx.Equal(FrameworkErrorCategory.Unavailable, failedSnapshot[2].Error.Category, "failed_error_category_invalid");
            AssertEx.True(failedSnapshot[2].Error.SafeFallback.IndexOf("temporary", StringComparison.Ordinal) >= 0, "safe_provider_message_missing");

            var cancelledRequest = CreateRequest("task.a2.cancelled", false);
            var cancelledHandle = CreateStartedHandle(cancelledRequest);
            var cancelledEvents = new List<ProviderStreamEventWire>
            {
                new ProviderStreamEventWire { EventKind = "started", StreamSequence = 1, ProviderId = cancelledRequest.ProviderId, ProfileId = cancelledRequest.ProfileId, RouteId = cancelledRequest.RouteId, ModelId = "model.a2" },
                new ProviderStreamEventWire { EventKind = "cancelled", StreamSequence = 2, ProviderId = cancelledRequest.ProviderId, ProfileId = cancelledRequest.ProfileId, RouteId = cancelledRequest.RouteId, ModelId = "model.a2", Error = new ProviderStreamErrorWire { ErrorCode = "request.cancelled", Category = "cancelled", Retryable = false, SafeMessage = "cancelled" } }
            };
            var cancelled = RuntimeServiceClient.ProjectStreamEvents(cancelledHandle, cancelledRequest, cancelledEvents, "corr.a2.cancelled");
            AssertEx.True(cancelled.IsSuccess, "cancelled_stream_projection_failed:" + Describe(cancelled.Error));
            var cancelledSnapshot = cancelledHandle.Snapshot();
            AssertEx.Equal(AiTaskEventKind.Cancelled, cancelledSnapshot[2].Kind, "cancelled_terminal_missing");
            AssertEx.True(cancelledSnapshot[2].Error == null, "cancelled_terminal_carried_provider_error");
        }

        private static void UnaryStructuredFallback()
        {
            var request = CreateRequest("task.a2.unary", true);
            var handle = CreateStartedHandle(request);
            var result = new CompletionResultWire
            {
                Schema = ProviderRuntimeSchema.Result,
                ProfileId = request.ProfileId,
                ProviderId = request.ProviderId,
                RouteId = request.RouteId,
                ModelId = "model.unary",
                Content = "fallback answer",
                StructuredJson = "{\"answer\":\"fallback\"}",
                Usage = new UsageWire { InputTokens = 7, OutputTokens = 9 }
            };
            var projected = RuntimeServiceClient.ProjectUnaryCompletion(handle, request, result, "corr.a2.unary");
            AssertEx.True(projected.IsSuccess, "unary_projection_failed:" + Describe(projected.Error));
            var events = handle.Snapshot();
            AssertEx.Equal(5, events.Count, "unary_projection_event_count_invalid");
            AssertEx.Equal(AiTaskEventKind.TextDelta, events[2].Kind, "unary_text_delta_missing");
            AssertEx.Equal("fallback answer", events[2].Text, "unary_text_invalid");
            AssertEx.Equal(AiTaskEventKind.UsageUpdate, events[3].Kind, "unary_usage_missing");
            AssertEx.Equal(AiTaskEventKind.Completed, events[4].Kind, "unary_completed_missing");
            AssertEx.Equal(string.Empty, events[4].Text, "unary_completed_text_must_be_empty");
            AssertEx.Equal(result.StructuredJson, events[4].StructuredJson, "unary_structured_result_not_preserved");
            AssertEx.Equal(RuntimeAiTransportMode.Stream, RuntimeServiceClient.SelectAiTransport(new[] { ProtocolConstants.CapabilityProviderCompleteV1, ProtocolConstants.CapabilityProviderStreamV1 } ), "stream_must_be_preferred");
            AssertEx.Equal(RuntimeAiTransportMode.Unary, RuntimeServiceClient.SelectAiTransport(new[] { ProtocolConstants.CapabilityProviderCompleteV1 }), "unary_fallback_not_selected");
        }

        private static void BudgetAndCapabilityNegatives()
        {
            var legacy = new RuntimeResourceBudget(1024, 4096, 512, 16);
            AssertEx.Equal(RuntimeResourceBudget.MaximumStreamFrameCount, legacy.FrameCount, "legacy_budget_frame_default_changed");
            var projected = RuntimeServiceClient.ProjectStreamBudget(new RuntimeResourceBudget(500, 999999, 99999, 99999, 99999), ProviderStreamBudgetDescriptor.Default(), "corr.a2.budget");
            AssertEx.True(projected.IsSuccess, "budget_projection_failed:" + Describe(projected.Error));
            AssertEx.Equal(RuntimeResourceBudget.MaximumStreamFrameCount, projected.Value.FrameCount, "frame_budget_cap_invalid");
            AssertEx.Equal(RuntimeService.MaximumOutputBytes, projected.Value.OutputBytes, "output_budget_cap_invalid");
            AssertEx.Equal(RuntimeService.MaximumTokens, projected.Value.Tokens, "token_budget_cap_invalid");
            AssertEx.Equal(RuntimeService.MaximumTextDeltas, projected.Value.TextDeltas, "delta_budget_cap_invalid");

            var frameZero = RuntimeServiceClient.ProjectStreamBudget(new RuntimeResourceBudget(0, 1, 1, 1, 1), ProviderStreamBudgetDescriptor.Default(), "corr.a2.frame-zero");
            AssertEx.Error(frameZero, "stream_budget_invalid", FrameworkErrorCategory.InvalidRequest);
            var frameOne = RuntimeServiceClient.ProjectStreamBudget(new RuntimeResourceBudget(1, 1, 1, 1, 1), ProviderStreamBudgetDescriptor.Default(), "corr.a2.frame-one");
            AssertEx.Error(frameOne, "stream_budget_invalid", FrameworkErrorCategory.InvalidRequest);

            var options = new RuntimeServiceClientOptions();
            AssertEx.True(options.RequestedCapabilities.Contains(ProtocolConstants.CapabilityProviderStreamV1), "default_stream_capability_not_requested");
            AssertEx.True(RuntimeServiceClient.SameCapabilities(
                new[] { ProtocolConstants.MessageTypeHealth, ProtocolConstants.CapabilityProviderCompleteV1, ProtocolConstants.CapabilityProviderStreamV1 },
                new[] { ProtocolConstants.MessageTypeHealth, ProtocolConstants.CapabilityProviderCompleteV1 }), "capability_subset_not_accepted");
            AssertEx.True(!RuntimeServiceClient.SameCapabilities(
                new[] { ProtocolConstants.MessageTypeHealth, ProtocolConstants.CapabilityProviderCompleteV1 },
                new[] { ProtocolConstants.MessageTypeHealth, "provider.unknown" }), "unrequested_capability_accepted");
            AssertEx.True(!RuntimeServiceClient.SameCapabilities(
                new[] { ProtocolConstants.MessageTypeHealth },
                new[] { ProtocolConstants.CapabilityProviderCompleteV1 }), "healthless_capability_response_accepted");
            AssertEx.Equal(RuntimeAiTransportMode.Unavailable, RuntimeServiceClient.SelectAiTransport(new[] { ProtocolConstants.MessageTypeHealth }), "missing_provider_capabilities_not_rejected");

            var tracker = new RuntimeServiceClient.RuntimeStreamBudgetTracker(new RuntimeResourceBudget(5, 100, 20, 2));
            AssertEx.True(tracker.Accept(new ProviderStreamEventWire { EventKind = "usage_update", Usage = new ProviderStreamUsageWire { InputTokens = 5, OutputTokens = 6 } }, out var error), "initial_usage_rejected:" + error);
            AssertEx.True(!tracker.Accept(new ProviderStreamEventWire { EventKind = "usage_update", Usage = new ProviderStreamUsageWire { InputTokens = 4, OutputTokens = 7 } }, out error), "non_monotonic_usage_accepted");
            AssertEx.Equal("usage_non_monotonic", error, "non_monotonic_usage_error_invalid");
            AssertEx.Equal(5, tracker.InputTokens, "rejected_usage_mutated_input");
            AssertEx.Equal(6, tracker.OutputTokens, "rejected_usage_mutated_output");
        }

        private static void WireParserNegatives()
        {
            var request = CreateRequest("task.a2.parser", true);
            var scope = new ProviderScopeRequest(request.ProfileId, request.ProviderId, request.RouteId);
            var validStarted = StreamEventJson(request, request.ProviderId, "started", 1);
            AssertEx.True(ProviderRuntimeWire.TryReadStreamEvent(validStarted, scope, out _, out var error), "valid_started_rejected:" + error);

            AssertRejected(validStarted.Substring(0, validStarted.Length - 1) + ",\"unknown\":1}", scope, "unknown_field");
            AssertRejected(StreamEventJson(request, request.ProviderId, "text_delta", 2, text: string.Empty), scope, "empty_text_delta");
            AssertRejected(StreamEventJson(request, request.ProviderId, "route_changed", 2, fromProviderId: request.ProviderId, toProviderId: request.ProviderId), scope, "same_route_provider");
            AssertRejected(StreamEventJson(request, request.ProviderId, "completed", 2, text: "must-not-be-terminal-text"), scope, "completed_text");
            AssertRejected(StreamEventJson(request, request.ProviderId, "completed", 2, structuredJson: "[]"), scope, "structured_array");
            AssertRejected(StreamEventJson(request, request.ProviderId, "completed", 2, terminal: false), scope, "terminal_flag");
            AssertRejected(StreamEventJson(request, "wrong.provider", "started", 1), scope, "provider_identity");
            AssertRejected(StreamEventJson(request, request.ProviderId, "usage_update", 2, usageWithoutValues: true), scope, "empty_usage");
            AssertRejected(StreamEventJson(request, request.ProviderId, "completed", 2, structuredJson: "{\"a\":1,\"a\":2}"), scope, "duplicate_structured_key");

            var completionRequest = CreateRequest("task.a2.completion-parser", false);
            var invalidCompletion = "{\"schema\":\"" + ProviderRuntimeSchema.Result + "\",\"profile_id\":\"" + completionRequest.ProfileId + "\",\"provider_id\":\"" + completionRequest.ProviderId + "\",\"route_id\":\"" + completionRequest.RouteId + "\",\"model_id\":\"model\",\"content\":\"answer\",\"structured_json\":[]}";
            AssertEx.True(!ProviderRuntimeWire.TryReadCompletionResult(invalidCompletion, completionRequest, out _, out _), "unary_structured_array_accepted");
        }

        private static void DynamicTerminalRace()
        {
            var request = CreateRequest("task.a2.race", false);
            var handle = CreateStartedHandle(request);
            var terminalResults = Task.WhenAll(
                Task.Run(() => handle.PublishTerminal(AiTaskEventKind.Completed, string.Empty, null, "model.race", 0, 0)),
                Task.Run(() => handle.PublishTerminal(AiTaskEventKind.Cancelled, string.Empty, null, "model.race", 0, 0)))
                .GetAwaiter().GetResult();
            AssertEx.Equal(1, terminalResults.Count(result => result.IsSuccess && result.Value), "terminal_race_allowed_multiple_winners");
            AssertEx.Equal(1, terminalResults.Count(result => result.IsSuccess && !result.Value), "terminal_race_loser_not_idempotent");
            var snapshot = handle.Snapshot();
            AssertEx.Equal(3, snapshot.Count, "terminal_race_snapshot_count_invalid");
            AssertEx.Equal(1, snapshot.Count(item => item.Kind == AiTaskEventKind.Completed || item.Kind == AiTaskEventKind.Cancelled || item.Kind == AiTaskEventKind.Failed), "terminal_race_terminal_count_invalid");
            AssertEx.Equal(3L, snapshot[2].Sequence, "terminal_race_sequence_not_dynamic");

            var cancellationRequest = CreateRequest("task.a2.local-cancel", false);
            var cancellationHandle = CreateStartedHandle(cancellationRequest);
            AssertEx.True(cancellationHandle.Publish(new AiTaskEvent(cancellationHandle.TaskId, cancellationHandle.MessageId, AiTaskEventKind.TextDelta, 3, "visible", null, "model", 0, 0)).IsSuccess, "pre_cancel_delta_rejected");
            var cancelled = cancellationHandle.CancelAsync(new System.Threading.CancellationTokenSource().Token).GetAwaiter().GetResult();
            AssertEx.True(cancelled.IsSuccess && cancelled.Value, "dynamic_local_cancel_failed");
            AssertEx.Equal(4L, cancellationHandle.Snapshot()[3].Sequence, "local_cancel_sequence_not_dynamic");
        }

        private static AiTaskHandle CreateStartedHandle(AiTaskRequest request)
        {
            var handle = new AiTaskHandle(request.TaskId, request.MessageId);
            AssertEx.True(handle.Publish(new AiTaskEvent(request.TaskId, request.MessageId, AiTaskEventKind.Accepted, 1, string.Empty, null, string.Empty, 0, 0)).IsSuccess, "test_accepted_rejected");
            AssertEx.True(handle.Publish(new AiTaskEvent(request.TaskId, request.MessageId, AiTaskEventKind.Started, 2, string.Empty, null, string.Empty, 0, 0, string.Empty, request.ProviderId, string.Empty, string.Empty)).IsSuccess, "test_started_rejected");
            return handle;
        }

        private static AiTaskRequest CreateRequest(string taskId, bool structured, RuntimeResourceBudget budget = null)
        {
            var inputJson = structured
                ? "{\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}],\"response_schema_json\":\"{\\\"type\\\":\\\"object\\\"}\"}"
                : "{\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}";
            return new AiTaskRequest(
                taskId,
                taskId + ".message",
                "route.a2",
                "provider.primary",
                "profile.a2",
                inputJson,
                new SchemaRef("reply.a2", new ApiVersion(1, 0)),
                "public",
                DateTimeOffset.UtcNow.AddMinutes(1),
                false,
                budget ?? new RuntimeResourceBudget(8, 4096, 32, 4),
                taskId + ".idempotency");
        }

        private static string StreamEventJson(
            AiTaskRequest request,
            string providerId,
            string eventKind,
            long streamSequence,
            string text = null,
            string structuredJson = null,
            int? inputTokens = null,
            int? outputTokens = null,
            string fromProviderId = null,
            string toProviderId = null,
            string errorCode = null,
            string errorCategory = null,
            bool? terminal = null,
            bool usageWithoutValues = false)
        {
            var builder = new StringBuilder("{\"schema\":").Append(Quote(ProviderRuntimeSchema.StreamEvent));
            builder.Append(",\"profile_id\":").Append(Quote(request.ProfileId));
            builder.Append(",\"provider_id\":").Append(Quote(providerId));
            builder.Append(",\"route_id\":").Append(Quote(request.RouteId));
            builder.Append(",\"model_id\":\"model.a2\"");
            builder.Append(",\"event_kind\":").Append(Quote(eventKind));
            builder.Append(",\"stream_sequence\":").Append(streamSequence);
            if (terminal.HasValue) builder.Append(",\"terminal\":").Append(terminal.Value ? "true" : "false");
            if (text != null) builder.Append(",\"text\":").Append(Quote(text));
            if (structuredJson != null) builder.Append(",\"structured_json\":").Append(Quote(structuredJson));
            if (usageWithoutValues) builder.Append(",\"usage\":{}");
            else if (inputTokens.HasValue || outputTokens.HasValue)
            {
                builder.Append(",\"usage\":{");
                var hasPrevious = false;
                if (inputTokens.HasValue)
                {
                    builder.Append("\"input_tokens\":").Append(inputTokens.Value);
                    hasPrevious = true;
                }
                if (outputTokens.HasValue)
                {
                    if (hasPrevious) builder.Append(',');
                    builder.Append("\"output_tokens\":").Append(outputTokens.Value);
                }
                builder.Append('}');
            }
            if (errorCode != null)
            {
                builder.Append(",\"error\":{\"error_code\":").Append(Quote(errorCode));
                builder.Append(",\"category\":").Append(Quote(errorCategory ?? "internal_failure"));
                builder.Append(",\"retryable\":false,\"safe_message\":\"safe\"}");
            }
            if (fromProviderId != null) builder.Append(",\"from_provider_id\":").Append(Quote(fromProviderId));
            if (toProviderId != null) builder.Append(",\"to_provider_id\":").Append(Quote(toProviderId));
            return builder.Append('}').ToString();
        }

        private static void AssertRejected(string payload, ProviderScopeRequest scope, string caseId)
        {
            AssertEx.True(!ProviderRuntimeWire.TryReadStreamEvent(payload, scope, out _, out _), "negative_stream_case_accepted:" + caseId);
        }

        private static string Quote(string value)
        {
            var builder = new StringBuilder("\"");
            for (var index = 0; index < (value ?? string.Empty).Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20) builder.Append("\\u").Append(((int)character).ToString("x4"));
                        else builder.Append(character);
                        break;
                }
            }
            return builder.Append('"').ToString();
        }

        private static string Describe(FrameworkError error)
        {
            return error == null ? string.Empty : error.Code + ":" + error.Category;
        }

        private sealed class FakeServiceIpc
        {
            private readonly IReadOnlyList<FakeIpcFrame> frames;

            internal FakeServiceIpc(IReadOnlyList<FakeIpcFrame> frames)
            {
                this.frames = frames ?? throw new ArgumentNullException(nameof(frames));
            }

            internal IReadOnlyList<FakeIpcFrame> ReadAll()
            {
                return frames;
            }
        }

        private sealed class FakeIpcFrame
        {
            internal FakeIpcFrame(long ipcSequence, long eventIndex, string eventKind, string payloadJson)
            {
                IpcSequence = ipcSequence;
                EventIndex = eventIndex;
                EventKind = eventKind;
                PayloadJson = payloadJson;
            }

            internal long IpcSequence { get; }
            internal long EventIndex { get; }
            internal string EventKind { get; }
            internal string PayloadJson { get; }
            internal long StreamSequence
            {
                get
                {
                    var marker = "\"stream_sequence\":";
                    var start = PayloadJson.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
                    var end = PayloadJson.IndexOf(',', start);
                    if (end < 0) end = PayloadJson.IndexOf('}', start);
                    return long.Parse(PayloadJson.Substring(start, end - start));
                }
            }

            internal string MessageType => ProtocolConstants.MessageTypeProviderStreamEvent;
            internal string PayloadSchema => ProviderRuntimeSchema.StreamEvent;
        }
    }
}
