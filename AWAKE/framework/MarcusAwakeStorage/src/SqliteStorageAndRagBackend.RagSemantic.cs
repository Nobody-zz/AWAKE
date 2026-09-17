using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MarcusAwakeFramework.Api;
using Microsoft.Data.Sqlite;

namespace MarcusAwakeStorage;

/// <summary>
/// Semantic retrieval for <see cref="SqliteStorageAndRagBackend"/>.
///
/// Division of labour (see <c>docs/PLAN-20260917-语义层接在哪怎么接.md</c>):
///   * the caller owns the corpus and composes each passage's text — the backend never reads the
///     worldbook, it just embeds whatever text it is handed;
///   * this file owns "text in, ranked document ids out": it embeds, stores, and does an exact
///     full scan, which was measured to stay under a millisecond until roughly a hundred thousand
///     passages. No approximate index, and therefore no recall loss;
///   * ranking, permission and prompt assembly stay where they already are — the caller parses
///     these hits, it does not receive a finished answer.
///
/// Vectors are keyed by <see cref="IRagEmbedder.ModelId"/>. Swapping the model, or changing how the
/// passages are composed, therefore does not silently mix two vector spaces: the stale rows stop
/// matching and are replaced on the next ingest.
/// </summary>
public sealed partial class SqliteStorageAndRagBackend
{
    /// <summary>
    /// Cosine floor below which a semantic hit is not returned at all. Both vectors are
    /// L2-normalized, so the comparison is a dot product in [-1, 1].
    ///
    /// The value comes from measuring both sides on this exact model and corpus
    /// (<c>tools/_semantic_floor_20260917.py</c>, output kept in
    /// <c>tools/_semantic_floor_20260917.json</c>):
    ///   * three unrelated questions ("what shall I have for lunch") peak at 0.339–0.392;
    ///   * the lowest target that any of the 26 real questions is meant to reach sits at 0.446;
    ///   * every target that the semantic arm actually ranks first sits at 0.550–0.795.
    ///
    /// 0.45 sits in the gap between the first two. ⚠️ It is a property of *this* model together with
    /// *this* corpus: re-measure it whenever either changes, or the arm will start answering
    /// confidently about lunch.
    /// </summary>
    private const double MinimumScore = 0.45;

    private static void CreateSemanticSchema(SqliteConnection connection)
    {
        ExecuteNonQuery(connection, "CREATE TABLE IF NOT EXISTS rag_embeddings(owner_id TEXT NOT NULL,campaign_guid TEXT NOT NULL,timeline_id TEXT NOT NULL,collection_id TEXT NOT NULL,document_id TEXT NOT NULL,model_id TEXT NOT NULL,dimension INTEGER NOT NULL,vector BLOB NOT NULL,updated_unix_ms INTEGER NOT NULL,PRIMARY KEY(owner_id,campaign_guid,timeline_id,collection_id,document_id));");
    }

    /// <summary>
    /// Embeds the ingested documents and replaces their stored vectors.
    ///
    /// Runs inside the caller's transaction, so a document and its vector can never disagree.
    /// Callers that need the vectors refreshed but the text kept should pass a null
    /// <paramref name="vectors"/> — the documents are ingested, semantic search simply finds
    /// nothing for them.
    /// </summary>
    private void WriteEmbeddings(SqliteConnection connection, SqliteTransaction transaction, RagIngestRequest request, RequestContext context, float[][] vectors, CancellationToken cancellationToken)
    {
        if (vectors == null || vectors.Length != request.Documents.Count) return;
        var modelId = embedder.ModelId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (var index = 0; index < request.Documents.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vector = vectors[index];
            if (vector == null || vector.Length != embedder.Dimension) continue;

            using var command = CreateCommand(connection, "INSERT INTO rag_embeddings(owner_id,campaign_guid,timeline_id,collection_id,document_id,model_id,dimension,vector,updated_unix_ms) VALUES(@owner,@campaign,@timeline,@collection,@document,@model,@dimension,@vector,@now) ON CONFLICT(owner_id,campaign_guid,timeline_id,collection_id,document_id) DO UPDATE SET model_id=excluded.model_id,dimension=excluded.dimension,vector=excluded.vector,updated_unix_ms=excluded.updated_unix_ms;", transaction);
            AddCollectionIdentityParameters(command, request.CollectionId, context);
            AddParameter(command, "@document", request.Documents[index].DocumentId);
            AddParameter(command, "@model", modelId);
            AddParameter(command, "@dimension", vector.Length);
            AddParameter(command, "@vector", ToBytes(vector));
            AddParameter(command, "@now", now);
            command.ExecuteNonQuery();
        }

        // Vectors written by a previous model are dead weight and, worse, would be silently compared
        // against the new model's query vectors if the model check were ever loosened. Drop them now.
        using (var prune = CreateCommand(connection, "DELETE FROM rag_embeddings WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline AND collection_id=@collection AND model_id<>@model;", transaction))
        {
            AddCollectionIdentityParameters(prune, request.CollectionId, context);
            AddParameter(prune, "@model", modelId);
            prune.ExecuteNonQuery();
        }
    }

