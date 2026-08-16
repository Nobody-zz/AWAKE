using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Awake;

internal static class NpcProactiveHooks
{
    internal static Func<int, List<Hero>> GetNearbyHeroes;
    internal static Func<string, Hero> FindHeroById;
    internal static Func<bool> IsDialogueOpen;
    internal static Func<bool> IsMessengerOpen;
    internal static Action<string, string> RecordDialogueContext;
    internal static Action<string, string> EnqueueDialogue;
}

internal sealed class NpcProactiveService
{
    private static NpcProactiveService _current;
    private readonly object _gate = new object();
    private readonly List<NpcProactiveCandidate> _candidates = new List<NpcProactiveCandidate>();
    private readonly Random _random = new Random();
    private bool _loaded;
    private bool _disposed;

    internal static NpcProactiveService Current
    {
        get { lock (typeof(NpcProactiveService)) return _current; }
    }

    internal static void SetCurrent(NpcProactiveService service)
    {
        lock (typeof(NpcProactiveService))
        {
            _current = service;
        }
    }

    internal static void ShutdownCurrent()
    {
        NpcProactiveService service;
        lock (typeof(NpcProactiveService))
        {
            service = _current;
            _current = null;
        }
        if (service != null)
        {
            try
            {
                service.Dispose();
            }
            catch (Exception ex)
            {
                AwakeLog.Write("npc_proactive_shutdown_error error=" + ex.Message);
            }
        }
    }

    internal static void ClearForTesting()
    {
        NpcProactiveService service = Current;
        if (service == null) return;
        lock (service._gate)
        {
            service._candidates.Clear();
            service._loaded = false;
        }
    }

