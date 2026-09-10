using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonaWorkbench.Core;

public static class PersonaAuthoringContractVersion
{
    public const string SchemaVersion = "awake.persona.authoring.v2";
    public const string RegistryVersion = "awake.persona.tags.v1";
    public const string InstructionVersion = "persona-instruction.v1";
    public const string CompilerVersion = "persona-compiler.v1";
    public const string OriginSchema = "persona-workbench.character.v1";
    public const int AuthoringRevision = 1;
}

public static class PersonaAuthoringV2Adapter
{
    private static readonly string[] RootSourceIds =
    {
        "schemaVersion", "id", "displayName", "status", "sourcePackId", "templateVersion", "sourceDescription",
        "core", "identityFacts", "summary", "publicDescription", "privateDescription", "contradictionDescription",
        "foodPreference", "selfClaimRules", "realSelfBehaviors", "selfClaimExamples"
    };

    private static readonly string[] LegacySourceIds =
    {
        "reactionProfile.sensitiveConditions", "reactionProfile.conditionalResponses", "commitmentProfile.priorityOrder",
        "commitmentProfile.protectedValues", "commitmentProfile.applicableScope", "commitmentProfile.exceptionCost",
        "commitmentProfile.breachResponse"
    };

    public static PersonaAuthoringBuildResult Build(PersonaDocument document, string? expandedText, string? expandedTextOrigin, PersonaAuthoringContractAssets? assets = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        string origin = expandedTextOrigin ?? PersonaAuthoringExpansionOrigin.None;
        string expansion = expandedText ?? string.Empty;
        string trimmedExpansion = expansion.Trim();
        if (!PersonaAuthoringExpansionOrigin.IsValid(origin)) return PersonaAuthoringBuildResult.Failure("persona.expansion_origin_invalid", "Expansion origin is not recognized.", origin);
        if (origin == PersonaAuthoringExpansionOrigin.None && trimmedExpansion.Length > 0) return PersonaAuthoringBuildResult.Failure("persona.expansion_origin_invalid", "A non-empty expansion requires an explicit origin.", origin);
        if (origin != PersonaAuthoringExpansionOrigin.None && trimmedExpansion.Length == 0) return PersonaAuthoringBuildResult.Failure("persona.expansion_origin_missing_text", "A non-empty expansion origin requires expansion text.", origin);

        assets ??= PersonaAuthoringContractAssets.LoadEmbedded();
        if (!assets.IsValid) return PersonaAuthoringBuildResult.Failure("persona.migration_required", "Authoring contract assets are missing or drifted.", origin, assets.Warnings);

        PersonaValidationResult sourceValidation = PersonaValidator.Validate(document, PersonaTagRegistry.CreateDefault());
        if (!sourceValidation.IsValid)
        {
            return PersonaAuthoringBuildResult.Failure(sourceValidation.Errors.Select(error => new PersonaAuthoringDiagnostic(error.Code, error.Message)).ToArray(), origin, assets.Warnings);
        }
        string normalizedId = document.Id.Trim();
        string normalizedSourcePackId = document.SourcePackId.Trim();
        if (!IsStableId(normalizedId)) return PersonaAuthoringBuildResult.Failure("persona.authoring_id_invalid", "The v1 Persona ID is not a stable lowercase dotted ID.", origin, assets.Warnings);
        if (!string.IsNullOrEmpty(normalizedSourcePackId) && !IsStableId(normalizedSourcePackId)) return PersonaAuthoringBuildResult.Failure("persona.authoring_source_pack_id_invalid", "The source pack ID is not a stable lowercase dotted ID.", origin, assets.Warnings);

        List<PersonaAuthoringDiagnostic> closureDiagnostics = ValidateCrosswalkClosure(assets);
        if (closureDiagnostics.Count > 0) return PersonaAuthoringBuildResult.Failure(closureDiagnostics, origin, assets.Warnings);

        string confirmedText = NormalizeText(document.SourceDescription);
        AwakePersonaAuthoringDocument target = new AwakePersonaAuthoringDocument
        {
            DocumentId = NormalizeText(normalizedId),
            DisplayName = NormalizeText(string.IsNullOrWhiteSpace(document.DisplayName) ? PersonaIdentityNormalizer.DefaultDisplayName : document.DisplayName.Trim()),
            ReviewStatus = NormalizeText(document.Status),
            RegistryDigest = Convert.ToHexString(SHA256.HashData(assets.RegistryUtf8)),
            SourcePackId = NormalizeText(normalizedSourcePackId),
            Source = new AwakePersonaAuthoringSource
            {
                ConfirmedText = confirmedText,
                ExpandedText = NormalizeText(expansion),
                ConfirmedTextSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(confirmedText)))
            },
            Authored = new AwakePersonaAuthoringAuthored
            {
                Core = NormalizeText(document.Core),
                IdentityFacts = NormalizeText(document.IdentityFacts),
                Summary = NormalizeText(document.Summary),
                PublicDescription = NormalizeText(document.PublicDescription),
                PrivateDescription = NormalizeText(document.PrivateDescription),
                ContradictionDescription = NormalizeText(document.ContradictionDescription),
                FoodPreference = NormalizeText(document.FoodPreference),
                SelfClaimRules = NormalizeList(document.SelfClaimRules),
                RealSelfBehaviors = NormalizeList(document.RealSelfBehaviors),
                SelfClaimExamples = NormalizeList(document.SelfClaimExamples)
            },
            Migration = new AwakePersonaAuthoringMigration
            {
                OriginTemplateVersion = NormalizeText(document.TemplateVersion),
                AuthoringRevision = PersonaAuthoringContractVersion.AuthoringRevision
            }
        };

        if (target.Authored.IdentityFacts.Length > 0)
        {
            target.Facts.Add(new AwakePersonaAuthoringFact
            {
                FactId = "identity_note",
                Kind = "identityNote",
                Value = target.Authored.IdentityFacts,
                Trust = "legacy_unverified",
                Provenance = "legacy_v1",
                Enabled = true
            });
        }

        Dictionary<string, ObservationCandidate> candidates = new Dictionary<string, ObservationCandidate>(StringComparer.Ordinal);
        List<string> warnings = new List<string>(assets.Warnings);
        foreach (PersonaAuthoringCrosswalkRow row in assets.Rows.Values.OrderBy(row => row.SourceId, StringComparer.Ordinal))
        {
            if (row.SourceKind is not ("tag" or "facet_strength" or "axis" or "legacy_field")) continue;
            if (!TryReadSourceValue(document, row, out object? rawValue) || !HasSourceValue(row.SourceKind, rawValue)) continue;
            if (row.SourceKind == "legacy_field")
            {
                PreserveLegacyValue(target.Migration.PreservedLegacyData, row.SourceId, rawValue);
                warnings.Add("legacy_value_preserved:" + row.SourceId);
                continue;
            }
            if (!assets.TryGetAction(row, rawValue!, out PersonaAuthoringValueAction? action)) return PersonaAuthoringBuildResult.Failure("persona.migration_required", "Crosswalk has no action for source value: " + row.SourceId, origin, warnings);
            if (action.Warning != null) warnings.Add(action.Warning + ":" + row.SourceId);
            switch (action.Action)
            {
                case "omit":
                    break;
                case "preserve_only":
                    PreserveLegacyValue(target.Migration.PreservedLegacyData, row.SourceId, rawValue);
                    warnings.Add("legacy_value_preserved:" + row.SourceId);
                    break;
                case "emit":
                    if (row.TargetId == null || !assets.RegistryTagIds.Contains(row.TargetId)) return PersonaAuthoringBuildResult.Failure("persona.migration_required", "Crosswalk emitted an unknown selector: " + row.SourceId, origin, warnings);
                    AwakePersonaAuthoringObservation observation = new AwakePersonaAuthoringObservation
                    {
                        SelectorId = row.TargetId,
                        Value = action.Strength ?? "present",
                        Provenance = "legacy_v1",
                        ReviewState = action.ReviewState ?? "needs_review",
                        Evidence = null,
                        Enabled = true
                    };
                    if (!MergeObservation(candidates, observation, SourceRank(row.SourceKind), row.SourceId, warnings)) return PersonaAuthoringBuildResult.Failure("persona.observation_duplicate", "Multiple source values have the same migration fidelity: " + row.TargetId, origin, warnings);
                    break;
                default:
                    return PersonaAuthoringBuildResult.Failure("persona.migration_required", "Crosswalk action is unsupported: " + action.Action, origin, warnings);
            }
        }

        target.Observations = candidates.Values.OrderBy(candidate => candidate.Observation.SelectorId, StringComparer.Ordinal).Select(candidate => candidate.Observation).ToList();
        warnings = warnings.Distinct(StringComparer.Ordinal).OrderBy(warning => warning, StringComparer.Ordinal).ToList();
        target.Migration.Warnings = warnings;
        List<PersonaAuthoringDiagnostic> targetDiagnostics = PersonaAuthoringV2Validator.Validate(target, assets);
        if (targetDiagnostics.Count > 0) return PersonaAuthoringBuildResult.Failure(targetDiagnostics, origin, warnings);

        try
        {
            return PersonaAuthoringBuildResult.Success(target, PersonaAuthoringCanonicalJson.Serialize(target), origin, warnings);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return PersonaAuthoringBuildResult.Failure("persona.canonicalization_failed", ex.Message, origin, warnings);
        }
    }

    private static List<PersonaAuthoringDiagnostic> ValidateCrosswalkClosure(PersonaAuthoringContractAssets assets)
    {
        HashSet<string> expected = new HashSet<string>(RootSourceIds, StringComparer.Ordinal);
        expected.UnionWith(LegacySourceIds);
        PersonaTagRegistry registry = PersonaTagRegistry.CreateDefault();
        foreach (string tagId in registry.Ids)
        {
            expected.Add("tags." + tagId);
            if (registry.TryGet(tagId, out PersonaTagDefinition definition) && definition.Category is not (PersonaTagCategory.Trigger or PersonaTagCategory.Boundary)) expected.Add("facetStrengths." + tagId);
        }
        foreach (string profilePath in new[] { "traitProfile", "expressionProfile", "behaviorProfile", "reactionProfile", "commitmentProfile" })
        {
            Type profileType = profilePath switch
            {
                "traitProfile" => typeof(PersonaTraitProfile),
                "expressionProfile" => typeof(PersonaExpressionProfile),
                "behaviorProfile" => typeof(PersonaBehaviorProfile),
                "reactionProfile" => typeof(PersonaReactionProfile),
                _ => typeof(PersonaCommitmentProfile)
            };
            foreach (PropertyInfo property in profileType.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(property => property.PropertyType == typeof(int?))) expected.Add(profilePath + "." + LowerFirst(property.Name));
        }

        List<PersonaAuthoringDiagnostic> diagnostics = new List<PersonaAuthoringDiagnostic>();
        foreach (string sourceId in expected.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!assets.Rows.ContainsKey(sourceId)) diagnostics.Add(new PersonaAuthoringDiagnostic("persona.crosswalk_closure_invalid", "Crosswalk must contain exactly one row for source field: " + sourceId));
        }
        return diagnostics;
    }

    private static bool TryReadSourceValue(PersonaDocument document, PersonaAuthoringCrosswalkRow row, out object? value)
    {
        value = null;
        if (row.SourceKind == "tag")
        {
            value = document.Tags.Contains(row.SourceId[5..], StringComparer.Ordinal);
            return true;
        }
        if (row.SourceKind == "facet_strength") return document.FacetStrengths.TryGetValue(row.SourceId[15..], out int strength) && SetValue(strength, out value);
        return ReadPropertyPath(document, row.SourceId, out value);
    }

    private static bool SetValue(object value, out object? result)
    {
        result = value;
        return true;
    }

    private static bool ReadPropertyPath(object instance, string path, out object? value)
    {
        value = instance;
        foreach (string segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (value == null) return false;
            PropertyInfo? property = value.GetType().GetProperty(UpperFirst(segment), BindingFlags.Instance | BindingFlags.Public);
            if (property == null) return false;
            value = property.GetValue(value);
        }
        return true;
    }

    private static bool HasSourceValue(string sourceKind, object? value) => sourceKind switch
    {
        "tag" => value is true,
        "legacy_field" => value is string text && text.Length > 0,
        _ => value != null
    };

    private static bool MergeObservation(IDictionary<string, ObservationCandidate> candidates, AwakePersonaAuthoringObservation observation, int rank, string sourceId, ICollection<string> warnings)
    {
        if (!candidates.TryGetValue(observation.SelectorId, out ObservationCandidate? existing))
        {
            candidates[observation.SelectorId] = new ObservationCandidate(observation, rank, sourceId);
            return true;
        }
        if (rank == existing.Rank) return false;
        if (rank > existing.Rank)
        {
            warnings.Add("lower_fidelity_observation_discarded:" + existing.SourceId);
            candidates[observation.SelectorId] = new ObservationCandidate(observation, rank, sourceId);
        }
        else warnings.Add("lower_fidelity_observation_discarded:" + sourceId);
        return true;
    }

    private static int SourceRank(string sourceKind) => sourceKind switch
    {
        "axis" => 3,
        "facet_strength" => 2,
        "tag" => 1,
        _ => 0
    };

    private static object? NormalizeValue(object? value) => value is string text ? NormalizeText(text) : value;
    private static string NormalizeText(string? value) => (value ?? string.Empty).Normalize(NormalizationForm.FormC);
    private static List<string> NormalizeList(IEnumerable<string>? values) => values?.Select(NormalizeText).ToList() ?? new List<string>();
    private static void PreserveLegacyValue(Dictionary<string, object?> bag, string sourceId, object? value)
    {
        string[] parts = sourceId.Split('.', 2);
        if (parts.Length != 2) { bag[sourceId] = NormalizeValue(value); return; }
        if (!bag.TryGetValue(parts[0], out object? nestedValue) || nestedValue is not Dictionary<string, object?> nested)
        {
            nested = new Dictionary<string, object?>(StringComparer.Ordinal);
            bag[parts[0]] = nested;
        }
        nested[parts[1]] = NormalizeValue(value);
    }
    private static string LowerFirst(string value) => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
    private static string UpperFirst(string value) => string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static bool IsStableId(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        foreach (string segment in value.Split('.'))
        {
            if (segment.Length == 0) return false;
            foreach (char character in segment)
            {
                if (!((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '_')) return false;
            }
        }
        return true;
    }

    private sealed class ObservationCandidate
    {
        public ObservationCandidate(AwakePersonaAuthoringObservation observation, int rank, string sourceId)
        {
            Observation = observation;
            Rank = rank;
            SourceId = sourceId;
        }

        public AwakePersonaAuthoringObservation Observation { get; }
        public int Rank { get; }
        public string SourceId { get; }
    }
}

