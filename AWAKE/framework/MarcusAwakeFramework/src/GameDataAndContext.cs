using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api
{
    public enum VisibilityLevel
    {
        PublicCatalog,
        PlayerKnown,
        ObservedHistory,
        FullSimulation
    }

    public sealed class VisibilityScope
    {
        public VisibilityScope(VisibilityLevel maximumLevel) { MaximumLevel = maximumLevel; }
        public VisibilityLevel MaximumLevel { get; }
        public bool Allows(VisibilityLevel requiredLevel) => requiredLevel <= MaximumLevel;
    }

    public sealed class GameDataQuery
    {
        public GameDataQuery(string entityType, PageRequest page, VisibilityScope visibility, SnapshotToken snapshot = null)
        {
            EntityType = ContractGuard.Id(entityType, nameof(entityType));
            Page = page ?? throw new ArgumentNullException(nameof(page));
            Visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
            Snapshot = snapshot;
        }
        public string EntityType { get; }
        public PageRequest Page { get; }
        public VisibilityScope Visibility { get; }
        public SnapshotToken Snapshot { get; }
    }

    public sealed class DynamicEntityDto
    {
        public DynamicEntityDto(EntityRef identity, IReadOnlyDictionary<string, string> fields)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }
        public EntityRef Identity { get; }
        public IReadOnlyDictionary<string, string> Fields { get; }
    }

    public interface IGameDataService
    {
        OperationResult<Page<DynamicEntityDto>> Query(GameDataQuery query, RequestContext context);
    }

    public sealed class ContextContribution
    {
        public ContextContribution(string sourceId, string text, VisibilityLevel requiredVisibility, int tokenCost, string classification)
        {
            SourceId = ContractGuard.Id(sourceId, nameof(sourceId));
            Text = text ?? string.Empty;
            RequiredVisibility = requiredVisibility;
            TokenCost = tokenCost < 0 ? throw new ArgumentOutOfRangeException(nameof(tokenCost)) : tokenCost;
            Classification = ContractGuard.Id(classification, nameof(classification));
            ContributionId = SourceId;
            ProviderId = SourceId;
            ContentType = ContextContentType.Text;
            AccessScope = classification ?? string.Empty;
            SourceClass = classification ?? string.Empty;
            EpistemicStatus = "fact";
            EntityRefs = new EntityRef[0];
            ObservedAtUtc = DateTimeOffset.MinValue;
            SnapshotToken = string.Empty;
            Priority = 0;
            TokenEstimate = TokenCost;
            ExpiresUtc = null;
            CloudExportClassification = classification ?? "none";
            PayloadJson = text ?? string.Empty;
        }

        public ContextContribution(string contributionId, string providerId, ContextContentType contentType, string accessScope, string sourceClass, string epistemicStatus, IReadOnlyList<EntityRef> entityRefs, DateTimeOffset observedAtUtc, string snapshotToken, int priority, int tokenEstimate, DateTimeOffset? expiresUtc, string cloudExportClassification, string payloadJson)
        {
            ContributionId = ContractGuard.Id(contributionId, nameof(contributionId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            ContentType = contentType;
            AccessScope = accessScope ?? string.Empty;
            SourceClass = sourceClass ?? string.Empty;
            EpistemicStatus = epistemicStatus ?? string.Empty;
            EntityRefs = entityRefs ?? new EntityRef[0];
            ObservedAtUtc = observedAtUtc;
            SnapshotToken = snapshotToken ?? string.Empty;
            Priority = priority;
            TokenEstimate = tokenEstimate < 0 ? throw new ArgumentOutOfRangeException(nameof(tokenEstimate)) : tokenEstimate;
            ExpiresUtc = expiresUtc;
            CloudExportClassification = cloudExportClassification ?? "none";
            PayloadJson = payloadJson ?? "{}";
            SourceId = ContributionId;
            Text = PayloadJson;
            RequiredVisibility = VisibilityLevel.PlayerKnown;
            TokenCost = TokenEstimate;
            Classification = CloudExportClassification;
        }

        public string SourceId { get; }
        public string Text { get; }
        public VisibilityLevel RequiredVisibility { get; }
        public int TokenCost { get; }
        public int EstimatedTokens => TokenCost;
        public string Classification { get; }
        public string ContributionId { get; }
        public string ProviderId { get; }
        public ContextContentType ContentType { get; }
        public string AccessScope { get; }
        public string SourceClass { get; }
        public string EpistemicStatus { get; }
        public IReadOnlyList<EntityRef> EntityRefs { get; }
        public DateTimeOffset ObservedAtUtc { get; }
        public string SnapshotToken { get; }
        public int Priority { get; }
        public int TokenEstimate { get; }
        public DateTimeOffset? ExpiresUtc { get; }
        public string CloudExportClassification { get; }
        public string PayloadJson { get; }
    }

    public sealed class ContextExclusion
    {
        public ContextExclusion(string sourceId, string reason) { SourceId = ContractGuard.Id(sourceId, nameof(sourceId)); Reason = ContractGuard.Id(reason, nameof(reason)); }
        public string SourceId { get; }
        public string ContributionId => SourceId;
        public string ProviderId => SourceId;
        public string Reason { get; }
    }

    public sealed class ContextPlan
    {
        public ContextPlan(IReadOnlyList<ContextContribution> included, IReadOnlyList<ContextExclusion> excluded, int tokenCost)
        {
            Included = included ?? throw new ArgumentNullException(nameof(included));
            Excluded = excluded ?? throw new ArgumentNullException(nameof(excluded));
            TokenCost = tokenCost;
        }
        public IReadOnlyList<ContextContribution> Included { get; }
        public IReadOnlyList<ContextExclusion> Excluded { get; }
        public int TokenCost { get; }
        public int EstimatedTokens => TokenCost;
    }

    public interface IContextPlanner
    {
        OperationResult<ContextPlan> Plan(RequestContext context, VisibilityScope visibility, IReadOnlyList<ContextContribution> contributions, int maximumTokens);
    }

    public sealed class ContextPlanner : IContextPlanner
    {
        public OperationResult<ContextPlan> Plan(RequestContext context, VisibilityScope visibility, IReadOnlyList<ContextContribution> contributions, int maximumTokens)
        {
            if (context == null || visibility == null || contributions == null || maximumTokens < 0) return OperationResult<ContextPlan>.Failed(FrameworkErrors.Create("context.invalid_request", FrameworkErrorCategory.InvalidRequest, "The context plan request is invalid.", context?.CorrelationId ?? "context-plan"));
            var included = new List<ContextContribution>();
            var excluded = new List<ContextExclusion>();
            var cost = 0;
            for (var index = 0; index < contributions.Count; index++)
            {
                var item = contributions[index];
                if (!visibility.Allows(item.RequiredVisibility)) { excluded.Add(new ContextExclusion(item.SourceId, "visibility_denied")); continue; }
                if (cost + item.TokenCost > maximumTokens) { excluded.Add(new ContextExclusion(item.SourceId, "token_budget")); continue; }
                included.Add(item);
                cost += item.TokenCost;
            }
            return OperationResult<ContextPlan>.Succeeded(new ContextPlan(included.AsReadOnly(), excluded.AsReadOnly(), cost));
        }
    }
}
