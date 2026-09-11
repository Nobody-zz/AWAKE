using System;
using System.Text;
using MarcusAwakeFramework.Api;

namespace MarcusAwakeFramework.Tests
{
    internal static class AiGatewayContractTests
    {
        internal static void StructuredJsonBounds()
        {
            string error;
            AssertEx.True(StructuredJsonContract.TryValidateObject("{\"answer\":\"ok\"}", out error), "structured_json_valid_object_rejected");

            var oversized = "{\"value\":\"" + new string('x', StructuredJsonContract.MaximumBytes) + "\"}";
            AssertEx.True(!StructuredJsonContract.TryValidateObject(oversized, out error), "structured_json_oversize_accepted");
            AssertEx.Equal("structured_json_too_large", error, "structured_json_oversize_error_mismatch");

            var deep = "{}";
            for (var level = 0; level < StructuredJsonContract.MaximumDepth + 1; level++) deep = "{\"nested\":" + deep + "}";
            AssertEx.True(!StructuredJsonContract.TryValidateObject(deep, out error), "structured_json_depth_accepted");
            AssertEx.Equal("structured_json_depth_exceeded", error, "structured_json_depth_error_mismatch");

            var properties = new StringBuilder("{");
            for (var index = 0; index < StructuredJsonContract.MaximumProperties + 1; index++)
            {
                if (index > 0) properties.Append(',');
                properties.Append("\"p").Append(index).Append("\":0");
            }
            properties.Append('}');
            AssertEx.True(!StructuredJsonContract.TryValidateObject(properties.ToString(), out error), "structured_json_property_limit_accepted");
            AssertEx.Equal("structured_json_property_limit", error, "structured_json_property_error_mismatch");
        }

        internal static void EventStructuredResultAndRouteMetadata()
        {
            var taskEvent = new AiTaskEvent(
                "task.event",
                "message.event",
                AiTaskEventKind.RouteChanged,
                3,
                string.Empty,
                null,
                "model-one",
                12,
                4,
                "{\"answer\":\"ok\"}",
                "provider-two",
                "provider-one",
                "provider-two");
            AssertEx.Equal("{\"answer\":\"ok\"}", taskEvent.StructuredJson, "event_structured_json_lost");
            AssertEx.Equal("provider-two", taskEvent.ActiveProviderId, "event_active_provider_lost");
            AssertEx.Equal("provider-one", taskEvent.FromProviderId, "event_from_provider_lost");
            AssertEx.Equal("provider-two", taskEvent.ToProviderId, "event_to_provider_lost");
            AssertEx.Equal(12, taskEvent.InputTokens, "event_input_tokens_lost");
            AssertEx.Equal(4, taskEvent.OutputTokens, "event_output_tokens_lost");

            var handle = new AiTaskHandle("task.route", "message.route");
            AssertEx.True(handle.Publish(new AiTaskEvent(handle.TaskId, handle.MessageId, AiTaskEventKind.Accepted, 1, string.Empty, null, string.Empty, 0, 0)).IsSuccess, "route_accepted_rejected");
            AssertEx.True(handle.Publish(new AiTaskEvent(handle.TaskId, handle.MessageId, AiTaskEventKind.Started, 2, string.Empty, null, string.Empty, 0, 0)).IsSuccess, "route_started_rejected");
            AssertEx.True(handle.Publish(new AiTaskEvent(handle.TaskId, handle.MessageId, AiTaskEventKind.RouteChanged, 3, string.Empty, null, string.Empty, 0, 0, string.Empty, "provider-two", "provider-one", "provider-two")).IsSuccess, "route_change_rejected");
            var snapshot = handle.Snapshot();
            AssertEx.Equal("provider-one", snapshot[2].FromProviderId, "route_snapshot_from_provider_lost");
            AssertEx.Equal("provider-two", snapshot[2].ToProviderId, "route_snapshot_to_provider_lost");

            var lateRoute = handle.Publish(new AiTaskEvent(handle.TaskId, handle.MessageId, AiTaskEventKind.TextDelta, 4, "visible", null, string.Empty, 0, 0));
            AssertEx.True(lateRoute.IsSuccess, "route_visible_text_rejected");
            var invalidRoute = handle.Publish(new AiTaskEvent(handle.TaskId, handle.MessageId, AiTaskEventKind.RouteChanged, 5, string.Empty, null, string.Empty, 0, 0, string.Empty, "provider-three", "provider-two", "provider-three"));
            AssertEx.Error(invalidRoute, "runtime.route_change_after_visible_text", FrameworkErrorCategory.InvalidRequest);
        }

