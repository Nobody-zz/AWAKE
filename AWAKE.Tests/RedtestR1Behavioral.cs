using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json.Linq;

namespace Awake.SdkSmoke;

/// <summary>
/// AWAKE R1 行为负路径红测（离线）。
///
/// 只驱动真实 AWAKE 源码中不依赖 Bannerlord 运行时的路径，配合 fake Host /
/// Storage / Permission。本次执行不启动 Bannerlord、不同步 dist 或游戏目录、
/// 不访问真实 Cloud/本地 Provider、不读取 API Key。
///
/// 无法离线执行的用例必须以 not_attempted / blocked 输出，不得伪造成通过。
/// </summary>
internal static class RedtestR1Behavioral
{
    private const string Target = "AWAKE-REDTEST-PREP-20260908";
    // 单一来源：红测记录的 BuildId 必须跟随源码版本，避免与 AwakeVersion.BuildId 脱节。
    private static readonly string BuildId = AwakeVersion.BuildId;
    private const string CandidateState = "source_only_pending_sync";
    private const string EvidenceRelativePath = @"docs\evidence\AWAKE-REDTEST-R1-BEHAVIORAL-20260908.json";

    private sealed class CaseResult
    {
        internal string Id;
        internal string Status;
        internal string Severity;
        internal string Title;
        internal string Detail;
        internal readonly List<string> Evidence = new List<string>();
    }

