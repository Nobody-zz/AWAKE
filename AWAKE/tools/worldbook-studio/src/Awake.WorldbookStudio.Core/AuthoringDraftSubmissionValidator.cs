namespace Awake.WorldbookStudio.Core;

internal static class AuthoringDraftSubmissionValidator
{
    internal sealed record AcceptedSubmission(
        IReadOnlyList<AuthoringDraftFact> Facts,
        IReadOnlyList<AuthoringDraftExpression> Expressions);

    public static void EnsureAcceptedSubmission(
        AuthoringDraftResult result,
        IReadOnlyList<AuthoringDraftFact>? submittedFacts,
        IReadOnlyList<AuthoringDraftExpression>? submittedExpressions)
        => NormalizeAcceptedSubmission(result, submittedFacts, submittedExpressions);

    public static IReadOnlyList<AuthoringDraftFact> NormalizeAcceptedFactsForContinuation(
        AuthoringDraftResult result,
        IReadOnlyList<AuthoringDraftFact>? submittedFacts)
    {
        ArgumentNullException.ThrowIfNull(result);
        var facts = submittedFacts ?? [];
        if (facts.Count == 0)
            throw InvalidSubmission("请先在草稿区至少采纳一条客观事实。" );
        var authoritativeFacts = AllFacts(result);
        EnsureUniqueIds(authoritativeFacts.Select(item => item.Id), "AI 事实");
        EnsureUniqueIds(facts.Select(item => item.Id), "提交的事实");
        var resultFacts = authoritativeFacts.ToDictionary(item => item.Id, StringComparer.Ordinal);
        return facts.Select(fact =>
        {
            if (!IsSubmittedReviewStatus(fact.ReviewStatus))
                throw InvalidSubmission("提交的事实必须先标记为已采纳。" );
            if (!resultFacts.TryGetValue(fact.Id, out var generated))
                throw InvalidSubmission("提交的事实不属于当前 AI 草稿，请重新生成并采纳。" );
            if (string.IsNullOrWhiteSpace(fact.Text))
                throw InvalidSubmission("提交的事实内容不能为空。" );
            return generated with { Text = fact.Text.Trim(), ReviewStatus = "accepted" };
        }).ToArray();
    }

    public static AcceptedSubmission NormalizeAcceptedSubmission(
        AuthoringDraftResult result,
        IReadOnlyList<AuthoringDraftFact>? submittedFacts,
        IReadOnlyList<AuthoringDraftExpression>? submittedExpressions)
    {
        ArgumentNullException.ThrowIfNull(result);

        var facts = submittedFacts ?? [];
        if (facts.Count == 0)
            throw InvalidSubmission("请先在草稿区至少采纳一条客观事实。" );

        var authoritativeFacts = AllFacts(result);
        var authoritativeExpressions = AllExpressions(result);
        EnsureUniqueIds(authoritativeFacts.Select(item => item.Id), "AI 事实");
        EnsureUniqueIds(authoritativeExpressions.Select(item => item.Id), "AI 身份表达");
        var resultFacts = authoritativeFacts.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var resultExpressions = authoritativeExpressions.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var acceptedFacts = EnsureSubmittedFacts(facts, resultFacts);

        var expressions = submittedExpressions ?? [];
        var acceptedExpressions = EnsureSubmittedExpressions(expressions, resultExpressions, acceptedFacts, resultFacts);
        return new AcceptedSubmission(acceptedFacts, acceptedExpressions);
    }

    private static IReadOnlyList<AuthoringDraftFact> AllFacts(AuthoringDraftResult result)
        => result.Facts
            .Concat(result.CandidateSet?.Candidates.SelectMany(item => item.Facts) ?? [])
            .Concat(result.Candidates?.SelectMany(item => item.Facts) ?? [])
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

    private static IReadOnlyList<AuthoringDraftExpression> AllExpressions(AuthoringDraftResult result)
        => result.Expressions
            .Concat(result.CandidateSet?.Candidates.SelectMany(item => item.Expressions) ?? [])
            .Concat(result.Candidates?.SelectMany(item => item.Expressions) ?? [])
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

    private static IReadOnlyList<AuthoringDraftFact> EnsureSubmittedFacts(
        IReadOnlyList<AuthoringDraftFact> facts,
        IReadOnlyDictionary<string, AuthoringDraftFact> resultFacts)
    {
        EnsureUniqueIds(facts.Select(item => item.Id), "提交的事实");
        var accepted = new List<AuthoringDraftFact>(facts.Count);
        foreach (var fact in facts)
        {
            if (!string.Equals(fact.ReviewStatus, "accepted", StringComparison.Ordinal))
                throw InvalidSubmission("提交的事实必须先标记为已采纳。" );
            if (string.IsNullOrWhiteSpace(fact.Text))
                throw InvalidSubmission("提交的事实内容不能为空。" );
            if (!resultFacts.TryGetValue(fact.Id, out var generated))
                throw InvalidSubmission("提交的事实不属于当前 AI 草稿，请重新生成并采纳。" );

            EnsureEvidenceUnchanged(generated.Evidence, fact.Evidence, $"事实 {fact.Id}");
            EnsureEvidenceGroupUnchanged(generated.EvidenceGroup, fact.EvidenceGroup, $"事实 {fact.Id}");
            accepted.Add(generated with { Text = fact.Text.Trim(), ReviewStatus = "accepted" });
        }
        return accepted;
    }

