using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Awake;
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
    TestWeeklyReport();
    TestWorldEventLedger();
    TestEventContractParity();
    TestDynamicKnowledgeProjection();
    TestEventPersistenceFailures();
    TestCampaignIsolation();
    TestWeeklyReportCatchUp();
    TestKnowledgeReadinessAndStoreBoundary();
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
    Require(WeeklyReportService.BuildText(records, 7).Contains("战争与军务", StringComparison.Ordinal), "weekly report text rendering failed");
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
    Require(WorldEventLedger.RecordAsync(1, "tax", "第一周粮税变化", "catchup.event.1", soldierAudience, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "first catch-up event was not persisted");
    Require(WorldEventLedger.RecordAsync(8, "battle", "第二周边境交战", "catchup.event.8", soldierAudience, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "second catch-up event was not persisted");
    Require(WorldEventLedger.RecordAsync(15, "feast", "第三周节庆举行", "catchup.event.15", soldierAudience, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "third catch-up event was not persisted");
    WorldEventServices.EnsureKnowledgeReadyAsync(21, CancellationToken.None).GetAwaiter().GetResult();
    List<WeeklyReportApplicationState> states = store.GetWeeklyReportStatesAsync(CancellationToken.None).GetAwaiter().GetResult();
    Require(states.Count == 3 && states.All(value => value.Status == "applied"), "day 21 did not apply all completed weekly report windows");
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
    Require(weekly.ReportIds.Count == 3 && weekly.ReportIds.Contains("awake:report:weekly-7") && weekly.ReportIds.Contains("awake:report:weekly-14") && weekly.ReportIds.Contains("awake:report:weekly-21"), "weekly query did not expose all catch-up report IDs");
    int writes = store.ReportWriteAttempts;
    WorldEventServices.EnsureKnowledgeReadyAsync(21, CancellationToken.None).GetAwaiter().GetResult();
    Require(store.ReportWriteAttempts == writes, "re-running the same completed day rewrote applied weekly reports");
    WorldEventLedger.ClearForTesting();
    WorldEventLedger.LoadFromStoreAsync(CancellationToken.None).GetAwaiter().GetResult();
    WorldEventServices.EnsureKnowledgeReadyAsync(21, CancellationToken.None).GetAwaiter().GetResult();
    WorldKnowledgeQueryResult reloaded = query.Query(new WorldbookQuery
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
    Require(reloaded.ReportIds.Count == 3 && store.ReportWriteAttempts == writes, "reload did not rebuild applied reports without duplicate writes");
    string originalReport = store.Document["weeklyReports"]?[0]?["report"]?.ToString(Newtonsoft.Json.Formatting.None);
    Require(WorldEventLedger.RecordAsync(2, "battle", "迟到事件不应改写已应用周报", "catchup.late.2", soldierAudience, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "late event was not persisted");
    WorldEventServices.EnsureKnowledgeReadyAsync(21, CancellationToken.None).GetAwaiter().GetResult();
    Require(store.ReportWriteAttempts == writes
        && StringComparer.Ordinal.Equals(store.Document["weeklyReports"]?[0]?["report"]?.ToString(Newtonsoft.Json.Formatting.None), originalReport),
        "late event changed an already applied weekly report snapshot");
    store.Document["weeklyReports"][0]["report"] = new JObject { ["reportId"] = "awake:report:weekly-7" };
    WorldEventServices.EnsureKnowledgeReadyAsync(21, CancellationToken.None).GetAwaiter().GetResult();
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
    var audience = new[] { "soldier" };
    Require(WorldEventLedger.RecordAsyncForCampaign(7, "battle", "就绪门控战事", "readiness.gated.7", audience, WorldEventLedger.CampaignGeneration, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "readiness test event was not persisted");
    WorldKnowledgeQueryResult blocked = query.Query(new WorldbookQuery
    {
        IdentityId = "profile.soldier",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "就绪门控战事",
        MaximumBytes = 4096
    });
    Require(blocked.State != "known" && string.IsNullOrWhiteSpace(blocked.RetrievedText), "knowledge projected while native readiness was unavailable");
    WorldEventServices.EnsureKnowledgeReadyAsync(7, CancellationToken.None).GetAwaiter().GetResult();
    Require(query.Query(new WorldbookQuery { IdentityId = "profile.soldier", PlayerText = "就绪门控战事", MaximumBytes = 4096 }).State != "known", "blocked readiness refresh projected knowledge");
    AwakeRuntime.NativeKnowledgeReady = true;
    WorldEventServices.EnsureKnowledgeReadyAsync(7, CancellationToken.None).GetAwaiter().GetResult();
    WorldKnowledgeQueryResult unblocked = query.Query(new WorldbookQuery
    {
        IdentityId = "profile.soldier",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "就绪门控战事",
        MaximumBytes = 4096
    });
    WorldEventRecord persistedRecord = WorldEventLedger.SnapshotAll().Single();
    Require(unblocked.State == "known" && unblocked.SourceIds.Contains(persistedRecord.EventId), "knowledge did not project after readiness recovered");

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
    Require(WorldEventLedger.RecordAsync(1, "battle", "即将被淘汰的战事", "eviction.old.1", evictionAudience, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "eviction source event was not persisted");
    WorldEventServices.EnsureKnowledgeReadyAsync(7, CancellationToken.None).GetAwaiter().GetResult();
    for (int day = 8; day <= 57; day++)
        Require(WorldEventLedger.RecordAsync(day, "battle", "容量挤出的新战事 " + day, "eviction.new." + day, evictionAudience, CancellationToken.None).GetAwaiter().GetResult().Succeeded, "eviction filler event was not persisted");
    Require(WorldEventLedger.SnapshotAll().All(value => value.EventKey != "eviction.old.1"), "evicted source event remained in the in-memory ledger");
    evictionStore.Document["weeklyReports"][0]["report"] = new JObject { ["reportId"] = "awake:report:weekly-7" };
    WorldEventServices.EnsureKnowledgeReadyAsync(7, CancellationToken.None).GetAwaiter().GetResult();
    Require(StringComparer.Ordinal.Equals((string)evictionStore.Document["weeklyReports"][0]["report"]["reportId"], "awake:report:weekly-7")
        && evictionStore.Document["weeklyReports"][0]["report"]["sections"] == null,
        "unrecoverable applied weekly snapshot was overwritten by an empty report");
    JObject emptyReport = WeeklyReportService.BuildWindow(Array.Empty<WorldEventRecord>(), 1, 7);
    Require(WorldEventContract.TryValidateWeeklyReport(emptyReport, out string reportError)
        && StringComparer.Ordinal.Equals((string)emptyReport["reportId"], "awake:report:weekly-7"), reportError);
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
