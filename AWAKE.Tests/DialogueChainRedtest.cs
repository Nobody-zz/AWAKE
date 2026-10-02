using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json.Linq;

namespace Awake.SdkSmoke;

/// <summary>
/// 010 对话链路红测（离线）。只驱动真实 AWAKE / 框架实现，不启动 Bannerlord，
/// 不触碰游戏目录、Provider 或 API Key。
/// 覆盖验收 1 的 ①权限 ②玩家绑定（离线可判定部分：无战役必须失败关闭）
/// ③提示词 ④存储 ⑤世界书 v2 解封；⑥默认装配路径在框架侧单测里断言。
/// </summary>
internal static class DialogueChainRedtest
{
    private const string ProbeNamespace = "awake.redtest.namespace";

    // 假存档 id：只用来证明"绑定之后落进哪个目录"，与真实存档无关。
    private const string TestCampaignId = "awake-redtest-campaign";

    internal static void Run()
    {
        RunPermissionChecks();
        RunPlayerBindingChecks();
        RunPromptChecks();
        RunStorageChecks();
        RunWorldbookPackageCheck();
        Console.WriteLine("PASS dialogue chain redtest");
    }

    private static void RunPlayerBindingChecks()
    {
        // 验收 1② 的离线可判定部分：没有战役时 player provider 必须失败关闭——
        // 不抛异常、不返回成功、错误码可区分（game_data.campaign_unavailable / game_data.player_unavailable）。
        // 「绑定成功」的正向断言依赖真实 Campaign + Hero.MainHero，只能由实机验证覆盖（见交付说明未验证项）。
        AwakePlayerSnapshotProvider provider = new AwakePlayerSnapshotProvider();
        OperationResult<PlayerSnapshotDto> result = provider
            .GetCurrentPlayerAsync(null, CancellationToken.None)
            .GetAwaiter().GetResult();
        Require(!result.IsSuccess, "player snapshot must fail closed when there is no active campaign");
        Require(result.Error != null, "player snapshot failure must carry an error");
        Require(StringComparer.Ordinal.Equals(result.Error.Code, "game_data.campaign_unavailable")
                || StringComparer.Ordinal.Equals(result.Error.Code, "game_data.player_unavailable"),
            "unexpected player snapshot error code: " + (result.Error?.Code ?? "none"));
        Console.WriteLine("PASS dialogue chain player binding redtest code=" + result.Error.Code);
    }

    private static void RunPermissionChecks()
    {
        AwakePermissionService service = new AwakePermissionService();
        string routePermission = NpcDialogueConstants.PermissionRouteInvoke;
        string cloudPermission = PermissionCatalog.CloudExportPermissionId(CloudExportPolicy.PlayerState);

        try
        {
            // ①-1 软权限：本机读取 / 提示词 / 落盘必须放行，否则玩家绑定与提示词门永远过不去。
            AwakeSettings.SetConfigForTesting(new AwakeConfig());
            Require(service.Evaluate(AwakeConstants.PermissionPlayerKnownRead, null).Decision == PermissionDecision.Granted,
                "soft permission " + AwakeConstants.PermissionPlayerKnownRead + " must be granted");
            Require(service.Evaluate(AwakeConstants.PermissionPromptRegistryWrite, null).Decision == PermissionDecision.Granted,
                "soft permission " + AwakeConstants.PermissionPromptRegistryWrite + " must be granted");
            Require(service.Evaluate(AwakeConstants.PermissionStorageWrite, null).Decision == PermissionDecision.Granted,
                "soft permission " + AwakeConstants.PermissionStorageWrite + " must be granted");

            // ①-2 硬权限默认拒绝：新授权开关默认关闭。
            Require(service.Evaluate(routePermission, null).Decision == PermissionDecision.Denied,
                "route permission must default to denied");
            Require(service.Evaluate(cloudPermission, null).Decision == PermissionDecision.Denied,
                "cloud export permission must default to denied");

            // ①-3 未知权限必须失败关闭。
            Require(service.Evaluate("awake.redtest.unknown.permission", null).Decision == PermissionDecision.Denied,
                "unknown permission must be denied");

            // ①-4 只开 AI 授权、云外发仍关闭 -> 云外发必须拒绝（D-4 两道开关）。
            AwakeSettings.SetConfigForTesting(new AwakeConfig
            {
                AllowAiRouting = true,
                EnableCloudExport = false,
                AllowCloudExportPlayerState = true
            });
            Require(service.Evaluate(routePermission, null).Decision == PermissionDecision.Granted,
                "route permission must be granted when AI routing is authorized");
            Require(service.Evaluate(cloudPermission, null).Decision == PermissionDecision.Denied,
                "cloud export must stay denied while cloud export is switched off");

            // ①-5 两道开关都开才放行云外发。
            AwakeSettings.SetConfigForTesting(new AwakeConfig
            {
                AllowAiRouting = true,
                EnableCloudExport = true,
                AllowCloudExportPlayerState = true
            });
            Require(service.Evaluate(cloudPermission, null).Decision == PermissionDecision.Granted,
                "cloud export must be granted when both switches are on");
            Require(service.Evaluate("ai.cloud_export:classification.not_registered", null).Decision == PermissionDecision.Denied,
                "unknown cloud export classification must be denied");

            // ①-6 Request 与 Evaluate 必须同结论，不得存在隐藏放行。
            OperationResult<PermissionEvaluation> requested =
                service.RequestAsync(routePermission, "probe", null, CancellationToken.None).GetAwaiter().GetResult();
            Require(requested.IsSuccess && requested.Value != null && requested.Value.Decision == PermissionDecision.Granted,
                "permission request must agree with evaluate");

            AwakeSettings.SetConfigForTesting(new AwakeConfig());
            OperationResult<PermissionEvaluation> deniedRequest =
                service.RequestAsync(routePermission, "probe", null, CancellationToken.None).GetAwaiter().GetResult();
            Require(deniedRequest.IsSuccess && deniedRequest.Value != null && deniedRequest.Value.Decision == PermissionDecision.Denied,
                "permission request must not escalate a denied evaluation");
        }
        finally
        {
            AwakeSettings.ResetConfigForTesting();
        }

        Console.WriteLine("PASS dialogue chain permission redtest");
    }

