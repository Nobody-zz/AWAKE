using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http.Json;
using PersonaWorkbench.Core;
using PersonaWorkbench.Web;

List<string> failures = new List<string>();
Run("allows only loopback listeners", AllowsOnlyLoopbackListeners);
Run("returns the unified workstation health shape", ReturnsTheUnifiedWorkstationHealthShape);
Run("serves identical health responses on both routes", ServesIdenticalHealthResponsesOnBothRoutes);
Run("provides a restrictive browser policy", ProvidesRestrictiveBrowserPolicy);
Run("builds a read-only DSL preview", BuildsReadOnlyDslPreview);
Run("surfaces preview compression diagnostics", SurfacesPreviewCompressionDiagnostics);
Run("exchanges a bootstrap token only once", ExchangesBootstrapTokenOnlyOnce);
Run("issues fresh bootstrap tokens for repeated launches", IssuesFreshBootstrapTokensForRepeatedLaunches);
Run("bounds consumed bootstrap token bookkeeping", BoundsConsumedBootstrapTokenBookkeeping);
Run("requires session and CSRF values", RequiresSessionAndCsrfValues);
Run("renews active workbench sessions", RenewsActiveWorkbenchSessions);
Run("authoring routes require session and csrf", AuthoringRoutesRequireSessionAndCsrf);
Run("authoring route handoff is idempotent and fenced", AuthoringRouteHandoffIsIdempotentAndFenced);
Run("rejects invalid cross-workstation handoff context", RejectsInvalidCrossWorkstationHandoffContext);
Run("runs authoring routes in an in-process TestHost", RunsAuthoringRoutesInProcessTestHost);
Run("expired session rejects late authoring response", ExpiredSessionRejectsLateAuthoringResponse);
Run("rejects document names outside the workspace", RejectsDocumentNamesOutsideWorkspace);
Run("normalizes workspace path and file failures", NormalizesWorkspacePathAndFileFailures);
Run("saves and reloads a free persona document", SavesAndReloadsFreePersonaDocument);
Run("rejects invalid documents before persistence", RejectsInvalidDocumentsBeforePersistence);
Run("rejects approval when DSL cannot fit without mutation", RejectsApprovalWhenDslCannotFitWithoutMutation);
Run("preserves document metadata across direct round trip", PreservesDocumentMetadataAcrossDirectRoundTrip);
Run("exposes structured document HTTP status mapping", ExposesStructuredDocumentHttpStatusMapping);
Run("guards browser document epochs and metadata", GuardsBrowserDocumentEpochsAndMetadata);
Run("requires explicit local approval", RequiresExplicitLocalApproval);
Run("accepts only safe provider endpoints", AcceptsOnlySafeProviderEndpoints);
Run("rejects provider redirects", RejectsProviderRedirects);
Run("limits provider requests and honors cooldown", LimitsProviderRequestsAndHonorsCooldown);
Run("expands character prose without persona JSON", ExpandsCharacterProseWithoutPersonaJson);
Run("expansion prompt drafts observable behavior without inventing biography", ExpansionPromptDraftsObservableBehaviorWithoutInventingBiography);
Run("preserves multi-paragraph expansion input", PreservesMultiParagraphExpansionInput);
Run("normalizes expansion controls deterministically", NormalizesExpansionControlsDeterministically);
Run("counts expansion control limits by Unicode scalar", CountsExpansionControlLimitsByUnicodeScalar);
Run("normalizes expansion envelope line endings", NormalizesExpansionEnvelopeLineEndings);
Run("rejects invalid expansion controls before HTTP dispatch", RejectsInvalidExpansionControlsBeforeHttpDispatch);
Run("serializes expansion controls in the canonical envelope", SerializesExpansionControlsInCanonicalEnvelope);
Run("keeps legacy expansion requests balanced by default", KeepsLegacyExpansionRequestsBalancedByDefault);
Run("reports truncated and malformed expansion responses", ReportsTruncatedAndMalformedExpansionResponses);
Run("routes expansion controls through provider action service", RoutesExpansionControlsThroughProviderActionService);
Run("rejects invalid action controls without quarantine", RejectsInvalidActionControlsWithoutQuarantine);
Run("blocks cross-action provider operations and releases the gate", BlocksCrossActionProviderOperationsAndReleasesTheGate);
Run("rejects unchanged or structured expansion output", RejectsUnchangedOrStructuredExpansionOutput);
Run("rejects expansion that drops quoted source anchors", RejectsExpansionThatDropsQuotedSourceAnchors);
Run("converts confirmed text through the complete Persona Provider draft chain", ConvertsConfirmedTextThroughCompletePersonaProviderDraftChain);
Run("draft prompt constrains the three author arrays", DraftPromptConstrainsAuthorArrays);
Run("enriches missing axes from grounded source evidence", EnrichesMissingAxesFromGroundedSourceEvidence);
Run("enriches missing reaction and exchange evidence", EnrichesMissingReactionAndExchangeEvidence);
Run("routes named DSL conversion through the sparse Provider client", RoutesNamedDslConversionThroughSparseProviderClient);
Run("conversion prompt preserves contradictions without changing its sparse contract", ConversionPromptPreservesContradictionsWithoutChangingItsSparseContract);
Run("generates missing local Persona identity from the Provider draft", GeneratesMissingLocalPersonaIdentityFromProviderDraft);
Run("converts a real Provider HTTP candidate into canonical DSL", ConvertsRealProviderHttpCandidateIntoCanonicalDsl);
Run("rejects DSL conversion fallback success on Provider failure", RejectsDslConversionFallbackSuccessOnProviderFailure);
Run("rejects core-only Persona drafts as unstructured", RejectsCoreOnlyPersonaDraftsAsUnstructured);
Run("requests small intermediate JSON without local metadata", RequestsSmallIntermediateJsonWithoutLocalMetadata);
Run("accepts wrapped intermediate JSON content", AcceptsWrappedIntermediateJsonContent);Run("accepts string-encoded intermediate JSON content", AcceptsStringEncodedIntermediateJsonContent);Run("reports missing intermediate JSON objects distinctly", ReportsMissingIntermediateJsonObjectsDistinctly);
Run("reports truncated intermediate candidates distinctly", ReportsTruncatedIntermediateCandidatesDistinctly);
Run("parses structured provider draft candidates", ParsesStructuredProviderDraftCandidates);
Run("reports invalid Provider core distinctly", ReportsInvalidProviderCoreDistinctly);
Run("reports invalid Provider identity distinctly", ReportsInvalidProviderIdentityDistinctly);
Run("includes a JSON instruction in provider requests", IncludesJsonInstructionInProviderRequests);
Run("requires exact source excerpts in sparse conversion prompt", RequiresExactSourceExcerptsInSparseConversionPrompt);
Run("disables thinking for official DeepSeek drafts", DisablesThinkingForOfficialDeepSeekDrafts);
Run("reports empty provider candidates distinctly", ReportsEmptyProviderCandidatesDistinctly);
Run("accepts fenced provider JSON", AcceptsFencedProviderJson);
Run("assigns local IDs to provider drafts", AssignsLocalIdsToProviderDrafts);
Run("normalizes provider multiline text", NormalizesProviderMultilineText);
Run("accepts numeric-string and integral-decimal Provider axes", AcceptsNumericStringAndIntegralDecimalProviderAxes);
Run("prunes one invalid Provider axis without discarding the draft", PrunesOneInvalidProviderAxisWithoutDiscardingDraft);
Run("normalizes profile text arrays", NormalizesProfileTextArrays);
Run("drops unproven provider fields", DropsUnprovenProviderFields);
Run("prunes fields whose Provider evidence is not in the source", PrunesFieldsWhoseProviderEvidenceIsNotInSource);
Run("rejects drafts when invalid evidence removes all Persona structure", RejectsDraftsWhenInvalidEvidenceRemovesAllPersonaStructure);
Run("rejects provider axes without source evidence", RejectsProviderAxesWithoutSourceEvidence);
Run("rejects verbatim provider prompt copies", RejectsVerbatimProviderPromptCopies);
Run("rejects unknown provider profile fields", RejectsUnknownProviderProfileFields);
Run("rejects provider authored fields without evidence", RejectsProviderAuthoredFieldsWithoutEvidence);
Run("keeps provider approval metadata local", KeepsProviderApprovalMetadataLocal);
Run("persists provider keys only as protected data", PersistsProviderKeysOnlyAsProtectedData);
Run("fails closed when secret protection is unavailable", FailsClosedWhenSecretProtectionIsUnavailable);
Run("redacts provider secrets from diagnostic text", RedactsProviderSecretsFromDiagnosticText);
Run("keeps transient provider keys in memory only", KeepsTransientProviderKeysInMemoryOnly);
Run("round trips a Windows DPAPI test secret", RoundTripsWindowsDpapiTestSecret);
Run("requires explicit confirmation before cloud provider generation", RequiresExplicitCloudConfirmationBeforeProviderGeneration);
Run("allows loopback provider generation without cloud confirmation", AllowsLoopbackProviderGenerationWithoutCloudConfirmation);
Run("does not call a provider without a transient key", DoesNotCallProviderWithoutTransientKey);
Run("allows keyless loopback provider generation", AllowsKeylessLoopbackProviderGeneration);
Run("routes text expansion through provider session controls", RoutesTextExpansionThroughProviderSessionControls);
Run("captures the actual Provider request body", CapturesActualProviderRequestBody);
Run("rejects invalid diagnostic capture stages", RejectsInvalidDiagnosticCaptureStages);
Run("isolates failed provider results from editable drafts", IsolatesFailedProviderResultsFromEditableDrafts);
Run("sets and clears only the provider session key", SetsAndClearsOnlyTheProviderSessionKey);
Run("rejects cloud endpoints that resolve to private addresses", RejectsCloudEndpointsThatResolveToPrivateAddresses);
Run("keeps provider settings transient in the browser", KeepsProviderSettingsTransientInTheBrowser);
Run("surfaces provider cooldown metadata without retrying", SurfacesProviderCooldownMetadataWithoutRetrying);
Run("reports a bounded provider timeout", ReportsABoundedProviderTimeout);
Run("reports user cancellation distinctly from timeout", ReportsUserCancellationDistinctlyFromTimeout);
Run("contains provider resolver failures", ContainsProviderResolverFailures);
Run("bounds quarantined provider failures", BoundsQuarantinedProviderFailures);
Run("quarantines cancellation without promoting a draft", QuarantinesCancellationWithoutPromotingADraft);
Run("renders provider resilience controls without unsafe HTML", RendersProviderResilienceControlsWithoutUnsafeHtml);
Run("provides an automated browser smoke gate", ProvidesAutomatedBrowserSmokeGate);
Run("provides a safe free preview launch and package gate", ProvidesSafeFreePreviewLaunchAndPackageGate);
Run("exposes explicit local approval workflow", ExposesExplicitLocalApprovalWorkflow);
Run("presents beginner persona tags in guided order", PresentsBeginnerPersonaTagsInGuidedOrder);
Run("puts free description before manual editing", PutsFreeDescriptionBeforeManualEditing);
Run("exposes facet strength controls", ExposesFacetStrengthControls);
Run("exposes disposition axis controls", ExposesDispositionAxisControls);
Run("exposes reaction and commitment profile controls", ExposesReactionAndCommitmentProfileControls);
Run("explains provider endpoint setup", ExplainsProviderEndpointSetup);
Run("keeps AI expansion isolated from persona fields", KeepsAiExpansionIsolatedFromPersonaFields);
Run("keeps DSL conversion separate from local preview", KeepsDslConversionSeparateFromLocalPreview);
Run("renders expansion guidance controls in beginner order", RendersExpansionGuidanceControlsInBeginnerOrder);
Run("serializes expansion guidance without persona pollution", SerializesExpansionGuidanceWithoutPersonaPollution);
Run("uses one browser provider operation lease", UsesOneBrowserProviderOperationLease);
Run("accepts only consistent bounded Provider usage", AcceptsOnlyConsistentBoundedProviderUsage);
Run("ignores malformed Provider usage", IgnoresMalformedProviderUsage);
Run("preserves usage on invalid sparse responses", PreservesUsageOnInvalidSparseResponses);
Run("preserves usage on invalid expansion responses", PreservesUsageOnInvalidExpansionResponses);
Run("preserves usage when sparse candidate parsing fails", PreservesUsageWhenSparseCandidateParsingFails);
Run("preserves oversized DSL request rejection", PreservesOversizedDslRequestRejection);
Run("uses Ollama low thinking and byte budgets", UsesOllamaLowThinkingAndByteBudgets);
Run("does not send think for compatible local providers", DoesNotSendThinkForCompatibleLocalProviders);
Run("parses nested reaction and commitment objects", ParsesNestedReactionAndCommitmentObjects);
Run("builds an AWAKE authoring preview without Provider", BuildsAwakeAuthoringPreviewWithoutProvider);
Run("keeps authoring preview failures non-destructive", KeepsAuthoringPreviewFailuresNonDestructive);
Run("exposes AWAKE authoring preview and download controls", ExposesAwakeAuthoringPreviewAndDownloadControls);
Run("rejects string reaction and commitment replacements", RejectsStringReactionAndCommitmentReplacements);
Run("normalizes evidence line endings without changing document source", NormalizesEvidenceWithoutChangingDocumentSource);
Run("reports running batch progress without waiting for completion", ReportsRunningBatchProgressWithoutWaitingForCompletion);
Run("rejects duplicate batch source ordinals", RejectsDuplicateBatchSourceOrdinals);
Run("preserves successful results when a batch item fails", PreservesSuccessfulResultsWhenBatchItemFails);
Run("cancels a batch before starting the next item", CancelsBatchBeforeStartingTheNextItem);
Run("keeps malformed batch candidates isolated", KeepsMalformedBatchCandidatesIsolated);
Run("assigns unique global ordinals to material segments", AssignsUniqueGlobalOrdinalsToMaterialSegments);

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine("PASS PersonaWorkbench.Web.Tests");
return 0;

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine("PASS " + name);
    }
    catch (Exception ex)
    {
        failures.Add("FAIL " + name + ": " + ex.Message);
    }
}

void AllowsOnlyLoopbackListeners()
{
    AssertTrue(WorkbenchWebPolicy.IsLoopback(IPAddress.Loopback), "127.0.0.1 must be allowed");
    AssertTrue(WorkbenchWebPolicy.IsLoopback(IPAddress.IPv6Loopback), "::1 must be allowed");
    AssertTrue(!WorkbenchWebPolicy.IsLoopback(IPAddress.Any), "0.0.0.0 must be rejected");
    AssertTrue(!WorkbenchWebPolicy.IsLoopback(IPAddress.Parse("192.168.1.5")), "LAN address must be rejected");
}

void ProvidesRestrictiveBrowserPolicy()
{
    string csp = WorkbenchWebPolicy.ContentSecurityPolicy;
    AssertTrue(csp.Contains("default-src 'self'", StringComparison.Ordinal), "CSP must restrict default sources");
    AssertTrue(csp.Contains("object-src 'none'", StringComparison.Ordinal), "CSP must disable plugins");
    AssertTrue(!csp.Contains("unsafe-inline", StringComparison.Ordinal), "CSP must not permit unsafe inline code");
    AssertEqual("no-referrer", WorkbenchWebPolicy.ReferrerPolicy, "referrer policy must not leak local state");
}

void PresentsBeginnerPersonaTagsInGuidedOrder()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    string[] requiredMarkers =
    {
        "id=\"tag-group-impression\"",
        "id=\"tag-group-speech\"",
        "id=\"tag-group-decisions\"",
        "id=\"tag-group-reactions\"",
        "id=\"tag-group-boundaries\""
    };

    int previousIndex = -1;
    foreach (string marker in requiredMarkers)
    {
        int currentIndex = html.IndexOf(marker, StringComparison.Ordinal);
        AssertTrue(currentIndex >= 0, "guided tag group is missing: " + marker);
        AssertTrue(currentIndex > previousIndex, "guided tag groups must follow beginner order");
        previousIndex = currentIndex;
    }

    AssertTrue(html.Contains("不懂标签也没关系", StringComparison.Ordinal), "tag helper must explain beginner usage");
    AssertTrue(html.Contains("data-tag-id=\"trigger.public_humiliation\"", StringComparison.Ordinal), "trigger tag must retain its stable ID");
}

void PutsFreeDescriptionBeforeManualEditing()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    int promptIndex = html.IndexOf("id=\"provider-prompt\"", StringComparison.Ordinal);
    int editorIndex = html.IndexOf("id=\"preview-form\"", StringComparison.Ordinal);
    int settingsIndex = html.IndexOf("id=\"provider-settings\"", StringComparison.Ordinal);

    AssertTrue(promptIndex >= 0 && editorIndex >= 0, "free description and manual editor must both exist");
    AssertTrue(promptIndex < editorIndex, "free description must appear before manual editing");
    AssertTrue(settingsIndex > promptIndex, "advanced provider settings must follow the primary prompt");
    AssertTrue(html.Contains("识别并生成 Persona", StringComparison.Ordinal), "primary action must be understandable to beginners");
    AssertTrue(html.Contains("扩展人物形象", StringComparison.Ordinal), "optional expansion action must remain visible");
    AssertTrue(html.Contains("<summary>AI 设置（高级）</summary>", StringComparison.Ordinal), "provider details must be collapsed for beginners");
    AssertTrue(html.Contains("AI 只负责扩充成可编辑人物文本", StringComparison.Ordinal), "provider guidance must state the narrow optional expansion role");
    AssertTrue(html.Contains("生成 DSL 仍是后续独立步骤", StringComparison.Ordinal), "provider guidance must separate expansion from DSL generation");
}

void KeepsAiExpansionIsolatedFromPersonaFields()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    string script = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));
    string program = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/Program.cs"));

    AssertTrue(html.Contains("id=\"expanded-description\"", StringComparison.Ordinal), "AI expansion must have a separate editable text area");
    AssertTrue(html.Contains("id=\"use-expanded-description\"", StringComparison.Ordinal), "adopting expanded text must require an explicit user action");
    AssertTrue(html.Contains("id=\"generate-provider-draft\"", StringComparison.Ordinal), "primary AI action must expose direct Persona generation");
    AssertTrue(script.Contains("/api/provider/expand-description", StringComparison.Ordinal), "optional expansion action must use the text expansion endpoint");
    AssertTrue(script.Contains("expandedDescriptionInput.value = payload.expandedText", StringComparison.Ordinal), "AI output must first land in the isolated expansion text area");
    int expansionStart = script.IndexOf("expandDescriptionButton.addEventListener", StringComparison.Ordinal);
    int expansionEnd = script.IndexOf("useExpandedDescriptionButton.addEventListener", StringComparison.Ordinal);
    AssertTrue(expansionStart >= 0 && expansionEnd > expansionStart, "expansion handler boundaries must remain detectable");
    string expansionHandler = script[expansionStart..expansionEnd];
    AssertTrue(!expansionHandler.Contains("applyEditableDocument(", StringComparison.Ordinal), "AI expansion must not write structured Persona fields");
    AssertTrue(script.Contains("applyEditableDocument(payload.draft, { preserveMetadata: true })", StringComparison.Ordinal), "structured Persona application must preserve local document metadata");
    AssertTrue(program.Contains("/api/provider/expand-description", StringComparison.Ordinal), "server must expose the authorized expansion endpoint");
}

void ReturnsTheUnifiedWorkstationHealthShape()
{
    WorkbenchHealth health = WorkbenchHealth.Create("pwb-instance-fixture", WorkbenchWebPolicy.DefaultPort);
    AssertTrue(health.Ok, "ready health must be ok");
    AssertEqual("AWAKE.PersonaWorkbench", health.Product, "health product must identify Persona Workbench");
    AssertEqual("persona_workbench", health.WorkstationId, "health workstation identity must be stable");
    AssertEqual("persona-workbench", health.WorkspaceId, "health workspace identity must be stable");
    AssertEqual("1", health.ProtocolVersion, "protocol_version must be a string");
    AssertEqual("1", health.LegacyProtocolVersion, "legacy protocolVersion must be a string");
    AssertEqual(health.InstanceId, health.LegacyInstanceId, "legacy instanceId must mirror instance_id");
    AssertTrue(health.LegacyWorkspaceHash.Length == 16, "legacy workspaceHash must be a 16-character prefix");
    AssertTrue(health.LegacyWorkspaceHash.All(Uri.IsHexDigit), "legacy workspaceHash must be hexadecimal");
    AssertEqual("ready", health.State, "health state must report readiness");
    AssertTrue(health.Port == WorkbenchWebPolicy.DefaultPort, "health must expose the bound port");

    string json = JsonSerializer.Serialize(health);
    AssertTrue(json.Contains("\"workstation_id\"", StringComparison.Ordinal), "health must use shared snake_case identity fields");
    AssertTrue(json.Contains("\"protocol_version\"", StringComparison.Ordinal), "health must expose protocol version");
    AssertTrue(json.Contains("\"workspace_id\"", StringComparison.Ordinal), "health must expose workspace identity");
    AssertTrue(json.Contains("\"protocolVersion\":\"1\"", StringComparison.Ordinal), "health must preserve the legacy string protocol field");
    AssertTrue(json.Contains("\"instanceId\"", StringComparison.Ordinal), "health must preserve the legacy instance field");
    AssertTrue(json.Contains("\"workspaceHash\"", StringComparison.Ordinal), "health must preserve the legacy workspace hash field");
    AssertTrue(!json.Contains("\"offline\"", StringComparison.Ordinal), "health must match the frozen unified field set");
}

void ServesIdenticalHealthResponsesOnBothRoutes()
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    WebApplication app = builder.Build();
    WorkbenchHealthRoutes.Map(
        app,
        () => WorkbenchHealth.Create("pwb-instance-health-test", WorkbenchWebPolicy.DefaultPort));
    app.StartAsync().GetAwaiter().GetResult();
    try
    {
        HttpClient client = app.GetTestClient();
        string rootHealth = client.GetStringAsync("/health").GetAwaiter().GetResult();
        string apiHealth = client.GetStringAsync("/api/health").GetAwaiter().GetResult();
        AssertEqual(rootHealth, apiHealth, "both health routes must return the same JSON contract");

        using JsonDocument parsed = JsonDocument.Parse(rootHealth);
        JsonElement root = parsed.RootElement;
        AssertEqual("persona_workbench", root.GetProperty("workstation_id").GetString() ?? string.Empty, "health route must expose the stable workstation identity");
        AssertEqual("1", root.GetProperty("protocol_version").GetString() ?? string.Empty, "health route must expose a string protocol version");
        AssertEqual("persona-workbench", root.GetProperty("workspace_id").GetString() ?? string.Empty, "health route must expose a stable workspace identity");
        AssertTrue(root.GetProperty("workspaceHash").GetString()?.Length == 16, "health route must expose the legacy workspace hash");
        AssertEqual("ready", root.GetProperty("state").GetString() ?? string.Empty, "health route must report ready");
    }
    finally
    {
        app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}

void CapturesActualProviderRequestBody()
{
    string capturePath = Path.Combine(Path.GetTempPath(), "pwb-capture-" + Guid.NewGuid().ToString("N") + ".jsonl");
    byte[]? dispatchedBody = null;
    try
    {
        using ProviderRequestCapture capture = new ProviderRequestCapture(capturePath);
        ProviderTextExpansionClient client = new ProviderTextExpansionClient(new DelegateHttpMessageHandler(async request =>
        {
            dispatchedBody = await request.Content!.ReadAsByteArrayAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"message\":{\"content\":\"角色。补充内容\",\"done_reason\":\"stop\"}}", System.Text.Encoding.UTF8, "application/json")
            };
        }), PublicProviderEndpointResolver(), TimeSpan.FromSeconds(5), capture);

        using (capture.BeginScope("expand-short"))
        {
            ProviderTextExpansionResult result = client.ExpandAsync(new ProviderTextExpansionRequest
            {
                Endpoint = "https://api.example.com/v1/chat/completions",
                Model = "test-model",
                Description = "角色。"
            }).GetAwaiter().GetResult();
            AssertTrue(result.Status == ProviderDraftStatus.Success, "captured expansion must still succeed");
        }

        AssertTrue(dispatchedBody != null && File.Exists(capturePath), "Provider dispatch and capture must both exist");
        string line = File.ReadAllLines(capturePath).Single();
        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement root = document.RootElement;
        byte[] capturedBody = Convert.FromBase64String(root.GetProperty("bodyBase64").GetString()!);
        AssertTrue(capturedBody.SequenceEqual(dispatchedBody!), "capture must contain the exact dispatched UTF-8 body");
        AssertEqual("expand-short", root.GetProperty("stage").GetString()!, "capture stage must be stable");
        AssertTrue(!line.Contains("Authorization", StringComparison.OrdinalIgnoreCase), "capture must not include Authorization");
        AssertTrue(!line.Contains("apiKey", StringComparison.OrdinalIgnoreCase), "capture must not include API keys");
    }
    finally
    {
        if (File.Exists(capturePath)) File.Delete(capturePath);
    }
}

