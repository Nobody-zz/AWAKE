using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MarcusAwakeFramework.Api;
using MarcusAwakeFramework.Tests.TestDoubles;

namespace MarcusAwakeFramework.Tests
{
    internal sealed class RuntimeVerticalEvidence
    {
        internal RuntimeVerticalEvidence()
        {
            Fixtures = new List<Dictionary<string, object>>();
            EventSequences = new List<Dictionary<string, object>>();
            TerminalReceipts = new List<Dictionary<string, object>>();
            RedactionAssertions = new List<Dictionary<string, object>>();
            BudgetAssertions = new List<Dictionary<string, object>>();
            Unverified = new List<string> { "fixture_only", "real_runtime_service_deferred", "live_provider_deferred", "sqlite_fts5_deferred" };
        }

        internal List<Dictionary<string, object>> Fixtures { get; }
        internal List<Dictionary<string, object>> EventSequences { get; }
        internal List<Dictionary<string, object>> TerminalReceipts { get; }
        internal List<Dictionary<string, object>> RedactionAssertions { get; }
        internal List<Dictionary<string, object>> BudgetAssertions { get; }
        internal List<string> Unverified { get; }
    }

    internal static class RuntimeVerticalSmoke
    {
        internal static RuntimeVerticalEvidence Run()
        {
            var evidence = new RuntimeVerticalEvidence();
            RunCase(evidence, "P3A-001-LIFECYCLE-TASK-RECEIPT", LifecycleTaskAndReceipt);
            RunCase(evidence, "P3A-002-SESSION-ROUTE-BUDGET", SessionRouteAndBudget);
            RunCase(evidence, "P3A-003-STREAM-CANCELLATION", StreamingCancellation);
            RunCase(evidence, "P3A-004-PROMPT-PROVIDER", PromptAndProviderFixtures);
            RunCase(evidence, "P3A-005-STORAGE-RAG-EGRESS", StorageRagAndEgress);
            RunCase(evidence, "P3A-006-DIAGNOSTIC-REDACTION", DiagnosticRedaction);
            RunCase(evidence, "P3A-007-QUOTA-CONCURRENCY", QuotaAndConcurrency);
            TaskRequestCanonicalizerVectors.Run(evidence);
            Console.WriteLine("P3A-RUNTIME-SUMMARY PASS fixtures=" + evidence.Fixtures.Count + " vectors=11");
            return evidence;
        }

        private static void RunCase(RuntimeVerticalEvidence evidence, string fixtureId, Action<RuntimeVerticalEvidence> action)
        {
            action(evidence);
            evidence.Fixtures.Add(new Dictionary<string, object> { { "fixture_id", fixtureId }, { "expected", "pass" }, { "actual", "pass" }, { "failure_code", string.Empty } });
        }