        internal static void HandleTerminalDisposeCancel()
        {
            var cancellationHandle = new AiTaskHandle("task.cancel.contract", "message.cancel.contract");
            AssertEx.True(cancellationHandle.Publish(new AiTaskEvent(cancellationHandle.TaskId, cancellationHandle.MessageId, AiTaskEventKind.Accepted, 1, string.Empty, null, string.Empty, 0, 0)).IsSuccess, "cancel_accepted_rejected");
            AssertEx.True(cancellationHandle.Publish(new AiTaskEvent(cancellationHandle.TaskId, cancellationHandle.MessageId, AiTaskEventKind.Started, 2, string.Empty, null, string.Empty, 0, 0)).IsSuccess, "cancel_started_rejected");
            var cancellation = cancellationHandle.CancelAsync(new System.Threading.CancellationTokenSource().Token).GetAwaiter().GetResult();
            AssertEx.True(cancellation.IsSuccess && cancellation.Value, "cancel_terminal_not_recorded");
            var duplicateCancellation = cancellationHandle.CancelAsync(new System.Threading.CancellationTokenSource().Token).GetAwaiter().GetResult();
            AssertEx.True(duplicateCancellation.IsSuccess && !duplicateCancellation.Value, "duplicate_cancel_not_idempotent");

            var completedHandle = new AiTaskHandle("task.dispose.contract", "message.dispose.contract");
            AssertEx.True(completedHandle.Publish(new AiTaskEvent(completedHandle.TaskId, completedHandle.MessageId, AiTaskEventKind.Accepted, 1, string.Empty, null, string.Empty, 0, 0)).IsSuccess, "dispose_accepted_rejected");
            AssertEx.True(completedHandle.Publish(new AiTaskEvent(completedHandle.TaskId, completedHandle.MessageId, AiTaskEventKind.Started, 2, string.Empty, null, string.Empty, 0, 0)).IsSuccess, "dispose_started_rejected");
            AssertEx.True(completedHandle.Publish(new AiTaskEvent(completedHandle.TaskId, completedHandle.MessageId, AiTaskEventKind.Completed, 3, string.Empty, null, string.Empty, 0, 0, "{\"done\":true}", string.Empty, string.Empty, string.Empty)).IsSuccess, "completed_terminal_not_recorded");
            var duplicateTerminal = completedHandle.Publish(new AiTaskEvent(completedHandle.TaskId, completedHandle.MessageId, AiTaskEventKind.Completed, 4, string.Empty, null, string.Empty, 0, 0));
            AssertEx.Error(duplicateTerminal, "runtime.terminal_event_duplicate", FrameworkErrorCategory.Conflict);
            completedHandle.Dispose();
            var disposed = completedHandle.Publish(new AiTaskEvent(completedHandle.TaskId, completedHandle.MessageId, AiTaskEventKind.TextDelta, 4, "late", null, string.Empty, 0, 0));
            AssertEx.Error(disposed, "runtime.task_disposed", FrameworkErrorCategory.Expired);
        }

        internal static void DeferredSettlementMapping()
        {
            var budget = new RuntimeResourceBudget(1024, 4096, 512, 16);
            var required = new AiTaskRequest("task.required", "message.required", "dialogue", "provider.example", "profile.example", "{\"topic\":\"world\"}", new SchemaRef("npc.reply", new ApiVersion(1, 0)), "public", DateTimeOffset.UtcNow.AddMinutes(1), true, budget, "idempotency.required");
            var deferred = new AiTaskRequest("task.deferred", "message.deferred", "dialogue", "provider.example", "profile.example", "{\"topic\":\"world\"}", new SchemaRef("npc.reply", new ApiVersion(1, 0)), "public", DateTimeOffset.UtcNow.AddMinutes(1), false, budget, "idempotency.deferred");
            AssertEx.Equal(SettlementRequirement.Required, required.SettlementRequirement, "required_settlement_mapping_invalid");
            AssertEx.Equal(SettlementRequirement.NotApplicable, deferred.SettlementRequirement, "deferred_settlement_mapping_invalid");

            var requiredCanonical = TaskRequestCanonicalizer.ForTask(new TaskRequestCanonicalInput("dialogue", "provider.example", "profile.example", "message.required", required.InputJson, required.OutputSchema, "required", 1024, 4096, 512, 16), "correlation.required");
            var deferredCanonical = TaskRequestCanonicalizer.ForTask(new TaskRequestCanonicalInput("dialogue", "provider.example", "profile.example", "message.deferred", deferred.InputJson, deferred.OutputSchema, "not_applicable", 1024, 4096, 512, 16), "correlation.deferred");
            AssertEx.True(requiredCanonical.IsSuccess && requiredCanonical.Value.CanonicalJson.Contains("\"settlement_requirement\":\"required\""), "required_canonical_settlement_missing");
            AssertEx.True(deferredCanonical.IsSuccess && deferredCanonical.Value.CanonicalJson.Contains("\"settlement_requirement\":\"not_applicable\""), "deferred_canonical_settlement_missing");
        }

