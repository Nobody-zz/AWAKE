using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal enum AwakeGoldRecoveryDecision
{
    Debit,
    CompleteWithoutDebit,
    Compensate
}

internal enum AwakeGoldSettlementPhase
{
    IndexPersisting,
    PendingPersisting,
    ReadyToDebit,
    CompletePersisting,
    CompensatedPersisting,
    Finished
}

internal sealed class AwakeGoldSettlement
{
    internal string InteractionId { get; set; }
    internal string CanonicalContactKey { get; set; }
    internal string TargetHeroId { get; set; }
    internal string SessionId { get; set; }
    internal string SnapshotToken { get; set; }
    internal string CorrelationId { get; set; }
    internal int Generation { get; set; }
    internal int Amount { get; set; }
    internal int ExpectedBalanceBefore { get; set; }
    internal int ExpectedBalanceAfter { get; set; }
    internal int Day { get; set; }
    internal bool DebitObserved { get; set; }
    internal AwakeGoldSettlementPhase Phase { get; set; }
    internal Task<WorldDrainSummary> PersistTask { get; set; }
}

internal static class AwakeGoldSettlementService
{
    private static readonly object Gate = new object();
    private static readonly Dictionary<string, AwakeGoldSettlement> Pending =
        new Dictionary<string, AwakeGoldSettlement>(StringComparer.Ordinal);
    private static Task<List<AwakeGoldSettlement>> _recoveryTask;
    private static bool _recoveryComplete;

    internal static bool IsReady
    {
        get { lock (Gate) return _recoveryComplete; }
    }

    internal static void ResetForCampaign()
    {
        lock (Gate)
        {
            Pending.Clear();
            _recoveryTask = null;
            _recoveryComplete = false;
        }
    }

    internal static AwakeGoldRecoveryDecision DecideRecovery(
        int currentBalance,
        int expectedBalanceBefore,
        int expectedBalanceAfter)
    {
        if (currentBalance == expectedBalanceBefore) return AwakeGoldRecoveryDecision.Debit;
        if (currentBalance == expectedBalanceAfter) return AwakeGoldRecoveryDecision.CompleteWithoutDebit;
        return AwakeGoldRecoveryDecision.Compensate;
    }

    internal static bool TryQueue(
        WorldStateStore store,
        JObject args,
        string interactionId,
        string canonicalContactKey,
        string snapshotToken,
        string correlationId,
        int balanceBefore)
    {
        if (store == null || args == null || string.IsNullOrWhiteSpace(interactionId)
            || string.IsNullOrWhiteSpace(canonicalContactKey))
        {
            return false;
        }
        int amount = (int)args["amount"];
        IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
        AwakeGoldSettlement settlement = new AwakeGoldSettlement
        {
            InteractionId = interactionId,
            CanonicalContactKey = canonicalContactKey,
            TargetHeroId = (string)args["targetHeroId"] ?? string.Empty,
            SessionId = host?.CurrentSession?.SessionId ?? string.Empty,
            SnapshotToken = snapshotToken ?? string.Empty,
            CorrelationId = correlationId ?? string.Empty,
            Generation = AwakeRuntime.SessionGeneration,
            Amount = amount,
            ExpectedBalanceBefore = balanceBefore,
            ExpectedBalanceAfter = balanceBefore - amount,
            Day = AwakeRuntime.CurrentGameDay(),
            Phase = AwakeGoldSettlementPhase.IndexPersisting
        };
        lock (Gate)
        {
            if (Pending.ContainsKey(interactionId)) return true;
            Pending[interactionId] = settlement;
        }
        settlement.PersistTask = PersistIndexAsync(store, settlement, true);
        return true;
    }

