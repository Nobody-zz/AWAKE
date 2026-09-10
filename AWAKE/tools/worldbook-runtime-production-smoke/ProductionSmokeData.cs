using System;
using Newtonsoft.Json.Linq;

namespace Awake.WorldbookRuntimeProductionSmoke;

internal static partial class Program
{
    private static WorldKnowledgeQueryService BindKnowledge()
    {
        WorldKnowledgeSnapshot snapshot = new WorldKnowledgeSnapshot
        {
            PackageId = "production-smoke",
            Version = "1",
            WorldId = "production-smoke-world",
            SchemaVersion = "awake.worldbook.v2"
        };
        foreach (string identityId in new[]
        {
            "awake:identity:headman",
            "awake:identity:merchant",
            "awake:identity:tavernkeeper",
            "awake:identity:ransom_broker"
        })
        {
            snapshot.Identities[identityId] = new WorldKnowledgeIdentity
            {
                Id = identityId,
                DisplayName = identityId
            };
        }
        WorldKnowledgeQueryService query = new WorldKnowledgeQueryService(snapshot);
        WorldEventServices.BindKnowledge(snapshot, query);
        return query;
    }

    private static JObject BuildWorldEventsState(string eventId, string eventKey, int day, string text)
    {
        return new JObject
        {
            ["schema"] = AwakeStorageContract.WorldEventsSchema,
            ["records"] = new JArray
            {
                new JObject
                {
                    ["id"] = eventId,
                    ["eventKey"] = eventKey,
                    ["day"] = day,
                    ["kind"] = "war",
                    ["domain"] = "war",
                    ["text"] = text,
                    ["occurredAt"] = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(day).ToString("O"),
                    ["visibilityIdentityIds"] = new JArray("awake:identity:headman")
                }
            },
            ["weeklyReports"] = new JArray(),
            ["appliedKeys"] = new JArray(),
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O")
        };
    }

    private static WorldStateCommand CreateCommand(string key)
    {
        return new WorldStateCommand(
            AiTaskConstants.WorldEventsNamespace,
            AiTaskConstants.WorldEventsKey,
            "production-smoke.command",
            key,
            string.Empty,
            WorldStateKind.WorldEvents,
            new JObject { ["operation"] = "production-smoke" },
            DateTimeOffset.UtcNow,
            "production-smoke");
    }
}
