using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace Awake;

internal sealed class AwakeHostComposition : IDisposable
{
    private const string CampaignId = "bannerlord.campaign";
    private const string TimelineId = "awake.timeline";

    private readonly object sync = new object();
    private RuntimeServiceClientOptions runtimeOptions;
    private RuntimeServiceClient runtimeClient;
    private FrameworkServiceOverrides serviceOverrides;
    private FrameworkHost host;
    private AwakeExtension extension;
    private SessionRef currentSession;
    private SessionLease currentLease;
    private Task<bool> campaignDrainTask;
    private bool pendingCampaignStart;
    private bool campaignStartScheduled;
    private bool campaignStartInProgress;
    private Func<string, bool> relaunchHook;
    private int runtimeRelaunchAttempts;
    private long lastRelaunchUtcTicks;
    private bool disposed;

    internal RuntimeServiceClient RuntimeClient => runtimeClient;
    internal IMarcusAwakeFrameworkHost Host => host;

    internal OperationResult<bool> Initialize(AwakeExtension awakeExtension)
    {
        if (awakeExtension == null)
        {
            return OperationResult<bool>.Failed(FrameworkErrors.Create(
                "awake.host.extension_missing",
                FrameworkErrorCategory.InvalidRequest,
                "The AWAKE extension is required.",
                "awake-host-init",
                owner: AwakeConstants.OwnerValue));
        }

        lock (sync)
        {
            if (disposed)
            {
                return OperationResult<bool>.Failed(FrameworkErrors.Create(
                    "awake.host.disposed",
                    FrameworkErrorCategory.Unavailable,
                    "The AWAKE host has been disposed.",
                    "awake-host-init",
                    retryable: true,
                    owner: AwakeConstants.OwnerValue));
            }

            if (host != null) return OperationResult<bool>.Succeeded(true);

            extension = awakeExtension;
            serviceOverrides = new FrameworkServiceOverrides
            {
                Permissions = new AwakePermissionService(),
                Prompts = new AwakePromptRegistry(),
                Storage = new AwakeFileStorageService(),
                GameData = new AwakePlayerSnapshotProvider()
                // Rag 保持框架默认的 UnavailableRagService：本批不接 Runtime RAG 数据面（011 再决策）。
            };
            runtimeOptions = new RuntimeServiceClientOptions(
                ResolveRuntimeServicePath(),
                "1.3.15");
            runtimeClient = new RuntimeServiceClient(runtimeOptions);
        }

        OperationResult<bool> registration = FrameworkHostLocator.Register(extension, runtimeClient, serviceOverrides);
        if (!registration.IsSuccess || !registration.Value)
        {
            runtimeClient.Dispose();
            lock (sync)
            {
                runtimeClient = null;
                runtimeOptions = null;
                serviceOverrides = null;
                extension = null;
            }
            return registration;
        }

        IMarcusAwakeFrameworkHost located = FrameworkHostLocator.Resolve();
        FrameworkHost locatedHost = located as FrameworkHost;
        if (locatedHost == null || !ReferenceEquals(locatedHost.Runtime, runtimeClient))
        {
            FrameworkHostLocator.Clear(located);
            runtimeClient.Dispose();
            lock (sync)
            {
                runtimeClient = null;
                runtimeOptions = null;
                serviceOverrides = null;
                extension = null;
            }
            return OperationResult<bool>.Failed(FrameworkErrors.Create(
                "awake.host.runtime_not_composed",
                FrameworkErrorCategory.Incompatible,
                "The AWAKE runtime service was not composed into the framework host.",
                "awake-host-init",
                owner: AwakeConstants.OwnerValue));
        }

        lock (sync) host = locatedHost;
        relaunchHook = TryRelaunchStoppedRuntime;
        AwakeRuntimeRecovery.RelaunchHook = relaunchHook;
        AwakeLog.Write("host_composed runtime_service_path=" + runtimeOptions.ServicePath);
        return OperationResult<bool>.Succeeded(true);
    }

