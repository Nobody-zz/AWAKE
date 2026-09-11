using MarcusAwakeFramework.Api;
using MarcusAwakeProvider;
using MarcusAwakeTransport;
using System;
using System.Text;
using System.Threading.Tasks;

namespace MarcusAwakeProvider.Tests;

internal static class ProviderContractMappingTests
{
    private static readonly (FrameworkErrorCategory CoreCategory, bool Retryable, bool FallbackAllowed)[] ExpectedMappings =
    {
        (FrameworkErrorCategory.InvalidRequest, false, false),
        (FrameworkErrorCategory.Denied, false, false),
        (FrameworkErrorCategory.Denied, false, false),
        (FrameworkErrorCategory.NotFound, false, false),
        (FrameworkErrorCategory.Conflict, false, false),
        (FrameworkErrorCategory.RateLimited, true, true),
        (FrameworkErrorCategory.Timeout, false, false),
        (FrameworkErrorCategory.Unavailable, true, false),
        (FrameworkErrorCategory.Unavailable, true, true),
        (FrameworkErrorCategory.Unavailable, true, true),
        (FrameworkErrorCategory.Denied, false, false),
        (FrameworkErrorCategory.Denied, false, false),
        (FrameworkErrorCategory.ProviderFailure, false, false),
        (FrameworkErrorCategory.ProviderFailure, false, false),
        (FrameworkErrorCategory.Cancelled, false, false),
        (FrameworkErrorCategory.Unsupported, false, false),
        (FrameworkErrorCategory.ResourceExhausted, false, false),
        (FrameworkErrorCategory.RecoveryRequired, false, false),
        (FrameworkErrorCategory.InternalFailure, false, false)
    };

    internal static Task RunAsync()
    {
        var providerCategories = Enum.GetValues<ProviderErrorCategory>();
        var wireCategories = ProviderProtocolContract.WireErrorCategories;
        TestSupport.Assert(providerCategories.Length == ExpectedMappings.Length, "provider_enum_count_changed");
        TestSupport.Assert(wireCategories.Count == providerCategories.Length, "provider_wire_vocabulary_count_mismatch");

        for (var index = 0; index < providerCategories.Length; index++)
        {
            var providerCategory = providerCategories[index];
            var providerCategoryName = providerCategory.ToString();
            var expectedWireCategory = ToWireCategory(providerCategoryName);
            TestSupport.Assert(StringComparer.Ordinal.Equals(wireCategories[index], expectedWireCategory), "provider_wire_category_mismatch:" + providerCategoryName);

            var mapping = FrameworkErrors.MapProviderError(providerCategoryName, " provider.example ", " correlation.example ");
            var expected = ExpectedMappings[index];
            TestSupport.Assert(StringComparer.Ordinal.Equals(mapping.ProviderCategory, providerCategoryName), "provider_mapping_category_mismatch:" + providerCategoryName);
            TestSupport.Assert(StringComparer.Ordinal.Equals(mapping.ProviderId, "provider.example"), "provider_mapping_provider_id_mismatch:" + providerCategoryName);
            TestSupport.Assert(StringComparer.Ordinal.Equals(mapping.CorrelationId, "correlation.example"), "provider_mapping_correlation_id_mismatch:" + providerCategoryName);
            TestSupport.Assert(mapping.CoreCategory == expected.CoreCategory, "provider_mapping_core_category_mismatch:" + providerCategoryName);
            TestSupport.Assert(mapping.Retryable == expected.Retryable, "provider_mapping_retryable_mismatch:" + providerCategoryName);
            TestSupport.Assert(mapping.FallbackAllowed == expected.FallbackAllowed, "provider_mapping_fallback_mismatch:" + providerCategoryName);
        }

        AssertUnknownMapping(null, "unknown_category_null_input");
        AssertUnknownMapping("   ", "unknown_category_blank_input");
        AssertUnknownMapping("invalidrequest", "unknown_category_case_mismatch");
        return Task.CompletedTask;
    }

    private static void AssertUnknownMapping(string? providerCategory, string caseId)
    {
        var mapping = FrameworkErrors.MapProviderError(providerCategory!, "provider.example", "correlation.example");
        TestSupport.Assert(StringComparer.Ordinal.Equals(mapping.ProviderCategory, "Unknown"), caseId + ":provider_category");
        TestSupport.Assert(mapping.CoreCategory == FrameworkErrorCategory.InternalFailure, caseId + ":core_category");
        TestSupport.Assert(!mapping.Retryable, caseId + ":retryable");
        TestSupport.Assert(!mapping.FallbackAllowed, caseId + ":fallback_allowed");
    }

    private static string ToWireCategory(string providerCategoryName)
    {
        var builder = new StringBuilder(providerCategoryName.Length + 8);
        for (var index = 0; index < providerCategoryName.Length; index++)
        {
            var character = providerCategoryName[index];
            if (index > 0 && char.IsUpper(character)) builder.Append('_');
            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