    internal static async Task<int> RunAsync()
    {
        List<CaseResult> cases = new List<CaseResult>();

        await Run(cases, "permission.denied_fail_closed", "P1",
            "权限被拒绝必须失败关闭且不授予", PermissionDeniedAsync);
        await Run(cases, "permission.unknown_fail_closed", "P1",
            "未登记权限必须失败关闭", PermissionUnknownAsync);
        await Run(cases, "permission.context_expired", "P1",
            "过期上下文必须失败关闭且不发起请求", PermissionExpiredAsync);
        await Run(cases, "permission.context_missing", "P1",
            "缺失上下文必须失败关闭", PermissionMissingContextAsync);
        await Run(cases, "permission.evaluate_throws_fail_closed", "P1",
            "权限评估抛异常必须失败关闭", PermissionEvaluateThrowsAsync);
        await Run(cases, "permission.cancelled_before_request", "P1",
            "已取消 token 必须在评估/请求前短路", PermissionCancelledAsync);
        await Run(cases, "permission.granted_positive_control", "E2",
            "权限授予路径可观察（正向对照）", PermissionGrantedAsync);
        await Run(cases, "event_effect.range_and_zero_validation", "P1",
            "事件关系效果越界与全零增量必须拒绝", EventEffectRangeAsync);
        await Run(cases, "event_effect.choice_and_target_gate", "P2",
            "事件效果必须匹配已选选项与目标", EventEffectChoiceAsync);
        await Run(cases, "gold.entry_boundary_matrix", "P1",
            "金币入口必须拒绝缺失/非整数/负数/零/超限/溢出金额", GoldBoundaryAsync);
        await Run(cases, "session.stale_generation_isolation", "P0",
            "旧 session 生成号与旧 Store 不得污染新 session", SessionStaleIsolationAsync);
        await Run(cases, "session.end_blocks_new_work", "P0",
            "session 结束后不得再接受新工作", SessionEndBlockAsync);

        await Run(cases, "ai_task.submit_context_missing", "P1",
            "缺失请求上下文必须失败关闭", AiTaskContextMissingAsync);
        await Run(cases, "ai_task.submit_cancelled_before_request", "P1",
            "已取消 token 必须在权限评估/提交前短路", AiTaskCancelledBeforeRequestAsync);
        await Run(cases, "ai_task.submit_route_missing", "P1",
            "缺失路由必须失败关闭且不触达权限评估", AiTaskRouteMissingAsync);
        await Run(cases, "ai_task.submit_route_denied_fail_closed", "P1",
            "路由权限被拒绝必须失败关闭", AiTaskRouteDeniedAsync);
        await Run(cases, "ai_task.submit_cloud_export_unknown", "P1",
            "未知云外发分类必须失败关闭", AiTaskCloudExportUnknownAsync);
        await Run(cases, "ai_task.submit_cloud_export_disabled", "P1",
            "云外发关闭时 player_state 必须失败关闭", AiTaskCloudExportDisabledAsync);
        await Run(cases, "ai_task.submit_player_unbound", "P1",
            "当前玩家无法绑定时必须失败关闭", AiTaskPlayerUnboundAsync);

        AddNotAttempted(cases, "ai_task.provider_error_unknown", "P1",
            "Provider 提交失败 / 不可用错误的统一映射",
            "提交在 EnsureCurrentHeroBoundAsync 之后才调用 host.Ai.SubmitAsync；离线 fake 无法实现 framework internal 的 ICompatibilityGameDataService，"
                + "英雄绑定必然失败，无法到达 Provider 提交点。",
            new[] { "src/AiTaskGateway.cs", "src/AwakeRuntime.cs" });
        AddNotAttempted(cases, "ai_task.duplicate_terminal_event_idempotency", "P1",
            "同一任务重复 terminal event 的幂等隔离",
            "需要真实 IAiTaskHandle 与其事件订阅流（经 Provider 提交产生），离线不可构造。",
            new[] { "src/AiTaskGateway.cs" });
        AddNotAttempted(cases, "ai_task.deadline_cancellation", "P2",
            "请求 deadline 到期的取消（区别于 caller token 取消）",
            "deadline 取消发生在 host.Ai.SubmitAsync 之后的请求生命周期内，离线无法到达该点；caller 取消已由 ai_task.submit_cancelled_before_request 覆盖。",
            new[] { "src/AiTaskGateway.cs" });

        AddStaticGap(cases, "config.json_fallback", "P1",
            "Config.json 兼容回退读取路径在源码中缺失",
            "全源码检索未发现任何 Config.json / config.json 读取或回退实现；MCM 隐式序列化之外无兼容路径，无法运行回退红测。",
            new[] { "src/AwakeConfig.cs" });
        AddStaticGap(cases, "event.contentpolicy_gate", "P1",
            "ContentPolicy / EnableWorldEffects 门未接入事件效果路径",
            "全源码检索未发现 EnableWorldEffects 或 ContentPolicy 标识，事件关系效果结算无内容策略门。",
            new[] { "src/AwakeEventEngine.cs", "src/AwakeEventEffectRules.cs" });

        AddNotAttempted(cases, "lifecycle.duplicate_ongamestart", "P1",
            "同一 CampaignGameStarter 二次 OnGameStart 的 Behavior 重复注册",
            "需要 Bannerlord CampaignGameStarter 与真实 Game 生命周期，离线不可执行。",
            new[] { "src/SubModule.cs" });
        AddNotAttempted(cases, "ui.overlay_ownership_focus", "P1",
            "跨 overlay layer 归属、互斥与焦点恢复",
            "需要 Bannerlord Gauntlet layer/movie，离线不可执行。",
            new[] { "src/NpcDialogueOverlay.cs", "src/AwakeMessengerOverlay.cs" });
        AddNotAttempted(cases, "ui.dialogue_close_cancellation", "P1",
            "对话关闭/换人/重开后的晚到结果隔离",
            "需要 Bannerlord VM/TextObject 运行环境，离线不可执行。",
            new[] { "src/NpcDialogueVM.cs" });
        AddNotAttempted(cases, "ui.tick_drain_budget", "P1",
            "高频 UI 事件 drain 的单帧预算",
            "需要 Bannerlord VM 与 tick 循环，离线不可执行。",
            new[] { "src/NpcDialogueVM.cs", "src/AwakeMessengerVM.cs" });
        AddBlocked(cases, "save.persona_recovery", "P0",
            "Bannerlord 存档 / 读档后的 Persona 恢复",
            "Persona persistence/projection 未接入 SyncData 与运行时存储，E5 保持 blocked_not_wired。",
            new[] { "src/PersonaPersistenceModels.cs", "src/AwakeEventBehavior.cs" });

        try
        {
            AwakeRuntime.ResetSessionStateForTesting();
        }
        catch (Exception ex)
        {
            Console.WriteLine("redtest cleanup warning: " + ex.Message);
        }

        string outputPath = WriteEvidence(cases);
        return Print(cases, outputPath);
    }

    // ------------------------------------------------------------------
    // 用例执行框架
    // ------------------------------------------------------------------

    private static async Task Run(
        List<CaseResult> cases,
        string id,
        string severity,
        string title,
        Func<Task<string>> body)
    {
        CaseResult result = new CaseResult
        {
            Id = id,
            Severity = severity,
            Title = title
        };
        try
        {
            result.Detail = await body().ConfigureAwait(false);
            result.Status = "PASS";
            result.Severity = "E2";
        }
        catch (Exception ex)
        {
            result.Status = "FAIL";
            result.Detail = ex.GetType().Name + ": " + ex.Message;
        }
        result.Evidence.Add("AWAKE.Tests/RedtestR1Behavioral.cs");
        cases.Add(result);
    }

    private static void AddStaticGap(
        List<CaseResult> cases,
        string id,
        string severity,
        string title,
        string detail,
        string[] evidence)
    {
        cases.Add(new CaseResult
        {
            Id = id,
            Status = "RISK",
            Severity = severity,
            Title = title,
            Detail = detail
        });
        cases[cases.Count - 1].Evidence.AddRange(evidence);
    }

