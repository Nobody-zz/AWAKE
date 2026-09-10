using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace Awake.SdkSmoke;

internal sealed class FakeKeyValueStore : IKeyValueStore
{
    private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

    internal int SetCount { get; private set; }
    internal bool FailSet { get; set; }
    internal int FailSetAfter { get; set; } = 1;
    internal bool FailGetWithKeyNotFound { get; set; }

    internal string GetValue(string key)
    {
        string value;
        if (_values.TryGetValue(key ?? string.Empty, out value)) return value;
        return null;
    }

    public Task<OperationResult<string>> GetAsync(string key, RequestContext context, CancellationToken cancellationToken)
    {
        if (FailGetWithKeyNotFound && string.IsNullOrEmpty(GetValue(key)))
        {
            return Task.FromResult(OperationResult<string>.Failed(FrameworkErrors.Create(
                "storage.key_not_found",
                FrameworkErrorCategory.NotFound,
                "key not found",
                null)));
        }
        return Task.FromResult(OperationResult<string>.Succeeded(GetValue(key)));
    }

    public Task<OperationResult<bool>> SetAsync(string key, string valueJson, RequestContext context, CancellationToken cancellationToken)
    {
        SetCount++;
        if (FailSet && SetCount >= FailSetAfter)
        {
            return Task.FromResult(OperationResult<bool>.Failed(FrameworkErrors.Create(
                "storage.write_failed",
                FrameworkErrorCategory.Unavailable,
                "write failed",
                null)));
        }
        _values[key ?? string.Empty] = valueJson;
        return Task.FromResult(OperationResult<bool>.Succeeded(true));
    }

    public Task<OperationResult<bool>> DeleteAsync(string key, RequestContext context, CancellationToken cancellationToken)
    {
        _values.Remove(key ?? string.Empty);
        return Task.FromResult(OperationResult<bool>.Succeeded(true));
    }
}


internal sealed class G3S0FakePermissionService : IPermissionService
{
    private readonly bool _granted;

    internal G3S0FakePermissionService(bool granted)
    {
        _granted = granted;
    }

    public PermissionEvaluation Evaluate(string permissionId, RequestContext context)
    {
        return new PermissionEvaluation(
            permissionId,
            new ExtensionId(AwakeConstants.OwnerValue),
            _granted ? PermissionDecision.Granted : PermissionDecision.Denied,
            "g3-s0-fake",
            null);
    }

    public Task<OperationResult<PermissionEvaluation>> RequestAsync(
        string permissionId,
        string purpose,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        PermissionEvaluation evaluation = Evaluate(permissionId, context);
        if (_granted)
        {
            return Task.FromResult(OperationResult<PermissionEvaluation>.Succeeded(evaluation));
        }
        return Task.FromResult(OperationResult<PermissionEvaluation>.Failed(FrameworkErrors.Create(
            "permission.request_denied",
            FrameworkErrorCategory.Denied,
            "G3-S0 fake permission denied.",
            context?.CorrelationId,
            owner: AwakeConstants.OwnerValue)));
    }

    public OperationResult<bool> Revoke(string permissionId, ExtensionId extensionId)
    {
        return OperationResult<bool>.Succeeded(true);
    }
}

internal sealed class G3S0FakeStorageService : IStorageService
{
    private readonly bool _available;
    private readonly FakeKeyValueStore _defaultStore;
    private readonly Dictionary<string, FakeKeyValueStore> _namespaceStores = new Dictionary<string, FakeKeyValueStore>(StringComparer.Ordinal);
    private readonly List<string> _openedNamespaces = new List<string>();

    internal G3S0FakeStorageService(
        FakeKeyValueStore defaultStore = null,
        bool available = true,
        IReadOnlyDictionary<string, FakeKeyValueStore> namespaceStores = null)
    {
        _defaultStore = defaultStore ?? new FakeKeyValueStore();
        _available = available;
        if (namespaceStores != null)
        {
            foreach (KeyValuePair<string, FakeKeyValueStore> pair in namespaceStores)
            {
                _namespaceStores[pair.Key] = pair.Value;
            }
        }
    }

