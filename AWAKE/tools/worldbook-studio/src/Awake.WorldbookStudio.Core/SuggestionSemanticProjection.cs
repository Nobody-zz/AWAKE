namespace Awake.WorldbookStudio.Core;

internal readonly record struct SuggestionSemanticProjectionResult(
    string Id,
    string Kind,
    string Severity,
    double Confidence,
    string Title,
    string Reason,
    string? CandidateText,
    KnowledgePatch? Patch,
    bool ReviewOnly,
    string? ApplyNonce,
    string SuggestionHash);

internal static class SuggestionSemanticProjection
{
    internal static SuggestionSemanticProjectionResult Project(SuggestionEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return new SuggestionSemanticProjectionResult(
            envelope.SuggestionId,
            envelope.Suggestion.Kind,
            envelope.Suggestion.Severity,
            envelope.Suggestion.Confidence,
            envelope.Suggestion.Title,
            envelope.Suggestion.Reason,
            envelope.Suggestion.CandidateText,
            envelope.Suggestion.Patch,
            envelope.Suggestion.ReviewOnly,
            envelope.ApplyNonce,
            envelope.SuggestionHash);
    }
}
