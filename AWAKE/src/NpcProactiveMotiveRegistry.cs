using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class NpcProactiveMotiveDefinition
{
    internal string Id { get; set; } = string.Empty;
    internal string DisplayName { get; set; } = string.Empty;
    internal int BaseWeight { get; set; } = 1;
    internal string OpeningHint { get; set; } = string.Empty;
    internal int MinAffinity { get; set; } = -100;
    internal int MaxAffinity { get; set; } = 100;
}

internal static class NpcProactiveMotiveRegistry
{
    private static readonly object Gate = new object();
    private static readonly Dictionary<string, NpcProactiveMotiveDefinition> Motives =
        new Dictionary<string, NpcProactiveMotiveDefinition>(StringComparer.Ordinal);

    internal static bool Register(NpcProactiveMotiveDefinition definition)
    {
        string error;
        if (!Validate(definition, out error))
        {
            AwakeLog.Write("npc_proactive_motive_invalid id=" + (definition?.Id ?? "null") + " error=" + error);
            return false;
        }
        lock (Gate)
        {
            Motives[definition.Id] = definition;
            return true;
        }
    }

    internal static IReadOnlyList<NpcProactiveMotiveDefinition> All()
    {
        lock (Gate)
        {
            return new List<NpcProactiveMotiveDefinition>(Motives.Values);
        }
    }

    internal static bool TryGet(string id, out NpcProactiveMotiveDefinition definition)
    {
        lock (Gate)
        {
            return Motives.TryGetValue(id ?? string.Empty, out definition);
        }
    }

    internal static bool Validate(NpcProactiveMotiveDefinition definition, out string error)
    {
        error = null;
        if (definition == null)
        {
            error = "null";
            return false;
        }
        if (string.IsNullOrWhiteSpace(definition.Id) || definition.Id.Length > 60)
        {
            error = "id";
            return false;
        }
        if (definition.BaseWeight < 1 || definition.BaseWeight > 100)
        {
            error = "baseWeight";
            return false;
        }
        if (definition.MinAffinity > definition.MaxAffinity)
        {
            error = "affinity_range";
            return false;
        }
        return true;
    }

    internal static void LoadFromRuleRegistry()
    {
        AwakeRuleRegistry.EnsureLoaded();
        foreach (AwakeRuleManifest manifest in AwakeRuleRegistry.All())
        {
            if (manifest == null || !manifest.Enabled) continue;
            JObject payload = manifest.Payload;
            if (payload == null || !StringComparer.Ordinal.Equals((string)payload["kind"], "proactive_motive")) continue;
            NpcProactiveMotiveDefinition definition = Parse(payload);
            if (definition != null) Register(definition);
        }
    }

    internal static void ResetForTesting()
    {
        lock (Gate)
        {
            Motives.Clear();
        }
    }

    private static NpcProactiveMotiveDefinition Parse(JObject payload)
    {
        try
        {
            return new NpcProactiveMotiveDefinition
            {
                Id = (string)payload["id"] ?? string.Empty,
                DisplayName = (string)payload["displayName"] ?? string.Empty,
                BaseWeight = IntValue(payload["baseWeight"], 1),
                OpeningHint = (string)payload["openingHint"] ?? string.Empty,
                MinAffinity = IntValue(payload["minAffinity"], -100),
                MaxAffinity = IntValue(payload["maxAffinity"], 100)
            };
        }
        catch
        {
            return null;
        }
    }

    private static int IntValue(JToken token, int fallback)
    {
        if (token == null || token.Type != JTokenType.Integer) return fallback;
        try { return (int)token; } catch { return fallback; }
    }
}
