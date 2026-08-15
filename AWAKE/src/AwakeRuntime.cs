using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;

namespace Awake;

internal static class KnowledgeConstants
{
    internal const string CorpusRelativePath = "Knowledge/awake_knowledge.json";
    internal const string CollectionId = "awake.knowledge";
    internal const string FingerprintKey = "knowledge.fingerprint.v1";
    internal const string PermissionRagWrite = "rag.collection.write:" + CollectionId;
    internal const string PermissionRagRead = "rag.collection.read:" + CollectionId;
    internal const string AccessScope = "ExtensionProvider";
    internal const int MaximumSearchResults = 5;
    internal const int MaximumRetrievedBlockBytes = 4096;
}

internal static class AwakeRuntime
{
    private static readonly object StaticGate = new object();
    private static int _sessionGeneration;
    private static bool _sessionEnded;
    private static string _currentHeroId = string.Empty;
    private static WorldStateStore _worldStateStore;
    private static Task<bool> _bindingTask;
    private static Task<bool> _interactiveBindingTask;
    private static IMarcusAiFrameworkHost _testHostOverride;

    internal static IMarcusAiFrameworkHost ResolveHost()
    {
        IMarcusAiFrameworkHost testOverride = _testHostOverride;
        if (testOverride != null) return testOverride;
        if (FrameworkHostLocator.TryGetHost(out IMarcusAiFrameworkHost host)) return host;
        return null;
    }

    internal static void SetHostOverrideForTesting(IMarcusAiFrameworkHost host)
    {
        _testHostOverride = host;
    }

    internal static RequestContext CreateContext(IMarcusAiFrameworkHost host, string correlationId)
    {
        SessionRef session = host?.CurrentSession ?? new SessionRef(string.Empty, string.Empty, string.Empty);
        return new RequestContext(
            new ExtensionId(AwakeConstants.OwnerValue),
            session,
            correlationId ?? Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow + AwakeConstants.RequestTimeout);
    }

