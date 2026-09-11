using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using MarcusAwakeFramework.Tests.TestDoubles;

namespace MarcusAwakeFramework.Tests
{
    internal static class AssertEx
    {
        public static void True(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        public static void Equal<T>(T expected, T actual, string message) { if (!Equals(expected, actual)) throw new InvalidOperationException(message + " expected=" + expected + " actual=" + actual); }
        public static void Error<T>(OperationResult<T> result, string code, FrameworkErrorCategory category)
        {
            True(result != null && !result.IsSuccess && result.Error != null, "Expected a failed operation.");
            Equal(code, result.Error.Code, "Unexpected error code.");
            Equal(category, result.Error.Category, "Unexpected error category.");
            True(!string.IsNullOrWhiteSpace(result.Error.Owner), "Error owner is missing.");
            True(!string.IsNullOrWhiteSpace(result.Error.CorrelationId), "Error correlation is missing.");
        }
    }

    internal static class Program
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

        private static int Main(string[] args)
        {
            if (HasOption(args, "--verify-evidence")) return VerifyEvidence(args);
            if (HasOption(args, "--runtime-vertical")) return RunRuntimeVertical(args);
            if (HasOption(args, "--p3d-a0-core")) return RunP3dA0Core();
            if (HasOption(args, "--p3d-a2-framework")) return RunP3dA2Framework();
            var tests = new Action[]
            {
                FrameworkCoreVerticalSmokeFixture.Run,
                IdentityAndTypedErrors,
                CapabilityAndPermission,
                SessionAndContext,
                GameDataPaging,
                CommandIdempotencyAndSave,
                IpcSequenceAndBackpressure,
                HostLifecycleAndProbe,
                DefaultHostComposition,
                ServiceOverrideComposition,
                RuntimeLifecycleBarriers,
                P3DA2FrameworkTests.Run
            };
            try
            {
                for (var index = 0; index < tests.Length; index++) tests[index]();
                Console.WriteLine("PASS ALL: " + tests.Length + " Framework Core contract tests");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.ToString());
                return 1;
            }
        }

        private static int RunP3dA0Core()
        {
            var tests = new (string Id, Action Run)[]
            {
                ("P3D-A0-C01-structured_json_bounds", AiGatewayContractTests.StructuredJsonBounds),
                ("P3D-A0-C02-event_structured_result_and_route_metadata", AiGatewayContractTests.EventStructuredResultAndRouteMetadata),
                ("P3D-A0-C03-handle_terminal_dispose_cancel", AiGatewayContractTests.HandleTerminalDisposeCancel),
                ("P3D-A0-C04-deferred_settlement_mapping", AiGatewayContractTests.DeferredSettlementMapping),
                ("P3D-A0-C05-unknown_error_fail_closed", AiGatewayContractTests.UnknownErrorFailClosed),
                ("P3D-A0-C06-task_scope_semantic_identity", AiGatewayContractTests.TaskScopeSemanticIdentity)
            };
            var passed = 0;
            var failed = 0;
            for (var index = 0; index < tests.Length; index++)
            {
                try
                {
                    tests[index].Run();
                    passed++;
                    Console.WriteLine("PASS " + tests[index].Id);
                }
                catch (Exception error)
                {
                    failed++;
                    Console.Error.WriteLine("FAIL " + tests[index].Id + " " + error.Message);
                }
            }

            Console.WriteLine("PASS_COUNT=" + passed);
            Console.WriteLine("FAIL_COUNT=" + failed);
            return failed == 0 ? 0 : 1;
        }

