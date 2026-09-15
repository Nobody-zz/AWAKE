using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Awake;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-runtime-smoke", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var manifest = new JObject
    {
        ["schemaVersion"] = "awake.worldbook.v2",
        ["packageId"] = "calradia:base",
        ["version"] = "1.0.0",
        ["kind"] = "universe",
        ["displayName"] = new JObject { ["zh-CN"] = "卡拉迪亚" },
        ["worldId"] = "calradia:world:calradia",
        ["entrypoints"] = new JObject { ["runtime"] = "runtime.json", ["index"] = "index.json" },
        ["hashes"] = new JObject()
    };
    var runtime = new JObject
    {
        ["schemaVersion"] = "awake.worldbook.v2",
        ["packageId"] = "calradia:base",
        ["version"] = "1.0.0",
        ["worldId"] = "calradia:world:calradia",
        ["revision"] = 1,
        ["entries"] = new JArray
        {
            new JObject
            {
                ["id"] = "calradia:entry:grain_tax",
                ["domain"] = "economy",
                ["title"] = new JObject { ["zh-CN"] = "粮税" },
                ["summary"] = new JObject { ["zh-CN"] = "收税时节。" },
                ["keywords"] = new JArray("粮税", "谷物"),
                ["expressions"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = "calradia:expression:grain_tax_summary",
                        ["detail"] = "summary",
                        ["text"] = new JObject { ["zh-CN"] = "农民知道收税的时节与大致份额。" },
                        ["grants"] = new JArray(new JObject { ["identity_id"] = "awake:identity:commoner", ["scope"] = "local", ["min_detail"] = "summary" }),
                        ["denies"] = new JArray()
                    }
                }
            }
        },
        ["identities"] = new JArray(new JObject { ["id"] = "awake:identity:commoner", ["displayName"] = new JObject { ["zh-CN"] = "平民" }, ["basePriority"] = 0 }),
        ["referrals"] = new JArray(),
        ["indexes"] = new JObject { ["keywordToEntryIds"] = new JObject { ["粮税"] = new JArray("calradia:entry:grain_tax") }, ["domainToEntryIds"] = new JObject() }
    };
    File.WriteAllText(Path.Combine(root, "manifest.json"), manifest.ToString(Newtonsoft.Json.Formatting.None));
    File.WriteAllText(Path.Combine(root, "runtime.json"), runtime.ToString(Newtonsoft.Json.Formatting.None));
    File.WriteAllText(Path.Combine(root, "index.json"), "{}");

    var snapshot = WorldKnowledgeLoader.Load(Path.Combine(root, "manifest.json"));
    var service = new WorldKnowledgeQueryService(snapshot);
    var known = service.Query(new WorldbookQuery { IdentityId = "profile.commoner", KnowledgeScope = "local", KnowledgeScopeAvailable = true, EffectiveDetail = "summary", EffectiveDetailAvailable = true, PlayerText = "粮税", MaximumBytes = 4096 });
    Require(known.State == "partial" && known.RetrievedText.Contains("收税的时节", StringComparison.Ordinal), "identity query did not return permitted expression");
    Require(service.TryApplyOverlay("replace_summary", "calradia:entry:grain_tax", "玩家改写摘要", 0, "smoke", out var firstError), firstError);
    var edited = service.Query(new WorldbookQuery { IdentityId = "profile.commoner", KnowledgeScope = "local", KnowledgeScopeAvailable = true, EffectiveDetail = "summary", EffectiveDetailAvailable = true, RequestedDetail = "summary", PlayerText = "粮税", MaximumBytes = 4096 });
    Require(edited.RetrievedText.Contains("玩家改写摘要", StringComparison.Ordinal), "overlay summary edit was not visible to query");
    Require(!service.TryApplyOverlay("replace_summary", "calradia:entry:grain_tax", "冲突修改", 0, "smoke", out var conflict) && conflict == "WB2-OVERLAY-CAS", "CAS conflict was not rejected");
    var exported = service.ExportOverlay();
    Require(exported["revision"]?.Value<int>() == 1 && exported["operations"] is JArray operations && operations.Count == 1, "overlay export is incomplete");
    var imported = new WorldKnowledgeQueryService(WorldKnowledgeLoader.Load(Path.Combine(root, "manifest.json")));
    Require(imported.TryImportOverlay(exported, out var importError), importError);
    Require(imported.ExportOverlay()["revision"]?.Value<int>() == 1, "overlay import did not replay revision");
    TestFixedFixturePackage();
    TestIdentityAccess();
    TestIdentityCapabilityRules();
    TestCanonicalEntityIds();
    TestPackageIntegrity();
    TestPermissionStateMachine();
    TestRegistrySelection();
    TestRepositoryPackageForm();
    TestWeeklyReport();
    TestWorldFactCapture();
    TestWorldFactJournalCodec();
    TestWorldFactQuery();
    RunWorldFactContextInterfaceStateMatrix();
    RunWorldFactContextInterfaceProvenance();
    RunWorldFactContextInterfaceLegacyFallback();
    RunWorldFactContextInterfaceCancellation();
    RunAwakeEventFactTriggerEvaluation();
    TestMinimalWeeklyReport();
    TestWorldReportV2Build();
    TestWorldReportV2PersistenceMinimal();
    TestWorldEventLedger();
    TestEventContractParity();
    TestDynamicKnowledgeProjection();
    TestEventPersistenceFailures();
    TestCampaignIsolation();
    TestWeeklyReportCatchUp();
    TestKnowledgeReadinessAndStoreBoundary();
    TestWorldFactCollectorWiring();
    TestProductionReadinessGateWiring();
    Console.WriteLine("PASS: runtime loader/query/overlay/identity/registry/weekly-report/event-ledger smoke");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void TestWorldFactCapture()
{
    Require(WorldFactCapture.ToTimeSlot(2d) == 288L && WorldFactCapture.ToTimeSlot(-1d) == -1L, "world fact time-slot normalization failed");

    Require(WorldFactCapture.TryCreateWar(10, 1441, "b", "乙国", "a", "甲国", out WorldFactCapture war)
        && war.Kind == "war_declared"
        && war.Text == "乙国与甲国开战。"
        && war.EventKey.StartsWith("wf1-", StringComparison.Ordinal)
        && war.Fact.Entities.Count == 2
        && war.Fact.FactId == new WorldFact(war.EventKey, 10, 1441, war.Kind, war.Fact.Entities).FactId
        && war.Fact.Entities[0].Id == "a" && war.Fact.Entities[1].Id == "b"
        && WeeklyReportService.InferDomain(war.Kind) == "politics", "war capture/domain mapping failed");
    Require(WorldFactCapture.TryCreatePeace(10, 1442, "a", "甲国", "b", "乙国", out WorldFactCapture peace)
        && peace.EventKey.StartsWith("wf1-", StringComparison.Ordinal)
        && WeeklyReportService.InferDomain(peace.Kind) == "politics", "peace capture/domain mapping failed");
    Require(WorldFactCapture.TryCreateSettlementOwnerChanged(10, 1443, "town_A", "阿卡拉特", "old_lord", "new_lord", "新领主", out WorldFactCapture settlement)
        && settlement.EventKey.StartsWith("wf1-", StringComparison.Ordinal)
        && settlement.Fact.Entities.Any(x => x.Id == "old_lord" && x.Role == "previous_owner")
        && settlement.Text == "阿卡拉特易主，现由新领主控制。"
        && WeeklyReportService.InferDomain(settlement.Kind) == "war", "settlement capture/domain mapping failed");
    Require(WorldFactCapture.TryCreateHeroKilled(10, 1444, "lord_a", "阿尔达", out WorldFactCapture death)
        && death.EventKey.StartsWith("wf1-", StringComparison.Ordinal)
        && death.Fact.Entities.Any(x => x.Id == "lord_a" && x.Role == "subject")
        && WeeklyReportService.InferDomain(death.Kind) == "culture", "hero death capture/domain mapping failed");
    Require(WorldFactCapture.TryCreateHeroPrisonerReleased(10, 1445, "lord_a", "阿尔达", "kingdom_a", "Ransom", out WorldFactCapture released)
        && released.EventKey.StartsWith("wf1-", StringComparison.Ordinal)
        && released.Fact.Entities.Any(x => x.Id == "kingdom_a" && x.Role == "capturer")
        && WeeklyReportService.InferDomain(released.Kind) == "culture", "hero release capture/domain mapping failed");
    Require(!WorldFactCapture.TryCreateHeroKilled(0, 1444, "lord_a", "阿尔达", out _)
        && !WorldFactCapture.TryCreateHeroPrisonerReleased(10, 1445, "lord_a", "阿尔达", "kingdom_a", "", out _), "invalid world fact input was accepted");
}

static void TestWorldFactJournalCodec()
{
    Require(WorldFactCapture.TryCreateWar(10, 1441, "a", "甲国", "b", "乙国", out WorldFactCapture capture), "journal fixture fact creation failed");
    JObject fact = capture.Fact.ToJson();
    JObject chunk = WorldFactJournalCodec.BuildChunk(10, 16, 1, new[] { fact });
    string chunkJson = chunk.ToString(Newtonsoft.Json.Formatting.None);
    WorldFactJournalReadResult loaded = WorldFactJournalCodec.ReadChunk(chunkJson);
    Require(loaded.Status == WorldFactJournalReadStatus.Success && loaded.Facts.Count == 1, "valid journal chunk was rejected");
    string chunkKey = "facts-00000010-00000016-r00000001-0000-" + WorldFactJournalCodec.Sha256Hex(chunkJson);
    Require(WorldFactJournalCodec.TryParseChunkKey(chunkKey, out int startDay, out int endDay, out int revision, out int ordinal, out string hash)
        && startDay == 10 && endDay == 16 && revision == 1 && ordinal == 0 && hash.Length == 64,
        "valid journal chunk key was rejected");
    Require(!WorldFactJournalCodec.TryParseChunkKey("facts-invalid", out _, out _, out _, out _, out _), "invalid journal chunk key was accepted");
    Require(WorldFactJournalCodec.ReadChunk(null).Status == WorldFactJournalReadStatus.Missing
        && WorldFactJournalCodec.ReadChunk(" ").Status == WorldFactJournalReadStatus.Corrupt, "journal missing/corrupt states collapsed");
    JObject bad = (JObject)fact.DeepClone(); bad["factId"] = "bad";
    Require(WorldFactJournalCodec.ReadChunk(WorldFactJournalCodec.BuildChunk(10, 16, 1, new[] { bad }).ToString()).Status == WorldFactJournalReadStatus.Corrupt, "invalid fact identity was accepted");
    JObject root = WorldFactJournalCodec.BuildRoot(10, 16, 1, new[] { "chunk-1" });
    Require(WorldFactJournalCodec.ReadRoot(root.ToString(), out _) == WorldFactJournalReadStatus.Success, "valid journal root was rejected");
    JObject unsortedRoot = WorldFactJournalCodec.BuildRoot(10, 16, 1, new[] { "facts-b", "facts-a" });
    Require(WorldFactJournalCodec.ReadRoot(unsortedRoot.ToString(), out _) == WorldFactJournalReadStatus.Success, "root key canonicalization failed");
    unsortedRoot["chunkKeys"] = new JArray("facts-b", "facts-a");
    Require(WorldFactJournalCodec.ReadRoot(unsortedRoot.ToString(), out _) == WorldFactJournalReadStatus.Corrupt, "unsorted root was accepted");
    Require(WorldFactJournalCodec.ReadRoot("{\"schema\":\"awake.world_fact_journal.root.v1\",\"phase\":\"collecting\",\"chunkKeys\":[]}", out _) == WorldFactJournalReadStatus.Corrupt, "collecting root was accepted");
}

static void TestWorldFactQuery()
{
    Require(WorldFactCapture.TryCreateWar(10, 1441, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture war), "query war fixture creation failed");
    Require(WorldFactCapture.TryCreateSettlementOwnerChanged(11, 1585, "settlement:town", "阿卡拉特", "hero:old", "hero:new", "新领主", out WorldFactCapture settlement), "query settlement fixture creation failed");
    Require(WorldFactCapture.TryCreateHeroKilled(12, 1729, "hero:fallen", "阿尔达", out WorldFactCapture death), "query hero fixture creation failed");
    Require(WorldFactCapture.TryCreatePeace(12, 1730, "faction:a", "甲国", "faction:c", "丙国", out WorldFactCapture modCapture), "query mod fixture creation failed");
    JObject modFact = modCapture.Fact.ToJson();
    modFact["origin"] = "mod_generated";
    modFact["authority"] = "mod_asserted";
    var facts = new[] { war.Fact.ToJson(), settlement.Fact.ToJson(), death.Fact.ToJson(), modFact };
    var query = new WorldFactQuery(
        _ => Task.FromResult(new WorldFactJournalReadResult(WorldFactJournalReadStatus.Success, facts, revision: 1)),
        () => Array.Empty<WorldEventRecord>());

    WorldFactQueryResult recent = query.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.RecentDynamics,
        CurrentDay = 12,
        MaximumResults = 2
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(recent.Status == WorldFactQueryStatus.Success
        && recent.Facts.Count == 2
        && (int)recent.Facts[0]["occurred"]["campaignDay"] == 12
        && (int)recent.Facts[1]["occurred"]["campaignDay"] == 11
        && recent.Decisions.Any(value => value.ReasonCode == "excluded_not_game_confirmed_public"),
        "recent policy did not filter, order or explain facts");

    WorldFactQueryResult weekly = query.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.WeeklyDynamics,
        CurrentDay = 12,
        MaximumResults = 0
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(weekly.Facts.Count == 3
        && weekly.SourceFactIds.Count == 3
        && (int)weekly.Facts[0]["occurred"]["campaignDay"] == 10,
        "weekly policy did not return the stable seven-day fact set");

    WorldFactQueryResult wide = query.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.WorldKnowledge,
        MaximumResults = 0
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(wide.Status == WorldFactQueryStatus.Success
        && wide.Facts.Count == 3
        && wide.SourceFactIds.Count == 3,
        "world knowledge wide query was truncated or included a non-knowledge fact");

    WorldFactQueryResult character = query.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.CharacterMemoryCandidate,
        CurrentDay = 12,
        HeroId = "hero:fallen",
        MaximumResults = 20
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(character.Facts.Count == 1
        && (string)character.Facts[0]["factId"] == death.Fact.FactId
        && character.Decisions.Any(value => value.ReasonCode == "excluded_no_related_entity"),
        "character policy did not require an entity relation");

    WorldFactQueryResult trigger = query.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.EventTriggerCandidate,
        StartDay = 10,
        EndDay = 12,
        AllowedKinds = new[] { "hero_killed" },
        MaximumResults = 20
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(trigger.Facts.Count == 1 && (string)trigger.Facts[0]["kind"] == "hero_killed"
        && trigger.Decisions.Any(value => value.ReasonCode == "excluded_kind_not_subscribed"),
        "event trigger policy did not enforce subscribed kinds");

    var legacy = new WorldEventRecord("awake:event:legacy-1", 12, "battle", "war", "旧记录", DateTimeOffset.UtcNow, "legacy-1");
    var fallbackQuery = new WorldFactQuery(
        _ => Task.FromResult(new WorldFactJournalReadResult(WorldFactJournalReadStatus.Missing)),
        () => new[] { legacy });
    WorldFactQueryResult fallback = fallbackQuery.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.RecentDynamics,
        CurrentDay = 12
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(fallback.Status == WorldFactQueryStatus.Unavailable && fallback.UsedLegacyFallback
        && fallback.Facts.Count == 1
        && (string)fallback.Facts[0]["origin"] == "legacy_import",
        "legacy fallback was not explicitly marked");
    WorldFactQueryResult legacyCharacter = fallbackQuery.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.CharacterMemoryCandidate,
        CurrentDay = 12,
        HeroId = "hero:fallen"
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(legacyCharacter.Facts.Count == 0
        && legacyCharacter.Decisions.Any(value => value.ReasonCode == "excluded_legacy_has_no_entities"),
        "legacy fallback was allowed into character memory candidates");

    var corruptQuery = new WorldFactQuery(
        _ => Task.FromResult(new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: "smoke.corrupt")),
        () => new[] { legacy });
    WorldFactQueryResult corrupt = corruptQuery.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.RecentDynamics,
        CurrentDay = 12
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(corrupt.Status == WorldFactQueryStatus.Corrupt && corrupt.Facts.Count == 0 && !corrupt.UsedLegacyFallback,
        "corrupt journal was mistaken for an empty legacy window");
}

