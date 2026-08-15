using System;
using System.Collections.Generic;
using System.Threading;
using MarcusAIFramework.Api;

namespace MarcusAIFramework.Sdk.FakeHost
{
    /// <summary>Deterministic test primitives for extension contract tests; this is not a game host.</summary>
    public sealed class FakeClock
    {
        public FakeClock(DateTimeOffset? initialUtc = null) { UtcNow = initialUtc ?? DateTimeOffset.UtcNow; }
        public DateTimeOffset UtcNow { get; private set; }
        public void Advance(TimeSpan amount) { if (amount < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(amount)); UtcNow = UtcNow.Add(amount); }
        public RequestContext Context(string caller, SessionRef session = null, string correlationId = "fake-correlation", TimeSpan? budget = null) =>
            new RequestContext(new ExtensionId(caller), session ?? new SessionRef("fake-campaign", "fake-timeline", "fake-session"), correlationId, UtcNow.Add(budget ?? TimeSpan.FromSeconds(30)));
    }

    public sealed class FakePermissionMatrix
    {
        private readonly HashSet<string> _grants = new HashSet<string>(StringComparer.Ordinal);
        public void Grant(string permission) { _grants.Add(permission ?? string.Empty); }
        public void Revoke(string permission) { _grants.Remove(permission ?? string.Empty); }
        public bool IsGranted(string permission) => _grants.Contains(permission ?? string.Empty);
        public OperationResult<bool> Require(string permission, RequestContext context)
        {
            if (context == null) return Denied("fake.permission_context_missing", null);
            return IsGranted(permission)
                ? OperationResult<bool>.Succeeded(true)
                : Denied("fake.permission_denied", context.CorrelationId);
        }
        private static OperationResult<bool> Denied(string code, string correlationId) =>
            OperationResult<bool>.Failed(FrameworkErrors.Create(code, FrameworkErrorCategory.Denied, "The fake permission matrix denied the request.", correlationId, owner: "MarcusAIFramework.Sdk"));
    }

    public sealed class FakeEventRecorder
    {
        private readonly List<EventEnvelope> _events = new List<EventEnvelope>();
        public IReadOnlyList<EventEnvelope> Events => _events.ToArray();
        public void Record(EventEnvelope envelope) { if (envelope != null) _events.Add(envelope); }
        public void Clear() { _events.Clear(); }
    }

    public sealed class FakeCapabilityInvoker
    {
        private readonly Dictionary<string, Func<string, RequestContext, OperationResult<string>>> _handlers =
            new Dictionary<string, Func<string, RequestContext, OperationResult<string>>>(StringComparer.Ordinal);
        public void Add(CapabilityId capability, Func<string, RequestContext, OperationResult<string>> handler)
        {
            if (capability == null || handler == null) throw new ArgumentNullException();
            _handlers[capability.Value] = handler;
        }
        public OperationResult<string> Invoke(CapabilityId capability, string payload, RequestContext context)
        {
            Func<string, RequestContext, OperationResult<string>> handler;
            if (capability == null || !_handlers.TryGetValue(capability.Value, out handler))
                return OperationResult<string>.Failed(FrameworkErrors.Create("fake.capability_unavailable", FrameworkErrorCategory.Unavailable, "The fake capability is unavailable.", context?.CorrelationId, true, "MarcusAIFramework.Sdk"));
            return handler(payload ?? "{}", context);
        }
    }

    public sealed class FakeToolCandidateValidator
    {
        private readonly Dictionary<string, ToolDescriptor> _tools = new Dictionary<string, ToolDescriptor>(StringComparer.Ordinal);
        private readonly FakePermissionMatrix _permissions;
        public FakeToolCandidateValidator(FakePermissionMatrix permissions) { _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions)); }
        public void Register(ToolDescriptor descriptor) { if (descriptor == null) throw new ArgumentNullException(nameof(descriptor)); _tools.Add(descriptor.QualifiedId, descriptor); }
        public OperationResult<ToolCandidateValidationResult> Validate(ToolCandidate candidate, IReadOnlyList<string> frozenAllowlist, RequestContext context, int currentTurn, int maximumCandidates = 8)
        {
            if (candidate == null || context == null || candidate.SourceTurn < 0 || candidate.SourceTurn > currentTurn ||
                candidate.CandidateOrdinal < 0 || candidate.CandidateOrdinal >= maximumCandidates ||
                string.IsNullOrWhiteSpace(candidate.ArgumentsJson) || !candidate.ArgumentsJson.TrimStart().StartsWith("{", StringComparison.Ordinal) ||
                !candidate.ArgumentsJson.TrimEnd().EndsWith("}", StringComparison.Ordinal))
                return Failure("tool.candidate_invalid", FrameworkErrorCategory.InvalidRequest, context?.CorrelationId);
            ToolDescriptor descriptor;
            if (!_tools.TryGetValue(candidate.ToolId, out descriptor))
                return Failure("tool.not_found", FrameworkErrorCategory.NotFound, context.CorrelationId);
            bool allowlisted = false;
            foreach (string value in frozenAllowlist ?? Array.Empty<string>())
                if (StringComparer.Ordinal.Equals(value, descriptor.QualifiedId) || StringComparer.Ordinal.Equals(value, descriptor.ToolId)) allowlisted = true;
            if (!allowlisted) return Failure("tool.not_allowlisted", FrameworkErrorCategory.Denied, context.CorrelationId);
            OperationResult<bool> permitted = _permissions.Require("tool.invoke:" + descriptor.QualifiedId, context);
            if (!permitted.IsSuccess) return Failure("tool.permission_denied", FrameworkErrorCategory.Denied, context.CorrelationId);
            return OperationResult<ToolCandidateValidationResult>.Succeeded(new ToolCandidateValidationResult(true, string.Empty, descriptor, descriptor.CommandRisk == CommandRiskTier.R3Strategic));
        }
        private static OperationResult<ToolCandidateValidationResult> Failure(string code, FrameworkErrorCategory category, string correlationId) =>
            OperationResult<ToolCandidateValidationResult>.Failed(FrameworkErrors.Create(code, category, "Fake tool candidate validation failed.", correlationId, owner: "MarcusAIFramework.Sdk"));
    }

    public sealed class FakeStream<T>
    {
        private readonly List<T> _items = new List<T>();
        public bool IsCompleted { get; private set; }
        public bool IsCancelled { get; private set; }
        public IReadOnlyList<T> Items => _items.ToArray();
        public void Push(T item) { if (IsCompleted || IsCancelled) throw new InvalidOperationException("The fake stream is closed."); _items.Add(item); }
        public void Complete() { IsCompleted = true; }
        public void Cancel() { IsCancelled = true; }
    }
}
