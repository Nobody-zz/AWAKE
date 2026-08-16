using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Awake;

internal sealed class WorldbookService
{
    private readonly List<WorldbookRule> _rules;
    private readonly Dictionary<string, List<WorldbookRule>> _keywordIndex =
        new Dictionary<string, List<WorldbookRule>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<WorldbookRule>> _ngramIndex =
        new Dictionary<string, List<WorldbookRule>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WorldbookRule> _rulesById =
        new Dictionary<string, WorldbookRule>(StringComparer.Ordinal);
    private readonly Dictionary<string, WorldbookPersona> _personasByCharacterId =
        new Dictionary<string, WorldbookPersona>(StringComparer.Ordinal);
    private readonly List<WorldbookImportWarning> _warnings;

    internal WorldbookService(WorldbookDocument document)
    {
        _rules = document?.Rules ?? new List<WorldbookRule>();
        _warnings = document?.Warnings ?? new List<WorldbookImportWarning>();
        if (document?.Personas != null)
        {
            foreach (WorldbookPersona persona in document.Personas)
            {
                if (!string.IsNullOrWhiteSpace(persona.CharacterId))
                {
                    _personasByCharacterId[persona.CharacterId] = persona;
                }
            }
        }
        foreach (WorldbookRule rule in _rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id)) continue;
            _rulesById[rule.Id] = rule;
            foreach (string keyword in rule.Keywords)
            {
                if (string.IsNullOrWhiteSpace(keyword)) continue;
                AddToIndex(_keywordIndex, keyword, rule);
            }
            foreach (string ngram in rule.Ngrams)
            {
                if (string.IsNullOrWhiteSpace(ngram)) continue;
                AddToIndex(_ngramIndex, ngram, rule);
            }
        }
    }

    internal WorldbookQueryResult Query(WorldbookQuery query)
    {
        WorldbookQueryResult result = new WorldbookQueryResult();
        result.Warnings.AddRange(_warnings);
        if (query == null)
        {
            result.Errors.Add("worldbook.query_missing");
            return result;
        }

        AppendPersona(query, result);
        List<WorldbookRule> identityPool = new List<WorldbookRule>();
        foreach (WorldbookRule rule in _rules)
        {
            if (!PassesHardGate(rule, query)) continue;
            if (WhenMatches(rule.When, query)) identityPool.Add(rule);
        }

        List<WorldbookRule> keywordCandidates = new List<WorldbookRule>();
        HashSet<string> matchedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string playerText = query.PlayerText ?? string.Empty;
        foreach (string keyword in AllIndexKeys(_keywordIndex))
        {
            if (playerText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
            matchedKeywords.Add(keyword);
            foreach (WorldbookRule rule in _keywordIndex[keyword])
            {
                if (!PassesHardGate(rule, query) || !WhenMatches(rule.When, query)) continue;
                AddUnique(keywordCandidates, rule);
            }
        }

        HashSet<string> matchedNgrams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string ngram in AllIndexKeys(_ngramIndex))
        {
            if (playerText.IndexOf(ngram, StringComparison.OrdinalIgnoreCase) < 0) continue;
            matchedNgrams.Add(ngram);
            foreach (WorldbookRule rule in _ngramIndex[ngram])
            {
                if (!PassesHardGate(rule, query) || !WhenMatches(rule.When, query)) continue;
                AddUnique(keywordCandidates, rule);
            }
        }

        result.MatchedKeywords.AddRange(matchedKeywords);
        result.MatchedKeywords.AddRange(matchedNgrams);
        result.MatchMode = matchedKeywords.Count > 0 && matchedNgrams.Count > 0
            ? "mixed"
            : matchedKeywords.Count > 0
                ? "keyword"
                : matchedNgrams.Count > 0
                    ? "ngram"
                    : "identity";

        identityPool.Sort((a, b) =>
        {
            long scoreA = Score(a, query);
            long scoreB = Score(b, query);
            int compare = scoreB.CompareTo(scoreA);
            if (compare != 0) return compare;
            compare = b.Priority.CompareTo(a.Priority);
            if (compare != 0) return compare;
            return string.CompareOrdinal(a.Id, b.Id);
        });
        keywordCandidates.Sort((a, b) =>
        {
            long scoreA = Score(a, query);
            long scoreB = Score(b, query);
            int compare = scoreB.CompareTo(scoreA);
            if (compare != 0) return compare;
            compare = b.Priority.CompareTo(a.Priority);
            if (compare != 0) return compare;
            return string.CompareOrdinal(a.Id, b.Id);
        });

        int budget = query.MaximumBytes > 0 ? query.MaximumBytes : 4096;
        StringBuilder builder = new StringBuilder();
        HashSet<string> injectedIds = new HashSet<string>(StringComparer.Ordinal);
        AppendPersonaText(builder, result, budget);

        foreach (WorldbookRule rule in identityPool)
        {
            result.IdentityRules.Add(rule);
            if (!TryAppendRule(builder, rule, query, budget, injectedIds)) continue;
            result.HitIds.Add(rule.Id);
        }

        foreach (WorldbookRule rule in keywordCandidates)
        {
            if (injectedIds.Contains(rule.Id)) continue;
            result.TopicRules.Add(rule);
            if (!TryAppendRule(builder, rule, query, budget, injectedIds)) continue;
            result.HitIds.Add(rule.Id);
        }

        result.RetrievedText = builder.ToString().Trim();
        result.ByteBudget = Encoding.UTF8.GetByteCount(result.RetrievedText);
        return result;
    }

    private void AppendPersona(WorldbookQuery query, WorldbookQueryResult result)
    {
        WorldbookPersona persona = null;
        if (!string.IsNullOrWhiteSpace(query.CharacterId))
        {
            _personasByCharacterId.TryGetValue(query.CharacterId, out persona);
        }
        if (persona == null && !string.IsNullOrWhiteSpace(query.HeroId))
        {
            foreach (WorldbookPersona candidate in _personasByCharacterId.Values)
            {
                if (StringComparer.Ordinal.Equals(candidate.CharacterId, query.HeroId)
                    || StringComparer.Ordinal.Equals(candidate.CharacterId, "hero:" + query.HeroId)
                    || candidate.KnownNames.Contains(query.HeroId, StringComparer.OrdinalIgnoreCase))
                {
                    persona = candidate;
                    break;
                }
            }
        }
        if (persona == null) return;
        result.Personality = persona.Personality;
        result.Background = persona.Background;
    }

    private static void AppendPersonaText(StringBuilder builder, WorldbookQueryResult result, int maximumBytes)
    {
        StringBuilder persona = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(result.Personality))
        {
            persona.Append("【人格】").Append(result.Personality);
        }
        if (!string.IsNullOrWhiteSpace(result.Background))
        {
            if (persona.Length > 0) persona.Append('\n');
            persona.Append("【背景】").Append(result.Background);
        }
        if (persona.Length == 0) return;
        string text = persona.ToString();
        if (Encoding.UTF8.GetByteCount(text) > maximumBytes) return;
        builder.Append(text);
    }

    private static bool PassesHardGate(WorldbookRule rule, WorldbookQuery query)
    {
        string tier = rule.When.ContentTier;
        if (!string.IsNullOrWhiteSpace(tier)
            && !StringComparer.Ordinal.Equals(tier, query.ContentTier))
        {
            return false;
        }
        return true;
    }

    internal static bool WhenMatches(WorldbookWhen when, WorldbookQuery query)
    {
        if (when == null || query == null) return true;
        if (when.HeroIds.Count > 0 && !ContainsOrdinal(when.HeroIds, query.HeroId)) return false;
        if (when.CharacterIds.Count > 0 && !ContainsOrdinal(when.CharacterIds, query.CharacterId)) return false;
        if (when.Cultures.Count > 0 && !ContainsOrdinal(when.Cultures, query.CultureId)) return false;
        if (when.KingdomIds.Count > 0 && !ContainsOrdinal(when.KingdomIds, query.KingdomId)) return false;
        if (when.SettlementIds.Count > 0 && !ContainsOrdinal(when.SettlementIds, query.SettlementId)) return false;
        if (when.Roles.Count > 0 && !ContainsOrdinal(when.Roles, query.Role)) return false;
        if (when.IdentityIds.Count > 0 && !ContainsOrdinal(when.IdentityIds, query.IdentityId)) return false;
        if (when.IsFemale != null && when.IsFemale != query.IsFemale) return false;
        if (when.IsClanLeader != null && when.IsClanLeader != query.IsClanLeader) return false;
        if (when.MinAge != null && query.Age < when.MinAge) return false;
        if (when.MaxAge != null && query.Age > when.MaxAge) return false;
        if (when.SkillMin.Count > 0)
        {
            foreach (KeyValuePair<string, int> pair in when.SkillMin)
            {
                int current;
                if (!query.Skills.TryGetValue(pair.Key, out current) || current < pair.Value) return false;
            }
        }
        return true;
    }

    private static bool TryAppendRule(
        StringBuilder builder,
        WorldbookRule rule,
        WorldbookQuery query,
        int maximumBytes,
        HashSet<string> injectedIds)
    {
        if (injectedIds.Contains(rule.Id)) return false;
        string content = ResolveContent(rule, query);
        if (string.IsNullOrWhiteSpace(content)) return false;
        string line = "· [" + rule.Id + "] " + content;
        int current = Encoding.UTF8.GetByteCount(builder.ToString());
        int next = current + Encoding.UTF8.GetByteCount(line) + (builder.Length > 0 ? 1 : 0);
        if (next > maximumBytes)
        {
            return false;
        }
        if (builder.Length > 0) builder.Append('\n');
        builder.Append(line);
        injectedIds.Add(rule.Id);
        return true;
    }

    internal static string ResolveContent(WorldbookRule rule, WorldbookQuery query)
    {
        if (rule.Variants.Count == 0) return rule.Content;
        List<WorldbookVariant> matching = new List<WorldbookVariant>();
        foreach (WorldbookVariant variant in rule.Variants)
        {
            if (WhenMatches(variant.When, query)) matching.Add(variant);
        }
        if (matching.Count == 0) return rule.Content;
        if (StringComparer.Ordinal.Equals(rule.VariantSelection, "af-best"))
        {
            WorldbookVariant best = null;
            int bestScore = int.MinValue;
            int bestSkillSum = int.MinValue;
            foreach (WorldbookVariant variant in matching)
            {
                int score = WhenMatchScore(variant.When, query);
                int skillSum = SumSkillMin(variant.When);
                if (best == null || score > bestScore || (score == bestScore && skillSum > bestSkillSum))
                {
                    best = variant;
                    bestScore = score;
                    bestSkillSum = skillSum;
                }
            }
            return best?.Content ?? rule.Content;
        }
        if (StringComparer.Ordinal.Equals(rule.VariantSelection, "all"))
        {
            StringBuilder builder = new StringBuilder();
            foreach (WorldbookVariant variant in matching)
            {
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(variant.Content);
            }
            return builder.ToString();
        }
        matching.Sort((a, b) =>
        {
            int compare = b.Priority.CompareTo(a.Priority);
            if (compare != 0) return compare;
            return 0;
        });
        return matching[0].Content;
    }

    private static long Score(WorldbookRule rule, WorldbookQuery query)
    {
        long identity = WhenMatchScore(rule.When, query);
        long context = 0;
        foreach (string keyword in query.SceneKeywords)
        {
            if (ContainsIgnoreCase(rule.Context.SceneKeywords, keyword)) context += 200;
        }
        foreach (string mode in query.ContextModes)
        {
            if (ContainsIgnoreCase(rule.Context.ContextModes, mode)) context += 150;
        }
        long recall = CountHits(rule.Keywords, query.PlayerText) * 100
            + CountHits(rule.Ngrams, query.PlayerText) * 50;
        return identity * 1000 + context * 100 + recall * 10 + rule.Priority;
    }

    internal static int WhenMatchScore(WorldbookWhen when, WorldbookQuery query)
    {
        int score = 0;
        if (ContainsOrdinal(when.HeroIds, query.HeroId)) score += 1000;
        if (ContainsOrdinal(when.CharacterIds, query.CharacterId)) score += 900;
        if (ContainsOrdinal(when.IdentityIds, query.IdentityId)) score += 800;
        if (ContainsOrdinal(when.Cultures, query.CultureId)) score += 600;
        if (ContainsOrdinal(when.KingdomIds, query.KingdomId)) score += 500;
        if (ContainsOrdinal(when.SettlementIds, query.SettlementId)) score += 400;
        if (ContainsOrdinal(when.Roles, query.Role)) score += 300;
        if (when.IsFemale != null && when.IsFemale == query.IsFemale) score += 100;
        if (when.MinAge != null && query.Age >= when.MinAge) score += 100;
        if (when.MaxAge != null && query.Age <= when.MaxAge) score += 100;
        if (when.IsClanLeader != null && when.IsClanLeader == query.IsClanLeader) score += 100;
        if (MeetsSkillMin(when, query)) score += 100;
        return score;
    }

    private static int SumSkillMin(WorldbookWhen when)
    {
        int sum = 0;
        foreach (KeyValuePair<string, int> pair in when.SkillMin)
        {
            if (pair.Value > 0) sum += pair.Value;
        }
        return sum;
    }

    private static bool MeetsSkillMin(WorldbookWhen when, WorldbookQuery query)
    {
        foreach (KeyValuePair<string, int> pair in when.SkillMin)
        {
            int current;
            if (!query.Skills.TryGetValue(pair.Key, out current) || current < pair.Value) return false;
        }
        return true;
    }

    private static bool ContainsOrdinal(List<string> values, string value)
    {
        foreach (string item in values)
        {
            if (StringComparer.Ordinal.Equals(item, value)) return true;
        }
        return false;
    }

    private static bool ContainsIgnoreCase(List<string> values, string value)
    {
        foreach (string item in values)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(item, value)) return true;
        }
        return false;
    }

    private static int CountHits(List<string> values, string text)
    {
        int count = 0;
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value) && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                count++;
            }
        }
        return count;
    }

    private static void AddToIndex(Dictionary<string, List<WorldbookRule>> index, string key, WorldbookRule rule)
    {
        List<WorldbookRule> list;
        if (!index.TryGetValue(key, out list))
        {
            list = new List<WorldbookRule>();
            index[key] = list;
        }
        if (!list.Contains(rule)) list.Add(rule);
    }

    private static IEnumerable<string> AllIndexKeys(Dictionary<string, List<WorldbookRule>> index)
    {
        return index.Keys;
    }

    private static void AddUnique(List<WorldbookRule> list, WorldbookRule rule)
    {
        if (!list.Contains(rule)) list.Add(rule);
    }
}
