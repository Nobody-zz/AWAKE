using System;
using MarcusAIFramework.Api;

namespace Awake;

internal static class AiTaskConstants
{
    // Route IDs must match "<ExtensionId>.route.<name>"; owner is AWAKE.
    internal const string RouteNpcDialogue = "AWAKE.route.npc.dialogue";
    internal const string RoutePreprocess = "AWAKE.route.preprocess";
    internal const string RoutePostprocess = "AWAKE.route.postprocess";
    internal const string RouteMemoryDaily = "AWAKE.route.memory.daily";

    internal const string PlayerContextProviderId = "awake.player.context";
    internal const string HeroContextProviderId = "awake.hero.context";

    internal const string NpcMemoriesNamespace = "awake.npc.memories";
    internal const string EventMetaNamespace = "awake.event_meta";
    internal const string RelationshipsNamespace = "awake.relationships";
    internal const string ProactiveNamespace = "awake.npc.proactive";
    internal const string WorldEventsNamespace = "awake.world.events";
    internal const string WorldEventsKey = "campaign.world_events.v1";
    internal const string MessengerNamespace = "awake.messenger";
    internal const string MessengerKey = "campaign.messenger.v1";
    internal const string EventMetaKey = "campaign.event_meta.v1";
    internal const string TranscriptNamespace = "awake.transcripts";
    internal const string ContactsNamespace = "awake.contacts";
    internal const string AuditNamespace = "awake.history.audit";

    internal const string TranscriptAppendCommandId = "awake.transcript.append.v1";
    internal const string TranscriptPinCommandId = "awake.transcript.pin.v1";
    internal const string TranscriptRollCommandId = "awake.transcript.roll.v1";
    internal const string ContactsUpsertCommandId = "awake.contacts.upsert.v1";

    internal const string RelationshipDeltaCommandId = "awake.relationship.delta.v1";

    internal const string PreprocessOutputSchema = "awake.preprocess.output.v1";
    internal const string PostprocessOutputSchema = "awake.postprocess.output.v1";
    internal const string MemoryDailyOutputSchema = "awake.memory.daily.output.v1";

    internal const string PlayerKnownScope = "PlayerKnown";
    internal const int ContextMaximumTokens = 512;
    internal const int ContextMaximumContributionsPerProvider = 1;
    internal const int ContextPayloadMaximumBytes = 16 * 1024;
    internal const int ContextTotalMaximumBytes = 64 * 1024;
    internal const int StorageValueMaximumBytes = 512 * 1024;
    internal const int StateEntriesMaximum = 100;
    internal const int AppliedKeysMaximum = 256;
    internal const int CacheMaximumEntries = 256;
    internal const int DrainMaximumRetries = 3;
    internal const int MemorySummaryMaximumChars = 240;
    internal const int MemoryFactsMaximum = 8;
    internal const int MemoryEntryMaximumBytes = 1500;
    internal const int MemoryPinnedMaximum = 20;
    internal const int MemoryEntriesMaximum = 100;

    internal static readonly string[] ContextProviderIds = new[]
    {
        PlayerContextProviderId,
        HeroContextProviderId
    };

    internal static readonly string[] NewRouteIds = new[]
    {
        RoutePreprocess,
        RoutePostprocess,
        RouteMemoryDaily
    };

    internal static readonly string[] AllRouteIds = new[]
    {
        RouteNpcDialogue,
        RoutePreprocess,
        RoutePostprocess,
        RouteMemoryDaily
    };

    internal static readonly string[] NewCommandIds = new[]
    {
        RelationshipDeltaCommandId
    };

    internal static readonly string[] StorageNamespaceIds = new[]
    {
        NpcMemoriesNamespace,
        EventMetaNamespace,
        RelationshipsNamespace,
        ProactiveNamespace,
        WorldEventsNamespace,
        MessengerNamespace,
        TranscriptNamespace,
        ContactsNamespace,
        AuditNamespace
    };

    internal static string RoutePermission(string routeId) => "ai.route.invoke:" + routeId;

    internal static string CommandPermission(string commandId) => "command.invoke:" + commandId;

    internal static SchemaRef CommandInputSchema(string commandId) => new SchemaRef(commandId + ".input", 1, 0);

    internal static SchemaRef CommandOutputSchema(string commandId) => new SchemaRef(commandId + ".output", 1, 0);
}