public static class PersonaAuthoringExpansionOrigin
{
    public const string None = "none";
    public const string Provider = "provider";
    public const string UserEdited = "user_edited";

    public static bool IsValid(string? value) => value is None or Provider or UserEdited;
}

public sealed class AwakePersonaAuthoringDocument
{
    [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = PersonaAuthoringContractVersion.SchemaVersion;
    [JsonPropertyName("documentId")] public string DocumentId { get; set; } = string.Empty;
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("reviewStatus")] public string ReviewStatus { get; set; } = PersonaReviewStatus.Draft;
    [JsonPropertyName("registryVersion")] public string RegistryVersion { get; set; } = PersonaAuthoringContractVersion.RegistryVersion;
    [JsonPropertyName("registryDigest")] public string RegistryDigest { get; set; } = string.Empty;
    [JsonPropertyName("instructionVersion")] public string InstructionVersion { get; set; } = PersonaAuthoringContractVersion.InstructionVersion;
    [JsonPropertyName("compilerVersion")] public string CompilerVersion { get; set; } = PersonaAuthoringContractVersion.CompilerVersion;
    [JsonPropertyName("sourcePackId")] public string SourcePackId { get; set; } = string.Empty;
    [JsonPropertyName("source")] public AwakePersonaAuthoringSource Source { get; set; } = new AwakePersonaAuthoringSource();
    [JsonPropertyName("authored")] public AwakePersonaAuthoringAuthored Authored { get; set; } = new AwakePersonaAuthoringAuthored();
    [JsonPropertyName("facts")] public List<AwakePersonaAuthoringFact> Facts { get; set; } = new List<AwakePersonaAuthoringFact>();
    [JsonPropertyName("observations")] public List<AwakePersonaAuthoringObservation> Observations { get; set; } = new List<AwakePersonaAuthoringObservation>();
    [JsonPropertyName("rules")] public List<AwakePersonaAuthoringRule> Rules { get; set; } = new List<AwakePersonaAuthoringRule>();
    [JsonPropertyName("migration")] public AwakePersonaAuthoringMigration Migration { get; set; } = new AwakePersonaAuthoringMigration();
}

