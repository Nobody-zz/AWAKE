using System.Text.Json;
using System.Text.RegularExpressions;

namespace PersonaWorkbench.Core;

public static class AuthoringHandoffSemanticValidator
{
    private static readonly Regex HandoffIdPattern = new(
        "^pwb-handoff-[a-f0-9]{32}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<string> Validate(
        AuthoringHandoff handoff,
        string requestId,
        DateTime nowUtc)
    {
        List<string> errors = new();
        if (handoff == null) return new[] { "handoff.required" };

        DateTime normalizedNow = nowUtc.Kind == DateTimeKind.Utc
            ? nowUtc
            : nowUtc.ToUniversalTime();

        if (handoff.SchemaVersion != PersonaContractClosureVersion.HandoffSchemaVersion)
            errors.Add("handoff.schema_version_invalid");
        if (!HandoffIdPattern.IsMatch(handoff.HandoffId))
            errors.Add("handoff.id_invalid");
        if (handoff.IssuedAtUtc.Kind != DateTimeKind.Utc
            || handoff.ExpiresAtUtc.Kind != DateTimeKind.Utc)
            errors.Add("handoff.timestamp_not_utc");
        if (handoff.ExpiresAtUtc <= handoff.IssuedAtUtc)
            errors.Add("handoff.expiry_order_invalid");
        if (handoff.ExpiresAtUtc <= normalizedNow)
            errors.Add("handoff.expired");

        AuthoringHandoffEnvelope? envelope = handoff.Envelope;
        if (envelope == null)
        {
            errors.Add("handoff.envelope_required");
        }
        else
        {
            if (!PersonaContractClosureValidator.IsValidWorkspaceId(envelope.WorkspaceId))
                errors.Add("handoff.workspace_id_invalid");
            if (envelope.Revision < 1)
                errors.Add("handoff.revision_invalid");
            if (envelope.SchemaVersion != "awake.workstation.handoff-envelope.v1")
                errors.Add("handoff.envelope_schema_version_invalid");
            if (envelope.Provenance == null
                || envelope.Producer != "persona_workbench"
                || envelope.Provenance.Producer != "persona_workbench")
                errors.Add("handoff.provenance_invalid");
            if (envelope.Producer == "persona_workbench"
                && !handoff.HandoffId.StartsWith("pwb-", StringComparison.Ordinal))
                errors.Add("handoff.id_producer_mismatch");
            if (envelope.DocumentId != handoff.DocumentId)
                errors.Add("handoff.envelope_document_mismatch");
            if (envelope.HandoffId != handoff.HandoffId)
                errors.Add("handoff.envelope_id_mismatch");
            if (envelope.SharedContentSha256 != handoff.ContentSha256.ToLowerInvariant())
                errors.Add("handoff.envelope_content_hash_mismatch");
            if (!envelope.ReviewOnly)
                errors.Add("handoff.review_only_invalid");
            if (envelope.SharedReviewStatus != "approved_local")
                errors.Add("handoff.review_status_invalid");
            if (envelope.IssuedAtUtc != handoff.IssuedAtUtc)
                errors.Add("handoff.envelope_issued_at_mismatch");
            if (envelope.ExpiresAtUtc != handoff.ExpiresAtUtc)
                errors.Add("handoff.envelope_expiry_mismatch");
            if (envelope.IssuedAtUtc.Kind != DateTimeKind.Utc
                || envelope.ExpiresAtUtc.Kind != DateTimeKind.Utc)
                errors.Add("handoff.envelope_timestamp_not_utc");
            if (envelope.Provenance != null
                && string.IsNullOrWhiteSpace(envelope.Provenance.SourceId))
                errors.Add("handoff.provenance_receipt_required");
            if (envelope.Provenance != null
                && string.IsNullOrWhiteSpace(envelope.Provenance.SourceSha256))
                errors.Add("handoff.provenance_source_hash_required");
            if (!string.Equals(envelope.Payload, handoff.CanonicalJson, StringComparison.Ordinal))
                errors.Add("handoff.envelope_payload_mismatch");
            if (string.IsNullOrWhiteSpace(envelope.RequestFingerprint))
                errors.Add("handoff.envelope_request_fingerprint_required");
        }

        if (string.IsNullOrWhiteSpace(handoff.CanonicalJson))
        {
            errors.Add("handoff.canonical_payload_required");
        }
        else
        {
            if (handoff.CanonicalJson[0] == '\uFEFF')
                errors.Add("handoff.canonical_payload_format_invalid");

            try
            {
                using JsonDocument document = JsonDocument.Parse(handoff.CanonicalJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    errors.Add("handoff.canonical_payload_invalid");
            }
            catch (JsonException)
            {
                errors.Add("handoff.canonical_payload_invalid");
            }

            string payloadHash = PersonaContractClosureValidator.HashUtf8(handoff.CanonicalJson);
            if (!string.Equals(handoff.ContentSha256.ToLowerInvariant(), payloadHash.ToLowerInvariant(), StringComparison.Ordinal))
                errors.Add("handoff.content_hash_mismatch");
            if (!string.Equals(handoff.CanonicalProofSha256.ToLowerInvariant(), payloadHash.ToLowerInvariant(), StringComparison.Ordinal))
                errors.Add("handoff.canonical_proof_mismatch");
        }

        if (string.IsNullOrWhiteSpace(requestId))
        {
            errors.Add("handoff.request_id_required");
        }
        else if (envelope != null)
        {
            string expectedFingerprint = AuthoringHandoffSharedFingerprint.Compute(envelope);
            if (!string.Equals(envelope.RequestFingerprint, expectedFingerprint, StringComparison.Ordinal))
                errors.Add("handoff.request_fingerprint_mismatch");
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }
}
