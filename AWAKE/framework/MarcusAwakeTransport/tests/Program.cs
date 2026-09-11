using MarcusAwakeTransport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeTransport.Tests;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args != null && args.Length > 0)
        {
            if (args.Length != 1) return 1;
            if (StringComparer.Ordinal.Equals(args[0], "--p3d-a0-contract")) return await RunProviderContractAsync().ConfigureAwait(false);
            if (StringComparer.Ordinal.Equals(args[0], "--p3d-a1-contract")) return await RunProviderResponseContractAsync().ConfigureAwait(false);
            return 1;
        }

        var cases = new (string Name, Func<Task> Run)[]
        {
            ("protocol_identity_and_pipe", TestProtocolIdentityAndPipeAsync),
            ("strict_codec_round_trip", TestStrictCodecRoundTripAsync),
            ("envelope_round_trip_and_validation", TestEnvelopeRoundTripAsync),
            ("invalid_json_and_unknown_field", TestInvalidJsonAsync),
            ("frame_io_round_trip", TestFrameIoAsync),
            ("sequence_window_ordering", TestSequenceWindowAsync),
            ("security_hash_and_key_derivation", TestSecurityAsync)
        };

        var passed = 0;
        var failed = 0;
        foreach (var testCase in cases)
        {
            try
            {
                await testCase.Run().ConfigureAwait(false);
                passed++;
                Console.WriteLine("PASS " + testCase.Name);
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine("FAIL " + testCase.Name + " " + exception.Message);
            }
        }

        Console.WriteLine("PASS_COUNT=" + passed.ToString());
        Console.WriteLine("FAIL_COUNT=" + failed.ToString());
        return failed == 0 ? 0 : 1;
    }

    private static Task TestProtocolIdentityAndPipeAsync()
    {
        var sidFingerprint = TransportSecurity.SidFingerprint("S-1-5-21-marcus-awake-test");
        var pipeName = ProtocolValidation.BuildPipeName(sidFingerprint);
        Require(ProtocolValidation.ValidatePipeName(pipeName, sidFingerprint).IsAccepted, "valid_pipe_rejected");
        Require(ProtocolValidation.ValidateProtocolIdentity(
            ProtocolConstants.ProtocolId,
            ProtocolConstants.ProtocolMajor,
            ProtocolConstants.ProtocolMinor,
            ProtocolConstants.FrameworkApiMajor,
            ProtocolConstants.ServiceId).IsAccepted, "valid_identity_rejected");
        Require(ProtocolValidation.ValidateProtocolIdentity(
            "MarcusAIFramework.Companion.v1",
            ProtocolConstants.ProtocolMajor,
            ProtocolConstants.ProtocolMinor,
            ProtocolConstants.FrameworkApiMajor,
            ProtocolConstants.ServiceId).ErrorCode == "legacy_protocol_rejected", "legacy_protocol_not_rejected");
        Require(ProtocolValidation.ValidatePipeName("MarcusAIFramework.Companion.v1").ErrorCode == "legacy_pipe_rejected", "legacy_pipe_not_rejected");
        return Task.CompletedTask;
    }

    private static Task TestStrictCodecRoundTripAsync()
    {
        var sidFingerprint = TransportSecurity.SidFingerprint("S-1-5-21-marcus-awake-test");
        var bootstrap = new BootstrapDescriptor
        {
            BannerlordApiVersion = "1.3.15",
            ClientInstanceId = "client-instance",
            LaunchTransactionId = "launch-transaction",
            LaunchNonce = "launch-nonce",
            ChallengeId = "challenge",
            SessionNonce = "session-nonce",
            UserSidFingerprint = sidFingerprint,
            ParentProcessId = 100,
            ParentStartUnixMilliseconds = 200,
            InstanceEpoch = 1,
            AuthKeyId = "bootstrap-v1",
            ExpectedServiceArtifactSha256 = new string('a', 64),
            PipeName = ProtocolValidation.BuildPipeName(sidFingerprint),
            ClientBootstrapProof = "client-proof",
            RequestedCapabilities = new[] { ProtocolConstants.CapabilityStorageRead }
        };

        var json = ProtocolCodec.SerializeBootstrap(bootstrap);
        Require(ProtocolCodec.TryParseBootstrap(json, out var parsed, out var error), "bootstrap_parse_failed:" + error);
        Require(parsed.ClientInstanceId == bootstrap.ClientInstanceId, "bootstrap_client_instance_mismatch");
        Require(parsed.RequestedCapabilities.Length == 1 && parsed.RequestedCapabilities[0] == ProtocolConstants.CapabilityStorageRead, "bootstrap_capability_mismatch");
        return Task.CompletedTask;
    }

    private static Task TestEnvelopeRoundTripAsync()
    {
        var envelope = CreateTaskEnvelope(ProtocolConstants.MessageTypeEcho, "echo-message", 1, "{\"value\":\"hello\"}");
        var json = ProtocolCodec.SerializeEnvelope(envelope);
        Require(ProtocolCodec.TryParseEnvelope(json, out var parsed, out var error), "envelope_parse_failed:" + error);
        Require(parsed.PayloadJson == "{\"value\":\"hello\"}", "payload_not_canonicalized");
        Require(parsed.TaskScope != null && parsed.TaskScope.TaskId == "task-1", "task_scope_missing");
        Require(ProtocolValidation.ValidateEnvelope(parsed).IsAccepted, "parsed_envelope_invalid");

        Require(StructuredJsonCanonicalizer.TryCanonicalizeObject("{\"value\":{\"nested\":true}}", out var canonicalStructuredJson, out var structuredError), "structured_json_canonicalization_failed:" + structuredError);
        Require(canonicalStructuredJson == "{\"value\":{\"nested\":true}}", "structured_json_canonicalization_mismatch:" + canonicalStructuredJson);

        var noFence = CreateTaskEnvelope(ProtocolConstants.MessageTypeEcho, "echo-no-fence", 1, "{\"value\":\"x\"}");
        noFence.CampaignGuid = string.Empty;
        noFence.TimelineId = string.Empty;
        noFence.SessionId = string.Empty;
        noFence.SessionGeneration = 0;
        noFence.OwnerId = string.Empty;
        noFence.TaskScope = null;
        try
        {
            ProtocolCodec.SerializeEnvelope(noFence);
        }
        catch (ArgumentException exception)
        {
            Require(exception.Message.StartsWith("missing_session_fence", StringComparison.Ordinal), "missing_fence_error_mismatch:" + exception.Message);
            return Task.CompletedTask;
        }

        throw new InvalidOperationException("missing_fence_not_rejected");
    }

    private static Task TestInvalidJsonAsync()
    {
        Require(!ProtocolCodec.TryParseBootstrap("{\"protocol_id\":\"marcus-awake-runtime\",\"protocol_id\":\"duplicate\"}", out _, out _), "duplicate_key_accepted");
        Require(!ProtocolCodec.TryParseBootstrap("{\"unknown\":true}", out _, out var error), "unknown_field_accepted");
        Require(error == "unknown_unknown", "unknown_field_error_mismatch:" + error);
        return Task.CompletedTask;
    }

    private static async Task TestFrameIoAsync()
    {
        const string json = "{\"message\":\"hello\",\"value\":1}";
        using (var stream = new MemoryStream())
        {
            await PipeFrameIO.WriteFrameAsync(stream, json, CancellationToken.None).ConfigureAwait(false);
            stream.Position = 0;
            var decoded = await PipeFrameIO.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
            Require(decoded == json, "frame_round_trip_mismatch");
        }

        var frame = PipeFrameIO.EncodeFrame(json);
        Require(PipeFrameIO.DecodeFrame(frame) == json, "frame_decode_mismatch");
        frame[0] = 0xff;
        frame[1] = 0xff;
        frame[2] = 0xff;
        frame[3] = 0x7f;
        ExpectInvalidData(() => PipeFrameIO.DecodeFrame(frame), "invalid_length_not_rejected");
    }

    private static Task TestSequenceWindowAsync()
    {
        var window = new SequenceWindow("session", 1, "nonce", 4);
        Require(window.Evaluate("session", 1, "nonce", 1, "message-1", "hash-1").Kind == SequenceDecisionKind.Accepted, "sequence_one_not_accepted");
        Require(window.Evaluate("session", 1, "nonce", 1, "message-1", "hash-1").Kind == SequenceDecisionKind.Duplicate, "duplicate_not_detected");
        Require(window.Evaluate("session", 1, "nonce", 3, "message-3", "hash-3").Kind == SequenceDecisionKind.SequenceOutOfOrder, "out_of_order_not_detected");
        Require(window.Evaluate("session", 1, "nonce", 2, "message-2", "hash-2").Kind == SequenceDecisionKind.Accepted, "sequence_two_not_accepted");
        Require(window.LastAccepted == 2, "future_sequence_polluted_watermark");
        Require(window.Evaluate("session", 1, "nonce", 3, "message-3", "hash-3").Kind == SequenceDecisionKind.Accepted, "future_sequence_retry_not_accepted");
        Require(window.LastAccepted == 3, "watermark_not_advanced");
        Require(window.Evaluate("session", 1, "wrong", 4, "message-4", "hash-4").Kind == SequenceDecisionKind.NonceMismatch, "nonce_mismatch_not_detected");
        Require(window.Evaluate("session", 1, "nonce", 10, "message-10", "hash-10").Kind == SequenceDecisionKind.SequenceGap, "sequence_gap_not_detected");
        Require(window.Evaluate("session", 1, "nonce", 1, "message-other", "hash-other").Kind == SequenceDecisionKind.ReplayRejected, "conflicting_replay_not_detected");
        return Task.CompletedTask;
    }

    private static async Task<int> RunProviderContractAsync()
    {
        var cases = new (string Name, Func<Task> Run)[]
        {
            ("P3D-A0-T01-versioned_and_capability_matrix", TestVersionedAndCapabilityMatrixAsync),
            ("P3D-A0-T02-output_schema_matrix", TestOutputSchemaMatrixAsync),
            ("P3D-A0-T03-header_only_opaque_payload", TestHeaderOnlyOpaquePayloadAsync),
            ("P3D-A0-T04-sequence_retry_non_contaminating", TestSequenceRetryNonContaminatingAsync),
            ("P3D-A0-T05-stream_usage_closed_set", TestStreamUsageClosedSetAsync),
            ("P3D-A0-T06-stream_error_category_closed_set", TestStreamErrorCategoryClosedSetAsync)
        };
        var passed = 0;
        var failed = 0;
        foreach (var testCase in cases)
        {
            try
            {
                await testCase.Run().ConfigureAwait(false);
                passed++;
                Console.WriteLine("PASS " + testCase.Name);
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine(testCase.Name + ":" + exception.Message);
            }
        }

        Console.WriteLine("PASS_COUNT=" + passed.ToString());
        Console.WriteLine("FAIL_COUNT=" + failed.ToString());
        return failed == 0 ? 0 : 1;
    }

    private static async Task<int> RunProviderResponseContractAsync()
    {
        try
        {
            await TestProviderResponseMessageMatrixAsync().ConfigureAwait(false);
            Console.WriteLine("PASS P3D-A1-T01-provider_response_message_matrix");
            Console.WriteLine("PASS_COUNT=1");
            Console.WriteLine("FAIL_COUNT=0");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("P3D-A1-T01-provider_response_message_matrix:" + exception.Message);
            Console.WriteLine("PASS_COUNT=0");
            Console.WriteLine("FAIL_COUNT=1");
            return 1;
        }
    }

    private static Task TestVersionedAndCapabilityMatrixAsync()
    {
        var expected = new (string MessageType, string Capability)[]
        {
            ("provider.profile_upsert.v1", "provider.configure.v1"),
            ("provider.profile_remove.v1", "provider.configure.v1"),
            ("provider.models.v1", "provider.models.v1"),
            ("provider.complete.v1", "provider.complete.v1"),
            ("provider.stream.v1", "provider.stream.v1")
        };

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < expected.Length; index++)
        {
            var item = expected[index];
            Require(ProviderProtocolContract.TryGetRequest(item.MessageType, out var contract), "request_missing:" + item.MessageType);
            Require(seen.Add(contract.MessageType), "request_duplicate:" + contract.MessageType);
            Require(StringComparer.Ordinal.Equals(contract.MessageType, item.MessageType), "request_id_mismatch:" + item.MessageType);
            Require(StringComparer.Ordinal.Equals(contract.RequiredCapability, item.Capability), "capability_mismatch:" + item.MessageType);
            Require(contract.RequiresTaskScope, "task_scope_not_required:" + item.MessageType);
        }

        Require(seen.Count == expected.Length, "request_matrix_size_mismatch");
        Require(!ProviderProtocolContract.IsProviderRequest("provider.complete"), "unversioned_provider_request_accepted");
        return Task.CompletedTask;
    }

    private static Task TestProviderResponseMessageMatrixAsync()
    {
        var cases = new (string MessageType, string Schema, string Payload)[]
        {
            (ProtocolConstants.MessageTypeProviderProfileResult, ProtocolConstants.ProviderProfileResultSchemaV1, "{\"schema\":\"marcus-awake.provider.profile_result.v1\",\"operation\":\"upsert\",\"profile_id\":\"profile.none\",\"provider_id\":\"provider.none\",\"route_id\":\"route.echo\",\"status\":\"ready\"}"),
            (ProtocolConstants.MessageTypeProviderModelsResult, ProtocolConstants.ProviderModelsResultSchemaV1, "{\"schema\":\"marcus-awake.provider.models_result.v1\",\"profile_id\":\"profile.none\",\"provider_id\":\"provider.none\",\"route_id\":\"route.echo\",\"models\":[],\"capabilities\":{}}"),
            (ProtocolConstants.MessageTypeProviderResult, ProtocolConstants.ProviderResultSchemaV1, "{\"schema\":\"marcus-awake.provider.result.v1\",\"profile_id\":\"profile.none\",\"provider_id\":\"provider.none\",\"route_id\":\"route.echo\",\"model_id\":\"model.none\",\"content\":\"reply\"}")
        };

        for (var index = 0; index < cases.Length; index++)
        {
            var item = cases[index];
            Require(ProviderProtocolContract.IsProviderResponse(item.MessageType), "provider_response_not_recognized:" + item.MessageType);
            var envelope = CreateTaskEnvelope(item.MessageType, "provider-response-" + index.ToString(), 1, item.Payload);
            envelope.PayloadSchema = item.Schema;
            envelope.AckStatus = ProtocolConstants.AckAccepted;
            envelope.OutcomeKind = ProtocolConstants.OutcomeAccepted;
            envelope.NonDurable = true;
            var encoded = ProtocolCodec.SerializeEnvelope(envelope);
            Require(ProtocolCodec.TryParseEnvelope(encoded, out var parsed, out var error), "provider_response_round_trip_failed:" + item.MessageType + ":" + error);
            Require(parsed.MessageType == item.MessageType && parsed.PayloadSchema == item.Schema, "provider_response_round_trip_mismatch:" + item.MessageType);
        }

        Require(!ProviderProtocolContract.IsProviderResponse("provider_result.v1"), "provider_response_versioned_near_miss_accepted");
        Require(!ProviderProtocolContract.IsProviderResponse("provider_result_extra"), "provider_response_extra_accepted");
        Require(!ProviderProtocolContract.IsProviderResponse("PROVIDER_RESULT"), "provider_response_case_variant_accepted");
        return Task.CompletedTask;
    }

    private static Task TestOutputSchemaMatrixAsync()
    {
        var expected = new (string MessageType, string RequestSchema, string OutputSchema)[]
        {
            ("provider.profile_upsert.v1", "marcus-awake.provider.profile_upsert.v1", "marcus-awake.provider.profile_result.v1"),
            ("provider.profile_remove.v1", "marcus-awake.provider.profile_remove.v1", "marcus-awake.provider.profile_result.v1"),
            ("provider.models.v1", "marcus-awake.provider.models.v1", "marcus-awake.provider.models_result.v1"),
            ("provider.complete.v1", "marcus-awake.provider.complete.v1", "marcus-awake.provider.result.v1"),
            ("provider.stream.v1", "marcus-awake.provider.stream.v1", "marcus-awake.provider.stream_event.v1")
        };

        for (var index = 0; index < expected.Length; index++)
        {
            var item = expected[index];
            Require(ProviderProtocolContract.TryGetRequest(item.MessageType, out var contract), "output_request_missing:" + item.MessageType);
            Require(StringComparer.Ordinal.Equals(contract.RequestSchema, item.RequestSchema), "request_schema_mismatch:" + item.MessageType);
            Require(StringComparer.Ordinal.Equals(contract.OutputSchema, item.OutputSchema), "output_schema_mismatch:" + item.MessageType);
            Require(contract.OutputSchemaMajor == 1 && contract.OutputSchemaMinor == 0, "output_schema_version_mismatch:" + item.MessageType);
        }

        return Task.CompletedTask;
    }

    private static Task TestHeaderOnlyOpaquePayloadAsync()
    {
        var payload = "{ \"api_key\":123, \"messages\":\"wrong-type\", \"opaque\": [ true, null ] }";
        var json = "{\"message_type\":\"provider.complete.v1\",\"payload\":" + payload + "}";
        Require(HeaderOnlyParser.TryParse(json, out var parsed, out var error), "header_only_parse_failed:" + error);
        Require(parsed.HasField("payload") && parsed.HasField("message_type"), "header_only_fields_missing");
        Require(StringComparer.Ordinal.Equals(parsed.Envelope.PayloadJson, payload), "opaque_payload_token_changed");
        Require(parsed.Envelope.MessageType == "provider.complete.v1", "header_message_type_changed");
        return Task.CompletedTask;
    }

    private static Task TestSequenceRetryNonContaminatingAsync()
    {
        var window = new SequenceWindow("session", 1, "nonce", 4);
        Require(window.Evaluate("session", 1, "nonce", 1, "one", "hash-one").Kind == SequenceDecisionKind.Accepted, "sequence_one_rejected");
        var future = window.Evaluate("session", 1, "nonce", 3, "three", "hash-three");
        Require(future.Kind == SequenceDecisionKind.SequenceOutOfOrder, "future_reject_no_record_not_rejected");
        Require(window.LastAccepted == 1 && window.ExpectedNext == 2, "future_reject_no_record_polluted_window");

        var gapFill = window.Evaluate("session", 1, "nonce", 2, "two", "hash-two");
        Require(gapFill.Kind == SequenceDecisionKind.Accepted, "gap_fill_retry_accept_missing");
        var retry = window.Evaluate("session", 1, "nonce", 3, "three", "hash-three");
        Require(retry.Kind == SequenceDecisionKind.Accepted, "gap_fill_retry_accept_rejected");
        return Task.CompletedTask;
    }

    private static Task TestStreamUsageClosedSetAsync()
    {
        const int maximumTokensPerField = 16777216;
        const int maximumTotalTokens = 33554432;

        Require(ProviderProtocolContract.IsValidUsage(0, 0), "usage_zero_zero_rejected");
        Require(ProviderProtocolContract.IsValidUsage(maximumTokensPerField, 0), "usage_input_upper_bound_rejected");
        Require(ProviderProtocolContract.IsValidUsage(0, maximumTokensPerField), "usage_output_upper_bound_rejected");
        Require(ProviderProtocolContract.IsValidUsage(maximumTokensPerField, maximumTokensPerField), "usage_total_upper_bound_rejected");
        Require(!ProviderProtocolContract.IsValidUsage(-1, 0), "negative_input_accepted");
        Require(!ProviderProtocolContract.IsValidUsage(0, -1), "negative_output_accepted");
        Require(!ProviderProtocolContract.IsValidUsage(maximumTokensPerField + 1, 0), "input_overflow_accepted");
        Require(!ProviderProtocolContract.IsValidUsage(0, maximumTokensPerField + 1), "output_overflow_accepted");
        Require(!ProviderProtocolContract.IsValidUsage(maximumTokensPerField, maximumTotalTokens - maximumTokensPerField + 1), "total_overflow_accepted");

        var stream = new ProviderStreamStateMachine("stream", "provider", "profile");
        Require(stream.Accept("started", 1, "provider").IsAccepted, "stream_start_rejected");
        Require(stream.Accept("usage_update", 2, "provider", hasUsage: true).IsAccepted, "usage_update_rejected");
        var invalidUsage = stream.Accept("usage_update", 3, "provider", hasUsage: false);
        Require(!invalidUsage.IsAccepted && invalidUsage.ErrorCode == "stream_usage_update_invalid", "usage_shape_not_rejected");
        Require(stream.NextSequence == 3, "rejected_usage_advanced_sequence");
        return Task.CompletedTask;
    }

    private static Task TestStreamErrorCategoryClosedSetAsync()
    {
        var expected = new[]
        {
            "invalid_request",
            "authentication",
            "forbidden",
            "not_found",
            "conflict",
            "rate_limited",
            "timeout",
            "unavailable",
            "server_unavailable",
            "transport_unavailable",
            "redirect_rejected",
            "policy_denied",
            "malformed_response",
            "incomplete_stream",
            "cancelled",
            "unsupported",
            "resource_exhausted",
            "corrupt_credential",
            "internal_failure"
        };

        Require(ProviderProtocolContract.WireErrorCategories.Count == expected.Length, "error_category_count_mismatch");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < expected.Length; index++)
        {
            Require(seen.Add(expected[index]), "error_category_expected_duplicate:" + expected[index]);
            Require(StringComparer.Ordinal.Equals(ProviderProtocolContract.WireErrorCategories[index], expected[index]), "error_category_order_or_value_mismatch:" + expected[index]);
            Require(ProviderProtocolContract.IsKnownProviderErrorCategory(expected[index]), "known_error_category_rejected:" + expected[index]);
        }

        Require(!ProviderProtocolContract.IsKnownProviderErrorCategory(null), "null_error_category_accepted");
        Require(!ProviderProtocolContract.IsKnownProviderErrorCategory(string.Empty), "blank_error_category_accepted");
        Require(!ProviderProtocolContract.IsKnownProviderErrorCategory("INVALID_REQUEST"), "case_variant_error_category_accepted");
        Require(!ProviderProtocolContract.IsKnownProviderErrorCategory("unknown"), "unknown_error_category_accepted");
        return Task.CompletedTask;
    }

    private sealed class ControlledSink : IFrameWriteSink
    {
        internal ControlledSink(int writeResult, bool throwWrite, bool throwFlush)
        {
            WriteResult = writeResult;
            ThrowWrite = throwWrite;
            ThrowFlush = throwFlush;
        }

        internal int WriteResult { get; set; }
        internal bool ThrowWrite { get; }
        internal bool ThrowFlush { get; }
        internal int WriteCalls { get; private set; }

        public Task<int> WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            WriteCalls++;
            if (ThrowWrite) throw new IOException("controlled_write_failure");
            return Task.FromResult(WriteResult);
        }

        public Task FlushAsync(CancellationToken cancellationToken)
        {
            if (ThrowFlush) throw new IOException("controlled_flush_failure");
            return Task.CompletedTask;
        }
    }

    private static Task TestSecurityAsync()
    {
        var key = Encoding.UTF8.GetBytes("test-key");
        var hash = TransportSecurity.Sha256Hex("payload");
        Require(hash.Length == 64, "sha256_length_invalid");
        var first = TransportSecurity.HmacSha256Hex(key, "test-domain", "one", "two");
        var second = TransportSecurity.HmacSha256Hex(key, "test-domain", "one", "two");
        Require(first == second && TransportSecurity.FixedTimeEquals(first, second), "hmac_not_deterministic");
        var derived = TransportSecurity.DeriveAuthKey(new byte[32], "launch", "domain", "transaction", "key-id");
        Require(derived.Length == 32, "derived_key_length_invalid");
        return Task.CompletedTask;
    }

    private static PipeEnvelope CreateTaskEnvelope(string messageType, string messageId, long sequence, string payload)
    {
        return new PipeEnvelope
        {
            MessageType = messageType,
            MessageId = messageId,
            CorrelationId = "correlation-1",
            CampaignGuid = "campaign-1",
            TimelineId = "timeline-1",
            SessionId = "session-1",
            SessionGeneration = 1,
            OwnerId = "owner-1",
            DeadlineUnixMilliseconds = 2000000000000,
            InstanceEpoch = 1,
            ConnectionEpoch = 1,
            DirectionNonce = "direction-nonce",
            Sequence = sequence,
            PayloadSchema = "echo.request.v1",
            PayloadJson = payload,
            TaskScope = new TaskScopeEnvelope
            {
                TaskId = "task-1",
                MessageId = messageId,
                OwnerId = "owner-1",
                RouteId = "route.echo",
                ProviderId = "provider.none",
                ProfileId = "profile.none",
                IdempotencyKey = "idempotency-1",
                RequestPayloadHash = "request-hash",
                OutputSchemaId = "echo.response",
                OutputSchemaMajor = 1,
                OutputSchemaMinor = 0,
                SettlementRequirement = "not_applicable"
            }
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void ExpectInvalidData(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
