using PersonaWorkbench.Core;

namespace PersonaWorkbench.Web;

public sealed class AwakeAuthoringPreviewRequest : PersonaPreviewRequest
{
    public string ExpandedText { get; set; } = string.Empty;
    public string ExpandedTextOrigin { get; set; } = PersonaAuthoringExpansionOrigin.None;
}

public sealed class AwakeAuthoringPreviewResponse
{
    public bool IsValid { get; init; }
    public string CanonicalJson { get; init; } = string.Empty;
    public string CanonicalSha256 { get; init; } = string.Empty;
    public string CanonicalUtf8Base64 { get; init; } = string.Empty;
    public string ExpandedTextOrigin { get; init; } = PersonaAuthoringExpansionOrigin.None;
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public static class AwakeAuthoringPreviewService
{
    public static AwakeAuthoringPreviewResponse Build(AwakeAuthoringPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        PersonaAuthoringBuildResult result = PersonaAuthoringV2Adapter.Build(
            PersonaPreviewService.CreateDocument(request),
            request.ExpandedText ?? string.Empty,
            request.ExpandedTextOrigin ?? PersonaAuthoringExpansionOrigin.None);

        if (!result.IsSuccess)
        {
            return new AwakeAuthoringPreviewResponse
            {
                IsValid = false,
                ExpandedTextOrigin = result.ExpandedTextOrigin,
                Warnings = result.Warnings,
                Errors = result.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message).ToArray()
            };
        }

        return new AwakeAuthoringPreviewResponse
        {
            IsValid = true,
            CanonicalJson = result.CanonicalJson,
            CanonicalSha256 = result.CanonicalSha256,
            CanonicalUtf8Base64 = Convert.ToBase64String(result.CanonicalUtf8),
            ExpandedTextOrigin = result.ExpandedTextOrigin,
            Warnings = result.Warnings
        };
    }
}