    internal static void Tick()
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null || AwakeRuntime.SessionEnded) return;
        StartOrFinishRecovery(store);
        List<AwakeGoldSettlement> snapshot;
        lock (Gate) snapshot = new List<AwakeGoldSettlement>(Pending.Values);
        foreach (AwakeGoldSettlement settlement in snapshot)
        {
            ProcessOne(store, settlement);
        }
    }

    private static void StartOrFinishRecovery(WorldStateStore store)
    {
        Task<List<AwakeGoldSettlement>> task;
        lock (Gate)
        {
            if (_recoveryComplete) return;
            if (_recoveryTask == null) _recoveryTask = RecoverAsync(store);
            task = _recoveryTask;
        }
        if (!task.IsCompleted) return;
        try
        {
            List<AwakeGoldSettlement> recovered = task.GetAwaiter().GetResult();
            lock (Gate)
            {
                foreach (AwakeGoldSettlement settlement in recovered)
                {
                    if (!Pending.ContainsKey(settlement.InteractionId)) Pending[settlement.InteractionId] = settlement;
                }
                _recoveryComplete = true;
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_gold_recovery_failed error=" + ex.Message);
            lock (Gate)
            {
                _recoveryTask = null;
            }
        }
    }

    private static void ProcessOne(WorldStateStore store, AwakeGoldSettlement settlement)
    {
        if (settlement == null || settlement.Phase == AwakeGoldSettlementPhase.Finished) return;
        if (settlement.Phase == AwakeGoldSettlementPhase.IndexPersisting)
        {
            if (!TryConsumePersistResult(settlement, out bool succeeded)) return;
            if (!succeeded)
            {
                Finish(settlement, "index_persist_failed");
                return;
            }
            settlement.Phase = AwakeGoldSettlementPhase.PendingPersisting;
            settlement.PersistTask = PersistPhaseAsync(store, settlement, "give_gold_pending", AiTaskConstants.GiveGoldPendingCommandId, string.Empty);
            return;
        }
        if (settlement.Phase == AwakeGoldSettlementPhase.PendingPersisting)
        {
            if (!TryConsumePersistResult(settlement, out bool succeeded)) return;
            if (!succeeded)
            {
                settlement.PersistTask = PersistIndexAsync(store, settlement, false);
                settlement.Phase = AwakeGoldSettlementPhase.CompensatedPersisting;
                return;
            }
            settlement.Phase = AwakeGoldSettlementPhase.ReadyToDebit;
        }
        if (settlement.Phase == AwakeGoldSettlementPhase.ReadyToDebit)
        {
            SettleOnMainThread(store, settlement);
            return;
        }
        if (settlement.Phase == AwakeGoldSettlementPhase.CompletePersisting)
        {
            if (!TryConsumePersistResult(settlement, out bool succeeded)) return;
            if (succeeded)
            {
                Finish(settlement, "complete");
                return;
            }
            if (settlement.DebitObserved && Hero.MainHero != null)
            {
                Hero.MainHero.Gold += settlement.Amount;
                settlement.DebitObserved = false;
            }
            settlement.Phase = AwakeGoldSettlementPhase.CompensatedPersisting;
            settlement.PersistTask = PersistCompensationAndIndexAsync(store, settlement, "complete_persist_failed");
            return;
        }
        if (settlement.Phase == AwakeGoldSettlementPhase.CompensatedPersisting)
        {
            if (!TryConsumePersistResult(settlement, out bool _)) return;
            Finish(settlement, "compensated");
        }
    }

    private static void SettleOnMainThread(WorldStateStore store, AwakeGoldSettlement settlement)
    {
        if (Hero.MainHero == null || settlement.Generation != AwakeRuntime.SessionGeneration)
        {
            settlement.Phase = AwakeGoldSettlementPhase.CompensatedPersisting;
            settlement.PersistTask = PersistCompensationAndIndexAsync(store, settlement, "session_or_player_unavailable");
            return;
        }
        AwakeGoldRecoveryDecision decision = DecideRecovery(
            Hero.MainHero.Gold,
            settlement.ExpectedBalanceBefore,
            settlement.ExpectedBalanceAfter);
        if (decision == AwakeGoldRecoveryDecision.Compensate)
        {
            settlement.Phase = AwakeGoldSettlementPhase.CompensatedPersisting;
            settlement.PersistTask = PersistCompensationAndIndexAsync(store, settlement, "balance_snapshot_mismatch");
            return;
        }
        if (decision == AwakeGoldRecoveryDecision.Debit)
        {
            if (Hero.MainHero.Gold < settlement.Amount)
            {
                settlement.Phase = AwakeGoldSettlementPhase.CompensatedPersisting;
                settlement.PersistTask = PersistCompensationAndIndexAsync(store, settlement, "insufficient_gold");
                return;
            }
            Hero.MainHero.Gold -= settlement.Amount;
        }
        settlement.DebitObserved = true;
        settlement.Phase = AwakeGoldSettlementPhase.CompletePersisting;
        settlement.PersistTask = PersistCompleteAndIndexAsync(store, settlement);
    }

    private static bool TryConsumePersistResult(AwakeGoldSettlement settlement, out bool succeeded)
    {
        succeeded = false;
        Task<WorldDrainSummary> task = settlement.PersistTask;
        if (task == null || !task.IsCompleted) return false;
        try
        {
            WorldDrainSummary summary = task.GetAwaiter().GetResult();
            succeeded = summary != null && summary.OwnerCommandObserved && summary.HardFailureCount == 0;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_gold_persist_error id=" + settlement.InteractionId + " error=" + ex.Message);
        }
        settlement.PersistTask = null;
        return true;
    }

    private static async Task<WorldDrainSummary> PersistIndexAsync(
        WorldStateStore store,
        AwakeGoldSettlement settlement,
        bool pending)
    {
        string key = settlement.InteractionId + (pending ? "|index|pending" : "|index|done");
        JObject arguments = new JObject
        {
            ["canonicalContactKey"] = settlement.CanonicalContactKey,
            ["interactionId"] = settlement.InteractionId,
            ["pending"] = pending,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O")
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.InteractionsNamespace,
            AiTaskConstants.InteractionsRecoveryIndexKey,
            AiTaskConstants.InteractionsIndexUpdateCommandId,
            key,
            string.Empty,
            WorldStateKind.InteractionIndex,
            arguments,
            DateTimeOffset.UtcNow,
            settlement.CorrelationId);
        if (!store.TryEnqueue(command)) return new WorldDrainSummary { HardFailureCount = 1 };
        return await store.DrainAsync(command.CommandId, command.IdempotencyKey, CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<WorldDrainSummary> PersistPhaseAsync(
        WorldStateStore store,
        AwakeGoldSettlement settlement,
        string mode,
        string commandId,
        string reason)
    {
        JObject arguments = new JObject
        {
            ["mode"] = mode,
            ["interactionId"] = settlement.InteractionId,
            ["amount"] = settlement.Amount,
            ["targetHeroId"] = settlement.TargetHeroId,
            ["expectedBalanceBefore"] = settlement.ExpectedBalanceBefore,
            ["expectedBalanceAfter"] = settlement.ExpectedBalanceAfter,
            ["sessionId"] = settlement.SessionId,
            ["generation"] = settlement.Generation,
            ["snapshotToken"] = settlement.SnapshotToken,
            ["day"] = settlement.Day,
            ["reason"] = reason ?? string.Empty
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.InteractionsNamespace,
            WorldStateStore.BuildInteractionKey(settlement.CanonicalContactKey),
            commandId,
            settlement.InteractionId + "|" + mode,
            settlement.CanonicalContactKey,
            WorldStateKind.Interaction,
            arguments,
            DateTimeOffset.UtcNow,
            settlement.CorrelationId);
        if (!store.TryEnqueue(command)) return new WorldDrainSummary { HardFailureCount = 1 };
        return await store.DrainAsync(command.CommandId, command.IdempotencyKey, CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<WorldDrainSummary> PersistCompleteAndIndexAsync(
        WorldStateStore store,
        AwakeGoldSettlement settlement)
    {
        WorldDrainSummary phase = await PersistPhaseAsync(
            store,
            settlement,
            "give_gold_complete",
            AiTaskConstants.GiveGoldCompleteCommandId,
            string.Empty).ConfigureAwait(false);
        if (!phase.OwnerCommandObserved || phase.HardFailureCount > 0) return phase;
        return await PersistIndexAsync(store, settlement, false).ConfigureAwait(false);
    }

    private static async Task<WorldDrainSummary> PersistCompensationAndIndexAsync(
        WorldStateStore store,
        AwakeGoldSettlement settlement,
        string reason)
    {
        WorldDrainSummary phase = await PersistPhaseAsync(
            store,
            settlement,
            "give_gold_compensated",
            AiTaskConstants.GiveGoldCompensatedCommandId,
            reason).ConfigureAwait(false);
        if (!phase.OwnerCommandObserved || phase.HardFailureCount > 0) return phase;
        return await PersistIndexAsync(store, settlement, false).ConfigureAwait(false);
    }

    private static async Task<List<AwakeGoldSettlement>> RecoverAsync(WorldStateStore store)
    {
        List<AwakeGoldSettlement> recovered = new List<AwakeGoldSettlement>();
        JObject index = await store.GetInteractionRecoveryIndexAsync(null, CancellationToken.None).ConfigureAwait(false);
        if (!(index?["entries"] is JArray entries)) return recovered;
        foreach (JToken token in entries)
        {
            if (!(token is JObject entry)) continue;
            string contactKey = (string)entry["canonicalContactKey"] ?? string.Empty;
            string interactionId = (string)entry["interactionId"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(contactKey) || string.IsNullOrWhiteSpace(interactionId)) continue;
            JObject ledger = await store.GetInteractionsAsync(contactKey, null, CancellationToken.None).ConfigureAwait(false);
            if (!(ledger?["interactions"] is JArray interactions)) continue;
            foreach (JToken interactionToken in interactions)
            {
                if (!(interactionToken is JObject interaction)
                    || !StringComparer.Ordinal.Equals((string)interaction["interactionId"], interactionId)
                    || !StringComparer.Ordinal.Equals((string)interaction["phase"], "pending"))
                {
                    continue;
                }
                recovered.Add(new AwakeGoldSettlement
                {
                    InteractionId = interactionId,
                    CanonicalContactKey = contactKey,
                    TargetHeroId = (string)interaction["targetHeroId"] ?? string.Empty,
                    SessionId = (string)interaction["sessionId"] ?? string.Empty,
                    SnapshotToken = (string)interaction["snapshotToken"] ?? string.Empty,
                    CorrelationId = (string)interaction["correlation"] ?? string.Empty,
                    Generation = AwakeRuntime.SessionGeneration,
                    Amount = (int?)interaction["amount"] ?? 0,
                    ExpectedBalanceBefore = (int?)interaction["expectedBalanceBefore"] ?? 0,
                    ExpectedBalanceAfter = (int?)interaction["expectedBalanceAfter"] ?? 0,
                    Day = (int?)interaction["day"] ?? 0,
                    Phase = AwakeGoldSettlementPhase.ReadyToDebit
                });
                break;
            }
        }
        return recovered;
    }

    private static void Finish(AwakeGoldSettlement settlement, string result)
    {
        settlement.Phase = AwakeGoldSettlementPhase.Finished;
        lock (Gate) Pending.Remove(settlement.InteractionId);
        AwakeLog.Write("awake_gold_settlement_finished id=" + settlement.InteractionId + " result=" + result);
    }
}