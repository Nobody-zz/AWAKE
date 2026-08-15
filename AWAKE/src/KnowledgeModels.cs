using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Awake;

internal sealed class KnowledgeDocument
{
    internal string DocumentId { get; set; }
    internal string Title { get; set; }
    internal List<string> Keywords { get; set; }
    internal string Content { get; set; }
    internal string SourceLocator { get; set; }
}

internal sealed class KnowledgeCorpus
{
    internal string SchemaVersion { get; set; }
    internal List<KnowledgeDocument> Documents { get; set; }
}

internal sealed class KnowledgeHit
{
    internal string DocumentId { get; }
    internal string Text { get; }

    internal KnowledgeHit(string documentId, string text)
    {
        DocumentId = documentId ?? string.Empty;
        Text = text ?? string.Empty;
    }
}

internal static class KnowledgeCorpusLoader
{
    internal static string ComputeFingerprint(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(bytes);
            StringBuilder builder = new StringBuilder(64);
            foreach (byte b in hash) builder.Append(b.ToString("x2"));
            return "awake.knowledge.v1:" + builder.ToString();
        }
    }

    internal static byte[] ReadCorpusFile(string relativePath)
    {
        string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
        DirectoryInfo current = new DirectoryInfo(assemblyDir);
        for (int i = 0; i < 6 && current != null; i++)
        {
            string candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate)) return File.ReadAllBytes(candidate);
            candidate = Path.Combine(current.FullName, "ModuleData", relativePath);
            if (File.Exists(candidate)) return File.ReadAllBytes(candidate);
            current = current.Parent;
        }
        return null;
    }

    internal static KnowledgeCorpus ParseCorpus(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;
        try
        {
            return JsonConvert.DeserializeObject<KnowledgeCorpus>(Encoding.UTF8.GetString(bytes));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("knowledge_corpus_parse_error error=" + ex.Message);
            return null;
        }
    }

    internal static string BuildRetrievedBlock(IReadOnlyList<KnowledgeHit> hits, int maximumBytes)
    {
        if (hits == null || hits.Count == 0) return string.Empty;
        StringBuilder builder = new StringBuilder();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (KnowledgeHit hit in hits)
        {
            if (string.IsNullOrWhiteSpace(hit.Text)) continue;
            if (!seen.Add(hit.DocumentId)) continue;
            string text = hit.Text.Trim();
            string line = "· [" + hit.DocumentId + "] " + text;
            int nextBytes = Encoding.UTF8.GetByteCount(builder.ToString()) + Encoding.UTF8.GetByteCount(line) + 1;
            if (nextBytes > maximumBytes)
            {
                if (builder.Length == 0)
                {
                    string trimmed = TruncateUtf8(line, maximumBytes);
                    if (trimmed.Length > 0) builder.Append(trimmed);
                }
                break;
            }
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(line);
        }
        return builder.ToString();
    }

    private static string TruncateUtf8(string value, int maximumBytes)
    {
        int bytes = 0;
        StringBuilder builder = new StringBuilder();
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            string element = enumerator.GetTextElement();
            int next = Encoding.UTF8.GetByteCount(element);
            if (bytes + next > maximumBytes) break;
            builder.Append(element);
            bytes += next;
        }
        return builder.ToString();
    }
}

internal sealed class LocalKeywordIndex
{
    private readonly List<LocalEntry> _entries = new List<LocalEntry>();

    internal LocalKeywordIndex(KnowledgeCorpus corpus)
    {
        if (corpus?.Documents == null) return;
        foreach (KnowledgeDocument doc in corpus.Documents)
        {
            if (string.IsNullOrWhiteSpace(doc.DocumentId) || string.IsNullOrWhiteSpace(doc.Content)) continue;
            _entries.Add(new LocalEntry(doc));
        }
    }

    internal bool IsEmpty => _entries.Count == 0;

    internal IReadOnlyList<KnowledgeHit> Search(string query, int maximum)
    {
        List<KnowledgeHit> results = new List<KnowledgeHit>();
        if (string.IsNullOrWhiteSpace(query) || _entries.Count == 0) return results;
        string[] terms = query.Split(new[] { ' ', '　', '\t', '，', '。', '！', '？', '、', '；', '：', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        List<Tuple<LocalEntry, int>> scored = new List<Tuple<LocalEntry, int>>();
        foreach (LocalEntry entry in _entries)
        {
            int score = entry.Score(terms);
            if (score > 0) scored.Add(Tuple.Create(entry, score));
        }
        scored.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        foreach (Tuple<LocalEntry, int> pair in scored)
        {
            results.Add(new KnowledgeHit(pair.Item1.Document.DocumentId, pair.Item1.Document.Content));
            if (results.Count >= maximum) break;
        }
        return results;
    }

    private sealed class LocalEntry
    {
        internal KnowledgeDocument Document { get; }
        private readonly string[] _keywords;

        internal LocalEntry(KnowledgeDocument doc)
        {
            Document = doc;
            List<string> keys = new List<string>();
            if (doc.Keywords != null) keys.AddRange(doc.Keywords);
            if (!string.IsNullOrWhiteSpace(doc.Title)) keys.Add(doc.Title);
            if (!string.IsNullOrWhiteSpace(doc.DocumentId)) keys.Add(doc.DocumentId);
            _keywords = keys.ToArray();
        }

        internal int Score(string[] terms)
        {
            int score = 0;
            foreach (string term in terms)
            {
                if (string.IsNullOrWhiteSpace(term)) continue;
                if (_keywords.Any(k => k.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)) score += 3;
                else if (Document.Content.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) score += 1;
            }
            return score;
        }
    }
}
