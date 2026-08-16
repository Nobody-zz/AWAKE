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

    internal static bool IsKnownSchema(string schema)
    {
        return StringComparer.Ordinal.Equals(schema, MemorySchema)
            || StringComparer.Ordinal.Equals(schema, RelationshipSchema)
            || StringComparer.Ordinal.Equals(schema, EventMetaSchema)
            || StringComparer.Ordinal.Equals(schema, ProactiveSchema)
            || StringComparer.Ordinal.Equals(schema, WorldEventsSchema)
            || StringComparer.Ordinal.Equals(schema, MessengerSchema);
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
