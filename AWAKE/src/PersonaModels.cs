using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
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
    [JsonProperty("id")]
    internal string Id { get; set; } = string.Empty;
    [JsonProperty("text")]
    internal string Text { get; set; } = string.Empty;
    [JsonProperty("status")]
    internal string Status { get; set; } = "active";
    [JsonProperty("source")]
    internal string Source { get; set; } = "content";
    [JsonProperty("priority")]
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
    [JsonProperty("id")]
    internal string Id { get; set; } = string.Empty;
    [JsonProperty("fromIdentity")]
    internal string FromIdentity { get; set; } = string.Empty;
    [JsonProperty("toIdentity")]
    internal string ToIdentity { get; set; } = string.Empty;
    [JsonProperty("reason")]
    internal string Reason { get; set; } = string.Empty;
    [JsonProperty("recordedAt")]
    internal string RecordedAt { get; set; } = string.Empty;
    [JsonProperty("source")]
    internal string Source { get; set; } = string.Empty;
}

internal sealed class PersonaContinuityState
{
    [JsonProperty("stableCore")]
    internal string StableCore { get; set; } = string.Empty;
    [JsonProperty("currentIdentity")]
    internal string CurrentIdentity { get; set; } = string.Empty;
    [JsonProperty("experiences")]
    internal List<PersonaExperience> Experiences { get; set; } = new List<PersonaExperience>();
    [JsonProperty("transitions")]
    internal List<PersonaIdentityTransition> Transitions { get; set; } = new List<PersonaIdentityTransition>();
    [JsonProperty("cultureId")]
    internal string CultureId { get; set; } = string.Empty;

    internal PersonaContinuityState DeepClone()
    {
        PersonaContinuityState clone = new PersonaContinuityState
        {
            StableCore = StableCore ?? string.Empty,
            CurrentIdentity = CurrentIdentity ?? string.Empty,
            CultureId = CultureId ?? string.Empty
        };
        foreach (PersonaExperience item in Experiences ?? new List<PersonaExperience>())
        {
            if (item == null) continue;
            clone.Experiences.Add(new PersonaExperience
            {
                Id = item.Id ?? string.Empty,
                Text = item.Text ?? string.Empty,
                Status = item.Status ?? string.Empty,
                Source = item.Source ?? string.Empty,
                Priority = item.Priority
            });
        }
        foreach (PersonaIdentityTransition item in Transitions ?? new List<PersonaIdentityTransition>())
        {
            if (item == null) continue;
            clone.Transitions.Add(new PersonaIdentityTransition
            {
                Id = item.Id ?? string.Empty,
                FromIdentity = item.FromIdentity ?? string.Empty,
                ToIdentity = item.ToIdentity ?? string.Empty,
                Reason = item.Reason ?? string.Empty,
                RecordedAt = item.RecordedAt ?? string.Empty,
                Source = item.Source ?? string.Empty
            });
        }
        return clone;
    }
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
    internal bool IsRuntimeFallback { get; set; }
    internal bool WasTrimmed { get; set; }
    internal bool HadConflict { get; set; }
    internal string DefinitionId { get; set; } = string.Empty;
    internal string SourcePackId { get; set; } = string.Empty;
    internal string Fingerprint { get; set; } = string.Empty;
    internal string Dsl { get; set; } = string.Empty;
    internal List<string> Warnings { get; } = new List<string>();
}

internal sealed class ContextSnapshot
{
    internal string CharacterId { get; set; } = string.Empty;
    internal string PersonaSubjectStableId { get; set; } = string.Empty;
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
    internal string PlayerInput { get; set; } = string.Empty;
    internal string BundleId { get; set; } = string.Empty;
    internal int BundleRevision { get; set; }
    internal string BundleDigest { get; set; } = string.Empty;
    internal int OverlayRevision { get; set; }
    internal long PersonaStateRevision { get; set; }
    internal string PersonaStateDigest { get; set; } = string.Empty;
    internal PersonaContinuityState PersonaContinuity { get; set; } = new PersonaContinuityState();

