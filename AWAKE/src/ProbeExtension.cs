using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("Awake.SdkSmoke")]

namespace Awake;

internal static class DialogueOverlayLifecycle
{
    internal static Action CloseAll = () => { };
}

internal static class CampaignResetLifecycle
{
    internal static Action Reset { get; set; }
}

internal static class ProbeLog
{
    private static readonly object FileLock = new object();

    internal static void Write(string line)
    {
        try
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            DirectoryInfo current = new DirectoryInfo(assemblyDir);
            string moduleDir = assemblyDir;
            for (int i = 0; i < 6 && current != null; i++)
            {
                if (File.Exists(Path.Combine(current.FullName, "SubModule.xml")))
                {
                    moduleDir = current.FullName;
                    break;
                }
                current = current.Parent;
            }
            string logs = Path.Combine(moduleDir, "Logs");
            Directory.CreateDirectory(logs);
            string path = Path.Combine(logs, "AwakeProbe.log");
            lock (FileLock)
            {
                TryRotate(path);
                File.AppendAllText(path,
                    DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'") + " " + line + Environment.NewLine);
            }
        }
        catch
        {
            // Probe logging must never take down module load.
        }
    }

    private static void TryRotate(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            FileInfo info = new FileInfo(path);
            if (info.Length < 1024 * 1024) return;
            string rotated = path + ".1";
            if (File.Exists(rotated)) File.Delete(rotated);
            File.Move(path, rotated);
        }
        catch
        {
        }
    }
}

internal sealed class AwakeSessionDrainResult
{
    internal AwakeSessionDrainResult(bool succeeded, string errorCode)
    {
        Succeeded = succeeded;
        ErrorCode = errorCode ?? string.Empty;
    }

    internal bool Succeeded { get; }
    internal string ErrorCode { get; }

    internal static AwakeSessionDrainResult Success()
    {
        return new AwakeSessionDrainResult(true, string.Empty);
    }
}

internal sealed class AwakeExtension : IFrameworkExtension
{
    private static readonly ExtensionId Owner = new ExtensionId("AWAKE");
    private static readonly CapabilityId EchoId = new CapabilityId("capability://AWAKE/probe/echo/v1");
    private static readonly object LifecycleSync = new object();
    private static Task<AwakeSessionDrainResult> sessionDrainTask = Task.FromResult(AwakeSessionDrainResult.Success());

    public ExtensionManifest Manifest { get; } = new ExtensionManifest(
        Owner,
        "AWAKE.ModuleName",
        AwakeVersion.Version,
        0,
        1,
        new[] { "1.3.15" },
        PermissionCatalog.ManifestPermissionIds,
        null,
        null,
        AiTaskConstants.AllRouteIds);

