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
    private readonly List<PersonaDefinition> _personaDefinitions;
    private readonly PersonaTagRegistry _personaTagRegistry;
    private readonly Dictionary<string, PersonaGenerationResult> _personaCache = new Dictionary<string, PersonaGenerationResult>(StringComparer.Ordinal);

    internal WorldbookService(WorldbookDocument document)
    {
        _rules = document?.Rules ?? new List<WorldbookRule>();
        _warnings = document?.Warnings ?? new List<WorldbookImportWarning>();
        _personaDefinitions = document?.PersonaDefinitions ?? new List<PersonaDefinition>();
        _personaTagRegistry = new PersonaTagRegistry(document?.PersonaTagRegistry ?? new PersonaTagRegistryDocument());
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

    internal int RuleCount => _rules.Count;
    internal int PersonaCount => _personasByCharacterId.Count;
    internal int PersonaDefinitionCount => _personaDefinitions.Count;
    internal int PersonaTagCount => _personaTagRegistry.Count;
    internal int WarningCount => _warnings.Count;
    internal IReadOnlyList<WorldbookImportWarning> Warnings => _warnings;

    internal PersonaGenerationResult BuildPersona(WorldbookQuery query, WorldbookMappingContext mappingContext, int maximumBytes)
    {
        query = query ?? new WorldbookQuery();
        WorldbookPersona legacy = FindLegacyPersona(query);
        PersonaDefinition definition = SelectPersonaDefinition(query, out List<string> selectionWarnings);
        PersonaContext context = new PersonaContext
        {
            CharacterId = string.IsNullOrWhiteSpace(query.HeroId) ? query.CharacterId : query.HeroId,
            HeroName = mappingContext?.BoundHeroName ?? string.Empty,
            CultureId = query.CultureId,
            KingdomId = query.KingdomId,
            KingdomName = mappingContext?.BoundKingdomName ?? string.Empty,
            ClanName = mappingContext?.BoundClanName ?? string.Empty,
            Role = query.Role,
            SceneKeywords = query.SceneKeywords ?? new List<string>(),
            ContextModes = query.ContextModes ?? new List<string>()
        };
        string legacyPersonality = legacy?.Personality ?? string.Empty;
        string legacyBackground = legacy?.Background ?? string.Empty;
        string fingerprint = PersonaDslGenerator.ComputeFingerprint(definition, context, legacyPersonality, legacyBackground, maximumBytes);
        PersonaGenerationResult cached;
        if (_personaCache.TryGetValue(fingerprint, out cached)) return cached;
        PersonaGenerationResult generated = PersonaDslGenerator.Generate(
            definition,
            _personaTagRegistry,
            context,
            legacyPersonality,
            legacyBackground,
            maximumBytes);
        generated.Warnings.AddRange(selectionWarnings);
        _personaCache[fingerprint] = generated;
        return generated;
    }
    internal List<WorldbookRule> Search(string text, int limit)
    {
        List<WorldbookRule> result = new List<WorldbookRule>();
        if (string.IsNullOrWhiteSpace(text) || limit <= 0) return result;
        foreach (string keyword in AllIndexKeys(_keywordIndex))
        {
            if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
            foreach (WorldbookRule rule in _keywordIndex[keyword])
            {
                AddUnique(result, rule);
            }
        }
        foreach (string ngram in AllIndexKeys(_ngramIndex))
        {
            if (text.IndexOf(ngram, StringComparison.OrdinalIgnoreCase) < 0) continue;
            foreach (WorldbookRule rule in _ngramIndex[ngram])
            {
                AddUnique(result, rule);
            }
        }
        result.Sort((a, b) =>
        {
            int compare = b.Priority.CompareTo(a.Priority);
            if (compare != 0) return compare;
            return string.CompareOrdinal(a.Id, b.Id);
        });
        if (result.Count > limit) result.RemoveRange(limit, result.Count - limit);
        return result;
    }

    internal WorldbookQueryResult Query(WorldbookQuery query, WorldbookMappingContext mappingContext = null)
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
            if (WhenMatches(rule.When, query) && HasIdentityConstraints(rule.When))
            {
                identityPool.Add(rule);
            }
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

        foreach (WorldbookRule rule in _rules)
        {
            if (!PassesHardGate(rule, query) || !WhenMatches(rule.When, query)) continue;
            if (ContextMatches(rule.Context, query)) AddUnique(keywordCandidates, rule);
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
            if (!TryAppendRule(builder, rule, query, budget, injectedIds, mappingContext)) continue;
            result.HitIds.Add(rule.Id);
        }

        foreach (WorldbookRule rule in keywordCandidates)
        {
            if (injectedIds.Contains(rule.Id)) continue;
            result.TopicRules.Add(rule);
            if (!TryAppendRule(builder, rule, query, budget, injectedIds, mappingContext)) continue;
            result.HitIds.Add(rule.Id);
        }

        result.RetrievedText = builder.ToString().Trim();
        result.ByteBudget = Encoding.UTF8.GetByteCount(result.RetrievedText);
        return result;
    }

    private PersonaDefinition SelectPersonaDefinition(WorldbookQuery query, out List<string> warnings)
    {
        warnings = new List<string>();
        List<Tuple<int, PersonaDefinition>> candidates = new List<Tuple<int, PersonaDefinition>>();
        foreach (PersonaDefinition definition in _personaDefinitions)
        {
            if (definition == null || !StringComparer.Ordinal.Equals(definition.Status, PersonaSchemaConstants.StatusApproved)) continue;
            int score = PersonaMatchScore(definition, query);
            if (score >= 0) candidates.Add(Tuple.Create(score, definition));
        }
        candidates.Sort((left, right) =>
        {
            int score = right.Item1.CompareTo(left.Item1);
            if (score != 0) return score;
            int priority = right.Item2.Priority.CompareTo(left.Item2.Priority);
            return priority != 0 ? priority : string.CompareOrdinal(left.Item2.Id, right.Item2.Id);
        });
        if (candidates.Count == 0) return null;
        Tuple<int, PersonaDefinition> best = candidates[0];
        List<PersonaDefinition> sameRank = candidates
            .Where(item => item.Item1 == best.Item1 && item.Item2.Priority == best.Item2.Priority)
            .Select(item => item.Item2)
            .ToList();
        if (sameRank.Count > 1)
        {
            warnings.Add("persona.definition_conflict:" + string.Join(",", sameRank.Select(item => item.Id).OrderBy(item => item, StringComparer.Ordinal)));
            return null;
        }
        return best.Item2;
    }

    private static int PersonaMatchScore(PersonaDefinition definition, WorldbookQuery query)
    {
        bool hasCharacter = !string.IsNullOrWhiteSpace(definition.CharacterId);
        bool hasIdentity = !string.IsNullOrWhiteSpace(definition.IdentityId);
        bool hasRole = !string.IsNullOrWhiteSpace(definition.Role);
        if (hasCharacter)
        {
            if (!StringComparer.Ordinal.Equals(definition.CharacterId, query.HeroId)
                && !StringComparer.Ordinal.Equals(definition.CharacterId, query.CharacterId)) return -1;
            return 1000;
        }
        if (hasIdentity && !StringComparer.Ordinal.Equals(definition.IdentityId, query.IdentityId)) return -1;
        if (hasRole && !StringComparer.Ordinal.Equals(definition.Role, query.Role)) return -1;
        if (hasIdentity) return 700;
        if (hasRole) return 500;
        if (StringComparer.Ordinal.Equals(definition.Scope, "fallback")) return 100;
        if (!hasIdentity && !hasRole && StringComparer.Ordinal.Equals(definition.Scope, "global")) return 50;
        return -1;
    }

    private WorldbookPersona FindLegacyPersona(WorldbookQuery query)
    {
        WorldbookPersona persona = null;
        if (!string.IsNullOrWhiteSpace(query.CharacterId)) _personasByCharacterId.TryGetValue(query.CharacterId, out persona);
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
        return persona;
    }
    private void AppendPersona(WorldbookQuery query, WorldbookQueryResult result)
    {
        WorldbookPersona persona = FindLegacyPersona(query);
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

    private static bool HasIdentityConstraints(WorldbookWhen when)
    {
        if (when == null) return false;
        return when.HeroIds.Count > 0
            || when.CharacterIds.Count > 0
            || when.IdentityIds.Count > 0
            || when.Cultures.Count > 0
            || when.KingdomIds.Count > 0
            || when.SettlementIds.Count > 0
            || when.Roles.Count > 0
            || when.IsFemale != null
            || when.IsClanLeader != null
            || when.MinAge != null
            || when.MaxAge != null
            || when.SkillMin.Count > 0;
    }

    private static bool ContextMatches(WorldbookContext context, WorldbookQuery query)
    {
        if (context == null || query == null) return false;
        if (query.SceneKeywords != null)
        {
            foreach (string keyword in query.SceneKeywords)
            {
                if (ContainsIgnoreCase(context.SceneKeywords, keyword)) return true;
            }
        }
        if (query.ContextModes != null)
        {
            foreach (string mode in query.ContextModes)
            {
                if (ContainsIgnoreCase(context.ContextModes, mode)) return true;
            }
        }
        return false;
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
        HashSet<string> injectedIds,
        WorldbookMappingContext mappingContext)
    {
        if (injectedIds.Contains(rule.Id)) return false;
        string content = ResolveContent(rule, query, mappingContext);
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

    internal static string ResolveContent(
        WorldbookRule rule,
        WorldbookQuery query,
        WorldbookMappingContext mappingContext = null)
    {
        string content = rule.Variants.Count == 0 ? rule.Content : ResolveVariantContent(rule, query);
        return WorldbookTextMappingResolver.Apply(content, rule.TextMappings, mappingContext);
    }

    private static string ResolveVariantContent(WorldbookRule rule, WorldbookQuery query)
    {
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
