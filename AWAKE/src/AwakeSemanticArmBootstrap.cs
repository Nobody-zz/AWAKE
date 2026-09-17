using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace Awake;

/// <summary>
/// 把「语义臂」接上生产路径的**那一根线**。
///
/// 为什么需要这么一个东西（09-17 实查的结论）：语义那一整条链早就齐了 ——
/// 服务侧有语义档（`SqliteStorageAndRagBackend`）、游戏侧有可空委托（`WorldKnowledgeQueryService.AttachSemanticIndex`）、
/// 适配器也写好了（`AwakeWorldKnowledgeSemanticIndex`）—— 但 `AttachSemanticIndex` 与 `TryIngest`
/// **全仓只有离线验台在调**，游戏里没有任何地方去挂它 ⇒ 玩家跑起来只有字面那条路，语义那半等于不存在。
/// 这个类就是补上"谁去调它"。
///
/// 时机的三条约束（都不是我挑的，是既有事实决定的）：
///   ① **世界书加载早于战役会话**（`ProbeExtension` 的 `WorldbookRuntime.EnsureCreated()`）。
///      而 RAG 数据面要一个**战役内**的 session lease（向量按 campaign/timeline 分行存）。
///      ⇒ 挂载点必须**等**，不能指望世界书一发布就有 host。本类因此是"先登记、后等 host"，
///      并且在 `CampaignSessionReady` 上再试一次（`RetryCurrent`）。
///   ② **入库要几十秒**（448 条 / 每批 16 条 / 每条都要编码）⇒ **绝不能放在加载或对话热路径上**，
///      一律后台线程（`AwakeBackgroundTask`）。
///   ③ **换战役会话必须重新入库**：向量表是按 `campaign_guid`/`timeline_id` 分行的
///      （见 `SqliteStorageAndRagBackend.RagSemantic`），新档里那些行不存在。
///      ⇒ 幂等键是「**语料指纹 ＋ 会话**」，不是"挂过一次就永远算数"。
///
/// 失败一律**静默回落**到字面通道：没有 host / 没有 RAG / 没模型 / 入库超时 / 编不出向量 ——
/// 任何一种都只是"这一路没说话"，不抛、不重试到死、不留半截状态。这一条是本项目写下的口径：
/// **语义是加分项，不是替代项**（`LIGHTWEIGHT-EMBEDDING-CANDIDATES-20260916.md` §六.1）。
/// </summary>
internal static class AwakeSemanticArmBootstrap
{
    /// <summary>日志前缀。真机上就是靠 `grep awake_semantic_arm` 判它到底接上没有。</summary>
    internal const string LogTag = "awake_semantic_arm";

    /// <summary>等 host 的次数 × 间隔。20 × 3 s = 60 s —— 只覆盖"世界书先加载、战役随后开"的常规间隔；
    /// 超过就走 `CampaignSessionReady` 上的重试，不在后台死等。</summary>
    internal const int HostWaitAttempts = 20;
    internal const int HostWaitIntervalMilliseconds = 3000;

    /// <summary>离线验台用：把等待窗口压小（默认 0 / -1 表示"用上面的常量"）。
    /// 与生产行为**没有语义差异** —— 都只是"读一次，读不到就睡一会儿再读"。</summary>
    internal static int HostWaitAttemptsOverride;
    internal static int HostWaitIntervalMillisecondsOverride = -1;

    private static int EffectiveAttempts
    {
        get { return HostWaitAttemptsOverride > 0 ? HostWaitAttemptsOverride : HostWaitAttempts; }
    }

    private static int EffectiveInterval
    {
        get { return HostWaitIntervalMillisecondsOverride >= 0 ? HostWaitIntervalMillisecondsOverride : HostWaitIntervalMilliseconds; }
    }

    private static readonly object Gate = new object();
    private static WorldKnowledgeQueryService _service;
    private static WorldKnowledgeSnapshot _snapshot;
    /// <summary>已经接上的「语料指纹|会话」。换任一个都要重来。</summary>
    private static string _attachedKey = string.Empty;
    private static bool _running;

    internal static bool HasAttached
    {
        get { lock (Gate) return _attachedKey.Length > 0; }
    }

    internal static string AttachedKey
    {
        get { lock (Gate) return _attachedKey; }
    }

    /// <summary>
    /// 世界书发布之后调用（`WorldbookRuntime.TryLoadAndPublish` 尾部）。
    /// **只登记 + 起后台线程，不做任何同步等待**。
    /// </summary>
    internal static void Schedule(WorldKnowledgeQueryService service, WorldKnowledgeSnapshot snapshot)
    {
        if (service == null || snapshot == null)
        {
            AwakeLog.Write(LogTag + " status=skip reason=no_service_or_snapshot");
            return;
        }
        lock (Gate)
        {
            _service = service;
            _snapshot = snapshot;
        }
        Kick();
    }