static void TestMinimalWeeklyReport()
{
    Require(WorldFactCapture.TryCreateWar(7, 1008, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture war), "minimal report war fixture creation failed");
    Require(WorldFactCapture.TryCreateHeroKilled(7, 1009, "hero:fallen", "阿尔达", out WorldFactCapture death), "minimal report hero fixture creation failed");
    var query = new WorldFactQuery(
        _ => Task.FromResult(new WorldFactJournalReadResult(
            WorldFactJournalReadStatus.Success,
            new[] { death.Fact.ToJson(), war.Fact.ToJson() },
            revision: 4)),
        () => Array.Empty<WorldEventRecord>());
    WorldFactQueryResult weekly = query.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.WeeklyDynamics,
        StartDay = 1,
        EndDay = 7,
        MaximumResults = 0
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(weekly.Status == WorldFactQueryStatus.Success
        && weekly.WindowStartDay == 1
        && weekly.WindowEndDay == 7
        && weekly.Facts.Count == 2, "completed weekly query did not preserve its explicit window");
    WeeklyFactReportBuildResult routed = WorldEventServices.TryBuildWeeklyReportFromQueryResult(weekly);
    Require(routed.Succeeded && routed.Report != null, routed.ErrorCode);
    Require(WeeklyDynamicsInput.TryCreate(weekly, out WeeklyDynamicsInput input, out string inputError), inputError);
    Require(WeeklyReportService.TryBuildFromFacts(input, out JObject first, out string reportError), reportError);
    Require(WeeklyReportService.TryBuildFromFacts(input, out JObject second, out reportError), reportError);
    Require(WorldFactJournalCodec.CanonicalJson(first) == WorldFactJournalCodec.CanonicalJson(second)
        && WeeklyReportService.BuildText(first, "本周动态", "本周没有记录。") == WeeklyReportService.BuildText(second, "本周动态", "本周没有记录。"),
        "minimal report was not deterministic");
    Require((string)first["schemaVersion"] == "awake.worldbook.weekly-report.v0-preview"
        && (int)first["windowStartDay"] == 1
        && (int)first["windowEndDay"] == 7
        && ((JArray)first["sourceFactIds"]).Count == 2
        && ((JArray)first["sections"]).Count == 2,
        "minimal report output contract is incomplete");
    Require(((JArray)first["sections"]).Children<JObject>()
        .SelectMany(section => ((JArray)section["items"]).Children<JObject>())
        .SelectMany(item => ((JArray)item["sourceFactIds"]).Values<string>())
        .OrderBy(value => value, StringComparer.Ordinal)
        .SequenceEqual(((JArray)first["sourceFactIds"]).Values<string>().OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal),
        "minimal report source-fact closure is incomplete");

    WorldFactQueryResult incomplete = query.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.WeeklyDynamics,
        StartDay = 5,
        EndDay = 10,
        MaximumResults = 0
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(incomplete.Status == WorldFactQueryStatus.InvalidRequest, "unfinished weekly window was accepted");

    var emptyQuery = new WorldFactQuery(
        _ => Task.FromResult(new WorldFactJournalReadResult(WorldFactJournalReadStatus.Empty, revision: 1)),
        () => Array.Empty<WorldEventRecord>());
    WorldFactQueryResult emptyResult = emptyQuery.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.WeeklyDynamics,
        StartDay = 1,
        EndDay = 7,
        MaximumResults = 0
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(WeeklyDynamicsInput.TryCreate(emptyResult, out WeeklyDynamicsInput emptyInput, out inputError)
        && WeeklyReportService.TryBuildFromFacts(emptyInput, out JObject emptyReport, out reportError)
        && ((JArray)emptyReport["sections"]).Count == 0
        && ((JArray)emptyReport["sourceFactIds"]).Count == 0, reportError ?? inputError);

    foreach (WorldFactJournalReadStatus failureStatus in new[]
    {
        WorldFactJournalReadStatus.Missing,
        WorldFactJournalReadStatus.Corrupt,
        WorldFactJournalReadStatus.Unavailable
    })
    {
        var failureQuery = new WorldFactQuery(
            _ => Task.FromResult(new WorldFactJournalReadResult(failureStatus)),
            () => Array.Empty<WorldEventRecord>());
        WorldFactQueryResult failure = failureQuery.ExecuteAsync(new WorldFactQueryRequest
        {
            Policy = WorldFactSelectionPolicy.WeeklyDynamics,
            StartDay = 1,
            EndDay = 7,
            MaximumResults = 0
        }, CancellationToken.None).GetAwaiter().GetResult();
        Require(!WeeklyDynamicsInput.TryCreate(failure, out _, out inputError)
            && inputError == "awake.world_fact.report.query_failed", "journal failure was accepted as a report input");
    }

    var legacy = new WorldEventRecord("awake:event:legacy-minimal", 7, "battle", "war", "旧记录", DateTimeOffset.UtcNow, "legacy-minimal");
    var legacyQuery = new WorldFactQuery(
        _ => Task.FromResult(new WorldFactJournalReadResult(WorldFactJournalReadStatus.Missing)),
        () => new[] { legacy });
    WorldFactQueryResult legacyResult = legacyQuery.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.WeeklyDynamics,
        StartDay = 1,
        EndDay = 7,
        MaximumResults = 0
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(!WeeklyDynamicsInput.TryCreate(legacyResult, out _, out inputError)
        && inputError == "awake.world_fact.report.legacy_fallback", "legacy fallback entered the formal report seam");

    WorldFactQueryResult duplicate = new WorldFactQueryResult(
        WorldFactQueryStatus.Success,
        WorldFactSelectionPolicy.WeeklyDynamics,
        new[] { war.Fact.ToJson(), war.Fact.ToJson() },
        windowStartDay: 1,
        windowEndDay: 7);
    Require(!WeeklyDynamicsInput.TryCreate(duplicate, out _, out inputError)
        && inputError == "awake.world_fact.report.fact_invalid", "duplicate fact IDs entered the formal report seam");

    JObject unknownDomain = war.Fact.ToJson();
    unknownDomain["presentation"]["domain"] = "unknown-domain";
    var unknownQuery = new WorldFactQuery(
        _ => Task.FromResult(new WorldFactJournalReadResult(
            WorldFactJournalReadStatus.Success,
            new[] { unknownDomain },
            revision: 5)),
        () => Array.Empty<WorldEventRecord>());
    WorldFactQueryResult unknownResult = unknownQuery.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.WeeklyDynamics,
        StartDay = 1,
        EndDay = 7,
        MaximumResults = 0
    }, CancellationToken.None).GetAwaiter().GetResult();
    WeeklyFactReportBuildResult unknownReport = WorldEventServices.TryBuildWeeklyReportFromQueryResult(unknownResult);
    Require(!unknownReport.Succeeded && unknownReport.ErrorCode == "awake.world_fact.report.fact_invalid",
        "unknown fact domain was silently assigned to a report section");
}

static void RunWorldFactContextInterfaceStateMatrix()
{
    Require(WorldFactCapture.TryCreateWar(7, 1008, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture war), "context state fixture creation failed");
    foreach (WorldFactJournalReadStatus status in new[]
    {
        WorldFactJournalReadStatus.Success,
        WorldFactJournalReadStatus.Empty,
        WorldFactJournalReadStatus.Missing,
        WorldFactJournalReadStatus.Unavailable,
        WorldFactJournalReadStatus.Corrupt
    })
    {
        int reads = 0;
        IReadOnlyList<JObject> facts = status == WorldFactJournalReadStatus.Success
            ? new[] { war.Fact.ToJson() }
            : Array.Empty<JObject>();
        var query = new WorldFactQuery(
            _ =>
            {
                reads++;
                return Task.FromResult(new WorldFactJournalReadResult(status, facts, revision: status == WorldFactJournalReadStatus.Success || status == WorldFactJournalReadStatus.Empty ? 3 : (int?)null, errorCode: status == WorldFactJournalReadStatus.Corrupt ? "context.corrupt" : null));
            },
            () => Array.Empty<WorldEventRecord>());
        WorldFactQueryResult result = query.ExecuteAsync(new WorldFactQueryRequest
        {
            Policy = WorldFactSelectionPolicy.EventTriggerCandidate,
            CurrentDay = 7,
            MaximumResults = 0
        }, CancellationToken.None).GetAwaiter().GetResult();
        Require(result.Status == (status == WorldFactJournalReadStatus.Success ? WorldFactQueryStatus.Success : MapContextStatus(status))
            && reads == 1, "context state was not preserved: " + status);
        if (status == WorldFactJournalReadStatus.Success || status == WorldFactJournalReadStatus.Empty)
            Require(result.JournalRevision == 3, "valid journal revision was not propagated");
    }

    int invalidReads = 0;
    var invalidQuery = new WorldFactQuery(
        _ =>
        {
            invalidReads++;
            return Task.FromResult(new WorldFactJournalReadResult(WorldFactJournalReadStatus.Success, revision: 1));
        });
    WorldFactQueryResult invalid = invalidQuery.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.EventTriggerCandidate,
        MaximumResults = -1
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(invalid.Status == WorldFactQueryStatus.InvalidRequest && invalidReads == 0, "invalid context request reached the reader");

    var missingRevisionQuery = new WorldFactQuery(
        _ => Task.FromResult(new WorldFactJournalReadResult(WorldFactJournalReadStatus.Success, new[] { war.Fact.ToJson() })));
    WorldFactQueryResult missingRevision = missingRevisionQuery.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.EventTriggerCandidate,
        CurrentDay = 7,
        MaximumResults = 0
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(missingRevision.Status == WorldFactQueryStatus.Corrupt
        && missingRevision.JournalRevision == null
        && missingRevision.ErrorCode == "awake.world_fact.journal_revision_missing",
        "missing journal revision was mistaken for a successful query");
}

static void RunWorldFactContextInterfaceProvenance()
{
    Require(WorldFactCapture.TryCreateWar(7, 1008, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture war), "context provenance fixture creation failed");
    var query = new WorldFactQuery(
        _ => Task.FromResult(new WorldFactJournalReadResult(
            WorldFactJournalReadStatus.Success,
            new[] { war.Fact.ToJson(), war.Fact.ToJson() },
            revision: 9)));
    WorldFactQueryResult result = query.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.EventTriggerCandidate,
        CurrentDay = 7,
        MaximumResults = 0
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(result.Status == WorldFactQueryStatus.Success
        && result.JournalRevision == 9
        && result.Facts.Count == 1
        && result.SourceFactIds.Count == 1
        && result.SourceFactIds[0] == war.Fact.FactId
        && result.Decisions.Any(value => value.ReasonCode == "excluded_duplicate_fact_id"),
        "context provenance or stable deduplication was not preserved");
}

static void RunWorldFactContextInterfaceLegacyFallback()
{
    var legacy = new WorldEventRecord("awake:event:legacy-context", 7, "battle", "war", "旧记录", DateTimeOffset.UtcNow, "legacy-context");
    var query = new WorldFactQuery(
        _ => Task.FromResult(new WorldFactJournalReadResult(WorldFactJournalReadStatus.Missing)),
        () => new[] { legacy });
    WorldFactQueryResult allowed = query.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.EventTriggerCandidate,
        CurrentDay = 7,
        MaximumResults = 0,
        AllowLegacyFallback = true
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(allowed.Status == WorldFactQueryStatus.Unavailable
        && allowed.LegacyFallbackState == LegacyFallbackState.Used
        && allowed.UsedLegacyFallback
        && allowed.JournalRevision == null,
        "allowed legacy fallback was not marked as non-formal");

    WorldFactQueryResult rejected = query.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.EventTriggerCandidate,
        CurrentDay = 7,
        MaximumResults = 0,
        AllowLegacyFallback = false
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(rejected.Status == WorldFactQueryStatus.Unavailable
        && rejected.LegacyFallbackState == LegacyFallbackState.Rejected
        && !rejected.UsedLegacyFallback
        && rejected.ErrorCode == "awake.world_fact.event.legacy_fallback",
        "legacy fallback rejection was not explicit");

    var emptyLegacyQuery = new WorldFactQuery(
        _ => Task.FromResult(new WorldFactJournalReadResult(WorldFactJournalReadStatus.Missing)),
        () => Array.Empty<WorldEventRecord>());
    WorldFactQueryResult notUsed = emptyLegacyQuery.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.EventTriggerCandidate,
        CurrentDay = 7,
        MaximumResults = 0
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(notUsed.Status == WorldFactQueryStatus.Missing && notUsed.LegacyFallbackState == LegacyFallbackState.NotUsed,
        "empty legacy source was mislabeled as used fallback");

    WorldEventContracts.SetFactContextReaderForTesting(new FixedFactContextReader(allowed));
    try
    {
        WorldFactQueryResult facade = WorldEventContracts.QueryEventTriggerCandidatesAsync(7, CancellationToken.None).GetAwaiter().GetResult();
        Require(facade.Status == WorldFactQueryStatus.Unavailable
            && facade.LegacyFallbackState == LegacyFallbackState.Used
            && facade.ErrorCode == "awake.world_fact.event.legacy_fallback",
            "event facade accepted a legacy fallback result");
    }
    finally
    {
        WorldEventContracts.SetFactContextReaderForTesting(null);
    }
}

static void RunWorldFactContextInterfaceCancellation()
{
    int reads = 0;
    var query = new WorldFactQuery(
        _ =>
        {
            reads++;
            return Task.FromException<WorldFactJournalReadResult>(new OperationCanceledException());
        });
    CancellationTokenSource preCancelled = new CancellationTokenSource();
    preCancelled.Cancel();
    WorldFactQueryResult pre = query.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.EventTriggerCandidate,
        MaximumResults = -1
    }, preCancelled.Token).GetAwaiter().GetResult();
    Require(pre.Status == WorldFactQueryStatus.Cancelled && pre.ErrorCode == "awake.cancelled" && reads == 0,
        "pre-cancel did not win over request validation");

    WorldFactQueryResult during = query.ExecuteAsync(new WorldFactQueryRequest
    {
        Policy = WorldFactSelectionPolicy.EventTriggerCandidate,
        CurrentDay = 7,
        MaximumResults = 0
    }, CancellationToken.None).GetAwaiter().GetResult();
    Require(during.Status == WorldFactQueryStatus.Cancelled && during.ErrorCode == "awake.cancelled" && reads == 1,
        "read cancellation was not converted to the stable result");
}

