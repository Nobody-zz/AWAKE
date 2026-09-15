using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json.Linq;

namespace Awake.WorldbookRuntimeProductionSmoke;

internal static partial class Program
{
    private static async Task RunWorldFactEventCallerTests()
    {
        int calls = 0;
        WorldFactQueryResult observed = new WorldFactQueryResult(
            WorldFactQueryStatus.Empty,
            WorldFactSelectionPolicy.EventTriggerCandidate,
            legacyFallbackState: LegacyFallbackState.NotUsed,
            journalRevision: 3,
            windowStartDay: 1,
            windowEndDay: 7);
        WorldEventContracts.SetFactContextReaderForTesting(new ProductionFactContextReader(() =>
        {
            calls++;
            return observed;
        }));

        AwakeRuntime.CurrentGameDayProvider = () => 7;
        AwakeEventEngine engine = new AwakeEventEngine();
        AwakeEventDefinition definition = new AwakeEventDefinition(
            "smoke.world-fact-event",
            "事实观察测试",
            "不应在无战役 UI 时弹出。",
            "继续",
            "跳过",
            source: AwakeEventSource.PresetRule,
            context: AwakeEventContext.MapMarch,
            subject: AwakeEventSubject.World,
            content: AwakeEventContent.World,
            resolution: AwakeEventResolution.NarrativeOnly,
            choiceShape: AwakeEventChoiceShape.Informational,
            persistence: AwakeEventPersistence.Repeatable);
        Check(engine.Register(new AwakeEventRule(definition, 1, 0, AwakeEventCondition.InSettlement)), "event caller fixture rule must register");

        try
        {
            await engine.OnHourlyTickAsync(CancellationToken.None).ConfigureAwait(false);
            Check(calls == 1, "hourly event engine must call the event fact facade exactly once");
            Check(HasCapturedLog("consumer=event")
                && HasCapturedLog("status=Empty")
                && HasCapturedLog("journal_revision=3")
                && HasCapturedLog("error_code="),
                "event fact facade log must contain the stable observation fields");
            Check(!HasCapturedLog("awake_event_engine_fired id=smoke.world-fact-event"),
                "empty fact observation must not alter the no-UI rule path");

            observed = new WorldFactQueryResult(
                WorldFactQueryStatus.Success,
                WorldFactSelectionPolicy.EventTriggerCandidate,
                journalRevision: 4,
                windowStartDay: 1,
                windowEndDay: 7);
            AwakeEventEngine successEngine = new AwakeEventEngine();
            AwakeEventDefinition successDefinition = new AwakeEventDefinition(
                "smoke.world-fact-event-success",
                "事实成功兼容测试",
                "无事实条件旧规则。",
                "继续",
                "跳过",
                source: AwakeEventSource.PresetRule,
                context: AwakeEventContext.MapMarch,
                subject: AwakeEventSubject.World,
                content: AwakeEventContent.World,
                resolution: AwakeEventResolution.NarrativeOnly,
                choiceShape: AwakeEventChoiceShape.Informational,
                persistence: AwakeEventPersistence.Repeatable);
            Check(successEngine.Register(new AwakeEventRule(successDefinition, 1, 0, AwakeEventCondition.InSettlement)),
                "success compatibility fixture rule must register");
            await successEngine.OnHourlyTickAsync(CancellationToken.None).ConfigureAwait(false);

            observed = new WorldFactQueryResult(
                WorldFactQueryStatus.Unavailable,
                WorldFactSelectionPolicy.EventTriggerCandidate,
                errorCode: "smoke.fact_unavailable");
            AwakeEventEngine unavailableEngine = new AwakeEventEngine();
            AwakeEventDefinition unavailableDefinition = new AwakeEventDefinition(
                "smoke.world-fact-event-unavailable",
                "事实不可用兼容测试",
                "无事实条件旧规则。",
                "继续",
                "跳过",
                source: AwakeEventSource.PresetRule,
                context: AwakeEventContext.MapMarch,
                subject: AwakeEventSubject.World,
                content: AwakeEventContent.World,
                resolution: AwakeEventResolution.NarrativeOnly,
                choiceShape: AwakeEventChoiceShape.Informational,
                persistence: AwakeEventPersistence.Repeatable);
            Check(unavailableEngine.Register(new AwakeEventRule(unavailableDefinition, 1, 0, AwakeEventCondition.InSettlement)),
                "unavailable compatibility fixture rule must register");
            await unavailableEngine.OnHourlyTickAsync(CancellationToken.None).ConfigureAwait(false);
            Check(calls == 3
                && HasCapturedLog("status=Success")
                && HasCapturedLog("status=Unavailable")
                && !HasCapturedLog("awake_event_engine_fired id=smoke.world-fact-event-success")
                && !HasCapturedLog("awake_event_engine_fired id=smoke.world-fact-event-unavailable"),
                "old rules did not preserve Engine behavior across fact statuses");
        }
        finally
        {
            WorldEventContracts.SetFactContextReaderForTesting(null);
        }

    }

