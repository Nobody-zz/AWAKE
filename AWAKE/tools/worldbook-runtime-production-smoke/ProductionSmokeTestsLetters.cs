using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Awake.WorldbookRuntimeProductionSmoke;

internal static partial class Program
{
    /// <summary>
    /// AWAKE-LETTER-DELIVERY-20260913：写信 → 送达 → 未读 → 阅读 → 回复，含 NPC 主动来信、
    /// 过期、失败留痕、幂等与敏感文本。送达时刻按固定公式（距离 ÷ 信使速度）计算，精度到游戏小时。
    /// 全部离线，不调用任何 Provider，不触碰 UI。
    /// </summary>
    private static async Task TestLetterDeliveryLifecycleAsync()
    {
        // 0) 送达公式：连续、按整点向上取整、有下限。距离每变一点，耗时即变，不分档。
        Check(AwakeLetterService.TravelHoursForDistance(40d) == 5,
            "formula: 40 map units at 8 units/hour must be exactly 5 hours");
        Check(AwakeLetterService.TravelHoursForDistance(400d) == 50,
            "formula: 400 map units at 8 units/hour must be 50 hours");
        Check(AwakeLetterService.TravelHoursForDistance(270d) == 34,
            "formula: the median inter-settlement distance (270) must be 34 hours");
        Check(AwakeLetterService.TravelHoursForDistance(10d) == 2,
            "formula: an adjacent settlement (10) must be 2 hours");
        Check(AwakeLetterService.TravelHoursForDistance(41d) != AwakeLetterService.TravelHoursForDistance(40d),
            "formula must be continuous: a single map unit can move the hour bucket");
        Check(AwakeLetterService.TravelHoursForDistance(0d) == 1
            && AwakeLetterService.TravelHoursForDistance(0.4d) == 1,
            "formula must never round down to zero hours");
        // 信使不是部队：必须快于原版纯骑兵小队（BaseSpeed 4 × (200/201)^0.4 × 1.3 ≈ 5.2 地图单位/小时）。
        // 中位城际 270 单位：骑兵 ≈ 52 小时，信使 34 小时 ⇒ 这条把"信使要比骑兵快"钉成回归断言。
        Check(AwakeLetterService.TravelHoursForDistance(270d) < (int)Math.Ceiling(270d / 5.2d),
            "courier must outpace a vanilla all-cavalry party on the same distance");

        ProductionSmokeHost host = CreateHost("letters-lifecycle");
        AwakeRuntime.SetHostOverrideForTesting(host);
        const int StartHour = 240;                                                     // 第 10 天 00:00
        const int OfflineDeliveryHours = AwakeLetterService.UnknownDistanceTravelHours; // 离线无地图 ⇒ 常量降级
        AwakeRuntime.CurrentGameDayProvider = () => StartHour / AwakeLetterConstants.HoursPerDay;
        AwakeRuntime.CurrentGameHoursProvider = () => StartHour;
        WorldStateStore store = CreateStore(host, new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace));
        await Install(store).ConfigureAwait(false);
        CancellationToken cancellationToken = CancellationToken.None;
        const string contact = "hero:letters";

        // 1) 发送：正文进 transcript，账本记 sent，送达时刻 ＝ 发送时刻 + 行程小时。
        bool sent = await AwakeLetterService.SendAsync(contact, string.Empty, "信件正文甲", cancellationToken).ConfigureAwait(false);
        Check(sent, "letter send must complete both the transcript body and the ledger entry; logs=" + RecentCapturedLogs());
        List<AwakeTranscriptLine> history = await AwakeTranscriptService.GetHistoryAsync(contact, cancellationToken).ConfigureAwait(false);
        Check(history.Count == 1
            && history[0].Source == "letter"
            && history[0].Kind == "player"
            && history[0].Text == "信件正文甲",
            "letter body must live in the transcript as the single source of text; count=" + history.Count);

