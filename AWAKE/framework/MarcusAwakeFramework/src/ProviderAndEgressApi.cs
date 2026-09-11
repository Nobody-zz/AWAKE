using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api
{
    public sealed class RouteProfile
    {
        public RouteProfile(string routeId, string providerId, string profileId, string modelId, bool isCloud, IReadOnlyList<string> capabilities)
        {
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            ProfileId = ContractGuard.Id(profileId, nameof(profileId));
            ModelId = modelId ?? string.Empty;
            IsCloud = isCloud;
            Capabilities = capabilities ?? new string[0];
        }

        public string RouteId { get; }
        public string ProviderId { get; }
        public string ProfileId { get; }
        public string ModelId { get; }
        public bool IsCloud { get; }
        public IReadOnlyList<string> Capabilities { get; }
    }

    public sealed class ProviderCapability
    {
        public ProviderCapability(string providerId, string capabilityId, bool available, string reason)
        {
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            CapabilityId = ContractGuard.Id(capabilityId, nameof(capabilityId));
            Available = available;
            Reason = reason ?? string.Empty;
        }

        public string ProviderId { get; }
        public string CapabilityId { get; }
        public bool Available { get; }
        public string Reason { get; }
    }

    public interface IRouteProfileResolver
    {
        OperationResult<RouteProfile> Resolve(string routeId, string providerId, string profileId, RequestContext context);
    }

    public enum EgressDecisionKind
    {
        Allowed,
        Denied,
        RequiresConsent,
        Unavailable
    }

    public sealed class EgressPolicyRequest
    {
        public EgressPolicyRequest(string classification, string routeId, string providerId, string profileId, string requestHash, IReadOnlyList<string> allowedFieldIds, IReadOnlyList<string> grantRuleIds, IReadOnlyList<string> allowedDomains)
        {
            Classification = ContractGuard.Id(classification, nameof(classification));
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            ProfileId = ContractGuard.Id(profileId, nameof(profileId));
            RequestHash = ContractGuard.Id(requestHash, nameof(requestHash));
            AllowedFieldIds = allowedFieldIds ?? new string[0];
            GrantRuleIds = grantRuleIds ?? new string[0];
            AllowedDomains = allowedDomains ?? new string[0];
        }

        public string Classification { get; }
        public string RouteId { get; }
        public string ProviderId { get; }
        public string ProfileId { get; }
        public string RequestHash { get; }
        public IReadOnlyList<string> AllowedFieldIds { get; }
        public IReadOnlyList<string> GrantRuleIds { get; }
        public IReadOnlyList<string> AllowedDomains { get; }
    }

    public sealed class EgressPolicyDecision
    {
        public EgressPolicyDecision(EgressDecisionKind kind, string policyReceiptId, string reason, string requestHash)
        {
            Kind = kind;
            PolicyReceiptId = ContractGuard.Id(policyReceiptId, nameof(policyReceiptId));
            Reason = reason ?? string.Empty;
            RequestHash = ContractGuard.Id(requestHash, nameof(requestHash));
        }

        public EgressDecisionKind Kind { get; }
        public string PolicyReceiptId { get; }
        public string Reason { get; }
        public string RequestHash { get; }
        public bool Allowed => Kind == EgressDecisionKind.Allowed;
    }

    public interface IEgressPolicy
    {
        OperationResult<EgressPolicyDecision> Evaluate(EgressPolicyRequest request, RequestContext context);
        OperationResult<bool> Consume(EgressPolicyDecision decision, AiTaskScope scope, RequestContext context);
    }

    public sealed class ProviderFailure
    {
        public ProviderFailure(string code, string safeFallback, bool retryable)
        {
            Code = ContractGuard.Id(code, nameof(code));
            SafeFallback = safeFallback ?? string.Empty;
            Retryable = retryable;
        }

        public string Code { get; }
        public string SafeFallback { get; }
        public bool Retryable { get; }
    }
}
