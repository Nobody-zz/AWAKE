using System;
using System.Collections.Generic;
using System.Linq;
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
        snapshot.Identities["hero:send"] = new WorldKnowledgeIdentity { Id = "hero:send", DisplayName = "发送测试" };
        snapshot.Identities["awake:identity:commoner"] = new WorldKnowledgeIdentity { Id = "awake:identity:commoner", DisplayName = "平民" };
        WorldKnowledgeEntry entry = new WorldKnowledgeEntry
        {
            Id = "smoke:dialogue:known",
            Domain = "smoke",
            Title = "测试近况",
            Summary = "测试领地近日平静。"
        };
        WorldKnowledgeExpression expression = new WorldKnowledgeExpression
        {
            Id = "smoke:dialogue:known:expression",
            Detail = "rumor",
            Text = "测试领地近日平静。"
        };
        expression.Grants.Add(new WorldKnowledgeRule { IdentityId = "awake:identity:public", Scope = "local", MinDetail = "rumor" });
        entry.Expressions.Add(expression);
        entry.Keywords.Add("近况");
        RegisterStaticEntry(snapshot, entry);
        WorldKnowledgeQueryService query = new WorldKnowledgeQueryService(snapshot);
        WorldEventServices.BindKnowledge(snapshot, query);
        Check(query.Search("近况", 10).Count == 1,
            "a static worldbook entry must stay queryable after the projection bind");
        return query;
    }

    // 世界书条目必须按“静态条目”注册，等同 WorldKnowledgeLoader.LoadEntries + BuildKeywordIndex。
    // dynamic 槽归 WorldKnowledgeProjectionService 独占：BindKnowledge → Projection.Bind → ApplyLocked
    // 会拿 BuildEntriesLocked() 的结果做全量替换。夹具若先把条目塞进 dynamic 槽再 Bind，条目会被空表
    // 清掉，查询随即零候选（match=identity / not_found），看上去像授权故障而不是夹具问题。
    // 另：expression 的 Detail 必须是合法档位（rumor / summary / detail / secret），
    // 写成别的值 DetailRank 返回 -1，会被判成 blocked/permission。
    private static void RegisterStaticEntry(WorldKnowledgeSnapshot snapshot, WorldKnowledgeEntry entry)
    {
        snapshot.Entries[entry.Id] = entry;
        foreach (string keyword in entry.Keywords)
        {
            if (!snapshot.KeywordIndex.TryGetValue(keyword, out List<string> ids))
                snapshot.KeywordIndex[keyword] = ids = new List<string>();
            if (!ids.Contains(entry.Id)) ids.Add(entry.Id);
        }
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

    private static void SeedWorldFactJournal(ProductionSmokeHost host, IEnumerable<WorldFactCapture> facts)
    {
        ProductionSmokeKeyValueStore journal = host.StorageAdapter.SetNamespace(AiTaskConstants.WorldFactJournalNamespace);
        JObject chunk = WorldFactJournalCodec.BuildChunk(1, 7, 1, facts == null
            ? Array.Empty<JObject>()
            : facts.Where(value => value != null).Select(value => value.Fact.ToJson()));
        string chunkJson = chunk.ToString(Newtonsoft.Json.Formatting.None);
        string chunkKey = "facts-00000001-00000007-r00000001-0000-" + WorldFactJournalCodec.Sha256Hex(chunkJson);
        journal.Put(chunkKey, chunkJson);
        journal.Put(
            AiTaskConstants.WorldFactJournalRootKey,
            WorldFactJournalCodec.BuildRoot(1, 7, 1, new[] { chunkKey }).ToString(Newtonsoft.Json.Formatting.None));
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