        internal static void UnknownErrorFailClosed()
        {
            var values = new[] { (string)null, "   ", "invalidrequest" };
            for (var index = 0; index < values.Length; index++)
            {
                var mapping = FrameworkErrors.MapProviderError(values[index], "provider.example", "correlation.example");
                AssertEx.Equal("Unknown", mapping.ProviderCategory, "unknown_provider_category_not_normalized");
                AssertEx.Equal(FrameworkErrorCategory.InternalFailure, mapping.CoreCategory, "unknown_provider_category_not_fail_closed");
                AssertEx.True(!mapping.Retryable && !mapping.FallbackAllowed, "unknown_provider_category_flags_open");
            }
        }

        internal static void TaskScopeSemanticIdentity()
        {
            var caller = new ExtensionId("awake.contract");
            var session = new SessionRef("campaign.contract", "timeline.contract", "session.contract");
            var scope = CreateScope(caller.Value, session, "route.one", "provider.one", "profile.one", "correlation.contract", "a");
            AssertEx.True(scope.BelongsTo(caller, session, 1, "correlation.contract"), "scope_exact_identity_rejected");
            AssertEx.True(!scope.BelongsTo(new ExtensionId("awake.other"), session, 1, "correlation.contract"), "scope_owner_mismatch_accepted");
            AssertEx.True(!scope.BelongsTo(caller, new SessionRef("campaign.other", "timeline.contract", "session.contract"), 1, "correlation.contract"), "scope_session_mismatch_accepted");
            AssertEx.True(!scope.BelongsTo(caller, session, 2, "correlation.contract"), "scope_generation_mismatch_accepted");
            AssertEx.True(!scope.BelongsTo(caller, session, 1, "correlation.other"), "scope_correlation_mismatch_accepted");

            var routeVariant = CreateScope(caller.Value, session, "route.two", "provider.one", "profile.one", "correlation.contract", "a");
            var providerVariant = CreateScope(caller.Value, session, "route.one", "provider.two", "profile.one", "correlation.contract", "a");
            var profileVariant = CreateScope(caller.Value, session, "route.one", "provider.one", "profile.two", "correlation.contract", "a");
            AssertEx.True(!StringComparer.Ordinal.Equals(scope.IdempotencyScopeKey, routeVariant.IdempotencyScopeKey), "route_missing_from_scope_identity");
            AssertEx.True(!StringComparer.Ordinal.Equals(scope.IdempotencyScopeKey, providerVariant.IdempotencyScopeKey), "provider_missing_from_scope_identity");
            AssertEx.True(!StringComparer.Ordinal.Equals(scope.IdempotencyScopeKey, profileVariant.IdempotencyScopeKey), "profile_missing_from_scope_identity");

            var canonical = TaskRequestCanonicalizer.ForTask(new TaskRequestCanonicalInput("route.one", "provider.one", "profile.one", "message.contract", "{\"value\":1}", new SchemaRef("reply", new ApiVersion(1, 0)), "not_applicable", 1024, 4096, 512, 16), "correlation.contract");
            AssertEx.True(canonical.IsSuccess && canonical.Value.Hash.Length == 64, "semantic_hash_not_generated");
            var invalid = TaskRequestCanonicalizer.CanonicalizeJson("{\"value\":01}", TaskRequestCanonicalizer.TaskDomain, correlationId: "correlation.invalid");
            AssertEx.Error(invalid, "canonical_number_noncanonical", FrameworkErrorCategory.InvalidRequest);
        }

        private static AiTaskScope CreateScope(string ownerId, SessionRef session, string routeId, string providerId, string profileId, string correlationId, string hashSeed)
        {
            return new AiTaskScope(
                "task." + routeId + "." + providerId + "." + profileId,
                "message." + routeId + "." + providerId + "." + profileId,
                ownerId,
                session,
                1,
                correlationId,
                "causation.contract",
                routeId,
                providerId,
                profileId,
                "idempotency.contract",
                new string(hashSeed[0], 64),
                new SchemaRef("reply", new ApiVersion(1, 0)),
                SettlementRequirement.NotApplicable);
        }
    }
}
