using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class WorldbookWhen
{
    internal List<string> HeroIds { get; set; } = new List<string>();
    internal List<string> CharacterIds { get; set; } = new List<string>();
    internal List<string> Cultures { get; set; } = new List<string>();
    internal List<string> KingdomIds { get; set; } = new List<string>();
    internal List<string> SettlementIds { get; set; } = new List<string>();
    internal List<string> Roles { get; set; } = new List<string>();
    internal List<string> IdentityIds { get; set; } = new List<string>();
    internal bool? IsFemale { get; set; }
    internal bool? IsClanLeader { get; set; }
    internal int? MinAge { get; set; }
    internal int? MaxAge { get; set; }
    internal Dictionary<string, int> SkillMin { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
    internal string ContentTier { get; set; }
}

internal sealed class WorldbookContext
{
    internal List<string> SceneKeywords { get; set; } = new List<string>();
    internal List<string> ContextModes { get; set; } = new List<string>();
}

internal sealed class WorldbookVariant
{
    internal int Priority { get; set; }
    internal WorldbookWhen When { get; set; } = new WorldbookWhen();
    internal string Content { get; set; } = string.Empty;
}

internal sealed class WorldbookTextMapping
{
    internal string SourceText { get; set; } = string.Empty;
    internal string Kind { get; set; } = string.Empty;
    internal string TargetId { get; set; } = string.Empty;
    internal int? AgeMin { get; set; }
    internal int? AgeMax { get; set; }
    internal string EmptyValueText { get; set; } = string.Empty;
    internal string TrueText { get; set; } = string.Empty;
    internal string FalseText { get; set; } = string.Empty;
    internal JObject Raw { get; set; }
}

internal sealed class WorldbookRule
{
    internal string Id { get; set; } = string.Empty;
    internal string Kind { get; set; } = "background";
    internal string Scope { get; set; } = "npc";
    internal string Persistence { get; set; } = "persistent";
    internal string VariantSelection { get; set; } = "first";
    internal int Priority { get; set; }
    internal WorldbookWhen When { get; set; } = new WorldbookWhen();
    internal WorldbookContext Context { get; set; } = new WorldbookContext();
    internal List<string> Keywords { get; set; } = new List<string>();
    internal List<string> Ngrams { get; set; } = new List<string>();
    internal List<string> RagShortTexts { get; set; } = new List<string>();
    internal List<string> SemanticPrototypes { get; set; } = new List<string>();
    internal string Content { get; set; } = string.Empty;
    internal List<WorldbookVariant> Variants { get; set; } = new List<WorldbookVariant>();
    internal List<WorldbookTextMapping> TextMappings { get; set; } = new List<WorldbookTextMapping>();
    internal JObject Raw { get; set; }
}

internal sealed class WorldbookPersona
{
    internal string CharacterId { get; set; } = string.Empty;
    internal string Personality { get; set; } = string.Empty;
    internal string Background { get; set; } = string.Empty;
    internal string VoiceId { get; set; } = string.Empty;
    internal List<string> KnownNames { get; set; } = new List<string>();
    internal JObject Raw { get; set; }
}

internal sealed class WorldbookManifest
{
    internal string SchemaVersion { get; set; } = "awake.worldbook.v1";
    internal string Id { get; set; } = string.Empty;
    internal string SourceFormat { get; set; } = "awake";
    internal string RulesDirectory { get; set; } = "rules";
    internal string PersonaDirectory { get; set; } = "personality_background";
    internal string UnnamedPersonaDirectory { get; set; } = "unnamed_persona";
    internal string VoiceMappingDirectory { get; set; } = "voice_mapping";
    internal string EventDataDirectory { get; set; } = "event_data";
    internal string DebtDirectory { get; set; } = "debt";
    internal string DialogueHistoryDirectory { get; set; } = "dialogue_history";
    internal string CompressedMemoryDirectory { get; set; } = "compressed_memory";
}

internal sealed class WorldbookDocument
{
    internal WorldbookManifest Manifest { get; set; } = new WorldbookManifest();
    internal List<WorldbookRule> Rules { get; set; } = new List<WorldbookRule>();
    internal List<WorldbookPersona> Personas { get; set; } = new List<WorldbookPersona>();
    internal List<WorldbookImportWarning> Warnings { get; set; } = new List<WorldbookImportWarning>();
    internal JToken UnnamedPersonaData { get; set; }
    internal JToken VoiceMappingData { get; set; }
    internal JToken EventData { get; set; }
    internal JToken DebtData { get; set; }
    internal JToken DialogueHistoryData { get; set; }
    internal JToken CompressedMemoryData { get; set; }
}

internal sealed class WorldbookImportWarning
{
    internal string Source { get; set; } = string.Empty;
    internal string Code { get; set; } = string.Empty;
    internal string Message { get; set; } = string.Empty;

    internal WorldbookImportWarning(string source, string code, string message)
    {
        Source = source ?? string.Empty;
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
    }
}

internal sealed class WorldbookQuery
{
    internal string HeroId { get; set; } = string.Empty;
    internal string CharacterId { get; set; } = string.Empty;
    internal string IdentityId { get; set; } = string.Empty;
    internal string CultureId { get; set; } = string.Empty;
    internal string KingdomId { get; set; } = string.Empty;
    internal string SettlementId { get; set; } = string.Empty;
    internal string Role { get; set; } = string.Empty;
    internal bool? IsFemale { get; set; }
    internal int Age { get; set; }
    internal bool IsClanLeader { get; set; }
    internal Dictionary<string, int> Skills { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
    internal string ContentTier { get; set; } = "pure";
    internal List<string> SceneKeywords { get; set; } = new List<string>();
    internal List<string> ContextModes { get; set; } = new List<string>();
    internal string PlayerText { get; set; } = string.Empty;
    internal int MaximumBytes { get; set; } = 4096;
}

internal sealed class WorldbookQueryResult
{
    internal List<WorldbookRule> IdentityRules { get; } = new List<WorldbookRule>();
    internal List<WorldbookRule> TopicRules { get; } = new List<WorldbookRule>();
    internal string RetrievedText { get; set; } = string.Empty;
    internal string MatchMode { get; set; } = "identity";
    internal int ByteBudget { get; set; }
    internal List<string> MatchedKeywords { get; } = new List<string>();
    internal List<string> ResolvedVariantIds { get; } = new List<string>();
    internal List<string> HitIds { get; } = new List<string>();
    internal List<WorldbookImportWarning> Warnings { get; } = new List<WorldbookImportWarning>();
    internal List<string> Errors { get; } = new List<string>();
    internal string Personality { get; set; } = string.Empty;
    internal string Background { get; set; } = string.Empty;
}
