using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api
{
    public enum HostState
    {
        Created,
        Registered,
        Ready,
        Closing,
        Drained,
        RecoveryRequired
    }

    public interface IMarcusAwakeFrameworkHost
    {
        FrameworkIdentity Identity { get; }
        HostState State { get; }
        ICapabilityBroker Capabilities { get; }
        IGameDataService GameData { get; }
        IContextPlanner Context { get; }
        ICommandService Commands { get; }
        ISessionCoordinator Sessions { get; }
        ISaveAnchorStore SaveAnchors { get; }
        IDiagnosticsService Diagnostics { get; }
        IRuntimeServicePort Runtime { get; }
    }

    public sealed class FrameworkHost : IMarcusAwakeFrameworkHost, IMarcusAiFrameworkHost
    {
        private readonly object sync = new object();
        private readonly ContextServiceAdapter compatibilityContext;
        private readonly IToolCandidateService tools;
        private readonly IRagService rag;
        private readonly IEventService events;
        private readonly IAiGateway ai;
        private readonly IAiModelService models;
        private readonly IMediaService media;
        private readonly IPromptRegistry prompts;
        private readonly IStorageService storage;
        private readonly IAssetService assets;
        private readonly IPermissionService permissions;
        private readonly ILoggingService log;
        private readonly Dictionary<string, ExtensionManifest> extensions = new Dictionary<string, ExtensionManifest>(StringComparer.Ordinal);
        private readonly Dictionary<string, CapabilityHandler> capabilityHandlers = new Dictionary<string, CapabilityHandler>(StringComparer.Ordinal);
        private HostState state;

        public FrameworkHost(FrameworkIdentity identity, ICapabilityBroker capabilities, IGameDataService gameData, IContextPlanner context, ICommandService commands, ISessionCoordinator sessions, ISaveAnchorStore saveAnchors, IDiagnosticsService diagnostics)
            : this(identity, capabilities, gameData, context, commands, sessions, saveAnchors, diagnostics, null, null)
        {
        }

        public FrameworkHost(FrameworkIdentity identity, ICapabilityBroker capabilities, IGameDataService gameData, IContextPlanner context, ICommandService commands, ISessionCoordinator sessions, ISaveAnchorStore saveAnchors, IDiagnosticsService diagnostics, IRuntimeServicePort runtime)
            : this(identity, capabilities, gameData, context, commands, sessions, saveAnchors, diagnostics, runtime, null)
        {
        }

        public FrameworkHost(FrameworkIdentity identity, ICapabilityBroker capabilities, IGameDataService gameData, IContextPlanner context, ICommandService commands, ISessionCoordinator sessions, ISaveAnchorStore saveAnchors, IDiagnosticsService diagnostics, IRuntimeServicePort runtime, FrameworkServiceOverrides overrides)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
            IGameDataService resolvedGameData = overrides != null && overrides.GameData != null
                ? new ProviderBackedGameDataService(overrides.GameData)
                : gameData;
            GameData = resolvedGameData ?? throw new ArgumentNullException(nameof(gameData));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
            Sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            SaveAnchors = saveAnchors ?? throw new ArgumentNullException(nameof(saveAnchors));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            Runtime = runtime ?? new StrictUnavailableRuntimeServicePort();
            compatibilityContext = new ContextServiceAdapter(Context);
            tools = new HostToolCandidateService();
            rag = overrides?.Rag ?? new UnavailableRagService();
            events = new InMemoryEventService();
            ai = runtime as IAiGateway ?? new UnavailableAiGateway();
            models = new UnavailableAiModelService();
            media = runtime as IMediaService ?? new UnavailableMediaService();
            prompts = overrides?.Prompts ?? new UnavailablePromptRegistry();
            storage = overrides?.Storage ?? new UnavailableStorageService();
            // Assets live in the Runtime Service's content-addressed store, so the runtime port is
            // the only asset implementation the host can offer. A runtime that does not speak the
            // asset surface keeps the typed unavailable shell instead of failing at call time.
            assets = runtime as IAssetService ?? new UnavailableAssetService();
            permissions = overrides?.Permissions ?? new UnavailablePermissionService();
            log = new UnavailableLoggingService();
            state = HostState.Created;
        }

        public static FrameworkHost CreateDefaultHost()
        {
            return CreateDefaultHost(null, "1.3.15");
        }

        public static FrameworkHost CreateDefaultHost(IRuntimeServicePort runtime, string bannerlordApi = "1.3.15")
        {
            return CreateDefaultHost(runtime, null, bannerlordApi);
        }

        public static FrameworkHost CreateDefaultHost(IRuntimeServicePort runtime, FrameworkServiceOverrides overrides, string bannerlordApi = "1.3.15")
        {
            FrameworkIdentity identity = FrameworkIdentity.Current(bannerlordApi);
            SessionCoordinator sessions = new SessionCoordinator(() => DateTimeOffset.UtcNow);
            HostCapabilityBroker capabilities = new HostCapabilityBroker();
            FrameworkHost host = null;
            HostDiagnosticsService diagnostics = new HostDiagnosticsService(
                identity,
                sessions,
                capabilities,
                () => host == null ? new ExtensionManifest[0] : host.GetExtensionsSnapshot(),
                () => host == null ? HostState.Created : host.State,
                () => DateTimeOffset.UtcNow);
            host = new FrameworkHost(
                identity,
                capabilities,
                new UnavailableGameDataService(),
                new ContextPlanner(),
                new HostCommandService(),
                sessions,
                new UnavailableSaveAnchorStore(),
                diagnostics,
                runtime,
                overrides);
            return host;
        }

        public FrameworkIdentity Identity { get; }
        public HostState State { get { lock (sync) return state; } }
        public ICapabilityBroker Capabilities { get; }
        public IGameDataService GameData { get; }
        public IContextPlanner Context { get; }
        public ICommandService Commands { get; }
        public ISessionCoordinator Sessions { get; }
        public ISaveAnchorStore SaveAnchors { get; }
        public IDiagnosticsService Diagnostics { get; }
        public IRuntimeServicePort Runtime { get; }
        public SessionRef CurrentSession
        {
            get
            {
                SessionLease current = Sessions.Current;
                return current == null ? null : current.Reference;
            }
        }
        public IToolCandidateService Tools => tools;
        public IRagService Rag => rag;
        public IEventService Events => events;
        public IAiGateway Ai => ai;
        public IAiModelService Models => models;
        public IMediaService Media => media;
        public IPromptRegistry Prompts => prompts;
        public IStorageService Storage => storage;
        public IAssetService Assets => assets;
        public IPermissionService Permissions => permissions;
        public ILoggingService Log => log;
        IContextService IMarcusAiFrameworkHost.Context => compatibilityContext;

        public OperationResult<bool> Register()
        {
            lock (sync)
            {
                if (state != HostState.Created) return Failure("host.invalid_register", "The host is already registered.", "host-register");
                state = HostState.Registered;
                return OperationResult<bool>.Succeeded(true);
            }
        }

        public OperationResult<bool> Ready()
        {
            lock (sync)
            {
                if (state != HostState.Registered) return Failure("host.invalid_ready", "The host is not registered.", "host-ready");
                state = HostState.Ready;
                return OperationResult<bool>.Succeeded(true);
            }
        }

        public OperationResult<bool> BeginClosing()
        {
            lock (sync)
            {
                if (state == HostState.Drained) return OperationResult<bool>.Succeeded(true);
                if (state != HostState.Ready) return Failure("host.invalid_close", "The host is not ready.", "host-close");
                state = HostState.Closing;
                return OperationResult<bool>.Succeeded(true);
            }
        }

        public OperationResult<bool> CompleteDrain()
        {
            lock (sync)
            {
                if (state != HostState.Closing) return Failure("host.invalid_drain", "The host is not closing.", "host-drain");
                SessionLease lease = Sessions.Current;
                if (lease != null && lease.State != SessionState.Drained) return Failure("host.session_drain_incomplete", "The session has not finished draining.", "host-drain");
                state = HostState.Drained;
                return OperationResult<bool>.Succeeded(true);
            }
        }

        internal OperationResult<bool> RegisterExtension(IFrameworkExtension extension)
        {
            if (extension == null) return Failure("extension.null", "An extension is required.", "extension-register");
            ExtensionManifest manifest;
            try
            {
                manifest = extension.Manifest;
            }
            catch
            {
                return Failure("extension.manifest_failed", "The extension manifest could not be read.", "extension-register");
            }
            if (manifest == null || manifest.ExtensionId == null) return Failure("extension.manifest_missing", "The extension manifest is missing.", "extension-register");

            lock (sync)
            {
                if (state != HostState.Registered && state != HostState.Ready) return Failure("host.extension_registration_invalid", "Extensions can only register on a registered or ready host.", "extension-register");
                if (extensions.ContainsKey(manifest.ExtensionId.Value)) return Failure("extension.duplicate", "The extension is already registered.", manifest.ExtensionId.Value);
                HostExtensionRegistration registration = new HostExtensionRegistration(this, manifest);
                try
                {
                    extension.Register(registration);
                }
                catch
                {
                    registration.Rollback();
                    return Failure("extension.register_failed", "The extension registration failed.", manifest.ExtensionId.Value);
                }
                extensions.Add(manifest.ExtensionId.Value, manifest);
                return OperationResult<bool>.Succeeded(true);
            }
        }

        internal IReadOnlyList<ExtensionManifest> GetExtensionsSnapshot()
        {
            lock (sync) return new List<ExtensionManifest>(extensions.Values).AsReadOnly();
        }

        internal OperationResult<bool> RegisterCapability(ExtensionManifest manifest, CapabilityDescriptor descriptor, CapabilityHandler handler)
        {
            if (manifest == null || descriptor == null || handler == null) return Failure("capability.invalid_registration", "Capability registration is invalid.", "capability-register");
            if (!OwnersMatch(manifest.ExtensionId, descriptor.Owner)) return Failure("capability.owner_mismatch", "Capability owner does not match the extension.", manifest.ExtensionId.Value);
            OperationResult<CapabilityDescriptor> registered = Capabilities.Register(manifest, descriptor);
            if (!registered.IsSuccess) return OperationResult<bool>.Failed(registered.Error);
            lock (sync) capabilityHandlers[descriptor.Id.Value] = handler;
            return OperationResult<bool>.Succeeded(true);
        }

        internal OperationResult<bool> RegisterCommand(ExtensionManifest manifest, CommandDescriptor descriptor, ICommandAdapter adapter)
        {
            if (manifest == null || descriptor == null || adapter == null) return Failure("command.invalid_registration", "Command registration is invalid.", "command-register");
            if (!OwnersMatch(manifest.ExtensionId, descriptor.Owner)) return Failure("command.owner_mismatch", "Command owner does not match the extension.", manifest.ExtensionId.Value);
            HostCommandService commandService = Commands as HostCommandService;
            if (commandService == null) return Failure("command.registry_unavailable", "The embedded command registry is not available.", manifest.ExtensionId.Value);
            return commandService.Register(descriptor, adapter);
        }

        internal OperationResult<bool> RegisterTool(ExtensionManifest manifest, ToolDescriptor descriptor)
        {
            if (manifest == null || descriptor == null) return Failure("tool.invalid_registration", "Tool registration is invalid.", "tool-register");
            if (!OwnersMatch(manifest.ExtensionId, descriptor.Owner)) return Failure("tool.owner_mismatch", "Tool owner does not match the extension.", manifest.ExtensionId.Value);
            return tools.Register(descriptor);
        }

        internal OperationResult<bool> RegisterContextProvider(ExtensionManifest manifest, IContextProvider provider)
        {
            if (manifest == null || provider == null) return Failure("context.invalid_registration", "Context provider registration is invalid.", "context-register");
            if (!OwnersMatch(manifest.ExtensionId, provider.Owner)) return Failure("context.owner_mismatch", "Context provider owner does not match the extension.", manifest.ExtensionId.Value);
            return compatibilityContext.RegisterProvider(provider);
        }

        internal void RemoveCapability(string capabilityId, ExtensionId owner)
        {
            lock (sync) capabilityHandlers.Remove(capabilityId ?? string.Empty);
            HostCapabilityBroker broker = Capabilities as HostCapabilityBroker;
            if (broker != null) broker.Remove(capabilityId, owner);
        }

        internal void RemoveCommand(string commandId, ExtensionId owner)
        {
            HostCommandService commandService = Commands as HostCommandService;
            if (commandService != null) commandService.Remove(commandId, owner);
        }

        internal void RemoveTool(string toolId, ExtensionId owner)
        {
            HostToolCandidateService toolService = tools as HostToolCandidateService;
            if (toolService != null) toolService.Remove(toolId, owner);
        }

        internal void RemoveContextProvider(string providerId, ExtensionId owner)
        {
            compatibilityContext.RemoveProvider(providerId, owner);
        }

        internal OperationResult<bool> AbortRegistration()
        {
            lock (sync)
            {
                if (state == HostState.Created) return OperationResult<bool>.Succeeded(true);
                if (state != HostState.Registered && state != HostState.Ready) return Failure("host.abort_invalid", "Only a partially registered host can be aborted.", "host-abort");
                SessionLease lease = Sessions.Current;
                if (lease != null && lease.State != SessionState.Drained) return Failure("host.session_active", "The host still owns an active session.", "host-abort");
                state = HostState.Created;
                return OperationResult<bool>.Succeeded(true);
            }
        }

        private static bool OwnersMatch(ExtensionId expected, ExtensionId actual)
        {
            return expected != null && actual != null && StringComparer.Ordinal.Equals(expected.Value, actual.Value);
        }

        private static OperationResult<bool> Failure(string code, string fallback, string correlationId)
        {
            return OperationResult<bool>.Failed(FrameworkErrors.Create(code, FrameworkErrorCategory.Conflict, fallback, correlationId));
        }
    }

    internal static class HostUnavailable
    {
        internal static OperationResult<T> Failure<T>(string service, string operation, RequestContext context)
        {
            return OperationResult<T>.Failed(FrameworkErrors.Create(
                "host." + service + ".unavailable",
                FrameworkErrorCategory.Unavailable,
                "The " + service + " service is not available.",
                context?.CorrelationId ?? "host-" + operation,
                retryable: true));
        }

        internal static Task<OperationResult<T>> TaskFailure<T>(string service, string operation, RequestContext context)
        {
            return Task.FromResult(Failure<T>(service, operation, context));
        }

        internal static OperationResult<T> Invalid<T>(string service, string operation, RequestContext context)
        {
            return OperationResult<T>.Failed(FrameworkErrors.Create(
                "host." + service + ".invalid_request",
                FrameworkErrorCategory.InvalidRequest,
                "The " + service + " request is invalid.",
                context?.CorrelationId ?? "host-" + operation));
        }
    }

    internal sealed class HostExtensionRegistration : IExtensionRegistration
    {
        private readonly FrameworkHost host;
        private readonly ExtensionManifest manifest;
        private readonly List<string> capabilities = new List<string>();
        private readonly List<string> commands = new List<string>();
        private readonly List<string> tools = new List<string>();
        private readonly List<string> providers = new List<string>();

        internal HostExtensionRegistration(FrameworkHost host, ExtensionManifest manifest)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        }

        public ExtensionId Owner => manifest.ExtensionId;

        public OperationResult<bool> RegisterCapability(CapabilityDescriptor descriptor, CapabilityHandler handler)
        {
            OperationResult<bool> result = host.RegisterCapability(manifest, descriptor, handler);
            if (result.IsSuccess && descriptor != null) capabilities.Add(descriptor.Id.Value);
            return result;
        }

        public OperationResult<bool> RegisterCommand(CommandDescriptor descriptor, ICommandAdapter adapter)
        {
            OperationResult<bool> result = host.RegisterCommand(manifest, descriptor, adapter);
            if (result.IsSuccess && descriptor != null) commands.Add(descriptor.CommandId);
            return result;
        }

        public OperationResult<bool> RegisterTool(ToolDescriptor descriptor)
        {
            OperationResult<bool> result = host.RegisterTool(manifest, descriptor);
            if (result.IsSuccess && descriptor != null) tools.Add(descriptor.ToolId);
            return result;
        }

        public OperationResult<bool> RegisterContextProvider(IContextProvider provider)
        {
            OperationResult<bool> result = host.RegisterContextProvider(manifest, provider);
            if (result.IsSuccess && provider != null) providers.Add(provider.ProviderId);
            return result;
        }

        internal void Rollback()
        {
            for (int index = providers.Count - 1; index >= 0; index--) host.RemoveContextProvider(providers[index], Owner);
            for (int index = commands.Count - 1; index >= 0; index--) host.RemoveCommand(commands[index], Owner);
            for (int index = tools.Count - 1; index >= 0; index--) host.RemoveTool(tools[index], Owner);
            for (int index = capabilities.Count - 1; index >= 0; index--) host.RemoveCapability(capabilities[index], Owner);
        }
    }

    internal sealed class HostCapabilityBroker : ICapabilityBroker
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, CapabilityDescriptor> descriptors = new Dictionary<string, CapabilityDescriptor>(StringComparer.Ordinal);
        private readonly List<string> order = new List<string>();

        public OperationResult<CapabilityDescriptor> Register(ExtensionManifest manifest, CapabilityDescriptor descriptor)
        {
            string correlation = manifest?.ExtensionId?.Value ?? "capability-register";
            if (manifest == null || descriptor == null) return OperationResult<CapabilityDescriptor>.Failed(FrameworkErrors.Create("capability.invalid_manifest", FrameworkErrorCategory.InvalidRequest, "Capability registration is invalid.", correlation));
            if (!StringComparer.Ordinal.Equals(manifest.ExtensionId.Value, descriptor.Owner.Value)) return OperationResult<CapabilityDescriptor>.Failed(FrameworkErrors.Create("capability.owner_mismatch", FrameworkErrorCategory.Denied, "Capability owner does not match the manifest.", correlation));
            lock (sync)
            {
                CapabilityDescriptor existing;
                if (descriptors.TryGetValue(descriptor.Id.Value, out existing))
                {
                    if (StringComparer.Ordinal.Equals(existing.Owner.Value, descriptor.Owner.Value) && existing.Version.Equals(descriptor.Version)) return OperationResult<CapabilityDescriptor>.Succeeded(existing);
                    return OperationResult<CapabilityDescriptor>.Failed(FrameworkErrors.Create("capability.conflict", FrameworkErrorCategory.Conflict, "Capability is already owned by another version or extension.", correlation));
                }
                descriptors.Add(descriptor.Id.Value, descriptor);
                order.Add(descriptor.Id.Value);
                return OperationResult<CapabilityDescriptor>.Succeeded(descriptor);
            }
        }

        public OperationResult<CapabilityDescriptor> Resolve(CapabilityId id)
        {
            if (id == null) return OperationResult<CapabilityDescriptor>.Failed(FrameworkErrors.Create("capability.unavailable", FrameworkErrorCategory.Unavailable, "Capability is unavailable.", "capability-resolve", retryable: true));
            lock (sync)
            {
                CapabilityDescriptor descriptor;
                if (!descriptors.TryGetValue(id.Value, out descriptor)) return OperationResult<CapabilityDescriptor>.Failed(FrameworkErrors.Create("capability.unavailable", FrameworkErrorCategory.Unavailable, "Capability is unavailable.", "capability-resolve", retryable: true));
                return OperationResult<CapabilityDescriptor>.Succeeded(descriptor);
            }
        }

        public IReadOnlyList<CapabilityDescriptor> Discover()
        {
            lock (sync)
            {
                List<CapabilityDescriptor> snapshot = new List<CapabilityDescriptor>();
                for (int index = 0; index < order.Count; index++) snapshot.Add(descriptors[order[index]]);
                return snapshot.AsReadOnly();
            }
        }

        internal void Remove(string capabilityId, ExtensionId owner)
        {
            if (string.IsNullOrWhiteSpace(capabilityId) || owner == null) return;
            lock (sync)
            {
                CapabilityDescriptor descriptor;
                if (!descriptors.TryGetValue(capabilityId, out descriptor) || !StringComparer.Ordinal.Equals(descriptor.Owner.Value, owner.Value)) return;
                descriptors.Remove(capabilityId);
                order.Remove(capabilityId);
            }
        }
    }

    internal sealed class ContextServiceAdapter : IContextService
    {
        private readonly object sync = new object();
        private readonly IContextPlanner planner;
        private readonly Dictionary<string, IContextProvider> providers = new Dictionary<string, IContextProvider>(StringComparer.Ordinal);
        private readonly List<string> order = new List<string>();

        internal ContextServiceAdapter(IContextPlanner planner)
        {
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
        }

        public IReadOnlyList<string> DiscoverProviders()
        {
            lock (sync) return new List<string>(order).AsReadOnly();
        }

        internal OperationResult<bool> RegisterProvider(IContextProvider provider)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.ProviderId) || provider.Owner == null) return HostUnavailable.Invalid<bool>("context", "register", null);
            lock (sync)
            {
                IContextProvider existing;
                if (providers.TryGetValue(provider.ProviderId, out existing))
                {
                    if (StringComparer.Ordinal.Equals(existing.Owner.Value, provider.Owner.Value)) return OperationResult<bool>.Succeeded(true);
                    return OperationResult<bool>.Failed(FrameworkErrors.Create("context.provider_conflict", FrameworkErrorCategory.Conflict, "The context provider ID is already registered by another extension.", provider.ProviderId));
                }
                providers.Add(provider.ProviderId, provider);
                order.Add(provider.ProviderId);
                return OperationResult<bool>.Succeeded(true);
            }
        }

        internal void RemoveProvider(string providerId, ExtensionId owner)
        {
            if (string.IsNullOrWhiteSpace(providerId) || owner == null) return;
            lock (sync)
            {
                IContextProvider provider;
                if (!providers.TryGetValue(providerId, out provider) || !StringComparer.Ordinal.Equals(provider.Owner.Value, owner.Value)) return;
                providers.Remove(providerId);
                order.Remove(providerId);
            }
        }

        public async Task<OperationResult<ContextPlan>> PlanAsync(ContextPlanRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            if (request == null || context == null) return HostUnavailable.Invalid<ContextPlan>("context", "plan", context);
            if (cancellationToken.IsCancellationRequested) return Cancelled<ContextPlan>(context);

            List<string> selected = new List<string>();
            AddUnique(selected, request.ProviderIds);
            AddUnique(selected, request.RequiredProviderIds);
            if (selected.Count == 0)
            {
                lock (sync) selected.AddRange(order);
            }

            HashSet<string> required = new HashSet<string>(StringComparer.Ordinal);
            AddUnique(required, request.RequiredProviderIds);
            List<ContextContribution> contributions = new List<ContextContribution>();
            List<ContextExclusion> exclusions = new List<ContextExclusion>();
            for (int index = 0; index < selected.Count; index++)
            {
                string providerId = selected[index];
                IContextProvider provider;
                lock (sync) providers.TryGetValue(providerId, out provider);
                if (provider == null)
                {
                    if (required.Contains(providerId)) return ProviderFailure<ContextPlan>(providerId, "The required context provider is unavailable.", context);
                    exclusions.Add(new ContextExclusion(providerId, "context.provider_unavailable"));
                    continue;
                }

                OperationResult<IReadOnlyList<ContextContribution>> contributed;
                try
                {
                    contributed = await provider.ContributeAsync(request, context, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return Cancelled<ContextPlan>(context);
                }
                catch
                {
                    if (required.Contains(providerId)) return ProviderFailure<ContextPlan>(providerId, "The required context provider failed.", context);
                    exclusions.Add(new ContextExclusion(providerId, "context.provider_failed"));
                    continue;
                }

                if (!contributed.IsSuccess || contributed.Value == null)
                {
                    if (required.Contains(providerId)) return ProviderFailure<ContextPlan>(providerId, "The required context provider failed.", context);
                    exclusions.Add(new ContextExclusion(providerId, "context.provider_failed"));
                    continue;
                }
                for (int contributionIndex = 0; contributionIndex < contributed.Value.Count; contributionIndex++)
                {
                    ContextContribution contribution = contributed.Value[contributionIndex];
                    if (contribution == null) continue;
                    if (!Allows(request.AllowedAccessScopes, contribution.AccessScope, contribution.SourceClass))
                    {
                        exclusions.Add(new ContextExclusion(contribution.SourceId, "access_scope_denied"));
                        continue;
                    }
                    if (!Allows(request.AllowedCloudExportClassifications, contribution.CloudExportClassification, null))
                    {
                        exclusions.Add(new ContextExclusion(contribution.SourceId, "cloud_export_denied"));
                        continue;
                    }
                    contributions.Add(contribution);
                }
            }

            OperationResult<ContextPlan> planned = planner.Plan(context, ResolveVisibility(request.AllowedAccessScopes), contributions, request.MaximumTokens);
            if (!planned.IsSuccess || planned.Value == null) return planned;
            for (int index = 0; index < planned.Value.Excluded.Count; index++) exclusions.Add(planned.Value.Excluded[index]);
            return OperationResult<ContextPlan>.Succeeded(new ContextPlan(planned.Value.Included, exclusions.AsReadOnly(), planned.Value.TokenCost));
        }

        private static void AddUnique(List<string> target, IReadOnlyList<string> values)
        {
            if (values == null) return;
            for (int index = 0; index < values.Count; index++)
            {
                string value = values[index];
                if (string.IsNullOrWhiteSpace(value) || target.Contains(value)) continue;
                target.Add(value);
            }
        }

        private static void AddUnique(HashSet<string> target, IReadOnlyList<string> values)
        {
            if (values == null) return;
            for (int index = 0; index < values.Count; index++)
            {
                string value = values[index];
                if (!string.IsNullOrWhiteSpace(value)) target.Add(value);
            }
        }

        private static bool Allows(IReadOnlyList<string> allowed, string primary, string secondary)
        {
            if (allowed == null || allowed.Count == 0) return true;
            for (int index = 0; index < allowed.Count; index++)
            {
                string value = allowed[index];
                if (StringComparer.Ordinal.Equals(value, primary) || StringComparer.Ordinal.Equals(value, secondary)) return true;
            }
            return false;
        }

        private static VisibilityScope ResolveVisibility(IReadOnlyList<string> scopes)
        {
            if (Contains(scopes, "FullSimulation")) return new VisibilityScope(VisibilityLevel.FullSimulation);
            if (Contains(scopes, "ObservedHistory")) return new VisibilityScope(VisibilityLevel.ObservedHistory);
            if (Contains(scopes, "PublicCatalog")) return new VisibilityScope(VisibilityLevel.PublicCatalog);
            return new VisibilityScope(VisibilityLevel.PlayerKnown);
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            if (values == null) return false;
            for (int index = 0; index < values.Count; index++) if (StringComparer.Ordinal.Equals(values[index], value)) return true;
            return false;
        }

        private static OperationResult<T> ProviderFailure<T>(string providerId, string fallback, RequestContext context)
        {
            return OperationResult<T>.Failed(FrameworkErrors.Create("context.provider_unavailable", FrameworkErrorCategory.Unavailable, fallback, context?.CorrelationId ?? "context-plan", retryable: true, details: new Dictionary<string, string> { { "providerId", providerId ?? string.Empty } }));
        }

        private static OperationResult<T> Cancelled<T>(RequestContext context)
        {
            return OperationResult<T>.Failed(FrameworkErrors.Create("context.cancelled", FrameworkErrorCategory.Cancelled, "The context plan was cancelled.", context?.CorrelationId ?? "context-plan"));
        }
    }

    internal sealed class HostToolCandidateService : IToolCandidateService
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, ToolDescriptor> descriptors = new Dictionary<string, ToolDescriptor>(StringComparer.Ordinal);
        private readonly List<string> order = new List<string>();

        public OperationResult<bool> Register(ToolDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.ToolId)) return HostUnavailable.Invalid<bool>("tool", "register", null);
            lock (sync)
            {
                ToolDescriptor existing;
                if (descriptors.TryGetValue(descriptor.QualifiedId, out existing))
                {
                    if (StringComparer.Ordinal.Equals(existing.Owner.Value, descriptor.Owner.Value)) return OperationResult<bool>.Succeeded(true);
                    return OperationResult<bool>.Failed(FrameworkErrors.Create("tool.conflict", FrameworkErrorCategory.Conflict, "The tool is already registered by another extension.", descriptor.QualifiedId));
                }
                descriptors.Add(descriptor.QualifiedId, descriptor);
                order.Add(descriptor.QualifiedId);
                return OperationResult<bool>.Succeeded(true);
            }
        }

        public IReadOnlyList<ToolDescriptor> Describe()
        {
            lock (sync)
            {
                List<ToolDescriptor> snapshot = new List<ToolDescriptor>();
                for (int index = 0; index < order.Count; index++) snapshot.Add(descriptors[order[index]]);
                return snapshot.AsReadOnly();
            }
        }

        public OperationResult<ToolCandidateValidationResult> Validate(ToolCandidate candidate, IReadOnlyList<string> allowedToolIds, RequestContext context, int currentTurn, int maximumCandidates = 8)
        {
            if (candidate == null || context == null || maximumCandidates < 1) return HostUnavailable.Invalid<ToolCandidateValidationResult>("tool", "validate", context);
            if (candidate.SourceTurn != currentTurn) return OperationResult<ToolCandidateValidationResult>.Failed(FrameworkErrors.Create("tool.turn_mismatch", FrameworkErrorCategory.Expired, "The tool candidate belongs to another turn.", context.CorrelationId));
            ToolDescriptor descriptor = null;
            lock (sync)
            {
                if (!descriptors.TryGetValue(candidate.ToolId, out descriptor))
                {
                    foreach (string qualifiedId in order)
                    {
                        if (StringComparer.Ordinal.Equals(descriptors[qualifiedId].ToolId, candidate.ToolId))
                        {
                            descriptor = descriptors[qualifiedId];
                            break;
                        }
                    }
                }
            }
            if (descriptor == null) return OperationResult<ToolCandidateValidationResult>.Failed(FrameworkErrors.Create("tool.unavailable", FrameworkErrorCategory.Unavailable, "The requested tool is unavailable.", context.CorrelationId, retryable: true));
            if (!Allows(allowedToolIds, descriptor.ToolId, descriptor.QualifiedId)) return OperationResult<ToolCandidateValidationResult>.Failed(FrameworkErrors.Create("tool.not_allowed", FrameworkErrorCategory.Denied, "The requested tool is not in the current allowlist.", context.CorrelationId));
            return OperationResult<ToolCandidateValidationResult>.Succeeded(new ToolCandidateValidationResult(true, string.Empty, descriptor, descriptor.CommandRisk >= CommandRiskTier.R2Gameplay));
        }

        internal void Remove(string toolId, ExtensionId owner)
        {
            if (string.IsNullOrWhiteSpace(toolId) || owner == null) return;
            lock (sync)
            {
                List<string> remove = new List<string>();
                foreach (string qualifiedId in order)
                {
                    ToolDescriptor descriptor = descriptors[qualifiedId];
                    if (StringComparer.Ordinal.Equals(descriptor.ToolId, toolId) && StringComparer.Ordinal.Equals(descriptor.Owner.Value, owner.Value)) remove.Add(qualifiedId);
                }
                for (int index = 0; index < remove.Count; index++)
                {
                    descriptors.Remove(remove[index]);
                    order.Remove(remove[index]);
                }
            }
        }

        private static bool Allows(IReadOnlyList<string> allowed, string toolId, string qualifiedId)
        {
            if (allowed == null || allowed.Count == 0) return false;
            for (int index = 0; index < allowed.Count; index++) if (StringComparer.Ordinal.Equals(allowed[index], toolId) || StringComparer.Ordinal.Equals(allowed[index], qualifiedId)) return true;
            return false;
        }
    }

    internal sealed class HostCommandService : ICommandService, ICompatibilityCommandService
    {
        private sealed class Registration
        {
            internal Registration(CommandDescriptor descriptor, ICommandAdapter adapter)
            {
                Descriptor = descriptor;
                Adapter = adapter;
            }
            internal CommandDescriptor Descriptor { get; }
            internal ICommandAdapter Adapter { get; }
        }

        private readonly object sync = new object();
        private readonly Dictionary<string, Registration> registrations = new Dictionary<string, Registration>(StringComparer.Ordinal);
        private readonly Dictionary<string, CommandLedgerEntry> entries = new Dictionary<string, CommandLedgerEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> entriesByDeduplication = new Dictionary<string, string>(StringComparer.Ordinal);
        private long sequence;

        public OperationResult<bool> Register(CommandDescriptor descriptor, ICommandAdapter adapter)
        {
            if (descriptor == null || adapter == null) return HostUnavailable.Invalid<bool>("command", "register", null);
            lock (sync)
            {
                Registration existing;
                if (registrations.TryGetValue(descriptor.CommandId, out existing))
                {
                    if (StringComparer.Ordinal.Equals(existing.Descriptor.Owner.Value, descriptor.Owner.Value) && existing.Descriptor.SchemaVersion.Equals(descriptor.SchemaVersion)) return OperationResult<bool>.Succeeded(true);
                    return OperationResult<bool>.Failed(FrameworkErrors.Create("command.conflict", FrameworkErrorCategory.Conflict, "The command is already registered by another extension or version.", descriptor.CommandId));
                }
                registrations.Add(descriptor.CommandId, new Registration(descriptor, adapter));
                return OperationResult<bool>.Succeeded(true);
            }
        }

        public OperationResult<CommandLedgerEntry> Prepare(CommandRequest request, RequestContext context)
        {
            if (request == null || context == null) return HostUnavailable.Invalid<CommandLedgerEntry>("command", "prepare", context);
            Registration registration = Find(request.CommandId);
            if (registration == null) return HostUnavailable.Failure<CommandLedgerEntry>("command", "prepare", context);
            IdempotencyScope scope = CreateScope(request, context, registration.Descriptor);
            string fullKey = scope.FullKey;
            lock (sync)
            {
                string existingId;
                if (entriesByDeduplication.TryGetValue(scope.DeduplicationKey, out existingId))
                {
                    CommandLedgerEntry existing = entries[existingId];
                    if (StringComparer.Ordinal.Equals(existing.Request.ArgumentsJson, request.ArgumentsJson ?? string.Empty)) return OperationResult<CommandLedgerEntry>.Succeeded(existing);
                    return OperationResult<CommandLedgerEntry>.Failed(FrameworkErrors.Create("command.idempotency_conflict", FrameworkErrorCategory.Conflict, "The idempotency key was reused with a different payload.", context.CorrelationId));
                }
            }

            OperationResult<CommandAdapterPreflight> preflight;
            try
            {
                preflight = registration.Adapter.Preflight(request, context);
            }
            catch
            {
                return OperationResult<CommandLedgerEntry>.Failed(FrameworkErrors.Create("command.preflight_failed", FrameworkErrorCategory.InternalFailure, "Command preflight failed.", context.CorrelationId));
            }
            if (!preflight.IsSuccess || preflight.Value == null) return OperationResult<CommandLedgerEntry>.Failed(preflight.Error ?? FrameworkErrors.Create("command.preflight_failed", FrameworkErrorCategory.Conflict, "Command preflight failed.", context.CorrelationId));

            lock (sync)
            {
                string existingId;
                if (entriesByDeduplication.TryGetValue(scope.DeduplicationKey, out existingId)) return OperationResult<CommandLedgerEntry>.Succeeded(entries[existingId]);
                CommandLedgerEntry entry = new CommandLedgerEntry("ledger-" + (entries.Count + 1).ToString(), request);
                entry.LedgerSequence = ++sequence;
                entries.Add(entry.LedgerEntryId, entry);
                entriesByDeduplication.Add(scope.DeduplicationKey, entry.LedgerEntryId);
                return OperationResult<CommandLedgerEntry>.Succeeded(entry);
            }
        }

        public OperationResult<SettlementReceipt> Settle(string ledgerEntryId, bool applied, string effectHash, RequestContext context)
        {
            if (string.IsNullOrWhiteSpace(ledgerEntryId) || context == null) return HostUnavailable.Invalid<SettlementReceipt>("command", "settle", context);
            CommandLedgerEntry entry;
            lock (sync) entries.TryGetValue(ledgerEntryId, out entry);
            if (entry == null) return OperationResult<SettlementReceipt>.Failed(FrameworkErrors.Create("command.ledger_not_found", FrameworkErrorCategory.NotFound, "The command ledger entry was not found.", context.CorrelationId));
            if (entry.Request.Scope != null && !entry.Request.Scope.Session.Equals(context.Session)) return OperationResult<SettlementReceipt>.Failed(FrameworkErrors.Create("command.session_mismatch", FrameworkErrorCategory.Denied, "The command session does not match the request context.", context.CorrelationId));
            lock (sync)
            {
                if (entry.State == CommandLedgerState.Applied || entry.State == CommandLedgerState.Rejected) return OperationResult<SettlementReceipt>.Failed(FrameworkErrors.Create("command.already_settled", FrameworkErrorCategory.Conflict, "The command is already settled.", context.CorrelationId));
                entry.State = applied ? CommandLedgerState.Applied : CommandLedgerState.Rejected;
                entry.EffectHash = effectHash ?? string.Empty;
                return OperationResult<SettlementReceipt>.Succeeded(new SettlementReceipt("settlement-" + entry.LedgerEntryId, entry, applied, entry.EffectHash));
            }
        }

        public OperationResult<CommandLedgerEntry> GetLedgerEntry(string ledgerEntryId, RequestContext context)
        {
            CommandLedgerEntry entry;
            lock (sync) entries.TryGetValue(ledgerEntryId ?? string.Empty, out entry);
            if (entry == null) return OperationResult<CommandLedgerEntry>.Failed(FrameworkErrors.Create("command.ledger_not_found", FrameworkErrorCategory.NotFound, "The command ledger entry was not found.", context?.CorrelationId ?? "command-get"));
            return OperationResult<CommandLedgerEntry>.Succeeded(entry);
        }

        public Task<OperationResult<CommandPreflight>> PreflightAsync(CommandRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            if (request == null || context == null) return Task.FromResult(HostUnavailable.Invalid<CommandPreflight>("command", "preflight", context));
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(Cancelled<CommandPreflight>(context));
            Registration registration = Find(request.CommandId);
            if (registration == null) return HostUnavailable.TaskFailure<CommandPreflight>("command", "preflight", context);
            OperationResult<CommandAdapterPreflight> preflight;
            try
            {
                preflight = registration.Adapter.Preflight(request, context);
            }
            catch
            {
                return Task.FromResult(OperationResult<CommandPreflight>.Failed(FrameworkErrors.Create("command.preflight_failed", FrameworkErrorCategory.InternalFailure, "Command preflight failed.", context.CorrelationId)));
            }
            if (!preflight.IsSuccess || preflight.Value == null) return Task.FromResult(OperationResult<CommandPreflight>.Failed(preflight.Error ?? FrameworkErrors.Create("command.preflight_failed", FrameworkErrorCategory.Conflict, "Command preflight failed.", context.CorrelationId)));
            return Task.FromResult(OperationResult<CommandPreflight>.Succeeded(new CommandPreflight(request.RequestId, registration.Descriptor.MinimumRisk, preflight.Value.Summary, preflight.Value.SnapshotToken, request.ExpiresUtc, registration.Descriptor.MinimumRisk >= CommandRiskTier.R2Gameplay)));
        }

        public Task<OperationResult<CommandReceipt>> SubmitAsync(CommandRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            if (request == null || context == null) return Task.FromResult(HostUnavailable.Invalid<CommandReceipt>("command", "submit", context));
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(Cancelled<CommandReceipt>(context));
            Registration registration = Find(request.CommandId);
            if (registration == null) return HostUnavailable.TaskFailure<CommandReceipt>("command", "submit", context);
            OperationResult<CommandAdapterPreflight> preflight;
            try
            {
                preflight = registration.Adapter.Preflight(request, context);
            }
            catch
            {
                return Task.FromResult(OperationResult<CommandReceipt>.Failed(FrameworkErrors.Create("command.preflight_failed", FrameworkErrorCategory.InternalFailure, "Command preflight failed.", context.CorrelationId)));
            }
            if (!preflight.IsSuccess || preflight.Value == null) return Task.FromResult(OperationResult<CommandReceipt>.Failed(preflight.Error ?? FrameworkErrors.Create("command.preflight_failed", FrameworkErrorCategory.Conflict, "Command preflight failed.", context.CorrelationId)));
            OperationResult<CommandAdapterResult> executed;
            try
            {
                executed = registration.Adapter.Execute(request, context, preflight.Value.SnapshotToken);
            }
            catch
            {
                return Task.FromResult(OperationResult<CommandReceipt>.Failed(FrameworkErrors.Create("command.execute_failed", FrameworkErrorCategory.InternalFailure, "Command execution failed.", context.CorrelationId)));
            }
            if (!executed.IsSuccess || executed.Value == null) return Task.FromResult(OperationResult<CommandReceipt>.Failed(executed.Error ?? FrameworkErrors.Create("command.execute_failed", FrameworkErrorCategory.Conflict, "Command execution failed.", context.CorrelationId)));
            CommandAdapterResult result = executed.Value;
            return Task.FromResult(OperationResult<CommandReceipt>.Succeeded(new CommandReceipt(request.RequestId, result.State, result.Summary, result.ResultEventId)));
        }

        internal void Remove(string commandId, ExtensionId owner)
        {
            if (string.IsNullOrWhiteSpace(commandId) || owner == null) return;
            lock (sync)
            {
                Registration registration;
                if (registrations.TryGetValue(commandId, out registration) && StringComparer.Ordinal.Equals(registration.Descriptor.Owner.Value, owner.Value)) registrations.Remove(commandId);
            }
        }

        private Registration Find(string commandId)
        {
            lock (sync)
            {
                Registration registration;
                registrations.TryGetValue(commandId ?? string.Empty, out registration);
                return registration;
            }
        }

        private static IdempotencyScope CreateScope(CommandRequest request, RequestContext context, CommandDescriptor descriptor)
        {
            IdempotencyScope supplied = request.Scope;
            string owner = supplied == null ? descriptor.Owner.Value : supplied.OwnerId;
            SessionRef session = supplied == null ? context.Session : supplied.Session;
            ApiVersion schema = supplied == null ? descriptor.SchemaVersion : supplied.CommandSchemaVersion;
            string idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? request.RequestId : request.IdempotencyKey;
            string payloadHash = string.IsNullOrWhiteSpace(request.ArgumentsJson) ? "{}" : request.ArgumentsJson;
            return new IdempotencyScope(owner, session, descriptor.CommandId, schema, idempotencyKey, payloadHash);
        }

        private static OperationResult<T> Cancelled<T>(RequestContext context)
        {
            return OperationResult<T>.Failed(FrameworkErrors.Create("command.cancelled", FrameworkErrorCategory.Cancelled, "The command was cancelled.", context?.CorrelationId ?? "command"));
        }
    }

    internal sealed class InMemoryEventService : IEventService
    {
        private sealed class Subscription : IEventSubscription
        {
            private readonly InMemoryEventService owner;
            private readonly string eventKind;
            private readonly Action<EventEnvelope> handler;
            private int disposed;

            internal Subscription(InMemoryEventService owner, string eventKind, Action<EventEnvelope> handler, string subscriptionId)
            {
                this.owner = owner;
                this.eventKind = eventKind;
                this.handler = handler;
                SubscriptionId = subscriptionId;
            }

            public string SubscriptionId { get; }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0) owner.Remove(eventKind, handler);
            }
        }

        private readonly object sync = new object();
        private readonly Dictionary<string, List<Action<EventEnvelope>>> handlers = new Dictionary<string, List<Action<EventEnvelope>>>(StringComparer.Ordinal);
        private long nextSubscription;

        public IEventSubscription Subscribe(string eventKind, Action<EventEnvelope> handler)
        {
            if (string.IsNullOrWhiteSpace(eventKind) || handler == null) return new UnavailableEventSubscription();
            lock (sync)
            {
                List<Action<EventEnvelope>> eventHandlers;
                if (!handlers.TryGetValue(eventKind, out eventHandlers))
                {
                    eventHandlers = new List<Action<EventEnvelope>>();
                    handlers.Add(eventKind, eventHandlers);
                }
                eventHandlers.Add(handler);
                nextSubscription++;
                return new Subscription(this, eventKind, handler, "event-subscription-" + nextSubscription.ToString());
            }
        }

        public OperationResult<bool> Publish(EventEnvelope envelope, EventDelivery delivery, RequestContext context)
        {
            if (envelope == null || context == null) return HostUnavailable.Invalid<bool>("event", "publish", context);
            List<Action<EventEnvelope>> snapshot = new List<Action<EventEnvelope>>();
            lock (sync)
            {
                List<Action<EventEnvelope>> eventHandlers;
                if (handlers.TryGetValue(envelope.EventKind, out eventHandlers)) snapshot.AddRange(eventHandlers);
            }
            for (int index = 0; index < snapshot.Count; index++)
            {
                try { snapshot[index](envelope); } catch { }
            }
            return OperationResult<bool>.Succeeded(true);
        }

        private void Remove(string eventKind, Action<EventEnvelope> handler)
        {
            lock (sync)
            {
                List<Action<EventEnvelope>> eventHandlers;
                if (!handlers.TryGetValue(eventKind, out eventHandlers)) return;
                eventHandlers.Remove(handler);
                if (eventHandlers.Count == 0) handlers.Remove(eventKind);
            }
        }
    }

    internal sealed class UnavailableEventSubscription : IEventSubscription
    {
        public string SubscriptionId => "event-subscription-unavailable";
        public void Dispose() { }
    }

    internal sealed class HostDiagnosticsService : IDiagnosticsService, ICompatibilityDiagnosticsService
    {
        private readonly FrameworkIdentity identity;
        private readonly ISessionCoordinator sessions;
        private readonly ICapabilityBroker capabilities;
        private readonly Func<IReadOnlyList<ExtensionManifest>> extensions;
        private readonly Func<HostState> hostState;
        private readonly Func<DateTimeOffset> clock;

        internal HostDiagnosticsService(FrameworkIdentity identity, ISessionCoordinator sessions, ICapabilityBroker capabilities, Func<IReadOnlyList<ExtensionManifest>> extensions, Func<HostState> hostState, Func<DateTimeOffset> clock)
        {
            this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
            this.extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
            this.hostState = hostState ?? throw new ArgumentNullException(nameof(hostState));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public OperationResult<FrameworkProbeReceipt> Probe(RequestContext context, FrameworkHealthState health)
        {
            if (context == null) return OperationResult<FrameworkProbeReceipt>.Failed(FrameworkErrors.Create("session_stale", FrameworkErrorCategory.Expired, "The request belongs to an inactive session.", "diagnostics-probe"));
            OperationResult<SessionLease> validation = sessions.ValidateLease(context);
            if (!validation.IsSuccess) return OperationResult<FrameworkProbeReceipt>.Failed(validation.Error);
            return OperationResult<FrameworkProbeReceipt>.Succeeded(new FrameworkProbeReceipt(identity, health, context.CorrelationId, validation.Value.Generation, clock()));
        }

        public HealthSnapshot GetHealth()
        {
            HostState state = hostState();
            HealthLevel hostLevel = state == HostState.Ready ? HealthLevel.Healthy : HealthLevel.Degraded;
            return new HealthSnapshot(clock(), new[]
            {
                new HealthComponent("host", hostLevel, "host." + state.ToString().ToLowerInvariant(), "Framework host state is " + state + "."),
                new HealthComponent("capabilities", HealthLevel.Healthy, "capability_registry_ready", "Capability registry is available."),
                new HealthComponent("runtime", HealthLevel.Unavailable, "runtime_service_unavailable", "Runtime Service is not connected."),
                new HealthComponent("ai", HealthLevel.Unavailable, "ai_gateway_unavailable", "AI gateway is not connected.")
            });
        }

        public IReadOnlyList<CapabilityDescriptor> GetCompatibilityReport() => capabilities.Discover() ?? new CapabilityDescriptor[0];

        public IReadOnlyList<ExtensionManifest> GetExtensions() => extensions() ?? new ExtensionManifest[0];
    }

    internal sealed class UnavailableGameDataService : IGameDataService, ICompatibilityGameDataService
    {
        public OperationResult<Page<DynamicEntityDto>> Query(GameDataQuery query, RequestContext context) => HostUnavailable.Failure<Page<DynamicEntityDto>>("game_data", "query", context);
        public Task<OperationResult<PlayerSnapshotDto>> GetCurrentPlayerAsync(RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<PlayerSnapshotDto>("game_data", "current_player", context);
    }

    internal sealed class UnavailableAiGateway : IAiGateway
    {
        public Task<OperationResult<IAiTaskHandle>> SubmitAsync(AiTaskRequest request, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<IAiTaskHandle>("ai_gateway", "submit", context);
        public Task<OperationResult<AiTaskReceipt>> GetReceiptAsync(AiTaskScope scope, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<AiTaskReceipt>("ai_gateway", "receipt", context);
    }

    internal sealed class UnavailableAiModelService : IAiModelService
    {
        public Task<OperationResult<RouteCapabilityReport>> GetCapabilitiesAsync(string routeId, string taskKind, string cloudExportClassification, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<RouteCapabilityReport>("ai_models", "capabilities", context);
        public Task<OperationResult<EmbeddingResult>> EmbedAsync(EmbeddingRequest request, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<EmbeddingResult>("ai_models", "embed", context);
        public Task<OperationResult<RerankResult>> RerankAsync(RerankRequest request, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<RerankResult>("ai_models", "rerank", context);
    }

    internal sealed class UnavailableMediaService : IMediaService
    {
        public Task<OperationResult<GeneratedAssetResult>> GenerateImageAsync(ImageGenerationRequest request, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<GeneratedAssetResult>("media", "image", context);
        public Task<OperationResult<GeneratedAssetResult>> SynthesizeSpeechAsync(TtsGenerationRequest request, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<GeneratedAssetResult>("media", "speech", context);
    }

    internal sealed class UnavailablePromptRegistry : IPromptRegistry
    {
        public Task<OperationResult<bool>> RegisterAsync(PromptDefinition definition, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<bool>("prompts", "register", context);
        public Task<OperationResult<PromptCompilation>> CompileAsync(PromptCompileRequest request, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<PromptCompilation>("prompts", "compile", context);
    }

    internal sealed class UnavailableStorageService : IStorageService
    {
        public Task<OperationResult<IKeyValueStore>> OpenCampaignNamespaceAsync(string namespaceId, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<IKeyValueStore>("storage", "campaign_namespace", context);
        public Task<OperationResult<IKeyValueStore>> OpenSessionNamespaceAsync(string namespaceId, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<IKeyValueStore>("storage", "session_namespace", context);
    }

    internal sealed class UnavailableRagService : IRagService
    {
        public Task<OperationResult<int>> IngestAsync(RagIngestRequest request, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<int>("rag", "ingest", context);
        public Task<OperationResult<IReadOnlyList<RagHit>>> SearchAsync(RagSearchRequest request, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<IReadOnlyList<RagHit>>("rag", "search", context);
    }

    internal sealed class UnavailableAssetService : IAssetService
    {
        public Task<OperationResult<AssetHandle>> ImportAsync(AssetImportRequest request, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<AssetHandle>("assets", "import", context);
        public Task<OperationResult<AssetMetadata>> GetMetadataAsync(string assetId, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<AssetMetadata>("assets", "metadata", context);
        public Task<OperationResult<AssetContent>> ReadAsync(string assetId, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<AssetContent>("assets", "read", context);
        public Task<OperationResult<bool>> SetPinnedAsync(string assetId, bool pinned, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<bool>("assets", "pin", context);
        public Task<OperationResult<AssetListPage>> ListAsync(int maximumResults, string cursor, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<AssetListPage>("assets", "list", context);
        public Task<OperationResult<AssetExportReceipt>> ExportAsync(string assetId, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<AssetExportReceipt>("assets", "export", context);
        public Task<OperationResult<bool>> DeleteAsync(string assetId, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<bool>("assets", "delete", context);
        public Task<OperationResult<AssetCleanupSummary>> CleanupAsync(AssetCleanupRequest request, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<AssetCleanupSummary>("assets", "cleanup", context);
    }

    internal sealed class UnavailablePermissionService : IPermissionService
    {
        private static readonly ExtensionId UnavailableOwner = new ExtensionId("MarcusAwakeFramework.unavailable");

        public PermissionEvaluation Evaluate(string permissionId, RequestContext context)
        {
            return new PermissionEvaluation(
                string.IsNullOrWhiteSpace(permissionId) ? "unavailable.permission" : permissionId,
                UnavailableOwner,
                PermissionDecision.NotRequested,
                "permission_service_unavailable",
                null);
        }

        public Task<OperationResult<PermissionEvaluation>> RequestAsync(string permissionId, string purpose, RequestContext context, CancellationToken cancellationToken) => HostUnavailable.TaskFailure<PermissionEvaluation>("permissions", "request", context);
        public OperationResult<bool> Revoke(string permissionId, ExtensionId extensionId) => HostUnavailable.Failure<bool>("permissions", "revoke", null);
    }

    internal sealed class UnavailableSaveAnchorStore : ISaveAnchorStore
    {
        private static readonly SaveAnchorSnapshot UnavailableSnapshot = new SaveAnchorSnapshot(
            "unavailable.campaign",
            "unavailable.timeline",
            0,
            0,
            string.Empty,
            "unavailable.schema",
            "unavailable.migration",
            "unavailable.worldbook");

        public SaveAnchorSnapshot Current => UnavailableSnapshot;
        public OperationResult<SaveAttempt> Prepare(SaveAnchorSnapshot snapshot) => HostUnavailable.Failure<SaveAttempt>("save_anchors", "prepare", null);
        public OperationResult<bool> MarkSerialized(string attemptId) => HostUnavailable.Failure<bool>("save_anchors", "serialize", null);
        public OperationResult<bool> Commit(string attemptId) => HostUnavailable.Failure<bool>("save_anchors", "commit", null);
    }

    internal sealed class StrictUnavailableRuntimeServicePort : IRuntimeServicePort
    {
        private readonly RuntimeServiceStatus status = new RuntimeServiceStatus(
            RuntimeServiceState.Stopped,
            "marcus-awake.runtime-service",
            string.Empty,
            0,
            new ApiVersion(1, 0),
            new string[0]);

        public RuntimeServiceStatus Status => status;
        public OperationResult<RuntimeServiceStatus> Start(RuntimeServiceStartRequest request, RequestContext context) => HostUnavailable.Failure<RuntimeServiceStatus>("runtime_service", "start", context);
        public OperationResult<RuntimeServiceStatus> BeginDrain(RequestContext context) => HostUnavailable.Failure<RuntimeServiceStatus>("runtime_service", "begin_drain", context);
        public OperationResult<RuntimeServiceStatus> CompleteDrain(RequestContext context) => HostUnavailable.Failure<RuntimeServiceStatus>("runtime_service", "complete_drain", context);
    }

    internal sealed class UnavailableLoggingService : ILoggingService
    {
        public string Directory => string.Empty;
        public void Write(string fileName, FrameworkLogLevel level, string message, Exception exception = null) { }
    }

    public static class FrameworkHostLocator
    {
        private static readonly object Sync = new object();
        private static IMarcusAwakeFrameworkHost current;
        private static string currentOwnerId;
        private static IRuntimeServicePort currentOwnerRuntime;
        private static FrameworkServiceOverrides currentOwnerOverrides;

        public static OperationResult<bool> Register(IMarcusAwakeFrameworkHost host)
        {
            if (host == null) return OperationResult<bool>.Failed(FrameworkErrors.Create("host.missing", FrameworkErrorCategory.InvalidRequest, "A framework host is required.", "host-locator"));
            lock (Sync)
            {
                if (host.State != HostState.Ready) return OperationResult<bool>.Failed(FrameworkErrors.Create("host.not_ready", FrameworkErrorCategory.Conflict, "Only a ready host can be located.", "host-locator"));
                if (current != null && !ReferenceEquals(current, host)) return OperationResult<bool>.Failed(FrameworkErrors.Create("host.conflict", FrameworkErrorCategory.Conflict, "A different framework host is already registered.", "host-locator"));
                if (ReferenceEquals(current, host)) return OperationResult<bool>.Succeeded(true);
                current = host;
                return OperationResult<bool>.Succeeded(true);
            }
        }

        public static OperationResult<bool> Register(IFrameworkExtension extension)
        {
            return Register(extension, null);
        }

        public static OperationResult<bool> Register(IFrameworkExtension extension, IRuntimeServicePort runtime)
        {
            return Register(extension, runtime, null);
        }

        public static OperationResult<bool> Register(IFrameworkExtension extension, IRuntimeServicePort runtime, FrameworkServiceOverrides overrides)
        {
            if (extension == null) return OperationResult<bool>.Failed(FrameworkErrors.Create("extension.null", FrameworkErrorCategory.InvalidRequest, "An extension is required.", "host-locator"));
            ExtensionManifest manifest;
            try { manifest = extension.Manifest; }
            catch { return OperationResult<bool>.Failed(FrameworkErrors.Create("extension.manifest_failed", FrameworkErrorCategory.InvalidRequest, "The extension manifest could not be read.", "host-locator")); }
            if (manifest == null || manifest.ExtensionId == null) return OperationResult<bool>.Failed(FrameworkErrors.Create("extension.manifest_missing", FrameworkErrorCategory.InvalidRequest, "The extension manifest is missing.", "host-locator"));
            IMarcusAwakeFrameworkHost located;
            lock (Sync) located = current;
            if (located != null)
            {
                FrameworkHost existing = located as FrameworkHost;
                if (existing == null) return OperationResult<bool>.Failed(FrameworkErrors.Create("host.registration_unavailable", FrameworkErrorCategory.Unavailable, "The located host does not expose embedded extension registration.", "host-locator", retryable: true));
                if (overrides != null)
                {
                    bool sameOverrides;
                    lock (Sync) sameOverrides = ReferenceEquals(currentOwnerOverrides, overrides);
                    if (!sameOverrides) return OperationResult<bool>.Failed(FrameworkErrors.Create("host.overrides_conflict", FrameworkErrorCategory.Conflict, "The located framework host was composed without these service overrides.", "host-locator"));
                }
                if (runtime != null)
                {
                    lock (Sync)
                    {
                        if (!ReferenceEquals(current, existing)
                            || !StringComparer.Ordinal.Equals(currentOwnerId, manifest.ExtensionId.Value)
                            || !ReferenceEquals(currentOwnerRuntime, runtime))
                        {
                            return OperationResult<bool>.Failed(FrameworkErrors.Create("host.owner_conflict", FrameworkErrorCategory.Conflict, "The located framework host is owned by another composition.", "host-locator"));
                        }
                    }
                    if (!ReferenceEquals(existing.Runtime, runtime)) return OperationResult<bool>.Failed(FrameworkErrors.Create("host.runtime_mismatch", FrameworkErrorCategory.Incompatible, "The located framework host uses a different runtime service.", "host-locator"));
                }
                return existing.RegisterExtension(extension);
            }

            FrameworkHost host = FrameworkHost.CreateDefaultHost(runtime, overrides);
            OperationResult<bool> registered = host.Register();
            if (!registered.IsSuccess) return registered;
            OperationResult<bool> extensionResult = host.RegisterExtension(extension);
            if (!extensionResult.IsSuccess)
            {
                host.AbortRegistration();
                return extensionResult;
            }
            OperationResult<bool> ready = host.Ready();
            if (!ready.IsSuccess)
            {
                host.AbortRegistration();
                return ready;
            }
            OperationResult<bool> published;
            lock (Sync)
            {
                if (current != null)
                {
                    host.AbortRegistration();
                    return OperationResult<bool>.Failed(FrameworkErrors.Create("host.conflict", FrameworkErrorCategory.Conflict, "A different framework host is already registered.", "host-locator"));
                }
                current = host;
                currentOwnerId = runtime == null ? null : manifest.ExtensionId.Value;
                currentOwnerRuntime = runtime;
                currentOwnerOverrides = overrides;
                published = OperationResult<bool>.Succeeded(true);
            }
            if (!published.IsSuccess) host.AbortRegistration();
            return published;
        }

        public static bool TryGetHost(out IMarcusAiFrameworkHost host)
        {
            lock (Sync)
            {
                host = current as IMarcusAiFrameworkHost;
                return host != null;
            }
        }

        public static IMarcusAwakeFrameworkHost Resolve()
        {
            lock (Sync) return current;
        }

        public static void Clear(IMarcusAwakeFrameworkHost host)
        {
            lock (Sync)
            {
                if (!ReferenceEquals(current, host)) return;
                current = null;
                currentOwnerId = null;
                currentOwnerRuntime = null;
                currentOwnerOverrides = null;
            }
        }
    }
}