    internal static string CanonicalizeArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson)) return "{}";
        try
        {
            Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(argumentsJson);
            return SortToken(obj).ToString(Newtonsoft.Json.Formatting.None);
        }
        catch
        {
            return argumentsJson.Trim();
        }
    }

    internal static string TruncateTextElements(string value, int maximumElements)
    {
        if (string.IsNullOrEmpty(value) || maximumElements <= 0) return string.Empty;
        int count = 0;
        StringBuilder builder = new StringBuilder();
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            if (count >= maximumElements) break;
            builder.Append(enumerator.GetTextElement());
            count++;
        }
        return builder.ToString();
    }

    internal static string TruncateTextElementsFromEnd(string value, int maximumElements)
    {
        if (string.IsNullOrEmpty(value) || maximumElements <= 0) return string.Empty;
        List<string> elements = new List<string>();
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }
        if (elements.Count <= maximumElements) return value;
        StringBuilder builder = new StringBuilder();
        for (int i = elements.Count - maximumElements; i < elements.Count; i++)
        {
            builder.Append(elements[i]);
        }
        return builder.ToString();
    }

    internal static bool ShouldRefreshPlayerKnown(string playerName, int lastRefreshDay, int currentDay)
    {
        return string.IsNullOrWhiteSpace(playerName) || currentDay != lastRefreshDay;
    }

    internal static Func<int> CurrentGameDayProvider { get; set; } = ReadCampaignGameDay;

    internal static int CurrentGameDay()
    {
        try
        {
            return (CurrentGameDayProvider ?? ReadCampaignGameDay)();
        }
        catch
        {
            return 0;
        }
    }

    private static int ReadCampaignGameDay()
    {
        Type campaignTime = Type.GetType("TaleWorlds.CampaignSystem.CampaignTime, TaleWorlds.CampaignSystem", throwOnError: false);
        if (campaignTime == null) return 0;
        System.Reflection.PropertyInfo now = campaignTime.GetProperty("Now", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (now == null || !now.CanRead) return 0;
        object value = now.GetValue(null, null);
        if (value == null) return 0;
        System.Reflection.PropertyInfo days = value.GetType().GetProperty("ToDays", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (days == null || !days.CanRead) return 0;
        return (int)Math.Floor(Convert.ToDouble(days.GetValue(value, null)));
    }

    private static Newtonsoft.Json.Linq.JToken SortToken(Newtonsoft.Json.Linq.JToken token)
    {
        if (token is Newtonsoft.Json.Linq.JObject obj)
        {
            Newtonsoft.Json.Linq.JObject sorted = new Newtonsoft.Json.Linq.JObject();
            foreach (var pair in obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                sorted[pair.Name] = SortToken(pair.Value);
            }
            return sorted;
        }
        if (token is Newtonsoft.Json.Linq.JArray array)
        {
            Newtonsoft.Json.Linq.JArray sortedArray = new Newtonsoft.Json.Linq.JArray();
            foreach (Newtonsoft.Json.Linq.JToken item in array)
            {
                sortedArray.Add(SortToken(item));
            }
            return sortedArray;
        }
        return token;
    }

    internal static string CurrentHeroId
    {
        get { lock (StaticGate) return _currentHeroId; }
    }

    internal static bool SessionEnded
    {
        get { lock (StaticGate) return _sessionEnded; }
    }

    internal static int SessionGeneration
    {
        get { lock (StaticGate) return _sessionGeneration; }
    }

    internal static WorldStateStore WorldStateStore
    {
        get { lock (StaticGate) return _worldStateStore; }
    }

    internal static void SetWorldStateStore(WorldStateStore store)
    {
        lock (StaticGate)
        {
            if (_sessionEnded) return;
            _worldStateStore = store;
        }
    }

    internal static WorldStateStore ClaimWorldStateStore(IMarcusAiFrameworkHost host, out bool created)
    {
        lock (StaticGate)
        {
            created = false;
            if (_sessionEnded) return null;
            if (_worldStateStore == null)
            {
                _worldStateStore = new WorldStateStore(host);
                created = true;
            }
            return _worldStateStore;
        }
    }

    internal static void ReleaseWorldStateStore(WorldStateStore expected)
    {
        lock (StaticGate)
        {
            if (expected != null && ReferenceEquals(_worldStateStore, expected))
            {
                _worldStateStore = null;
            }
        }
    }

    internal static async Task<bool> EnsureWorldStateReadyAsync(IMarcusAiFrameworkHost host, CancellationToken cancellationToken)
    {
        if (host == null) return false;
        if (SessionEnded)
        {
            AwakeLog.Write("world_state_ready_after_session_end");
            return false;
        }
        WorldStateStore store = null;
        bool claimed = false;
        try
        {
            RequestContext context = CreateContext(host, Guid.NewGuid().ToString("N"));
            PermissionDefinition storagePermission;
            if (!PermissionCatalog.TryGet(AwakeConstants.PermissionStorageWrite, out storagePermission))
            {
                AwakeLog.Write("world_state_storage_catalog_missing");
                return false;
            }
            PermissionGateResult gate = await new PermissionGate(host).EnsureAsync(
                storagePermission,
                context,
                cancellationToken,
                "AWAKE 需要写入运行时状态。").ConfigureAwait(false);
            if (!gate.Granted)
            {
                AwakeLog.Write("world_state_storage_permission_denied code=" + (gate.Error?.Code ?? "none"));
                return false;
            }

            store = WorldStateStore;
            if (store == null)
            {
                lock (StaticGate)
                {
                    store = _worldStateStore;
                    if (store == null && !_sessionEnded)
                    {
                        store = new WorldStateStore(host);
                        _worldStateStore = store;
                        claimed = true;
                    }
                }
            }
            bool any = await store.OpenNamespacesAsync(cancellationToken).ConfigureAwait(false);
            if (!any)
            {
                if (claimed)
                {
                    lock (StaticGate)
                    {
                        if (ReferenceEquals(_worldStateStore, store)) _worldStateStore = null;
                    }
                }
                AwakeLog.Write("world_state_storage_open_failed");
                return false;
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            if (claimed)
            {
                lock (StaticGate)
                {
                    if (ReferenceEquals(_worldStateStore, store)) _worldStateStore = null;
                }
            }
            throw;
        }
        catch (Exception ex)
        {
            if (claimed)
            {
                lock (StaticGate)
                {
                    if (ReferenceEquals(_worldStateStore, store)) _worldStateStore = null;
                }
            }
            AwakeLog.Write("world_state_ready_error error=" + ex.Message);
            return false;
        }
    }

    internal static void ResetSessionStateForCampaign()
    {
        lock (StaticGate)
        {
            _sessionEnded = false;
            _currentHeroId = string.Empty;
            _worldStateStore = null;
            _bindingTask = null;
            _interactiveBindingTask = null;
        }
        AwakeRuntimeStatus.ResetForTesting();
    }

    internal static void ResetSessionStateForTesting()
    {
        lock (StaticGate)
        {
            _sessionEnded = false;
            _sessionGeneration = 0;
            _currentHeroId = string.Empty;
            _worldStateStore = null;
            _bindingTask = null;
            _interactiveBindingTask = null;
            _testHostOverride = null;
            CurrentGameDayProvider = ReadCampaignGameDay;
        }
    }
    internal static void BeginSessionEnd()
    {
        lock (StaticGate)
        {
            _sessionEnded = true;
            _sessionGeneration++;
            _currentHeroId = string.Empty;
            _bindingTask = null;
            _interactiveBindingTask = null;
        }
    }

    internal static Task<bool> EnsureCurrentHeroBoundAsync(CancellationToken cancellationToken)
    {
        return EnsureCurrentHeroBoundAsync(ResolveHost(), cancellationToken, requestPermission: false);
    }

    internal static Task<bool> EnsureCurrentHeroBoundAsync(IMarcusAiFrameworkHost host, CancellationToken cancellationToken, bool requestPermission = false)
    {
        lock (StaticGate)
        {
            if (!_sessionEnded && !string.IsNullOrEmpty(_currentHeroId))
            {
                return Task.FromResult(true);
            }
            if (requestPermission)
            {
                if (_interactiveBindingTask != null)
                {
                    return _interactiveBindingTask;
                }
                Task<bool> interactiveBinding = BindingCoreAsync(host, cancellationToken, requestPermission: true);
                _interactiveBindingTask = interactiveBinding;
                _ = interactiveBinding.ContinueWith(
                    _ =>
                    {
                        lock (StaticGate)
                        {
                            if (ReferenceEquals(_interactiveBindingTask, interactiveBinding)) _interactiveBindingTask = null;
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return interactiveBinding;
            }
            if (_bindingTask != null)
            {
                return _bindingTask;
            }
            Task<bool> binding = BindingCoreAsync(host, cancellationToken, requestPermission: false);
            _bindingTask = binding;
            _ = binding.ContinueWith(
                _ =>
                {
                    lock (StaticGate)
                    {
                        if (ReferenceEquals(_bindingTask, binding)) _bindingTask = null;
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return binding;
        }
    }

    private static async Task<bool> BindingCoreAsync(IMarcusAiFrameworkHost host, CancellationToken cancellationToken, bool requestPermission)
    {
        try
        {
            if (host == null || host.GameData == null)
            {
                return false;
            }
            RequestContext context = CreateContext(host, Guid.NewGuid().ToString("N"));
            PermissionDefinition playerKnown;
            if (!PermissionCatalog.TryGet(AwakeConstants.PermissionPlayerKnownRead, out playerKnown))
            {
                AwakeLog.Write("player_hero_bind_catalog_missing");
                return false;
            }
            PermissionGateResult playerKnownGate;
            if (requestPermission)
            {
                playerKnownGate = await new PermissionGate(host).EnsureAsync(
                    playerKnown,
                    context,
                    cancellationToken,
                    "AWAKE 需要读取当前玩家信息以完成角色绑定。").ConfigureAwait(false);
            }
            else
            {
                playerKnownGate = new PermissionGate(host).Evaluate(playerKnown, context);
            }
            if (!playerKnownGate.Granted)
            {
                AwakeLog.Write("player_hero_bind_permission_denied code=" + (playerKnownGate.Error?.Code ?? "none") + " correlation=" + context.CorrelationId);
                return false;
            }
            OperationResult<PlayerSnapshotDto> result = await host.GameData.GetCurrentPlayerAsync(context, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess || result.Value == null || result.Value.Hero == null
                || result.Value.Hero.Id == null || string.IsNullOrWhiteSpace(result.Value.Hero.Id.StableId))
            {
                AwakeLog.Write("player_hero_bind_failed code=" + (result.Error?.Code ?? "empty"));
                return false;
            }
            string heroId = result.Value.Hero.Id.StableId;
            lock (StaticGate)
            {
                if (_sessionEnded) return false;
                _currentHeroId = heroId;
            }
            AwakeLog.Write("player_hero_bound hero=" + heroId);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("player_hero_bind_error error=" + ex.Message);
            return false;
        }

    }
}
