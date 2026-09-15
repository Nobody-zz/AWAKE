using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonaWorkbench.Core;

public sealed class PersonaDocumentFormatException : Exception
{
    public PersonaDocumentFormatException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

public static class PersonaDocumentCodec
{
    public const int MaximumUtf8Bytes = 64 * 1024;

    private static readonly HashSet<string> AllowedProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "schemaVersion",
        "id",
        "displayName",
        "core",
        "identityFacts",
        "summary",
        "sourcePackId",
        "templateVersion",
        "status",
        "sourceDescription",
        "publicDescription",
        "privateDescription",
        "contradictionDescription",
        "selfClaimRules",
        "realSelfBehaviors",
        "selfClaimExamples",
        "tensionAxes",
        "tags",
        "facetStrengths",
        "traitProfile",
        "expressionProfile",
        "behaviorProfile",
        "reactionProfile",
        "commitmentProfile"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public static string Serialize(PersonaDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        string json = JsonSerializer.Serialize(document, SerializerOptions);
        EnsureWithinLimit(json);
        return json;
    }

    public static PersonaDocument Deserialize(string json, PersonaTagRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(registry);
        EnsureWithinLimit(json);

        try
        {
            using JsonDocument parsed = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            });
            ValidateRootShape(parsed.RootElement);

            PersonaDocument? document = JsonSerializer.Deserialize<PersonaDocument>(json, SerializerOptions);
            if (document == null) throw new PersonaDocumentFormatException("persona.document_invalid", "Persona document is empty.");
            if (document.Tags == null) throw new PersonaDocumentFormatException("persona.document_invalid", "Tags must be an array.");
            if (document.FacetStrengths == null) throw new PersonaDocumentFormatException("persona.document_invalid", "Facet strengths must be an object.");
            if (document.TraitProfile == null) throw new PersonaDocumentFormatException("persona.document_invalid", "Trait profile must be an object.");
            if (document.ExpressionProfile == null) throw new PersonaDocumentFormatException("persona.document_invalid", "Expression profile must be an object.");
            if (document.BehaviorProfile == null) throw new PersonaDocumentFormatException("persona.document_invalid", "Behavior profile must be an object.");
            if (document.ReactionProfile == null) throw new PersonaDocumentFormatException("persona.document_invalid", "Reaction profile must be an object.");
            if (document.CommitmentProfile == null) throw new PersonaDocumentFormatException("persona.document_invalid", "Commitment profile must be an object.");

            PersonaValidationResult validation = PersonaValidator.Validate(document, registry);
            if (!validation.IsValid)
            {
                throw new PersonaDocumentFormatException(
                    validation.Errors[0].Code,
                    string.Join("; ", validation.Errors.Select(error => error.Message)));
            }

            return document;
        }
        catch (PersonaDocumentFormatException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new PersonaDocumentFormatException("persona.document_invalid", "Persona document JSON is invalid.", ex);
        }
    }

    private static void EnsureWithinLimit(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumUtf8Bytes)
        {
            throw new PersonaDocumentFormatException("persona.document_too_large", "Persona document exceeds the UTF-8 size limit.");
        }
    }

    private static void ValidateRootShape(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new PersonaDocumentFormatException("persona.document_invalid", "Persona document root must be an object.");
        }

        HashSet<string> encountered = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!AllowedProperties.Contains(property.Name))
            {
                throw new PersonaDocumentFormatException("persona.document_unknown_property", "Unknown Persona document property: " + property.Name);
            }
            if (!encountered.Add(property.Name))
            {
                throw new PersonaDocumentFormatException("persona.document_duplicate_property", "Duplicate Persona document property: " + property.Name);
            }
        }
    }
}

public static class PersonaDocumentStore
{
    public static PersonaDocument Read(string path, PersonaTagRegistry registry)
    {
        string fullPath = RequirePath(path);
        FileInfo info = new FileInfo(fullPath);
        if (!info.Exists) throw new FileNotFoundException("Persona document was not found.", fullPath);
        if (info.Length > PersonaDocumentCodec.MaximumUtf8Bytes)
        {
            throw new PersonaDocumentFormatException("persona.document_too_large", "Persona document exceeds the UTF-8 size limit.");
        }

        return PersonaDocumentCodec.Deserialize(File.ReadAllText(fullPath, Encoding.UTF8), registry);
    }

    public static void WriteAtomically(string path, PersonaDocument document)
    {
        string fullPath = RequirePath(path);
        string json = PersonaDocumentCodec.Serialize(document);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Persona document directory is required.");
        Directory.CreateDirectory(directory);

        string temporaryPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static string RequirePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Persona document path is required.", nameof(path));
        return Path.GetFullPath(path);
    }
}
