using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Awake;

/// <summary>
/// 「发信」这一步的两步编排：① 正文落 transcript ② 账本落记录。
/// <para>
/// **为什么单独成件**：<see cref="AwakeLetterService"/> 经 AwakeMessengerService → NpcDialogueLauncher
/// 触及 Gauntlet UI overlay，所以它不在 <c>AWAKE.Tests.csproj</c> 的显式清单里；
/// 而**半提交恰好发生在这两步之间**，判据原本无处可写。本件不引用任何 TaleWorlds 类型、
/// 不碰 UI／战役／宿主，可整体编进离线验台（不带替身跑真编排）。
/// </para>
/// <para>
/// **口径（2026-09-22 已定）**：账本为权威（界面读账本），正文是内容载体。
/// 半提交时正文孤儿无害，但必须可清理；**不得因正文存在就报发送成功**；
/// 且**不许静默部分成功**——项目口径「静默成功比抛错危险」。
/// </para>
/// </summary>
internal static class AwakeLetterCommit
{
    internal const string TranscriptWriteFailed = "transcript_write_failed";
    internal const string LedgerWriteFailed = "ledger_write_failed";

    /// <summary>两步编排的结果。**发送成功只以 <see cref="LedgerRecorded"/> 为准。**</summary>
    internal sealed class LetterCommitResult
    {
        internal string LetterId = string.Empty;
        internal string IdempotencyKey = string.Empty;

        /// <summary>① 正文是否已落 transcript。</summary>
        internal bool TranscriptWritten;

        /// <summary>② 账本是否已落记录。界面读账本 ⇒ 只有它为真才等于"信发出去了"。</summary>
        internal bool LedgerRecorded;

        /// <summary>①真②假 ＝ 半提交：正文成了孤儿，需要清理线索。</summary>
        internal bool HalfCommitted;

        /// <summary>账本里留下了 <c>failed</c> 留痕（正文没落盘那条路）。</summary>
        internal bool FailureRecorded;

        internal string FailureReason = string.Empty;
    }

    /// <summary>
    /// 先写正文、再写账本。两步之间没有事务，所以第 2 步失败会留下"正文在、账本没有"的半提交。
    /// 本方法不掩盖它：如实返回，并**留下可查痕迹**。
    /// </summary>
    internal static async Task<LetterCommitResult> WriteOutboundAsync(
        string contactKey,
        string conversationId,
        string idempotencyKey,
        int day,
        string location,
        string text,
        string sender,
        string recipient,
        int createdHour,
        int deliverHour,
        string replyToId,
        CancellationToken cancellationToken)
    {
        AwakeLetterRecord record = new AwakeLetterRecord
        {
            Id = AwakeLetterConstants.NewLetterId(idempotencyKey),
            ContactKey = contactKey,
            Direction = AwakeLetterConstants.Outbound,
            Sender = sender,
            Recipient = recipient,
            CreatedHour = createdHour,
            DeliverHour = deliverHour,
            Status = AwakeLetterConstants.StatusSent,
            ReplyToId = replyToId ?? string.Empty
        };

        LetterCommitResult result = new LetterCommitResult
        {
            LetterId = record.Id,
            IdempotencyKey = idempotencyKey ?? string.Empty
        };

        bool appended = await AwakeTranscriptService.AppendLetterAsync(
            contactKey,
            conversationId,
            day,
            location,
            text,
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (!appended)
        {
            result.FailureReason = TranscriptWriteFailed;
            result.FailureRecorded = await RecordFailedAsync(
                contactKey,
                record.Id,
                AwakeLetterConstants.Outbound,
                sender,
                recipient,
                createdHour,
                deliverHour,
                TranscriptWriteFailed,
                idempotencyKey,
                cancellationToken).ConfigureAwait(false);
            return result;
        }

        result.TranscriptWritten = true;

        bool recorded = await RecordAsync(
            AddArguments(record),
            "letter-add|" + idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (recorded)
        {
            result.LedgerRecorded = true;
            return result;
        }

        // 半提交：正文已落、账本没落。除了"不报成功"，**必须留下可查痕迹**——
        // 这条日志里的 letterId 与幂等键是那具孤儿正文唯一定位／清理的门牌。
        // 没有它这一步就是静默的：正文躺在 transcript 里、账本干干净净、谁也不知道。
        result.HalfCommitted = true;
        result.FailureReason = LedgerWriteFailed;
        AwakeLog.Write("letter_send_half_committed key=" + (contactKey ?? "null")
            + " letter=" + result.LetterId
            + " idem=" + result.IdempotencyKey
            + " transcript=written ledger=missing reason=" + LedgerWriteFailed);
        return result;
    }

    /// <summary>正文没能落盘时的账本留痕：只有生命周期，没有正文（审计用）。</summary>
    internal static Task<bool> RecordFailedAsync(
        string contactKey,
        string letterId,
        string direction,
        string sender,
        string recipient,
        int createdHour,
        int deliverHour,
        string failureReason,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return RecordAsync(
            AddArguments(new AwakeLetterRecord
            {
                Id = letterId,
                ContactKey = contactKey,
                Direction = direction,
                Sender = sender,
                Recipient = recipient,
                CreatedHour = createdHour,
                DeliverHour = deliverHour,
                Status = AwakeLetterConstants.StatusFailed,
                FailureReason = failureReason
            }),
            "letter-failed|" + idempotencyKey,
            cancellationToken);
    }

    internal static JObject AddArguments(AwakeLetterRecord record)
    {
        return new JObject
        {
            ["op"] = AwakeLetterConstants.OpAdd,
            ["letter"] = record.ToJson()
        };
    }

    internal static async Task<bool> RecordAsync(JObject arguments, string idempotencyKey, CancellationToken cancellationToken)
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return false;
        return await store.UpdateLettersAsync(arguments, idempotencyKey, cancellationToken).ConfigureAwait(false);
    }
}
