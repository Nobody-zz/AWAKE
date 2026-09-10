using System;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class AwakeStorageContract
{
    internal const string MemorySchema = "awake.npc.memory.v1";
    internal const string RelationshipSchema = "awake.relationship.state.v1";
    internal const string EventMetaSchema = "awake.event_meta.v1";
    internal const string ProactiveSchema = "awake.npc.proactive.v1";
    internal const string WorldEventsSchema = "awake.world_events.v1";
    internal const string MessengerSchema = "awake.messenger.v1";
    internal const string TranscriptSchema = AwakeTranscriptConstants.Schema;
    internal const string TranscriptMetaSchema = AwakeTranscriptConstants.MetaSchema;
    internal const string ContactsSchema = AwakeTranscriptConstants.ContactsSchema;
    internal const string AuditSchema = AwakeTranscriptConstants.AuditSchema;
    internal const string OnboardingSchema = "awake.onboarding.v1";
    internal const string DialogueQueueSchema = "awake.dialogue.queue.v1";
    internal const string InteractionSchema = "awake.interactions.v1";
    internal const string InteractionRecoveryIndexSchema = "awake.interactions.recovery-index.v1";
    internal const string PersonaContinuitySchema = "awake.persona.continuity.v1";
    internal const string PersonaOverrideSchema = "awake.persona.override.v1";
    internal const string PersonaRecoverySchema = "awake.persona.recovery.v1";
    internal const string WorldbookOverlaySchema = "awake.worldbook.overlay.v1";

    internal static bool IsKnownSchema(string schema)
    {
        return StringComparer.Ordinal.Equals(schema, MemorySchema)
            || StringComparer.Ordinal.Equals(schema, RelationshipSchema)
            || StringComparer.Ordinal.Equals(schema, EventMetaSchema)
            || StringComparer.Ordinal.Equals(schema, ProactiveSchema)
            || StringComparer.Ordinal.Equals(schema, WorldEventsSchema)
            || StringComparer.Ordinal.Equals(schema, MessengerSchema)
            || StringComparer.Ordinal.Equals(schema, TranscriptSchema)
            || StringComparer.Ordinal.Equals(schema, TranscriptMetaSchema)
            || StringComparer.Ordinal.Equals(schema, ContactsSchema)
            || StringComparer.Ordinal.Equals(schema, AuditSchema)
            || StringComparer.Ordinal.Equals(schema, OnboardingSchema)
            || StringComparer.Ordinal.Equals(schema, DialogueQueueSchema)
            || StringComparer.Ordinal.Equals(schema, InteractionSchema)
            || StringComparer.Ordinal.Equals(schema, InteractionRecoveryIndexSchema)
            || StringComparer.Ordinal.Equals(schema, PersonaContinuitySchema)
            || StringComparer.Ordinal.Equals(schema, PersonaOverrideSchema)
            || StringComparer.Ordinal.Equals(schema, PersonaRecoverySchema);
    }

    internal static string ExpectedSchema(WorldStateKind kind)
    {
        switch (kind)
        {
            case WorldStateKind.Memory:
                return MemorySchema;
            case WorldStateKind.Relationship:
                return RelationshipSchema;
            case WorldStateKind.EventMeta:
                return EventMetaSchema;
            case WorldStateKind.Proactive:
                return ProactiveSchema;
            case WorldStateKind.WorldEvents:
                return WorldEventsSchema;
            case WorldStateKind.Messenger:
                return MessengerSchema;
            case WorldStateKind.Transcript:
                return TranscriptSchema;
            case WorldStateKind.Contacts:
                return ContactsSchema;
            case WorldStateKind.Audit:
                return AuditSchema;
            case WorldStateKind.Onboarding:
                return OnboardingSchema;
            case WorldStateKind.PendingDialogue:
                return DialogueQueueSchema;
            case WorldStateKind.Interaction:
                return InteractionSchema;
            case WorldStateKind.InteractionIndex:
                return InteractionRecoveryIndexSchema;
            case WorldStateKind.PersonaContinuity:
                return PersonaContinuitySchema;
            case WorldStateKind.PersonaOverride:
                return PersonaOverrideSchema;
            case WorldStateKind.PersonaRecovery:
                return PersonaRecoverySchema;
            default:
                return string.Empty;
        }
    }

    internal static bool TryNormalizeSchema(JObject state, string expectedSchema)
    {
        if (state == null || string.IsNullOrWhiteSpace(expectedSchema)) return false;
        string current = (string)state["schema"];
        if (string.IsNullOrWhiteSpace(current))
        {
            state["schema"] = expectedSchema;
            return true;
        }
        return StringComparer.Ordinal.Equals(current, expectedSchema);
    }
}
