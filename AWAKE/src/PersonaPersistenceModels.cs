using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Awake;

internal static class PersonaPersistenceConstants
{
    internal const string ContinuitySchema = "awake.persona.continuity.v1";
    internal const string OverrideSchema = "awake.persona.override.v1";
    internal const string RecoverySchema = "awake.persona.recovery.v1";
    internal const string SessionPending = "session_pending";
    internal const string UnsavedRecovery = "unsaved_recovery";
    internal const string SaveCommitted = "save_committed";
}

internal sealed class PersonaTimelineIdentity
{
    internal string CampaignId { get; set; } = string.Empty;
    internal string SaveId { get; set; } = string.Empty;
    internal string TimelineId { get; set; } = string.Empty;
    internal string BranchId { get; set; } = string.Empty;
    internal string ParentBranchId { get; set; } = string.Empty;
    internal long ForkSequence { get; set; }
}

internal sealed class PersonaProjectionWatermarks
{
    internal long TranscriptAcceptedSequence { get; set; }
    internal long EffectsAcceptedSequence { get; set; }
    internal long MemoryAcceptedSequence { get; set; }
    internal long PersonaAcceptedSequence { get; set; }
}

internal sealed class PersonaPersistenceEnvelope
{
    internal string Schema { get; set; } = PersonaPersistenceConstants.ContinuitySchema;
    internal string CharacterId { get; set; } = string.Empty;
    internal PersonaTimelineIdentity Timeline { get; set; } = new PersonaTimelineIdentity();
    internal long Sequence { get; set; }
    internal PersonaProjectionWatermarks Watermarks { get; set; } = new PersonaProjectionWatermarks();
    internal string Source { get; set; } = string.Empty;
    internal string PayloadHash { get; set; } = string.Empty;
}

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

internal static class PersonaStorageKey
{
    internal const string NamespaceId = "awake.persona.state";

    internal static bool TryBuild(
        PersonaTimelineIdentity timeline,
        string characterId,
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
        if (string.IsNullOrWhiteSpace(characterId))
        {
            error = "persona.storage.character_key_missing";
            return false;
        }
        key = string.Join("|", new[]
        {
            Escape(timeline.CampaignId),
            Escape(timeline.TimelineId),
            Escape(timeline.BranchId),
            Escape(characterId)
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