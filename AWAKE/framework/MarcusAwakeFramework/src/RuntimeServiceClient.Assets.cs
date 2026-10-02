using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeTransport;

namespace MarcusAwakeFramework.Api
{
    /// <summary>
    /// Governed asset surface. Assets live in the Runtime Service's content-addressed store; the
    /// game process never learns a real file path and never writes one.
    ///
    /// A portrait does not fit in a single frame (<c>MaxFrameBytes</c> is 256 KiB), so the read is
    /// chunked: the client asks for <c>[offset, offset + maximum_bytes)</c> repeatedly and stitches
    /// the slices back together. Every slice carries the full asset identity so a response from a
    /// different asset can never be spliced into the buffer.
    ///
    /// Only <see cref="ReadAsync"/> is implemented in this slice. The remaining asset operations
    /// answer with a typed <c>Unsupported</c> failure instead of silently succeeding, so a caller
    /// that needs them learns the gap rather than losing data.
    /// </summary>
    public sealed partial class RuntimeServiceClient
    {
        /// <inheritdoc />
        public async Task<OperationResult<AssetContent>> ReadAsync(
            string assetId,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            var correlationId = context == null ? "runtime-asset-read" : context.CorrelationId;
            var rejection = AssetRuntimeWire.ValidateRead(assetId, correlationId);
            if (rejection != null) return OperationResult<AssetContent>.Failed(rejection);

            var preparation = PrepareProviderCall(
                context,
                cancellationToken,
                ProtocolConstants.CapabilityAssetRead,
                "runtime-asset-read",
                out var activeConnection,
                out var lifecycleToken,
                "asset");
            if (!preparation.IsSuccess) return OperationResult<AssetContent>.Failed(preparation.Error);

            var campaign = RequireAssetCampaignSession(context, correlationId);
            if (campaign != null) return OperationResult<AssetContent>.Failed(campaign);

            using (var buffer = new MemoryStream())
            {
                AssetHandle handle = null;
                long offset = 0L;

                while (true)
                {
                    var payload = AssetRuntimeWire.ReadPayload(assetId, offset);
                    var taskScope = CreateAssetTaskScope(context, payload);
                    var envelope = activeConnection.CreateBusinessEnvelope(
                        ProtocolConstants.MessageTypeAssetRead,
                        AssetRuntimeWire.ReadSchema,
                        payload,
                        taskScope,
                        context,
                        context.Deadline);

                    var chunkOutcome = await SendProviderOperationAsync(
                        activeConnection,
                        envelope,
                        context.Deadline,
                        lifecycleToken,
                        cancellationToken,
                        response => ParseAssetChunk(response, assetId, offset, correlationId),
                        "asset").ConfigureAwait(false);

                    if (!chunkOutcome.IsSuccess) return OperationResult<AssetContent>.Failed(chunkOutcome.Error);

                    var chunk = chunkOutcome.Value;
                    if (handle == null)
                    {
                        handle = chunk.Handle;
                    }
                    else if (!AssetRuntimeWire.SameIdentity(handle, chunk.Handle))
                    {
                        return OperationResult<AssetContent>.Failed(AssetRuntimeWire.ProtocolFailure("asset.chunk_identity_changed", correlationId));
                    }

                    // A zero-length slice that does not finish the asset would spin forever.
                    if (chunk.Content.Length == 0 && !chunk.Done)
                    {
                        return OperationResult<AssetContent>.Failed(AssetRuntimeWire.ProtocolFailure("asset.chunk_empty", correlationId));
                    }

                    buffer.Write(chunk.Content, 0, chunk.Content.Length);
                    offset += chunk.Content.Length;

                    if (chunk.Done) break;

                    if (offset >= chunk.ByteLength)
                    {
                        return OperationResult<AssetContent>.Failed(AssetRuntimeWire.ProtocolFailure("asset.chunk_truncated", correlationId));
                    }
                }

                var bytes = buffer.ToArray();
                if (bytes.Length != handle.ByteLength)
                {
                    return OperationResult<AssetContent>.Failed(AssetRuntimeWire.ProtocolFailure("asset.byte_length_mismatch", correlationId));
                }

                return OperationResult<AssetContent>.Succeeded(new AssetContent(handle, bytes));
            }
        }

