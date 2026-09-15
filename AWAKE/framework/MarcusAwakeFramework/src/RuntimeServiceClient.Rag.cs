using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeTransport;

namespace MarcusAwakeFramework.Api
{
    /// <summary>
    /// RAG data-plane client. The framework side owns the API/IPC/permission boundary and the
    /// Runtime Service owns SQLite/FTS5, so this only forwards the business message and reads back
    /// the typed result — see <see cref="RagRuntimeWire"/> for the wire shape.
    /// </summary>
    public sealed partial class RuntimeServiceClient
    {
        public async Task<OperationResult<int>> IngestAsync(RagIngestRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            var correlationId = context == null ? "runtime-rag-ingest" : context.CorrelationId;
            var rejection = RagRuntimeWire.ValidateIngest(request, correlationId);
            if (rejection != null) return OperationResult<int>.Failed(rejection);

            var preparation = PrepareProviderCall(context, cancellationToken, ProtocolConstants.CapabilityRagWrite, "runtime-rag-ingest", out var activeConnection, out var lifecycleToken, "RAG");
            if (!preparation.IsSuccess) return OperationResult<int>.Failed(preparation.Error);
            var campaign = RequireCampaignSession(context, correlationId);
            if (campaign != null) return OperationResult<int>.Failed(campaign);

            var payload = RagRuntimeWire.IngestPayload(request);
            var taskScope = CreateRagTaskScope("rag-ingest", context, payload);
            var envelope = activeConnection.CreateBusinessEnvelope(ProtocolConstants.MessageTypeRagIngest, RagRuntimeWire.IngestSchema, payload, taskScope, context, context.Deadline);
            return await SendProviderOperationAsync(activeConnection, envelope, context.Deadline, lifecycleToken, cancellationToken, response => ParseRagIngestResponse(response, request, correlationId), "RAG").ConfigureAwait(false);
        }

        public async Task<OperationResult<IReadOnlyList<RagHit>>> SearchAsync(RagSearchRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            var correlationId = context == null ? "runtime-rag-search" : context.CorrelationId;
            var rejection = RagRuntimeWire.ValidateSearch(request, correlationId);
            if (rejection != null) return OperationResult<IReadOnlyList<RagHit>>.Failed(rejection);

            var preparation = PrepareProviderCall(context, cancellationToken, ProtocolConstants.CapabilityRagRead, "runtime-rag-search", out var activeConnection, out var lifecycleToken, "RAG");
            if (!preparation.IsSuccess) return OperationResult<IReadOnlyList<RagHit>>.Failed(preparation.Error);
            var campaign = RequireCampaignSession(context, correlationId);
            if (campaign != null) return OperationResult<IReadOnlyList<RagHit>>.Failed(campaign);

            var payload = RagRuntimeWire.SearchPayload(request);
            var taskScope = CreateRagTaskScope("rag-search", context, payload);
            var envelope = activeConnection.CreateBusinessEnvelope(ProtocolConstants.MessageTypeRagSearch, RagRuntimeWire.SearchSchema, payload, taskScope, context, context.Deadline);
            return await SendProviderOperationAsync(activeConnection, envelope, context.Deadline, lifecycleToken, cancellationToken, response => ParseRagSearchResponse(response, request, correlationId), "RAG").ConfigureAwait(false);
        }

        /// <summary>
        /// RAG namespaces are campaign-scoped, so a session that is not a campaign session cannot be
        /// served. Failing here keeps the public API from throwing out of CreateBusinessEnvelope.
        /// </summary>
        private static FrameworkError RequireCampaignSession(RequestContext context, string correlationId)
        {
            if (context.Session != null && context.Session.IsCampaign) return null;
            return FrameworkErrors.Create("rag.campaign_session_required", FrameworkErrorCategory.InvalidRequest, "A RAG request requires an active campaign session.", correlationId);
        }