    private static void AddNotAttempted(
        List<CaseResult> cases,
        string id,
        string severity,
        string title,
        string detail,
        string[] evidence)
    {
        AddStaticGap(cases, id, severity, title, detail, evidence);
        cases[cases.Count - 1].Status = "NOT_ATTEMPTED";
    }

    private static void AddBlocked(
        List<CaseResult> cases,
        string id,
        string severity,
        string title,
        string detail,
        string[] evidence)
    {
        AddStaticGap(cases, id, severity, title, detail, evidence);
        cases[cases.Count - 1].Status = "BLOCKED";
    }

    // ------------------------------------------------------------------
    // 权限门控负路径
    // ------------------------------------------------------------------

    private static async Task<string> PermissionDeniedAsync()
    {
        RedtestPermissionService permissions = new RedtestPermissionService(PermissionDecision.Denied);
        PermissionGate gate = new PermissionGate(new RedtestHost(permissions));
        RequestContext context = new FakeClock().Context("redtest");

        PermissionGateResult result = await gate
            .EnsureAsync(CatalogDefinition(), context, CancellationToken.None, "redtest")
            .ConfigureAwait(false);

        Assert(!result.Granted, "权限拒绝后不得授予");
        Assert(result.Error != null, "权限拒绝必须携带 FrameworkError");
        Assert(result.Error.Category == FrameworkErrorCategory.Denied,
            "拒绝分类应为 Denied，实际=" + result.Error.Category);
        return "granted=false; error=" + result.Error.Code + "; category=" + result.Error.Category;
    }

    private static async Task<string> PermissionUnknownAsync()
    {
        RedtestPermissionService permissions = new RedtestPermissionService(PermissionDecision.Granted);
        PermissionGate gate = new PermissionGate(new RedtestHost(permissions));
        RequestContext context = new FakeClock().Context("redtest");
        PermissionDefinition unknown = new PermissionDefinition(
            "ai.route:redtest.not.in.catalog",
            PermissionCategory.Route,
            PermissionEnforcement.Hard,
            "redtest unknown",
            "redtest unknown");

        PermissionGateResult result = await gate
            .EnsureAsync(unknown, context, CancellationToken.None)
            .ConfigureAwait(false);

        Assert(!result.Granted, "未登记权限不得授予");
        Assert(result.Error != null && result.Error.Code == "awake.permission_unknown",
            "未登记权限应返回 awake.permission_unknown，实际=" + (result.Error?.Code ?? "null"));
        Assert(permissions.RequestCount == 0, "未登记权限不得发起框架请求，实际=" + permissions.RequestCount);
        return "granted=false; error=" + result.Error.Code + "; requestCount=0";
    }

    private static async Task<string> PermissionExpiredAsync()
    {
        RedtestPermissionService permissions = new RedtestPermissionService(PermissionDecision.Granted);
        PermissionGate gate = new PermissionGate(new RedtestHost(permissions));
        RequestContext expired = new FakeClock().Context(
            "redtest", null, "redtest-expired", TimeSpan.FromSeconds(-5));

        Assert(expired.IsExpired, "测试前置失败：构造的上下文应为已过期");

        PermissionGateResult result = await gate
            .EnsureAsync(CatalogDefinition(), expired, CancellationToken.None)
            .ConfigureAwait(false);

        Assert(!result.Granted, "过期上下文不得授予");
        Assert(result.Error != null
            && result.Error.Code == "awake.context_expired"
            && result.Error.Category == FrameworkErrorCategory.Expired,
            "过期上下文应返回 awake.context_expired/Expired，实际="
                + (result.Error?.Code ?? "null") + "/" + (result.Error?.Category.ToString() ?? "null"));
        Assert(permissions.EvaluateCount == 0 && permissions.RequestCount == 0,
            "过期上下文必须在评估/请求前短路");
        return "granted=false; error=awake.context_expired; evaluateCount=0; requestCount=0";
    }

    private static async Task<string> PermissionMissingContextAsync()
    {
        RedtestPermissionService permissions = new RedtestPermissionService(PermissionDecision.Granted);
        PermissionGate gate = new PermissionGate(new RedtestHost(permissions));

        PermissionGateResult result = await gate
            .EnsureAsync(CatalogDefinition(), null, CancellationToken.None)
            .ConfigureAwait(false);

        Assert(!result.Granted, "缺失上下文不得授予");
        Assert(result.Error != null && result.Error.Code == "awake.context_missing",
            "缺失上下文应返回 awake.context_missing，实际=" + (result.Error?.Code ?? "null"));
        Assert(permissions.EvaluateCount == 0 && permissions.RequestCount == 0,
            "缺失上下文必须在评估/请求前短路");
        return "granted=false; error=awake.context_missing; evaluateCount=0; requestCount=0";
    }