static void RunAwakeEventFactTriggerEvaluation()
{
    Require(WorldFactCapture.TryCreateWar(5, 720, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture war),
        "fact trigger war fixture creation failed");
    Require(WorldFactCapture.TryCreateSettlementOwnerChanged(12, 1728, "settlement:x", "甲城", "hero:old", "hero:new", "新领主", out WorldFactCapture ownerChanged),
        "fact trigger settlement fixture creation failed");
    Require(WorldFactCapture.TryCreateWar(4, 576, "faction:c", "丙国", "faction:d", "丁国", out WorldFactCapture oldWar),
        "fact trigger old fixture creation failed");
    Require(WorldFactCapture.TryCreateWar(13, 1872, "faction:e", "戊国", "faction:f", "己国", out WorldFactCapture futureWar),
        "fact trigger future fixture creation failed");

    JObject payload = new JObject
    {
        ["kind"] = "event",
        ["weight"] = 1,
        ["condition"] = "Always",
        ["event"] = new JObject
        {
            ["id"] = "smoke.fact-trigger",
            ["title"] = "事实触发",
            ["body"] = "测试事实条件。",
            ["optionA"] = "继续",
            ["optionB"] = "跳过",
            ["source"] = "PresetRule",
            ["context"] = "MapMarch",
            ["subject"] = "World",
            ["content"] = "World",
            ["resolution"] = "NarrativeOnly",
            ["choiceShape"] = "Informational",
            ["persistence"] = "Repeatable"
        },
        ["factTrigger"] = new JObject
        {
            ["allowedKinds"] = new JArray("settlement_owner_changed", "war_declared", "war_declared"),
            ["minimumMatches"] = 2,
            ["maximumAgeDays"] = 7
        }
    };
    Require(AwakeEventDataLoader.TryParseRule(payload, out AwakeEventRule rule, out string parseError), parseError);
    Require(rule.FactTrigger.AllowedKinds.SequenceEqual(
        new[] { "settlement_owner_changed", "war_declared" }, StringComparer.Ordinal),
        "fact trigger kinds were not normalized deterministically");

    WorldFactQueryResult success = new WorldFactQueryResult(
        WorldFactQueryStatus.Success,
        WorldFactSelectionPolicy.EventTriggerCandidate,
        new[] { war.Fact.ToJson(), war.Fact.ToJson(), ownerChanged.Fact.ToJson(), oldWar.Fact.ToJson(), futureWar.Fact.ToJson() },
        journalRevision: 4);
    AwakeEventCandidateEvaluation matched = AwakeEventCandidateEvaluator.Evaluate(rule, success, 12);
    Require(matched.Eligible
        && matched.ReasonCode == "fact_trigger_matched"
        && matched.Status == WorldFactQueryStatus.Success
        && matched.JournalRevision == 4
        && matched.MatchedFactIds.Count == 2
        && matched.MatchedFactIds[0] == war.Fact.FactId
        && matched.MatchedFactIds[1] == ownerChanged.Fact.FactId
        && matched.MatchedFactIds.Distinct(StringComparer.Ordinal).Count() == 2,
        "fact trigger did not apply kind, age, and duplicate rules");

    AwakeEventCandidateEvaluation insufficient = AwakeEventCandidateEvaluator.Evaluate(
        rule,
        new WorldFactQueryResult(
            WorldFactQueryStatus.Success,
            WorldFactSelectionPolicy.EventTriggerCandidate,
            new[] { oldWar.Fact.ToJson() },
            journalRevision: 5),
        12);
    Require(!insufficient.Eligible && insufficient.ReasonCode == "fact_trigger_no_match",
        "old fact incorrectly satisfied the trigger");

    AwakeEventCandidateEvaluation empty = AwakeEventCandidateEvaluator.Evaluate(
        rule,
        new WorldFactQueryResult(WorldFactQueryStatus.Empty, WorldFactSelectionPolicy.EventTriggerCandidate, journalRevision: 6),
        12);
    Require(!empty.Eligible && empty.ReasonCode == "fact_trigger_no_match", "empty facts did not block the trigger");

    AwakeEventCandidateEvaluation unavailable = AwakeEventCandidateEvaluator.Evaluate(
        rule,
        new WorldFactQueryResult(WorldFactQueryStatus.Unavailable, WorldFactSelectionPolicy.EventTriggerCandidate, errorCode: "smoke.fact_unavailable"),
        12);
    Require(!unavailable.Eligible && unavailable.ReasonCode == "smoke.fact_unavailable"
        && unavailable.Status == WorldFactQueryStatus.Unavailable,
        "unavailable facts did not fail closed with their reason");

    AwakeEventRule legacyRule = new AwakeEventRule(rule.Definition, 1, 0, AwakeEventCondition.Always);
    AwakeEventCandidateEvaluation legacy = AwakeEventCandidateEvaluator.Evaluate(
        legacyRule,
        new WorldFactQueryResult(WorldFactQueryStatus.Unavailable, WorldFactSelectionPolicy.EventTriggerCandidate),
        12);
    Require(legacy.Eligible && legacy.ReasonCode == "fact_trigger_not_required",
        "legacy rule behavior changed when facts were unavailable");
    foreach (WorldFactQueryStatus status in new[] { WorldFactQueryStatus.Success, WorldFactQueryStatus.Empty })
    {
        AwakeEventCandidateEvaluation legacyAvailable = AwakeEventCandidateEvaluator.Evaluate(
            legacyRule,
            new WorldFactQueryResult(status, WorldFactSelectionPolicy.EventTriggerCandidate),
            12);
        Require(legacyAvailable.Eligible && legacyAvailable.ReasonCode == "fact_trigger_not_required",
            "legacy rule behavior changed for status " + status);
    }

    AwakeEventCandidateEvaluation cancelled = AwakeEventCandidateEvaluator.Evaluate(
        rule,
        new WorldFactQueryResult(WorldFactQueryStatus.Cancelled, WorldFactSelectionPolicy.EventTriggerCandidate, errorCode: "awake.cancelled"),
        12);
    Require(!cancelled.Eligible && cancelled.ReasonCode == "awake.cancelled", "cancelled facts entered eligibility");

    AwakeEventCandidateEvaluation missing = AwakeEventCandidateEvaluator.Evaluate(
        rule,
        new WorldFactQueryResult(WorldFactQueryStatus.Missing, WorldFactSelectionPolicy.EventTriggerCandidate),
        12);
    Require(!missing.Eligible && missing.ReasonCode == "fact_trigger_no_match", "missing facts did not block the trigger");
    AwakeEventCandidateEvaluation corrupt = AwakeEventCandidateEvaluator.Evaluate(
        rule,
        new WorldFactQueryResult(WorldFactQueryStatus.Corrupt, WorldFactSelectionPolicy.EventTriggerCandidate, errorCode: "smoke.fact_corrupt"),
        12);
    Require(!corrupt.Eligible && corrupt.ReasonCode == "smoke.fact_corrupt", "corrupt facts did not preserve the failure reason");
    AwakeEventCandidateEvaluation fallback = AwakeEventCandidateEvaluator.Evaluate(
        rule,
        new WorldFactQueryResult(
            WorldFactQueryStatus.Success,
            WorldFactSelectionPolicy.EventTriggerCandidate,
            new[] { war.Fact.ToJson() },
            usedLegacyFallback: true,
            legacyFallbackState: LegacyFallbackState.Used),
        12);
    Require(!fallback.Eligible && fallback.ReasonCode == "fact_trigger_legacy_fallback",
        "legacy fallback facts entered formal trigger matching");

    JObject invalid = (JObject)payload.DeepClone();
    invalid["factTrigger"]["maximumAgeDays"] = 8;
    Require(!AwakeEventDataLoader.TryParseRule(invalid, out _, out parseError)
        && parseError == "factTrigger.maximumAgeDays",
        "invalid fact trigger age was accepted");

    invalid = (JObject)payload.DeepClone();
    invalid["factTrigger"]["allowedKinds"] = new JArray("war_declared", " ");
    Require(!AwakeEventDataLoader.TryParseRule(invalid, out _, out parseError)
        && parseError == "factTrigger.allowedKinds",
        "blank fact trigger kind was accepted");
}

static WorldFactQueryStatus MapContextStatus(WorldFactJournalReadStatus status)
{
    switch (status)
    {
        case WorldFactJournalReadStatus.Empty: return WorldFactQueryStatus.Empty;
        case WorldFactJournalReadStatus.Missing: return WorldFactQueryStatus.Missing;
        case WorldFactJournalReadStatus.Corrupt: return WorldFactQueryStatus.Corrupt;
        default: return WorldFactQueryStatus.Unavailable;
    }
}

static void TestWorldReportV2Build()
{
    Require(WorldFactCapture.TryCreateWar(7, 1008, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture war), "v2 report war fixture creation failed");
    Require(WorldFactCapture.TryCreateHeroKilled(7, 1009, "hero:fallen", "阿尔达", out WorldFactCapture death), "v2 report hero fixture creation failed");
    WorldFactQueryResult result = new WorldFactQueryResult(
        WorldFactQueryStatus.Success,
        WorldFactSelectionPolicy.WeeklyDynamics,
        new[] { death.Fact.ToJson(), war.Fact.ToJson() },
        windowStartDay: 1,
        windowEndDay: 7);
    Require(WeeklyDynamicsInput.TryCreate(result, out WeeklyDynamicsInput input, out string inputError), inputError);
    Require(WeeklyReportService.TryBuildV2FromFacts(input, out JObject first, out string reportError), reportError);
    Require(WeeklyReportService.TryBuildV2FromFacts(input, out JObject second, out reportError), reportError);
    Require(WeeklyReportService.TryValidateV2Report(first, out reportError), reportError);
    Require(WeeklyReportService.CanonicalizeV2(first) == WeeklyReportService.CanonicalizeV2(second)
        && StringComparer.Ordinal.Equals((string)first["contentFingerprint"], (string)second["contentFingerprint"]),
        "v2 report generation was not deterministic");

    string fixtureRoot = FindWorldReportV2FixtureRoot();
    JObject schema = LoadJsonNoDates(Path.Combine(Directory.GetParent(fixtureRoot).FullName, "weekly-report.schema.json"));
    Require((string)schema["$id"] == "awake.worldbook.weekly-report.v2"
        && schema["additionalProperties"]?.Value<bool>() == false,
        "v2 schema fixture is missing or has the wrong identity");
    JObject validFixture = LoadJsonNoDates(Path.Combine(fixtureRoot, "valid-report.json"));
    Require(WeeklyReportService.TryValidateV2Report(validFixture, out reportError), reportError);
    Require(StringComparer.Ordinal.Equals(WeeklyReportService.CanonicalizeV2(validFixture), File.ReadAllText(Path.Combine(fixtureRoot, "expected-canonical.json")).TrimEnd('\r', '\n')),
        "v2 golden canonical JSON changed");
    Require(StringComparer.Ordinal.Equals(WeeklyReportService.ComputeV2Fingerprint(validFixture), (string)validFixture["contentFingerprint"]),
        "v2 golden fingerprint is not reproducible");
    Require(StringComparer.Ordinal.Equals(first.ToString(Formatting.None), validFixture.ToString(Formatting.None)),
        "v2 generated report does not match the golden report");

    JObject emptyFixture = LoadJsonNoDates(Path.Combine(fixtureRoot, "valid-empty-report.json"));
    Require(WeeklyReportService.TryValidateV2Report(emptyFixture, out reportError), reportError);
    Require(((JArray)emptyFixture["sections"]).Count == 0 && ((JArray)emptyFixture["sourceFactIds"]).Count == 0,
        "v2 empty report contains fabricated content");
    foreach (string name in new[] { "invalid-source-closure.json", "invalid-window.json", "invalid-fingerprint.json" })
        Require(!WeeklyReportService.TryValidateV2Report(LoadJsonNoDates(Path.Combine(fixtureRoot, name)), out _), "invalid v2 fixture was accepted: " + name);

    WorldFactQueryResult legacy = new WorldFactQueryResult(
        WorldFactQueryStatus.Success,
        WorldFactSelectionPolicy.WeeklyDynamics,
        new[] { war.Fact.ToJson() },
        usedLegacyFallback: true,
        windowStartDay: 1,
        windowEndDay: 7);
    Require(!WeeklyDynamicsInput.TryCreate(legacy, out _, out inputError)
        && inputError == "awake.world_fact.report.legacy_fallback", "legacy fallback entered the v2 report seam");
    WorldFactQueryResult failed = new WorldFactQueryResult(
        WorldFactQueryStatus.Corrupt,
        WorldFactSelectionPolicy.WeeklyDynamics,
        errorCode: "awake.world_fact.chunk_corrupt",
        windowStartDay: 1,
        windowEndDay: 7);
    Require(!WeeklyDynamicsInput.TryCreate(failed, out _, out inputError), "failed journal entered the v2 report seam");
}

