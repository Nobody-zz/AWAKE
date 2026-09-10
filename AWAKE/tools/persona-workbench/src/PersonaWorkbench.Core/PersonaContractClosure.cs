using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PersonaWorkbench.Core;

public static class PersonaContractClosureVersion
{
    public const string ReceiptSchemaVersion = "persona-workbench.approval-receipt.v1";
    public const string HandoffSchemaVersion = "persona-workbench.authoring-handoff.v1";
    public const string AwakeApprovalNotRequested = "not_requested";
    public const int SourceRevision = 1;
    public const int AuthoringRevision = 1;
    public const int CrosswalkRevision = 1;
    public const int RegistryRevision = 1;
}

public static class AuthoringHandoffLifecycleStatus
{
    public const string Issued = "issued";
    public const string Accepted = "accepted";
    public const string Consumed = "consumed";
    public const string Expired = "expired";
}

public sealed class AuthoringHandoffEnvelopeContext
{
    public string WorkspaceId { get; init; } = string.Empty;
    public long Revision { get; init; }
}

public sealed class AuthoringHandoffProvenance
{
    [JsonIgnore] public string Producer { get; init; } = "persona_workbench";
    [JsonPropertyName("source_schema")] public string SourceSchema { get; init; } = string.Empty;
    [JsonPropertyName("source_id")] public string SourceId { get; init; } = string.Empty;
    [JsonPropertyName("source_revision")] public int SourceRevision { get; init; }
    [JsonPropertyName("source_sha256")] public string SourceSha256 { get; init; } = string.Empty;
    [JsonPropertyName("issuer_id")] public string IssuerId { get; init; } = string.Empty;
    [JsonIgnore] public string Authority { get; init; } = "persona_local_authoring";
    [JsonIgnore] public string ReceiptId { get; init; } = string.Empty;
    [JsonIgnore] public string EvidenceId { get; init; } = string.Empty;
}

public sealed class AuthoringHandoffEnvelope
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "awake.workstation.handoff-envelope.v1";
    [JsonPropertyName("handoff_id")] public string HandoffId { get; init; } = string.Empty;
    [JsonPropertyName("workspace_id")] public string WorkspaceId { get; init; } = string.Empty;
    [JsonPropertyName("document_id")] public string DocumentId { get; init; } = string.Empty;
    [JsonPropertyName("revision")] public long Revision { get; init; }
    [JsonPropertyName("content_sha256")] public string SharedContentSha256 { get; init; } = string.Empty;
    [JsonPropertyName("producer")] public string Producer { get; init; } = "persona_workbench";
    [JsonPropertyName("provenance")] public AuthoringHandoffProvenance Provenance { get; init; } = new();
    [JsonPropertyName("review_only")] public bool ReviewOnly { get; init; } = true;
    [JsonPropertyName("review_status")] public string SharedReviewStatus { get; init; } = "approved_local";
    [JsonPropertyName("issued_at_utc")] public DateTime IssuedAtUtc { get; init; }
    [JsonPropertyName("expires_at_utc")] public DateTime ExpiresAtUtc { get; init; }
    [JsonPropertyName("payload")] public string Payload { get; init; } = string.Empty;
    [JsonPropertyName("request_fingerprint")] public string RequestFingerprint { get; init; } = string.Empty;
    [JsonIgnore] public string ContentSha256 { get; init; } = string.Empty;
    [JsonIgnore] public string ReviewStatus { get; init; } = "local_approved";
    [JsonIgnore] public DateTime ExpiresAt { get; init; }
    [JsonIgnore] public string LifecycleStatus { get; init; } = AuthoringHandoffLifecycleStatus.Issued;
}

public sealed class ContractFingerprint
{
    [JsonPropertyName("revision")] public int Revision { get; init; }
    [JsonPropertyName("sha256")] public string Sha256 { get; init; } = string.Empty;
}

public sealed class ContractIssuerRecord
{
    [JsonPropertyName("issuerId")] public string IssuerId { get; init; } = string.Empty;
    [JsonPropertyName("sessionId")] public string SessionId { get; init; } = string.Empty;
    [JsonPropertyName("issuedAtUtc")] public DateTime IssuedAtUtc { get; init; }
}