        List<AwakeLetterRecord> ledger = await AwakeLetterService.GetLedgerAsync(contact, cancellationToken).ConfigureAwait(false);
        Check(ledger.Count == 1, "one sent letter must produce exactly one ledger entry; count=" + ledger.Count);
        AwakeLetterRecord first = ledger[0];
        Check(first.Direction == AwakeLetterConstants.Outbound
            && first.Status == AwakeLetterConstants.StatusSent
            && first.CreatedHour == StartHour
            && first.DeliverHour == StartHour + OfflineDeliveryHours,
            "outbound letter must start as sent with an hour-precise delivery time; dir=" + first.Direction
            + " status=" + first.Status + " createdHour=" + first.CreatedHour + " deliverHour=" + first.DeliverHour);
        Check(StringComparer.Ordinal.Equals(first.Id, history[0].Id),
            "ledger id must equal the transcript line id so lifecycle stays traceable to its body");

        string raw = host.StorageAdapter.GetNamespace(AiTaskConstants.LettersNamespace)?.Read(AiTaskConstants.LettersKey);
        Check(!string.IsNullOrWhiteSpace(raw) && raw.IndexOf("\"schema\":\"awake.letters.v1\"", StringComparison.Ordinal) >= 0,
            "letter ledger must persist under the awake.letters namespace with its own schema");

        int deliverHour = StartHour + OfflineDeliveryHours;

        // 2) 未到送达时刻仍为 sent（差一小时也不行）。
        await AwakeLetterService.AdvanceAsync(deliverHour - 1, cancellationToken).ConfigureAwait(false);
        ledger = await AwakeLetterService.GetLedgerAsync(contact, cancellationToken).ConfigureAwait(false);
        Check(FindLetter(ledger, first.Id).Status == AwakeLetterConstants.StatusSent,
            "letter must stay sent before its delivery hour");

        // 3) 到达送达小时 ⇒ delivered。小时级触发，不必等到次日。
        AwakeRuntime.CurrentGameHoursProvider = () => deliverHour;
        await AwakeLetterService.AdvanceAsync(deliverHour, cancellationToken).ConfigureAwait(false);
        ledger = await AwakeLetterService.GetLedgerAsync(contact, cancellationToken).ConfigureAwait(false);
        Check(FindLetter(ledger, first.Id).Status == AwakeLetterConstants.StatusDelivered,
            "letter must become delivered on its delivery hour, not its delivery day");

        // 4) NPC 主动来信 ⇒ 未读。
        bool inbound = await AwakeLetterService.CreateInboundAsync(contact, "某贵族", "来信正文乙", "inbound-1", cancellationToken).ConfigureAwait(false);
        Check(inbound, "inbound NPC letter must be recorded");
        string inboundId = AwakeLetterConstants.NewLetterId("inbound-1");
        AwakeLetterService.AwakeLetterUnreadSummary unread = await AwakeLetterService.GetUnreadAsync(cancellationToken).ConfigureAwait(false);
        Check(unread.Total == 1 && unread.ByContact.TryGetValue(contact, out int perContact) && perContact == 1,
            "unread must aggregate inbound letters per contact; total=" + unread.Total);

        // 5) 阅读 ⇒ 出未读。
        bool read = await AwakeLetterService.MarkReadAsync(inboundId, cancellationToken).ConfigureAwait(false);
        Check(read, "marking an inbound letter read must persist");
        unread = await AwakeLetterService.GetUnreadAsync(cancellationToken).ConfigureAwait(false);
        Check(unread.Total == 0, "a read inbound letter must leave the unread count; total=" + unread.Total);
        ledger = await AwakeLetterService.GetLedgerAsync(contact, cancellationToken).ConfigureAwait(false);
        AwakeLetterRecord inboundRecord = FindLetter(ledger, inboundId);
        Check(inboundRecord != null
            && inboundRecord.Status == AwakeLetterConstants.StatusRead
            && inboundRecord.ReadHour == deliverHour,
            "read letter must record its read hour");

