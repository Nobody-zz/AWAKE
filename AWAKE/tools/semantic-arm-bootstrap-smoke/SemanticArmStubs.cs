using System;
using MarcusAwakeFramework.Api;

namespace Awake;

/// <summary>
/// 验台用的 `AwakeRuntime` 另一片（`SmokeStubs.cs` 里那片是给知识核心用的）。
/// `AwakeSemanticArmBootstrap` 会用到三个成员，这里把它们补上。
///
/// **这是本验台唯一一处"片"**，所以必须说清它与真件的差距：
///   · `ResolveHost` —— 真件走 `FrameworkHostLocator` 并校验宿主身份（必须是内嵌的那份 AWAKE）；
///     这里由验台直接摆一个宿主。**差别只在"谁来定位"**，定位之后的形状一致。
///   · `CreateContext` —— 与真件 `AwakeRuntime.CreateContext` **逐句同源**：
///     能拿到 `Sessions.Current` 的 lease 就用 lease（真件也是这条），拿不到才退回
///     `host.CurrentSession` 的兼容重载。这条不能简写：RAG 的行是按
///     `campaign_guid/timeline_id` 分的，用哪种重载决定验出来的会话形状对不对。
///   · `HostResolutionStatus` —— 只进日志，这里给个等价的状态串。
/// </summary>
internal static partial class AwakeRuntime
{
    /// <summary>验台摆的宿主（`IMarcusAiFrameworkHost`，即内嵌宿主对外那一面）。</summary>
    internal static IMarcusAiFrameworkHost StubHost;

    /// <summary>验台摆的完整宿主，用来取 `Sessions.Current`（真件也是从完整宿主上取）。</summary>
    internal static IMarcusAwakeFrameworkHost StubFullHost;

    /// <summary>被问到过几次 host —— 用来证"世界书先到、host 后到"那段等待真的发生过。</summary>
    internal static int ResolveCallCount;

    internal static void ResetStubHost()
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
}