public sealed class ContractSessionBinding
{
    [JsonPropertyName("sessionId")] public string SessionId { get; init; } = string.Empty;
    [JsonPropertyName("authorization")] public string Authorization { get; init; } = "session_csrf";
}

public sealed class ContractIssuerContext
{
    public string SessionId { get; init; } = string.Empty;
    public string IssuerId { get; init; } = string.Empty;
    public long Fence { get; init; }
}

public sealed class ApprovalReceipt
{
    [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; init; } = PersonaContractClosureVersion.ReceiptSchemaVersion;
    [JsonPropertyName("receiptId")] public string ReceiptId { get; init; } = string.Empty;
    [JsonPropertyName("localApproval")] public string LocalApproval { get; init; } = PersonaReviewStatus.Approved;
    [JsonPropertyName("awakeApproval")] public string AwakeApproval { get; init; } = PersonaContractClosureVersion.AwakeApprovalNotRequested;
    [JsonPropertyName("documentId")] public string DocumentId { get; init; } = string.Empty;
    [JsonPropertyName("contentSha256")] public string ContentSha256 { get; init; } = string.Empty;
    [JsonPropertyName("canonicalProofSha256")] public string CanonicalProofSha256 { get; init; } = string.Empty;
    [JsonPropertyName("evidenceId")] public string EvidenceId { get; init; } = string.Empty;
    [JsonPropertyName("issuedAtUtc")] public DateTime IssuedAtUtc { get; init; }
    [JsonPropertyName("expiresAtUtc")] public DateTime ExpiresAtUtc { get; init; }
    [JsonPropertyName("issuer")] public ContractIssuerRecord Issuer { get; init; } = new();
    [JsonPropertyName("session")] public ContractSessionBinding Session { get; init; } = new();
    [JsonPropertyName("fence")] public long Fence { get; init; }
    [JsonPropertyName("source")] public ContractFingerprint Source { get; init; } = new();
    [JsonPropertyName("authoring")] public ContractFingerprint Authoring { get; init; } = new();
    [JsonPropertyName("crosswalk")] public ContractFingerprint Crosswalk { get; init; } = new();
    [JsonPropertyName("registry")] public ContractFingerprint Registry { get; init; } = new();
    [JsonPropertyName("warnings")] public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class AuthoringHandoff
{
    [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; init; } = PersonaContractClosureVersion.HandoffSchemaVersion;
    [JsonPropertyName("handoffId")] public string HandoffId { get; init; } = string.Empty;
    [JsonPropertyName("receiptId")] public string ReceiptId { get; init; } = string.Empty;
    [JsonPropertyName("awakeApproval")] public string AwakeApproval { get; init; } = PersonaContractClosureVersion.AwakeApprovalNotRequested;
    [JsonPropertyName("documentId")] public string DocumentId { get; init; } = string.Empty;
    [JsonPropertyName("canonicalJson")] public string CanonicalJson { get; init; } = string.Empty;
    [JsonPropertyName("contentSha256")] public string ContentSha256 { get; init; } = string.Empty;
    [JsonPropertyName("canonicalProofSha256")] public string CanonicalProofSha256 { get; init; } = string.Empty;
    [JsonPropertyName("evidenceId")] public string EvidenceId { get; init; } = string.Empty;
    [JsonPropertyName("issuedAtUtc")] public DateTime IssuedAtUtc { get; init; }
    [JsonPropertyName("expiresAtUtc")] public DateTime ExpiresAtUtc { get; init; }
    [JsonPropertyName("issuer")] public ContractIssuerRecord Issuer { get; init; } = new();
    [JsonPropertyName("session")] public ContractSessionBinding Session { get; init; } = new();
    [JsonPropertyName("fence")] public long Fence { get; init; }
    [JsonPropertyName("source")] public ContractFingerprint Source { get; init; } = new();
    [JsonPropertyName("authoring")] public ContractFingerprint Authoring { get; init; } = new();
    [JsonPropertyName("crosswalk")] public ContractFingerprint Crosswalk { get; init; } = new();
    [JsonPropertyName("registry")] public ContractFingerprint Registry { get; init; } = new();
    [JsonPropertyName("requestFingerprint")] public string RequestFingerprint { get; init; } = string.Empty;
    [JsonPropertyName("envelope")] public AuthoringHandoffEnvelope Envelope { get; init; } = new();
}

public sealed class ContractClosureResult<T>
{
    private ContractClosureResult(T? value, IReadOnlyList<string> errors, string requestFingerprint)
    {
        Value = value;
        Errors = errors;
        RequestFingerprint = requestFingerprint;
    }

