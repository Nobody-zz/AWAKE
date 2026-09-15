using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class PersonaPersistenceConstants
{
    // schema 字面量单点化：权威定义在 AwakeStorageContract（schema registry）内，这里只做别名。
    internal const string ContinuitySchema = AwakeStorageContract.PersonaContinuitySchema;
    internal const string OverrideSchema = AwakeStorageContract.PersonaOverrideSchema;
    internal const string RecoverySchema = AwakeStorageContract.PersonaRecoverySchema;
    internal const string SessionPending = "session_pending";
    internal const string UnsavedRecovery = "unsaved_recovery";
    internal const string SaveCommitted = "save_committed";
}

internal sealed class PersonaTimelineIdentity
{
    [JsonProperty("campaignId")]
    internal string CampaignId { get; set; } = string.Empty;
    [JsonProperty("saveId")]
    internal string SaveId { get; set; } = string.Empty;
    [JsonProperty("timelineId")]
    internal string TimelineId { get; set; } = string.Empty;
    [JsonProperty("branchId")]
    internal string BranchId { get; set; } = string.Empty;
    [JsonProperty("parentBranchId")]
    internal string ParentBranchId { get; set; } = string.Empty;
    [JsonProperty("forkSequence")]
    internal long ForkSequence { get; set; }
}

internal sealed class PersonaProjectionWatermarks
{
    [JsonProperty("transcriptAcceptedSequence")]
    internal long TranscriptAcceptedSequence { get; set; }
    [JsonProperty("effectsAcceptedSequence")]
    internal long EffectsAcceptedSequence { get; set; }
    [JsonProperty("memoryAcceptedSequence")]
    internal long MemoryAcceptedSequence { get; set; }
    [JsonProperty("personaAcceptedSequence")]
    internal long PersonaAcceptedSequence { get; set; }
}

internal sealed class PersonaPersistenceEnvelope
{
    [JsonProperty("schema")]
    internal string Schema { get; set; } = PersonaPersistenceConstants.ContinuitySchema;
    [JsonProperty("characterId")]
    internal string CharacterId { get; set; } = string.Empty;
    [JsonProperty("timeline")]
    internal PersonaTimelineIdentity Timeline { get; set; } = new PersonaTimelineIdentity();
    [JsonProperty("sequence")]
    internal long Sequence { get; set; }
    [JsonProperty("watermarks")]
    internal PersonaProjectionWatermarks Watermarks { get; set; } = new PersonaProjectionWatermarks();
    [JsonProperty("source")]
    internal string Source { get; set; } = string.Empty;
    [JsonProperty("payloadHash")]
    internal string PayloadHash { get; set; } = string.Empty;
}

internal sealed class PersonaRuntimeStateDocument
{
    [JsonProperty("schema")]
    internal string Schema { get; set; } = PersonaPersistenceConstants.ContinuitySchema;
    [JsonProperty("personaSubjectStableId")]
    internal string PersonaSubjectStableId { get; set; } = string.Empty;
    [JsonProperty("characterId")]
    internal string CharacterId { get; set; } = string.Empty;
    [JsonProperty("timeline")]
    internal PersonaTimelineIdentity Timeline { get; set; } = new PersonaTimelineIdentity();
    [JsonProperty("activeBundleId")]
    internal string ActiveBundleId { get; set; } = string.Empty;
    [JsonProperty("activeBundleRevision")]
    internal int ActiveBundleRevision { get; set; }
    [JsonProperty("activeBundleDigest")]
    internal string ActiveBundleDigest { get; set; } = string.Empty;
    [JsonProperty("payloadDigest")]
    internal string PayloadDigest { get; set; } = string.Empty;
    [JsonProperty("revision")]
    internal long Revision { get; set; }
    [JsonProperty("sequence")]
    internal long Sequence { get; set; }
    [JsonProperty("watermarks")]
    internal PersonaProjectionWatermarks Watermarks { get; set; } = new PersonaProjectionWatermarks();
    [JsonProperty("continuity")]
    internal PersonaContinuityState Continuity { get; set; } = new PersonaContinuityState();