        // 6) 回复 ⇒ 双向关联。
        bool replied = await AwakeLetterService.ReplyAsync(inboundId, contact, "回信正文丙", cancellationToken).ConfigureAwait(false);
        Check(replied, "reply must create an outbound letter and link both sides");
        ledger = await AwakeLetterService.GetLedgerAsync(contact, cancellationToken).ConfigureAwait(false);
        AwakeLetterRecord original = FindLetter(ledger, inboundId);
        Check(original.Status == AwakeLetterConstants.StatusReplied
            && !string.IsNullOrWhiteSpace(original.ReplyLetterId),
            "replied letter must flip to replied and point at its answer; status=" + original.Status);
        AwakeLetterRecord answer = FindLetter(ledger, original.ReplyLetterId);
        Check(answer != null
            && answer.Direction == AwakeLetterConstants.Outbound
            && StringComparer.Ordinal.Equals(answer.ReplyToId, inboundId),
            "the answer must carry replyToId back to the original letter");

        // 7) 超期未回复 ⇒ expired。
        int laterHour = deliverHour + AwakeLetterConstants.HoursPerDay;
        AwakeRuntime.CurrentGameDayProvider = () => laterHour / AwakeLetterConstants.HoursPerDay;
        AwakeRuntime.CurrentGameHoursProvider = () => laterHour;
        bool later = await AwakeLetterService.SendAsync(contact, string.Empty, "信件正文丁", cancellationToken).ConfigureAwait(false);
        Check(later, "second outbound letter must be recorded");
        ledger = await AwakeLetterService.GetLedgerAsync(contact, cancellationToken).ConfigureAwait(false);
        AwakeLetterRecord pending = null;
        foreach (AwakeLetterRecord record in ledger)
        {
            if (record.CreatedHour == laterHour && record.Status == AwakeLetterConstants.StatusSent) pending = record;
        }
        Check(pending != null, "second outbound letter must start as sent");
        int expiryHour = laterHour + AwakeLetterService.ExpiryDays * AwakeLetterConstants.HoursPerDay + 1;
        AwakeRuntime.CurrentGameDayProvider = () => expiryHour / AwakeLetterConstants.HoursPerDay;
        AwakeRuntime.CurrentGameHoursProvider = () => expiryHour;
        await AwakeLetterService.AdvanceAsync(expiryHour, cancellationToken).ConfigureAwait(false);
        ledger = await AwakeLetterService.GetLedgerAsync(contact, cancellationToken).ConfigureAwait(false);
        Check(FindLetter(ledger, pending.Id).Status == AwakeLetterConstants.StatusExpired,
            "an unanswered letter past the expiry window must become expired");

        // 8) 幂等：同 key 不产生第二条。
        await AwakeLetterService.SendAsync(contact, "idem-letter-1", "信件正文戊", cancellationToken).ConfigureAwait(false);
        await AwakeLetterService.SendAsync(contact, "idem-letter-1", "信件正文戊", cancellationToken).ConfigureAwait(false);
        ledger = await AwakeLetterService.GetLedgerAsync(contact, cancellationToken).ConfigureAwait(false);
        string idempotentId = AwakeLetterConstants.NewLetterId("idem-letter-1");
        int duplicates = 0;
        foreach (AwakeLetterRecord record in ledger)
        {
            if (StringComparer.Ordinal.Equals(record.Id, idempotentId)) duplicates++;
        }
        Check(duplicates == 1, "repeating one idempotency key must not create a second ledger entry; count=" + duplicates);