    private static void RunPromptChecks()
    {
        AwakePromptRegistry registry = new AwakePromptRegistry();
        PromptDefinition definition = NpcPromptTemplate.CreateDefinition();

        OperationResult<bool> registered =
            registry.RegisterAsync(definition, null, CancellationToken.None).GetAwaiter().GetResult();
        Require(registered.IsSuccess, "prompt registration must succeed, code=" + (registered.Error?.Code ?? "none"));

        Dictionary<string, string> variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["npc_identity"] = "村民",
            ["player_turn"] = "今年的收成怎么样？"
        };
        OperationResult<PromptCompilation> compiled = registry.CompileAsync(
            new PromptCompileRequest(definition.PromptId, definition.Version, definition.Revision, variables),
            null,
            CancellationToken.None).GetAwaiter().GetResult();
        Require(compiled.IsSuccess && compiled.Value != null, "prompt compile must succeed, code=" + (compiled.Error?.Code ?? "none"));
        Require(compiled.Value.CompiledText.Contains("村民"), "compiled prompt dropped the variable value");
        Require(!compiled.Value.CompiledText.Contains("{{player_turn}}"), "compiled prompt left the player_turn placeholder unresolved");
        Require(!string.IsNullOrWhiteSpace(compiled.Value.OutputSchemaJson), "compiled prompt lost the output contract schema");

        // 未登记的修订必须失败关闭，不得静默返回空文本。
        OperationResult<PromptCompilation> unknownRevision = registry.CompileAsync(
            new PromptCompileRequest(definition.PromptId, definition.Version, "redtest-unknown", variables),
            null,
            CancellationToken.None).GetAwaiter().GetResult();
        Require(!unknownRevision.IsSuccess, "compiling an unregistered prompt revision must fail");

