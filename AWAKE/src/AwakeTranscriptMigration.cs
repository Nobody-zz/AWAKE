using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class AwakeTranscriptMigration
{
    private static readonly HashSet<string> MigratedContacts = new HashSet<string>(StringComparer.Ordinal);

    internal static async Task<bool> MigrateAsync(CancellationToken cancellationToken)
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return false;
        JObject messenger;
        try
        {
            messenger = await store.GetMessengerAsync(null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("transcript_migration_load_error error=" + ex.Message);
            return false;
        }
        if (messenger?["chats"] is not JObject chats) return false;

        bool any = false;
        foreach (JProperty property in chats.Properties())
        {
            if (property.Value is not JArray lines || lines.Count == 0) continue;
            string canonicalKey = Canonicalize(property.Name);
            if (string.IsNullOrWhiteSpace(canonicalKey) || MigratedContacts.Contains(canonicalKey)) continue;

            List<AwakeTranscriptLine> transcriptLines = new List<AwakeTranscriptLine>();
            int index = 0;
            foreach (JToken token in lines)
            {
                if (token is not JObject line) continue;
                string speaker = (string)line["speaker"] ?? string.Empty;
                string text = (string)line["text"] ?? string.Empty;
                int day = IntValue(line["day"]);
                string kind = IsPlayerSpeaker(speaker) ? "player" : "npc";
                transcriptLines.Add(new AwakeTranscriptLine(
                    "legacy:" + canonicalKey + ":" + index,
                    day,
                    string.Empty,
                    speaker,
                    text,
                    "messenger",
                    string.Empty,
                    kind));
                index++;
            }
            if (transcriptLines.Count == 0) continue;

            const int batchSize = 50;
            bool allAppended = true;
            for (int batch = 0; batch < (transcriptLines.Count + batchSize - 1) / batchSize; batch++)
            {
                int start = batch * batchSize;
                int count = Math.Min(batchSize, transcriptLines.Count - start);
                bool appended = await store.AppendTranscriptLinesAsync(
                    canonicalKey,
                    0,
                    transcriptLines.GetRange(start, count),
                    "migrate:" + canonicalKey + ":" + batch,
                    cancellationToken).ConfigureAwait(false);
                if (!appended)
                {
                    AwakeLog.Write("transcript_migration_append_failed key=" + canonicalKey + " batch=" + batch);
                    allAppended = false;
                    break;
                }
            }
            if (!allAppended) continue;
            await store.EnsureContactAsync(
                canonicalKey,
                "migrate-contact:" + canonicalKey,
                cancellationToken).ConfigureAwait(false);
            MigratedContacts.Add(canonicalKey);
            any = true;
        }
        return any;
    }

    private static string Canonicalize(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return string.Empty;
        if (targetId.StartsWith("hero:", StringComparison.Ordinal))
        {
            return targetId;
        }
        if (targetId.StartsWith("npc:", StringComparison.Ordinal))
        {
            string rest = targetId.Substring(4);
            int agentMarker = rest.IndexOf(":a", StringComparison.Ordinal);
            return agentMarker > 0 ? "npc:" + rest.Substring(0, agentMarker) : "npc:" + rest;
        }
        return "hero:" + targetId;
    }

    private static bool IsPlayerSpeaker(string speaker)
    {
        return StringComparer.Ordinal.Equals(speaker, "你")
            || StringComparer.Ordinal.Equals(speaker, "Player")
            || StringComparer.OrdinalIgnoreCase.Equals(speaker, "You");
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }

    internal static void ResetForTesting()
    {
        MigratedContacts.Clear();
    }
}
