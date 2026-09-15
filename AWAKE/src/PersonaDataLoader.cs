using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class PersonaDataLoader
{
    internal static void Load(WorldbookDocument document, string baseDir)
    {
        if (document == null || document.Manifest == null) return;
        string registryPath = ResolvePath(baseDir, document.Manifest.PersonaTagRegistryFile);
        if (File.Exists(registryPath))
        {
            try
            {
                document.PersonaTagRegistry = ParseRegistry(JObject.Parse(File.ReadAllText(registryPath)), registryPath, document.Warnings);
            }
            catch (Exception ex)
            {
                document.Warnings.Add(new WorldbookImportWarning(registryPath, "persona_registry_parse_failed", ex.Message));
            }
        }
        else
        {
            document.Warnings.Add(new WorldbookImportWarning(registryPath, "persona_registry_missing", "人格标签注册表不存在，Persona 将回退旧人格文本。"));
        }

        string definitionDir = ResolvePath(baseDir, document.Manifest.PersonaDefinitionDirectory);
        if (!Directory.Exists(definitionDir)) return;
        foreach (string file in Directory.GetFiles(definitionDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                JToken token = JToken.Parse(File.ReadAllText(file));
                if (token is JArray array)
                {
                    for (int index = 0; index < array.Count; index++)
                    {
                        PersonaDefinition definition;
                        if (array[index] is JObject item && TryParseDefinition(item, Path.GetFileNameWithoutExtension(file) + "_" + index, file, out definition))
                        {
                            document.PersonaDefinitions.Add(definition);
                        }
                    }
                }
                else if (token is JObject obj)
                {
                    PersonaDefinition definition;
                    if (TryParseDefinition(obj, Path.GetFileNameWithoutExtension(file), file, out definition))
                    {
                        document.PersonaDefinitions.Add(definition);
                    }
                }
            }
            catch (Exception ex)
            {
                document.Warnings.Add(new WorldbookImportWarning(file, "persona_definition_parse_failed", ex.Message));
            }
        }
        Validate(document);
    }

    internal static PersonaTagRegistryDocument ParseRegistry(JObject obj, string source, List<WorldbookImportWarning> warnings)
    {
        PersonaTagRegistryDocument document = new PersonaTagRegistryDocument
        {
            SchemaVersion = StringValue(obj, "schemaVersion", "SchemaVersion") ?? PersonaSchemaConstants.RegistrySchema
        };
        JArray tags = ArrayValue(obj, "tags", "Tags");
        foreach (JToken token in tags ?? new JArray())
        {
            JObject item = token as JObject;
            if (item == null) continue;
            PersonaTagDefinition tag = new PersonaTagDefinition
            {
                Id = StringValue(item, "id", "Id") ?? string.Empty,
                Category = StringValue(item, "category", "Category") ?? string.Empty,
                DisplayName = StringValue(item, "displayName", "DisplayName") ?? string.Empty,
                Meaning = StringValue(item, "meaning", "Meaning") ?? string.Empty,
                PromptText = StringValue(item, "promptText", "PromptText") ?? string.Empty,
                Conflicts = StringList(item, "conflicts", "Conflicts"),
                Raw = (JObject)item.DeepClone()
            };
            if (string.IsNullOrWhiteSpace(tag.Id) || !PersonaTagCategories.IsKnown(tag.Category))
            {
                warnings.Add(new WorldbookImportWarning(source, "persona_tag_invalid", tag.Id));
                continue;
            }
            document.Tags.Add(tag);
        }
        JArray bundles = ArrayValue(obj, "bundles", "Bundles");
        foreach (JToken token in bundles ?? new JArray())
        {
            JObject item = token as JObject;
            if (item == null) continue;
            PersonaBundleDefinition bundle = new PersonaBundleDefinition
            {
                Id = StringValue(item, "id", "Id") ?? string.Empty,
                DisplayName = StringValue(item, "displayName", "DisplayName") ?? string.Empty,
                Tags = StringList(item, "tags", "Tags"),
                Raw = (JObject)item.DeepClone()
            };
            if (string.IsNullOrWhiteSpace(bundle.Id))
            {
                warnings.Add(new WorldbookImportWarning(source, "persona_bundle_invalid", "bundle id missing"));
                continue;
            }
            document.Bundles.Add(bundle);
        }
        return document;
    }

    internal static bool TryParseDefinition(JObject obj, string fallbackId, string sourcePath, out PersonaDefinition definition)
    {
        definition = null;
        if (obj == null) return false;
        definition = new PersonaDefinition
        {
            SchemaVersion = StringValue(obj, "schemaVersion", "SchemaVersion") ?? PersonaSchemaConstants.DefinitionSchema,
            Id = StringValue(obj, "id", "Id") ?? fallbackId,
            CharacterId = StringValue(obj, "characterId", "CharacterId") ?? string.Empty,
            IdentityId = StringValue(obj, "identityId", "IdentityId") ?? string.Empty,
            Role = StringValue(obj, "role", "Role") ?? string.Empty,
            SourcePackId = StringValue(obj, "sourcePackId", "SourcePackId") ?? string.Empty,
            TemplateVersion = StringValue(obj, "templateVersion", "TemplateVersion") ?? "1",
            Status = StringValue(obj, "status", "Status") ?? PersonaSchemaConstants.StatusDraft,
            Priority = IntValue(obj, "priority", "Priority"),
            Scope = StringValue(obj, "scope", "Scope") ?? "character",
            Core = StringValue(obj, "core", "Core") ?? string.Empty,
            IdentityFacts = StringValue(obj, "identityFacts", "IdentityFacts") ?? string.Empty,
            RelationStyle = StringValue(obj, "relationStyle", "RelationStyle") ?? string.Empty,
            CurrentStateHints = StringValue(obj, "currentStateHints", "CurrentStateHints") ?? string.Empty,
                        Summary = StringValue(obj, "summary", "Summary") ?? string.Empty,
            PublicDescription = StringValue(obj, "publicDescription", "PublicDescription") ?? string.Empty,
            PrivateDescription = StringValue(obj, "privateDescription", "PrivateDescription") ?? string.Empty,
            ContradictionDescription = StringValue(obj, "contradictionDescription", "ContradictionDescription") ?? string.Empty,
            SelfClaimRules = StringList(obj, "selfClaimRules", "SelfClaimRules"),
            RealSelfBehaviors = StringList(obj, "realSelfBehaviors", "RealSelfBehaviors"),
            SelfClaimExamples = StringList(obj, "selfClaimExamples", "SelfClaimExamples"),
            Bundles = StringList(obj, "bundles", "Bundles"),
            Experiences = ParseExperiences(ArrayValue(obj, "experiences", "Experiences")),
            Raw = (JObject)obj.DeepClone(),
            SourcePath = sourcePath ?? string.Empty
        };
        foreach (JToken token in ArrayValue(obj, "tags", "Tags") ?? new JArray())
        {
            JObject item = token as JObject;
            if (item == null) continue;
            definition.Tags.Add(new PersonaTagUse
            {
                Id = StringValue(item, "id", "Id") ?? string.Empty,
                Priority = IntValue(item, "priority", "Priority"),
                SceneKeywords = StringList(item, "sceneKeywords", "SceneKeywords"),
                ContextModes = StringList(item, "contextModes", "ContextModes")
            });
        }
        return !string.IsNullOrWhiteSpace(definition.Id);
    }

    private static List<PersonaExperience> ParseExperiences(JArray array)
    {
        List<PersonaExperience> result = new List<PersonaExperience>();
        foreach (JToken token in array ?? new JArray())
        {
            JObject item = token as JObject;
            if (item == null) continue;
            result.Add(new PersonaExperience
            {
                Id = StringValue(item, "id", "Id") ?? string.Empty,
                Text = StringValue(item, "text", "Text") ?? string.Empty,
                Status = StringValue(item, "status", "Status") ?? "active",
                Source = StringValue(item, "source", "Source") ?? "content",
                Priority = IntValue(item, "priority", "Priority")
            });
        }
        return result;
    }

    private static void Validate(WorldbookDocument document)
    {
        HashSet<string> tagIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (PersonaTagDefinition tag in document.PersonaTagRegistry.Tags)
        {
            if (!tagIds.Add(tag.Id)) document.Warnings.Add(new WorldbookImportWarning(tag.Id, "persona_tag_duplicate", "人格标签 ID 重复。"));
        }
        HashSet<string> definitionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (PersonaDefinition definition in document.PersonaDefinitions)
        {
            if (!definitionIds.Add(definition.Id)) document.Warnings.Add(new WorldbookImportWarning(definition.Id, "persona_definition_duplicate", "人格模板 ID 重复。"));
            if (!StringComparer.Ordinal.Equals(definition.SchemaVersion, PersonaSchemaConstants.DefinitionSchema)) document.Warnings.Add(new WorldbookImportWarning(definition.SourcePath, "persona_schema_unsupported", definition.SchemaVersion));
            if (!StringComparer.Ordinal.Equals(definition.Status, PersonaSchemaConstants.StatusApproved)
                && !StringComparer.Ordinal.Equals(definition.Status, PersonaSchemaConstants.StatusDraft)
                && !StringComparer.Ordinal.Equals(definition.Status, PersonaSchemaConstants.StatusDisabled)) document.Warnings.Add(new WorldbookImportWarning(definition.SourcePath, "persona_status_unknown", definition.Status));
        }
    }

    private static string ResolvePath(string baseDir, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return baseDir ?? ".";
        return Path.GetFullPath(Path.Combine(baseDir ?? ".", relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string StringValue(JObject obj, params string[] names)
    {
        foreach (string name in names)
        {
            JToken token = obj?[name];
            if (token == null || token.Type == JTokenType.Null) continue;
            return token.ToString();
        }
        return null;
    }

    private static int IntValue(JObject obj, params string[] names)
    {
        string value = StringValue(obj, names);
        int parsed;
        return int.TryParse(value, out parsed) ? parsed : 0;
    }

    private static JArray ArrayValue(JObject obj, params string[] names)
    {
        foreach (string name in names)
        {
            JArray value = obj?[name] as JArray;
            if (value != null) return value;
        }
        return null;
    }

    private static List<string> StringList(JObject obj, params string[] names)
    {
        List<string> result = new List<string>();
        JArray array = ArrayValue(obj, names);
        if (array == null) return result;
        foreach (JToken token in array)
        {
            string value = token?.ToString();
            if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
        }
        return result;
    }
}