        private static int RunP3dA2Framework()
        {
            try
            {
                P3DA2FrameworkTests.Run();
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.ToString());
                return 1;
            }
        }

        private static void ProviderErrorMapping()
        {
            var expected = new[]
            {
                FrameworkErrorCategory.InvalidRequest,
                FrameworkErrorCategory.Denied,
                FrameworkErrorCategory.Denied,
                FrameworkErrorCategory.NotFound,
                FrameworkErrorCategory.Conflict,
                FrameworkErrorCategory.RateLimited,
                FrameworkErrorCategory.Timeout,
                FrameworkErrorCategory.Unavailable,
                FrameworkErrorCategory.Unavailable,
                FrameworkErrorCategory.Unavailable,
                FrameworkErrorCategory.Denied,
                FrameworkErrorCategory.Denied,
                FrameworkErrorCategory.ProviderFailure,
                FrameworkErrorCategory.ProviderFailure,
                FrameworkErrorCategory.Cancelled,
                FrameworkErrorCategory.Unsupported,
                FrameworkErrorCategory.ResourceExhausted,
                FrameworkErrorCategory.RecoveryRequired,
                FrameworkErrorCategory.InternalFailure
            };
            var names = new[] { "InvalidRequest", "Authentication", "Forbidden", "NotFound", "Conflict", "RateLimited", "Timeout", "Unavailable", "ServerUnavailable", "TransportUnavailable", "RedirectRejected", "PolicyDenied", "MalformedResponse", "IncompleteStream", "Cancelled", "Unsupported", "ResourceExhausted", "CorruptCredential", "InternalFailure" };
            AssertEx.Equal(expected.Length, names.Length, "provider_mapping_vector_count_mismatch");
            for (var index = 0; index < names.Length; index++)
            {
                var mapping = FrameworkErrors.MapProviderError(names[index], "provider.example", "correlation.example");
                AssertEx.Equal(names[index], mapping.ProviderCategory, "provider_mapping_name_mismatch");
                AssertEx.Equal(expected[index], mapping.CoreCategory, "provider_mapping_core_mismatch");
            }

            var unknown = FrameworkErrors.MapProviderError("invalidrequest", "provider.example", "correlation.example");
            AssertEx.Equal("Unknown", unknown.ProviderCategory, "provider_unknown_not_fail_closed");
            AssertEx.Equal(FrameworkErrorCategory.InternalFailure, unknown.CoreCategory, "provider_unknown_core_mismatch");
            AssertEx.True(!unknown.Retryable && !unknown.FallbackAllowed, "provider_unknown_flags_mismatch");
        }

        private static void AiTaskHandleLifecycle()
        {
            var handle = new AiTaskHandle("task.lifecycle", "message.lifecycle");
            var observed = new List<AiTaskEvent>();
            using (handle.Subscribe(observed.Add))
            {
                AssertEx.True(handle.Publish(new AiTaskEvent(handle.TaskId, handle.MessageId, AiTaskEventKind.Accepted, 1, string.Empty, null, string.Empty, 0, 0)).IsSuccess, "accepted_rejected");
                AssertEx.True(handle.Publish(new AiTaskEvent(handle.TaskId, handle.MessageId, AiTaskEventKind.Started, 2, string.Empty, null, string.Empty, 0, 0)).IsSuccess, "started_rejected");
                AssertEx.True(handle.Publish(new AiTaskEvent(handle.TaskId, handle.MessageId, AiTaskEventKind.TextDelta, 3, "hello", null, string.Empty, 0, 0, string.Empty, "provider.one", string.Empty, string.Empty)).IsSuccess, "text_delta_rejected");
                AssertEx.True(handle.Publish(new AiTaskEvent(handle.TaskId, handle.MessageId, AiTaskEventKind.Completed, 4, string.Empty, null, string.Empty, 0, 0, "{\"answer\":\"ok\"}", "provider.one", string.Empty, string.Empty)).IsSuccess, "completed_rejected");
            }

            AssertEx.Equal(4, handle.Snapshot().Count, "handle_snapshot_count_mismatch");
            AssertEx.Equal(4, observed.Count, "handle_subscription_count_mismatch");
            var late = handle.Publish(new AiTaskEvent(handle.TaskId, handle.MessageId, AiTaskEventKind.TextDelta, 5, "late", null, string.Empty, 0, 0));
            AssertEx.Error(late, "runtime.terminal_event_duplicate", FrameworkErrorCategory.Conflict);
        }

        private static void AiTaskHandleCancellation()
        {
            var handle = new AiTaskHandle("task.cancel", "message.cancel");
            handle.Publish(new AiTaskEvent(handle.TaskId, handle.MessageId, AiTaskEventKind.Accepted, 1, string.Empty, null, string.Empty, 0, 0));
            handle.Publish(new AiTaskEvent(handle.TaskId, handle.MessageId, AiTaskEventKind.Started, 2, string.Empty, null, string.Empty, 0, 0));
            var cancellation = handle.CancelAsync(new CancellationTokenSource().Token).GetAwaiter().GetResult();
            AssertEx.True(cancellation.IsSuccess && cancellation.Value, "handle_cancel_failed");
            var duplicate = handle.CancelAsync(new CancellationTokenSource().Token).GetAwaiter().GetResult();
            AssertEx.True(duplicate.IsSuccess && !duplicate.Value, "duplicate_cancel_not_idempotent");
            var snapshot = handle.Snapshot();
            AssertEx.Equal(AiTaskEventKind.Cancelled, snapshot[snapshot.Count - 1].Kind, "cancel_terminal_missing");
        }

        private static void AiTaskHandleStructuredJsonAndCapacity()
        {
            var structured = new AiTaskHandle("task.structured", "message.structured", 4);
            structured.Publish(new AiTaskEvent(structured.TaskId, structured.MessageId, AiTaskEventKind.Accepted, 1, string.Empty, null, string.Empty, 0, 0));
            structured.Publish(new AiTaskEvent(structured.TaskId, structured.MessageId, AiTaskEventKind.Started, 2, string.Empty, null, string.Empty, 0, 0));
            var invalid = structured.Publish(new AiTaskEvent(structured.TaskId, structured.MessageId, AiTaskEventKind.Completed, 3, string.Empty, null, string.Empty, 0, 0, "[]", string.Empty, string.Empty, string.Empty));
            AssertEx.Error(invalid, "structured_json_object_required", FrameworkErrorCategory.InvalidRequest);

            var bounded = new AiTaskHandle("task.bounded", "message.bounded", 2);
            bounded.Publish(new AiTaskEvent(bounded.TaskId, bounded.MessageId, AiTaskEventKind.Accepted, 1, string.Empty, null, string.Empty, 0, 0));
            bounded.Publish(new AiTaskEvent(bounded.TaskId, bounded.MessageId, AiTaskEventKind.Started, 2, string.Empty, null, string.Empty, 0, 0));
            var exhausted = bounded.Publish(new AiTaskEvent(bounded.TaskId, bounded.MessageId, AiTaskEventKind.TextDelta, 3, "too much", null, string.Empty, 0, 0));
            AssertEx.Error(exhausted, "runtime.task_snapshot_exhausted", FrameworkErrorCategory.ResourceExhausted);
        }

        private static int RunRuntimeVertical(string[] args)
        {
            try
            {
                var evidence = RuntimeVerticalSmoke.Run();
                var evidencePath = GetOption(args, "--evidence-path");
                if (!string.IsNullOrWhiteSpace(evidencePath)) EvidenceWriter.WriteP3aE2(evidencePath, evidence);
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.ToString());
                return 1;
            }
        }

        private static int VerifyEvidence(string[] args)
        {
            try
            {
                var evidencePath = GetOption(args, "--evidence-path");
                if (string.IsNullOrWhiteSpace(evidencePath)) throw new InvalidOperationException("--evidence-path is required for evidence verification.");
                EvidenceSchemaValidator.VerifyP3aE2(evidencePath);
                Console.WriteLine("P3A-E2-EVIDENCE PASS");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.ToString());
                return 1;
            }
        }

        private static bool HasOption(string[] args, string option)
        {
            if (args == null) return false;
            for (var index = 0; index < args.Length; index++) if (StringComparer.Ordinal.Equals(args[index], option)) return true;
            return false;
        }

        private static string GetOption(string[] args, string option)
        {
            if (args == null) return null;
            for (var index = 0; index + 1 < args.Length; index++) if (StringComparer.Ordinal.Equals(args[index], option)) return args[index + 1];
            return null;
        }

        private static void IdentityAndTypedErrors()
        {
            var identity = FrameworkIdentity.Current();
            AssertEx.Equal("awake.framework", identity.ProductId, "Identity product mismatch.");
            AssertEx.Equal("MarcusAwakeFramework", identity.AssemblyName, "Identity assembly mismatch.");
            AssertEx.Equal(2, identity.ApiVersion.Major, "Framework API major mismatch.");
            AssertEx.Equal("1.3.15", identity.BannerlordApi, "Bannerlord API mismatch.");
            var failed = OperationResult<string>.Failed(FrameworkErrors.Create("test.denied", FrameworkErrorCategory.Denied, "Denied", "corr-identity", owner: "test-owner"));
            AssertEx.Error(failed, "test.denied", FrameworkErrorCategory.Denied);
        }

        private static void CapabilityAndPermission()
        {
            var broker = new InMemoryCapabilityBroker();
            var extension = new ExtensionId("awake.test");
            var capability = new CapabilityId("awake.test.query@1");
            var manifest = new ExtensionManifest(extension, new ApiVersion(1, 0), new[] { capability }, new[] { "data.public_catalog.read" }, new ExtensionDependency[0]);
            var descriptor = new CapabilityDescriptor(capability, extension, new ApiVersion(1, 0), "query", new CapabilityId[0]);
            AssertEx.True(broker.Register(manifest, descriptor).IsSuccess, "Capability registration failed.");
            AssertEx.True(CapabilityGraphValidator.Validate(new[] { descriptor }).IsSuccess, "Capability graph validation failed.");
            var missing = new CapabilityDescriptor(new CapabilityId("awake.test.dependent@1"), extension, new ApiVersion(1, 0), "query", new[] { new CapabilityId("awake.test.missing@1") });
            AssertEx.Error(CapabilityGraphValidator.Validate(new[] { missing }), "capability.missing_dependency", FrameworkErrorCategory.Conflict);
            var cycleA = new CapabilityDescriptor(new CapabilityId("awake.test.cycle-a@1"), extension, new ApiVersion(1, 0), "query", new[] { new CapabilityId("awake.test.cycle-b@1") });
            var cycleB = new CapabilityDescriptor(new CapabilityId("awake.test.cycle-b@1"), extension, new ApiVersion(1, 0), "query", new[] { new CapabilityId("awake.test.cycle-a@1") });
            AssertEx.Error(CapabilityGraphValidator.Validate(new[] { cycleA, cycleB }), "capability.cycle", FrameworkErrorCategory.Conflict);
            AssertEx.True(broker.Register(manifest, descriptor).IsSuccess, "Idempotent capability registration failed.");
            var conflict = new CapabilityDescriptor(capability, new ExtensionId("other.extension"), new ApiVersion(1, 0), "query", new CapabilityId[0]);
            AssertEx.Error(broker.Register(new ExtensionManifest(conflict.Owner, new ApiVersion(1, 0), new[] { capability }, new string[0], new ExtensionDependency[0]), conflict), "capability.conflict", FrameworkErrorCategory.Conflict);
            var catalog = new PermissionCatalog();
            catalog.Register("data.public_catalog.read");
            var gate = new PermissionGate(catalog);
            var session = new SessionRef("campaign-a", "timeline-a", "session-a");
            var lease = new SessionCoordinator().BeginSession(session).Value;
            var context = new RequestContext(extension, lease, "corr-permission", Now.AddMinutes(1));
            AssertEx.Error(gate.Evaluate("unknown.permission", context), "permission.unknown", FrameworkErrorCategory.Denied);
            gate.Grant("data.public_catalog.read");
            AssertEx.True(gate.Evaluate("data.public_catalog.read", context).IsSuccess, "Granted permission was rejected.");
        }

        private static void SessionAndContext()
        {
            var sessions = new SessionCoordinator();
            var session = new SessionRef("campaign-b", "timeline-b", "session-b");
            var lease = sessions.BeginSession(session).Value;
            var context = new RequestContext(new ExtensionId("awake.test"), lease, "corr-session", Now.AddMinutes(1));
            AssertEx.True(sessions.ValidateLease(context).IsSuccess, "Current session was rejected.");
            var planner = new ContextPlanner();
            var contributions = new List<ContextContribution>
            {
                new ContextContribution("public", "public", VisibilityLevel.PublicCatalog, 2, "public"),
                new ContextContribution("secret", "secret", VisibilityLevel.FullSimulation, 2, "secret"),
                new ContextContribution("history", "history", VisibilityLevel.ObservedHistory, 3, "history")
            };
            var plan = planner.Plan(context, new VisibilityScope(VisibilityLevel.ObservedHistory), contributions, 4).Value;
            AssertEx.Equal(1, plan.Included.Count, "Context visibility or budget filtering failed.");
            AssertEx.Equal(2, plan.Excluded.Count, "Context exclusion count mismatch.");
            AssertEx.Equal("secret", plan.Excluded[0].SourceId, "Visibility exclusion mismatch.");
            AssertEx.Equal("history", plan.Excluded[1].SourceId, "Budget exclusion mismatch.");
            AssertEx.True(sessions.BeginClosing(session).IsSuccess, "Session close failed.");
            AssertEx.True(sessions.CompleteDrain(session, lease.Generation).IsSuccess, "Session drain failed.");
            AssertEx.Error(sessions.ValidateLease(context), "session_stale", FrameworkErrorCategory.Expired);
        }

        private static void GameDataPaging()
        {
            var service = new InMemoryGameDataService(Now.AddMinutes(5));
            service.Add(new DynamicEntityDto(new EntityRef("hero", "hero-1"), new Dictionary<string, string> { { "name", "One" } }));
            service.Add(new DynamicEntityDto(new EntityRef("hero", "hero-2"), new Dictionary<string, string> { { "name", "Two" } }));
            service.Add(new DynamicEntityDto(new EntityRef("settlement", "town-1"), new Dictionary<string, string> { { "name", "Town" } }));
            var session = new SessionRef("campaign-c", "timeline-c", "session-c");
            var lease = new SessionCoordinator().BeginSession(session).Value;
            var context = new RequestContext(new ExtensionId("awake.test"), lease, "corr-data", Now.AddMinutes(1));
            var first = service.Query(new GameDataQuery("hero", new PageRequest(1), new VisibilityScope(VisibilityLevel.PlayerKnown)), context).Value;
            AssertEx.Equal(1, first.Items.Count, "First page size mismatch.");
            AssertEx.Equal("1", first.NextCursor, "Next cursor mismatch.");
            var second = service.Query(new GameDataQuery("hero", new PageRequest(1, first.NextCursor), new VisibilityScope(VisibilityLevel.PlayerKnown)), context).Value;
            AssertEx.Equal("hero-2", second.Items[0].Identity.StableId, "Second page identity mismatch.");
        }

        private static void CommandIdempotencyAndSave()
        {
            var session = new SessionRef("campaign-d", "timeline-d", "session-d");
            var lease = new SessionCoordinator().BeginSession(session).Value;
            var context = new RequestContext(new ExtensionId("awake.test"), lease, "corr-command", Now.AddMinutes(1));
            var descriptor = new CommandDescriptor("awake.test.command", new ApiVersion(1, 0), new ExtensionId("awake.test"), CommandRisk.R1);
            var scope = new IdempotencyScope("awake.test", session, descriptor.CommandId, descriptor.SchemaVersion, "key-1", "hash-a");
            var service = new InMemoryCommandService();
            var first = service.Prepare(new CommandRequest(descriptor, scope, "{}"), context).Value;
            var duplicate = service.Prepare(new CommandRequest(descriptor, scope, "{}"), context).Value;
            AssertEx.Equal(first.LedgerEntryId, duplicate.LedgerEntryId, "Idempotent prepare created a duplicate.");
            var conflictingScope = new IdempotencyScope("awake.test", session, descriptor.CommandId, descriptor.SchemaVersion, "key-1", "hash-b");
            AssertEx.Error(service.Prepare(new CommandRequest(descriptor, conflictingScope, "{\"different\":true}"), context), "command.idempotency_conflict", FrameworkErrorCategory.Conflict);
            var settlement = service.Settle(first.LedgerEntryId, true, "effect-a", context).Value;
            AssertEx.True(settlement.Applied, "Settlement was not applied.");
            var anchors = new InMemorySaveAnchorStore();
            var attempt = anchors.Prepare(new SaveAnchorSnapshot(session.CampaignGuid, session.TimelineId, 1, settlement.LedgerSequence, "receipt-a", "storage-v1", "migration-1", "worldbook-1")).Value;
            AssertEx.True(anchors.MarkSerialized(attempt.AttemptId).IsSuccess, "Anchor serialization failed.");
            AssertEx.True(anchors.Commit(attempt.AttemptId).IsSuccess, "Anchor commit failed.");
            AssertEx.Equal(settlement.LedgerSequence, anchors.Current.CommittedAnchorSequence, "Anchor sequence mismatch.");
        }

        private static void IpcSequenceAndBackpressure()
        {
            var window = new IpcSequenceWindow("session-e", 4, "nonce-e", 4);
            var first = new IpcEnvelope("session-e", 4, "nonce-e", 1, "message-1", "payload-a");
            AssertEx.Equal(IpcFrameDecisionKind.Accepted, window.Evaluate(first).Kind, "First IPC frame was not accepted.");
            AssertEx.Equal(IpcFrameDecisionKind.SafeRetry, window.Evaluate(first).Kind, "Safe IPC retry was not recognized.");
            var conflict = new IpcEnvelope("session-e", 4, "nonce-e", 1, "message-1", "payload-b");
            AssertEx.Equal(IpcFrameDecisionKind.ReplayRejected, window.Evaluate(conflict).Kind, "Conflicting sequence was not rejected.");
            var gap = new IpcEnvelope("session-e", 4, "nonce-e", 3, "message-3", "payload-c");
            AssertEx.Equal(IpcFrameDecisionKind.SequenceOutOfOrder, window.Evaluate(gap).Kind, "Window gap was not buffered.");
            var second = new IpcEnvelope("session-e", 4, "nonce-e", 2, "message-2", "payload-b");
            AssertEx.Equal(IpcFrameDecisionKind.Accepted, window.Evaluate(second).Kind, "Buffered IPC sequence did not advance.");
            AssertEx.Equal(3L, window.LastAccepted, "Buffered IPC sequence watermark mismatch.");
            AssertEx.Equal(IpcFrameDecisionKind.SafeRetry, window.Evaluate(gap).Kind, "Buffered IPC retry was not recognized.");
            var wrongNonce = new IpcEnvelope("session-e", 4, "nonce-other", 4, "message-4", "payload-b");
            AssertEx.Equal(IpcFrameDecisionKind.NonceMismatch, window.Evaluate(wrongNonce).Kind, "Nonce mismatch was not rejected.");
            var gate = new BackpressureGate(1);
            AssertEx.True(gate.TryEnter(), "Backpressure gate did not admit first task.");
            AssertEx.True(!gate.TryEnter(), "Backpressure gate exceeded capacity.");
            gate.Exit();
            AssertEx.True(gate.TryEnter(), "Backpressure gate did not recover.");
        }

        private static void HostLifecycleAndProbe()
        {
            var identity = FrameworkIdentity.Current();
            var sessionCoordinator = new SessionCoordinator();
            var host = new FrameworkHost(identity, new InMemoryCapabilityBroker(), new InMemoryGameDataService(Now.AddMinutes(5)), new ContextPlanner(), new InMemoryCommandService(), sessionCoordinator, new InMemorySaveAnchorStore(), new DiagnosticsService(identity, sessionCoordinator, () => Now));
            AssertEx.True(host.Register().IsSuccess, "Host registration failed.");
            AssertEx.True(host.Ready().IsSuccess, "Host ready transition failed.");
            AssertEx.True(FrameworkHostLocator.Register(host).IsSuccess, "Host locator registration failed.");
            AssertEx.True(ReferenceEquals(host, FrameworkHostLocator.Resolve()), "Host locator returned a different host.");
            var session = new SessionRef("campaign-f", "timeline-f", "session-f");
            var lease = sessionCoordinator.BeginSession(session).Value;
            var context = new RequestContext(new ExtensionId("awake.test"), lease, "corr-probe", Now.AddMinutes(1));
            var receipt = host.Diagnostics.Probe(context, FrameworkHealthState.Ready).Value;
            AssertEx.Equal("MarcusAwakeFramework", receipt.Identity.AssemblyName, "Probe identity mismatch.");
            var closingLease = sessionCoordinator.BeginClosing(session).Value;
            AssertEx.True(sessionCoordinator.CompleteDrain(session, closingLease.Generation).IsSuccess, "Session drain failed.");
            AssertEx.True(host.BeginClosing().IsSuccess, "Host close transition failed.");
            AssertEx.True(host.CompleteDrain().IsSuccess, "Host drain transition failed.");
            FrameworkHostLocator.Clear(host);
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "Host locator was not cleared.");
        }

        private static void DefaultHostComposition()
        {
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "Default host fixture started with a populated locator.");
            var extensionId = new ExtensionId("awake.host-composition-test");
            var manifest = new ExtensionManifest(extensionId, new ApiVersion(1, 0), new CapabilityId[0], new string[0], new ExtensionDependency[0]);
            var extension = new TestExtension(manifest);
            var registration = FrameworkHostLocator.Register(extension);
            AssertEx.True(registration.IsSuccess, "Default host registration failed.");
            var host = FrameworkHostLocator.Resolve();
            AssertEx.True(host != null, "Default host was not published.");
            AssertEx.Equal(HostState.Ready, host.State, "Default host did not reach Ready.");
            IMarcusAiFrameworkHost compatibilityHost;
            AssertEx.True(FrameworkHostLocator.TryGetHost(out compatibilityHost), "Compatibility host projection was not available.");
            var unavailable = compatibilityHost.Ai.SubmitAsync(null, null, CancellationToken.None).GetAwaiter().GetResult();
            AssertEx.Error(unavailable, "host.ai_gateway.unavailable", FrameworkErrorCategory.Unavailable);
            AssertUnavailableServiceStubs(compatibilityHost, "locator-default", expectPermissions: true);
            FrameworkHostLocator.Clear(host);
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "Default host locator was not cleared.");

            var externalHost = FrameworkHost.CreateDefaultHost();
            AssertEx.True(externalHost.Register().IsSuccess, "External host registration failed.");
            AssertEx.True(externalHost.Ready().IsSuccess, "External host ready transition failed.");
            AssertUnavailableServiceStubs(externalHost, "create-default-host", expectPermissions: true);
            AssertEx.True(FrameworkHostLocator.Register(externalHost).IsSuccess, "External host locator registration failed.");
            var conflictingRuntime = new RuntimeService(
                "marcus-awake.runtime-service",
                new ApiVersion(1, 0),
                new[] { "ai.gateway" },
                new FixtureAiGateway(new FixtureRouteProfileResolver()));
            var conflictExtensionId = new ExtensionId("awake.host-runtime-conflict-test");
            var conflictManifest = new ExtensionManifest(conflictExtensionId, new ApiVersion(1, 0), new CapabilityId[0], new string[0], new ExtensionDependency[0]);
            var conflictExtension = new TestExtension(conflictManifest);
            var conflictRegistration = FrameworkHostLocator.Register(conflictExtension, conflictingRuntime);
            AssertEx.Error(conflictRegistration, "host.owner_conflict", FrameworkErrorCategory.Conflict);
            AssertEx.True(ReferenceEquals(externalHost, FrameworkHostLocator.Resolve()), "A runtime conflict removed the external host.");
            FrameworkHostLocator.Clear(externalHost);
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "External host fixture was not cleared.");

            AssertEx.True(FrameworkHostLocator.Resolve() == null, "Injected host fixture started with a populated locator.");
            var injectedRuntime = new RuntimeService(
                "marcus-awake.runtime-service",
                new ApiVersion(1, 0),
                new[] { "ai.gateway" },
                new FixtureAiGateway(new FixtureRouteProfileResolver()));
            var injectedExtensionId = new ExtensionId("awake.host-runtime-injection-test");
            var injectedManifest = new ExtensionManifest(injectedExtensionId, new ApiVersion(1, 0), new CapabilityId[0], new string[0], new ExtensionDependency[0]);
            var injectedExtension = new TestExtension(injectedManifest);
            var injectedRegistration = FrameworkHostLocator.Register(injectedExtension, injectedRuntime);
            AssertEx.True(injectedRegistration.IsSuccess, "Injected runtime host registration failed.");
            var injectedHost = FrameworkHostLocator.Resolve() as FrameworkHost;
            AssertEx.True(injectedHost != null, "Injected runtime host was not published.");
            AssertEx.True(ReferenceEquals(injectedRuntime, injectedHost.Runtime), "Injected runtime was not retained by the host.");
            AssertEx.True(ReferenceEquals(injectedRuntime, injectedHost.Ai), "Injected runtime was not exposed as the AI gateway.");
            AssertEx.Equal(RuntimeServiceState.Created, injectedRuntime.Status.State, "Injected runtime started during host registration.");
            AssertUnavailableServiceStubs(injectedHost, "register-with-runtime", expectPermissions: true);
            FrameworkHostLocator.Clear(injectedHost);
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "Injected runtime host locator was not cleared.");

            var externalRuntime = new RuntimeService(
                "marcus-awake.runtime-service",
                new ApiVersion(1, 0),
                new[] { "ai.gateway" },
                new FixtureAiGateway(new FixtureRouteProfileResolver()));
            var ownerIsolationHost = FrameworkHost.CreateDefaultHost(externalRuntime);
            AssertEx.True(ownerIsolationHost.Register().IsSuccess, "External host registration failed.");
            AssertEx.True(ownerIsolationHost.Ready().IsSuccess, "External host ready transition failed.");
            AssertEx.True(FrameworkHostLocator.Register(ownerIsolationHost).IsSuccess, "External host locator registration failed.");
            var awakeRuntime = new RuntimeService(
                "marcus-awake.runtime-service",
                new ApiVersion(1, 0),
                new[] { "ai.gateway" },
                new FixtureAiGateway(new FixtureRouteProfileResolver()));
            var awakeExtension = new TestExtension(new ExtensionManifest(
                new ExtensionId("awake.owner-isolation-test"),
                new ApiVersion(1, 0),
                new CapabilityId[0],
                new string[0],
                new ExtensionDependency[0]));
            var isolated = FrameworkHostLocator.Register(awakeExtension, awakeRuntime);
            AssertEx.Error(isolated, "host.owner_conflict", FrameworkErrorCategory.Conflict);
            AssertEx.True(ReferenceEquals(ownerIsolationHost, FrameworkHostLocator.Resolve()), "Owner isolation cleared or replaced the external host.");
            FrameworkHostLocator.Clear(ownerIsolationHost);
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "External host locator was not cleared.");
        }

        // 010 方案 D-2：默认装配路径必须继续返回 Unavailable 存根；只有显式传入服务包才允许替换。
        private static void AssertUnavailableServiceStubs(IMarcusAiFrameworkHost host, string label, bool expectPermissions)
        {
            if (expectPermissions)
            {
                var evaluation = host.Permissions.Evaluate("awake.probe.permission", null);
                AssertEx.True(evaluation != null && evaluation.Decision == PermissionDecision.NotRequested, label + ": default permission evaluation was not the unavailable stub.");
                AssertEx.Equal("permission_service_unavailable", evaluation.Reason, label + ": default permission stub reason mismatch.");
                AssertEx.Error(
                    host.Permissions.RequestAsync("awake.probe.permission", "probe", null, CancellationToken.None).GetAwaiter().GetResult(),
                    "host.permissions.unavailable",
                    FrameworkErrorCategory.Unavailable);
            }
            AssertEx.Error(host.Prompts.RegisterAsync(null, null, CancellationToken.None).GetAwaiter().GetResult(), "host.prompts.unavailable", FrameworkErrorCategory.Unavailable);
            AssertEx.Error(host.Storage.OpenCampaignNamespaceAsync("awake.probe", null, CancellationToken.None).GetAwaiter().GetResult(), "host.storage.unavailable", FrameworkErrorCategory.Unavailable);
            AssertEx.Error(host.Rag.SearchAsync(null, null, CancellationToken.None).GetAwaiter().GetResult(), "host.rag.unavailable", FrameworkErrorCategory.Unavailable);
            // 默认装配走的是 UnavailableGameDataService 自身实现，错误码带 host. 前缀；
            // 只有自定义服务未实现兼容层接口时才会回落到扩展方法的 game_data.unavailable。
            AssertEx.Error(host.GameData.GetCurrentPlayerAsync(null, CancellationToken.None).GetAwaiter().GetResult(), "host.game_data.unavailable", FrameworkErrorCategory.Unavailable);
        }

        private static void ServiceOverrideComposition()
        {
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "Service override fixture started with a populated locator.");
            var overrides = new FrameworkServiceOverrides
            {
                GameData = new ProbeSnapshotProvider(),
                Permissions = new ProbePermissionService()
            };
            var runtime = new RuntimeService(
                "marcus-awake.runtime-service",
                new ApiVersion(1, 0),
                new[] { "ai.gateway" },
                new FixtureAiGateway(new FixtureRouteProfileResolver()));
            var extension = new TestExtension(new ExtensionManifest(
                new ExtensionId("awake.service-overrides-test"),
                new ApiVersion(1, 0),
                new CapabilityId[0],
                new string[0],
                new ExtensionDependency[0]));

            var registration = FrameworkHostLocator.Register(extension, runtime, overrides);
            AssertEx.True(registration.IsSuccess, "Service override registration failed.");
            var resolved = FrameworkHostLocator.Resolve();
            var host = resolved as IMarcusAiFrameworkHost;
            AssertEx.True(host != null, "Service override host was not published.");

            // 公开 provider 必须真的贯通到兼容层出口，否则玩家绑定会永远失败（F-11）。
            var snapshot = host.GameData.GetCurrentPlayerAsync(null, CancellationToken.None).GetAwaiter().GetResult();
            AssertEx.True(snapshot.IsSuccess && snapshot.Value != null && snapshot.Value.Hero != null, "Provider-backed game data returned no snapshot.");
            AssertEx.Equal("hero.probe", snapshot.Value.Hero.Id.StableId, "Provider-backed snapshot id mismatch.");
            AssertEx.Equal(
                PermissionDecision.Granted,
                host.Permissions.Evaluate("awake.probe.permission", null).Decision,
                "Injected permission service was not used.");

            // 未提供的三类仍必须是存根，服务包不得隐式放行其余能力。
            AssertEx.Error(host.Prompts.RegisterAsync(null, null, CancellationToken.None).GetAwaiter().GetResult(), "host.prompts.unavailable", FrameworkErrorCategory.Unavailable);
            AssertEx.Error(host.Storage.OpenCampaignNamespaceAsync("awake.probe", null, CancellationToken.None).GetAwaiter().GetResult(), "host.storage.unavailable", FrameworkErrorCategory.Unavailable);
            AssertEx.Error(host.Rag.SearchAsync(null, null, CancellationToken.None).GetAwaiter().GetResult(), "host.rag.unavailable", FrameworkErrorCategory.Unavailable);

            // 复用同一 owner 时传入不同服务包必须显式冲突，不得静默沿用旧装配。
            var conflicting = new FrameworkServiceOverrides { GameData = new ProbeSnapshotProvider() };
            AssertEx.Error(FrameworkHostLocator.Register(extension, runtime, conflicting), "host.overrides_conflict", FrameworkErrorCategory.Conflict);
            AssertEx.True(ReferenceEquals(resolved, FrameworkHostLocator.Resolve()), "A rejected override package replaced the located host.");
            AssertEx.Equal(
                PermissionDecision.Granted,
                host.Permissions.Evaluate("awake.probe.permission", null).Decision,
                "A rejected override package disturbed the composed host.");

            FrameworkHostLocator.Clear(resolved);
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "Service override locator was not cleared.");
        }

        private sealed class ProbeSnapshotProvider : IPlayerSnapshotProvider
        {
            public Task<OperationResult<PlayerSnapshotDto>> GetCurrentPlayerAsync(RequestContext context, CancellationToken cancellationToken)
            {
                var provenance = new DataProvenance(
                    DataAccessScope.PlayerKnown,
                    SourceClass.GameRuntime,
                    EpistemicStatus.Fact,
                    DateTimeOffset.UtcNow,
                    "probe-token");
                var hero = new HeroDto(new EntityRef("hero", "hero.probe"), "Probe", null, null, true, true, 1, provenance);
                return Task.FromResult(OperationResult<PlayerSnapshotDto>.Succeeded(new PlayerSnapshotDto(hero, null, null, "probe-token")));
            }
        }

        private sealed class ProbePermissionService : IPermissionService
        {
            private static readonly ExtensionId ProbeOwner = new ExtensionId("awake.probe");

            public PermissionEvaluation Evaluate(string permissionId, RequestContext context)
            {
                return new PermissionEvaluation(permissionId, ProbeOwner, PermissionDecision.Granted, "probe", null);
            }

            public Task<OperationResult<PermissionEvaluation>> RequestAsync(string permissionId, string purpose, RequestContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationResult<PermissionEvaluation>.Succeeded(Evaluate(permissionId, context)));
            }

            public OperationResult<bool> Revoke(string permissionId, ExtensionId extensionId)
            {
                return OperationResult<bool>.Succeeded(true);
            }
        }

        private static void RuntimeLifecycleBarriers()
        {
            var fixture = RuntimeServiceComposition.CreateFixture("closing-context");
            try
            {
                AssertEx.True(fixture.StartRuntime().IsSuccess, "Runtime fixture did not start.");
                var closing = fixture.Sessions.BeginClosing(fixture.Reference);
                AssertEx.True(closing.IsSuccess, "Runtime fixture session did not close.");
                var closingContext = new RequestContext(fixture.Caller, closing.Value, "corr-runtime-closing", FixtureLifecycleDriver.FixtureDeadline);
                AssertEx.True(fixture.Runtime.BeginDrain(closingContext).IsSuccess, "Runtime drain rejected the closing session lease.");
                AssertEx.True(fixture.Runtime.CompleteDrain(closingContext).IsSuccess, "Runtime drain did not complete after closing session.");
            }
            finally
            {
                fixture.Dispose();
            }

            var client = new RuntimeServiceClient(new RuntimeServiceClientOptions(servicePath: "missing-runtime-service.exe"));
            var context = new RequestContext(new ExtensionId("awake.runtime.lifecycle-test"), new SessionRef("campaign-client", "timeline-client", "session-client"), "corr-client-lifecycle", DateTimeOffset.UtcNow.AddMinutes(1));
            var pendingStart = new TaskCompletionSource<OperationResult<RuntimeServiceStatus>>(TaskCreationOptions.RunContinuationsAsynchronously);
            SetClientLifecycleState(client, RuntimeServiceState.Starting, pendingStart.Task);
            Task<OperationResult<RuntimeServiceStatus>> stopTask = client.StopAsync(context, CancellationToken.None);
            AssertEx.True(!stopTask.Wait(100), "StopAsync completed before the starting operation joined.");
            pendingStart.SetResult(OperationResult<RuntimeServiceStatus>.Failed(FrameworkErrors.Create("fixture.start_failed", FrameworkErrorCategory.Unavailable, "Fixture start failed.", "corr-client-lifecycle")));
            AssertEx.True(!stopTask.GetAwaiter().GetResult().IsSuccess, "StopAsync hid a failed startup.");
            client.Dispose();

            var disposeClient = new RuntimeServiceClient(new RuntimeServiceClientOptions(servicePath: "missing-runtime-service.exe"));
            var pendingDispose = new TaskCompletionSource<OperationResult<RuntimeServiceStatus>>(TaskCreationOptions.RunContinuationsAsynchronously);
            SetClientLifecycleState(disposeClient, RuntimeServiceState.Starting, pendingDispose.Task);
            Task disposeTask = Task.Run(() => disposeClient.Dispose());
            AssertEx.True(!disposeTask.Wait(100), "Dispose completed before the starting operation joined.");
            pendingDispose.SetResult(OperationResult<RuntimeServiceStatus>.Failed(FrameworkErrors.Create("fixture.start_failed", FrameworkErrorCategory.Unavailable, "Fixture start failed.", "corr-client-dispose")));
            AssertEx.True(disposeTask.Wait(2000), "Dispose did not complete after the starting operation joined.");
            AssertEx.Equal(RuntimeServiceState.Stopped, disposeClient.Status.State, "Dispose did not leave the client stopped.");
        }

        private static void SetClientLifecycleState(RuntimeServiceClient client, RuntimeServiceState state, Task<OperationResult<RuntimeServiceStatus>> startTask)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RuntimeServiceClient).GetField("state", flags).SetValue(client, state);
            typeof(RuntimeServiceClient).GetField("lastStartTask", flags).SetValue(client, startTask);
        }

        private sealed class TestExtension : IFrameworkExtension
        {
            internal TestExtension(ExtensionManifest manifest) { Manifest = manifest; }

            public ExtensionManifest Manifest { get; }

            public void Register(IExtensionRegistration registration) { }

            public void OnLifecycle(ExtensionLifecycleStage stage, SessionRef session) { }
        }
    }
}