static void TestWorldReportV2PersistenceMinimal()
{
    Require(WorldFactCapture.TryCreateWar(7, 1008, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture war), "v2 persistence war fixture creation failed");
    WorldFactQueryResult result = new WorldFactQueryResult(
        WorldFactQueryStatus.Success,
        WorldFactSelectionPolicy.WeeklyDynamics,
        new[] { war.Fact.ToJson() },
        windowStartDay: 1,
        windowEndDay: 7);
    Require(WeeklyDynamicsInput.TryCreate(result, out WeeklyDynamicsInput input, out string inputError), inputError);
    Require(WeeklyReportService.TryBuildV2FromFacts(input, out JObject report, out string reportError), reportError);

    var store = new WorldStateStore();
    AwakeRuntime.WorldStateStore = store;
    WeeklyReportStateWriteResult applied = WorldEventServices.PersistV2WeeklyReportAsync(report, 7, CancellationToken.None).GetAwaiter().GetResult();
    Require(applied.Status == WeeklyReportStateWriteResult.Applied && store.ReportWriteAttempts == 1, "new v2 report was not persisted");
    store = store.Reopen();
    AwakeRuntime.WorldStateStore = store;
    List<WeeklyReportApplicationState> states = store.GetWeeklyReportStatesAsync(CancellationToken.None).GetAwaiter().GetResult();
    Require(states.Count == 1 && states[0].SchemaVersion == "awake.worldbook.weekly-report.v2" && states[0].Report != null
        && StringComparer.Ordinal.Equals(states[0].ContentFingerprint, (string)report["contentFingerprint"]), "reopened v2 report was incomplete");

    WeeklyReportStateWriteResult duplicate = WorldEventServices.PersistV2WeeklyReportAsync(report, 7, CancellationToken.None).GetAwaiter().GetResult();
    Require(duplicate.Status == WeeklyReportStateWriteResult.AlreadyApplied && store.ReportWriteAttempts == 1, "same v2 report was written twice");

    JObject conflictReport = (JObject)report.DeepClone();
    conflictReport["sections"][0]["items"][0]["text"]["zh-CN"] = "第 7 天：甲国与乙国暂时停战。";
    conflictReport["contentFingerprint"] = WeeklyReportService.ComputeV2Fingerprint(conflictReport);
    WeeklyReportStateWriteResult conflict = WorldEventServices.PersistV2WeeklyReportAsync(conflictReport, 7, CancellationToken.None).GetAwaiter().GetResult();
    Require(conflict.Status == WeeklyReportStateWriteResult.Conflict && store.ReportWriteAttempts == 1, "different v2 content did not produce conflict");

    store.Document["weeklyReports"][0]["report"] = new JObject { ["schemaVersion"] = "awake.worldbook.weekly-report.v2", ["reportId"] = (string)report["reportId"] };
    WeeklyReportStateWriteResult repaired = WorldEventServices.PersistV2WeeklyReportAsync(report, 7, CancellationToken.None).GetAwaiter().GetResult();
    Require(repaired.Status == WeeklyReportStateWriteResult.Applied && store.ReportWriteAttempts == 2, "corrupt v2 report was not repaired");
    store = store.Reopen();
    AwakeRuntime.WorldStateStore = store;
    states = store.GetWeeklyReportStatesAsync(CancellationToken.None).GetAwaiter().GetResult();
    Require(states.Count == 1 && states[0].Report != null && WeeklyReportService.TryValidateV2Report(states[0].Report, out _), "repaired v2 report was not readable after reopen");

    JObject invalid = (JObject)report.DeepClone();
    invalid["unexpected"] = true;
    WeeklyReportStateWriteResult invalidResult = WorldEventServices.PersistV2WeeklyReportAsync(invalid, 7, CancellationToken.None).GetAwaiter().GetResult();
    Require(invalidResult.Status == WeeklyReportStateWriteResult.Failed, "invalid v2 payload was accepted");

    var failedStore = new WorldStateStore { FailReportWrites = true };
    AwakeRuntime.WorldStateStore = failedStore;
    WeeklyReportStateWriteResult failed = WorldEventServices.PersistV2WeeklyReportAsync(report, 7, CancellationToken.None).GetAwaiter().GetResult();
    Require(failed.Status == WeeklyReportStateWriteResult.Retryable && failedStore.ReportWriteAttempts == 0, "v2 storage failure was reported as success");

    var v1Store = new WorldStateStore();
    JObject v1Report = WeeklyReportService.Build(
        new[] { new WorldEventRecord("awake:event:v1-report", 7, "tax", "economy", "旧版报告", DateTimeOffset.UtcNow, "v1-report") }, 7);
    v1Report["reportId"] = (string)report["reportId"];
    v1Store.Document["weeklyReports"] = new JArray(new JObject
    {
        ["reportId"] = (string)report["reportId"],
        ["windowStartDay"] = 1,
        ["windowEndDay"] = 7,
        ["status"] = "applied",
        ["attemptCount"] = 1,
        ["lastAttemptDay"] = 7,
        ["lastErrorCode"] = string.Empty,
        ["report"] = v1Report
    });
    AwakeRuntime.WorldStateStore = v1Store;
    WeeklyReportStateWriteResult v1Conflict = WorldEventServices.PersistV2WeeklyReportAsync(report, 7, CancellationToken.None).GetAwaiter().GetResult();
    Require(v1Conflict.Status == WeeklyReportStateWriteResult.Conflict
        && StringComparer.Ordinal.Equals((string)v1Store.Document["weeklyReports"][0]["report"]["schemaVersion"], "awake.worldbook.weekly-report.v1"),
        "v2 persistence overwrote an existing v1 report");

    WorldEventLedger.ClearForTesting();
    AwakeRuntime.NativeKnowledgeReady = true;
    var v1ReadStore = new WorldStateStore();
    Require(WorldFactCapture.TryCreateWar(7, 1008, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture v1ReadFact), "v1 read fixture fact creation failed");
    JObject v1ReadFactJson = v1ReadFact.Fact.ToJson();
    v1ReadFactJson["legacyVisibilityIdentityIds"] = new JArray("commoner");
    v1ReadStore.Journal = new WorldFactJournalReadResult(WorldFactJournalReadStatus.Success, new[] { v1ReadFactJson }, revision: 1);
    JObject v1ReadReport = WeeklyReportService.Build(
        new[] { new WorldEventRecord("awake:event:v1-read", 7, "tax", "economy", "旧版报告读取", DateTimeOffset.UtcNow, "v1-read", new[] { "commoner" }) }, 7);
    v1ReadStore.Document["weeklyReports"] = new JArray(new JObject
    {
        ["reportId"] = (string)v1ReadReport["reportId"],
        ["schemaVersion"] = "awake.worldbook.weekly-report.v1",
        ["windowStartDay"] = 1,
        ["windowEndDay"] = 7,
        ["status"] = "applied",
        ["attemptCount"] = 1,
        ["lastAttemptDay"] = 7,
        ["lastErrorCode"] = string.Empty,
        ["report"] = v1ReadReport
    });
    AwakeRuntime.WorldStateStore = v1ReadStore;
    WorldKnowledgeSnapshot v1Snapshot = WorldKnowledgeLoader.Load(Path.Combine(FindFixedFixtureRoot(), "manifest.json"));
    var v1Query = new WorldKnowledgeQueryService(v1Snapshot);
    WorldEventServices.BindKnowledge(v1Snapshot, v1Query);
    WorldEventServices.EnsureKnowledgeReadyAsync(7, CancellationToken.None).GetAwaiter().GetResult();
    WorldKnowledgeQueryResult v1Projected = v1Query.Query(new WorldbookQuery
    {
        IdentityId = "profile.commoner",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "周报",
        MaximumBytes = 4096
    });
    List<WeeklyReportApplicationState> v1States = v1ReadStore.GetWeeklyReportStatesAsync(CancellationToken.None).GetAwaiter().GetResult();
    Require(v1States.Count == 1
        && v1States[0].SchemaVersion == "awake.worldbook.weekly-report.v1"
        && v1States[0].Report != null
        && v1Projected.ReportIds.Contains((string)v1ReadReport["reportId"]),
        "v1 report read/projection compatibility was not demonstrated");
    AwakeRuntime.WorldStateStore = null;
}

static void TestIdentityCapabilityRules()
{
    var villager = WorldbookIdentityCapabilityRules.Resolve("villager", false, 30, 0);
    Require(villager.ProfileId == "profile.villager" && villager.KnowledgeScope == "local" && villager.EffectiveDetail == "rumor", "villager capability mapping failed");

    var headman = WorldbookIdentityCapabilityRules.Resolve("headman", false, 45, 0);
    Require(headman.ProfileId == "profile.headman" && headman.KnowledgeScope == "national" && headman.EffectiveDetail == "detail", "headman capability mapping failed");

    var youngNoble = WorldbookIdentityCapabilityRules.Resolve("lord", true, 22, 20);
    var matureNoble = WorldbookIdentityCapabilityRules.Resolve("lord", true, 50, 20);
    var stewardNoble = WorldbookIdentityCapabilityRules.Resolve("lord", true, 22, 80);
    Require(youngNoble.EffectiveDetail == "summary" && matureNoble.EffectiveDetail == "secret" && stewardNoble.EffectiveDetail == "detail", "noble age/management progression failed");

    var unknown = WorldbookIdentityCapabilityRules.Resolve("hero", false, 30, 0);
    Require(unknown.ProfileId == "profile.anonymous" && !unknown.KnowledgeScopeAvailable && !unknown.EffectiveDetailAvailable, "unknown identity did not fail closed");
}

static void TestFixedFixturePackage()
{
    string fixtureRoot = FindFixedFixtureRoot();
    string manifestPath = Path.Combine(fixtureRoot, "manifest.json");
    JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
    JObject runtime = JObject.Parse(File.ReadAllText(Path.Combine(fixtureRoot, "runtime.json")));
    JObject index = JObject.Parse(File.ReadAllText(Path.Combine(fixtureRoot, "index.json")));
    string manifestHash = WorldbookPackageIntegrity.ComputeManifestHash(manifest);
    string contentHash = WorldbookPackageIntegrity.ComputeContentHash("runtime.json", runtime, "index.json", index);
    string packageHash = WorldbookPackageIntegrity.ComputePackageHash(manifestHash, contentHash);
    Require(
        StringComparer.OrdinalIgnoreCase.Equals(manifestHash, (string)manifest["hashes"]?["manifestHash"])
            && StringComparer.OrdinalIgnoreCase.Equals(contentHash, (string)manifest["hashes"]?["contentHash"])
            && StringComparer.OrdinalIgnoreCase.Equals(packageHash, (string)manifest["hashes"]?["packageHash"]),
        "fixed fixture hashes mismatch manifest=" + manifestHash + " content=" + contentHash + " package=" + packageHash);

    WorldbookVerifiedPackage verified = WorldbookPackageIntegrity.ReadAndVerify(
        manifestPath,
        "awake:test-worldbook",
        "1.0.0",
        "universe",
        manifestHash,
        contentHash,
        packageHash);
    WorldKnowledgeSnapshot snapshot = WorldKnowledgeLoader.LoadVerified(verified);
    Require(snapshot.PackageId == "awake:test-worldbook" && snapshot.WorldId == "awake:world:fixed_test", "verified fixture metadata was not loaded");
    Require(snapshot.Entries.Count == 2 && snapshot.Identities.Count == 3, "verified fixture contents were not loaded");

    var service = new WorldKnowledgeQueryService(snapshot);
    WorldKnowledgeQueryResult commoner = service.Query(new WorldbookQuery
    {
        IdentityId = "profile.commoner",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "secret",
        PlayerText = "粮税"
    });
    Require(commoner.State == "partial" && commoner.HitIds.Contains("awake:entry:grain_tax") && commoner.RetrievedText.Contains("征粮的大致时节", StringComparison.Ordinal), "verified fixture commoner query failed");

    WorldKnowledgeQueryResult soldier = service.Query(new WorldbookQuery
    {
        IdentityId = "profile.soldier",
        KnowledgeScope = "national",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "detail",
        EffectiveDetailAvailable = true,
        RequestedDetail = "secret",
        Role = "soldier",
        PlayerText = "北境战争"
    });
    Require(soldier.State == "partial" && soldier.HitIds.Contains("awake:entry:northern_war") && soldier.RetrievedText.Contains("行军、补给与交战经过", StringComparison.Ordinal) && !soldier.RetrievedText.Contains("调动、盟约与损失", StringComparison.Ordinal), "verified fixture soldier query exposed the wrong detail");

    WorldKnowledgeQueryResult noble = service.Query(new WorldbookQuery
    {
        IdentityId = "profile.noble",
        KnowledgeScope = "elite",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "secret",
        EffectiveDetailAvailable = true,
        RequestedDetail = "secret",
        PlayerText = "北境战争"
    });
    Require(noble.State == "known" && noble.RetrievedText.Contains("调动、盟约与损失", StringComparison.Ordinal), "verified fixture noble query failed");
}

static void TestCanonicalEntityIds()
{
    Require(WorldbookEntityId.Canonical("kingdom", "entity.kingdom.vlandia") == "awake:kingdom:vlandia", "kingdom entity id mapping failed");
    Require(WorldbookEntityId.Canonical("culture", "calradia:culture:vlandia") == "awake:culture:vlandia", "culture entity id mapping failed");
    Require(WorldbookEntityId.Canonical("settlement", "awake:entity:settlement:pravend") == "awake:settlement:pravend", "settlement entity id mapping failed");
    Require(WorldbookEntityId.Canonical("role", "headman") == "awake:role:headman", "role entity id mapping failed");
    Require(WorldbookEntityId.Canonical("kingdom", "entity.culture.vlandia") == string.Empty, "wrong entity type was accepted");
}

static void TestPackageIntegrity()
{
    string contractRoot = FindContractRoot();
    JObject goldenManifest = JObject.Parse(File.ReadAllText(Path.Combine(contractRoot, "golden-manifest-input.json")));
    JObject goldenContent = JObject.Parse(File.ReadAllText(Path.Combine(contractRoot, "golden-content-input.json")));
    JObject goldenHashes = JObject.Parse(File.ReadAllText(Path.Combine(contractRoot, "golden-hashes.json")));
    string actualManifestHash = WorldbookPackageIntegrity.ComputeManifestHash(goldenManifest);
    string actualContentHash = WorldbookPackageIntegrity.ComputeContentHash("runtime.json", (JObject)goldenContent["runtime.json"], "index.json", (JObject)goldenContent["index.json"]);
    Require(actualManifestHash == goldenHashes["manifestHash"]?.Value<string>(), "runtime manifest golden hash mismatch actual=" + actualManifestHash + " expected=" + goldenHashes["manifestHash"]?.Value<string>());
    Require(actualContentHash == goldenHashes["contentHash"]?.Value<string>(), "runtime content golden hash mismatch actual=" + actualContentHash + " expected=" + goldenHashes["contentHash"]?.Value<string>());
    Require(WorldbookPackageIntegrity.ComputePackageHash(goldenHashes["manifestHash"]?.Value<string>(), goldenHashes["contentHash"]?.Value<string>()) == goldenHashes["packageHash"]?.Value<string>(), "runtime package golden hash mismatch");

    string root = Path.Combine(Path.GetTempPath(), "awake-worldbook-integrity-smoke", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        WriteVerifiedPackage(root, "calradia:base", "calradia:world:calradia", out string manifestHash, out string contentHash, out string packageHash);
        WorldbookVerifiedPackage verified = WorldbookPackageIntegrity.ReadAndVerify(Path.Combine(root, "manifest.json"), "calradia:base", "1.0.0", "universe", manifestHash, contentHash, packageHash);
        Require(verified.PackageHash == packageHash, "verified package hash was not retained");

        JObject runtime = JObject.Parse(File.ReadAllText(Path.Combine(root, "runtime.json")));
        runtime["entries"]![0]!["summary"]!["zh-CN"] = "被篡改的摘要";
        File.WriteAllText(Path.Combine(root, "runtime.json"), runtime.ToString(Newtonsoft.Json.Formatting.None));
        RequireThrows(() => WorldbookPackageIntegrity.ReadAndVerify(Path.Combine(root, "manifest.json")), "WB2-HASH-MISMATCH:content", "runtime tamper was accepted");

        WriteVerifiedPackage(root, "calradia:base", "calradia:world:calradia", out manifestHash, out contentHash, out packageHash);
        JObject index = JObject.Parse(File.ReadAllText(Path.Combine(root, "index.json")));
        index["entryIds"]![0] = "awake:entry:tampered";
        File.WriteAllText(Path.Combine(root, "index.json"), index.ToString(Newtonsoft.Json.Formatting.None));
        RequireThrows(() => WorldbookPackageIntegrity.ReadAndVerify(Path.Combine(root, "manifest.json")), "WB2-INDEX-MISMATCH:entry_ids", "index tamper was accepted");
    }
    finally { try { Directory.Delete(root, true); } catch { } }
}

static void TestIdentityAccess()
{
    string root = Path.Combine(Path.GetTempPath(), "awake-worldbook-identity-smoke", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var runtime = new JObject
        {
            ["schemaVersion"] = "awake.worldbook.v2",
            ["packageId"] = "calradia:base",
            ["version"] = "1.0.0",
            ["worldId"] = "calradia:world:calradia",
            ["revision"] = 1,
            ["entries"] = new JArray(
                Entry("calradia:entry:royal_archive", "政治", "王室档案", new[] { "王室档案" },
                    Expression("calradia:expression:royal_secret", "secret", "贵族才知道的王室档案。", Grant("noble")),
                    Expression("calradia:expression:royal_referral", "rumor", "", Grant("commoner", new[] { "awake:referral:broker" }), enabled: false)),
                Entry("calradia:entry:mature_noble", "政治", "老贵族秘闻", new[] { "老贵族秘闻" },
                    Expression("calradia:expression:mature", "secret", "年长贵族掌握的秘闻。", Grant("noble_mature", conditions: new JObject { ["min_age"] = 45 }))),
                Entry("calradia:entry:steward_secret", "经济", "账册秘闻", new[] { "账册秘闻" },
                    Expression("calradia:expression:steward", "secret", "高管理贵族掌握的账册秘闻。", Grant("noble_high_steward", conditions: new JObject { ["min_management"] = 80 }))),
                Entry("calradia:entry:headman", "政治", "本国政事", new[] { "本国政事" },
                    Expression("calradia:expression:headman", "detail", "头人熟悉本国政事。", Grant("headman", conditions: new JObject { ["kingdom_ids"] = new JArray("calradia:kingdom:vlandia") }))),
                Entry("calradia:entry:soldier", "战争", "战争常识", new[] { "战争常识" },
                    Expression("calradia:expression:soldier", "detail", "士兵熟悉战争常识。", Grant("soldier", conditions: new JObject { ["role_ids"] = new JArray("soldier") })))
            ),
            ["identities"] = new JArray(
                Identity("commoner"),
                Identity("noble"),
                Identity("noble_mature", "noble"),
                Identity("noble_high_steward", "noble"),
                Identity("headman", "commoner"),
                Identity("soldier", "commoner")),
            ["referrals"] = new JArray(new JObject
            {
                ["id"] = "awake:referral:broker",
                ["displayName"] = new JObject { ["zh-CN"] = "公证商人" },
                ["reason"] = new JObject { ["zh-CN"] = "他知道更多。" },
                ["priority"] = 10
            }),
            ["indexes"] = new JObject { ["keywordToEntryIds"] = new JObject(), ["domainToEntryIds"] = new JObject() }
        };
        File.WriteAllText(Path.Combine(root, "manifest.json"), Manifest().ToString(Newtonsoft.Json.Formatting.None));
        File.WriteAllText(Path.Combine(root, "runtime.json"), runtime.ToString(Newtonsoft.Json.Formatting.None));
        File.WriteAllText(Path.Combine(root, "index.json"), "{}");
        WorldKnowledgeQueryService service = new WorldKnowledgeQueryService(WorldKnowledgeLoader.Load(Path.Combine(root, "manifest.json")));
        WorldKnowledgeQueryResult commoner = service.Query(new WorldbookQuery { IdentityId = "profile.commoner", KnowledgeScope = "local", KnowledgeScopeAvailable = true, EffectiveDetail = "summary", EffectiveDetailAvailable = true, PlayerText = "王室档案" });
        Require(commoner.State == "not_found" && commoner.ReferralIds.Count == 0 && string.IsNullOrWhiteSpace(commoner.RetrievedText), "disabled referral expression leaked a referral");
        Require(service.Query(new WorldbookQuery { IdentityId = "profile.noble", KnowledgeScope = "elite", KnowledgeScopeAvailable = true, EffectiveDetail = "secret", EffectiveDetailAvailable = true, PlayerText = "王室档案" }).State == "known", "noble did not receive noble archive");
        Require(service.Query(new WorldbookQuery { IdentityId = "profile.noble_mature", KnowledgeScope = "elite", KnowledgeScopeAvailable = true, EffectiveDetail = "secret", EffectiveDetailAvailable = true, Age = 50, PlayerText = "老贵族秘闻" }).State == "known", "mature noble age gate failed");
        Require(service.Query(new WorldbookQuery { IdentityId = "profile.noble_high_steward", KnowledgeScope = "elite", KnowledgeScopeAvailable = true, EffectiveDetail = "secret", EffectiveDetailAvailable = true, Skills = new Dictionary<string, int> { ["management"] = 80 }, PlayerText = "账册秘闻" }).State == "known", "management gate failed");
        Require(service.Query(new WorldbookQuery { IdentityId = "profile.headman", KnowledgeScope = "national", KnowledgeScopeAvailable = true, EffectiveDetail = "detail", EffectiveDetailAvailable = true, RequestedDetail = "detail", KingdomId = "vlandia", PlayerText = "本国政事" }).State == "known", "headman national gate failed");
        Require(service.Query(new WorldbookQuery { IdentityId = "profile.headman", KnowledgeScope = "national", KnowledgeScopeAvailable = true, EffectiveDetail = "detail", EffectiveDetailAvailable = true, RequestedDetail = "detail", KingdomId = "battania", PlayerText = "本国政事" }).State == "not_found", "headman read foreign national knowledge");
        Require(service.Query(new WorldbookQuery { IdentityId = "profile.soldier", KnowledgeScope = "national", KnowledgeScopeAvailable = true, EffectiveDetail = "detail", EffectiveDetailAvailable = true, RequestedDetail = "detail", Role = "soldier", PlayerText = "战争常识" }).State == "known", "soldier war knowledge failed");
    }
    finally { try { Directory.Delete(root, true); } catch { } }
}