void RejectsInvalidDiagnosticCaptureStages()
{
    string capturePath = Path.Combine(Path.GetTempPath(), "pwb-capture-" + Guid.NewGuid().ToString("N") + ".jsonl");
    try
    {
        using ProviderRequestCapture capture = new ProviderRequestCapture(capturePath);
        bool rejected = false;
        try { capture.BeginScope("invalid-stage"); }
        catch (ArgumentException) { rejected = true; }
        AssertTrue(rejected, "unknown diagnostic stages must be rejected");
    }
    finally
    {
        if (File.Exists(capturePath)) File.Delete(capturePath);
    }
}

void KeepsDslConversionSeparateFromLocalPreview()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    string script = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));
    string program = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/Program.cs"));
    AssertTrue(html.Contains("id=\"convert-expanded-to-dsl\"", StringComparison.Ordinal), "UI must expose a separate DSL conversion action");
    AssertTrue(script.Contains("/api/provider/convert-to-dsl", StringComparison.Ordinal), "browser must call the independent DSL conversion endpoint");
    AssertTrue(script.Contains("applyEditableDocument(payload.draft, { preserveMetadata: true })", StringComparison.Ordinal), "successful conversion must fill editable Persona fields while preserving local metadata");
    AssertTrue(!script.Contains("payload.usedLocalFallback", StringComparison.Ordinal), "conversion UI must not accept heuristic fallback success");
    AssertTrue(script.Contains("provider.persona_structure_empty", StringComparison.Ordinal), "conversion UI must explain empty structured Persona results");
    AssertTrue(!html.Contains("value=\"free.demo.sable\"", StringComparison.Ordinal) && !html.Contains("value=\"赛布尔\"", StringComparison.Ordinal), "new workbench sessions must not inherit the demo identity");
    AssertTrue(script.Contains("derivePersonaFileName", StringComparison.Ordinal), "successful name detection must generate a safe local file name");
    AssertTrue(!script.Contains("if (!localId || !localDisplayName)", StringComparison.Ordinal), "blank identity fields must be allowed until the Provider draft is parsed");
    AssertTrue(!html.Contains("pattern=\"[A-Za-z0-9._-]+\\.persona\\.json\"", StringComparison.Ordinal), "Unicode character names must be allowed in local Persona file names");
    AssertTrue(program.Contains("/api/provider/convert-to-dsl", StringComparison.Ordinal), "server must expose the independent DSL conversion endpoint");
}

void RendersExpansionGuidanceControlsInBeginnerOrder()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    int descriptionIndex = html.IndexOf("id=\"provider-prompt\"", StringComparison.Ordinal);
    int guidanceIndex = html.IndexOf("id=\"expansion-guidance\"", StringComparison.Ordinal);
    int actionIndex = html.IndexOf("id=\"generate-provider-draft\"", StringComparison.Ordinal);

    AssertTrue(descriptionIndex >= 0 && guidanceIndex > descriptionIndex && actionIndex > guidanceIndex, "optional guidance must follow the source and precede the primary action");
    AssertTrue(html.Contains("id=\"expansion-direction\"", StringComparison.Ordinal), "direction textarea is required");
    AssertTrue(html.Contains("id=\"expansion-focus-preset\"", StringComparison.Ordinal), "focus preset is required");
    AssertTrue(html.Contains("id=\"expansion-keyword-input\"", StringComparison.Ordinal), "keyword input is required");
    AssertTrue(html.Contains("id=\"add-expansion-keyword\"", StringComparison.Ordinal), "keyword add action is required");
    AssertTrue(html.Contains("id=\"expansion-keyword-list\"", StringComparison.Ordinal), "keyword edit list is required");
    AssertTrue(html.Contains("id=\"expansion-avoid-topics\"", StringComparison.Ordinal), "avoid topics input is required");
}

void SerializesExpansionGuidanceWithoutPersonaPollution()
{
    string script = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));
    AssertTrue(script.Contains("function expansionControls()", StringComparison.Ordinal), "browser must build an independent controls object");
    AssertTrue(script.Contains("controls: expansionControls()", StringComparison.Ordinal), "expansion request must send controls independently");
    AssertTrue(!script.Contains("sourceDescription: expansionDirection", StringComparison.Ordinal), "direction must never enter Persona source description");
    AssertTrue(!script.Contains("core: expansionDirection", StringComparison.Ordinal), "direction must never enter Persona core");
}

void UsesOneBrowserProviderOperationLease()
{
    string script = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));
    AssertTrue(script.Contains("async function runProviderOperation", StringComparison.Ordinal), "browser must expose one outer Provider operation lease");
    AssertTrue(script.Contains("provider.request_in_flight", StringComparison.Ordinal), "busy operations need the stable error code");
    AssertTrue(script.Contains("convertExpandedToDslButton.disabled = isGenerating", StringComparison.Ordinal), "DSL conversion must be disabled during Provider operations");
    AssertTrue(script.Contains("setProviderKeyButton.disabled = isGenerating", StringComparison.Ordinal), "key operations must be disabled during Provider operations");
    AssertTrue(script.Contains("confirmCloudProviderButton.disabled = isGenerating", StringComparison.Ordinal), "endpoint confirmation must be disabled during Provider operations");
}

void ExposesFacetStrengthControls()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    string script = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));
    AssertTrue(html.Contains("class=\"facet-strength\"", StringComparison.Ordinal), "UI must expose facet strength controls");
    AssertTrue(html.Contains("未设定", StringComparison.Ordinal) && html.Contains("核心特征", StringComparison.Ordinal), "UI must expose unset and defining strength labels");
    AssertTrue(script.Contains("selectedFacetStrengths", StringComparison.Ordinal) && script.Contains("facetStrengths", StringComparison.Ordinal), "browser document payload must carry facet strengths");
}

void ExposesDispositionAxisControls()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    string script = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));

    AssertTrue(html.Contains("id=\"trait-caution\"", StringComparison.Ordinal), "trait group must expose a bipolar caution axis");
    AssertTrue(html.Contains("id=\"expression-directness\"", StringComparison.Ordinal), "expression group must expose a bipolar directness axis");
    AssertTrue(html.Contains("id=\"behavior-leadership\"", StringComparison.Ordinal), "behavior group must expose a bipolar leadership axis");
    AssertTrue(html.Contains("id=\"legacy-facet-controls\"", StringComparison.Ordinal), "legacy facet strengths must remain available in a compatibility section");
    AssertTrue(script.Contains("traitProfile: traitProfile()", StringComparison.Ordinal), "browser payload must include the trait profile");
    AssertTrue(script.Contains("expressionProfile: expressionProfile()", StringComparison.Ordinal), "browser payload must include the expression profile");
    AssertTrue(script.Contains("behaviorProfile: behaviorProfile()", StringComparison.Ordinal), "browser payload must include the behavior profile");
    AssertTrue(script.Contains("setAxis(\"#trait-caution\"", StringComparison.Ordinal), "browser load must restore trait axes");
}

void ExposesReactionAndCommitmentProfileControls()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    string script = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));

    AssertTrue(html.Contains("id=\"reaction-confrontation\"", StringComparison.Ordinal), "reaction profile must expose a confrontation axis");
    AssertTrue(html.Contains("id=\"reaction-support-seeking\"", StringComparison.Ordinal), "reaction profile must expose a support-seeking axis");
    AssertTrue(html.Contains("id=\"commitment-value-tradeability\"", StringComparison.Ordinal), "commitment profile must expose a value-tradeability axis");
    AssertTrue(html.Contains("id=\"commitment-exception-cost\"", StringComparison.Ordinal), "commitment profile must expose structured exception cost text");
    AssertTrue(html.Contains("具体条件与旧格式兼容", StringComparison.Ordinal), "legacy trigger tags must remain available outside the primary axis UI");
    AssertTrue(script.Contains("reactionProfile: reactionProfile()", StringComparison.Ordinal), "browser payload must include reaction profile data");
    AssertTrue(script.Contains("commitmentProfile: commitmentProfile()", StringComparison.Ordinal), "browser payload must include commitment profile data");
    AssertTrue(script.Contains("value === null || value === undefined", StringComparison.Ordinal), "browser load must preserve explicit zero axis values");
}

void ExplainsProviderEndpointSetup()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    string script = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));

    AssertTrue(html.Contains("Chat Completions 完整接口地址", StringComparison.Ordinal), "provider UI must name the full endpoint field");
    AssertTrue(html.Contains("模型 ID", StringComparison.Ordinal), "provider UI must explain model IDs");
    AssertTrue(html.Contains("本机 Provider", StringComparison.Ordinal), "provider UI must explain local mode");
    AssertTrue(html.Contains("id=\"provider-protocol\"", StringComparison.Ordinal), "provider UI must expose the local protocol choice");
    AssertTrue(html.Contains("我确认使用这个云端地址", StringComparison.Ordinal), "cloud confirmation must be explicit");
    AssertTrue(script.Contains("classifyProviderEndpoint", StringComparison.Ordinal), "provider UI must classify local and HTTPS endpoints");
    AssertTrue(script.Contains("confirmCloudProviderButton.disabled", StringComparison.Ordinal), "local endpoints must not expose an active cloud confirmation action");
}

void BuildsReadOnlyDslPreview()
{
    PersonaPreviewResponse response = PersonaPreviewService.Build(new PersonaPreviewRequest
    {
        Id = "free.preview.sable",
        Core = "她不急于表态。",
        IdentityFacts = "自由旅行者。",
        Tags = new List<string> { "expression.measured", "trait.cautious" },
        TraitProfile = new PersonaTraitProfile { Caution = 1 },
        ExpressionProfile = new PersonaExpressionProfile { Directness = 0 },
        BehaviorProfile = new PersonaBehaviorProfile { Leadership = 2 },
        ReactionProfile = new PersonaReactionProfile
        {
            Confrontation = 1,
            SensitiveConditions = "被要求立即站队。"
        },
        CommitmentProfile = new PersonaCommitmentProfile
        {
            PromiseCaution = 2,
            PriorityOrder = "长期承诺 > 眼前利益"
        }
    });

    AssertTrue(response.IsValid, "valid preview request must succeed");
    AssertTrue(response.Dsl.Contains("[PERSONA_LOAD]", StringComparison.Ordinal), "preview must contain the canonical Persona template");
    AssertTrue(response.Dsl.Contains("[PERSONALITY_PUBLIC]", StringComparison.Ordinal), "preview must include the public personality section");
    AssertTrue(response.Dsl.Contains("TRAIT_RISK_CAUTIOUS_SLIGHT", StringComparison.Ordinal), "preview request must map the trait profile");
    AssertTrue(response.Dsl.Contains("EXPRESSION_DIRECTNESS_BALANCED", StringComparison.Ordinal), "preview request must preserve an explicit balanced expression axis");
    AssertTrue(response.Dsl.Contains("BEHAVIOR_LEADERSHIP_COMMANDING_STRONG", StringComparison.Ordinal), "preview request must map the behavior profile");
    AssertTrue(response.Dsl.Contains("[PERSONALITY_PRIVATE]", StringComparison.Ordinal), "preview request must map the reaction profile");
    AssertTrue(response.Dsl.Contains("COMMITMENT_PROMISE_CAUTIOUS_PROMISES_STRONG", StringComparison.Ordinal), "preview request must map the commitment profile");
}

void SurfacesPreviewCompressionDiagnostics()
{
    PersonaPreviewResponse response = PersonaPreviewService.Build(new PersonaPreviewRequest
    {
        Id = "free.preview-compressed",
        DisplayName = "预览压缩",
        Core = "核心人格。",
        PublicDescription = string.Concat(Enumerable.Repeat("重复公开证据。", 500))
    });

    AssertTrue(response.IsValid, "optional preview evidence should still produce a valid DSL");
    AssertTrue(response.Diagnostic?.Compressed == true, "preview must expose compression diagnostics");
    AssertTrue(response.Diagnostic?.ActualBytes <= response.Diagnostic?.MaximumBytes, "compressed preview must report final emitted byte count");
}

void ExchangesBootstrapTokenOnlyOnce()
{
    WorkbenchSessionManager sessions = new WorkbenchSessionManager("bootstrap-test-token");

    AssertTrue(sessions.TryExchange("bootstrap-test-token", out WorkbenchSessionGrant grant), "first bootstrap exchange must work");
    AssertTrue(!sessions.TryExchange("bootstrap-test-token", out _), "bootstrap token must be single use");
    AssertTrue(!sessions.TryExchange("wrong-token", out _), "wrong bootstrap token must fail");
    AssertTrue(!string.IsNullOrWhiteSpace(grant.SessionToken), "session token is required");
    AssertTrue(!string.IsNullOrWhiteSpace(grant.CsrfToken), "CSRF token is required");
}

void IssuesFreshBootstrapTokensForRepeatedLaunches()
{
    WorkbenchSessionManager sessions = new WorkbenchSessionManager("bootstrap-test-token");
    string freshToken = sessions.IssueBootstrapToken();

    AssertTrue(!string.Equals(freshToken, "bootstrap-test-token", StringComparison.Ordinal), "each launcher request must receive a fresh token");
    AssertTrue(sessions.TryExchange("bootstrap-test-token", out WorkbenchSessionGrant firstGrant), "initial token must remain exchangeable");
    AssertTrue(sessions.TryExchange(freshToken, out WorkbenchSessionGrant repeatedLaunchGrant), "fresh launch token must be exchangeable once");
    AssertEqual(firstGrant.SessionToken, repeatedLaunchGrant.SessionToken, "reopening the workbench must reuse the active local session");
    AssertTrue(!sessions.TryExchange(freshToken, out _), "fresh launch tokens must remain single use");
}

void BoundsConsumedBootstrapTokenBookkeeping()
{
    WorkbenchSessionManager sessions = new WorkbenchSessionManager("bootstrap-test-token");
    for (int index = 0; index < 2000; index++)
    {
        string token = sessions.IssueBootstrapToken();
        AssertTrue(sessions.TryExchange(token, out _), "issued token must exchange during long-run bookkeeping test");
    }

    FieldInfo orderField = typeof(WorkbenchSessionManager).GetField("_bootstrapOrder", BindingFlags.Instance | BindingFlags.NonPublic)!;
    object order = orderField.GetValue(sessions)!;
    int retainedCount = (int)order.GetType().GetProperty("Count")!.GetValue(order)!;
    AssertTrue(retainedCount <= 8, "consumed bootstrap order entries must remain bounded");
}

void RequiresSessionAndCsrfValues()
{
    WorkbenchSessionManager sessions = new WorkbenchSessionManager("bootstrap-test-token");
    AssertTrue(sessions.TryExchange("bootstrap-test-token", out WorkbenchSessionGrant grant), "bootstrap exchange must work");

    AssertTrue(sessions.IsAuthorized(grant.SessionToken, grant.CsrfToken), "matching session grant must authorize");
    AssertTrue(!sessions.IsAuthorized(grant.SessionToken, null), "missing CSRF token must fail");
    AssertTrue(!sessions.IsAuthorized(null, grant.CsrfToken), "missing session token must fail");
    AssertTrue(!sessions.IsAuthorized(grant.SessionToken, "wrong"), "wrong CSRF token must fail");
}

void RenewsActiveWorkbenchSessions()
{
    DateTime now = new DateTime(2026, 8, 19, 8, 0, 0, DateTimeKind.Utc);
    WorkbenchSessionManager sessions = new WorkbenchSessionManager(
        "bootstrap-test-token",
        () => now,
        TimeSpan.FromMinutes(30));
    AssertTrue(sessions.TryExchange("bootstrap-test-token", out WorkbenchSessionGrant grant), "bootstrap exchange must work");

    now = now.AddMinutes(29);
    AssertTrue(sessions.IsAuthorized(grant.SessionToken, grant.CsrfToken), "active session must authorize before idle expiry");
    now = now.AddMinutes(29);
    AssertTrue(sessions.IsAuthorized(grant.SessionToken, grant.CsrfToken), "authorized activity must renew the idle expiry");
    now = now.AddMinutes(31);
    AssertTrue(!sessions.IsAuthorized(grant.SessionToken, grant.CsrfToken), "session must expire after the full idle timeout");
}
void RejectsDocumentNamesOutsideWorkspace()
{
    AssertTrue(WorkspaceDocumentService.IsSafeDocumentName("sable.persona.json"), "standard persona file must be accepted");
    AssertTrue(!WorkspaceDocumentService.IsSafeDocumentName("../sable.persona.json"), "path traversal must be rejected");
    AssertTrue(!WorkspaceDocumentService.IsSafeDocumentName("sable.json"), "wrong extension must be rejected");
}