    private static async Task RunWorldFactTriggerCallerTests()
    {
        int calls = 0;
        WorldFactQueryResult observed = null;
        Check(WorldFactCapture.TryCreateWar(7, 1008, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture war),
            "event fact trigger caller fixture creation failed");
        WorldEventContracts.SetFactContextReaderForTesting(new ProductionFactContextReader(() =>
        {
            calls++;
            return observed;
        }));

        observed = new WorldFactQueryResult(
            WorldFactQueryStatus.Success,
            WorldFactSelectionPolicy.EventTriggerCandidate,
            new[] { war.Fact.ToJson() },
            journalRevision: 4,
            windowStartDay: 1,
            windowEndDay: 7);

        AwakeRuntime.CurrentGameDayProvider = () => 7;
        AwakeEventEngine engine = new AwakeEventEngine();
        AwakeEventDefinition definition = new AwakeEventDefinition(
            "smoke.world-fact-trigger",
            "事实触发测试",
            "不应在无战役 UI 时弹出。",
            "继续",
            "跳过",
            source: AwakeEventSource.PresetRule,
            context: AwakeEventContext.MapMarch,
            subject: AwakeEventSubject.World,
            content: AwakeEventContent.World,
            resolution: AwakeEventResolution.NarrativeOnly,
            choiceShape: AwakeEventChoiceShape.Informational,
            persistence: AwakeEventPersistence.Repeatable);
        AwakeEventFactTrigger trigger = new AwakeEventFactTrigger(
            new[] { "war_declared" }, 1, 7);
        Check(engine.Register(new AwakeEventRule(definition, 1, 0, AwakeEventCondition.InSettlement, factTrigger: trigger)),
            "event fact trigger fixture rule must register");

        try
        {
            await engine.OnHourlyTickAsync(CancellationToken.None).ConfigureAwait(false);
            Check(calls == 1, "fact trigger hourly caller must query the facade exactly once");
            Check(HasCapturedLog("awake_event_fact_trigger id=smoke.world-fact-trigger eligible=True")
                && HasCapturedLog("reason=fact_trigger_matched")
                && HasCapturedLog("matched=1")
                && HasCapturedLog("journal_revision=4"),
                "production hourly caller did not observe the matching fact trigger: " + RecentCapturedLogs());

            observed = new WorldFactQueryResult(
                WorldFactQueryStatus.Missing,
                WorldFactSelectionPolicy.EventTriggerCandidate,
                journalRevision: 5);
            await engine.OnHourlyTickAsync(CancellationToken.None).ConfigureAwait(false);
            observed = new WorldFactQueryResult(
                WorldFactQueryStatus.Corrupt,
                WorldFactSelectionPolicy.EventTriggerCandidate,
                errorCode: "smoke.fact_corrupt",
                journalRevision: 6);
            await engine.OnHourlyTickAsync(CancellationToken.None).ConfigureAwait(false);
            observed = new WorldFactQueryResult(
                WorldFactQueryStatus.Success,
                WorldFactSelectionPolicy.EventTriggerCandidate,
                new[] { war.Fact.ToJson() },
                usedLegacyFallback: true,
                legacyFallbackState: LegacyFallbackState.Used,
                journalRevision: 7);
            await engine.OnHourlyTickAsync(CancellationToken.None).ConfigureAwait(false);
            Check(calls == 4
                && HasCapturedLog("eligible=False reason=fact_trigger_no_match")
                && HasCapturedLog("eligible=False reason=smoke.fact_corrupt")
                && HasCapturedLog("eligible=False reason=fact_trigger_legacy_fallback")
                && !HasCapturedLog("awake_event_engine_fired id=smoke.world-fact-trigger"),
                "production fact trigger failure states did not fail closed in the Engine");
        }
        finally
        {
            WorldEventContracts.SetFactContextReaderForTesting(null);
        }
    }