public sealed class AwakePersonaAuthoringSource
{
    [JsonPropertyName("confirmedText")] public string ConfirmedText { get; set; } = string.Empty;
    [JsonPropertyName("expandedText")] public string ExpandedText { get; set; } = string.Empty;
    [JsonPropertyName("confirmedTextSha256")] public string ConfirmedTextSha256 { get; set; } = string.Empty;
}

public sealed class AwakePersonaAuthoringAuthored
{
    [JsonPropertyName("core")] public string Core { get; set; } = string.Empty;
    [JsonPropertyName("identityFacts")] public string IdentityFacts { get; set; } = string.Empty;
    [JsonPropertyName("summary")] public string Summary { get; set; } = string.Empty;
    [JsonPropertyName("publicDescription")] public string PublicDescription { get; set; } = string.Empty;
    [JsonPropertyName("privateDescription")] public string PrivateDescription { get; set; } = string.Empty;
    [JsonPropertyName("contradictionDescription")] public string ContradictionDescription { get; set; } = string.Empty;
    [JsonPropertyName("foodPreference")] public string FoodPreference { get; set; } = string.Empty;
    [JsonPropertyName("selfClaimRules")] public List<string> SelfClaimRules { get; set; } = new List<string>();
    [JsonPropertyName("realSelfBehaviors")] public List<string> RealSelfBehaviors { get; set; } = new List<string>();
    [JsonPropertyName("selfClaimExamples")] public List<string> SelfClaimExamples { get; set; } = new List<string>();
}

