using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Awake;

internal sealed class AwakeEventEngine
{
    private readonly object _gate = new object();
    private readonly List<AwakeEventRule> _rules = new List<AwakeEventRule>();
    private readonly Dictionary<string, AwakeEventRule> _rulesById = new Dictionary<string, AwakeEventRule>(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _cooldowns = new Dictionary<string, double>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _dailyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _dailyDays = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _metaVersions = new Dictionary<string, int>(StringComparer.Ordinal);
    private bool _metaLoaded;
    private bool _busy;

    internal int RuleCount
    {
        get { lock (_gate) return _rules.Count; }
    }

    internal bool Register(AwakeEventRule rule)
    {
        if (rule == null) return false;
        string error;
        if (!AwakeEventValidation.Validate(rule.Definition, out error))
        {
            AwakeLog.Write("awake_event_register_invalid id=" + rule.Definition.Id + " error=" + error);
            return false;
        }
        lock (_gate)
        {
            for (int i = 0; i < _rules.Count; i++)
            {
                if (StringComparer.Ordinal.Equals(_rules[i].Definition.Id, rule.Definition.Id))
                {
                    _rules[i] = rule;
                    _rulesById[rule.Definition.Id] = rule;
                    return true;
                }
            }
            _rules.Add(rule);
            _rulesById[rule.Definition.Id] = rule;
            return true;
        }
    }

    internal void ResetForTesting()
    {
        lock (_gate)
        {
            _rules.Clear();
            _rulesById.Clear();
            _cooldowns.Clear();
            _dailyCounts.Clear();
            _dailyDays.Clear();
            _metaVersions.Clear();
            _metaLoaded = false;
        }
        _busy = false;
        EventDialogueQueue.ClearForTesting();
    }

    internal async Task OnHourlyTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            await OnHourlyTickCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_tick_error error=" + ex.Message);
        }
    }

    private async Task OnHourlyTickCoreAsync(CancellationToken cancellationToken)
    {
        if (_busy) return;
        List<AwakeEventRule> snapshot;
        lock (_gate)
        {
            if (_rules.Count == 0) return;
            snapshot = new List<AwakeEventRule>(_rules);
        }

        _busy = true;
        bool release = true;
        try
        {
            double nowHour = CurrentGameHour();
            int day = CurrentGameDay();
            await EnsureEventMetaLoadedAsync(cancellationToken).ConfigureAwait(false);

            List<AwakeEventRule> eligible = new List<AwakeEventRule>();
            foreach (AwakeEventRule rule in snapshot)
            {
                if (!CanTriggerSync(rule, nowHour, day)) continue;
                if (!ConditionMet(rule.Condition)) continue;
                eligible.Add(rule);
            }

            AwakeEventRule selected = AwakeEventEngineCore.SelectWeighted(eligible, new Random());
            if (selected == null) return;
            await RecordTriggerAsync(selected, nowHour, day, cancellationToken).ConfigureAwait(false);
            bool shown = ShowRule(selected);
            release = !shown;
        }
        catch (OperationCanceledException)
        {
            _busy = false;
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_tick_core_error error=" + ex.Message);
            _busy = false;
        }
        finally
        {
            if (release) _busy = false;
        }
    }

    private async Task RunChainAsync(AwakeEventRule rule, double nowHour)
    {
        bool release = true;
        try
        {
            int day = CurrentGameDay();
            await EnsureEventMetaLoadedAsync(CancellationToken.None).ConfigureAwait(false);
            if (!CanTriggerSync(rule, nowHour, day))
            {
                AwakeLog.Write("awake_event_chain_blocked id=" + rule.Definition.Id);
                return;
            }
            await RecordTriggerAsync(rule, nowHour, day, CancellationToken.None).ConfigureAwait(false);
            bool shown = ShowRule(rule);
            release = !shown;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_chain_error id=" + rule.Definition.Id + " error=" + ex.Message);
            _busy = false;
        }
        finally
        {
            if (release) _busy = false;
        }
    }

    private async Task EnsureEventMetaLoadedAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_metaLoaded) return;
        }

        IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
        if (host == null)
        {
            lock (_gate) _metaLoaded = true;
            return;
        }
        try
        {
            await AwakeRuntime.EnsureWorldStateReadyAsync(host, cancellationToken).ConfigureAwait(false);
            WorldStateStore store = AwakeRuntime.WorldStateStore;
            if (store == null) return;
            RequestContext context = AwakeRuntime.CreateContext(host, Guid.NewGuid().ToString("N"));
            JObject doc = await store.GetEventMetaAsync(context, cancellationToken).ConfigureAwait(false);
            if (doc == null)
            {
                lock (_gate) _metaLoaded = true;
                return;
            }

            double nowHour = CurrentGameHour();
            lock (_gate)
            {
                JObject versions = doc["versions"] as JObject;
                if (versions != null)
                {
                    foreach (JProperty property in versions.Properties())
                    {
                        _metaVersions[property.Name] = IntValue(property.Value);
                    }
                }
                JObject cooldowns = doc["cooldowns"] as JObject;
                if (cooldowns != null)
                {
                    foreach (JProperty property in cooldowns.Properties())
                    {
                        double last = DoubleValue(property.Value);
                        if (last > nowHour) continue;
                        _cooldowns[property.Name] = last;
                    }
                }
                JObject daily = doc["daily"] as JObject;
                if (daily != null)
                {
                    foreach (JProperty property in daily.Properties())
                    {
                        JObject entry = property.Value as JObject;
                        if (entry == null) continue;
                        _dailyDays[property.Name] = IntValue(entry["day"]);
                        _dailyCounts[property.Name] = IntValue(entry["count"]);
                    }
                }
                _metaLoaded = true;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_meta_load_error error=" + ex.Message);
        }
    }

    private bool CanTriggerSync(AwakeEventRule rule, double nowHour, int day)
    {
        lock (_gate)
        {
            double last;
            if (_cooldowns.TryGetValue(rule.Definition.Id, out last)
                && !AwakeEventEngineCore.IsCooldownReady(last, nowHour, rule.CooldownHours))
            {
                return false;
            }
            if (rule.MaxPerDay > 0)
            {
                int count;
                int recordedDay;
                if (_dailyDays.TryGetValue(rule.Definition.Id, out recordedDay)
                    && recordedDay == day
                    && _dailyCounts.TryGetValue(rule.Definition.Id, out count)
                    && count >= rule.MaxPerDay)
                {
                    return false;
                }
            }
            return true;
        }
    }

    private async Task RecordTriggerAsync(AwakeEventRule rule, double nowHour, int day, CancellationToken cancellationToken)
    {
        int version;
        int count;
        lock (_gate)
        {
            int current;
            _metaVersions.TryGetValue(rule.Definition.Id, out current);
            version = current + 1;
            _metaVersions[rule.Definition.Id] = version;
            _cooldowns[rule.Definition.Id] = nowHour;
            _dailyDays[rule.Definition.Id] = day;
            int previous;
            _dailyCounts.TryGetValue(rule.Definition.Id, out previous);
            count = previous + 1;
            _dailyCounts[rule.Definition.Id] = count;
        }

        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return;
        try
        {
            await store.UpdateEventMetaAsync(
                rule.Definition.Id,
                version,
                nowHour,
                day,
                count,
                AwakeRuntime.SessionGeneration + "|" + rule.Definition.Id + "|" + version,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_meta_write_error id=" + rule.Definition.Id + " error=" + ex.Message);
        }
    }

    private bool ShowRule(AwakeEventRule rule)
    {
        _busy = true;
        AwakeLog.Write("awake_event_engine_fired id=" + rule.Definition.Id + " hour=" + CurrentGameHour());
        try
        {
            AwakeUiDispatcher.Enqueue(() =>
            {
                try
                {
                    if (!CanShowPopup())
                    {
                        _busy = false;
                        return;
                    }
                    bool shown = AwakeEventPopupService.Show(
                        rule.Definition,
                        (id, choice) => OnChoice(rule, choice),
                        () => _busy = false);
                    if (!shown) _busy = false;
                }
                catch (Exception ex)
                {
                    AwakeLog.Write("awake_event_show_error id=" + rule.Definition.Id + " error=" + ex.Message);
                    _busy = false;
                }
            });
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_show_error id=" + rule.Definition.Id + " error=" + ex.Message);
            _busy = false;
            return false;
        }
    }

    private void OnChoice(AwakeEventRule rule, string choice)
    {
        try
        {
            _busy = false;
            WorldEventLedger.Record(CurrentGameDay(), "event", rule.Definition.Title);
            TryQueueDialogueAction(rule, choice);
            AwakeEventRule next = AwakeEventChainCore.Resolve(_rulesById, rule.Definition.Id, choice);
            if (next == null) return;
            AwakeLog.Write("awake_event_chain_advanced from=" + rule.Definition.Id + " to=" + next.Definition.Id);
            _busy = true;
            _ = RunChainAsync(next, CurrentGameHour());
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_choice_error id=" + rule.Definition.Id + " error=" + ex.Message);
            _busy = false;
        }
    }

    private static void TryQueueDialogueAction(AwakeEventRule rule, string choice)
    {
        try
        {
            AwakeEventDialogueAction action = rule?.Definition?.DialogueAction;
            if (action == null || !StringComparer.Ordinal.Equals(action.Choice, choice)) return;
            string targetId = ResolveDialogueTarget(action.TargetId);
            if (string.IsNullOrWhiteSpace(targetId))
            {
                WorldEventLedger.Record(CurrentGameDay(), "npc_dialogue_open_failed", (action.TargetId ?? "unknown") + ":target_unavailable");
                return;
            }
            EventDialogueQueue.Enqueue(targetId, AwakeRuntime.TruncateTextElements(action.OpeningHint, 240));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_dialogue_action_error error=" + ex.Message);
        }
    }

    private static string ResolveDialogueTarget(string targetId)
    {
        if (StringComparer.Ordinal.Equals(targetId, "@current_settlement_lord"))
        {
            try
            {
                return Settlement.CurrentSettlement?.OwnerClan?.Leader?.StringId;
            }
            catch
            {
                return null;
            }
        }
        return targetId;
    }

    private static bool CanShowPopup()
    {
        try
        {
            if (NpcDialogueOverlay.IsOpen || AwakeMessengerOverlay.IsOpen) return false;
            if (InformationManager.IsAnyInquiryActive()) return false;
            if (Campaign.Current?.ConversationManager != null
                && Campaign.Current.ConversationManager.IsConversationInProgress)
            {
                return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ConditionMet(AwakeEventCondition condition)
    {
        try
        {
            MobileParty mainParty = MobileParty.MainParty;
            switch (condition)
            {
                case AwakeEventCondition.Always:
                    return true;
                case AwakeEventCondition.InSettlement:
                    return Settlement.CurrentSettlement != null;
                case AwakeEventCondition.InArmy:
                    return mainParty?.Army != null;
                case AwakeEventCondition.Camping:
                    return mainParty != null
                        && !mainParty.IsMoving
                        && !mainParty.IsCurrentlyAtSea
                        && Settlement.CurrentSettlement == null;
                case AwakeEventCondition.HasPrisoners:
                    return mainParty?.PrisonRoster != null
                        && (mainParty.PrisonRoster.TotalRegulars > 0 || mainParty.PrisonRoster.TotalHeroes > 0);
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_condition_error condition=" + condition + " error=" + ex.Message);
            return false;
        }
    }

    private static double CurrentGameHour()
    {
        try
        {
            return CampaignTime.Now.ToHours;
        }
        catch
        {
            return 0d;
        }
    }

    private static int CurrentGameDay()
    {
        return AwakeRuntime.CurrentGameDay();
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; }
        catch { return 0; }
    }

    private static double DoubleValue(JToken token)
    {
        if (token == null) return 0d;
        try { return Convert.ToDouble(token, System.Globalization.CultureInfo.InvariantCulture); }
        catch { return 0d; }
    }
}