    public T? Value { get; }
    public IReadOnlyList<string> Errors { get; }
    public string RequestFingerprint { get; }
    public bool IsSuccess => Value != null && Errors.Count == 0;

    public static ContractClosureResult<T> Success(T value, string requestFingerprint = "") => new(value, Array.Empty<string>(), requestFingerprint);
    public static ContractClosureResult<T> Failure(params string[] errors) => new(default, errors, string.Empty);
}

public static class PersonaContractClosureValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null, WriteIndented = false };

    public static IReadOnlyList<string> ValidateReceipt(ApprovalReceipt receipt, PersonaDocument document, ContractIssuerContext? context, PersonaAuthoringContractAssets assets, DateTime nowUtc)
    {
        List<string> errors = new();
        if (receipt == null) return new[] { "receipt.required" };
        if (document == null) errors.Add("document.required");
        if (context == null) errors.Add("session.context_required");
        ValidateCommon(errors, receipt.SchemaVersion, PersonaContractClosureVersion.ReceiptSchemaVersion, receipt.DocumentId, receipt.LocalApproval, receipt.AwakeApproval, receipt.Issuer, receipt.Session, receipt.Fence, receipt.IssuedAtUtc, receipt.ExpiresAtUtc, context, assets, receipt.Source, receipt.Authoring, receipt.Crosswalk, receipt.Registry, nowUtc);
        if (document != null)
        {
            if (document.Status != PersonaReviewStatus.Approved) errors.Add("receipt.local_approval_required");
            PersonaAuthoringBuildResult built = PersonaAuthoringV2Adapter.Build(document, string.Empty, PersonaAuthoringExpansionOrigin.None, assets);
            if (!built.IsSuccess || built.Document == null) errors.AddRange(built.Diagnostics.Select(d => d.Code));
            else
            {
                string canonicalHash = HashUtf8(built.CanonicalJson);
                if (!string.Equals(receipt.DocumentId, built.Document.DocumentId, StringComparison.Ordinal)) errors.Add("receipt.document_mismatch");
                if (!string.Equals(receipt.ContentSha256, canonicalHash, StringComparison.Ordinal)) errors.Add("receipt.content_hash_mismatch");
                if (!string.Equals(receipt.CanonicalProofSha256, canonicalHash, StringComparison.Ordinal)) errors.Add("receipt.canonical_proof_mismatch");
                if (!string.Equals(receipt.Source.Sha256, SourceDocumentHash(document), StringComparison.Ordinal)) errors.Add("receipt.source_hash_mismatch");
                if (receipt.Warnings.Count > 0 && built.Warnings.Count == 0) errors.Add("receipt.warning_binding_mismatch");
                if (built.Warnings.Count > 0 && receipt.Warnings.Count == 0) errors.Add("receipt.warning_binding_missing");
            }
        }
        if (receipt.Warnings == null) errors.Add("receipt.warnings_invalid");
        if (string.IsNullOrWhiteSpace(receipt.EvidenceId)) errors.Add("receipt.evidence_required");
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> ValidateHandoff(AuthoringHandoff handoff, ApprovalReceipt receipt, PersonaDocument document, ContractIssuerContext context, PersonaAuthoringContractAssets assets, DateTime nowUtc, string? requestId = null)
    {
        List<string> errors = new();
        if (handoff == null) return new[] { "handoff.required" };
        if (receipt == null) errors.Add("receipt.required");
        if (document == null) errors.Add("document.required");
        if (context == null)
        {
            errors.Add("session.context_required");
            return errors.Distinct(StringComparer.Ordinal).ToArray();
        }
        ValidateCommon(errors, handoff.SchemaVersion, PersonaContractClosureVersion.HandoffSchemaVersion, handoff.DocumentId, string.Empty, handoff.AwakeApproval, handoff.Issuer, handoff.Session, handoff.Fence, handoff.IssuedAtUtc, handoff.ExpiresAtUtc, context, assets, handoff.Source, handoff.Authoring, handoff.Crosswalk, handoff.Registry, nowUtc);
        if (handoff.AwakeApproval != PersonaContractClosureVersion.AwakeApprovalNotRequested) errors.Add("handoff.awake_approval_forbidden");
        ValidateEnvelope(errors, handoff, receipt);
        if (receipt != null && document != null)
        {
            if (receipt.Session is not ContractSessionBinding receiptSession || receipt.Issuer is not ContractIssuerRecord receiptIssuer)
            {
                errors.Add("handoff.receipt_binding_invalid");
            }
            else
            {
                if (receiptSession.SessionId != context.SessionId || receiptIssuer.SessionId != context.SessionId) errors.Add("handoff.session_mismatch");
                errors.AddRange(ValidateReceipt(receipt, document, new ContractIssuerContext { SessionId = receiptSession.SessionId, IssuerId = receiptIssuer.IssuerId, Fence = receipt.Fence }, assets, nowUtc));
            }
            if (receipt.Warnings == null || receipt.Warnings.Count > 0) errors.Add("handoff.warnings_fail_closed");
            if (handoff.ReceiptId != receipt.ReceiptId) errors.Add("handoff.receipt_mismatch");
            if (handoff.DocumentId != receipt.DocumentId) errors.Add("handoff.document_mismatch");
            if (handoff.ContentSha256 != receipt.ContentSha256 || handoff.CanonicalProofSha256 != receipt.CanonicalProofSha256) errors.Add("handoff.hash_mismatch");
            if (!string.Equals(handoff.CanonicalJson, BuildCanonical(document, assets), StringComparison.Ordinal)) errors.Add("handoff.canonical_json_mismatch");
        }
        if (string.IsNullOrWhiteSpace(handoff.RequestFingerprint)) errors.Add("handoff.request_fingerprint_required");
        if (requestId != null) errors.AddRange(AuthoringHandoffSemanticValidator.Validate(handoff, requestId, nowUtc));
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static void ValidateEnvelope(List<string> errors, AuthoringHandoff handoff, ApprovalReceipt? receipt)
    {
        AuthoringHandoffEnvelope? envelope = handoff.Envelope;
        if (envelope == null)
        {
            errors.Add("handoff.envelope_required");
            return;
        }

        if (envelope.SchemaVersion != "awake.workstation.handoff-envelope.v1")
            errors.Add("handoff.envelope_schema_version_invalid");
        if (!IsValidWorkspaceId(envelope.WorkspaceId)) errors.Add("handoff.workspace_id_invalid");
        if (envelope.Revision < 1) errors.Add("handoff.revision_invalid");
        if (!IsSha256(envelope.SharedContentSha256) || !string.Equals(envelope.SharedContentSha256, handoff.ContentSha256.ToLowerInvariant(), StringComparison.Ordinal))
            errors.Add("handoff.envelope_content_hash_mismatch");
        if (envelope.Producer != "persona_workbench")
            errors.Add("handoff.envelope_producer_invalid");
        if (!string.Equals(envelope.DocumentId, handoff.DocumentId, StringComparison.Ordinal))
            errors.Add("handoff.envelope_document_mismatch");
        if (!string.Equals(envelope.HandoffId, handoff.HandoffId, StringComparison.Ordinal))
            errors.Add("handoff.envelope_id_mismatch");
        if (!envelope.ReviewOnly)
            errors.Add("handoff.envelope_review_only_invalid");
        if (envelope.SharedReviewStatus != "approved_local")
            errors.Add("handoff.envelope_review_status_invalid");
        if (envelope.IssuedAtUtc != handoff.IssuedAtUtc)
            errors.Add("handoff.envelope_issued_at_mismatch");
        if (envelope.ExpiresAtUtc != handoff.ExpiresAtUtc)
            errors.Add("handoff.envelope_expiry_mismatch");
        if (envelope.Provenance == null
            || envelope.Provenance.Producer != "persona_workbench"
            || receipt == null
            || envelope.Provenance.SourceSchema != PersonaContractClosureVersion.HandoffSchemaVersion
            || envelope.Provenance.SourceId != handoff.DocumentId
            || envelope.Provenance.SourceRevision != envelope.Revision
            || envelope.Provenance.SourceSha256 != envelope.SharedContentSha256
            || envelope.Provenance.IssuerId != receipt.Issuer.IssuerId)
            errors.Add("handoff.envelope_provenance_invalid");
        if (!string.Equals(envelope.Payload, handoff.CanonicalJson.TrimEnd('\r', '\n'), StringComparison.Ordinal))
            errors.Add("handoff.envelope_payload_mismatch");
        if (string.IsNullOrWhiteSpace(envelope.RequestFingerprint))
            errors.Add("handoff.envelope_request_fingerprint_required");
    }

    private static void ValidateCommon(List<string> errors, string schemaVersion, string expectedSchema, string documentId, string localApproval, string awakeApproval, ContractIssuerRecord issuer, ContractSessionBinding session, long fence, DateTime issuedAtUtc, DateTime expiresAtUtc, ContractIssuerContext? context, PersonaAuthoringContractAssets assets, ContractFingerprint source, ContractFingerprint authoring, ContractFingerprint crosswalk, ContractFingerprint registry, DateTime nowUtc)
    {
        if (context == null) { errors.Add("session.context_required"); return; }
        if (schemaVersion != expectedSchema) errors.Add("contract.schema_invalid");
        if (string.IsNullOrWhiteSpace(documentId)) errors.Add("contract.document_required");
        if (!string.IsNullOrWhiteSpace(localApproval) && localApproval != PersonaReviewStatus.Approved) errors.Add("contract.local_approval_invalid");
        if (awakeApproval != PersonaContractClosureVersion.AwakeApprovalNotRequested) errors.Add("contract.awake_approval_invalid");
        if (issuer == null || string.IsNullOrWhiteSpace(issuer.IssuerId) || issuer.SessionId != context.SessionId) errors.Add("issuer.invalid");
        if (session == null || session.SessionId != context.SessionId || session.Authorization != "session_csrf") errors.Add("session.binding_invalid");
        if (issuer != null && issuer.IssuerId != context.IssuerId) errors.Add("issuer.record_invalid");
        if (fence < 1 || fence != context.Fence) errors.Add("session.fence_invalid");
        if (issuedAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc.Kind != DateTimeKind.Utc || issuedAtUtc > expiresAtUtc || expiresAtUtc <= nowUtc) errors.Add("contract.expired");
        ContractFingerprint[] fingerprints = { source, authoring, crosswalk, registry };
        if (fingerprints.Any(f => f == null || f.Revision < 1 || !IsSha256(f.Sha256))) errors.Add("contract.fingerprint_invalid");
        if (assets == null || !MatchesAssets(source, authoring, crosswalk, registry, assets)) errors.Add("contract.assets_drifted");
    }

    private static bool MatchesAssets(ContractFingerprint source, ContractFingerprint authoring, ContractFingerprint crosswalk, ContractFingerprint registry, PersonaAuthoringContractAssets assets)
    {
        return source.Revision == PersonaContractClosureVersion.SourceRevision
            && authoring.Revision == assets.AuthoringRevision
            && crosswalk.Revision == assets.CrosswalkRevision
            && registry.Revision == assets.RegistryRevision
            && source.Revision == PersonaContractClosureVersion.SourceRevision
            && authoring.Sha256 == assets.CurrentAuthoringSha256
            && crosswalk.Sha256 == assets.CurrentCrosswalkSha256
            && registry.Sha256 == assets.CurrentRegistrySha256;
    }

    public static string BuildCanonical(PersonaDocument document, PersonaAuthoringContractAssets assets)
    {
        PersonaAuthoringBuildResult result = PersonaAuthoringV2Adapter.Build(document, string.Empty, PersonaAuthoringExpansionOrigin.None, assets);
        if (!result.IsSuccess) throw new InvalidOperationException("Cannot build canonical authoring document.");
        return result.CanonicalJson;
    }

    public static string HashUtf8(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string SourceDocumentHash(PersonaDocument document) => HashUtf8(JsonSerializer.Serialize(document, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false }));
    public static string RequestFingerprint(string requestId, string content) => HashUtf8(requestId + "\n" + content);
    private static bool IsSha256(string? value) => value?.Length == 64 && value.All(c => Uri.IsHexDigit(c));
    public static bool IsValidWorkspaceId(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && Regex.IsMatch(value, "^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant);
}

public sealed class PersonaContractClosureService
{
    private readonly object _gate = new();
    private readonly PersonaAuthoringContractAssets _assets;
    private readonly Func<DateTime> _utcNow;
    private readonly Dictionary<string, (string Fingerprint, ApprovalReceipt Receipt)> _receiptsByRequest = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Fingerprint, AuthoringHandoff Handoff)> _handoffsByRequest = new(StringComparer.Ordinal);
    private readonly HashSet<string> _consumedReceiptIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _consumedHandoffIds = new(StringComparer.Ordinal);

    public PersonaContractClosureService(PersonaAuthoringContractAssets? assets = null, Func<DateTime>? utcNow = null)
    {
        _assets = assets ?? PersonaAuthoringContractAssets.LoadEmbedded();
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public PersonaAuthoringContractAssets Assets => _assets;

    public ContractClosureResult<ApprovalReceipt> IssueReceipt(PersonaDocument document, ContractIssuerContext context, string requestId, string evidenceId, TimeSpan lifetime)
    {
        if (document == null) return ContractClosureResult<ApprovalReceipt>.Failure("document.required");
        if (document.Status != PersonaReviewStatus.Approved) return ContractClosureResult<ApprovalReceipt>.Failure("receipt.local_approval_required");
        if (context == null || string.IsNullOrWhiteSpace(context.SessionId) || string.IsNullOrWhiteSpace(context.IssuerId) || context.Fence < 1) return ContractClosureResult<ApprovalReceipt>.Failure("session.context_invalid");
        DateTime now = EnsureUtc(_utcNow());
        PersonaAuthoringBuildResult built = PersonaAuthoringV2Adapter.Build(document, string.Empty, PersonaAuthoringExpansionOrigin.None, _assets);
        if (!built.IsSuccess || built.Document == null) return ContractClosureResult<ApprovalReceipt>.Failure(built.Diagnostics.Select(d => d.Code).Concat(new[] { built.ErrorCode }).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray());
        string fingerprint = PersonaContractClosureValidator.RequestFingerprint(requestId, built.CanonicalJson + "\n" + evidenceId);
        string requestKey = context.SessionId + ":" + requestId;
        lock (_gate)
        {
            if (_receiptsByRequest.TryGetValue(requestKey, out var existing))
            {
                return existing.Fingerprint == fingerprint ? ContractClosureResult<ApprovalReceipt>.Success(existing.Receipt, fingerprint) : ContractClosureResult<ApprovalReceipt>.Failure("receipt.replay_conflict");
            }
            ApprovalReceipt receipt = new()
            {
                ReceiptId = "pwb-receipt-" + Guid.NewGuid().ToString("N"),
                DocumentId = built.Document.DocumentId,
                ContentSha256 = PersonaContractClosureValidator.HashUtf8(built.CanonicalJson),
                CanonicalProofSha256 = PersonaContractClosureValidator.HashUtf8(built.CanonicalJson),
                EvidenceId = evidenceId ?? string.Empty,
                IssuedAtUtc = now,
                ExpiresAtUtc = now.Add(lifetime <= TimeSpan.Zero ? TimeSpan.FromMinutes(30) : lifetime),
                Issuer = new ContractIssuerRecord { IssuerId = context.IssuerId, SessionId = context.SessionId, IssuedAtUtc = now },
                Session = new ContractSessionBinding { SessionId = context.SessionId },
                Fence = context.Fence,
                Source = Fingerprint(_assets.SourceRevision, PersonaContractClosureValidator.SourceDocumentHash(document)),
                Authoring = Fingerprint(_assets.AuthoringRevision, _assets.CurrentAuthoringSha256),
                Crosswalk = Fingerprint(_assets.CrosswalkRevision, _assets.CurrentCrosswalkSha256),
                Registry = Fingerprint(_assets.RegistryRevision, _assets.CurrentRegistrySha256),
                Warnings = built.Warnings.ToArray()
            };
            _receiptsByRequest[requestKey] = (fingerprint, receipt);
            return ContractClosureResult<ApprovalReceipt>.Success(receipt, fingerprint);
        }
    }

    public ContractClosureResult<AuthoringHandoff> IssueHandoff(ApprovalReceipt receipt, PersonaDocument document, ContractIssuerContext context, AuthoringHandoffEnvelopeContext envelopeContext, string requestId, TimeSpan lifetime)
    {
        if (receipt == null) return ContractClosureResult<AuthoringHandoff>.Failure("receipt.required");
        if (document == null) return ContractClosureResult<AuthoringHandoff>.Failure("document.required");
        if (context == null || string.IsNullOrWhiteSpace(context.SessionId) || string.IsNullOrWhiteSpace(context.IssuerId) || context.Fence < 1) return ContractClosureResult<AuthoringHandoff>.Failure("session.context_invalid");
        if (envelopeContext == null || !PersonaContractClosureValidator.IsValidWorkspaceId(envelopeContext.WorkspaceId)) return ContractClosureResult<AuthoringHandoff>.Failure("handoff.workspace_id_invalid");
        if (envelopeContext.Revision < 1) return ContractClosureResult<AuthoringHandoff>.Failure("handoff.revision_invalid");
        DateTime now = EnsureUtc(_utcNow());
        if (receipt.Session == null || receipt.Issuer == null) return ContractClosureResult<AuthoringHandoff>.Failure("receipt.session_binding_invalid");
        ContractIssuerContext receiptContext = new() { SessionId = receipt.Session.SessionId, IssuerId = receipt.Issuer.IssuerId, Fence = receipt.Fence };
        IReadOnlyList<string> receiptErrors = PersonaContractClosureValidator.ValidateReceipt(receipt, document, receiptContext, _assets, now);
        if (receiptErrors.Count > 0) return ContractClosureResult<AuthoringHandoff>.Failure(receiptErrors.ToArray());
        string canonical = PersonaContractClosureValidator.BuildCanonical(document, _assets);
        string fingerprint = PersonaContractClosureValidator.RequestFingerprint(requestId, receipt.ReceiptId + "\n" + envelopeContext.WorkspaceId + "\n" + envelopeContext.Revision + "\n" + canonical);
        string requestKey = context.SessionId + ":" + requestId;
        lock (_gate)
        {
            if (_handoffsByRequest.TryGetValue(requestKey, out var existing))
            {
                return existing.Fingerprint == fingerprint ? ContractClosureResult<AuthoringHandoff>.Success(existing.Handoff, fingerprint) : ContractClosureResult<AuthoringHandoff>.Failure("handoff.replay_conflict");
            }
            if (_consumedReceiptIds.Contains(receipt.ReceiptId)) return ContractClosureResult<AuthoringHandoff>.Failure("receipt.replay_conflict");
            string handoffId = "pwb-handoff-" + Guid.NewGuid().ToString("N");
            DateTime expiresAt = now.Add(lifetime <= TimeSpan.Zero ? TimeSpan.FromMinutes(30) : lifetime);
            string sharedCanonical = canonical.TrimEnd('\r', '\n');
            string sharedContentHash = PersonaContractClosureValidator.HashUtf8(sharedCanonical).ToLowerInvariant();
            AuthoringHandoffEnvelope sharedEnvelope = new()
            {
                SchemaVersion = "awake.workstation.handoff-envelope.v1",
                HandoffId = handoffId,
                WorkspaceId = envelopeContext.WorkspaceId,
                DocumentId = receipt.DocumentId,
                Revision = envelopeContext.Revision,
                SharedContentSha256 = sharedContentHash,
                Producer = "persona_workbench",
                Provenance = new AuthoringHandoffProvenance
                {
                    Producer = "persona_workbench",
                    SourceSchema = PersonaContractClosureVersion.HandoffSchemaVersion,
                    SourceId = receipt.DocumentId,
                    SourceRevision = checked((int)envelopeContext.Revision),
                    SourceSha256 = sharedContentHash,
                    IssuerId = receipt.Issuer.IssuerId,
                    ReceiptId = receipt.ReceiptId,
                    EvidenceId = receipt.EvidenceId
                },
                ReviewOnly = true,
                SharedReviewStatus = "approved_local",
                IssuedAtUtc = now,
                ExpiresAtUtc = expiresAt,
                    Payload = sharedCanonical,
                RequestFingerprint = string.Empty,
                ContentSha256 = receipt.ContentSha256,
                ReviewStatus = "local_approved",
                ExpiresAt = expiresAt,
                LifecycleStatus = AuthoringHandoffLifecycleStatus.Issued
            };
            string sharedRequestFingerprint = AuthoringHandoffSharedFingerprint.Compute(sharedEnvelope);
            sharedEnvelope = new AuthoringHandoffEnvelope
            {
                SchemaVersion = sharedEnvelope.SchemaVersion,
                HandoffId = sharedEnvelope.HandoffId,
                WorkspaceId = sharedEnvelope.WorkspaceId,
                DocumentId = sharedEnvelope.DocumentId,
                Revision = sharedEnvelope.Revision,
                SharedContentSha256 = sharedEnvelope.SharedContentSha256,
                Producer = sharedEnvelope.Producer,
                Provenance = sharedEnvelope.Provenance,
                ReviewOnly = sharedEnvelope.ReviewOnly,
                SharedReviewStatus = sharedEnvelope.SharedReviewStatus,
                IssuedAtUtc = sharedEnvelope.IssuedAtUtc,
                ExpiresAtUtc = sharedEnvelope.ExpiresAtUtc,
                Payload = sharedEnvelope.Payload,
                RequestFingerprint = sharedRequestFingerprint,
                ContentSha256 = sharedEnvelope.ContentSha256,
                ReviewStatus = sharedEnvelope.ReviewStatus,
                ExpiresAt = sharedEnvelope.ExpiresAt,
                LifecycleStatus = sharedEnvelope.LifecycleStatus
            };
            AuthoringHandoff handoff = new()
            {
                HandoffId = handoffId,
                ReceiptId = receipt.ReceiptId,
                DocumentId = receipt.DocumentId,
                CanonicalJson = canonical,
                ContentSha256 = receipt.ContentSha256,
                CanonicalProofSha256 = receipt.CanonicalProofSha256,
                EvidenceId = receipt.EvidenceId,
                IssuedAtUtc = now,
                ExpiresAtUtc = expiresAt,
                Issuer = new ContractIssuerRecord { IssuerId = context.IssuerId, SessionId = context.SessionId, IssuedAtUtc = now },
                Session = new ContractSessionBinding { SessionId = context.SessionId },
                Fence = context.Fence,
                Source = receipt.Source,
                Authoring = receipt.Authoring,
                Crosswalk = receipt.Crosswalk,
                Registry = receipt.Registry,
                RequestFingerprint = fingerprint,
                Envelope = sharedEnvelope
            };
            IReadOnlyList<string> errors = PersonaContractClosureValidator.ValidateHandoff(handoff, receipt, document, context, _assets, now, requestId);
            if (errors.Count > 0) return ContractClosureResult<AuthoringHandoff>.Failure(errors.ToArray());
            _consumedReceiptIds.Add(receipt.ReceiptId);
            _consumedHandoffIds.Add(handoff.HandoffId);
            _handoffsByRequest[requestKey] = (fingerprint, handoff);
            return ContractClosureResult<AuthoringHandoff>.Success(handoff, fingerprint);
        }
    }

    private static ContractFingerprint Fingerprint(int revision, string sha256) => new() { Revision = revision, Sha256 = sha256 };
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}