void NormalizesWorkspacePathAndFileFailures()
{
    WorkspaceDocumentService service = new WorkspaceDocumentService();
    AssertTrue(!WorkspaceDocumentService.IsSafeDocumentName("CON.persona.json"), "Windows device names must be rejected");
    AssertTrue(!WorkspaceDocumentService.IsSafeDocumentName("AUX.persona.json"), "Windows device aliases must be rejected");
    AssertTrue(!WorkspaceDocumentService.IsSafeDocumentName("bad\0.persona.json"), "control characters must be rejected");

    string rootFile = Path.Combine(Path.GetTempPath(), "persona-workbench-root-file-" + Guid.NewGuid().ToString("N"));
    File.WriteAllText(rootFile, "not a directory");
    try
    {
        WorkspaceDocumentResponse response = service.Save(new WorkspaceSaveRequest
        {
            RootPath = rootFile,
            FileName = "sable.persona.json",
            Document = new PersonaDocument { Id = "free.test.sable", Core = "Test." }
        });
        AssertTrue(!response.IsSuccess, "a file cannot be used as a workspace root");
        AssertEqual("workspace.root_invalid", response.ErrorCode, "file roots need a stable error code");
    }
    finally
    {
        File.Delete(rootFile);
    }

    WorkspaceDocumentResponse invalidPath = service.Load(new WorkspaceDocumentRequest
    {
        RootPath = "bad\0root",
        FileName = "sable.persona.json"
    });
    AssertTrue(!invalidPath.IsSuccess, "invalid root characters must not throw");
    AssertEqual("workspace.root_invalid", invalidPath.ErrorCode, "invalid roots need a stable error code");
}
void SavesAndReloadsFreePersonaDocument()
{
    string directory = Path.Combine(Path.GetTempPath(), "persona-workbench-web-tests-" + Guid.NewGuid().ToString("N"));
    try
    {
        WorkspaceDocumentService service = new WorkspaceDocumentService();
        WorkspaceDocumentResponse saved = service.Save(new WorkspaceSaveRequest
        {
            RootPath = directory,
            FileName = "sable.persona.json",
            Document = new PersonaDocument
            {
                Id = "free.web.sable",
                Core = "她只相信可验证的承诺。",
                Tags = new List<string> { "trait.cautious" }
            }
        });
        AssertTrue(saved.IsSuccess, "local save must succeed");
        AssertTrue(!string.IsNullOrWhiteSpace(saved.ContentHash), "save must return a content hash");

        WorkspaceDocumentResponse loaded = service.Load(new WorkspaceDocumentRequest
        {
            RootPath = directory,
            FileName = "sable.persona.json"
        });
        AssertTrue(loaded.IsSuccess, "local load must succeed");
        AssertEqual("free.web.sable", loaded.Document!.Id, "loaded document must match saved document");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

void RejectsInvalidDocumentsBeforePersistence()
{
    string directory = Path.Combine(Path.GetTempPath(), "persona-workbench-invalid-document-tests-" + Guid.NewGuid().ToString("N"));
    try
    {
        WorkspaceDocumentService service = new WorkspaceDocumentService();
        WorkspaceDocumentResponse emptyCore = service.Save(new WorkspaceSaveRequest
        {
            RootPath = directory,
            FileName = "invalid.persona.json",
            Document = new PersonaDocument { Id = "free.invalid.core" }
        });

        AssertTrue(!emptyCore.IsSuccess, "empty Core must be rejected before persistence");
        AssertEqual("persona.core_required", emptyCore.ErrorCode, "empty Core must expose the validator error");
        AssertTrue(!File.Exists(Path.Combine(directory, "invalid.persona.json")), "invalid documents must not be written");

        WorkspaceDocumentResponse nullTag = service.Save(new WorkspaceSaveRequest
        {
            RootPath = directory,
            FileName = "invalid-tag.persona.json",
            Document = new PersonaDocument
            {
                Id = "free.invalid.tag",
                Core = "核心人格。",
                Tags = new List<string> { null! }
            }
        });

        AssertTrue(!nullTag.IsSuccess, "null tags must be rejected before persistence");
        AssertEqual("tag.invalid", nullTag.ErrorCode, "null tags must expose a stable validator error");
        AssertTrue(!File.Exists(Path.Combine(directory, "invalid-tag.persona.json")), "documents with null tags must not be written");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

void RejectsApprovalWhenDslCannotFitWithoutMutation()
{
    string directory = Path.Combine(Path.GetTempPath(), "persona-workbench-approval-budget-tests-" + Guid.NewGuid().ToString("N"));
    try
    {
        WorkspaceDocumentService service = new WorkspaceDocumentService();
        WorkspaceDocumentResponse initial = service.Save(new WorkspaceSaveRequest
        {
            RootPath = directory,
            FileName = "budget.persona.json",
            Document = new PersonaDocument
            {
                Id = "free.approval.budget",
                Core = "旧核心人格。"
            }
        });
        AssertTrue(initial.IsSuccess, "baseline document must be saved");

        PersonaDocument candidate = new PersonaDocument
        {
            Id = "free.approval.budget",
            Core = "核心人格。",
            ReactionProfile = new PersonaReactionProfile
            {
                SensitiveConditions = new string('敏', 900),
                ConditionalResponses = new string('反', 900)
            },
            CommitmentProfile = new PersonaCommitmentProfile
            {
                PriorityOrder = new string('序', 900),
                ProtectedValues = new string('值', 900),
                ApplicableScope = new string('域', 900),
                ExceptionCost = new string('代', 900),
                BreachResponse = new string('果', 900)
            },
            Status = PersonaReviewStatus.Draft
        };
        WorkspaceDocumentResponse rejected = service.Approve(new WorkspaceSaveRequest
        {
            RootPath = directory,
            FileName = "budget.persona.json",
            Document = candidate,
            ExpectedContentHash = initial.ContentHash
        });

        AssertTrue(!rejected.IsSuccess, "approval must fail when the canonical DSL cannot fit");
        AssertEqual("persona.template_core_budget_exceeded", rejected.ErrorCode, "approval must expose the canonical budget error");
        AssertEqual(PersonaReviewStatus.Draft, candidate.Status, "failed approval must not mutate the caller document status");
        AssertEqual(initial.ContentHash, service.Load(new WorkspaceDocumentRequest
        {
            RootPath = directory,
            FileName = "budget.persona.json"
        }).ContentHash, "failed approval must preserve the target content hash");

        WorkspaceDocumentResponse loaded = service.Load(new WorkspaceDocumentRequest
        {
            RootPath = directory,
            FileName = "budget.persona.json"
        });
        AssertTrue(loaded.IsSuccess, "the original document must remain loadable after failed approval");
        AssertEqual("旧核心人格。", loaded.Document!.Core, "failed approval must preserve the original Core");
        AssertEqual(PersonaReviewStatus.Draft, loaded.Document.Status, "failed approval must preserve the original status");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

void PreservesDocumentMetadataAcrossDirectRoundTrip()
{
    string directory = Path.Combine(Path.GetTempPath(), "persona-workbench-metadata-tests-" + Guid.NewGuid().ToString("N"));
    try
    {
        WorkspaceDocumentService service = new WorkspaceDocumentService();
        WorkspaceDocumentResponse saved = service.Save(new WorkspaceSaveRequest
        {
            RootPath = directory,
            FileName = "metadata.persona.json",
            Document = new PersonaDocument
            {
                Id = "free.metadata.roundtrip",
                Core = "保留元数据的核心人格。",
                SourcePackId = "source.pack.current",
                TemplateVersion = "persona-load.v7"
            }
        });
        AssertTrue(saved.IsSuccess, "metadata baseline must save");

        WorkspaceDocumentResponse loaded = service.Load(new WorkspaceDocumentRequest
        {
            RootPath = directory,
            FileName = "metadata.persona.json"
        });
        loaded.Document!.Core = "修改后的核心人格。";
        WorkspaceDocumentResponse resaved = service.Save(new WorkspaceSaveRequest
        {
            RootPath = directory,
            FileName = "metadata.persona.json",
            Document = loaded.Document,
            ExpectedContentHash = loaded.ContentHash
        });
        AssertTrue(resaved.IsSuccess, "edited metadata document must save");

        WorkspaceDocumentResponse roundTripped = service.Load(new WorkspaceDocumentRequest
        {
            RootPath = directory,
            FileName = "metadata.persona.json"
        });
        AssertEqual("source.pack.current", roundTripped.Document!.SourcePackId, "SourcePackId must survive save after load");
        AssertEqual("persona-load.v7", roundTripped.Document.TemplateVersion, "TemplateVersion must survive save after load");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

void ExposesStructuredDocumentHttpStatusMapping()
{
    string source = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/Program.cs"));
    AssertTrue(source.Contains("ToWorkspaceDocumentResult", StringComparison.Ordinal), "document endpoints must use a shared structured result mapper");
    AssertTrue(source.Contains("StatusCodes.Status400BadRequest", StringComparison.Ordinal), "invalid document requests must map to HTTP 400");
    AssertTrue(source.Contains("StatusCodes.Status404NotFound", StringComparison.Ordinal), "missing documents must map to HTTP 404");
    AssertTrue(source.Contains("StatusCodes.Status409Conflict", StringComparison.Ordinal), "workspace conflicts must map to HTTP 409");
    AssertTrue(source.Contains("StatusCodes.Status500InternalServerError", StringComparison.Ordinal), "unexpected document failures must map to HTTP 500");
    AssertTrue(source.Contains("app.UseExceptionHandler", StringComparison.Ordinal), "unexpected failures must use a structured JSON exception boundary");
}

void GuardsBrowserDocumentEpochsAndMetadata()
{
    string source = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));
    AssertTrue(source.Contains("schemaVersion:", StringComparison.Ordinal), "currentDocument must preserve schemaVersion");
    AssertTrue(source.Contains("sourcePackId:", StringComparison.Ordinal), "currentDocument must preserve sourcePackId");
    AssertTrue(source.Contains("templateVersion:", StringComparison.Ordinal), "currentDocument must preserve templateVersion");
    AssertTrue(source.Contains("documentEpoch", StringComparison.Ordinal), "browser document operations need a document epoch");
    AssertTrue(source.Contains("previewRequestGeneration", StringComparison.Ordinal), "preview requests need a response generation");
    AssertTrue(source.Contains("isCurrentDocumentState", StringComparison.Ordinal), "async document responses need a shared freshness guard");
    AssertTrue(source.Contains("clearDocumentTransientState", StringComparison.Ordinal), "loading a document must clear Provider transient state");
    AssertTrue(!source.Contains("document.querySelector(\"#core\").value = snapshot.core;", StringComparison.Ordinal), "failed Provider conversion must not restore a stale partial Core snapshot");
}

void RequiresExplicitLocalApproval()
{
    string directory = Path.Combine(Path.GetTempPath(), "persona-workbench-approval-tests-" + Guid.NewGuid().ToString("N"));
    try
    {
        WorkspaceDocumentService service = new WorkspaceDocumentService();
        WorkspaceSaveRequest request = new WorkspaceSaveRequest
        {
            RootPath = directory,
            FileName = "approval.persona.json",
            Document = new PersonaDocument
            {
                Id = "free.review.approval",
                Core = "她只接受经过核验的结论。",
                Status = "approved"
            }
        };

        WorkspaceDocumentResponse saved = service.Save(request);
        AssertTrue(saved.IsSuccess, "ordinary save must succeed");
        AssertEqual("draft", saved.Document!.Status, "ordinary save must not accept client-authored approval");

        request.ExpectedContentHash = saved.ContentHash;
        WorkspaceDocumentResponse approved = service.Approve(request);
        AssertTrue(approved.IsSuccess, "explicit local approval must succeed");
        AssertEqual("approved", approved.Document!.Status, "explicit approval must persist approved status");

        request.ExpectedContentHash = approved.ContentHash;
        request.Document.Core = "她在新证据出现后修正了结论。";
        WorkspaceDocumentResponse edited = service.Save(request);
        AssertTrue(edited.IsSuccess, "editing an approved document must still save");
        AssertEqual("draft", edited.Document!.Status, "edited content must return to draft");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

void AcceptsOnlySafeProviderEndpoints()
{
    AssertTrue(ProviderEndpointPolicy.TryValidate("https://api.example.com/v1", out _, out _), "HTTPS cloud endpoint must be accepted");
    AssertTrue(ProviderEndpointPolicy.TryValidate("http://127.0.0.1:11434/v1", out _, out _), "loopback HTTP endpoint must be accepted");
    AssertTrue(ProviderEndpointPolicy.TryValidate("http://localhost:8080/v1", out _, out _), "localhost HTTP endpoint must be accepted");
    AssertTrue(!ProviderEndpointPolicy.TryValidate("http://api.example.com/v1", out _, out _), "non-loopback HTTP endpoint must be rejected");
    AssertTrue(!ProviderEndpointPolicy.TryValidate("file:///C:/provider", out _, out _), "unsupported scheme must be rejected");
}

void RejectsProviderRedirects()
{
    ProviderDraftClient client = new ProviderDraftClient(new DelegateHttpMessageHandler(_ =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("https://elsewhere.example/v1") }
        })), PublicProviderEndpointResolver());

    ProviderDraftResult result = client.GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.RedirectRejected, "redirect must be rejected without a retry");
}

void LimitsProviderRequestsAndHonorsCooldown()
{
    TaskCompletionSource<HttpResponseMessage> firstResponse = new TaskCompletionSource<HttpResponseMessage>();
    int calls = 0;
    ProviderDraftClient client = new ProviderDraftClient(new DelegateHttpMessageHandler(_ =>
    {
        calls++;
        return calls == 1
            ? firstResponse.Task
            : Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)
            {
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30)) }
            });
    }), PublicProviderEndpointResolver());

    Task<ProviderDraftResult> inFlight = client.GenerateAsync(CreateProviderRequest());
    ProviderDraftResult concurrent = client.GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(concurrent.Status == ProviderDraftStatus.Busy, "a second request must not run while another request is active");

    firstResponse.SetResult(new HttpResponseMessage((HttpStatusCode)429)
    {
        Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30)) }
    });
    ProviderDraftResult rateLimited = inFlight.GetAwaiter().GetResult();
    AssertTrue(rateLimited.Status == ProviderDraftStatus.RateLimited, "429 must be surfaced as rate-limited");

    ProviderDraftResult coolingDown = client.GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(coolingDown.Status == ProviderDraftStatus.CoolingDown, "cooldown must prevent an automatic retry");
    AssertEqual("1", calls.ToString(), "cooldown must not submit another HTTP request");
}

void ExpandsCharacterProseWithoutPersonaJson()
{
    string? requestBody = null;
    string responseJson = JsonSerializer.Serialize(new
    {
        choices = new[]
        {
            new { message = new { content = "她年仅二十，身形娇小，却把战场上的迟疑视为耻辱。面对敌人时果断冷酷，面对恋爱情境时则会以逞强掩饰慌乱。" } }
        }
    });
    ProviderTextExpansionClient client = new ProviderTextExpansionClient(new DelegateHttpMessageHandler(async request =>
    {
        requestBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };
    }), PublicProviderEndpointResolver());

    ProviderTextExpansionResult result = client.ExpandAsync(CreateExpansionRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "valid prose expansion must succeed");
    AssertTrue(result.ExpandedText.Contains("面对恋爱情境", StringComparison.Ordinal), "expanded prose must be returned without Persona assembly");
    string capturedBody = requestBody ?? throw new InvalidOperationException("expansion request body was not captured");
    AssertTrue(capturedBody.Contains("Do not produce tags", StringComparison.Ordinal), "expansion prompt must prohibit Persona structure");
    AssertTrue(capturedBody.Contains("first Chinese full stop", StringComparison.Ordinal), "expansion prompt must define the exact first-sentence boundary");
    AssertTrue(!capturedBody.Contains("response_format", StringComparison.Ordinal), "text expansion must not request a JSON response format");
    using JsonDocument capturedPayload = JsonDocument.Parse(capturedBody);
    string submittedEnvelope = capturedPayload.RootElement.GetProperty("messages")[1].GetProperty("content").GetString() ?? string.Empty;
    AssertTrue(submittedEnvelope.Contains("年龄二十", StringComparison.Ordinal), "expansion envelope must preserve source-language characters inside the user message");
    AssertTrue(!submittedEnvelope.Contains("\\u5e74", StringComparison.OrdinalIgnoreCase), "expansion envelope must not expose literal Unicode escape sequences to the model");
    AssertTrue(capturedBody.Contains("\"max_tokens\":3072", StringComparison.Ordinal), "text expansion must use the expanded safety ceiling");
}

void ExpansionPromptDraftsObservableBehaviorWithoutInventingBiography()
{
    string? requestBody = null;
    string responseJson = JsonSerializer.Serialize(new
    {
        choices = new[]
        {
            new { message = new { content = "她表面顺从，实际很记仇，也很会观察别人。她在众人面前少争辩，把判断藏在沉默里；受压时会先记下对方的轻慢，再寻找合适的时机讨回代价。" } }
        }
    });
    ProviderTextExpansionClient client = new ProviderTextExpansionClient(new DelegateHttpMessageHandler(async request =>
    {
        requestBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
    }), PublicProviderEndpointResolver());

    ProviderTextExpansionRequest request = CreateExpansionRequest();
    request.Description = "她表面顺从，实际很记仇，也很会观察别人。";
    ProviderTextExpansionResult result = client.ExpandAsync(request).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.Success, "behavioral drafting fixture must remain a valid expansion");
    using JsonDocument payload = JsonDocument.Parse(requestBody ?? throw new InvalidOperationException("expansion prompt request was not captured"));
    string systemPrompt = payload.RootElement.GetProperty("messages")[0].GetProperty("content").GetString() ?? string.Empty;
    AssertTrue(systemPrompt.Contains("observable public behavior", StringComparison.Ordinal), "expansion prompt must ask for observable public behavior");
    AssertTrue(systemPrompt.Contains("private behavior", StringComparison.Ordinal), "expansion prompt must ask for private behavior");
    AssertTrue(systemPrompt.Contains("pressure reactions", StringComparison.Ordinal), "expansion prompt must ask for pressure reactions");
    AssertTrue(systemPrompt.Contains("preserve opposing traits", StringComparison.OrdinalIgnoreCase), "expansion prompt must preserve opposing traits");
    AssertTrue(systemPrompt.Contains("shameful motives", StringComparison.Ordinal), "expansion prompt must not sanitize unpleasant motives");
    AssertTrue(systemPrompt.Contains("biographical facts", StringComparison.Ordinal), "expansion prompt must keep unsupported biography forbidden");
    AssertTrue(systemPrompt.Contains("Do not produce tags", StringComparison.Ordinal), "expansion prompt must retain the plain-prose output contract");
}

void PreservesMultiParagraphExpansionInput()
{
    const string description = "第一段：身份与经历。\n\n第二段：人格、行为和矛盾。";
    string? requestBody = null;
    string responseJson = JsonSerializer.Serialize(new
    {
        choices = new[]
        {
            new { message = new { content = "第一段被保留。\n\n第二段也被保留，并补充了行为联系。" } }
        }
    });
    ProviderTextExpansionClient client = new ProviderTextExpansionClient(new DelegateHttpMessageHandler(async request =>
    {
        requestBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };
    }), PublicProviderEndpointResolver());
    ProviderTextExpansionRequest request = CreateExpansionRequest();
    request.Description = description;

    ProviderTextExpansionResult result = client.ExpandAsync(request).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.Success, "multi-paragraph expansion input must succeed");
    using JsonDocument payload = JsonDocument.Parse(requestBody ?? throw new InvalidOperationException("multi-paragraph request body was not captured"));
    string submitted = payload.RootElement.GetProperty("messages")[1].GetProperty("content").GetString() ?? string.Empty;
    using JsonDocument envelope = JsonDocument.Parse(submitted);
    AssertEqual(description, envelope.RootElement.GetProperty("sourceDescription").GetString() ?? string.Empty, "internal blank lines must be preserved inside the source envelope field");
    AssertTrue(result.ExpandedText.Contains("\n\n", StringComparison.Ordinal), "a single paragraph break in Provider output must be preserved");
}

void RejectsUnchangedOrStructuredExpansionOutput()
{
    ProviderTextExpansionRequest request = CreateExpansionRequest();
    ProviderTextExpansionResult unchanged = CreateExpansionClient(request.Description).ExpandAsync(request).GetAwaiter().GetResult();
    AssertTrue(unchanged.Status == ProviderDraftStatus.ResponseInvalid, "unchanged expansion must be rejected");
    AssertEqual("provider.expansion_unchanged", unchanged.ErrorCode, "unchanged expansion must have a distinct error code");

    ProviderTextExpansionResult structured = CreateExpansionClient("[PERSONA_LOAD]\nTRAIT_RISK_CAUTIOUS_STRONG").ExpandAsync(request).GetAwaiter().GetResult();
    AssertTrue(structured.Status == ProviderDraftStatus.ResponseInvalid, "Persona DSL must not be accepted as prose expansion");
    AssertEqual("provider.expansion_text_invalid", structured.ErrorCode, "structured expansion must have a distinct error code");
}

void RejectsExpansionThatDropsQuotedSourceAnchors()
{
    ProviderTextExpansionRequest request = CreateExpansionRequest();
    request.Description = "“孤高的女神”阿芙洛狄忒有着高傲品德。";
    ProviderTextExpansionResult result = CreateExpansionClient("阿芙洛狄忒拥有高傲品德，但扩充遗漏了原文称谓。")
        .ExpandAsync(request)
        .GetAwaiter()
        .GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.ResponseInvalid, "expansion that drops a quoted source anchor must be rejected");
    AssertEqual("provider.expansion_evidence_missing", result.ErrorCode, "quoted source anchor loss needs a stable error code");
}
void NormalizesExpansionControlsDeterministically()
{
    ProviderTextExpansionControls controls = new ProviderTextExpansionControls
    {
        Direction = "重点深化警惕与关系反差",
        FocusPreset = "personality_behavior",
        FocusKeywords = new List<ProviderTextExpansionKeyword>
        {
            new ProviderTextExpansionKeyword { Text = "警惕", Weight = "light" },
            new ProviderTextExpansionKeyword { Text = "警惕", Weight = "strong" },
            new ProviderTextExpansionKeyword { Text = "失去家园", Weight = "medium" }
        },
        AvoidTopics = new List<string> { "外貌", "外貌" }
    };

    AssertTrue(ProviderTextExpansionControlNormalizer.TryNormalize(controls, out ProviderTextExpansionControls normalized, out string errorCode), "valid controls must normalize: " + errorCode);
    AssertEqual("personality_behavior", normalized.FocusPreset, "preset must remain stable");
    AssertEqual("2", normalized.FocusKeywords.Count.ToString(), "duplicate keywords must collapse");
    AssertEqual("strong", normalized.FocusKeywords[0].Weight, "duplicate keywords must retain strongest weight");
    AssertEqual("警惕", normalized.FocusKeywords[0].Text, "first keyword spelling must be retained");
    AssertEqual("1", normalized.AvoidTopics.Count.ToString(), "duplicate avoid topics must collapse");
}

void CountsExpansionControlLimitsByUnicodeScalar()
{
    string sixtyFourEmoji = string.Concat(Enumerable.Repeat("😀", 64));
    ProviderTextExpansionControls accepted = new ProviderTextExpansionControls
    {
        FocusKeywords = new List<ProviderTextExpansionKeyword> { new ProviderTextExpansionKeyword { Text = sixtyFourEmoji, Weight = "medium" } }
    };
    AssertTrue(ProviderTextExpansionControlNormalizer.TryNormalize(accepted, out _, out string acceptedError), "64 Unicode scalars must be accepted: " + acceptedError);

    ProviderTextExpansionControls rejected = new ProviderTextExpansionControls
    {
        FocusKeywords = new List<ProviderTextExpansionKeyword> { new ProviderTextExpansionKeyword { Text = sixtyFourEmoji + "😀", Weight = "medium" } }
    };
    AssertTrue(!ProviderTextExpansionControlNormalizer.TryNormalize(rejected, out _, out string rejectedError), "65 Unicode scalars must be rejected");
    AssertEqual("provider.expansion_controls_invalid", rejectedError, "scalar overflow needs the stable controls error");
}

void NormalizesExpansionEnvelopeLineEndings()
{
    string? requestBody = null;
    ProviderTextExpansionClient client = new ProviderTextExpansionClient(new DelegateHttpMessageHandler(async request =>
    {
        requestBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "扩写后的文本。" } } } }), System.Text.Encoding.UTF8, "application/json")
        };
    }), PublicProviderEndpointResolver());
    ProviderTextExpansionRequest request = CreateExpansionRequest();
    request.Description = "第一段。\r\n\r\n第二段。";
    request.Controls = new ProviderTextExpansionControls { Direction = "第一条。\r第二条。" };

    ProviderTextExpansionResult result = client.ExpandAsync(request).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.Success, "line-ending normalization request must succeed");
    using JsonDocument outer = JsonDocument.Parse(requestBody ?? throw new InvalidOperationException("request body was not captured"));
    using JsonDocument envelope = JsonDocument.Parse(outer.RootElement.GetProperty("messages")[1].GetProperty("content").GetString() ?? string.Empty);
    AssertEqual("第一段。\n\n第二段。", envelope.RootElement.GetProperty("sourceDescription").GetString() ?? string.Empty, "source CRLF must normalize to LF");
    AssertEqual("第一条。\n第二条。", envelope.RootElement.GetProperty("direction").GetString() ?? string.Empty, "direction CR must normalize to LF");
}

void RejectsInvalidExpansionControlsBeforeHttpDispatch()
{
    int calls = 0;
    ProviderTextExpansionClient client = new ProviderTextExpansionClient(new DelegateHttpMessageHandler(_ =>
    {
        calls++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }), PublicProviderEndpointResolver());
    ProviderTextExpansionRequest request = CreateExpansionRequest();
    request.Controls = new ProviderTextExpansionControls { FocusPreset = "unknown" };

    ProviderTextExpansionResult result = client.ExpandAsync(request).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.InvalidRequest, "invalid controls must fail as an invalid request");
    AssertEqual("provider.expansion_controls_invalid", result.ErrorCode, "invalid controls need a stable error code");
    AssertEqual("0", calls.ToString(), "invalid controls must not dispatch HTTP");
}

void SerializesExpansionControlsInCanonicalEnvelope()
{
    string? requestBody = null;
    ProviderTextExpansionClient client = new ProviderTextExpansionClient(new DelegateHttpMessageHandler(async request =>
    {
        requestBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "她会在亲密关系中以逞强掩饰慌乱。" } } } }), System.Text.Encoding.UTF8, "application/json")
        };
    }), PublicProviderEndpointResolver());
    ProviderTextExpansionRequest request = CreateExpansionRequest();
    request.Controls = new ProviderTextExpansionControls
    {
        Direction = "深化行为与情感反差",
        FocusPreset = "relationship_emotion",
        FocusKeywords = new List<ProviderTextExpansionKeyword> { new ProviderTextExpansionKeyword { Text = "手足无措", Weight = "strong" } },
        AvoidTopics = new List<string> { "外貌" }
    };

    ProviderTextExpansionResult result = client.ExpandAsync(request).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.Success, "canonical envelope request must succeed");
    using JsonDocument outer = JsonDocument.Parse(requestBody ?? throw new InvalidOperationException("request body was not captured"));
    string content = outer.RootElement.GetProperty("messages")[1].GetProperty("content").GetString() ?? string.Empty;
    using JsonDocument envelope = JsonDocument.Parse(content);
    AssertEqual("persona-expansion.v1", envelope.RootElement.GetProperty("protocol").GetString() ?? string.Empty, "envelope protocol must be fixed");
    AssertEqual(request.Description, envelope.RootElement.GetProperty("sourceDescription").GetString() ?? string.Empty, "source must remain separate");
    AssertEqual("relationship_emotion", envelope.RootElement.GetProperty("focusPreset").GetString() ?? string.Empty, "preset must be serialized separately");
    AssertEqual("strong", envelope.RootElement.GetProperty("focusKeywords")[0].GetProperty("weight").GetString() ?? string.Empty, "keyword weight must be serialized");
    AssertEqual("外貌", envelope.RootElement.GetProperty("avoidTopics")[0].GetString() ?? string.Empty, "avoid topic must be serialized separately");
    AssertTrue(!content.Contains("深化行为与情感反差", StringComparison.Ordinal) || envelope.RootElement.GetProperty("direction").GetString() == "深化行为与情感反差", "direction must remain data in its own field");
}

void KeepsLegacyExpansionRequestsBalancedByDefault()
{
    ProviderTextExpansionRequest request = CreateExpansionRequest();
    AssertTrue(request.Controls == null, "legacy request construction must leave controls absent");
    AssertTrue(ProviderTextExpansionControlNormalizer.TryNormalize(request.Controls, out ProviderTextExpansionControls normalized, out string errorCode), "missing controls must use balanced defaults: " + errorCode);
    AssertEqual("balanced", normalized.FocusPreset, "legacy requests must default to balanced expansion");
    AssertEqual("0", normalized.FocusKeywords.Count.ToString(), "legacy requests must have no focus keywords");
}

void ReportsTruncatedAndMalformedExpansionResponses()
{
    ProviderTextExpansionRequest request = CreateExpansionRequest();
    string truncatedJson = JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "length", message = new { content = "未完成的扩写" } } } });
    ProviderTextExpansionResult truncated = CreateExpansionClientFromResponse(truncatedJson).ExpandAsync(request).GetAwaiter().GetResult();
    AssertTrue(truncated.Status == ProviderDraftStatus.ResponseInvalid, "truncated expansion must be rejected");
    AssertEqual("provider.expansion_truncated", truncated.ErrorCode, "truncation needs a stable error code");

    string malformedShapeJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = new[] { "not", "text" } } } } });
    ProviderTextExpansionResult malformedShape = CreateExpansionClientFromResponse(malformedShapeJson).ExpandAsync(request).GetAwaiter().GetResult();
    AssertEqual("provider.expansion_response_shape_invalid", malformedShape.ErrorCode, "non-string content needs a stable shape error");

    string structuredJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "# Analysis\n角色应该……" } } } });
    ProviderTextExpansionResult structured = CreateExpansionClientFromResponse(structuredJson).ExpandAsync(request).GetAwaiter().GetResult();
    AssertEqual("provider.expansion_text_invalid", structured.ErrorCode, "structured prose needs a stable text error");
}

void RoutesExpansionControlsThroughProviderActionService()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    FakeProviderTextExpansionClient expansionClient = new FakeProviderTextExpansionClient(new ProviderTextExpansionResult
    {
        Status = ProviderDraftStatus.Success,
        ExpandedText = "她在公开场合强撑镇定，私下才显露慌乱。"
    });
    ProviderDraftActionService service = new ProviderDraftActionService(vault, new FakeProviderDraftClient(CreateSuccessfulProviderResult()), expansionClient);
    ProviderTextExpansionActionRequest request = new ProviderTextExpansionActionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        Description = "她总是在亲密关系里逞强。",
        Controls = new ProviderTextExpansionControls
        {
            Direction = "深化公开与私下反差",
            FocusPreset = "relationship_emotion",
            FocusKeywords = new List<ProviderTextExpansionKeyword> { new ProviderTextExpansionKeyword { Text = "逞强", Weight = "strong" } }
        }
    };
    AssertTrue(service.TryConfirmCloudEndpoint(request.Endpoint, out _), "cloud endpoint confirmation must succeed");

    ProviderTextExpansionActionResponse response = service.ExpandDescriptionAsync(request).GetAwaiter().GetResult();

    AssertTrue(response.Status == ProviderDraftActionStatus.Success, "valid controlled expansion must succeed");
    AssertTrue(expansionClient.LastRequest?.Controls != null, "controls must reach the provider client");
    AssertEqual("relationship_emotion", expansionClient.LastRequest!.Controls!.FocusPreset, "normalized preset must reach provider client");
    AssertEqual("strong", expansionClient.LastRequest.Controls.FocusKeywords[0].Weight, "normalized keyword weight must reach provider client");
}

