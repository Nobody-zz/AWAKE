using System.Text;

namespace PersonaWorkbench.Core;

public static class PersonaSchema
{
    public const string Version = "persona-workbench.character.v1";
}

public static class PersonaReviewStatus
{
    public const string Draft = "draft";
    public const string Approved = "approved";

    public static bool IsValid(string? value)
    {
        return string.Equals(value, Draft, StringComparison.Ordinal)
            || string.Equals(value, Approved, StringComparison.Ordinal);
    }
}

public enum PersonaTagCategory
{
    Trait,
    Expression,
    Behavior,
    Trigger,
    Boundary
}

public sealed class PersonaDocument
{
    public string SchemaVersion { get; set; } = PersonaSchema.Version;
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Core { get; set; } = string.Empty;
    public string IdentityFacts { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SourcePackId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = "persona-load.v2";
    public string Status { get; set; } = PersonaReviewStatus.Draft;
    public string SourceDescription { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string PrivateDescription { get; set; } = string.Empty;
    public string ContradictionDescription { get; set; } = string.Empty;
    public string FoodPreference { get; set; } = string.Empty;
    public List<string> SelfClaimRules { get; set; } = new List<string>();
    public List<string> RealSelfBehaviors { get; set; } = new List<string>();
    public List<string> SelfClaimExamples { get; set; } = new List<string>();
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, int> FacetStrengths { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
    public PersonaTraitProfile TraitProfile { get; set; } = new PersonaTraitProfile();
    public PersonaExpressionProfile ExpressionProfile { get; set; } = new PersonaExpressionProfile();
    public PersonaBehaviorProfile BehaviorProfile { get; set; } = new PersonaBehaviorProfile();
    public PersonaReactionProfile ReactionProfile { get; set; } = new PersonaReactionProfile();
    public PersonaCommitmentProfile CommitmentProfile { get; set; } = new PersonaCommitmentProfile();
}

public sealed class PersonaTraitProfile
{
    public int? Caution { get; set; }
    public int? Ambition { get; set; }
    public int? Pride { get; set; }
    public int? Pragmatism { get; set; }
    public int? InGroupLoyalty { get; set; }
    public int? Tradition { get; set; }
}

public sealed class PersonaExpressionProfile
{
    public int? Restraint { get; set; }
    public int? Directness { get; set; }
    public int? Formality { get; set; }
    public int? Playfulness { get; set; }
    public int? Warmth { get; set; }
}

public sealed class PersonaBehaviorProfile
{
    public int? Conditionality { get; set; }
    public int? Deliberation { get; set; }
    public int? TrustTesting { get; set; }
    public int? Leverage { get; set; }
    public int? InGroupPriority { get; set; }
    public int? Leadership { get; set; }
}

public sealed class PersonaReactionProfile
{
    public int? Confrontation { get; set; }
    public int? Expression { get; set; }
    public int? Timing { get; set; }
    public int? Resentment { get; set; }
    public int? SupportSeeking { get; set; }
    public string SensitiveConditions { get; set; } = string.Empty;
    public string ConditionalResponses { get; set; } = string.Empty;
}

public sealed class PersonaCommitmentProfile
{
    public int? PromiseCaution { get; set; }
    public int? PromisePersistence { get; set; }
    public int? ValueTradeability { get; set; }
    public string PriorityOrder { get; set; } = string.Empty;
    public string ProtectedValues { get; set; } = string.Empty;
    public string ApplicableScope { get; set; } = string.Empty;
    public string ExceptionCost { get; set; } = string.Empty;
    public string BreachResponse { get; set; } = string.Empty;
}

public sealed class PersonaTagDefinition
{
    public PersonaTagDefinition(string id, PersonaTagCategory category, string displayName)
    {
        Id = id;
        Category = category;
        DisplayName = displayName;
    }

    public string Id { get; }
    public PersonaTagCategory Category { get; }
    public string DisplayName { get; }
}

public sealed class PersonaTagRegistry
{
    private readonly Dictionary<string, PersonaTagDefinition> _tags;

    public PersonaTagRegistry(IEnumerable<PersonaTagDefinition> tags)
    {
        _tags = tags.ToDictionary(tag => tag.Id, StringComparer.Ordinal);
    }

    public static PersonaTagRegistry CreateDefault()
    {
        return new PersonaTagRegistry(new[]
        {
            new PersonaTagDefinition("trait.cautious", PersonaTagCategory.Trait, "谨慎"),
            new PersonaTagDefinition("trait.ambitious", PersonaTagCategory.Trait, "野心勃勃"),
            new PersonaTagDefinition("trait.proud", PersonaTagCategory.Trait, "自尊强烈"),
            new PersonaTagDefinition("trait.pragmatic", PersonaTagCategory.Trait, "现实务实"),
            new PersonaTagDefinition("trait.guardian", PersonaTagCategory.Trait, "护卫自己人"),
            new PersonaTagDefinition("trait.traditional", PersonaTagCategory.Trait, "重视传统"),
            new PersonaTagDefinition("expression.measured", PersonaTagCategory.Expression, "措辞克制"),
            new PersonaTagDefinition("expression.direct", PersonaTagCategory.Expression, "直白明确"),
            new PersonaTagDefinition("expression.formal", PersonaTagCategory.Expression, "礼貌正式"),
            new PersonaTagDefinition("expression.teasing", PersonaTagCategory.Expression, "喜欢戏谑试探"),
            new PersonaTagDefinition("expression.warm", PersonaTagCategory.Expression, "温和亲近"),
            new PersonaTagDefinition("behavior.bargains", PersonaTagCategory.Behavior, "先谈条件"),
            new PersonaTagDefinition("behavior.observes_before_acting", PersonaTagCategory.Behavior, "先观察再行动"),
            new PersonaTagDefinition("behavior.tests_loyalty", PersonaTagCategory.Behavior, "习惯试探忠诚"),
            new PersonaTagDefinition("behavior.keeps_leverage", PersonaTagCategory.Behavior, "习惯留后手"),
            new PersonaTagDefinition("behavior.protects_inner_circle", PersonaTagCategory.Behavior, "优先保护自己人"),
            new PersonaTagDefinition("behavior.takes_command", PersonaTagCategory.Behavior, "倾向直接主导"),
            new PersonaTagDefinition("trigger.public_humiliation", PersonaTagCategory.Trigger, "被公开羞辱时反击"),
            new PersonaTagDefinition("boundary.no_empty_promises", PersonaTagCategory.Boundary, "不作空头许诺")
        });
    }

    public IReadOnlyList<string> Ids => _tags.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();

    public bool TryGet(string id, out PersonaTagDefinition definition)
    {
        return _tags.TryGetValue(id ?? string.Empty, out definition!);
    }
}

public sealed class PersonaValidationError
{
    public PersonaValidationError(string code, string message)
    {
        Code = code;
        Message = message;
    }

    public string Code { get; }
    public string Message { get; }
}

public sealed class PersonaValidationResult
{
    public List<PersonaValidationError> Errors { get; } = new List<PersonaValidationError>();
    public bool IsValid => Errors.Count == 0;
}

public static class PersonaValidator
{
    public static PersonaValidationResult Validate(PersonaDocument document, PersonaTagRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(registry);

        PersonaValidationResult result = new PersonaValidationResult();
        if (!string.Equals(document.SchemaVersion, PersonaSchema.Version, StringComparison.Ordinal))
        {
            result.Errors.Add(new PersonaValidationError("schema.unsupported", "Unsupported Persona schema version."));
        }
        if (string.IsNullOrWhiteSpace(document.Id))
        {
            result.Errors.Add(new PersonaValidationError("persona.id_required", "Persona ID is required."));
        }
        if (string.IsNullOrWhiteSpace(document.Core))
        {
            result.Errors.Add(new PersonaValidationError("persona.core_required", "Persona core is required."));
        }
        if (!PersonaReviewStatus.IsValid(document.Status))
        {
            result.Errors.Add(new PersonaValidationError("persona.status_invalid", "Persona status must be draft or approved."));
        }

        if (document.Tags is null)
        {
            result.Errors.Add(new PersonaValidationError("persona.tags_required", "Persona tags must be an array."));
        }
        else
        {
            HashSet<string> encounteredTags = new HashSet<string>(StringComparer.Ordinal);
            foreach (string? tagId in document.Tags)
            {
                if (string.IsNullOrWhiteSpace(tagId))
                {
                    result.Errors.Add(new PersonaValidationError("tag.invalid", "Persona tags cannot contain null or empty values."));
                    continue;
                }
                if (!encounteredTags.Add(tagId))
                {
                    result.Errors.Add(new PersonaValidationError("tag.duplicate", "Persona tags cannot contain duplicates: " + tagId));
                    continue;
                }
                if (!registry.TryGet(tagId, out _))
                {
                    result.Errors.Add(new PersonaValidationError("tag.unregistered", "Tag is not registered: " + tagId));
                }
            }
        }

        if (document.FacetStrengths is null)
        {
            result.Errors.Add(new PersonaValidationError("persona.facet_strengths_required", "Facet strengths must be an object."));
        }
        else
        {
            foreach ((string tagId, int strength) in document.FacetStrengths)
            {
                if (!registry.TryGet(tagId, out PersonaTagDefinition definition))
                {
                    result.Errors.Add(new PersonaValidationError("facet.unregistered", "Facet is not registered: " + tagId));
                    continue;
                }
                if (definition.Category is PersonaTagCategory.Trigger or PersonaTagCategory.Boundary)
                {
                    result.Errors.Add(new PersonaValidationError("facet.category_not_supported", "Facet strength is not supported for trigger or boundary tags: " + tagId));
                }
                if (strength is < 1 or > 4)
                {
                    result.Errors.Add(new PersonaValidationError("facet.strength_invalid", "Facet strength must be between 1 and 4: " + tagId));
                }
            }
        }

        if (document.SelfClaimRules is null)
        {
            result.Errors.Add(new PersonaValidationError("persona.self_claim_rules_required", "Self claim rules must be an array."));
        }
        if (document.RealSelfBehaviors is null)
        {
            result.Errors.Add(new PersonaValidationError("persona.real_self_behaviors_required", "Real self behaviors must be an array."));
        }
        if (document.SelfClaimExamples is null)
        {
            result.Errors.Add(new PersonaValidationError("persona.self_claim_examples_required", "Self claim examples must be an array."));
        }
        if (document.TraitProfile is null)
        {
            result.Errors.Add(new PersonaValidationError("persona.trait_profile_required", "Trait profile must be an object."));
        }
        if (document.ExpressionProfile is null)
        {
            result.Errors.Add(new PersonaValidationError("persona.expression_profile_required", "Expression profile must be an object."));
        }
        if (document.BehaviorProfile is null)
        {
            result.Errors.Add(new PersonaValidationError("persona.behavior_profile_required", "Behavior profile must be an object."));
        }
        if (document.ReactionProfile is null)
        {
            result.Errors.Add(new PersonaValidationError("persona.reaction_profile_required", "Reaction profile must be an object."));
        }
        if (document.CommitmentProfile is null)
        {
            result.Errors.Add(new PersonaValidationError("persona.commitment_profile_required", "Commitment profile must be an object."));
        }

        ValidateAxis(result, "trait.caution", document.TraitProfile?.Caution);
        ValidateAxis(result, "trait.ambition", document.TraitProfile?.Ambition);
        ValidateAxis(result, "trait.pride", document.TraitProfile?.Pride);
        ValidateAxis(result, "trait.pragmatism", document.TraitProfile?.Pragmatism);
        ValidateAxis(result, "trait.in_group_loyalty", document.TraitProfile?.InGroupLoyalty);
        ValidateAxis(result, "trait.tradition", document.TraitProfile?.Tradition);
        ValidateAxis(result, "expression.restraint", document.ExpressionProfile?.Restraint);
        ValidateAxis(result, "expression.directness", document.ExpressionProfile?.Directness);
        ValidateAxis(result, "expression.formality", document.ExpressionProfile?.Formality);
        ValidateAxis(result, "expression.playfulness", document.ExpressionProfile?.Playfulness);
        ValidateAxis(result, "expression.warmth", document.ExpressionProfile?.Warmth);
        ValidateAxis(result, "behavior.conditionality", document.BehaviorProfile?.Conditionality);
        ValidateAxis(result, "behavior.deliberation", document.BehaviorProfile?.Deliberation);
        ValidateAxis(result, "behavior.trust_testing", document.BehaviorProfile?.TrustTesting);
        ValidateAxis(result, "behavior.leverage", document.BehaviorProfile?.Leverage);
        ValidateAxis(result, "behavior.in_group_priority", document.BehaviorProfile?.InGroupPriority);
        ValidateAxis(result, "behavior.leadership", document.BehaviorProfile?.Leadership);
        ValidateAxis(result, "reaction.confrontation", document.ReactionProfile?.Confrontation);
        ValidateAxis(result, "reaction.expression", document.ReactionProfile?.Expression);
        ValidateAxis(result, "reaction.timing", document.ReactionProfile?.Timing);
        ValidateAxis(result, "reaction.resentment", document.ReactionProfile?.Resentment);
        ValidateAxis(result, "reaction.support_seeking", document.ReactionProfile?.SupportSeeking);
        ValidateAxis(result, "commitment.promise_caution", document.CommitmentProfile?.PromiseCaution);
        ValidateAxis(result, "commitment.promise_persistence", document.CommitmentProfile?.PromisePersistence);
        ValidateAxis(result, "commitment.value_tradeability", document.CommitmentProfile?.ValueTradeability);

        return result;
    }

    private static void ValidateAxis(PersonaValidationResult result, string axisId, int? value)
    {
        if (value is < -2 or > 2)
        {
            result.Errors.Add(new PersonaValidationError("axis.value_invalid", "Axis value must be between -2 and 2: " + axisId));
        }
    }
}

public enum PersonaDslGenerationStatus
{
    Success,
    Compressed,
    BudgetExceeded,
    CoreBudgetExceeded
}

public sealed class PersonaDslDiagnostic
{
    public string ErrorCode { get; init; } = string.Empty;
    public int MaximumBytes { get; init; }
    public int ActualBytes { get; init; }
    public bool Compressed { get; init; }
    public IReadOnlyList<string> Actions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ProtectedFieldsPreserved { get; init; } = Array.Empty<string>();
}

public sealed class PersonaDslResult
{
    public PersonaDslResult(string dsl)
        : this(dsl, PersonaDslGenerationStatus.Success, null)
    {
    }

    public PersonaDslResult(string dsl, PersonaDslGenerationStatus status, PersonaDslDiagnostic? diagnostic)
    {
        Dsl = dsl;
        Status = status;
        Diagnostic = diagnostic;
    }

    public string Dsl { get; }
    public PersonaDslGenerationStatus Status { get; }
    public PersonaDslDiagnostic? Diagnostic { get; }
    public bool IsSuccess => Status is PersonaDslGenerationStatus.Success or PersonaDslGenerationStatus.Compressed;
}

public static class PersonaIdentityNormalizer
{
    public const string DefaultDisplayName = "未命名角色";

    public static void NormalizeInPlace(PersonaDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.DisplayName = string.IsNullOrWhiteSpace(document.DisplayName)
            ? DefaultDisplayName
            : document.DisplayName.Trim();
        document.Id = document.Id?.Trim() ?? string.Empty;
    }
}

public static class PersonaDslGenerator
{
    public static PersonaDslResult Generate(PersonaDocument document, PersonaTagRegistry registry, int maximumBytes)
    {
        return CanonicalPersonaTemplateGenerator.Generate(document, registry, maximumBytes);
    }

}
