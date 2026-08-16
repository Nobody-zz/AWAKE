using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class AwakeTranscriptService
{
    internal static async Task<bool> AppendTurnAsync(
        string contactKey,
        string conversationId,
        int day,
        string location,
        string playerText,
        string npcText,
        string npcName,
        string source,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null
            || string.IsNullOrWhiteSpace(contactKey)
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || !AwakeTranscriptValidator.IsValidSource(source))
        {
            AwakeLog.Write("transcript_turn_rejected key=" + (contactKey ?? "null") + " source=" + (source ?? "null"));
            return false;
        }

        string prefix = "turn|" + idempotencyKey;
        AwakeTranscriptLine playerLine = new AwakeTranscriptLine(
            prefix + "|p",
            day,
            location ?? string.Empty,
            AwakeLocalization.Resolve("awake.ui.you", "你"),
            playerText ?? string.Empty,
            source,
            conversationId ?? string.Empty,
            "player");
        AwakeTranscriptLine npcLine = new AwakeTranscriptLine(
            prefix + "|n",
            day,
            location ?? string.Empty,
            string.IsNullOrWhiteSpace(npcName)
                ? AwakeLocalization.Resolve("awake.scene_shout.speaker", "附近的人们")
                : npcName,
            npcText ?? string.Empty,
            source,
            conversationId ?? string.Empty,
            "npc");

        string error;
        if (!AwakeTranscriptValidator.ValidateLine(playerLine, out error)
            || !AwakeTranscriptValidator.ValidateLine(npcLine, out error))
        {
            return false;
        }

        bool appended = await store.AppendTranscriptLinesAsync(
            contactKey,
            0,
            new[] { playerLine, npcLine },
            idempotencyKey + ":turn",
            cancellationToken).ConfigureAwait(false);
        if (!appended) return false;
        await store.EnsureContactAsync(contactKey, idempotencyKey + ":contact", cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal static async Task<bool> AppendLetterAsync(
        string contactKey,
        string conversationId,
        int day,
        string location,
        string text,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null
            || string.IsNullOrWhiteSpace(contactKey)
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || string.IsNullOrWhiteSpace(text))
        {
            AwakeLog.Write("letter_rejected key=" + (contactKey ?? "null"));
            return false;
        }
        AwakeTranscriptLine line = new AwakeTranscriptLine(
            "letter|" + idempotencyKey,
            day,
            location ?? string.Empty,
            AwakeLocalization.Resolve("awake.ui.you", "你"),
            text,
            "letter",
            conversationId ?? string.Empty,
            "player");
        string error;
        if (!AwakeTranscriptValidator.ValidateLine(line, out error))
        {
            AwakeLog.Write("letter_invalid key=" + contactKey + " error=" + error);
            return false;
        }
        bool appended = await store.AppendTranscriptLinesAsync(
            contactKey,
            0,
            new[] { line },
            idempotencyKey + ":letter",
            cancellationToken).ConfigureAwait(false);
        if (!appended) return false;
        await store.EnsureContactAsync(contactKey, idempotencyKey + ":contact", cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal static async Task<List<AwakeTranscriptLine>> GetHistoryAsync(
        string contactKey,
        CancellationToken cancellationToken)
    {
        List<AwakeTranscriptLine> result = new List<AwakeTranscriptLine>();
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null || string.IsNullOrWhiteSpace(contactKey)) return result;
        for (int chunkIndex = 0; chunkIndex < 64; chunkIndex++)
        {
            JObject chunk = await store.GetTranscriptChunkAsync(
                contactKey,
                chunkIndex,
                null,
                cancellationToken).ConfigureAwait(false);
            if (chunk == null) break;
            if (chunk["entries"] is JArray entries)
            {
                HashSet<string> pinnedIds = new HashSet<string>(StringComparer.Ordinal);
                if (chunk["pinnedIds"] is JArray pinned)
                {
                    foreach (JToken token in pinned)
                    {
                        string id = (string)token;
                        if (!string.IsNullOrWhiteSpace(id)) pinnedIds.Add(id);
                    }
                }
                foreach (JToken token in entries)
                {
                    AwakeTranscriptLine line = AwakeTranscriptLine.FromJson(token);
                    if (line != null)
                    {
                        line.IsPinned = pinnedIds.Contains(line.Id);
                        line.ChunkIndex = chunkIndex;
                        result.Add(line);
                    }
                    if (result.Count >= AwakeTranscriptConstants.MaximumLinesPerContact) break;
                }
            }
            if (result.Count >= AwakeTranscriptConstants.MaximumLinesPerContact) break;
        }
        return result;
    }

    internal static async Task<bool> PinLineAsync(
        string contactKey,
        int chunkIndex,
        string lineId,
        bool pin,
        CancellationToken cancellationToken)
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null || string.IsNullOrWhiteSpace(contactKey) || string.IsNullOrWhiteSpace(lineId)) return false;
        return await store.PinTranscriptAsync(
            contactKey,
            chunkIndex,
            lineId,
            pin,
            "pin|" + lineId + "|" + pin,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<List<string>> LoadContactKeysAsync(CancellationToken cancellationToken)
    {
        List<string> result = new List<string>();
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return result;
        JObject contacts = await store.GetContactsAsync(null, cancellationToken).ConfigureAwait(false);
        if (contacts?["contacts"] is JArray keys)
        {
            foreach (JToken token in keys)
            {
                string key = (string)token;
                if (!string.IsNullOrWhiteSpace(key)) result.Add(key);
            }
        }
        return result;
    }
}
