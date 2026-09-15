using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Awake;

/// <summary>信件账本的取值域与限制。正文不在这里——transcript 才是正文权威。</summary>
internal static class AwakeLetterConstants
{
    internal const string Outbound = "outbound";
    internal const string Inbound = "inbound";

    internal const string StatusSent = "sent";
    internal const string StatusDelivered = "delivered";
    internal const string StatusRead = "read";
    internal const string StatusReplied = "replied";
    internal const string StatusExpired = "expired";
    internal const string StatusFailed = "failed";

    internal const string OpAdd = "add";
    internal const string OpRead = "read";
    internal const string OpReply = "reply";
    internal const string OpAdvance = "advance";

    /// <summary>单 campaign 账本条目上限；超出丢最老的。</summary>
    internal const int MaximumLetters = 400;

    /// <summary>
    /// 信件正文 UTF-8 上限。**刻意宽于** <c>AwakeTranscriptConstants.MaximumTextUtf8Bytes</c>（1200）。
    /// <para>
    /// 这两层判据不同、故意不调平：信件层判"请求是否合法"（宽松，2000），transcript 层判"能否落库"（严格，1200）。
    /// 1201–2000 的信过校验、被 transcript 拒 ⇒ 走降级：<c>SendAsync</c> 返回 false、落 <c>failed</c>
    /// （<c>failureReason=transcript_write_failed</c>）、不留半写历史、日志不打正文。**非静默失败**。
    /// </para>
    /// <para>
    /// ⚠️ 副作用：这个差值正是 transcript 写失败路径**唯一可构造**的手段（smoke 用例 9 以此触发）。
    /// 调平两侧 ⇒ 该路径与用例一并失效。故 smoke 内钉了一条不变式：
    /// <c>MaximumLetterBytes &gt; MaximumTextUtf8Bytes</c>。
    /// </para>
    /// <para>结论（2026-09-13 结案）：维持现状，不再作为待决项。</para>
    /// </summary>
    internal const int MaximumLetterBytes = 2000;

    /// <summary>一个游戏日包含的游戏内小时数。送达与过期均以绝对小时为精度。</summary>
    internal const int HoursPerDay = 24;

    /// <summary>未读哨兵值（绝对小时）。</summary>
    internal const int UnreadHour = -1;

    internal static bool IsUnreadStatus(string status)
    {
        return StringComparer.Ordinal.Equals(status, StatusSent)
            || StringComparer.Ordinal.Equals(status, StatusDelivered);
    }

    internal static bool IsValidDirection(string direction)
    {
        return StringComparer.Ordinal.Equals(direction, Outbound)
            || StringComparer.Ordinal.Equals(direction, Inbound);
    }

    /// <summary>账本 id ＝ 该信 transcript 行的 id（1:1 追溯，不复制正文）。</summary>
    internal static string NewLetterId(string idempotencyKey)
    {
        return "letter|" + (idempotencyKey ?? string.Empty);
    }
}

internal sealed class AwakeLetterRecord
{
    internal string Id { get; set; } = string.Empty;
    internal string ContactKey { get; set; } = string.Empty;
    internal string Direction { get; set; } = string.Empty;
    internal string Sender { get; set; } = string.Empty;
    internal string Recipient { get; set; } = string.Empty;

    // 绝对小时（自战役起算）——送达、阅读、过期判定一律以此为准。
    internal int CreatedHour { get; set; }
    internal int DeliverHour { get; set; }
    internal int ReadHour { get; set; } = AwakeLetterConstants.UnreadHour;

    internal string Status { get; set; } = string.Empty;
    internal string ReplyToId { get; set; } = string.Empty;
    internal string ReplyLetterId { get; set; } = string.Empty;
    internal string FailureReason { get; set; } = string.Empty;

    // 派生日粒度：仅供展示与旧读法，不作为判定依据。
    internal int CreatedDay => CreatedHour / AwakeLetterConstants.HoursPerDay;
    internal int DeliverDay => DeliverHour / AwakeLetterConstants.HoursPerDay;
    internal int ReadDay => ReadHour < 0 ? AwakeLetterConstants.UnreadHour : ReadHour / AwakeLetterConstants.HoursPerDay;

    internal JObject ToJson()
    {
        return new JObject
        {
            ["id"] = Id,
            ["contactKey"] = ContactKey,
            ["direction"] = Direction,
            ["sender"] = Sender,
            ["recipient"] = Recipient,
            ["createdHour"] = CreatedHour,
            ["deliverHour"] = DeliverHour,
            ["readHour"] = ReadHour,
            // 派生日字段一并落盘，兼容仍按天读取的既有消费方。
            ["createdDay"] = CreatedDay,
            ["deliverDay"] = DeliverDay,
            ["readDay"] = ReadDay,
            ["status"] = Status,
            ["replyToId"] = ReplyToId,
            ["replyLetterId"] = ReplyLetterId,
            ["failureReason"] = FailureReason
        };
    }