    internal int OpenCount { get; private set; }
    internal int FailuresRemaining { get; set; }
    internal string FailNamespaceId { get; set; }
    internal IReadOnlyList<string> OpenedNamespaces => _openedNamespaces;
    internal FakeKeyValueStore DefaultStore => _defaultStore;
    internal int StateWriteCount
    {
        get
        {
            int count = _defaultStore.SetCount;
            foreach (FakeKeyValueStore store in _namespaceStores.Values)
            {
                if (!ReferenceEquals(store, _defaultStore)) count += store.SetCount;
            }
            return count;
        }
    }

    public Task<OperationResult<IKeyValueStore>> OpenCampaignNamespaceAsync(
        string namespaceId,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        OpenCount++;
        if (!_available)
        {
            return Task.FromResult(FailStorage("storage.unavailable", context));
        }
        if (FailuresRemaining > 0)
        {
            FailuresRemaining--;
            return Task.FromResult(FailStorage("storage.open_failed", context));
        }
        if (!string.IsNullOrWhiteSpace(FailNamespaceId)
            && StringComparer.Ordinal.Equals(namespaceId, FailNamespaceId))
        {
            return Task.FromResult(FailStorage("storage.open_failed", context));
        }

        FakeKeyValueStore store;
        if (!_namespaceStores.TryGetValue(namespaceId ?? string.Empty, out store)) store = _defaultStore;
        _openedNamespaces.Add(namespaceId ?? string.Empty);
        return Task.FromResult(OperationResult<IKeyValueStore>.Succeeded((IKeyValueStore)store));
    }

    public Task<OperationResult<IKeyValueStore>> OpenSessionNamespaceAsync(
        string namespaceId,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        return OpenCampaignNamespaceAsync(namespaceId, context, cancellationToken);
    }

    public Task<OperationResult<IRawSqlSession>> OpenSidecarAsync(RequestContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult<IRawSqlSession>.Failed(FrameworkErrors.Create(
            "storage.not_implemented",
            FrameworkErrorCategory.Unsupported,
            "G3-S0 fake sidecar is not implemented.",
            null)));
    }

    public Task<OperationResult<IReadOnlySqlSession>> OpenReadOnlyViewsAsync(RequestContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult<IReadOnlySqlSession>.Failed(FrameworkErrors.Create(
            "storage.not_implemented",
            FrameworkErrorCategory.Unsupported,
            "G3-S0 fake read-only views are not implemented.",
            null)));
    }

    public Task<OperationResult<TimelineExportInfo>> ExportCurrentTimelineAsync(RequestContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult<TimelineExportInfo>.Failed(FrameworkErrors.Create(
            "storage.not_implemented",
            FrameworkErrorCategory.Unsupported,
            "G3-S0 fake export is not implemented.",
            null)));
    }

    public Task<OperationResult<TimelineImportInfo>> ImportTimelineAsync(string exportId, RequestContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult<TimelineImportInfo>.Failed(FrameworkErrors.Create(
            "storage.not_implemented",
            FrameworkErrorCategory.Unsupported,
            "G3-S0 fake import is not implemented.",
            null)));
    }

    private static OperationResult<IKeyValueStore> FailStorage(string code, RequestContext context)
    {
        return OperationResult<IKeyValueStore>.Failed(FrameworkErrors.Create(
            code,
            FrameworkErrorCategory.Unavailable,
            "G3-S0 fake storage open failed.",
            context?.CorrelationId,
            retryable: true,
            owner: AwakeConstants.OwnerValue));
    }
}

internal sealed class G3S0FakeHost : IMarcusAiFrameworkHost
{
    internal G3S0FakeHost(G3S0FakeStorageService storage, G3S0FakePermissionService permissions)
    {
        Storage = storage;
        Permissions = permissions;
        CurrentSession = new SessionRef("g3-s0-campaign", "g3-s0-timeline", "g3-s0-session");
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
    public IStorageService Storage { get; }
    public IAssetService Assets => null;
    public IPermissionService Permissions { get; }
    public IDiagnosticsService Diagnostics => null;
    public ILoggingService Log => null;
}
