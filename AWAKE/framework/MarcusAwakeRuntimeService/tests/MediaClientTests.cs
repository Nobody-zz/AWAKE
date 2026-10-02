using MarcusAwakeFramework.Api;
using MarcusAwakeTransport;
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeRuntimeService.Tests;

/// <summary>
/// End-to-end gate for the governed media surface: a real framework client, a real Runtime Service
/// process, and a fake provider HTTP endpoint on loopback. The point is to prove the whole chain —
/// route resolution, provider call, CAS import, typed result — and that the bytes land in the asset
/// store rather than in the frame.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class MediaClientTests
{
    // A real 1x1 PNG. The asset store sniffs magic bytes, so the fake provider has to answer with an
    // actual image instead of a plausible-looking blob.
    private const string OnePixelPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private const string RouteId = "route.awake.portrait";
    private const string ProfileId = "profile.media-client";
    private const string ProviderId = "provider.media-client";
    private const string CredentialReference = "credential.media-client";
    private const string OwnerId = "media-client-harness";

    internal static async Task<int> RunAsync()
    {
        var passed = 0;
        var failed = 0;
        var servicePath = ResolveServicePath();
        var providerAssemblyPath = ResolveProviderAssemblyPath();
        // Under the test output directory rather than the system temp root: the confined sandbox
        // denies creating new directories directly under %TEMP%, and this keeps the run hermetic.
        var runRoot = Path.Combine(AppContext.BaseDirectory, "_media-client", Guid.NewGuid().ToString("N"));
        var dataRoot = Path.Combine(runRoot, "runtime");
        var credentialRoot = Path.Combine(runRoot, "credentials");
        Directory.CreateDirectory(runRoot);

        // The service is a separate process, so these have to be set before the client spawns it.
        Environment.SetEnvironmentVariable("MARCUS_AWAKE_PROVIDER_ASSEMBLY_PATH", providerAssemblyPath);
        Environment.SetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_CREDENTIAL_ROOT", credentialRoot);
        Environment.SetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_DATA_ROOT", dataRoot);

        var png = Convert.FromBase64String(OnePixelPngBase64);
        var pngHash = Sha256Hex(png);
        // Larger than a single slice: chunked readback only proves anything when the fixture cannot
        // fit inside one MaxAssetChunkBytes frame.
        var largePng = LargePngBytes(ProtocolConstants.MaxAssetChunkBytes * 3 + 12345);
        var largePngBase64 = Convert.ToBase64String(largePng);

        await using var fakeServer = new FakeProviderHttpServer();
        fakeServer.ResponseBodyOverride = "{\"data\":[{\"b64_json\":\"" + OnePixelPngBase64 + "\"}]}";

        var sessionCoordinator = new SessionCoordinator();
        var session = new SessionRef("campaign-media-client", "timeline-media-client", "session-media-client");
        var lease = sessionCoordinator.BeginSession(session);
        if (!lease.IsSuccess)
        {
            Console.Error.WriteLine("FAIL session_start_failed:" + DescribeError(lease.Error));
            return 1;
        }

        var context = new RequestContext(new ExtensionId(OwnerId), lease.Value, "media-client-correlation", DateTimeOffset.UtcNow.AddMinutes(10));
        var client = new RuntimeServiceClient(new RuntimeServiceClientOptions(servicePath: servicePath, requestedCapabilities: new[]
        {
            ProtocolConstants.MessageTypeHealth,
            ProtocolConstants.MessageTypeCancel,
            ProtocolConstants.CapabilityProviderConfigureV1,
            ProtocolConstants.CapabilityProviderCredentialsV1,
            ProtocolConstants.CapabilityProviderImageV1,
            ProtocolConstants.CapabilityAssetRead
        }));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            var started = await client.StartAsync(new RuntimeServiceStartRequest(ProtocolConstants.ServiceId, new ApiVersion(2, 0), ProtocolConstants.MaxFrameBytes), context, cancellation.Token).ConfigureAwait(false);
            if (!started.IsSuccess || !started.Value.IsReady)
            {
                Console.Error.WriteLine("FAIL client_start_failed:" + DescribeError(started.Error));
                return 1;
            }

            var credential = await client.UpsertCredentialAsync(
                new ProviderCredentialRequest(ProfileId, ProviderId, RouteId, CredentialReference, "media-client-secret-" + Guid.NewGuid().ToString("N")),
                context,
                cancellation.Token).ConfigureAwait(false);
            if (!credential.IsSuccess)
            {
                Console.Error.WriteLine("FAIL credential_upsert_failed:" + DescribeError(credential.Error));
                return 1;
            }

            var profile = await client.UpsertProfileAsync(
                new ProviderProfileRequest(ProfileId, ProviderId, RouteId, "openai_compatible", "http://127.0.0.1:" + fakeServer.Port.ToString(CultureInfo.InvariantCulture) + "/v1/", "fake-image-model", CredentialReference, true),
                context,
                cancellation.Token).ConfigureAwait(false);
            if (!profile.IsSuccess || !StringComparer.Ordinal.Equals(profile.Value.Status, "ready"))
            {
                Console.Error.WriteLine("FAIL profile_upsert_failed:" + DescribeError(profile.Error));
                return 1;
            }

            await RunCaseAsync("media_image_end_to_end_returns_handle_and_persists_object", async () =>
            {
                var outcome = await client.GenerateImageAsync(CreateRequest("a weathered mercenary captain", "media-client-1"), context, cancellation.Token).ConfigureAwait(false);
                Require(outcome.IsSuccess, "media_image_failed:" + DescribeError(outcome.Error));
                var handle = outcome.Value.Handle;
                Require(!string.IsNullOrWhiteSpace(handle.AssetId), "asset_id_missing");
                Require(StringComparer.Ordinal.Equals(handle.MediaType, "image/png"), "media_type_invalid:" + handle.MediaType);
                Require(handle.ByteLength == png.Length, "byte_length_invalid:" + handle.ByteLength.ToString(CultureInfo.InvariantCulture));
                Require(StringComparer.Ordinal.Equals(handle.ContentHash, pngHash), "content_hash_invalid:" + handle.ContentHash);
                Require(StringComparer.Ordinal.Equals(handle.OwnerExtensionId.Value, OwnerId), "owner_extension_invalid:" + handle.OwnerExtensionId.Value);
                Require(StringComparer.Ordinal.Equals(handle.LogicalKind, "image"), "logical_kind_invalid:" + handle.LogicalKind);
                Require(StringComparer.Ordinal.Equals(handle.RetentionClass, "campaign"), "retention_class_invalid:" + handle.RetentionClass);

                var objectPath = ObjectPath(dataRoot, pngHash);
                Require(File.Exists(objectPath), "cas_object_missing:" + objectPath);
                Require(File.ReadAllBytes(objectPath).Length == png.Length, "cas_object_byte_length_invalid");
                Require(fakeServer.LastRequestText.Contains("images/generations", StringComparison.Ordinal), "image_endpoint_not_used:" + FirstLine(fakeServer.LastRequestText));
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("media_image_identical_bytes_deduplicate_into_one_object", async () =>
            {
                var before = CountObjects(dataRoot);
                var outcome = await client.GenerateImageAsync(CreateRequest("a weathered mercenary captain", "media-client-2"), context, cancellation.Token).ConfigureAwait(false);
                Require(outcome.IsSuccess, "media_image_dedup_failed:" + DescribeError(outcome.Error));
                Require(StringComparer.Ordinal.Equals(outcome.Value.Handle.ContentHash, pngHash), "dedup_content_hash_changed:" + outcome.Value.Handle.ContentHash);
                Require(CountObjects(dataRoot) == before, "dedup_created_second_object:" + CountObjects(dataRoot).ToString(CultureInfo.InvariantCulture));
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("media_image_local_validation_rejects_before_any_http", async () =>
            {
                var requestsBefore = fakeServer.RequestCount;
                var outcome = await client.GenerateImageAsync(CreateRequest("   ", "media-client-3"), context, cancellation.Token).ConfigureAwait(false);
                var error = Failure(outcome, "empty_prompt_was_accepted");
                Require(StringComparer.Ordinal.Equals(error.Code, "media.image_prompt_required"), "empty_prompt_code_invalid:" + error.Code);
                Require(error.Category == FrameworkErrorCategory.InvalidRequest, "empty_prompt_category_invalid:" + error.Category.ToString());
                Require(fakeServer.RequestCount == requestsBefore, "local_rejection_still_called_provider");
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("media_image_declared_cloud_export_is_carried_not_silently_downgraded", async () =>
            {
                var requestsBefore = fakeServer.RequestCount;
                var classified = new ImageGenerationRequest(RouteId, "classified portrait prompt", null, 512, 768, 0L, "media-client-test", "campaign", "npc_persona", DateTimeOffset.UtcNow.AddMinutes(2), "media-client-4");
                var outcome = await client.GenerateImageAsync(classified, context, cancellation.Token).ConfigureAwait(false);
                // Running the gate is the caller's job, exactly like the text path: the framework must
                // not refuse a well-formed declaration, or callers would be pushed to lie with "none"
                // and the real policy would never run.
                Require(outcome.IsSuccess, "classified_export_was_refused:" + DescribeError(outcome.Error));
                Require(fakeServer.RequestCount > requestsBefore, "classified_export_never_reached_the_provider");
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("media_image_blank_cloud_export_is_rejected_before_any_http", async () =>
            {
                var requestsBefore = fakeServer.RequestCount;
                var blank = new ImageGenerationRequest(RouteId, "unclassified portrait prompt", null, 0, 0, 0L, "media-client-test", "campaign", string.Empty, DateTimeOffset.UtcNow.AddMinutes(2), "media-client-4b");
                var outcome = await client.GenerateImageAsync(blank, context, cancellation.Token).ConfigureAwait(false);
                var error = Failure(outcome, "blank_export_classification_was_accepted");
                Require(StringComparer.Ordinal.Equals(error.Code, "media.image_cloud_export_required"), "blank_export_code_invalid:" + error.Code);
                Require(error.Category == FrameworkErrorCategory.InvalidRequest, "blank_export_category_invalid:" + error.Category.ToString());
                Require(fakeServer.RequestCount == requestsBefore, "blank_export_still_called_the_provider");
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("media_image_malformed_cloud_export_is_rejected_before_any_http", async () =>
            {
                var requestsBefore = fakeServer.RequestCount;
                var malformed = new ImageGenerationRequest(RouteId, "malformed portrait prompt", null, 0, 0, 0L, "media-client-test", "campaign", "NPC Persona!", DateTimeOffset.UtcNow.AddMinutes(2), "media-client-4c");
                var outcome = await client.GenerateImageAsync(malformed, context, cancellation.Token).ConfigureAwait(false);
                var error = Failure(outcome, "malformed_export_classification_was_accepted");
                Require(StringComparer.Ordinal.Equals(error.Code, "media.image_cloud_export_invalid"), "malformed_export_code_invalid:" + error.Code);
                Require(error.Category == FrameworkErrorCategory.InvalidRequest, "malformed_export_category_invalid:" + error.Category.ToString());
                Require(fakeServer.RequestCount == requestsBefore, "malformed_export_still_called_the_provider");
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("media_image_unknown_route_fails_typed_and_imports_nothing", async () =>
            {
                var objectsBefore = CountObjects(dataRoot);
                var request = new ImageGenerationRequest("route.media-client.absent", "a portrait", null, 0, 0, 0L, "media-client-test", "campaign", "none", DateTimeOffset.UtcNow.AddMinutes(2), "media-client-5");
                var outcome = await client.GenerateImageAsync(request, context, cancellation.Token).ConfigureAwait(false);
                var error = Failure(outcome, "unknown_route_was_accepted");
                Require(!error.Code.StartsWith("media.image_", StringComparison.Ordinal), "unknown_route_failed_locally_not_at_runtime:" + error.Code);
                Require(CountObjects(dataRoot) == objectsBefore, "unknown_route_imported_an_object");
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("media_image_provider_failure_maps_to_typed_denied", async () =>
            {
                fakeServer.ResponseStatusCode = 401;
                try
                {
                    var outcome = await client.GenerateImageAsync(CreateRequest("a portrait the provider refuses", "media-client-6"), context, cancellation.Token).ConfigureAwait(false);
                    var error = Failure(outcome, "provider_denial_was_accepted");
                    Require(error.Category == FrameworkErrorCategory.Denied, "provider_denial_category_invalid:" + error.Code + "/" + error.Category.ToString());
                }
                finally
                {
                    fakeServer.ResponseStatusCode = 200;
                }

                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("media_image_player2_profile_kind_is_accepted_end_to_end", async () =>
            {
                const string player2Route = "route.awake.portrait.player2";
                const string player2Profile = "profile.media-client.player2";
                const string player2Provider = "provider.media-client.player2";
                const string player2Credential = "credential.media-client.player2";

                var credentialResult = await client.UpsertCredentialAsync(
                    new ProviderCredentialRequest(player2Profile, player2Provider, player2Route, player2Credential, "media-client-player2-secret"),
                    context,
                    cancellation.Token).ConfigureAwait(false);
                Require(credentialResult.IsSuccess, "player2_credential_failed:" + DescribeError(credentialResult.Error));

                // player2 这条形状漏过一次：适配器工厂能造它，但准入名单与框架侧的 kind 白名单
                // 都不认它 ⇒ 整条形状在协议层就被拒，工厂里那段是够不着的死代码。
                var profileResult = await client.UpsertProfileAsync(
                    new ProviderProfileRequest(player2Profile, player2Provider, player2Route, "player2", "http://127.0.0.1:" + fakeServer.Port.ToString(CultureInfo.InvariantCulture) + "/v1/", "player2-image", player2Credential, true),
                    context,
                    cancellation.Token).ConfigureAwait(false);
                Require(profileResult.IsSuccess && StringComparer.Ordinal.Equals(profileResult.Value.Status, "ready"), "player2_profile_rejected:" + DescribeError(profileResult.Error));

                fakeServer.ResponseBodyOverride = "{\"image\":\"" + OnePixelPngBase64 + "\"}";
                try
                {
                    var request = new ImageGenerationRequest(player2Route, "a player2 portrait", null, 512, 768, 0L, "media-client-test", "campaign", "none", DateTimeOffset.UtcNow.AddMinutes(2), "media-client-8");
                    var outcome = await client.GenerateImageAsync(request, context, cancellation.Token).ConfigureAwait(false);
                    Require(outcome.IsSuccess, "player2_image_failed:" + DescribeError(outcome.Error));
                    Require(StringComparer.Ordinal.Equals(outcome.Value.Handle.ContentHash, pngHash), "player2_image_hash_invalid:" + outcome.Value.Handle.ContentHash);
                    Require(StringComparer.Ordinal.Equals(outcome.Value.Handle.MediaType, "image/png"), "player2_image_media_type_invalid:" + outcome.Value.Handle.MediaType);
                    Require(fakeServer.LastRequestText.Contains("image/generate", StringComparison.Ordinal), "player2_generate_path_not_used:" + FirstLine(fakeServer.LastRequestText));
                }
                finally
                {
                    fakeServer.ResponseBodyOverride = "{\"data\":[{\"b64_json\":\"" + OnePixelPngBase64 + "\"}]}";
                }

                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("asset_read_chunked_round_trip_returns_exact_bytes", async () =>
            {
                fakeServer.ResponseBodyOverride = "{\"data\":[{\"b64_json\":\"" + largePngBase64 + "\"}]}";
                try
                {
                    var generated = await client.GenerateImageAsync(CreateRequest("a full body portrait of a mercenary captain", "media-client-7"), context, cancellation.Token).ConfigureAwait(false);
                    Require(generated.IsSuccess, "large_image_failed:" + DescribeError(generated.Error));
                    var handle = generated.Value.Handle;
                    // If the fixture ever shrinks, this case would quietly stop covering chunking.
                    Require(handle.ByteLength > ProtocolConstants.MaxAssetChunkBytes, "fixture_not_larger_than_one_chunk:" + handle.ByteLength.ToString(CultureInfo.InvariantCulture));

                    var read = await client.ReadAsync(handle.AssetId, context, cancellation.Token).ConfigureAwait(false);
                    Require(read.IsSuccess, "asset_read_failed:" + DescribeError(read.Error));

                    var content = read.Value;
                    Require(content.ByteLength == largePng.Length, "asset_read_byte_length_invalid:" + content.ByteLength.ToString(CultureInfo.InvariantCulture));
                    Require(content.Handle.ByteLength == handle.ByteLength, "asset_read_handle_length_invalid");
                    Require(StringComparer.Ordinal.Equals(content.Handle.ContentHash, handle.ContentHash), "asset_read_content_hash_invalid:" + content.Handle.ContentHash);
                    Require(StringComparer.Ordinal.Equals(content.Handle.AssetId, handle.AssetId), "asset_read_asset_id_invalid");
                    Require(StringComparer.Ordinal.Equals(content.Handle.OwnerExtensionId.Value, OwnerId), "asset_read_owner_invalid:" + content.Handle.OwnerExtensionId.Value);
                    Require(StringComparer.Ordinal.Equals(content.Handle.RetentionClass, "campaign"), "asset_read_retention_invalid:" + content.Handle.RetentionClass);

                    var readBack = content.GetContentCopy();
                    Require(StringComparer.Ordinal.Equals(Sha256Hex(readBack), handle.ContentHash), "asset_read_bytes_corrupt:" + Sha256Hex(readBack));
                    for (var index = 0; index < readBack.Length; index++)
                    {
                        if (readBack[index] != largePng[index])
                        {
                            Require(false, "asset_read_byte_mismatch_at:" + index.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                }
                finally
                {
                    fakeServer.ResponseBodyOverride = "{\"data\":[{\"b64_json\":\"" + OnePixelPngBase64 + "\"}]}";
                }

                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("asset_read_unknown_id_fails_typed_not_found", async () =>
            {
                var read = await client.ReadAsync("00000000000000000000000000000000", context, cancellation.Token).ConfigureAwait(false);
                var error = Failure(read, "unknown_asset_was_read");
                Require(StringComparer.Ordinal.Equals(error.Code, "asset.not_found"), "unknown_asset_code_invalid:" + error.Code);
                Require(error.Category == FrameworkErrorCategory.NotFound, "unknown_asset_category_invalid:" + error.Category.ToString());
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("asset_read_local_validation_rejects_blank_id", async () =>
            {
                var read = await client.ReadAsync("   ", context, cancellation.Token).ConfigureAwait(false);
                var error = Failure(read, "blank_asset_id_was_accepted");
                Require(StringComparer.Ordinal.Equals(error.Code, "asset.asset_id_required"), "blank_asset_id_code_invalid:" + error.Code);
                Require(error.Category == FrameworkErrorCategory.InvalidRequest, "blank_asset_id_category_invalid:" + error.Category.ToString());
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("asset_unimplemented_operations_fail_typed_unsupported", async () =>
            {
                RequireUnsupported(Failure(await client.ImportAsync(new AssetImportRequest(png, "image/png", "image", "media-client-test", "media-client-test", "campaign"), context, cancellation.Token).ConfigureAwait(false), "import_was_accepted"), "asset.import_unsupported");
                RequireUnsupported(Failure(await client.GetMetadataAsync("00000000000000000000000000000000", context, cancellation.Token).ConfigureAwait(false), "metadata_was_accepted"), "asset.metadata_unsupported");
                RequireUnsupported(Failure(await client.SetPinnedAsync("00000000000000000000000000000000", true, context, cancellation.Token).ConfigureAwait(false), "pin_was_accepted"), "asset.pin_unsupported");
                RequireUnsupported(Failure(await client.ListAsync(10, null, context, cancellation.Token).ConfigureAwait(false), "list_was_accepted"), "asset.list_unsupported");
                RequireUnsupported(Failure(await client.ExportAsync("00000000000000000000000000000000", context, cancellation.Token).ConfigureAwait(false), "export_was_accepted"), "asset.export_unsupported");
                RequireUnsupported(Failure(await client.DeleteAsync("00000000000000000000000000000000", context, cancellation.Token).ConfigureAwait(false), "delete_was_accepted"), "asset.delete_unsupported");
                RequireUnsupported(Failure(await client.CleanupAsync(new AssetCleanupRequest(DateTimeOffset.UtcNow, new[] { "campaign" }, 10, true), context, cancellation.Token).ConfigureAwait(false), "cleanup_was_accepted"), "asset.cleanup_unsupported");
                passed++;
            }).ConfigureAwait(false);
        }
        finally
        {
            client.Dispose();
            var closing = sessionCoordinator.BeginClosing(session);
            if (closing.IsSuccess) sessionCoordinator.CompleteDrain(session, closing.Value.Generation);
            try
            {
                Directory.Delete(runRoot, true);
            }
            catch (Exception)
            {
            }
        }

        Console.WriteLine("MEDIA_CLIENT " + passed.ToString(CultureInfo.InvariantCulture) + "/" + (passed + failed).ToString(CultureInfo.InvariantCulture) + (failed == 0 ? " PASS" : " FAIL"));
        return failed == 0 ? 0 : 1;

        async Task RunCaseAsync(string name, Func<Task> action)
        {
            Console.WriteLine("CASE " + name + " START");
            try
            {
                await action().ConfigureAwait(false);
                Console.WriteLine("CASE " + name + " PASS");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine("CASE " + name + " FAIL " + exception.GetType().Name + ":" + exception.Message);
            }
        }
    }

    /// <summary>
    /// A PNG big enough to need several frames. The asset store only sniffs the magic bytes, so a
    /// real signature followed by deterministic filler is enough to exercise chunked readback.
    /// </summary>
    private static byte[] LargePngBytes(int length)
    {
        var bytes = new byte[length];
        bytes[0] = 0x89;
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
        bytes[4] = 0x0D;
        bytes[5] = 0x0A;
        bytes[6] = 0x1A;
        bytes[7] = 0x0A;
        for (var index = 8; index < length; index++)
        {
            bytes[index] = (byte)((index * 31 + 7) & 0xFF);
        }

        return bytes;
    }

    private static ImageGenerationRequest CreateRequest(string prompt, string idempotencyKey)
    {
        return new ImageGenerationRequest(RouteId, prompt, null, 512, 768, 0L, "media-client-test", "campaign", "none", DateTimeOffset.UtcNow.AddMinutes(2), idempotencyKey);
    }

    private static string ObjectPath(string dataRoot, string contentHash)
    {
        return Path.Combine(dataRoot, "assets", "objects", contentHash.Substring(0, 2), contentHash);
    }

    private static int CountObjects(string dataRoot)
    {
        var root = Path.Combine(dataRoot, "assets", "objects");
        return Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length : 0;
    }

    private static string Sha256Hex(byte[] content)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(content);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var index = text.IndexOf('\n');
        return index < 0 ? text : text.Substring(0, index);
    }

    private static string DescribeError(FrameworkError error)
    {
        if (error == null) return "<none>";
        var text = error.Code + "/" + error.Category.ToString();
        if (error.Details != null && error.Details.Count > 0)
        {
            var builder = new StringBuilder(text);
            foreach (var pair in error.Details)
            {
                builder.Append(':').Append(pair.Key).Append('=').Append(pair.Value);
            }

            text = builder.ToString();
        }

        return text;
    }

    private static FrameworkError Failure<T>(OperationResult<T> outcome, string message)
    {
        if (outcome.IsSuccess || outcome.Error == null) throw new InvalidOperationException(message);
        return outcome.Error;
    }

    private static void RequireUnsupported(FrameworkError error, string expectedCode)
    {
        Require(StringComparer.Ordinal.Equals(error.Code, expectedCode), "unsupported_code_invalid:" + error.Code + "!=" + expectedCode);
        Require(error.Category == FrameworkErrorCategory.Unsupported, "unsupported_category_invalid:" + error.Code + "/" + error.Category.ToString());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string ResolveProviderAssemblyPath()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MarcusAwakeProvider", "_build_out", "Release", "MarcusAwakeProvider.dll"));
        if (!File.Exists(path)) throw new InvalidOperationException("provider_assembly_missing:" + path);
        return path;
    }

    private static string ResolveServicePath()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "_build_out", "Release", "MarcusAwakeRuntimeService.exe"));
        if (!File.Exists(path)) throw new InvalidOperationException("service_executable_missing:" + path);
        return path;
    }
}