    public void Register(IExtensionRegistration registration)
    {
        if (registration == null) throw new ArgumentNullException(nameof(registration));

        OperationResult<bool> capability = registration.RegisterCapability(
            new CapabilityDescriptor(
                EchoId,
                Owner,
                "probe",
                new SchemaRef("awake.echo.request", 1, 0),
                new SchemaRef("awake.echo.response", 1, 0),
                CapabilityVisibility.Public,
                CapabilityMaturity.Experimental,
                CapabilityAvailability.Available,
                "game"),
            HandleEchoAsync);
        if (!capability.IsSuccess || !capability.Value)
        {
            throw new InvalidOperationException("register_capability_failed code=" + (capability.Error?.Code ?? "unknown"));
        }

        OperationResult<bool> provider = registration.RegisterContextProvider(new ProbeContextProvider());
        if (!provider.IsSuccess || !provider.Value)
        {
            throw new InvalidOperationException("register_context_provider_failed code=" + (provider.Error?.Code ?? "unknown"));
        }

        foreach (IContextProvider worldProvider in new IContextProvider[]
        {
            new PlayerContextProvider(),
            new HeroContextProvider()
        })
        {
            OperationResult<bool> worldProviderResult = registration.RegisterContextProvider(worldProvider);
            if (!worldProviderResult.IsSuccess || !worldProviderResult.Value)
            {
                throw new InvalidOperationException("register_world_context_provider_failed id=" + worldProvider.ProviderId + " code=" + (worldProviderResult.Error?.Code ?? "unknown"));
            }
        }

        RegisterWorldCommand(registration, new CommandDescriptor(
            AiTaskConstants.RelationshipDeltaCommandId,
            Owner,
            CommandRiskTier.R2Gameplay,
            AiTaskConstants.CommandInputSchema(AiTaskConstants.RelationshipDeltaCommandId),
            AiTaskConstants.CommandOutputSchema(AiTaskConstants.RelationshipDeltaCommandId),
            new[] { "1.3.15" }),
            new AwakeRelationshipDeltaAdapter(),
            "relationship_delta");

        RegisterWorldCommand(registration, new CommandDescriptor(
            AiTaskConstants.WorldEffectRecordCommandId,
            Owner,
            CommandRiskTier.R2Gameplay,
            AiTaskConstants.CommandInputSchema(AiTaskConstants.WorldEffectRecordCommandId),
            AiTaskConstants.CommandOutputSchema(AiTaskConstants.WorldEffectRecordCommandId),
            new[] { "1.3.15" }),
            new AwakeWorldEffectRecordAdapter(),
            "world_effect_record");

        RegisterWorldCommand(registration, new CommandDescriptor(
            AiTaskConstants.PromiseRequestCommandId,
            Owner,
            CommandRiskTier.R1Interface,
            AiTaskConstants.CommandInputSchema(AiTaskConstants.PromiseRequestCommandId),
            AiTaskConstants.CommandOutputSchema(AiTaskConstants.PromiseRequestCommandId),
            new[] { "1.3.15" }),
            new AwakePromiseRequestAdapter(),
            "promise_request");

        RegisterWorldCommand(registration, new CommandDescriptor(
            AiTaskConstants.PromiseUpdateCommandId,
            Owner,
            CommandRiskTier.R1Interface,
            AiTaskConstants.CommandInputSchema(AiTaskConstants.PromiseUpdateCommandId),
            AiTaskConstants.CommandOutputSchema(AiTaskConstants.PromiseUpdateCommandId),
            new[] { "1.3.15" }),
            new AwakePromiseUpdateAdapter(),
            "promise_update");

        RegisterWorldCommand(registration, new CommandDescriptor(
            AiTaskConstants.GiveGoldCommandId,
            Owner,
            CommandRiskTier.R2Gameplay,
            AiTaskConstants.CommandInputSchema(AiTaskConstants.GiveGoldCommandId),
            AiTaskConstants.CommandOutputSchema(AiTaskConstants.GiveGoldCommandId),
            new[] { "1.3.15" }),
            new AwakeGiveGoldAdapter(),
            "give_gold");
    }

    internal Task<AwakeSessionDrainResult> WaitForSessionEndDrainAsync()
    {
        lock (LifecycleSync) return sessionDrainTask;
    }

    private static void RegisterWorldCommand(IExtensionRegistration registration, CommandDescriptor descriptor, ICommandAdapter adapter, string label)
    {
        OperationResult<bool> result = registration.RegisterCommand(descriptor, adapter);
        if (!result.IsSuccess || !result.Value)
        {
            throw new InvalidOperationException("register_" + label + "_command_failed code=" + (result.Error?.Code ?? "unknown"));
        }
    }

