using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeTransport;

namespace MarcusAwakeFramework.Api
{
    /// <summary>
    /// Governed media surface. Image generation is a provider operation: the Runtime Service resolves
    /// the logical route to a provider profile, generates the image, imports the bytes into the
    /// content-addressed asset store, and answers with the resulting <see cref="AssetHandle"/>.
    ///
    /// The bytes deliberately never cross the frame boundary — a portrait is far larger than
    /// <c>MaxFrameBytes</c> — so callers read the content back through the asset surface using the
    /// handle they get here. The game process therefore never performs HTTP, never holds a provider
    /// key, and never decides where an asset lives.
    /// </summary>
    public sealed partial class RuntimeServiceClient
    {
        /// <inheritdoc />
        public async Task<OperationResult<GeneratedAssetResult>> GenerateImageAsync(
            ImageGenerationRequest request,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            var correlationId = context == null ? "runtime-media-image" : context.CorrelationId;
            var rejection = MediaRuntimeWire.ValidateImage(request, correlationId);
            if (rejection != null) return OperationResult<GeneratedAssetResult>.Failed(rejection);

            var preparation = PrepareProviderCall(
                context,
                cancellationToken,
                ProtocolConstants.CapabilityProviderImageV1,
                "runtime-media-image",
                out var activeConnection,
                out var lifecycleToken,
                "media");
            if (!preparation.IsSuccess) return OperationResult<GeneratedAssetResult>.Failed(preparation.Error);

            // Fail here rather than letting CreateBusinessEnvelope throw: a public API must not
            // surface an ArgumentException for a caller-side precondition.
            var campaign = RequireMediaCampaignSession(context, correlationId);
            if (campaign != null) return OperationResult<GeneratedAssetResult>.Failed(campaign);

            var payload = MediaRuntimeWire.ImagePayload(request);
            var taskScope = CreateMediaTaskScope(request, context, payload);
            var envelope = activeConnection.CreateBusinessEnvelope(
                ProtocolConstants.MessageTypeProviderImageV1,
                MediaRuntimeWire.ImageSchema,
                payload,
                taskScope,
                context,
                context.Deadline);

            return await SendProviderOperationAsync(
                activeConnection,
                envelope,
                context.Deadline,
                lifecycleToken,
                cancellationToken,
                response => ParseImageResponse(response, request.RouteId, correlationId),
                "media").ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task<OperationResult<GeneratedAssetResult>> SynthesizeSpeechAsync(
            TtsGenerationRequest request,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            var correlationId = context == null ? "runtime-media-speech" : context.CorrelationId;
            return Task.FromResult(OperationResult<GeneratedAssetResult>.Failed(FrameworkErrors.Create(
                "media.speech_unsupported",
                FrameworkErrorCategory.Unsupported,
                "Speech synthesis is not available.",
                correlationId,
                retryable: false,
                owner: "MarcusAwakeFramework")));
        }

        /// <summary>
        /// Generated assets are owned by a campaign, so a session that is not a campaign session
        /// cannot be served. Failing here keeps the public API from throwing out of
        /// <c>CreateBusinessEnvelope</c>.
        /// </summary>
        private static FrameworkError RequireMediaCampaignSession(RequestContext context, string correlationId)
        {
            if (context.Session != null && context.Session.IsCampaign) return null;
            return FrameworkErrors.Create("media.campaign_session_required", FrameworkErrorCategory.InvalidRequest, "An image generation request requires an active campaign session.", correlationId);
        }

        /// <summary>
        /// Media tasks carry no provider identity: the caller names a logical route and the runtime
        /// resolves which profile and provider serve it. The scope still has to be complete, so the
        /// profile and provider slots hold stable placeholders.
        /// </summary>
        private static TaskScopeEnvelope CreateMediaTaskScope(ImageGenerationRequest request, RequestContext context, string payload)
        {
            var messageId = "media-image-" + Guid.NewGuid().ToString("N");
            return new TaskScopeEnvelope
            {
                TaskId = messageId + "-task",
                MessageId = messageId,
                OwnerId = context.Caller.Value,
                RouteId = request.RouteId,
                ProviderId = MediaRuntimeWire.ProviderId,
                ProfileId = MediaRuntimeWire.ProfileId,
                IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                    ? "media-image-" + Guid.NewGuid().ToString("N")
                    : request.IdempotencyKey,
                RequestPayloadHash = TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes(CanonicalizePayload(payload))),
                OutputSchemaId = MediaRuntimeWire.ImageResultSchema,
                OutputSchemaMajor = 1,
                OutputSchemaMinor = 0,
                SettlementRequirement = "not_applicable"
            };
        }