static void TestPermissionStateMachine()
{
    string root = Path.Combine(Path.GetTempPath(), "awake-worldbook-permission-smoke", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var runtime = new JObject
        {
            ["schemaVersion"] = "awake.worldbook.v2",
            ["packageId"] = "calradia:base",
            ["version"] = "1.0.0",
            ["worldId"] = "calradia:world:calradia",
            ["revision"] = 1,
            ["entries"] = new JArray(
                Entry("calradia:entry:tiered_fact", "政治", "分层事实", new[] { "分层事实" },
                    Expression("calradia:expression:tiered_rumor", "rumor", "平民只听过传闻。", Grant("commoner", scope: "local", minDetail: "rumor")),
                    Expression("calradia:expression:tiered_secret", "secret", "贵族掌握完整秘密。", Grant("noble", scope: "elite", minDetail: "secret"))),
                Entry("calradia:entry:cross_expression_deny", "政治", "跨表达封锁", new[] { "跨表达封锁" },
                    Expression("calradia:expression:deny_grant", "summary", "普通人知道摘要。", Grant("commoner", scope: "local", minDetail: "summary")),
                    Expression("calradia:expression:deny_only", "detail", "女性被明确封锁。", Grant("public", scope: "local", minDetail: "rumor"), denies: new[] { Grant("commoner", scope: "local", minDetail: "rumor", conditions: new JObject { ["is_female"] = true }) })),
                Entry("calradia:entry:unknown_capability", "经济", "能力未知", new[] { "能力未知" },
                    Expression("calradia:expression:unknown_capability", "detail", "需要能力证明的内容。", Grant("commoner", scope: "national", minDetail: "detail"))),
                Entry("calradia:entry:public_referral", "政治", "公开转问", new[] { "公开转问" },
                    Expression("calradia:expression:public_referral", "detail", "广泛知识不应直接返回。", Grant("commoner", new[] { "awake:referral:public_merchant", "awake:referral:private_archive" }, scope: "national", minDetail: "detail"))),
                Entry("calradia:entry:adult_gate", "文化", "成人门控", new[] { "成人门控" },
                    Expression("calradia:expression:adult", "summary", "成人拓展内容。", Grant("commoner", scope: "local", minDetail: "summary")))
            ),
            ["identities"] = new JArray(Identity("commoner"), Identity("noble")),
            ["referrals"] = new JArray(
                new JObject { ["id"] = "awake:referral:public_merchant", ["displayName"] = new JObject { ["zh-CN"] = "公证商人" }, ["reason"] = new JObject { ["zh-CN"] = "公开询问" }, ["priority"] = 0, ["publiclyAskable"] = true },
                new JObject { ["id"] = "awake:referral:private_archive", ["displayName"] = new JObject { ["zh-CN"] = "私密档案员" }, ["reason"] = new JObject { ["zh-CN"] = "不应公开" }, ["priority"] = 0, ["publiclyAskable"] = false }),
            ["indexes"] = new JObject { ["keywordToEntryIds"] = new JObject(), ["domainToEntryIds"] = new JObject() }
        };
        File.WriteAllText(Path.Combine(root, "manifest.json"), Manifest().ToString(Newtonsoft.Json.Formatting.None));
        File.WriteAllText(Path.Combine(root, "runtime.json"), runtime.ToString(Newtonsoft.Json.Formatting.None));
        File.WriteAllText(Path.Combine(root, "index.json"), "{}");
        WorldKnowledgeSnapshot permissionSnapshot = WorldKnowledgeLoader.Load(Path.Combine(root, "manifest.json"));
        WorldKnowledgeQueryService service = new WorldKnowledgeQueryService(permissionSnapshot);

        WorldKnowledgeQueryResult partial = service.Query(new WorldbookQuery
        {
            IdentityId = "profile.commoner",
            KnowledgeScope = "local",
            KnowledgeScopeAvailable = true,
            EffectiveDetail = "summary",
            EffectiveDetailAvailable = true,
            RequestedDetail = "secret",
            PlayerText = "分层事实"
        });
        Require(partial.State == "partial" && partial.RetrievedText.Contains("平民只听过传闻。", StringComparison.Ordinal), "lower detail did not produce partial result");

        WorldKnowledgeQueryResult unknown = service.Query(new WorldbookQuery { IdentityId = "profile.commoner", PlayerText = "能力未知" });
        Require(unknown.State == "blocked" && string.IsNullOrWhiteSpace(unknown.RetrievedText) && unknown.HitIds.Count == 0, "unknown capability did not fail closed");

        WorldKnowledgeQueryResult referral = service.Query(new WorldbookQuery
        {
            IdentityId = "profile.commoner",
            KnowledgeScope = "local",
            KnowledgeScopeAvailable = true,
            EffectiveDetail = "summary",
            EffectiveDetailAvailable = true,
            RequestedDetail = "summary",
            PlayerText = "公开转问"
        });
        Require(referral.State == "referral" && referral.ReferralIds.Count == 1 && referral.ReferralIds[0] == "awake:referral:public_merchant", "non-public referral leaked or public referral was lost");

        WorldKnowledgeQueryResult unknownReferral = service.Query(new WorldbookQuery { IdentityId = "profile.anonymous", PlayerText = "公开转问" });
        Require(unknownReferral.ReferralIds.Count == 0, "unknown capability emitted referral");

        var identityEvaluation = WorldbookIdentityEvaluator.Evaluate(new WorldbookQuery
        {
            IdentityId = "profile.commoner",
            KnowledgeScope = "local",
            KnowledgeScopeAvailable = true,
            EffectiveDetail = "summary",
            EffectiveDetailAvailable = true
        }, permissionSnapshot);
        Require(!WorldbookIdentityEvaluator.CapabilityMatches(new WorldKnowledgeRule
        {
            IdentityId = "awake:identity:commoner",
            Scope = "unclassified",
            MinDetail = "summary"
        }, identityEvaluation), "unknown rule scope was not rejected");
        Require(!WorldbookIdentityEvaluator.CapabilityMatches(new WorldKnowledgeRule
        {
            IdentityId = "awake:identity:commoner",
            Scope = "local",
            MinDetail = "unclassified"
        }, identityEvaluation), "unknown rule detail was not rejected");

        WorldKnowledgeQueryResult allowed = service.Query(new WorldbookQuery { IdentityId = "profile.commoner", KnowledgeScope = "local", KnowledgeScopeAvailable = true, EffectiveDetail = "summary", EffectiveDetailAvailable = true, RequestedDetail = "summary", IsFemale = false, PlayerText = "跨表达封锁" });
        Require(allowed.State == "known" && allowed.RetrievedText.Contains("普通人知道摘要。", StringComparison.Ordinal), "cross-expression grant was not available when deny did not match");

        WorldKnowledgeQueryResult denied = service.Query(new WorldbookQuery { IdentityId = "profile.commoner", KnowledgeScope = "local", KnowledgeScopeAvailable = true, EffectiveDetail = "summary", EffectiveDetailAvailable = true, RequestedDetail = "summary", IsFemale = true, PlayerText = "跨表达封锁" });
        Require(denied.State == "blocked" && string.IsNullOrWhiteSpace(denied.RetrievedText) && denied.ReferralIds.Count == 0, "cross-expression deny was bypassed");

        runtime["extensions"] = new JObject { ["contentTier"] = "adult_optional" };
        File.WriteAllText(Path.Combine(root, "runtime.json"), runtime.ToString(Newtonsoft.Json.Formatting.None));
        WorldKnowledgeQueryService gated = new WorldKnowledgeQueryService(WorldKnowledgeLoader.Load(Path.Combine(root, "manifest.json")));
        WorldKnowledgeQueryResult blocked = gated.Query(new WorldbookQuery { IdentityId = "profile.commoner", KnowledgeScope = "local", KnowledgeScopeAvailable = true, EffectiveDetail = "summary", EffectiveDetailAvailable = true, ContentTier = "pure", PlayerText = "成人门控" });
        Require(blocked.State == "blocked" && string.IsNullOrWhiteSpace(blocked.RetrievedText) && blocked.HitIds.Count == 0 && blocked.ReferralIds.Count == 0, "content gate leaked knowledge");
    }
    finally { try { Directory.Delete(root, true); } catch { } }
}

static void TestRegistrySelection()
{
    string root = Path.Combine(Path.GetTempPath(), "awake-worldbook-registry-smoke", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "base"));
        Directory.CreateDirectory(Path.Combine(root, "alt"));
        WriteVerifiedPackage(Path.Combine(root, "base"), "calradia:base", "calradia:world:calradia", out string baseManifestHash, out string baseContentHash, out string basePackageHash);
        WriteVerifiedPackage(Path.Combine(root, "alt"), "other:world", "other:world:other", out string altManifestHash, out string altContentHash, out string altPackageHash);
        JObject registry = new JObject
        {
            ["schemaVersion"] = "awake.worldbook.registry.v1",
            ["packages"] = new JArray(
                new JObject { ["packageId"] = "calradia:base", ["version"] = "1.0.0", ["kind"] = "universe", ["enabledByDefault"] = true, ["relativePath"] = "base/manifest.json", ["manifestHash"] = baseManifestHash, ["contentHash"] = baseContentHash, ["packageHash"] = basePackageHash },
                new JObject { ["packageId"] = "other:world", ["version"] = "1.0.0", ["kind"] = "universe", ["enabledByDefault"] = false, ["relativePath"] = "alt/manifest.json", ["manifestHash"] = altManifestHash, ["contentHash"] = altContentHash, ["packageHash"] = altPackageHash })
        };
        string registryPath = Path.Combine(root, "registry.json");
        File.WriteAllText(registryPath, registry.ToString(Newtonsoft.Json.Formatting.None));
        WorldbookPackageRegistry loaded = WorldbookPackageRegistry.Load(registryPath);
        Require(loaded.Select().PackageId == "calradia:base", "registry default selection failed");
        Require(loaded.Select("other:world").PackageId == "other:world", "registry explicit selection failed");
        registry["packages"][0]["relativePath"] = "../outside/manifest.json";
        File.WriteAllText(registryPath, registry.ToString(Newtonsoft.Json.Formatting.None));
        RequireThrows(() => WorldbookPackageRegistry.Load(registryPath).Select(), "WB2-PATH-ESCAPE", "registry path escape accepted");
        registry["packages"][0]["relativePath"] = "base/manifest.json";
        registry["packages"][0]["packageHash"] = "wrong-hash";
        File.WriteAllText(registryPath, registry.ToString(Newtonsoft.Json.Formatting.None));
        RequireThrows(() => WorldbookPackageRegistry.Load(registryPath).Select(), "WB2-HASH-MISMATCH:package", "registry hash mismatch accepted");
    }
    finally { try { Directory.Delete(root, true); } catch { } }
}