public sealed class AwakePersonaAuthoringFact
{
    [JsonPropertyName("factId")] public string FactId { get; set; } = string.Empty;
    [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
    [JsonPropertyName("value")] public object? Value { get; set; }
    [JsonPropertyName("trust")] public string Trust { get; set; } = string.Empty;
    [JsonPropertyName("provenance")] public string Provenance { get; set; } = string.Empty;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
}

public sealed class AwakePersonaAuthoringEvidence
{
    [JsonPropertyName("sourceHash")] public string SourceHash { get; set; } = string.Empty;
    [JsonPropertyName("startUtf16")] public int StartUtf16 { get; set; }
    [JsonPropertyName("lengthUtf16")] public int LengthUtf16 { get; set; }
    [JsonPropertyName("quote")] public string Quote { get; set; } = string.Empty;
}

public sealed class AwakePersonaAuthoringObservation
{
    [JsonPropertyName("selectorId")] public string SelectorId { get; set; } = string.Empty;
    [JsonPropertyName("value")] public object? Value { get; set; }
    [JsonPropertyName("provenance")] public string Provenance { get; set; } = string.Empty;
    [JsonPropertyName("reviewState")] public string ReviewState { get; set; } = string.Empty;
    [JsonPropertyName("evidence")] public AwakePersonaAuthoringEvidence? Evidence { get; set; }
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
}

public sealed class AwakePersonaAuthoringRule
{
    [JsonPropertyName("ruleId")] public string RuleId { get; set; } = string.Empty;
    [JsonPropertyName("triggerSelectorId")] public string TriggerSelectorId { get; set; } = string.Empty;
    [JsonPropertyName("responseSelectorId")] public string ResponseSelectorId { get; set; } = string.Empty;
    [JsonPropertyName("scope")] public string Scope { get; set; } = string.Empty;
    [JsonPropertyName("strength")] public string Strength { get; set; } = string.Empty;
    [JsonPropertyName("priority")] public int Priority { get; set; }
    [JsonPropertyName("counterweightSelectorId")] public string? CounterweightSelectorId { get; set; }
    [JsonPropertyName("provenance")] public string Provenance { get; set; } = string.Empty;
    [JsonPropertyName("evidence")] public AwakePersonaAuthoringEvidence? Evidence { get; set; }
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
}

public sealed class AwakePersonaAuthoringMigration
{
    [JsonPropertyName("originSchema")] public string OriginSchema { get; set; } = PersonaAuthoringContractVersion.OriginSchema;
    [JsonPropertyName("originTemplateVersion")] public string OriginTemplateVersion { get; set; } = string.Empty;
    [JsonPropertyName("authoringRevision")] public int AuthoringRevision { get; set; } = PersonaAuthoringContractVersion.AuthoringRevision;
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = new List<string>();
    [JsonPropertyName("preservedLegacyData")] public Dictionary<string, object?> PreservedLegacyData { get; set; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}

public sealed class PersonaAuthoringDiagnostic
{
    public PersonaAuthoringDiagnostic(string code, string message, string path = "")
    {
        Code = code;
        Message = message;
        Path = path;
    }

    public string Code { get; }
    public string Message { get; }
    public string Path { get; }
}

public sealed class PersonaAuthoringBuildResult
{
    private PersonaAuthoringBuildResult(bool isSuccess, string errorCode, AwakePersonaAuthoringDocument? document, string canonicalJson, string expandedTextOrigin, IReadOnlyList<string> warnings, IReadOnlyList<PersonaAuthoringDiagnostic> diagnostics)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Document = document;
        CanonicalJson = canonicalJson;
        CanonicalUtf8 = Encoding.UTF8.GetBytes(canonicalJson);
        CanonicalSha256 = CanonicalUtf8.Length == 0 ? string.Empty : Convert.ToHexString(SHA256.HashData(CanonicalUtf8));
        ExpandedTextOrigin = expandedTextOrigin;
        Warnings = warnings;
        Diagnostics = diagnostics;
    }

    public bool IsSuccess { get; }
    public string ErrorCode { get; }
    public AwakePersonaAuthoringDocument? Document { get; }
    public string CanonicalJson { get; }
    public byte[] CanonicalUtf8 { get; }
    public string CanonicalSha256 { get; }
    public string ExpandedTextOrigin { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<PersonaAuthoringDiagnostic> Diagnostics { get; }

    internal static PersonaAuthoringBuildResult Success(AwakePersonaAuthoringDocument document, string canonicalJson, string origin, IReadOnlyList<string> warnings)
    {
        return new PersonaAuthoringBuildResult(true, string.Empty, document, canonicalJson, origin, warnings, Array.Empty<PersonaAuthoringDiagnostic>());
    }

    internal static PersonaAuthoringBuildResult Failure(string code, string message, string origin, IReadOnlyList<string>? warnings = null)
    {
        return Failure(new[] { new PersonaAuthoringDiagnostic(code, message) }, origin, warnings);
    }

    internal static PersonaAuthoringBuildResult Failure(IReadOnlyList<PersonaAuthoringDiagnostic> diagnostics, string origin, IReadOnlyList<string>? warnings = null)
    {
        string code = diagnostics.Count == 0 ? "persona.migration_failed" : diagnostics[0].Code;
        return new PersonaAuthoringBuildResult(false, code, null, string.Empty, origin, warnings ?? Array.Empty<string>(), diagnostics);
    }
}

internal sealed class PersonaAuthoringCrosswalkRow
{
    public PersonaAuthoringCrosswalkRow(string sourceId, string sourceKind, string targetField, string? targetId, string valueEncoding)
    {
        SourceId = sourceId;
        SourceKind = sourceKind;
        TargetField = targetField;
        TargetId = targetId;
        ValueEncoding = valueEncoding;
    }

    public string SourceId { get; }
    public string SourceKind { get; }
    public string TargetField { get; }
    public string? TargetId { get; }
    public string ValueEncoding { get; }
}

internal sealed class PersonaAuthoringValueEncoding
{
    public PersonaAuthoringValueEncoding(string id) { Id = id; }
    public string Id { get; }
    public Dictionary<string, PersonaAuthoringValueAction> Actions { get; } = new Dictionary<string, PersonaAuthoringValueAction>(StringComparer.Ordinal);
}

internal sealed class PersonaAuthoringValueAction
{
    public PersonaAuthoringValueAction(string action, string? strength, string? reviewState, string? warning)
    {
        Action = action;
        Strength = strength;
        ReviewState = reviewState;
        Warning = warning;
    }

    public string Action { get; }
    public string? Strength { get; }
    public string? ReviewState { get; }
    public string? Warning { get; }
}

public sealed class PersonaAuthoringContractAssets
{
    private const string AuthoringSchemaSha256 = "5B5E704329C8383868D66CD41DFF9E693B67E5A8AED8ACBA0EF5511410340BF9";
    private const string CrosswalkSha256 = "F15DEB55EFA34FC0224F6784FC9E3E03DB1649B41C680C5A43F92FAE6014ECFD";
    private const string CanonicalizationSha256 = "B9BE5523DAF6723E45C803157250FD5D96F2847C58C494CC0F5CD405F27E317C";
    private const string RegistrySha256 = "59CB54D92F32B5CA9BDD5D739367FE7B9964A41EF54978FED9AEA22B950634E6";
    private const string AuthoringSchemaResource = "PersonaWorkbench.Core.Contracts.awake.persona.authoring.v2.schema.json";
    private const string CrosswalkResource = "PersonaWorkbench.Core.Contracts.persona-workbench-to-awake.crosswalk.v1.json";
    private const string CanonicalizationResource = "PersonaWorkbench.Core.Contracts.persona-canonical-json.v1.json";
    private const string RegistryResource = "PersonaWorkbench.Core.Contracts.tag_registry.json";

    private PersonaAuthoringContractAssets(byte[] schema, byte[] crosswalk, byte[] canonicalization, byte[] registry, bool isValid, IReadOnlyList<string> warnings, IReadOnlyDictionary<string, PersonaAuthoringCrosswalkRow> rows, IReadOnlyDictionary<string, PersonaAuthoringValueEncoding> encodings, IReadOnlySet<string> registryTagIds)
    {
        AuthoringSchemaUtf8 = schema;
        CrosswalkUtf8 = crosswalk;
        CanonicalizationUtf8 = canonicalization;
        RegistryUtf8 = registry;
        IsValid = isValid;
        Warnings = warnings;
        Rows = rows;
        Encodings = encodings;
        RegistryTagIds = registryTagIds;
    }