    internal JObject ToJsonObject()
    {
        return JObject.Parse(JsonConvert.SerializeObject(this, Formatting.None));
    }

    internal string ToCanonicalJson()
    {
        JObject canonical = ToJsonObject();
        canonical.Remove("payloadDigest");
        return canonical.ToString(Formatting.None);
    }

    internal string ComputePayloadDigest()
    {
        using (SHA256 sha = SHA256.Create())
        {
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(ToCanonicalJson())))
                .Replace("-", string.Empty)
                .ToUpperInvariant();
        }
    }

    internal PersonaRuntimeStateDocument DeepClone()
    {
        return FromJson(ToJsonObject().ToString(Formatting.None));
    }

    internal static PersonaRuntimeStateDocument FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            PersonaRuntimeStateDocument document = JsonConvert.DeserializeObject<PersonaRuntimeStateDocument>(json);
            return document;
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class PersonaStateWriteResult
{
    internal bool Applied { get; set; }
    internal bool Duplicate { get; set; }
    internal bool CommitUnknown { get; set; }
    internal string Code { get; set; } = string.Empty;
}

// 未启用：recovery 状态机属下一批次，本批次保留模型但不给消费者。
internal sealed class PersonaRecoveryRecord
{
    internal string Schema { get; set; } = PersonaPersistenceConstants.RecoverySchema;
    internal string CommitGroupId { get; set; } = string.Empty;
    internal string Status { get; set; } = PersonaPersistenceConstants.SessionPending;
    internal bool SaveAnchorConfirmed { get; set; }
    internal PersonaTimelineIdentity Timeline { get; set; } = new PersonaTimelineIdentity();
    internal long Sequence { get; set; }
    internal string TranscriptKey { get; set; } = string.Empty;
    internal string MemoryProjectionKey { get; set; } = string.Empty;
    internal string EffectsKey { get; set; } = string.Empty;
}

// G3-B runtime state uses the campaign namespace key; PersonaContinuitySync remains player-anchor-only.
internal static class PersonaStorageKey
{
    internal const string NamespaceId = "awake.persona.state";

    internal static bool TryBuild(
        PersonaTimelineIdentity timeline,
        string personaSubjectStableId,
        out string key,
        out string error)
    {
        key = string.Empty;
        error = string.Empty;
        if (timeline == null || string.IsNullOrWhiteSpace(timeline.CampaignId)
            || string.IsNullOrWhiteSpace(timeline.TimelineId)
            || string.IsNullOrWhiteSpace(timeline.BranchId))
        {
            error = "persona.storage.timeline_key_fields_missing";
            return false;
        }
        if (string.IsNullOrWhiteSpace(personaSubjectStableId))
        {
            error = "persona.storage.subject_key_missing";
            return false;
        }
        if (!personaSubjectStableId.StartsWith("hero:", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(personaSubjectStableId.Substring("hero:".Length)))
        {
            error = "persona.storage.subject_key_invalid";
            return false;
        }
        key = string.Join("|", new[]
        {
            Escape(timeline.CampaignId),
            Escape(timeline.TimelineId),
            Escape(timeline.BranchId),
            Escape(personaSubjectStableId)
        });
        return true;
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("%", "%25").Replace("|", "%7C");
    }
}
internal static class PersonaPersistenceValidator
{
    internal static bool TryValidateEnvelope(PersonaPersistenceEnvelope envelope, out string error)
    {
        error = string.Empty;
        if (envelope == null) return Fail("persona.persistence.envelope_missing", out error);
        if (!StringComparer.Ordinal.Equals(envelope.Schema, PersonaPersistenceConstants.ContinuitySchema)
            && !StringComparer.Ordinal.Equals(envelope.Schema, PersonaPersistenceConstants.OverrideSchema)) return Fail("persona.persistence.schema_invalid", out error);
        if (string.IsNullOrWhiteSpace(envelope.CharacterId)) return Fail("persona.persistence.character_missing", out error);
        if (!TryValidateTimeline(envelope.Timeline, out error)) return false;
        if (envelope.Sequence < 0) return Fail("persona.persistence.sequence_invalid", out error);
        if (envelope.Watermarks == null) return Fail("persona.persistence.watermarks_missing", out error);
        if (envelope.Watermarks.TranscriptAcceptedSequence > envelope.Sequence
            || envelope.Watermarks.EffectsAcceptedSequence > envelope.Sequence
            || envelope.Watermarks.MemoryAcceptedSequence > envelope.Sequence
            || envelope.Watermarks.PersonaAcceptedSequence > envelope.Sequence)
        {
            return Fail("persona.persistence.watermark_ahead_of_sequence", out error);
        }
        return true;
    }