        /// <inheritdoc />
        public Task<OperationResult<AssetHandle>> ImportAsync(
            AssetImportRequest request,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            // Import stays on the runtime side: the runtime owns the store and the bytes it writes
            // are the bytes a provider produced, not bytes a mod handed over.
            return Task.FromResult(OperationResult<AssetHandle>.Failed(
                AssetRuntimeWire.Unsupported("asset.import_unsupported", "Importing caller-supplied asset bytes is not available.", context)));
        }

        /// <inheritdoc />
        public Task<OperationResult<AssetMetadata>> GetMetadataAsync(
            string assetId,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<AssetMetadata>.Failed(
                AssetRuntimeWire.Unsupported("asset.metadata_unsupported", "Reading asset metadata is not available.", context)));
        }

        /// <inheritdoc />
        public Task<OperationResult<bool>> SetPinnedAsync(
            string assetId,
            bool pinned,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<bool>.Failed(
                AssetRuntimeWire.Unsupported("asset.pin_unsupported", "Pinning an asset is not available.", context)));
        }

        /// <inheritdoc />
        public Task<OperationResult<AssetListPage>> ListAsync(
            int maximumResults,
            string cursor,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<AssetListPage>.Failed(
                AssetRuntimeWire.Unsupported("asset.list_unsupported", "Listing assets is not available.", context)));
        }

        /// <inheritdoc />
        public Task<OperationResult<AssetExportReceipt>> ExportAsync(
            string assetId,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<AssetExportReceipt>.Failed(
                AssetRuntimeWire.Unsupported("asset.export_unsupported", "Exporting an asset is not available.", context)));
        }

        /// <inheritdoc />
        public Task<OperationResult<bool>> DeleteAsync(
            string assetId,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            // Deletion is a retention decision, not a caller decision: a portrait is referenced by
            // the campaign that generated it and only cleanup may reclaim it.
            return Task.FromResult(OperationResult<bool>.Failed(
                AssetRuntimeWire.Unsupported("asset.delete_unsupported", "Deleting an asset is not available.", context)));
        }

        /// <inheritdoc />
        public Task<OperationResult<AssetCleanupSummary>> CleanupAsync(
            AssetCleanupRequest request,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<AssetCleanupSummary>.Failed(
                AssetRuntimeWire.Unsupported("asset.cleanup_unsupported", "Cleaning up assets is not available.", context)));
        }

        /// <summary>
        /// Assets are owned by a campaign, so a session that is not a campaign session cannot be
        /// served. Failing here keeps the public API from throwing out of
        /// <c>CreateBusinessEnvelope</c>.
        /// </summary>
        private static FrameworkError RequireAssetCampaignSession(RequestContext context, string correlationId)
        {
            if (context.Session != null && context.Session.IsCampaign) return null;
            return FrameworkErrors.Create(
                "asset.campaign_session_required",
                FrameworkErrorCategory.InvalidRequest,
                "An asset request requires an active campaign session.",
                correlationId);
        }

        /// <summary>
        /// Asset tasks carry no provider identity, but the protocol still requires a complete task
        /// scope, so the route, provider and profile slots hold stable placeholders.
        /// </summary>
        private static TaskScopeEnvelope CreateAssetTaskScope(RequestContext context, string payload)
        {
            var messageId = "asset-read-" + Guid.NewGuid().ToString("N");
            return new TaskScopeEnvelope
            {
                TaskId = messageId + "-task",
                MessageId = messageId,
                OwnerId = context.Caller.Value,
                RouteId = AssetRuntimeWire.RouteId,
                ProviderId = AssetRuntimeWire.ProviderId,
                ProfileId = AssetRuntimeWire.ProfileId,
                IdempotencyKey = messageId,
                RequestPayloadHash = TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes(CanonicalizePayload(payload))),
                OutputSchemaId = AssetRuntimeWire.ResultSchema,
                OutputSchemaMajor = 1,
                OutputSchemaMinor = 0,
                SettlementRequirement = "not_applicable"
            };
        }

        private static OperationResult<AssetChunk> ParseAssetChunk(
            PipeEnvelope response,
            string assetId,
            long offset,
            string correlationId)
        {
            if (IsErrorResponse(response))
            {
                return OperationResult<AssetChunk>.Failed(CreateAssetError(response, assetId, correlationId));
            }

            if (!StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeAssetResult)
                || !StringComparer.Ordinal.Equals(response.PayloadSchema, AssetRuntimeWire.ResultSchema))
            {
                throw new ClientProtocolException("asset_read_response_identity_invalid");
            }

            if (!AssetRuntimeWire.TryReadChunk(response.PayloadJson, assetId, offset, out var chunk, out var error))
            {
                throw new ClientProtocolException(string.IsNullOrEmpty(error) ? "asset_read_result_invalid" : error);
            }

            return OperationResult<AssetChunk>.Succeeded(chunk);
        }

        private static FrameworkError CreateAssetError(PipeEnvelope response, string assetId, string correlationId)
        {
            if (!RagRuntimeWire.TryReadError(response.PayloadJson, out var errorCode, out var retryable))
            {
                throw new ClientProtocolException("asset_error_decode_failed");
            }

            return FrameworkErrors.Create(
                errorCode,
                AssetRuntimeWire.ReadErrorCategory(response.PayloadJson),
                "The asset request failed.",
                correlationId,
                retryable: retryable,
                owner: "MarcusAwakeRuntimeService",
                details: new Dictionary<string, string> { { "asset_id", assetId } });
        }
    }

    /// <summary>One slice of an asset read: the asset identity plus the bytes that slice carried.</summary>
    internal sealed class AssetChunk
    {
        internal AssetChunk(AssetHandle handle, byte[] content, long byteLength, bool done)
        {
            Handle = handle;
            Content = content;
            ByteLength = byteLength;
            Done = done;
        }

        internal AssetHandle Handle { get; }

        internal byte[] Content { get; }

        /// <summary>Declared total length of the whole asset, repeated on every slice.</summary>
        internal long ByteLength { get; }

        internal bool Done { get; }
    }

    /// <summary>
    /// Wire shape for <c>asset.read</c> / <c>asset_result</c>. Asset frames belong to the
    /// storage/RAG family, so the payload carries no <c>schema</c> member; the schema is pinned by
    /// the frame's <c>payload_schema</c> instead.
    /// </summary>
    internal static class AssetRuntimeWire
    {
        internal const string ReadSchema = ProtocolConstants.AssetReadSchemaV1;
        internal const string ResultSchema = ProtocolConstants.AssetResultSchemaV1;

        /// <summary>Stable placeholders: the protocol requires a complete task scope, but an asset task has no provider identity of its own.</summary>
        internal const string RouteId = "runtime.asset";
        internal const string ProviderId = "runtime.asset";
        internal const string ProfileId = "runtime.asset";

        internal const int MaximumAssetIdBytes = 128;

        private static readonly string[] ResultProperties =
        {
            "asset_id", "content_hash", "media_type", "byte_length", "logical_kind", "created_by_task",
            "owner_extension_id", "campaign_id", "timeline_id", "provenance", "retention_class",
            "offset", "chunk_byte_length", "chunk_base64", "done"
        };

        internal static FrameworkError ValidateRead(string assetId, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                return Reject("asset.asset_id_required", "An asset id is required.", correlationId);
            }

            if (Encoding.UTF8.GetByteCount(assetId) > MaximumAssetIdBytes)
            {
                return Reject("asset.asset_id_too_long", "The asset id is too long.", correlationId);
            }

            return null;
        }

        internal static string ReadPayload(string assetId, long offset)
        {
            var builder = new StringBuilder(192);
            builder.Append("{\"asset_id\":").Append(ProviderRuntimeWire.Quote(assetId));
            builder.Append(",\"offset\":").Append(offset.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"maximum_bytes\":").Append(ProtocolConstants.MaxAssetChunkBytes.ToString(CultureInfo.InvariantCulture));
            builder.Append('}');
            return builder.ToString();
        }

        internal static bool TryReadChunk(
            string payload,
            string expectedAssetId,
            long expectedOffset,
            out AssetChunk chunk,
            out string error)
        {
            chunk = null;

            var wire = ProviderRuntimeJson.Deserialize<AssetReadResultWire>(payload, out error);
            if (wire == null
                || !ProviderRuntimeJson.HasExactTopLevelProperties(payload, ResultProperties, ResultProperties, out error))
            {
                return false;
            }

            // Every slice is checked against the request that produced it: a slice for another
            // asset, or for another offset, must never reach the stitching buffer.
            if (!StringComparer.Ordinal.Equals(wire.AssetId, expectedAssetId)
                || wire.Offset != expectedOffset
                || wire.ByteLength < 1
                || wire.ChunkByteLength < 0
                || wire.ChunkBase64 == null
                || string.IsNullOrWhiteSpace(wire.ContentHash)
                || string.IsNullOrWhiteSpace(wire.MediaType)
                || string.IsNullOrWhiteSpace(wire.OwnerExtensionId))
            {
                error = "asset_result_mismatch";
                return false;
            }

            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(wire.ChunkBase64);
            }
            catch (FormatException)
            {
                error = "asset_result_mismatch";
                return false;
            }

            if (decoded.Length != wire.ChunkByteLength)
            {
                error = "asset_result_mismatch";
                return false;
            }

            AssetHandle handle;
            try
            {
                handle = new AssetHandle(
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
            }
            catch (ArgumentException)
            {
                error = "asset_result_mismatch";
                return false;
            }

            chunk = new AssetChunk(handle, decoded, wire.ByteLength, wire.Done);
            error = string.Empty;
            return true;
        }

        /// <summary>Two slices belong to the same asset only when the whole identity agrees.</summary>
        internal static bool SameIdentity(AssetHandle left, AssetHandle right)
        {
            if (left == null || right == null) return false;
            return StringComparer.Ordinal.Equals(left.AssetId, right.AssetId)
                && StringComparer.Ordinal.Equals(left.ContentHash, right.ContentHash)
                && left.ByteLength == right.ByteLength
                && StringComparer.Ordinal.Equals(left.MediaType, right.MediaType)
                && StringComparer.Ordinal.Equals(left.LogicalKind, right.LogicalKind)
                && StringComparer.Ordinal.Equals(left.RetentionClass, right.RetentionClass)
                && StringComparer.Ordinal.Equals(left.OwnerExtensionId.Value, right.OwnerExtensionId.Value)
                && StringComparer.Ordinal.Equals(left.CampaignId, right.CampaignId)
                && StringComparer.Ordinal.Equals(left.TimelineId, right.TimelineId);
        }

        internal static FrameworkErrorCategory ReadErrorCategory(string payload)
        {
            var wire = ProviderRuntimeJson.Deserialize<GenericErrorWire>(payload, out _);
            if (wire == null) return FrameworkErrorCategory.InternalFailure;
            return MapCategory(wire.Category);
        }

        internal static FrameworkError Unsupported(string code, string safeFallback, RequestContext context)
        {
            return FrameworkErrors.Create(
                code,
                FrameworkErrorCategory.Unsupported,
                safeFallback,
                context == null ? "runtime-asset" : context.CorrelationId,
                retryable: false,
                owner: "MarcusAwakeFramework");
        }

        internal static FrameworkError ProtocolFailure(string code, string correlationId)
        {
            return FrameworkErrors.Create(
                code,
                FrameworkErrorCategory.Incompatible,
                "The runtime returned an inconsistent asset response.",
                correlationId,
                retryable: false,
                owner: "MarcusAwakeFramework");
        }

        private static FrameworkError Reject(string code, string safeFallback, string correlationId)
        {
            return FrameworkErrors.Create(code, FrameworkErrorCategory.InvalidRequest, safeFallback, correlationId, retryable: false, owner: "MarcusAwakeFramework");
        }

        private static FrameworkErrorCategory MapCategory(string category)
        {
            switch (category)
            {
                case "invalid_request": return FrameworkErrorCategory.InvalidRequest;
                case "incompatible": return FrameworkErrorCategory.Incompatible;
                case "unsupported": return FrameworkErrorCategory.Unsupported;
                case "unavailable": return FrameworkErrorCategory.Unavailable;
                case "denied": return FrameworkErrorCategory.Denied;
                case "not_found": return FrameworkErrorCategory.NotFound;
                case "conflict": return FrameworkErrorCategory.Conflict;
                case "expired": return FrameworkErrorCategory.Expired;
                case "rate_limited": return FrameworkErrorCategory.RateLimited;
                case "provider_failure": return FrameworkErrorCategory.ProviderFailure;
                case "timeout": return FrameworkErrorCategory.Timeout;
                case "cancelled": return FrameworkErrorCategory.Cancelled;
                case "resource_exhausted": return FrameworkErrorCategory.ResourceExhausted;
                case "recovery_required": return FrameworkErrorCategory.RecoveryRequired;
                default: return FrameworkErrorCategory.InternalFailure;
            }
        }
    }

    [DataContract]
    internal sealed class AssetReadResultWire
    {
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
        [DataMember(Name = "offset")] public long Offset { get; set; }
        [DataMember(Name = "chunk_byte_length")] public int ChunkByteLength { get; set; }
        [DataMember(Name = "chunk_base64")] public string ChunkBase64 { get; set; }
        [DataMember(Name = "done")] public bool Done { get; set; }
    }
}
