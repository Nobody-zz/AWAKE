using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Awake.WorldbookStudio.Core;

public sealed record SuggestionEnvelope(
    string SchemaVersion,
    string SuggestionId,
    string DocumentId,
    string ProviderId,
    string RequestHash,
    string RequestNonceHash,
    string SourceDocumentHash,
    int SavedRevision,
    string SessionBindingHash,
    string BufferId,
    string SuggestionHash,
    string? ApplyNonce,
    DateTimeOffset CreatedAt,
    AssistanceSuggestion Suggestion,
    bool Applied);

public sealed class SuggestionStore
{
    private const string SchemaVersion = "suggestion-envelope.v1";
    private const int MaxEnvelopeBytes = 1024 * 1024;
    private const long MaxStoreBytes = 64L * 1024 * 1024;
    private static readonly Regex SafeId = new("^[A-Za-z0-9._-]{1,128}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly WorkspaceService _workspace;
    private readonly object _gate = new();

    public SuggestionStore(WorkspaceService workspace)
    {
        _workspace = workspace;
        _workspace.Initialize();
    }

    public IReadOnlyList<SuggestionEnvelope> Save(
        AssistanceRequest request,
        AssistanceResult result,
        string sessionBinding,
        string bufferId,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        AssistanceBinding.Validate(request, result);
        var sessionHash = HashBinding(sessionBinding);
        ValidateBufferId(bufferId);
        var createdAt = now ?? DateTimeOffset.UtcNow;
        var output = new List<SuggestionEnvelope>();
        foreach (var suggestion in result.Suggestions)
        {
            var suggestionHash = CanonicalJson.Hash(SerializeSuggestion(suggestion));
            var storageKey = CanonicalJson.Hash(new JsonObject
            {
                ["request_hash"] = request.RequestHash,
                ["provider_id"] = request.ProviderId,
                ["request_nonce_hash"] = Hashing.Sha256Text(request.Nonce),
                ["suggestion_id"] = suggestion.Id
            });
            var target = _workspace.Policy.RequireSuggestions(
                Path.Combine(_workspace.Root, "authoring", "suggestions", $"{storageKey}.json"),
                "保存 AI 建议");
            lock (_gate)
            {
                if (File.Exists(target))
                {
                    var existing = ParseEnvelope(File.ReadAllText(target));
                    output.Add(existing);
                    continue;
                }

                var envelope = new SuggestionEnvelope(
                    SchemaVersion,
                    suggestion.Id,
                    request.DocumentId,
                    request.ProviderId,
                    request.RequestHash,
                    Hashing.Sha256Text(request.Nonce),
                    request.SourceDocumentHash,
                    request.SavedRevision,
                    sessionHash,
                    bufferId,
                    suggestionHash,
                    NewNonce(),
                    createdAt,
                    suggestion,
                    false);
                WriteAtomic(target, envelope);
                output.Add(envelope);
            }
        }
        return output;
    }

    public SuggestionEnvelope Read(
        string suggestionId,
        string documentId,
        string sourceDocumentHash,
        int savedRevision,
        string sessionBinding,
        string bufferId)
    {
        ValidateBufferId(bufferId);
        var sessionHash = HashBinding(sessionBinding);
        lock (_gate)
        {
            var found = Find(suggestionId);
            if (found is null
                || !Matches(found.Value.Envelope, suggestionId, documentId, sourceDocumentHash, savedRevision, sessionHash, bufferId)
                || found.Value.Envelope.Applied
                || string.IsNullOrWhiteSpace(found.Value.Envelope.ApplyNonce))
                throw NotFound();
            return found.Value.Envelope;
        }
    }

    public void InvalidatePendingForBuffer(
        string documentId,
        string sourceDocumentHash,
        int savedRevision,
        string sessionBinding,
        string bufferId)
    {
        ValidateBufferId(bufferId);
        var sessionHash = HashBinding(sessionBinding);
        lock (_gate)
        {
            var directory = Path.Combine(_workspace.Root, "authoring", "suggestions");
            if (!Directory.Exists(directory)) return;
            foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                SuggestionEnvelope envelope;
                try { envelope = ParseEnvelope(File.ReadAllText(path)); }
                catch (InvalidOperationException) { continue; }
                if (envelope.Applied || !Matches(envelope, envelope.SuggestionId, documentId, sourceDocumentHash, savedRevision, sessionHash, bufferId)) continue;
                WriteAtomic(path, envelope with { Applied = true, ApplyNonce = null });
            }
        }
    }

