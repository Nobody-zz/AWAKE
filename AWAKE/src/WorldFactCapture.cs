using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class WorldFactEntity
{
    internal string Type { get; }
    internal string Id { get; }
    internal string Role { get; }
    internal WorldFactEntity(string type, string id, string role) { Type = type; Id = id; Role = role; }
}

internal sealed class WorldFact
{
    internal const string Schema = "awake.world_fact.v1";
    internal string FactId { get; }
    internal string EventKey { get; }
    internal int Day { get; }
    internal long TimeSlot { get; }
    internal string Kind { get; }
    internal IReadOnlyList<WorldFactEntity> Entities { get; }
    internal string PresentationSummary { get; }
    internal string PresentationDomain { get; }

    internal WorldFact(string eventKey, int day, long timeSlot, string kind, IEnumerable<WorldFactEntity> entities, string presentationSummary = null, string presentationDomain = null)
    {
        EventKey = eventKey;
        FactId = Hex("awake.world_fact.v1\n" + eventKey);
        Day = day;
        TimeSlot = timeSlot;
        Kind = kind;
        PresentationSummary = presentationSummary ?? string.Empty;
        PresentationDomain = presentationDomain ?? string.Empty;
        Entities = (entities ?? Enumerable.Empty<WorldFactEntity>())
            .Where(x => x != null).OrderBy(x => x.Type, StringComparer.Ordinal)
            .ThenBy(x => x.Id, StringComparer.Ordinal).ThenBy(x => x.Role, StringComparer.Ordinal).ToArray();
    }

    internal JObject ToJson()
    {
        JObject result = new JObject {
            ["schema"] = Schema, ["factId"] = FactId, ["eventKey"] = EventKey,
            ["occurred"] = new JObject { ["campaignDay"] = Day, ["timeSlot"] = TimeSlot },
            ["kind"] = Kind, ["origin"] = "native_game", ["authority"] = "game_confirmed",
            ["initialKnowledgeScope"] = "public",
            ["presentation"] = new JObject { ["summary"] = PresentationSummary, ["domain"] = PresentationDomain }
        };
        JArray entities = new JArray();
        foreach (WorldFactEntity entity in Entities)
            entities.Add(new JObject { ["type"] = entity.Type, ["id"] = entity.Id, ["role"] = entity.Role });
        result["entities"] = entities;
        return result;
    }

    internal static string CreateEventKey(string kind, Action<JObject> append)
    {
        JObject doc = new JObject { ["schema"] = "awake.world_fact.event_key.v1", ["kind"] = kind };
        append(doc);
        return "wf1-" + Hex(doc.ToString(Formatting.None));
    }

    internal static bool IsStableFactId(string eventKey, string factId)
        => !string.IsNullOrWhiteSpace(eventKey) && StringComparer.Ordinal.Equals(factId, Hex("awake.world_fact.v1\n" + eventKey));

    private static string Hex(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            foreach (byte valueByte in bytes) result.Append(valueByte.ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }
}

/// <summary>Reduces live Bannerlord callback arguments before asynchronous persistence.</summary>
internal sealed class WorldFactCapture
{
    internal WorldFact Fact { get; }
    internal int Day => Fact.Day;
    internal string Kind => Fact.Kind;
    internal string Text { get; }
    internal string EventKey => Fact.EventKey;
    private WorldFactCapture(WorldFact fact, string text) { Fact = fact; Text = text; }
    internal static long ToTimeSlot(double days) => double.IsNaN(days) || double.IsInfinity(days) || days < 0d ? -1L : (long)Math.Floor(days * 144d);

    internal static bool TryCreateWar(int day, long slot, string a, string an, string b, string bn, out WorldFactCapture fact)
        => CreateFaction(day, slot, "war_declared", a, an, b, bn, "与", "开战。", out fact);
    internal static bool TryCreatePeace(int day, long slot, string a, string an, string b, string bn, out WorldFactCapture fact)
        => CreateFaction(day, slot, "peace_made", a, an, b, bn, "与", "议和。", out fact);