    public byte[] AuthoringSchemaUtf8 { get; }
    public byte[] CrosswalkUtf8 { get; }
    public byte[] CanonicalizationUtf8 { get; }
    public byte[] RegistryUtf8 { get; }
    public int SourceRevision => PersonaContractClosureVersion.SourceRevision;
    public int AuthoringRevision => PersonaContractClosureVersion.AuthoringRevision;
    public int CrosswalkRevision => PersonaContractClosureVersion.CrosswalkRevision;
    public int RegistryRevision => PersonaContractClosureVersion.RegistryRevision;
    public string CurrentSourceSha256 => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PersonaSchema.Version)));
    public string CurrentAuthoringSha256 => Convert.ToHexString(SHA256.HashData(AuthoringSchemaUtf8));
    public string CurrentCrosswalkSha256 => Convert.ToHexString(SHA256.HashData(CrosswalkUtf8));
    public string CurrentCanonicalizationSha256 => Convert.ToHexString(SHA256.HashData(CanonicalizationUtf8));
    public string CurrentRegistrySha256 => Convert.ToHexString(SHA256.HashData(RegistryUtf8));
    public bool IsValid { get; }
    public IReadOnlyList<string> Warnings { get; }
    internal IReadOnlyDictionary<string, PersonaAuthoringCrosswalkRow> Rows { get; }
    internal IReadOnlyDictionary<string, PersonaAuthoringValueEncoding> Encodings { get; }
    internal IReadOnlySet<string> RegistryTagIds { get; }

    public static PersonaAuthoringContractAssets LoadEmbedded()
    {
        Assembly assembly = typeof(PersonaAuthoringContractAssets).Assembly;
        return FromRawUtf8(
            ReadResource(assembly, AuthoringSchemaResource),
            ReadResource(assembly, CrosswalkResource),
            ReadResource(assembly, CanonicalizationResource),
            ReadResource(assembly, RegistryResource));
    }

    public static PersonaAuthoringContractAssets FromRawUtf8(byte[] authoringSchemaUtf8, byte[] crosswalkUtf8, byte[] canonicalizationUtf8, byte[] registryUtf8)
    {
        byte[] schema = authoringSchemaUtf8?.ToArray() ?? Array.Empty<byte>();
        byte[] crosswalk = crosswalkUtf8?.ToArray() ?? Array.Empty<byte>();
        byte[] canonicalization = canonicalizationUtf8?.ToArray() ?? Array.Empty<byte>();
        byte[] registry = registryUtf8?.ToArray() ?? Array.Empty<byte>();
        try
        {
            RequireDigest(schema, AuthoringSchemaSha256, "authoring schema");
            RequireDigest(crosswalk, CrosswalkSha256, "crosswalk");
            RequireDigest(canonicalization, CanonicalizationSha256, "canonicalization contract");
            RequireDigest(registry, RegistrySha256, "tag registry");
            using JsonDocument schemaJson = ParseRequired(schema, "authoring schema");
            using JsonDocument crosswalkJson = ParseRequired(crosswalk, "crosswalk");
            using JsonDocument canonicalizationJson = ParseRequired(canonicalization, "canonicalization contract");
            using JsonDocument registryJson = ParseRequired(registry, "tag registry");
            RequireString(schemaJson.RootElement, "$id", PersonaAuthoringContractVersion.SchemaVersion);
            RequireString(canonicalizationJson.RootElement, "$id", "persona-canonical-json.v1");
            RequireString(canonicalizationJson.RootElement, "contractVersion", "persona-canonical-json.v1");
            RequireString(crosswalkJson.RootElement, "$id", "persona-workbench-to-awake.crosswalk.v1");
            RequireString(crosswalkJson.RootElement, "schemaVersion", "persona-workbench-to-awake.crosswalk.v1");
            RequireString(crosswalkJson.RootElement, "sourceSchema", PersonaAuthoringContractVersion.OriginSchema);
            RequireString(crosswalkJson.RootElement, "targetAuthoringSchema", PersonaAuthoringContractVersion.SchemaVersion);
            RequireString(registryJson.RootElement, "schemaVersion", PersonaAuthoringContractVersion.RegistryVersion);
            string registryDigest = Convert.ToHexString(SHA256.HashData(registry));
            JsonElement targetRegistry = crosswalkJson.RootElement.GetProperty("targetRegistry");
            RequireString(targetRegistry, "schemaVersion", PersonaAuthoringContractVersion.RegistryVersion);
            RequireString(targetRegistry, "digestMode", "raw_utf8_bytes");
            RequireString(targetRegistry, "sha256", registryDigest);
            HashSet<string> registryTagIds = ParseRegistryIds(registryJson.RootElement);
            Dictionary<string, PersonaAuthoringValueEncoding> encodings = ParseEncodings(crosswalkJson.RootElement);
            Dictionary<string, PersonaAuthoringCrosswalkRow> rows = ParseRows(crosswalkJson.RootElement, encodings, registryTagIds, registryDigest);
            List<string> warnings = BuildCrosswalkWarnings(crosswalkJson.RootElement, rows);
            return new PersonaAuthoringContractAssets(schema, crosswalk, canonicalization, registry, true, warnings, rows, encodings, registryTagIds);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return new PersonaAuthoringContractAssets(schema, crosswalk, canonicalization, registry, false, new[] { "persona_contract_invalid:" + ex.Message }, new Dictionary<string, PersonaAuthoringCrosswalkRow>(StringComparer.Ordinal), new Dictionary<string, PersonaAuthoringValueEncoding>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
        }
    }

    internal bool TryGetAction(PersonaAuthoringCrosswalkRow row, object value, out PersonaAuthoringValueAction action)
    {
        action = null!;
        return Encodings.TryGetValue(row.ValueEncoding, out PersonaAuthoringValueEncoding? encoding)
            && encoding.Actions.TryGetValue(ToActionKey(value), out action!);
    }

    private static byte[] ReadResource(Assembly assembly, string name)
    {
        using Stream? stream = assembly.GetManifestResourceStream(name);
        if (stream == null) return Array.Empty<byte>();
        using MemoryStream buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static JsonDocument ParseRequired(byte[] bytes, string label)
    {
        if (bytes.Length == 0) throw new InvalidOperationException(label + " resource is missing.");
        return JsonDocument.Parse(bytes);
    }

    private static void RequireDigest(byte[] bytes, string expected, string label)
    {
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), expected, StringComparison.Ordinal)) throw new InvalidOperationException(label + " digest drifted.");
    }

    private static void RequireString(JsonElement parent, string name, string expected)
    {
        if (!parent.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.String || !string.Equals(property.GetString(), expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Contract property mismatch: " + name);
        }
    }

    private static HashSet<string> ParseRegistryIds(JsonElement root)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement tag in root.GetProperty("tags").EnumerateArray())
        {
            string id = tag.GetProperty("id").GetString() ?? string.Empty;
            if (string.IsNullOrEmpty(id) || !ids.Add(id)) throw new InvalidOperationException("Registry tag ID is missing or duplicated.");
        }
        if (ids.Count == 0) throw new InvalidOperationException("Registry has no tags.");
        return ids;
    }

    private static Dictionary<string, PersonaAuthoringValueEncoding> ParseEncodings(JsonElement root)
    {
        Dictionary<string, PersonaAuthoringValueEncoding> result = new Dictionary<string, PersonaAuthoringValueEncoding>(StringComparer.Ordinal);
        foreach (JsonElement encoding in root.GetProperty("valueEncodings").EnumerateArray())
        {
            string id = encoding.GetProperty("id").GetString() ?? string.Empty;
            if (string.IsNullOrEmpty(id) || !result.TryAdd(id, new PersonaAuthoringValueEncoding(id))) throw new InvalidOperationException("Value encoding is missing or duplicated.");
            if (!encoding.TryGetProperty("actions", out JsonElement actions)) continue;
            foreach (JsonProperty actionProperty in actions.EnumerateObject())
            {
                JsonElement action = actionProperty.Value;
                result[id].Actions.Add(actionProperty.Name, new PersonaAuthoringValueAction(
                    action.GetProperty("action").GetString() ?? string.Empty,
                    action.TryGetProperty("strength", out JsonElement strength) ? strength.GetString() : null,
                    action.TryGetProperty("reviewState", out JsonElement reviewState) ? reviewState.GetString() : null,
                    action.TryGetProperty("warning", out JsonElement warning) ? warning.GetString() : null));
            }
        }
        return result;
    }

    private static Dictionary<string, PersonaAuthoringCrosswalkRow> ParseRows(JsonElement root, IReadOnlyDictionary<string, PersonaAuthoringValueEncoding> encodings, IReadOnlySet<string> registryTagIds, string registryDigest)
    {
        Dictionary<string, PersonaAuthoringCrosswalkRow> result = new Dictionary<string, PersonaAuthoringCrosswalkRow>(StringComparer.Ordinal);
        foreach (JsonElement row in root.GetProperty("rows").EnumerateArray())
        {
            string sourceId = row.GetProperty("sourceId").GetString() ?? string.Empty;
            string? targetId = row.GetProperty("targetId").ValueKind == JsonValueKind.Null ? null : row.GetProperty("targetId").GetString();
            string valueEncoding = row.GetProperty("valueEncoding").GetString() ?? string.Empty;
            PersonaAuthoringCrosswalkRow parsed = new PersonaAuthoringCrosswalkRow(sourceId, row.GetProperty("sourceKind").GetString() ?? string.Empty, row.GetProperty("targetField").GetString() ?? string.Empty, targetId, valueEncoding);
            if (string.IsNullOrEmpty(sourceId) || !result.TryAdd(sourceId, parsed)) throw new InvalidOperationException("Crosswalk source ID is missing or duplicated.");
            if (!encodings.ContainsKey(valueEncoding)) throw new InvalidOperationException("Crosswalk references an unknown value encoding.");
            if (!string.Equals(row.GetProperty("registrySha256").GetString(), registryDigest, StringComparison.Ordinal)) throw new InvalidOperationException("Crosswalk registry digest does not match the embedded registry.");
            if (targetId != null && parsed.TargetField == "observations" && encodings[valueEncoding].Actions.Values.Any(action => action.Action == "emit") && !registryTagIds.Contains(targetId)) throw new InvalidOperationException("Crosswalk emits an unregistered selector: " + targetId);
        }
        return result;
    }

    private static List<string> BuildCrosswalkWarnings(JsonElement root, IReadOnlyDictionary<string, PersonaAuthoringCrosswalkRow> rows)
    {
        HashSet<string> inventory = new HashSet<string>(StringComparer.Ordinal);
        if (root.TryGetProperty("sourceVocabulary", out JsonElement vocabulary) && vocabulary.TryGetProperty("rootFields", out JsonElement rootFields))
        {
            foreach (JsonElement value in rootFields.EnumerateArray()) inventory.Add(value.GetString() ?? string.Empty);
        }
        return rows.Values.Any(row => row.SourceKind == "root_field" && !inventory.Contains(row.SourceId))
            ? new List<string> { "crosswalk_source_vocabulary_incomplete" }
            : new List<string>();
    }

    private static string ToActionKey(object value)
    {
        return value switch
        {
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }
}

