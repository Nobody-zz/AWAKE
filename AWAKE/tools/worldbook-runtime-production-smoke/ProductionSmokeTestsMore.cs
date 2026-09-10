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
    private static Task TestNpcDialogueStructuredCompletionAsync()
    {
        ProductionSmokeHost host = CreateHost("npc-dialogue-structured");
        using (NpcDialogueService service = new NpcDialogueService(host, string.Empty, "测试 NPC", string.Empty))
        {
            SetPrivateField(service, "_generation", 1);
            SetPrivateField(service, "_sending", true);
            SetPrivateField(service, "_pendingPlayerText", "你是谁？");
            AiTaskEvent completed = new AiTaskEvent(
                "task-npc-dialogue",
                "message-npc-dialogue",
                AiTaskEventKind.Completed,
                3,
                string.Empty,
                null,
                "fixture-model",
                0,
                0,
                "{\"reply\":\"结构化回应\",\"mood\":\"平静\",\"effects\":[]}",
                "fixture-provider",
                string.Empty,
                string.Empty);

            InvokePrivate(service, "HandleCompleted", 1, "correlation-dialogue", completed);

            NpcDialogueUiEvent uiEvent;
            Check(service.TryDrainUiEvent(out uiEvent), "structured dialogue completion must emit a UI event");
            Check(uiEvent.Kind == NpcDialogueUiEventKind.TurnCompleted, "structured dialogue completion must succeed");
            Check(uiEvent.Turn != null && uiEvent.Turn.Reply == "结构化回应", "structured dialogue reply must reach the UI result");
        }

        return Task.CompletedTask;
    }

    private static Task TestNpcMemoryStructuredContentAsync()
    {
        string summary = NpcMemorySummaryTemplate.ParseSummary("{\"summary\":\"结构化摘要\"}");
        Check(summary == "结构化摘要", "structured memory summary must be accepted");
        string textFallback = NpcMemorySummaryTemplate.ParseSummary("{\"summary\":\"文本回退摘要\"}");
        Check(textFallback == "文本回退摘要", "text fallback payload must remain parseable");
        return Task.CompletedTask;
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new InvalidOperationException("missing private field: " + name);
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string name, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException("missing private method: " + name);
        method.Invoke(target, arguments);
    }

    private static async Task TestLoadCannotClearNewEventsAsync()
    {
        ProductionSmokeHost host = CreateHost("load-monotonicity");
        ProductionSmokeKeyValueStore storage = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        storage.Put(AiTaskConstants.WorldEventsKey, BuildWorldEventsState(
            "awake:event:loaded-old",
            "loaded-old-key",
            90,
            "old persisted event").ToString(Newtonsoft.Json.Formatting.None));
        ProductionSmokeGetBarrier barrier = storage.BlockNextGet(AiTaskConstants.WorldEventsKey);
        WorldStateStore store = CreateStore(host, storage);
        await Install(store).ConfigureAwait(false);

        Task load = WorldEventLedger.LoadFromStoreAsync(CancellationToken.None);
        await WaitForAsync(barrier.Started, "old load did not reach storage").ConfigureAwait(false);
        try
        {
            WorldEventAppendResult append = await WorldEventLedger.RecordAsync(
                100,
                "war",
                "new event while load is waiting",
                "load-new-key",
                CancellationToken.None).ConfigureAwait(false);
            Check(append.Succeeded, "new event must persist while load is waiting");
            Check(WorldEventLedger.SnapshotAll().Any(value => value.EventKey == "load-new-key"), "new event must enter ledger before load completes");
        }
        finally
        {
            barrier.Release();
        }

        await WaitForAsync(load, "stale load did not settle").ConfigureAwait(false);
        IReadOnlyList<WorldEventRecord> records = WorldEventLedger.SnapshotAll();
        Check(records.Count == 1, "load with a changed revision must not replace the current ledger");
        Check(records[0].EventKey == "load-new-key", "new event must survive a concurrent load");
        Check(!records.Any(value => value.EventKey == "loaded-old-key"), "old load payload must not be applied after the ledger changed");
        await store.BeginFinalDrainAsync().ConfigureAwait(false);
    }

    private static async Task TestStoreSwitchBlocksOldLoadAsync()
    {
        ProductionSmokeHost host = CreateHost("store-switch");
        ProductionSmokeKeyValueStore oldStorage = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        oldStorage.Put(AiTaskConstants.WorldEventsKey, BuildWorldEventsState(
            "awake:event:old-store",
            "old-store-key",
            90,
            "old store event").ToString(Newtonsoft.Json.Formatting.None));
        ProductionSmokeGetBarrier barrier = oldStorage.BlockNextGet(AiTaskConstants.WorldEventsKey);
        WorldStateStore oldStore = CreateStore(host, oldStorage);
        await Install(oldStore).ConfigureAwait(false);

        Task oldLoad = WorldEventLedger.LoadFromStoreAsync(CancellationToken.None);
        await WaitForAsync(barrier.Started, "old store load did not reach storage").ConfigureAwait(false);
        try
        {
            AwakeRuntime.ResetSessionStateForCampaign();
            Check(oldStore.LifecycleState == WorldStateStoreLifecycle.Ending, "campaign reset must invalidate the old store");
            Check(AwakeRuntime.WorldStateStore == null, "campaign reset must detach the old store");

            ProductionSmokeKeyValueStore newStorage = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
            newStorage.Put(AiTaskConstants.WorldEventsKey, BuildWorldEventsState(
                "awake:event:new-store",
                "new-store-key",
                101,
                "new store event").ToString(Newtonsoft.Json.Formatting.None));
            WorldStateStore newStore = CreateStore(host, newStorage);
            Task installNewStore = Install(newStore);
            await WaitForAsync(installNewStore, "new store install did not settle after the old drain").ConfigureAwait(false);
            Check(oldStore.LifecycleState == WorldStateStoreLifecycle.Ended, "replacement install must wait for the old store drain");
            barrier.Release();
            await WaitForAsync(oldLoad, "old store load did not settle after replacement").ConfigureAwait(false);
            Check(WorldEventLedger.Count == 0, "old store load must not repopulate the new campaign ledger");

            await WorldEventLedger.LoadFromStoreAsync(CancellationToken.None).ConfigureAwait(false);
            IReadOnlyList<WorldEventRecord> records = WorldEventLedger.SnapshotAll();
            Check(records.Count == 1 && records[0].EventKey == "new-store-key", "new store must be the only load source after replacement");
            Check(!records.Any(value => value.EventKey == "old-store-key"), "old store data must not cross the store boundary");
            await newStore.BeginFinalDrainAsync().ConfigureAwait(false);
        }
        finally
        {
            barrier.Release();
            await oldLoad.ConfigureAwait(false);
            await oldStore.BeginFinalDrainAsync().ConfigureAwait(false);
        }
    }

    private static async Task TestReadinessProjectionMonotonicityAsync()
    {
        ProductionSmokeHost host = CreateHost("readiness-monotonicity");
        ProductionSmokeKeyValueStore storage = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        storage.Put(AiTaskConstants.WorldEventsKey, BuildWorldEventsState(
            "awake:event:readiness-old",
            "readiness-old-key",
            100,
            "readiness old event").ToString(Newtonsoft.Json.Formatting.None));
        WorldStateStore store = CreateStore(host, storage);
        await Install(store).ConfigureAwait(false);
        WorldKnowledgeQueryService query = BindKnowledge();
        int generation = WorldEventLedger.CampaignGeneration;

        await WorldEventLedger.LoadFromStoreAsync(CancellationToken.None).ConfigureAwait(false);
        Check(!AwakeRuntime.IsNativeKnowledgeReady(), "native readiness must be false before the readiness probe succeeds");
        Check(query.Search("readiness old event", 10).Count == 0, "events must not project before native readiness");
        WorldEventLedgerSnapshot beforeReady = WorldEventLedger.CaptureSnapshot();
        Check(!WorldEventServices.TryProjectEventsIfReady(generation, store, beforeReady), "direct projection must fail closed before readiness");

        AwakeRuntime.NativeReadinessProbeForTesting = (probeGeneration, cancellationToken) =>
            Task.FromResult(NativeReadinessResult.Ready(probeGeneration, host.CurrentSession.SessionId));
        NativeReadinessResult readiness = await AwakeRuntime.EnsureNativeReadinessAsync(
            host.CurrentSession.SessionId,
            CancellationToken.None).ConfigureAwait(false);
        Check(readiness.Status == NativeReadinessStatus.Ready, "native readiness probe must report ready");
        Check(AwakeRuntime.IsNativeKnowledgeReady(), "ready result must open the projection gate");
        Check(WorldEventServices.TryProjectEventsIfReady(generation, store, beforeReady), "ready current snapshot must project");
        Check(query.Search("readiness old event", 10).Count == 1, "ready projection must expose the loaded event");

        WorldEventLedgerSnapshot stale = WorldEventLedger.CaptureSnapshot();
        WorldEventAppendResult append = await WorldEventLedger.RecordAsync(
            101,
            "war",
            "readiness new event",
            "readiness-new-key",
            CancellationToken.None).ConfigureAwait(false);
        Check(append.Succeeded, "new event must persist after readiness");
        WorldEventLedgerSnapshot current = WorldEventLedger.CaptureSnapshot();
        Check(current.CampaignGeneration == generation, "current snapshot must retain the campaign generation");
        Check(current.LedgerRevision > stale.LedgerRevision, "new event must advance ledger revision");
        Check(current.SnapshotRevision > stale.SnapshotRevision, "new event must advance snapshot revision");
        Check(!WorldEventServices.TryProjectEventsIfReady(generation, store, stale), "an older snapshot must not overwrite a newer projection");
        Check(query.Search("readiness new event", 10).Count == 1, "newer projection must remain visible after stale projection rejection");
        await store.BeginFinalDrainAsync().ConfigureAwait(false);
    }
}