    internal void BeginCampaignSession()
    {
        bool beginDrain = false;
        bool startNow = false;
        lock (sync)
        {
            if (disposed || host == null || extension == null) return;
            if (campaignStartScheduled || campaignStartInProgress) return;
            if (currentLease != null && currentLease.State != SessionState.Drained)
            {
                pendingCampaignStart = true;
                beginDrain = campaignDrainTask == null;
            }
            else
            {
                pendingCampaignStart = false;
                currentSession = null;
                currentLease = null;
                campaignDrainTask = null;
                campaignStartInProgress = true;
                startNow = true;
            }
        }

        if (beginDrain)
        {
            AwakeLog.Write("host_campaign_session_start_deferred reason=session_drain");
            BeginCampaignSessionEnd(false);
            return;
        }

        if (startNow) StartCampaignSessionCore();
    }

    private void StartCampaignSessionCore()
    {
        FrameworkHost activeHost;
        AwakeExtension activeExtension;
        SessionRef session;
        lock (sync)
        {
            if (disposed || host == null || extension == null)
            {
                campaignStartInProgress = false;
                return;
            }
            activeHost = host;
            activeExtension = extension;
            session = new SessionRef(CampaignId, TimelineId, "session-" + Guid.NewGuid().ToString("N"));
        }

        OperationResult<SessionLease> started = activeHost.Sessions.BeginSession(session);
        if (!started.IsSuccess || started.Value == null)
        {
            lock (sync) campaignStartInProgress = false;
            AwakeLog.Write("host_campaign_session_start_failed code=" + (started.Error?.Code ?? "unknown"));
            return;
        }

        lock (sync)
        {
            currentSession = session;
            currentLease = started.Value;
            campaignStartInProgress = false;
            runtimeRelaunchAttempts = 0;
            lastRelaunchUtcTicks = 0;
        }

        activeExtension.OnLifecycle(ExtensionLifecycleStage.CampaignSessionStarting, session);
        activeExtension.OnLifecycle(ExtensionLifecycleStage.CampaignSessionReady, session);
        StartRuntimeService(started.Value, session);
        AwakeLog.Write("host_campaign_session_ready session=" + session.SessionId);
    }

    internal void EndCampaignSession()
    {
        BeginCampaignSessionEnd(false);
    }

    private Task<bool> BeginCampaignSessionEnd(bool allowDisposed)
    {
        FrameworkHost activeHost;
        AwakeExtension activeExtension;
        RuntimeServiceClient activeRuntime;
        SessionRef session;
        SessionLease lease;
        TaskCompletionSource<bool> completion;
        bool beginClosing;
        lock (sync)
        {
            if ((disposed && !allowDisposed) || host == null || extension == null || currentSession == null || currentLease == null) return Task.FromResult(true);
            if (currentLease.State == SessionState.Drained) return Task.FromResult(true);
            if (campaignDrainTask != null) return campaignDrainTask;
            if (currentLease.State != SessionState.Ready && currentLease.State != SessionState.Closing) return Task.FromResult(true);
            activeHost = host;
            activeExtension = extension;
            activeRuntime = runtimeClient;
            session = currentSession;
            lease = currentLease;
            beginClosing = lease.State == SessionState.Ready;
            completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            campaignDrainTask = completion.Task;
        }

        try
        {
            if (beginClosing)
            {
                activeExtension.OnLifecycle(ExtensionLifecycleStage.SessionEnding, session);
                OperationResult<SessionLease> closing = activeHost.Sessions.BeginClosing(session);
                if (!closing.IsSuccess)
                {
                    CompleteCampaignDrain(activeHost, session, lease, completion, false, closing.Error?.Code ?? "session_close_failed");
                    return completion.Task;
                }
            }

            Task<OperationResult<RuntimeServiceStatus>> runtimeDrain = BeginRuntimeDrain(activeRuntime, activeHost, lease, session);
            AwakeBackgroundTask.Run(
                async () =>
                {
                    AwakeSessionDrainResult awakeDrain;
                    try
                    {
                        awakeDrain = await activeExtension.WaitForSessionEndDrainAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        AwakeLog.Write("host_awake_drain_wait_failed error=" + exception.Message);
                        CompleteCampaignDrain(activeHost, session, lease, completion, false, "awake_drain_exception");
                        return;
                    }

                    if (awakeDrain == null || !awakeDrain.Succeeded)
                    {
                        AwakeLog.Write("host_awake_drain_failed code=" + (awakeDrain?.ErrorCode ?? "awake_drain_failed"));
                        CompleteCampaignDrain(activeHost, session, lease, completion, false, awakeDrain?.ErrorCode ?? "awake_drain_failed");
                        return;
                    }

                    OperationResult<RuntimeServiceStatus> runtimeResult;
                    try
                    {
                        runtimeResult = await runtimeDrain.ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        AwakeLog.Write("host_runtime_drain_wait_failed error=" + exception.Message);
                        CompleteCampaignDrain(activeHost, session, lease, completion, false, "runtime_drain_exception");
                        return;
                    }

                    if (!runtimeResult.IsSuccess || runtimeResult.Value == null)
                    {
                        AwakeLog.Write("host_runtime_drain_failed code=" + (runtimeResult.Error?.Code ?? "unknown"));
                        CompleteCampaignDrain(activeHost, session, lease, completion, false, runtimeResult.Error?.Code ?? "runtime_drain_failed");
                        return;
                    }

                    OperationResult<bool> drained = activeHost.Sessions.CompleteDrain(session, lease.Generation);
                    CompleteCampaignDrain(activeHost, session, lease, completion, drained.IsSuccess, drained.Error?.Code ?? "session_drain_failed");
                },
                "awake_host_session_drain");
            return completion.Task;
        }
        catch (Exception exception)
        {
            AwakeLog.Write("host_campaign_session_end_failed error=" + exception.Message);
            CompleteCampaignDrain(activeHost, session, lease, completion, false, "session_end_exception");
            return completion.Task;
        }
    }

