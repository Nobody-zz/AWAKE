using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MarcusAwakeProvider.Tests;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 0)
        {
            if (args.Length != 1 || !StringComparer.Ordinal.Equals(args[0], "--p3d-a0-provider")) return 1;
            return await RunProviderContractMappingAsync().ConfigureAwait(false);
        }

        var tests = new (string Name, Func<Task> Run)[]
        {
            ("profile_validation_rejects_unsafe_inputs_without_http", ProviderTests.ProfileValidationRejectsUnsafeInputsWithoutHttpAsync),
            ("local_and_cloud_profile_contract", ProviderTests.LocalAndCloudProfileContractAsync),
            ("provider_kind_codec_and_adapter_factory", ProviderTests.ProviderKindCodecAndAdapterFactoryAsync),
            ("policy_rejection_happens_before_http", ProviderTests.PolicyRejectionHappensBeforeHttpAsync),
            ("credential_redaction_and_disposal", ProviderTests.CredentialRedactionAndDisposalAsync),
            ("in_memory_credential_store_round_trip", ProviderTests.InMemoryCredentialStoreRoundTripAsync),
            ("protected_file_store_round_trip_and_corruption", ProviderTests.ProtectedFileStoreRoundTripAndCorruptionAsync),
            ("openai_models_and_capability_report", ProviderTests.OpenAiModelsAndCapabilityReportAsync),
            ("anthropic_models_and_headers", ProviderTests.AnthropicModelsAndHeadersAsync),
            ("ollama_models_without_auth", ProviderTests.OllamaModelsWithoutAuthAsync),
            ("typed_http_failures_and_retry_after", ProviderTests.TypedHttpFailuresAndRetryAfterAsync),
            ("openai_completion_and_structured_output", ProviderTests.OpenAiCompletionAndStructuredOutputAsync),
            ("structured_response_must_be_valid_json", ProviderTests.StructuredResponseMustBeValidJsonAsync),
            ("bounded_response_rejects_content_length_and_compression", ProviderTests.BoundedResponseRejectsContentLengthAndCompressionAsync),
            ("openai_sse_orders_deltas_usage_and_completion", ProviderTests.OpenAiSseOrdersDeltasUsageAndCompletionAsync),
            ("sse_requires_done_and_rejects_malformed_frames", ProviderTests.SseRequiresDoneAndRejectsMalformedFramesAsync),
            ("cancellation_and_deadline_are_typed", ProviderTests.CancellationAndDeadlineAreTypedAsync),
            ("router_falls_back_once_and_skips_duplicates", ProviderTests.RouterFallsBackOnceAndSkipsDuplicatesAsync),
            ("router_stream_fallback_is_only_before_visible_output", ProviderTests.RouterStreamFallbackIsOnlyBeforeVisibleOutputAsync),
            ("redirect_is_rejected_and_never_followed", ProviderTests.RedirectIsRejectedAndNeverFollowedAsync),

            // 生图（片 1）：形状与鉴权
            ("player2_kind_codec_and_factory", ProviderTests.Player2KindCodecAndFactoryAsync),
            ("player2_image_generate_and_edit_paths", ProviderTests.Player2ImageGenerateAndEditPathsAsync),
            ("player2_image_idempotency_key_when_provided", ProviderTests.Player2ImageIdempotencyKeyIsSentWhenProvidedAsync),
            ("player2_image_authorization_follows_cloud", ProviderTests.Player2ImageAuthorizationFollowsCloudNotShapeAsync),
            ("player2_chat_operations_report_unsupported", ProviderTests.Player2ChatOperationsReportUnsupportedAsync),
            // 生图（片 1）：两条「改坏就会红」的判据
            ("image_payload_data_uri_prefix_stripped_exactly", ProviderTests.ImagePayloadDataUriPrefixIsStrippedExactlyAsync),
            ("image_media_type_follows_bytes_not_declared_mime", ProviderTests.ImageMediaTypeFollowsBytesNotDeclaredMimeAsync),
            ("image_media_sniffing_and_dimensions", ProviderTests.ImageMediaSniffingAndDimensionsAsync),
            // 生图（片 1）：限额、坏载荷、HTTP 失败
            ("image_asset_limit_enforced", ProviderTests.ImageAssetLimitIsEnforcedAsync),
            ("image_request_limit_enforced_before_http", ProviderTests.ImageRequestLimitIsEnforcedBeforeHttpAsync),
            ("image_bad_payloads_are_typed_failures", ProviderTests.ImageBadPayloadsAreTypedFailuresAsync),
            ("image_http_failure_mapped_not_swallowed", ProviderTests.ImageHttpFailureIsMappedNotSwallowedAsync),
            // 生图（片 1）：OpenAI 兼容那条路
            ("openai_image_uses_generations_path_and_b64", ProviderTests.OpenAiImageUsesGenerationsPathAndB64Async),
            ("openai_image_url_only_rejected_never_followed", ProviderTests.OpenAiImageUrlOnlyIsRejectedAndNeverFollowedAsync),
            // 生图（片 1）：路由按能力过滤
            ("router_skips_non_image_adapters", ProviderTests.RouterSkipsNonImageAdaptersAsync),
            ("router_reports_unsupported_without_image_candidate", ProviderTests.RouterReportsUnsupportedWhenNothingCanGenerateAsync),
        };

        var failures = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run().ConfigureAwait(false);
                Console.WriteLine("PASS " + test.Name);
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine("FAIL " + test.Name + " :: " + exception.Message);
            }
        }

        Console.WriteLine(failures == 0 ? "PASS ALL" : "FAILURES=" + failures);
        return failures == 0 ? 0 : 1;
    }

    private static async Task<int> RunProviderContractMappingAsync()
    {
        try
        {
            await ProviderContractMappingTests.RunAsync().ConfigureAwait(false);
            Console.Write("PASS_COUNT=1\nFAIL_COUNT=0\nHTTP_REQUEST_COUNT=0\nEXTERNAL_NETWORK=false\n");
            return 0;
        }
        catch (Exception)
        {
            return 1;
        }
    }
}