static void TestRepositoryPackageForm()
{
    // Runs against the REAL AWAKE/ModuleData/Worldbook tree, not a synthetic fixture: a registry.v1
    // at the root, one packages/<world>/ runtime three, and the persona layer beside them
    // (2026-09-14, see docs/worldbook-studio-plan/RUNTIME-MAPPING-CONTRACT.md).
    // ReadAndVerify recomputes all three hashes over the shipped runtime.json (~1.5 MB), so a single
    // byte of drift in the committed package turns this red.
    string worldbookRoot = FindRepositoryWorldbookRoot();
    WorldbookPackageRegistry registry = WorldbookPackageRegistry.Load(Path.Combine(worldbookRoot, "manifest.json"));
    WorldbookActivationState activation = registry.Select();
    // NOTE the TWO-segment package_id (ns:name). Three segments (`awake:worldbook:calradia`) violate
    // contract/v1/common.schema.json#/$defs/package_id and were corrected on 2026-09-15; `worldId`
    // below is a stable_id and legitimately keeps three segments. Do not "unify" the two patterns.
    Require(activation.PackageId == "awake:worldbook.calradia", "repository registry default selection is not the calradia world: " + activation.PackageId);
    Require(activation.Version == "1.0.0", "repository registry version mismatch: " + activation.Version);
    string expectedRoot = Path.GetFullPath(worldbookRoot).TrimEnd(Path.DirectorySeparatorChar);
    Require(activation.ManifestPath.StartsWith(expectedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "repository registry resolved outside the worldbook root: " + activation.ManifestPath);

    WorldbookVerifiedPackage verified = registry.LastSelectedPackage;
    Require(verified.Manifest["worldId"]?.Value<string>() == "awake:world:calradia", "repository package worldId mismatch");
    JArray runtimeEntries = verified.Runtime["entries"] as JArray;
    JArray indexEntries = verified.Index["entryIds"] as JArray;
    Require(runtimeEntries != null && runtimeEntries.Count > 0, "repository package runtime carries no entries");
    Require(indexEntries != null && indexEntries.Count == runtimeEntries.Count, "repository package index does not cover the runtime entries");

    // The same directory serves the persona layer. src/PersonaRootLocator.cs must find it by SHAPE
    // from deep inside the package, without ever reading the worldbook manifest.
    string personaRoot = PersonaRootLocator.Locate(Path.GetDirectoryName(verified.ManifestPath));
    Require(personaRoot != null && Path.GetFullPath(personaRoot).TrimEnd(Path.DirectorySeparatorChar) == expectedRoot, "persona root locator did not resolve the shared worldbook root: " + (personaRoot ?? "<null>"));
    string definitions = Path.Combine(personaRoot, "persona_definitions", "definitions");
    Require(Directory.Exists(definitions) && Directory.GetFiles(definitions, "*.json").Length > 0, "persona definitions are missing beside the worldbook registry");
    Require(File.Exists(Path.Combine(personaRoot, "persona_definitions", "tag_registry.json")), "persona tag registry is missing beside the worldbook registry");
}

static string FindRepositoryWorldbookRoot()
{
    // AWAKE_WORLDBOOK_ROOT points the same checks at a DEPLOYED copy (e.g. the game module's
    // ModuleData/Worldbook) instead of the repository one. That is how "what the game will actually
    // read" gets verified by the real reader rather than inferred from a byte-identical copy.
    string overridden = Environment.GetEnvironmentVariable("AWAKE_WORLDBOOK_ROOT");
    if (!string.IsNullOrWhiteSpace(overridden))
    {
        if (!File.Exists(Path.Combine(overridden, "manifest.json")))
        {
            throw new DirectoryNotFoundException("AWAKE_WORLDBOOK_ROOT has no manifest.json: " + overridden);
        }
        return overridden;
    }
    DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
    for (int i = 0; i < 10 && current != null; i++, current = current.Parent)
    {
        string candidate = Path.Combine(current.FullName, "ModuleData", "Worldbook", "manifest.json");
        if (File.Exists(candidate)) return Path.Combine(current.FullName, "ModuleData", "Worldbook");
    }
    throw new DirectoryNotFoundException("repository worldbook root not found (ModuleData/Worldbook/manifest.json)");
}

static void TestWeeklyReport()
{
    var records = new List<WorldEventRecord>
    {
        new WorldEventRecord("awake:event:politics-a", 1, "crown", "politics", "王位争执"),
        new WorldEventRecord("awake:event:politics-a", 1, "crown", "politics", "重复提交"),
        new WorldEventRecord("awake:event:economy-b", 2, "tax", "economy", "税粮上涨"),
        new WorldEventRecord("awake:event:culture-c", 3, "feast", "culture", "节庆举行"),
        new WorldEventRecord("awake:event:culture-d", 3, "feast", "culture", "节庆举行"),
        new WorldEventRecord("awake:event:war-d", 4, "battle", "war", "边境交战"),
        new WorldEventRecord("awake:event:old", 0, "crown", "politics", "窗口外旧事")
    };
    string first = WeeklyReportService.Build(records, 7).ToString(Newtonsoft.Json.Formatting.None);
    string second = WeeklyReportService.Build(records, 7).ToString(Newtonsoft.Json.Formatting.None);
    JObject report = JObject.Parse(first);
    Require(first == second, "weekly report is not deterministic");
    Require(report["sections"] is JArray sections && sections.Count == 4, "weekly report domain sections incomplete");
    Require(report["sourceEventIds"] is JArray sourceIds && sourceIds.Count == 5, "weekly report source closure incomplete");
    Require(report["sections"]?[0]?["items"] is JArray politics && politics.Count == 1, "duplicate event id was not removed");
    Require(report["sections"]?[2]?["items"] is JArray culture && culture.Count == 2, "same text with distinct event ids was collapsed");
    Require(((DateTimeOffset)DateTimeOffset.Parse((string)report["period"]?["end"])).Day == 9, "weekly report period end is not exclusive");
    Require(WeeklyReportService.BuildText(records, 7).Contains("战争与领地", StringComparison.Ordinal), "weekly report text rendering failed");
}

static void TestWorldEventLedger()
{
    WorldEventLedger.ClearForTesting();
    AwakeRuntime.WorldStateStore = new WorldStateStore();
    Require(WorldEventLedger.RecordAsync(10, "tax", "粮价上涨", "source.tax.10", CancellationToken.None).GetAwaiter().GetResult().Status == WorldEventAppendResult.Persisted, "first event was rejected");
    Require(WorldEventLedger.RecordAsync(10, "tax", "粮价上涨", "source.tax.10", CancellationToken.None).GetAwaiter().GetResult().Status == WorldEventAppendResult.DuplicateConfirmed, "same event key was not idempotent");
    Require(WorldEventLedger.RecordAsync(10, "tax", "粮价上涨", "source.tax.11", CancellationToken.None).GetAwaiter().GetResult().Status == WorldEventAppendResult.Persisted, "same text with a new event key was collapsed");
    Require(WorldEventLedger.Count == 2, "ledger did not retain exactly one record per event key");

    WorldEventLedger.ClearForTesting();
    var replayStore = new WorldStateStore
    {
        Document = new JObject
        {
                ["records"] = new JArray(
                new JObject { ["id"] = "awake:event:replay-1", ["eventKey"] = "replay.1", ["day"] = 1, ["kind"] = "battle", ["domain"] = "war", ["occurredAt"] = "1970-01-02T00:00:00.0000000+00:00", ["text"] = "边境交战" },
                new JObject { ["id"] = "awake:event:replay-1b", ["eventKey"] = "replay.1", ["day"] = 1, ["kind"] = "battle", ["text"] = "不同 ID 的重复重放" },
                new JObject { ["id"] = "awake:event:replay-1", ["eventKey"] = "replay.1", ["day"] = 1, ["kind"] = "battle", ["text"] = "同 ID 的重复重放" },
                new JObject { ["id"] = "awake:event:replay-2", ["eventKey"] = "replay.2", ["day"] = 2, ["kind"] = "battle", ["text"] = "第二次交战" })
        }
    };
    AwakeRuntime.WorldStateStore = replayStore;
    WorldEventLedger.LoadFromStoreAsync(CancellationToken.None).GetAwaiter().GetResult();
    Require(WorldEventLedger.Count == 2, "reload/replay did not deduplicate stable event keys");
    WorldEventRecord replayed = WorldEventServices.Recorder.SnapshotWeek(2).Single(record => record.EventKey == "replay.1");
    Require(replayed.Domain == "war" && replayed.OccurredAt.Day == 2, "reload did not preserve event contract fields");

    WorldEventLedger.ClearForTesting();
    AwakeRuntime.WorldStateStore = new WorldStateStore();
    var concurrentResults = Enumerable.Range(0, 32)
        .Select(_ => WorldEventLedger.RecordAsync(20, "battle", "并发提交", "source.concurrent", CancellationToken.None))
        .ToArray();
    System.Threading.Tasks.Task.WhenAll(concurrentResults).GetAwaiter().GetResult();
    Require(concurrentResults.Count(task => task.Result.Status == WorldEventAppendResult.Persisted) == 1 && WorldEventLedger.Count == 1, "concurrent duplicate event keys were not serialized idempotently");

    WorldEventLedger.ClearForTesting();
    for (int index = 0; index < 60; index++)
        Require(WorldEventLedger.RecordAsync(30, "event", "容量测试", "source.capacity." + index, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "capacity event was not persisted");
    Require(WorldEventLedger.Count == 50, "ledger capacity boundary changed");
    Require(WorldEventLedger.SnapshotWeek(30).Select(x => x.EventKey).Distinct(StringComparer.Ordinal).Count() == 50, "capacity snapshot contains duplicate event keys");
    Require(WorldEventLedger.RecordAsync(30, "event", "容量测试", "source.capacity.0", CancellationToken.None).GetAwaiter().GetResult().Status == WorldEventAppendResult.DuplicateConfirmed, "recently evicted event key was accepted again");
    WorldEventLedger.ClearForTesting();
    AwakeRuntime.WorldStateStore = null;
}

static void TestEventContractParity()
{
    WorldEventLedger.ClearForTesting();
    AwakeRuntime.WorldStateStore = new WorldStateStore();
    Require(WorldEventServices.Recorder.RecordAsync(40, "tax change", "粮价上涨", "parity.tax.40", CancellationToken.None).GetAwaiter().GetResult().Status == WorldEventAppendResult.Persisted, "recorder facade did not persist event");
    Require(WorldEventServices.Recorder.Count == 1, "recorder facade count is not ledger-backed");

    WorldEventRecord record = WorldEventServices.Recorder.SnapshotWeek(40)[0];
    JObject eventContract = WorldEventContract.ToEventRecord(record);
    Require(WorldEventContract.TryValidateEventRecord(eventContract, out string eventError), eventError);
    Require((string)eventContract["eventType"] == "tax_change", "event type normalization did not produce a stable contract value");
    Require((string)eventContract["domain"] == "economy", "event domain projection did not preserve inferred domain");

    JObject report = WorldEventServices.Reports.Build(WorldEventServices.Recorder.SnapshotWeek(40), 40);
    Require(WorldEventContract.TryValidateWeeklyReport(report, out string reportError), reportError);
    Require((string)report["generatedBy"] == "awake:system:weekly-report-generator", "report facade did not use the contract generator");

    JObject tamperedEvent = (JObject)eventContract.DeepClone();
    tamperedEvent["unexpected"] = true;
    Require(!WorldEventContract.TryValidateEventRecord(tamperedEvent, out _), "unknown event contract property was accepted");

    JObject tamperedFact = (JObject)eventContract.DeepClone();
    tamperedFact["facts"][0]["factId"] = "duplicate";
    Require(!WorldEventContract.TryValidateEventRecord(tamperedFact, out _), "invalid event fact was accepted");

    JObject tamperedReport = (JObject)report.DeepClone();
    tamperedReport["unexpected"] = true;
    Require(!WorldEventContract.TryValidateWeeklyReport(tamperedReport, out _), "unknown report contract property was accepted");

    WorldEventLedger.ClearForTesting();
    WorldStateStore persistenceStore = new WorldStateStore();
    AwakeRuntime.WorldStateStore = persistenceStore;
    Require(WorldEventServices.Recorder.RecordAsync(41, "tax", "存储字段", "parity.persist.41", CancellationToken.None).GetAwaiter().GetResult().Status == WorldEventAppendResult.Persisted, "persistent recorder facade did not persist event");
    Require(persistenceStore.LastDomain == "economy" && persistenceStore.LastOccurredAt.Day == 11, "storage append lost event contract projection fields");
    WorldEventLedger.ClearForTesting();
    AwakeRuntime.WorldStateStore = null;
}

static void TestDynamicKnowledgeProjection()
{
    WorldEventLedger.ClearForTesting();
    WorldEventRecord defaultAudience = new WorldEventRecord("awake:event:default-audience", 6, "tax", "economy", "默认受众", DateTimeOffset.UtcNow, "dynamic.default", null);
    WorldEventRecord emptyAudience = new WorldEventRecord("awake:event:empty-audience", 6, "tax", "economy", "显式空受众", DateTimeOffset.UtcNow, "dynamic.empty", Array.Empty<string>());
    WorldEventRecord invalidAudience = new WorldEventRecord("awake:event:invalid-audience", 6, "tax", "economy", "非法受众", DateTimeOffset.UtcNow, "dynamic.invalid", new[] { "??" });
    Require(defaultAudience.VisibilityIdentityIds.Contains("awake:identity:headman", StringComparer.Ordinal)
        && !defaultAudience.VisibilityIdentityIds.Contains("awake:identity:commoner", StringComparer.Ordinal), "missing dynamic audience did not use the conservative default");
    Require(emptyAudience.VisibilityIdentityIds.Count == 0 && invalidAudience.VisibilityIdentityIds.Count == 0, "explicit empty or invalid dynamic audiences did not fail closed");
    WorldKnowledgeSnapshot snapshot = WorldKnowledgeLoader.Load(Path.Combine(FindFixedFixtureRoot(), "manifest.json"));
    var query = new WorldKnowledgeQueryService(snapshot);
    var projection = new WorldKnowledgeProjectionService();
    projection.Bind(snapshot, query);
    var record = new WorldEventRecord("awake:event:dynamic-1", 7, "battle", "war", "边境营地失守", DateTimeOffset.UtcNow, "dynamic.event.1", new[] { "soldier" });
    WorldEventLedgerSnapshot firstProjection = new WorldEventLedgerSnapshot(0, 1, new[] { record });
    Require(projection.TryReplaceEvents(firstProjection, null), "initial dynamic projection was rejected");
    WorldKnowledgeQueryResult soldier = query.Query(new WorldbookQuery
    {
        IdentityId = "profile.soldier",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "边境营地",
        MaximumBytes = 4096
    });
    Require(soldier.State == "known" && soldier.HitIds.Count == 1 && soldier.SourceIds.Contains(record.EventId), "persisted event did not enter the unified knowledge query");
    WorldKnowledgeQueryResult commoner = query.Query(new WorldbookQuery
    {
        IdentityId = "profile.commoner",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "边境营地",
        MaximumBytes = 4096
    });
    Require(commoner.State != "known" && string.IsNullOrWhiteSpace(commoner.RetrievedText), "dynamic audience whitelist was not enforced");
    Require(projection.TryReplaceEvents(firstProjection, null), "rebuilding dynamic projection was rejected");
    WorldKnowledgeQueryResult repeated = query.Query(new WorldbookQuery
    {
        IdentityId = "profile.soldier",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "边境营地",
        MaximumBytes = 4096
    });
    Require(repeated.HitIds.Count == 1, "rebuilding dynamic projection duplicated an event entry");
    WorldEventRecord newerRecord = new WorldEventRecord("awake:event:dynamic-2", 8, "battle", "war", "第二条边境消息", DateTimeOffset.UtcNow, "dynamic.event.2", new[] { "soldier" });
    WorldEventLedgerSnapshot newerProjection = new WorldEventLedgerSnapshot(0, 2, 2, new[] { record, newerRecord });
    Require(projection.TryReplaceEvents(newerProjection, null), "newer dynamic projection was rejected");
    Require(!projection.TryReplaceEvents(firstProjection, null), "older dynamic projection rolled back the newer snapshot");
    WorldKnowledgeQueryResult afterRollbackAttempt = query.Query(new WorldbookQuery
    {
        IdentityId = "profile.soldier",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "第二条边境消息",
        MaximumBytes = 4096
    });
    Require(afterRollbackAttempt.SourceIds.Contains(newerRecord.EventId), "older dynamic projection removed a newer event");
    JObject report = WeeklyReportService.BuildWindow(new[] { record }, 1, 7);
    WorldEventLedgerSnapshot reportProjection = new WorldEventLedgerSnapshot(0, 2, 3, new[] { record, newerRecord });
    Require(projection.TryReplaceSources(reportProjection, new[] { report }, null), "weekly report projection was rejected");
    WorldKnowledgeQueryResult weekly = query.Query(new WorldbookQuery
    {
        IdentityId = "profile.soldier",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "周报",
        MaximumBytes = 4096
    });
    Require(weekly.State == "known" && weekly.ReportIds.Contains((string)report["reportId"]), "weekly report did not enter the unified knowledge query");
    WorldKnowledgeQueryResult unknown = query.Query(new WorldbookQuery { IdentityId = "profile.unknown", PlayerText = "边境营地", MaximumBytes = 4096 });
    Require(unknown.State != "known" && string.IsNullOrWhiteSpace(unknown.RetrievedText), "unknown identity received dynamic knowledge");
}

static void TestCampaignIsolation()
{
    WorldEventLedger.ClearForTesting();
    var queuedPending = new System.Threading.Tasks.TaskCompletionSource<WorldEventAppendResult>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
    var queuedStore = new WorldStateStore
    {
        PendingEventWrite = queuedPending,
        EventWriteStarted = new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously)
    };
    AwakeRuntime.WorldStateStore = queuedStore;
    WorldEventServices.QueueRecord(71, "battle", "旧战役排队写入", "campaign.queued.71");
    queuedStore.EventWriteStarted.Task.GetAwaiter().GetResult();
    WorldEventLedger.ClearForTesting();
    queuedStore.PendingEventWrite = null;
    queuedPending.SetResult(new WorldEventAppendResult { Status = WorldEventAppendResult.Persisted, EventId = "awake:event:queued-old", EventKey = "campaign.queued.71" });
    AwakeBackgroundTask.LastTask.GetAwaiter().GetResult();
    Require(WorldEventLedger.Count == 0, "queued old campaign write leaked into the new ledger");

    WorldEventLedger.ClearForTesting();
    var pending = new System.Threading.Tasks.TaskCompletionSource<WorldEventAppendResult>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
    var store = new WorldStateStore
    {
        PendingEventWrite = pending,
        EventWriteStarted = new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously)
    };
    AwakeRuntime.WorldStateStore = store;
    var oldWrite = WorldEventLedger.RecordAsync(70, "battle", "旧战役延迟写入", "campaign.old.70", CancellationToken.None);
    store.EventWriteStarted.Task.GetAwaiter().GetResult();
    WorldEventLedger.ClearForTesting();
    store.PendingEventWrite = null;
    pending.SetResult(new WorldEventAppendResult { Status = WorldEventAppendResult.Persisted, EventId = "awake:event:old", EventKey = "campaign.old.70" });
    WorldEventAppendResult stale = oldWrite.GetAwaiter().GetResult();
    Require(stale.Status == WorldEventAppendResult.PersistenceUnknown && stale.Code == "awake.world_event.stale_session" && WorldEventLedger.Count == 0, "old campaign write leaked into the new ledger");
    WorldEventAppendResult current = WorldEventLedger.RecordAsync(70, "battle", "新战役同键写入", "campaign.old.70", CancellationToken.None).GetAwaiter().GetResult();
    Require(current.Status == WorldEventAppendResult.Persisted && WorldEventLedger.Count == 1, "stale pending key blocked the new campaign");
    WorldEventLedger.ClearForTesting();
    AwakeRuntime.WorldStateStore = null;
}

