using System.Text;
using System.Text.RegularExpressions;

namespace PersonaWorkbench.Core;

public static class PersonaSemanticEnricher
{
    public static void MergeEvidenceBackedAxes(PersonaDocument target, string? sourceText, PersonaTagRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(registry);
        string source = (sourceText ?? string.Empty).Trim();
        if (source.Length == 0 || string.IsNullOrWhiteSpace(target.Id)) return;

        PersonaDocument heuristic;
        try
        {
            heuristic = PersonaIntermediateCandidateParser.CreateHeuristicDocument(source, target.Id, target.DisplayName, registry);
        }
        catch (ArgumentException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        target.TraitProfile.Caution ??= heuristic.TraitProfile.Caution;
        target.TraitProfile.Ambition ??= heuristic.TraitProfile.Ambition;
        target.TraitProfile.Pride ??= heuristic.TraitProfile.Pride;
        target.TraitProfile.Pragmatism ??= heuristic.TraitProfile.Pragmatism;
        target.TraitProfile.InGroupLoyalty ??= heuristic.TraitProfile.InGroupLoyalty;
        target.TraitProfile.Tradition ??= heuristic.TraitProfile.Tradition;

        target.ExpressionProfile.Restraint ??= heuristic.ExpressionProfile.Restraint;
        target.ExpressionProfile.Directness ??= heuristic.ExpressionProfile.Directness;
        target.ExpressionProfile.Formality ??= heuristic.ExpressionProfile.Formality;
        target.ExpressionProfile.Playfulness ??= heuristic.ExpressionProfile.Playfulness;
        target.ExpressionProfile.Warmth ??= heuristic.ExpressionProfile.Warmth;

        target.BehaviorProfile.Conditionality ??= heuristic.BehaviorProfile.Conditionality;
        target.BehaviorProfile.Deliberation ??= heuristic.BehaviorProfile.Deliberation;
        target.BehaviorProfile.TrustTesting ??= heuristic.BehaviorProfile.TrustTesting;
        target.BehaviorProfile.Leverage ??= heuristic.BehaviorProfile.Leverage;
        target.BehaviorProfile.InGroupPriority ??= heuristic.BehaviorProfile.InGroupPriority;
        target.BehaviorProfile.Leadership ??= heuristic.BehaviorProfile.Leadership;

        target.ReactionProfile.Confrontation ??= heuristic.ReactionProfile.Confrontation;
        target.ReactionProfile.Expression ??= heuristic.ReactionProfile.Expression;
        target.ReactionProfile.Timing ??= heuristic.ReactionProfile.Timing;
        target.ReactionProfile.Resentment ??= heuristic.ReactionProfile.Resentment;
        target.ReactionProfile.SupportSeeking ??= heuristic.ReactionProfile.SupportSeeking;

        target.CommitmentProfile.PromiseCaution ??= heuristic.CommitmentProfile.PromiseCaution;
        target.CommitmentProfile.PromisePersistence ??= heuristic.CommitmentProfile.PromisePersistence;
        target.CommitmentProfile.ValueTradeability ??= heuristic.CommitmentProfile.ValueTradeability;

        foreach (string tag in heuristic.Tags)
        {
            if (!target.Tags.Contains(tag, StringComparer.Ordinal)) target.Tags.Add(tag);
        }

        MergeEvidenceBackedReaction(target, source);
        MergeEvidenceBackedCommitment(target, source, registry);
    }

    private static void MergeEvidenceBackedReaction(PersonaDocument target, string source)
    {
        if (!string.IsNullOrWhiteSpace(target.ReactionProfile.SensitiveConditions)
            && !string.IsNullOrWhiteSpace(target.ReactionProfile.ConditionalResponses)) return;

        foreach (string sentence in ExtractSentences(source))
        {
            if (!ContainsAny(sentence, "弱点|如果|要是|受到|遭到|触发|诱发|敏感|易被|失去反抗|表现服从")) continue;
            int responseIndex = FindResponseIndex(sentence);
            if (responseIndex <= 0) continue;

            string condition = sentence[..responseIndex].Trim(' ', '，', '；', '：', ':');
            string response = sentence[responseIndex..].Trim();
            if (Encoding.UTF8.GetByteCount(condition) < 4 || Encoding.UTF8.GetByteCount(response) < 4) continue;

            if (string.IsNullOrWhiteSpace(target.ReactionProfile.SensitiveConditions))
            {
                target.ReactionProfile.SensitiveConditions = LimitEvidence(condition);
            }
            if (string.IsNullOrWhiteSpace(target.ReactionProfile.ConditionalResponses))
            {
                target.ReactionProfile.ConditionalResponses = LimitEvidence(response);
            }
            return;
        }
    }

    private static void MergeEvidenceBackedCommitment(PersonaDocument target, string source, PersonaTagRegistry registry)
    {
        foreach (string sentence in ExtractSentences(source))
        {
            if (!ContainsAny(sentence, "为了|犒赏|交换|回报|筹码|承诺|许诺|答应|空头")) continue;

            if (ContainsAny(sentence, "不轻易承诺|不轻易许诺|不随便答应|拒绝承诺|空头承诺|空头许诺"))
            {
                target.CommitmentProfile.PromiseCaution ??= 2;
                AddTag(target, registry, "boundary.no_empty_promises");
            }

            if (!ContainsAny(sentence, "为了|犒赏|交换|回报|筹码")) continue;

            target.BehaviorProfile.Conditionality ??= 1;
            target.CommitmentProfile.ValueTradeability ??= -1;
            string purpose = ExtractPurpose(sentence);
            string protectedValue = ExtractProtectedValue(sentence);
            string exchangeTarget = ExtractExchangeTarget(sentence);
            if (string.IsNullOrWhiteSpace(target.CommitmentProfile.PriorityOrder) && purpose.Length > 0)
            {
                target.CommitmentProfile.PriorityOrder = LimitEvidence(purpose);
            }
            if (string.IsNullOrWhiteSpace(target.CommitmentProfile.ProtectedValues) && protectedValue.Length > 0)
            {
                target.CommitmentProfile.ProtectedValues = LimitEvidence(protectedValue);
            }
            if (string.IsNullOrWhiteSpace(target.CommitmentProfile.ApplicableScope) && exchangeTarget.Length > 0)
            {
                target.CommitmentProfile.ApplicableScope = LimitEvidence(exchangeTarget);
            }
            AddTag(target, registry, "behavior.bargains");
            return;
        }
    }

    private static void AddTag(PersonaDocument target, PersonaTagRegistry registry, string tag)
    {
        if (registry.TryGet(tag, out _) && !target.Tags.Contains(tag, StringComparer.Ordinal)) target.Tags.Add(tag);
    }

    private static string ExtractPurpose(string sentence)
    {
        int purposeIndex = sentence.IndexOf("为了", StringComparison.Ordinal);
        if (purposeIndex >= 0)
        {
            int start = purposeIndex + 2;
            int end = sentence.Length;
            foreach (string marker in new[] { "会用", "用", "来", "便", "则" })
            {
                int markerIndex = sentence.IndexOf(marker, start, StringComparison.Ordinal);
                if (markerIndex >= 0 && markerIndex < end) end = markerIndex;
            }
            return sentence[start..end].Trim(' ', '，', '；', '：', ':');
        }

        int maintenanceIndex = sentence.IndexOf("以维持", StringComparison.Ordinal);
        if (maintenanceIndex >= 0) return sentence[(maintenanceIndex + 1)..].Trim(' ', '，', '；', '：', ':');
        return string.Empty;
    }

    private static string ExtractProtectedValue(string sentence)
    {
        foreach (string marker in new[] { "维持", "运作", "保护", "激励" })
        {
            int markerIndex = sentence.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0) continue;
            int start = markerIndex + marker.Length;
            int end = sentence.Length;
            foreach (char delimiter in new[] { '，', '。', '；', ',', ';' })
            {
                int delimiterIndex = sentence.IndexOf(delimiter, start);
                if (delimiterIndex >= 0 && delimiterIndex < end) end = delimiterIndex;
            }
            string value = sentence[start..end].Trim(' ', '，', '。', '；', ',', ';', '：', ':');
            if (value.Length > 0) return value;
        }
        return string.Empty;
    }

