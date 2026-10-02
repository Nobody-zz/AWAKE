using MarcusAwakeFramework.Api;
using MarcusAwakeTransport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeRuntimeService.Tests;

/// <summary>
/// End-to-end evidence for the framework-side RAG client (F-015). This drives the real
/// <see cref="RuntimeServiceClient"/> against the real service host and its SQLite/FTS5 backend,
/// so it covers the seam the service-only P3C cases cannot see: payload shape, capability
/// negotiation, task scope and result decoding.
/// </summary>
internal static class RagClientTests
{
    private const string Collection = "worldbook.calradia";
    private const string Fingerprint = "corpus.v1";

    internal static async Task<int> RunAsync()
    {
        var passed = 0;
        var failed = 0;

        var servicePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "_build_out", "Release", "MarcusAwakeRuntimeService.exe"));
        if (!File.Exists(servicePath))
        {
            Console.Error.WriteLine("FAIL service_executable_missing:" + servicePath);
            return 1;
        }

        // Under the test output directory rather than the system temp root: the confined sandbox
        // denies creating new directories directly under %TEMP%, and this keeps the run hermetic.
        var dataRoot = Path.Combine(AppContext.BaseDirectory, "_rag-client", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);
        Environment.SetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_DATA_ROOT", dataRoot);

        var sessionCoordinator = new SessionCoordinator();
        var session = new SessionRef("campaign-rag-client", "timeline-rag-client", "session-rag-client");
        var lease = sessionCoordinator.BeginSession(session);
        if (!lease.IsSuccess)
        {
            Console.Error.WriteLine("FAIL session_start_failed:" + DescribeError(lease.Error));
            return 1;
        }

        var context = new RequestContext(new ExtensionId("rag-client-harness"), lease.Value, "rag-client-correlation", DateTimeOffset.UtcNow.AddMinutes(10));
        var client = new RuntimeServiceClient(new RuntimeServiceClientOptions(servicePath: servicePath, requestedCapabilities: new[]
        {
            ProtocolConstants.MessageTypeHealth,
            ProtocolConstants.CapabilityRagRead,
            ProtocolConstants.CapabilityRagWrite
        }));

        using var cancellation = new CancellationTokenSource();
        try
        {
            var started = await client.StartAsync(new RuntimeServiceStartRequest(ProtocolConstants.ServiceId, new ApiVersion(2, 0), ProtocolConstants.MaxFrameBytes), context, cancellation.Token).ConfigureAwait(false);
            if (!started.IsSuccess || !started.Value.IsReady)
            {
                Console.Error.WriteLine("FAIL client_start_failed:" + DescribeError(started.Error));
                return 1;
            }

            await RunCaseAsync("F015-01 ingest_then_keyword_search_round_trip", async () =>
            {
                var ingest = await client.IngestAsync(new RagIngestRequest(Collection, Fingerprint, new[]
                {
                    Document("doc.town.pravend", "Pravend is a Vlandian town on the western coast of Calradia.", "worldbook/town/pravend", "public"),
                    Document("doc.clan.deymer", "Clan dey Meroc holds the fief of Charas in the Vlandian heartland.", "worldbook/clan/deymer", "public")
                }), context, cancellation.Token).ConfigureAwait(false);
                Require(ingest.IsSuccess, "ingest_failed:" + DescribeError(ingest.Error));
                Require(ingest.Value == 2, "ingest_count_invalid:" + ingest.Value.ToString());

                var search = await client.SearchAsync(new RagSearchRequest(Collection, Fingerprint, "Calradia", Array.Empty<string>(), 8), context, cancellation.Token).ConfigureAwait(false);
                Require(search.IsSuccess, "search_failed:" + DescribeError(search.Error));
                Require(search.Value.Count == 1, "search_hit_count_invalid:" + search.Value.Count.ToString());
                Require(search.Value[0].DocumentId == "doc.town.pravend", "search_document_id_invalid:" + search.Value[0].DocumentId);
                Require(search.Value[0].CorpusFingerprint == Fingerprint, "search_fingerprint_echo_invalid:" + search.Value[0].CorpusFingerprint);
                Require(search.Value[0].Mode == RetrievalMode.Keyword, "search_mode_invalid:" + search.Value[0].Mode.ToString());
                Require(search.Value[0].Text.Contains("Pravend"), "search_text_invalid");
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("F015-02 search_filters_by_access_scope", async () =>
            {
                var ingest = await client.IngestAsync(new RagIngestRequest(Collection, Fingerprint, new[]
                {
                    Document("doc.secret.council", "The Calradia war council convenes at Calradia keep.", "worldbook/secret/council", "noble-only")
                }), context, cancellation.Token).ConfigureAwait(false);
                Require(ingest.IsSuccess, "scoped_ingest_failed:" + DescribeError(ingest.Error));

                // An unfiltered search sees every scope in the collection.
                var broad = await client.SearchAsync(new RagSearchRequest(Collection, Fingerprint, "Calradia", Array.Empty<string>(), 8), context, cancellation.Token).ConfigureAwait(false);
                Require(broad.IsSuccess, "broad_search_failed:" + DescribeError(broad.Error));
                Require(broad.Value.Count == 2, "broad_search_hit_count_invalid:" + broad.Value.Count.ToString());

                // A scope the caller may read returns only that scope.
                var allowed = await client.SearchAsync(new RagSearchRequest(Collection, Fingerprint, "Calradia", new[] { "noble-only" }, 8), context, cancellation.Token).ConfigureAwait(false);
                Require(allowed.IsSuccess, "scoped_search_failed:" + DescribeError(allowed.Error));
                Require(allowed.Value.Count == 1 && allowed.Value[0].DocumentId == "doc.secret.council", "scoped_search_leaked_or_missed:" + allowed.Value.Count.ToString());

                // A scope nobody holds returns nothing rather than an error.
                var denied = await client.SearchAsync(new RagSearchRequest(Collection, Fingerprint, "Calradia", new[] { "commoner-only" }, 8), context, cancellation.Token).ConfigureAwait(false);
                Require(denied.IsSuccess, "denied_search_failed:" + DescribeError(denied.Error));
                Require(denied.Value.Count == 0, "denied_search_returned_hits:" + denied.Value.Count.ToString());
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("F015-03 search_honours_result_limit", async () =>
            {
                var search = await client.SearchAsync(new RagSearchRequest(Collection, Fingerprint, "Calradia", Array.Empty<string>(), 1), context, cancellation.Token).ConfigureAwait(false);
                Require(search.IsSuccess, "limited_search_failed:" + DescribeError(search.Error));
                Require(search.Value.Count == 1, "limited_search_ignored_limit:" + search.Value.Count.ToString());
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("F015-04 stale_corpus_fingerprint_is_typed_conflict", async () =>
            {
                var search = await client.SearchAsync(new RagSearchRequest(Collection, "corpus.v-never-ingested", "Calradia", Array.Empty<string>(), 8), context, cancellation.Token).ConfigureAwait(false);
                Require(!search.IsSuccess, "stale_corpus_search_succeeded");
                Require(search.Error.Code == "rag.index_stale", "stale_corpus_code_invalid:" + search.Error.Code);
                Require(search.Error.Category == FrameworkErrorCategory.Conflict, "stale_corpus_category_invalid:" + search.Error.Category.ToString());
                Require(search.Error.Retryable, "stale_corpus_retryable_lost");
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("F015-05 ingest_fingerprint_conflict_is_typed", async () =>
            {
                var ingest = await client.IngestAsync(new RagIngestRequest(Collection, "corpus.v-conflicting", new[]
                {
                    Document("doc.town.pravend", "A conflicting restatement.", "worldbook/town/pravend", "public")
                }), context, cancellation.Token).ConfigureAwait(false);
                Require(!ingest.IsSuccess, "conflicting_ingest_succeeded");
                Require(ingest.Error.Code == "rag.corpus_fingerprint_conflict", "conflict_code_invalid:" + ingest.Error.Code);
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("F015-06 document_without_text_is_rejected_locally", async () =>
            {
                var ingest = await client.IngestAsync(new RagIngestRequest(Collection, "corpus.v-rejected", new[]
                {
                    Document("doc.empty.text", string.Empty, "worldbook/empty", "public")
                }), context, cancellation.Token).ConfigureAwait(false);
                Require(!ingest.IsSuccess, "empty_text_ingest_succeeded");
                Require(ingest.Error.Code == "rag.document_invalid", "empty_text_code_invalid:" + ingest.Error.Code);
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("F015-07 oversized_batch_is_rejected_locally", async () =>
            {
                var documents = new RagDocument[17];
                for (var index = 0; index < documents.Length; index++) documents[index] = Document("doc.batch." + index.ToString("D2"), "Calradia batch document " + index.ToString(), "worldbook/batch/" + index.ToString("D2"), "public");
                var ingest = await client.IngestAsync(new RagIngestRequest(Collection, "corpus.v-batch", documents), context, cancellation.Token).ConfigureAwait(false);
                Require(!ingest.IsSuccess, "oversized_batch_ingest_succeeded");
                Require(ingest.Error.Code == "rag.batch_too_large", "oversized_batch_code_invalid:" + ingest.Error.Code);
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("F015-08 non_keyword_mode_is_not_silently_downgraded", async () =>
            {
                var hybrid = new RagSearchRequest(Collection, Fingerprint, "Calradia", Array.Empty<string>(), 8, RetrievalMode.Hybrid, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Array.Empty<string>());
                var search = await client.SearchAsync(hybrid, context, cancellation.Token).ConfigureAwait(false);
                Require(!search.IsSuccess, "hybrid_search_silently_served_as_keyword");
                Require(search.Error.Code == "rag.retrieval_mode_unsupported", "hybrid_mode_code_invalid:" + search.Error.Code);
                Require(search.Error.Category == FrameworkErrorCategory.Unsupported, "hybrid_mode_category_invalid:" + search.Error.Category.ToString());
                passed++;
            }).ConfigureAwait(false);

            await RunCaseAsync("F015-09 rag_requires_a_campaign_session", async () =>
            {
                var unbound = new RequestContext(new ExtensionId("rag-client-harness"), new SessionRef(string.Empty, string.Empty, "session-unbound"), context.CorrelationId, DateTimeOffset.UtcNow.AddMinutes(5));
                var search = await client.SearchAsync(new RagSearchRequest(Collection, Fingerprint, "Calradia", Array.Empty<string>(), 8), unbound, cancellation.Token).ConfigureAwait(false);
                Require(!search.IsSuccess, "unbound_session_search_succeeded");
                Require(search.Error.Code == "rag.campaign_session_required" || search.Error.Code == "runtime.session_stale", "unbound_session_code_invalid:" + search.Error.Code);
                passed++;
            }).ConfigureAwait(false);

            Console.WriteLine("RAG_CLIENT " + passed.ToString() + "/" + (passed + failed).ToString() + (failed == 0 ? " PASS" : " FAIL"));
            return failed == 0 ? 0 : 1;
        }
        finally
        {
            client.Dispose();
            var closing = sessionCoordinator.BeginClosing(session);
            if (closing.IsSuccess) sessionCoordinator.CompleteDrain(session, closing.Value.Generation);
            try { Directory.Delete(dataRoot, true); } catch (Exception) { }
        }

        async Task RunCaseAsync(string name, Func<Task> action)
        {
            Console.WriteLine("CASE " + name + " START");
            try
            {
                await action().ConfigureAwait(false);
                Console.WriteLine("CASE " + name + " PASS");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine("CASE " + name + " FAIL " + exception.GetType().Name + ":" + exception.Message);
            }
        }
    }

    private static RagDocument Document(string documentId, string text, string sourceLocator, string accessScope)
    {
        return new RagDocument(documentId, text, sourceLocator, accessScope, "worldbook", sourceLocator + ".src", DateTimeOffset.UtcNow);
    }

    private static string DescribeError(FrameworkError error)
    {
        return error == null ? "<none>" : error.Code + "/" + error.Category.ToString();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
