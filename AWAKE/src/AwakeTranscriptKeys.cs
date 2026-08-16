using System;
using System.Text;

namespace Awake;

internal static class AwakeTranscriptKeys
{
    internal const string ContactsKey = "campaign.contacts.v1";
    internal const string AuditKey = "campaign.history.audit.v1";

    internal static string EncodeContactKey(string canonicalContactKey)
    {
        if (string.IsNullOrWhiteSpace(canonicalContactKey)) return "unknown";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(canonicalContactKey))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    internal static string TranscriptChunkKey(string canonicalContactKey, int chunkIndex)
    {
        return "campaign.transcript.v1." + EncodeContactKey(canonicalContactKey) + "." + chunkIndex;
    }

    internal static string TranscriptMetaKey(string canonicalContactKey)
    {
        return "campaign.transcript.meta.v1." + EncodeContactKey(canonicalContactKey);
    }
}