    private static string ExtractExchangeTarget(string sentence)
    {
        Match targetBeforeVerb = Regex.Match(sentence, "对(?<target>[^，。；]*?)(?:进行)?[“\\\"']?犒赏", RegexOptions.CultureInvariant);
        if (targetBeforeVerb.Success) return targetBeforeVerb.Groups["target"].Value.Trim();

        int rewardIndex = sentence.IndexOf("犒赏", StringComparison.Ordinal);
        if (rewardIndex < 0) return string.Empty;
        int start = rewardIndex + 2;
        while (start < sentence.Length && "“\"'「『”\"'".Contains(sentence[start])) start++;
        int end = sentence.Length;
        foreach (string marker in new[] { "，", "。", "；", "以", "来" })
        {
            int markerIndex = sentence.IndexOf(marker, start, StringComparison.Ordinal);
            if (markerIndex >= 0 && markerIndex < end) end = markerIndex;
        }
        return sentence[start..end].Trim(' ', '，', '。', '；', ',', ';', '”', '”', '"', '\'');
    }

    private static int FindResponseIndex(string sentence)
    {
        int responseIndex = -1;
        foreach (string marker in new[] { "就会", "便会", "将会", "导致", "会", "诱发", "失去", "表现服从" })
        {
            int candidate = sentence.IndexOf(marker, StringComparison.Ordinal);
            if (candidate > 0 && (responseIndex < 0 || candidate < responseIndex)) responseIndex = candidate;
        }
        return responseIndex;
    }

    private static IReadOnlyList<string> ExtractSentences(string source)
    {
        return Regex.Matches(source, @"[^。！？!?\r\n]+[。！？!?]?")
            .Select(match => match.Value.Trim())
            .Where(sentence => sentence.Length > 0)
            .ToArray();
    }

    private static string LimitEvidence(string value)
    {
        const int maximumBytes = 2048;
        if (Encoding.UTF8.GetByteCount(value) <= maximumBytes) return value;
        StringBuilder builder = new StringBuilder();
        foreach (char character in value)
        {
            int nextBytes = Encoding.UTF8.GetByteCount(builder.ToString() + character);
            if (nextBytes > maximumBytes) break;
            builder.Append(character);
        }
        return builder.ToString();
    }

    private static bool ContainsAny(string value, string terms)
    {
        return terms.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
