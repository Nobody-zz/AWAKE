using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace Awake.WorldbookRuntimeProductionSmoke;

internal sealed class ProductionSmokeHost : IMarcusAiFrameworkHost
{
    internal ProductionSmokeHost(SessionRef session, bool grantPermissions = true)
    {
        CurrentSession = session ?? throw new ArgumentNullException(nameof(session));
        StorageAdapter = new ProductionSmokeStorage();
        EventsAdapter = new ProductionSmokeEvents();
        PermissionsAdapter = new ProductionSmokePermissions(grantPermissions);
    }

    internal ProductionSmokeStorage StorageAdapter { get; }
    internal ProductionSmokeEvents EventsAdapter { get; }
    internal ProductionSmokePermissions PermissionsAdapter { get; }

    public FrameworkIdentity Identity { get; } = FrameworkIdentity.Current("1.3.15");
    public SessionRef CurrentSession { get; }
    public ICapabilityBroker Capabilities => null;
    public IToolCandidateService Tools => null;
    public IGameDataService GameData => null;
    public IContextService Context => null;
    public IRagService Rag => null;
    public IEventService Events => EventsAdapter;
    public ICommandService Commands => null;
    public IAiGateway Ai => null;
    public IAiModelService Models => null;
    public IMediaService Media => null;
    public IPromptRegistry Prompts => null;
    public IStorageService Storage => StorageAdapter;
    public IAssetService Assets => null;
    public IPermissionService Permissions => PermissionsAdapter;
    public IDiagnosticsService Diagnostics => null;
    public ILoggingService Log => null;
}

internal sealed class ProductionSmokeStorage : IStorageService
{
    private readonly object _gate = new object();
    private readonly Dictionary<string, ProductionSmokeKeyValueStore> _stores = new Dictionary<string, ProductionSmokeKeyValueStore>(StringComparer.Ordinal);

    internal ProductionSmokeKeyValueStore SetNamespace(string namespaceId, ProductionSmokeKeyValueStore store = null)
    {
        if (string.IsNullOrWhiteSpace(namespaceId)) throw new ArgumentException("Namespace is required.", nameof(namespaceId));
        lock (_gate)
        {
            ProductionSmokeKeyValueStore value = store ?? new ProductionSmokeKeyValueStore(namespaceId);
            _stores[namespaceId] = value;
            return value;
        }
    }

    internal ProductionSmokeKeyValueStore GetNamespace(string namespaceId)
    {
        lock (_gate)
        {
            return _stores.TryGetValue(namespaceId, out ProductionSmokeKeyValueStore store) ? store : null;
        }
    }

    public Task<OperationResult<IKeyValueStore>> OpenCampaignNamespaceAsync(string namespaceId, RequestContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult<IKeyValueStore>.Succeeded(GetOrCreateNamespace(namespaceId)));
    }

    public Task<OperationResult<IKeyValueStore>> OpenSessionNamespaceAsync(string namespaceId, RequestContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult<IKeyValueStore>.Succeeded(GetOrCreateNamespace(namespaceId)));
    }

    private ProductionSmokeKeyValueStore GetOrCreateNamespace(string namespaceId)
    {
        lock (_gate)
        {
            if (_stores.TryGetValue(namespaceId, out ProductionSmokeKeyValueStore existing)) return existing;
            ProductionSmokeKeyValueStore created = new ProductionSmokeKeyValueStore(namespaceId);
            _stores[namespaceId] = created;
            return created;
        }
    }
}

internal sealed class ProductionSmokeKeyValueStore : IKeyValueStore
{
    private readonly object _gate = new object();
    private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly string _namespaceId;
    private bool _blockNextGet;
    private string _blockedKey;
    private TaskCompletionSource<bool> _blockedStarted;
    private TaskCompletionSource<bool> _blockedRelease;
    private bool _blockNextSet;
    private string _blockedSetKey;
    private TaskCompletionSource<bool> _blockedSetStarted;
    private TaskCompletionSource<bool> _blockedSetRelease;
    private int _getCount;
    private int _setCount;

    internal ProductionSmokeKeyValueStore(string namespaceId)
    {
        _namespaceId = namespaceId ?? string.Empty;
    }

    internal int GetCount => Volatile.Read(ref _getCount);
    internal int SetCount => Volatile.Read(ref _setCount);
    internal string NamespaceId => _namespaceId;

    internal void Put(string key, string value)
    {
        lock (_gate) _values[key] = value;
    }

    internal string Read(string key)
    {
        lock (_gate) return _values.TryGetValue(key, out string value) ? value : null;
    }