void RejectsInvalidActionControlsWithoutQuarantine()
{
    FakeProviderTextExpansionClient expansionClient = new FakeProviderTextExpansionClient(new ProviderTextExpansionResult { Status = ProviderDraftStatus.Success, ExpandedText = "unused" });
    ProviderDraftActionService service = new ProviderDraftActionService(new ProviderSessionKeyVault(), new FakeProviderDraftClient(CreateSuccessfulProviderResult()), expansionClient);
    ProviderTextExpansionActionRequest request = new ProviderTextExpansionActionRequest
    {
        Endpoint = "http://127.0.0.1:11434/v1/chat/completions",
        Model = "local-model",
        Description = "一个谨慎的旅行者。",
        Controls = new ProviderTextExpansionControls { FocusPreset = "not_registered" }
    };

    ProviderTextExpansionActionResponse response = service.ExpandDescriptionAsync(request).GetAwaiter().GetResult();

    AssertTrue(response.Status == ProviderDraftActionStatus.InvalidRequest, "invalid action controls must fail locally");
    AssertEqual("provider.expansion_controls_invalid", response.ErrorCode, "action controls need the stable validation error");
    AssertEqual("0", expansionClient.CallCount.ToString(), "invalid action controls must not call provider client");
    AssertEqual("0", service.GetQuarantinedFailures().Count.ToString(), "invalid controls must not enter quarantine");
}

void BlocksCrossActionProviderOperationsAndReleasesTheGate()
{
    BlockingProviderTextExpansionClient expansionClient = new BlockingProviderTextExpansionClient();
    ProviderDraftActionService service = new ProviderDraftActionService(new ProviderSessionKeyVault(), new FakeProviderDraftClient(CreateSuccessfulProviderResult()), expansionClient);
    ProviderTextExpansionActionRequest request = new ProviderTextExpansionActionRequest
    {
        Endpoint = "http://127.0.0.1:11434/v1/chat/completions",
        Model = "local-model",
        Description = "一个谨慎的旅行者。"
    };

    Task<ProviderTextExpansionActionResponse> pending = service.ExpandDescriptionAsync(request);
    expansionClient.WaitUntilEntered();

    AssertTrue(!service.TrySetSessionKey("another-secret-123", out string busyError), "session-key mutation must be blocked while expansion is running");
    AssertEqual("provider.request_in_flight", busyError, "cross-action busy state needs the stable error code");
    AssertTrue(!service.TryClearSessionKey(out string clearBusyError), "session-key clearing must be blocked while expansion is running");
    AssertEqual("provider.request_in_flight", clearBusyError, "clear-key busy state needs the stable error code");
    AssertTrue(!service.TryConfirmCloudEndpoint("https://api.example.com/v1/chat/completions", out string confirmBusyError), "endpoint confirmation must be blocked while expansion is running");
    AssertEqual("provider.request_in_flight", confirmBusyError, "endpoint-confirmation busy state needs the stable error code");

    expansionClient.Complete(new ProviderTextExpansionResult { Status = ProviderDraftStatus.Success, ExpandedText = "扩写完成。" });
    ProviderTextExpansionActionResponse response = pending.GetAwaiter().GetResult();
    AssertTrue(response.Status == ProviderDraftActionStatus.Success, "pending expansion must complete successfully");
    AssertTrue(service.TrySetSessionKey("another-secret-123", out string releasedError), "operation gate must release after completion: " + releasedError);
    AssertTrue(service.TryClearSessionKey(out string clearReleasedError), "clear-key operation must be released after completion: " + clearReleasedError);
    AssertTrue(service.TryConfirmCloudEndpoint("https://api.example.com/v1/chat/completions", out string confirmReleasedError), "endpoint confirmation must be released after completion: " + confirmReleasedError);
}

void ConvertsConfirmedTextThroughCompletePersonaProviderDraftChain()
{
    FakeProviderDraftClient draftClient = new FakeProviderDraftClient(new ProviderDraftResult
    {
        Status = ProviderDraftStatus.ResponseInvalid,
        ErrorCode = "provider.full_draft_must_not_run"
    });
    FakeProviderDslConversionClient conversionClient = new FakeProviderDslConversionClient(new ProviderDslConversionResult
    {
        Status = ProviderDraftStatus.Success,
        CandidateJson = "{\"summary\":\"二十岁的傲娇女战士\",\"axes\":[{\"index\":2,\"value\":2,\"source\":\"傲娇\"}]}"
    });
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    ProviderDraftActionService service = new ProviderDraftActionService(vault, draftClient, null, conversionClient);
    ProviderDslConversionActionRequest request = new ProviderDslConversionActionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = "二十岁的傲娇女战士，身材娇小，涉世未深，是个坚强的女战士，对敌人不存仁慈，但恋爱时却显得手足无措。",
        LocalId = "free.generated.colin",
        LocalDisplayName = "科林"
    };
    AssertTrue(service.TryConfirmCloudEndpoint(request.Endpoint, out _), "cloud conversion endpoint confirmation must succeed");

    ProviderDslConversionActionResponse response = service.ConvertToDslAsync(request).GetAwaiter().GetResult();

    AssertTrue(response.Status == ProviderDraftActionStatus.Success, "confirmed text must use the sparse Persona conversion chain");
    AssertEqual("1", conversionClient.CallCount.ToString(), "sparse conversion must call the Provider once");
    AssertEqual("0", draftClient.CallCount.ToString(), "direct conversion must not call the legacy full-draft client");
    AssertTrue(response.Dsl.Contains("TRAIT_PRIDE_PROUD_STRONG", StringComparison.Ordinal), "evidence-backed pride axis must reach canonical DSL");
    AssertTrue(!response.Dsl.Contains("provider.wrong.id", StringComparison.Ordinal), "Provider identity metadata must not replace local identity");
}

void EnrichesMissingAxesFromGroundedSourceEvidence()
{
    FakeProviderDslConversionClient conversionClient = new FakeProviderDslConversionClient(new ProviderDslConversionResult
    {
        Status = ProviderDraftStatus.Success,
        CandidateJson = "{\"identityFacts\":\"阿芙洛狄忒\"}"
    });
    ProviderDraftActionService service = new ProviderDraftActionService(
        new ProviderSessionKeyVault(),
        new FakeProviderDraftClient(new ProviderDraftResult { Status = ProviderDraftStatus.ResponseInvalid, ErrorCode = "legacy_must_not_run" }),
        null,
        conversionClient);

    ProviderDslConversionActionResponse response = service.ConvertToDslAsync(new ProviderDslConversionActionRequest
    {
        Endpoint = "http://127.0.0.1:11434/v1/chat/completions",
        Model = "gpt-oss:20b",
        ProviderProtocol = ProviderProtocolKind.Ollama,
        SourceText = "阿芙洛狄忒具有骄傲品德，外表高冷，对男性礼貌。",
        LocalId = "free.enriched",
        LocalDisplayName = "阿芙洛狄忒"
    }).GetAwaiter().GetResult();

    AssertTrue(response.Status == ProviderDraftActionStatus.Success, "grounded fallback axes should make a sparse candidate usable");
    AssertTrue(response.Dsl.Contains("TRAIT_PRIDE_PROUD_SLIGHT", StringComparison.Ordinal), "source pride evidence must enrich a missing axis");
    AssertTrue(response.Dsl.Contains("EXPRESSION_FORMALITY_FORMAL_SLIGHT", StringComparison.Ordinal), "source formality evidence must enrich a missing axis");
    AssertTrue(response.Dsl.Contains("EXPRESSION_WARMTH_ALOOF_SLIGHT", StringComparison.Ordinal), "source aloofness evidence must enrich a missing axis");
}
void EnrichesMissingReactionAndExchangeEvidence()
{
    FakeProviderDslConversionClient conversionClient = new FakeProviderDslConversionClient(new ProviderDslConversionResult
    {
        Status = ProviderDraftStatus.Success,
        CandidateJson = "{\"identityFacts\":\"测试角色\"}"
    });
    ProviderDraftActionService service = new ProviderDraftActionService(
        new ProviderSessionKeyVault(),
        new FakeProviderDraftClient(new ProviderDraftResult { Status = ProviderDraftStatus.ResponseInvalid, ErrorCode = "legacy_must_not_run" }),
        null,
        conversionClient);

    ProviderDslConversionActionResponse response = service.ConvertToDslAsync(new ProviderDslConversionActionRequest
    {
        Endpoint = "http://127.0.0.1:11434/v1/chat/completions",
        Model = "gpt-oss:20b",
        ProviderProtocol = ProviderProtocolKind.Ollama,
        SourceText = "测试角色有三个弱点：如果受到拷问，就会失去反抗。为了运作部队，会用身体犒赏亲卫。",
        LocalId = "free.semantic-enrichment",
        LocalDisplayName = "测试角色"
    }).GetAwaiter().GetResult();

    AssertTrue(response.Status == ProviderDraftActionStatus.Success && response.Draft != null, "grounded reaction and exchange evidence should make a sparse candidate usable");
    AssertTrue(response.Draft!.ReactionProfile.SensitiveConditions.Contains("如果受到拷问", StringComparison.Ordinal), "reaction conditions must be returned in the draft");
    AssertTrue(response.Draft.ReactionProfile.ConditionalResponses.Contains("就会失去反抗", StringComparison.Ordinal), "conditional responses must be returned in the draft");
    AssertTrue(response.Dsl.Contains("SENSITIVE_CONDITIONS=", StringComparison.Ordinal), "reaction evidence must reach canonical DSL");
    AssertTrue(response.Dsl.Contains("COMMITMENT_VALUE_TRADEABLE_VALUES_SLIGHT", StringComparison.Ordinal), "exchange evidence must reach canonical DSL");
    AssertTrue(response.Dsl.Contains("BEHAVIOR_CONDITIONALITY_BARGAINING_FIRST_SLIGHT", StringComparison.Ordinal), "exchange evidence must retain the bargaining behavior axis");
}
void RoutesNamedDslConversionThroughSparseProviderClient()
{
    FakeProviderDraftClient draftClient = new FakeProviderDraftClient(new ProviderDraftResult
    {
        Status = ProviderDraftStatus.ResponseInvalid,
        ErrorCode = "provider.full_draft_must_not_run"
    });
    FakeProviderDslConversionClient conversionClient = new FakeProviderDslConversionClient(new ProviderDslConversionResult
    {
        Status = ProviderDraftStatus.Success,
        CandidateJson = "{\"axes\":[{\"index\":2,\"value\":2,\"source\":\"自尊强烈\"}],\"flags\":[{\"index\":1,\"source\":\"不作空头许诺\"}]}"
    });
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    ProviderDraftActionService service = new ProviderDraftActionService(vault, draftClient, null, conversionClient);
    ProviderDslConversionActionRequest request = new ProviderDslConversionActionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = "科林自尊强烈，而且不作空头许诺。",
        LocalId = "free.generated.colin",
        LocalDisplayName = "科林"
    };
    AssertTrue(service.TryConfirmCloudEndpoint(request.Endpoint, out _), "cloud conversion endpoint confirmation must succeed");

    ProviderDslConversionActionResponse response = service.ConvertToDslAsync(request).GetAwaiter().GetResult();

    AssertTrue(response.Status == ProviderDraftActionStatus.Success && response.Draft != null, "named conversion must complete through the sparse Provider path");
    AssertEqual("1", conversionClient.CallCount.ToString(), "sparse Provider conversion client must be called exactly once");
    AssertEqual("0", draftClient.CallCount.ToString(), "full Persona draft client must not be called for named DSL conversion");
    AssertTrue(response.Dsl.Contains("TRAIT_PRIDE_PROUD_STRONG", StringComparison.Ordinal), "sparse Provider axis must reach canonical DSL");
    AssertTrue(response.Dsl.Contains("BOUNDARY_NO_EMPTY_PROMISES", StringComparison.Ordinal), "sparse Provider flag must reach canonical DSL");
    AssertTrue(response.Dsl.Contains("TOKEN=CONSTRAINT_TRIGGER_AND_SCOPE_REQUIRED", StringComparison.Ordinal), "canonical constraints must remain in Provider conversion output");
}

void ConversionPromptPreservesContradictionsWithoutChangingItsSparseContract()
{
    string? requestBody = null;
    const string source = "她公开顺从，私下记仇；她不轻易许诺，也不会忘记别人欠下的代价。";
    string candidate = "{\"summary\":\"她公开顺从，私下记仇\",\"identityFacts\":\"她公开顺从\",\"reaction\":{\"sensitiveConditions\":\"别人欠下的代价\",\"conditionalResponses\":\"不会忘记\"},\"axes\":[{\"index\":20,\"value\":2,\"source\":\"私下记仇\"}],\"flags\":[{\"index\":1,\"source\":\"不轻易许诺\"}]}";
    ProviderDslConversionClient client = new ProviderDslConversionClient(new DelegateHttpMessageHandler(async request =>
    {
        requestBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content = candidate } } }
            }), Encoding.UTF8, "application/json")
        };
    }), PublicProviderEndpointResolver());

    ProviderDslConversionResult result = client.ConvertAsync(new ProviderDslConversionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = source,
        LocalId = "fixture.persona",
        LocalDisplayName = "夹具人物"
    }).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.Success, "contradiction conversion fixture must remain valid");
    using JsonDocument payload = JsonDocument.Parse(requestBody ?? throw new InvalidOperationException("conversion prompt request was not captured"));
    string systemPrompt = payload.RootElement.GetProperty("messages")[0].GetProperty("content").GetString() ?? string.Empty;
    AssertTrue(systemPrompt.Contains("sparse evidence candidate", StringComparison.Ordinal), "conversion prompt must retain sparse candidate semantics");
    AssertTrue(systemPrompt.Contains("exact source phrase", StringComparison.Ordinal), "conversion prompt must retain exact source evidence");
    AssertTrue(systemPrompt.Contains("Allowed root fields", StringComparison.Ordinal), "conversion prompt must retain the existing root field contract");
    AssertTrue(systemPrompt.Contains("preserve supported contradictions", StringComparison.OrdinalIgnoreCase), "conversion prompt must preserve supported contradictions");
    AssertTrue(systemPrompt.Contains("unpleasant motives", StringComparison.Ordinal), "conversion prompt must not sanitize supported motives");
    AssertTrue(!systemPrompt.Contains("confidence", StringComparison.OrdinalIgnoreCase), "conversion prompt must not introduce a new confidence field");
}

void ConvertsRealProviderHttpCandidateIntoCanonicalDsl()
{
    const string source = "科林二十岁，傲娇而自尊强烈，面对敌人会直接迎击；她重视承诺，恋爱时手足无措。";
    const string candidate = "{\"summary\":\"科林二十岁，傲娇而自尊强烈\",\"axes\":[{\"index\":2,\"value\":2,\"source\":\"自尊强烈\"},{\"index\":17,\"value\":2,\"source\":\"直接迎击\"}]}";
    ProviderDslConversionClient conversionClient = new ProviderDslConversionClient(new DelegateHttpMessageHandler(async request =>
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = candidate } } } }), System.Text.Encoding.UTF8, "application/json")
        };
    }), PublicProviderEndpointResolver());
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    ProviderDraftActionService service = new ProviderDraftActionService(vault, new FakeProviderDraftClient(new ProviderDraftResult { Status = ProviderDraftStatus.ResponseInvalid, ErrorCode = "legacy_must_not_run" }), null, conversionClient);
    ProviderDslConversionActionRequest request = new ProviderDslConversionActionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = source,
        LocalId = "free.generated.colin",
        LocalDisplayName = "科林"
    };
    AssertTrue(service.TryConfirmCloudEndpoint(request.Endpoint, out _), "cloud conversion endpoint confirmation must succeed");

    ProviderDslConversionActionResponse response = service.ConvertToDslAsync(request).GetAwaiter().GetResult();

    AssertTrue(response.Status == ProviderDraftActionStatus.Success && response.Draft != null, "real sparse Provider response must reach the conversion action");
    AssertEqual("free.generated.colin", response.Draft!.Id, "local stable ID must survive the sparse HTTP candidate path");
    AssertEqual("科林", response.Draft.DisplayName, "local display name must survive the sparse HTTP candidate path");
    AssertTrue(response.Dsl.Contains("TRAIT_PRIDE_PROUD_STRONG", StringComparison.Ordinal), "real Provider pride evidence must produce an English stable axis");
    AssertTrue(response.Dsl.Contains("REACTION_CONFRONTATION_CONFRONTATIONAL_STRONG", StringComparison.Ordinal), "real Provider reaction evidence must produce an English stable axis");
    AssertTrue(!response.Dsl.Contains("provider.wrong.id", StringComparison.Ordinal), "Provider metadata must remain outside canonical DSL");
}
void GeneratesMissingLocalPersonaIdentityFromProviderDraft()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    FakeProviderDslConversionClient conversionClient = new FakeProviderDslConversionClient(new ProviderDslConversionResult
    {
        Status = ProviderDraftStatus.Success,
        CandidateJson = "{\"axes\":[{\"index\":2,\"value\":2,\"source\":\"骄傲\"}]}"
    });
    ProviderDraftActionService service = new ProviderDraftActionService(vault, new FakeProviderDraftClient(new ProviderDraftResult { Status = ProviderDraftStatus.ResponseInvalid, ErrorCode = "legacy_must_not_run" }), null, conversionClient);
    ProviderDslConversionActionRequest request = new ProviderDslConversionActionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = "科林是一名年轻而骄傲的女战士。"
    };
    AssertTrue(service.TryConfirmCloudEndpoint(request.Endpoint, out _), "cloud conversion endpoint confirmation must succeed");

    ProviderDslConversionActionResponse response = service.ConvertToDslAsync(request).GetAwaiter().GetResult();

    AssertTrue(response.Status == ProviderDraftActionStatus.Success && response.Draft != null, "missing local identity must be generated after sparse Provider parsing");
    AssertEqual("未命名角色", response.Draft!.DisplayName, "blank local display name must receive a local default");
    AssertTrue(response.Draft.Id.StartsWith("free.generated.", StringComparison.Ordinal), "missing stable ID must be generated locally");
    AssertTrue(response.Dsl.Contains("NAME=\"未命名角色\"", StringComparison.Ordinal), "generated identity must reach canonical DSL");
}
void RejectsDslConversionFallbackSuccessOnProviderFailure()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    ProviderDraftActionService service = new ProviderDraftActionService(vault, new FakeProviderDraftClient(new ProviderDraftResult
    {
        Status = ProviderDraftStatus.ResponseInvalid,
        ErrorCode = "provider.candidate_json_invalid"
    }));
    ProviderDslConversionActionRequest request = new ProviderDslConversionActionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = "科林是一个复杂角色。",
        LocalId = "free.generated.colin",
        LocalDisplayName = "科林"
    };
    AssertTrue(service.TryConfirmCloudEndpoint(request.Endpoint, out _), "cloud conversion endpoint confirmation must succeed");

    ProviderDslConversionActionResponse response = service.ConvertToDslAsync(request).GetAwaiter().GetResult();

    AssertTrue(response.Status == ProviderDraftActionStatus.InvalidRequest, "missing sparse conversion client must remain unavailable");
    AssertEqual("provider.intermediate_action_unavailable", response.ErrorCode, "conversion must not fall back to the legacy draft client");
    AssertTrue(string.IsNullOrEmpty(response.Dsl), "unavailable sparse conversion must not publish heuristic DSL");
    AssertTrue(!response.UsedLocalFallback, "conversion must not report a local fallback success");
}
void RejectsCoreOnlyPersonaDraftsAsUnstructured()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    ProviderDraftActionService service = new ProviderDraftActionService(
        vault,
        new FakeProviderDraftClient(new ProviderDraftResult { Status = ProviderDraftStatus.ResponseInvalid, ErrorCode = "legacy_must_not_run" }),
        null,
        new FakeProviderDslConversionClient(new ProviderDslConversionResult
        {
            Status = ProviderDraftStatus.Success,
            CandidateJson = "{\"summary\":null}"
        }));
    ProviderDslConversionActionRequest request = new ProviderDslConversionActionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = "科林的完整人物资料。",
        LocalId = "free.generated.colin",
        LocalDisplayName = "科林"
    };
    AssertTrue(service.TryConfirmCloudEndpoint(request.Endpoint, out _), "cloud conversion endpoint confirmation must succeed");

    ProviderDslConversionActionResponse response = service.ConvertToDslAsync(request).GetAwaiter().GetResult();

    AssertTrue(response.Status == ProviderDraftActionStatus.ProviderFailure, "empty grounded output must not be reported as structured conversion success");
    AssertEqual("provider.persona_structure_empty", response.ErrorCode, "empty structure must have a stable diagnostic");
    AssertTrue(string.IsNullOrEmpty(response.Dsl), "unstructured output must not overwrite the current DSL");
}
void RequestsSmallIntermediateJsonWithoutLocalMetadata()
{
    string? requestBody = null;
    const string candidate = "{\"summary\":\"谨慎而自持\",\"axes\":[{\"index\":0,\"value\":2,\"source\":\"谨慎\"}]}";
    string responseJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = candidate } } } });
    ProviderDslConversionClient client = new ProviderDslConversionClient(new DelegateHttpMessageHandler(async request =>
    {
        requestBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };
    }), PublicProviderEndpointResolver());
    ProviderDslConversionResult result = client.ConvertAsync(new ProviderDslConversionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = "谨慎而自持的角色。",
        LocalId = "free.generated.persona",
        LocalDisplayName = "测试角色"
    }).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "small intermediate JSON response must parse");
    AssertEqual(candidate, result.CandidateJson, "provider client must return only the intermediate candidate JSON");
    string capturedBody = requestBody ?? throw new InvalidOperationException("intermediate conversion request body was not captured");
    AssertTrue(capturedBody.Contains("Allowed root fields", StringComparison.Ordinal), "conversion prompt must enumerate the small root contract");
    AssertTrue(capturedBody.Contains("axes is an array", StringComparison.Ordinal), "conversion prompt must define sparse axis entries");
    AssertTrue(capturedBody.Contains("flags is an array", StringComparison.Ordinal), "conversion prompt must define sparse flag entries");
    AssertTrue(capturedBody.Contains("original order", StringComparison.Ordinal), "conversion prompt must require source-grounded text extraction");
    AssertTrue(capturedBody.Contains("copied word-for-word", StringComparison.Ordinal), "conversion prompt must require bounded same-language prose");
    AssertTrue(capturedBody.Contains("at most 8 entries", StringComparison.Ordinal), "conversion prompt must bound sparse axis count");
    AssertTrue(!capturedBody.Contains("free.generated.persona", StringComparison.Ordinal), "local ID must never be sent to the provider");
    AssertTrue(!capturedBody.Contains("测试角色", StringComparison.Ordinal), "local display name must never be sent to the provider");
    AssertTrue(!capturedBody.Contains("\"evidence\":", StringComparison.OrdinalIgnoreCase), "small intermediate conversion must not restore the old evidence object");
    AssertTrue(!capturedBody.Contains("[PERSONA_LOAD]", StringComparison.Ordinal), "default conversion must not ask the provider to author DSL");
    AssertTrue(capturedBody.Contains("\"temperature\":0", StringComparison.Ordinal), "intermediate conversion must use deterministic sampling");
    AssertTrue(capturedBody.Contains("\"response_format\":{\"type\":\"json_object\"}", StringComparison.Ordinal), "intermediate conversion must request provider JSON mode");
    AssertTrue(capturedBody.Contains("\"max_tokens\":4096", StringComparison.Ordinal), "short intermediate conversion must use the expanded safety ceiling");
}
void AcceptsWrappedIntermediateJsonContent()
{
    const string candidate = "{\"summary\":\"谨慎而自持\",\"axes\":[{\"index\":0,\"value\":2,\"source\":\"谨慎\"}]}";
    string responseJson = JsonSerializer.Serialize(new
    {
        choices = new[]
        {
            new { message = new { content = "Here is the JSON:\n" + candidate + "\n" } }
        }
    });
    ProviderDslConversionClient client = new ProviderDslConversionClient(new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
    })), PublicProviderEndpointResolver());

    ProviderDslConversionResult result = client.ConvertAsync(new ProviderDslConversionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = "谨慎而自持的角色。",
        LocalId = "free.generated.persona",
        LocalDisplayName = "测试角色"
    }).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.Success, "wrapped intermediate JSON must be recovered before local parsing");
    AssertEqual(candidate, result.CandidateJson, "recovered candidate must contain only the JSON object");
}
void AcceptsStringEncodedIntermediateJsonContent()
{
    const string candidate = "{\"summary\":\"谨慎而自持\",\"axes\":[{\"index\":0,\"value\":2,\"source\":\"谨慎\"}]}";
    string encodedCandidate = JsonSerializer.Serialize(candidate);
    string responseJson = JsonSerializer.Serialize(new
    {
        choices = new[]
        {
            new { message = new { content = encodedCandidate } }
        }
    });
    ProviderDslConversionClient client = new ProviderDslConversionClient(new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
    })), PublicProviderEndpointResolver());

    ProviderDslConversionResult result = client.ConvertAsync(new ProviderDslConversionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = "谨慎而自持的角色。",
        LocalId = "free.generated.persona",
        LocalDisplayName = "测试角色"
    }).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.Success, "one string-encoded intermediate JSON object must be decoded once");
    AssertEqual(candidate, result.CandidateJson, "decoded candidate must contain only the JSON object");
}
void ReportsMissingIntermediateJsonObjectsDistinctly()
{
    string responseJson = JsonSerializer.Serialize(new
    {
        choices = new[]
        {
            new { message = new { content = "I cannot produce the requested structure." } }
        }
    });
    ProviderDslConversionClient client = new ProviderDslConversionClient(new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
    })), PublicProviderEndpointResolver());

    ProviderDslConversionResult result = client.ConvertAsync(new ProviderDslConversionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = "谨慎而自持的角色。",
        LocalId = "free.generated.persona",
        LocalDisplayName = "测试角色"
    }).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.ResponseInvalid, "non-JSON Provider content must remain rejected");
    AssertEqual("provider.intermediate_candidate_object_missing", result.ErrorCode, "missing JSON objects must have a distinct diagnostic");
}
void ReportsTruncatedIntermediateCandidatesDistinctly()
{
    string responseJson = JsonSerializer.Serialize(new
    {
        choices = new[]
        {
            new { finish_reason = "length", message = new { content = "{\"summary\":\"被截断" } }
        }
    });
    ProviderDslConversionClient client = new ProviderDslConversionClient(new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
    })), PublicProviderEndpointResolver());

    ProviderDslConversionResult result = client.ConvertAsync(new ProviderDslConversionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = "谨慎而自持的角色。",
        LocalId = "free.generated.persona",
        LocalDisplayName = "测试角色"
    }).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.ResponseInvalid, "length-limited Provider content must remain rejected");
    AssertEqual("provider.intermediate_candidate_truncated", result.ErrorCode, "length finish reason must have a distinct diagnostic");
}
void ParsesStructuredProviderDraftCandidates()
{
    const string candidate = """
    {
      "id":"free.ai.sable",
      "displayName":"Sable",
      "core":"她把承诺视为必须验证的契约，面对未知关系保持审慎。",
      "identityFacts":"自由旅行者。",
      "summary":"她以可验证的承诺判断他人。",
      "privateDescription":"私下会反复核对别人是否兑现承诺。",
      "selfClaimRules":["不会替陌生人作无凭据的保证。"],
      "realSelfBehaviors":["独处时会重新核对已经听到的承诺。"],
      "selfClaimExamples":["我只接受能够证明的承诺。"],
      "tags":["trait.cautious"],
      "traitProfile":{"caution":1,"ambition":null},
      "expressionProfile":{},
      "behaviorProfile":{},
      "reactionProfile":{},
      "commitmentProfile":{},
      "evidence":{
        "identityFacts":"自由旅行者",
        "summary":"只相信可验证的承诺",
        "privateDescription":"只相信可验证的承诺",
        "selfClaimRules.0":"可验证的承诺",
        "realSelfBehaviors.0":"可验证的承诺",
        "selfClaimExamples.0":"可验证的承诺",
        "tags.trait.cautious":"只相信可验证的承诺",
        "traitProfile.caution":"只相信可验证的承诺"
      }
    }
    """;
    ProviderDraftClient client = CreateProviderClientForCandidate(candidate);

    ProviderDraftResult result = client.GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "valid structured response must produce a draft");
    AssertTrue(result.Draft != null, "successful response must include a draft");
    AssertEqual("free.ai.sable", result.Draft!.Id, "draft ID must come from structured output");
    AssertTrue(result.Draft.Tags.SequenceEqual(new[] { "trait.cautious" }), "draft tags must be parsed from structured output");
    AssertTrue(result.Draft.TraitProfile.Caution == 1, "draft axes must be parsed from structured output");
    AssertTrue(result.Draft.TraitProfile.Ambition == null, "explicit null axes must remain unset");
    AssertEqual("她以可验证的承诺判断他人。", result.Draft.Summary, "authored summary must be parsed");
    AssertTrue(result.Draft.SelfClaimRules.Count == 1 && result.Draft.RealSelfBehaviors.Count == 1 && result.Draft.SelfClaimExamples.Count == 1, "authored lists must be parsed");
    AssertEqual(CreateProviderRequest().Prompt, result.Draft.SourceDescription, "source description must be assigned locally from the request");
    AssertEqual("draft", result.Draft.Status, "provider candidates must remain drafts");
    AssertEqual("persona-load.v2", result.Draft.TemplateVersion, "template version must be assigned locally");
}