        // 9) 正文超 transcript 单行上限 ⇒ 记 failed，且不留半写历史。
        // 前提：信件校验上限必须比 transcript 单行上限宽，否则"过校验却存不下"的区间不存在，
        // 本用例（以及整条 transcript_write_failed 降级路径）将无从构造。两者是**有意分层**：
        // 信件层判"请求是否合法"，transcript 层判"能否落库"。若将来有人把两侧调平，这里会红。
        Check(AwakeLetterConstants.MaximumLetterBytes > AwakeTranscriptConstants.MaximumTextUtf8Bytes,
            "letter limit must stay wider than transcript line limit, or the write-failure path becomes unreachable; letter="
            + AwakeLetterConstants.MaximumLetterBytes + " transcript=" + AwakeTranscriptConstants.MaximumTextUtf8Bytes);
        int historyBefore = (await AwakeTranscriptService.GetHistoryAsync(contact, cancellationToken).ConfigureAwait(false)).Count;
        string oversized = new string('长', 450); // 1350 UTF-8 字节：过信件校验(2000)，超 transcript 单行(1200)
        bool oversizedSent = await AwakeLetterService.SendAsync(contact, string.Empty, oversized, cancellationToken).ConfigureAwait(false);
        Check(!oversizedSent, "a letter the transcript cannot store must not report success");
        int historyAfter = (await AwakeTranscriptService.GetHistoryAsync(contact, cancellationToken).ConfigureAwait(false)).Count;
        Check(historyAfter == historyBefore, "a failed letter must not leave partial history");
        ledger = await AwakeLetterService.GetLedgerAsync(contact, cancellationToken).ConfigureAwait(false);
        AwakeLetterRecord failed = null;
        foreach (AwakeLetterRecord record in ledger)
        {
            if (record.Status == AwakeLetterConstants.StatusFailed) failed = record;
        }
        Check(failed != null && failed.FailureReason == "transcript_write_failed",
            "a failed letter must leave an auditable ledger entry with its failure reason");

        // 10) 敏感文本：正文一律不得进日志。
        Check(!HasCapturedLog("信件正文甲")
            && !HasCapturedLog("来信正文乙")
            && !HasCapturedLog("回信正文丙")
            && !HasCapturedLog(oversized),
            "letter bodies must never appear in captured logs");

