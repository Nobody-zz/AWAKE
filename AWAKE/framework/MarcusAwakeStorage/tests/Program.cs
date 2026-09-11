using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Microsoft.Data.Sqlite;
using MarcusAwakeStorage;

namespace MarcusAwakeStorage.Tests;

internal static class Program
{
    private static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("campaign_and_session_kv", TestCampaignAndSessionKvAsync),
            ("rag_fts_upsert_scope_and_order", TestRagFtsUpsertScopeAndOrderAsync),
            ("rag_fingerprint_conflict_is_atomic", TestRagFingerprintConflictAsync),
            ("rag_identity_and_mode_boundaries", TestRagIdentityAndModeBoundariesAsync),
            ("timeline_ledger_and_restart_recovery", TestTimelineLedgerAndRestartAsync),
            ("cancellation_and_deadline_errors", TestCancellationAndDeadlineAsync)
        };

        var failures = new List<string>();
        foreach (var test in tests)
        {
            try
            {
                await test.Run().ConfigureAwait(false);
                Console.WriteLine("PASS " + test.Name);
            }
            catch (Exception exception)
            {
                failures.Add(test.Name + ": " + exception.Message);
                Console.WriteLine("FAIL " + test.Name + " " + exception);
            }
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("PASS ALL");
            return 0;
        }

        Console.WriteLine("FAILURES " + failures.Count);
        return 1;
    }

    private static async Task TestCampaignAndSessionKvAsync()
    {
        var databasePath = NewDatabasePath();
        try
        {
            using (var backend = new SqliteStorageAndRagBackend(databasePath))
            using (var first = new TestSession("session-a"))
            {
                using var tokenSource = new CancellationTokenSource();
                var campaign = await backend.OpenCampaignNamespaceAsync("campaign-state", first.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(campaign);
                var session = await backend.OpenSessionNamespaceAsync("session-state", first.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(session);
                AssertSuccess(await campaign.Value.SetAsync("gold", "{\"value\":1}", first.Context, tokenSource.Token).ConfigureAwait(false));
                AssertSuccess(await session.Value.SetAsync("gold", "session-value", first.Context, tokenSource.Token).ConfigureAwait(false));
                AssertEqual("{\"value\":1}", (await campaign.Value.GetAsync("gold", first.Context, tokenSource.Token).ConfigureAwait(false)).Value, "campaign KV round-trip");
                AssertEqual("session-value", (await session.Value.GetAsync("gold", first.Context, tokenSource.Token).ConfigureAwait(false)).Value, "session KV round-trip");
                AssertSuccess(await campaign.Value.DeleteAsync("missing", first.Context, tokenSource.Token).ConfigureAwait(false));
                var exactLimit = new string('x', 512 * 1024);
                AssertSuccess(await campaign.Value.SetAsync("limit", exactLimit, first.Context, tokenSource.Token).ConfigureAwait(false));
                var overLimit = await campaign.Value.SetAsync("over", exactLimit + "x", first.Context, tokenSource.Token).ConfigureAwait(false);
                AssertFailure(overLimit, "storage.value_too_large");

                var wrongOwner = new RequestContext(new ExtensionId("other.owner"), first.Lease, "wrong-owner", DateTimeOffset.UtcNow.AddMinutes(1));
                AssertFailure(await campaign.Value.GetAsync("gold", wrongOwner, tokenSource.Token).ConfigureAwait(false), "storage.scope_denied");
                using (var outsider = new TestSession("session-outsider"))
                {
                    AssertFailure(await session.Value.GetAsync("gold", outsider.Context, tokenSource.Token).ConfigureAwait(false), "storage.scope_denied");
                }
            }

            using (var backend = new SqliteStorageAndRagBackend(databasePath))
            using (var second = new TestSession("session-b"))
            {
                using var tokenSource = new CancellationTokenSource();
                var campaign = await backend.OpenCampaignNamespaceAsync("campaign-state", second.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(campaign);
                var session = await backend.OpenSessionNamespaceAsync("session-state", second.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(session);
                AssertEqual("{\"value\":1}", (await campaign.Value.GetAsync("gold", second.Context, tokenSource.Token).ConfigureAwait(false)).Value, "campaign survives session change");
                AssertEqual(null, (await session.Value.GetAsync("gold", second.Context, tokenSource.Token).ConfigureAwait(false)).Value, "session isolation");
            }
        }
        finally
        {
            CleanupDatabase(databasePath);
        }
    }

    private static async Task TestRagFtsUpsertScopeAndOrderAsync()
    {
        var databasePath = NewDatabasePath();
        try
        {
            using (var backend = new SqliteStorageAndRagBackend(databasePath))
            using (var fixture = new TestSession("rag"))
            {
                using var tokenSource = new CancellationTokenSource();
                var documents = LoadFixtureDocuments();
                var ingest = await backend.IngestAsync(new RagIngestRequest("worldbook", "corpus-v1", documents), fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(ingest);
                AssertEqual(documents.Count, ingest.Value, "ingest count");
                var publicRequest = new RagSearchRequest("worldbook", "corpus-v1", "world knowledge", new[] { "public" }, 10, RetrievalMode.Keyword, fixture.Caller.Value, fixture.Reference.CampaignGuid, fixture.Reference.TimelineId, fixture.Reference.SessionId, "", Array.Empty<string>());
                var publicSearch = await backend.SearchAsync(publicRequest, fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(publicSearch);
                AssertEqual(1, publicSearch.Value.Count, "access scope filtering");
                AssertEqual("doc-b", publicSearch.Value[0].DocumentId, "public document");

                var allRequest = new RagSearchRequest("worldbook", "corpus-v1", "world knowledge", Array.Empty<string>(), 10);
                var allSearch = await backend.SearchAsync(allRequest, fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(allSearch);
                AssertEqual(2, allSearch.Value.Count, "empty scopes means isolated collection");
                AssertEqual("doc-a", allSearch.Value[0].DocumentId, "deterministic document id tie-break");
                AssertEqual("doc-b", allSearch.Value[1].DocumentId, "deterministic document id tie-break");

                var updated = new RagDocument("doc-b", "World knowledge was updated.", "fixture:public", "public", "worldbook", "fixture-corpus", DateTimeOffset.Parse("2026-08-27T00:00:02Z"));
                var updateResult = await backend.IngestAsync(new RagIngestRequest("worldbook", "corpus-v1", new[] { updated }), fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(updateResult);
                AssertEqual(1, updateResult.Value, "upsert count");
                var updatedSearch = await backend.SearchAsync(publicRequest, fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(updatedSearch);
                AssertEqual(1, updatedSearch.Value.Count, "upsert has one hit");
                AssertEqual("World knowledge was updated.", updatedSearch.Value[0].Text, "upsert text");

                using (var schemaConnection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString()))
                {
                    schemaConnection.Open();
                    using (var schemaCommand = schemaConnection.CreateCommand())
                    {
                        schemaCommand.CommandText = "SELECT type FROM sqlite_master WHERE name = 'rag_documents_fts';";
                        AssertEqual("table", Convert.ToString(schemaCommand.ExecuteScalar()), "FTS5 schema table");
                    }
                }
            }
        }
        finally
        {
            CleanupDatabase(databasePath);
        }
    }

    private static async Task TestRagFingerprintConflictAsync()
    {
        var databasePath = NewDatabasePath();
        try
        {
            using (var backend = new SqliteStorageAndRagBackend(databasePath))
            using (var fixture = new TestSession("fingerprint"))
            {
                using var tokenSource = new CancellationTokenSource();
                var initial = new RagDocument("stable", "stable world fact", "fixture:stable", "public", "worldbook", "fixture-corpus", DateTimeOffset.UtcNow);
                var initialResult = await backend.IngestAsync(new RagIngestRequest("worldbook", "corpus-v1", new[] { initial }), fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(initialResult);
                var conflictDocument = new RagDocument("new", "must not be committed", "fixture:new", "public", "worldbook", "fixture-corpus", DateTimeOffset.UtcNow);
                var conflict = await backend.IngestAsync(new RagIngestRequest("worldbook", "corpus-v2", new[] { conflictDocument }), fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertFailure(conflict, "rag.corpus_fingerprint_conflict");
                var oldSearch = await backend.SearchAsync(new RagSearchRequest("worldbook", "corpus-v1", "stable", Array.Empty<string>(), 10), fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(oldSearch);
                AssertEqual(1, oldSearch.Value.Count, "old corpus remains readable");
                var newSearch = await backend.SearchAsync(new RagSearchRequest("worldbook", "corpus-v2", "must", Array.Empty<string>(), 10), fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertFailure(newSearch, "rag.index_stale");
            }
        }
        finally
        {
            CleanupDatabase(databasePath);
        }
    }

    private static async Task TestRagIdentityAndModeBoundariesAsync()
    {
        var databasePath = NewDatabasePath();
        try
        {
            using (var backend = new SqliteStorageAndRagBackend(databasePath))
            using (var fixture = new TestSession("identity"))
            {
                using var tokenSource = new CancellationTokenSource();
                var document = new RagDocument("identity-doc", "identity world", "fixture:identity", "public", "worldbook", "fixture-corpus", DateTimeOffset.UtcNow);
                var ingest = await backend.IngestAsync(new RagIngestRequest("worldbook", "corpus-v1", new[] { document }), fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(ingest);
                var hybrid = new RagSearchRequest("worldbook", "corpus-v1", "world", Array.Empty<string>(), 10, RetrievalMode.Hybrid, "", "", "", "", "", Array.Empty<string>());
                AssertFailure(await backend.SearchAsync(hybrid, fixture.Context, tokenSource.Token).ConfigureAwait(false), "rag.retrieval_mode_unsupported");
                var semantic = new RagSearchRequest("worldbook", "corpus-v1", "world", Array.Empty<string>(), 10, RetrievalMode.Semantic, "", "", "", "", "", Array.Empty<string>());
                AssertFailure(await backend.SearchAsync(semantic, fixture.Context, tokenSource.Token).ConfigureAwait(false), "rag.retrieval_mode_unsupported");
                var wrongOwner = new RagSearchRequest("worldbook", "corpus-v1", "world", Array.Empty<string>(), 10, RetrievalMode.Keyword, "other.owner", fixture.Reference.CampaignGuid, fixture.Reference.TimelineId, fixture.Reference.SessionId, "", Array.Empty<string>());
                AssertFailure(await backend.SearchAsync(wrongOwner, fixture.Context, tokenSource.Token).ConfigureAwait(false), "rag.scope_denied");
                var wrongTimeline = new RagSearchRequest("worldbook", "corpus-v1", "world", Array.Empty<string>(), 10, RetrievalMode.Keyword, fixture.Caller.Value, fixture.Reference.CampaignGuid, "other-timeline", fixture.Reference.SessionId, "", Array.Empty<string>());
                AssertFailure(await backend.SearchAsync(wrongTimeline, fixture.Context, tokenSource.Token).ConfigureAwait(false), "rag.scope_denied");
                var empty = new RagSearchRequest("worldbook", "corpus-v1", "", Array.Empty<string>(), 10);
                var emptyResult = await backend.SearchAsync(empty, fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(emptyResult);
                AssertEqual(0, emptyResult.Value.Count, "empty keyword query");
            }
        }
        finally
        {
            CleanupDatabase(databasePath);
        }
    }
    private static async Task TestTimelineLedgerAndRestartAsync()
    {
        var databasePath = NewDatabasePath();
        try
        {
            using (var backend = new SqliteStorageAndRagBackend(databasePath))
            using (var fixture = new TestSession("ledger"))
            {
                using var tokenSource = new CancellationTokenSource();
                var first = new TimelineLedgerEvent("event-1", "world.fact", "{\"fact\":1}", DateTimeOffset.Parse("2026-08-27T00:00:00Z"), "corr-1", "cause-1");
                var firstAppend = await backend.AppendTimelineEventAsync(first, fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(firstAppend);
                AssertEqual(1L, firstAppend.Value, "first ledger sequence");
                var idempotent = await backend.AppendTimelineEventAsync(first, fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(idempotent);
                AssertEqual(1L, idempotent.Value, "idempotent event sequence");
                var conflicting = new TimelineLedgerEvent("event-1", "world.fact", "{\"fact\":2}", first.OccurredAt, "corr-1", "cause-1");
                AssertFailure(await backend.AppendTimelineEventAsync(conflicting, fixture.Context, tokenSource.Token).ConfigureAwait(false), "storage.ledger_event_conflict");
                var second = new TimelineLedgerEvent("event-2", "world.fact", "{\"fact\":2}", DateTimeOffset.Parse("2026-08-27T00:00:01Z"), "corr-2", "cause-1");
                var secondAppend = await backend.AppendTimelineEventAsync(second, fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(secondAppend);
                AssertEqual(2L, secondAppend.Value, "second ledger sequence");
                var page = await backend.ReadTimelineAsync(0, 10, fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(page);
                AssertEqual(2, page.Value.Count, "ledger row count");
                AssertEqual("event-1", page.Value[0].EventId, "ledger ordering");
                AssertEqual("event-2", page.Value[1].EventId, "ledger ordering");
            }

            using (var backend = new SqliteStorageAndRagBackend(databasePath))
            using (var fixture = new TestSession("ledger"))
            {
                using var tokenSource = new CancellationTokenSource();
                var page = await backend.ReadTimelineAsync(0, 10, fixture.Context, tokenSource.Token).ConfigureAwait(false);
                AssertSuccess(page);
                AssertEqual(2, page.Value.Count, "ledger restart recovery");
            }
        }
        finally
        {
            CleanupDatabase(databasePath);
        }
    }

    private static async Task TestCancellationAndDeadlineAsync()
    {
        var databasePath = NewDatabasePath();
        try
        {
            using (var backend = new SqliteStorageAndRagBackend(databasePath))
            using (var fixture = new TestSession("cancel"))
            {
                var missing = await backend.OpenCampaignNamespaceAsync("state", fixture.Context, CancellationToken.None).ConfigureAwait(false);
                AssertFailure(missing, "storage.cancellation_token_missing");
                using var cancelledSource = new CancellationTokenSource();
                cancelledSource.Cancel();
                var cancelled = await backend.OpenCampaignNamespaceAsync("state", fixture.Context, cancelledSource.Token).ConfigureAwait(false);
                AssertFailure(cancelled, "awake.cancelled");
                using (var expired = new TestSession("expired", DateTimeOffset.UtcNow.AddMilliseconds(-1)))
                using (var expiredToken = new CancellationTokenSource())
                {
                    var deadline = await backend.OpenCampaignNamespaceAsync("state", expired.Context, expiredToken.Token).ConfigureAwait(false);
                    AssertFailure(deadline, "storage.deadline_expired");
                }

                using var firstToken = new CancellationTokenSource();
                var largeDocuments = Enumerable.Range(0, 120).Select(index => new RagDocument("large-" + index.ToString("D3"), new string('x', 32768), "fixture:large", "public", "worldbook", "fixture-corpus", DateTimeOffset.UtcNow)).ToArray();
                var longIngest = backend.IngestAsync(new RagIngestRequest("large", "corpus-v1", largeDocuments), fixture.Context, firstToken.Token);
                await Task.Delay(10).ConfigureAwait(false);
                using (var queuedDeadlineSource = new CancellationTokenSource())
                using (var queuedDeadline = new TestSession("queued", DateTimeOffset.UtcNow.AddMilliseconds(20)))
                {
                    var queued = await backend.OpenCampaignNamespaceAsync("queued", queuedDeadline.Context, queuedDeadlineSource.Token).ConfigureAwait(false);
                    AssertFailure(queued, "storage.deadline_expired");
                }
                firstToken.Cancel();
                await longIngest.ConfigureAwait(false);
            }
        }
        finally
        {
            CleanupDatabase(databasePath);
        }
    }

    private static List<RagDocument> LoadFixtureDocuments()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-corpus.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.EnumerateArray().Select(item => new RagDocument(
            item.GetProperty("documentId").GetString(),
            item.GetProperty("text").GetString(),
            item.GetProperty("sourceLocator").GetString(),
            item.GetProperty("accessScope").GetString(),
            item.GetProperty("sourceClass").GetString(),
            item.GetProperty("corpusLocator").GetString(),
            item.GetProperty("observedAt").GetDateTimeOffset())).ToList();
    }

    private static string NewDatabasePath()
    {
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "storage-" + Guid.NewGuid().ToString("N") + ".db");
    }

    private static void CleanupDatabase(string databasePath)
    {
        foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void AssertSuccess<T>(OperationResult<T> result)
    {
        if (result == null || !result.IsSuccess) throw new InvalidOperationException("Expected success but received " + result?.Error?.Code);
    }

    private static void AssertFailure<T>(OperationResult<T> result, string code)
    {
        if (result == null || result.IsSuccess || !StringComparer.Ordinal.Equals(code, result.Error.Code)) throw new InvalidOperationException("Expected failure " + code + " but received " + result?.Error?.Code);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
    }

    private sealed class TestSession : IDisposable
    {
        private readonly SessionCoordinator coordinator;

        internal TestSession(string sessionId, DateTimeOffset? deadline = null)
        {
            Caller = new ExtensionId("fixture.storage.owner");
            Reference = new SessionRef("campaign-fixture", "timeline-fixture", sessionId);
            coordinator = new SessionCoordinator();
            var begun = coordinator.BeginSession(Reference);
            AssertSuccess(begun);
            Lease = begun.Value;
            Context = new RequestContext(Caller, Lease, "corr-" + sessionId, deadline ?? DateTimeOffset.UtcNow.AddMinutes(5));
        }

        internal ExtensionId Caller { get; }
        internal SessionRef Reference { get; }
        internal SessionLease Lease { get; }
        internal RequestContext Context { get; }

        public void Dispose()
        {
            if (Lease.State == SessionState.Ready)
            {
                var closing = coordinator.BeginClosing(Reference);
                if (closing.IsSuccess) coordinator.CompleteDrain(Reference, closing.Value.Generation);
            }
        }
    }
}
