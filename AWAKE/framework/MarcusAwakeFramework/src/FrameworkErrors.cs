using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api
{
    public enum FrameworkErrorCategory
    {
        InvalidRequest,
        Incompatible,
        Unsupported,
        Unavailable,
        Denied,
        NotFound,
        Conflict,
        Expired,
        RateLimited,
        ProviderFailure,
        Timeout,
        Cancelled,
        ResourceExhausted,
        RecoveryRequired,
        InternalFailure
    }

    public sealed class FrameworkError
    {
        public FrameworkError(string code, FrameworkErrorCategory category, string safeFallback, bool retryable, string owner, string correlationId, IReadOnlyDictionary<string, string> details = null)
            : this(code, category, "MAF_Error_" + (code ?? "framework_unknown").Replace('.', '_'), safeFallback, retryable, owner, correlationId, details)
        {
        }

        public FrameworkError(string code, FrameworkErrorCategory category, string messageTextId, string safeFallback, bool retryable, string owner, string correlationId, IReadOnlyDictionary<string, string> details = null)
        {
            Code = ContractGuard.Id(code, nameof(code));
            Category = category;
            MessageTextId = messageTextId ?? string.Empty;
            SafeFallback = safeFallback ?? string.Empty;
            Retryable = retryable;
            Owner = ContractGuard.Id(owner, nameof(owner));
            CorrelationId = ContractGuard.Id(correlationId, nameof(correlationId));
            Details = details ?? new Dictionary<string, string>();
        }

        public string Code { get; }
        public FrameworkErrorCategory Category { get; }
        public string MessageTextId { get; }
        public string SafeFallback { get; }
        public bool Retryable { get; }
        public string Owner { get; }
        public string CorrelationId { get; }
        public IReadOnlyDictionary<string, string> Details { get; }
    }

    public sealed class ProviderErrorMapping
    {
        internal ProviderErrorMapping(string providerCategory, string providerId, string correlationId, FrameworkErrorCategory coreCategory, bool retryable, bool fallbackAllowed)
        {
            ProviderCategory = providerCategory ?? throw new ArgumentNullException(nameof(providerCategory));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            CorrelationId = ContractGuard.Id(correlationId, nameof(correlationId));
            CoreCategory = coreCategory;
            Retryable = retryable;
            FallbackAllowed = fallbackAllowed;
        }

        public string ProviderCategory { get; }
        public string ProviderId { get; }
        public string CorrelationId { get; }
        public FrameworkErrorCategory CoreCategory { get; }
        public bool Retryable { get; }
        public bool FallbackAllowed { get; }
    }

    public static class FrameworkErrors
    {
        public static FrameworkError Create(string code, FrameworkErrorCategory category, string safeFallback, string correlationId, bool retryable = false, string owner = "MarcusAwakeFramework", IReadOnlyDictionary<string, string> details = null) =>
            new FrameworkError(code, category, safeFallback, retryable, owner, correlationId ?? "no-correlation", details);

        public static ProviderErrorMapping MapProviderError(string providerCategory, string providerId, string correlationId)
        {
            var normalizedProviderId = ContractGuard.Id(providerId, nameof(providerId));
            var normalizedCorrelationId = ContractGuard.Id(correlationId, nameof(correlationId));
            if (string.IsNullOrWhiteSpace(providerCategory))
            {
                return new ProviderErrorMapping("Unknown", normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.InternalFailure, false, false);
            }

            var category = providerCategory.Trim();
            switch (category)
            {
                case "InvalidRequest": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.InvalidRequest, false, false);
                case "Authentication":
                case "Forbidden":
                case "RedirectRejected":
                case "PolicyDenied": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.Denied, false, false);
                case "NotFound": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.NotFound, false, false);
                case "Conflict": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.Conflict, false, false);
                case "RateLimited": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.RateLimited, true, true);
                case "Timeout": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.Timeout, false, false);
                case "Unavailable": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.Unavailable, true, false);
                case "ServerUnavailable":
                case "TransportUnavailable": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.Unavailable, true, true);
                case "MalformedResponse":
                case "IncompleteStream": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.ProviderFailure, false, false);
                case "Cancelled": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.Cancelled, false, false);
                case "Unsupported": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.Unsupported, false, false);
                case "ResourceExhausted": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.ResourceExhausted, false, false);
                case "CorruptCredential": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.RecoveryRequired, false, false);
                case "InternalFailure": return Mapping(category, normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.InternalFailure, false, false);
                default: return new ProviderErrorMapping("Unknown", normalizedProviderId, normalizedCorrelationId, FrameworkErrorCategory.InternalFailure, false, false);
            }
        }

        private static ProviderErrorMapping Mapping(string providerCategory, string providerId, string correlationId, FrameworkErrorCategory coreCategory, bool retryable, bool fallbackAllowed)
        {
            return new ProviderErrorMapping(providerCategory, providerId, correlationId, coreCategory, retryable, fallbackAllowed);
        }
    }

    public sealed class OperationResult<T>
    {
        private OperationResult(bool isSuccess, T value, FrameworkError error)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
        }

        public bool IsSuccess { get; }
        public T Value { get; }
        public FrameworkError Error { get; }
        public static OperationResult<T> Succeeded(T value) => new OperationResult<T>(true, value, null);
        public static OperationResult<T> Failed(FrameworkError error) => new OperationResult<T>(false, default(T), error ?? throw new ArgumentNullException(nameof(error)));
    }
}