    private OperationResult<IReadOnlyList<RagHit>> SearchSemantic(SqliteConnection connection, RagSearchRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var transaction = connection.BeginTransaction();
        var storedFingerprint = GetCollectionFingerprint(connection, request.CollectionId, context, transaction);
        if (storedFingerprint == null || !StringComparer.Ordinal.Equals(storedFingerprint, request.CorpusFingerprint))
        {
            transaction.Rollback();
            return Failure<IReadOnlyList<RagHit>>("rag.index_stale", FrameworkErrorCategory.Conflict, "The RAG corpus fingerprint is stale.", context, true);
        }

        float[][] queryVectors;
        try
        {
            queryVectors = embedder.Encode(new[] { request.Query }, cancellationToken);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException))
        {
            transaction.Rollback();
            return Failure<IReadOnlyList<RagHit>>("rag.embedding_failed", FrameworkErrorCategory.Unavailable, "The embedding model could not encode the query.", context, true);
        }
        if (queryVectors.Length != 1 || queryVectors[0] == null || queryVectors[0].Length != embedder.Dimension)
        {
            transaction.Rollback();
            return Failure<IReadOnlyList<RagHit>>("rag.embedding_failed", FrameworkErrorCategory.Unavailable, "The embedding model returned an unexpected vector shape.", context);
        }

        var query = queryVectors[0];
        var scopeClause = new StringBuilder();
        var scopeParameters = new List<KeyValuePair<string, string>>();
        if (request.AccessScopes.Count > 0)
        {
            scopeClause.Append(" AND d.access_scope IN (");
            for (var index = 0; index < request.AccessScopes.Count; index++)
            {
                if (index > 0) scopeClause.Append(",");
                var parameterName = "@scope" + index;
                scopeClause.Append(parameterName);
                scopeParameters.Add(new KeyValuePair<string, string>(parameterName, request.AccessScopes[index] ?? string.Empty));
            }
            scopeClause.Append(")");
        }

        // Exact full scan. Measured: 1,000 passages ≈ 0.47 ms, 100,000 ≈ 48 ms
        // (tools/_probe_vector_scan_20260917). An approximate index would buy nothing here and
        // would cost recall, which is the opposite of what this channel exists for.
        var sql = "SELECT d.document_id,d.text,d.source_locator,e.vector FROM rag_embeddings AS e JOIN rag_documents AS d ON d.owner_id=e.owner_id AND d.campaign_guid=e.campaign_guid AND d.timeline_id=e.timeline_id AND d.collection_id=e.collection_id AND d.document_id=e.document_id WHERE e.owner_id=@owner AND e.campaign_guid=@campaign AND e.timeline_id=@timeline AND e.collection_id=@collection AND e.model_id=@model AND e.dimension=@dimension" + scopeClause + ";";
        var scored = new List<SemanticCandidate>();
        using (var command = CreateCommand(connection, sql, transaction))
        {
            AddParameter(command, "@owner", context.Caller.Value);
            AddParameter(command, "@campaign", context.Session.CampaignGuid);
            AddParameter(command, "@timeline", context.Session.TimelineId);
            AddParameter(command, "@collection", request.CollectionId);
            AddParameter(command, "@model", embedder.ModelId);
            AddParameter(command, "@dimension", embedder.Dimension);
            for (var index = 0; index < scopeParameters.Count; index++) AddParameter(command, scopeParameters[index].Key, scopeParameters[index].Value);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var vector = FromBytes(reader.GetValue(3) as byte[], embedder.Dimension);
                if (vector == null) continue;
                scored.Add(new SemanticCandidate(Dot(query, vector), reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        // Rank is the position in this ordering; ties break on document id so the same corpus always
        // answers the same question the same way.
        scored.Sort((left, right) =>
        {
            var byScore = right.Score.CompareTo(left.Score);
            return byScore != 0 ? byScore : StringComparer.Ordinal.Compare(left.DocumentId, right.DocumentId);
        });

        var hits = new List<RagHit>(Math.Min(scored.Count, request.MaximumResults));
        var totalBytes = 0;
        for (var index = 0; index < scored.Count && hits.Count < request.MaximumResults; index++)
        {
            var candidate = scored[index];
            if (candidate.Score < MinimumScore)
            {
                // Below the floor the ordering stops carrying information — a nearest-neighbour
                // search always has a nearest neighbour, so without this every unrelated question
                // comes back with "the closest places", which is exactly the confident wrong answer
                // the retrieval chain exists to avoid. Returning nothing lets the caller fall back
                // to its literal arm and, if that is empty too, to "I have not heard of it".
                continue;
            }
            var textBytes = ByteCount(candidate.Text);
            if (totalBytes + textBytes > options.MaxResultBytes)
            {
                // The text still has to travel back for the wire contract to accept the hit, so a
                // passage that does not fit is skipped rather than truncated into a wrong answer.
                continue;
            }
            totalBytes += textBytes;
            hits.Add(new RagHit(candidate.DocumentId, candidate.Text, candidate.SourceLocator, hits.Count, request.CorpusFingerprint, RetrievalMode.Semantic));
        }

        transaction.Commit();
        return OperationResult<IReadOnlyList<RagHit>>.Succeeded(hits.AsReadOnly());
    }

    private readonly struct SemanticCandidate
    {
        public SemanticCandidate(double score, string documentId, string text, string sourceLocator)
        {
            Score = score;
            DocumentId = documentId;
            Text = text;
            SourceLocator = sourceLocator;
        }

        public double Score { get; }
        public string DocumentId { get; }
        public string Text { get; }
        public string SourceLocator { get; }
    }

    private static double Dot(float[] left, float[] right)
    {
        double sum = 0;
        for (var index = 0; index < left.Length; index++) sum += (double)left[index] * right[index];
        return sum;
    }

    private static byte[] ToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] FromBytes(byte[] bytes, int dimension)
    {
        if (bytes == null || bytes.Length != dimension * sizeof(float)) return null;
        var vector = new float[dimension];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }
}