    private static async Task<string> PermissionEvaluateThrowsAsync()
    {
        RedtestPermissionService permissions = new RedtestPermissionService(
            PermissionDecision.Granted, throwOnEvaluate: true);
        PermissionGate gate = new PermissionGate(new RedtestHost(permissions));
        RequestContext context = new FakeClock().Context("redtest");

        PermissionGateResult result = await gate
            .EnsureAsync(CatalogDefinition(), context, CancellationToken.None)
            .ConfigureAwait(false);

        Assert(!result.Granted, "评估异常不得授予");
        Assert(result.Error != null
            && result.Error.Code == "awake.permission_evaluate_error"
            && result.Error.Category == FrameworkErrorCategory.InternalFailure,
            "评估异常应返回 awake.permission_evaluate_error/InternalFailure，实际="
                + (result.Error?.Code ?? "null") + "/" + (result.Error?.Category.ToString() ?? "null"));
        Assert(permissions.RequestCount == 0, "评估异常不得继续发起请求，实际=" + permissions.RequestCount);
        return "granted=false; error=awake.permission_evaluate_error; requestCount=0";
    }

    private static async Task<string> PermissionCancelledAsync()
    {
        RedtestPermissionService permissions = new RedtestPermissionService(PermissionDecision.Denied);
        PermissionGate gate = new PermissionGate(new RedtestHost(permissions));
        RequestContext context = new FakeClock().Context("redtest");
        CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        PermissionGateResult result = await gate
            .EnsureAsync(CatalogDefinition(), context, source.Token)
            .ConfigureAwait(false);

        Assert(!result.Granted, "已取消 token 不得授予");
        Assert(result.Error != null
            && result.Error.Code == "awake.cancelled"
            && result.Error.Category == FrameworkErrorCategory.Cancelled,
            "取消应返回 awake.cancelled/Cancelled，实际="
                + (result.Error?.Code ?? "null") + "/" + (result.Error?.Category.ToString() ?? "null"));
        Assert(permissions.EvaluateCount == 0 && permissions.RequestCount == 0,
            "取消必须在评估/请求前短路，实际 evaluate=" + permissions.EvaluateCount
                + " request=" + permissions.RequestCount);
        return "granted=false; error=awake.cancelled; evaluateCount=0; requestCount=0";
    }

    private static Task<string> PermissionGrantedAsync()
    {
        RedtestPermissionService permissions = new RedtestPermissionService(PermissionDecision.Granted);
        PermissionGate gate = new PermissionGate(new RedtestHost(permissions));
        RequestContext context = new FakeClock().Context("redtest");

        PermissionGateResult result = gate.Evaluate(CatalogDefinition(), context);

        Assert(result.Granted, "已授予权限应返回 Granted=true");
        Assert(result.Error == null, "授予时不应携带错误");
        return Task.FromResult("granted=true; error=none");
    }

    // ------------------------------------------------------------------
    // 事件效果负路径
    // ------------------------------------------------------------------

    private static Task<string> EventEffectRangeAsync()
    {
        JObject valid = AwakeEventEffectRules.BuildRelationshipArgs(
            "hero:target", new AwakeEventEffect("a", "hero:target", 5, 0, 0, "redtest"), null);
        Assert(valid != null, "合法关系效果应构建参数");
        Assert((int)valid["trustDelta"] == 5, "合法效果 trustDelta 应为 5");

        Assert(AwakeEventEffectRules.BuildRelationshipArgs(
                null, new AwakeEventEffect("a", "hero:target", 1, 0, 0), null) == null,
            "缺失目标应拒绝");
        Assert(AwakeEventEffectRules.BuildRelationshipArgs(
                "hero:target", new AwakeEventEffect("a", "hero:target", 101, 0, 0), null) == null,
            "trustDelta 越上限应拒绝");
        Assert(AwakeEventEffectRules.BuildRelationshipArgs(
                "hero:target", new AwakeEventEffect("a", "hero:target", -101, 0, 0), null) == null,
            "trustDelta 越下限应拒绝");
        Assert(AwakeEventEffectRules.BuildRelationshipArgs(
                "hero:target", new AwakeEventEffect("a", "hero:target", 0, 101, 0), null) == null,
            "loveDelta 越上限应拒绝");
        Assert(AwakeEventEffectRules.BuildRelationshipArgs(
                "hero:target", new AwakeEventEffect("a", "hero:target", 0, 0, -101), null) == null,
            "hostilityDelta 越下限应拒绝");
        Assert(AwakeEventEffectRules.BuildRelationshipArgs(
                "hero:target", new AwakeEventEffect("a", "hero:target", 0, 0, 0), null) == null,
            "全零增量应拒绝");

        return Task.FromResult("valid accepted; missing-target/±101/zero all rejected");
    }

