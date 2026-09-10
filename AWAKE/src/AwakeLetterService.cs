using System;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace Awake;

internal static class AwakeLetterRequestValidator
{
    internal static string GetRejectionReason(string contactKey, string text)
    {
        if (string.IsNullOrWhiteSpace(contactKey)) return "contact_missing";
        if (string.IsNullOrWhiteSpace(text)) return "text_missing";
        if (System.Text.Encoding.UTF8.GetByteCount(text) > AwakeLetterService.MaximumLetterBytes) return "text_too_long";
        return string.Empty;
    }
}

internal static class AwakeLetterService
{
    internal const int MaximumLetterBytes = 2000;

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
        if (!await AwakeRuntime.EnsureWorldStateReadyAsync(host, cancellationToken, new[] { AiTaskConstants.TranscriptNamespace, AiTaskConstants.ContactsNamespace }).ConfigureAwait(false))
        {
            AwakeLog.Write("letter_send_rejected key=" + contactKey + " reason=storage_not_ready");
            return false;
        }
        string convId = string.IsNullOrWhiteSpace(conversationId)
            ? "letter|" + Guid.NewGuid().ToString("N")
            : conversationId;
        string location = TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement?.Name?.ToString() ?? string.Empty;
        bool sent = await AwakeTranscriptService.AppendLetterAsync(
            contactKey,
            convId,
            AwakeRuntime.CurrentGameDay(),
            location,
            text.Trim(),
            convId,
            cancellationToken).ConfigureAwait(false);
        if (sent)
        {
            AwakeLog.Write("letter_send_succeeded key=" + contactKey + " conversation=" + convId + " source=letter");
        }
        return sent;
    }
}