        private static OperationResult<GeneratedAssetResult> ParseImageResponse(PipeEnvelope response, string routeId, string correlationId)
        {
            if (IsErrorResponse(response))
            {
                return OperationResult<GeneratedAssetResult>.Failed(CreateMediaError(response, routeId, correlationId));
            }

            if (!StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeProviderImageResult)
                || !StringComparer.Ordinal.Equals(response.PayloadSchema, MediaRuntimeWire.ImageResultSchema))
            {
                throw new ClientProtocolException("media_image_response_identity_invalid");
            }

            if (!MediaRuntimeWire.TryReadImageResult(response.PayloadJson, out var result, out _))
            {
                throw new ClientProtocolException("media_image_result_invalid");
            }

            return OperationResult<GeneratedAssetResult>.Succeeded(result);
        }

        private static FrameworkError CreateMediaError(PipeEnvelope response, string routeId, string correlationId)
        {
            if (!ProviderRuntimeWire.TryReadError(response.PayloadJson, out var error))
            {
                throw new ClientProtocolException("media_error_decode_failed");
            }

            return CreateProviderFrameworkError(error, correlationId, new ProviderScopeRequest(MediaRuntimeWire.ProfileId, MediaRuntimeWire.ProviderId, routeId));
        }
    }

    /// <summary>
    /// Wire shape for <c>provider.image.v1</c>. The payload names only the logical route; the
    /// runtime resolves profile and provider identity itself, so those fields stay optional and the
    /// runtime task scope is the single source of truth for them.
    /// </summary>
    internal static class MediaRuntimeWire
    {
        internal const string ImageSchema = ProtocolConstants.ProviderImageSchemaV1;
        internal const string ImageResultSchema = ProtocolConstants.ProviderImageResultSchemaV1;

        /// <summary>Stable placeholders: the protocol requires a complete task scope, but a media task has no provider identity of its own.</summary>
        internal const string ProviderId = "runtime.media";
        internal const string ProfileId = "runtime.media";

        internal const int MaximumPromptBytes = 8192;
        internal const int MaximumNegativePromptBytes = 8192;
        internal const int MaximumDimension = 4096;
        internal const int MaximumClassificationBytes = 64;

        private static readonly string[] ImageResultProperties =
        {
            "schema", "profile_id", "provider_id", "route_id", "asset_id", "content_hash", "media_type",
            "byte_length", "logical_kind", "created_by_task", "owner_extension_id", "campaign_id",
            "timeline_id", "provenance", "retention_class", "resolved_model"
        };

        internal static FrameworkError ValidateImage(ImageGenerationRequest request, string correlationId)
        {
            if (request == null)
            {
                return Reject("media.image_request_required", "An image generation request is required.", correlationId);
            }

            if (string.IsNullOrWhiteSpace(request.RouteId))
            {
                return Reject("media.image_route_required", "An image route is required.", correlationId);
            }

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return Reject("media.image_prompt_required", "An image prompt is required.", correlationId);
            }

            if (Encoding.UTF8.GetByteCount(request.Prompt) > MaximumPromptBytes)
            {
                return Reject("media.image_prompt_too_long", "The image prompt is too long.", correlationId);
            }

            if (request.NegativePrompt != null && Encoding.UTF8.GetByteCount(request.NegativePrompt) > MaximumNegativePromptBytes)
            {
                return Reject("media.image_negative_prompt_too_long", "The negative prompt is too long.", correlationId);
            }

            if (request.Width < 0 || request.Width > MaximumDimension || request.Height < 0 || request.Height > MaximumDimension)
            {
                return Reject("media.image_dimensions_out_of_range", "The requested image dimensions are out of range.", correlationId);
            }

            // The classification is a promise about what leaves the machine, and running the gate is
            // the caller's job — exactly like AiTaskRequest.CloudExportClassification on the text
            // path. The framework only guarantees the declaration is well formed, so a malformed or
            // missing one can never be mistaken for "nothing to declare" and silently defaulted.
            var classification = request.CloudExportClassification;
            if (string.IsNullOrWhiteSpace(classification))
            {
                return Reject("media.image_cloud_export_required", "An image request must declare a cloud export classification.", correlationId);
            }

            if (!IsWellFormedClassification(classification))
            {
                return FrameworkErrors.Create(
                    "media.image_cloud_export_invalid",
                    FrameworkErrorCategory.InvalidRequest,
                    "The cloud export classification must be a bounded lowercase identifier.",
                    correlationId,
                    retryable: false,
                    owner: "MarcusAwakeFramework");
            }

            return null;
        }

        internal static string ImagePayload(ImageGenerationRequest request)
        {
            var builder = new StringBuilder(256);
            builder.Append("{\"schema\":").Append(ProviderRuntimeWire.Quote(ImageSchema));
            builder.Append(",\"route_id\":").Append(ProviderRuntimeWire.Quote(request.RouteId));
            builder.Append(",\"prompt\":").Append(ProviderRuntimeWire.Quote(request.Prompt));

            if (!string.IsNullOrEmpty(request.NegativePrompt))
            {
                builder.Append(",\"negative_prompt\":").Append(ProviderRuntimeWire.Quote(request.NegativePrompt));
            }

            if (request.Width > 0) builder.Append(",\"width\":").Append(request.Width.ToString(CultureInfo.InvariantCulture));
            if (request.Height > 0) builder.Append(",\"height\":").Append(request.Height.ToString(CultureInfo.InvariantCulture));

            // The runtime accepts a bounded seed; a seed outside that window is simply not sent.
            if (request.Seed > 0 && request.Seed <= int.MaxValue)
            {
                builder.Append(",\"seed\":").Append(request.Seed.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrEmpty(request.Provenance))
            {
                builder.Append(",\"provenance\":").Append(ProviderRuntimeWire.Quote(request.Provenance));
            }

            if (!string.IsNullOrEmpty(request.RetentionClass))
            {
                builder.Append(",\"retention_class\":").Append(ProviderRuntimeWire.Quote(request.RetentionClass));
            }

            builder.Append('}');
            return builder.ToString();
        }

        internal static bool TryReadImageResult(string payload, out GeneratedAssetResult result, out string error)
        {
            result = null;
            var wire = ProviderRuntimeJson.Deserialize<MediaImageResultWire>(payload, out error);
            if (wire == null
                || !ProviderRuntimeJson.HasExactTopLevelProperties(payload, ImageResultProperties, ImageResultProperties, out error))
            {
                return false;
            }

            if (!StringComparer.Ordinal.Equals(wire.Schema, ImageResultSchema)
                || string.IsNullOrWhiteSpace(wire.AssetId)
                || string.IsNullOrWhiteSpace(wire.ContentHash)
                || string.IsNullOrWhiteSpace(wire.MediaType)
                || wire.ByteLength < 1
                || string.IsNullOrWhiteSpace(wire.OwnerExtensionId))
            {
                error = "provider_schema_mismatch";
                return false;
            }

            try
            {
                var handle = new AssetHandle(
                    wire.AssetId,
                    wire.ContentHash,
                    wire.MediaType,
                    wire.ByteLength,
                    wire.LogicalKind,
                    wire.CreatedByTask,
                    new ExtensionId(wire.OwnerExtensionId),
                    wire.CampaignId,
                    wire.TimelineId,
                    wire.Provenance,
                    wire.RetentionClass);
                result = new GeneratedAssetResult(handle, wire.ResolvedModel ?? string.Empty);
                return true;
            }
            catch (ArgumentException)
            {
                error = "provider_schema_mismatch";
                return false;
            }
        }

        private static FrameworkError Reject(string code, string safeFallback, string correlationId)
        {
            return FrameworkErrors.Create(code, FrameworkErrorCategory.InvalidRequest, safeFallback, correlationId, retryable: false, owner: "MarcusAwakeFramework");
        }

        /// <summary>
        /// The permission id shape the gate will build is <c>ai.cloud_export:&lt;classification&gt;</c>,
        /// so the classification has to stay a bounded lowercase identifier.
        /// </summary>
        private static bool IsWellFormedClassification(string value)
        {
            if (Encoding.UTF8.GetByteCount(value) > MaximumClassificationBytes) return false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var allowed = (character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '_';
                if (!allowed) return false;
            }

            return true;
        }
    }

    [DataContract]
    internal sealed class MediaImageResultWire
    {
        [DataMember(Name = "schema")] public string Schema { get; set; }
        [DataMember(Name = "profile_id")] public string ProfileId { get; set; }
        [DataMember(Name = "provider_id")] public string ProviderId { get; set; }
        [DataMember(Name = "route_id")] public string RouteId { get; set; }
        [DataMember(Name = "asset_id")] public string AssetId { get; set; }
        [DataMember(Name = "content_hash")] public string ContentHash { get; set; }
        [DataMember(Name = "media_type")] public string MediaType { get; set; }
        [DataMember(Name = "byte_length")] public long ByteLength { get; set; }
        [DataMember(Name = "logical_kind")] public string LogicalKind { get; set; }
        [DataMember(Name = "created_by_task")] public string CreatedByTask { get; set; }
        [DataMember(Name = "owner_extension_id")] public string OwnerExtensionId { get; set; }
        [DataMember(Name = "campaign_id")] public string CampaignId { get; set; }
        [DataMember(Name = "timeline_id")] public string TimelineId { get; set; }
        [DataMember(Name = "provenance")] public string Provenance { get; set; }
        [DataMember(Name = "retention_class")] public string RetentionClass { get; set; }
        [DataMember(Name = "resolved_model")] public string ResolvedModel { get; set; }
    }
}