        await store.BeginFinalDrainAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// NPC 主动来信（信件闭环的另一半：NPC → 玩家）。决策层全部离线可判：
    /// 上一封未读不催、冷却期内不寄、概率不得被强行绕过；幂等键按"联系人 × 游戏日"稳定；
    /// 入站信可带信使在途时间（先 sent，到点才 delivered 并计入未读）。不调用任何 Provider、不触碰 UI。
    /// </summary>
    private static async Task TestNpcProactiveLetterInitiativeAsync()
    {
        // 1) 决策：上一封还没被读到 ⇒ 一律不催，哪怕 roll 0（概率必然命中）。
        NpcLetterInitiator.NpcLetterCandidate unread = new NpcLetterInitiator.NpcLetterCandidate
        {
            ContactKey = "hero:unread",
            HasRelationship = true,
            Affinity = 80,
            HasUnreadInbound = true
        };
        Check(!NpcLetterInitiator.ShouldInitiate(unread, 240, 0d, 35),
            "an unread inbound letter must stop a second one, even at roll 0");

        // 2) 决策：冷却期内不寄（一小时前刚寄过）。
        NpcLetterInitiator.NpcLetterCandidate cooling = new NpcLetterInitiator.NpcLetterCandidate
        {
            ContactKey = "hero:cooling",
            HasRelationship = true,
            Affinity = 80,
            LastInboundHour = 240
        };
        Check(!NpcLetterInitiator.ShouldInitiate(cooling, 241, 0d, 35),
            "a letter sent one hour ago must still be inside the cooldown window");

        // 3) 决策：冷却已过 + 关系亲密 + roll 0 ⇒ 寄。
        NpcLetterInitiator.NpcLetterCandidate ready = new NpcLetterInitiator.NpcLetterCandidate
        {
            ContactKey = "hero:ready",
            HasRelationship = true,
            Affinity = 80,
            LastInboundHour = 240
        };
        int coolHour = 240 + NpcLetterInitiator.CooldownHours;
        Check(NpcLetterInitiator.ShouldInitiate(ready, coolHour, 0d, 35),
            "past the cooldown, a warm relationship with roll 0 must initiate");

        // 4) 决策：概率有上限 ⇒ 高 roll 永远不能强行触发。
        Check(!NpcLetterInitiator.ShouldInitiate(ready, coolHour, 0.999d, 35),
            "the trigger must stay probabilistic: a high roll can never force a letter");

        // 5) 决策：尚无关系 ⇒ 最低档概率，roll 0.5 不该命中。
        NpcLetterInitiator.NpcLetterCandidate stranger = new NpcLetterInitiator.NpcLetterCandidate
        {
            ContactKey = "hero:stranger"
        };
        Check(!NpcLetterInitiator.ShouldInitiate(stranger, 240, 0.5d, 35),
            "a contact with no relationship must stay at the lowest trigger chance");

        // 6) 冷却口径不得与"主动搭话"漂移。
        Check(NpcLetterInitiator.CooldownHours == NpcProactiveConstants.CooldownDays * AwakeLetterConstants.HoursPerDay,
            "the letter cooldown must stay tied to the shared proactive cooldown");

        // 7) 幂等键：同一联系人同一游戏日内固定，跨日才翻页。
        string keyMorning = NpcLetterInitiator.BuildIdempotencyKey("hero:ready", 240);
        string keyEvening = NpcLetterInitiator.BuildIdempotencyKey("hero:ready", 240 + 12);
        string keyNextDay = NpcLetterInitiator.BuildIdempotencyKey("hero:ready", 240 + AwakeLetterConstants.HoursPerDay);
        Check(StringComparer.Ordinal.Equals(keyMorning, keyEvening),
            "one contact must yield one idempotency key per game day; morning=" + keyMorning + " evening=" + keyEvening);
        Check(!StringComparer.Ordinal.Equals(keyMorning, keyNextDay),
            "the idempotency key must roll over with the game day");
        Check(keyMorning.StartsWith(NpcLetterInitiator.IdempotencyPrefix, StringComparison.Ordinal),
            "initiative letters must be distinguishable by their idempotency prefix; key=" + keyMorning);

        // 8) 正文：触发理由与署名都在，占位符必须已替换，且必须装得进 transcript 单行。
        string body = NpcLetterInitiator.BuildLetterBody(
            "某贵族",
            NpcProactiveService.BuildTriggerReason(80, true),
            "想问问你近来的打算");
        Check(body.IndexOf("某贵族", StringComparison.Ordinal) >= 0,
            "the letter must be signed by its sender; body=" + body);
        Check(body.IndexOf("想问问你近来的打算", StringComparison.Ordinal) >= 0,
            "the letter must carry the motive's opening hint; body=" + body);
        Check(body.IndexOf('{') < 0 && body.IndexOf('}') < 0,
            "every placeholder must be substituted; body=" + body);
        Check(System.Text.Encoding.UTF8.GetByteCount(body) <= AwakeTranscriptConstants.MaximumTextUtf8Bytes,
            "an initiative letter must fit the transcript single-line limit; bytes="
            + System.Text.Encoding.UTF8.GetByteCount(body));

        // 9) 入站信可带在途时间：先 sent，到点才 delivered。
        ProductionSmokeHost host = CreateHost("npc-proactive-initiative");
        AwakeRuntime.SetHostOverrideForTesting(host);
        const int StartHour = 480;
        AwakeRuntime.CurrentGameDayProvider = () => StartHour / AwakeLetterConstants.HoursPerDay;
        AwakeRuntime.CurrentGameHoursProvider = () => StartHour;
        WorldStateStore store = CreateStore(host, new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace));
        await Install(store).ConfigureAwait(false);
        CancellationToken cancellationToken = CancellationToken.None;

