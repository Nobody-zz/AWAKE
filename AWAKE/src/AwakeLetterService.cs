using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Awake;

/// <summary>
/// 远程信件送达闭环（运行时侧）。正文仍由 transcript 承载；本服务只负责账本与生命周期。
/// 批次：AWAKE-LETTER-DELIVERY-20260913。UI 不在本批范围内。
/// </summary>
internal static class AwakeLetterService
{
    /// <summary>
    /// 信使速度（地图单位／游戏小时）。送达时间 ＝ 距离 ÷ 该速度，精度到游戏小时。
    /// <para>
    /// 依据（1.3.15 反编译源实测）：<c>DefaultPartySpeedCalculatingModel.BaseSpeed = 4</c> 是**每游戏小时**
    /// 推进的地图单位数——移动结算为 <c>MobileParty.NextMoveDistance = Speed * dt</c>，dt 即小时增量；
    /// 地图实测跨度 706 × 548 地图单位（`settlements.xml` posX/posY，与运行时 `CampaignVec2` 同系），
    /// 部队 4 单位/小时 ⇒ 横穿大陆约 7 天，与原版体感一致。**不是**"地图单位/天"。
    /// </para>
    /// <para>
    /// 纯骑兵小队理论上限 ＝ <c>4 × (200/(200+1))^0.4 × 1.3 ≈ 5.2</c> 地图单位/小时（骑兵加成
    /// <c>0.3 × cavalry/total</c>）。信使＝单人、无辎重、驿站换马、昼夜兼程，取该上限的约 1.5 倍
    /// ⇒ <b>8 地图单位/小时 ＝ 192 地图单位/天</b>：中位城际 270 单位 ⇒ 34 小时；横穿大陆 ≈ 3.5 天。
    /// 恒速且不计地形／夜间惩罚 ⇒ 实际比任何部队都快。
    /// </para>
    /// </summary>
    internal const double CourierMapUnitsPerHour = 8d;

    /// <summary>取不到地图上下文（离线测试／无战役）时的确定性降级行程（游戏小时）。</summary>
    internal const int UnknownDistanceTravelHours = 24;

    /// <summary>未回复信件的过期窗口（游戏日）。</summary>
    internal const int ExpiryDays = 14;

    internal sealed class AwakeLetterUnreadSummary
    {
        internal int Total { get; set; }
        internal Dictionary<string, int> ByContact { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
    }

    // ---------- 送达时间：固定公式 ----------

    /// <summary>
    /// 送达耗时（游戏小时）＝ 距离 ÷ 信使速度，向上取整到整点，最少 1 小时。
    /// 连续函数——距离每变化一点，耗时即变化，不分档。
    /// </summary>
    internal static int TravelHoursForDistance(double distanceMapUnits)
    {
        if (double.IsNaN(distanceMapUnits) || double.IsInfinity(distanceMapUnits) || distanceMapUnits <= 0d) return 1;
        double hours = distanceMapUnits / CourierMapUnitsPerHour;
        int rounded = (int)Math.Ceiling(hours);
        return rounded < 1 ? 1 : rounded;
    }

    /// <summary>解算某联系人的送达耗时；取不到距离时降级为常量。</summary>
    internal static int ResolveTravelHours(string contactKey)
    {
        if (TryResolveDistance(contactKey, out double distanceMapUnits))
        {
            return TravelHoursForDistance(distanceMapUnits);
        }
        return UnknownDistanceTravelHours;
    }