    private static async Task RunWorldFactTriggerContentApiTests()
    {
        AwakeRuleRegistry.ResetForTesting();
        ProductionFactTriggerPack pack = new ProductionFactTriggerPack();
        AwakeContentPackManager.Register(pack);
        Check(pack.Registered, "public content registry rejected a valid fact trigger event");
        AwakeRuleManifest manifest;
        Check(AwakeRuleRegistry.TryGet("smoke.content-fact-trigger.event", out manifest),
            "public content registry did not store the event manifest");
        Check(manifest.Payload["factTrigger"] is JObject, "content API did not map factTrigger into payload");
        AwakeEventRule rule;
        Check(AwakeEventDataLoader.TryParseRule(manifest.Payload, out rule, out string error), error);
        Check(rule.FactTrigger != null
            && rule.FactTrigger.AllowedKinds.Count == 1
            && rule.FactTrigger.AllowedKinds[0] == "war_declared"
            && rule.FactTrigger.MinimumMatches == 1
            && rule.FactTrigger.MaximumAgeDays == 7,
            "content API factTrigger payload did not round-trip through the loader");

        int calls = 0;
        Check(WorldFactCapture.TryCreateWar(7, 1008, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture war),
            "content API Engine fixture creation failed");
        WorldEventContracts.SetFactContextReaderForTesting(new ProductionFactContextReader(() =>
        {
            calls++;
            return new WorldFactQueryResult(
                WorldFactQueryStatus.Success,
                WorldFactSelectionPolicy.EventTriggerCandidate,
                new[] { war.Fact.ToJson() },
                journalRevision: 7,
                windowStartDay: 1,
                windowEndDay: 7);
        }));
        AwakeRuntime.CurrentGameDayProvider = () => 7;
        AwakeEventEngine engine = new AwakeEventEngine();
        engine.EnsureRulesLoadedFromRegistry();
        Check(engine.RuleCount == 1, "content API event was not loaded into the Engine registry");
        await engine.OnHourlyTickAsync(CancellationToken.None).ConfigureAwait(false);
        Check(calls == 1 && HasCapturedLog("awake_event_fact_trigger id=smoke.content-fact-trigger eligible=True")
            && HasCapturedLog("reason=fact_trigger_matched"),
            "content API event did not reach the Engine fact candidate path");
        WorldEventContracts.SetFactContextReaderForTesting(null);
    }