static void TestEventPersistenceFailures()
{
    WorldEventLedger.ClearForTesting();
    var store = new WorldStateStore { FailEventWrites = true };
    AwakeRuntime.WorldStateStore = store;
    WorldEventAppendResult failed = WorldEventLedger.RecordAsync(60, "battle", "未保存的战事", "failure.event.60", CancellationToken.None).GetAwaiter().GetResult();
    Require(failed.Status == WorldEventAppendResult.PersistenceRetryable && WorldEventLedger.Count == 0, "failed event entered the authoritative ledger");
    store.FailEventWrites = false;
    WorldEventAppendResult recovered = WorldEventLedger.RecordAsync(60, "battle", "未保存的战事", "failure.event.60", CancellationToken.None).GetAwaiter().GetResult();
    Require(recovered.Status == WorldEventAppendResult.Persisted && WorldEventLedger.Count == 1, "failed event could not be retried with the same event key");
    WorldEventAppendResult conflict = WorldEventLedger.RecordAsync(60, "battle", "内容已改变", "failure.event.60", CancellationToken.None).GetAwaiter().GetResult();
    Require(conflict.Status == WorldEventAppendResult.KeyConflict && WorldEventLedger.Count == 1, "event key conflict was accepted as a duplicate");
    WorldEventLedger.ClearForTesting();
    store = new WorldStateStore { ThrowEventWrites = true };
    AwakeRuntime.WorldStateStore = store;
    WorldEventAppendResult unknown = WorldEventLedger.RecordAsync(61, "battle", "结果未知的战事", "unknown.event.61", CancellationToken.None).GetAwaiter().GetResult();
    Require(unknown.Status == WorldEventAppendResult.PersistenceUnknown && WorldEventLedger.Count == 0, "exceptional event write was treated as persisted");
    WorldEventLedger.ClearForTesting();
    var readStore = new WorldStateStore { FailReads = true };
    AwakeRuntime.WorldStateStore = readStore;
    long ticks = 0;
    WorldEventLedger.UtcTicksProviderForTesting = () => ticks;
    WorldEventLedger.LoadFromStoreAsync(CancellationToken.None).GetAwaiter().GetResult();
    Require(WorldEventLedger.Count == 0, "failed ledger load was treated as a successful empty load");
    readStore.FailReads = false;
    readStore.Document = new JObject
    {
        ["schema"] = "awake.world_events.v1",
        ["records"] = new JArray(Enumerable.Range(1, 60).Reverse().Select(day => new JObject
        {
            ["id"] = "awake:event:stored-" + day,
            ["day"] = day,
            ["kind"] = "battle",
            ["text"] = "重载顺序测试 " + day,
            ["eventKey"] = "stored.event." + day,
            ["domain"] = "war",
            ["occurredAt"] = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(day).ToString("O")
        }).Cast<object>()),
        ["appliedKeys"] = new JArray(),
        ["weeklyReports"] = new JArray()
    };
    ticks = TimeSpan.FromSeconds(11).Ticks;
    WorldEventLedger.LoadFromStoreAsync(CancellationToken.None).GetAwaiter().GetResult();
    Require(WorldEventLedger.Count == 50
        && WorldEventLedger.SnapshotAll().First().Day == 11
        && WorldEventLedger.SnapshotAll().Last().Day == 60,
        "ledger reload did not retain the newest events in chronological order");
    WorldEventLedger.ClearForTesting();
    AwakeRuntime.WorldStateStore = null;
}