    private static bool CreateFaction(int day, long slot, string kind, string a, string an, string b, string bn, string join, string suffix, out WorldFactCapture fact)
    {
        fact = null;
        if (!Valid(day, slot, a, an, b, bn) || StringComparer.Ordinal.Equals(a.Trim(), b.Trim())) return false;
        string[] ids = { a.Trim(), b.Trim() }; Array.Sort(ids, StringComparer.Ordinal);
        return Create(day, slot, kind, an.Trim() + join + bn.Trim() + suffix, out fact,
            x => { x["timeSlot"] = slot; x["factionIds"] = new JArray(ids); },
            new WorldFactEntity("faction", ids[0], "party_a"), new WorldFactEntity("faction", ids[1], "party_b"));
    }

    internal static bool TryCreateSettlementOwnerChanged(int day, long slot, string sid, string sn, string oldId, string newId, string nn, out WorldFactCapture fact)
    {
        fact = null; if (!Valid(day, slot, sid, sn, newId, nn)) return false; string oldValue = Optional(oldId);
        return Create(day, slot, "settlement_owner_changed", sn.Trim() + "易主，现由" + nn.Trim() + "控制。", out fact,
            x => { x["timeSlot"] = slot; x["settlementId"] = sid.Trim(); x["previousOwnerHeroId"] = oldValue == null ? JValue.CreateNull() : oldValue; x["newOwnerHeroId"] = newId.Trim(); },
            new WorldFactEntity("settlement", sid.Trim(), "affected"), oldValue == null ? null : new WorldFactEntity("hero", oldValue, "previous_owner"), new WorldFactEntity("hero", newId.Trim(), "new_owner"));
    }

    internal static bool TryCreateHeroKilled(int day, long slot, string victimId, string victimName, string killerId, out WorldFactCapture fact)
    {
        fact = null; if (!Valid(day, slot, victimId, victimName)) return false; string killer = Optional(killerId);
        return Create(day, slot, "hero_killed", victimName.Trim() + "死亡。", out fact,
            x => { x["timeSlot"] = slot; x["victimHeroId"] = victimId.Trim(); x["killerHeroId"] = killer == null ? JValue.CreateNull() : killer; },
            new WorldFactEntity("hero", victimId.Trim(), "subject"), killer == null ? null : new WorldFactEntity("hero", killer, "killer"));
    }

    internal static bool TryCreateHeroKilled(int day, long slot, string victimId, string victimName, out WorldFactCapture fact)
        => TryCreateHeroKilled(day, slot, victimId, victimName, null, out fact);

    internal static bool TryCreateHeroPrisonerReleased(int day, long slot, string prisonerId, string prisonerName, string capturerId, string detail, int detailCode, out WorldFactCapture fact)
    {
        fact = null; if (!Valid(day, slot, prisonerId, prisonerName, detail)) return false; string capturer = Optional(capturerId);
        return Create(day, slot, "hero_prisoner_released", prisonerName.Trim() + "结束囚禁，重获自由。", out fact,
            x => { x["timeSlot"] = slot; x["prisonerHeroId"] = prisonerId.Trim(); x["capturerFactionId"] = capturer == null ? JValue.CreateNull() : capturer; x["releaseDetail"] = detail.Trim(); x["releaseDetailCode"] = detailCode; },
            new WorldFactEntity("hero", prisonerId.Trim(), "prisoner"), capturer == null ? null : new WorldFactEntity("faction", capturer, "capturer"));
    }

    internal static bool TryCreateHeroPrisonerReleased(int day, long slot, string prisonerId, string prisonerName, string capturerId, string detail, out WorldFactCapture fact)
        => TryCreateHeroPrisonerReleased(day, slot, prisonerId, prisonerName, capturerId, detail, 0, out fact);

    private static bool Create(int day, long slot, string kind, string text, out WorldFactCapture fact, Action<JObject> key, params WorldFactEntity[] entities)
    {
        fact = new WorldFactCapture(new WorldFact(WorldFact.CreateEventKey(kind, key), day, slot, kind, entities, text, WeeklyReportService.InferDomain(kind)), text); return true;
    }
    private static string Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool Valid(int day, long slot, params string[] values)
        => day > 0 && slot >= day * 144L && slot < (day + 1L) * 144L && values.All(x => !string.IsNullOrWhiteSpace(x));
}