    internal static AwakeLetterRecord FromJson(JToken token)
    {
        if (token is not JObject obj) return null;
        AwakeLetterRecord record = new AwakeLetterRecord
        {
            Id = (string)obj["id"] ?? string.Empty,
            ContactKey = (string)obj["contactKey"] ?? string.Empty,
            Direction = (string)obj["direction"] ?? string.Empty,
            Sender = (string)obj["sender"] ?? string.Empty,
            Recipient = (string)obj["recipient"] ?? string.Empty,
            // 旧存档只有日字段：按 日 × 24 迁移到绝对小时，读档即完成升级。
            CreatedHour = HourValue(obj, "createdHour", "createdDay"),
            DeliverHour = HourValue(obj, "deliverHour", "deliverDay"),
            ReadHour = OptionalHourValue(obj, "readHour", "readDay"),
            Status = (string)obj["status"] ?? string.Empty,
            ReplyToId = (string)obj["replyToId"] ?? string.Empty,
            ReplyLetterId = (string)obj["replyLetterId"] ?? string.Empty,
            FailureReason = (string)obj["failureReason"] ?? string.Empty
        };
        if (string.IsNullOrWhiteSpace(record.Id)) return null;
        return record;
    }

    private static int HourValue(JObject obj, string hourKey, string dayKey)
    {
        if (obj[hourKey] != null) return IntValue(obj[hourKey]);
        return IntValue(obj[dayKey]) * AwakeLetterConstants.HoursPerDay;
    }

    private static int OptionalHourValue(JObject obj, string hourKey, string dayKey)
    {
        if (obj[hourKey] != null) return IntValue(obj[hourKey]);
        if (obj[dayKey] != null)
        {
            int day = IntValue(obj[dayKey]);
            return day < 0 ? AwakeLetterConstants.UnreadHour : day * AwakeLetterConstants.HoursPerDay;
        }
        return AwakeLetterConstants.UnreadHour;
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }
}

/// <summary>
/// 信件账本文档（命名空间 <c>awake.letters</c>，键 <c>campaign.letters.v1</c>）的读写与状态机。
/// 与 Onboarding 同范式：命令式写入 + appliedKeys 幂等 + 文档级 schema 守卫。
/// </summary>
internal static class LetterLedgerDocument
{
    internal static JObject NewState()
    {
        return new JObject
        {
            ["schema"] = AwakeStorageContract.LettersSchema,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["letters"] = new JArray(),
            ["appliedKeys"] = new JArray()
        };
    }

    internal static void EnsureShape(JObject state)
    {
        state["schema"] = AwakeStorageContract.LettersSchema;
        if (!(state["letters"] is JArray)) state["letters"] = new JArray();
        if (!(state["appliedKeys"] is JArray)) state["appliedKeys"] = new JArray();
    }

    internal static List<AwakeLetterRecord> ParseLetters(JObject state)
    {
        List<AwakeLetterRecord> result = new List<AwakeLetterRecord>();
        if (state?["letters"] is not JArray array) return result;
        foreach (JToken token in array)
        {
            AwakeLetterRecord record = AwakeLetterRecord.FromJson(token);
            if (record != null) result.Add(record);
        }
        return result;
    }

    /// <summary>命令式状态机。返回非空字符串即失败码（沿用仓储 applyError 约定）。</summary>
    internal static string Apply(JObject state, WorldStateCommand command)
    {
        EnsureShape(state);
        JArray letters = (JArray)state["letters"];
        string op = (string)command.Arguments?["op"] ?? string.Empty;

        if (StringComparer.Ordinal.Equals(op, AwakeLetterConstants.OpAdd))
        {
            string error = ApplyAdd(letters, command);
            if (!string.IsNullOrWhiteSpace(error)) return error;
        }
        else if (StringComparer.Ordinal.Equals(op, AwakeLetterConstants.OpRead))
        {
            ApplyRead(letters, command);
        }
        else if (StringComparer.Ordinal.Equals(op, AwakeLetterConstants.OpReply))
        {
            string error = ApplyReply(letters, command);
            if (!string.IsNullOrWhiteSpace(error)) return error;
        }
        else if (StringComparer.Ordinal.Equals(op, AwakeLetterConstants.OpAdvance))
        {
            ApplyAdvance(letters, command);
        }
        else
        {
            return "awake.world_state.letters.unknown_op";
        }

        Trim(letters, AwakeLetterConstants.MaximumLetters);
        JArray appliedKeys = (JArray)state["appliedKeys"];
        appliedKeys.Add(command.IdempotencyKey);
        Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
        return string.Empty;
    }