static void TestWeeklyReportCatchUp()
{
    WorldEventLedger.ClearForTesting();
    var store = new WorldStateStore();
    AwakeRuntime.WorldStateStore = store;
    WorldKnowledgeSnapshot snapshot = WorldKnowledgeLoader.Load(Path.Combine(FindFixedFixtureRoot(), "manifest.json"));
    var query = new WorldKnowledgeQueryService(snapshot);
    WorldEventServices.BindKnowledge(snapshot, query);
    var soldierAudience = new[] { "soldier" };
    Require(WorldFactCapture.TryCreateWar(15, 2160, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture catchupFact), "catch-up report fact fixture creation failed");
    store.Journal = new WorldFactJournalReadResult(
        WorldFactJournalReadStatus.Success,
        new[] { catchupFact.Fact.ToJson() },
        revision: 1);
    Require(WorldEventLedger.RecordAsync(1, "tax", "第一周粮税变化", "catchup.event.1", soldierAudience, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "first catch-up event was not persisted");
    Require(WorldEventLedger.RecordAsync(8, "battle", "第二周边境交战", "catchup.event.8", soldierAudience, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "second catch-up event was not persisted");
    Require(WorldEventLedger.RecordAsync(15, "feast", "第三周节庆举行", "catchup.event.15", soldierAudience, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "third catch-up event was not persisted");
    FormalWeeklyReportResult first = WorldEventServices.EnsureFormalReportsReadyAsync(21, CancellationToken.None).GetAwaiter().GetResult();
    Require(first.HasFormalReport && (string)first.Report["reportId"] == "awake:report:weekly-v2-21", "latest completed weekly report was not applied");
    List<WeeklyReportApplicationState> states = store.GetWeeklyReportStatesAsync(CancellationToken.None).GetAwaiter().GetResult();
    Require(states.Count == 1 && states.All(value => value.Status == "applied"), "day 21 did not apply only the latest completed weekly report window");
    AwakeRuntime.NativeKnowledgeReady = true;
    WorldEventServices.EnsureKnowledgeReadyAsync(21, CancellationToken.None).GetAwaiter().GetResult();
    WorldKnowledgeQueryResult weekly = query.Query(new WorldbookQuery
    {
        IdentityId = "profile.headman",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "周报",
        MaximumBytes = 4096
    });
    Require(weekly.ReportIds.Count == 1 && weekly.ReportIds.Contains("awake:report:weekly-v2-21"),
        "weekly query did not expose the latest completed report ID; search="
        + string.Join(",", query.Search("周报", 10).Select(value => value.ReportId ?? value.Id))
        + " projectionRevision=" + WorldEventServices.Projection.Revision
        + " state=" + weekly.State
        + " text=" + weekly.RetrievedText);
    int writes = store.ReportWriteAttempts;
    WorldEventServices.EnsureKnowledgeReadyAsync(21, CancellationToken.None).GetAwaiter().GetResult();
    Require(store.ReportWriteAttempts == writes, "re-running the same completed day rewrote applied weekly reports");
    WorldEventLedger.ClearForTesting();
    WorldEventLedger.LoadFromStoreAsync(CancellationToken.None).GetAwaiter().GetResult();
    WorldEventServices.EnsureKnowledgeReadyAsync(21, CancellationToken.None).GetAwaiter().GetResult();
    WorldKnowledgeQueryResult reloaded = query.Query(new WorldbookQuery
    {
        IdentityId = "profile.headman",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "周报",
        MaximumBytes = 4096
    });
    Require(reloaded.ReportIds.Count == 1 && store.ReportWriteAttempts == writes, "reload did not reuse the latest applied report without duplicate writes");
    string originalReport = store.Document["weeklyReports"]?[0]?["report"]?.ToString(Newtonsoft.Json.Formatting.None);
    Require(WorldEventLedger.RecordAsync(2, "battle", "迟到事件不应改写已应用周报", "catchup.late.2", soldierAudience, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "late event was not persisted");
    WorldEventServices.EnsureFormalReportsReadyAsync(21, CancellationToken.None).GetAwaiter().GetResult();
    Require(store.ReportWriteAttempts == writes
        && StringComparer.Ordinal.Equals(store.Document["weeklyReports"]?[0]?["report"]?.ToString(Newtonsoft.Json.Formatting.None), originalReport),
        "late event changed an already applied weekly report snapshot");
    store.Document["weeklyReports"][0]["report"] = new JObject { ["reportId"] = "awake:report:weekly-v2-21" };
    WorldEventServices.EnsureFormalReportsReadyAsync(21, CancellationToken.None).GetAwaiter().GetResult();
    Require(store.ReportWriteAttempts == writes + 1
        && WorldEventContract.TryValidateWeeklyReport((JObject)store.Document["weeklyReports"][0]["report"], out _),
        "invalid weekly report snapshot was not repaired");
    WorldEventLedger.ClearForTesting();
    AwakeRuntime.WorldStateStore = null;
}

static void TestKnowledgeReadinessAndStoreBoundary()
{
    WorldEventLedger.ClearForTesting();
    AwakeRuntime.NativeKnowledgeReady = false;
    var blockedStore = new WorldStateStore();
    AwakeRuntime.WorldStateStore = blockedStore;
    WorldKnowledgeSnapshot snapshot = WorldKnowledgeLoader.Load(Path.Combine(FindFixedFixtureRoot(), "manifest.json"));
    var query = new WorldKnowledgeQueryService(snapshot);
    WorldEventServices.BindKnowledge(snapshot, query);
    var audience = new[] { "commoner" };
    Require(WorldFactCapture.TryCreateWar(7, 1008, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture readinessFact), "readiness fact fixture creation failed");
    JObject readinessFactJson = readinessFact.Fact.ToJson();
    readinessFactJson["legacyVisibilityIdentityIds"] = new JArray("commoner");
    blockedStore.Journal = new WorldFactJournalReadResult(WorldFactJournalReadStatus.Success, new[] { readinessFactJson }, revision: 1);
    Require(WorldEventLedger.RecordAsyncForCampaign(7, "battle", "就绪门控战事", "readiness.gated.7", audience, WorldEventLedger.CampaignGeneration, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "readiness test event was not persisted");
    WorldKnowledgeQueryResult blocked = query.Query(new WorldbookQuery
    {
        IdentityId = "profile.commoner",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "甲国",
        MaximumBytes = 4096
    });
    Require(blocked.State != "known" && string.IsNullOrWhiteSpace(blocked.RetrievedText), "knowledge projected while native readiness was unavailable");
    FormalWeeklyReportResult formalWhileNativeDown = WorldEventServices.EnsureFormalReportsReadyAsync(7, CancellationToken.None).GetAwaiter().GetResult();
    Require(formalWhileNativeDown.HasFormalReport, "formal weekly report did not persist while native readiness was unavailable");
    WorldEventServices.EnsureKnowledgeReadyAsync(7, CancellationToken.None).GetAwaiter().GetResult();
    Require(query.Query(new WorldbookQuery { IdentityId = "profile.commoner", PlayerText = "甲国", MaximumBytes = 4096 }).State != "known", "blocked readiness refresh projected knowledge");
    AwakeRuntime.NativeKnowledgeReady = true;
    WorldEventServices.EnsureKnowledgeReadyAsync(7, CancellationToken.None).GetAwaiter().GetResult();
    WorldKnowledgeQueryResult unblocked = query.Query(new WorldbookQuery
    {
        IdentityId = "profile.commoner",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "甲国",
        MaximumBytes = 4096
    });
    Require(unblocked.State == "known" && unblocked.SourceIds.Contains(readinessFact.Fact.FactId), "knowledge did not project after readiness recovered");

    WorldEventLedger.ClearForTesting();
    var unavailableStore = new WorldStateStore { FailReads = true };
    AwakeRuntime.WorldStateStore = unavailableStore;
    FormalWeeklyReportResult unavailable = WorldEventServices.EnsureFormalReportsReadyAsync(7, CancellationToken.None).GetAwaiter().GetResult();
    Require(StringComparer.Ordinal.Equals(unavailable.Status, FormalWeeklyReportResult.Unavailable)
        && !unavailable.HasFormalReport
        && unavailableStore.ReportWriteAttempts == 0,
        "failed storage read was treated as an empty formal report");

    WorldEventLedger.ClearForTesting();
    AwakeRuntime.NativeKnowledgeReady = true;
    var pending = new TaskCompletionSource<WorldEventAppendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
    var oldStore = new WorldStateStore
    {
        PendingEventWrite = pending,
        EventWriteStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
    };
    AwakeRuntime.WorldStateStore = oldStore;
    Task<WorldEventAppendResult> oldWrite = WorldEventLedger.RecordAsync(8, "battle", "旧存储延迟事件", "store.switch.8", CancellationToken.None);
    oldStore.EventWriteStarted.Task.GetAwaiter().GetResult();
    var newStore = new WorldStateStore();
    AwakeRuntime.WorldStateStore = newStore;
    pending.SetResult(new WorldEventAppendResult
    {
        Status = WorldEventAppendResult.Persisted,
        EventId = "awake:event:store-switch-old",
        EventKey = "store.switch.8"
    });
    WorldEventAppendResult oldResult = oldWrite.GetAwaiter().GetResult();
    Require(oldResult.Code == "awake.world_event.stale_session" && WorldEventLedger.Count == 0, "old storage task changed the current ledger");
    Require(((JArray)newStore.Document["records"]).Count == 0, "old storage task wrote into the replacement store");
    WorldEventAppendResult newResult = WorldEventLedger.RecordAsync(8, "battle", "新存储事件", "store.switch.8", CancellationToken.None).GetAwaiter().GetResult();
    Require(newResult.Status == WorldEventAppendResult.Persisted && ((JArray)newStore.Document["records"]).Count == 1, "replacement storage could not accept the same event key");

    WorldEventLedger.ClearForTesting();
    long loadTicks = TimeSpan.FromSeconds(11).Ticks;
    WorldEventLedger.UtcTicksProviderForTesting = () => loadTicks;
    var pendingRead = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
    var oldReadStore = new WorldStateStore
    {
        PendingWorldEventsRead = pendingRead,
        WorldEventsReadStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
    };
    AwakeRuntime.WorldStateStore = oldReadStore;
    Task oldLoad = WorldEventLedger.LoadFromStoreAsync(CancellationToken.None);
    oldReadStore.WorldEventsReadStarted.Task.GetAwaiter().GetResult();
    var replacementReadStore = new WorldStateStore
    {
        Document = new JObject
        {
            ["schema"] = "awake.world_events.v1",
            ["records"] = new JArray(new JObject
            {
                ["id"] = "awake:event:replacement-read",
                ["day"] = 9,
                ["kind"] = "battle",
                ["text"] = "替换存储读取事件",
                ["eventKey"] = "replacement.read.9",
                ["domain"] = "war",
                ["occurredAt"] = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(9).ToString("O")
            }),
            ["appliedKeys"] = new JArray(),
            ["weeklyReports"] = new JArray()
        }
    };
    AwakeRuntime.WorldStateStore = replacementReadStore;
    pendingRead.SetResult(new JObject
    {
        ["schema"] = "awake.world_events.v1",
        ["records"] = new JArray(new JObject
        {
            ["id"] = "awake:event:old-read",
            ["day"] = 9,
            ["kind"] = "battle",
            ["text"] = "旧存储读取事件",
            ["eventKey"] = "old.read.9",
            ["domain"] = "war"
        }),
        ["appliedKeys"] = new JArray(),
        ["weeklyReports"] = new JArray()
    });
    oldLoad.GetAwaiter().GetResult();
    Require(WorldEventLedger.Count == 0, "old storage load populated the current ledger");
    loadTicks += TimeSpan.FromSeconds(11).Ticks;
    WorldEventLedger.LoadFromStoreAsync(CancellationToken.None).GetAwaiter().GetResult();
    Require(WorldEventLedger.Count == 1 && WorldEventLedger.SnapshotAll()[0].EventKey == "replacement.read.9", "replacement storage load was not accepted after the stale load");
    var replacementReadStore2 = new WorldStateStore
    {
        Document = (JObject)replacementReadStore.Document.DeepClone()
    };
    replacementReadStore2.Document["records"][0]["eventKey"] = "replacement.read.10";
    replacementReadStore2.Document["records"][0]["id"] = "awake:event:replacement-read-10";
    AwakeRuntime.WorldStateStore = replacementReadStore2;
    loadTicks += TimeSpan.FromSeconds(11).Ticks;
    WorldEventLedger.LoadFromStoreAsync(CancellationToken.None).GetAwaiter().GetResult();
    Require(WorldEventLedger.Count == 1 && WorldEventLedger.SnapshotAll()[0].EventKey == "replacement.read.10", "loaded ledger did not refresh after storage instance replacement");

    WorldEventLedger.ClearForTesting();
    AwakeRuntime.WorldStateStore = new WorldStateStore();
    WorldKnowledgeSnapshot evictionSnapshot = WorldKnowledgeLoader.Load(Path.Combine(FindFixedFixtureRoot(), "manifest.json"));
    var evictionQuery = new WorldKnowledgeQueryService(evictionSnapshot);
    WorldEventServices.BindKnowledge(evictionSnapshot, evictionQuery);
    WorldStateStore evictionStore = AwakeRuntime.WorldStateStore;
    var evictionAudience = new[] { "soldier" };
    Require(WorldFactCapture.TryCreateWar(7, 1008, "faction:a", "甲国", "faction:b", "乙国", out WorldFactCapture evictionFact), "eviction report fact fixture creation failed");
    evictionStore.Journal = new WorldFactJournalReadResult(WorldFactJournalReadStatus.Success, new[] { evictionFact.Fact.ToJson() }, revision: 1);
    Require(WorldEventLedger.RecordAsync(1, "battle", "即将被淘汰的战事", "eviction.old.1", evictionAudience, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "eviction source event was not persisted");
    Require(WorldEventServices.EnsureFormalReportsReadyAsync(7, CancellationToken.None).GetAwaiter().GetResult().HasFormalReport, "eviction weekly report was not applied");
    for (int day = 8; day <= 57; day++)
        Require(WorldEventLedger.RecordAsync(day, "battle", "容量挤出的新战事 " + day, "eviction.new." + day, evictionAudience, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "eviction filler event was not persisted");
    Require(WorldEventLedger.SnapshotAll().All(value => value.EventKey != "eviction.old.1"), "evicted source event remained in the in-memory ledger");
    evictionStore.Document["weeklyReports"][0]["report"] = new JObject { ["reportId"] = "awake:report:weekly-v2-7" };
    FormalWeeklyReportResult repairedEviction = WorldEventServices.EnsureFormalReportsReadyAsync(7, CancellationToken.None).GetAwaiter().GetResult();
    JObject repairedEvictionReport = evictionStore.Document["weeklyReports"][0]["report"] as JObject;
    string reportError = string.Empty;
    Require(repairedEviction.HasFormalReport
        && StringComparer.Ordinal.Equals((string)repairedEvictionReport?["reportId"], "awake:report:weekly-v2-7")
        && WorldEventContract.TryValidateWeeklyReport(repairedEvictionReport, out reportError), reportError);
    AwakeRuntime.WorldStateStore = null;
    AwakeRuntime.NativeKnowledgeReady = true;
}

static void TestProductionReadinessGateWiring()
{
    string sourceRoot = FindAwakeSourceRoot();
    string behavior = File.ReadAllText(Path.Combine(sourceRoot, "src", "AwakeEventBehavior.cs"));
    string probe = File.ReadAllText(Path.Combine(sourceRoot, "src", "ProbeExtension.cs"));
    string runtime = File.ReadAllText(Path.Combine(sourceRoot, "src", "AwakeRuntime.cs"));
    string subModule = File.ReadAllText(Path.Combine(sourceRoot, "src", "SubModule.cs"));
    Require(behavior.Contains("EnsureKnowledgeReadyIfNativeReadyAsync", StringComparison.Ordinal)
        && !behavior.Contains("WorldEventServices.EnsureKnowledgeReadyAsync", StringComparison.Ordinal),
        "hourly knowledge refresh bypassed the native readiness gate");
    Require(probe.Contains("EnsureKnowledgeReadyIfNativeReadyAsync", StringComparison.Ordinal)
        && !probe.Contains("WorldEventServices.EnsureKnowledgeReadyAsync", StringComparison.Ordinal),
        "campaign-ready knowledge refresh bypassed the native readiness gate");
    Require(runtime.Contains("readiness.Status != NativeReadinessStatus.Ready", StringComparison.Ordinal)
        && runtime.Contains("readiness.SessionGeneration != generation", StringComparison.Ordinal),
        "native readiness gate does not require Ready status and matching generation");
    Require(subModule.Contains("AwakeRuntime.ResetSessionStateForCampaign()", StringComparison.Ordinal)
        && !subModule.Contains("WorldEventServices.Recorder.ResetForCampaign()", StringComparison.Ordinal),
        "campaign reset bypassed the unified runtime reset boundary");
    string ledger = File.ReadAllText(Path.Combine(sourceRoot, "src", "WorldEventLedger.cs"));
    Require(!ledger.Contains("WorldEventServices.Projection.ReplaceEvents", StringComparison.Ordinal)
        && ledger.Contains("TryProjectEventsIfReady", StringComparison.Ordinal),
        "world event ledger retained a direct dynamic projection path");
    string services = File.ReadAllText(Path.Combine(sourceRoot, "src", "WorldEventContracts.cs"));
    Require(services.Contains("TryProjectSourcesIfReady", StringComparison.Ordinal)
        && services.Contains("AwakeRuntime.IsNativeKnowledgeReady", StringComparison.Ordinal),
        "unified readiness-aware source projection entry is missing");
}

static void TestWorldFactCollectorWiring()
{
    string sourceRoot = FindAwakeSourceRoot();
    string collector = File.ReadAllText(Path.Combine(sourceRoot, "src", "AwakeWorldFactCollectorBehavior.cs"));
    string subModule = File.ReadAllText(Path.Combine(sourceRoot, "src", "SubModule.cs"));
    Require(subModule.Contains("new AwakeWorldFactCollectorBehavior()", StringComparison.Ordinal), "world fact collector is not registered on campaign start");
    Require(collector.Contains("CampaignEvents.WarDeclared.AddNonSerializedListener", StringComparison.Ordinal)
        && collector.Contains("CampaignEvents.MakePeace.AddNonSerializedListener", StringComparison.Ordinal)
        && collector.Contains("CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener", StringComparison.Ordinal)
        && collector.Contains("CampaignEvents.HeroKilledEvent.AddNonSerializedListener", StringComparison.Ordinal)
        && collector.Contains("CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener", StringComparison.Ordinal),
        "world fact collector does not subscribe to the approved five events");
    Require(collector.Contains("awake_world_fact_capture_unavailable", StringComparison.Ordinal)
        && collector.Contains("WorldStateStore store = AwakeRuntime.WorldStateStore", StringComparison.Ordinal)
        && !collector.Contains("EnsureWorldStateReadyAsync", StringComparison.Ordinal)
        && !collector.Contains("PermissionGate", StringComparison.Ordinal)
        && !collector.Contains("EnsureAsync", StringComparison.Ordinal),
        "world fact collector violates the no-store/no-permission boundary");
}

static JObject Manifest(string packageId = "calradia:base", string packageHash = "") => new JObject
{
    ["schemaVersion"] = "awake.worldbook.v2",
    ["packageId"] = packageId,
    ["version"] = "1.0.0",
    ["kind"] = "universe",
    ["worldId"] = "calradia:world:calradia",
    ["entrypoints"] = new JObject { ["runtime"] = "runtime.json", ["index"] = "index.json" },
    ["hashes"] = new JObject { ["packageHash"] = packageHash }
};

static void WriteVerifiedPackage(string root, string packageId, string worldId, out string manifestHash, out string contentHash, out string packageHash)
{
    Directory.CreateDirectory(root);
    string token = packageId.Replace(':', '_');
    string entryId = "awake:entry:" + token;
    var runtime = new JObject
    {
        ["schemaVersion"] = "awake.worldbook.v2",
        ["packageId"] = packageId,
        ["version"] = "1.0.0",
        ["worldId"] = worldId,
        ["revision"] = 1,
        ["entries"] = new JArray(new JObject
        {
            ["id"] = entryId,
            ["domain"] = "culture",
            ["title"] = new JObject { ["zh-CN"] = "测试条目" },
            ["summary"] = new JObject { ["zh-CN"] = "测试摘要" },
            ["keywords"] = new JArray("测试"),
            ["expressions"] = new JArray(new JObject
            {
                ["id"] = "awake:expression:" + token,
                ["detail"] = "summary",
                ["text"] = new JObject { ["zh-CN"] = "测试内容" },
                ["grants"] = new JArray(),
                ["denies"] = new JArray()
            })
        }),
        ["identities"] = new JArray(new JObject { ["id"] = "awake:identity:commoner", ["displayName"] = new JObject { ["zh-CN"] = "平民" }, ["basePriority"] = 0 }),
        ["referrals"] = new JArray(),
        ["indexes"] = new JObject
        {
            ["keywordToEntryIds"] = new JObject { ["测试"] = new JArray(entryId) },
            ["domainToEntryIds"] = new JObject { ["culture"] = new JArray(entryId) }
        }
    };
    var index = new JObject
    {
        ["schemaVersion"] = "awake.worldbook.index.v1",
        ["entryIds"] = new JArray(entryId),
        ["keywordToEntryIds"] = new JObject { ["测试"] = new JArray(entryId) },
        ["domainToEntryIds"] = new JObject { ["culture"] = new JArray(entryId) }
    };
    var manifest = Manifest(packageId);
    manifest["worldId"] = worldId;
    manifest["hashes"] = new JObject();
    manifestHash = WorldbookPackageIntegrity.ComputeManifestHash(manifest);
    contentHash = WorldbookPackageIntegrity.ComputeContentHash("runtime.json", runtime, "index.json", index);
    packageHash = WorldbookPackageIntegrity.ComputePackageHash(manifestHash, contentHash);
    manifest["hashes"] = new JObject { ["manifestHash"] = manifestHash, ["contentHash"] = contentHash, ["packageHash"] = packageHash };
    File.WriteAllText(Path.Combine(root, "manifest.json"), manifest.ToString(Newtonsoft.Json.Formatting.None));
    File.WriteAllText(Path.Combine(root, "runtime.json"), runtime.ToString(Newtonsoft.Json.Formatting.None));
    File.WriteAllText(Path.Combine(root, "index.json"), index.ToString(Newtonsoft.Json.Formatting.None));
}

static JObject Identity(string id, string parent = null) => new JObject
{
    ["id"] = "awake:identity:" + id,
    ["displayName"] = new JObject { ["zh-CN"] = id },
    ["basePriority"] = 0,
    ["parents"] = parent == null ? new JArray() : new JArray("awake:identity:" + parent)
};

static JObject Grant(string identity, string[] referrals = null, JObject conditions = null, string scope = "national", string minDetail = "summary") => new JObject
{
    ["identity_id"] = "awake:identity:" + identity,
    ["scope"] = scope,
    ["min_detail"] = minDetail,
    ["referral_ids"] = referrals == null ? new JArray() : new JArray(referrals),
    ["conditions"] = conditions
};

static JObject Expression(string id, string detail, string text, JObject grant, bool enabled = true, JObject[] denies = null) => new JObject
{
    ["id"] = id,
    ["detail"] = detail,
    ["text"] = new JObject { ["zh-CN"] = text },
    ["enabled"] = enabled,
    ["grants"] = new JArray(grant),
    ["denies"] = denies == null ? new JArray() : new JArray(denies)
};

static JObject Entry(string id, string domain, string title, string[] keywords, params JObject[] expressions) => new JObject
{
    ["id"] = id,
    ["domain"] = domain == "政治" ? "politics" : domain == "经济" ? "economy" : domain == "战争" ? "war" : "culture",
    ["title"] = new JObject { ["zh-CN"] = title },
    ["summary"] = new JObject { ["zh-CN"] = title },
    ["keywords"] = new JArray(keywords),
    ["expressions"] = new JArray(expressions)
};

static void RequireThrows(Action action, string expected, string message)
{
    try { action(); }
    catch (Exception ex) { Require(ex.Message.Contains(expected, StringComparison.Ordinal), message + ": " + ex.Message); return; }
    throw new InvalidOperationException(message);
}

static string FindContractRoot()
{
    DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
    for (int i = 0; i < 10 && current != null; i++, current = current.Parent)
    {
        string candidate = Path.Combine(current.FullName, "tools", "worldbook-contract", "v1");
        if (File.Exists(Path.Combine(candidate, "golden-hashes.json"))) return candidate;
    }
    throw new DirectoryNotFoundException("worldbook contract root not found");
}

static JObject LoadJsonNoDates(string path)
{
    using (JsonTextReader reader = new JsonTextReader(File.OpenText(path)))
    {
        reader.DateParseHandling = DateParseHandling.None;
        return (JObject)JToken.ReadFrom(reader);
    }
}

static string FindWorldReportV2FixtureRoot()
{
    DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
    for (int i = 0; i < 10 && current != null; i++, current = current.Parent)
    {
        string candidate = Path.Combine(current.FullName, "tools", "worldbook-contract", "v2", "fixtures");
        if (File.Exists(Path.Combine(candidate, "valid-report.json"))) return candidate;
    }
    throw new DirectoryNotFoundException("world report v2 fixture root not found");
}

static string FindFixedFixtureRoot()
{
    string candidate = Path.Combine(AppContext.BaseDirectory, "fixtures", "fixed-v2");
    if (File.Exists(Path.Combine(candidate, "manifest.json"))) return candidate;
    throw new DirectoryNotFoundException("fixed worldbook fixture not found: " + candidate);
}

static string FindAwakeSourceRoot()
{
    DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
    for (int i = 0; i < 10 && current != null; i++, current = current.Parent)
    {
        string candidate = Path.Combine(current.FullName, "src", "AwakeRuntime.cs");
        if (File.Exists(candidate)) return current.FullName;
    }
    throw new DirectoryNotFoundException("AWAKE source root not found");
}

sealed class FixedFactContextReader : IWorldFactContextReader
{
    private readonly WorldFactQueryResult _result;

    internal FixedFactContextReader(WorldFactQueryResult result)
    {
        _result = result;
    }

    public Task<WorldFactQueryResult> QueryAsync(WorldFactQueryRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_result);
    }
}
