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

        try
        {
            string path = Path.Combine(
                AwakeModulePaths.ResolveModuleDirectory(),
                "PlayerExports",
                "AwakeState",
                SafeSegment(ResolveCampaignId()),
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

    private static string ResolveCampaignId()
    {
        Campaign campaign = Campaign.Current;
        string id = campaign == null ? null : campaign.UniqueGameId;
        return string.IsNullOrWhiteSpace(id) ? "unbound" : id;
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
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
    }
}