    internal async Task OnHourlyTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_disposed || AwakeRuntime.SessionEnded) return;
            if (!AwakeSettings.Current.EnableNpcProactive) return;
            if ((NpcProactiveHooks.IsDialogueOpen?.Invoke() ?? false)
                || (NpcProactiveHooks.IsMessengerOpen?.Invoke() ?? false))
            {
                return;
            }
            if (InformationManager.IsAnyInquiryActive()) return;

            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            int day = AwakeRuntime.CurrentGameDay();
            bool changed = CleanupExpired(day);
            if (!HasPending())
            {
                changed |= await EvaluateAsync(day, cancellationToken).ConfigureAwait(false);
            }
            if (changed)
            {
                await SaveAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_proactive_hourly_error error=" + ex.Message);
        }
    }

    internal void OnApplicationTick()
    {
        try
        {
            if (_disposed || AwakeRuntime.SessionEnded) return;
            if (!AwakeSettings.Current.EnableNpcProactive) return;
            if ((NpcProactiveHooks.IsDialogueOpen?.Invoke() ?? false)
                || (NpcProactiveHooks.IsMessengerOpen?.Invoke() ?? false))
            {
                return;
            }
            if (InformationManager.IsAnyInquiryActive()) return;

            NpcProactiveCandidate selected = null;
            lock (_gate)
            {
                foreach (NpcProactiveCandidate candidate in _candidates)
                {
                    if (candidate.State == NpcProactiveState.Pending)
                    {
                        candidate.State = NpcProactiveState.Opening;
                        selected = candidate;
                        break;
                    }
                }
            }
            if (selected == null) return;

            Hero hero = NpcProactiveHooks.FindHeroById?.Invoke(selected.HeroId);
            if (hero == null)
            {
                MarkExpired(selected.HeroId);
                _ = SaveAsync(CancellationToken.None);
                return;
            }

            _ = SaveAsync(CancellationToken.None);
            ShowInquiry(selected, hero);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_proactive_tick_error error=" + ex.Message);
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded) return;
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null)
        {
            _loaded = true;
            return;
        }
        JObject doc = await store.GetProactiveAsync(null, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _candidates.Clear();
            if (doc?["candidates"] is JArray candidates)
            {
                foreach (JToken token in candidates)
                {
                    NpcProactiveCandidate candidate = NpcProactiveCandidate.FromJson(token);
                    if (!string.IsNullOrWhiteSpace(candidate.HeroId))
                    {
                        _candidates.Add(candidate);
                    }
                }
            }
            _loaded = true;
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        JArray candidates = new JArray();
        lock (_gate)
        {
            foreach (NpcProactiveCandidate candidate in _candidates)
            {
                candidates.Add(candidate.ToJson());
            }
        }
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return;
        await store.UpdateProactiveAsync(
            candidates,
            "proactive|" + Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);
    }

    private bool CleanupExpired(int day)
    {
        bool changed = false;
        lock (_gate)
        {
            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                NpcProactiveCandidate candidate = _candidates[i];
                bool remove = candidate.State == NpcProactiveState.Expired
                    || candidate.State == NpcProactiveState.None;
                if (!remove
                    && (candidate.State == NpcProactiveState.Pending
                        || candidate.State == NpcProactiveState.Opening)
                    && day > candidate.ExpiresAtDay)
                {
                    remove = true;
                }
                if (!remove
                    && (candidate.State == NpcProactiveState.Accepted
                        || candidate.State == NpcProactiveState.Rejected)
                    && day >= candidate.CooldownDay)
                {
                    remove = true;
                }
                if (remove)
                {
                    _candidates.RemoveAt(i);
                    changed = true;
                }
            }
        }
        return changed;
    }

    private bool HasPending()
    {
        lock (_gate)
        {
            foreach (NpcProactiveCandidate candidate in _candidates)
            {
                if (candidate.State == NpcProactiveState.Pending) return true;
            }
        }
        return false;
    }

    private bool HasCandidateForHero(string heroId)
    {
        lock (_gate)
        {
            foreach (NpcProactiveCandidate candidate in _candidates)
            {
                if (StringComparer.Ordinal.Equals(candidate.HeroId, heroId)) return true;
            }
        }
        return false;
    }

    private async Task<bool> EvaluateAsync(int day, CancellationToken cancellationToken)
    {
        List<Hero> heroes = NpcProactiveHooks.GetNearbyHeroes?.Invoke(NpcProactiveConstants.EvaluationLimit)
            ?? new List<Hero>();
        if (heroes.Count == 0) return false;

        for (int i = heroes.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            Hero swap = heroes[i];
            heroes[i] = heroes[j];
            heroes[j] = swap;
        }

        WorldStateStore store = AwakeRuntime.WorldStateStore;
        foreach (Hero hero in heroes)
        {
            if (hero == null || string.IsNullOrWhiteSpace(hero.StringId)) continue;
            if (HasCandidateForHero(hero.StringId)) continue;

            NpcProactiveCandidate existing = FindCandidate(hero.StringId);
            if (existing != null)
            {
                if (day < existing.CooldownDay || existing.Fatigue >= NpcProactiveConstants.MaximumFatigue) continue;
            }

            int affinity = 0;
            if (store != null)
            {
                JObject relationship = await store.GetRelationshipAsync(hero.StringId, null, cancellationToken).ConfigureAwait(false);
                if (relationship != null)
                {
                    affinity = Clamp(
                        IntValue(relationship["trust"]) + IntValue(relationship["love"]) - IntValue(relationship["hostility"]),
                        -100,
                        100);
                }
            }

            NpcProactiveMotive motive = Math.Abs(affinity) >= 20
                ? NpcProactiveMotive.Relationship
                : NpcProactiveMotive.Casual;
            int chancePercent = Clamp(AwakeSettings.Current.NpcProactiveChance, 0, 100);
            double chance = (NpcProactiveConstants.BaseChance
                + affinity * NpcProactiveConstants.RelationshipBonusPerPoint)
                * (chancePercent / 35.0);
            chance = Math.Min(NpcProactiveConstants.ChanceMaximum, chance);
            if (_random.NextDouble() >= chance) continue;

            lock (_gate)
            {
                if (HasCandidateForHeroUnlocked(hero.StringId)) continue;
                _candidates.Add(new NpcProactiveCandidate
                {
                    HeroId = hero.StringId,
                    Motive = motive,
                    Urgency = Math.Abs(affinity) >= 50 ? 2 : 1,
                    Affinity = affinity,
                    State = NpcProactiveState.Pending,
                    Day = day,
                    ExpiresAtDay = day + NpcProactiveConstants.ExpiresAfterDays,
                    CooldownDay = day + NpcProactiveConstants.CooldownDays,
                    Fatigue = 1,
                    OpeningHint = AwakeLocalization.Resolve(
                        "awake.proactive.opening_hint",
                        "对方主动想和你谈谈。")
                });
            }
            AwakeLog.Write("npc_proactive_candidate_created hero=" + hero.StringId + " motive=" + motive);
            return true;
        }
        return false;
    }

    private NpcProactiveCandidate FindCandidate(string heroId)
    {
        lock (_gate)
        {
            foreach (NpcProactiveCandidate candidate in _candidates)
            {
                if (StringComparer.Ordinal.Equals(candidate.HeroId, heroId)) return candidate;
            }
        }
        return null;
    }

    private bool HasCandidateForHeroUnlocked(string heroId)
    {
        foreach (NpcProactiveCandidate candidate in _candidates)
        {
            if (StringComparer.Ordinal.Equals(candidate.HeroId, heroId)) return true;
        }
        return false;
    }

    private void MarkExpired(string heroId)
    {
        lock (_gate)
        {
            NpcProactiveCandidate candidate = FindCandidateUnlocked(heroId);
            if (candidate != null) candidate.State = NpcProactiveState.Expired;
        }
    }

    private void ShowInquiry(NpcProactiveCandidate candidate, Hero hero)
    {
        string name = hero?.Name?.ToString() ?? candidate.HeroId;
        string title = AwakeLocalization.Resolve("awake.proactive.popup.title", "某人的呼唤");
        string text = AwakeLocalization.Resolve(
            "awake.proactive.popup.text",
            "{NAME} 想和你谈谈。",
            new Dictionary<string, string> { ["NAME"] = name });
        string accept = AwakeLocalization.Resolve("awake.proactive.accept", "谈谈");
        string decline = AwakeLocalization.Resolve("awake.proactive.decline", "改天");
        InformationManager.ShowInquiry(
            new InquiryData(
                title,
                text,
                true,
                true,
                accept,
                decline,
                () => OnAccept(candidate.HeroId),
                () => OnDecline(candidate.HeroId),
                string.Empty,
                0f,
                null,
                null,
                null),
            true,
            false);
    }

    private void OnAccept(string heroId)
    {
        int day = AwakeRuntime.CurrentGameDay();
        lock (_gate)
        {
            NpcProactiveCandidate candidate = FindCandidateUnlocked(heroId);
            if (candidate == null) return;
            candidate.State = NpcProactiveState.Accepted;
            candidate.CooldownDay = day + NpcProactiveConstants.CooldownDays;
            candidate.Fatigue = Math.Min(NpcProactiveConstants.MaximumFatigue, candidate.Fatigue + 1);
        }
        NpcProactiveCandidate snapshot = FindCandidate(heroId);
        string hint = snapshot?.OpeningHint ?? string.Empty;
        NpcProactiveHooks.RecordDialogueContext?.Invoke(heroId, hint);
        NpcProactiveHooks.EnqueueDialogue?.Invoke(heroId, hint);
        AwakeFeedback.ShowSuccess(AwakeLocalization.Resolve(
            "awake.feedback.proactive_accepted",
            "对方愿意谈谈。"));
        _ = SaveAsync(CancellationToken.None);
        AwakeLog.Write("npc_proactive_accepted hero=" + heroId);
    }

    private void OnDecline(string heroId)
    {
        int day = AwakeRuntime.CurrentGameDay();
        lock (_gate)
        {
            NpcProactiveCandidate candidate = FindCandidateUnlocked(heroId);
            if (candidate == null) return;
            candidate.State = NpcProactiveState.Rejected;
            candidate.CooldownDay = day + NpcProactiveConstants.RejectCooldownDays;
            candidate.Fatigue = Math.Min(NpcProactiveConstants.MaximumFatigue, candidate.Fatigue + 1);
        }
        AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
            "awake.feedback.proactive_declined",
            "你决定改天再说。"));
        _ = SaveAsync(CancellationToken.None);
        AwakeLog.Write("npc_proactive_declined hero=" + heroId);
    }

    private NpcProactiveCandidate FindCandidateUnlocked(string heroId)
    {
        foreach (NpcProactiveCandidate candidate in _candidates)
        {
            if (StringComparer.Ordinal.Equals(candidate.HeroId, heroId)) return candidate;
        }
        return null;
    }

    private void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _candidates.Clear();
        }
        AwakeLog.Write("npc_proactive_service_disposed");
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }

    private static int Clamp(int value, int minimum, int maximum)
    {
        if (value < minimum) return minimum;
        if (value > maximum) return maximum;
        return value;
    }
}