    private static async Task RunWorldFactEventCancellationTests()
    {
        int calls = 0;
        WorldEventContracts.SetFactContextReaderForTesting(new ProductionFactContextReader(() =>
        {
            calls++;
            return new WorldFactQueryResult(
                WorldFactQueryStatus.Success,
                WorldFactSelectionPolicy.EventTriggerCandidate,
                journalRevision: 5);
        }));

        AwakeRuntime.CurrentGameDayProvider = () => 7;
        AwakeEventEngine engine = new AwakeEventEngine();
        AwakeEventDefinition definition = new AwakeEventDefinition(
            "smoke.world-fact-cancelled",
            "取消事实测试",
            "取消时不应进入候选链。",
            "继续",
            "跳过",
            source: AwakeEventSource.PresetRule,
            context: AwakeEventContext.MapMarch,
            subject: AwakeEventSubject.World,
            content: AwakeEventContent.World,
            resolution: AwakeEventResolution.NarrativeOnly,
            choiceShape: AwakeEventChoiceShape.Informational,
            persistence: AwakeEventPersistence.Repeatable);
        Check(engine.Register(new AwakeEventRule(definition, 1, 0, AwakeEventCondition.InSettlement)),
            "cancelled event fixture rule must register");
        CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await engine.OnHourlyTickAsync(cancellation.Token).ConfigureAwait(false);
            Check(calls == 0, "pre-cancelled event tick must not start fact observation");
            Check(!HasCapturedLog("awake_event_fact_trigger id=smoke.world-fact-cancelled")
                && !HasCapturedLog("awake_event_engine_fired id=smoke.world-fact-cancelled"),
                "cancelled event tick entered candidate or popup paths");
        }
        finally
        {
            cancellation.Dispose();
            WorldEventContracts.SetFactContextReaderForTesting(null);
        }
    }

    private static async Task TestPendingEventProvenanceAsync()
    {
        ProductionSmokeHost host = CreateHost("pending-event-provenance");
        ProductionSmokeKeyValueStore worldEvents = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        WorldStateStore store = CreateStore(host, worldEvents);
        await Install(store).ConfigureAwait(false);

        store.EnqueuePendingEventForTesting(new WorldPendingEvent(
            "awake:event:pending-provenance",
            "awake.test.command",
            string.Empty,
            new JObject { ["candidate"] = true },
            "pending-provenance-correlation",
            "awake.test.event",
            "awake.test.event.v1"));
        await store.DrainAsync(CancellationToken.None).ConfigureAwait(false);

        EventEnvelope published = host.EventsAdapter.Published.Single();
        Check(published.AccessScope == DataAccessScope.SensitiveExtension, "pending event must not publish as PlayerKnown");
        Check(published.SourceClass == SourceClass.ExtensionProvider, "pending event source provenance must be preserved");
        Check(published.EpistemicStatus == EpistemicStatus.Fact, "applied pending event must retain fact status");
    }

    private static async Task TestWeeklyReportSnapshotRepairAsync()
    {
        ProductionSmokeHost host = CreateHost("weekly-repair");
        ProductionSmokeKeyValueStore storage = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        JObject state = BuildWorldEventsState(
            "awake:event:weekly-repair-source",
            "weekly-repair-source-key",
            7,
            "weekly report repair event");
        Check(WorldFactCapture.TryCreateWar(7, 1008, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture repairFact), "weekly repair fact fixture creation failed");
        SeedWorldFactJournal(host, new[] { repairFact });
        state["weeklyReports"] = new JArray
        {
            new JObject
            {
                ["reportId"] = "awake:report:weekly-v2-7",
                ["schemaVersion"] = WeeklyReportService.V2SchemaVersion,
                ["windowStartDay"] = 1,
                ["windowEndDay"] = 7,
                ["status"] = "applied",
                ["attemptCount"] = 1,
                ["lastAttemptDay"] = 7,
                ["lastErrorCode"] = string.Empty,
                ["report"] = new JObject { ["reportId"] = "awake:report:weekly-7" }
            }
        };
        storage.Put(AiTaskConstants.WorldEventsKey, state.ToString(Newtonsoft.Json.Formatting.None));
        WorldStateStore store = CreateStore(host, storage);
        store.InjectStoreForTesting(AiTaskConstants.WorldFactJournalNamespace, host.StorageAdapter.GetNamespace(AiTaskConstants.WorldFactJournalNamespace));
        await Install(store).ConfigureAwait(false);
        WorldKnowledgeQueryService query = BindKnowledge();
        AwakeRuntime.CurrentGameDayProvider = () => 7;

        await WorldEventLedger.LoadFromStoreAsync(CancellationToken.None).ConfigureAwait(false);
        Check(!AwakeRuntime.IsNativeKnowledgeReady(), "native readiness must be false before the readiness probe succeeds");
        Check(query.Search("周报", 10).Count == 0, "weekly report must not project before native readiness");
        Check(!WorldEventServices.TryProjectEventsIfReady(
            WorldEventLedger.CampaignGeneration,
            store,
            WorldEventLedger.CaptureSnapshot()),
            "weekly report source events must not project before native readiness");

        AwakeRuntime.NativeReadinessProbeForTesting = (generation, cancellationToken) =>
            Task.FromResult(NativeReadinessResult.Ready(generation, host.CurrentSession.SessionId));
        NativeReadinessResult readiness = await AwakeRuntime.EnsureNativeReadinessAsync(
            host.CurrentSession.SessionId,
            CancellationToken.None).ConfigureAwait(false);
        Check(readiness.Status == NativeReadinessStatus.Ready, "weekly repair readiness must be ready");
        FormalWeeklyReportResult repairedFormal = await WorldEventServices.EnsureFormalReportsReadyAsync(7, CancellationToken.None).ConfigureAwait(false);
        Check(repairedFormal.HasFormalReport, "corrupt v2 weekly report must be repaired before projection: " + repairedFormal.Code);
        Check(await AwakeRuntime.EnsureKnowledgeReadyIfNativeReadyAsync(CancellationToken.None).ConfigureAwait(false), "knowledge readiness must complete after native readiness");
        Check(query.Search("周报", 10).Count == 1, "repaired weekly report must project exactly once");

        JObject persisted = ParseJsonObjectPreservingDateStrings(storage.Read(AiTaskConstants.WorldEventsKey));
        JObject applied = ((JArray)persisted["weeklyReports"]).Children<JObject>()
            .First(value => StringComparer.Ordinal.Equals((string)value["reportId"], "awake:report:weekly-v2-7"));
        JObject report = applied["report"] as JObject;
        Check(StringComparer.Ordinal.Equals((string)applied["status"], "applied"), "repaired weekly report must remain applied");
        Check(report != null && StringComparer.Ordinal.Equals((string)report["reportId"], "awake:report:weekly-v2-7"), "repaired snapshot must preserve reportId");
        Check(report != null && WorldEventContract.TryValidateWeeklyReport(report, out _), "repaired snapshot must satisfy the production report contract");
        await store.BeginFinalDrainAsync().ConfigureAwait(false);
    }

    private static async Task TestSubModuleResetBoundaryAsync()
    {
        ProductionSmokeHost host = CreateHost("submodule-reset");
        ProductionSmokeKeyValueStore storage = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        WorldStateStore store = CreateStore(host, storage);
        await Install(store).ConfigureAwait(false);

        SubModule module = new SubModule();
        MethodInfo onSubModuleLoad = typeof(SubModule).GetMethod(
            "OnSubModuleLoad",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check(onSubModuleLoad != null, "production SubModule load entry must exist");
        onSubModuleLoad.Invoke(module, null);
        Check(CampaignResetLifecycle.Reset != null, "SubModule must install the production campaign reset callback");
        ProductionSmokeGetBarrier barrier = null;
        try
        {
            WorldEventAppendResult result = await WorldEventLedger.RecordAsync(
                100,
                "war",
                "submodule reset boundary event",
                "submodule-reset-key",
                CancellationToken.None).ConfigureAwait(false);
            Check(result.Succeeded, "pre-reset event must persist");
            Check(WorldEventLedger.Count == 1, "pre-reset event must be visible before SubModule reset");
            barrier = storage.BlockNextGet(AiTaskConstants.WorldEventsKey);
            Check(store.TryEnqueue(CreateCommand("submodule-reset-drain")), "reset drain command must be accepted before reset");

            CampaignResetLifecycle.Reset();
            await WaitForAsync(barrier.Started, "SubModule reset drain did not reach storage").ConfigureAwait(false);
            Check(WorldEventLedger.Count == 0, "SubModule reset must clear the campaign ledger through the unified boundary");
            Check(store.LifecycleState == WorldStateStoreLifecycle.Ending, "SubModule reset must leave the old store in Ending while drain runs");
            Check(AwakeRuntime.WorldStateStore == null, "SubModule reset must detach the old world state store");
        }
        finally
        {
            barrier?.Release();
            await store.BeginFinalDrainAsync().ConfigureAwait(false);
            CampaignResetLifecycle.Reset = null;
        }
    }

    private static async Task TestFinalDrainSingleFlightAsync()
    {
        ProductionSmokeHost host = CreateHost("single-flight");
        ProductionSmokeKeyValueStore worldEvents = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        ProductionSmokeKeyValueStore memories = new ProductionSmokeKeyValueStore(AiTaskConstants.NpcMemoriesNamespace);
        WorldStateStore store = CreateStore(host, worldEvents, memories);
        await Install(store).ConfigureAwait(false);
        Check(store.ReserveMemory("hero.single-flight", "dialogue", 100, out _, out _), "single-flight reservation must be accepted");

        Task<WorldFinalDrainResult> first = store.BeginFinalDrainAsync();
        Task<WorldFinalDrainResult> second = store.BeginFinalDrainAsync();
        Check(ReferenceEquals(first, second), "repeated final drain must return the same task");
        Check(!first.IsCompleted, "final drain must remain asynchronous after the call returns");

        WorldFinalDrainResult result = await WithTimeoutAsync(first, "single-flight final drain did not settle").ConfigureAwait(false);
        Check(result.Succeeded, "single-flight final drain must succeed for a valid store");
        Check(result.PendingWrites == 0 && result.PendingEvents == 0 && result.DroppedItems == 0, "successful final drain must report an empty queue");
        Check(store.LifecycleState == WorldStateStoreLifecycle.Ended, "single-flight final drain must end the store");
    }

    private static async Task TestFailedDrainFailsClosedAsync()
    {
        ProductionSmokeHost host = CreateHost("failed-drain");
        host.EventsAdapter.FailPublishes = true;
        ProductionSmokeKeyValueStore storage = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        WorldStateStore store = CreateStore(host, storage);
        await Install(store).ConfigureAwait(false);
        store.EnqueuePendingEventForTesting(new WorldPendingEvent(
            "failed-drain-event",
            "failed-drain-command",
            string.Empty,
            new JObject { ["eventKey"] = "failed-drain-key" },
            "failed-drain-correlation",
            "awake.test.event",
            "awake.test.event.v1"));

        Task<WorldFinalDrainResult> drain = AwakeRuntime.BeginSessionEnd();
        WorldFinalDrainResult result = await WithTimeoutAsync(drain, "failed final drain did not settle").ConfigureAwait(false);
        Check(!result.Succeeded, "failed event publication must fail the final drain");
        Check(result.DroppedItems == 1, "failed event publication must report the dropped event");
        Check(StringComparer.Ordinal.Equals(result.ErrorCode, "awake.world_state.final_drain_dropped"), "failed final drain must expose a stable dropped error code");
        Check(store.LifecycleState == WorldStateStoreLifecycle.Ended, "failed final drain must still end the store");

        AwakeRuntime.ResetSessionStateForCampaign();
        bool ready = await AwakeRuntime.EnsureWorldStateReadyAsync(
            host,
            CancellationToken.None,
            new[] { AiTaskConstants.WorldEventsNamespace }).ConfigureAwait(false);
        Check(!ready, "a failed retired drain must block readiness");
        Check(AwakeRuntime.WorldStateStore == null, "failed drain must not install a replacement store");

        WorldStateStore candidate = CreateStore(host, new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace));
        bool installed = await AwakeRuntime.SetWorldStateStore(candidate).ConfigureAwait(false);
        Check(!installed, "a failed retired drain must block the async setter");
        Check(candidate.LifecycleState == WorldStateStoreLifecycle.Ended, "rejected replacement store must be drained and ended");
    }

    private static async Task TestProbeSessionEndingNonBlockingAsync()
    {
        ProductionSmokeHost host = CreateHost("probe-session-ending");
        ProductionSmokeKeyValueStore storage = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        ProductionSmokeGetBarrier barrier = storage.BlockNextGet(AiTaskConstants.WorldEventsKey);
        WorldStateStore store = CreateStore(host, storage);
        await Install(store).ConfigureAwait(false);
        Check(store.TryEnqueue(CreateCommand("probe-session-ending-command")), "session-ending probe command must be accepted before the boundary");

        AwakeExtension extension = new AwakeExtension();
        try
        {
            extension.OnLifecycle(ExtensionLifecycleStage.SessionEnding, host.CurrentSession);
            Task<AwakeSessionDrainResult> sessionDrain = extension.WaitForSessionEndDrainAsync();
            Check(sessionDrain != null, "SessionEnding must publish a typed session drain task");
            Check(!sessionDrain.IsCompleted, "SessionEnding drain must remain pending while storage is blocked");
            await Task.Delay(50).ConfigureAwait(false);
            Check(store.LifecycleState == WorldStateStoreLifecycle.Ending, "SessionEnding must return while the store is still draining");
            Task[] observed;
            lock (ObservedBackgroundTasks) observed = ObservedBackgroundTasks.ToArray();
            Check(observed.Any(task => task != null && !task.IsCompleted), "SessionEnding must leave an observable background drain in progress");
            barrier.Release();
            await WaitUntilAsync(
                () =>
                {
                    lock (ObservedBackgroundTasks)
                    {
                        return ObservedBackgroundTasks.All(task => task == null || task.IsCompleted);
                    }
                },
                "SessionEnding background drain did not complete",
                5000).ConfigureAwait(false);
            Check(store.LifecycleState == WorldStateStoreLifecycle.Ended, "SessionEnding background drain must end the store");
            AwakeSessionDrainResult result = await WithTimeoutAsync(
                sessionDrain,
                "typed SessionEnding drain did not settle").ConfigureAwait(false);
            Check(result != null && result.Succeeded, "typed SessionEnding drain must report success");
        }
        finally
        {
            barrier.Release();
        }
    }

    private static async Task TestProbeCampaignReadyBoundaryAsync()
    {
        ProductionSmokeHost host = CreateHost("probe-ready");
        AwakeRuntime.SetHostOverrideForTesting(host);
        AwakeRuntime.CurrentGameDayProvider = () => 7;
        Check(WorldFactCapture.TryCreateWar(7, 1008, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture war), "campaign-ready fact fixture creation failed");
        SeedWorldFactJournal(host, new[] { war });
        int generationBefore = AwakeRuntime.SessionGeneration;
        AwakeRuntime.NativeReadinessProbeForTesting = (generation, cancellationToken) =>
            Task.FromResult(NativeReadinessResult.Failed(generation, "production_smoke_native_unavailable", retryable: true));

        SubModule module = new SubModule();
        MethodInfo onSubModuleLoad = typeof(SubModule).GetMethod(
            "OnSubModuleLoad",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check(onSubModuleLoad != null, "production SubModule load entry must exist for campaign-ready reset");
        onSubModuleLoad.Invoke(module, null);
        Action installedReset = CampaignResetLifecycle.Reset;
        Check(installedReset != null, "SubModule must install the campaign-ready reset callback");
        int resetCount = 0;
        CampaignResetLifecycle.Reset = () =>
        {
            resetCount++;
            installedReset();
        };

        AwakeExtension extension = new AwakeExtension();
        try
        {
            extension.OnLifecycle(ExtensionLifecycleStage.CampaignSessionReady, host.CurrentSession);
            NativeReadinessResult readiness = await WithTimeoutAsync(
                AwakeRuntime.EnsureNativeReadinessAsync(host.CurrentSession.SessionId, CancellationToken.None),
                "Probe campaign-ready readiness did not settle").ConfigureAwait(false);

            await WaitUntilAsync(
                () => AwakeRuntime.WorldStateStore != null,
                "CampaignSessionReady storage-only readiness did not install a store",
                5000).ConfigureAwait(false);
            WorldStateStore currentStore = AwakeRuntime.WorldStateStore;
            List<WeeklyReportApplicationState> reports = null;
            try
            {
                await WaitUntilAsync(
                    () =>
                    {
                        reports = currentStore.GetWeeklyReportStatesAsync(CancellationToken.None).GetAwaiter().GetResult();
                        return reports.Any(value => StringComparer.Ordinal.Equals(value.SchemaVersion, WeeklyReportService.V2SchemaVersion)
                            && value.Report != null
                            && StringComparer.Ordinal.Equals(value.Status, "applied"));
                    },
                    "CampaignSessionReady did not persist a v2 formal report",
                    5000).ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                string rawState = host.StorageAdapter.GetNamespace(AiTaskConstants.WorldEventsNamespace)?.Read(AiTaskConstants.WorldEventsKey);
                string validation = "none";
                try
                {
                    JObject rawDocument = ParseJsonObjectPreservingDateStrings(rawState);
                    JObject rawReport = rawDocument["weeklyReports"]?.Children<JObject>()
                        .Select(value => value["report"] as JObject)
                        .FirstOrDefault(value => value != null);
                    if (rawReport != null)
                    {
                        string validationError;
                        validation = WeeklyReportService.TryValidateV2Report(rawReport, out validationError)
                            ? "valid"
                            : validationError;
                    }
                }
                catch (Exception diagnosticException)
                {
                    validation = "diagnostic_exception:" + diagnosticException.Message;
                }
                string reportStateDump = reports == null
                    ? "null"
                    : string.Join(";", reports.Select(value => (value.SchemaVersion ?? string.Empty) + "/" + (value.Status ?? string.Empty) + "/" + (value.Report == null ? "no-report" : value.Report.ToString(Newtonsoft.Json.Formatting.None))));
                throw new InvalidOperationException(exception.Message + " states=" + reportStateDump
                    + " validation=" + validation
                    + " raw=" + (rawState ?? "null")
                    + " logs=" + RecentCapturedLogs());
            }

            Check(resetCount == 1, "CampaignSessionReady must invoke the installed reset callback exactly once");
            Check(AwakeRuntime.SessionGeneration == generationBefore + 1, "Probe campaign-ready entry must advance the campaign generation exactly once");
            Check(!AwakeRuntime.SessionEnded, "Probe campaign-ready entry must leave the new campaign active");
            Check(readiness.Status == NativeReadinessStatus.Failed, "Probe campaign-ready entry must preserve the failed native boundary");
            Check(readiness.SessionGeneration == AwakeRuntime.SessionGeneration, "Probe readiness must belong to the current campaign generation");
            Check(!AwakeRuntime.IsNativeKnowledgeReady(), "failed native readiness must keep knowledge projection closed");
            Check(host.PermissionsAdapter.RequestCount == 0, "CampaignSessionReady storage-only path must not request permission");
            Check(reports.Count(value => StringComparer.Ordinal.Equals(value.SchemaVersion, WeeklyReportService.V2SchemaVersion)) == 1,
                "CampaignSessionReady must create one v2 formal report state");
        }
        finally
        {
            extension.OnLifecycle(ExtensionLifecycleStage.Unregistered, host.CurrentSession);
        }
    }

    private static async Task TestDirectWriteReplacementBoundaryAsync()
    {
        ProductionSmokeHost host = CreateHost("direct-write-replacement");
        ProductionSmokeKeyValueStore oldWorldEvents = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        ProductionSmokeKeyValueStore oldMemories = new ProductionSmokeKeyValueStore(AiTaskConstants.NpcMemoriesNamespace);
        WorldStateStore oldStore = CreateStore(host, oldWorldEvents, oldMemories);
        await Install(oldStore).ConfigureAwait(false);

        string heroId = "hero.direct-write-replacement";
        ProductionSmokeSetBarrier barrier = oldMemories.BlockNextSet(WorldStateStore.BuildHeroKey(heroId));
        Task<bool> oldWrite = oldStore.WriteEmptyMemoryAsync(heroId, CancellationToken.None);
        try
        {
            await WaitForAsync(barrier.Started, "old Store direct write did not reach storage").ConfigureAwait(false);

            WorldStateStore replacement = CreateStore(
                host,
                new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace),
                new ProductionSmokeKeyValueStore(AiTaskConstants.NpcMemoriesNamespace));
            Task<bool> replacementInstall = AwakeRuntime.SetWorldStateStore(replacement);
            await WaitUntilAsync(
                () => oldStore.LifecycleState == WorldStateStoreLifecycle.Ending,
                "old Store did not enter Ending before replacement",
                5000).ConfigureAwait(false);
            Check(!replacementInstall.IsCompleted, "replacement Store must wait for an in-flight direct write on the old Store");

            barrier.Release();
            Check(await WithTimeoutAsync(oldWrite, "old Store direct write did not settle"), "old Store direct write must complete before replacement");
            Check(await WithTimeoutAsync(replacementInstall, "replacement Store installation did not settle"), "replacement Store must install after old direct write drains");
            Check(ReferenceEquals(AwakeRuntime.WorldStateStore, replacement), "replacement Store must become the current Store");
        }
        finally
        {
            barrier.Release();
        }
    }
}

internal sealed class ProductionFactContextReader : IWorldFactContextReader
{
    private readonly Func<WorldFactQueryResult> _read;

    internal ProductionFactContextReader(Func<WorldFactQueryResult> read)
    {
        _read = read;
    }

    public Task<WorldFactQueryResult> QueryAsync(WorldFactQueryRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_read());
    }
}

internal sealed class ProductionFactTriggerPack : IAwakeContentPack
{
    internal bool Registered { get; private set; }
    public string Id => "smoke.content-fact-trigger";

    public void Register(IAwakeContentRegistry registry)
    {
        Registered = registry.RegisterEvent(new AwakeContentEvent
        {
            Id = Id,
            Title = "内容包事实触发",
            Body = "内容包映射测试。",
            OptionA = "继续",
            OptionB = "跳过",
            FactTrigger = new AwakeContentFactTrigger
            {
                AllowedKinds = new[] { "war_declared" },
                MinimumMatches = 1,
                MaximumAgeDays = 7
            }
        });
    }
}