void ReportsInvalidProviderCoreDistinctly()
{
    const string candidate = """
    {"id":"free.ai.bad-core","displayName":"Sable","core":["not","text"],"identityFacts":"","tags":[],"evidence":{}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.ResponseInvalid, "a non-text core must be rejected");
    AssertEqual("provider.candidate_core_invalid", result.ErrorCode, "invalid core must not be misreported as a display-name failure");
}

void ReportsInvalidProviderIdentityDistinctly()
{
    const string candidate = """
    {"id":"free.ai.bad-identity","displayName":"Sable","core":"她很谨慎。","identityFacts":{"role":"traveler"},"tags":[],"evidence":{}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.ResponseInvalid, "a non-text identity must be rejected");
    AssertEqual("provider.candidate_identity_invalid", result.ErrorCode, "invalid identity must not be misreported as a display-name failure");
}
void IncludesJsonInstructionInProviderRequests()
{
    string? requestBody = null;
    const string candidate = """
    {"id":"free.ai.sable","displayName":"Sable","core":"她将承诺视作可检验的契约。","identityFacts":"自由旅行者。","tags":[],"evidence":{"identityFacts":"自由旅行者"}}
    """;
    string responseJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = candidate } } } });
    ProviderDraftClient client = new ProviderDraftClient(new DelegateHttpMessageHandler(async request =>
    {
        requestBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };
    }), PublicProviderEndpointResolver());

    ProviderDraftResult result = client.GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "JSON-instructed provider request must remain parseable");
    string capturedBody = requestBody ?? throw new InvalidOperationException("provider request body was not captured");
    AssertTrue(capturedBody.Contains("\"role\":\"system\"", StringComparison.Ordinal), "provider request must include a system instruction");
    AssertTrue(capturedBody.Contains("JSON object", StringComparison.Ordinal), "provider request must explicitly require JSON");
    AssertTrue(capturedBody.Contains("traitProfile.caution", StringComparison.Ordinal), "provider request must require evidence-backed profile paths");
    AssertTrue(capturedBody.Contains("Use null whenever", StringComparison.Ordinal), "provider request must prohibit guessing unsupported axes");
    AssertTrue(capturedBody.Contains("Do not return sourceDescription, templateVersion, status, or sourcePackId", StringComparison.Ordinal), "provider request must keep approval metadata local");
    AssertTrue(capturedBody.Contains("\"response_format\":{\"type\":\"json_object\"}", StringComparison.Ordinal), "provider request must enable JSON Output");
    AssertTrue(capturedBody.Contains("\"max_tokens\":4096", StringComparison.Ordinal), "provider request must leave enough room for a complete evidence-backed JSON draft while remaining bounded");
}

void RequiresExactSourceExcerptsInSparseConversionPrompt()
{
    string promptSource = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/ProviderDslConversionContract.cs"));
    AssertTrue(promptSource.Contains("copied word-for-word", StringComparison.Ordinal), "sparse conversion prompt must require exact source excerpts");
    AssertTrue(promptSource.Contains("never paraphrase", StringComparison.Ordinal), "sparse conversion prompt must forbid paraphrase");
}
void DisablesThinkingForOfficialDeepSeekDrafts()
{
    string? requestBody = null;
    const string candidate = "{\"id\":\"free.ai.deepseek\",\"displayName\":\"DeepSeek\",\"core\":\"她谨慎观察后再行动。\",\"identityFacts\":\"\",\"tags\":[],\"evidence\":{}}";
    string responseJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = candidate } } } });
    ProviderDraftClient client = new ProviderDraftClient(new DelegateHttpMessageHandler(async request =>
    {
        requestBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };
    }), PublicProviderEndpointResolver());

    ProviderDraftRequest request = CreateProviderRequest();
    request.Endpoint = "https://api.deepseek.com/chat/completions";
    ProviderDraftResult result = client.GenerateAsync(request).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "official DeepSeek response must remain parseable");
    AssertTrue((requestBody ?? string.Empty).Contains("\"thinking\":{\"type\":\"disabled\"}", StringComparison.Ordinal), "official DeepSeek drafts must disable thinking so reasoning cannot consume the structured answer budget");
}

void ReportsEmptyProviderCandidatesDistinctly()
{
    ProviderDraftResult result = CreateProviderClientForCandidate(string.Empty).GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.ResponseInvalid, "empty Provider content must be rejected");
    AssertEqual("provider.candidate_empty", result.ErrorCode, "empty Provider content must not be reported as an oversized candidate");
}
void AcceptsFencedProviderJson()
{
    const string candidate = """
    ```json
    {"id":"free.ai.fenced","displayName":"Sable","core":"她谨慎观察后再行动。","identityFacts":"","tags":[],"evidence":{}}
    ```
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "a single JSON code fence must be normalized before strict candidate validation");
    AssertEqual("free.ai.fenced", result.Draft!.Id, "fenced JSON must preserve the candidate data");
}

void AssignsLocalIdsToProviderDrafts()
{
    const string candidate = """
    {"id":null,"displayName":"Sable","core":"她谨慎观察后再行动。","identityFacts":"","tags":[],"evidence":{}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "Provider drafts must not fail when the model leaves the stable ID unset");
    AssertEqual("free.generated.persona", result.Draft!.Id, "stable draft IDs must be assigned locally rather than invented by the Provider");
}
void NormalizesProviderMultilineText()
{
    const string candidate = """
    {"id":"free.ai.multiline","displayName":"Sable","core":"谨慎观察。\n确认风险后行动。","identityFacts":"自由旅行者。\n依靠临时委托为生。","tags":[],"evidence":{"identityFacts":"自由旅行者"}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "ordinary multiline narrative strings must be normalized safely");
    AssertEqual("谨慎观察。 确认风险后行动。", result.Draft!.Core, "core line breaks must normalize to spaces");
    AssertEqual("自由旅行者。 依靠临时委托为生。", result.Draft.IdentityFacts, "identity line breaks must normalize to spaces");
}
void AcceptsNumericStringAndIntegralDecimalProviderAxes()
{
    const string candidate = """
    {"id":"free.ai.numeric","displayName":"科林","core":"她骄傲而进取。","identityFacts":"","tags":[],"traitProfile":{"ambition":1.0,"pride":"2"},"evidence":{"traitProfile.ambition":"进取","traitProfile.pride":"骄傲"}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(new ProviderDraftRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        Prompt = "科林骄傲而进取。"
    }).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.Success && result.Draft != null, "equivalent numeric Provider axes must parse");
    AssertTrue(result.Draft!.TraitProfile.Ambition == 1 && result.Draft.TraitProfile.Pride == 2, "numeric strings and integral decimals must normalize to integer axes");
}

void PrunesOneInvalidProviderAxisWithoutDiscardingDraft()
{
    const string candidate = """
    {"id":"free.ai.partial-axis","displayName":"科林","core":"她自尊强烈。","identityFacts":"","tags":["trait.proud"],"traitProfile":{"pride":"very"},"evidence":{"tags.trait.proud":"自尊强烈","traitProfile.pride":"自尊强烈"}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(new ProviderDraftRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        Prompt = "科林自尊强烈。"
    }).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.Success && result.Draft != null, "one malformed optional axis must not discard otherwise valid structure");
    AssertTrue(result.Draft!.TraitProfile.Pride == null, "malformed optional axis must remain unset");
    AssertTrue(result.Draft.Tags.SequenceEqual(new[] { "trait.proud" }), "independently evidenced registered tags must remain");
}
void NormalizesProfileTextArrays()
{
    const string candidate = """
    {"id":"free.ai.profile-list","displayName":"Sable","core":"她谨慎观察后再行动。","identityFacts":"","tags":[],"reactionProfile":{"sensitiveConditions":["公开羞辱","同伴遇险"],"conditionalResponses":["先警告","继续逼迫时反击"]},"commitmentProfile":{"priorityOrder":["同伴","尊严","利益"],"protectedValues":["承诺","家族"]},"evidence":{"reactionProfile.sensitiveConditions":"公开羞辱","reactionProfile.conditionalResponses":"反击","commitmentProfile.priorityOrder":"同伴","commitmentProfile.protectedValues":"承诺"}}
    """;
    ProviderDraftRequest request = CreateProviderRequest();
    request.Prompt = "她会在公开羞辱时先警告，继续逼迫时反击；同伴遇险时优先保护同伴，将尊严置于利益之前，并重视承诺与家族。";
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(request).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "profile narrative fields may use bounded string arrays");
    AssertEqual("公开羞辱；同伴遇险", result.Draft!.ReactionProfile.SensitiveConditions, "reaction profile arrays must normalize to one editable string");
    AssertEqual("同伴；尊严；利益", result.Draft.CommitmentProfile.PriorityOrder, "commitment profile arrays must normalize to one editable string");
}
void PrunesFieldsWhoseProviderEvidenceIsNotInSource()
{
    const string candidate = """
    {"id":"free.ai.partial-evidence","displayName":"科林","core":"她骄傲而谨慎。","identityFacts":"","tags":["trait.proud"],"traitProfile":{"caution":2,"pride":2},"reactionProfile":{"confrontation":2},"evidence":{"tags.trait.proud":"骄傲","traitProfile.caution":"从不冒险","traitProfile.pride":"骄傲","reactionProfile.confrontation":"直接迎击"}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(new ProviderDraftRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        Prompt = "科林骄傲，遇到敌人会直接迎击。"
    }).GetAwaiter().GetResult();

    AssertTrue(result.Status == ProviderDraftStatus.Success && result.Draft != null, "one paraphrased evidence item must not discard independently grounded structure");
    AssertTrue(result.Draft!.TraitProfile.Caution == null, "the axis with invalid evidence must be cleared");
    AssertTrue(result.Draft.TraitProfile.Pride == 2, "a valid axis in the same profile must remain");
    AssertTrue(result.Draft.ReactionProfile.Confrontation == 2, "independently grounded profile groups must remain");
    AssertTrue(result.Draft.Tags.SequenceEqual(new[] { "trait.proud" }), "independently grounded registered tags must remain");
}

void RejectsDraftsWhenInvalidEvidenceRemovesAllPersonaStructure()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    ProviderDraftActionService service = new ProviderDraftActionService(
        vault,
        new FakeProviderDraftClient(new ProviderDraftResult { Status = ProviderDraftStatus.ResponseInvalid, ErrorCode = "legacy_must_not_run" }),
        null,
        new FakeProviderDslConversionClient(new ProviderDslConversionResult
        {
            Status = ProviderDraftStatus.Success,
            CandidateJson = "{\"axes\":[{\"index\":0,\"value\":2,\"source\":\"从不冒险\"}]}"
        }));
    ProviderDslConversionActionRequest request = new ProviderDslConversionActionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        SourceText = "科林很复杂。",
        LocalId = "free.generated.colin",
        LocalDisplayName = "科林"
    };
    AssertTrue(service.TryConfirmCloudEndpoint(request.Endpoint, out _), "cloud conversion endpoint confirmation must succeed");

    ProviderDslConversionActionResponse response = service.ConvertToDslAsync(request).GetAwaiter().GetResult();

    AssertTrue(response.Status == ProviderDraftActionStatus.ProviderFailure, "a draft with no grounded structure after pruning must still fail");
    AssertEqual("provider.persona_structure_empty", response.ErrorCode, "empty grounded structure must retain the existing stable diagnostic");
    AssertTrue(string.IsNullOrEmpty(response.Dsl), "an empty grounded draft must not overwrite the DSL");
}
void RejectsProviderAxesWithoutSourceEvidence()
{
    const string candidate = """
    {"id":"free.ai.no-evidence","displayName":"Sable","core":"她面对陌生人时先观察再行动。","identityFacts":"","tags":[],"traitProfile":{"caution":2},"evidence":{}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "an inferred axis without evidence must be dropped rather than promoted");
    AssertTrue(result.Draft!.TraitProfile.Caution == null, "an axis without evidence must remain unset");
}

void DropsUnprovenProviderFields()
{
    const string candidate = """
    {"id":"free.ai.partial","displayName":"Sable","core":"她只相信可验证的承诺。","identityFacts":"自由旅行者。","tags":["trait.cautious"],"traitProfile":{"caution":1,"ambition":2},"evidence":{"identityFacts":"自由旅行者","traitProfile.caution":"可验证的承诺"}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "partially evidenced candidates must remain usable");
    AssertTrue(result.Draft!.TraitProfile.Caution == 1, "an independently evidenced axis must remain");
    AssertTrue(result.Draft.TraitProfile.Ambition == null, "only the axis without evidence must be cleared");
    AssertTrue(result.Draft.Tags.Count == 0, "a tag without evidence must be dropped");
}
void RejectsVerbatimProviderPromptCopies()
{
    const string candidate = """
    {"id":"free.ai.copy","displayName":"Sable","core":"她只相信可验证的承诺，是一名自由旅行者。","identityFacts":"","tags":[],"evidence":{}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.ResponseInvalid, "a verbatim prompt copy must not become the generated core");
    AssertEqual("provider.candidate_verbatim_copy_rejected", result.ErrorCode, "verbatim copies must have a stable error code");
}

void RejectsUnknownProviderProfileFields()
{
    const string candidate = """
    {"id":"free.ai.unknown","displayName":"Sable","core":"她将承诺视作需要证明的契约。","identityFacts":"","tags":[],"traitProfile":{"mysteryAxis":1},"evidence":{"traitProfile.mysteryAxis":"可验证的承诺"}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.ResponseInvalid, "unknown profile fields must be rejected");
    AssertEqual("provider.candidate_profile_unknown_field", result.ErrorCode, "unknown profile fields must have a stable error code");
}

