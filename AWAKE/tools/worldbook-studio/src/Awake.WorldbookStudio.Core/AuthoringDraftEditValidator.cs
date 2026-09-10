namespace Awake.WorldbookStudio.Core;

internal sealed record AuthoringDraftMetadataReview(
    AuthoringDraftMetadata FinalMetadata,
    string Status,
    IReadOnlyList<string> ModifiedFields,
    string RevalidationStatus);

internal static class AuthoringDraftEditValidator
{
    public static AuthoringDraftMetadataReview RevalidateMetadata(
        AuthoringDraftMetadata? generated,
        AuthoringDraftMetadata submitted,
        IReadOnlyList<AuthoringDraftFact> acceptedFacts,
        AuthoringDraftIntent? intent)
    {
        ArgumentNullException.ThrowIfNull(submitted);
        var modifiedFields = new List<string>();
        if (generated is null || !string.Equals(generated.Title, submitted.Title, StringComparison.Ordinal))
            modifiedFields.Add("title");
        if (generated is null || !string.Equals(generated.Summary, submitted.Summary, StringComparison.Ordinal))
            modifiedFields.Add("summary");
        if (generated is null || !string.Equals(generated.Domain, submitted.Domain, StringComparison.Ordinal))
            modifiedFields.Add("domain");
        if (generated is null || !string.Equals(generated.Subdomain, submitted.Subdomain, StringComparison.Ordinal))
            modifiedFields.Add("subdomain");
        if (generated is null || !generated.RelatedDomains.SequenceEqual(submitted.RelatedDomains, StringComparer.Ordinal))
            modifiedFields.Add("related_domains");

        EnsureNoForbiddenEdit(
            intent,
            $"{submitted.Title}\n{submitted.Summary}\n{submitted.Subdomain}\n{string.Join("\n", submitted.RelatedDomains)}");

        var revalidationStatus = "source_checked";
        if (modifiedFields.Count > 0
            && (!IsGroundedInFacts(submitted.Title, acceptedFacts)
                || !IsGroundedInFacts(submitted.Summary, acceptedFacts)))
            revalidationStatus = "manual_required";

        return new AuthoringDraftMetadataReview(
            submitted,
            modifiedFields.Count == 0 ? "unchanged" : "author_modified",
            modifiedFields,
            revalidationStatus);
    }

    public static AuthoringDraftSubmissionValidator.AcceptedSubmission RevalidateAcceptedSubmission(
        AuthoringDraftResult result,
        AuthoringDraftIntent? intent,
        IReadOnlyList<AuthoringDraftFact>? submittedFacts,
        IReadOnlyList<AuthoringDraftExpression>? submittedExpressions)
    {
        var normalized = AuthoringDraftSubmissionValidator.NormalizeAcceptedSubmission(
            result,
            submittedFacts,
            submittedExpressions);
        var facts = normalized.Facts
            .Select(fact => RevalidateFact(result, intent, fact))
            .ToArray();
        var expressions = normalized.Expressions
            .Select(expression => RevalidateExpression(result, intent, expression))
            .ToArray();
        return new AuthoringDraftSubmissionValidator.AcceptedSubmission(facts, expressions);
    }

    private static AuthoringDraftFact RevalidateFact(
        AuthoringDraftResult result,
        AuthoringDraftIntent? intent,
        AuthoringDraftFact fact)
    {
        var generated = FindFact(result, fact.Id);
        if (string.Equals(generated.Text.Trim(), fact.Text.Trim(), StringComparison.Ordinal))
            return fact with { ReviewStatus = "accepted" };
        EnsureNoForbiddenEdit(intent, fact.Text);
        EnsureEditedTextIsSourceBound(fact.Text, EvidenceFor(generated), $"事实 {fact.Id}");
        return fact with
        {
            ReviewStatus = "author_modified",
            Evidence = generated.Evidence is null ? null : MarkUnverified(generated.Evidence),
            EvidenceGroup = generated.EvidenceGroup?.Select(MarkUnverified).ToArray()
        };
    }

    private static AuthoringDraftExpression RevalidateExpression(
        AuthoringDraftResult result,
        AuthoringDraftIntent? intent,
        AuthoringDraftExpression expression)
    {
        var generated = FindExpression(result, expression.Id);
        if (string.Equals(generated.Text.Trim(), expression.Text.Trim(), StringComparison.Ordinal))
            return expression with { ReviewStatus = "accepted" };
        EnsureNoForbiddenEdit(intent, expression.Text);
        EnsureEditedTextIsSourceBound(
            expression.Text,
            expression.Evidence is null ? [] : [expression.Evidence],
            $"身份表达 {expression.Id}");
        return expression with
        {
            ReviewStatus = "author_modified",
            Evidence = generated.Evidence is null ? null : MarkUnverified(generated.Evidence)
        };
    }

    private static AuthoringDraftFact FindFact(AuthoringDraftResult result, string id)
        => result.Facts
            .Concat(result.CandidateSet?.Candidates.SelectMany(item => item.Facts) ?? [])
            .Concat(result.Candidates?.SelectMany(item => item.Facts) ?? [])
            .FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"WB-AI-DRAFT-EDIT-409: 找不到已采纳事实：{id}。");

    private static AuthoringDraftExpression FindExpression(AuthoringDraftResult result, string id)
        => result.Expressions
            .Concat(result.CandidateSet?.Candidates.SelectMany(item => item.Expressions) ?? [])
            .Concat(result.Candidates?.SelectMany(item => item.Expressions) ?? [])
            .FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"WB-AI-DRAFT-EDIT-409: 找不到已采纳身份表达：{id}。");

    private static IReadOnlyList<AuthoringDraftEvidence> EvidenceFor(AuthoringDraftFact fact)
        => (fact.EvidenceGroup ?? [])
            .Concat(fact.Evidence is null ? [] : [fact.Evidence])
            .ToArray();

    private static void EnsureEditedTextIsSourceBound(
        string editedText,
        IReadOnlyList<AuthoringDraftEvidence> evidence,
        string label)
    {
        if (!evidence.Any(item => SourceEvidenceMatcher.TryFind(item.Quote, editedText, out _)))
            throw new InvalidOperationException(
                $"WB-AI-DRAFT-EDIT-422: {label} 的修改文本无法在原始证据中定位，已阻断建档；请重新生成或人工复核。");
    }

    private static void EnsureNoForbiddenEdit(AuthoringDraftIntent? intent, string editedText)
    {
        foreach (var forbidden in intent?.MustNotInvent ?? [])
        {
            if (!string.IsNullOrWhiteSpace(forbidden)
                && editedText.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"WB-AI-DRAFT-EDIT-422: 修改文本触发禁止新增约束“{forbidden}”，已阻断建档。");
        }
    }

    private static bool IsGroundedInFacts(
        string? text,
        IReadOnlyList<AuthoringDraftFact> facts)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return facts.Any(fact =>
            SourceEvidenceMatcher.TryFind(fact.Text, text, out _)
            || EvidenceFor(fact).Any(evidence => SourceEvidenceMatcher.TryFind(evidence.Quote, text, out _)));
    }

    private static AuthoringDraftEvidence MarkUnverified(AuthoringDraftEvidence evidence)
        => evidence with { Verified = false };

}