        Console.WriteLine("PASS dialogue chain prompt redtest");
    }

    private static void RunStorageChecks()
    {
        AwakeFileStorageService storage = new AwakeFileStorageService();
        string stateRoot = Path.Combine(
            AwakeModulePaths.ResolveModuleDirectory(),
            "PlayerExports",
            "AwakeState");
        string unboundFile = Path.Combine(stateRoot, "unbound", ProbeNamespace + ".json");
        string boundFile = Path.Combine(stateRoot, TestCampaignId, ProbeNamespace + ".json");

        try
        {
            // 缺陷④（2026-10-01）：campaign 还没绑定时，存储必须**拒绝写入并报可重试**。
            // 口径是主控直接定的（docs/HANDOFF-STORAGE-CONSISTENCY-20260922.md §1.2 Q11-4）：
            // 「campaign 未绑定时 → 拒绝写入并报可重试（行为变更，现行是照写）」。
            // 现行落进共享的 unbound\ 桶：写入"成功"，等真存档 id 出来以后 ResolveCampaignId()
            // 已经返回真 id，开局前写的那些账本再也读不回来（09-14 真机实证：unbound\ 与
            // 1IgZ8yHJynfn\ 两份并存，前者时间戳就是那一场）。
            AwakeFileStorageService.CampaignIdProviderForTesting = null;
            OperationResult<IKeyValueStore> unbound = storage
                .OpenCampaignNamespaceAsync(ProbeNamespace, null, CancellationToken.None)
                .GetAwaiter().GetResult();
            Require(!unbound.IsSuccess, "campaign namespace must be refused while the campaign is unbound");
            Require(unbound.Error != null, "refusing an unbound campaign namespace must carry an error");
            Require(StringComparer.Ordinal.Equals(unbound.Error.Code, "awake.storage.campaign_unbound"),
                "unbound campaign namespace must report awake.storage.campaign_unbound, got="
                + (unbound.Error == null ? "none" : unbound.Error.Code));
            Require(unbound.Error.Retryable, "unbound campaign namespace must be retryable");
            Require(!File.Exists(unboundFile),
                "an unbound campaign must not write into the shared unbound bucket: " + unboundFile);

            // 绑定之后必须落在**真存档 id** 的目录里。
            AwakeFileStorageService.CampaignIdProviderForTesting = () => TestCampaignId;
            OperationResult<IKeyValueStore> opened = storage
                .OpenCampaignNamespaceAsync(ProbeNamespace, null, CancellationToken.None)
                .GetAwaiter().GetResult();
            Require(opened.IsSuccess && opened.Value != null,
                "campaign namespace must open once the campaign is bound, code="
                + (opened.Error?.Code ?? "none"));
            IKeyValueStore store = opened.Value;

            Require(store.SetAsync("probe.key", "probe-value", null, CancellationToken.None).GetAwaiter().GetResult().IsSuccess,
                "storage set failed");
            OperationResult<string> read = store.GetAsync("probe.key", null, CancellationToken.None).GetAwaiter().GetResult();
            Require(read.IsSuccess && StringComparer.Ordinal.Equals(read.Value, "probe-value"),
                "storage get must return the stored value, got=" + (read.Value ?? "null"));
            Require(store.DeleteAsync("probe.key", null, CancellationToken.None).GetAwaiter().GetResult().IsSuccess,
                "storage delete failed");
            OperationResult<string> afterDelete = store.GetAsync("probe.key", null, CancellationToken.None).GetAwaiter().GetResult();
            Require(afterDelete.IsSuccess && string.IsNullOrEmpty(afterDelete.Value),
                "storage get after delete must be empty");

            Require(File.Exists(boundFile),
                "campaign state must live under the campaign id directory: " + boundFile);
            Require(!File.Exists(unboundFile),
                "a bound campaign must not write into the shared unbound bucket: " + unboundFile);

            // 会话作用域必须显式不可用，不得静默造第二条数据面。
            OperationResult<IKeyValueStore> session = storage
                .OpenSessionNamespaceAsync(ProbeNamespace, null, CancellationToken.None)
                .GetAwaiter().GetResult();
            Require(!session.IsSuccess
                && session.Error != null
                && StringComparer.Ordinal.Equals(session.Error.Code, "awake.storage.session_scope_unavailable"),
                "session scope must be explicitly unavailable");
        }
        finally
        {
            AwakeFileStorageService.CampaignIdProviderForTesting = null;
            Cleanup(boundFile);
            Cleanup(unboundFile);
        }

        Console.WriteLine("PASS dialogue chain storage redtest");
    }

    private static void RunWorldbookPackageCheck()
    {
        string manifest = ResolvePilotManifest();
        Require(manifest != null, "pilot worldbook package manifest was not found under AWAKE/release/awake-worldbook-pilot");

        JObject parsed = JObject.Parse(File.ReadAllText(manifest));
        Require(StringComparer.Ordinal.Equals((string)parsed["schemaVersion"], "awake.worldbook.v2"),
            "pilot manifest schemaVersion must be awake.worldbook.v2, got=" + (string)parsed["schemaVersion"]);

        WorldbookVerifiedPackage verified = WorldbookPackageIntegrity.ReadAndVerify(manifest);
        WorldKnowledgeSnapshot snapshot = WorldKnowledgeLoader.LoadVerified(verified);
        Require(snapshot.Entries.Count > 0, "pilot package must expose at least one entry");
        Require(snapshot.Identities.Count > 0, "pilot package must expose at least one identity");

        Console.WriteLine("PASS dialogue chain worldbook redtest entries=" + snapshot.Entries.Count
            + " identities=" + snapshot.Identities.Count);

        // 验收 1⑤ 的判据是「部署的 manifest」而不是工程里的备用件（D-12 门禁 2 用文件实测而非运行日志）。
        string deployed = ResolveDeployedManifest();
        Require(deployed != null,
            "deployed worldbook manifest not found; expected <game>\\Modules\\AWAKE\\ModuleData\\Worldbook\\manifest.json");
        JObject deployedParsed = JObject.Parse(File.ReadAllText(deployed));
        // 形态以 `docs/worldbook-studio-plan/RUNTIME-MAPPING-CONTRACT.md`〈仓库侧包形态〉为准：
        // 09-14 拍板并落地的是 **registry.v1 存根 ＋ packages/<universe>/ 三件套**，
        // 仓库侧与游戏目录两侧都按此形态投送过（09-14 23:39／09-15 16:57）。
        // 本行原写 `awake.worldbook.v2`（更早的试点形态）—— 判据没跟着决定走，属**判据陈旧**，不是实现回退。
        Require(StringComparer.Ordinal.Equals((string)deployedParsed["schemaVersion"], "awake.worldbook.registry.v1"),
            "deployed manifest schemaVersion must be awake.worldbook.registry.v1, got=" + (string)deployedParsed["schemaVersion"]);
        // `registry.v1` 是**存根**，它指向 `packages/<universe>/` 三件套 ⇒ 校验必须走两步：
        // 「Load 存根 → Select 选包（顺带重算三 hash）→ LoadVerified」。
        // 原实现拿存根直接 `ReadAndVerify`，会报 `WB2-SCHEMA-UNSUPPORTED:manifest`
        // —— 那是把存根当包 manifest 用，属**判据用错入口**，不是包坏了。
        WorldbookPackageRegistry deployedRegistry = WorldbookPackageRegistry.Load(deployed);
        WorldbookActivationState deployedActivation = deployedRegistry.Select();
        Require(deployedRegistry.LastSelectedPackage != null,
            "deployed registry must select a universe package; packageId=" + deployedActivation.PackageId);
        WorldKnowledgeSnapshot deployedSnapshot =
            WorldKnowledgeLoader.LoadVerified(deployedRegistry.LastSelectedPackage);
        Require(deployedSnapshot.Entries.Count > 0, "deployed package must expose at least one entry");
        Console.WriteLine("PASS dialogue chain worldbook deployed redtest entries=" + deployedSnapshot.Entries.Count
            + " package=" + deployedActivation.PackageId);
    }

    private static string ResolvePilotManifest()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && current != null; i++)
        {
            string candidate = Path.Combine(current.FullName, "AWAKE", "release", "awake-worldbook-pilot", "manifest.json");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        return null;
    }

    private static string ResolveDeployedManifest()
    {
        string moduleRoot = Environment.GetEnvironmentVariable("AWAKE_GAME_MODULE");
        if (string.IsNullOrWhiteSpace(moduleRoot))
        {
            moduleRoot = @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE";
        }
        string candidate = Path.Combine(moduleRoot, "ModuleData", "Worldbook", "manifest.json");
        return File.Exists(candidate) ? candidate : null;
    }

    private static void Cleanup(string file)
    {
        try
        {
            if (File.Exists(file)) File.Delete(file);
            // Save() 是「先写 .tmp 再换名」两步；换名失败时 .tmp 会留下，
            // 于是这个判据自己往模块目录里漏垃圾（2026-10-01 实测：残留
            // awake.redtest.namespace.json.tmp 卡住了上一级目录的清理）。清理必须连它一起收。
            string tempFile = file + ".tmp";
            if (File.Exists(tempFile)) File.Delete(tempFile);
            string directory = Path.GetDirectoryName(file);
            for (int i = 0; i < 3 && !string.IsNullOrEmpty(directory); i++)
            {
                if (!Directory.Exists(directory) || Directory.GetFileSystemEntries(directory).Length != 0) break;
                Directory.Delete(directory);
                directory = Path.GetDirectoryName(directory);
            }
        }
        catch
        {
            // 清理失败不影响红测结论。
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("dialogue-chain redtest failed: " + message);
    }
}
