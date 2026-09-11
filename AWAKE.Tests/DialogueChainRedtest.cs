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
        string file = Path.Combine(
            AwakeModulePaths.ResolveModuleDirectory(),
            "PlayerExports",
            "AwakeState",
            "unbound",
            ProbeNamespace + ".json");

        try
        {
            OperationResult<IKeyValueStore> opened = storage
                .OpenCampaignNamespaceAsync(ProbeNamespace, null, CancellationToken.None)
                .GetAwaiter().GetResult();
            Require(opened.IsSuccess && opened.Value != null,
                "campaign namespace must open, code=" + (opened.Error?.Code ?? "none"));
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
            Cleanup(file);
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
        Require(StringComparer.Ordinal.Equals((string)deployedParsed["schemaVersion"], "awake.worldbook.v2"),
            "deployed manifest schemaVersion must be awake.worldbook.v2, got=" + (string)deployedParsed["schemaVersion"]);
        WorldKnowledgeSnapshot deployedSnapshot =
            WorldKnowledgeLoader.LoadVerified(WorldbookPackageIntegrity.ReadAndVerify(deployed));
        Require(deployedSnapshot.Entries.Count > 0, "deployed package must expose at least one entry");
        Console.WriteLine("PASS dialogue chain worldbook deployed redtest entries=" + deployedSnapshot.Entries.Count);
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