    private void CompleteCampaignDrain(
        FrameworkHost activeHost,
        SessionRef session,
        SessionLease lease,
        TaskCompletionSource<bool> completion,
        bool success,
        string failureCode)
    {
        bool schedulePendingStart = false;
        lock (sync)
        {
            if (ReferenceEquals(currentSession, session) && ReferenceEquals(currentLease, lease))
            {
                campaignDrainTask = null;
                if (success)
                {
                    currentSession = null;
                    currentLease = null;
                    if (pendingCampaignStart && !disposed && !campaignStartScheduled)
                    {
                        pendingCampaignStart = false;
                        campaignStartScheduled = true;
                        schedulePendingStart = true;
                    }
                }
            }
        }

        AwakeLog.Write("host_campaign_session_drained success=" + success + " code=" + (success ? string.Empty : failureCode));
        completion.TrySetResult(success);
        if (schedulePendingStart)
        {
            AwakeUiDispatcher.Enqueue(() =>
            {
                lock (sync) campaignStartScheduled = false;
                BeginCampaignSession();
            });
        }
    }

    public void Dispose()
    {
        FrameworkHost activeHost;
        AwakeExtension activeExtension;
        RuntimeServiceClient activeRuntime;
        SessionRef session;
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            pendingCampaignStart = false;
            campaignStartScheduled = false;
            activeHost = host;
            activeExtension = extension;
            activeRuntime = runtimeClient;
            session = currentSession;
        }

        Task<bool> sessionDrain = BeginCampaignSessionEnd(true);
        if (sessionDrain.IsCompleted)
        {
            FinalizeDispose(activeHost, activeExtension, activeRuntime, session);
            return;
        }