    public SuggestionEnvelope ConsumeApplyNonce(
        string suggestionId,
        string documentId,
        string sourceDocumentHash,
        int savedRevision,
        string sessionBinding,
        string bufferId,
        string applyNonce)
    {
        ValidateBufferId(bufferId);
        var sessionHash = HashBinding(sessionBinding);
        if (string.IsNullOrWhiteSpace(applyNonce)) throw NotFound();
        lock (_gate)
        {
            var found = Find(suggestionId);
            if (found is null || !Matches(found.Value.Envelope, suggestionId, documentId, sourceDocumentHash, savedRevision, sessionHash, bufferId)
                || found.Value.Envelope.Applied
                || !string.Equals(found.Value.Envelope.ApplyNonce, applyNonce, StringComparison.Ordinal))
                throw NotFound();
            var consumed = found.Value.Envelope with { Applied = true, ApplyNonce = null };
            WriteAtomic(found.Value.Path, consumed);
            return consumed;
        }
    }

    public void Reject(
        string suggestionId,
        string documentId,
        string sourceDocumentHash,
        int savedRevision,
        string sessionBinding,
        string bufferId,
        string applyNonce)
    {
        ValidateBufferId(bufferId);
        var sessionHash = HashBinding(sessionBinding);
        if (string.IsNullOrWhiteSpace(applyNonce)) throw NotFound();
        lock (_gate)
        {
            var found = Find(suggestionId);
            if (found is null || !Matches(found.Value.Envelope, suggestionId, documentId, sourceDocumentHash, savedRevision, sessionHash, bufferId)
                || found.Value.Envelope.Applied
                || !string.Equals(found.Value.Envelope.ApplyNonce, applyNonce, StringComparison.Ordinal))
                throw NotFound();
            WriteAtomic(found.Value.Path, found.Value.Envelope with { Applied = true, ApplyNonce = null });
        }
    }
    private (string Path, SuggestionEnvelope Envelope)? Find(string suggestionId)
    {
        if (string.IsNullOrWhiteSpace(suggestionId)) return null;
        var directory = Path.Combine(_workspace.Root, "authoring", "suggestions");
        if (!Directory.Exists(directory)) return null;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            SuggestionEnvelope envelope;
            try { envelope = ParseEnvelope(File.ReadAllText(path)); }
            catch (InvalidOperationException) { continue; }
            if (string.Equals(envelope.SuggestionId, suggestionId, StringComparison.Ordinal)) return (path, envelope);
        }
        return null;
    }

    private void WriteAtomic(string target, SuggestionEnvelope envelope)
    {
        var bytes = Encoding.UTF8.GetBytes(CanonicalJson.Serialize(SerializeEnvelope(envelope)) + Environment.NewLine);
        if (bytes.Length > MaxEnvelopeBytes) throw new InvalidOperationException("WB-AI-SUGGESTION-413: 建议文件超过大小上限。");
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var existingBytes = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !path.Equals(target, StringComparison.OrdinalIgnoreCase))
            .Sum(path => new FileInfo(path).Length);
        if (existingBytes + bytes.Length > MaxStoreBytes) throw new InvalidOperationException("WB-AI-SUGGESTION-413: 建议存储空间超过上限。");
        var temporary = target + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            File.Move(temporary, target, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static bool Matches(SuggestionEnvelope envelope, string suggestionId, string documentId, string sourceDocumentHash, int savedRevision, string sessionHash, string bufferId)
        => string.Equals(envelope.SuggestionId, suggestionId, StringComparison.Ordinal)
            && string.Equals(envelope.DocumentId, documentId, StringComparison.Ordinal)
            && string.Equals(envelope.SourceDocumentHash, sourceDocumentHash, StringComparison.Ordinal)
            && envelope.SavedRevision == savedRevision
            && string.Equals(envelope.SessionBindingHash, sessionHash, StringComparison.Ordinal)
            && string.Equals(envelope.BufferId, bufferId, StringComparison.Ordinal);

    private static string HashBinding(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512) throw NotFound();
        return Hashing.Sha256Text(value);
    }

    private static void ValidateBufferId(string bufferId)
    {
        if (!SafeId.IsMatch(bufferId ?? string.Empty)) throw NotFound();
    }

    private static InvalidOperationException NotFound()
        => new("WB-AI-SUGGESTION-404: AI 建议不存在或已失效。");

    private static string NewNonce()
        => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static JsonObject SerializeEnvelope(SuggestionEnvelope envelope)
        => new()
        {
            ["schema_version"] = envelope.SchemaVersion,
            ["suggestion_id"] = envelope.SuggestionId,
            ["document_id"] = envelope.DocumentId,
            ["provider_id"] = envelope.ProviderId,
            ["request_hash"] = envelope.RequestHash,
            ["request_nonce_hash"] = envelope.RequestNonceHash,
            ["source_document_hash"] = envelope.SourceDocumentHash,
            ["saved_revision"] = envelope.SavedRevision,
            ["session_binding_hash"] = envelope.SessionBindingHash,
            ["buffer_id"] = envelope.BufferId,
            ["suggestion_hash"] = envelope.SuggestionHash,
            ["apply_nonce"] = envelope.ApplyNonce,
            ["created_at"] = envelope.CreatedAt.ToUniversalTime().ToString("O"),
            ["suggestion"] = SerializeSuggestion(envelope.Suggestion),
            ["applied"] = envelope.Applied
        };

    private static JsonObject SerializeSuggestion(AssistanceSuggestion suggestion)
        => new()
        {
            ["id"] = suggestion.Id,
            ["kind"] = suggestion.Kind,
            ["severity"] = suggestion.Severity,
            ["confidence"] = suggestion.Confidence,
            ["title"] = suggestion.Title,
            ["reason"] = suggestion.Reason,
            ["candidate_text"] = suggestion.CandidateText,
            ["patch"] = suggestion.Patch is null ? null : SerializePatch(suggestion.Patch),
            ["review_only"] = suggestion.ReviewOnly
        };

    private static JsonObject SerializePatch(KnowledgePatch patch)
    {
        var operations = new JsonArray();
        foreach (var operation in patch.Operations)
        {
            operations.Add(new JsonObject
            {
                ["op"] = operation.Op,
                ["path"] = operation.Path,
                ["value"] = operation.Value is null ? null : JsonNode.Parse(operation.Value.ToJsonString())
            });
        }
        return new JsonObject { ["schema_version"] = patch.SchemaVersion, ["operations"] = operations };
    }

    private static SuggestionEnvelope ParseEnvelope(string content)
    {
        JsonObject root;
        try { root = JsonNode.Parse(content)?.AsObject() ?? throw new JsonException(); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new InvalidOperationException("WB-AI-SUGGESTION-500: 建议文件格式无效。");
        }
        EnsureKeys(root, "schema_version", "suggestion_id", "document_id", "provider_id", "request_hash", "request_nonce_hash", "source_document_hash", "saved_revision", "session_binding_hash", "buffer_id", "suggestion_hash", "apply_nonce", "created_at", "suggestion", "applied");
        if (ReadString(root, "schema_version") != SchemaVersion) throw new InvalidOperationException("WB-AI-SUGGESTION-500: 建议版本无效。");
        var suggestionRoot = root["suggestion"]?.AsObject() ?? throw new InvalidOperationException("WB-AI-SUGGESTION-500: 建议内容缺失。");
        var resultRoot = new JsonObject
        {
            ["schema_version"] = "assistance.result.v1",
            ["request_hash"] = ReadString(root, "request_hash"),
            ["source_document_hash"] = ReadString(root, "source_document_hash"),
            ["suggestions"] = new JsonArray(JsonNode.Parse(suggestionRoot.ToJsonString())!)
        };
        var result = AssistanceResultParser.Parse(resultRoot);
        var createdAt = DateTimeOffset.Parse(ReadString(root, "created_at"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
        return new SuggestionEnvelope(
            SchemaVersion,
            ReadString(root, "suggestion_id"),
            ReadString(root, "document_id"),
            ReadString(root, "provider_id"),
            ReadString(root, "request_hash"),
            ReadString(root, "request_nonce_hash"),
            ReadString(root, "source_document_hash"),
            ReadInt(root, "saved_revision"),
            ReadString(root, "session_binding_hash"),
            ReadString(root, "buffer_id"),
            ReadString(root, "suggestion_hash"),
            root["apply_nonce"]?.GetValue<string>(),
            createdAt,
            result.Suggestions[0],
            root["applied"]?.GetValue<bool>() ?? throw new InvalidOperationException("WB-AI-SUGGESTION-500: applied 字段缺失。"));
    }

    private static void EnsureKeys(JsonObject root, params string[] allowed)
    {
        var set = allowed.ToHashSet(StringComparer.Ordinal);
        if (root.Any(item => !set.Contains(item.Key))) throw new InvalidOperationException("WB-AI-SUGGESTION-500: 建议包含未声明字段。");
    }

    private static string ReadString(JsonObject root, string key)
        => root[key]?.GetValue<string>() ?? throw new InvalidOperationException("WB-AI-SUGGESTION-500: 建议字段缺失。");

    private static int ReadInt(JsonObject root, string key)
        => root[key]?.GetValue<int>() ?? throw new InvalidOperationException("WB-AI-SUGGESTION-500: 建议 revision 无效。");
}

public sealed record DocumentBuffer(
    string DocumentPath,
    string DocumentId,
    string SourceDocumentHash,
    int Revision,
    string BufferId,
    JsonNode Document);

public static class DocumentCas
{
    private static readonly Regex SafeId = new("^[A-Za-z0-9._-]{1,128}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static DocumentBuffer Open(string documentPath, string documentId, string sourceDocumentHash, int revision, string bufferId, JsonNode document)
    {
        var normalizedBufferId = bufferId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(documentPath) || string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(sourceDocumentHash) || revision < 1 || !SafeId.IsMatch(normalizedBufferId))
            throw new InvalidOperationException("WB-AI-CAS-409: 编辑器缓冲区身份无效。");
        return new DocumentBuffer(documentPath, documentId, sourceDocumentHash, revision, normalizedBufferId, JsonNode.Parse(document.ToJsonString())!);
    }

    public static DocumentBuffer Apply(DocumentBuffer buffer, SuggestionEnvelope envelope, string applyNonce)
    {
        if (envelope.Applied
            || string.IsNullOrWhiteSpace(envelope.ApplyNonce)
            || !string.Equals(envelope.ApplyNonce, applyNonce, StringComparison.Ordinal)
            || !string.Equals(buffer.DocumentId, envelope.DocumentId, StringComparison.Ordinal)
            || !string.Equals(buffer.SourceDocumentHash, envelope.SourceDocumentHash, StringComparison.Ordinal)
            || buffer.Revision != envelope.SavedRevision
            || !string.Equals(buffer.BufferId, envelope.BufferId, StringComparison.Ordinal))
            throw new InvalidOperationException("WB-AI-CAS-409: 编辑器缓冲区已变化或建议已失效。");
        if (envelope.Suggestion.Patch is null) throw new InvalidOperationException("WB-AI-PATCH-422: 建议没有可应用的补丁。");
        var updated = JsonPatchEngine.Apply(buffer.Document, envelope.Suggestion.Patch);
        var nextRevision = checked(buffer.Revision + 1);
        if (updated is JsonObject updatedObject) updatedObject["revision"] = nextRevision;
        return buffer with { Revision = nextRevision, Document = updated };
    }
}
