using System;
using MarcusAwakeFramework.Api;

namespace Awake;

/// <summary>
/// 模拟器里的 `AwakeRuntime` 另一片（`SmokeStubs.cs` 里那片是给知识核心用的）。
/// `AwakeSemanticArmBootstrap` 会用到三个成员，这里把它们补上。
///
/// 与 `tools/semantic-arm-bootstrap-smoke/SemanticArmStubs.cs` **同源** ——
/// `CreateContext` 逐句一致（能拿到 `Sessions.Current` 的 lease 就用 lease，
/// 拿不到才退回 `host.CurrentSession` 的兼容重载）。这条不能简写：
/// RAG 的行是按 `campaign_guid/timeline_id` 分的，用哪种重载决定验出来的会话形状对不对。
///
/// 唯一一处"片"仍是 `ResolveHost`：真件走 `FrameworkHostLocator` 并校验宿主身份
/// （必须是内嵌的那份 AWAKE），模拟器里没有游戏宿主，改由 `BootHostForSim` 直接摆一个。
/// **差别只在"谁来定位"**，定位之后的形状一致。
/// </summary>
internal static partial class AwakeRuntime
{
    /// <summary>模拟器摆的宿主（`IMarcusAiFrameworkHost`，内嵌宿主对外那一面）。</summary>
    internal static IMarcusAiFrameworkHost StubHost;

    /// <summary>模拟器摆的完整宿主，用来取 `Sessions.Current`。</summary>
    internal static IMarcusAwakeFrameworkHost StubFullHost;

    /// <summary>被问到过几次 host —— 用来观察"世界书先到、host 后到"那段等待。</summary>
    internal static int ResolveCallCount;

    internal static void ResetSimHost()
    {
        StubHost = null;
        StubFullHost = null;
        ResolveCallCount = 0;
    }

    internal static IMarcusAiFrameworkHost ResolveHost()
    {
        ResolveCallCount++;
        return StubHost;
    }

    internal static string HostResolutionStatus
    {
        get { return StubHost == null ? "unresolved" : "embedded_host_ready"; }
    }

    internal static RequestContext CreateContext(IMarcusAiFrameworkHost host, string correlationId)
    {
        IMarcusAwakeFrameworkHost fullHost = StubFullHost;
        if (fullHost != null
            && (host == null || Object.ReferenceEquals(fullHost, host))
            && fullHost.Sessions != null)
        {
            SessionLease lease = fullHost.Sessions.Current;
            if (lease != null)
            {
                return new RequestContext(
                    new ExtensionId("AWAKE"),
                    lease,
                    correlationId ?? Guid.NewGuid().ToString("N"),
                    DateTimeOffset.UtcNow + TimeSpan.FromSeconds(90));
            }
        }

        SessionRef session = host?.CurrentSession ?? new SessionRef(string.Empty, string.Empty, string.Empty);
        return new RequestContext(
            new ExtensionId("AWAKE"),
            session,
            correlationId ?? Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow + TimeSpan.FromSeconds(90));
    }

    /// <summary>
    /// 用**框架自己的**宿主与会话协调器摆一个真宿主，供语义臂等 host。
    /// 游戏的次序是"世界书先加载、战役会话后建立"，所以模拟器里也分两步：
    /// 先 <c>Schedule</c>（此时没有 host，应当挂不上），再 <c>BootHostForSim</c> + <c>RetryCurrent</c>。
    /// </summary>
    internal static IMarcusAiFrameworkHost BootHostForSim(IRagService rag, string campaign, string timeline, string session)
    {
        var overrides = new FrameworkServiceOverrides { Rag = rag };
        FrameworkHost host = FrameworkHost.CreateDefaultHost(null, overrides, "1.3.15");
        OperationResult<SessionLease> lease = host.Sessions.BeginSession(new SessionRef(campaign, timeline, session));
        if (!lease.IsSuccess) throw new InvalidOperationException("session_begin_failed:" + (lease.Error?.Code));
        StubFullHost = host;
        StubHost = host;
        return host;
    }
}