        const string remote = "hero:remote";
        const int transit = 34; // 中位城际（270 地图单位）对应的信使行程
        const string initiativeKey = "npc-letter|hero:remote|20";
        bool created = await AwakeLetterService.CreateInboundAsync(
            remote, "某远方贵族", "远方来信", initiativeKey, cancellationToken, transit).ConfigureAwait(false);
        Check(created, "an initiative letter with transit time must be recorded; logs=" + RecentCapturedLogs());
        string letterId = AwakeLetterConstants.NewLetterId(initiativeKey);
        List<AwakeLetterRecord> ledger = await AwakeLetterService.GetLedgerAsync(remote, cancellationToken).ConfigureAwait(false);
        AwakeLetterRecord inTransit = FindLetter(ledger, letterId);
        Check(inTransit != null
            && inTransit.Direction == AwakeLetterConstants.Inbound
            && inTransit.Status == AwakeLetterConstants.StatusSent
            && inTransit.DeliverHour == StartHour + transit,
            "an in-transit letter must start as sent with its arrival hour; status="
            + (inTransit == null ? "null" : inTransit.Status) + " deliverHour="
            + (inTransit == null ? -1 : inTransit.DeliverHour));

        // 10) 还在路上的信不该计入未读——玩家看不到它。
        AwakeLetterService.AwakeLetterUnreadSummary unreadSummary =
            await AwakeLetterService.GetUnreadAsync(cancellationToken).ConfigureAwait(false);
        Check(unreadSummary.Total == 0,
            "a letter still on the road must not count as unread (there is nothing to open yet); total=" + unreadSummary.Total);

        await AwakeLetterService.AdvanceAsync(StartHour + transit - 1, cancellationToken).ConfigureAwait(false);
        ledger = await AwakeLetterService.GetLedgerAsync(remote, cancellationToken).ConfigureAwait(false);
        Check(FindLetter(ledger, letterId).Status == AwakeLetterConstants.StatusSent,
            "an in-transit letter must not arrive early");
        unreadSummary = await AwakeLetterService.GetUnreadAsync(cancellationToken).ConfigureAwait(false);
        Check(unreadSummary.Total == 0, "an early tick must still leave the unread count at zero");

        await AwakeLetterService.AdvanceAsync(StartHour + transit, cancellationToken).ConfigureAwait(false);
        ledger = await AwakeLetterService.GetLedgerAsync(remote, cancellationToken).ConfigureAwait(false);
        Check(FindLetter(ledger, letterId).Status == AwakeLetterConstants.StatusDelivered,
            "an in-transit letter must arrive on its delivery hour");
        unreadSummary = await AwakeLetterService.GetUnreadAsync(cancellationToken).ConfigureAwait(false);
        Check(unreadSummary.Total == 1 && unreadSummary.ByContact.TryGetValue(remote, out int perContact) && perContact == 1,
            "arrival must surface the letter as unread; total=" + unreadSummary.Total);

        // 11) 正文一律不得进日志。
        Check(!HasCapturedLog("远方来信"),
            "initiative letter bodies must never appear in captured logs");

        // 12) 编排守卫：离线拿不到战役联系人时安静返回，既不产信也不抛异常。
        string produced = await NpcLetterInitiator.TryProduceAsync(StartHour, cancellationToken).ConfigureAwait(false);
        Check(StringComparer.Ordinal.Equals(produced, string.Empty),
            "with no campaign contacts the initiator must stay silent; produced=" + produced);

        await store.BeginFinalDrainAsync().ConfigureAwait(false);
    }

    private static AwakeLetterRecord FindLetter(List<AwakeLetterRecord> ledger, string id)
    {
        if (ledger == null || string.IsNullOrWhiteSpace(id)) return null;
        foreach (AwakeLetterRecord record in ledger)
        {
            if (StringComparer.Ordinal.Equals(record.Id, id)) return record;
        }
        return null;
    }
}
