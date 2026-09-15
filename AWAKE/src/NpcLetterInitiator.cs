using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace Awake;

/// <summary>
/// NPC 主动来信：把"动机"翻译成一封真的寄给玩家的信。
/// <para>
/// 与 <see cref="NpcProactiveService"/> 的分工——那位处理**附近**的人主动搭话（弹 inquiry 当面谈），
/// 本类处理**远方**的熟人主动寄信。一个走近你，一个写信给你，天然互斥，不争同一条会话。
/// 链路：动机命中 → <see cref="AwakeLetterService.CreateInboundAsync"/>（带信使在途时间）
/// → <see cref="AwakeLetterService.AdvanceAsync"/> 到点转 delivered → 未读计数。
/// </para>
/// <para>
/// 状态一律从**账本**推导（该联系人最近一封入站信的时刻与状态），不新建存储：
/// 上一封还没被读到就不再寄，寄过则受冷却约束。因此同一天重复 tick 天然不会重复产信。
/// </para>
/// </summary>
internal static class NpcLetterInitiator
{
    internal const string IdempotencyPrefix = "npc-letter|";

    /// <summary>同一联系人两次主动来信的最小间隔（游戏小时）。与"主动搭话"共用同一冷却口径。</summary>
    internal const int CooldownHours = NpcProactiveConstants.CooldownDays * AwakeLetterConstants.HoursPerDay;

    private static readonly Random Roll = new Random();

    /// <summary>决策输入快照——纯数据，便于离线构造与断言。</summary>
    internal sealed class NpcLetterCandidate
    {
        internal string ContactKey { get; set; } = string.Empty;
        internal string DisplayName { get; set; } = string.Empty;
        internal int Affinity { get; set; }
        internal bool HasRelationship { get; set; }

        /// <summary>该联系人最近一封入站信的创建小时；从未寄过为 -1。</summary>
        internal int LastInboundHour { get; set; } = -1;

        /// <summary>是否存在"已寄出但玩家尚未读到"的信（sent / delivered）——有则不再催。</summary>
        internal bool HasUnreadInbound { get; set; }
    }

    private sealed class ContactLetterState
    {
        internal int LastInboundHour { get; set; } = -1;
        internal bool HasUnreadInbound { get; set; }
    }