    public void OnLifecycle(ExtensionLifecycleStage stage, SessionRef session)
    {
        ProbeLog.Write("lifecycle stage=" + stage + " session=" + (session?.SessionId ?? "none"));
        try
        {
            switch (stage)
            {
                case ExtensionLifecycleStage.CampaignSessionReady:
                    Action campaignReset = CampaignResetLifecycle.Reset;
                    if (campaignReset != null)
                    {
                        campaignReset();
                    }
                    else
                    {
                        AwakeRuntime.ResetSessionStateForCampaign();
                    }
                    int campaignGeneration = AwakeRuntime.SessionGeneration;
                    IMarcusAiFrameworkHost storageHost = AwakeRuntime.ResolveHost();
                    if (storageHost == null) FrameworkHostLocator.TryGetHost(out storageHost);
                    Task<bool> storageReadiness = AwakeRuntime.EnsureWorldStateStorageReadyAsync(
                        storageHost,
                        AwakeRuntime.SessionCancellationToken);
                    _ = storageReadiness.ContinueWith(
                        task =>
                        {
                            if (task.Status == TaskStatus.RanToCompletion
                                && task.Result
                                && AwakeRuntime.IsCurrentSessionGeneration(campaignGeneration))
                            {
                                AwakeBackgroundTask.Run(
                                    () => RestoreCampaignStateAsync(campaignGeneration),
                                    "awake_campaign_state_restore");
                            }
                            else if (task.Status == TaskStatus.RanToCompletion)
                            {
                                AwakeLog.Write("awake_campaign_storage_not_ready generation=" + campaignGeneration);
                            }
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    Task<NativeReadinessResult> nativeReadiness = AwakeRuntime.EnsureNativeReadinessAsync(
                        session?.SessionId,
                        CancellationToken.None);
                    _ = nativeReadiness.ContinueWith(
                        task =>
                        {
                            if (task.Status == TaskStatus.RanToCompletion && task.Result != null)
                            {
                                NativeReadinessResult result = task.Result;
                                AwakeLog.Write("native_readiness status=" + result.Status
                                    + " generation=" + result.SessionGeneration
                                    + " code=" + (result.FailureCode ?? string.Empty));
                            }
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    _ = nativeReadiness.ContinueWith(
                        task =>
                        {
                            if (task.Status == TaskStatus.RanToCompletion
                                && task.Result != null
                                && task.Result.Status == NativeReadinessStatus.Ready
                                && task.Result.SessionGeneration == AwakeRuntime.SessionGeneration)
                            {
                                AwakeBackgroundTask.Run(
                                    () => AwakeRuntime.EnsureKnowledgeReadyIfNativeReadyAsync(AwakeRuntime.SessionCancellationToken),
                                    "awake_knowledge_projection");
                            }
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    AwakeGoldSettlementService.ResetForCampaign();
                    AwakeOnboardingService.ResetForCampaign();
                    EventDialogueQueue.ResetForCampaign();
                    try
                    {
                        DialogueOverlayLifecycle.CloseAll?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        AwakeLog.Write("dialogue_overlay_campaign_ready_close_error error=" + ex.Message);
                    }
                    NpcMemoryService.ShutdownCurrent();
                    if (FrameworkHostLocator.TryGetHost(out IMarcusAiFrameworkHost memoryHost))
                    {
                        NpcMemoryService.SetCurrent(new NpcMemoryService(memoryHost));
                        AwakeLog.Write("npc_memory_service_initialized");
                    }
                    KnowledgeRuntime.ShutdownCurrent();
                    WorldbookRuntime.ShutdownCurrent();
                    WorldbookRuntime.EnsureCreated();
                    AwakeRuleRegistry.EnsureLoaded();
                    NpcProactiveService.SetCurrent(new NpcProactiveService());
                    break;
                case ExtensionLifecycleStage.SessionEnding:
                    try
                    {
                        DialogueOverlayLifecycle.CloseAll?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        AwakeLog.Write("dialogue_overlay_session_end_close_error error=" + ex.Message);
                    }
                    AwakeDialogueSessionCoordinator.CloseAll();
                    Task<WorldFinalDrainResult> worldDrain = AwakeRuntime.BeginSessionEnd();
                    NpcMemoryService memoryServiceForDrain = NpcMemoryService.Current;
                    Task<AwakeSessionDrainResult> ownDrain = AwakeBackgroundTask.Run<AwakeSessionDrainResult>(
                        () => DrainAwakeSessionAsync(worldDrain, memoryServiceForDrain),
                        "awake_session_drain");
                    lock (LifecycleSync) sessionDrainTask = ownDrain;
                    KnowledgeRuntime.ShutdownCurrent();
                    WorldbookRuntime.ShutdownCurrent();
                    NpcProactiveService.ShutdownCurrent();
                    break;
                case ExtensionLifecycleStage.Unregistered:
                    try
                    {
                        DialogueOverlayLifecycle.CloseAll?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        AwakeLog.Write("dialogue_overlay_unregistered_close_error error=" + ex.Message);
                    }
                    AwakeDialogueSessionCoordinator.CloseAll();
                    NpcMemoryService.ShutdownCurrent();
                    CampaignResetLifecycle.Reset = null;
                    KnowledgeRuntime.ShutdownCurrent();
                    WorldbookRuntime.ShutdownCurrent();
                    NpcProactiveService.ShutdownCurrent();
                    break;
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_lifecycle_failed stage=" + stage + " error=" + ex.Message);
        }
    }

    private static async Task RestoreCampaignStateAsync(int sessionGeneration)
    {
        CancellationToken cancellationToken = AwakeRuntime.SessionCancellationToken;
        if (!AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration)) return;
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null || !AwakeRuntime.IsCurrentSession(sessionGeneration, store))
        {
            AwakeLog.Write("awake_campaign_state_restore_skipped reason=storage_not_ready");
            return;
        }
        FormalWeeklyReportResult formal = await WorldEventServices.EnsureFormalReportsReadyAsync(
            AwakeRuntime.CurrentGameDay(),
            cancellationToken).ConfigureAwait(false);
        if (!AwakeRuntime.IsCurrentSession(sessionGeneration, store)) return;
        AwakeLog.Write("awake_campaign_formal_report status=" + formal.Status + " code=" + formal.Code);
        await AwakeOnboardingService.LoadFromStoreAsync(sessionGeneration, store, cancellationToken).ConfigureAwait(false);
        if (!AwakeRuntime.IsCurrentSession(sessionGeneration, store)) return;
        await EventDialogueQueue.LoadFromStoreAsync(cancellationToken).ConfigureAwait(false);
        if (!AwakeRuntime.IsCurrentSession(sessionGeneration, store)) return;
        AwakeLog.Write("awake_campaign_state_restore_completed generation=" + sessionGeneration);
    }

    private static async Task<AwakeSessionDrainResult> DrainAwakeSessionAsync(
        Task<WorldFinalDrainResult> worldDrain,
        NpcMemoryService memoryService)
    {
        WorldFinalDrainResult worldResult;
        try
        {
            worldResult = await (worldDrain ?? Task.FromResult(WorldFinalDrainResult.NotStarted())).ConfigureAwait(false);
            AwakeLog.Write("world_state_final_drain_observed succeeded=" + (worldResult?.Succeeded == true)
                + " pending_writes=" + (worldResult?.PendingWrites ?? 0)
                + " pending_events=" + (worldResult?.PendingEvents ?? 0)
                + " dropped=" + (worldResult?.DroppedItems ?? 0)
                + " code=" + (worldResult?.ErrorCode ?? "awake.world_state.final_drain_error"));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_final_drain_observe_error error=" + ex.Message);
            worldResult = new WorldFinalDrainResult(false, 0, 0, 0, "awake.world_state.final_drain_error");
        }

        bool memorySucceeded = true;
        try
        {
            if (memoryService != null) await memoryService.DrainBackgroundAsync(5000).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            memorySucceeded = false;
            AwakeLog.Write("npc_memory_drain_background_error error=" + ex.Message);
        }

        if (!worldResult.Succeeded || !memorySucceeded)
        {
            return new AwakeSessionDrainResult(false, worldResult.ErrorCode ?? "awake.session.drain_failed");
        }

        NpcMemoryService.ShutdownCurrent();
        return AwakeSessionDrainResult.Success();
    }

    internal static Task<OperationResult<string>> HandleEchoAsync(
        string payloadJson,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(OperationResult<string>.Failed(FrameworkErrors.Create(
                "awake.cancelled",
                FrameworkErrorCategory.Cancelled,
                "The probe request was cancelled.",
                context?.CorrelationId,
                owner: Owner.Value)));
        }

        if (context == null || context.IsExpired)
        {
            return Task.FromResult(OperationResult<string>.Failed(FrameworkErrors.Create(
                "awake.context_expired",
                FrameworkErrorCategory.Expired,
                "The probe request context expired.",
                context?.CorrelationId,
                owner: Owner.Value)));
        }

        bool validObject = false;
        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            try
            {
                validObject = JToken.Parse(payloadJson) is JObject;
            }
            catch
            {
                validObject = false;
            }
        }
        if (!validObject)
        {
            return Task.FromResult(OperationResult<string>.Failed(FrameworkErrors.Create(
                "awake.invalid_payload",
                FrameworkErrorCategory.InvalidRequest,
                "The probe payload must be a JSON object.",
                context.CorrelationId,
                owner: Owner.Value)));
        }

        return Task.FromResult(OperationResult<string>.Succeeded(
            "{\"source\":\"AWAKE\",\"echo\":true,\"correlation\":\"" + context.CorrelationId + "\"}"));
    }
}

internal sealed class ProbeContextProvider : IContextProvider
{
    public ExtensionId Owner { get; } = new ExtensionId("AWAKE");
    public string ProviderId { get; } = "awake.probe.context";

    public Task<OperationResult<IReadOnlyList<ContextContribution>>> ContributeAsync(
        ContextPlanRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(OperationResult<IReadOnlyList<ContextContribution>>.Failed(
                FrameworkErrors.Create(
                    "awake.cancelled",
                    FrameworkErrorCategory.Cancelled,
                    "The context provider request was cancelled.",
                    context?.CorrelationId,
                    owner: Owner.Value)));
        }

        if (context == null || context.IsExpired)
        {
            return Task.FromResult(OperationResult<IReadOnlyList<ContextContribution>>.Failed(
                FrameworkErrors.Create(
                    "awake.context_expired",
                    FrameworkErrorCategory.Expired,
                    "The context provider request expired.",
                    context?.CorrelationId,
                    owner: Owner.Value)));
        }

        ContextContribution contribution = new ContextContribution(
            "awake.probe.context.1",
            ProviderId,
            ContextContentType.Structured,
            "ExtensionProvider",
            "PlayerKnown",
            "fact",
            Array.Empty<EntityRef>(),
            DateTimeOffset.UtcNow,
            string.Empty,
            1,
            0,
            null,
            "local-only",
            "{}");

        return Task.FromResult(OperationResult<IReadOnlyList<ContextContribution>>.Succeeded(
            new List<ContextContribution> { contribution }));
    }
}