    internal ProductionSmokeGetBarrier BlockNextGet(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));
        lock (_gate)
        {
            if (_blockNextGet) throw new InvalidOperationException("A read barrier is already armed.");
            _blockNextGet = true;
            _blockedKey = key;
            _blockedStarted = NewCompletionSource();
            _blockedRelease = NewCompletionSource();
            return new ProductionSmokeGetBarrier(_blockedStarted.Task, () => _blockedRelease.TrySetResult(true));
        }
    }

    internal ProductionSmokeSetBarrier BlockNextSet(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));
        lock (_gate)
        {
            if (_blockNextSet) throw new InvalidOperationException("A write barrier is already armed.");
            _blockNextSet = true;
            _blockedSetKey = key;
            _blockedSetStarted = NewCompletionSource();
            _blockedSetRelease = NewCompletionSource();
            return new ProductionSmokeSetBarrier(_blockedSetStarted.Task, () => _blockedSetRelease.TrySetResult(true));
        }
    }

    public async Task<OperationResult<string>> GetAsync(string key, RequestContext context, CancellationToken cancellationToken)
    {
        Task release = null;
        lock (_gate)
        {
            if (_blockNextGet && StringComparer.Ordinal.Equals(_blockedKey, key))
            {
                _blockNextGet = false;
                _blockedStarted.TrySetResult(true);
                release = _blockedRelease.Task;
            }
        }
        if (release != null) await release.ConfigureAwait(false);
        Interlocked.Increment(ref _getCount);
        lock (_gate)
        {
            if (_values.TryGetValue(key, out string value)) return TaskResult.Success(value);
        }
        return TaskResult.Failure<string>("storage.key_not_found", FrameworkErrorCategory.NotFound, context);
    }

    public async Task<OperationResult<bool>> SetAsync(string key, string value, RequestContext context, CancellationToken cancellationToken)
    {
        Task release = null;
        lock (_gate)
        {
            if (_blockNextSet && StringComparer.Ordinal.Equals(_blockedSetKey, key))
            {
                _blockNextSet = false;
                _blockedSetStarted.TrySetResult(true);
                release = _blockedSetRelease.Task;
            }
        }
        if (release != null) await release.ConfigureAwait(false);
        lock (_gate) _values[key] = value;
        Interlocked.Increment(ref _setCount);
        return TaskResult.Success(true);
    }

    public Task<OperationResult<bool>> DeleteAsync(string key, RequestContext context, CancellationToken cancellationToken)
    {
        lock (_gate) _values.Remove(key);
        return Task.FromResult(TaskResult.Success(true));
    }

    private static TaskCompletionSource<bool> NewCompletionSource()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

internal sealed class ProductionSmokeGetBarrier
{
    private readonly Action _release;

    internal ProductionSmokeGetBarrier(Task started, Action release)
    {
        Started = started ?? throw new ArgumentNullException(nameof(started));
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    internal Task Started { get; }

    internal void Release()
    {
        _release();
    }
}

internal sealed class ProductionSmokeSetBarrier
{
    private readonly Action _release;

    internal ProductionSmokeSetBarrier(Task started, Action release)
    {
        Started = started ?? throw new ArgumentNullException(nameof(started));
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    internal Task Started { get; }

    internal void Release()
    {
        _release();
    }
}

internal sealed class ProductionSmokePermissions : IPermissionService
{
    private readonly bool _grant;

    internal ProductionSmokePermissions(bool grant)
    {
        _grant = grant;
    }

    public PermissionEvaluation Evaluate(string permissionId, RequestContext context)
    {
        return new PermissionEvaluation(
            permissionId,
            new ExtensionId("AWAKE"),
            _grant ? PermissionDecision.Granted : PermissionDecision.Denied,
            _grant ? "production smoke grant" : "production smoke deny",
            null);
    }

    public Task<OperationResult<PermissionEvaluation>> RequestAsync(string permissionId, string purpose, RequestContext context, CancellationToken cancellationToken)
    {
        PermissionEvaluation evaluation = Evaluate(permissionId, context);
        return Task.FromResult(OperationResult<PermissionEvaluation>.Succeeded(evaluation));
    }

    public OperationResult<bool> Revoke(string permissionId, ExtensionId extensionId)
    {
        return OperationResult<bool>.Succeeded(true);
    }
}

internal sealed class ProductionSmokeEvents : IEventService
{
    private readonly object _gate = new object();
    private readonly List<EventEnvelope> _published = new List<EventEnvelope>();

    internal bool FailPublishes { get; set; }
    internal bool ThrowPublishes { get; set; }

    internal IReadOnlyList<EventEnvelope> Published
    {
        get
        {
            lock (_gate) return _published.ToArray();
        }
    }

    public IEventSubscription Subscribe(string eventKind, Action<EventEnvelope> handler)
    {
        return new ProductionSmokeSubscription();
    }

    public OperationResult<bool> Publish(EventEnvelope envelope, EventDelivery delivery, RequestContext context)
    {
        if (ThrowPublishes) throw new InvalidOperationException("production smoke event publish failure");
        if (FailPublishes)
        {
            return OperationResult<bool>.Failed(FrameworkErrors.Create(
                "production_smoke.event_publish_failed",
                FrameworkErrorCategory.ProviderFailure,
                "production smoke event publish failed",
                context?.CorrelationId ?? "production-smoke"));
        }
        lock (_gate) _published.Add(envelope);
        return OperationResult<bool>.Succeeded(true);
    }
}

internal sealed class ProductionSmokeSubscription : IEventSubscription
{
    public string SubscriptionId { get; } = Guid.NewGuid().ToString("N");

    public void Dispose()
    {
    }
}

internal static class TaskResult
{
    internal static OperationResult<T> Success<T>(T value)
    {
        return OperationResult<T>.Succeeded(value);
    }

    internal static OperationResult<T> Failure<T>(string code, FrameworkErrorCategory category, RequestContext context)
    {
        return OperationResult<T>.Failed(FrameworkErrors.Create(
            code,
            category,
            code,
            context?.CorrelationId ?? "production-smoke"));
    }
}