    /// <summary>
    /// 战役会话就绪后再试一次（`ExtensionLifecycleStage.CampaignSessionReady`）。
    /// 世界书是**在战役之前**加载的，那一次往往等不到 host；这里补上第二次机会。
    /// 已经接上且会话没变则什么都不做。
    /// </summary>
    internal static void RetryCurrent()
    {
        bool have;
        lock (Gate) have = _service != null && _snapshot != null;
        if (!have)
        {
            AwakeLog.Write(LogTag + " status=skip reason=no_scheduled_worldbook");
            return;
        }
        AwakeLog.Write(LogTag + " status=retry_requested reason=campaign_session_ready");
        Kick();
    }

    /// <summary>离线验台用：清掉登记与"已接上"的判定，好让同一个进程里反复跑多轮。</summary>
    internal static void ResetForTesting()
    {
        lock (Gate)
        {
            _service = null;
            _snapshot = null;
            _attachedKey = string.Empty;
            _running = false;
        }
    }

    private static void Kick()
    {
        lock (Gate)
        {
            if (_running) return;
            _running = true;
        }
        AwakeBackgroundTask.Run(RunAsync, LogTag);
    }

    private static async Task RunAsync()
    {
        try
        {
            WorldKnowledgeQueryService service;
            WorldKnowledgeSnapshot snapshot;
            lock (Gate)
            {
                service = _service;
                snapshot = _snapshot;
            }
            if (service == null || snapshot == null) return;

            IMarcusAiFrameworkHost host = await WaitForHostAsync().ConfigureAwait(false);
            if (host == null)
            {
                AwakeLog.Write(LogTag + " status=host_unavailable attempts=" + EffectiveAttempts
                    + " host_resolution=" + AwakeRuntime.HostResolutionStatus);
                return;
            }

            IRagService rag;
            try
            {
                rag = host.Rag;
            }
            catch (Exception ex)
            {
                AwakeLog.Write(LogTag + " status=no_rag error=" + ex.Message);
                return;
            }
            if (rag == null)
            {
                AwakeLog.Write(LogTag + " status=no_rag");
                return;
            }

            RequestContext context = AwakeRuntime.CreateContext(host, "awake-semantic-arm");
            if (context == null || context.Session == null || !context.Session.IsCampaign)
            {
                // 非战役（主菜单/编辑器）里没有可用的 session lease ⇒ 入库进去也是错行。
                AwakeLog.Write(LogTag + " status=not_campaign session=" + (context?.Session?.SessionId ?? "none"));
                return;
            }

            string fingerprint = AwakeWorldKnowledgeSemanticIndex.ComposeFingerprint(snapshot.PackageId, snapshot.Version);
            string key = fingerprint + "|" + context.Session.ToString();
            lock (Gate)
            {
                if (StringComparer.Ordinal.Equals(_attachedKey, key))
                {
                    AwakeLog.Write(LogTag + " status=already_attached key=" + key);
                    return;
                }
            }

            var index = new AwakeWorldKnowledgeSemanticIndex(rag, context, fingerprint);
            // 取词条的姿势与离线验台**同源**（`tools/worldbook-rag-merge` 也是 `snapshot.Entries.Values`）
            // —— 平行实现必须同源，否则验台上量的 23/24 对生产不成立。
            var entries = new List<WorldKnowledgeEntry>(snapshot.Entries.Values);
            int ingested;
            string error;
            bool ok = index.TryIngest(entries, out ingested, out error);
            if (!ok)
            {
                // **入库失败就不挂**：挂上一个永远返回空表的索引，只会让每轮对话白多一次 IPC。
                AwakeLog.Write(LogTag + " status=ingest_failed code=" + (error ?? string.Empty)
                    + " entries=" + snapshot.Entries.Count + " fingerprint=" + fingerprint);
                return;
            }

            service.AttachSemanticIndex(index);
            lock (Gate) _attachedKey = key;
            AwakeLog.Write(LogTag + " status=attached entries=" + ingested
                + " of=" + snapshot.Entries.Count
                + " fingerprint=" + fingerprint
                + " session=" + context.Session.SessionId);
        }
        catch (Exception ex)
        {
            // 只有"这一路没说话"，绝不让它把世界书加载或对话拖死。
            AwakeLog.Write(LogTag + " status=error error=" + ex.Message);
        }
        finally
        {
            lock (Gate) _running = false;
        }
    }

    private static async Task<IMarcusAiFrameworkHost> WaitForHostAsync()
    {
        int attempts = EffectiveAttempts;
        int interval = EffectiveInterval;
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
            if (host != null && host.Rag != null) return host;
            if (attempt < attempts && interval > 0)
            {
                await Task.Delay(interval).ConfigureAwait(false);
            }
        }
        return null;
    }
}
