using PersonaWorkbench.Core;

namespace PersonaWorkbench.Web;

public class PersonaPreviewRequest
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Core { get; set; } = string.Empty;
    public string IdentityFacts { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SourcePackId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = "persona-load.v2";
    public string Status { get; set; } = "draft";
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

public sealed class PersonaPreviewResponse
{
    public bool IsValid { get; init; }
    public string Dsl { get; init; } = string.Empty;
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public PersonaDslDiagnostic? Diagnostic { get; init; }
}

public static class PersonaPreviewService
{
    public static PersonaPreviewResponse Build(PersonaPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        PersonaDocument document = CreateDocument(request);
        PersonaIdentityNormalizer.NormalizeInPlace(document);
        PersonaTagRegistry registry = PersonaTagRegistry.CreateDefault();
        PersonaValidationResult validation = PersonaValidator.Validate(document, registry);
        if (!validation.IsValid)
        {
            return new PersonaPreviewResponse
            {
                IsValid = false,
                Errors = validation.Errors.Select(error => error.Code + ": " + error.Message).ToArray()
            };
        }

        PersonaDslResult result = PersonaDslGenerator.Generate(document, registry, 4096);
        if (!result.IsSuccess)
        {
            return new PersonaPreviewResponse
            {
                IsValid = false,
                Errors = new[] { result.Diagnostic?.ErrorCode ?? "persona.template_budget_exceeded" },
                Diagnostic = result.Diagnostic
            };
        }

        return new PersonaPreviewResponse
        {
            IsValid = true,
            Dsl = result.Dsl,
            Diagnostic = result.Diagnostic
        };
    }

    public static PersonaDocument CreateDocument(PersonaPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new PersonaDocument
        {
            Id = request.Id ?? string.Empty,
            DisplayName = request.DisplayName ?? string.Empty,
            Core = request.Core ?? string.Empty,
            IdentityFacts = request.IdentityFacts ?? string.Empty,
            Summary = request.Summary ?? string.Empty,
            SourcePackId = request.SourcePackId ?? string.Empty,
            TemplateVersion = request.TemplateVersion ?? "persona-load.v2",
            Status = request.Status ?? "draft",
            SourceDescription = request.SourceDescription ?? string.Empty,
            PublicDescription = request.PublicDescription ?? string.Empty,
            PrivateDescription = request.PrivateDescription ?? string.Empty,
            ContradictionDescription = request.ContradictionDescription ?? string.Empty,
            FoodPreference = request.FoodPreference ?? string.Empty,
            SelfClaimRules = request.SelfClaimRules ?? new List<string>(),
            RealSelfBehaviors = request.RealSelfBehaviors ?? new List<string>(),
            SelfClaimExamples = request.SelfClaimExamples ?? new List<string>(),
            Tags = request.Tags ?? new List<string>(),
            FacetStrengths = request.FacetStrengths ?? new Dictionary<string, int>(StringComparer.Ordinal),
            TraitProfile = request.TraitProfile ?? new PersonaTraitProfile(),
            ExpressionProfile = request.ExpressionProfile ?? new PersonaExpressionProfile(),
            BehaviorProfile = request.BehaviorProfile ?? new PersonaBehaviorProfile(),
            ReactionProfile = request.ReactionProfile ?? new PersonaReactionProfile(),
            CommitmentProfile = request.CommitmentProfile ?? new PersonaCommitmentProfile()
        };
    }
}
