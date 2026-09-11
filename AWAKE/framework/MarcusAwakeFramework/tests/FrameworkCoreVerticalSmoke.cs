using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using MarcusAwakeFramework.Tests.TestDoubles;

namespace MarcusAwakeFramework.Tests
{
    internal sealed class FixtureLifecycleDriver
    {
        internal static readonly DateTimeOffset FixtureNow = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);
        internal static readonly DateTimeOffset FixtureDeadline = FixtureNow.AddHours(1);

        private readonly FrameworkIdentity identity;
        private readonly ExtensionId caller;

        internal FixtureLifecycleDriver(string suffix)
        {
            identity = FrameworkIdentity.Current();
            caller = new ExtensionId("awake.framework.smoke");
            Reference = new SessionRef("campaign-smoke-" + suffix, "timeline-smoke-" + suffix, "session-smoke-" + suffix);
            Bootstrap = new FrameworkBootstrap(CreateHost);
        }

        internal SessionRef Reference { get; }
        internal FrameworkBootstrap Bootstrap { get; }
        internal FrameworkHost LastHost { get; private set; }
        internal SessionCoordinator LastSessions { get; private set; }

        internal FrameworkHost CreateHost()
        {
            var sessions = new SessionCoordinator(() => FixtureNow);
            var gameData = new InMemoryGameDataService(FixtureDeadline);
            gameData.Add(new DynamicEntityDto(new EntityRef("settlement", "smoke-settlement-1"), new Dictionary<string, string> { { "name", "Smoke One" } }));
            gameData.Add(new DynamicEntityDto(new EntityRef("hero", "smoke-hero-1"), new Dictionary<string, string> { { "name", "Smoke Hero" } }));
            gameData.Add(new DynamicEntityDto(new EntityRef("settlement", "smoke-settlement-2"), new Dictionary<string, string> { { "name", "Smoke Two" } }));
            var host = new FrameworkHost(
                identity,
                new InMemoryCapabilityBroker(),
                gameData,
                new ContextPlanner(),
                new InMemoryCommandService(),
                sessions,
                new InMemorySaveAnchorStore(),
                new DiagnosticsService(identity, sessions, () => FixtureNow));
            LastHost = host;
            LastSessions = sessions;
            return host;
        }

        internal OperationResult<FrameworkBootstrapResult> Start(BootstrapFaultPoint faultPoint = BootstrapFaultPoint.None)
        {
            return Bootstrap.Start(Reference, faultPoint);
        }

        internal RequestContext CreateContext(SessionLease lease, string correlationId)
        {
            return new RequestContext(caller, lease, correlationId, FixtureDeadline);
        }