public static class PersonaAuthoringV2Validator
{
    private static readonly HashSet<string> FactKinds = new HashSet<string>(new[] { "ageYears", "role", "appearance", "identityNote" }, StringComparer.Ordinal);
    private static readonly HashSet<string> FactTrusts = new HashSet<string>(new[] { "user_confirmed", "legacy_unverified" }, StringComparer.Ordinal);
    private static readonly HashSet<string> FactProvenance = new HashSet<string>(new[] { "manual_user_confirmation", "legacy_v1", "migration", "imported" }, StringComparer.Ordinal);
    private static readonly HashSet<string> ObservationProvenance = new HashSet<string>(new[] { "manual", "legacy_v1", "provider_evidence", "workbench_ai", "migration", "imported" }, StringComparer.Ordinal);
    private static readonly HashSet<string> ReviewStates = new HashSet<string>(new[] { "accepted", "needs_review", "rejected" }, StringComparer.Ordinal);
    private static readonly HashSet<string> RuleScopes = new HashSet<string>(new[] { "global", "public", "private", "conflict", "negotiation", "romance" }, StringComparer.Ordinal);
    private static readonly HashSet<string> RuleStrengths = new HashSet<string>(new[] { "slight", "moderate", "strong" }, StringComparer.Ordinal);

    public static List<PersonaAuthoringDiagnostic> Validate(AwakePersonaAuthoringDocument document, PersonaAuthoringContractAssets assets)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(assets);
        List<PersonaAuthoringDiagnostic> diagnostics = new List<PersonaAuthoringDiagnostic>();
        if (document.SchemaVersion != PersonaAuthoringContractVersion.SchemaVersion) Add(diagnostics, "persona.authoring_schema_invalid", "Schema version is invalid.");
        ValidateStableId(diagnostics, "documentId", document.DocumentId, true);
        ValidateText(diagnostics, "displayName", document.DisplayName, true, 128);
        if (document.ReviewStatus is not ("draft" or "approved" or "disabled")) Add(diagnostics, "persona.authoring_status_invalid", "Review status is invalid.");
        string registryDigest = Convert.ToHexString(SHA256.HashData(assets.RegistryUtf8));
        if (document.RegistryVersion != PersonaAuthoringContractVersion.RegistryVersion || document.RegistryDigest != registryDigest) Add(diagnostics, "persona.authoring_registry_invalid", "Registry version or digest is invalid.");
        if (document.InstructionVersion != PersonaAuthoringContractVersion.InstructionVersion) Add(diagnostics, "persona.authoring_instruction_invalid", "Instruction version is invalid.");
        if (document.CompilerVersion != PersonaAuthoringContractVersion.CompilerVersion) Add(diagnostics, "persona.authoring_compiler_invalid", "Compiler version is invalid.");
        if (!string.IsNullOrEmpty(document.SourcePackId)) ValidateStableId(diagnostics, "sourcePackId", document.SourcePackId, true);

