using PersonaWorkbench.Core;

namespace PersonaWorkbench.Web;

public class WorkspaceDocumentRequest
{
    public string RootPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public sealed class WorkspaceSaveRequest : WorkspaceDocumentRequest
{
    public PersonaDocument? Document { get; set; }
    public string? ExpectedContentHash { get; set; }
}

public sealed class WorkspaceDocumentResponse
{
    public bool IsSuccess { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string ConflictPath { get; init; } = string.Empty;
    public string ContentHash { get; init; } = string.Empty;
    public PersonaDocument? Document { get; init; }
}

public sealed class WorkspaceDocumentService
{
    private const int CanonicalDslMaximumBytes = 4096;
    private static readonly PersonaTagRegistry TagRegistry = PersonaTagRegistry.CreateDefault();

    public static bool IsSafeDocumentName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal)
            || fileName.StartsWith(".", StringComparison.Ordinal)
            || !fileName.EndsWith(".persona.json", StringComparison.OrdinalIgnoreCase)
            || fileName.Any(char.IsControl)
            || fileName.EndsWith(".", StringComparison.Ordinal)
            || fileName.EndsWith(" ", StringComparison.Ordinal)) return false;

        string stem = fileName[..^".persona.json".Length];
        string deviceName = stem.Split('.', StringSplitOptions.RemoveEmptyEntries)[0].TrimEnd('.', ' ');
        return deviceName.Length > 0
            && !new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(deviceName, StringComparer.OrdinalIgnoreCase)
            && !fileName.Any(character => Path.GetInvalidFileNameChars().Contains(character));
    }

    public WorkspaceDocumentResponse Load(WorkspaceDocumentRequest request)
    {
        if (!TryValidateRequest(request, out WorkspaceDocumentResponse error)) return error;

        try
        {
            using PersonaWorkspace workspace = PersonaWorkspace.Open(request.RootPath);
            workspace.Recover();
            PersonaWorkspaceDocument document = workspace.Read(request.FileName, PersonaTagRegistry.CreateDefault());
            return new WorkspaceDocumentResponse
            {
                IsSuccess = true,
                Document = document.Document,
                ContentHash = document.ContentHash
            };
        }
        catch (FileNotFoundException)
        {
            return Failure("workspace.document_not_found", "Persona document was not found.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure("workspace.access_denied", "Workspace access was denied.");
        }
        catch (IOException)
        {
            return Failure("workspace.io_error", "Workspace file access failed.");
        }
        catch (ArgumentException)
        {
            return Failure("workspace.path_invalid", "Workspace path is invalid.");
        }
        catch (PersonaWorkspaceException ex)
        {
            return Failure(ex.Code, ex.Message, ex.ConflictPath);
        }
        catch (PersonaDocumentFormatException ex)
        {
            return Failure(ex.Code, ex.Message);
        }
    }

    public WorkspaceDocumentResponse Save(WorkspaceSaveRequest request)
    {
        return SaveWithStatus(request, PersonaReviewStatus.Draft);
    }

    public WorkspaceDocumentResponse Approve(WorkspaceSaveRequest request)
    {
        return SaveWithStatus(request, PersonaReviewStatus.Approved);
    }

    private static WorkspaceDocumentResponse SaveWithStatus(WorkspaceSaveRequest request, string status)
    {
        if (!TryValidateRequest(request, out WorkspaceDocumentResponse error)) return error;
        if (request.Document == null) return Failure("workspace.document_required", "Persona document is required.");

        PersonaDocument candidate = CloneDocument(request.Document);
        candidate.Status = status;
        PersonaValidationResult validation = PersonaValidator.Validate(candidate, TagRegistry);
        if (!validation.IsValid)
        {
            return Failure(validation.Errors[0].Code, validation.Errors[0].Message);
        }
        if (string.Equals(status, PersonaReviewStatus.Approved, StringComparison.Ordinal))
        {
            PersonaDslResult dsl = PersonaDslGenerator.Generate(candidate, TagRegistry, CanonicalDslMaximumBytes);
            if (!dsl.IsSuccess)
            {
                return Failure(
                    dsl.Diagnostic?.ErrorCode ?? "persona.template_budget_exceeded",
                    "Persona canonical DSL could not be generated within the configured budget.");
            }
        }

        try
        {
            using PersonaWorkspace workspace = PersonaWorkspace.Open(request.RootPath);
            workspace.Recover();
            PersonaWorkspaceDocument document = workspace.Save(request.FileName, candidate, request.ExpectedContentHash);
            return new WorkspaceDocumentResponse
            {
                IsSuccess = true,
                Document = document.Document,
                ContentHash = document.ContentHash
            };
        }
        catch (UnauthorizedAccessException)
        {
            return Failure("workspace.access_denied", "Workspace access was denied.");
        }
        catch (IOException)
        {
            return Failure("workspace.io_error", "Workspace file access failed.");
        }
        catch (ArgumentException)
        {
            return Failure("workspace.path_invalid", "Workspace path is invalid.");
        }
        catch (PersonaWorkspaceException ex)
        {
            return Failure(ex.Code, ex.Message, ex.ConflictPath);
        }
        catch (PersonaDocumentFormatException ex)
        {
            return Failure(ex.Code, ex.Message);
        }
    }

    private static PersonaDocument CloneDocument(PersonaDocument source)
    {
        return new PersonaDocument
        {
            SchemaVersion = source.SchemaVersion,
            Id = source.Id,
            DisplayName = source.DisplayName,
            Core = source.Core,
            IdentityFacts = source.IdentityFacts,
            Summary = source.Summary,
            SourcePackId = source.SourcePackId,
            TemplateVersion = source.TemplateVersion,
            Status = source.Status,
            SourceDescription = source.SourceDescription,
            PublicDescription = source.PublicDescription,
            PrivateDescription = source.PrivateDescription,
            ContradictionDescription = source.ContradictionDescription,
            SelfClaimRules = source.SelfClaimRules is null ? null! : source.SelfClaimRules.ToList(),
            RealSelfBehaviors = source.RealSelfBehaviors is null ? null! : source.RealSelfBehaviors.ToList(),
            SelfClaimExamples = source.SelfClaimExamples is null ? null! : source.SelfClaimExamples.ToList(),
            Tags = source.Tags is null ? null! : source.Tags.ToList(),
            FacetStrengths = source.FacetStrengths is null ? null! : new Dictionary<string, int>(source.FacetStrengths, StringComparer.Ordinal),
            TraitProfile = source.TraitProfile is null ? null! : new PersonaTraitProfile
            {
                Caution = source.TraitProfile.Caution,
                Ambition = source.TraitProfile.Ambition,
                Pride = source.TraitProfile.Pride,
                Pragmatism = source.TraitProfile.Pragmatism,
                InGroupLoyalty = source.TraitProfile.InGroupLoyalty,
                Tradition = source.TraitProfile.Tradition
            },
            ExpressionProfile = source.ExpressionProfile is null ? null! : new PersonaExpressionProfile
            {
                Restraint = source.ExpressionProfile.Restraint,
                Directness = source.ExpressionProfile.Directness,
                Formality = source.ExpressionProfile.Formality,
                Playfulness = source.ExpressionProfile.Playfulness,
                Warmth = source.ExpressionProfile.Warmth
            },
            BehaviorProfile = source.BehaviorProfile is null ? null! : new PersonaBehaviorProfile
            {
                Conditionality = source.BehaviorProfile.Conditionality,
                Deliberation = source.BehaviorProfile.Deliberation,
                TrustTesting = source.BehaviorProfile.TrustTesting,
                Leverage = source.BehaviorProfile.Leverage,
                InGroupPriority = source.BehaviorProfile.InGroupPriority,
                Leadership = source.BehaviorProfile.Leadership
            },
            ReactionProfile = source.ReactionProfile is null ? null! : new PersonaReactionProfile
            {
                Confrontation = source.ReactionProfile.Confrontation,
                Expression = source.ReactionProfile.Expression,
                Timing = source.ReactionProfile.Timing,
                Resentment = source.ReactionProfile.Resentment,
                SupportSeeking = source.ReactionProfile.SupportSeeking,
                SensitiveConditions = source.ReactionProfile.SensitiveConditions,
                ConditionalResponses = source.ReactionProfile.ConditionalResponses
            },
            CommitmentProfile = source.CommitmentProfile is null ? null! : new PersonaCommitmentProfile
            {
                PromiseCaution = source.CommitmentProfile.PromiseCaution,
                PromisePersistence = source.CommitmentProfile.PromisePersistence,
                ValueTradeability = source.CommitmentProfile.ValueTradeability,
                PriorityOrder = source.CommitmentProfile.PriorityOrder,
                ProtectedValues = source.CommitmentProfile.ProtectedValues,
                ApplicableScope = source.CommitmentProfile.ApplicableScope,
                ExceptionCost = source.CommitmentProfile.ExceptionCost,
                BreachResponse = source.CommitmentProfile.BreachResponse
            }
        };
    }

    private static bool TryValidateRequest(WorkspaceDocumentRequest request, out WorkspaceDocumentResponse error)
    {
        if (request == null)
        {
            error = Failure("workspace.request_invalid", "Workspace request is required.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(request.RootPath))
        {
            error = Failure("workspace.root_required", "Workspace root path is required.");
            return false;
        }
        if (!IsSafeDocumentName(request.FileName))
        {
            error = Failure("workspace.file_name_invalid", "Persona document name must be a local .persona.json file name.");
            return false;
        }
        try
        {
            string fullRoot = Path.GetFullPath(request.RootPath);
            if (File.Exists(fullRoot))
            {
                error = Failure("workspace.root_invalid", "Workspace root must be a directory.");
                return false;
            }
        }
        catch (ArgumentException)
        {
            error = Failure("workspace.root_invalid", "Workspace root path is invalid.");
            return false;
        }

        error = new WorkspaceDocumentResponse();
        return true;
    }

    private static WorkspaceDocumentResponse Failure(string code, string message, string? conflictPath = null)
    {
        return new WorkspaceDocumentResponse
        {
            IsSuccess = false,
            ErrorCode = code,
            ErrorMessage = message,
            ConflictPath = conflictPath ?? string.Empty
        };
    }
}