    /// <summary>
    /// 是否该为这个联系人寄一封信。<paramref name="roll"/> 为 [0,1) 的随机数，离线可注入以消除不确定性。
    /// 三条否决线：上一封还没被读到、仍在冷却期内、概率未命中。
    /// </summary>
    internal static bool ShouldInitiate(NpcLetterCandidate candidate, int currentHour, double roll, int chancePercent)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(candidate.ContactKey)) return false;
        if (candidate.HasUnreadInbound) return false;
        if (candidate.LastInboundHour >= 0 && currentHour - candidate.LastInboundHour < CooldownHours) return false;
        double chance = NpcProactiveService.ComputeTriggerChance(candidate.Affinity, candidate.HasRelationship, chancePercent);
        if (chance <= 0d) return false;
        return roll < chance;
    }

    /// <summary>主动来信的幂等键：同一联系人每个游戏日最多一封。</summary>
    internal static string BuildIdempotencyKey(string contactKey, int currentHour)
    {
        int day = currentHour / AwakeLetterConstants.HoursPerDay;
        return IdempotencyPrefix + (contactKey ?? string.Empty) + "|" + day;
    }

    /// <summary>
    /// 确定性正文（离线可复现，不调用任何 Provider）。关系色彩来自动机的 openingHint 与触发理由，
    /// 末行署名。异步真机接入 AI 生成后，此处仍作为兜底文本。
    /// </summary>
    internal static string BuildLetterBody(string displayName, string triggerReason, string openingHint)
    {
        string reason = triggerReason ?? string.Empty;
        string hint = openingHint ?? string.Empty;
        string name = string.IsNullOrWhiteSpace(displayName)
            ? AwakeLocalization.Resolve("awake.ui.contact_hero", "英雄")
            : displayName;
        return AwakeLocalization.Resolve(
            "awake.letter.proactive.body",
            "{REASON}{HINT}\n\n——{NAME}",
            new Dictionary<string, string>
            {
                ["REASON"] = reason,
                ["HINT"] = hint.Length == 0 ? string.Empty : "\n\n" + hint,
                ["NAME"] = name
            });
    }

    /// <summary>每小时调用一次。返回产出的联系人键；空串表示这一小时没有寄信。</summary>
    internal static async Task<string> TryProduceAsync(int currentHour, CancellationToken cancellationToken)
    {
        try
        {
            if (AwakeRuntime.SessionEnded) return string.Empty;
            if (!AwakeSettings.Current.EnableNpcProactive) return string.Empty;
            if (currentHour < 0) return string.Empty;

            IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
            if (host == null) return string.Empty;
            if (!await AwakeRuntime.EnsureWorldStateReadyAsync(host, cancellationToken, new[]
            {
                AiTaskConstants.TranscriptNamespace,
                AiTaskConstants.ContactsNamespace,
                AiTaskConstants.LettersNamespace
            }).ConfigureAwait(false))
            {
                return string.Empty;
            }

            List<AwakeContactInfo> remote = new List<AwakeContactInfo>();
            foreach (AwakeContactInfo contact in AwakeMessengerService.BuildContacts())
            {
                // 附近的走"主动搭话"，只有远方联系人才需要信使。
                if (contact == null || contact.IsNearby) continue;
                if (contact.Target?.Hero == null) continue;
                if (string.IsNullOrWhiteSpace(contact.CanonicalContactKey)) continue;
                remote.Add(contact);
            }
            if (remote.Count == 0) return string.Empty;

            Dictionary<string, ContactLetterState> letterIndex = await BuildLetterIndexAsync(cancellationToken).ConfigureAwait(false);
            Shuffle(remote);
            int budget = remote.Count < NpcProactiveConstants.EvaluationLimit ? remote.Count : NpcProactiveConstants.EvaluationLimit;
            int chancePercent = AwakeSettings.Current.NpcProactiveChance;
            WorldStateStore store = AwakeRuntime.WorldStateStore;

            for (int i = 0; i < budget; i++)
            {
                AwakeContactInfo contact = remote[i];
                NpcLetterCandidate candidate = await BuildCandidateAsync(store, contact, letterIndex, cancellationToken).ConfigureAwait(false);
                if (candidate == null) continue;
                if (!ShouldInitiate(candidate, currentHour, Roll.NextDouble(), chancePercent)) continue;

                NpcProactiveMotiveDefinition motive = NpcProactiveService.SelectMotive(candidate.Affinity, Roll);
                string body = BuildLetterBody(
                    candidate.DisplayName,
                    NpcProactiveService.BuildTriggerReason(candidate.Affinity, candidate.HasRelationship),
                    motive.OpeningHint);
                int travelHours = AwakeLetterService.ResolveTravelHours(candidate.ContactKey);

                bool created = await AwakeLetterService.CreateInboundAsync(
                    candidate.ContactKey,
                    candidate.DisplayName,
                    body,
                    BuildIdempotencyKey(candidate.ContactKey, currentHour),
                    cancellationToken,
                    travelHours).ConfigureAwait(false);
                if (created)
                {
                    // 正文绝不进日志（日志只留生命周期与动机）。
                    AwakeLog.Write("npc_letter_initiated key=" + candidate.ContactKey
                        + " motive=" + motive.Id
                        + " affinity=" + candidate.Affinity
                        + " transit=" + travelHours);
                    return candidate.ContactKey;
                }
            }
            return string.Empty;
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_letter_initiative_error error=" + ex.Message);
            return string.Empty;
        }
    }

    /// <summary>一次读全账本，按联系人聚合出"最近一封入站信"与"是否有未读入站信"。</summary>
    private static async Task<Dictionary<string, ContactLetterState>> BuildLetterIndexAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, ContactLetterState> index =
            new Dictionary<string, ContactLetterState>(StringComparer.Ordinal);
        foreach (AwakeLetterRecord record in await AwakeLetterService.GetLedgerAsync(null, cancellationToken).ConfigureAwait(false))
        {
            if (!StringComparer.Ordinal.Equals(record.Direction, AwakeLetterConstants.Inbound)) continue;
            string key = record.ContactKey ?? string.Empty;
            ContactLetterState state;
            if (!index.TryGetValue(key, out state) || state == null)
            {
                state = new ContactLetterState();
                index[key] = state;
            }
            if (record.CreatedHour > state.LastInboundHour) state.LastInboundHour = record.CreatedHour;
            if (AwakeLetterConstants.IsUnreadStatus(record.Status)) state.HasUnreadInbound = true;
        }
        return index;
    }

    private static async Task<NpcLetterCandidate> BuildCandidateAsync(
        WorldStateStore store,
        AwakeContactInfo contact,
        Dictionary<string, ContactLetterState> letterIndex,
        CancellationToken cancellationToken)
    {
        string heroId = contact.Target?.Hero?.StringId;
        if (string.IsNullOrWhiteSpace(heroId)) return null;

        int affinity = 0;
        bool hasRelationship = false;
        if (store != null)
        {
            JObject relationship = await store.GetRelationshipAsync(heroId, null, cancellationToken).ConfigureAwait(false);
            if (relationship != null)
            {
                hasRelationship = true;
                affinity = Clamp(
                    IntValue(relationship["trust"]) + IntValue(relationship["love"]) - IntValue(relationship["hostility"]),
                    -100,
                    100);
            }
        }

        ContactLetterState state;
        if (!letterIndex.TryGetValue(contact.CanonicalContactKey, out state) || state == null)
        {
            state = new ContactLetterState();
        }

        return new NpcLetterCandidate
        {
            ContactKey = contact.CanonicalContactKey,
            DisplayName = contact.DisplayName,
            Affinity = affinity,
            HasRelationship = hasRelationship,
            LastInboundHour = state.LastInboundHour,
            HasUnreadInbound = state.HasUnreadInbound
        };
    }

    private static void Shuffle(List<AwakeContactInfo> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = Roll.Next(i + 1);
            AwakeContactInfo swap = items[i];
            items[i] = items[j];
            items[j] = swap;
        }
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