    private static Task<string> EventEffectChoiceAsync()
    {
        AwakeEventEffect effect = new AwakeEventEffect("a", "hero:target", 5, 0, 0, "redtest");
        Assert(AwakeEventEffectRules.ShouldApply(effect, "a"), "匹配选项应生效");
        Assert(!AwakeEventEffectRules.ShouldApply(effect, "b"), "不匹配选项不得生效");
        Assert(!AwakeEventEffectRules.ShouldApply(
                new AwakeEventEffect("a", string.Empty, 5, 0, 0), "a"),
            "缺失目标不得生效");
        Assert(!AwakeEventEffectRules.ShouldApply(null, "a"), "空效果不得生效");
        return Task.FromResult("choice gate and target gate enforced");
    }

    // ------------------------------------------------------------------
    // 金币入口边界
    // ------------------------------------------------------------------

    private static Task<string> GoldBoundaryAsync()
    {
        string error;
        Assert(AwakeGiveGoldAdapter.Validate(
                new JObject { ["targetHeroId"] = "hero:boundary", ["amount"] = 1 }, out error),
            "合法下界 amount=1 应通过：" + error);
        Assert(AwakeGiveGoldAdapter.Validate(
                new JObject { ["targetHeroId"] = "hero:boundary", ["amount"] = 100000 }, out error),
            "合法上界 amount=100000 应通过：" + error);

        JObject[] invalid =
        {
            new JObject { ["targetHeroId"] = "hero:boundary" },
            new JObject { ["targetHeroId"] = "hero:boundary", ["amount"] = "100" },
            new JObject { ["targetHeroId"] = "hero:boundary", ["amount"] = -1 },
            new JObject { ["targetHeroId"] = "hero:boundary", ["amount"] = 0 },
            new JObject { ["targetHeroId"] = "hero:boundary", ["amount"] = 100001 },
            new JObject { ["targetHeroId"] = "hero:boundary", ["amount"] = (long)int.MaxValue + 1L }
        };
        foreach (JObject args in invalid)
        {
            Assert(!AwakeGiveGoldAdapter.Validate(args, out error),
                "非法金额应被拒绝：" + args.ToString(Newtonsoft.Json.Formatting.None));
        }
        return Task.FromResult("amount boundaries 1/100000 accepted; 6 invalid forms rejected");
    }

    // ------------------------------------------------------------------
    // session 生命周期隔离
    // ------------------------------------------------------------------

    private static async Task<string> SessionStaleIsolationAsync()
    {
        AwakeRuntime.ResetSessionStateForTesting();
        G3S0FakeHost hostA = new G3S0FakeHost(new G3S0FakeStorageService(), new G3S0FakePermissionService(true));
        bool readyA = await AwakeRuntime.EnsureWorldStateReadyAsync(
            hostA, CancellationToken.None, new[] { AiTaskConstants.PersonaStateNamespace }).ConfigureAwait(false);
        WorldStateStore storeA = AwakeRuntime.WorldStateStore;
        Assert(readyA && storeA != null, "旧 session 的 Store 应可建立");
        int generationA = AwakeRuntime.SessionGeneration;
        Assert(AwakeRuntime.IsCurrentSession(generationA, storeA), "新建 Store 应属于当前 session");

        AwakeRuntime.ResetSessionStateForCampaign();
        int generationB = AwakeRuntime.SessionGeneration;

        Assert(generationA != generationB, "重置后 session 生成号必须变化");
        Assert(!AwakeRuntime.IsCurrentSession(generationA, storeA),
            "旧 generation + 旧 Store 必须被判定为非当前 session");
        Assert(!AwakeRuntime.IsCurrentSessionGeneration(generationA),
            "旧 generation 必须被判定为非当前");
        Assert(AwakeRuntime.IsCurrentSessionGeneration(generationB),
            "新 generation 应为当前 session");
        return "generationA=" + generationA + " -> generationB=" + generationB
            + "; stale store rejected; current generation accepted";
    }

    private static async Task<string> SessionEndBlockAsync()
    {
        AwakeRuntime.ResetSessionStateForTesting();
        int generationBeforeEnd = AwakeRuntime.SessionGeneration;
        Assert(!AwakeRuntime.SessionEnded, "重置后 SessionEnded 应为假");

        await AwakeRuntime.BeginSessionEnd().ConfigureAwait(false);

        Assert(AwakeRuntime.SessionEnded, "BeginSessionEnd 后 SessionEnded 应为真");
        Assert(!AwakeRuntime.IsCurrentSessionGeneration(generationBeforeEnd),
            "session 结束后旧 generation 必须被拒绝");
        Assert(!AwakeRuntime.IsCurrentSessionGeneration(AwakeRuntime.SessionGeneration),
            "session 结束后任何 generation 都不得被判定为当前");
        return "sessionEnded=true; all generations rejected after end";
    }