    internal string ComputeFingerprint()
    {
        JObject canonical = new JObject
        {
            ["bundleId"] = BundleId ?? string.Empty,
            ["bundleRevision"] = BundleRevision,
            ["bundleDigest"] = BundleDigest ?? string.Empty,
            ["overlayRevision"] = OverlayRevision,
            ["characterId"] = CharacterId ?? string.Empty,
            ["personaSubjectStableId"] = PersonaSubjectStableId ?? string.Empty,
            ["personaStateRevision"] = PersonaStateRevision,
            ["personaStateDigest"] = PersonaStateDigest ?? string.Empty,
            ["heroName"] = HeroName ?? string.Empty,
            ["cultureId"] = CultureId ?? string.Empty,
            ["kingdomId"] = KingdomId ?? string.Empty,
            ["kingdomName"] = KingdomName ?? string.Empty,
            ["clanName"] = ClanName ?? string.Empty,
            ["role"] = Role ?? string.Empty,
            ["relation"] = Relation ?? string.Empty,
            ["currentState"] = CurrentState ?? string.Empty,
            ["memoryHint"] = MemoryHint ?? string.Empty,
            ["sceneKeywords"] = new JArray((SceneKeywords ?? new List<string>()).OrderBy(item => item, StringComparer.Ordinal)),
            ["contextModes"] = new JArray((ContextModes ?? new List<string>()).OrderBy(item => item, StringComparer.Ordinal)),
            ["playerInput"] = PlayerInput ?? string.Empty,
            ["personaContinuity"] = BuildContinuityFingerprint(PersonaContinuity)
        };
        using (SHA256 sha = SHA256.Create())
        {
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString(Newtonsoft.Json.Formatting.None))))
                .Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    internal PersonaContext ToPersonaContext()
    {
        return new PersonaContext
        {
            CharacterId = CharacterId ?? string.Empty,
            HeroName = HeroName ?? string.Empty,
            CultureId = CultureId ?? string.Empty,
            KingdomId = KingdomId ?? string.Empty,
            KingdomName = KingdomName ?? string.Empty,
            ClanName = ClanName ?? string.Empty,
            Role = Role ?? string.Empty,
            Relation = Relation ?? string.Empty,
            CurrentState = CurrentState ?? string.Empty,
            MemoryHint = MemoryHint ?? string.Empty,
            SceneKeywords = new List<string>(SceneKeywords ?? new List<string>()),
            ContextModes = new List<string>(ContextModes ?? new List<string>()),
            Continuity = PersonaContinuity?.DeepClone() ?? new PersonaContinuityState(),
            PlayerOverride = null
        };
    }

    internal ContextSnapshot DeepClone()
    {
        return new ContextSnapshot
        {
            CharacterId = CharacterId ?? string.Empty,
            PersonaSubjectStableId = PersonaSubjectStableId ?? string.Empty,
            HeroName = HeroName ?? string.Empty,
            CultureId = CultureId ?? string.Empty,
            KingdomId = KingdomId ?? string.Empty,
            KingdomName = KingdomName ?? string.Empty,
            ClanName = ClanName ?? string.Empty,
            Role = Role ?? string.Empty,
            Relation = Relation ?? string.Empty,
            CurrentState = CurrentState ?? string.Empty,
            MemoryHint = MemoryHint ?? string.Empty,
            SceneKeywords = new List<string>(SceneKeywords ?? new List<string>()),
            ContextModes = new List<string>(ContextModes ?? new List<string>()),
            PlayerInput = PlayerInput ?? string.Empty,
            BundleId = BundleId ?? string.Empty,
            BundleRevision = BundleRevision,
            BundleDigest = BundleDigest ?? string.Empty,
            OverlayRevision = OverlayRevision,
            PersonaStateRevision = PersonaStateRevision,
            PersonaStateDigest = PersonaStateDigest ?? string.Empty,
            PersonaContinuity = PersonaContinuity?.DeepClone() ?? new PersonaContinuityState()
        };
    }

    private static JObject BuildContinuityFingerprint(PersonaContinuityState continuity)
    {
        continuity = continuity ?? new PersonaContinuityState();
        return new JObject
        {
            ["stableCore"] = continuity.StableCore ?? string.Empty,
            ["currentIdentity"] = continuity.CurrentIdentity ?? string.Empty,
            ["cultureId"] = continuity.CultureId ?? string.Empty,
            ["experiences"] = new JArray((continuity.Experiences ?? new List<PersonaExperience>()).Select(item => new JObject
            {
                ["id"] = item?.Id ?? string.Empty,
                ["text"] = item?.Text ?? string.Empty,
                ["status"] = item?.Status ?? string.Empty,
                ["source"] = item?.Source ?? string.Empty,
                ["priority"] = item?.Priority ?? 0
            })),
            ["transitions"] = new JArray((continuity.Transitions ?? new List<PersonaIdentityTransition>()).Select(item => new JObject
            {
                ["id"] = item?.Id ?? string.Empty,
                ["from"] = item?.FromIdentity ?? string.Empty,
                ["to"] = item?.ToIdentity ?? string.Empty,
                ["reason"] = item?.Reason ?? string.Empty,
                ["recordedAt"] = item?.RecordedAt ?? string.Empty,
                ["source"] = item?.Source ?? string.Empty
            }))
        };
    }
}

internal sealed class RuntimeBundle
{
    internal string BundleId { get; set; } = string.Empty;
    internal string Version { get; set; } = string.Empty;
    internal int Revision { get; set; }
    internal string Digest { get; set; } = string.Empty;
    internal PersonaDefinition Definition { get; set; }
    internal PersonaTagRegistry Registry { get; set; }

    internal bool IsApproved
    {
        get
        {
            return !string.IsNullOrWhiteSpace(BundleId)
                && !string.IsNullOrWhiteSpace(Digest)
                && Definition != null
                && StringComparer.Ordinal.Equals(Definition.Status, PersonaSchemaConstants.StatusApproved)
                && Registry != null;
        }
    }
}
