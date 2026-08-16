using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class AwakeTranscriptConstants
{
    internal const string Schema = "awake.transcript.v1";
    internal const string MetaSchema = "awake.transcript.meta.v1";
    internal const string ContactsSchema = "awake.contacts.v1";
    internal const string AuditSchema = "awake.history.audit.v1";

    internal const int MaximumLinesPerContact = 200;
    internal const int MaximumPinnedLines = 20;
    internal const int MaximumTextUtf8Bytes = 1200;
    internal const int MaximumChunkUtf8Bytes = 128 * 1024;
    internal const int MaximumAuditEntries = 1000;

    internal static readonly string[] ValidSources = new[]
    {
        "messenger", "scene", "encounter", "event", "proactive", "letter", "system", "map", "dev_test"
    };

    internal static readonly string[] ValidKinds = new[]
    {
        "player", "npc", "system", "failed", "hidden"
    };
}

internal sealed class AwakeTranscriptLine
{
    internal string Id { get; }
    internal int Day { get; }
    internal string Location { get; }
    internal string Speaker { get; }
    internal string Text { get; }
    internal string Source { get; }
    internal string ConversationId { get; }
    internal string Kind { get; }
    internal bool IsPinned { get; set; }
    internal int ChunkIndex { get; set; } = -1;

    internal AwakeTranscriptLine(
        string id,
        int day,
        string location,
        string speaker,
        string text,
        string source,
        string conversationId,
        string kind)
    {
        Id = id ?? string.Empty;
        Day = day;
        Location = location ?? string.Empty;
        Speaker = speaker ?? string.Empty;
        Text = text ?? string.Empty;
        Source = source ?? string.Empty;
        ConversationId = conversationId ?? string.Empty;
        Kind = kind ?? string.Empty;
    }

    internal JObject ToJson()
    {
        return new JObject
        {
            ["id"] = Id,
            ["day"] = Day,
            ["location"] = Location,
            ["speaker"] = Speaker,
            ["text"] = Text,
            ["source"] = Source,
            ["conversationId"] = ConversationId,
            ["kind"] = Kind
        };
    }

    internal static AwakeTranscriptLine FromJson(JToken token)
    {
        if (token is not JObject obj) return null;
        return new AwakeTranscriptLine(
            (string)obj["id"] ?? string.Empty,
            IntValue(obj["day"]),
            (string)obj["location"] ?? string.Empty,
            (string)obj["speaker"] ?? string.Empty,
            (string)obj["text"] ?? string.Empty,
            (string)obj["source"] ?? string.Empty,
            (string)obj["conversationId"] ?? string.Empty,
            (string)obj["kind"] ?? string.Empty);
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }
}

internal static class AwakeTranscriptValidator
{
    internal static bool IsValidSource(string source)
    {
        return Array.IndexOf(AwakeTranscriptConstants.ValidSources, source ?? string.Empty) >= 0;
    }

    internal static bool IsValidKind(string kind)
    {
        return Array.IndexOf(AwakeTranscriptConstants.ValidKinds, kind ?? string.Empty) >= 0;
    }

    internal static bool ValidateLine(AwakeTranscriptLine line, out string error)
    {
        error = string.Empty;
        if (line == null)
        {
            error = "line";
            return false;
        }
        if (string.IsNullOrWhiteSpace(line.Id) || line.Id.Length > 120)
        {
            error = "id";
            return false;
        }
        if (line.Day < 0)
        {
            error = "day";
            return false;
        }
        if (!IsValidSource(line.Source))
        {
            error = "source";
            return false;
        }
        if (!IsValidKind(line.Kind))
        {
            error = "kind";
            return false;
        }
        if (System.Text.Encoding.UTF8.GetByteCount(line.Text) > AwakeTranscriptConstants.MaximumTextUtf8Bytes)
        {
            error = "text_too_large";
            return false;
        }
        if (System.Text.Encoding.UTF8.GetByteCount(line.Speaker) > 240)
        {
            error = "speaker_too_large";
            return false;
        }
        return true;
    }
}

internal sealed class AwakeTranscriptMeta
{
    internal int LastChunkIndex { get; set; } = -1;
    internal int NextChunkIndex { get; set; }
    internal List<string> PinnedIds { get; } = new List<string>();

    internal JObject ToJson()
    {
        return new JObject
        {
            ["schema"] = AwakeTranscriptConstants.MetaSchema,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["lastChunk"] = LastChunkIndex,
            ["nextChunk"] = NextChunkIndex,
            ["pinnedIds"] = new JArray(PinnedIds),
            ["appliedKeys"] = new JArray()
        };
    }

    internal static AwakeTranscriptMeta FromJson(JObject obj)
    {
        AwakeTranscriptMeta meta = new AwakeTranscriptMeta
        {
            LastChunkIndex = IntValue(obj?["lastChunk"]),
            NextChunkIndex = IntValue(obj?["nextChunk"])
        };
        if (obj?["pinnedIds"] is JArray pinned)
        {
            foreach (JToken token in pinned)
            {
                string id = (string)token;
                if (!string.IsNullOrWhiteSpace(id)) meta.PinnedIds.Add(id);
            }
        }
        return meta;
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return -1;
        try { return (int)token; } catch { return -1; }
    }
}
