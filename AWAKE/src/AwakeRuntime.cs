using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal enum NativeReadinessStatus
{
    Ready,
    Failed,
    Cancelled,
    Skipped
}

internal sealed class NativeReadinessResult
{
    private NativeReadinessResult(
        NativeReadinessStatus status,
        int sessionGeneration,
        string sessionId,
        string failureCode,
        bool retryable)
    {
        Status = status;
        SessionGeneration = sessionGeneration;
        SessionId = sessionId;
        FailureCode = failureCode;
        Retryable = retryable;
    }

    internal NativeReadinessStatus Status { get; }
    internal int SessionGeneration { get; }
    internal string SessionId { get; }
    internal string FailureCode { get; }
    internal bool Retryable { get; }

    internal static NativeReadinessResult Ready(int generation, string sessionId)
    {
        return new NativeReadinessResult(NativeReadinessStatus.Ready, generation, sessionId, string.Empty, false);
    }

    internal static NativeReadinessResult Failed(int generation, string failureCode, bool retryable)
    {
        return new NativeReadinessResult(NativeReadinessStatus.Failed, generation, string.Empty, failureCode, retryable);
    }

    internal static NativeReadinessResult Cancelled(int generation, string sessionId)
    {
        return new NativeReadinessResult(NativeReadinessStatus.Cancelled, generation, sessionId, "cancelled", true);
    }

    internal static NativeReadinessResult Skipped(int generation, string sessionId, string failureCode)
    {
        return new NativeReadinessResult(NativeReadinessStatus.Skipped, generation, sessionId, failureCode, false);
    }

