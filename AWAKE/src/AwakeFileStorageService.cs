using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace Awake;

/// <summary>
/// 游戏侧文件型存储，替代框架默认的 UnavailableStorageService。
/// 每个命名空间一个 JSON 文件，落模块目录 PlayerExports\AwakeState\&lt;campaign&gt;\&lt;namespace&gt;.json。
/// 这是本批的唯一权威数据面；Runtime 侧 SQLite/RAG 因客户端能力集不含 storage.*/rag.* 而未启用
/// （见 PLAN-AWAKE-DIALOGUE-CHAIN-010 的 D-9，011 再决策合并或择一）。
/// </summary>
internal sealed class AwakeFileStorageService : IStorageService
{
    private static readonly object Gate = new object();
    private static readonly Dictionary<string, JsonFileKeyValueStore> Stores =
        new Dictionary<string, JsonFileKeyValueStore>(StringComparer.Ordinal);

    public Task<OperationResult<IKeyValueStore>> OpenCampaignNamespaceAsync(
        string namespaceId,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        // 战役标识与主角同属游戏对象，统一回到游戏线程读取。
        return AwakeUiDispatcher.RunOnGameThreadAsync(
            () => Task.FromResult(OpenCampaignNamespace(namespaceId, context)),
            cancellationToken);
    }

    public Task<OperationResult<IKeyValueStore>> OpenSessionNamespaceAsync(
        string namespaceId,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        // AWAKE 目前只使用战役命名空间；会话作用域显式标记未启用，不静默造第二条数据面。
        AwakeLog.Write("storage_session_namespace_unsupported namespace=" + (namespaceId ?? "null"));
        return Task.FromResult(OperationResult<IKeyValueStore>.Failed(FrameworkErrors.Create(
            "awake.storage.session_scope_unavailable",
            FrameworkErrorCategory.Unavailable,
            "AWAKE does not provide session-scoped storage.",
            context?.CorrelationId ?? "storage-session",
            owner: AwakeConstants.OwnerValue)));
    }

    private static OperationResult<IKeyValueStore> OpenCampaignNamespace(string namespaceId, RequestContext context)
    {
        string correlation = context?.CorrelationId ?? "storage-open";
        if (string.IsNullOrWhiteSpace(namespaceId))
        {
            return OperationResult<IKeyValueStore>.Failed(FrameworkErrors.Create(
                "awake.storage.namespace_missing",
                FrameworkErrorCategory.InvalidRequest,
                "A storage namespace id is required.",
                correlation,
                owner: AwakeConstants.OwnerValue));
        }

        // 缺陷④（2026-10-01）：campaign 还没绑定时**拒绝写入并报可重试**。
        // 口径是主控直接定的（docs/HANDOFF-STORAGE-CONSISTENCY-20260922.md §1.2 Q11-4：
        // 「campaign 未绑定时存储怎么办 → 拒绝写入并报可重试（行为变更，现行是照写）」）。
        // 旧行为落共享的 unbound\ 桶：写入"成功"，可开局后 ResolveCampaignId() 返回真 id，
        // 同一份账本再也读不回来（09-14 真机实证：unbound\ 与 1IgZ8yHJynfn\ 两份并存）。
        string campaignId = ResolveCampaignId();
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            AwakeLog.Write("storage_namespace_open_unbound namespace=" + namespaceId);
            return OperationResult<IKeyValueStore>.Failed(FrameworkErrors.Create(
                "awake.storage.campaign_unbound",
                FrameworkErrorCategory.Unavailable,
                "The campaign is not bound yet; campaign storage is refused until it is.",
                correlation,
                retryable: true,
                owner: AwakeConstants.OwnerValue));
        }