    // ------------------------------------------------------------------
    // 辅助与证据输出
    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // AI 任务网关提交边界（离线，止于英雄绑定之前）
    // ------------------------------------------------------------------

    private static AiTaskGateway NewGateway(IPermissionService permissions)
    {
        return new AiTaskGateway(new RedtestHost(permissions));
    }

    private static async Task<string> AiTaskContextMissingAsync()
    {
        using (AiTaskGateway gateway = NewGateway(new RedtestPermissionService(PermissionDecision.Granted)))
        {
            AiTaskSubmitResult result = await gateway.SubmitAsync(
                AiTaskConstants.RoutePreprocess, "redtest", null, CloudExportPolicy.None, false,
                null, null, CancellationToken.None).ConfigureAwait(false);
            Assert(result != null && !result.Ok, "缺失上下文不得提交");
            Assert(result.ErrorCode == "awake.context_missing",
                "应返回 awake.context_missing，实际=" + result.ErrorCode);
        }
        return "ok=false; code=awake.context_missing";
    }

    private static async Task<string> AiTaskCancelledBeforeRequestAsync()
    {
        RedtestPermissionService permissions = new RedtestPermissionService(PermissionDecision.Granted);
        using (AiTaskGateway gateway = NewGateway(permissions))
        {
            RequestContext context = new FakeClock().Context("redtest");
            CancellationTokenSource source = new CancellationTokenSource();
            source.Cancel();
            AiTaskSubmitResult result = await gateway.SubmitAsync(
                AiTaskConstants.RoutePreprocess, "redtest", null, CloudExportPolicy.None, false,
                null, context, source.Token).ConfigureAwait(false);
            Assert(result != null && !result.Ok, "已取消 token 不得提交");
            Assert(result.ErrorCode == "awake.cancelled",
                "应返回 awake.cancelled，实际=" + result.ErrorCode);
            Assert(result.Error != null && result.Error.Category == FrameworkErrorCategory.Cancelled,
                "取消分类应为 Cancelled，实际=" + (result.Error?.Category.ToString() ?? "null"));
            Assert(permissions.EvaluateCount == 0 && permissions.RequestCount == 0,
                "取消必须在权限评估/请求前短路，实际 evaluate=" + permissions.EvaluateCount
                    + " request=" + permissions.RequestCount);
        }
        return "ok=false; code=awake.cancelled; evaluateCount=0; requestCount=0";
    }

    private static async Task<string> AiTaskRouteMissingAsync()
    {
        RedtestPermissionService permissions = new RedtestPermissionService(PermissionDecision.Granted);
        using (AiTaskGateway gateway = NewGateway(permissions))
        {
            RequestContext context = new FakeClock().Context("redtest");
            AiTaskSubmitResult result = await gateway.SubmitAsync(
                "   ", "redtest", null, CloudExportPolicy.None, false,
                null, context, CancellationToken.None).ConfigureAwait(false);
            Assert(result != null && !result.Ok, "缺失路由不得提交");
            Assert(result.ErrorCode == "awake.route_missing",
                "应返回 awake.route_missing，实际=" + result.ErrorCode);
            Assert(permissions.EvaluateCount == 0 && permissions.RequestCount == 0,
                "路由缺失必须在权限评估前短路，实际 evaluate=" + permissions.EvaluateCount
                    + " request=" + permissions.RequestCount);
        }
        return "ok=false; code=awake.route_missing; permission untouched";
    }

    private static async Task<string> AiTaskRouteDeniedAsync()
    {
        using (AiTaskGateway gateway = NewGateway(new RedtestPermissionService(PermissionDecision.Denied)))
        {
            RequestContext context = new FakeClock().Context("redtest");
            AiTaskSubmitResult result = await gateway.SubmitAsync(
                AiTaskConstants.RoutePreprocess, "redtest", null, CloudExportPolicy.None, false,
                null, context, CancellationToken.None).ConfigureAwait(false);
            Assert(result != null && !result.Ok, "路由权限被拒不得提交");
            Assert(result.Error != null && result.Error.Category == FrameworkErrorCategory.Denied,
                "拒绝分类应为 Denied，实际=" + (result.Error?.Category.ToString() ?? "null"));
            Assert((result.ErrorDisplay ?? string.Empty).Contains("ai.route.invoke:"),
                "提示应指向被拒路由权限，实际=" + result.ErrorDisplay);
        }
        return "ok=false; category=Denied; display mentions ai.route.invoke";
    }