    private static string ApplyAdd(JArray letters, WorldStateCommand command)
    {
        AwakeLetterRecord record = AwakeLetterRecord.FromJson(command.Arguments?["letter"]);
        if (record == null || string.IsNullOrWhiteSpace(record.Id)) return "awake.world_state.letters.invalid_letter";
        if (!AwakeLetterConstants.IsValidDirection(record.Direction)) return "awake.world_state.letters.invalid_direction";
        if (FindIndex(letters, record.Id) >= 0) return string.Empty; // 幂等：已存在即视为成功
        letters.Add(record.ToJson());
        return string.Empty;
    }

    private static void ApplyRead(JArray letters, WorldStateCommand command)
    {
        string id = (string)command.Arguments?["id"] ?? string.Empty;
        int hour = IntValue(command.Arguments?["hour"]);
        int index = FindIndex(letters, id);
        if (index < 0) return;
        JObject letter = (JObject)letters[index];
        if (!AwakeLetterConstants.IsUnreadStatus((string)letter["status"])) return;
        letter["status"] = AwakeLetterConstants.StatusRead;
        letter["readHour"] = hour;
        letter["readDay"] = hour / AwakeLetterConstants.HoursPerDay;
    }

    private static string ApplyReply(JArray letters, WorldStateCommand command)
    {
        string inboundId = (string)command.Arguments?["inboundId"] ?? string.Empty;
        AwakeLetterRecord outbound = AwakeLetterRecord.FromJson(command.Arguments?["outbound"]);
        if (outbound == null || string.IsNullOrWhiteSpace(outbound.Id)) return "awake.world_state.letters.invalid_letter";
        outbound.ReplyToId = inboundId;
        if (FindIndex(letters, outbound.Id) < 0) letters.Add(outbound.ToJson());
        int index = FindIndex(letters, inboundId);
        if (index < 0) return string.Empty;
        JObject inbound = (JObject)letters[index];
        inbound["status"] = AwakeLetterConstants.StatusReplied;
        inbound["replyLetterId"] = outbound.Id;
        return string.Empty;
    }

    /// <summary>
    /// 按绝对小时推进：到达判定 <c>hour &gt;= deliverHour</c>；过期判定 <c>hour - createdHour &gt; expiryHours</c>。
    /// 同一函数同时承担"送达 + 过期"，不区分调用来源。
    /// </summary>
    private static void ApplyAdvance(JArray letters, WorldStateCommand command)
    {
        int hour = IntValue(command.Arguments?["hour"]);
        int expiryHours = IntValue(command.Arguments?["expiryHours"]);
        foreach (JToken token in letters)
        {
            if (token is not JObject letter) continue;
            string status = (string)letter["status"] ?? string.Empty;
            if (StringComparer.Ordinal.Equals(status, AwakeLetterConstants.StatusSent)
                && hour >= LetterHour(letter, "deliverHour", "deliverDay"))
            {
                letter["status"] = AwakeLetterConstants.StatusDelivered;
                status = AwakeLetterConstants.StatusDelivered;
            }
            if (expiryHours > 0
                && (StringComparer.Ordinal.Equals(status, AwakeLetterConstants.StatusSent)
                    || StringComparer.Ordinal.Equals(status, AwakeLetterConstants.StatusDelivered))
                && hour - LetterHour(letter, "createdHour", "createdDay") > expiryHours)
            {
                letter["status"] = AwakeLetterConstants.StatusExpired;
            }
        }
    }

    /// <summary>读绝对小时；旧文档只有日字段时按 日 × 24 迁移。</summary>
    private static int LetterHour(JObject letter, string hourKey, string dayKey)
    {
        if (letter[hourKey] != null) return IntValue(letter[hourKey]);
        return IntValue(letter[dayKey]) * AwakeLetterConstants.HoursPerDay;
    }

    private static int FindIndex(JArray letters, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return -1;
        for (int i = 0; i < letters.Count; i++)
        {
            if (letters[i] is JObject candidate && StringComparer.Ordinal.Equals((string)candidate["id"], id)) return i;
        }
        return -1;
    }

    private static void Trim(JArray array, int maximum)
    {
        while (array.Count > maximum) array.RemoveAt(0);
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }
}