        private static void LifecycleTaskAndReceipt(RuntimeVerticalEvidence evidence)
        {
            using (var fixture = RuntimeServiceComposition.CreateFixture("lifecycle"))
            using (var callerCancellation = new CancellationTokenSource())
            {
                var start = fixture.StartRuntime();
                AssertEx.True(start.IsSuccess, "Runtime service did not start.");
                AssertEx.Equal(RuntimeServiceState.Ready, start.Value.State, "Runtime service did not reach Ready.");
                var repeat = fixture.StartRuntime();
                AssertEx.True(repeat.IsSuccess, "Runtime service start was not idempotent.");
                AssertEx.Equal(start.Value.ConnectionEpoch, repeat.Value.ConnectionEpoch, "Idempotent start changed the connection epoch.");

                var request = fixture.CreateTask("lifecycle-task", "lifecycle-message", inputJson: "{\"topic\":\"world\"}", idempotencyKey: "lifecycle-idempotency");
                var submitted = fixture.Runtime.SubmitAsync(request, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                AssertEx.True(submitted.IsSuccess, "Runtime task submission failed.");
                AssertEx.True(fixture.Gateway.LastScope != null, "Runtime service did not bind an AI task scope.");
                AssertEx.Equal(request.IdempotencyKey, fixture.Gateway.LastScope.IdempotencyKey, "Task idempotency key was not propagated.");
                AssertEx.Equal(fixture.Context.Caller.Value, fixture.Gateway.LastScope.OwnerId, "Task owner was not propagated.");
                var duplicate = fixture.Runtime.SubmitAsync(request, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                AssertEx.True(duplicate.IsSuccess && ReferenceEquals(submitted.Value, duplicate.Value), "Duplicate task submission did not return the existing handle.");
                var handle = submitted.Value;
                var initialEvents = handle.Snapshot();
                AssertEx.Equal(2, initialEvents.Count, "The task did not emit accepted and started events.");
                AssertEx.Equal(AiTaskEventKind.Accepted, initialEvents[0].Kind, "The first task event was not Accepted.");
                AssertEx.Equal(AiTaskEventKind.Started, initialEvents[1].Kind, "The second task event was not Started.");
                AssertEx.True(fixture.Gateway.Complete(request.TaskId, "The world is changing.", 12, 18), "The fixture task did not complete.");
                AssertEx.True(!fixture.Gateway.Complete(request.TaskId, "duplicate", 12, 1), "A duplicate terminal event was accepted.");
                var finalEvents = handle.Snapshot();
                AssertEx.Equal(5, finalEvents.Count, "The task stream did not contain the expected events.");
                AssertEx.Equal(AiTaskEventKind.Completed, finalEvents[finalEvents.Count - 1].Kind, "The task did not end in Completed.");
                for (var index = 0; index < finalEvents.Count; index++) AssertEx.Equal(index + 1L, finalEvents[index].Sequence, "Task event sequence is not contiguous.");
                var receipt = fixture.Runtime.GetReceiptAsync(fixture.Gateway.LastScope, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                AssertEx.True(receipt.IsSuccess, "The completed task receipt was not available.");
                AssertEx.Equal("completed", receipt.Value.TerminalStatus, "The task receipt status was incorrect.");
                AssertEx.Equal(SettlementRequirement.NotApplicable, receipt.Value.SettlementRequirement, "P3A task unexpectedly required game settlement.");
                evidence.EventSequences.Add(new Dictionary<string, object> { { "task_id", request.TaskId }, { "events", finalEvents.Select(item => item.Kind.ToString()).ToArray() }, { "sequences", finalEvents.Select(item => item.Sequence).ToArray() } });
                evidence.TerminalReceipts.Add(new Dictionary<string, object> { { "task_id", receipt.Value.TaskId }, { "receipt_id", receipt.Value.ReceiptId }, { "terminal_status", receipt.Value.TerminalStatus }, { "request_payload_hash", receipt.Value.RequestPayloadHash }, { "settlement_requirement", "not_applicable" } });
                handle.Dispose();
                var drain = fixture.Runtime.BeginDrain(fixture.Context);
                AssertEx.True(drain.IsSuccess, "Runtime service did not enter draining.");
                AssertEx.True(fixture.Runtime.CompleteDrain(fixture.Context).IsSuccess, "Runtime service did not complete drain.");
            }
        }

        private static void SessionRouteAndBudget(RuntimeVerticalEvidence evidence)
        {
            using (var fixture = RuntimeServiceComposition.CreateFixture("validation"))
            using (var callerCancellation = new CancellationTokenSource())
            {
                AssertEx.True(fixture.StartRuntime().IsSuccess, "Validation fixture runtime did not start.");
                var unknownRoute = fixture.CreateTask("unknown-route", "unknown-route-message", providerId: "missing-provider");
                AssertEx.Error(fixture.Runtime.SubmitAsync(unknownRoute, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult(), "route.profile_unavailable", FrameworkErrorCategory.Unavailable);
                var inputBudget = fixture.CreateTask("input-budget", "input-budget-message", budget: new RuntimeResourceBudget(RuntimeService.MaximumInputBytes + 1, 1, 1, 1));
                AssertEx.Error(fixture.Runtime.SubmitAsync(inputBudget, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult(), "runtime.input_budget_exceeded", FrameworkErrorCategory.ResourceExhausted);
                var outputBudget = fixture.CreateTask("output-budget", "output-budget-message", budget: new RuntimeResourceBudget(1, RuntimeService.MaximumOutputBytes + 1, 1, 1));
                AssertEx.Error(fixture.Runtime.SubmitAsync(outputBudget, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult(), "runtime.output_budget_exceeded", FrameworkErrorCategory.ResourceExhausted);
                var tokenBudget = fixture.CreateTask("token-budget", "token-budget-message", budget: new RuntimeResourceBudget(1, 1, RuntimeService.MaximumTokens + 1, 1));
                AssertEx.Error(fixture.Runtime.SubmitAsync(tokenBudget, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult(), "runtime.token_budget_exceeded", FrameworkErrorCategory.ResourceExhausted);
                var deltaBudget = fixture.CreateTask("delta-budget", "delta-budget-message", budget: new RuntimeResourceBudget(1, 1, 1, RuntimeService.MaximumTextDeltas + 1));
                AssertEx.Error(fixture.Runtime.SubmitAsync(deltaBudget, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult(), "runtime.delta_budget_exceeded", FrameworkErrorCategory.ResourceExhausted);
                var invalidJson = fixture.CreateTask("invalid-json", "invalid-json-message", inputJson: "{");
                AssertEx.Error(fixture.Runtime.SubmitAsync(invalidJson, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult(), "canonical_json_unexpected_end", FrameworkErrorCategory.InvalidRequest);
                var expired = new RequestContext(fixture.Caller, fixture.Lease, "corr-expired", FixtureLifecycleDriver.FixtureNow.AddMinutes(-1));
                AssertEx.Error(fixture.Runtime.SubmitAsync(fixture.CreateTask("expired", "expired-message"), expired, callerCancellation.Token).GetAwaiter().GetResult(), "runtime.deadline_expired", FrameworkErrorCategory.Expired);
                AssertEx.Error(fixture.Runtime.SubmitAsync(fixture.CreateTask("missing-token", "missing-token-message"), fixture.Context, CancellationToken.None).GetAwaiter().GetResult(), "runtime.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest);
                evidence.BudgetAssertions.Add(new Dictionary<string, object> { { "input_bytes", RuntimeService.MaximumInputBytes }, { "output_bytes", RuntimeService.MaximumOutputBytes }, { "tokens", RuntimeService.MaximumTokens }, { "text_deltas", RuntimeService.MaximumTextDeltas }, { "route_failure", "route.profile_unavailable" }, { "stale_failure", "runtime.deadline_expired" } });
            }
        }

        private static void StreamingCancellation(RuntimeVerticalEvidence evidence)
        {
            using (var fixture = RuntimeServiceComposition.CreateFixture("cancel"))
            using (var callerCancellation = new CancellationTokenSource())
            {
                AssertEx.True(fixture.StartRuntime().IsSuccess, "Cancellation fixture runtime did not start.");
                var request = fixture.CreateTask("cancel-task", "cancel-message", idempotencyKey: "cancel-idempotency");
                var submitted = fixture.Runtime.SubmitAsync(request, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                AssertEx.True(submitted.IsSuccess, "Cancellation task submission failed.");
                AssertEx.True(fixture.Gateway.Cancel(request.TaskId, callerCancellation.Token).GetAwaiter().GetResult().IsSuccess, "Cancellation was not accepted.");
                var events = submitted.Value.Snapshot();
                AssertEx.Equal(AiTaskEventKind.Cancelled, events[events.Count - 1].Kind, "The task did not end in Cancelled.");
                AssertEx.True(!fixture.Gateway.Complete(request.TaskId, "late", 1, 1), "A completion raced past a cancelled terminal.");
                var receipt = fixture.Runtime.GetReceiptAsync(fixture.Gateway.LastScope, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                AssertEx.True(receipt.IsSuccess, "Cancelled task receipt was not available.");
                AssertEx.Equal("cancelled", receipt.Value.TerminalStatus, "Cancelled task receipt status was incorrect.");
                evidence.EventSequences.Add(new Dictionary<string, object> { { "task_id", request.TaskId }, { "events", events.Select(item => item.Kind.ToString()).ToArray() }, { "terminal", "cancelled" } });
                evidence.TerminalReceipts.Add(new Dictionary<string, object> { { "task_id", receipt.Value.TaskId }, { "receipt_id", receipt.Value.ReceiptId }, { "terminal_status", receipt.Value.TerminalStatus }, { "settlement_requirement", "not_applicable" } });
                submitted.Value.Dispose();
            }
        }

        private static void PromptAndProviderFixtures(RuntimeVerticalEvidence evidence)
        {
            using (var fixture = RuntimeServiceComposition.CreateFixture("prompt-provider"))
            using (var callerCancellation = new CancellationTokenSource())
            {
                AssertEx.True(fixture.StartRuntime().IsSuccess, "Prompt fixture runtime did not start.");
                var definition = new PromptDefinition("npc.reply", "1", "1", "awake.runtime.vertical", "text", "Speak to {{name}}.", new[] { "name" }, "npc.reply", "{\"answer\":\"string\"}", new string[0], "dialogue", "zh-CN", false);
                AssertEx.True(fixture.Prompts.RegisterAsync(definition, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult().IsSuccess, "Prompt registration failed.");
                var compiled = fixture.Prompts.CompileAsync(new PromptCompileRequest("npc.reply", "1", "1", new Dictionary<string, string> { { "name", "the captain" } }), fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                AssertEx.True(compiled.IsSuccess, "Prompt compilation failed.");
                AssertEx.Equal("Speak to the captain.", compiled.Value.CompiledText, "Prompt variable substitution was incorrect.");
                AssertEx.Error(fixture.Prompts.CompileAsync(new PromptCompileRequest("npc.reply", "1", "1", new Dictionary<string, string>()), fixture.Context, callerCancellation.Token).GetAwaiter().GetResult(), "prompt.variable_missing", FrameworkErrorCategory.InvalidRequest);
                var validOutput = fixture.Prompts.ValidateOutputAsync("npc.reply", "{\"n\":1,\"answer\":\"ok\"}", fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                AssertEx.True(validOutput.IsSuccess, "Valid structured output was rejected.");
                AssertEx.Equal("{\"answer\":\"ok\",\"n\":1}", validOutput.Value.PayloadJson, "Structured output was not canonicalized.");
                var invalidOutput = fixture.Prompts.ValidateOutputAsync("npc.reply", "{", fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                AssertEx.Error(invalidOutput, "prompt.output_invalid", FrameworkErrorCategory.InvalidRequest);
                AssertEx.True(invalidOutput.Error.Details.ContainsKey("raw_output_fingerprint") && !invalidOutput.Error.Details.ContainsKey("raw_output"), "Invalid output diagnostics were not redacted.");

                var request = fixture.CreateTask("provider-task", "provider-message", inputJson: "{\"topic\":\"war\"}");
                var translator = new FixtureProviderTranslator();
                var openAi = translator.Translate(request, fixture.Routes.Resolve("dialogue", "openai-compatible", "default", fixture.Context).Value);
                var anthropic = translator.Translate(request, fixture.Routes.Resolve("dialogue", "anthropic", "default", fixture.Context).Value);
                var ollama = translator.Translate(request, fixture.Routes.Resolve("dialogue", "ollama", "default", fixture.Context).Value);
                AssertEx.Equal("{\"messages\":[{\"content\":{\"topic\":\"war\"},\"role\":\"user\"}],\"model\":\"fixture-model\"}", openAi.BodyJson, "OpenAI-compatible fixture translation changed.");
                AssertEx.Equal("{\"max_tokens\":512,\"messages\":[{\"content\":{\"topic\":\"war\"},\"role\":\"user\"}],\"model\":\"fixture-model\"}", anthropic.BodyJson, "Anthropic fixture translation changed.");
                AssertEx.Equal("{\"model\":\"fixture-model\",\"prompt\":{\"topic\":\"war\"},\"stream\":true}", ollama.BodyJson, "Ollama fixture translation changed.");
                var capabilities = new FixtureProviderCapabilityCatalog();
                AssertEx.True(capabilities.Resolve("openai-compatible", "text").Available, "OpenAI-compatible capability fixture was unavailable.");
                AssertEx.True(!capabilities.Resolve("player2", "text").Available, "Unavailable provider fixture was incorrectly available.");
            }
        }

        private static void StorageRagAndEgress(RuntimeVerticalEvidence evidence)
        {
            using (var fixture = RuntimeServiceComposition.CreateFixture("storage-rag"))
            using (var callerCancellation = new CancellationTokenSource())
            {
                AssertEx.True(fixture.StartRuntime().IsSuccess, "Storage fixture runtime did not start.");
                var store = fixture.Storage.OpenSessionNamespaceAsync("runtime-test", fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                AssertEx.True(store.IsSuccess, "Session storage namespace did not open.");
                AssertEx.True(store.Value.SetAsync("key", "value", fixture.Context, callerCancellation.Token).GetAwaiter().GetResult().IsSuccess, "Session storage write failed.");
                AssertEx.Equal("value", store.Value.GetAsync("key", fixture.Context, callerCancellation.Token).GetAwaiter().GetResult().Value, "Session storage read returned the wrong value.");

                var documents = new List<RagDocument>();
                for (var index = 0; index < 10; index++) documents.Add(new RagDocument("doc-" + index, "world knowledge item " + index, "source-" + index, "public", "world_knowledge", "corpus-a", FixtureLifecycleDriver.FixtureNow));
                var ingest = fixture.Rag.IngestAsync(new RagIngestRequest("worldbook.base", "corpus-a-v1", documents), fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                AssertEx.True(ingest.IsSuccess && ingest.Value == 10, "RAG ingest did not replace the corpus atomically.");
                var searchRequest = new RagSearchRequest("worldbook.base", "corpus-a-v1", "world knowledge", new[] { "public" }, 64, RetrievalMode.Keyword, fixture.Caller.Value, fixture.Reference.CampaignGuid, fixture.Reference.TimelineId, fixture.Reference.SessionId, "profile-v1", new[] { "grant-b", "grant-a" });
                var hits = fixture.Rag.SearchAsync(searchRequest, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                AssertEx.True(hits.IsSuccess, "Keyword RAG search failed.");
                AssertEx.True(hits.Value.Count <= 8, "RAG search exceeded the chunk bound.");
                AssertEx.True(hits.Value.Sum(item => System.Text.Encoding.UTF8.GetByteCount(item.Text)) <= 16384, "RAG search exceeded the byte bound.");
                var stale = new RagSearchRequest("worldbook.base", "corpus-a-old", "world", new[] { "public" }, 1, RetrievalMode.Keyword, fixture.Caller.Value, fixture.Reference.CampaignGuid, fixture.Reference.TimelineId, fixture.Reference.SessionId, "profile-v1", new[] { "grant-a" });
                AssertEx.Error(fixture.Rag.SearchAsync(stale, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult(), "rag_index_stale", FrameworkErrorCategory.Conflict);
                var semantic = new RagSearchRequest("worldbook.base", "corpus-a-v1", "world", new[] { "public" }, 1, RetrievalMode.Semantic, fixture.Caller.Value, fixture.Reference.CampaignGuid, fixture.Reference.TimelineId, fixture.Reference.SessionId, "profile-v1", new[] { "grant-a" });
                AssertEx.Error(fixture.Rag.SearchAsync(semantic, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult(), "rag.unverified", FrameworkErrorCategory.Unsupported);
                var egress = TaskRequestCanonicalizer.ForEgress(new EgressCanonicalInput("dialogue", "openai-compatible", "default", "{\"b\":2,\"a\":1}", new[] { "field-b", "field-a", "field-a" }, new[] { "grant-b", "grant-a" }, new[] { "archive-2", "archive-1" }, new[] { "entry-2", "entry-1" }, new[] { "api.example", "api.example" }));
                AssertEx.True(egress.IsSuccess, "Egress canonicalization failed.");
                AssertEx.True(egress.Value.CanonicalJson.IndexOf("field-a", StringComparison.Ordinal) < egress.Value.CanonicalJson.IndexOf("field-b", StringComparison.Ordinal), "Egress set-like fields were not sorted.");
                var scope = new AiTaskScope("policy-task", "policy-message", fixture.Caller.Value, fixture.Reference, fixture.Lease.Generation, fixture.Context.CorrelationId, fixture.Context.CorrelationId, "dialogue", "openai-compatible", "default", "policy-idempotency", "task-hash", new SchemaRef("npc.reply", new ApiVersion(1, 0)), SettlementRequirement.NotApplicable);
                var policy = new FixtureEgressPolicy();
                var decision = policy.Evaluate(new EgressPolicyRequest("public", "dialogue", "openai-compatible", "default", egress.Value.Hash, new[] { "field-a" }, new[] { "grant-a" }, new[] { "api.example" }), fixture.Context);
                AssertEx.True(decision.IsSuccess && decision.Value.Allowed, "Allowed egress policy was denied.");
                AssertEx.True(policy.Consume(decision.Value, scope, fixture.Context).IsSuccess, "Allowed egress policy was not consumed.");
                var denied = policy.Evaluate(new EgressPolicyRequest("secret", "dialogue", "openai-compatible", "default", egress.Value.Hash, new string[0], new string[0], new string[0]), fixture.Context);
                AssertEx.True(denied.IsSuccess && !denied.Value.Allowed, "Secret egress policy was allowed.");
            }
        }

        private static void DiagnosticRedaction(RuntimeVerticalEvidence evidence)
        {
            var fields = new Dictionary<string, string>
            {
                { "task_id", "task-safe" },
                { "resolved_model", "fixture-model" },
                { "raw_prompt", "prompt-secret" },
                { "raw_model_output", "model-secret" },
                { "api_key", "key-secret" },
                { "authorization_header", "Bearer secret" },
                { "raw_provider_payload", "payload-secret" },
                { "raw_hero_id", "hero-secret" },
                { "filesystem_path", "C:\\private\\file" },
                { "credential_reference", "credential-secret" },
                { "identity_permission", "profile.secret" }
            };
            var redacted = RuntimeDiagnosticRedactor.Redact(fields).Fields;
            var omitted = new[] { "raw_prompt", "raw_model_output", "api_key", "authorization_header", "raw_provider_payload", "raw_hero_id", "filesystem_path" };
            for (var index = 0; index < omitted.Length; index++) AssertEx.True(!redacted.ContainsKey(omitted[index]), "A forbidden diagnostic field was not omitted: " + omitted[index]);
            AssertEx.Equal(RuntimeDiagnosticRedactor.RedactedMarker, redacted["credential_reference"], "Credential marker was not applied.");
            AssertEx.Equal(RuntimeDiagnosticRedactor.RedactedMarker, redacted["identity_permission"], "Identity permission marker was not applied.");
            AssertEx.Equal("fixture-model", redacted["resolved_model"], "Safe diagnostic field was lost.");
            evidence.RedactionAssertions.Add(new Dictionary<string, object> { { "field", "raw_prompt" }, { "expected_mode", "omitted" }, { "actual_mode", "omitted" }, { "forbidden_value_present", false }, { "passed", true } });
            evidence.RedactionAssertions.Add(new Dictionary<string, object> { { "field", "credential_reference" }, { "expected_mode", "marker" }, { "actual_mode", "marker" }, { "forbidden_value_present", false }, { "passed", true } });
        }

        private static void QuotaAndConcurrency(RuntimeVerticalEvidence evidence)
        {
            using (var fixture = RuntimeServiceComposition.CreateFixture("quota"))
            using (var callerCancellation = new CancellationTokenSource())
            {
                AssertEx.True(fixture.StartRuntime().IsSuccess, "Quota fixture runtime did not start.");
                var handles = new List<IAiTaskHandle>();
                for (var index = 0; index < RuntimeService.MaximumConcurrentTasksPerOwner; index++)
                {
                    var request = fixture.CreateTask("quota-task-" + index, "quota-message-" + index, idempotencyKey: "quota-idempotency-" + index);
                    var submitted = fixture.Runtime.SubmitAsync(request, fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                    AssertEx.True(submitted.IsSuccess, "The owner quota rejected an allowed task.");
                    handles.Add(submitted.Value);
                }
                var rejected = fixture.Runtime.SubmitAsync(fixture.CreateTask("quota-task-over", "quota-message-over", idempotencyKey: "quota-idempotency-over"), fixture.Context, callerCancellation.Token).GetAwaiter().GetResult();
                AssertEx.Error(rejected, "runtime.quota_exhausted", FrameworkErrorCategory.ResourceExhausted);
                for (var index = 0; index < handles.Count; index++) handles[index].Dispose();
                AssertEx.Equal(0, fixture.Runtime.ActiveTaskCount, "Disposing task handles did not release the owner quota.");
                evidence.BudgetAssertions.Add(new Dictionary<string, object> { { "concurrent_tasks_per_owner", RuntimeService.MaximumConcurrentTasksPerOwner }, { "over_limit_error", "runtime.quota_exhausted" }, { "released_after_dispose", true } });
            }
        }
    }
}