        if (document.Source == null) Add(diagnostics, "persona.authoring_source_invalid", "Source object is required.");
        else
        {
            ValidateText(diagnostics, "source.confirmedText", document.Source.ConfirmedText, false, null);
            ValidateText(diagnostics, "source.expandedText", document.Source.ExpandedText, false, null);
            string expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document.Source.ConfirmedText)));
            if (document.Source.ConfirmedTextSha256 != expectedHash) Add(diagnostics, "persona.authoring_source_hash_invalid", "Confirmed source hash does not match the normalized source.");
        }

        if (document.Authored == null) Add(diagnostics, "persona.authoring_authored_invalid", "Authored object is required.");
        else
        {
            ValidateText(diagnostics, "authored.core", document.Authored.Core, false, null);
            ValidateText(diagnostics, "authored.identityFacts", document.Authored.IdentityFacts, false, null);
            ValidateText(diagnostics, "authored.summary", document.Authored.Summary, false, null);
            ValidateText(diagnostics, "authored.publicDescription", document.Authored.PublicDescription, false, null);
            ValidateText(diagnostics, "authored.privateDescription", document.Authored.PrivateDescription, false, null);
            ValidateText(diagnostics, "authored.contradictionDescription", document.Authored.ContradictionDescription, false, null);
            ValidateText(diagnostics, "authored.foodPreference", document.Authored.FoodPreference, false, null);
            ValidateTextList(diagnostics, "authored.selfClaimRules", document.Authored.SelfClaimRules);
            ValidateTextList(diagnostics, "authored.realSelfBehaviors", document.Authored.RealSelfBehaviors);
            ValidateTextList(diagnostics, "authored.selfClaimExamples", document.Authored.SelfClaimExamples);
        }

        ValidateFacts(diagnostics, document.Facts);
        ValidateObservations(diagnostics, document, assets);
        ValidateRules(diagnostics, document, assets);
        ValidateMigration(diagnostics, document.Migration);
        return diagnostics;
    }

    private static void ValidateFacts(ICollection<PersonaAuthoringDiagnostic> diagnostics, List<AwakePersonaAuthoringFact>? facts)
    {
        if (facts == null) { Add(diagnostics, "persona.authoring_facts_invalid", "Facts array is required."); return; }
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (AwakePersonaAuthoringFact fact in facts)
        {
            ValidateStableId(diagnostics, "facts.factId", fact.FactId, true);
            if (!ids.Add(fact.FactId)) Add(diagnostics, "persona.authoring_duplicate_fact_id", "Fact IDs must be unique: " + fact.FactId);
            if (!FactKinds.Contains(fact.Kind)) Add(diagnostics, "persona.authoring_fact_invalid", "Fact kind is invalid: " + fact.FactId);
            if (!FactTrusts.Contains(fact.Trust) || !FactProvenance.Contains(fact.Provenance)) Add(diagnostics, "persona.authoring_fact_invalid", "Fact trust or provenance is invalid: " + fact.FactId);
            if (fact.Kind == "ageYears")
            {
                if (fact.Value is not int age || age is < 0 or > 120) Add(diagnostics, "persona.authoring_fact_invalid", "Age fact must be an integer from 0 to 120: " + fact.FactId);
            }
            else if (fact.Value is not string text || text.Length == 0) Add(diagnostics, "persona.authoring_fact_invalid", "Text fact value is required: " + fact.FactId);
        }
    }

    private static void ValidateObservations(ICollection<PersonaAuthoringDiagnostic> diagnostics, AwakePersonaAuthoringDocument document, PersonaAuthoringContractAssets assets)
    {
        if (document.Observations == null) { Add(diagnostics, "persona.authoring_observations_invalid", "Observations array is required."); return; }
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (AwakePersonaAuthoringObservation observation in document.Observations)
        {
            ValidateStableId(diagnostics, "observations.selectorId", observation.SelectorId, true);
            if (!ids.Add(observation.SelectorId)) Add(diagnostics, "persona.authoring_duplicate_observation_id", "Observation selector IDs must be unique: " + observation.SelectorId);
            if (!assets.RegistryTagIds.Contains(observation.SelectorId)) Add(diagnostics, "persona.authoring_selector_unregistered", "Observation selector is not registered: " + observation.SelectorId);
            if (observation.Value is null || !(observation.Value is string || observation.Value is int || observation.Value is bool)) Add(diagnostics, "persona.authoring_observation_invalid", "Observation value has an invalid type: " + observation.SelectorId);
            if (observation.Value is string text && text.Length == 0) Add(diagnostics, "persona.authoring_observation_invalid", "Observation text value cannot be empty: " + observation.SelectorId);
            if (!ObservationProvenance.Contains(observation.Provenance) || !ReviewStates.Contains(observation.ReviewState)) Add(diagnostics, "persona.authoring_observation_invalid", "Observation provenance or review state is invalid: " + observation.SelectorId);
            ValidateEvidence(diagnostics, document, observation.Evidence, "observations.evidence");
        }
    }

    private static void ValidateRules(ICollection<PersonaAuthoringDiagnostic> diagnostics, AwakePersonaAuthoringDocument document, PersonaAuthoringContractAssets assets)
    {
        if (document.Rules == null) { Add(diagnostics, "persona.authoring_rules_invalid", "Rules array is required."); return; }
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (AwakePersonaAuthoringRule rule in document.Rules)
        {
            ValidateStableId(diagnostics, "rules.ruleId", rule.RuleId, true);
            if (!ids.Add(rule.RuleId)) Add(diagnostics, "persona.authoring_duplicate_rule_id", "Rule IDs must be unique: " + rule.RuleId);
            ValidateStableId(diagnostics, "rules.triggerSelectorId", rule.TriggerSelectorId, true);
            ValidateStableId(diagnostics, "rules.responseSelectorId", rule.ResponseSelectorId, true);
            if (!assets.RegistryTagIds.Contains(rule.TriggerSelectorId) || !assets.RegistryTagIds.Contains(rule.ResponseSelectorId)) Add(diagnostics, "persona.authoring_selector_unregistered", "Rule selector is not registered: " + rule.RuleId);
            if (!RuleScopes.Contains(rule.Scope) || !RuleStrengths.Contains(rule.Strength) || rule.Priority is < 0 or > 100) Add(diagnostics, "persona.authoring_rule_invalid", "Rule metadata is invalid: " + rule.RuleId);
            if (rule.CounterweightSelectorId != null && !assets.RegistryTagIds.Contains(rule.CounterweightSelectorId)) Add(diagnostics, "persona.authoring_selector_unregistered", "Rule counterweight is not registered: " + rule.RuleId);
            if (!ObservationProvenance.Contains(rule.Provenance)) Add(diagnostics, "persona.authoring_rule_invalid", "Rule provenance is invalid: " + rule.RuleId);
            ValidateEvidence(diagnostics, document, rule.Evidence, "rules.evidence");
        }
    }

    private static void ValidateMigration(ICollection<PersonaAuthoringDiagnostic> diagnostics, AwakePersonaAuthoringMigration? migration)
    {
        if (migration == null) { Add(diagnostics, "persona.authoring_migration_invalid", "Migration object is required."); return; }
        if (migration.OriginSchema != PersonaAuthoringContractVersion.OriginSchema) Add(diagnostics, "persona.authoring_migration_invalid", "Origin schema is invalid.");
        ValidateText(diagnostics, "migration.originTemplateVersion", migration.OriginTemplateVersion, true, null);
        if (migration.AuthoringRevision < 1) Add(diagnostics, "persona.authoring_migration_invalid", "Authoring revision must be positive.");
        ValidateTextList(diagnostics, "migration.warnings", migration.Warnings);
        if (migration.PreservedLegacyData == null) Add(diagnostics, "persona.authoring_migration_invalid", "Preserved legacy data must be an object.");
    }

    private static void ValidateEvidence(ICollection<PersonaAuthoringDiagnostic> diagnostics, AwakePersonaAuthoringDocument document, AwakePersonaAuthoringEvidence? evidence, string path)
    {
        if (evidence == null || document.Source == null) return;
        if (evidence.SourceHash != document.Source.ConfirmedTextSha256) Add(diagnostics, "persona.authoring_evidence_invalid", "Evidence source hash does not match confirmed text.");
        if (evidence.StartUtf16 < 0 || evidence.LengthUtf16 < 1 || evidence.StartUtf16 > document.Source.ConfirmedText.Length - evidence.LengthUtf16) Add(diagnostics, "persona.authoring_evidence_invalid", "Evidence span is outside confirmed text.", path);
        ValidateText(diagnostics, path + ".quote", evidence.Quote, true, null);
    }

    private static void ValidateTextList(ICollection<PersonaAuthoringDiagnostic> diagnostics, string path, IEnumerable<string>? values)
    {
        if (values == null) { Add(diagnostics, "persona.authoring_text_list_invalid", "Text list is required: " + path); return; }
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string? value in values)
        {
            if (value == null || value.Length == 0) Add(diagnostics, "persona.authoring_text_list_invalid", "Text list contains an empty value: " + path);
            else ValidateText(diagnostics, path, value, true, null);
            if (value != null && !seen.Add(value)) Add(diagnostics, "persona.authoring_text_list_duplicate", "Text list contains a duplicate value: " + path);
        }
    }

    private static void ValidateStableId(ICollection<PersonaAuthoringDiagnostic> diagnostics, string path, string? value, bool required)
    {
        if (string.IsNullOrEmpty(value))
        {
            if (required) Add(diagnostics, "persona.authoring_id_invalid", "Stable ID is required: " + path);
            return;
        }
        if (!IsStableId(value)) Add(diagnostics, "persona.authoring_id_invalid", "Stable ID is invalid: " + path);
    }

    private static void ValidateText(ICollection<PersonaAuthoringDiagnostic> diagnostics, string path, string? value, bool required, int? maximumLength)
    {
        if (value == null || (required && value.Length == 0))
        {
            Add(diagnostics, "persona.authoring_text_invalid", "Text is required: " + path);
            return;
        }
        if (maximumLength.HasValue && value.Length > maximumLength.Value) Add(diagnostics, "persona.authoring_text_invalid", "Text is too long: " + path);
        if (HasForbiddenCharacter(value)) Add(diagnostics, "persona.authoring_text_invalid", "Text contains a forbidden control or unpaired surrogate: " + path);
    }

    private static bool HasForbiddenCharacter(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character < 0x20 || character == 0x7f) return true;
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1])) return true;
                index++;
            }
            else if (char.IsLowSurrogate(character)) return true;
        }
        return false;
    }

    private static bool IsStableId(string value)
    {
        foreach (string segment in value.Split('.'))
        {
            if (segment.Length == 0) return false;
            foreach (char character in segment)
            {
                if (!((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '_')) return false;
            }
        }
        return true;
    }

    private static void Add(ICollection<PersonaAuthoringDiagnostic> diagnostics, string code, string message, string path = "")
    {
        diagnostics.Add(new PersonaAuthoringDiagnostic(code, message, path));
    }
}

public static class PersonaAuthoringCanonicalJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public static string Serialize(AwakePersonaAuthoringDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        string raw = JsonSerializer.Serialize(document, SerializerOptions);
        using JsonDocument parsed = JsonDocument.Parse(raw);
        using MemoryStream output = new MemoryStream();
        using (Utf8JsonWriter writer = new Utf8JsonWriter(output, new JsonWriterOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false
        }))
        {
            WriteElement(writer, parsed.RootElement, string.Empty);
            writer.Flush();
        }
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static void WriteElement(Utf8JsonWriter writer, JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteElement(writer, property.Value, path.Length == 0 ? property.Name : path + "." + property.Name);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                IEnumerable<JsonElement> values = element.EnumerateArray();
                string? stableKey = path switch
                {
                    "facts" => "factId",
                    "observations" => "selectorId",
                    "rules" => "ruleId",
                    _ => null
                };
                if (stableKey != null) values = values.OrderBy(value => value.GetProperty(stableKey).GetString() ?? string.Empty, StringComparer.Ordinal);
                foreach (JsonElement value in values) WriteElement(writer, value, path);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new JsonException("Unsupported JSON value kind.");
        }
    }
}



