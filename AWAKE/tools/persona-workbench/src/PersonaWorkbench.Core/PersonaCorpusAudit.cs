using System.Text;

namespace PersonaWorkbench.Core;

/// <summary>
/// 全库产出体检：对一批 Persona 卡做**跨卡**与**卡内**的客观一致性检查。
///
/// 存在理由（2026-09-15）：
/// 门禁第五道 O5「跨卡印章」只扫 <c>core</c> 的 10-gram 与 <c>selfClaimExamples</c> 的开头 8 字，
/// **结构上扫不到 selfClaimRules / realSelfBehaviors** —— 而这两个字段恰恰最容易被批量模板化。
/// 实测：全库 76 卡跑 O5 全绿的同时，有 3 张卡的 realSelfBehaviors[0] 逐字相同；
/// 更早还有 52 张卡的 selfClaimRules[0] 与「自称“我”或“&lt;显示名&gt;”」逐字相同。
/// 本类补上这一维，并把「零信息句」这类可客观判定的缺陷一并纳入。
///
/// 判据全部是文本比对，不读标签登记表 —— 因此在登记表本身出问题时依然可用。
/// </summary>
public static class PersonaCorpusAudit
{
    /// <summary>跨卡认定为「印章 / 骨架」的最小张数（与门禁 O5 同门槛）。</summary>
    public const int MinimumReusedCards = 3;

    /// <summary>
    /// 骨架前缀的最短长度（归一后）。取 5 的实测依据：全库 76 卡里，4 字命中的两组
    /// （<c>有人问我</c> / <c>儿子问我</c>）都只是通用开场白，不是批量套模板的痕迹；
    /// 而真骨架都 ≥8 字（<c>称可汗为“蒙楚格”</c> 10 字、<c>独处时会检查武器</c> 8 字）。
    /// 取 5 能在不漏真骨架的前提下滤掉这两类噪声。
    /// </summary>
    private const int MinimumSkeletonLength = 5;

    public const string RuleTextReused = "corpus.text_reused";
    public const string RuleSkeletonPrefix = "corpus.skeleton_prefix";
    public const string RuleSelfReferenceOnly = "card.self_reference_only";
    public const string RuleRepeatedLine = "card.repeated_line";
    public const string RuleEditorSampleText = "card.editor_sample_text";

    /// <summary>
    /// 编辑器界面上作为「示例」出现的词。它们一旦出现在卡里，就说明有人把输入框的
    /// 提示文字当成了内容抄下来（卡里不该出现任何一个）。
    /// </summary>
    public static readonly IReadOnlyList<string> EditorSamplePhrases = new[]
    {
        "角色名",
        "对外只自称",
        "维持对外礼仪",
        "方宜"
    };

    private static readonly char[] ClauseSeparators = { '，', '；', ',', ';' };
    private static readonly char[] QuoteCharacters = { '"', '\'', '“', '”', '‘', '’', '「', '」', '『', '』' };

    public static PersonaCorpusAuditReport Analyze(IEnumerable<PersonaDocument>? documents)
    {
        List<PersonaDocument> cards = (documents ?? Enumerable.Empty<PersonaDocument>())
            .Where(document => document is not null)
            .ToList();

        List<PersonaCorpusAuditFinding> findings = new List<PersonaCorpusAuditFinding>();
        findings.AddRange(FindCrossCardTextReuse(cards));
        findings.AddRange(FindCrossCardSkeletonPrefixes(cards));
        foreach (PersonaDocument card in cards)
        {
            findings.AddRange(FindCardLocalIssues(card));
        }

        findings.Sort((left, right) =>
        {
            int bySeverity = string.CompareOrdinal(left.Severity, right.Severity);
            if (bySeverity != 0) return bySeverity;
            int byRule = string.CompareOrdinal(left.Rule, right.Rule);
            if (byRule != 0) return byRule;
            int byCount = right.Cards.Count.CompareTo(left.Cards.Count);
            if (byCount != 0) return byCount;
            return string.CompareOrdinal(left.Text, right.Text);
        });

        return new PersonaCorpusAuditReport
        {
            CardCount = cards.Count,
            Findings = findings.AsReadOnly()
        };
    }

