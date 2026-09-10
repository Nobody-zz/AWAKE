using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Awake.WorldbookRuntimeProductionSmoke;

internal static partial class Program
{
    private static async Task TestLifecycleAndFinalDrainAsync()
    {
        ProductionSmokeHost host = CreateHost("lifecycle");
        ProductionSmokeKeyValueStore worldEvents = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        ProductionSmokeKeyValueStore memories = new ProductionSmokeKeyValueStore(AiTaskConstants.NpcMemoriesNamespace);
        WorldStateStore store = CreateStore(host, worldEvents, memories);
        await Install(store).ConfigureAwait(false);

        Check(store.LifecycleState == WorldStateStoreLifecycle.Active, "new store must be active");
        Check(store.ReserveMemory("hero.lifecycle", "dialogue", 100, out _, out _), "pre-boundary reservation must be accepted");
        _ = AwakeRuntime.BeginSessionEnd();
        Check(AwakeRuntime.SessionEnded, "runtime must be ended");
        Check(store.SessionEnded && store.LifecycleState == WorldStateStoreLifecycle.Ending, "store must enter ending state");
        Check(!store.ReserveMemory("hero.rejected", "dialogue", 100, out _, out _), "new business reservation must be rejected after ending");
        Check(!store.TryEnqueue(CreateCommand("after-end")), "new business command must be rejected after ending");
        _ = AwakeRuntime.BeginSessionEnd();
        await store.BeginFinalDrainAsync().ConfigureAwait(false);
        Check(store.LifecycleState == WorldStateStoreLifecycle.Ended, "final drain must end the store");
        Check(memories.SetCount == 1, "accepted pre-boundary reservation must be drained exactly once");
        await AwakeRuntime.ReleaseWorldStateStore(store).ConfigureAwait(false);
        Check(AwakeRuntime.WorldStateStore == null, "released store must be detached");
    }

    private static async Task TestEnsureWorldStateReplacementAsync()
    {
        ProductionSmokeHost host = CreateHost("replacement");
        ProductionSmokeKeyValueStore oldNamespace = new ProductionSmokeKeyValueStore("old.namespace");
        WorldStateStore oldStore = new WorldStateStore(host);
        oldStore.InjectStoreForTesting("old.namespace", oldNamespace);
        await Install(oldStore).ConfigureAwait(false);
        ProductionSmokeKeyValueStore candidateNamespace = host.StorageAdapter.SetNamespace(AiTaskConstants.WorldEventsNamespace);

        bool ready = await AwakeRuntime.EnsureWorldStateReadyAsync(
            host,
            CancellationToken.None,
            new[] { AiTaskConstants.WorldEventsNamespace }).ConfigureAwait(false);

        WorldStateStore current = AwakeRuntime.WorldStateStore;
        Check(ready, "replacement readiness must succeed");
        Check(current != null && !ReferenceEquals(current, oldStore), "replacement must install a new production store");
        Check(oldStore.LifecycleState == WorldStateStoreLifecycle.Ended, "replacement must wait for old store final drain");
        Check(current != null && current.LifecycleState == WorldStateStoreLifecycle.Active, "new store must remain active");
        Check(current != null && current.HasNamespaces(new[] { AiTaskConstants.WorldEventsNamespace }), "new store must contain requested namespace");
        Check(candidateNamespace == host.StorageAdapter.GetNamespace(AiTaskConstants.WorldEventsNamespace), "candidate namespace must come from host storage");
        Check(!oldStore.TryEnqueue(CreateCommand("old-after-replacement")), "old store must reject new business writes");
        await oldStore.BeginFinalDrainAsync().ConfigureAwait(false);
        Check(oldStore.LifecycleState == WorldStateStoreLifecycle.Ended, "replaced store must be endable");
    }

