using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class PersonaSchemaConstants
{
    internal const string DefinitionSchema = "awake.persona.definition.v1";
    internal const string RegistrySchema = "awake.persona.tags.v1";
    internal const string GeneratorVersion = "persona-load.v2";
    internal const string StatusApproved = "approved";
    internal const string StatusDraft = "draft";
    internal const string StatusDisabled = "disabled";
}

internal static class PersonaTagCategories
{
    internal const string Trait = "trait";
    internal const string Expression = "expression";
    internal const string Behavior = "behavior";
    internal const string Trigger = "trigger";
    internal const string Boundary = "boundary";

    internal static bool IsKnown(string value)
    {
        return StringComparer.Ordinal.Equals(value, Trait)
            || StringComparer.Ordinal.Equals(value, Expression)
            || StringComparer.Ordinal.Equals(value, Behavior)
            || StringComparer.Ordinal.Equals(value, Trigger)
            || StringComparer.Ordinal.Equals(value, Boundary);
    }
}

internal sealed class PersonaTagDefinition
{
    internal string Id { get; set; } = string.Empty;
    internal string Category { get; set; } = string.Empty;
    internal string DisplayName { get; set; } = string.Empty;
    internal string Meaning { get; set; } = string.Empty;
    internal string PromptText { get; set; } = string.Empty;
    internal List<string> Conflicts { get; set; } = new List<string>();
    internal JObject Raw { get; set; }
}

internal sealed class PersonaBundleDefinition
{
    internal string Id { get; set; } = string.Empty;
    internal string DisplayName { get; set; } = string.Empty;
    internal List<string> Tags { get; set; } = new List<string>();
    internal JObject Raw { get; set; }
}

internal sealed class PersonaTagRegistryDocument
{
    internal string SchemaVersion { get; set; } = PersonaSchemaConstants.RegistrySchema;
    internal List<PersonaTagDefinition> Tags { get; set; } = new List<PersonaTagDefinition>();
    internal List<PersonaBundleDefinition> Bundles { get; set; } = new List<PersonaBundleDefinition>();
}

internal sealed class PersonaTagUse
{
    internal string Id { get; set; } = string.Empty;
    internal int Priority { get; set; }
    internal List<string> SceneKeywords { get; set; } = new List<string>();
    internal List<string> ContextModes { get; set; } = new List<string>();
}

internal sealed class PersonaExperience
{
    internal string Id { get; set; } = string.Empty;
    internal string Text { get; set; } = string.Empty;
    internal string Status { get; set; } = "active";
    internal string Source { get; set; } = "content";
    internal int Priority { get; set; }
}

internal sealed class PersonaDefinition
{
    internal string SchemaVersion { get; set; } = PersonaSchemaConstants.DefinitionSchema;
    internal string Id { get; set; } = string.Empty;
    internal string CharacterId { get; set; } = string.Empty;
    internal string IdentityId { get; set; } = string.Empty;
    internal string Role { get; set; } = string.Empty;
    internal string SourcePackId { get; set; } = string.Empty;
    internal string TemplateVersion { get; set; } = "1";
    internal string Status { get; set; } = PersonaSchemaConstants.StatusDraft;
    internal int Priority { get; set; }
    internal string Scope { get; set; } = "character";
    internal string Core { get; set; } = string.Empty;
    internal string IdentityFacts { get; set; } = string.Empty;
    internal string RelationStyle { get; set; } = string.Empty;
    internal string CurrentStateHints { get; set; } = string.Empty;
    internal string Summary { get; set; } = string.Empty;
    internal string PublicDescription { get; set; } = string.Empty;
    internal string PrivateDescription { get; set; } = string.Empty;
    internal string ContradictionDescription { get; set; } = string.Empty;
    internal string FoodPreference { get; set; } = string.Empty;
    internal List<string> SelfClaimRules { get; set; } = new List<string>();
    internal List<string> RealSelfBehaviors { get; set; } = new List<string>();
    internal List<string> SelfClaimExamples { get; set; } = new List<string>();
    internal List<PersonaTagUse> Tags { get; set; } = new List<PersonaTagUse>();
    internal List<string> Bundles { get; set; } = new List<string>();
    internal List<PersonaExperience> Experiences { get; set; } = new List<PersonaExperience>();
    internal JObject Raw { get; set; }
    internal string SourcePath { get; set; } = string.Empty;
}

internal sealed class PersonaIdentityTransition
{
    internal string Id { get; set; } = string.Empty;
    internal string FromIdentity { get; set; } = string.Empty;
    internal string ToIdentity { get; set; } = string.Empty;
    internal string Reason { get; set; } = string.Empty;
    internal string RecordedAt { get; set; } = string.Empty;
    internal string Source { get; set; } = string.Empty;
}

internal sealed class PersonaContinuityState
{
    internal string StableCore { get; set; } = string.Empty;
    internal string CurrentIdentity { get; set; } = string.Empty;
    internal List<PersonaExperience> Experiences { get; set; } = new List<PersonaExperience>();
    internal List<PersonaIdentityTransition> Transitions { get; set; } = new List<PersonaIdentityTransition>();
    internal string CultureId { get; set; } = string.Empty;
}

internal sealed class PersonaOverrideSet
{
    internal string Version { get; set; } = "1";
    internal string Status { get; set; } = PersonaSchemaConstants.StatusDraft;
    internal string CoreOverride { get; set; } = string.Empty;
    internal List<PersonaTagUse> Tags { get; set; } = new List<PersonaTagUse>();
    internal List<PersonaExperience> Experiences { get; set; } = new List<PersonaExperience>();
    internal List<string> DisabledExperienceIds { get; set; } = new List<string>();
}
internal sealed class PersonaContext
{
    internal string CharacterId { get; set; } = string.Empty;
    internal string HeroName { get; set; } = string.Empty;
    internal string CultureId { get; set; } = string.Empty;
    internal string KingdomId { get; set; } = string.Empty;
    internal string KingdomName { get; set; } = string.Empty;
    internal string ClanName { get; set; } = string.Empty;
    internal string Role { get; set; } = string.Empty;
    internal string Relation { get; set; } = string.Empty;
    internal string CurrentState { get; set; } = string.Empty;
    internal string MemoryHint { get; set; } = string.Empty;
    internal List<string> SceneKeywords { get; set; } = new List<string>();
    internal List<string> ContextModes { get; set; } = new List<string>();
    internal PersonaContinuityState Continuity { get; set; } = new PersonaContinuityState();
    internal PersonaOverrideSet PlayerOverride { get; set; } = new PersonaOverrideSet();
}

internal sealed class PersonaGenerationResult
{
    internal bool IsUsable { get; set; }
    internal bool UsedLegacyFallback { get; set; }
    internal bool WasTrimmed { get; set; }
    internal bool HadConflict { get; set; }
    internal string DefinitionId { get; set; } = string.Empty;
    internal string SourcePackId { get; set; } = string.Empty;
    internal string Fingerprint { get; set; } = string.Empty;
    internal string Dsl { get; set; } = string.Empty;
    internal List<string> Warnings { get; } = new List<string>();
}