    /// <summary>玩家与联系人当前位置的地图距离（地图单位）。取不到返回 false（确定性降级）。</summary>
    internal static bool TryResolveDistance(string contactKey, out double distanceMapUnits)
    {
        distanceMapUnits = 0d;
        if (string.IsNullOrWhiteSpace(contactKey)) return false;
        try
        {
            Hero hero = FindHero(contactKey);
            if (hero == null) return false;
            MobileParty mainParty = MobileParty.MainParty;
            if (mainParty == null) return false;
            MapDistanceModel model = Campaign.Current?.Models?.MapDistanceModel;
            if (model == null) return false;

            MobileParty npcParty = hero.PartyBelongedTo;
            if (npcParty != null)
            {
                float partyDistance = model.GetDistance(mainParty, npcParty, MobileParty.NavigationType.Default, out _);
                if (partyDistance >= 0f && partyDistance < 10000000f)
                {
                    distanceMapUnits = partyDistance;
                    return true;
                }
            }

            Settlement settlement = hero.CurrentSettlement ?? hero.StayingInSettlement;
            if (settlement != null)
            {
                float settlementDistance = model.GetDistance(mainParty, settlement, false, MobileParty.NavigationType.Default, out _);
                if (settlementDistance >= 0f && settlementDistance < 10000000f)
                {
                    distanceMapUnits = settlementDistance;
                    return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("letter_distance_probe_error key=" + contactKey + " error=" + ex.Message);
            return false;
        }
    }

    // ---------- 发送：玩家 → NPC ----------

    internal static async Task<bool> SendAsync(
        string contactKey,
        string conversationId,
        string text,
        CancellationToken cancellationToken)
    {
        string rejectionReason = AwakeLetterRequestValidator.GetRejectionReason(contactKey, text);
        if (!string.IsNullOrWhiteSpace(rejectionReason))
        {
            AwakeLog.Write("letter_send_rejected key=" + (contactKey ?? "null") + " reason=" + rejectionReason);
            return false;
        }
        IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
        if (host == null)
        {
            AwakeLog.Write("letter_send_rejected key=" + contactKey + " reason=host_missing");
            return false;
        }
        if (!await EnsureReadyAsync(host, cancellationToken).ConfigureAwait(false))
        {
            AwakeLog.Write("letter_send_rejected key=" + contactKey + " reason=storage_not_ready");
            return false;
        }

        string idempotencyKey = string.IsNullOrWhiteSpace(conversationId)
            ? "letter|" + Guid.NewGuid().ToString("N")
            : conversationId;
        int sentHour = AwakeRuntime.CurrentGameAbsoluteHour();
        int travelHours = ResolveTravelHours(contactKey);
        int deliverHour = sentHour + travelHours;
        string sender = PlayerSpeaker();
        string recipient = ResolveContactDisplayName(contactKey);

        return await WriteOutboundAsync(
            contactKey,
            idempotencyKey,
            idempotencyKey,
            text,
            sender,
            recipient,
            sentHour,
            deliverHour,
            string.Empty,
            cancellationToken).ConfigureAwait(false);
    }

    // ---------- NPC 主动来信：NPC → 玩家 ----------

    /// <param name="travelHours">
    /// 送信在途时间（游戏小时）。默认 0 ＝ "当面递到"：账本落盘即可读（既有语义，smoke 依赖）。
    /// 传正数表示信使仍在路上——信件先落 <c>sent</c>，到点由 <see cref="AdvanceAsync"/> 转 <c>delivered</c>，
    /// 这样"远方寄来的信"与玩家写的信走同一条时钟。负数一律按 0 处理。
    /// </param>
    internal static async Task<bool> CreateInboundAsync(
        string contactKey,
        string senderName,
        string text,
        string idempotencyKey,
        CancellationToken cancellationToken,
        int travelHours = 0)
    {
        string rejectionReason = AwakeLetterRequestValidator.GetRejectionReason(contactKey, text);
        if (!string.IsNullOrWhiteSpace(rejectionReason))
        {
            AwakeLog.Write("letter_inbound_rejected key=" + (contactKey ?? "null") + " reason=" + rejectionReason);
            return false;
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            AwakeLog.Write("letter_inbound_rejected key=" + contactKey + " reason=idempotency_missing");
            return false;
        }
        IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
        if (host == null)
        {
            AwakeLog.Write("letter_inbound_rejected key=" + contactKey + " reason=host_missing");
            return false;
        }
        if (!await EnsureReadyAsync(host, cancellationToken).ConfigureAwait(false))
        {
            AwakeLog.Write("letter_inbound_rejected key=" + contactKey + " reason=storage_not_ready");
            return false;
        }

        int hour = AwakeRuntime.CurrentGameAbsoluteHour();
        int inTransitHours = travelHours < 0 ? 0 : travelHours;
        string sender = string.IsNullOrWhiteSpace(senderName)
            ? AwakeLocalization.Resolve("awake.scene_shout.speaker", "附近的人们")
            : senderName;
        string letterId = AwakeLetterConstants.NewLetterId(idempotencyKey);

        bool appended = await AwakeTranscriptService.AppendLetterAsync(
            contactKey,
            idempotencyKey,
            DayOf(hour),
            ResolveLocation(),
            text,
            idempotencyKey,
            cancellationToken,
            sender,
            "npc").ConfigureAwait(false);
        if (!appended)
        {
            await AwakeLetterCommit.RecordFailedAsync(contactKey, letterId, AwakeLetterConstants.Inbound, sender, PlayerSpeaker(), hour, hour, "transcript_write_failed", idempotencyKey, cancellationToken).ConfigureAwait(false);
            return false;
        }

        bool recorded = await AwakeLetterCommit.RecordAsync(
            AwakeLetterCommit.AddArguments(new AwakeLetterRecord
            {
                Id = letterId,
                ContactKey = contactKey,
                Direction = AwakeLetterConstants.Inbound,
                Sender = sender,
                Recipient = PlayerSpeaker(),
                CreatedHour = hour,
                DeliverHour = hour + inTransitHours,
                // 在途 ⇒ sent（到点由 AdvanceAsync 转 delivered）；当面递到 ⇒ 到达即未读。
                Status = inTransitHours > 0
                    ? AwakeLetterConstants.StatusSent
                    : AwakeLetterConstants.StatusDelivered
            }),
            "letter-add|" + idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (recorded)
        {
            AwakeLog.Write("letter_inbound_recorded key=" + contactKey + " id=" + letterId + " transit=" + inTransitHours);
        }
        return recorded;
    }

    // ---------- 阅读 ----------

    internal static async Task<bool> MarkReadAsync(string letterId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(letterId)) return false;
        bool ok = await AwakeLetterCommit.RecordAsync(
            new JObject
            {
                ["op"] = AwakeLetterConstants.OpRead,
                ["id"] = letterId,
                ["hour"] = AwakeRuntime.CurrentGameAbsoluteHour()
            },
            "letter-read|" + letterId,
            cancellationToken).ConfigureAwait(false);
        if (ok) AwakeLog.Write("letter_read_recorded id=" + letterId);
        return ok;
    }

    // ---------- 回复：玩家 → 原来信 ----------

    internal static async Task<bool> ReplyAsync(
        string inboundLetterId,
        string contactKey,
        string text,
        CancellationToken cancellationToken)
    {
        string rejectionReason = AwakeLetterRequestValidator.GetRejectionReason(contactKey, text);
        if (!string.IsNullOrWhiteSpace(rejectionReason))
        {
            AwakeLog.Write("letter_reply_rejected key=" + (contactKey ?? "null") + " reason=" + rejectionReason);
            return false;
        }
        if (string.IsNullOrWhiteSpace(inboundLetterId))
        {
            AwakeLog.Write("letter_reply_rejected key=" + contactKey + " reason=inbound_missing");
            return false;
        }
        IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
        if (host == null)
        {
            AwakeLog.Write("letter_reply_rejected key=" + contactKey + " reason=host_missing");
            return false;
        }
        if (!await EnsureReadyAsync(host, cancellationToken).ConfigureAwait(false))
        {
            AwakeLog.Write("letter_reply_rejected key=" + contactKey + " reason=storage_not_ready");
            return false;
        }

        int sentHour = AwakeRuntime.CurrentGameAbsoluteHour();
        int travelHours = ResolveTravelHours(contactKey);
        string idempotencyKey = "letter-reply|" + inboundLetterId;
        bool appended = await AwakeTranscriptService.AppendLetterAsync(
            contactKey,
            idempotencyKey,
            DayOf(sentHour),
            ResolveLocation(),
            text,
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (!appended)
        {
            AwakeLog.Write("letter_reply_rejected key=" + contactKey + " reason=transcript_write_failed");
            return false;
        }

        bool recorded = await AwakeLetterCommit.RecordAsync(
            new JObject
            {
                ["op"] = AwakeLetterConstants.OpReply,
                ["inboundId"] = inboundLetterId,
                ["outbound"] = new AwakeLetterRecord
                {
                    Id = AwakeLetterConstants.NewLetterId(idempotencyKey),
                    ContactKey = contactKey,
                    Direction = AwakeLetterConstants.Outbound,
                    Sender = PlayerSpeaker(),
                    Recipient = ResolveContactDisplayName(contactKey),
                    CreatedHour = sentHour,
                    DeliverHour = sentHour + travelHours,
                    Status = AwakeLetterConstants.StatusSent,
                    ReplyToId = inboundLetterId
                }.ToJson()
            },
            "letter-reply-record|" + inboundLetterId,
            cancellationToken).ConfigureAwait(false);
        if (recorded) AwakeLog.Write("letter_reply_recorded key=" + contactKey + " inbound=" + inboundLetterId);
        return recorded;
    }

    // ---------- 日推进：送达 / 过期 ----------

    /// <summary>
    /// 按当前绝对小时推进账本：未达信件的 <c>deliverHour</c> 到达即转 delivered；超期转 expired。
    /// 由每小时 tick 调用，幂等键含小时，重复调用不产生额外副作用。
    /// </summary>
    internal static async Task AdvanceAsync(int currentHour, CancellationToken cancellationToken)
    {
        if (currentHour < 0) return;
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return;
        try
        {
            await store.UpdateLettersAsync(
                new JObject
                {
                    ["op"] = AwakeLetterConstants.OpAdvance,
                    ["hour"] = currentHour,
                    ["expiryHours"] = ExpiryDays * AwakeLetterConstants.HoursPerDay
                },
                "letter-advance|" + currentHour,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AwakeLog.Write("letter_advance_error error=" + ex.Message);
        }
    }

    // ---------- 读取 ----------

    internal static async Task<List<AwakeLetterRecord>> GetLedgerAsync(string contactKey, CancellationToken cancellationToken)
    {
        List<AwakeLetterRecord> result = new List<AwakeLetterRecord>();
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return result;
        JObject doc = await store.GetLettersAsync(null, cancellationToken).ConfigureAwait(false);
        foreach (AwakeLetterRecord record in LetterLedgerDocument.ParseLetters(doc))
        {
            if (string.IsNullOrWhiteSpace(contactKey)
                || StringComparer.Ordinal.Equals(record.ContactKey, contactKey))
            {
                result.Add(record);
            }
        }
        result.Sort((a, b) => b.CreatedHour.CompareTo(a.CreatedHour));
        return result;
    }

    internal static async Task<AwakeLetterUnreadSummary> GetUnreadAsync(CancellationToken cancellationToken)
    {
        AwakeLetterUnreadSummary summary = new AwakeLetterUnreadSummary();
        foreach (AwakeLetterRecord record in await GetLedgerAsync(null, cancellationToken).ConfigureAwait(false))
        {
            if (!StringComparer.Ordinal.Equals(record.Direction, AwakeLetterConstants.Inbound)) continue;
            // 只算"已到手却还没读"的信：仍在信使路上的信玩家根本看不到，计进未读会指向一个空列表。
            if (!StringComparer.Ordinal.Equals(record.Status, AwakeLetterConstants.StatusDelivered)) continue;
            summary.Total++;
            int count;
            summary.ByContact.TryGetValue(record.ContactKey, out count);
            summary.ByContact[record.ContactKey] = count + 1;
        }
        return summary;
    }

    // ---------- 内部 ----------

    private static async Task<bool> WriteOutboundAsync(
        string contactKey,
        string conversationId,
        string idempotencyKey,
        string text,
        string sender,
        string recipient,
        int createdHour,
        int deliverHour,
        string replyToId,
        CancellationToken cancellationToken)
    {
        // 两步编排（写正文 ⇒ 写账本）连同失败留痕都在 AwakeLetterCommit 里：
        // 那个件不引 TaleWorlds，编得进离线验台 —— 半提交的判据才写得了。
        AwakeLetterCommit.LetterCommitResult commit = await AwakeLetterCommit.WriteOutboundAsync(
            contactKey,
            conversationId,
            idempotencyKey,
            DayOf(createdHour),
            ResolveLocation(),
            text,
            sender,
            recipient,
            createdHour,
            deliverHour,
            replyToId,
            cancellationToken).ConfigureAwait(false);

        if (commit.LedgerRecorded)
        {
            AwakeLog.Write("letter_send_succeeded key=" + contactKey + " conversation=" + conversationId + " hours=" + (deliverHour - createdHour) + " source=letter");
        }
        return commit.LedgerRecorded;
    }

    private static Task<bool> EnsureReadyAsync(IMarcusAiFrameworkHost host, CancellationToken cancellationToken)
    {
        return AwakeRuntime.EnsureWorldStateReadyAsync(host, cancellationToken, new[]
        {
            AiTaskConstants.TranscriptNamespace,
            AiTaskConstants.ContactsNamespace,
            AiTaskConstants.LettersNamespace
        });
    }

    private static int DayOf(int absoluteHour)
    {
        return absoluteHour / AwakeLetterConstants.HoursPerDay;
    }

    private static string PlayerSpeaker()
    {
        return AwakeLocalization.Resolve("awake.ui.you", "你");
    }

    private static string ResolveLocation()
    {
        try
        {
            return TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement?.Name?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>按联系人键反查目标英雄；拿不到战役上下文时返回 null（确定性降级）。</summary>
    private static Hero FindHero(string contactKey)
    {
        if (string.IsNullOrWhiteSpace(contactKey)) return null;
        try
        {
            foreach (AwakeContactInfo contact in AwakeMessengerService.BuildContacts())
            {
                if (contact == null || !StringComparer.Ordinal.Equals(contact.CanonicalContactKey, contactKey)) continue;
                return contact.Target?.Hero;
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("letter_contact_probe_error error=" + ex.Message);
        }
        return null;
    }

    private static string ResolveContactDisplayName(string contactKey)
    {
        if (string.IsNullOrWhiteSpace(contactKey)) return string.Empty;
        try
        {
            foreach (AwakeContactInfo contact in AwakeMessengerService.BuildContacts())
            {
                if (contact == null || !StringComparer.Ordinal.Equals(contact.CanonicalContactKey, contactKey)) continue;
                if (!string.IsNullOrWhiteSpace(contact.DisplayName)) return contact.DisplayName;
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("letter_contact_probe_error error=" + ex.Message);
        }
        return contactKey;
    }
}