void RejectsProviderAuthoredFieldsWithoutEvidence()
{
    const string candidate = """
    {"id":"free.ai.author-no-evidence","displayName":"Sable","core":"她将承诺视作需要验证的契约。","identityFacts":"","summary":"她谨慎判断所有承诺。","tags":[],"evidence":{}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "authored text without source evidence must be dropped rather than promoted");
    AssertTrue(string.IsNullOrWhiteSpace(result.Draft!.Summary), "authored text without evidence must be cleared");
}

/// <summary>
/// 上一批 76 张卡里有 52 张的自称规则是同一句模板（"自称『我』或【显示名】"），
/// 根因是草稿提示词只列了 selfClaimRules / realSelfBehaviors / selfClaimExamples 三个字段名，
/// 对"该写什么"一个字没规定 —— 模型只能照界面示例编。
/// 这条判据直接抓真实发出的请求体：规定必须在，可照抄的示例句式必须不在。
/// </summary>
void DraftPromptConstrainsAuthorArrays()
{
    string body = string.Empty;
    ProviderDraftClient client = new ProviderDraftClient(new DelegateHttpMessageHandler(async request =>
    {
        body = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "{\"core\":\"她只相信可验证的承诺。\"}" } } } }),
                System.Text.Encoding.UTF8, "application/json")
        };
    }), PublicProviderEndpointResolver());

    client.GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();

    using JsonDocument document = JsonDocument.Parse(body);
    JsonElement messages = document.RootElement.GetProperty("messages");
    string systemPrompt = messages[0].GetProperty("content").GetString() ?? string.Empty;

    AssertTrue(systemPrompt.Length > 0, "draft prompt must be dispatched");
    AssertTrue(
        systemPrompt.Contains("hold for any character", StringComparison.Ordinal),
        "draft prompt must say a rule that would hold for any character carries no information");
    AssertTrue(
        systemPrompt.Contains("return an empty array instead of a filler line", StringComparison.Ordinal),
        "draft prompt must allow an empty author array instead of a filler line");
    AssertTrue(
        systemPrompt.Contains("never reuse a sentence that would fit a different character", StringComparison.Ordinal),
        "draft prompt must forbid reusing a sentence across characters");
    AssertTrue(
        !systemPrompt.Contains("对外只自称", StringComparison.Ordinal),
        "draft prompt must not ship a ready-to-copy sample rule");
    AssertTrue(
        !systemPrompt.Contains("方宜", StringComparison.Ordinal),
        "draft prompt must not ship a sample display name");
}

void KeepsProviderApprovalMetadataLocal()
{
    const string candidate = """
    {"id":"free.ai.approval","displayName":"Sable","core":"她将承诺视作需要验证的契约。","identityFacts":"","status":"approved","tags":[],"evidence":{}}
    """;
    ProviderDraftResult result = CreateProviderClientForCandidate(candidate).GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.ResponseInvalid, "provider output must not set approval state");
    AssertEqual("provider.candidate_unknown_or_duplicate_field", result.ErrorCode, "approval metadata must remain an unknown provider field");
}

ProviderDraftClient CreateProviderClientForCandidate(string candidate)
{
    string responseJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = candidate } } } });
    return new ProviderDraftClient(new DelegateHttpMessageHandler(_ =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        })), PublicProviderEndpointResolver());
}

ProviderTextExpansionClient CreateExpansionClient(string expandedText)
{
    string responseJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = expandedText } } } });
    return CreateExpansionClientFromResponse(responseJson);
}

ProviderTextExpansionClient CreateExpansionClientFromResponse(string responseJson)
{
    return new ProviderTextExpansionClient(new DelegateHttpMessageHandler(_ =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        })), PublicProviderEndpointResolver());
}

ProviderTextExpansionRequest CreateExpansionRequest()
{
    return new ProviderTextExpansionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        Description = "年龄二十，傲娇，身材娇小，是个坚强的女战士，对敌人不存仁慈，但恋爱时手足无措。"
    };
}

ProviderDraftRequest CreateProviderRequest()
{
    return new ProviderDraftRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        Prompt = "她只相信可验证的承诺，是一名自由旅行者。"
    };
}

void PersistsProviderKeysOnlyAsProtectedData()
{
    ProviderSecretConfigurationService service = new ProviderSecretConfigurationService(new FakeSecretProtector(isAvailable: true));
    const string apiKey = "test-provider-secret-123";
    AssertTrue(service.TryCreatePersistent("https://api.example.com/v1", "test-model", apiKey, out ProviderPersistentConfiguration? configuration, out string errorCode), "available protector must create a persistent configuration: " + errorCode);
    AssertTrue(configuration != null, "persistent configuration is required");
    string serialized = JsonSerializer.Serialize(configuration);
    AssertTrue(!serialized.Contains(apiKey, StringComparison.Ordinal), "serialized configuration must not contain plaintext API key");
    AssertTrue(service.TryGetApiKey(configuration!, out string restored, out errorCode), "protected key must be readable by the backend: " + errorCode);
    AssertEqual(apiKey, restored, "protected key must round trip");
}

void FailsClosedWhenSecretProtectionIsUnavailable()
{
    ProviderSecretConfigurationService service = new ProviderSecretConfigurationService(new FakeSecretProtector(isAvailable: false));
    AssertTrue(!service.TryCreatePersistent("https://api.example.com/v1", "test-model", "test-provider-secret-123", out ProviderPersistentConfiguration? configuration, out string errorCode), "unavailable secret protection must reject persistence");
    AssertTrue(configuration == null, "failed persistence must not create configuration");
    AssertEqual("provider.secret_protection_unavailable", errorCode, "unavailable protection must have an explicit error code");
}

void RedactsProviderSecretsFromDiagnosticText()
{
    string redacted = ProviderSecretRedactor.Redact("Authorization: Bearer test-provider-secret-123; key=test-provider-secret-123", new[] { "test-provider-secret-123" });
    AssertTrue(!redacted.Contains("test-provider-secret-123", StringComparison.Ordinal), "registered secret must not remain in diagnostic text");
    AssertTrue(redacted.Contains("[REDACTED]", StringComparison.Ordinal), "redacted diagnostic text must mark removed secret values");
}

void KeepsTransientProviderKeysInMemoryOnly()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out string errorCode), "valid transient key must be accepted: " + errorCode);
    AssertTrue(vault.TryGet(out string key), "transient key must be available to the backend during the session");
    AssertEqual("test-provider-secret-123", key, "transient key must be returned unchanged");
    vault.Clear();
    AssertTrue(!vault.TryGet(out _), "cleared transient key must no longer be available");
}

void RoundTripsWindowsDpapiTestSecret()
{
    WindowsDpapiSecretProtector protector = new WindowsDpapiSecretProtector();
    if (!protector.IsAvailable) return;

    const string testSecret = "dpapi-test-";
    AssertTrue(protector.TryProtect(testSecret, out string protectedSecret, out string errorCode), "DPAPI must protect a test-only secret: " + errorCode);
    AssertTrue(!protectedSecret.Contains(testSecret, StringComparison.Ordinal), "DPAPI output must not contain test plaintext");
    AssertTrue(protector.TryUnprotect(protectedSecret, out string restored, out errorCode), "DPAPI must unprotect a test-only secret: " + errorCode);
    AssertEqual(testSecret, restored, "DPAPI test secret must round trip");
}

void RequiresExplicitCloudConfirmationBeforeProviderGeneration()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    FakeProviderDraftClient client = new FakeProviderDraftClient(CreateSuccessfulProviderResult());
    ProviderDraftActionService service = new ProviderDraftActionService(vault, client);

    ProviderDraftActionResponse unconfirmed = service.GenerateAsync(CreateProviderActionRequest("https://api.example.com/v1/chat/completions")).GetAwaiter().GetResult();
    AssertTrue(unconfirmed.Status == ProviderDraftActionStatus.CloudConfirmationRequired, "cloud generation must require explicit confirmation");
    AssertEqual("0", client.CallCount.ToString(), "unconfirmed cloud generation must not call the provider");

    AssertTrue(service.TryConfirmCloudEndpoint("https://api.example.com/v1/chat/completions", out string errorCode), "cloud endpoint confirmation must succeed: " + errorCode);
    ProviderDraftActionResponse confirmed = service.GenerateAsync(CreateProviderActionRequest("https://api.example.com/v1/chat/completions")).GetAwaiter().GetResult();
    AssertTrue(confirmed.Status == ProviderDraftActionStatus.Success, "confirmed cloud generation must produce a draft");
    AssertTrue(confirmed.Draft != null, "confirmed cloud generation must return an editable draft candidate");
    AssertEqual("1", client.CallCount.ToString(), "confirmed cloud generation must call the provider once");
    AssertEqual("test-provider-secret-123", client.LastRequest!.ApiKey!, "transient key must be forwarded only to the backend provider request");
}

void AllowsLoopbackProviderGenerationWithoutCloudConfirmation()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    FakeProviderDraftClient client = new FakeProviderDraftClient(CreateSuccessfulProviderResult());
    ProviderDraftActionService service = new ProviderDraftActionService(vault, client);

    ProviderDraftActionResponse response = service.GenerateAsync(CreateProviderActionRequest("http://127.0.0.1:11434/v1/chat/completions")).GetAwaiter().GetResult();
    AssertTrue(response.Status == ProviderDraftActionStatus.Success, "loopback generation must not need cloud confirmation");
    AssertEqual("1", client.CallCount.ToString(), "loopback generation must call the provider once");
}

void DoesNotCallProviderWithoutTransientKey()
{
    FakeProviderDraftClient client = new FakeProviderDraftClient(CreateSuccessfulProviderResult());
    ProviderDraftActionService service = new ProviderDraftActionService(new ProviderSessionKeyVault(), client);
    AssertTrue(service.TryConfirmCloudEndpoint("https://api.example.com/v1/chat/completions", out _), "cloud endpoint confirmation must succeed");

    ProviderDraftActionResponse response = service.GenerateAsync(CreateProviderActionRequest("https://api.example.com/v1/chat/completions")).GetAwaiter().GetResult();
    AssertTrue(response.Status == ProviderDraftActionStatus.SessionKeyMissing, "missing transient key must reject generation");
    AssertEqual("0", client.CallCount.ToString(), "missing transient key must not call the provider");
}

void AllowsKeylessLoopbackProviderGeneration()
{
    FakeProviderDraftClient client = new FakeProviderDraftClient(CreateSuccessfulProviderResult());
    ProviderDraftActionService service = new ProviderDraftActionService(new ProviderSessionKeyVault(), client);

    ProviderDraftActionResponse response = service.GenerateAsync(CreateProviderActionRequest("http://127.0.0.1:11434/v1/chat/completions")).GetAwaiter().GetResult();
    AssertTrue(response.Status == ProviderDraftActionStatus.Success, "loopback provider must support keyless generation");
    AssertTrue(client.LastRequest != null && string.IsNullOrEmpty(client.LastRequest.ApiKey), "keyless loopback request must not manufacture a key");
}

void RoutesTextExpansionThroughProviderSessionControls()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    FakeProviderTextExpansionClient expansionClient = new FakeProviderTextExpansionClient(new ProviderTextExpansionResult
    {
        Status = ProviderDraftStatus.Success,
        ExpandedText = "她在战场上果断冷酷，却会在亲密关系中以逞强掩饰慌乱。"
    });
    ProviderDraftActionService service = new ProviderDraftActionService(vault, new FakeProviderDraftClient(CreateSuccessfulProviderResult()), expansionClient);

    ProviderTextExpansionActionRequest request = new ProviderTextExpansionActionRequest
    {
        Endpoint = "https://api.example.com/v1/chat/completions",
        Model = "test-model",
        Description = "坚强但不擅长恋爱的年轻女战士。"
    };
    ProviderTextExpansionActionResponse unconfirmed = service.ExpandDescriptionAsync(request).GetAwaiter().GetResult();
    AssertTrue(unconfirmed.Status == ProviderDraftActionStatus.CloudConfirmationRequired, "cloud expansion must require explicit confirmation");
    AssertEqual("0", expansionClient.CallCount.ToString(), "unconfirmed expansion must not call the provider");

    AssertTrue(service.TryConfirmCloudEndpoint(request.Endpoint, out _), "cloud expansion endpoint confirmation must succeed");
    ProviderTextExpansionActionResponse confirmed = service.ExpandDescriptionAsync(request).GetAwaiter().GetResult();
    AssertTrue(confirmed.Status == ProviderDraftActionStatus.Success, "confirmed expansion must succeed");
    AssertTrue(confirmed.ExpandedText.Contains("亲密关系", StringComparison.Ordinal), "action response must return only expanded prose");
    AssertEqual("1", expansionClient.CallCount.ToString(), "confirmed expansion must call the provider once");
    AssertEqual("test-provider-secret-123", expansionClient.LastRequest!.ApiKey!, "expansion must use the transient session key");
}

void IsolatesFailedProviderResultsFromEditableDrafts()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    FakeProviderDraftClient client = new FakeProviderDraftClient(new ProviderDraftResult { Status = ProviderDraftStatus.ResponseInvalid, ErrorCode = "provider.candidate_validation_failed" });
    ProviderDraftActionService service = new ProviderDraftActionService(vault, client);
    AssertTrue(service.TryConfirmCloudEndpoint("https://api.example.com/v1/chat/completions", out _), "cloud endpoint confirmation must succeed");

    ProviderDraftActionResponse response = service.GenerateAsync(CreateProviderActionRequest("https://api.example.com/v1/chat/completions")).GetAwaiter().GetResult();
    AssertTrue(response.Status == ProviderDraftActionStatus.ProviderFailure, "invalid provider result must be isolated as a failure");
    AssertTrue(response.Draft == null, "failed provider result must not become an editable draft");
    AssertEqual("1", service.GetQuarantinedFailures().Count.ToString(), "failed provider result must be quarantined");
    AssertEqual("provider.candidate_validation_failed", service.GetQuarantinedFailures()[0].ErrorCode, "quarantine must retain only the safe failure code");
}

void SetsAndClearsOnlyTheProviderSessionKey()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    ProviderDraftActionService service = new ProviderDraftActionService(vault, new FakeProviderDraftClient(CreateSuccessfulProviderResult()));
    AssertTrue(service.TrySetSessionKey("test-provider-secret-123", out string errorCode), "session key setup must succeed: " + errorCode);
    AssertTrue(vault.TryGet(out string configuredKey), "configured session key must remain in the backend vault");
    AssertEqual("test-provider-secret-123", configuredKey, "session key must not be transformed by action service");
    AssertTrue(service.TryClearSessionKey(out string clearError), "transient key must be clearable: " + clearError);
    AssertTrue(!vault.TryGet(out _), "clearing provider settings must remove the in-memory key");
}

void RejectsCloudEndpointsThatResolveToPrivateAddresses()
{
    int handlerCalls = 0;
    ProviderDraftClient client = new ProviderDraftClient(
        new DelegateHttpMessageHandler(_ =>
        {
            handlerCalls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }),
        new FixedProviderEndpointResolver(IPAddress.Loopback));

    ProviderDraftResult result = client.GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.TransportError, "cloud endpoint resolving to loopback must be rejected");
    AssertEqual("provider.endpoint_resolution_rejected", result.ErrorCode, "private endpoint resolution must return an explicit safe error");
    AssertEqual("0", handlerCalls.ToString(), "rejected DNS resolution must prevent the HTTP request");
}

void KeepsProviderSettingsTransientInTheBrowser()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    string script = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));
    string program = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/Program.cs"));

    AssertTrue(html.Contains("id=\"provider-api-key\"", StringComparison.Ordinal), "settings UI must identify the transient API-key field");
    AssertTrue(html.Contains("type=\"password\"", StringComparison.Ordinal), "API key field must be masked");
    AssertTrue(html.Contains("autocomplete=\"off\"", StringComparison.Ordinal), "API key field must not request browser autofill persistence");
    AssertTrue(script.Contains("providerKeyInput.value = \"\"", StringComparison.Ordinal), "browser must clear the transient API-key field after submission");
    AssertTrue(!script.Contains("localStorage", StringComparison.Ordinal) && !script.Contains("sessionStorage", StringComparison.Ordinal), "provider settings script must not use browser persistent storage");
    AssertTrue(program.Contains("/api/provider/session-key", StringComparison.Ordinal) && program.Contains("/api/provider/clear-session-key", StringComparison.Ordinal), "provider settings routes must support explicit set and clear operations");
}

void SurfacesProviderCooldownMetadataWithoutRetrying()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    DateTimeOffset cooldown = DateTimeOffset.UtcNow.AddSeconds(30);
    FakeProviderDraftClient client = new FakeProviderDraftClient(new ProviderDraftResult
    {
        Status = ProviderDraftStatus.RateLimited,
        ErrorCode = "provider.rate_limited",
        CooldownUntilUtc = cooldown
    });
    ProviderDraftActionService service = new ProviderDraftActionService(vault, client);
    AssertTrue(service.TryConfirmCloudEndpoint("https://api.example.com/v1/chat/completions", out _), "cloud endpoint confirmation must succeed");

    ProviderDraftActionResponse response = service.GenerateAsync(CreateProviderActionRequest("https://api.example.com/v1/chat/completions")).GetAwaiter().GetResult();
    AssertTrue(response.Status == ProviderDraftActionStatus.ProviderFailure, "rate-limited provider result must not retry automatically");
    AssertTrue(response.ProviderStatus == ProviderDraftStatus.RateLimited, "response must preserve the provider rate-limit status");
    AssertTrue(response.CooldownUntilUtc == cooldown, "response must preserve cooldown metadata");
    AssertEqual("1", client.CallCount.ToString(), "rate-limit result must use exactly one provider call");
}

void ReportsABoundedProviderTimeout()
{
    ProviderDraftClient client = new ProviderDraftClient(
        new CancellationAwareHttpMessageHandler(async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }),
        PublicProviderEndpointResolver(),
        TimeSpan.FromMilliseconds(10));

    ProviderDraftResult result = client.GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.TransportError, "expired provider deadline must be classified as a transport failure");
    AssertEqual("provider.timeout", result.ErrorCode, "expired provider deadline must return an explicit timeout code");
}

void ReportsUserCancellationDistinctlyFromTimeout()
{
    CancellationAwareHttpMessageHandler handler = new CancellationAwareHttpMessageHandler(async cancellationToken =>
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK);
    });
    ProviderDraftClient client = new ProviderDraftClient(handler, PublicProviderEndpointResolver(), TimeSpan.FromSeconds(5));
    using CancellationTokenSource cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    ProviderDraftResult result = client.GenerateAsync(CreateProviderRequest(), cancellation.Token).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Cancelled, "user cancellation must remain distinct from provider timeout");
    AssertEqual("provider.cancelled", result.ErrorCode, "user cancellation must retain its explicit error code");
    AssertEqual("0", handler.CallCount.ToString(), "an already-cancelled request must not reach HTTP transport");
}

void ContainsProviderResolverFailures()
{
    ProviderDraftClient client = new ProviderDraftClient(
        new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))),
        new ThrowingProviderEndpointResolver());

    ProviderDraftResult result = client.GenerateAsync(CreateProviderRequest()).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.TransportError, "resolver exceptions must become bounded transport failures");
    AssertEqual("provider.endpoint_resolution_rejected", result.ErrorCode, "resolver exceptions need a stable failure code");
}

void BoundsQuarantinedProviderFailures()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    ProviderDraftActionService service = new ProviderDraftActionService(vault, new FakeProviderDraftClient(new ProviderDraftResult
    {
        Status = ProviderDraftStatus.ResponseInvalid,
        ErrorCode = "provider.test_failure"
    }));
    AssertTrue(service.TryConfirmCloudEndpoint("https://api.example.com/v1/chat/completions", out _), "cloud endpoint confirmation must succeed");

    for (int index = 0; index < 200; index++)
    {
        _ = service.GenerateAsync(CreateProviderActionRequest("https://api.example.com/v1/chat/completions")).GetAwaiter().GetResult();
    }

    AssertEqual("128", service.GetQuarantinedFailures().Count.ToString(), "quarantined failures must remain bounded");
}

void QuarantinesCancellationWithoutPromotingADraft()
{
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    AssertTrue(vault.TrySet("test-provider-secret-123", out _), "test key setup must succeed");
    ProviderDraftActionService service = new ProviderDraftActionService(vault, new FakeProviderDraftClient(new ProviderDraftResult
    {
        Status = ProviderDraftStatus.Cancelled,
        ErrorCode = "provider.cancelled"
    }));
    AssertTrue(service.TryConfirmCloudEndpoint("https://api.example.com/v1/chat/completions", out _), "cloud endpoint confirmation must succeed");

    ProviderDraftActionResponse response = service.GenerateAsync(CreateProviderActionRequest("https://api.example.com/v1/chat/completions")).GetAwaiter().GetResult();
    AssertTrue(response.Status == ProviderDraftActionStatus.ProviderFailure, "cancelled provider result must remain a non-success action result");
    AssertTrue(response.Draft == null && response.ProviderStatus == ProviderDraftStatus.Cancelled, "cancelled request must never promote a draft");
    AssertEqual("1", service.GetQuarantinedFailures().Count.ToString(), "cancelled provider request must be recorded as an isolated failure");
}

void RendersProviderResilienceControlsWithoutUnsafeHtml()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    string script = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));
    string program = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/Program.cs"));

    AssertTrue(html.Contains("id=\"cancel-provider-draft\"", StringComparison.Ordinal) && html.Contains("id=\"provider-failures\"", StringComparison.Ordinal), "resilience UI must expose cancellation and safe failure list containers");
    AssertTrue(script.Contains("AbortController", StringComparison.Ordinal) && script.Contains("generateProviderDraftButton.disabled", StringComparison.Ordinal), "generation UI must lock while a request is active");
    AssertTrue(script.Contains("failureItem.textContent", StringComparison.Ordinal) && !script.Contains("innerHTML", StringComparison.Ordinal), "failure records must render as text, never HTML");
    AssertTrue(script.Contains("工作台服务连接失败", StringComparison.Ordinal), "browser transport failures must be distinguished from provider responses");
    AssertTrue(script.Contains("Provider 返回失败（", StringComparison.Ordinal), "provider response failures must retain the error code");
    AssertTrue(script.Contains("工作台返回了无法解析的响应", StringComparison.Ordinal), "invalid local responses must be reported separately");
    AssertTrue(script.Contains("本机会话已过期", StringComparison.Ordinal), "expired sessions must not be misreported as invalid Provider endpoints");
    AssertTrue(script.Contains("本次会话尚未设置云端 API Key", StringComparison.Ordinal), "missing provider keys must have an actionable message");
    AssertTrue(script.Contains("provider.expansion_controls_invalid", StringComparison.Ordinal), "invalid expansion controls must have a stable browser message");
    AssertTrue(script.Contains("provider.expansion_envelope_size_invalid", StringComparison.Ordinal), "oversized expansion envelopes must have a stable browser message");
    AssertTrue(script.Contains("provider.expansion_truncated", StringComparison.Ordinal), "truncated expansion responses must have a stable browser message");
    AssertTrue(script.Contains("provider.expansion_response_shape_invalid", StringComparison.Ordinal), "malformed expansion response shapes must have a stable browser message");
    AssertTrue(script.Contains("provider.expansion_text_invalid", StringComparison.Ordinal), "invalid expansion text must have a stable browser message");
    AssertTrue(script.Contains("provider.request_in_flight", StringComparison.Ordinal), "cross-tab Provider conflicts must have a stable browser message");
    AssertTrue(!script.Contains("provider.expansion_format_invalid", StringComparison.Ordinal), "retired expansion error codes must not remain in the browser mapping");
    AssertTrue(script.Contains("setProviderSessionKeyFromInput", StringComparison.Ordinal), "generation must accept a key entered in the current form");
    AssertTrue(program.Contains("/api/provider/failures", StringComparison.Ordinal), "provider failure metadata must have an authorized local endpoint");
    AssertTrue(program.Contains("session.unauthorized", StringComparison.Ordinal), "protected routes must return a structured session error");
    AssertTrue(script.Contains("readJsonOrEmpty", StringComparison.Ordinal) && script.Contains("handleExpiredSession", StringComparison.Ordinal), "file actions must handle an expired session before consuming a JSON body");
}

void ProvidesAutomatedBrowserSmokeGate()
{
    string script = File.ReadAllText(FindWorkbenchSourceFile("tests/browser-smoke.ps1"));
    string project = File.ReadAllText(FindWorkbenchSourceFile("tests/PersonaWorkbench.BrowserSmoke/PersonaWorkbench.BrowserSmoke.csproj"));
    string source = File.ReadAllText(FindWorkbenchSourceFile("tests/PersonaWorkbench.BrowserSmoke/Program.cs"));

    AssertTrue(script.Contains("PersonaWorkbench.BrowserSmoke.csproj", StringComparison.Ordinal), "browser smoke entrypoint must run the checked-in test project");
    AssertTrue(project.Contains("net10.0", StringComparison.Ordinal), "browser smoke must use the existing local .NET toolchain");
    AssertTrue(source.Contains("--remote-debugging-port", StringComparison.Ordinal) && source.Contains("Runtime.evaluate", StringComparison.Ordinal), "browser smoke must drive real Edge through CDP");
    AssertTrue(source.Contains("rate-limit", StringComparison.Ordinal) && source.Contains("truncated", StringComparison.Ordinal) && source.Contains("malformed-json", StringComparison.Ordinal), "fake Provider must expose deterministic failure modes");
    AssertTrue(source.Contains("provider.request_in_flight", StringComparison.Ordinal), "browser smoke must verify the shared Provider operation gate");
}

void ExposesExplicitLocalApprovalWorkflow()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    string script = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));
    string program = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/Program.cs"));

    AssertTrue(html.Contains("id=\"review-status\"", StringComparison.Ordinal), "UI must show the local review status");
    AssertTrue(html.Contains("id=\"approve-document\"", StringComparison.Ordinal), "UI must expose an explicit approval action");
    AssertTrue(script.Contains("setReviewStatus(\"draft\")", StringComparison.Ordinal), "editing or Provider generation must be able to return the document to draft");
    AssertTrue(script.Contains("/api/documents/approve", StringComparison.Ordinal), "browser approval must use the dedicated local endpoint");
    AssertTrue(program.Contains("/api/documents/approve", StringComparison.Ordinal), "server must expose the authorized local approval endpoint");
}

void ProvidesSafeFreePreviewLaunchAndPackageGate()
{
    string launchScript = File.ReadAllText(FindWorkbenchSourceFile("start-free-preview.ps1"));
    string desktopLauncher = File.ReadAllText(FindWorkbenchSourceFile("launch-free-preview.vbs"));
    string stopScript = File.ReadAllText(FindWorkbenchSourceFile("stop-free-preview.ps1"));
    string packageScript = File.ReadAllText(FindWorkbenchSourceFile("package-free-preview.ps1"));
    string launcherSource = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Launcher/Program.cs"));
    string browserScript = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));
    string program = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/Program.cs"));

    AssertTrue(desktopLauncher.Contains("WScript.Shell", StringComparison.Ordinal) && desktopLauncher.Contains("start-free-preview.ps1", StringComparison.Ordinal), "desktop launcher must invoke the PowerShell launcher without relying on the shortcut shell association");
    AssertTrue(launchScript.Contains("127.0.0.1", StringComparison.Ordinal), "launcher must target loopback only");
    AssertTrue(launchScript.Contains("-WindowStyle Hidden", StringComparison.Ordinal), "launcher must keep the server process hidden");
    AssertTrue(launchScript.Contains("--no-browser", StringComparison.Ordinal), "launcher must centralize browser opening instead of racing the server process");
    AssertTrue(launchScript.Contains("Start-PersonaWorkbenchWindow", StringComparison.Ordinal), "launcher must use an explicit visible window helper");
    AssertTrue(launchScript.Contains("--app=", StringComparison.Ordinal), "launcher must prefer a dedicated Edge app window instead of a background browser tab");
    AssertTrue(launchScript.Contains("--user-data-dir=", StringComparison.Ordinal), "launcher must isolate the app window from the user\'s existing browser session");
    AssertTrue(launchScript.Contains("SetForegroundWindow", StringComparison.Ordinal), "launcher must bring the dedicated app window to the foreground");
    AssertTrue(launchScript.Contains("$null -ne $windowHandle", StringComparison.Ordinal) && launchScript.Contains("[IntPtr]::Zero", StringComparison.Ordinal), "launcher must reject a missing Edge window handle before calling Win32 activation APIs");
    AssertTrue(launchScript.Contains("MainModule.FileName", StringComparison.Ordinal) && launchScript.Contains("HasExited", StringComparison.Ordinal), "launcher must reuse the exact running executable and reject a failed duplicate process");
    AssertTrue(launchScript.Contains("startup_begin", StringComparison.Ordinal) && launchScript.Contains("startup_ready", StringComparison.Ordinal) && launchScript.Contains("startup_error", StringComparison.Ordinal), "startup script must record the complete startup lifecycle");
    AssertTrue(launchScript.Contains("BuildId", StringComparison.Ordinal) && launchScript.Contains("SourceManifestHash", StringComparison.Ordinal), "package startup logs must expose the validated build identity");
    AssertTrue(launcherSource.Contains("PersistFailureLog", StringComparison.Ordinal) && launcherSource.Contains("server.stderr.log", StringComparison.Ordinal), "visible launcher must create a fallback failure log when PowerShell cannot do so");
    AssertTrue(browserScript.Contains("/api/session/launch-token", StringComparison.Ordinal), "a direct or refreshed page must request a fresh one-time launch token");
    AssertTrue(program.Contains("/api/session/launch-token", StringComparison.Ordinal), "server must expose the loopback launch-token endpoint");
    AssertTrue(stopScript.Contains("MainModule.FileName", StringComparison.Ordinal) && stopScript.Contains(".Kill()", StringComparison.Ordinal), "stopper must verify the exact process path before stopping");
    AssertTrue(stopScript.Contains("Find-ExpectedProcesses", StringComparison.Ordinal), "stopper must recover from a stale PID by locating the exact executable path");
    AssertTrue(packageScript.Contains(".env", StringComparison.Ordinal) && packageScript.Contains(".history", StringComparison.Ordinal) && packageScript.Contains("*.map", StringComparison.Ordinal), "package gate must reject secrets, history and source maps");
    AssertTrue(packageScript.Contains("PersonaWorkbench.Web.csproj", StringComparison.Ordinal) && packageScript.Contains("--self-contained true", StringComparison.Ordinal) && packageScript.Contains("PublishSingleFile=true", StringComparison.Ordinal), "package script must publish a self-contained single-file app without a Node dependency");
    AssertTrue(packageScript.Contains("runtimeconfig.json", StringComparison.Ordinal) && packageScript.Contains("external .NET runtime", StringComparison.Ordinal), "package gate must reject accidental framework-dependent releases");
    AssertTrue(packageScript.Contains("UTF8Encoding($true)", StringComparison.Ordinal) && packageScript.Contains("PACKAGE-VERSION.txt", StringComparison.Ordinal), "package must be Windows PowerShell compatible and identify clean release folders");
    AssertTrue(launcherSource.Contains("PackageIntegrity.TryValidate", StringComparison.Ordinal) && launcherSource.Contains("PACKAGE-MANIFEST.sha256.txt", StringComparison.Ordinal), "launcher must reject mixed or corrupted package files before startup");
    AssertTrue(launcherSource.Contains("RedirectStandardOutput = false", StringComparison.Ordinal) && launcherSource.Contains("ReadFailureLog", StringComparison.Ordinal), "launcher must not block on output handles inherited by the background web process");
    AssertTrue(packageScript.Contains("launch-free-preview.vbs", StringComparison.Ordinal), "package must include the desktop-safe launcher");
    AssertTrue(packageScript.Contains("usageDocument", StringComparison.Ordinal) && packageScript.Contains("PersonaWorkbench-*.md", StringComparison.Ordinal), "package must include the user handbook");
    AssertTrue(browserScript.Contains("/api/provider/batch-progress/", StringComparison.Ordinal) && program.Contains("/api/provider/batch-progress/{batchId}", StringComparison.Ordinal), "batch progress must have a browser poller and an authorized Web route");
    AssertTrue(browserScript.Contains("formatBatchProgress", StringComparison.Ordinal) && browserScript.Contains("currentTitle", StringComparison.Ordinal), "batch UI must show live counts and the current source item");
}

string FindWorkbenchSourceFile(string relativePath)
{
    DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory != null)
    {
        string candidate = Path.Combine(directory.FullName, relativePath);
        if (File.Exists(candidate)) return candidate;
        directory = directory.Parent;
    }

    throw new FileNotFoundException("Unable to locate workbench source file.", relativePath);
}

ProviderDraftActionRequest CreateProviderActionRequest(string endpoint)
{
    return new ProviderDraftActionRequest
    {
        Endpoint = endpoint,
        Model = "test-model",
        Prompt = "她只相信可验证的承诺，是一名自由旅行者。"
    };
}

ProviderDraftResult CreateSuccessfulProviderResult()
{
    return new ProviderDraftResult
    {
        Status = ProviderDraftStatus.Success,
        Draft = new PersonaDocument
        {
            Id = "free.ai.sable",
            Core = "她只相信可验证的承诺。",
            Tags = new List<string> { "trait.cautious" }
        }
    };
}

IProviderEndpointResolver PublicProviderEndpointResolver()
{
    return new FixedProviderEndpointResolver(IPAddress.Parse("93.184.216.34"));
}

void AssertTrue(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

void AcceptsOnlyConsistentBoundedProviderUsage()
{
    using JsonDocument document = JsonDocument.Parse("{\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":8,\"total_tokens\":20}}");
    ProviderUsage? usage = ProviderUsage.TryParse(document.RootElement);
    AssertTrue(usage != null && usage.PromptTokens == 12 && usage.CompletionTokens == 8 && usage.TotalTokens == 20, "valid usage must be preserved");
}
void IgnoresMalformedProviderUsage()
{
    string[] samples = { "{\"usage\":null}", "{\"usage\":{\"prompt_tokens\":-1,\"completion_tokens\":1,\"total_tokens\":0}}", "{\"usage\":{\"prompt_tokens\":1.5,\"completion_tokens\":1,\"total_tokens\":2}}", "{\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1,\"total_tokens\":3}}", "{\"usage\":{\"prompt_tokens\":2147483647,\"completion_tokens\":1,\"total_tokens\":2147483648}}" };
    foreach (string sample in samples)
    {
        using JsonDocument document = JsonDocument.Parse(sample);
        AssertTrue(ProviderUsage.TryParse(document.RootElement) == null, "malformed usage must be unavailable");
    }
}

void PreservesOversizedDslRequestRejection()
{
    bool dispatched = false;
    ProviderDslConversionClient client = new ProviderDslConversionClient(new DelegateHttpMessageHandler(_ => { dispatched = true; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); }), PublicProviderEndpointResolver());
    ProviderDslConversionResult result = client.ConvertAsync(new ProviderDslConversionRequest { Endpoint = "https://api.example.com/v1/chat/completions", Model = "test-model", SourceText = new string('字', 25 * 1024), LocalId = "id", LocalDisplayName = "角色" }).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.InvalidRequest, "oversized source must remain an invalid request");
    AssertEqual("provider.intermediate_request_invalid", result.ErrorCode, "oversized source error code must remain stable");
    AssertTrue(!dispatched, "oversized source must not dispatch HTTP");
}
void NormalizesEvidenceWithoutChangingDocumentSource()
{
    PersonaTagRegistry registry = PersonaTagRegistry.CreateDefault();
    string documentSource = "角色\r\n谨慎。";
    PersonaIntermediateCandidateParseResult parsed = PersonaIntermediateCandidateParser.Parse("{\"summary\":\"谨慎。\"}", documentSource, "id", "角色", registry);
    AssertTrue(parsed.IsValid && parsed.Document != null, "normalized evidence source must validate the candidate");
    AssertEqual(documentSource, parsed.Document!.Core, "document source must preserve original line endings");
    AssertEqual(documentSource, parsed.Document.SourceDescription, "source description must preserve original document text");
}

void PreservesUsageOnInvalidSparseResponses()
{
    string responseJson = """{"usage":{"prompt_tokens":10,"completion_tokens":4,"total_tokens":14},"choices":[{"finish_reason":"length","message":{"content":"{\"axes\":["}}]}""";
    ProviderDslConversionClient client = new ProviderDslConversionClient(new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json") })), PublicProviderEndpointResolver());
    ProviderDslConversionResult result = client.ConvertAsync(new ProviderDslConversionRequest { Endpoint = "https://api.example.com/v1/chat/completions", Model = "test-model", SourceText = "谨慎角色。", LocalId = "id", LocalDisplayName = "角色" }).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.ResponseInvalid, "truncated sparse response must remain invalid");
    AssertTrue(result.Usage != null && result.Usage.TotalTokens == 14, "valid usage must survive invalid sparse response");
}
void PreservesUsageOnInvalidExpansionResponses()
{
    string responseJson = "{\"usage\":{\"prompt_tokens\":11,\"completion_tokens\":5,\"total_tokens\":16},\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"扩写未完成\"}}]}";
    ProviderTextExpansionClient client = new ProviderTextExpansionClient(new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json") })), PublicProviderEndpointResolver());
    ProviderTextExpansionResult result = client.ExpandAsync(new ProviderTextExpansionRequest { Endpoint = "https://api.example.com/v1/chat/completions", Model = "test-model", Description = "角色描述" }).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.ResponseInvalid, "truncated expansion response must remain invalid");
    AssertTrue(result.Usage != null && result.Usage.TotalTokens == 16, "valid usage must survive invalid expansion response");
}

void PreservesUsageWhenSparseCandidateParsingFails()
{
    ProviderUsage usage = new ProviderUsage(13, 7, 20);
    ProviderSessionKeyVault vault = new ProviderSessionKeyVault();
    FakeProviderDslConversionClient conversionClient = new FakeProviderDslConversionClient(new ProviderDslConversionResult
    {
        Status = ProviderDraftStatus.Success,
        CandidateJson = "{\"reaction\":{\"unknown\":\"内容\"}}",
        Usage = usage
    });
    ProviderDraftActionService service = new ProviderDraftActionService(
        vault,
        new FakeProviderDraftClient(new ProviderDraftResult { Status = ProviderDraftStatus.ResponseInvalid, ErrorCode = "provider.full_draft_must_not_run" }),
        null,
        conversionClient);
    ProviderDslConversionActionResponse response = service.ConvertToDslAsync(new ProviderDslConversionActionRequest
    {
        Endpoint = "http://127.0.0.1:11434/v1/chat/completions",
        Model = "test-model",
        SourceText = "内容",
        LocalId = "free.generated.test",
        LocalDisplayName = "测试角色"
    }).GetAwaiter().GetResult();

    AssertTrue(response.Status == ProviderDraftActionStatus.ProviderFailure, "invalid sparse candidate must remain a Provider failure");
    AssertEqual("persona.intermediate_unknown_or_duplicate_field", response.ErrorCode, "candidate parser diagnostic must remain observable");
    AssertTrue(response.Usage != null && response.Usage.TotalTokens == 20, "valid usage must survive sparse candidate parsing failure");
}

void UsesOllamaLowThinkingAndByteBudgets()
{
    string? expansionBody = null;
    ProviderTextExpansionClient expansionClient = new ProviderTextExpansionClient(new DelegateHttpMessageHandler(async request =>
    {
        expansionBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"message\":{\"content\":\"她很高傲，也很克制。\"},\"done\":true,\"done_reason\":\"stop\"}", System.Text.Encoding.UTF8, "application/json")
        };
    }), new FixedProviderEndpointResolver(IPAddress.Loopback));
    ProviderTextExpansionResult expansionResult = expansionClient.ExpandAsync(new ProviderTextExpansionRequest
    {
        Endpoint = "http://127.0.0.1:11434/v1/chat/completions",
        Model = "gpt-oss:20b",
        ProviderProtocol = ProviderProtocolKind.Ollama,
        Description = "她很高傲。"
    }).GetAwaiter().GetResult();
    AssertTrue(expansionResult.Status == ProviderDraftStatus.Success, "loopback Ollama expansion must dispatch");
    AssertTrue((expansionBody ?? string.Empty).Contains("\"think\":\"low\"", StringComparison.Ordinal), "Ollama expansion must request low thinking");
    AssertTrue((expansionBody ?? string.Empty).Contains("\"seed\":42", StringComparison.Ordinal), "Ollama expansion must use the stable local sampling seed");
    AssertTrue((expansionBody ?? string.Empty).Contains("\"num_predict\":3072", StringComparison.Ordinal), "short Ollama expansion must use the expanded safety ceiling");

    string? shortBody = null;
    ProviderDslConversionClient shortClient = new ProviderDslConversionClient(new DelegateHttpMessageHandler(async request =>
    {
        shortBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}", System.Text.Encoding.UTF8, "application/json") };
    }), new FixedProviderEndpointResolver(IPAddress.Loopback));
    ProviderDslConversionResult shortResult = shortClient.ConvertAsync(new ProviderDslConversionRequest
    {
        Endpoint = "http://127.0.0.1:11434/v1/chat/completions",
        Model = "gpt-oss:20b",
        SourceText = "短文本",
        ProviderProtocol = ProviderProtocolKind.Ollama
    }).GetAwaiter().GetResult();
    AssertTrue(shortResult.Status == ProviderDraftStatus.Success, "short Ollama conversion must dispatch");
    AssertTrue((shortBody ?? string.Empty).Contains("\"num_predict\":4096", StringComparison.Ordinal), "short Ollama DSL input must use the expanded safety ceiling");
    AssertTrue((shortBody ?? string.Empty).Contains("\"num_ctx\":16384", StringComparison.Ordinal), "short Ollama DSL input must use the adaptive context ceiling");
    AssertTrue((shortBody ?? string.Empty).Contains("\"format\":{", StringComparison.Ordinal), "native Ollama DSL input must use the constrained JSON schema");
    AssertTrue((shortBody ?? string.Empty).Contains("\"think\":\"low\"", StringComparison.Ordinal), "Ollama conversion must request low thinking");
    AssertTrue((shortBody ?? string.Empty).Contains("\"seed\":42", StringComparison.Ordinal), "Ollama conversion must use the stable local sampling seed");

    string? longBody = null;
    ProviderDslConversionClient longClient = new ProviderDslConversionClient(new DelegateHttpMessageHandler(async request =>
    {
        longBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}", System.Text.Encoding.UTF8, "application/json") };
    }), new FixedProviderEndpointResolver(IPAddress.Loopback));
    ProviderDslConversionResult longResult = longClient.ConvertAsync(new ProviderDslConversionRequest
    {
        Endpoint = "http://127.0.0.1:11434/v1/chat/completions",
        Model = "gpt-oss:20b",
        SourceText = new string('a', 4097),
        ProviderProtocol = ProviderProtocolKind.Ollama
    }).GetAwaiter().GetResult();
    AssertTrue(longResult.Status == ProviderDraftStatus.Success, "long Ollama conversion must dispatch");
    AssertTrue((longBody ?? string.Empty).Contains("\"num_predict\":6144", StringComparison.Ordinal), "4097-byte Ollama DSL input must use the expanded safety ceiling");
    AssertTrue((longBody ?? string.Empty).Contains("\"num_ctx\":32768", StringComparison.Ordinal), "4097-byte Ollama DSL input must use the adaptive context ceiling");
}

void DoesNotSendThinkForCompatibleLocalProviders()
{
    string? requestBody = null;
    ProviderDslConversionClient client = new ProviderDslConversionClient(new DelegateHttpMessageHandler(async request =>
    {
        requestBody = await request.Content!.ReadAsStringAsync();
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}", System.Text.Encoding.UTF8, "application/json") };
    }), new FixedProviderEndpointResolver(IPAddress.Loopback));
    ProviderDslConversionResult result = client.ConvertAsync(new ProviderDslConversionRequest
    {
        Endpoint = "http://127.0.0.1:1234/v1/chat/completions",
        Model = "local-model",
        SourceText = "兼容协议",
        ProviderProtocol = ProviderProtocolKind.OpenAiCompatible
    }).GetAwaiter().GetResult();
    AssertTrue(result.Status == ProviderDraftStatus.Success, "compatible local conversion must dispatch");
    AssertTrue(!(requestBody ?? string.Empty).Contains("\"think\"", StringComparison.Ordinal), "non-Ollama local providers must not receive the Ollama think option");
}

void ParsesNestedReactionAndCommitmentObjects()
{
    const string source = "她被迫表态时会公开回击；她把部队运作置于高傲品德之前。";
    PersonaIntermediateCandidateParseResult result = PersonaIntermediateCandidateParser.Parse(
        "{\"reaction\":{\"sensitiveConditions\":\"被迫表态\",\"conditionalResponses\":\"公开回击\"},\"commitment\":{\"priorityOrder\":\"部队运作\",\"protectedValues\":\"高傲品德\"}}",
        source,
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());
    AssertTrue(result.IsValid && result.Document != null, "nested reaction and commitment objects must parse");
    AssertEqual("被迫表态", result.Document!.ReactionProfile.SensitiveConditions, "reaction sensitive conditions must be preserved");
    AssertEqual("公开回击", result.Document.ReactionProfile.ConditionalResponses, "reaction responses must be preserved");
    AssertEqual("部队运作", result.Document.CommitmentProfile.PriorityOrder, "commitment priority must be preserved");
    AssertEqual("高傲品德", result.Document.CommitmentProfile.ProtectedValues, "commitment values must be preserved");

    PersonaIntermediateCandidateParseResult nullResult = PersonaIntermediateCandidateParser.Parse(
        "{\"reaction\":null,\"commitment\":null}",
        source,
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());
    AssertTrue(nullResult.IsValid, "null nested profiles must mean unknown rather than invalid");
}

void RejectsStringReactionAndCommitmentReplacements()
{
    PersonaIntermediateCandidateParseResult result = PersonaIntermediateCandidateParser.Parse(
        "{\"reaction\":\"先观察后回击\",\"commitment\":\"谨慎承诺\"}",
        "先观察后回击，谨慎承诺。",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());
    AssertTrue(!result.IsValid, "string reaction and commitment replacements must be rejected");
    AssertEqual("persona.intermediate_unknown_or_duplicate_field", result.ErrorCode, "nested type violations need the stable candidate error");
}

void BuildsAwakeAuthoringPreviewWithoutProvider()
{
    AwakeAuthoringPreviewRequest request = new AwakeAuthoringPreviewRequest
    {
        Id = "web.fixture.authoring",
        DisplayName = "Web 测试角色",
        Core = "手写核心。",
        IdentityFacts = "来自边境。",
        SourcePackId = "fixture_pack",
        TemplateVersion = "persona-load.v2",
        Status = "draft",
        SourceDescription = "确认来源。",
        Tags = new List<string> { "trait.pragmatic" },
        FacetStrengths = new Dictionary<string, int>(StringComparer.Ordinal),
        ExpandedText = "单独扩充。",
        ExpandedTextOrigin = "provider"
    };

    AwakeAuthoringPreviewResponse response = AwakeAuthoringPreviewService.Build(request);

    AssertTrue(response.IsValid, "authoring preview must not require a Provider call");
    AssertTrue(response.CanonicalJson.Contains("awake.persona.authoring.v2", StringComparison.Ordinal), "authoring preview must return v2 JSON");
    AssertEqual("provider", response.ExpandedTextOrigin, "preview diagnostics must retain expansion origin");
    AssertTrue(response.CanonicalUtf8Base64.Length > 0, "preview must expose canonical bytes for download");
    AssertEqual(response.CanonicalSha256, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Convert.FromBase64String(response.CanonicalUtf8Base64))), "download bytes must match preview hash");
}

void KeepsAuthoringPreviewFailuresNonDestructive()
{
    AwakeAuthoringPreviewRequest request = new AwakeAuthoringPreviewRequest
    {
        Id = "invalid-id",
        DisplayName = "失败角色",
        Core = "手写核心。",
        SourceDescription = "确认来源。",
        ExpandedText = string.Empty,
        ExpandedTextOrigin = "none"
    };

    AwakeAuthoringPreviewResponse response = AwakeAuthoringPreviewService.Build(request);

    AssertTrue(!response.IsValid, "invalid authoring input must be rejected");
    AssertEqual(string.Empty, response.CanonicalJson, "failed preview must not return replacement JSON");
    AssertEqual(string.Empty, response.CanonicalUtf8Base64, "failed preview must not return replacement bytes");
    AssertTrue(response.Errors.Count > 0, "failed preview must expose diagnostics");
}

void ExposesAwakeAuthoringPreviewAndDownloadControls()
{
    string html = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/index.html"));
    string script = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/wwwroot/app.js"));
    string program = File.ReadAllText(FindWorkbenchSourceFile("src/PersonaWorkbench.Web/Program.cs"));

    AssertTrue(html.Contains("id=\"generate-awake-authoring-preview\"", StringComparison.Ordinal), "UI must expose the authoring-v2 preview action");
    AssertTrue(html.Contains("id=\"download-awake-authoring\"", StringComparison.Ordinal), "UI must expose the authoring-v2 download action");
    AssertTrue(html.Contains("id=\"authoring-preview-output\"", StringComparison.Ordinal), "UI must expose a separate authoring preview surface");
    AssertTrue(script.Contains("/api/preview/awake-authoring", StringComparison.Ordinal), "browser must call the authoring preview route");
    AssertTrue(script.Contains("canonicalUtf8Base64", StringComparison.Ordinal), "browser download must use returned canonical bytes");
    AssertTrue(program.Contains("/api/preview/awake-authoring", StringComparison.Ordinal), "Web API must expose the authoring preview route");
}

void ReportsRunningBatchProgressWithoutWaitingForCompletion()
{
    BlockingSecondProviderDslConversionClient conversionClient = new BlockingSecondProviderDslConversionClient(SuccessfulBatchConversion(), SuccessfulBatchConversion());
    BatchGenerationService service = new BatchGenerationService(CreateBatchActionService(conversionClient));
    BatchGenerationRequest request = CreateBatchRequest(
        "batch-progress-20260830",
        CreateBatchItem("batch-item-1", 1, "第一段资料，角色骄傲。"),
        CreateBatchItem("batch-item-2", 2, "第二段资料，角色骄傲。"));

    Task<BatchGenerationResponse> pending = service.GenerateAsync(request);
    AssertTrue(conversionClient.WaitUntilSecondCall(), "batch must expose progress while the second Provider call is pending");
    BatchGenerationResponse? progress = service.GetProgress(request.BatchId);
    AssertTrue(progress != null, "running batch progress must be queryable");
    AssertEqual(BatchGenerationStatuses.Running, progress!.Status, "pending batch must report running status");
    AssertEqual("1", progress.Completed.ToString(), "progress must count the completed first item");
    AssertEqual("2", progress.CurrentSourceOrdinal.ToString(), "progress must identify the current source ordinal");
    AssertEqual("1", progress.Results.Count.ToString(), "progress must retain completed result cards");

    conversionClient.CompleteSecond(SuccessfulBatchConversion());
    BatchGenerationResponse response = pending.GetAwaiter().GetResult();
    AssertEqual(BatchGenerationStatuses.Completed, response.Status, "completed batch must return its final status");
    AssertEqual("2", response.Completed.ToString(), "final response must count both processed items");
    AssertTrue(service.GetProgress(request.BatchId)?.Status == BatchGenerationStatuses.Completed, "final progress must remain available for the current page");
}

void RejectsDuplicateBatchSourceOrdinals()
{
    FakeProviderDslConversionClient conversionClient = new FakeProviderDslConversionClient(SuccessfulBatchConversion());
    BatchGenerationService service = new BatchGenerationService(CreateBatchActionService(conversionClient));
    BatchGenerationResponse response = service.GenerateAsync(CreateBatchRequest(
        "batch-duplicate-ordinal-20260830",
        CreateBatchItem("batch-item-1", 1, "第一段资料，角色骄傲。"),
        CreateBatchItem("batch-item-2", 1, "第二段资料，角色骄傲。"))).GetAwaiter().GetResult();

    AssertEqual(BatchGenerationStatuses.Invalid, response.Status, "duplicate source ordinals must reject the batch");
    AssertEqual("batch.source_ordinal_invalid", response.Errors.Single(), "duplicate source ordinals need a direct error code");
    AssertEqual("0", conversionClient.CallCount.ToString(), "invalid source ordering must not call the Provider");
}

void PreservesSuccessfulResultsWhenBatchItemFails()
{
    SequencedProviderDslConversionClient conversionClient = new SequencedProviderDslConversionClient(call => call == 1
        ? SuccessfulBatchConversion()
        : new ProviderDslConversionResult { Status = ProviderDraftStatus.ResponseInvalid, ErrorCode = "provider.test_failure" });
    BatchGenerationService service = new BatchGenerationService(CreateBatchActionService(conversionClient));
    BatchGenerationResponse response = service.GenerateAsync(CreateBatchRequest(
        "batch-partial-20260830",
        CreateBatchItem("batch-item-1", 1, "第一段资料，角色骄傲。"),
        CreateBatchItem("batch-item-2", 2, "第二段资料，角色骄傲。"))).GetAwaiter().GetResult();

    AssertEqual(BatchGenerationStatuses.Partial, response.Status, "one Provider failure must produce a partial batch");
    AssertEqual("1", response.Succeeded.ToString(), "successful results must remain counted");
    AssertEqual("1", response.Failed.ToString(), "failed results must remain counted");
    AssertTrue(response.Results[0].Success, "the first result must remain successful");
    AssertEqual("provider.test_failure", response.Results[1].ErrorCode, "the failed result must preserve its Provider error");
}

void CancelsBatchBeforeStartingTheNextItem()
{
    BlockingSecondProviderDslConversionClient conversionClient = new BlockingSecondProviderDslConversionClient(SuccessfulBatchConversion(), SuccessfulBatchConversion());
    BatchGenerationService service = new BatchGenerationService(CreateBatchActionService(conversionClient));
    BatchGenerationRequest request = CreateBatchRequest(
        "batch-cancel-20260830",
        CreateBatchItem("batch-item-1", 1, "第一段资料，角色骄傲。"),
        CreateBatchItem("batch-item-2", 2, "第二段资料，角色骄傲。"),
        CreateBatchItem("batch-item-3", 3, "第三段资料，角色骄傲。"));

    Task<BatchGenerationResponse> pending = service.GenerateAsync(request);
    AssertTrue(conversionClient.WaitUntilSecondCall(), "batch must reach the second Provider call before cancellation");
    BatchCancelResponse cancel = service.Cancel(request.BatchId);
    AssertTrue(cancel.IsAccepted, "active batch cancellation must be accepted");
    conversionClient.CompleteSecond(SuccessfulBatchConversion());
    BatchGenerationResponse response = pending.GetAwaiter().GetResult();

    AssertEqual(BatchGenerationStatuses.Cancelled, response.Status, "cancelled batch must report cancelled status");
    AssertEqual("2", response.Completed.ToString(), "the in-flight item may finish before cancellation settles");
    AssertEqual("2", conversionClient.CallCount.ToString(), "cancellation must prevent the next item from starting");
    AssertEqual("batch.not_started_after_cancel", response.Results[2].ErrorCode, "unstarted items must be marked as cancelled");
    AssertEqual("batch.not_active", service.Cancel(request.BatchId).ErrorCode, "a finished batch must not be cancelled again");
}

void KeepsMalformedBatchCandidatesIsolated()
{
    FakeProviderDslConversionClient conversionClient = new FakeProviderDslConversionClient(new ProviderDslConversionResult
    {
        Status = ProviderDraftStatus.Success,
        CandidateJson = "{ malformed"
    });
    BatchGenerationService service = new BatchGenerationService(CreateBatchActionService(conversionClient));
    BatchGenerationResponse response = service.GenerateAsync(CreateBatchRequest(
        "batch-malformed-20260830",
        CreateBatchItem("batch-item-1", 1, "第一段资料，角色骄傲。"))).GetAwaiter().GetResult();

    AssertEqual(BatchGenerationStatuses.Partial, response.Status, "a malformed candidate must not be reported as a completed batch");
    AssertEqual("1", response.Failed.ToString(), "the malformed candidate must be counted as failed");
    AssertTrue(!response.Results[0].Success, "a malformed candidate must not become an editable success");
    AssertEqual("第一段资料，角色骄傲。", response.Results[0].SourceText, "the failed result must preserve its source text");
    AssertTrue(!string.IsNullOrWhiteSpace(response.Results[0].ErrorCode), "the malformed candidate must expose an error code");
    AssertEqual(string.Empty, response.Results[0].Dsl, "a malformed candidate must not publish DSL");
}

void AssignsUniqueGlobalOrdinalsToMaterialSegments()
{
    MaterialSegmentResponse response = MaterialImportService.Segment(new MaterialSegmentRequest
    {
        Sources = new List<MaterialSourceRequest>
        {
            new MaterialSourceRequest
            {
                SourceFile = "测试资料.md",
                Text = "第一段资料，角色骄傲。\n\n第二段资料，角色谨慎。"
            }
        }
    });

    AssertTrue(response.IsValid, "valid source text must be segmented");
    AssertEqual("2", response.Segments.Count.ToString(), "two paragraphs must produce two material segments");
    AssertEqual("1", response.Segments[0].SourceOrdinal.ToString(), "first material segment must receive ordinal one");
    AssertEqual("2", response.Segments[1].SourceOrdinal.ToString(), "second material segment must receive ordinal two");
    AssertTrue(response.Segments.Select(segment => segment.SourceOrdinal).Distinct().Count() == response.Segments.Count, "material segment ordinals must be unique");
}


void AuthoringRoutesRequireSessionAndCsrf()
{
    PersonaContractClosureService closure = new PersonaContractClosureService();
    WorkbenchSessionManager sessions = new WorkbenchSessionManager("bootstrap-fixture");
    ApprovalReceiptRouteRequest request = new ApprovalReceiptRouteRequest { Document = CreateContractDocument(), RequestId = "route-receipt", EvidenceId = "fixture-evidence", Fence = 1 };
    DefaultHttpContext unauthorized = new DefaultHttpContext();
    (int status, string body) denied = ExecuteResult(PersonaContractRouteFactory.IssueReceipt(unauthorized.Request, request, sessions, closure));
    AssertEqual("401", denied.status.ToString(), "receipt route must reject missing session authorization");
    AssertTrue(denied.body.Contains("session.unauthorized", StringComparison.Ordinal), "unauthorized response must be machine-readable");
    AssertTrue(!Directory.Exists(Path.Combine(Path.GetTempPath(), "awake-write-fixture")), "fixture must not write AWAKE output");
}

void AuthoringRouteHandoffIsIdempotentAndFenced()
{
    PersonaContractClosureService closure = new PersonaContractClosureService();
    WorkbenchSessionManager sessions = new WorkbenchSessionManager("bootstrap-fixture-2");
    AssertTrue(sessions.TryExchange("bootstrap-fixture-2", out WorkbenchSessionGrant grant), "fixture session exchange must succeed");
    DefaultHttpContext context = new DefaultHttpContext();
    context.Request.Headers["X-Pwb-Session"] = grant.SessionToken;
    context.Request.Headers["X-Pwb-Csrf"] = grant.CsrfToken;
    PersonaDocument document = CreateContractDocument();
    ApprovalReceiptRouteRequest receiptRequest = new ApprovalReceiptRouteRequest { Document = document, RequestId = "route-receipt-2", EvidenceId = "fixture-evidence-2", Fence = 1 };
    ApprovalReceipt receipt = JsonSerializer.Deserialize<ApprovalReceipt>(ExecuteResult(PersonaContractRouteFactory.IssueReceipt(context.Request, receiptRequest, sessions, closure)).body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) ?? throw new InvalidOperationException("receipt response must deserialize");
    AuthoringHandoffRouteRequest handoffRequest = new AuthoringHandoffRouteRequest { Receipt = receipt, Document = document, WorkspaceId = "workspace.route", Revision = 4, RequestId = "route-handoff-2", Fence = 2 };
    string firstBody = ExecuteResult(PersonaContractRouteFactory.IssueHandoff(context.Request, handoffRequest, sessions, closure)).body;
    string secondBody = ExecuteResult(PersonaContractRouteFactory.IssueHandoff(context.Request, handoffRequest, sessions, closure)).body;
    AuthoringHandoff first = JsonSerializer.Deserialize<AuthoringHandoff>(firstBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) ?? throw new InvalidOperationException("handoff response must deserialize");
    AuthoringHandoff second = JsonSerializer.Deserialize<AuthoringHandoff>(secondBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) ?? throw new InvalidOperationException("retry response must deserialize");
    AssertEqual(first.HandoffId, second.HandoffId, "identical handoff request must be idempotent");
    AssertEqual("workspace.route", first.Envelope.WorkspaceId, "handoff route must preserve workspace_id");
    AssertEqual("issued", first.Envelope.LifecycleStatus, "legacy CLR lifecycle view must remain issued");
    AssertEqual("local_approved", first.Envelope.ReviewStatus, "legacy CLR review view must remain local approval");
    AssertEqual("awake.workstation.handoff-envelope.v1", first.Envelope.SchemaVersion, "handoff route must return the shared envelope schema");
    AssertEqual("persona_workbench", first.Envelope.Producer, "handoff route must identify Persona Workbench as producer");
    AssertTrue(first.Envelope.ReviewOnly, "handoff route must mark the envelope review-only");
    AssertEqual("approved_local", first.Envelope.SharedReviewStatus, "shared envelope must use approved_local");
    AssertEqual(first.CanonicalJson, first.Envelope.Payload, "shared envelope payload must contain the canonical Persona JSON");
    AssertTrue(first.Envelope.SharedContentSha256 == first.ContentSha256.ToLowerInvariant(), "shared envelope hash must be lowercase and bound to the payload");
    AssertTrue(first.Envelope.RequestFingerprint.Length == 64 && first.Envelope.RequestFingerprint.All(Uri.IsHexDigit), "shared envelope fingerprint must be present");
    using (JsonDocument envelopeJson = JsonDocument.Parse(JsonSerializer.Serialize(first.Envelope)))
    {
        JsonElement envelope = envelopeJson.RootElement;
        AssertTrue(envelope.TryGetProperty("schema_version", out _), "serialized envelope must expose schema_version");
        AssertTrue(envelope.TryGetProperty("payload", out _), "serialized envelope must expose payload");
        AssertTrue(envelope.TryGetProperty("request_fingerprint", out _), "serialized envelope must expose request_fingerprint");
        AssertTrue(!envelope.TryGetProperty("expires_at", out _), "serialized shared envelope must not expose legacy expires_at");
        AssertTrue(!envelope.TryGetProperty("lifecycle_status", out _), "serialized shared envelope must not expose consumer lifecycle_status");
    }
    AssertEqual("not_requested", first.AwakeApproval, "handoff route must not claim AWAKE approval");
    DefaultHttpContext oldFence = new DefaultHttpContext();
    oldFence.Request.Headers["X-Pwb-Session"] = grant.SessionToken;
    oldFence.Request.Headers["X-Pwb-Csrf"] = grant.CsrfToken;
    (int oldStatus, string oldBody) oldResult = ExecuteResult(PersonaContractRouteFactory.IssueHandoff(oldFence.Request, new AuthoringHandoffRouteRequest { Receipt = receipt, Document = document, WorkspaceId = "workspace.route", Revision = 4, RequestId = "route-handoff-old", Fence = 1 }, sessions, closure));
    AssertEqual("409", oldResult.oldStatus.ToString(), "old fence must be rejected");
    AssertTrue(oldResult.oldBody.Contains("session.fence_old", StringComparison.Ordinal), "old fence rejection must be explicit");
}


void RunsAuthoringRoutesInProcessTestHost()
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(PersonaContractRouteFactory).Assembly.GetName().Name });
    builder.WebHost.UseTestServer();
    WebApplication app = builder.Build();
    WorkbenchSessionManager sessions = new WorkbenchSessionManager("testhost-bootstrap");
    PersonaContractClosureService closure = new PersonaContractClosureService();
    app.MapPost("/api/authoring/receipt", (HttpRequest request, ApprovalReceiptRouteRequest body) => PersonaContractRouteFactory.IssueReceipt(request, body, sessions, closure));
    app.MapPost("/api/authoring/handoff", (HttpRequest request, AuthoringHandoffRouteRequest body) => PersonaContractRouteFactory.IssueHandoff(request, body, sessions, closure));
    app.StartAsync().GetAwaiter().GetResult();
    try
    {
        using HttpClient client = app.GetTestClient();
        AssertTrue(sessions.TryExchange("testhost-bootstrap", out WorkbenchSessionGrant grant), "TestHost fixture session exchange must succeed");
        client.DefaultRequestHeaders.Add("X-Pwb-Session", grant.SessionToken);
        client.DefaultRequestHeaders.Add("X-Pwb-Csrf", grant.CsrfToken);
        PersonaDocument document = CreateContractDocument();
        HttpResponseMessage receiptResponse = client.PostAsJsonAsync("/api/authoring/receipt", new ApprovalReceiptRouteRequest { Document = document, RequestId = "testhost-receipt", EvidenceId = "testhost-evidence", Fence = 1 }).GetAwaiter().GetResult();
        AssertEqual("200", ((int)receiptResponse.StatusCode).ToString(), "TestHost receipt route must return success");
        ApprovalReceipt receipt = receiptResponse.Content.ReadFromJsonAsync<ApprovalReceipt>().GetAwaiter().GetResult() ?? throw new InvalidOperationException("TestHost receipt must deserialize");
        HttpResponseMessage handoffResponse = client.PostAsJsonAsync("/api/authoring/handoff", new AuthoringHandoffRouteRequest { Receipt = receipt, Document = document, WorkspaceId = "workspace.testhost", Revision = 9, RequestId = "testhost-handoff", Fence = 2 }).GetAwaiter().GetResult();
        AssertEqual("200", ((int)handoffResponse.StatusCode).ToString(), "TestHost handoff route must return success: " + handoffResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        AuthoringHandoff handoff = handoffResponse.Content.ReadFromJsonAsync<AuthoringHandoff>().GetAwaiter().GetResult() ?? throw new InvalidOperationException("TestHost handoff must deserialize");
        AssertEqual("not_requested", handoff.AwakeApproval, "TestHost handoff must not claim AWAKE approval");
        AssertEqual("workspace.testhost", handoff.Envelope.WorkspaceId, "TestHost handoff must include the cross-workstation envelope");
        AssertEqual("9", handoff.Envelope.Revision.ToString(), "TestHost handoff must preserve the document revision");
    }
    finally
    {
        app.StopAsync().GetAwaiter().GetResult();
        app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}

void RejectsInvalidCrossWorkstationHandoffContext()
{
    PersonaContractClosureService closure = new PersonaContractClosureService();
    WorkbenchSessionManager sessions = new WorkbenchSessionManager("bootstrap-invalid-envelope");
    AssertTrue(sessions.TryExchange("bootstrap-invalid-envelope", out WorkbenchSessionGrant grant), "invalid envelope fixture session exchange must succeed");
    DefaultHttpContext context = new DefaultHttpContext();
    context.Request.Headers["X-Pwb-Session"] = grant.SessionToken;
    context.Request.Headers["X-Pwb-Csrf"] = grant.CsrfToken;
    (int status, string body) result = ExecuteResult(PersonaContractRouteFactory.IssueHandoff(context.Request, new AuthoringHandoffRouteRequest
    {
        Receipt = new ApprovalReceipt(),
        Document = CreateContractDocument(),
        WorkspaceId = "not safe",
        Revision = 0,
        RequestId = "invalid-envelope",
        Fence = 1
    }, sessions, closure));
    AssertEqual("400", result.status.ToString(), "invalid envelope context must be rejected");
    AssertTrue(result.body.Contains("handoff.request_invalid", StringComparison.Ordinal), "invalid envelope context must be machine-readable");
}

void ExpiredSessionRejectsLateAuthoringResponse()
{
    DateTime now = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc);
    WorkbenchSessionManager sessions = new WorkbenchSessionManager("bootstrap-fixture-3", () => now, TimeSpan.FromMinutes(1));
    AssertTrue(sessions.TryExchange("bootstrap-fixture-3", out WorkbenchSessionGrant grant), "expiry fixture session exchange must succeed");
    now = now.AddMinutes(2);
    bool authorized = sessions.TryAuthorizeFence(grant.SessionToken, grant.CsrfToken, 1, "late-response", out _, out _, out string errorCode);
    AssertTrue(!authorized && errorCode == "session.unauthorized", "late response after expiry must be rejected");
}

PersonaDocument CreateContractDocument()
{
    return new PersonaDocument
    {
        Id = "fixture.contract.persona", DisplayName = "契约夹具", Core = "先观察再行动。", IdentityFacts = "来自边境。", Summary = "谨慎。", SourcePackId = "fixture.pack", TemplateVersion = "persona-load.v2", Status = PersonaReviewStatus.Approved, SourceDescription = "先观察再行动。", PublicDescription = "公开场合克制。", PrivateDescription = "私下核算代价。", ContradictionDescription = "谨慎与野心并存。", SelfClaimRules = new List<string> { "对外只自称我。" }, RealSelfBehaviors = new List<string> { "先确认代价。" }, SelfClaimExamples = new List<string> { "我会如何回应？" }, Tags = new List<string>(), FacetStrengths = new Dictionary<string, int>(StringComparer.Ordinal), TraitProfile = new PersonaTraitProfile(), ExpressionProfile = new PersonaExpressionProfile(), BehaviorProfile = new PersonaBehaviorProfile(), ReactionProfile = new PersonaReactionProfile(), CommitmentProfile = new PersonaCommitmentProfile()
    };
}

(int status, string body) ExecuteResult(IResult result)
{
    DefaultHttpContext context = new DefaultHttpContext();
    context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
    context.Response.Body = new MemoryStream();
    result.ExecuteAsync(context).GetAwaiter().GetResult();
    context.Response.Body.Position = 0;
    return (context.Response.StatusCode, new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEnd());
}
ProviderDraftActionService CreateBatchActionService(IProviderDslConversionClient conversionClient)
{
    return new ProviderDraftActionService(
        new ProviderSessionKeyVault(),
        new FakeProviderDraftClient(new ProviderDraftResult { Status = ProviderDraftStatus.ResponseInvalid, ErrorCode = "legacy_must_not_run" }),
        null,
        conversionClient);
}

BatchGenerationRequest CreateBatchRequest(string batchId, params BatchGenerationItem[] items)
{
    return new BatchGenerationRequest
    {
        BatchId = batchId,
        Endpoint = "http://127.0.0.1:11434/v1/chat/completions",
        Model = "local-model",
        ProviderProtocol = ProviderProtocolKind.Ollama,
        Items = items.ToList()
    };
}

BatchGenerationItem CreateBatchItem(string itemId, int sourceOrdinal, string sourceText)
{
    return new BatchGenerationItem
    {
        ItemId = itemId,
        SourceOrdinal = sourceOrdinal,
        SourceFile = "测试资料.md",
        Title = "资料段 " + sourceOrdinal,
        SourceText = sourceText
    };
}

ProviderDslConversionResult SuccessfulBatchConversion()
{
    return new ProviderDslConversionResult
    {
        Status = ProviderDraftStatus.Success,
        CandidateJson = "{\"summary\":\"测试角色\",\"axes\":[{\"index\":2,\"value\":2,\"source\":\"骄傲\"}]}"
    };
}

void AssertEqual(string expected, string actual, string message)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(message + " Expected: " + expected + " Actual: " + actual);
    }
}

sealed class DelegateHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public DelegateHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request);
    }
}

sealed class FakeSecretProtector : IProviderSecretProtector
{
    private readonly bool _isAvailable;

    public FakeSecretProtector(bool isAvailable)
    {
        _isAvailable = isAvailable;
    }

    public bool IsAvailable => _isAvailable;

    public bool TryProtect(string plaintext, out string protectedValue, out string errorCode)
    {
        protectedValue = _isAvailable ? "fake:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext)) : string.Empty;
        errorCode = _isAvailable ? string.Empty : "provider.secret_protection_unavailable";
        return _isAvailable;
    }

    public bool TryUnprotect(string protectedValue, out string plaintext, out string errorCode)
    {
        plaintext = string.Empty;
        errorCode = string.Empty;
        if (!_isAvailable)
        {
            errorCode = "provider.secret_protection_unavailable";
            return false;
        }

        if (!protectedValue.StartsWith("fake:", StringComparison.Ordinal))
        {
            errorCode = "provider.secret_invalid";
            return false;
        }

        plaintext = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue[5..]));
        return true;
    }
}

sealed class FakeProviderDraftClient : IProviderDraftClient
{
    private readonly ProviderDraftResult _result;

    public FakeProviderDraftClient(ProviderDraftResult result)
    {
        _result = result;
    }

    public int CallCount { get; private set; }
    public ProviderDraftRequest? LastRequest { get; private set; }

    public Task<ProviderDraftResult> GenerateAsync(ProviderDraftRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastRequest = request;
        return Task.FromResult(_result);
    }
}

sealed class FakeProviderTextExpansionClient : IProviderTextExpansionClient
{
    private readonly ProviderTextExpansionResult _result;

    public FakeProviderTextExpansionClient(ProviderTextExpansionResult result)
    {
        _result = result;
    }

    public int CallCount { get; private set; }
    public ProviderTextExpansionRequest? LastRequest { get; private set; }

    public Task<ProviderTextExpansionResult> ExpandAsync(ProviderTextExpansionRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastRequest = request;
        return Task.FromResult(_result);
    }
}

sealed class BlockingProviderTextExpansionClient : IProviderTextExpansionClient
{
    private readonly ManualResetEventSlim _entered = new ManualResetEventSlim(false);
    private readonly TaskCompletionSource<ProviderTextExpansionResult> _completion = new TaskCompletionSource<ProviderTextExpansionResult>(TaskCreationOptions.RunContinuationsAsynchronously);

    public void WaitUntilEntered() => _entered.Wait(TimeSpan.FromSeconds(5));
    public void Complete(ProviderTextExpansionResult result) => _completion.TrySetResult(result);

    public Task<ProviderTextExpansionResult> ExpandAsync(ProviderTextExpansionRequest request, CancellationToken cancellationToken = default)
    {
        _entered.Set();
        return _completion.Task;
    }
}

sealed class FakeProviderDslConversionClient : IProviderDslConversionClient
{
    private readonly ProviderDslConversionResult _result;

    public FakeProviderDslConversionClient(ProviderDslConversionResult result)
    {
        _result = result;
    }

    public int CallCount { get; private set; }
    public ProviderDslConversionRequest? LastRequest { get; private set; }

    public Task<ProviderDslConversionResult> ConvertAsync(ProviderDslConversionRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastRequest = request;
        return Task.FromResult(_result);
    }
}

sealed class SequencedProviderDslConversionClient : IProviderDslConversionClient
{
    private readonly Func<int, ProviderDslConversionResult> _resultFactory;
    private int _callCount;

    public SequencedProviderDslConversionClient(Func<int, ProviderDslConversionResult> resultFactory)
    {
        _resultFactory = resultFactory;
    }

    public int CallCount => Volatile.Read(ref _callCount);

    public Task<ProviderDslConversionResult> ConvertAsync(ProviderDslConversionRequest request, CancellationToken cancellationToken = default)
    {
        int callNumber = Interlocked.Increment(ref _callCount);
        return Task.FromResult(_resultFactory(callNumber));
    }
}

sealed class BlockingSecondProviderDslConversionClient : IProviderDslConversionClient
{
    private readonly ProviderDslConversionResult _firstResult;
    private readonly ProviderDslConversionResult _defaultResult;
    private readonly ManualResetEventSlim _secondCallEntered = new ManualResetEventSlim(false);
    private readonly TaskCompletionSource<ProviderDslConversionResult> _secondCompletion = new TaskCompletionSource<ProviderDslConversionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _callCount;

    public BlockingSecondProviderDslConversionClient(ProviderDslConversionResult firstResult, ProviderDslConversionResult defaultResult)
    {
        _firstResult = firstResult;
        _defaultResult = defaultResult;
    }

    public int CallCount => Volatile.Read(ref _callCount);

    public bool WaitUntilSecondCall() => _secondCallEntered.Wait(TimeSpan.FromSeconds(5));

    public void CompleteSecond(ProviderDslConversionResult result) => _secondCompletion.TrySetResult(result);

    public Task<ProviderDslConversionResult> ConvertAsync(ProviderDslConversionRequest request, CancellationToken cancellationToken = default)
    {
        int callNumber = Interlocked.Increment(ref _callCount);
        if (callNumber == 1) return Task.FromResult(_firstResult);
        if (callNumber == 2)
        {
            _secondCallEntered.Set();
            return _secondCompletion.Task;
        }
        return Task.FromResult(_defaultResult);
    }
}

sealed class ThrowingProviderEndpointResolver : IProviderEndpointResolver
{
    public Task<IReadOnlyList<IPAddress>> ResolveAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("resolver failed");
    }
}

sealed class FixedProviderEndpointResolver : IProviderEndpointResolver
{
    private readonly IReadOnlyList<IPAddress> _addresses;

    public FixedProviderEndpointResolver(params IPAddress[] addresses)
    {
        _addresses = addresses;
    }

    public Task<IReadOnlyList<IPAddress>> ResolveAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_addresses);
    }
}

sealed class CancellationAwareHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<CancellationToken, Task<HttpResponseMessage>> _handler;
    public int CallCount { get; private set; }

    public CancellationAwareHttpMessageHandler(Func<CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return _handler(cancellationToken);
    }
}