    internal NativeReadinessResult ForSession(int generation, string sessionId)
    {
        return new NativeReadinessResult(Status, generation, sessionId, FailureCode, Retryable);
    }
}

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
    private const string EmbeddedFrameworkProductId = "awake.framework";
    private const string EmbeddedFrameworkAssemblyName = "MarcusAwakeFramework";
    private const string EmbeddedRuntimeServiceId = "marcus-awake.runtime-service";
    private static readonly object StaticGate = new object();
    private static int _sessionGeneration;
    private static CancellationTokenSource _sessionCancellation = new CancellationTokenSource();
    private static long _worldStateDrainBoundaryGeneration;
    private static bool _sessionEnded;
    private static string _currentHeroId = string.Empty;
    private static WorldStateStore _worldStateStore;
    private static Task<bool> _bindingTask;
    private static Task<bool> _interactiveBindingTask;
    private static Task<NativeReadinessResult> _nativeReadinessTask;
    private static IMarcusAiFrameworkHost _testHostOverride;
    private static readonly SemaphoreSlim StoreTransitionGate = new SemaphoreSlim(1, 1);
    private static readonly List<RetiredWorldStateDrain> RetiredWorldStateDrains = new List<RetiredWorldStateDrain>();
    private static bool _worldStateDrainFailed;
    private static string _worldStateDrainFailureCode = string.Empty;
    private static string _hostResolutionStatus = "unresolved";
    private static RuntimeServiceState? _embeddedRuntimeState;

    private sealed class RetiredWorldStateDrain
    {
        internal WorldStateStore Store { get; }
        internal Task<WorldFinalDrainResult> DrainTask { get; }
        internal long BoundaryGeneration { get; }
        internal bool Failed { get; set; }

        internal RetiredWorldStateDrain(
            WorldStateStore store,
            Task<WorldFinalDrainResult> drainTask,
            long boundaryGeneration)
        {
            Store = store;
            DrainTask = drainTask;
            BoundaryGeneration = boundaryGeneration;
        }
    }

    internal static Func<int, CancellationToken, Task<NativeReadinessResult>> NativeReadinessProbeForTesting { get; set; }

    internal static string HostResolutionStatus
    {
        get { lock (StaticGate) return _hostResolutionStatus; }
    }

    internal static RuntimeServiceState? EmbeddedRuntimeState
    {
        get { lock (StaticGate) return _embeddedRuntimeState; }
    }

    internal static IMarcusAiFrameworkHost ResolveHost()
    {
        IMarcusAiFrameworkHost testOverride;
        bool sessionEnded;
        lock (StaticGate)
        {
            testOverride = _testHostOverride;
            sessionEnded = _sessionEnded;
        }
        if (testOverride != null)
        {
            PublishHostResolution("test_override", null);
            return testOverride;
        }
        if (sessionEnded)
        {
            PublishHostResolution("session_ended", null);
            return null;
        }

        IMarcusAwakeFrameworkHost fullHost;
        try
        {
            fullHost = FrameworkHostLocator.Resolve();
            IMarcusAiFrameworkHost host;
            RuntimeServiceState? runtimeState;
            string failureCode;
            if (!TryValidateEmbeddedHost(fullHost, out host, out runtimeState, out failureCode))
            {
                PublishHostResolution(failureCode, runtimeState);
                return null;
            }

            PublishHostResolution("embedded_host_ready", runtimeState);
            return host;
        }
        catch (Exception ex)
        {
            PublishHostResolution("host_validation_error", null);
            AwakeLog.Write("awake_host_resolve_error error=" + ex.Message);
            return null;
        }
    }

    internal static void SetHostOverrideForTesting(IMarcusAiFrameworkHost host)
    {
        lock (StaticGate) _testHostOverride = host;
    }

    private static bool TryValidateEmbeddedHost(
        IMarcusAwakeFrameworkHost fullHost,
        out IMarcusAiFrameworkHost host,
        out RuntimeServiceState? runtimeState,
        out string failureCode)
    {
        host = null;
        runtimeState = null;
        failureCode = "host_missing";
        if (fullHost == null) return false;

        FrameworkIdentity identity = fullHost.Identity;
        if (identity == null
            || !StringComparer.Ordinal.Equals(identity.ProductId, EmbeddedFrameworkProductId)
            || !StringComparer.Ordinal.Equals(identity.AssemblyName, EmbeddedFrameworkAssemblyName))
        {
            failureCode = "host_not_embedded_awake";
            return false;
        }
        if (fullHost.State != HostState.Ready)
        {
            failureCode = "host_not_ready";
            return false;
        }

        IRuntimeServicePort runtime = fullHost.Runtime;
        if (runtime == null)
        {
            failureCode = "runtime_port_missing";
            return false;
        }
        RuntimeServiceStatus status = runtime.Status;
        if (status == null)
        {
            failureCode = "runtime_status_missing";
            return false;
        }
        runtimeState = status.State;
        if (!status.IsReady)
        {
            failureCode = "runtime_not_ready";
            return false;
        }
        if (!StringComparer.Ordinal.Equals(status.ServiceId, EmbeddedRuntimeServiceId))
        {
            failureCode = "runtime_service_mismatch";
            return false;
        }
        if (!(runtime is IAiGateway))
        {
            failureCode = "runtime_ai_gateway_missing";
            return false;
        }

        host = fullHost as IMarcusAiFrameworkHost;
        if (host == null || host.Ai == null)
        {
            host = null;
            failureCode = "compatibility_ai_host_missing";
            return false;
        }
        return true;
    }

    private static void PublishHostResolution(string status, RuntimeServiceState? runtimeState)
    {
        bool changed;
        lock (StaticGate)
        {
            string normalized = string.IsNullOrWhiteSpace(status) ? "unknown" : status;
            changed = !StringComparer.Ordinal.Equals(_hostResolutionStatus, normalized)
                || _embeddedRuntimeState != runtimeState;
            _hostResolutionStatus = normalized;
            _embeddedRuntimeState = runtimeState;
        }
        if (changed)
        {
            AwakeLog.Write("awake_host_resolution status=" + status
                + " runtime=" + (runtimeState?.ToString() ?? "none"));
        }
    }

    internal static RequestContext CreateContext(IMarcusAiFrameworkHost host, string correlationId)
    {
        IMarcusAwakeFrameworkHost fullHost = FrameworkHostLocator.Resolve();
        if (fullHost != null
            && (host == null || Object.ReferenceEquals(fullHost, host))
            && fullHost.Sessions != null)
        {
            SessionLease lease = fullHost.Sessions.Current;
            if (lease != null)
            {
                return new RequestContext(
                    new ExtensionId(AwakeConstants.OwnerValue),
                    lease,
                    correlationId ?? Guid.NewGuid().ToString("N"),
                    DateTimeOffset.UtcNow + AwakeConstants.RequestTimeout);
            }
        }

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

    internal static bool IsCurrentSessionGeneration(int generation)
    {
        lock (StaticGate) return !_sessionEnded && _sessionGeneration == generation;
    }

    internal static CancellationToken SessionCancellationToken
    {
        get { lock (StaticGate) return _sessionCancellation.Token; }
    }

    internal static bool IsCurrentSession(int generation, WorldStateStore expectedStore)
    {
        lock (StaticGate)
        {
            return !_sessionEnded
                && _sessionGeneration == generation
                && ReferenceEquals(_worldStateStore, expectedStore);
        }
    }

    internal static WorldStateStore WorldStateStore
    {
        get { lock (StaticGate) return _worldStateStore; }
    }

    internal static bool IsNativeKnowledgeReady()
    {
        lock (StaticGate)
        {
            if (_sessionEnded || _nativeReadinessTask == null || _nativeReadinessTask.Status != TaskStatus.RanToCompletion)
                return false;
            try
            {
                NativeReadinessResult result = _nativeReadinessTask.GetAwaiter().GetResult();
                return result != null
                    && result.Status == NativeReadinessStatus.Ready
                    && result.SessionGeneration == _sessionGeneration;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static async Task<bool> SetWorldStateStore(
        WorldStateStore store,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (store == null) return false;
        await StoreTransitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int sessionGeneration;
            bool sessionWasEnded;
            Task<WorldFinalDrainResult> previousDrain = null;
            lock (StaticGate)
            {
                sessionGeneration = _sessionGeneration;
                sessionWasEnded = _sessionEnded;
                if (_sessionEnded)
                {
                    previousDrain = RetireWorldStateStoreLocked(store);
                }
                else if (ReferenceEquals(_worldStateStore, store))
                {
                    return true;
                }
                else
                {
                    sessionGeneration = _sessionGeneration;
                    if (_worldStateStore != null)
                    {
                        previousDrain = RetireWorldStateStoreLocked(_worldStateStore);
                        _worldStateStore = null;
                    }
                }
            }

            if (previousDrain != null)
            {
                await previousDrain.ConfigureAwait(false);
            }
            if (!await AwaitRetiredWorldStateDrainsAsync(cancellationToken).ConfigureAwait(false))
            {
                await RetireStandaloneWorldStateStoreAsync(store).ConfigureAwait(false);
                return false;
            }

            if (sessionWasEnded)
            {
                await RetireStandaloneWorldStateStoreAsync(store).ConfigureAwait(false);
                return false;
            }

            bool installed = false;
            lock (StaticGate)
            {
                if (!_sessionEnded
                    && !_worldStateDrainFailed
                    && _sessionGeneration == sessionGeneration
                    && _worldStateStore == null
                    && store.LifecycleState == WorldStateStoreLifecycle.Active)
                {
                    _worldStateStore = store;
                    installed = true;
                }
            }
            if (installed) return true;
            await RetireStandaloneWorldStateStoreAsync(store).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException)
        {
            _ = RetireStandaloneWorldStateStoreAsync(store);
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_store_set_error error=" + ex.Message);
            await RetireStandaloneWorldStateStoreAsync(store).ConfigureAwait(false);
            return false;
        }
        finally
        {
            StoreTransitionGate.Release();
        }
    }

    internal static Task<WorldStateStore> ClaimWorldStateStore(
        IMarcusAiFrameworkHost host,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        return ClaimWorldStateStoreAsync(host, cancellationToken);
    }

    internal static async Task<WorldStateStore> ClaimWorldStateStoreAsync(
        IMarcusAiFrameworkHost host,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (host == null) return null;
        await StoreTransitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Task<WorldFinalDrainResult> previousDrain = null;
            lock (StaticGate)
            {
                if (_sessionEnded || _worldStateDrainFailed) return null;
                if (_worldStateStore != null && !_worldStateStore.SessionEnded)
                {
                    return _worldStateStore;
                }
                if (_worldStateStore != null)
                {
                    previousDrain = RetireWorldStateStoreLocked(_worldStateStore);
                    _worldStateStore = null;
                }
            }
            if (previousDrain != null) await previousDrain.ConfigureAwait(false);
            if (!await AwaitRetiredWorldStateDrainsAsync(cancellationToken).ConfigureAwait(false)) return null;

            lock (StaticGate)
            {
                if (_sessionEnded || _worldStateDrainFailed || _worldStateStore != null) return null;
                _worldStateStore = new WorldStateStore(host);
                return _worldStateStore;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_store_claim_error error=" + ex.Message);
            return null;
        }
        finally
        {
            StoreTransitionGate.Release();
        }
    }

    internal static async Task<WorldFinalDrainResult> ReleaseWorldStateStore(WorldStateStore expected)
    {
        if (expected == null) return WorldFinalDrainResult.NotStarted();
        await StoreTransitionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            Task<WorldFinalDrainResult> drainTask;
            lock (StaticGate)
            {
                if (ReferenceEquals(_worldStateStore, expected))
                {
                    _worldStateStore = null;
                }
                drainTask = RetireWorldStateStoreLocked(expected);
            }
            return await drainTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_store_release_error error=" + ex.Message);
            return new WorldFinalDrainResult(false, 0, 0, 0, "awake.world_state.final_drain_error");
        }
        finally
        {
            StoreTransitionGate.Release();
        }
    }

    internal static Task<NativeReadinessResult> EnsureNativeReadinessAsync(string sessionId, CancellationToken cancellationToken)
    {
        lock (StaticGate)
        {
            if (_sessionEnded)
            {
                return Task.FromResult(NativeReadinessResult.Skipped(_sessionGeneration, sessionId, "session_ended"));
            }
            if (_nativeReadinessTask != null)
            {
                return _nativeReadinessTask;
            }

            int generation = _sessionGeneration;
            Func<int, CancellationToken, Task<NativeReadinessResult>> probe = NativeReadinessProbeForTesting;
            _nativeReadinessTask = RunNativeReadinessAsync(generation, sessionId, cancellationToken, probe);
            return _nativeReadinessTask;
        }
    }

    internal static async Task<bool> EnsureKnowledgeReadyIfNativeReadyAsync(CancellationToken cancellationToken)
    {
        int generation;
        lock (StaticGate)
        {
            if (_sessionEnded) return false;
            generation = _sessionGeneration;
        }

        NativeReadinessResult readiness = await EnsureNativeReadinessAsync(string.Empty, cancellationToken).ConfigureAwait(false);
        if (readiness == null
            || readiness.Status != NativeReadinessStatus.Ready
            || readiness.SessionGeneration != generation
            || !IsCurrentSessionGeneration(generation))
            return false;

        int currentDay = CurrentGameDay();
        if (currentDay <= 0) return false;
        await WorldEventServices.EnsureKnowledgeReadyAsync(currentDay, cancellationToken).ConfigureAwait(false);
        return IsCurrentSessionGeneration(generation);
    }

    private static async Task<NativeReadinessResult> RunNativeReadinessAsync(
        int generation,
        string sessionId,
        CancellationToken cancellationToken,
        Func<int, CancellationToken, Task<NativeReadinessResult>> probe)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
                return PublishNativeReadiness(generation, sessionId, NativeReadinessResult.Cancelled(generation, sessionId));

            NativeReadinessResult result = probe == null
                ? await ProbeNativeReadinessAsync(generation, sessionId, cancellationToken).ConfigureAwait(false)
                : await probe(generation, cancellationToken).ConfigureAwait(false);
            return PublishNativeReadiness(
                generation,
                sessionId,
                result ?? NativeReadinessResult.Failed(generation, "empty_result", retryable: true));
        }
        catch (OperationCanceledException)
        {
            return PublishNativeReadiness(generation, sessionId, NativeReadinessResult.Cancelled(generation, sessionId));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("native_readiness_error error=" + ex.Message);
            return PublishNativeReadiness(
                generation,
                sessionId,
                NativeReadinessResult.Failed(generation, "native_readiness_exception", retryable: true));
        }
    }

    private static NativeReadinessResult PublishNativeReadiness(
        int generation,
        string sessionId,
        NativeReadinessResult result)
    {
        lock (StaticGate)
        {
            if (_sessionEnded || _sessionGeneration != generation)
                return NativeReadinessResult.Skipped(generation, sessionId, "stale_session");
            NativeReadinessResult normalized = (result ?? NativeReadinessResult.Failed(generation, "empty_result", retryable: true))
                .ForSession(generation, sessionId);
            return normalized;
        }
    }

    private static Task<NativeReadinessResult> ProbeNativeReadinessAsync(
        int generation,
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(NativeReadinessResult.Cancelled(generation, sessionId));
        }
        try
        {
            if (Campaign.Current == null)
            {
                return Task.FromResult(NativeReadinessResult.Skipped(generation, sessionId, "campaign_unavailable"));
            }
            if (Hero.MainHero == null)
            {
                return Task.FromResult(NativeReadinessResult.Skipped(generation, sessionId, "player_unavailable"));
            }
            return Task.FromResult(NativeReadinessResult.Ready(generation, sessionId));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("native_readiness_probe_error error=" + ex.Message);
            return Task.FromResult(NativeReadinessResult.Failed(generation, "native_probe_exception", retryable: true));
        }
    }

    internal static Task<bool> EnsureWorldStateReadyAsync(IMarcusAiFrameworkHost host, CancellationToken cancellationToken)
    {
        return EnsureWorldStateReadyAsync(host, cancellationToken, null);
    }

    internal static async Task<bool> EnsureWorldStateReadyAsync(
        IMarcusAiFrameworkHost host,
        CancellationToken cancellationToken,
        IReadOnlyCollection<string> requiredNamespaces)
    {
        if (host == null) return false;
        if (SessionEnded)
        {
            AwakeLog.Write("world_state_ready_after_session_end");
            return false;
        }

        string[] targetNamespaces = requiredNamespaces == null || requiredNamespaces.Count == 0
            ? AiTaskConstants.StorageNamespaceIds
            : new List<string>(requiredNamespaces).ToArray();
        if (targetNamespaces.Length == 0) return false;

        await StoreTransitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        WorldStateStore candidate = null;
        try
        {
            if (!await AwaitRetiredWorldStateDrainsAsync(cancellationToken).ConfigureAwait(false))
            {
                AwakeLog.Write("world_state_ready_drain_barrier_failed code=" + _worldStateDrainFailureCode);
                return false;
            }

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

            int sessionGeneration;
            Task<WorldFinalDrainResult> previousDrain = null;
            lock (StaticGate)
            {
                if (_sessionEnded) return false;
                sessionGeneration = _sessionGeneration;
                WorldStateStore existing = _worldStateStore;
                if (existing != null && existing.HasNamespaces(targetNamespaces)) return true;
                if (existing != null)
                {
                    previousDrain = RetireWorldStateStoreLocked(existing);
                    _worldStateStore = null;
                }
            }
            if (previousDrain != null) await previousDrain.ConfigureAwait(false);
            if (!await AwaitRetiredWorldStateDrainsAsync(cancellationToken).ConfigureAwait(false))
            {
                AwakeLog.Write("world_state_ready_replacement_drain_failed code=" + _worldStateDrainFailureCode);
                return false;
            }

            candidate = new WorldStateStore(host);
            bool opened = await candidate.OpenNamespacesAsync(cancellationToken, targetNamespaces).ConfigureAwait(false);
            if (!opened || !candidate.HasNamespaces(targetNamespaces))
            {
                AwakeLog.Write("world_state_storage_open_failed required=" + string.Join(",", targetNamespaces));
                await RetireStandaloneWorldStateStoreAsync(candidate).ConfigureAwait(false);
                candidate = null;
                return false;
            }

            bool installed = false;
            lock (StaticGate)
            {
                if (!_sessionEnded
                    && !_worldStateDrainFailed
                    && _sessionGeneration == sessionGeneration
                    && _worldStateStore == null
                    && candidate.LifecycleState == WorldStateStoreLifecycle.Active)
                {
                    _worldStateStore = candidate;
                    installed = true;
                }
            }
            if (installed)
            {
                candidate = null;
                return true;
            }

            await RetireStandaloneWorldStateStoreAsync(candidate).ConfigureAwait(false);
            candidate = null;
            return false;
        }
        catch (OperationCanceledException)
        {
            if (candidate != null) _ = RetireStandaloneWorldStateStoreAsync(candidate);
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_ready_error error=" + ex.Message);
            if (candidate != null) await RetireStandaloneWorldStateStoreAsync(candidate).ConfigureAwait(false);
            return false;
        }
        finally
        {
            StoreTransitionGate.Release();
        }
    }

    internal static void ResetSessionStateForCampaign()
    {
        WorldEventServices.WithCampaignBoundary(() =>
        {
            ResetSessionStateForCampaignCore();
            WorldEventServices.ResetForCampaign();
        });
    }

    internal static void ResetSessionStateForCampaignCore()
    {
        CancellationTokenSource previousCancellation;
        lock (StaticGate)
        {
            previousCancellation = _sessionCancellation;
            _sessionCancellation = new CancellationTokenSource();
            if (_worldStateStore != null)
            {
                RetireWorldStateStoreLocked(_worldStateStore);
            }
            _sessionGeneration++;
            _worldStateDrainBoundaryGeneration++;
            _sessionEnded = false;
            _currentHeroId = string.Empty;
            _worldStateStore = null;
            _bindingTask = null;
            _interactiveBindingTask = null;
            _nativeReadinessTask = null;
            _worldStateDrainFailed = false;
            _worldStateDrainFailureCode = string.Empty;
        }
        previousCancellation.Cancel();
        AwakeRuntimeStatus.ResetForTesting();
    }

    internal static async Task ResetSessionStateForTestingAsync()
    {
        _ = BeginSessionEnd();
        await AwaitRetiredWorldStateDrainsAsync(CancellationToken.None).ConfigureAwait(false);
        CancellationTokenSource previousCancellation;
        lock (StaticGate)
        {
            previousCancellation = _sessionCancellation;
            _sessionCancellation = new CancellationTokenSource();
            _sessionEnded = false;
            _sessionGeneration = 0;
            _currentHeroId = string.Empty;
            _worldStateStore = null;
            _bindingTask = null;
            _interactiveBindingTask = null;
            _nativeReadinessTask = null;
            _testHostOverride = null;
            _worldStateDrainFailed = false;
            _worldStateDrainFailureCode = string.Empty;
            RetiredWorldStateDrains.Clear();
            CurrentGameDayProvider = ReadCampaignGameDay;
        }
        previousCancellation.Cancel();
        AwakeRuntimeStatus.ResetForTesting();
    }

    internal static void ResetSessionStateForTesting()
    {
        ResetSessionStateForTestingAsync().GetAwaiter().GetResult();
    }

    internal static Task<WorldFinalDrainResult> BeginSessionEnd()
    {
        Task<WorldFinalDrainResult> drainTask = null;
        CancellationTokenSource sessionCancellation = null;
        WorldEventServices.WithCampaignBoundary(() =>
        {
            lock (StaticGate)
            {
                sessionCancellation = _sessionCancellation;
                if (_worldStateStore != null)
                {
                    drainTask = RetireWorldStateStoreLocked(_worldStateStore);
                    _worldStateStore = null;
                }
                _sessionEnded = true;
                _sessionGeneration++;
                _worldStateDrainBoundaryGeneration++;
                _currentHeroId = string.Empty;
                _bindingTask = null;
                _interactiveBindingTask = null;
                _nativeReadinessTask = null;
            }
        });
        sessionCancellation?.Cancel();
        return drainTask ?? Task.FromResult(WorldFinalDrainResult.NotStarted());
    }

    private static Task<WorldFinalDrainResult> RetireWorldStateStoreLocked(WorldStateStore store)
    {
        if (store == null) return Task.FromResult(WorldFinalDrainResult.NotStarted());
        foreach (RetiredWorldStateDrain retired in RetiredWorldStateDrains)
        {
            if (ReferenceEquals(retired.Store, store)) return retired.DrainTask;
        }

        store.BeginSessionEnd();
        Task<WorldFinalDrainResult> drainTask = store.BeginFinalDrainAsync();
        RetiredWorldStateDrain retiredDrain = new RetiredWorldStateDrain(
            store,
            drainTask,
            _worldStateDrainBoundaryGeneration);
        RetiredWorldStateDrains.Add(retiredDrain);
        _ = ObserveWorldStateDrainAsync(retiredDrain);
        return drainTask;
    }

    private static async Task ObserveWorldStateDrainAsync(RetiredWorldStateDrain retired)
    {
        WorldFinalDrainResult result;
        try
        {
            result = await retired.DrainTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_drain_observer_error error=" + ex.Message);
            result = new WorldFinalDrainResult(false, 0, 0, 0, "awake.world_state.final_drain_error");
        }
        lock (StaticGate)
        {
            bool appliesToCurrentBoundary = retired.BoundaryGeneration == _worldStateDrainBoundaryGeneration;
            if (result == null || !result.Succeeded)
            {
                retired.Failed = true;
                if (string.IsNullOrWhiteSpace(_worldStateDrainFailureCode))
                {
                    _worldStateDrainFailureCode = result?.ErrorCode ?? "awake.world_state.final_drain_error";
                }
                if (appliesToCurrentBoundary)
                {
                    _worldStateDrainFailed = true;
                }
            }
            if (!retired.Failed)
            {
                RetiredWorldStateDrains.RemoveAll(value => ReferenceEquals(value, retired));
            }
        }
    }

    private static async Task<bool> AwaitRetiredWorldStateDrainsAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task<WorldFinalDrainResult>[] pending;
            lock (StaticGate)
            {
                ObserveCompletedWorldStateDrainsLocked();
                pending = RetiredWorldStateDrains
                    .Where(value => !value.DrainTask.IsCompleted)
                    .Select(value => value.DrainTask)
                    .ToArray();
            }
            if (pending.Length == 0)
            {
                lock (StaticGate)
                {
                    ObserveCompletedWorldStateDrainsLocked();
                    pending = RetiredWorldStateDrains
                        .Where(value => !value.DrainTask.IsCompleted)
                        .Select(value => value.DrainTask)
                        .ToArray();
                    if (pending.Length == 0)
                    {
                        return !_worldStateDrainFailed
                            && !RetiredWorldStateDrains.Any(value => value.Failed);
                    }
                }
            }

            Task all = Task.WhenAll(pending);
            if (cancellationToken.CanBeCanceled)
            {
                Task cancellation = Task.Delay(Timeout.Infinite, cancellationToken);
                Task completed = await Task.WhenAny(all, cancellation).ConfigureAwait(false);
                if (!ReferenceEquals(completed, all)) throw new OperationCanceledException(cancellationToken);
            }
            try
            {
                await all.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lock (StaticGate)
                {
                    _worldStateDrainFailed = true;
                    if (string.IsNullOrWhiteSpace(_worldStateDrainFailureCode))
                    {
                        _worldStateDrainFailureCode = "awake.world_state.final_drain_error";
                    }
                }
                AwakeLog.Write("world_state_drain_barrier_error error=" + ex.Message);
            }
        }
    }

    private static void ObserveCompletedWorldStateDrainsLocked()
    {
        for (int i = RetiredWorldStateDrains.Count - 1; i >= 0; i--)
        {
            RetiredWorldStateDrain retired = RetiredWorldStateDrains[i];
            if (!retired.DrainTask.IsCompleted) continue;
            WorldFinalDrainResult result;
            try
            {
                result = retired.DrainTask.GetAwaiter().GetResult();
            }
            catch
            {
                result = new WorldFinalDrainResult(false, 0, 0, 0, "awake.world_state.final_drain_error");
            }
            bool appliesToCurrentBoundary = retired.BoundaryGeneration == _worldStateDrainBoundaryGeneration;
            if (result == null || !result.Succeeded)
            {
                retired.Failed = true;
                if (string.IsNullOrWhiteSpace(_worldStateDrainFailureCode))
                {
                    _worldStateDrainFailureCode = result?.ErrorCode ?? "awake.world_state.final_drain_error";
                }
                if (appliesToCurrentBoundary)
                {
                    _worldStateDrainFailed = true;
                }
            }
            else
            {
                RetiredWorldStateDrains.RemoveAt(i);
            }
        }
    }

    private static async Task RetireStandaloneWorldStateStoreAsync(WorldStateStore store)
    {
        if (store == null) return;
        Task<WorldFinalDrainResult> drainTask;
        lock (StaticGate)
        {
            drainTask = RetireWorldStateStoreLocked(store);
        }
        try
        {
            await drainTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_rejected_store_drain_error error=" + ex.Message);
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
