using System;
using System.Threading;
using System.Threading.Tasks;

namespace Awake;

internal static class AwakeLetterService
{
    internal const int MaximumLetterBytes = 2000;

    internal static async Task<bool> SendAsync(
        string contactKey,
        string conversationId,
        string text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contactKey)
            || string.IsNullOrWhiteSpace(text)
            || System.Text.Encoding.UTF8.GetByteCount(text) > MaximumLetterBytes)
        {
            AwakeLog.Write("letter_send_rejected key=" + (contactKey ?? "null"));
            return false;
        }
        string convId = string.IsNullOrWhiteSpace(conversationId)
            ? "letter|" + Guid.NewGuid().ToString("N")
            : conversationId;
        string location = TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement?.Name?.ToString() ?? string.Empty;
        return await AwakeTranscriptService.AppendLetterAsync(
            contactKey,
            convId,
            AwakeRuntime.CurrentGameDay(),
            location,
            text,
            convId,
            cancellationToken).ConfigureAwait(false);
    }
}