    private static async Task TestRecordBeforeBoundaryAsync()
    {
        ProductionSmokeHost host = CreateHost("before-boundary");
        ProductionSmokeKeyValueStore storage = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        WorldStateStore store = CreateStore(host, storage);
        await Install(store).ConfigureAwait(false);

        WorldEventAppendResult result = await WorldEventLedger.RecordAsync(
            100,
            "war",
            "accepted before boundary",
            "before-boundary-key",
            CancellationToken.None).ConfigureAwait(false);

        Check(result.Succeeded, "completed pre-boundary event must persist");
        Check(WorldEventLedger.SnapshotAll().Any(value => value.EventKey == "before-boundary-key"), "pre-boundary event must enter current ledger");
        _ = AwakeRuntime.BeginSessionEnd();
        Check(store.LifecycleState == WorldStateStoreLifecycle.Ending, "boundary must invalidate store after accepted write");
        Check(WorldEventLedger.SnapshotAll().Any(value => value.EventKey == "before-boundary-key"), "ending alone must not erase already accepted ledger data");
        AwakeRuntime.ResetSessionStateForCampaign();
        Check(WorldEventLedger.Count == 0, "campaign reset must clear previous ledger generation");
        await store.BeginFinalDrainAsync().ConfigureAwait(false);
    }

    private static async Task TestRecordAfterBoundaryAsync()
    {
        ProductionSmokeHost host = CreateHost("after-boundary");
        ProductionSmokeKeyValueStore storage = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        ProductionSmokeGetBarrier barrier = storage.BlockNextGet(AiTaskConstants.WorldEventsKey);
        WorldStateStore store = CreateStore(host, storage);
        await Install(store).ConfigureAwait(false);
        try
        {
            Task<WorldEventAppendResult> pending = WorldEventLedger.RecordAsync(
                100,
                "war",
                "ends while awaiting",
                "after-boundary-key",
                CancellationToken.None);
            await WaitForAsync(barrier.Started, "append read did not start").ConfigureAwait(false);
            _ = AwakeRuntime.BeginSessionEnd();
            barrier.Release();
            WorldEventAppendResult result = await WithTimeoutAsync(pending, "append did not settle").ConfigureAwait(false);

            Check(StringComparer.Ordinal.Equals(result.Code, "awake.world_event.stale_session"), "await-after-boundary must return stale session");
            Check(WorldEventLedger.Count == 0, "stale event must not enter the new ledger");
        }
        finally
        {
            barrier.Release();
            await store.BeginFinalDrainAsync().ConfigureAwait(false);
            await AwakeRuntime.ReleaseWorldStateStore(store).ConfigureAwait(false);
        }
    }

    private static async Task TestQueuedRecordGenerationAsync()
    {
        ProductionSmokeHost host = CreateHost("queued-generation");
        ProductionSmokeKeyValueStore oldStorage = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        ProductionSmokeGetBarrier barrier = oldStorage.BlockNextGet(AiTaskConstants.WorldEventsKey);
        WorldStateStore oldStore = CreateStore(host, oldStorage);
        await Install(oldStore).ConfigureAwait(false);
        try
        {
            Check(WorldEventLedger.QueueRecord(100, "war", "queued old generation", "reused-business-key"), "queue record must accept valid input");
            await WaitForAsync(barrier.Started, "queued record did not reach production storage").ConfigureAwait(false);
            AwakeRuntime.ResetSessionStateForCampaign();

            ProductionSmokeKeyValueStore newStorage = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
            WorldStateStore newStore = CreateStore(host, newStorage);
            Task installNewStore = Install(newStore);
            await Task.Delay(50).ConfigureAwait(false);
            Check(!installNewStore.IsCompleted, "new generation install must wait for the old drain barrier");
            barrier.Release();
            await WaitForAsync(installNewStore, "new generation store install did not settle").ConfigureAwait(false);
            await WaitUntilAsync(() => oldStorage.GetCount > 0 && HasCapturedLog("stale_session"), "queued old generation did not settle", 4000).ConfigureAwait(false);

            Check(WorldEventLedger.Count == 0, "queued old generation must not enter new ledger");
            WorldEventAppendResult newResult = await WorldEventLedger.RecordAsync(
                101,
                "war",
                "new generation reuses business key",
                "reused-business-key",
                CancellationToken.None).ConfigureAwait(false);
            Check(newResult.Succeeded, "new generation must accept an old generation business key");
            Check(WorldEventLedger.SnapshotAll().Any(value => value.EventKey == "reused-business-key"), "new generation event must be visible");
        }
        finally
        {
            barrier.Release();
            await oldStore.BeginFinalDrainAsync().ConfigureAwait(false);
            await AwakeRuntime.ReleaseWorldStateStore(oldStore).ConfigureAwait(false);
        }
    }}