    private static IReadOnlyList<AuthoringDraftExpression> EnsureSubmittedExpressions(
        IReadOnlyList<AuthoringDraftExpression> expressions,
        IReadOnlyDictionary<string, AuthoringDraftExpression> resultExpressions,
        IReadOnlyList<AuthoringDraftFact> acceptedFacts,
        IReadOnlyDictionary<string, AuthoringDraftFact> resultFacts)
    {
        var acceptedFactIds = EnsureUniqueIds(acceptedFacts.Select(item => item.Id), "采纳的事实");
        EnsureUniqueIds(expressions.Select(item => item.Id), "提交的身份表达");
        var accepted = new List<AuthoringDraftExpression>(expressions.Count);
        foreach (var expression in expressions)
        {
            if (!IsSubmittedReviewStatus(expression.ReviewStatus))
                throw InvalidSubmission("提交的身份表达必须先标记为已采纳。" );
            if (string.IsNullOrWhiteSpace(expression.Text))
                throw InvalidSubmission("提交的身份表达内容不能为空。" );
            if (!resultExpressions.TryGetValue(expression.Id, out var generated))
                throw InvalidSubmission("提交的身份表达不属于当前 AI 草稿，请重新生成并采纳。" );
            if (generated.FactIds.Any(factId => !resultFacts.ContainsKey(factId)))
                throw InvalidSubmission("AI 身份表达引用了结果中不存在的客观事实。" );
            if (generated.FactIds.Any(factId => !acceptedFactIds.Contains(factId)))
                throw InvalidSubmission("身份表达引用了尚未采纳的客观事实，请先采纳对应事实。" );
            if (generated.FactIds.Count != 1)
                throw InvalidSubmission("每条身份表达必须绑定一条客观事实；请重新生成后再采纳。" );
            EnsureListUnchanged(generated.FactIds, expression.FactIds, $"身份表达 {expression.Id} 引用的事实");
            EnsureListUnchanged(generated.ProfileIds, expression.ProfileIds, $"身份表达 {expression.Id} 适用身份");
            if (!string.Equals(generated.Perspective, expression.Perspective, StringComparison.Ordinal)
                || !string.Equals(generated.Layer, expression.Layer, StringComparison.Ordinal)
                || generated.Inferred != expression.Inferred)
                throw InvalidSubmission("身份表达的身份、表达层级和推断标记不能由客户端修改，请重新生成后再采纳。" );

            EnsureEvidenceUnchanged(generated.Evidence, expression.Evidence, $"身份表达 {expression.Id}");
            accepted.Add(generated with { Text = expression.Text.Trim(), ReviewStatus = "accepted" });
        }
        return accepted;
    }

    private static void EnsureEvidenceUnchanged(
        AuthoringDraftEvidence? generated,
        AuthoringDraftEvidence? submitted,
        string label)
    {
        if (generated is null && submitted is null) return;
        if (generated is null || submitted is null
            || !string.Equals(generated.ReferenceId, submitted.ReferenceId, StringComparison.Ordinal)
            || !string.Equals(generated.Locator, submitted.Locator, StringComparison.Ordinal)
            || !string.Equals(generated.Quote, submitted.Quote, StringComparison.Ordinal)
            || !string.Equals(generated.QuoteHash, submitted.QuoteHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                generated.LocatorObject is null ? null : CanonicalJson.Serialize(generated.LocatorObject),
                submitted.LocatorObject is null ? null : CanonicalJson.Serialize(submitted.LocatorObject),
                StringComparison.Ordinal))
            throw InvalidSubmission($"{label}的原文依据不能由客户端修改，请重新生成后再采纳。" );
    }

    private static void EnsureEvidenceGroupUnchanged(
        IReadOnlyList<AuthoringDraftEvidence>? generated,
        IReadOnlyList<AuthoringDraftEvidence>? submitted,
        string label)
    {
        var left = generated ?? [];
        var right = submitted ?? [];
        if (left.Count > 0 && right.Count == 0) return;
        if (left.Count != right.Count)
            throw InvalidSubmission($"{label}的证据集合不能由客户端修改，请重新生成后再采纳。" );
        for (var index = 0; index < left.Count; index++)
            EnsureEvidenceUnchanged(left[index], right[index], label);
    }

    private static void EnsureListUnchanged(
        IReadOnlyList<string> generated,
        IReadOnlyList<string> submitted,
        string label)
    {
        if (generated.Count != submitted.Count
            || generated.Where((value, index) => !string.Equals(value, submitted[index], StringComparison.Ordinal)).Any())
            throw InvalidSubmission($"{label}不能由客户端修改，请重新生成后再采纳。" );
    }

    private static IReadOnlySet<string> EnsureUniqueIds(IEnumerable<string> ids, string label)
    {
        var values = ids.ToArray();
        if (values.Any(string.IsNullOrWhiteSpace) || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw InvalidSubmission($"{label}包含重复或无效编号。" );
        return values.ToHashSet(StringComparer.Ordinal);
    }

    private static InvalidOperationException InvalidSubmission(string message)
        => new($"WB-AI-DRAFT-422: {message}");

    private static bool IsSubmittedReviewStatus(string? status)
        => status is "accepted" or "author_modified";
}