        try
        {
            string path = Path.Combine(
                AwakeModulePaths.ResolveModuleDirectory(),
                "PlayerExports",
                "AwakeState",
                SafeSegment(campaignId),
                SafeSegment(namespaceId) + ".json");

            JsonFileKeyValueStore store;
            lock (Gate)
            {
                if (!Stores.TryGetValue(path, out store) || store == null)
                {
                    store = new JsonFileKeyValueStore(namespaceId, path);
                    Stores[path] = store;
                }
            }

            AwakeLog.Write("storage_namespace_opened namespace=" + namespaceId + " scope=campaign path=" + path);
            return OperationResult<IKeyValueStore>.Succeeded(store);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("storage_namespace_open_error namespace=" + namespaceId + " error=" + ex.Message);
            return OperationResult<IKeyValueStore>.Failed(FrameworkErrors.Create(
                "awake.storage.namespace_open_failed",
                FrameworkErrorCategory.InternalFailure,
                "The storage namespace could not be opened.",
                correlation,
                owner: AwakeConstants.OwnerValue));
        }
    }

    // 测试缝：离线没有 Campaign，造不出「已绑定真存档 id」的路径。
    // 生产环境恒为 null ⇒ 走真实的 Campaign.Current.UniqueGameId。
    internal static Func<string> CampaignIdProviderForTesting { get; set; }

    // 未绑定时返回 null（而不是过去那个 "unbound" 桶名）——调用方据此**拒绝**写入，
    // 而不是往一个共享位置里塞数据（2026-10-01 缺陷④）。
    private static string ResolveCampaignId()
    {
        Func<string> provider = CampaignIdProviderForTesting;
        if (provider != null)
        {
            return provider();
        }
        Campaign campaign = Campaign.Current;
        return campaign == null ? null : campaign.UniqueGameId;
    }

    private static string SafeSegment(string value)
    {
        StringBuilder builder = new StringBuilder(value == null ? 0 : value.Length);
        foreach (char c in value ?? string.Empty)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' ? c : '_');
        }
        return builder.Length == 0 ? "unnamed" : builder.ToString();
    }

    private sealed class JsonFileKeyValueStore : IKeyValueStore
    {
        private readonly string namespaceId;
        private readonly string path;
        private readonly object gate = new object();
        private Dictionary<string, string> values;

        internal JsonFileKeyValueStore(string namespaceId, string path)
        {
            this.namespaceId = namespaceId;
            this.path = path;
        }

        public Task<OperationResult<string>> GetAsync(string key, RequestContext context, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                if (string.IsNullOrEmpty(key))
                {
                    return Task.FromResult(Failure<string>("awake.storage.key_missing", "A storage key is required.", context));
                }
                try
                {
                    string value;
                    Dictionary<string, string> current = Load();
                    return Task.FromResult(current.TryGetValue(key, out value) && value != null
                        ? OperationResult<string>.Succeeded(value)
                        : OperationResult<string>.Succeeded(string.Empty));
                }
                catch (Exception ex)
                {
                    AwakeLog.Write("storage_get_error namespace=" + namespaceId + " key=" + key + " error=" + ex.Message);
                    return Task.FromResult(Failure<string>("awake.storage.read_failed", "The stored value could not be read.", context));
                }
            }
        }

        public Task<OperationResult<bool>> SetAsync(string key, string value, RequestContext context, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                if (string.IsNullOrEmpty(key))
                {
                    return Task.FromResult(Failure<bool>("awake.storage.key_missing", "A storage key is required.", context));
                }
                try
                {
                    Dictionary<string, string> current = Load();
                    current[key] = value ?? string.Empty;
                    Save();
                    return Task.FromResult(OperationResult<bool>.Succeeded(true));
                }
                catch (Exception ex)
                {
                    AwakeLog.Write("storage_set_error namespace=" + namespaceId + " key=" + key + " error=" + ex.Message);
                    return Task.FromResult(Failure<bool>("awake.storage.write_failed", "The value could not be stored.", context));
                }
            }
        }

        public Task<OperationResult<bool>> DeleteAsync(string key, RequestContext context, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                if (string.IsNullOrEmpty(key))
                {
                    return Task.FromResult(Failure<bool>("awake.storage.key_missing", "A storage key is required.", context));
                }
                try
                {
                    Dictionary<string, string> current = Load();
                    if (current.Remove(key)) Save();
                    return Task.FromResult(OperationResult<bool>.Succeeded(true));
                }
                catch (Exception ex)
                {
                    AwakeLog.Write("storage_delete_error namespace=" + namespaceId + " key=" + key + " error=" + ex.Message);
                    return Task.FromResult(Failure<bool>("awake.storage.write_failed", "The value could not be deleted.", context));
                }
            }
        }

        private static OperationResult<T> Failure<T>(string code, string message, RequestContext context)
        {
            return OperationResult<T>.Failed(FrameworkErrors.Create(
                code,
                FrameworkErrorCategory.Unavailable,
                message,
                context?.CorrelationId ?? "storage",
                retryable: true,
                owner: AwakeConstants.OwnerValue));
        }

        private Dictionary<string, string> Load()
        {
            if (values != null) return values;
            Dictionary<string, string> loaded = new Dictionary<string, string>(StringComparer.Ordinal);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    JObject root = JObject.Parse(json);
                    JObject entries = root["values"] as JObject;
                    if (entries != null)
                    {
                        foreach (JProperty property in entries.Properties())
                        {
                            loaded[property.Name] = property.Value == null ? string.Empty : property.Value.ToString();
                        }
                    }
                }
            }
            values = loaded;
            return values;
        }

        private void Save()
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            JObject payload = new JObject
            {
                ["namespace"] = namespaceId,
                ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("o"),
                ["values"] = JObject.FromObject(values)
            };

            string temp = path + ".tmp";
            File.WriteAllText(temp, payload.ToString(Formatting.None), new UTF8Encoding(false));
            if (!File.Exists(path))
            {
                File.Move(temp, path);
                return;
            }

            // 首选原子替换：读者要么看到旧文件、要么看到新文件，不会看到半个。
            // ⚠️ 但 ReplaceFile 不是到处都可用 —— 2026-10-01 实测：本机在 DSH 文件沙箱下
            //    （工作区与临时目录都试过）一律返回 UnauthorizedAccessException「对路径的访问被拒绝」，
            //    而同一目录下 File.Copy(temp, path, true) 正常。结果是离线烟测
            //    dialogue-chain-redtest 的 storage delete 判据恒红 —— 门红在环境，不在代码。
            //    因此这里退化为覆盖拷贝：原子性在 Replace 可用的环境仍然保留。
            try
            {
                File.Replace(temp, path, null);
            }
            catch (IOException)
            {
                ReplaceByCopy(temp, path);
            }
            catch (UnauthorizedAccessException)
            {
                ReplaceByCopy(temp, path);
            }
        }

        private static void ReplaceByCopy(string temp, string path)
        {
            File.Copy(temp, path, true);
            File.Delete(temp);
        }
    }
}