        sessionDrain.ContinueWith(
            completed =>
            {
                if (completed.IsFaulted || completed.IsCanceled || !completed.Result)
                {
                    AwakeLog.Write("host_dispose_drain_failed error=" + (completed.Exception?.GetBaseException()?.Message ?? "drain_incomplete"));
                    return;
                }
                FinalizeDispose(activeHost, activeExtension, activeRuntime, session);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void FinalizeDispose(
        FrameworkHost activeHost,
        AwakeExtension activeExtension,
        RuntimeServiceClient activeRuntime,
        SessionRef session)
    {
        try
        {
            activeExtension?.OnLifecycle(ExtensionLifecycleStage.Unregistered, session);
        }
        catch (Exception exception)
        {
            AwakeLog.Write("host_unregistered_lifecycle_failed error=" + exception.Message);
        }

        if (activeHost != null)
        {
            try
            {
                if (activeHost.State == HostState.Ready) activeHost.BeginClosing();
                if (activeHost.State == HostState.Closing) activeHost.CompleteDrain();
            }
            catch (Exception exception)
            {
                AwakeLog.Write("host_close_failed error=" + exception.Message);
            }
            FrameworkHostLocator.Clear(activeHost);
        }

        activeRuntime?.Dispose();
        lock (sync)
        {
            if (ReferenceEquals(AwakeRuntimeRecovery.RelaunchHook, relaunchHook))
            {
                AwakeRuntimeRecovery.RelaunchHook = null;
            }
            relaunchHook = null;
            host = null;
            extension = null;
            runtimeClient = null;
            runtimeOptions = null;
            currentSession = null;
            currentLease = null;
            campaignDrainTask = null;
            pendingCampaignStart = false;
            campaignStartScheduled = false;
            campaignStartInProgress = false;
        }
    }

    /// <summary>
    /// Relaunches a Runtime Service the framework tore down (state = Stopped) while the campaign
    /// session is still live. Reached only through <see cref="AwakeRuntimeRecovery"/> from the MCM
    /// gates, so it runs on the user's next AI action rather than in a tick loop.
    /// </summary>
    private bool TryRelaunchStoppedRuntime(string reason)
    {
        RuntimeServiceClient activeRuntime;
        SessionLease lease;
        SessionRef session;
        int attempt = 0;
        string skipReason = null;
        string previousState = "missing";

        lock (sync)
        {
            if (disposed || runtimeClient == null || currentLease == null || currentSession == null) return false;
            if (currentLease.State != SessionState.Ready) return false;

            activeRuntime = runtimeClient;
            lease = currentLease;
            session = currentSession;

            RuntimeServiceStatus status = activeRuntime.Status;
            previousState = status?.State.ToString() ?? "missing";
            long nowTicks = DateTime.UtcNow.Ticks;
            if (!AwakeRuntimeRecovery.ShouldRelaunch(
                    status?.State,
                    runtimeRelaunchAttempts,
                    lastRelaunchUtcTicks,
                    nowTicks,
                    out string policyReason))
            {
                skipReason = policyReason;
            }
            else
            {
                runtimeRelaunchAttempts++;
                lastRelaunchUtcTicks = nowTicks;
                attempt = runtimeRelaunchAttempts;
            }
        }

        if (skipReason != null)
        {
            AwakeLog.Write("runtime_service_relaunch_skipped reason=" + reason + " policy=" + skipReason);
            return false;
        }

        AwakeLog.Write("runtime_service_relaunch reason=" + reason
            + " attempt=" + attempt
            + " max=" + AwakeRuntimeRecovery.MaxAttemptsPerSession
            + " previous_state=" + previousState);
        StartRuntimeService(lease, session);
        return true;
    }
    private void StartRuntimeService(SessionLease lease, SessionRef session)
    {
        RuntimeServiceClient activeRuntime;
        RuntimeServiceClientOptions activeOptions;
        lock (sync)
        {
            activeRuntime = runtimeClient;
            activeOptions = runtimeOptions;
        }
        if (activeRuntime == null || activeOptions == null || lease == null || session == null) return;

        AwakeBackgroundTask.Run(
            async () =>
            {
                using (CancellationTokenSource operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(lease.CancellationToken))
                {
                    operationCancellation.CancelAfter(AwakeConstants.RequestTimeout);
                    RequestContext context = new RequestContext(
                        new ExtensionId(AwakeConstants.OwnerValue),
                        lease,
                        "awake.runtime.start." + Guid.NewGuid().ToString("N"),
                        DateTimeOffset.UtcNow + AwakeConstants.RequestTimeout);
                    OperationResult<RuntimeServiceStatus> result = await activeRuntime.StartAsync(
                        new RuntimeServiceStartRequest(activeOptions.ServiceId, activeOptions.ProtocolVersion, activeOptions.MaximumFrameBytes),
                        context,
                        operationCancellation.Token).ConfigureAwait(false);
                    AwakeLog.Write("runtime_service_start success=" + result.IsSuccess
                        + " state=" + (result.Value?.State.ToString() ?? "unknown")
                        + " code=" + (result.Error?.Code ?? string.Empty));

                    if (!result.IsSuccess || result.Value == null || !result.Value.IsReady)
                    {
                        AwakeUiDispatcher.Enqueue(() => AwakeSettings.UpdateRuntimeStatus(
                            "Runtime Service 尚未就绪，请检查 AWAKE 内置 Runtime。"));
                        return;
                    }

                    if (!IsCurrentCampaignSession(lease, session))
                    {
                        AwakeLog.Write("runtime_service_profile_apply_skipped reason=session_stale");
                        return;
                    }

                    IProviderRuntimePort provider = activeRuntime as IProviderRuntimePort;
                    if (provider == null)
                    {
                        AwakeLog.Write("runtime_service_profile_apply_skipped reason=provider_port_missing");
                        AwakeUiDispatcher.Enqueue(() => AwakeSettings.UpdateRuntimeStatus(
                            "Runtime Service 已就绪，但 Provider 端口不可用。"));
                        return;
                    }

                    AwakeUiDispatcher.Enqueue(() => AwakeSettings.UpdateRuntimeStatus(
                        "Runtime Service 已就绪，正在应用 AI 配置。"));
                    RequestContext providerContext = new RequestContext(
                        new ExtensionId(AwakeConstants.OwnerValue),
                        lease,
                        "awake.runtime.provider.apply." + Guid.NewGuid().ToString("N"),
                        DateTimeOffset.UtcNow + AwakeConstants.RequestTimeout);
                    OperationResult<bool> profiles = await AwakeProviderConfiguration.ApplyProfilesAsync(
                        provider,
                        providerContext,
                        operationCancellation.Token).ConfigureAwait(false);
                    AwakeLog.Write("runtime_service_profile_apply success=" + profiles.IsSuccess
                        + " code=" + (profiles.Error?.Code ?? string.Empty));
                    if (!profiles.IsSuccess)
                    {
                        AwakeProviderConfiguration.RecordProviderFailure("provider_apply_auto", profiles.Error);
                    }

                    if (IsCurrentCampaignSession(lease, session))
                    {
                        AwakeProviderConfiguration.RecordAutomaticApply(profiles);
                    }
                    else
                    {
                        AwakeLog.Write("runtime_service_profile_apply_result_discarded reason=session_stale");
                    }
                }
            },
            "awake_runtime_service_start");
    }

    private bool IsCurrentCampaignSession(SessionLease lease, SessionRef session)
    {
        lock (sync)
        {
            return !disposed
                && host != null
                && ReferenceEquals(currentLease, lease)
                && ReferenceEquals(currentSession, session)
                && lease != null
                && lease.State == SessionState.Ready;
        }
    }

    private static Task<OperationResult<RuntimeServiceStatus>> BeginRuntimeDrain(RuntimeServiceClient runtime, FrameworkHost host, SessionLease lease, SessionRef session)
    {
        if (runtime == null || host == null || lease == null || session == null)
        {
            return Task.FromResult(OperationResult<RuntimeServiceStatus>.Failed(FrameworkErrors.Create(
                "awake.runtime.drain_unavailable",
                FrameworkErrorCategory.Unavailable,
                "The AWAKE Runtime Service is unavailable during session drain.",
                "awake.runtime.drain",
                retryable: true,
                owner: AwakeConstants.OwnerValue)));
        }
        RequestContext context = new RequestContext(
            new ExtensionId(AwakeConstants.OwnerValue),
            lease,
            "awake.runtime.drain." + Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow + AwakeConstants.RequestTimeout);
        return runtime.BeginDrainAsync(context, CancellationToken.None);
    }

    private static string ResolveRuntimeServicePath()
    {
        string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrWhiteSpace(assemblyDirectory)) assemblyDirectory = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(assemblyDirectory, "Runtime", "MarcusAwakeRuntimeService.exe");
    }
}