        private static TaskScopeEnvelope CreateRagTaskScope(string operation, RequestContext context, string payload)
        {
            var messageId = operation + "-" + Guid.NewGuid().ToString("N");
            return new TaskScopeEnvelope
            {
                TaskId = messageId + "-task",
                MessageId = messageId,
                OwnerId = context.Caller.Value,
                RouteId = RagRuntimeWire.RouteId,
                ProviderId = RagRuntimeWire.ProviderId,
                ProfileId = RagRuntimeWire.ProfileId,
                IdempotencyKey = operation + "-" + Guid.NewGuid().ToString("N"),
                RequestPayloadHash = TransportSecurity.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(CanonicalizePayload(payload))),
                OutputSchemaId = RagRuntimeWire.ResultSchema,
                OutputSchemaMajor = 1,
                OutputSchemaMinor = 0,
                SettlementRequirement = "not_applicable"
            };
        }

        private static OperationResult<int> ParseRagIngestResponse(PipeEnvelope response, RagIngestRequest request, string correlationId)
        {
            if (IsErrorResponse(response)) return OperationResult<int>.Failed(CreateRagError(response, correlationId));
            if (!IsRagResult(response)) throw new ClientProtocolException("rag_ingest_response_identity_invalid");
            if (!RagRuntimeWire.TryReadIngestResult(response.PayloadJson, request, out var ingested, out _)) throw new ClientProtocolException("rag_ingest_result_invalid");
            return OperationResult<int>.Succeeded(ingested);
        }

        private static OperationResult<IReadOnlyList<RagHit>> ParseRagSearchResponse(PipeEnvelope response, RagSearchRequest request, string correlationId)
        {
            if (IsErrorResponse(response)) return OperationResult<IReadOnlyList<RagHit>>.Failed(CreateRagError(response, correlationId));
            if (!IsRagResult(response)) throw new ClientProtocolException("rag_search_response_identity_invalid");
            if (!RagRuntimeWire.TryReadSearchResult(response.PayloadJson, request, out var hits, out _)) throw new ClientProtocolException("rag_search_result_invalid");
            return OperationResult<IReadOnlyList<RagHit>>.Succeeded(hits);
        }

        private static bool IsErrorResponse(PipeEnvelope response)
        {
            return response != null && StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeError);
        }

        private static bool IsRagResult(PipeEnvelope response)
        {
            return response != null
                && StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeRagResult)
                && StringComparer.Ordinal.Equals(response.PayloadSchema, RagRuntimeWire.ResultSchema);
        }

        private static FrameworkError CreateRagError(PipeEnvelope response, string correlationId)
        {
            if (response == null || !RagRuntimeWire.TryReadError(response.PayloadJson, out var errorCode, out var retryable))
            {
                throw new ClientProtocolException("rag_error_decode_failed");
            }

            return FrameworkErrors.Create(
                // The storage dispatcher emits fully qualified codes ("rag.index_stale",
                // "storage.unavailable", ...), so they pass through as-is.
                errorCode,
                MapRagErrorCategory(response.PayloadJson),
                "The RAG request failed.",
                correlationId,
                retryable,
                "MarcusAwakeRuntimeService");
        }

        private static FrameworkErrorCategory MapRagErrorCategory(string payload)
        {
            var wire = ProviderRuntimeJson.Deserialize<GenericErrorWire>(payload, out _);
            switch (wire == null ? string.Empty : (wire.Category ?? string.Empty))
            {
                case "invalid_request": return FrameworkErrorCategory.InvalidRequest;
                case "incompatible": return FrameworkErrorCategory.Incompatible;
                case "unsupported": return FrameworkErrorCategory.Unsupported;
                case "denied": return FrameworkErrorCategory.Denied;
                case "not_found": return FrameworkErrorCategory.NotFound;
                case "conflict": return FrameworkErrorCategory.Conflict;
                case "expired": return FrameworkErrorCategory.Expired;
                case "rate_limited": return FrameworkErrorCategory.RateLimited;
                case "provider_failure": return FrameworkErrorCategory.ProviderFailure;
                case "timeout": return FrameworkErrorCategory.Timeout;
                case "cancelled": return FrameworkErrorCategory.Cancelled;
                case "recovery_required": return FrameworkErrorCategory.RecoveryRequired;
                case "unavailable": return FrameworkErrorCategory.Unavailable;
                case "resource_exhausted": return FrameworkErrorCategory.ResourceExhausted;
                default: return FrameworkErrorCategory.InternalFailure;
            }
        }
    }
}
