using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class WorldbookLoader
{
    internal static WorldbookDocument LoadDirectory(string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            throw new InvalidOperationException("worldbook manifest not found: " + (manifestPath ?? "null"));
        }
        JObject manifestToken;
        try
        {
            manifestToken = JObject.Parse(File.ReadAllText(manifestPath));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("worldbook manifest parse failed: " + ex.Message);
        }

        WorldbookManifest manifest = ParseManifest(manifestToken);
        WorldbookDocument document = new WorldbookDocument { Manifest = manifest };
        ValidateManifest(manifest, document.Warnings);
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? ".";
        string rulesDir = ResolvePath(baseDir, manifest.RulesDirectory);
        if (Directory.Exists(rulesDir))
        {
            foreach (string file in Directory.GetFiles(rulesDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                JToken token;
                try
                {
                    token = JToken.Parse(File.ReadAllText(file));
                }
                catch (Exception ex)
                {
                    document.Warnings.Add(new WorldbookImportWarning(
                        Path.GetFileName(file),
                        "rule_file_parse_failed",
                        ex.Message));
                    continue;
                }
                string fallback = Path.GetFileNameWithoutExtension(file);
                if (token is JArray array)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        WorldbookRule rule;
                        List<WorldbookImportWarning> warnings = new List<WorldbookImportWarning>();
                        if (array[i] is JObject item
                            && TryParseRule(item, fallback + "_" + i, manifest.SourceFormat, warnings, out rule))
                        {
                            document.Rules.Add(rule);
                        }
                        document.Warnings.AddRange(warnings);
                    }
                }
                else if (token is JObject obj)
                {
                    WorldbookRule rule;
                    List<WorldbookImportWarning> warnings = new List<WorldbookImportWarning>();
                    if (TryParseRule(obj, fallback, manifest.SourceFormat, warnings, out rule))
                    {
                        document.Rules.Add(rule);
                    }
                    document.Warnings.AddRange(warnings);
                }
            }
        }

        string personaDir = ResolvePath(baseDir, manifest.PersonaDirectory);
        if (Directory.Exists(personaDir))
        {
            foreach (string file in Directory.GetFiles(personaDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                JToken token;
                try
                {
                    token = JToken.Parse(File.ReadAllText(file));
                }
                catch (Exception ex)
                {
                    document.Warnings.Add(new WorldbookImportWarning(
                        Path.GetFileName(file),
                        "persona_file_parse_failed",
                        ex.Message));
                    continue;
                }
                string fallback = Path.GetFileNameWithoutExtension(file);
                if (token is JArray array)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        WorldbookPersona persona;
                        List<WorldbookImportWarning> warnings = new List<WorldbookImportWarning>();
                        if (array[i] is JObject item
                            && TryParsePersona(item, fallback + "_" + i, manifest.SourceFormat, warnings, out persona))
                        {
                            document.Personas.Add(persona);
                        }
                        document.Warnings.AddRange(warnings);
                    }
                }
                else if (token is JObject obj)
                {
                    WorldbookPersona persona;
                    List<WorldbookImportWarning> warnings = new List<WorldbookImportWarning>();
                    if (TryParsePersona(obj, fallback, manifest.SourceFormat, warnings, out persona))
                    {
                        document.Personas.Add(persona);
                    }
                    document.Warnings.AddRange(warnings);
                }
            }
        }
        LoadOptionalJsonFile(
            document,
            baseDir,
            manifest.UnnamedPersonaDirectory,
            "UnnamedNpcProfiles.json",
            "unnamed_persona_data",
            value => document.UnnamedPersonaData = value);
        LoadOptionalJsonFile(
            document,
            baseDir,
            manifest.VoiceMappingDirectory,
            "VoiceMapping.json",
            "voice_mapping_data",
            value => document.VoiceMappingData = value);
        LoadEventData(document, baseDir, manifest.EventDataDirectory);
        LoadFirstJsonInDirectory(
            document,
            baseDir,
            manifest.DebtDirectory,
            "debt_data",
            value => document.DebtData = value);
        LoadFirstJsonInDirectory(
            document,
            baseDir,
            manifest.DialogueHistoryDirectory,
            "dialogue_history_data",
            value => document.DialogueHistoryData = value);
        LoadFirstJsonInDirectory(
            document,
            baseDir,
            manifest.CompressedMemoryDirectory,
            "compressed_memory_data",
            value => document.CompressedMemoryData = value);
        EnforceUniqueIds(document);
        if (document.Rules.Count == 0)
        {
            document.Warnings.Add(new WorldbookImportWarning("manifest", "empty_rules", "世界书没有加载到任何规则。"));
        }
        return document;
    }

    internal static WorldbookManifest ParseManifest(JObject obj)
    {
        WorldbookManifest manifest = new WorldbookManifest
        {
            SchemaVersion = Str(obj, "schemaVersion", "SchemaVersion") ?? "awake.worldbook.v1",
            Id = Str(obj, "id", "Id") ?? string.Empty,
            SourceFormat = Str(obj, "sourceFormat", "SourceFormat") ?? "awake",
            RulesDirectory = Str(obj, "rulesDirectory", "RulesDirectory") ?? "rules",
            PersonaDirectory = Str(obj, "personaDirectory", "PersonaDirectory") ?? "personality_background",
            UnnamedPersonaDirectory = Str(obj, "unnamedPersonaDirectory", "UnnamedPersonaDirectory") ?? "unnamed_persona",
            VoiceMappingDirectory = Str(obj, "voiceMappingDirectory", "VoiceMappingDirectory") ?? "voice_mapping",
            EventDataDirectory = Str(obj, "eventDataDirectory", "EventDataDirectory") ?? "event_data",
            DebtDirectory = Str(obj, "debtDirectory", "DebtDirectory") ?? "debt",
            DialogueHistoryDirectory = Str(obj, "dialogueHistoryDirectory", "DialogueHistoryDirectory") ?? "dialogue_history",
            CompressedMemoryDirectory = Str(obj, "compressedMemoryDirectory", "CompressedMemoryDirectory") ?? "compressed_memory"
        };
        return manifest;
    }

    internal static bool TryParseRule(
        JObject obj,
        string fallbackId,
        string sourceFormat,
        List<WorldbookImportWarning> warnings,
        out WorldbookRule rule)
    {
        rule = null;
        if (obj == null) return false;
        string id = Str(obj, "id", "Id");
        if (string.IsNullOrWhiteSpace(id)) id = fallbackId;
        if (string.IsNullOrWhiteSpace(id)) return false;
        string content = Str(obj, "content", "Content") ?? string.Empty;
        List<WorldbookVariant> variants = ParseVariants(obj["variants"] as JArray ?? obj["Variants"] as JArray);
        if (string.IsNullOrWhiteSpace(content) && variants.Count == 0) return false;

        rule = new WorldbookRule
        {
            Id = id,
            Kind = Str(obj, "kind", "Kind") ?? "background",
            Scope = Str(obj, "scope", "Scope") ?? "npc",
            Persistence = Str(obj, "persistence", "Persistence") ?? "persistent",
            VariantSelection = StringComparer.Ordinal.Equals(sourceFormat, "af") ? "af-best" : "first",
            Priority = Int(obj, "priority", "Priority") ?? 0,
            When = ParseWhen(Obj(obj, "when", "When")),
            Context = ParseContext(Obj(obj, "context", "Context")),
            Keywords = StrList(obj, "keywords", "Keywords"),
            Ngrams = StrList(obj, "ngrams", "Ngrams"),
            RagShortTexts = StrList(obj, "ragShortTexts", "RagShortTexts"),
            SemanticPrototypes = StrList(obj, "semanticPrototypes", "SemanticPrototypes"),
            Content = content,
            Variants = variants,
            TextMappings = ParseTextMappings(obj["textMappings"] as JArray ?? obj["TextMappings"] as JArray),
            Raw = (JObject)obj.DeepClone()
        };
        ValidateRule(rule, warnings);
        if (StringComparer.Ordinal.Equals(sourceFormat, "af") && rule.TextMappings.Count > 0)
        {
            AddWarning(warnings, id, "text_mappings_preserved", "AF TextMappings 已保留原始数据，尚未实现动态替换。");
        }
        if (StringComparer.Ordinal.Equals(sourceFormat, "af") && rule.Variants.Count > 1)
        {
            AddWarning(warnings, id, "af_variant_semantics", "AF 多 Variants 按 AF 的 when 匹配语义解析，不使用 AWAKE priority/order 默认规则。");
        }
        return true;
    }

    internal static bool TryParsePersona(
        JObject obj,
        string fallbackCharacterId,
        string sourceFormat,
        List<WorldbookImportWarning> warnings,
        out WorldbookPersona persona)
    {
        persona = null;
        if (obj == null) return false;
        string characterId = Str(obj, "characterId", "CharacterId", "CharacterObjectId");
        if (string.IsNullOrWhiteSpace(characterId)) characterId = fallbackCharacterId;
        if (string.IsNullOrWhiteSpace(characterId)) return false;
        persona = new WorldbookPersona
        {
            CharacterId = characterId,
            Personality = Str(obj, "personality", "Personality") ?? string.Empty,
            Background = Str(obj, "background", "Background") ?? string.Empty,
            VoiceId = Str(obj, "voiceId", "VoiceId") ?? string.Empty,
            KnownNames = StrList(obj, "knownNames", "KnownNames", "Aliases", "aliases"),
            Raw = (JObject)obj.DeepClone()
        };
        if (StringComparer.Ordinal.Equals(sourceFormat, "af") && !string.IsNullOrWhiteSpace(persona.VoiceId))
        {
            AddWarning(warnings, characterId, "voice_id_preserved", "AF VoiceId 已保留，AWAKE 媒体层未实现前不会使用。");
        }
        return true;
    }

    private static WorldbookWhen ParseWhen(JObject obj)
    {
        WorldbookWhen when = new WorldbookWhen();
        if (obj == null) return when;
        when.HeroIds = StrList(obj, "heroIds", "HeroIds");
        when.CharacterIds = StrList(obj, "characterIds", "CharacterIds");
        when.Cultures = StrList(obj, "cultures", "Cultures");
        when.KingdomIds = StrList(obj, "kingdomIds", "KingdomIds");
        when.SettlementIds = StrList(obj, "settlementIds", "SettlementIds");
        when.Roles = StrList(obj, "roles", "Roles");
        when.IdentityIds = StrList(obj, "identityIds", "IdentityIds");
        when.IsFemale = Bool(obj, "isFemale", "IsFemale");
        when.IsClanLeader = Bool(obj, "isClanLeader", "IsClanLeader");
        when.MinAge = Int(obj, "minAge", "MinAge");
        when.MaxAge = Int(obj, "maxAge", "MaxAge");
        when.ContentTier = Str(obj, "contentTier", "ContentTier");
        JObject skills = Obj(obj, "skillMin", "SkillMin");
        if (skills != null)
        {
            foreach (JProperty property in skills.Properties())
            {
                int value;
                if (int.TryParse(property.Value?.ToString(), out value))
                {
                    when.SkillMin[property.Name] = value;
                }
            }
        }
        return when;
    }

    private static WorldbookContext ParseContext(JObject obj)
    {
        WorldbookContext context = new WorldbookContext();
        if (obj == null) return context;
        context.SceneKeywords = StrList(obj, "sceneKeywords", "SceneKeywords");
        context.ContextModes = StrList(obj, "contextModes", "ContextModes");
        return context;
    }

    private static List<WorldbookVariant> ParseVariants(JArray array)
    {
        List<WorldbookVariant> variants = new List<WorldbookVariant>();
        if (array == null) return variants;
        foreach (JToken token in array)
        {
            JObject item = token as JObject;
            if (item == null) continue;
            string content = Str(item, "content", "Content") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content)) continue;
            variants.Add(new WorldbookVariant
            {
                Priority = Int(item, "priority", "Priority") ?? 0,
                When = ParseWhen(Obj(item, "when", "When")),
                Content = content
            });
        }
        return variants;
    }

    private static List<WorldbookTextMapping> ParseTextMappings(JArray array)
    {
        List<WorldbookTextMapping> mappings = new List<WorldbookTextMapping>();
        if (array == null) return mappings;
        foreach (JToken token in array)
        {
            if (token is JObject obj)
            {
                mappings.Add(new WorldbookTextMapping
                {
                    SourceText = Str(obj, "sourceText", "SourceText") ?? string.Empty,
                    Kind = Str(obj, "kind", "Kind") ?? string.Empty,
                    TargetId = Str(obj, "targetId", "TargetId") ?? string.Empty,
                    AgeMin = Int(obj, "ageMin", "AgeMin"),
                    AgeMax = Int(obj, "ageMax", "AgeMax"),
                    EmptyValueText = Str(obj, "emptyValueText", "EmptyValueText") ?? string.Empty,
                    TrueText = Str(obj, "trueText", "TrueText") ?? string.Empty,
                    FalseText = Str(obj, "falseText", "FalseText") ?? string.Empty,
                    Raw = (JObject)obj.DeepClone()
                });
            }
        }
        return mappings;
    }

    private static void LoadOptionalJsonFile(
        WorldbookDocument document,
        string baseDir,
        string directory,
        string fileName,
        string warningCode,
        Action<JToken> assign)
    {
        string path = ResolvePath(baseDir, directory);
        if (!Directory.Exists(path)) return;
        string file = Path.Combine(path, fileName);
        if (!File.Exists(file)) return;
        try
        {
            assign(JToken.Parse(File.ReadAllText(file)));
        }
        catch (Exception ex)
        {
            document.Warnings.Add(new WorldbookImportWarning(fileName, warningCode, ex.Message));
        }
    }

    private static void LoadFirstJsonInDirectory(
        WorldbookDocument document,
        string baseDir,
        string directory,
        string warningCode,
        Action<JToken> assign)
    {
        string path = ResolvePath(baseDir, directory);
        if (!Directory.Exists(path)) return;
        string[] files = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly);
        if (files.Length == 0) return;
        try
        {
            assign(JToken.Parse(File.ReadAllText(files[0])));
        }
        catch (Exception ex)
        {
            document.Warnings.Add(new WorldbookImportWarning(Path.GetFileName(files[0]), warningCode, ex.Message));
        }
    }

    private static void LoadEventData(WorldbookDocument document, string baseDir, string directory)
    {
        string path = ResolvePath(baseDir, directory);
        if (!Directory.Exists(path)) return;
        Newtonsoft.Json.Linq.JObject eventData = new Newtonsoft.Json.Linq.JObject();
        LoadOptionalJsonFile(
            document,
            baseDir,
            directory,
            "EventRecords.json",
            "event_records_data",
            value => eventData["eventRecords"] = value);
        LoadOptionalJsonFile(
            document,
            baseDir,
            directory,
            "KingdomOpeningSummaries.json",
            "kingdom_opening_summaries_data",
            value => eventData["kingdomOpeningSummaries"] = value);
        LoadOptionalJsonFile(
            document,
            baseDir,
            directory,
            "WorldOpeningSummary.json",
            "world_opening_summary_data",
            value => eventData["worldOpeningSummary"] = value);
        if (eventData.Count > 0) document.EventData = eventData;
    }

    private static void ValidateManifest(WorldbookManifest manifest, List<WorldbookImportWarning> warnings)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            AddWarning(warnings, "manifest", "manifest_id_missing", "manifest 缺少 id。");
        }
        if (!StringComparer.Ordinal.Equals(manifest.SchemaVersion, "awake.worldbook.v1"))
        {
            AddWarning(warnings, manifest.Id, "manifest_schema_unsupported", "未知 schemaVersion: " + manifest.SchemaVersion);
        }
        if (!StringComparer.Ordinal.Equals(manifest.SourceFormat, "awake")
            && !StringComparer.Ordinal.Equals(manifest.SourceFormat, "af"))
        {
            AddWarning(warnings, manifest.Id, "manifest_source_format_unsupported", "未知 sourceFormat: " + manifest.SourceFormat);
        }
    }

    private static void ValidateRule(WorldbookRule rule, List<WorldbookImportWarning> warnings)
    {
        if (!IsAllowed(rule.Kind, "persona", "background", "world", "relationship", "scene"))
        {
            AddWarning(warnings, rule.Id, "rule_kind_unsupported", "未知 kind: " + rule.Kind);
        }
        if (!IsAllowed(rule.Scope, "global", "npc", "kingdom", "settlement", "culture"))
        {
            AddWarning(warnings, rule.Id, "rule_scope_unsupported", "未知 scope: " + rule.Scope);
        }
        if (!IsAllowed(rule.Persistence, "persistent", "contextual"))
        {
            AddWarning(warnings, rule.Id, "rule_persistence_unsupported", "未知 persistence: " + rule.Persistence);
        }
        string tier = rule.When.ContentTier;
        if (!string.IsNullOrWhiteSpace(tier) && !IsAllowed(tier, "pure", "standard", "intense"))
        {
            AddWarning(warnings, rule.Id, "rule_content_tier_unsupported", "未知 contentTier: " + tier);
        }
        if (rule.Priority < 0 || rule.Priority > 1000)
        {
            AddWarning(warnings, rule.Id, "rule_priority_clamped", "priority 越界，已限制到 0-1000。");
            rule.Priority = Math.Max(0, Math.Min(1000, rule.Priority));
        }
    }

    private static void EnforceUniqueIds(WorldbookDocument document)
    {
        HashSet<string> ruleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorldbookRule rule in document.Rules)
        {
            if (!ruleIds.Add(rule.Id))
            {
                document.Warnings.Add(new WorldbookImportWarning(rule.Id, "rule_id_duplicate", "规则 ID 重复。"));
            }
        }
        HashSet<string> personaIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorldbookPersona persona in document.Personas)
        {
            if (!personaIds.Add(persona.CharacterId))
            {
                document.Warnings.Add(new WorldbookImportWarning(persona.CharacterId, "persona_id_duplicate", "角色 ID 重复。"));
            }
        }
    }

    private static bool IsAllowed(string value, params string[] allowed)
    {
        foreach (string candidate in allowed)
        {
            if (StringComparer.Ordinal.Equals(value, candidate)) return true;
        }
        return false;
    }

    private static string ResolvePath(string baseDir, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return baseDir;
        return Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);
    }

    private static string Str(JObject obj, params string[] names)
    {
        foreach (string name in names)
        {
            JToken token = obj[name];
            if (token != null && token.Type == JTokenType.String)
            {
                return (string)token;
            }
        }
        return null;
    }

    private static List<string> StrList(JObject obj, params string[] names)
    {
        List<string> result = new List<string>();
        foreach (string name in names)
        {
            JArray array = obj[name] as JArray;
            if (array == null) continue;
            foreach (JToken token in array)
            {
                if (token != null && token.Type == JTokenType.String)
                {
                    string value = ((string)token).Trim();
                    if (value.Length > 0 && !result.Contains(value, StringComparer.Ordinal))
                    {
                        result.Add(value);
                    }
                }
            }
            break;
        }
        return result;
    }

    private static bool? Bool(JObject obj, params string[] names)
    {
        foreach (string name in names)
        {
            JToken token = obj[name];
            if (token != null && token.Type == JTokenType.Boolean)
            {
                return (bool)token;
            }
        }
        return null;
    }

    private static int? Int(JObject obj, params string[] names)
    {
        foreach (string name in names)
        {
            JToken token = obj[name];
            if (token != null && token.Type == JTokenType.Integer)
            {
                return (int)token;
            }
        }
        return null;
    }

    private static JObject Obj(JObject obj, params string[] names)
    {
        foreach (string name in names)
        {
            if (obj[name] is JObject value) return value;
        }
        return null;
    }

    private static void AddWarning(List<WorldbookImportWarning> warnings, string source, string code, string message)
    {
        warnings?.Add(new WorldbookImportWarning(source, code, message));
    }
}
