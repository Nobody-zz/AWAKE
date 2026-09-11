using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api
{
    public delegate Task<OperationResult<string>> CapabilityHandler(string payloadJson, RequestContext context, CancellationToken cancellationToken);

    public enum CapabilityAvailability
    {
        Available,
        Degraded,
        Unavailable,
        Unsupported,
        Denied,
        Incompatible
    }

    public enum CapabilityMaturity
    {
        Planned,
        Experimental,
        Preview,
        Stable,
        Deprecated
    }

    public enum CapabilityVisibility
    {
        Public,
        ConsentRequired,
        Private
    }
    public enum CommandRiskTier
    {
        R0Query,
        R1Interface,
        R2Gameplay,
        R3Strategic
    }

    public enum CommandState
    {
        Proposed,
        Rejected,
        Preflighted,
        AwaitingApproval,
        Authorized,
        Revalidating,
        Executing,
        Succeeded,
        Failed,
        Partial,
        Expired,
        Cancelled
    }

    public sealed class CommandAdapterPreflight
    {
        public CommandAdapterPreflight(string summary, string snapshotToken)
        {
            Summary = summary ?? string.Empty;
            SnapshotToken = snapshotToken ?? string.Empty;
        }

        public string Summary { get; }
        public string SnapshotToken { get; }
    }

    public sealed class CommandAdapterResult
    {
        public CommandAdapterResult(CommandState state, string summary, string resultEventId = null)
        {
            State = state;
            Summary = summary ?? string.Empty;
            ResultEventId = resultEventId ?? string.Empty;
        }

        public CommandState State { get; }
        public string Summary { get; }
        public string ResultEventId { get; }
    }

    public interface ICommandAdapter
    {
        OperationResult<CommandAdapterPreflight> Preflight(CommandRequest request, RequestContext context);
        OperationResult<CommandAdapterResult> Execute(CommandRequest request, RequestContext context, string expectedSnapshotToken);
    }

    public enum ExtensionLifecycleStage
    {
        Registered,
        FrameworkReady,
        CampaignSessionStarting,
        CampaignSessionReady,
        MissionSessionStarting,
        MissionSessionReady,
        Suspending,
        SessionEnding,
        Unregistered
    }

    public interface IFrameworkExtension
    {
        ExtensionManifest Manifest { get; }
        void Register(IExtensionRegistration registration);
        void OnLifecycle(ExtensionLifecycleStage stage, SessionRef session);
    }

    public interface IExtensionRegistration
    {
        ExtensionId Owner { get; }
        OperationResult<bool> RegisterCapability(CapabilityDescriptor descriptor, CapabilityHandler handler);
        OperationResult<bool> RegisterCommand(CommandDescriptor descriptor, ICommandAdapter adapter);
        OperationResult<bool> RegisterTool(ToolDescriptor descriptor);
        OperationResult<bool> RegisterContextProvider(IContextProvider provider);
    }

    public sealed class ContextPlanRequest
    {
        public ContextPlanRequest(IReadOnlyList<string> providerIds, IReadOnlyList<string> requiredProviderIds, IReadOnlyList<string> allowedAccessScopes, IReadOnlyList<string> allowedCloudExportClassifications, int maximumTokens)
        {
            if (maximumTokens < 1 || maximumTokens > 1000000) throw new ArgumentOutOfRangeException(nameof(maximumTokens));
            ProviderIds = providerIds ?? new string[0];
            RequiredProviderIds = requiredProviderIds ?? new string[0];
            AllowedAccessScopes = allowedAccessScopes ?? new string[0];
            AllowedCloudExportClassifications = allowedCloudExportClassifications ?? new string[0];
            MaximumTokens = maximumTokens;
        }

        public IReadOnlyList<string> ProviderIds { get; }
        public IReadOnlyList<string> RequiredProviderIds { get; }
        public IReadOnlyList<string> AllowedAccessScopes { get; }
        public IReadOnlyList<string> AllowedCloudExportClassifications { get; }
        public int MaximumTokens { get; }
    }

    public interface IContextProvider
    {
        string ProviderId { get; }
        ExtensionId Owner { get; }
        Task<OperationResult<IReadOnlyList<ContextContribution>>> ContributeAsync(ContextPlanRequest request, RequestContext context, CancellationToken cancellationToken);
    }

    public enum PermissionDecision
    {
        Granted,
        Denied,
        NotRequested,
        Expired
    }

    public sealed class PermissionEvaluation
    {
        public PermissionEvaluation(string permissionId, ExtensionId extensionId, PermissionDecision decision, string reason, DateTimeOffset? validUntilUtc)
        {
            PermissionId = permissionId ?? string.Empty;
            ExtensionId = extensionId;
            Decision = decision;
            Reason = reason ?? string.Empty;
            ValidUntilUtc = validUntilUtc;
        }

        public string PermissionId { get; }
        public ExtensionId ExtensionId { get; }
        public PermissionDecision Decision { get; }
        public string Reason { get; }
        public DateTimeOffset? ValidUntilUtc { get; }
    }

    public interface IPermissionService
    {
        PermissionEvaluation Evaluate(string permissionId, RequestContext context);
        Task<OperationResult<PermissionEvaluation>> RequestAsync(string permissionId, string purpose, RequestContext context, CancellationToken cancellationToken);
        OperationResult<bool> Revoke(string permissionId, ExtensionId extensionId);
    }

    public interface IToolCandidateService
    {
        OperationResult<bool> Register(ToolDescriptor descriptor);
        IReadOnlyList<ToolDescriptor> Describe();
        OperationResult<ToolCandidateValidationResult> Validate(ToolCandidate candidate, IReadOnlyList<string> allowedToolIds, RequestContext context, int currentTurn, int maximumCandidates = 8);
    }

    public interface IEventSubscription : IDisposable
    {
        string SubscriptionId { get; }
    }

    public interface IEventService
    {
        IEventSubscription Subscribe(string eventKind, Action<EventEnvelope> handler);
        OperationResult<bool> Publish(EventEnvelope envelope, EventDelivery delivery, RequestContext context);
    }

    public interface IContextService
    {
        IReadOnlyList<string> DiscoverProviders();
        Task<OperationResult<ContextPlan>> PlanAsync(ContextPlanRequest request, RequestContext context, CancellationToken cancellationToken);
    }

    public interface ILoggingService
    {
        string Directory { get; }
        void Write(string fileName, FrameworkLogLevel level, string message, Exception exception = null);
    }
}