    internal static bool TryValidateRuntimeState(
        PersonaRuntimeStateDocument document,
        PersonaTimelineIdentity expectedTimeline,
        string expectedSubjectStableId,
        RuntimeBundle expectedBundle,
        out string error)
    {
        error = string.Empty;
        if (document == null) return Fail("persona.runtime_state.document_missing", out error);
        if (!StringComparer.Ordinal.Equals(document.Schema, PersonaPersistenceConstants.ContinuitySchema))
            return Fail("persona.runtime_state.schema_invalid", out error);
        if (string.IsNullOrWhiteSpace(document.PersonaSubjectStableId)
            || !document.PersonaSubjectStableId.StartsWith("hero:", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(document.PersonaSubjectStableId.Substring("hero:".Length)))
            return Fail("persona.runtime_state.subject_invalid", out error);
        if (!string.IsNullOrWhiteSpace(expectedSubjectStableId)
            && !StringComparer.Ordinal.Equals(document.PersonaSubjectStableId, expectedSubjectStableId))
            return Fail("persona.runtime_state.subject_mismatch", out error);
        if (string.IsNullOrWhiteSpace(document.CharacterId))
            return Fail("persona.runtime_state.character_missing", out error);
        if (!TryValidateTimeline(document.Timeline, out error)) return false;
        if (expectedTimeline != null && !SameTimeline(document.Timeline, expectedTimeline))
            return Fail("persona.runtime_state.timeline_mismatch", out error);
        if (string.IsNullOrWhiteSpace(document.ActiveBundleId)
            || document.ActiveBundleRevision <= 0
            || !IsSha256(document.ActiveBundleDigest))
            return Fail("persona.runtime_state.bundle_invalid", out error);
        if (expectedBundle != null
            && (!StringComparer.Ordinal.Equals(document.ActiveBundleId, expectedBundle.BundleId)
                || document.ActiveBundleRevision != expectedBundle.Revision
                || !StringComparer.OrdinalIgnoreCase.Equals(document.ActiveBundleDigest, expectedBundle.Digest)))
            return Fail("persona.runtime_state.bundle_mismatch", out error);
        if (document.PayloadDigest != document.ComputePayloadDigest())
            return Fail("persona.runtime_state.digest_invalid", out error);
        if (document.Revision < 0 || document.Sequence < 0)
            return Fail("persona.runtime_state.sequence_invalid", out error);
        if (document.Watermarks == null || document.Continuity == null)
            return Fail("persona.runtime_state.payload_missing", out error);
        if (document.Watermarks.TranscriptAcceptedSequence < 0
            || document.Watermarks.EffectsAcceptedSequence < 0
            || document.Watermarks.MemoryAcceptedSequence < 0
            || document.Watermarks.PersonaAcceptedSequence < 0
            || document.Watermarks.TranscriptAcceptedSequence > document.Sequence
            || document.Watermarks.EffectsAcceptedSequence > document.Sequence
            || document.Watermarks.MemoryAcceptedSequence > document.Sequence
            || document.Watermarks.PersonaAcceptedSequence > document.Sequence)
            return Fail("persona.runtime_state.watermark_invalid", out error);
        return true;
    }

    internal static bool AreSameRuntimeState(PersonaRuntimeStateDocument left, PersonaRuntimeStateDocument right)
    {
        return left != null && right != null
            && StringComparer.Ordinal.Equals(left.ToJsonObject().ToString(Formatting.None), right.ToJsonObject().ToString(Formatting.None));
    }

    private static bool SameTimeline(PersonaTimelineIdentity left, PersonaTimelineIdentity right)
    {
        return left != null && right != null
            && StringComparer.Ordinal.Equals(left.CampaignId, right.CampaignId)
            && StringComparer.Ordinal.Equals(left.TimelineId, right.TimelineId)
            && StringComparer.Ordinal.Equals(left.BranchId, right.BranchId)
            && StringComparer.Ordinal.Equals(left.ParentBranchId, right.ParentBranchId)
            && left.ForkSequence == right.ForkSequence;
    }

    private static bool IsSha256(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
        foreach (char item in value)
        {
            if (!Uri.IsHexDigit(item)) return false;
        }
        return true;
    }

    internal static bool TryValidateRecovery(PersonaRecoveryRecord record, out string error)
    {
        error = string.Empty;
        if (record == null) return Fail("persona.recovery.record_missing", out error);
        if (!StringComparer.Ordinal.Equals(record.Schema, PersonaPersistenceConstants.RecoverySchema)) return Fail("persona.recovery.schema_invalid", out error);
        if (string.IsNullOrWhiteSpace(record.CommitGroupId)) return Fail("persona.recovery.commit_group_missing", out error);
        if (!TryValidateTimeline(record.Timeline, out error)) return false;
        if (record.Sequence < 0) return Fail("persona.recovery.sequence_invalid", out error);
        bool knownStatus = StringComparer.Ordinal.Equals(record.Status, PersonaPersistenceConstants.SessionPending)
            || StringComparer.Ordinal.Equals(record.Status, PersonaPersistenceConstants.UnsavedRecovery)
            || StringComparer.Ordinal.Equals(record.Status, PersonaPersistenceConstants.SaveCommitted);
        if (!knownStatus) return Fail("persona.recovery.status_invalid", out error);
        if (StringComparer.Ordinal.Equals(record.Status, PersonaPersistenceConstants.SaveCommitted) && !record.SaveAnchorConfirmed)
        {
            return Fail("persona.recovery.save_commit_without_anchor", out error);
        }
        return true;
    }

    // 未启用：投影裁剪属下一批次（本批次 watermark 恒 0，无裁剪可做）。
    internal static bool IsAcceptedForProjection(PersonaPersistenceEnvelope envelope, long projectionSequence, long sequence)
    {
        if (envelope == null || projectionSequence < 0 || sequence < 0) return false;
        return sequence <= projectionSequence && sequence <= envelope.Sequence;
    }

    private static bool TryValidateTimeline(PersonaTimelineIdentity timeline, out string error)
    {
        error = string.Empty;
        if (timeline == null) return Fail("persona.persistence.timeline_missing", out error);
        if (string.IsNullOrWhiteSpace(timeline.CampaignId)) return Fail("persona.persistence.campaign_missing", out error);
        if (string.IsNullOrWhiteSpace(timeline.TimelineId)) return Fail("persona.persistence.timeline_id_missing", out error);
        if (string.IsNullOrWhiteSpace(timeline.BranchId)) return Fail("persona.persistence.branch_id_missing", out error);
        if (timeline.ForkSequence < 0) return Fail("persona.persistence.fork_sequence_invalid", out error);
        if (string.IsNullOrWhiteSpace(timeline.ParentBranchId) && timeline.ForkSequence != 0)
        {
            return Fail("persona.persistence.root_fork_sequence_invalid", out error);
        }
        return true;
    }

    private static bool Fail(string value, out string error)
    {
        error = value;
        return false;
    }
}