    // ── 跨卡：整条文本复用 ────────────────────────────────────────────────────
    private static IEnumerable<PersonaCorpusAuditFinding> FindCrossCardTextReuse(IReadOnlyList<PersonaDocument> cards)
    {
        Dictionary<string, List<TextOccurrence>> index = IndexByNormalizedText(cards, wholeClauseOnly: false);
        foreach (KeyValuePair<string, List<TextOccurrence>> entry in OrderByReach(index))
        {
            List<string> distinctCards = DistinctCards(entry.Value);
            if (distinctCards.Count < MinimumReusedCards) continue;

            yield return new PersonaCorpusAuditFinding
            {
                Rule = RuleTextReused,
                Severity = PersonaCorpusAuditSeverity.Warning,
                Field = string.Join(" / ", entry.Value.Select(o => o.Field).Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal)),
                Cards = distinctCards,
                Text = entry.Value[0].Text,
                Message = string.Format(
                    "同一条文本在 {0} 张卡里逐字重复（归一显示名后仍是同一条）。跨卡重复多半是批量套模板留下的，"
                    + "要么删掉、要么改写成该角色特有；若它本来就是共享的世界事实，请把它收进世界设定而不是逐卡复述。",
                    distinctCards.Count)
            };
        }
    }

    // ── 跨卡：首分句骨架（尾巴各异，前缀相同）───────────────────────────────
    private static IEnumerable<PersonaCorpusAuditFinding> FindCrossCardSkeletonPrefixes(IReadOnlyList<PersonaDocument> cards)
    {
        Dictionary<string, List<TextOccurrence>> index = IndexByNormalizedText(cards, wholeClauseOnly: true);
        foreach (KeyValuePair<string, List<TextOccurrence>> entry in OrderByReach(index))
        {
            List<string> distinctCards = DistinctCards(entry.Value);
            if (distinctCards.Count < MinimumReusedCards) continue;

            // 整条都一样的情况交给 corpus.text_reused，不必重复报。
            bool everyOccurrenceIdentical = entry.Value
                .Select(o => NormalizeWhole(o.Text, o.DisplayName))
                .Distinct(StringComparer.Ordinal)
                .Count() == 1;
            if (everyOccurrenceIdentical) continue;

            yield return new PersonaCorpusAuditFinding
            {
                Rule = RuleSkeletonPrefix,
                Severity = PersonaCorpusAuditSeverity.Warning,
                Field = string.Join(" / ", entry.Value.Select(o => o.Field).Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal)),
                Cards = distinctCards,
                Text = entry.Value[0].Clause,
                Message = string.Format(
                    "{0} 张卡的这一条**开头一段完全相同**、只有后半句不同 —— 这是按槽位批量生成时漏改开头留下的骨架。"
                    + "按本项目口径，只有当这一段**能由卡内已有字段推出来**（例如「自称＝我／本名」）时才该删；"
                    + "推不出来的（人名带封号、家族名）本来就该留。要清就清开头那一句，别连后半句一起删。",
                    distinctCards.Count)
            };
        }
    }

    // ── 卡内 ─────────────────────────────────────────────────────────────────
    private static IEnumerable<PersonaCorpusAuditFinding> FindCardLocalIssues(PersonaDocument card)
    {
        string label = CardLabel(card);
        string displayName = (card.DisplayName ?? string.Empty).Trim();

        Dictionary<string, List<string>> seen = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach ((string field, string text) in Texts(card))
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            // 零信息句：这条除了「自称＝我／本名」之外没有任何内容，而 SELF_CLAIM_NAME 槽位已经承接了同一件事。
            if (field == FieldSelfClaimRules && IsSelfReferenceOnly(text, displayName))
            {
                yield return new PersonaCorpusAuditFinding
                {
                    Rule = RuleSelfReferenceOnly,
                    Severity = PersonaCorpusAuditSeverity.Error,
                    Field = field,
                    Cards = new[] { label },
                    Text = text,
                    Message = "这一条只是「自称我／本名」，卡里没有它推不出的信息（生成器的 SELF_CLAIM_NAME 槽位已经承接同一件事）。"
                        + "留着等于占位不干活，删掉；要写就写这条规则**额外**排除了什么（例如只自称我、不用本名）。"
                };
            }

            // 抄了编辑器界面上的示例文字。
            foreach (string phrase in EditorSamplePhrases)
            {
                if (!text.Contains(phrase, StringComparison.Ordinal)) continue;
                yield return new PersonaCorpusAuditFinding
                {
                    Rule = RuleEditorSampleText,
                    Severity = PersonaCorpusAuditSeverity.Error,
                    Field = field,
                    Cards = new[] { label },
                    Text = text,
                    Message = string.Format(
                        "这条里出现了编辑器界面的示例文字「{0}」。示例是给人看格式的，不是内容，逐字抄进来会被当成角色设定。",
                        phrase)
                };
                break;
            }

            // 同一张卡里两条一模一样。
            string key = NormalizeWhole(text, displayName);
            if (key.Length == 0) continue;
            if (!seen.TryGetValue(key, out List<string>? occurrences)) seen[key] = occurrences = new List<string>();
            occurrences.Add(field);
        }

        foreach (KeyValuePair<string, List<string>> entry in seen)
        {
            List<string> distinctFields = entry.Value.Distinct(StringComparer.Ordinal).ToList();
            if (entry.Value.Count < 2) continue;
            yield return new PersonaCorpusAuditFinding
            {
                Rule = RuleRepeatedLine,
                Severity = PersonaCorpusAuditSeverity.Error,
                Field = string.Join(" / ", distinctFields),
                Cards = new[] { label },
                Text = entry.Key,
                Message = "同一张卡里这条出现了 " + entry.Value.Count + " 次（完全相同）。重复计数不会让模型更当回事，只会占预算。"
            };
        }
    }

    // ── 工具 ─────────────────────────────────────────────────────────────────
    private const string FieldSelfClaimRules = "selfClaimRules";

    private static Dictionary<string, List<TextOccurrence>> IndexByNormalizedText(
        IReadOnlyList<PersonaDocument> cards,
        bool wholeClauseOnly)
    {
        Dictionary<string, List<TextOccurrence>> index = new Dictionary<string, List<TextOccurrence>>(StringComparer.Ordinal);
        foreach (PersonaDocument card in cards)
        {
            string displayName = (card.DisplayName ?? string.Empty).Trim();
            foreach ((string field, string text) in Texts(card))
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (wholeClauseOnly && !IsSkeletonCandidate(field)) continue;

                string candidate = wholeClauseOnly ? FirstClause(text) : text;
                string key = NormalizeWhole(candidate, displayName);
                if (key.Length == 0) continue;
                if (wholeClauseOnly && key.Length < MinimumSkeletonLength) continue;

                if (!index.TryGetValue(key, out List<TextOccurrence>? occurrences))
                {
                    index[key] = occurrences = new List<TextOccurrence>();
                }
                occurrences.Add(new TextOccurrence(CardLabel(card), displayName, field, text, candidate));
            }
        }

        return index;
    }

    private static IEnumerable<KeyValuePair<string, List<TextOccurrence>>> OrderByReach(
        Dictionary<string, List<TextOccurrence>> index)
    {
        return index
            .Where(entry => DistinctCards(entry.Value).Count >= MinimumReusedCards)
            .OrderByDescending(entry => DistinctCards(entry.Value).Count)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal);
    }

    /// <summary>骨架只看「逐条书写」的三个作者字段；core 之类是整段散文，不做首分句切分。</summary>
    private static bool IsSkeletonCandidate(string field)
    {
        return field is "selfClaimRules" or "realSelfBehaviors" or "selfClaimExamples";
    }

    private static string FirstClause(string text)
    {
        int cut = text.IndexOfAny(ClauseSeparators);
        return cut < 0 ? text : text[..cut];
    }

    private static List<string> DistinctCards(List<TextOccurrence> occurrences)
    {
        return occurrences
            .Select(o => o.Card)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(card => card, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>整条归一：显示名换 {N}、去空白、统一引号与全角标点。</summary>
    private static string NormalizeWhole(string text, string displayName)
    {
        string normalized = text.Replace("\r", string.Empty).Replace("\n", string.Empty);
        if (displayName.Length > 0)
        {
            normalized = normalized.Replace(displayName, PlaceholderName, StringComparison.Ordinal);
        }
        return CollapseWhitespaceAndQuotes(normalized);
    }

    private static string CollapseWhitespaceAndQuotes(string text)
    {
        StringBuilder builder = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (char.IsWhiteSpace(character)) continue;
            builder.Append(Array.IndexOf(QuoteCharacters, character) >= 0 ? '"' : character);
        }
        return builder.ToString().Trim();
    }

    private static bool IsSelfReferenceOnly(string text, string displayName)
    {
        string loose = StripQuotes(NormalizeWhole(text, displayName));
        string withoutName = PlaceholderName;
        return loose == "自称我或" + withoutName
            || loose == "自称我" + withoutName
            || loose == "自称" + withoutName;
    }

    private static string StripQuotes(string text)
    {
        StringBuilder builder = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (character == '"') continue;
            builder.Append(character);
        }
        return builder.ToString();
    }

    private static string CardLabel(PersonaDocument card)
    {
        string name = (card.DisplayName ?? string.Empty).Trim();
        return name.Length > 0 ? name : (string.IsNullOrWhiteSpace(card.Id) ? "(未命名)" : card.Id);
    }

    private static IEnumerable<(string Field, string Text)> Texts(PersonaDocument card)
    {
        const string fieldCore = "core";
        const string fieldSummary = "summary";
        const string fieldPublic = "publicDescription";
        const string fieldPrivate = "privateDescription";
        const string fieldContradiction = "contradictionDescription";
        const string fieldBehaviors = "realSelfBehaviors";
        const string fieldExamples = "selfClaimExamples";

        yield return (fieldCore, card.Core ?? string.Empty);
        yield return (fieldSummary, card.Summary ?? string.Empty);
        yield return (fieldPublic, card.PublicDescription ?? string.Empty);
        yield return (fieldPrivate, card.PrivateDescription ?? string.Empty);
        yield return (fieldContradiction, card.ContradictionDescription ?? string.Empty);

        foreach (string item in card.SelfClaimRules ?? new List<string>())
        {
            yield return (FieldSelfClaimRules, item ?? string.Empty);
        }
        foreach (string item in card.RealSelfBehaviors ?? new List<string>())
        {
            yield return (fieldBehaviors, item ?? string.Empty);
        }
        foreach (string item in card.SelfClaimExamples ?? new List<string>())
        {
            yield return (fieldExamples, item ?? string.Empty);
        }
    }

    private const string PlaceholderName = "{N}";

    private readonly record struct TextOccurrence(string Card, string DisplayName, string Field, string Text, string Clause);
}

public static class PersonaCorpusAuditSeverity
{
    public const string Error = "error";
    public const string Warning = "warning";
}

public sealed class PersonaCorpusAuditFinding
{
    public string Rule { get; init; } = string.Empty;
    public string Severity { get; init; } = PersonaCorpusAuditSeverity.Warning;
    public string Field { get; init; } = string.Empty;
    public IReadOnlyList<string> Cards { get; init; } = Array.Empty<string>();
    public string Text { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class PersonaCorpusAuditReport
{
    public int CardCount { get; init; }
    public IReadOnlyList<PersonaCorpusAuditFinding> Findings { get; init; } = Array.Empty<PersonaCorpusAuditFinding>();

    public int ErrorCount => Findings.Count(finding => finding.Severity == PersonaCorpusAuditSeverity.Error);
    public int WarningCount => Findings.Count(finding => finding.Severity == PersonaCorpusAuditSeverity.Warning);
    public bool IsClean => Findings.Count == 0;
}