    private static async Task<string> AiTaskCloudExportUnknownAsync()
    {
        using (AiTaskGateway gateway = NewGateway(new RedtestPermissionService(PermissionDecision.Granted)))
        {
            RequestContext context = new FakeClock().Context("redtest");
            AiTaskSubmitResult result = await gateway.SubmitAsync(
                AiTaskConstants.RoutePreprocess, "redtest", null, "redtest.unknown.classification", false,
                null, context, CancellationToken.None).ConfigureAwait(false);
            Assert(result != null && !result.Ok, "未知云外发分类不得提交");
            Assert(result.ErrorCode == "awake.cloud_export_unknown",
                "应返回 awake.cloud_export_unknown，实际=" + result.ErrorCode);
        }
        return "ok=false; code=awake.cloud_export_unknown";
    }

    private static async Task<string> AiTaskCloudExportDisabledAsync()
    {
        AwakeSettings.SetConfigForTesting(new AwakeConfig { EnableCloudExport = false });
        try
        {
            using (AiTaskGateway gateway = NewGateway(new RedtestPermissionService(PermissionDecision.Granted)))
            {
                RequestContext context = new FakeClock().Context("redtest");
                AiTaskSubmitResult result = await gateway.SubmitAsync(
                    AiTaskConstants.RoutePreprocess, "redtest", null, CloudExportPolicy.PlayerState, false,
                    null, context, CancellationToken.None).ConfigureAwait(false);
                Assert(result != null && !result.Ok, "云外发关闭时不得提交 player_state");
                Assert(result.ErrorCode == "awake.cloud_export_disabled",
                    "应返回 awake.cloud_export_disabled，实际=" + result.ErrorCode);
            }
        }
        finally
        {
            AwakeSettings.ResetConfigForTesting();
        }
        return "ok=false; code=awake.cloud_export_disabled";
    }

    private static async Task<string> AiTaskPlayerUnboundAsync()
    {
        AwakeRuntime.ResetSessionStateForTesting();
        AwakeSettings.SetConfigForTesting(new AwakeConfig());
        try
        {
            using (AiTaskGateway gateway = NewGateway(new RedtestPermissionService(PermissionDecision.Granted)))
            {
                RequestContext context = new FakeClock().Context("redtest");
                AiTaskSubmitResult result = await gateway.SubmitAsync(
                    AiTaskConstants.RoutePreprocess, "redtest", null, CloudExportPolicy.None, false,
                    null, context, CancellationToken.None).ConfigureAwait(false);
                Assert(result != null && !result.Ok, "玩家未绑定时不得提交");
                Assert(result.ErrorCode == "awake.player_unbound",
                    "应返回 awake.player_unbound，实际=" + result.ErrorCode);
                Assert(result.Error != null && result.Error.Retryable, "未绑定应为可重试错误");
            }
        }
        finally
        {
            AwakeSettings.ResetConfigForTesting();
        }
        return "ok=false; code=awake.player_unbound; retryable=true";
    }