        internal RequestContext CreateExpiredContext(SessionLease lease, string correlationId)
        {
            return new RequestContext(caller, lease, correlationId, FixtureNow.AddMinutes(-1));
        }
    }

    internal static class FrameworkCoreVerticalSmokeFixture
    {
        internal static void Run()
        {
            var cases = new KeyValuePair<string, Action>[]
            {
                new KeyValuePair<string, Action>("FCORE-001-BOOTSTRAP-SESSION-READY", BootstrapAndSessionReady),
                new KeyValuePair<string, Action>("FCORE-002-REQUEST-CONTEXT-GENERATION", RequestContextBindsGeneration),
                new KeyValuePair<string, Action>("FCORE-003-GATED-DATA-CONTEXT-RECEIPT", GatedDataContextAndDiagnostics),
                new KeyValuePair<string, Action>("FCORE-004-CLOSING-REJECTS-REQUEST", ClosingRejectsNewRequests),
                new KeyValuePair<string, Action>("FCORE-005-PENDING-DRAIN-LATE-COMPLETE", PendingDrainAndLateComplete),
                new KeyValuePair<string, Action>("FCORE-006-REPEAT-COMPLETE", RepeatedCompleteIsRejected),
                new KeyValuePair<string, Action>("FCORE-007-DEADLINE-TYPED-ERROR", DeadlineErrorsAreTyped),
                new KeyValuePair<string, Action>("FCORE-008-BOOTSTRAP-ROLLBACK-HOST-REGISTER", BootstrapRollbackAfterHostRegister),
                new KeyValuePair<string, Action>("FCORE-009-BOOTSTRAP-ROLLBACK-HOST-READY", BootstrapRollbackAfterHostReady),
                new KeyValuePair<string, Action>("FCORE-010-BOOTSTRAP-ROLLBACK-LOCATOR-REGISTER", BootstrapRollbackAfterLocatorRegister),
                new KeyValuePair<string, Action>("FCORE-011-BOOTSTRAP-ROLLBACK-SESSION-BEGIN", BootstrapRollbackAfterSessionBegin),
                new KeyValuePair<string, Action>("FCORE-012-LOCATOR-CONFLICT-PRESERVED", LocatorConflictPreservesCurrentHost),
                new KeyValuePair<string, Action>("FCORE-013-IPC-REPLAY-ORDER", IpcReplayAndOrder),
                new KeyValuePair<string, Action>("FCORE-014-BACKPRESSURE-CAPACITY", BackpressureRejectsOverCapacity),
                new KeyValuePair<string, Action>("FCORE-015-RECOVERY-BLOCKS-SESSION", RecoveryBlocksNewSession),
                new KeyValuePair<string, Action>("FCORE-016-SESSION-READY-IDEMPOTENCY", SessionReadyIsIdempotent),
                new KeyValuePair<string, Action>("FCORE-017-CONCURRENT-BEGIN-SESSION", ConcurrentBeginSessionIsIdempotent),
                new KeyValuePair<string, Action>("FCORE-018-CONCURRENT-CLOSE-VALIDATE", ConcurrentCloseAndValidateAreSafe),
                new KeyValuePair<string, Action>("FCORE-019-CONCURRENT-COMPLETE-DRAIN", ConcurrentCompleteAndDrainAreFenced),
                new KeyValuePair<string, Action>("FCORE-020-CONCURRENT-IPC-BACKPRESSURE", ConcurrentIpcAndBackpressureAreBounded)
            };

            for (var index = 0; index < cases.Length; index++) RunCase(cases[index].Key, cases[index].Value);
            Console.WriteLine("FCORE-SUMMARY PASS " + cases.Length);
        }

        private static void RunCase(string caseId, Action test)
        {
            Console.WriteLine("CASE " + caseId + " START");
            try
            {
                test();
                Console.WriteLine("CASE " + caseId + " PASS");
            }
            catch
            {
                Console.WriteLine("CASE " + caseId + " FAIL");
                throw;
            }
        }

        private static void BootstrapAndSessionReady()
        {
            RequireEmptyLocator();
            var driver = new FixtureLifecycleDriver("normal");
            var started = driver.Start();
            AssertEx.True(started.IsSuccess, "Normal bootstrap failed.");
            var result = started.Value;
            AssertEx.Equal(HostState.Ready, result.Host.State, "Bootstrap did not leave the host ready.");
            AssertEx.Equal(SessionState.Ready, result.Session.State, "Bootstrap did not reach SessionReady.");
            AssertEx.True(ReferenceEquals(result.Host, FrameworkHostLocator.Resolve()), "Ready host was not published to the locator.");
            AssertEx.True(ReferenceEquals(result.Session, driver.LastSessions.Current), "SessionReady did not become current.");
            AssertEx.Equal(1L, result.Session.Generation, "Initial session generation mismatch.");
            AssertEx.True(result.Session.DrainTaskId == null, "A ready session has a drain task.");
            CloseSessionAndHost(result.Host, result.Session);
        }

        private static void RequestContextBindsGeneration()
        {
            RequireEmptyLocator();
            var driver = new FixtureLifecycleDriver("context-generation");
            var started = driver.Start();
            AssertEx.True(started.IsSuccess, "Context generation bootstrap failed.");
            var host = started.Value.Host;
            var firstLease = started.Value.Session;
            var firstGeneration = firstLease.Generation;
            var oldContext = driver.CreateContext(firstLease, "corr-context-old");
            AssertEx.True(ReferenceEquals(firstLease.Reference, oldContext.Session), "RequestContext session binding mismatch.");
            AssertEx.Equal(firstGeneration, oldContext.SessionGeneration, "RequestContext generation binding mismatch.");
            AssertEx.True(oldContext.CancellationToken == firstLease.CancellationToken, "RequestContext cancellation binding mismatch.");
            AssertEx.True(driver.LastSessions.ValidateLease(oldContext).IsSuccess, "Current RequestContext did not validate.");

            var closing = BeginClosing(driver.LastSessions, driver.Reference);
            AssertEx.Equal(firstGeneration + 1, firstLease.Generation, "Closing did not advance the session fence.");
            AssertEx.Equal(firstGeneration, oldContext.SessionGeneration, "RequestContext generation changed after creation.");
            AssertEx.True(oldContext.CancellationToken.IsCancellationRequested, "Closing did not cancel the session token.");
            CompleteSessionDrain(driver.LastSessions, closing);

            var reopened = driver.LastSessions.BeginSession(driver.Reference);
            AssertEx.True(reopened.IsSuccess, "Drained session did not reopen as a new session.");
            AssertEx.True(!ReferenceEquals(firstLease, reopened.Value), "Drained lease was reused.");
            AssertEx.Error(driver.LastSessions.ValidateLease(oldContext), "session_generation_conflict", FrameworkErrorCategory.Conflict);
            var newContext = driver.CreateContext(reopened.Value, "corr-context-new");
            AssertEx.Equal(reopened.Value.Generation, newContext.SessionGeneration, "New RequestContext generation mismatch.");
            AssertEx.True(driver.LastSessions.ValidateLease(newContext).IsSuccess, "New RequestContext did not validate.");
            CloseSessionAndHost(host, reopened.Value);
        }

        private static void GatedDataContextAndDiagnostics()
        {
            RequireEmptyLocator();
            var driver = new FixtureLifecycleDriver("gated-calls");
            var started = driver.Start();
            AssertEx.True(started.IsSuccess, "Gated-call bootstrap failed.");
            var host = started.Value.Host;
            var lease = started.Value.Session;
            var context = driver.CreateContext(lease, "corr-gated-calls");

            var validation = host.Sessions.ValidateLease(context);
            AssertEx.True(validation.IsSuccess, "Session validation gate failed.");
            AssertEx.True(ReferenceEquals(lease, validation.Value), "Validation returned a different lease.");

            var firstPageResult = host.GameData.Query(
                new GameDataQuery("settlement", new PageRequest(1), new VisibilityScope(VisibilityLevel.PlayerKnown)),
                context);
            AssertEx.True(firstPageResult.IsSuccess, "GameData query failed after validation.");
            AssertEx.Equal(1, firstPageResult.Value.Items.Count, "GameData page size mismatch.");
            AssertEx.Equal("smoke-settlement-1", firstPageResult.Value.Items[0].Identity.StableId, "GameData first item mismatch.");
            AssertEx.Equal("1", firstPageResult.Value.NextCursor, "GameData cursor mismatch.");

            var secondPageResult = host.GameData.Query(
                new GameDataQuery("settlement", new PageRequest(1, firstPageResult.Value.NextCursor), new VisibilityScope(VisibilityLevel.PlayerKnown), firstPageResult.Value.Snapshot),
                context);
            AssertEx.True(secondPageResult.IsSuccess, "GameData second page failed.");
            AssertEx.Equal("smoke-settlement-2", secondPageResult.Value.Items[0].Identity.StableId, "GameData second item mismatch.");
            AssertEx.True(secondPageResult.Value.NextCursor == null, "GameData returned an unexpected third page.");

            var contributions = new List<ContextContribution>
            {
                new ContextContribution("public-fact", "public", VisibilityLevel.PublicCatalog, 3, "public"),
                new ContextContribution("hidden-history", "history", VisibilityLevel.ObservedHistory, 2, "history"),
                new ContextContribution("budget-fact", "budget", VisibilityLevel.PublicCatalog, 4, "public")
            };
            var planResult = host.Context.Plan(context, new VisibilityScope(VisibilityLevel.PublicCatalog), contributions, 6);
            AssertEx.True(planResult.IsSuccess, "ContextPlanner failed.");
            AssertEx.Equal(1, planResult.Value.Included.Count, "ContextPlanner included count mismatch.");
            AssertEx.Equal("public-fact", planResult.Value.Included[0].SourceId, "ContextPlanner included wrong source.");
            AssertEx.Equal(3, planResult.Value.TokenCost, "ContextPlanner token cost mismatch.");
            AssertEx.Equal(2, planResult.Value.Excluded.Count, "ContextPlanner excluded count mismatch.");
            AssertEx.Equal("visibility_denied", planResult.Value.Excluded[0].Reason, "ContextPlanner visibility reason mismatch.");
            AssertEx.Equal("token_budget", planResult.Value.Excluded[1].Reason, "ContextPlanner budget reason mismatch.");

            var receiptResult = host.Diagnostics.Probe(context, FrameworkHealthState.Ready);
            AssertEx.True(receiptResult.IsSuccess, "Diagnostics probe failed.");
            var receipt = receiptResult.Value;
            AssertEx.Equal("MarcusAwakeFramework", receipt.Identity.AssemblyName, "Diagnostics identity mismatch.");
            AssertEx.Equal(FrameworkHealthState.Ready, receipt.Health, "Diagnostics health mismatch.");
            AssertEx.Equal(context.CorrelationId, receipt.CorrelationId, "Diagnostics correlation mismatch.");
            AssertEx.Equal(lease.Generation, receipt.SessionGeneration, "Diagnostics generation mismatch.");
            AssertEx.Equal(FixtureLifecycleDriver.FixtureNow, receipt.ObservedAt, "Diagnostics clock mismatch.");
            CloseSessionAndHost(host, lease);
        }

        private static void ClosingRejectsNewRequests()
        {
            RequireEmptyLocator();
            var driver = new FixtureLifecycleDriver("closing");
            var started = driver.Start();
            AssertEx.True(started.IsSuccess, "Closing bootstrap failed.");
            var host = started.Value.Host;
            var lease = started.Value.Session;
            var closing = BeginClosing(driver.LastSessions, driver.Reference);
            AssertEx.True(lease.CancellationToken.IsCancellationRequested, "Closing did not cancel the session.");

            var newContext = driver.CreateContext(lease, "corr-closing-request");
            AssertEx.Error(host.Sessions.ValidateLease(newContext), "session_closing", FrameworkErrorCategory.Conflict);
            AssertEx.Error(host.Sessions.BeginOperation(newContext, "op-closing-request"), "session_closing", FrameworkErrorCategory.Conflict);
            AssertEx.Error(host.Diagnostics.Probe(newContext, FrameworkHealthState.Ready), "session_closing", FrameworkErrorCategory.Conflict);

            CompleteSessionDrain(driver.LastSessions, closing);
            FinishHostDrain(host);
            var drainedContext = driver.CreateContext(lease, "corr-drained-request");
            AssertEx.Error(driver.LastSessions.ValidateLease(drainedContext), "session_stale", FrameworkErrorCategory.Expired);
        }

        private static void PendingDrainAndLateComplete()
        {
            RequireEmptyLocator();
            var driver = new FixtureLifecycleDriver("pending");
            var started = driver.Start();
            AssertEx.True(started.IsSuccess, "Pending-operation bootstrap failed.");
            var host = started.Value.Host;
            var lease = started.Value.Session;
            var context = driver.CreateContext(lease, "corr-pending-operation");
            var operationResult = driver.LastSessions.BeginOperation(context, "op-pending");
            AssertEx.True(operationResult.IsSuccess, "Pending operation did not start.");
            AssertEx.Equal(1, lease.PendingOperationCount, "Pending operation count was not incremented.");

            var closing = BeginClosing(driver.LastSessions, driver.Reference);
            var incomplete = driver.LastSessions.CompleteDrain(driver.Reference, closing.Generation);
            AssertEx.Error(incomplete, "session_drain_incomplete", FrameworkErrorCategory.Conflict);
            AssertEx.Equal(1, lease.PendingOperationCount, "Incomplete drain changed the pending count.");

            var lateComplete = operationResult.Value.Complete();
            AssertEx.Error(lateComplete, "session_stale_result", FrameworkErrorCategory.Expired);
            AssertEx.Equal(0, lease.PendingOperationCount, "Late completion did not release the pending operation.");
            CompleteSessionDrain(driver.LastSessions, closing);
            FinishHostDrain(host);
        }

        private static void RepeatedCompleteIsRejected()
        {
            RequireEmptyLocator();
            var driver = new FixtureLifecycleDriver("repeat-complete");
            var started = driver.Start();
            AssertEx.True(started.IsSuccess, "Repeated-complete bootstrap failed.");
            var host = started.Value.Host;
            var lease = started.Value.Session;
            var context = driver.CreateContext(lease, "corr-repeat-complete");
            var operation = driver.LastSessions.BeginOperation(context, "op-repeat").Value;
            AssertEx.True(operation.Complete().IsSuccess, "First operation completion failed.");
            AssertEx.Equal(0, lease.PendingOperationCount, "First completion did not release the operation.");
            AssertEx.Error(operation.Complete(), "session_operation_already_completed", FrameworkErrorCategory.Conflict);
            AssertEx.Equal(0, lease.PendingOperationCount, "Repeated completion changed the pending count.");
            CloseSessionAndHost(host, lease);
        }

        private static void DeadlineErrorsAreTyped()
        {
            RequireEmptyLocator();
            var driver = new FixtureLifecycleDriver("deadline");
            var started = driver.Start();
            AssertEx.True(started.IsSuccess, "Deadline bootstrap failed.");
            var host = started.Value.Host;
            var lease = started.Value.Session;
            var expired = driver.CreateExpiredContext(lease, "corr-expired");
            AssertEx.Error(host.Sessions.ValidateLease(expired), "request_deadline_expired", FrameworkErrorCategory.Expired);
            AssertEx.Error(host.Sessions.BeginOperation(expired, "op-expired"), "request_deadline_expired", FrameworkErrorCategory.Expired);
            AssertEx.Error(host.Diagnostics.Probe(expired, FrameworkHealthState.Ready), "request_deadline_expired", FrameworkErrorCategory.Expired);
            CloseSessionAndHost(host, lease);
        }

        private static void BootstrapRollbackAfterHostRegister()
        {
            AssertBootstrapFaultRollsBack(BootstrapFaultPoint.AfterHostRegister, "Host.Register", "fault-host-register");
        }

        private static void BootstrapRollbackAfterHostReady()
        {
            AssertBootstrapFaultRollsBack(BootstrapFaultPoint.AfterHostReady, "Host.Ready", "fault-host-ready");
        }

        private static void BootstrapRollbackAfterLocatorRegister()
        {
            AssertBootstrapFaultRollsBack(BootstrapFaultPoint.AfterLocatorRegister, "Locator.Register", "fault-locator-register");
        }

        private static void BootstrapRollbackAfterSessionBegin()
        {
            AssertBootstrapFaultRollsBack(BootstrapFaultPoint.AfterSessionBegin, "Session.BeginSession", "fault-session-begin");
        }

        private static void AssertBootstrapFaultRollsBack(BootstrapFaultPoint faultPoint, string stage, string suffix)
        {
            RequireEmptyLocator();
            var driver = new FixtureLifecycleDriver(suffix);
            var failed = driver.Start(faultPoint);
            AssertEx.Error(failed, "bootstrap.injected_failure", FrameworkErrorCategory.Conflict);
            AssertEx.True(failed.Error.Details.ContainsKey("stage"), "Bootstrap failure stage is missing.");
            AssertEx.Equal(stage, failed.Error.Details["stage"], "Bootstrap failure stage mismatch.");
            var failedHost = driver.LastHost;
            var failedSessions = driver.LastSessions;
            AssertEx.Equal(HostState.Created, failedHost.State, "Bootstrap rollback did not reset the candidate host.");
            AssertNoLiveSession(failedSessions, "Bootstrap rollback left a live session.");
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "Bootstrap rollback left a candidate in the locator.");

            var retry = driver.Start();
            AssertEx.True(retry.IsSuccess, "Faultless bootstrap did not recover after rollback.");
            AssertEx.True(!ReferenceEquals(failedHost, retry.Value.Host), "Recovery reused the rolled-back host.");
            AssertEx.Equal(HostState.Ready, retry.Value.Host.State, "Recovery host was not ready.");
            AssertEx.Equal(SessionState.Ready, retry.Value.Session.State, "Recovery session did not reach SessionReady.");
            AssertEx.True(ReferenceEquals(retry.Value.Host, FrameworkHostLocator.Resolve()), "Recovery host was not published.");
            CloseSessionAndHost(retry.Value.Host, retry.Value.Session);
        }

        private static void LocatorConflictPreservesCurrentHost()
        {
            RequireEmptyLocator();
            var holder = new FixtureLifecycleDriver("locator-holder");
            var currentHost = holder.CreateHost();
            AssertEx.True(currentHost.Register().IsSuccess, "Locator holder registration failed.");
            AssertEx.True(currentHost.Ready().IsSuccess, "Locator holder ready transition failed.");
            AssertEx.True(FrameworkHostLocator.Register(currentHost).IsSuccess, "Locator holder publication failed.");

            var candidateDriver = new FixtureLifecycleDriver("locator-candidate");
            var failed = candidateDriver.Start();
            AssertEx.Error(failed, "host.conflict", FrameworkErrorCategory.Conflict);
            var candidateHost = candidateDriver.LastHost;
            AssertEx.Equal(HostState.Created, candidateHost.State, "Locator conflict did not roll back the candidate host.");
            AssertNoLiveSession(candidateDriver.LastSessions, "Locator conflict opened a live candidate session.");
            AssertEx.True(ReferenceEquals(currentHost, FrameworkHostLocator.Resolve()), "Locator conflict replaced the current host.");

            CloseHostWithoutSession(currentHost);
            var retry = candidateDriver.Start();
            AssertEx.True(retry.IsSuccess, "Faultless bootstrap did not recover after locator conflict.");
            AssertEx.True(ReferenceEquals(retry.Value.Host, FrameworkHostLocator.Resolve()), "Recovered host was not published after locator conflict.");
            CloseSessionAndHost(retry.Value.Host, retry.Value.Session);
        }

        private static void IpcReplayAndOrder()
        {
            var window = new IpcSequenceWindow("smoke-session", 7, "smoke-nonce", 4);
            var first = new IpcEnvelope("smoke-session", 7, "smoke-nonce", 1, "message-1", "payload-a");
            var firstDecision = window.Evaluate(first);
            AssertEx.Equal(IpcFrameDecisionKind.Accepted, firstDecision.Kind, "First IPC frame was not accepted.");
            AssertEx.Equal(IpcAckStatus.Accepted, firstDecision.AckStatus.Value, "First IPC acknowledgement mismatch.");

            var retryDecision = window.Evaluate(first);
            AssertEx.Equal(IpcFrameDecisionKind.SafeRetry, retryDecision.Kind, "Duplicate IPC frame was not a safe retry.");
            AssertEx.Equal(IpcAckStatus.DurablyRecorded, retryDecision.AckStatus.Value, "Duplicate IPC acknowledgement mismatch.");

            var conflictingReplay = new IpcEnvelope("smoke-session", 7, "smoke-nonce", 1, "message-1", "payload-b");
            AssertEx.Equal(IpcFrameDecisionKind.ReplayRejected, window.Evaluate(conflictingReplay).Kind, "Conflicting replay was not rejected.");

            var outOfOrder = new IpcEnvelope("smoke-session", 7, "smoke-nonce", 3, "message-3", "payload-c");
            AssertEx.Equal(IpcFrameDecisionKind.SequenceOutOfOrder, window.Evaluate(outOfOrder).Kind, "Out-of-order IPC frame was not held.");
            var second = new IpcEnvelope("smoke-session", 7, "smoke-nonce", 2, "message-2", "payload-b");
            AssertEx.Equal(IpcFrameDecisionKind.Accepted, window.Evaluate(second).Kind, "Out-of-order IPC frame did not recover.");
            AssertEx.Equal(3L, window.LastAccepted, "IPC sequence watermark mismatch.");
            AssertEx.Equal(IpcFrameDecisionKind.SafeRetry, window.Evaluate(outOfOrder).Kind, "Buffered IPC retry was not recognized.");

            var tooFar = new IpcEnvelope("smoke-session", 7, "smoke-nonce", 8, "message-8", "payload-d");
            AssertEx.Equal(IpcFrameDecisionKind.SequenceGap, window.Evaluate(tooFar).Kind, "IPC sequence gap was not rejected.");
            var wrongNonce = new IpcEnvelope("smoke-session", 7, "other-nonce", 4, "message-4", "payload-e");
            AssertEx.Equal(IpcFrameDecisionKind.NonceMismatch, window.Evaluate(wrongNonce).Kind, "IPC nonce mismatch was not rejected.");
        }

        private static void BackpressureRejectsOverCapacity()
        {
            var gate = new BackpressureGate(2);
            AssertEx.Equal(2, gate.Capacity, "Backpressure capacity mismatch.");
            AssertEx.True(gate.TryEnter(), "Backpressure did not admit first operation.");
            AssertEx.True(gate.TryEnter(), "Backpressure did not admit second operation.");
            AssertEx.True(!gate.TryEnter(), "Backpressure admitted an over-capacity operation.");
            AssertEx.Equal(2, gate.Active, "Backpressure active count mismatch at capacity.");
            gate.Exit();
            AssertEx.True(gate.TryEnter(), "Backpressure did not recover after exit.");
            gate.Exit();
            gate.Exit();
            gate.Exit();
            AssertEx.Equal(0, gate.Active, "Backpressure active count underflowed.");
        }

        private static void RecoveryBlocksNewSession()
        {
            RequireEmptyLocator();
            var driver = new FixtureLifecycleDriver("recovery");
            var started = driver.Start();
            AssertEx.True(started.IsSuccess, "Recovery bootstrap failed.");
            var host = started.Value.Host;
            var lease = started.Value.Session;
            var closing = BeginClosing(driver.LastSessions, driver.Reference);
            AssertEx.True(driver.LastSessions.EnterRecoveryRequired(driver.Reference).IsSuccess, "Recovery transition failed.");
            AssertEx.Equal(SessionState.RecoveryRequired, lease.State, "Session did not enter recovery state.");

            var sameReference = driver.LastSessions.BeginSession(driver.Reference);
            AssertEx.Error(sameReference, "session_recovery_required", FrameworkErrorCategory.RecoveryRequired);
            var otherReference = new SessionRef("campaign-recovery-other", "timeline-recovery-other", "session-recovery-other");
            AssertEx.Error(driver.LastSessions.BeginSession(otherReference), "session_recovery_required", FrameworkErrorCategory.RecoveryRequired);
            var recoveryContext = driver.CreateContext(lease, "corr-recovery");
            AssertEx.Error(driver.LastSessions.ValidateLease(recoveryContext), "session_recovery_required", FrameworkErrorCategory.RecoveryRequired);
            AssertEx.Error(driver.LastSessions.BeginOperation(recoveryContext, "op-recovery"), "session_recovery_required", FrameworkErrorCategory.RecoveryRequired);
            AssertEx.Error(driver.LastSessions.CompleteDrain(driver.Reference, closing.Generation), "session_recovery_required", FrameworkErrorCategory.RecoveryRequired);
            var repeatedClose = driver.LastSessions.BeginClosing(driver.Reference);
            AssertEx.True(repeatedClose.IsSuccess, "Recovery close was not idempotent.");
            AssertEx.True(ReferenceEquals(lease, repeatedClose.Value), "Recovery close returned a different lease.");
            AssertEx.Equal(SessionState.RecoveryRequired, repeatedClose.Value.State, "Recovery close changed the recovery state.");
            FrameworkHostLocator.Clear(host);
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "Recovery case left a host in the locator.");
        }

        private static void SessionReadyIsIdempotent()
        {
            RequireEmptyLocator();
            var driver = new FixtureLifecycleDriver("session-idempotency");
            var started = driver.Start();
            AssertEx.True(started.IsSuccess, "Session idempotency bootstrap failed.");
            var host = started.Value.Host;
            var lease = started.Value.Session;
            var repeated = driver.LastSessions.BeginSession(driver.Reference);
            AssertEx.True(repeated.IsSuccess, "Repeated SessionReady was not accepted idempotently.");
            AssertEx.True(ReferenceEquals(lease, repeated.Value), "Repeated SessionReady returned a different lease.");
            AssertEx.Equal(lease.Generation, repeated.Value.Generation, "Repeated SessionReady changed generation.");

            var otherReference = new SessionRef("campaign-session-other", "timeline-session-other", "session-session-other");
            AssertEx.Error(driver.LastSessions.BeginSession(otherReference), "session.active", FrameworkErrorCategory.Conflict);
            var closing = BeginClosing(driver.LastSessions, driver.Reference);
            var repeatedClosing = driver.LastSessions.BeginClosing(driver.Reference);
            AssertEx.True(repeatedClosing.IsSuccess, "Repeated SessionEnding was not idempotent.");
            AssertEx.True(ReferenceEquals(closing, repeatedClosing.Value), "Repeated SessionEnding returned a different lease.");
            AssertEx.Equal(closing.DrainTaskId, repeatedClosing.Value.DrainTaskId, "Repeated SessionEnding changed drain task ID.");
            CompleteSessionDrain(driver.LastSessions, closing);
            FinishHostDrain(host);
        }

        private static void ConcurrentBeginSessionIsIdempotent()
        {
            var sessions = new SessionCoordinator();
            var reference = new SessionRef("campaign-concurrent-begin", "timeline-concurrent-begin", "session-concurrent-begin");
            var barrier = new Barrier(2);
            SessionLease first = null;
            SessionLease second = null;
            var firstTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                first = sessions.BeginSession(reference).Value;
            });
            var secondTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                second = sessions.BeginSession(reference).Value;
            });
            Task.WaitAll(firstTask, secondTask);
            AssertEx.True(first != null && second != null, "Concurrent SessionReady did not return leases.");
            AssertEx.True(ReferenceEquals(first, second), "Concurrent SessionReady created duplicate leases.");
            var closing = BeginClosing(sessions, reference);
            CompleteSessionDrain(sessions, closing);
        }

        private static void ConcurrentCloseAndValidateAreSafe()
        {
            RequireEmptyLocator();
            var driver = new FixtureLifecycleDriver("concurrent-close-validate");
            var started = driver.Start();
            AssertEx.True(started.IsSuccess, "Concurrent close/validate bootstrap failed.");
            var host = started.Value.Host;
            var lease = started.Value.Session;
            var context = driver.CreateContext(lease, "corr-concurrent-close-validate");
            var barrier = new Barrier(2);
            OperationResult<SessionLease> closeResult = null;
            OperationResult<SessionLease> validateResult = null;
            var closeTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                closeResult = driver.LastSessions.BeginClosing(driver.Reference);
            });
            var validateTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                validateResult = driver.LastSessions.ValidateLease(context);
            });
            Task.WaitAll(closeTask, validateTask);
            AssertEx.True(closeResult != null && closeResult.IsSuccess, "Concurrent SessionEnding failed.");
            AssertEx.True(validateResult != null && (validateResult.IsSuccess || validateResult.Error.Code == "session_closing"), "Concurrent validation returned an unexpected result.");
            AssertEx.Equal(SessionState.Closing, lease.State, "Concurrent close did not leave the session closing.");
            CompleteSessionDrain(driver.LastSessions, closeResult.Value);
            FinishHostDrain(host);
        }

        private static void ConcurrentCompleteAndDrainAreFenced()
        {
            RequireEmptyLocator();
            var driver = new FixtureLifecycleDriver("concurrent-complete-drain");
            var started = driver.Start();
            AssertEx.True(started.IsSuccess, "Concurrent complete/drain bootstrap failed.");
            var host = started.Value.Host;
            var context = driver.CreateContext(started.Value.Session, "corr-concurrent-complete-drain");
            var operation = driver.LastSessions.BeginOperation(context, "op-concurrent-complete-drain").Value;
            var closing = BeginClosing(driver.LastSessions, driver.Reference);
            var barrier = new Barrier(2);
            OperationResult<bool> completeResult = null;
            OperationResult<bool> drainResult = null;
            var completeTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                completeResult = operation.Complete();
            });
            var drainTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                drainResult = driver.LastSessions.CompleteDrain(driver.Reference, closing.Generation);
            });
            Task.WaitAll(completeTask, drainTask);
            AssertEx.Error(completeResult, "session_stale_result", FrameworkErrorCategory.Expired);
            AssertEx.True(drainResult.IsSuccess || (drainResult.Error != null && drainResult.Error.Code == "session_drain_incomplete"), "Concurrent drain returned an unexpected result.");
            if (!drainResult.IsSuccess) CompleteSessionDrain(driver.LastSessions, closing);
            FinishHostDrain(host);
        }

        private static void ConcurrentIpcAndBackpressureAreBounded()
        {
            var window = new IpcSequenceWindow("session-concurrent-ipc", 7, "nonce-concurrent-ipc", 4);
            var barrier = new Barrier(2);
            IpcFrameDecision firstDecision = null;
            IpcFrameDecision secondDecision = null;
            var firstTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                firstDecision = window.Evaluate(new IpcEnvelope("session-concurrent-ipc", 7, "nonce-concurrent-ipc", 1, "message-1", "payload-1"));
            });
            var secondTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                secondDecision = window.Evaluate(new IpcEnvelope("session-concurrent-ipc", 7, "nonce-concurrent-ipc", 2, "message-2", "payload-2"));
            });
            Task.WaitAll(firstTask, secondTask);
            AssertEx.True(firstDecision != null && secondDecision != null, "Concurrent IPC evaluation did not return decisions.");
            AssertEx.True(window.LastAccepted == 2, "Concurrent IPC evaluation lost the contiguous watermark.");
            AssertEx.True(firstDecision.Kind == IpcFrameDecisionKind.Accepted || secondDecision.Kind == IpcFrameDecisionKind.Accepted, "Concurrent IPC evaluation accepted no frame.");
            var duplicateBarrier = new Barrier(2);
            IpcFrameDecision duplicateFirst = null;
            IpcFrameDecision duplicateSecond = null;
            var duplicateTaskA = Task.Run(() =>
            {
                duplicateBarrier.SignalAndWait();
                duplicateFirst = window.Evaluate(new IpcEnvelope("session-concurrent-ipc", 7, "nonce-concurrent-ipc", 3, "message-3", "payload-3"));
            });
            var duplicateTaskB = Task.Run(() =>
            {
                duplicateBarrier.SignalAndWait();
                duplicateSecond = window.Evaluate(new IpcEnvelope("session-concurrent-ipc", 7, "nonce-concurrent-ipc", 3, "message-3", "payload-3"));
            });
            Task.WaitAll(duplicateTaskA, duplicateTaskB);
            AssertEx.True(duplicateFirst.Kind == IpcFrameDecisionKind.Accepted || duplicateSecond.Kind == IpcFrameDecisionKind.Accepted, "Concurrent IPC duplicate was not accepted.");
            AssertEx.True(duplicateFirst.Kind == IpcFrameDecisionKind.SafeRetry || duplicateSecond.Kind == IpcFrameDecisionKind.SafeRetry, "Concurrent IPC duplicate was not classified as a safe retry.");

            var gate = new BackpressureGate(2);
            var gateBarrier = new Barrier(3);
            var admissions = new bool[3];
            var gateTasks = new Task[3];
            for (var index = 0; index < gateTasks.Length; index++)
            {
                var taskIndex = index;
                gateTasks[index] = Task.Run(() =>
                {
                    gateBarrier.SignalAndWait();
                    admissions[taskIndex] = gate.TryEnter();
                });
            }
            Task.WaitAll(gateTasks);
            var admitted = 0;
            for (var index = 0; index < admissions.Length; index++) if (admissions[index]) admitted++;
            AssertEx.Equal(2, admitted, "Concurrent backpressure admitted the wrong number of tasks.");
            AssertEx.Equal(2, gate.Active, "Concurrent backpressure active count mismatch.");
            for (var index = 0; index < admissions.Length; index++) if (admissions[index]) gate.Exit();
            AssertEx.Equal(0, gate.Active, "Concurrent backpressure did not release all tasks.");
        }

        private static SessionLease BeginClosing(ISessionCoordinator sessions, SessionRef reference)
        {
            var result = sessions.BeginClosing(reference);
            AssertEx.True(result.IsSuccess, "Session close transition failed.");
            AssertEx.Equal(SessionState.Closing, result.Value.State, "Session did not enter Closing.");
            AssertEx.True(!string.IsNullOrWhiteSpace(result.Value.DrainTaskId), "Closing did not create a drain task ID.");
            return result.Value;
        }

        private static void CompleteSessionDrain(ISessionCoordinator sessions, SessionLease closing)
        {
            var result = sessions.CompleteDrain(closing.Reference, closing.Generation);
            AssertEx.True(result.IsSuccess, "Session drain failed.");
            AssertEx.Equal(SessionState.Drained, closing.State, "Session did not enter Drained.");
        }

        private static void CloseSessionAndHost(FrameworkHost host, SessionLease lease)
        {
            var closing = BeginClosing(host.Sessions, lease.Reference);
            CompleteSessionDrain(host.Sessions, closing);
            FinishHostDrain(host);
        }

        private static void FinishHostDrain(FrameworkHost host)
        {
            AssertEx.True(host.BeginClosing().IsSuccess, "Host close transition failed.");
            AssertEx.True(host.CompleteDrain().IsSuccess, "Host drain failed.");
            AssertEx.Equal(HostState.Drained, host.State, "Host did not enter Drained.");
            FrameworkHostLocator.Clear(host);
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "Host locator was not cleared.");
        }

        private static void CloseHostWithoutSession(FrameworkHost host)
        {
            AssertEx.True(host.BeginClosing().IsSuccess, "Sessionless host close transition failed.");
            AssertEx.True(host.CompleteDrain().IsSuccess, "Sessionless host drain failed.");
            AssertEx.Equal(HostState.Drained, host.State, "Sessionless host did not enter Drained.");
            FrameworkHostLocator.Clear(host);
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "Sessionless host locator was not cleared.");
        }

        private static void AssertNoLiveSession(SessionCoordinator sessions, string message)
        {
            var current = sessions.Current;
            AssertEx.True(current == null || current.State == SessionState.Drained, message);
        }

        private static void RequireEmptyLocator()
        {
            AssertEx.True(FrameworkHostLocator.Resolve() == null, "Fixture case started with a non-empty host locator.");
        }
    }
}