    private static PermissionDefinition CatalogDefinition()
    {
        PermissionDefinition definition;
        if (!PermissionCatalog.TryGet(AwakeConstants.PermissionPlayerKnownRead, out definition))
        {
            throw new InvalidOperationException(
                "测试前置失败：权限目录缺少 " + AwakeConstants.PermissionPlayerKnownRead);
        }
        return definition;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string WriteEvidence(List<CaseResult> cases)
    {
        string workspaceRoot = GetWorkspaceRoot();
        string outputPath = Path.Combine(workspaceRoot, EvidenceRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? workspaceRoot);

        JArray caseArray = new JArray();
        foreach (CaseResult item in cases)
        {
            JArray evidence = new JArray();
            foreach (string path in item.Evidence) evidence.Add(path);
            caseArray.Add(new JObject
            {
                ["id"] = item.Id,
                ["status"] = item.Status,
                ["severity"] = item.Severity,
                ["title"] = item.Title,
                ["detail"] = item.Detail,
                ["evidence"] = evidence
            });
        }

        JObject summary = new JObject();
        foreach (string status in new[] { "PASS", "FAIL", "RISK", "BLOCKED", "NOT_ATTEMPTED" })
        {
            int count = 0;
            foreach (CaseResult item in cases) if (item.Status == status) count++;
            summary[status] = count;
        }

        JObject report = new JObject
        {
            ["schema_version"] = "awake.redtest.r1-behavioral.v1",
            ["generated_at_utc"] = DateTime.UtcNow.ToString("o"),
            ["target"] = Target,
            ["build_id"] = BuildId,
            ["candidate_state"] = CandidateState,
            ["mode"] = "offline_behavioral_negative_path",
            ["game_started"] = false,
            ["game_directory_synchronized"] = false,
            ["real_provider_accessed"] = false,
            ["api_key_read"] = false,
            ["summary"] = summary,
            ["cases"] = caseArray,
            ["evidence_boundary"] = "离线行为负路径证据；RISK/BLOCKED/NOT_ATTEMPTED 不计为通过，也不代表 Bannerlord E4/E5。",
            ["next_action"] = "分类 FAIL 为 product/test/environment defect；修复需另立批次并生成新 BuildId；E4 等待同步授权。"
        };

        File.WriteAllText(
            outputPath,
            report.ToString(Newtonsoft.Json.Formatting.Indented),
            new UTF8Encoding(false));
        return outputPath;
    }

    private static string GetWorkspaceRoot()
    {
        DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (current != null)
        {
			string awakeRoot = Path.Combine(current.FullName, "AWAKE");
            if (Directory.Exists(awakeRoot))
            {
                return awakeRoot;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException("Could not locate the AWAKE project root for R1 evidence.");
    }

    private static int Print(List<CaseResult> cases, string outputPath)
    {
        int failures = 0;
        foreach (string status in new[] { "FAIL", "RISK", "BLOCKED", "NOT_ATTEMPTED", "PASS" })
        {
            foreach (CaseResult item in cases)
            {
                if (item.Status != status) continue;
                Console.WriteLine(item.Status.PadRight(14) + item.Id + "  [" + item.Severity + "]  " + item.Title);
                if (item.Status != "PASS")
                {
                    Console.WriteLine("               -> " + item.Detail);
                }
                if (item.Status == "FAIL") failures++;
            }
        }
        Console.WriteLine("R1 behavioral evidence: " + outputPath);
        Console.WriteLine(failures == 0
            ? "REDTEST R1 BEHAVIORAL COMPLETE (no FAIL)"
            : "REDTEST R1 BEHAVIORAL COMPLETE WITH " + failures + " FAIL");
        return failures == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------
    // 测试专用 fake：与 G3-S0 fake 不同，这里需要可控的拒绝/抛异常/计数
    // ------------------------------------------------------------------

    private sealed class RedtestHost : IMarcusAiFrameworkHost
    {
        private readonly IPermissionService _permissions;
        private readonly IStorageService _storage;

        internal RedtestHost(IPermissionService permissions, IStorageService storage = null)
        {
            _permissions = permissions;
            _storage = storage;
            CurrentSession = new SessionRef("redtest-campaign", "redtest-timeline", "redtest-session");
        }

        public FrameworkIdentity Identity => null;
        public SessionRef CurrentSession { get; }
        public ICapabilityBroker Capabilities => null;
        public IToolCandidateService Tools => null;
        public IGameDataService GameData => null;
        public IContextService Context => null;
        public IRagService Rag => null;
        public IEventService Events => null;
        public ICommandService Commands => null;
        public IAiGateway Ai => null;
        public IAiModelService Models => null;
        public IMediaService Media => null;
        public IPromptRegistry Prompts => null;
        public IStorageService Storage => _storage;
        public IAssetService Assets => null;
        public IPermissionService Permissions => _permissions;
        public IDiagnosticsService Diagnostics => null;
        public ILoggingService Log => null;
    }

    private sealed class RedtestPermissionService : IPermissionService
    {
        private readonly PermissionDecision _decision;
        private readonly bool _throwOnEvaluate;

        internal int EvaluateCount;
        internal int RequestCount;

        internal RedtestPermissionService(PermissionDecision decision, bool throwOnEvaluate = false)
        {
            _decision = decision;
            _throwOnEvaluate = throwOnEvaluate;
        }

        public PermissionEvaluation Evaluate(string permissionId, RequestContext context)
        {
            EvaluateCount++;
            if (_throwOnEvaluate)
            {
                throw new InvalidOperationException("redtest permission evaluate failure");
            }
            return new PermissionEvaluation(
                permissionId,
                new ExtensionId(AwakeConstants.OwnerValue),
                _decision,
                "redtest-fake",
                null);
        }

        public Task<OperationResult<PermissionEvaluation>> RequestAsync(
            string permissionId,
            string purpose,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (_decision == PermissionDecision.Granted)
            {
                return Task.FromResult(OperationResult<PermissionEvaluation>.Succeeded(Evaluate(permissionId, context)));
            }
            return Task.FromResult(OperationResult<PermissionEvaluation>.Failed(FrameworkErrors.Create(
                "permission.request_denied",
                FrameworkErrorCategory.Denied,
                "redtest permission denied",
                context?.CorrelationId,
                owner: AwakeConstants.OwnerValue)));
        }

        public OperationResult<bool> Revoke(string permissionId, ExtensionId extensionId)
        {
            return OperationResult<bool>.Succeeded(true);
        }
    }
}
