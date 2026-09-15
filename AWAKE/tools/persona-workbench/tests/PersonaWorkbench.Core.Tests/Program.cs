using System.Text.Json;
using System.Security.Cryptography;
using PersonaWorkbench.Core;

List<string> failures = new List<string>();
// 体检用例的夹具计数器：给每张卡的 core 一个**逐卡不同、且与显示名无关**的自述，
// 免得归一之后三张卡反而变成同一条（那样体检会正确地判成跨卡复用，把用例本身搞红）。
int auditCardSerial = 0;
Run("generates deterministic DSL in category order", GeneratesDeterministicDsl);
Run("generates keyword constrained canonical DSL", GeneratesKeywordConstrainedCanonicalDsl);
Run("matches the shared canonical golden fixture", MatchesSharedCanonicalGoldenFixture);
Run("renders canonical authored sections", RendersCanonicalAuthoredSections);
Run("registers starter personality and behavior tags", RegistersStarterPersonalityAndBehaviorTags);
Run("renders explicit facet strengths over legacy tags", RendersExplicitFacetStrengths);
Run("renders disposition axes over matching legacy facets", RendersDispositionAxesOverLegacyFacets);
Run("renders reaction and commitment profiles in stable order", RendersReactionAndCommitmentProfiles);
Run("rejects out-of-range profile axes", RejectsOutOfRangeProfileAxes);
Run("renders axes at the contract boundary", RendersAxesAtContractBoundary);
Run("parses axis extreme tokens back to the boundary value", ParsesAxisExtremeTokens);
Run("rejects invalid facet strengths", RejectsInvalidFacetStrengths);
Run("rejects unknown tags", RejectsUnknownTags);
Run("rejects unknown review status", RejectsUnknownReviewStatus);
Run("round trips a free persona document", RoundTripsDocument);
Run("rejects unknown JSON fields", RejectsUnknownJsonFields);
Run("rejects oversized JSON input", RejectsOversizedJson);
Run("writes a document through the local store", WritesDocumentThroughLocalStore);
Run("prevents two writable workspace sessions", PreventsTwoWritableWorkspaceSessions);
Run("preserves a conflict draft after external modification", PreservesConflictDraftAfterExternalModification);
Run("restores history from a prepared commit journal", RestoresHistoryFromPreparedCommitJournal);
Run("completes a prepared journal when the target is still old", CompletesPreparedJournalWhenTargetIsStillOld);
Run("preserves an external target during recovery conflict", PreservesExternalTargetDuringRecoveryConflict);
Run("parses restricted Persona DSL candidates", ParsesRestrictedPersonaDslCandidates);
Run("rejects provider-owned Persona DSL metadata", RejectsProviderOwnedPersonaDslMetadata);
Run("parses a small intermediate Persona candidate", ParsesSmallIntermediatePersonaCandidate);
Run("prunes invented intermediate Persona text", PrunesInventedIntermediatePersonaText);
Run("prunes invalid sparse intermediate entries", PrunesInvalidSparseIntermediateEntries);
Run("requires source evidence for sparse axes", RequiresSourceEvidenceForSparseAxes);
Run("prunes semantically unrelated sparse evidence", PrunesSemanticallyUnrelatedSparseEvidence);
Run("builds a heuristic Persona from free text", BuildsHeuristicPersonaFromFreeText);
Run("enriches explicit reaction and exchange evidence", EnrichesExplicitReactionAndExchangeEvidence);
Run("rejects duplicate DSL axis definitions", RejectsDuplicateDslAxisDefinitions);
Run("rejects duplicate DSL facet definitions", RejectsDuplicateDslFacetDefinitions);
Run("rejects negated directness evidence", RejectsNegatedDirectnessEvidence);
Run("does not match informal as formal", DoesNotMatchInformalAsFormal);
Run("does not match swarm as warm", DoesNotMatchSwarmAsWarm);
Run("does not match non-tradeable as tradeable", DoesNotMatchNonTradeableAsTradeable);
Run("preserves cautious promise and non-tradeable evidence", PreservesNegatedPositiveEvidence);
Run("rejects unknown nested intermediate fields", RejectsUnknownNestedIntermediateFields);
Run("trims oversized templates by priority", TrimsOversizedTemplatesByPriority);
Run("trims optional prose before keyword sections", TrimsOptionalProseBeforeKeywordSections);
Run("reports compression while preserving protected fields", ReportsCompressionWhilePreservingProtectedFields);
Run("deduplicates repeated evidence within one field", DeduplicatesRepeatedEvidenceWithinOneField);
Run("reports evidence deduplication in diagnostics", ReportsEvidenceDeduplicationInDiagnostics);
Run("preserves same-section evidence from distinct fields", PreservesSameSectionEvidenceFromDistinctFields);
Run("compresses scalar-boundary text without splitting Unicode", CompressesScalarBoundaryTextWithoutSplittingUnicode);
Run("reports core budget failure without partial DSL", ReportsCoreBudgetFailureWithoutPartialDsl);
Run("verifies protected entries after canonical render", VerifiesProtectedEntriesAfterCanonicalRender);
Run("migrates a v1 document to authoring v2", MigratesV1DocumentToAuthoringV2);
Run("keeps expansion separate from authored core", KeepsExpansionSeparateFromAuthoredCore);
Run("merges mapped observations by fidelity", MergesMappedObservationsByFidelity);
Run("follows the crosswalk zero-axis omission", FollowsTheCrosswalkZeroAxisOmission);
Run("preserves unmapped legacy values with warnings", PreservesUnmappedLegacyValuesWithWarnings);
Run("rejects invalid authoring input and duplicate source tags", RejectsInvalidAuthoringInputAndDuplicateSourceTags);
Run("produces deterministic authoring canonical bytes", ProducesDeterministicAuthoringCanonicalBytes);
Run("rejects drifted authoring contract assets", RejectsDriftedAuthoringContractAssets);
Run("issues only approved receipt and isolated handoff", IssuesOnlyApprovedReceiptAndIsolatedHandoff);
Run("issues a cross-workstation handoff envelope", IssuesCrossWorkstationHandoffEnvelope);
Run("returns identical handoff for identical retry", ReturnsIdenticalHandoffForIdenticalRetry);
Run("fails handoff closed on receipt warnings", FailsHandoffClosedOnReceiptWarnings);
Run("rejects expired receipt and contract drift", RejectsExpiredReceiptAndContractDrift);
Run("validates handoff semantic bindings", ValidatesHandoffSemanticBindings);
Run("corpus audit: reports cross-card text reuse", CorpusAuditReportsCrossCardTextReuse);
Run("corpus audit: ignores reuse below the card threshold", CorpusAuditIgnoresReuseBelowThreshold);
Run("corpus audit: normalizes the display name before comparing", CorpusAuditNormalizesDisplayName);
Run("corpus audit: reports a shared opening with differing tails", CorpusAuditReportsSkeletonPrefix);
Run("corpus audit: flags a self-reference-only rule", CorpusAuditFlagsSelfReferenceOnlyRule);
Run("corpus audit: keeps a rule that excludes the given name", CorpusAuditKeepsRuleThatExcludesName);
Run("corpus audit: flags editor sample text and repeated lines", CorpusAuditFlagsSampleTextAndRepeats);
Run("corpus audit: passes a clean corpus", CorpusAuditPassesCleanCorpus);

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine("PASS PersonaWorkbench.Core.Tests");
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

void MatchesSharedCanonicalGoldenFixture()
{
    string path = FindGoldenFixture();
    string json = File.ReadAllText(path);
    using JsonDocument fixture = JsonDocument.Parse(json);
    PersonaDocument document = JsonSerializer.Deserialize<PersonaDocument>(json, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }) ?? throw new InvalidOperationException("golden Persona input must deserialize");
    string expected = fixture.RootElement.GetProperty("expectedDsl").GetString() ?? string.Empty;
    string actual = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096).Dsl;
    AssertEqual(expected, actual, "Workbench output must match the shared canonical fixture");
}

void GeneratesDeterministicDsl()
{
    PersonaTagRegistry registry = PersonaTagRegistry.CreateDefault();
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.demo.sable",
        DisplayName = "赛布尔",
        Core = "谨慎而自持，不轻易交出真实意图。",
        IdentityFacts = "她是一名独立旅行者。",
        Tags = new List<string>
        {
            "boundary.no_empty_promises",
            "expression.measured",
            "trait.cautious"
        },
        TraitProfile = new PersonaTraitProfile { Caution = 1 }
    };

    PersonaDslResult first = PersonaDslGenerator.Generate(document, registry, 4096);
    PersonaDslResult second = PersonaDslGenerator.Generate(document, registry, 4096);

    AssertEqual(first.Dsl, second.Dsl, "DSL must be deterministic");
    AssertTrue(first.Dsl.Contains("[PERSONA_LOAD]", StringComparison.Ordinal), "formal template header must be present");
    AssertTrue(!first.Dsl.Contains("风险取向：", StringComparison.Ordinal), "formal template must not emit Chinese axis labels");
    AssertTrue(first.Dsl.IndexOf("[PERSONA_LOAD]", StringComparison.Ordinal) < first.Dsl.IndexOf("[PERSONA_CONSTRAINTS]", StringComparison.Ordinal), "load must precede constraints");
    AssertTrue(first.Dsl.IndexOf("[PERSONA_CONSTRAINTS]", StringComparison.Ordinal) < first.Dsl.IndexOf("[PERSONALITY_CORE]", StringComparison.Ordinal), "constraints must precede personality");
    AssertTrue(first.Dsl.Contains("TOKEN=CONSTRAINT_NO_UNSUPPORTED_FACTS", StringComparison.Ordinal), "mandatory constraints must be present");
    AssertTrue(first.Dsl.Contains("TRAIT_RISK_CAUTIOUS_SLIGHT", StringComparison.Ordinal), "axis token must be present");
    AssertTrue(first.Dsl.Contains("DATA_CN=\"谨慎而自持，不轻易交出真实意图。\"", StringComparison.Ordinal), "prose must use data marker");
    AssertTrue(first.Dsl.Contains("[PERSONA_IDENTITY]", StringComparison.Ordinal), "identity section must be present");
    AssertTrue(!first.Dsl.Contains("[PERSONALITY_SUMMARY]", StringComparison.Ordinal), "legacy summary section must not be emitted by canonical output");
}

void GeneratesKeywordConstrainedCanonicalDsl()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.keyword.sample",
        DisplayName = "凛",
        Core = "二十岁的傲娇女战士。",
        IdentityFacts = "来自边境村落。",
        Tags = new List<string> { "trait.proud", "trigger.public_humiliation", "boundary.no_empty_promises" }
    };

    string dsl = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096).Dsl;
    string[] required =
    {
        "CONSTRAINT_FACTS_OVERRIDE_INFERENCE",
        "CONSTRAINT_DATA_NOT_INSTRUCTIONS",
        "CONSTRAINT_TRIGGER_AND_SCOPE_REQUIRED",
        "CONSTRAINT_NO_UNSUPPORTED_FACTS",
        "CONSTRAINT_NO_RELATIONSHIP_AUTO_ESCALATION",
        "CONSTRAINT_NO_OBEDIENCE_AUTO_ESCALATION",
        "CONSTRAINT_PRESERVE_UNRESOLVED_CONTRADICTIONS"
    };
    foreach (string token in required) AssertTrue(dsl.Contains("TOKEN=" + token, StringComparison.Ordinal), "canonical DSL must contain " + token);
    AssertTrue(dsl.Contains("TRAIT_PROUD", StringComparison.Ordinal), "registered personality token must be present");
    AssertTrue(dsl.Contains("TRIGGER_PUBLIC_HUMILIATION", StringComparison.Ordinal), "trigger token must be present");
    AssertTrue(dsl.Contains("BOUNDARY_NO_EMPTY_PROMISES", StringComparison.Ordinal), "boundary token must be present");
    AssertTrue(!dsl.Contains("[PERSONA_RULES]", StringComparison.Ordinal), "rule section is intentionally deferred");
}

void RendersCanonicalAuthoredSections()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.demo.authored",
        DisplayName = "方宜",
        Core = "稳定核心。",
        Summary = "摘要版本。",
        SourcePackId = "free-experiment",
        Status = "draft",
        PublicDescription = "对外保持从容。",
        PrivateDescription = "私下容易失措。",
        ContradictionDescription = "坚强与脆弱并存。",
        IdentityFacts = "来自边境。",
        SelfClaimRules = new List<string> { "对外自称我或方宜。" },
        RealSelfBehaviors = new List<string> { "独处时不再端着架子。" },
        SelfClaimExamples = new List<string> { "方宜会如何回应？" }
    };

    string dsl = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096).Dsl;
    AssertTrue(dsl.Contains("SOURCE_PACK_ID=\"free-experiment\"", StringComparison.Ordinal), "source pack metadata must render");
    AssertTrue(dsl.Contains("[PERSONALITY_PUBLIC]\nDATA_CN=\"对外保持从容。\"", StringComparison.Ordinal), "public authored section must render");
    AssertTrue(dsl.Contains("[PERSONALITY_PRIVATE]\nDATA_CN=\"私下容易失措。\"", StringComparison.Ordinal), "private authored section must render");
    AssertTrue(dsl.Contains("[PERSONALITY_CONTRADICTION]\nDATA_CN=\"坚强与脆弱并存。\"", StringComparison.Ordinal), "contradiction authored section must render");
    AssertTrue(dsl.Contains("[PERSONA_IDENTITY]\nID=\"free.demo.authored\"\nNAME=\"方宜\"\nDATA_CN=\"来自边境。\"", StringComparison.Ordinal), "identity facts must render in the identity section");
    AssertTrue(dsl.Contains("DATA_CN=\"对外自称我或方宜。\"", StringComparison.Ordinal), "self claim rule data must render");
    AssertTrue(dsl.Contains("DATA_CN=\"独处时不再端着架子。\"", StringComparison.Ordinal), "real self behavior data must render");
    AssertTrue(dsl.Contains("DATA_CN=\"方宜会如何回应？\"", StringComparison.Ordinal), "self claim example data must render");
    AssertTrue(!dsl.Contains("[PERSONALITY_SUMMARY]", StringComparison.Ordinal), "deferred legacy summary section must not be emitted by the minimal canonical template");
}

void RegistersStarterPersonalityAndBehaviorTags()
{
    PersonaTagRegistry registry = PersonaTagRegistry.CreateDefault();
    string[] required =
    {
        "trait.cautious", "trait.ambitious", "trait.proud", "trait.pragmatic", "trait.guardian", "trait.traditional",
        "expression.measured", "expression.direct", "expression.formal", "expression.teasing", "expression.warm",
        "behavior.bargains", "behavior.observes_before_acting", "behavior.tests_loyalty", "behavior.keeps_leverage", "behavior.protects_inner_circle", "behavior.takes_command",
        "trigger.public_humiliation", "boundary.no_empty_promises"
    };
    foreach (string id in required) AssertTrue(registry.TryGet(id, out _), "starter tag must be registered: " + id);

    PersonaValidationResult validation = PersonaValidator.Validate(new PersonaDocument
    {
        Id = "free.demo.registry",
        Core = "稳定核心。",
        Tags = required.ToList()
    }, registry);
    AssertTrue(validation.IsValid, "starter personality and behavior tags must be registered");
}

void RendersExplicitFacetStrengths()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.demo.strengths",
        Core = "她会先衡量风险，再决定要露出多少底牌。",
        Tags = new List<string>
        {
            "trait.cautious",
            "boundary.no_empty_promises"
        },
        FacetStrengths = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["trait.cautious"] = 4,
            ["expression.measured"] = 1,
            ["behavior.bargains"] = 2
        }
    };

    PersonaDslResult result = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096);

    AssertTrue(result.Dsl.Contains("FACET_TRAIT_CAUTIOUS_STRENGTH_4", StringComparison.Ordinal), "explicit trait strength must be preserved");
    AssertTrue(result.Dsl.Contains("FACET_EXPRESSION_MEASURED_STRENGTH_1", StringComparison.Ordinal), "explicit expression strength must be preserved");
    AssertTrue(result.Dsl.Contains("FACET_BEHAVIOR_BARGAINS_STRENGTH_2", StringComparison.Ordinal), "explicit behavior strength must be preserved");
    AssertTrue(result.Dsl.Contains("BOUNDARY_NO_EMPTY_PROMISES", StringComparison.Ordinal), "boundary tag must be preserved");
    AssertTrue(!result.Dsl.Contains("TRAIT_CAUTIOUS\n", StringComparison.Ordinal), "qualified trait strength must replace the unqualified tag");
}

void RejectsUnknownTags()
{
    PersonaValidationResult validation = PersonaValidator.Validate(new PersonaDocument
    {
        Id = "free.demo.unknown-tag",
        Core = "测试。",
        Tags = new List<string> { "trait.not_registered" }
    }, PersonaTagRegistry.CreateDefault());

    AssertTrue(!validation.IsValid, "unknown tags must be rejected");
    AssertTrue(validation.Errors.Any(error => error.Code == "tag.unregistered"), "unknown tags must expose the stable validation code");
}

void RejectsInvalidFacetStrengths()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.demo.invalid-strengths",
        Core = "测试。",
        FacetStrengths = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["trait.cautious"] = 0,
            ["behavior.bargains"] = 5,
            ["trigger.public_humiliation"] = 2,
            ["trait.not_registered"] = 3
        }
    };

    PersonaValidationResult validation = PersonaValidator.Validate(document, PersonaTagRegistry.CreateDefault());

    AssertTrue(!validation.IsValid, "invalid facet strengths must fail validation");
    AssertTrue(validation.Errors.Count(error => error.Code == "facet.strength_invalid") == 2, "out-of-range strengths must be reported");
    AssertTrue(validation.Errors.Any(error => error.Code == "facet.category_not_supported"), "trigger and boundary strengths must be rejected");
    AssertTrue(validation.Errors.Any(error => error.Code == "facet.unregistered"), "unknown facet IDs must be rejected");
}

void ParsesRestrictedPersonaDslCandidates()
{
    const string candidate = """
        [PERSONA_LOAD]
        SELF_CLAIM_NAME
        LANG_ZH_CN_ONLY

        [PERSONALITY_CORE]
        TRAIT_RISK_CAUTIOUS_STRONG
        TRAIT_PRIDE_PROUD_STRONG
        DESC_CN="谨慎而自持，却把尊严看得比安稳更重。"

        [PERSONALITY_PUBLIC]
        EXPRESSION_DIRECTNESS_DIRECT_SLIGHT
        DESC_CN="公开场合说话直白，不擅长遮掩真正态度。"

        [PERSONALITY_PRIVATE]
        BEHAVIOR_DELIBERATION_OBSERVE_FIRST_SLIGHT
        REACTION_CONFRONTATION_CONFRONTATIONAL_STRONG
        SENSITIVE_CONDITIONS="被公开羞辱"
        CONDITIONAL_RESPONSES="先记下羞辱，再选择合适时机反击。"

        [PERSONALITY_CONTRADICTION]
        COMMITMENT_PROMISE_CAUTIOUS_PROMISES_STRONG
        BOUNDARY_NO_EMPTY_PROMISES
        DESC_CN="她不轻易许诺，但一旦许下就会承担代价。"

        [PERSONALITY_SUMMARY]
        DESC_CN="一名谨慎、骄傲且不轻易让步的年轻战士。"

        [SELF_IDENTITY]
        FACTS_CN="来自边境村落，自小以剑为伴。"
        """;

    PersonaDslCandidateParseResult result = PersonaDslCandidateParser.Parse(
        candidate,
        "二十岁的年轻女战士。",
        "free.generated.persona",
        "凛",
        PersonaTagRegistry.CreateDefault());
    AssertTrue(result.IsValid && result.Document != null, "restricted DSL candidate must parse");
    AssertEqual("free.generated.persona", result.Document!.Id, "local ID must be supplied by the caller");
    AssertEqual("凛", result.Document.DisplayName, "local display name must be supplied by the caller");
    AssertTrue(result.Document.TraitProfile.Caution == 2, "candidate axis must map to local profile");
    AssertTrue(result.Document.Tags.Contains("boundary.no_empty_promises", StringComparer.Ordinal), "registered boundary tag must be restored");

    string canonical = PersonaDslGenerator.Generate(result.Document, PersonaTagRegistry.CreateDefault(), 4096).Dsl;
    AssertTrue(canonical.Contains("TRAIT_RISK_CAUTIOUS_STRONG", StringComparison.Ordinal), "canonical output must preserve parsed axis");
    AssertTrue(canonical.Contains("ID=\"free.generated.persona\"", StringComparison.Ordinal), "canonical output must inject local identity");
}

void RejectsProviderOwnedPersonaDslMetadata()
{
    const string candidate = """
        [PERSONA_LOAD]
        SELF_CLAIM_NAME
        LANG_ZH_CN_ONLY
        TEMPLATE_VERSION="attacker-template"

        [PERSONALITY_CORE]
        DESC_CN="谨慎而自持。"
        """;
    PersonaDslCandidateParseResult result = PersonaDslCandidateParser.Parse(
        candidate,
        "谨慎的角色。",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());
    AssertTrue(!result.IsValid, "provider-owned metadata must be rejected");
    AssertEqual("persona.dsl_candidate_local_metadata_forbidden", result.ErrorCode, "metadata rejection must be explicit");
}

void RendersDispositionAxesOverLegacyFacets()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.demo.disposition-axes",
        Core = "测试统一双极轴。",
        Tags = new List<string> { "trait.cautious", "expression.direct", "behavior.takes_command" },
        FacetStrengths = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["trait.cautious"] = 4,
            ["expression.direct"] = 3,
            ["behavior.takes_command"] = 2
        },
        TraitProfile = new PersonaTraitProfile { Caution = -1 },
        ExpressionProfile = new PersonaExpressionProfile { Directness = 0 },
        BehaviorProfile = new PersonaBehaviorProfile { Leadership = 2 }
    };

    string dsl = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096).Dsl;

    AssertTrue(dsl.Contains("TRAIT_RISK_BOLD_SLIGHT", StringComparison.Ordinal), "configured risk axis must render");
    AssertTrue(dsl.Contains("EXPRESSION_DIRECTNESS_BALANCED", StringComparison.Ordinal), "configured directness axis must render");
    AssertTrue(dsl.Contains("BEHAVIOR_LEADERSHIP_COMMANDING_STRONG", StringComparison.Ordinal), "configured leadership axis must render");
    AssertTrue(!dsl.Contains("FACET_TRAIT_CAUTIOUS", StringComparison.Ordinal), "configured risk axis must replace matching legacy facet");
    AssertTrue(!dsl.Contains("TRAIT_CAUTIOUS\n", StringComparison.Ordinal), "configured risk axis must replace matching legacy tag");
}

void RendersReactionAndCommitmentProfiles()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.demo.profiles",
        Core = "她不会立刻暴露自己的软处。",
        Tags = new List<string> { "trigger.public_humiliation", "boundary.no_empty_promises" },
        ReactionProfile = new PersonaReactionProfile
        {
            Confrontation = 2,
            Expression = -1,
            Timing = 0,
            SensitiveConditions = "被迫在众人面前表态。",
            ConditionalResponses = "先观察退路，再决定是否回击。"
        },
        CommitmentProfile = new PersonaCommitmentProfile
        {
            PromiseCaution = 2,
            PromisePersistence = 1,
            ValueTradeability = -1,
            PriorityOrder = "自己人 > 家族名誉 > 眼前利益",
            ExceptionCost = "破例必须留下可追讨的代价。"
        }
    };

    string dsl = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096).Dsl;

    AssertTrue(dsl.Contains("[PERSONALITY_PRIVATE]", StringComparison.Ordinal), "reaction profile must render in the private section");
    AssertTrue(dsl.Contains("REACTION_CONFRONTATION_CONFRONTATIONAL_STRONG", StringComparison.Ordinal), "confrontation profile must render");
    AssertTrue(dsl.Contains("REACTION_EMOTION_SUPPRESSED_SLIGHT", StringComparison.Ordinal), "emotion profile must render");
    AssertTrue(dsl.Contains("REACTION_TIMING_BALANCED", StringComparison.Ordinal), "timing profile must render");
    AssertTrue(dsl.Contains("COMMITMENT_PROMISE_CAUTIOUS_PROMISES_STRONG", StringComparison.Ordinal), "commitment profile must render");
    AssertTrue(dsl.Contains("COMMITMENT_FULFILLMENT_PERSISTENT_FULFILLMENT_SLIGHT", StringComparison.Ordinal), "fulfillment profile must render");
    AssertTrue(dsl.Contains("COMMITMENT_VALUE_TRADEABLE_VALUES_SLIGHT", StringComparison.Ordinal), "value profile must render");
    AssertTrue(dsl.IndexOf("[PERSONALITY_PRIVATE]", StringComparison.Ordinal) < dsl.IndexOf("[PERSONALITY_CONTRADICTION]", StringComparison.Ordinal), "reaction must precede commitment");
}

void RejectsOutOfRangeProfileAxes()
{
    // 契约 domain 是 -3..3（character.v1.schema.json 数值轴 minimum/maximum、PersonaValidator
    // 的 axis.value_invalid 文案、以及卡库里 44/76 张卡的实际取值）。旧断言按 -2..2 写，与三者相悖。
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.demo.invalid-profiles",
        Core = "测试。",
        TraitProfile = new PersonaTraitProfile { Caution = -4 },
        BehaviorProfile = new PersonaBehaviorProfile { Leadership = 4 },
        ReactionProfile = new PersonaReactionProfile { Confrontation = -5 },
        CommitmentProfile = new PersonaCommitmentProfile { ValueTradeability = 5 }
    };

    PersonaValidationResult validation = PersonaValidator.Validate(document, PersonaTagRegistry.CreateDefault());

    AssertTrue(validation.Errors.Count(error => error.Code == "axis.value_invalid") == 4, "profile axes outside -3..3 must be rejected");
}

/// <summary>
/// 回归：契约允许的 |轴| = 3 必须能渲染。/ 修复前 AddAxisId 只覆盖 -2..2 并对 ±3 抛
/// persona.axis_value_invalid，导致卡库里 44/76 张卡（蒙楚格、阿丝塔、乌尔玻斯等）在预览
/// 与 Provider 这条链上完全渲染不出来。
/// </summary>
void RendersAxesAtContractBoundary()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.demo.axis-boundary",
        Core = "测试。",
        TraitProfile = new PersonaTraitProfile { Pragmatism = 3, Caution = -3 },
        ExpressionProfile = new PersonaExpressionProfile { Warmth = 3 },
        BehaviorProfile = new PersonaBehaviorProfile { InGroupPriority = 3 },
        CommitmentProfile = new PersonaCommitmentProfile { PromisePersistence = 3, ValueTradeability = -3 },
        Tags = new List<string> { "trait.cautious" }
    };

    string dsl = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096).Dsl;

    AssertTrue(dsl.Contains("TRAIT_PRAGMATISM_PRAGMATIC_EXTREME", StringComparison.Ordinal), "axis value 3 must render as _EXTREME");
    AssertTrue(dsl.Contains("TRAIT_RISK_BOLD_EXTREME", StringComparison.Ordinal), "axis value -3 must render as the negative _EXTREME");
    AssertTrue(dsl.Contains("EXPRESSION_WARMTH_WARM_EXTREME", StringComparison.Ordinal), "warmth 3 must render");
    AssertTrue(dsl.Contains("BEHAVIOR_IN_GROUP_PRIORITY_PROTECTS_IN_GROUP_EXTREME", StringComparison.Ordinal), "in-group priority 3 must render");
    AssertTrue(dsl.Contains("COMMITMENT_FULFILLMENT_PERSISTENT_FULFILLMENT_EXTREME", StringComparison.Ordinal), "promise persistence 3 must render");
    AssertTrue(dsl.Contains("COMMITMENT_VALUE_TRADEABLE_VALUES_EXTREME", StringComparison.Ordinal), "value tradeability -3 must render");
    AssertTrue(!dsl.Contains("_STRONG", StringComparison.Ordinal), "_EXTREME must not be flattened into _STRONG");
}

/// <summary>回归：解析侧必须与渲染侧同源——_EXTREME 读回 ±3，而不是被当成未知 token 丢掉。</summary>
void ParsesAxisExtremeTokens()
{
    const string candidate = """
        [PERSONA_LOAD]
        SELF_CLAIM_NAME
        LANG_ZH_CN_ONLY

        [PERSONALITY_CORE]
        TRAIT_PRAGMATISM_PRAGMATIC_EXTREME
        TRAIT_RISK_BOLD_EXTREME
        DESC_CN="测试。"

        [PERSONALITY_CONTRADICTION]
        COMMITMENT_FULFILLMENT_PERSISTENT_FULFILLMENT_EXTREME
        """;

    PersonaDslCandidateParseResult result = PersonaDslCandidateParser.Parse(
        candidate,
        "测试卡。",
        "free.demo.axis-extreme",
        "试",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(result.IsValid && result.Document != null, "_EXTREME tokens must parse");
    AssertTrue(result.Document!.TraitProfile.Pragmatism == 3, "positive _EXTREME must read back as 3");
    AssertTrue(result.Document.TraitProfile.Caution == -3, "negative _EXTREME must read back as -3");
    AssertTrue(result.Document.CommitmentProfile.PromisePersistence == 3, "commitment _EXTREME must read back as 3");
}

void RejectsUnknownReviewStatus()
{
    PersonaValidationResult validation = PersonaValidator.Validate(new PersonaDocument
    {
        Id = "free.review.invalid",
        Core = "稳定核心。",
        Status = "published"
    }, PersonaTagRegistry.CreateDefault());

    AssertTrue(!validation.IsValid, "unknown review status must be rejected");
    AssertTrue(validation.Errors.Any(error => error.Code == "persona.status_invalid"), "status validation must expose a stable error code");
}

void RoundTripsDocument()
{
    PersonaDocument original = new PersonaDocument
    {
        Id = "free.demo.roundtrip",
        DisplayName = "阿斯特拉",
        Core = "冷静地衡量每一次选择。",
        IdentityFacts = "她独自经营一间小型工坊。",
        Summary = "她把选择当作账本。",
        SourcePackId = "free-experiment",
        TemplateVersion = "persona-load.v2",
        Status = "draft",
        SourceDescription = "她谨慎而坚定。",
        PublicDescription = "对外保持冷静。",
        PrivateDescription = "私下容易犹豫。",
        ContradictionDescription = "冷静与犹豫并存。",
        SelfClaimRules = new List<string> { "对外自称我。" },
        RealSelfBehaviors = new List<string> { "独处时放松。" },
        SelfClaimExamples = new List<string> { "我会再想想。" },
        Tags = new List<string> { "trait.cautious", "behavior.bargains" },
        FacetStrengths = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["trait.cautious"] = 3
        },
        TraitProfile = new PersonaTraitProfile { Ambition = 1 },
        ExpressionProfile = new PersonaExpressionProfile { Warmth = -2 },
        BehaviorProfile = new PersonaBehaviorProfile { Deliberation = 2 },
        ReactionProfile = new PersonaReactionProfile { Timing = 0 },
        CommitmentProfile = new PersonaCommitmentProfile { PromiseCaution = -1 }
    };

    string json = PersonaDocumentCodec.Serialize(original);
    PersonaDocument restored = PersonaDocumentCodec.Deserialize(json, PersonaTagRegistry.CreateDefault());

    AssertEqual(PersonaSchema.Version, restored.SchemaVersion, "schema version must round trip");
    AssertEqual(original.Id, restored.Id, "ID must round trip");
    AssertEqual(original.DisplayName, restored.DisplayName, "display name must round trip");
    AssertEqual(original.Summary, restored.Summary, "summary must round trip");
    AssertEqual(original.SourcePackId, restored.SourcePackId, "source pack must round trip");
    AssertTrue(restored.SelfClaimRules.SequenceEqual(original.SelfClaimRules), "self claim rules must round trip");
    AssertTrue(restored.RealSelfBehaviors.SequenceEqual(original.RealSelfBehaviors), "real self behaviors must round trip");
    AssertTrue(restored.SelfClaimExamples.SequenceEqual(original.SelfClaimExamples), "self claim examples must round trip");
    AssertEqual(original.Tags[1], restored.Tags[1], "tags must round trip");
    AssertTrue(restored.FacetStrengths.TryGetValue("trait.cautious", out int strength) && strength == 3, "facet strengths must round trip");
    AssertTrue(restored.TraitProfile.Ambition == 1, "trait axis must round trip");
    AssertTrue(restored.ExpressionProfile.Warmth == -2, "expression axis must round trip");
    AssertTrue(restored.BehaviorProfile.Deliberation == 2, "behavior axis must round trip");
    AssertTrue(restored.ReactionProfile.Timing == 0, "zero reaction axis must round trip as an explicit balanced value");
    AssertTrue(restored.CommitmentProfile.PromiseCaution == -1, "commitment axis must round trip");
}

void RejectsUnknownJsonFields()
{
    string json = "{\"schemaVersion\":\"persona-workbench.character.v1\",\"id\":\"free.demo.extra\",\"core\":\"测试。\",\"unexpected\":true}";

    PersonaDocumentFormatException error = AssertThrows<PersonaDocumentFormatException>(
        () => PersonaDocumentCodec.Deserialize(json, PersonaTagRegistry.CreateDefault()),
        "unknown JSON fields must be rejected");

    AssertEqual("persona.document_unknown_property", error.Code, "wrong unknown field error code");
}

void RejectsOversizedJson()
{
    string json = "{\"schemaVersion\":\"persona-workbench.character.v1\",\"id\":\"free.demo.large\",\"core\":\"" +
        new string('测', PersonaDocumentCodec.MaximumUtf8Bytes) + "\"}";

    PersonaDocumentFormatException error = AssertThrows<PersonaDocumentFormatException>(
        () => PersonaDocumentCodec.Deserialize(json, PersonaTagRegistry.CreateDefault()),
        "oversized JSON must be rejected");

    AssertEqual("persona.document_too_large", error.Code, "wrong oversized document error code");
}

void WritesDocumentThroughLocalStore()
{
    string directory = Path.Combine(Path.GetTempPath(), "persona-workbench-tests-" + Guid.NewGuid().ToString("N"));
    string path = Path.Combine(directory, "free.demo.store.persona.json");
    try
    {
        PersonaDocumentStore.WriteAtomically(path, new PersonaDocument
        {
            Id = "free.demo.store",
            Core = "记录必须完整。"
        });

        PersonaDocument restored = PersonaDocumentStore.Read(path, PersonaTagRegistry.CreateDefault());
        AssertEqual("free.demo.store", restored.Id, "stored document must be readable");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

void PreventsTwoWritableWorkspaceSessions()
{
    string directory = CreateTemporaryWorkspace();
    try
    {
        using PersonaWorkspace first = PersonaWorkspace.Open(directory);
        PersonaWorkspaceException error = AssertThrows<PersonaWorkspaceException>(
            () => PersonaWorkspace.Open(directory),
            "second writable session must be rejected");

        AssertEqual("workspace.locked", error.Code, "wrong workspace lock error code");
    }
    finally
    {
        DeleteTemporaryWorkspace(directory);
    }
}

void PreservesConflictDraftAfterExternalModification()
{
    string directory = CreateTemporaryWorkspace();
    try
    {
        using PersonaWorkspace workspace = PersonaWorkspace.Open(directory);
        PersonaTagRegistry registry = PersonaTagRegistry.CreateDefault();
        workspace.Save("sable.persona.json", new PersonaDocument
        {
            Id = "free.demo.sable",
            Core = "初始版本。"
        }, expectedContentHash: null);
        PersonaWorkspaceDocument snapshot = workspace.Read("sable.persona.json", registry);

        PersonaDocumentStore.WriteAtomically(Path.Combine(directory, "sable.persona.json"), new PersonaDocument
        {
            Id = "free.demo.sable",
            Core = "外部修改版本。"
        });

        PersonaWorkspaceException error = AssertThrows<PersonaWorkspaceException>(
            () => workspace.Save("sable.persona.json", new PersonaDocument
            {
                Id = "free.demo.sable",
                Core = "本地编辑版本。"
            }, snapshot.ContentHash),
            "stale snapshot must not overwrite external modification");

        AssertEqual("workspace.conflict", error.Code, "wrong conflict error code");
        AssertTrue(!string.IsNullOrWhiteSpace(error.ConflictPath), "conflict draft path is required");
        AssertTrue(File.Exists(error.ConflictPath!), "conflict draft must be retained");
        PersonaDocument target = workspace.Read("sable.persona.json", registry).Document;
        AssertEqual("外部修改版本。", target.Core, "external version must remain authoritative");
    }
    finally
    {
        DeleteTemporaryWorkspace(directory);
    }
}

void RestoresHistoryFromPreparedCommitJournal()
{
    string directory = CreateTemporaryWorkspace();
    try
    {
        string historyRelativePath = ".history/sable.before.persona.json";
        string temporaryRelativePath = ".pending/sable.pending.tmp";
        string targetRelativePath = "sable.persona.json";
        string historyPath = Path.Combine(directory, historyRelativePath);
        string temporaryPath = Path.Combine(directory, temporaryRelativePath);

        PersonaDocumentStore.WriteAtomically(historyPath, new PersonaDocument
        {
            Id = "free.demo.sable",
            Core = "应恢复的历史版本。"
        });
        PersonaDocumentStore.WriteAtomically(temporaryPath, new PersonaDocument
        {
            Id = "free.demo.sable",
            Core = "未完成的新版本。"
        });
        File.WriteAllText(
            PersonaWorkspace.GetJournalPath(directory),
            "{\"version\":\"persona-workbench.commit.v1\",\"targetRelativePath\":\"" + targetRelativePath +
            "\",\"backupRelativePath\":\"" + historyRelativePath + "\",\"temporaryRelativePath\":\"" + temporaryRelativePath +
            "\",\"phase\":\"prepared\"}");

        using PersonaWorkspace workspace = PersonaWorkspace.Open(directory);
        workspace.Recover();

        PersonaDocument restored = PersonaDocumentStore.Read(Path.Combine(directory, targetRelativePath), PersonaTagRegistry.CreateDefault());
        AssertEqual("应恢复的历史版本。", restored.Core, "prepared journal must restore the backup version");
        AssertTrue(!File.Exists(PersonaWorkspace.GetJournalPath(directory)), "recovered journal must be removed");
    }
    finally
    {
        DeleteTemporaryWorkspace(directory);
    }
}

void CompletesPreparedJournalWhenTargetIsStillOld()
{
    string directory = CreateTemporaryWorkspace();
    try
    {
        string targetRelativePath = "sable.persona.json";
        string temporaryRelativePath = ".pending/sable.pending.tmp";
        string targetPath = Path.Combine(directory, targetRelativePath);
        string temporaryPath = Path.Combine(directory, temporaryRelativePath);
        PersonaDocumentStore.WriteAtomically(targetPath, new PersonaDocument { Id = "free.journal", Core = "旧版本。" });
        PersonaDocumentStore.WriteAtomically(temporaryPath, new PersonaDocument { Id = "free.journal", Core = "新版本。" });
        string previousHash = ComputeFileHash(targetPath);
        string pendingHash = ComputeFileHash(temporaryPath);
        File.WriteAllText(PersonaWorkspace.GetJournalPath(directory),
            "{\"version\":\"persona-workbench.commit.v1\",\"targetRelativePath\":\"" + targetRelativePath +
            "\",\"backupRelativePath\":\"\",\"temporaryRelativePath\":\"" + temporaryRelativePath +
            "\",\"previousContentHash\":\"" + previousHash + "\",\"pendingContentHash\":\"" + pendingHash + "\",\"phase\":\"prepared\"}");

        using PersonaWorkspace workspace = PersonaWorkspace.Open(directory);
        workspace.Recover();

        PersonaDocument restored = PersonaDocumentStore.Read(targetPath, PersonaTagRegistry.CreateDefault());
        AssertEqual("新版本。", restored.Core, "recovery must complete a move when the target still has the previous hash");
        AssertTrue(!File.Exists(PersonaWorkspace.GetJournalPath(directory)), "completed recovery must remove the journal");
    }
    finally
    {
        DeleteTemporaryWorkspace(directory);
    }
}

void PreservesExternalTargetDuringRecoveryConflict()
{
    string directory = CreateTemporaryWorkspace();
    try
    {
        string targetRelativePath = "sable.persona.json";
        string temporaryRelativePath = ".pending/sable.pending.tmp";
        string targetPath = Path.Combine(directory, targetRelativePath);
        string temporaryPath = Path.Combine(directory, temporaryRelativePath);
        PersonaDocumentStore.WriteAtomically(targetPath, new PersonaDocument { Id = "free.journal", Core = "外部版本。" });
        PersonaDocumentStore.WriteAtomically(temporaryPath, new PersonaDocument { Id = "free.journal", Core = "未完成版本。" });
        File.WriteAllText(PersonaWorkspace.GetJournalPath(directory),
            "{\"version\":\"persona-workbench.commit.v1\",\"targetRelativePath\":\"" + targetRelativePath +
            "\",\"backupRelativePath\":\"\",\"temporaryRelativePath\":\"" + temporaryRelativePath +
            "\",\"previousContentHash\":\"old-hash\",\"pendingContentHash\":\"pending-hash\",\"phase\":\"prepared\"}");

        using PersonaWorkspace workspace = PersonaWorkspace.Open(directory);
        PersonaWorkspaceException error = AssertThrows<PersonaWorkspaceException>(() => workspace.Recover(), "recovery must not overwrite an unrecognized external target");
        AssertEqual("workspace.recovery_conflict", error.Code, "recovery conflict must have a stable error code");
        AssertEqual("外部版本。", PersonaDocumentStore.Read(targetPath, PersonaTagRegistry.CreateDefault()).Core, "external target must remain unchanged");
        AssertTrue(File.Exists(temporaryPath), "pending data must remain available after a recovery conflict");
        AssertTrue(File.Exists(PersonaWorkspace.GetJournalPath(directory)), "journal must remain available after a recovery conflict");
    }
    finally
    {
        DeleteTemporaryWorkspace(directory);
    }
}

string ComputeFileHash(string path)
{
    using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    return Convert.ToHexString(SHA256.HashData(stream));
}

void ParsesSmallIntermediatePersonaCandidate()
{
    const string source = "年龄20，傲娇，身材娇小，涉世未深，是个坚强的女战士，对敌人不存仁慈，但恋爱时却显得手足无措，平时做事总是先行动，遇到冲突会正面迎击，情绪也容易外露，而且不轻易许诺。";
    const string candidate = """
        {
          "summary": "坚强的女战士",
          "identityFacts": "年龄20",
          "publicDescription": "傲娇",
          "privateDescription": "涉世未深，对敌人不存仁慈",
          "contradictionDescription": "恋爱时却显得手足无措",
          "reaction": {
            "sensitiveConditions": "恋爱时",
            "conditionalResponses": "显得手足无措"
          },
          "commitment": {
            "protectedValues": "坚强",
            "breachResponse": "对敌人不存仁慈"
          },
          "axes": [
            { "index": 2, "value": 2, "source": "傲娇" },
            { "index": 12, "value": -1, "source": "先行动" },
            { "index": 17, "value": 2, "source": "正面迎击" },
            { "index": 18, "value": 1, "source": "情绪也容易外露" },
            { "index": 22, "value": 2, "source": "不轻易许诺" }
          ],
          "flags": [
            { "index": 1, "source": "不轻易许诺" }
          ]
        }
        """;

    PersonaIntermediateCandidateParseResult result = PersonaIntermediateCandidateParser.Parse(
        candidate,
        source,
        "free.generated.persona",
        "凛",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(result.IsValid && result.Document != null, "valid sparse intermediate candidate must parse");
    PersonaDocument document = result.Document!;
    AssertEqual(source, document.Core, "confirmed source text must remain the authoritative core");
    AssertEqual("free.generated.persona", document.Id, "local ID must be injected outside the provider candidate");
    AssertEqual("凛", document.DisplayName, "local display name must be injected outside the provider candidate");
    AssertEqual("坚强的女战士", document.Summary, "extractive summary must be retained");
    AssertEqual("涉世未深，对敌人不存仁慈", document.PrivateDescription, "ordered exact source spans may be joined without inventing words");
    AssertTrue(document.TraitProfile.Pride == 2, "sparse trait axis must map by stable index");
    AssertTrue(document.BehaviorProfile.Deliberation == -1, "sparse behavior axis must map by stable index");
    AssertTrue(document.ReactionProfile.Confrontation == 2, "sparse reaction axis must map by stable index");
    AssertTrue(document.CommitmentProfile.PromiseCaution == 2, "sparse commitment axis must map by stable index");
    AssertTrue(document.Tags.SequenceEqual(new[] { "boundary.no_empty_promises" }), "sparse flags must map by stable index");
}

void PrunesInventedIntermediatePersonaText()
{
    PersonaIntermediateCandidateParseResult result = PersonaIntermediateCandidateParser.Parse(
        "{\"summary\":\"经历过无数场生死战斗\"}",
        "年轻而坚强的女战士。",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(result.IsValid && result.Document != null, "invalid optional text must not prevent core-only conversion");
    AssertEqual(string.Empty, result.Document!.Summary, "invented optional text must be discarded");
    AssertEqual("年轻而坚强的女战士。", result.Document.Core, "confirmed core must survive optional field pruning");
}

void PrunesInvalidSparseIntermediateEntries()
{
    PersonaIntermediateCandidateParseResult result = PersonaIntermediateCandidateParser.Parse(
        "{\"axes\":[{\"index\":99,\"value\":2,\"source\":\"骄傲\"},{\"index\":2,\"value\":0,\"source\":\"骄傲\"},{\"index\":2,\"value\":2,\"source\":\"虚构\"}],\"flags\":[{\"index\":7,\"source\":\"骄傲\"}]}",
        "骄傲的年轻战士。",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(result.IsValid && result.Document != null, "invalid sparse entries must be pruned individually");
    AssertTrue(result.Document!.TraitProfile.Pride == null, "invalid or ungrounded axis entries must not enter the document");
    AssertTrue(result.Document.Tags.Count == 0, "invalid flag entries must not enter the document");
}

void RequiresSourceEvidenceForSparseAxes()
{
    PersonaIntermediateCandidateParseResult result = PersonaIntermediateCandidateParser.Parse(
        "{\"axes\":[{\"index\":1,\"value\":2},{\"index\":2,\"value\":2,\"source\":\"骄傲\"}]}",
        "骄傲的年轻战士。",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(result.IsValid && result.Document != null, "missing item evidence must prune guesses rather than reject the whole candidate");
    AssertTrue(result.Document!.TraitProfile.Ambition == null, "axis entries without source evidence must be removed");
    AssertTrue(result.Document.TraitProfile.Pride == 2, "axis entries with source evidence must remain");
}
void PrunesSemanticallyUnrelatedSparseEvidence()
{
    const string source = "年龄20，傲娇，身材娇小，是个坚强的女战士，对敌人不存仁慈，但恋爱时却显得手足无措。";
    PersonaIntermediateCandidateParseResult result = PersonaIntermediateCandidateParser.Parse(
        "{\"axes\":[{\"index\":2,\"value\":2,\"source\":\"傲娇\"},{\"index\":10,\"value\":-2,\"source\":\"身材娇小\"}],\"flags\":[{\"index\":0,\"source\":\"对敌人不存仁慈\"},{\"index\":1,\"source\":\"恋爱时却显得手足无措\"}]}",
        source,
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(result.IsValid && result.Document != null, "semantically unrelated optional items must be pruned without losing the core");
    AssertTrue(result.Document!.TraitProfile.Pride == 2, "a direct pride cue must remain accepted");
    AssertTrue(result.Document.ExpressionProfile.Warmth == null, "body size must not be accepted as warmth evidence");
    AssertTrue(result.Document.Tags.Count == 0, "unrelated hostility and romantic awkwardness must not activate legacy flags");
}

void RejectsDuplicateDslAxisDefinitions()
{
    const string candidate = """
        [PERSONA_LOAD]
        SELF_CLAIM_NAME
        LANG_ZH_CN_ONLY

        [PERSONALITY_CORE]
        TRAIT_RISK_CAUTIOUS_SLIGHT
        TRAIT_RISK_CAUTIOUS_STRONG
        """;

    PersonaDslCandidateParseResult result = PersonaDslCandidateParser.Parse(
        candidate,
        "谨慎的角色。",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(!result.IsValid, "duplicate semantic axes must be rejected");
    AssertEqual("persona.dsl_candidate_axis_duplicate", result.ErrorCode, "duplicate axis error code must be stable");
}

void RejectsDuplicateDslFacetDefinitions()
{
    const string candidate = """
        [PERSONA_LOAD]
        SELF_CLAIM_NAME
        LANG_ZH_CN_ONLY

        [PERSONALITY_CORE]
        FACET_TRAIT_CAUTIOUS_STRENGTH_1
        FACET_TRAIT_CAUTIOUS_STRENGTH_2
        """;

    PersonaDslCandidateParseResult result = PersonaDslCandidateParser.Parse(
        candidate,
        "谨慎的角色。",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(!result.IsValid, "duplicate semantic facets must be rejected");
    AssertEqual("persona.dsl_candidate_facet_duplicate", result.ErrorCode, "duplicate facet error code must be stable");
}

void RejectsNegatedDirectnessEvidence()
{
    const string source = "间接表达，且不采用直接方式。";
    PersonaIntermediateCandidateParseResult result = PersonaIntermediateCandidateParser.Parse(
        "{\"axes\":[{\"index\":7,\"value\":2,\"source\":\"间接表达，且不采用直接方式\"}]}",
        source,
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(result.IsValid && result.Document != null, "negated evidence must prune only the unsupported axis");
    AssertTrue(result.Document!.ExpressionProfile.Directness == null, "negated directness must not create positive directness");
}

void DoesNotMatchInformalAsFormal()
{
    PersonaIntermediateCandidateParseResult result = PersonaIntermediateCandidateParser.Parse(
        "{\"axes\":[{\"index\":8,\"value\":2,\"source\":\"informal\"}]}",
        "informal",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(result.IsValid && result.Document != null, "substring-only evidence must be pruned");
    AssertTrue(result.Document!.ExpressionProfile.Formality == null, "informal must not match formal");
}

void DoesNotMatchSwarmAsWarm()
{
    PersonaIntermediateCandidateParseResult result = PersonaIntermediateCandidateParser.Parse(
        "{\"axes\":[{\"index\":10,\"value\":2,\"source\":\"swarm\"}]}",
        "swarm",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(result.IsValid && result.Document != null, "substring-only warmth evidence must be pruned");
    AssertTrue(result.Document!.ExpressionProfile.Warmth == null, "swarm must not match warm");
}

void DoesNotMatchNonTradeableAsTradeable()
{
    PersonaIntermediateCandidateParseResult result = PersonaIntermediateCandidateParser.Parse(
        "{\"axes\":[{\"index\":24,\"value\":-2,\"source\":\"non-tradeable\"}]}",
        "non-tradeable",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(result.IsValid && result.Document != null, "hyphenated negation evidence must be pruned for the opposite polarity");
    AssertTrue(result.Document!.CommitmentProfile.ValueTradeability == null, "non-tradeable must not match tradeable");
}

void PreservesNegatedPositiveEvidence()
{
    PersonaIntermediateCandidateParseResult promise = PersonaIntermediateCandidateParser.Parse(
        "{\"axes\":[{\"index\":22,\"value\":2,\"source\":\"不轻易承诺\"}]}",
        "不轻易承诺",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());
    PersonaIntermediateCandidateParseResult value = PersonaIntermediateCandidateParser.Parse(
        "{\"axes\":[{\"index\":24,\"value\":2,\"source\":\"不可交换\"}]}",
        "不可交换",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(promise.IsValid && promise.Document?.CommitmentProfile.PromiseCaution == 2, "cautious promise phrase must remain positive evidence");
    AssertTrue(value.IsValid && value.Document?.CommitmentProfile.ValueTradeability == 2, "non-tradeable phrase must remain positive evidence");
}

string RepeatText(string value, int count)
{
    return string.Concat(Enumerable.Repeat(value, count));
}
void TrimsOversizedTemplatesByPriority()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.oversized",
        DisplayName = "测试角色",
        Core = RepeatText("核心人格。", 600),
        IdentityFacts = RepeatText("身份事实。", 400),
        PublicDescription = RepeatText("公开表达。", 400),
        PrivateDescription = RepeatText("私下行为。", 400),
        ContradictionDescription = RepeatText("承诺与破例。", 400),
        Summary = RepeatText("摘要。", 400),
        Tags = new List<string> { "boundary.no_empty_promises" },
        TraitProfile = new PersonaTraitProfile { Caution = 1, Pride = 2 },
        CommitmentProfile = new PersonaCommitmentProfile { PromiseCaution = 2 }
    };

    string dsl = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 1024).Dsl;

    AssertTrue(System.Text.Encoding.UTF8.GetByteCount(dsl) <= 1024, "trimmed template must stay within the byte budget");
    AssertTrue(dsl.Contains("[PERSONALITY_CORE]", StringComparison.Ordinal), "core section must be retained");
    AssertTrue(dsl.Contains("[PERSONA_IDENTITY]", StringComparison.Ordinal), "identity section must be retained");
    AssertTrue(dsl.Contains("BOUNDARY_NO_EMPTY_PROMISES", StringComparison.Ordinal), "boundary must be retained");
}
void TrimsOptionalProseBeforeKeywordSections()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.prose-first",
        DisplayName = "预算测试",
        Core = "稳定核心。",
        IdentityFacts = "身份事实。",
        PublicDescription = RepeatText("公开正文。", 180),
        PrivateDescription = RepeatText("私下正文。", 180),
        Tags = new List<string>
        {
            "expression.measured",
            "behavior.bargains",
            "boundary.no_empty_promises"
        }
    };

    string dsl = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 1024).Dsl;

    AssertTrue(System.Text.Encoding.UTF8.GetByteCount(dsl) <= 1024, "trimmed keyword template must stay within the byte budget");
    AssertTrue(dsl.Contains("EXPRESSION_MEASURED", StringComparison.Ordinal), "public keyword must survive optional public prose trimming");
    AssertTrue(dsl.Contains("BEHAVIOR_BARGAINS", StringComparison.Ordinal), "private keyword must survive optional private prose trimming");
    AssertTrue(dsl.Contains("BOUNDARY_NO_EMPTY_PROMISES", StringComparison.Ordinal), "boundary keyword must survive trimming");
    AssertTrue(!dsl.Contains("公开正文。", StringComparison.Ordinal), "oversized public prose must be removed before its keyword section");
    AssertTrue(!dsl.Contains("私下正文。", StringComparison.Ordinal), "oversized private prose must be removed before its keyword section");
}

void ReportsCompressionWhilePreservingProtectedFields()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.compressed",
        DisplayName = "压缩测试",
        Core = "核心人格。",
        PublicDescription = RepeatText("公开证据。", 500),
        PrivateDescription = RepeatText("私下证据。", 500),
        Tags = new List<string> { "boundary.no_empty_promises" },
        TraitProfile = new PersonaTraitProfile { Pride = 2 }
    };

    PersonaDslResult result = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 1024);

    AssertTrue(result.IsSuccess, "optional evidence compression should succeed");
    AssertTrue(result.Status == PersonaDslGenerationStatus.Compressed, "compression must be visible in the typed result");
    AssertTrue(result.Diagnostic?.Compressed == true, "compression diagnostic must be set");
    AssertTrue(result.Dsl.Contains("ID=\"free.compressed\"", StringComparison.Ordinal), "identity ID must survive compression");
    AssertTrue(result.Dsl.Contains("NAME=\"压缩测试\"", StringComparison.Ordinal), "identity name must survive compression");
    AssertTrue(result.Dsl.Contains("TRAIT_PRIDE_PROUD_STRONG", StringComparison.Ordinal), "configured core axis must survive compression");
    AssertTrue(result.Dsl.Contains("BOUNDARY_NO_EMPTY_PROMISES", StringComparison.Ordinal), "configured boundary must survive compression");
}

void DeduplicatesRepeatedEvidenceWithinOneField()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.dedup",
        DisplayName = "去重测试",
        Core = "核心人格。",
        SelfClaimRules = new List<string> { "不退让。", "  不退让。  ", "先观察。" }
    };

    string dsl = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096).Dsl;
    int first = dsl.IndexOf("不退让。", StringComparison.Ordinal);
    int second = dsl.IndexOf("不退让。", first + 1, StringComparison.Ordinal);

    AssertTrue(first >= 0, "first evidence occurrence must remain");
    AssertTrue(second < 0, "normalized duplicate evidence must be removed");
    AssertTrue(dsl.Contains("先观察。", StringComparison.Ordinal), "distinct evidence must remain");
}

void ReportsEvidenceDeduplicationInDiagnostics()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.dedup-diagnostic",
        DisplayName = "去重诊断",
        Core = "核心人格。",
        SelfClaimRules = new List<string> { "不退让。", "  不退让。  ", "先观察。" }
    };

    PersonaDslResult result = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096);

    AssertTrue(result.IsSuccess, "deduplication should not prevent canonical generation");
    AssertTrue(result.Diagnostic?.Actions.Any(action => action.StartsWith("deduplicated:SELF_CLAIM_RULES:1", StringComparison.Ordinal)) == true, "deduplication must be visible in diagnostics");
}

void PreservesSameSectionEvidenceFromDistinctFields()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.same-section-evidence",
        DisplayName = "同段证据",
        Core = "核心人格。",
        PublicDescription = "她公开保持克制。",
        SelfClaimRules = new List<string> { "她公开保持克制。" }
    };

    PersonaDslResult result = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096);

    AssertTrue(result.IsSuccess, "same-section evidence from distinct fields must remain a valid canonical template");
    AssertTrue(result.Dsl.Split("DATA_CN=\"她公开保持克制。\"", StringSplitOptions.None).Length - 1 == 2, "distinct fields must not be deduplicated across semantic field boundaries");
}

void CompressesScalarBoundaryTextWithoutSplittingUnicode()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.scalar-boundary",
        DisplayName = "Unicode 压缩",
        Core = string.Concat(Enumerable.Repeat("😀", 400))
    };

    PersonaDslResult result = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 1024);

    AssertTrue(result.IsSuccess, "long text without sentence punctuation should compress at a Unicode scalar boundary");
    AssertTrue(result.Status == PersonaDslGenerationStatus.Compressed, "scalar-boundary compression must be observable");
    AssertTrue(result.Diagnostic?.ErrorCode == "compressed_optional_fields", "compressed output must expose the stable diagnostic code");
    AssertTrue(result.Diagnostic?.ProtectedFieldsPreserved.Contains("PERSONALITY_CORE.DATA_CN", StringComparer.Ordinal) != true, "segmentable core evidence must not be reported as atomic protected data");
    AssertTrue(System.Text.Encoding.UTF8.GetByteCount(result.Dsl) <= 1024, "scalar-boundary output must stay within the byte budget");
    for (int index = 0; index < result.Dsl.Length; index++)
    {
        if (char.IsHighSurrogate(result.Dsl[index])) AssertTrue(index + 1 < result.Dsl.Length && char.IsLowSurrogate(result.Dsl[index + 1]), "output must not contain an unpaired high surrogate");
        if (char.IsLowSurrogate(result.Dsl[index])) AssertTrue(index > 0 && char.IsHighSurrogate(result.Dsl[index - 1]), "output must not contain an unpaired low surrogate");
    }
}

void ReportsCoreBudgetFailureWithoutPartialDsl()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.too-small",
        DisplayName = "预算失败",
        Core = "核心人格。"
    };

    PersonaDslResult result = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 128);

    AssertTrue(!result.IsSuccess, "an impossible core budget must not be reported as success");
    AssertTrue(result.Status == PersonaDslGenerationStatus.CoreBudgetExceeded, "core budget failure status must be explicit");
    AssertEqual(string.Empty, result.Dsl, "failed generation must not return partial DSL");
    AssertEqual("persona.template_core_budget_exceeded", result.Diagnostic?.ErrorCode ?? string.Empty, "core budget error code must be stable");
}

void VerifiesProtectedEntriesAfterCanonicalRender()
{
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.roundtrip-protected",
        DisplayName = "她说\"不\"",
        Core = "高傲而克制。",
        IdentityFacts = "来自边境。\n不轻易许诺。",
        TraitProfile = new PersonaTraitProfile { Pride = 2 },
        BehaviorProfile = new PersonaBehaviorProfile { Conditionality = 2 },
        ReactionProfile = new PersonaReactionProfile
        {
            Confrontation = 2,
            SensitiveConditions = "受到公开羞辱时",
            ConditionalResponses = "先警告，再反击。"
        },
        CommitmentProfile = new PersonaCommitmentProfile
        {
            PromiseCaution = 2,
            ProtectedValues = "亲卫与名誉"
        },
        Tags = new List<string> { "trigger.public_humiliation", "boundary.no_empty_promises" }
    };

    PersonaDslResult result = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096);

    AssertTrue(result.IsSuccess, "protected semantic fields must survive parse-render verification");
    AssertTrue(result.Dsl.Contains("NAME=\"她说\\\"不\\\"\"", StringComparison.Ordinal), "escaped identity must remain valid after verification");
    AssertTrue(result.Dsl.Contains("SENSITIVE_CONDITIONS=\"受到公开羞辱时\"", StringComparison.Ordinal), "reaction conditions must remain protected");
    AssertTrue(result.Diagnostic?.ProtectedFieldsPreserved.Contains("PERSONALITY_PRIVATE.SENSITIVE_CONDITIONS", StringComparer.Ordinal) == true, "diagnostics must report protected reaction fields");
}
void RejectsUnknownNestedIntermediateFields()
{
    PersonaIntermediateCandidateParseResult invalid = PersonaIntermediateCandidateParser.Parse(
        "{\"reaction\":{\"unknown\":\"内容\"}}",
        "内容",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());
    PersonaIntermediateCandidateParseResult empty = PersonaIntermediateCandidateParser.Parse(
        "{\"reaction\":{},\"commitment\":{}}",
        "内容",
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertTrue(!invalid.IsValid, "unknown nested fields must be rejected");
    AssertEqual("persona.intermediate_unknown_or_duplicate_field", invalid.ErrorCode, "nested field error code must be stable");
    AssertTrue(empty.IsValid && empty.Document != null, "empty optional nested objects must remain valid");
}
void BuildsHeuristicPersonaFromFreeText()
{
    const string source = "年龄20，傲娇，身材娇小，涉世未深，是个坚强的女战士，对敌人不存仁慈，但恋爱时却显得手足无措。";
    PersonaDocument document = PersonaIntermediateCandidateParser.CreateHeuristicDocument(
        source,
        "free.generated.persona",
        "测试角色",
        PersonaTagRegistry.CreateDefault());

    AssertEqual(source, document.Core, "heuristic conversion must preserve the confirmed source as core");
    AssertTrue(document.TraitProfile.Pride == 1, "explicit pride evidence must become a slight positive axis");
    AssertTrue(document.BehaviorProfile.TrustTesting == -1, "inexperience evidence must become a slight trusting axis");
    AssertTrue(document.ExpressionProfile.Warmth == null, "body size and romantic awkwardness must not imply warmth");
    string dsl = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096).Dsl;
    AssertTrue(dsl.Contains("TRAIT_PRIDE_PROUD_SLIGHT", StringComparison.Ordinal), "heuristic pride must render canonically");
    AssertTrue(dsl.Contains("BEHAVIOR_TRUST_TRUSTING_SLIGHT", StringComparison.Ordinal), "heuristic trust must render canonically");
}
void EnrichesExplicitReactionAndExchangeEvidence()
{
    const string source = "角色有三个弱点：如果受到拷问，就会失去反抗。为了运作部队，会用身体犒赏亲卫。";
    PersonaDocument document = new PersonaDocument
    {
        Id = "free.semantic-enrichment",
        DisplayName = "测试角色",
        Core = source
    };

    PersonaSemanticEnricher.MergeEvidenceBackedAxes(document, source, PersonaTagRegistry.CreateDefault());

    AssertTrue(document.ReactionProfile.SensitiveConditions.Contains("如果受到拷问", StringComparison.Ordinal), "explicit trigger evidence must enter reaction conditions");
    AssertTrue(document.ReactionProfile.ConditionalResponses.Contains("就会失去反抗", StringComparison.Ordinal), "explicit response evidence must enter conditional reactions");
    AssertTrue(document.BehaviorProfile.Conditionality == 1, "explicit body-for-reward exchange must enrich conditionality");
    AssertTrue(document.CommitmentProfile.ValueTradeability == -1, "explicit reward exchange must enrich value tradeability");
    AssertEqual("运作部队", document.CommitmentProfile.PriorityOrder, "purpose evidence must remain compact");
    AssertEqual("部队", document.CommitmentProfile.ProtectedValues, "protected value evidence must remain compact");
    AssertEqual("亲卫", document.CommitmentProfile.ApplicableScope, "exchange target evidence must remain compact");
    AssertTrue(document.Tags.Contains("behavior.bargains", StringComparer.Ordinal), "explicit exchange must activate the bargaining behavior tag");

    string dsl = PersonaDslGenerator.Generate(document, PersonaTagRegistry.CreateDefault(), 4096).Dsl;
    AssertTrue(dsl.Contains("SENSITIVE_CONDITIONS=", StringComparison.Ordinal), "reaction evidence must render in the private section");
    AssertTrue(dsl.Contains("COMMITMENT_VALUE_TRADEABLE_VALUES_SLIGHT", StringComparison.Ordinal), "exchange evidence must render in the contradiction section");
}

void MigratesV1DocumentToAuthoringV2()
{
    PersonaDocument source = CreateAuthoringFixtureDocument();
    PersonaAuthoringBuildResult result = PersonaAuthoringV2Adapter.Build(source, string.Empty, "none");

    AssertTrue(result.IsSuccess && result.Document != null, "valid v1 source must migrate successfully");
    AssertEqual("awake.persona.authoring.v2", result.Document!.SchemaVersion, "authoring schema version must be pinned");
    AssertEqual(source.Id, result.Document.DocumentId, "document ID must be copied");
    AssertEqual(source.Core, result.Document.Authored.Core, "core text must be copied without rewriting");
    AssertEqual(source.SourceDescription, result.Document.Source.ConfirmedText, "confirmed source must be copied");
    AssertTrue(result.Document.Source.ConfirmedTextSha256.Length == 64, "confirmed source hash must be emitted");
    AssertEqual("identity_note", result.Document.Facts.Single().FactId, "identity facts must become one stable identity note");
    AssertEqual("needs_review", result.Document.Observations.Single(observation => observation.SelectorId == "trait.pragmatic").ReviewState, "migrated observations must require review");
    AssertTrue(result.Document.Rules.Count == 0, "v1 prose must not fabricate formal rules");
    using JsonDocument parsed = JsonDocument.Parse(result.CanonicalJson);
    AssertEqual("awake.persona.authoring.v2", parsed.RootElement.GetProperty("schemaVersion").GetString() ?? string.Empty, "canonical JSON must satisfy the authoring root shape");
}

void KeepsExpansionSeparateFromAuthoredCore()
{
    PersonaDocument source = CreateAuthoringFixtureDocument();
    const string expansion = "独立的可编辑扩充，不应覆盖手写核心。";

    PersonaAuthoringBuildResult result = PersonaAuthoringV2Adapter.Build(source, expansion, "user_edited");

    AssertTrue(result.IsSuccess && result.Document != null, "separate expansion must be accepted");
    AssertEqual(source.Core, result.Document!.Authored.Core, "authored core must remain independent");
    AssertEqual(expansion, result.Document.Source.ExpandedText, "expanded text must use only the separate expansion input");
    AssertEqual("user_edited", result.ExpandedTextOrigin, "expansion origin must remain diagnostic metadata");
    AssertTrue(!result.CanonicalJson.Contains("expandedTextOrigin", StringComparison.Ordinal), "expansion origin must not leak into authoring-v2 schema");
}

void MergesMappedObservationsByFidelity()
{
    PersonaDocument source = CreateAuthoringFixtureDocument();
    source.Tags = new List<string> { "trait.pragmatic" };
    source.FacetStrengths = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["trait.pragmatic"] = 2
    };
    source.TraitProfile.Pragmatism = 1;

    PersonaAuthoringBuildResult result = PersonaAuthoringV2Adapter.Build(source, string.Empty, "none");

    AssertTrue(result.IsSuccess && result.Document != null, "overlapping mapped evidence must migrate");
    AwakePersonaAuthoringObservation observation = result.Document!.Observations.Single(item => item.SelectorId == "trait.pragmatic");
    AssertEqual("slight", observation.Value?.ToString() ?? string.Empty, "axis evidence must outrank facet and tag evidence");
    AssertTrue(result.Warnings.Any(warning => warning.Contains("facetStrengths.trait.pragmatic", StringComparison.Ordinal)), "discarded facet evidence must be diagnosed");
    AssertTrue(result.Warnings.Any(warning => warning.Contains("tags.trait.pragmatic", StringComparison.Ordinal)), "discarded tag evidence must be diagnosed");
}

void PreservesUnmappedLegacyValuesWithWarnings()
{
    PersonaDocument source = CreateAuthoringFixtureDocument();
    source.Tags = new List<string> { "trait.cautious" };
    source.FacetStrengths = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["trait.cautious"] = 4
    };
    source.TraitProfile.Caution = -1;
    source.ReactionProfile.SensitiveConditions = "受到羞辱时反击。";

    PersonaAuthoringBuildResult result = PersonaAuthoringV2Adapter.Build(source, string.Empty, "none");

    AssertTrue(result.IsSuccess && result.Document != null, "preserve-only legacy values must not block migration");
    Dictionary<string, object?> preserved = result.Document!.Migration.PreservedLegacyData;
    AssertTrue(preserved.TryGetValue("tags", out object? tagBag) && tagBag is Dictionary<string, object?> tags && tags.ContainsKey("trait.cautious"), "unmapped tag must be preserved");
    AssertTrue(preserved.TryGetValue("facetStrengths", out object? facetBag) && facetBag is Dictionary<string, object?> facets && facets.ContainsKey("trait.cautious"), "unmapped facet must be preserved");
    AssertTrue(preserved.TryGetValue("traitProfile", out object? axisBag) && axisBag is Dictionary<string, object?> axes && axes.ContainsKey("caution"), "unmapped axis must be preserved");
    AssertTrue(preserved.TryGetValue("reactionProfile", out object? reactionBag) && reactionBag is Dictionary<string, object?> reaction && string.Equals(reaction["sensitiveConditions"]?.ToString(), "受到羞辱时反击。", StringComparison.Ordinal), "legacy prose must be preserved");
    AssertTrue(result.Warnings.Any(warning => warning.Contains("legacy", StringComparison.Ordinal)), "preserved legacy data must be visible in warnings");
    AssertTrue(result.Warnings.Any(warning => warning.Contains("legacy_strength_collapsed", StringComparison.Ordinal)), "facet strength collapse must be diagnosed");
}

void FollowsTheCrosswalkZeroAxisOmission()
{
    PersonaDocument source = CreateAuthoringFixtureDocument();
    source.Tags.Clear();
    source.FacetStrengths.Clear();
    source.TraitProfile.Pragmatism = 0;

    PersonaAuthoringBuildResult result = PersonaAuthoringV2Adapter.Build(source, string.Empty, "none");

    AssertTrue(result.IsSuccess && result.Document != null, "zero-axis migration must remain valid");
    AssertTrue(!result.Document!.Observations.Any(observation => observation.SelectorId == "trait.pragmatic"), "current crosswalk zero action is omit");
    AssertTrue(!result.Document.Migration.PreservedLegacyData.ContainsKey("traitProfile"), "omitted zero axis must not enter the preserved bag");
}

void RejectsInvalidAuthoringInputAndDuplicateSourceTags()
{
    PersonaDocument invalidId = CreateAuthoringFixtureDocument();
    invalidId.Id = "invalid-id";
    PersonaAuthoringBuildResult invalid = PersonaAuthoringV2Adapter.Build(invalidId, string.Empty, "none");
    AssertTrue(!invalid.IsSuccess && invalid.ErrorCode.StartsWith("persona.", StringComparison.Ordinal), "invalid authoring IDs must fail closed");

    PersonaDocument duplicateTags = CreateAuthoringFixtureDocument();
    duplicateTags.Tags.Add("trait.pragmatic");
    PersonaAuthoringBuildResult duplicate = PersonaAuthoringV2Adapter.Build(duplicateTags, string.Empty, "none");
    AssertTrue(!duplicate.IsSuccess, "duplicate v1 tags must not produce authoring output");
    AssertTrue(duplicate.ErrorCode.Contains("duplicate", StringComparison.Ordinal), "duplicate source tags need a stable diagnostic");

    PersonaAuthoringBuildResult invalidOrigin = PersonaAuthoringV2Adapter.Build(CreateAuthoringFixtureDocument(), "扩充", "provider_typo");
    AssertEqual("persona.expansion_origin_invalid", invalidOrigin.ErrorCode, "unknown expansion origin must fail closed");
}

void ProducesDeterministicAuthoringCanonicalBytes()
{
    PersonaDocument source = CreateAuthoringFixtureDocument();
    PersonaAuthoringBuildResult first = PersonaAuthoringV2Adapter.Build(source, "扩充", "provider");
    PersonaAuthoringBuildResult second = PersonaAuthoringV2Adapter.Build(source, "扩充", "provider");

    AssertTrue(first.IsSuccess && second.IsSuccess, "determinism fixture must migrate");
    AssertEqual(first.CanonicalJson, second.CanonicalJson, "canonical JSON must be deterministic");
    AssertTrue(first.CanonicalUtf8.SequenceEqual(System.Text.Encoding.UTF8.GetBytes(first.CanonicalJson)), "canonical bytes must be UTF-8 JSON bytes");
    AssertTrue(first.CanonicalUtf8.Length > 0 && first.CanonicalUtf8[^1] != (byte)'\n', "canonical bytes must not end with LF");
    AssertTrue(first.CanonicalUtf8[0] != 0xEF, "canonical bytes must not contain a UTF-8 BOM");
    AssertEqual(first.CanonicalSha256, second.CanonicalSha256, "canonical hashes must be deterministic");
    AssertTrue(first.CanonicalJson.StartsWith("{\"authored\"", StringComparison.Ordinal), "object properties must use ordinal canonical order");
}

void RejectsDriftedAuthoringContractAssets()
{
    PersonaAuthoringContractAssets embedded = PersonaAuthoringContractAssets.LoadEmbedded();
    byte[] driftedRegistry = embedded.RegistryUtf8.ToArray();
    driftedRegistry[0] = (byte)(driftedRegistry[0] ^ 1);
    PersonaAuthoringContractAssets drifted = PersonaAuthoringContractAssets.FromRawUtf8(
        embedded.AuthoringSchemaUtf8,
        embedded.CrosswalkUtf8,
        embedded.CanonicalizationUtf8,
        driftedRegistry);

    PersonaAuthoringBuildResult result = PersonaAuthoringV2Adapter.Build(CreateAuthoringFixtureDocument(), string.Empty, "none", drifted);

    AssertEqual("persona.migration_required", result.ErrorCode, "drifted contract assets must not fall back to a local mapping");
    AssertTrue(result.CanonicalUtf8.Length == 0, "contract failure must not return partial bytes");
}

PersonaDocument CreateAuthoringFixtureDocument()
{
    return new PersonaDocument
    {
        Id = "fixture.persona.authoring",
        DisplayName = "测试角色",
        Core = "手写核心人格。",
        IdentityFacts = "来自边境村落。",
        Summary = "谨慎的边境角色。",
        SourcePackId = "fixture_pack",
        TemplateVersion = "persona-load.v2",
        Status = "approved",
        SourceDescription = "确认来源：先观察再行动。",
        PublicDescription = "公开场合保持克制。",
        PrivateDescription = "私下先确认代价。",
        ContradictionDescription = "高傲与谨慎并存。",
        SelfClaimRules = new List<string> { "对外只自称我。" },
        RealSelfBehaviors = new List<string> { "独处时放下戒备。" },
        SelfClaimExamples = new List<string> { "我会如何回应？" },
        Tags = new List<string> { "trait.pragmatic", "expression.measured", "behavior.bargains" },
        FacetStrengths = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["trait.pragmatic"] = 2,
            ["expression.measured"] = 2,
            ["behavior.bargains"] = 2
        },
        TraitProfile = new PersonaTraitProfile { Pragmatism = 2 },
        ExpressionProfile = new PersonaExpressionProfile { Restraint = 2 },
        BehaviorProfile = new PersonaBehaviorProfile { Conditionality = 2 }
    };
}


void IssuesOnlyApprovedReceiptAndIsolatedHandoff()
{
    DateTime now = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc);
    PersonaContractClosureService service = new PersonaContractClosureService(utcNow: () => now);
    PersonaDocument document = CreateAuthoringFixtureDocument();
    document.Tags.Clear();
    document.FacetStrengths.Clear();
    document.ReactionProfile.SensitiveConditions = string.Empty;
    document.ReactionProfile.ConditionalResponses = string.Empty;
    document.CommitmentProfile.PriorityOrder = string.Empty;
    document.CommitmentProfile.ProtectedValues = string.Empty;
    document.CommitmentProfile.ApplicableScope = string.Empty;
    document.CommitmentProfile.ExceptionCost = string.Empty;
    document.CommitmentProfile.BreachResponse = string.Empty;
    ContractIssuerContext context = new ContractIssuerContext { SessionId = "pwb-session-" + new string('a', 32), IssuerId = "pwb-issuer-" + new string('b', 32), Fence = 1 };
    ContractClosureResult<ApprovalReceipt> receipt = service.IssueReceipt(document, context, "receipt-1", "evidence-1", TimeSpan.FromMinutes(10));
    AssertTrue(receipt.IsSuccess && receipt.Value != null, "approved local document must issue receipt");
    AssertEqual("approved", receipt.Value!.LocalApproval, "receipt must record local approval");
    AssertEqual("not_requested", receipt.Value.AwakeApproval, "receipt must not claim AWAKE approval");
    AuthoringHandoffEnvelopeContext envelope = new AuthoringHandoffEnvelopeContext { WorkspaceId = "workspace.fixture", Revision = 7 };
    ContractClosureResult<AuthoringHandoff> handoff = service.IssueHandoff(receipt.Value, document, context, envelope, "handoff-1", TimeSpan.FromMinutes(10));
    AssertTrue(handoff.IsSuccess && handoff.Value != null, "validated receipt must issue handoff: " + string.Join(",", handoff.Errors) + " receiptWarnings=" + string.Join("|", receipt.Value!.Warnings));
    AssertEqual("not_requested", handoff.Value!.AwakeApproval, "handoff must isolate AWAKE approval");
    PersonaDocument draft = CreateAuthoringFixtureDocument();
    draft.Status = PersonaReviewStatus.Draft;
    AssertTrue(!service.IssueReceipt(draft, context, "receipt-draft", "evidence-1", TimeSpan.FromMinutes(10)).IsSuccess, "draft must not issue receipt");
}

void IssuesCrossWorkstationHandoffEnvelope()
{
    DateTime now = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc);
    PersonaContractClosureService service = new PersonaContractClosureService(utcNow: () => now);
    PersonaDocument document = CreateAuthoringFixtureDocument();
    document.Tags.Clear();
    document.FacetStrengths.Clear();
    document.ReactionProfile.SensitiveConditions = string.Empty;
    document.ReactionProfile.ConditionalResponses = string.Empty;
    document.CommitmentProfile.PriorityOrder = string.Empty;
    document.CommitmentProfile.ProtectedValues = string.Empty;
    document.CommitmentProfile.ApplicableScope = string.Empty;
    document.CommitmentProfile.ExceptionCost = string.Empty;
    document.CommitmentProfile.BreachResponse = string.Empty;
    ContractIssuerContext context = new ContractIssuerContext { SessionId = "pwb-session-" + new string('7', 32), IssuerId = "pwb-issuer-" + new string('8', 32), Fence = 7 };
    ApprovalReceipt receipt = service.IssueReceipt(document, context, "receipt-envelope", "evidence-envelope", TimeSpan.FromMinutes(10)).Value!;
    AuthoringHandoffEnvelopeContext envelopeContext = new AuthoringHandoffEnvelopeContext { WorkspaceId = "awake.authoring.fixture", Revision = 12 };
    AuthoringHandoff handoff = service.IssueHandoff(receipt, document, context, envelopeContext, "handoff-envelope", TimeSpan.FromMinutes(10)).Value!;

    AssertEqual("awake.authoring.fixture", handoff.Envelope.WorkspaceId, "envelope must preserve workspace_id");
    AssertEqual(handoff.DocumentId, handoff.Envelope.DocumentId, "envelope document_id must mirror the authoritative document id");
    AssertEqual("12", handoff.Envelope.Revision.ToString(), "envelope must preserve revision");
    AssertEqual(handoff.ContentSha256, handoff.Envelope.ContentSha256, "envelope content_sha256 must mirror canonical content");
    AssertEqual("persona_workbench", handoff.Envelope.Provenance.Producer, "provenance must identify Persona Workbench");
    AssertEqual("persona_local_authoring", handoff.Envelope.Provenance.Authority, "provenance must preserve local authoring authority");
    AssertEqual(receipt.ReceiptId, handoff.Envelope.Provenance.ReceiptId, "provenance must bind the approval receipt");
    AssertEqual(receipt.EvidenceId, handoff.Envelope.Provenance.EvidenceId, "provenance must bind evidence");
    AssertEqual("local_approved", handoff.Envelope.ReviewStatus, "review_status must describe local review only");
    AssertEqual(handoff.HandoffId, handoff.Envelope.HandoffId, "envelope handoff_id must mirror the handoff");
    AssertEqual(handoff.ExpiresAtUtc.ToString("O"), handoff.Envelope.ExpiresAt.ToString("O"), "envelope expires_at must mirror expiry");
    AssertEqual("issued", handoff.Envelope.LifecycleStatus, "Persona Workbench may only produce issued handoffs");
    AssertEqual("not_requested", handoff.AwakeApproval, "envelope must not elevate AWAKE approval");
}

void ReturnsIdenticalHandoffForIdenticalRetry()
{
    PersonaContractClosureService service = new PersonaContractClosureService();
    PersonaDocument document = CreateAuthoringFixtureDocument();
    document.Tags.Clear();
    document.FacetStrengths.Clear();
    document.ReactionProfile.SensitiveConditions = string.Empty;
    document.ReactionProfile.ConditionalResponses = string.Empty;
    document.CommitmentProfile.PriorityOrder = string.Empty;
    document.CommitmentProfile.ProtectedValues = string.Empty;
    document.CommitmentProfile.ApplicableScope = string.Empty;
    document.CommitmentProfile.ExceptionCost = string.Empty;
    document.CommitmentProfile.BreachResponse = string.Empty;
    ContractIssuerContext context = new ContractIssuerContext { SessionId = "pwb-session-" + new string('c', 32), IssuerId = "pwb-issuer-" + new string('d', 32), Fence = 2 };
    ApprovalReceipt receipt = service.IssueReceipt(document, context, "receipt-2", "evidence-2", TimeSpan.FromMinutes(10)).Value!;
    AuthoringHandoffEnvelopeContext envelope = new AuthoringHandoffEnvelopeContext { WorkspaceId = "workspace.retry", Revision = 2 };
    ContractClosureResult<AuthoringHandoff> firstResult = service.IssueHandoff(receipt, document, context, envelope, "handoff-2", TimeSpan.FromMinutes(10));
    AssertTrue(firstResult.IsSuccess && firstResult.Value != null, "first handoff failed: " + string.Join(",", firstResult.Errors));
    AuthoringHandoff first = firstResult.Value!;
    AuthoringHandoff second = service.IssueHandoff(receipt, document, context, envelope, "handoff-2", TimeSpan.FromMinutes(10)).Value!;
    AssertEqual(first.HandoffId, second.HandoffId, "identical retry must return the same handoff id");
    AssertEqual(first.RequestFingerprint, second.RequestFingerprint, "identical retry must preserve request fingerprint");
}

void FailsHandoffClosedOnReceiptWarnings()
{
    PersonaContractClosureService service = new PersonaContractClosureService();
    PersonaDocument document = CreateAuthoringFixtureDocument();
    ContractIssuerContext context = new ContractIssuerContext { SessionId = "pwb-session-" + new string('e', 32), IssuerId = "pwb-issuer-" + new string('f', 32), Fence = 3 };
    ApprovalReceipt receipt = service.IssueReceipt(document, context, "receipt-warning", "evidence-3", TimeSpan.FromMinutes(10)).Value!;
    ApprovalReceipt warned = new ApprovalReceipt
    {
        SchemaVersion = receipt.SchemaVersion, ReceiptId = receipt.ReceiptId, LocalApproval = receipt.LocalApproval, AwakeApproval = receipt.AwakeApproval, DocumentId = receipt.DocumentId, ContentSha256 = receipt.ContentSha256, CanonicalProofSha256 = receipt.CanonicalProofSha256, EvidenceId = receipt.EvidenceId, IssuedAtUtc = receipt.IssuedAtUtc, ExpiresAtUtc = receipt.ExpiresAtUtc, Issuer = receipt.Issuer, Session = receipt.Session, Fence = receipt.Fence, Source = receipt.Source, Authoring = receipt.Authoring, Crosswalk = receipt.Crosswalk, Registry = receipt.Registry, Warnings = new[] { "preserve_only:legacy" }
    };
    AuthoringHandoffEnvelopeContext envelope = new AuthoringHandoffEnvelopeContext { WorkspaceId = "workspace.warning", Revision = 3 };
    ContractClosureResult<AuthoringHandoff> result = service.IssueHandoff(warned, document, context, envelope, "handoff-warning", TimeSpan.FromMinutes(10));
    AssertTrue(!result.IsSuccess && result.Errors.Contains("handoff.warnings_fail_closed"), "warning-bearing receipt must not create handoff");
}

void RejectsExpiredReceiptAndContractDrift()
{
    DateTime now = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc);
    PersonaAuthoringContractAssets assets = PersonaAuthoringContractAssets.LoadEmbedded();
    PersonaContractClosureService service = new PersonaContractClosureService(assets, () => now);
    PersonaDocument document = CreateAuthoringFixtureDocument();
    ContractIssuerContext context = new ContractIssuerContext { SessionId = "pwb-session-" + new string('1', 32), IssuerId = "pwb-issuer-" + new string('2', 32), Fence = 4 };
    ApprovalReceipt receipt = service.IssueReceipt(document, context, "receipt-expiry", "evidence-4", TimeSpan.FromMinutes(1)).Value!;
    now = now.AddMinutes(2);
    AssertTrue(PersonaContractClosureValidator.ValidateReceipt(receipt, document, context, assets, now).Contains("contract.expired"), "expired receipt must be rejected");
    byte[] driftedRegistry = assets.RegistryUtf8.Concat(new byte[] { 0x20 }).ToArray();
    PersonaAuthoringContractAssets drifted = PersonaAuthoringContractAssets.FromRawUtf8(assets.AuthoringSchemaUtf8, assets.CrosswalkUtf8, assets.CanonicalizationUtf8, driftedRegistry);
    AssertTrue(!service.IssueReceipt(document, context, "receipt-drift", "evidence-4", TimeSpan.FromMinutes(10)).IsSuccess || !drifted.IsValid, "contract asset drift must fail closed");
}

void ValidatesHandoffSemanticBindings()
{
    DateTime now = new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    PersonaContractClosureService service = new PersonaContractClosureService(
        PersonaAuthoringContractAssets.LoadEmbedded(),
        () => now);
    PersonaDocument document = CreateAuthoringFixtureDocument();
    document.Tags.Clear();
    document.FacetStrengths.Clear();
    document.ReactionProfile.SensitiveConditions = string.Empty;
    document.ReactionProfile.ConditionalResponses = string.Empty;
    document.CommitmentProfile.PriorityOrder = string.Empty;
    document.CommitmentProfile.ProtectedValues = string.Empty;
    document.CommitmentProfile.ApplicableScope = string.Empty;
    document.CommitmentProfile.ExceptionCost = string.Empty;
    document.CommitmentProfile.BreachResponse = string.Empty;
    ContractIssuerContext context = new ContractIssuerContext
    {
        SessionId = "pwb-session-" + new string('a', 32),
        IssuerId = "pwb-issuer-" + new string('b', 32),
        Fence = 8
    };
    ApprovalReceipt receipt = service.IssueReceipt(
        document,
        context,
        "semantic-receipt",
        "semantic-evidence",
        TimeSpan.FromMinutes(10)).Value!;
    AuthoringHandoffEnvelopeContext envelope = new AuthoringHandoffEnvelopeContext
    {
        WorkspaceId = "workspace.semantic",
        Revision = 4
    };
    ContractClosureResult<AuthoringHandoff> handoffResult = service.IssueHandoff(
        receipt,
        document,
        context,
        envelope,
        "semantic-handoff",
        TimeSpan.FromMinutes(10));
    AssertTrue(
        handoffResult.IsSuccess && handoffResult.Value != null,
        "semantic handoff fixture must be issuable: " + string.Join(",", handoffResult.Errors));
    AuthoringHandoff handoff = handoffResult.Value!;

    IReadOnlyList<string> validErrors = AuthoringHandoffSemanticValidator.Validate(handoff, "semantic-handoff", now);
    AssertTrue(
        validErrors.Count == 0,
        "a producer handoff with matching fields must pass semantic validation: " + string.Join(",", validErrors));

    AuthoringHandoff forgedHash = new AuthoringHandoff
    {
        SchemaVersion = handoff.SchemaVersion,
        HandoffId = handoff.HandoffId,
        ReceiptId = handoff.ReceiptId,
        AwakeApproval = handoff.AwakeApproval,
        DocumentId = handoff.DocumentId,
        CanonicalJson = handoff.CanonicalJson + " ",
        ContentSha256 = handoff.ContentSha256,
        CanonicalProofSha256 = handoff.CanonicalProofSha256,
        EvidenceId = handoff.EvidenceId,
        IssuedAtUtc = handoff.IssuedAtUtc,
        ExpiresAtUtc = handoff.ExpiresAtUtc,
        Issuer = handoff.Issuer,
        Session = handoff.Session,
        Fence = handoff.Fence,
        Source = handoff.Source,
        Authoring = handoff.Authoring,
        Crosswalk = handoff.Crosswalk,
        Registry = handoff.Registry,
        RequestFingerprint = handoff.RequestFingerprint,
        Envelope = handoff.Envelope
    };
    AssertTrue(
        AuthoringHandoffSemanticValidator.Validate(forgedHash, "semantic-handoff", now)
            .Contains("handoff.content_hash_mismatch"),
        "changing the canonical payload must invalidate its content hash");

    AuthoringHandoff forgedFingerprint = new AuthoringHandoff
    {
        SchemaVersion = handoff.SchemaVersion,
        HandoffId = handoff.HandoffId,
        ReceiptId = handoff.ReceiptId,
        AwakeApproval = handoff.AwakeApproval,
        DocumentId = handoff.DocumentId,
        CanonicalJson = handoff.CanonicalJson,
        ContentSha256 = handoff.ContentSha256,
        CanonicalProofSha256 = handoff.CanonicalProofSha256,
        EvidenceId = handoff.EvidenceId,
        IssuedAtUtc = handoff.IssuedAtUtc,
        ExpiresAtUtc = handoff.ExpiresAtUtc,
        Issuer = handoff.Issuer,
        Session = handoff.Session,
        Fence = handoff.Fence,
        Source = handoff.Source,
        Authoring = handoff.Authoring,
        Crosswalk = handoff.Crosswalk,
        Registry = handoff.Registry,
        RequestFingerprint = handoff.RequestFingerprint,
        Envelope = new AuthoringHandoffEnvelope
        {
            WorkspaceId = handoff.Envelope.WorkspaceId,
            DocumentId = handoff.Envelope.DocumentId,
            Revision = handoff.Envelope.Revision + 1,
            ContentSha256 = handoff.Envelope.ContentSha256,
            Provenance = handoff.Envelope.Provenance,
            ReviewStatus = handoff.Envelope.ReviewStatus,
            HandoffId = handoff.Envelope.HandoffId,
            ExpiresAt = handoff.Envelope.ExpiresAt,
            LifecycleStatus = handoff.Envelope.LifecycleStatus
        }
    };
    IReadOnlyList<string> fingerprintErrors = AuthoringHandoffSemanticValidator.Validate(
        forgedFingerprint,
        "semantic-handoff",
        now);
    AssertTrue(
        fingerprintErrors.Contains("handoff.request_fingerprint_mismatch"),
        "changing the envelope revision must invalidate the request fingerprint");

    AuthoringHandoff forgedProducer = new AuthoringHandoff
    {
        SchemaVersion = handoff.SchemaVersion,
        HandoffId = "wbs-handoff-" + new string('1', 32),
        ReceiptId = handoff.ReceiptId,
        AwakeApproval = handoff.AwakeApproval,
        DocumentId = handoff.DocumentId,
        CanonicalJson = handoff.CanonicalJson,
        ContentSha256 = handoff.ContentSha256,
        CanonicalProofSha256 = handoff.CanonicalProofSha256,
        EvidenceId = handoff.EvidenceId,
        IssuedAtUtc = handoff.IssuedAtUtc,
        ExpiresAtUtc = handoff.ExpiresAtUtc,
        Issuer = handoff.Issuer,
        Session = handoff.Session,
        Fence = handoff.Fence,
        Source = handoff.Source,
        Authoring = handoff.Authoring,
        Crosswalk = handoff.Crosswalk,
        Registry = handoff.Registry,
        RequestFingerprint = handoff.RequestFingerprint,
        Envelope = new AuthoringHandoffEnvelope
        {
            WorkspaceId = handoff.Envelope.WorkspaceId,
            DocumentId = handoff.Envelope.DocumentId,
            Revision = handoff.Envelope.Revision,
            ContentSha256 = handoff.Envelope.ContentSha256,
            Provenance = new AuthoringHandoffProvenance
            {
                Producer = "worldbook_studio",
                Authority = handoff.Envelope.Provenance.Authority,
                ReceiptId = handoff.Envelope.Provenance.ReceiptId,
                EvidenceId = handoff.Envelope.Provenance.EvidenceId
            },
            ReviewStatus = handoff.Envelope.ReviewStatus,
            HandoffId = "wbs-handoff-" + new string('1', 32),
            ExpiresAt = handoff.Envelope.ExpiresAt,
            LifecycleStatus = handoff.Envelope.LifecycleStatus
        }
    };
    IReadOnlyList<string> producerErrors = AuthoringHandoffSemanticValidator.Validate(
        forgedProducer,
        "semantic-handoff",
        now);
    AssertTrue(
        producerErrors.Contains("handoff.id_invalid")
            && producerErrors.Contains("handoff.provenance_invalid"),
        "Persona Workbench must reject a handoff with a foreign producer or prefix");

    AuthoringHandoff expired = new AuthoringHandoff
    {
        SchemaVersion = handoff.SchemaVersion,
        HandoffId = handoff.HandoffId,
        ReceiptId = handoff.ReceiptId,
        AwakeApproval = handoff.AwakeApproval,
        DocumentId = handoff.DocumentId,
        CanonicalJson = handoff.CanonicalJson,
        ContentSha256 = handoff.ContentSha256,
        CanonicalProofSha256 = handoff.CanonicalProofSha256,
        EvidenceId = handoff.EvidenceId,
        IssuedAtUtc = now.AddMinutes(-10),
        ExpiresAtUtc = now,
        Issuer = handoff.Issuer,
        Session = handoff.Session,
        Fence = handoff.Fence,
        Source = handoff.Source,
        Authoring = handoff.Authoring,
        Crosswalk = handoff.Crosswalk,
        Registry = handoff.Registry,
        RequestFingerprint = handoff.RequestFingerprint,
        Envelope = handoff.Envelope
    };
    AssertTrue(
        AuthoringHandoffSemanticValidator.Validate(expired, "semantic-handoff", now)
            .Contains("handoff.expired"),
        "expiry equality with the current UTC time must fail closed");
}
string FindGoldenFixture()
{
    DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory != null)
    {
        string candidate = Path.Combine(directory.FullName, "AWAKE", "docs", "fixtures", "persona-load-v2-golden.json");
        if (File.Exists(candidate)) return candidate;
        candidate = Path.Combine(directory.FullName, "docs", "fixtures", "persona-load-v2-golden.json");
        if (File.Exists(candidate)) return candidate;
        directory = directory.Parent;
    }

    throw new FileNotFoundException("Shared Persona golden fixture was not found.");
}

// ── 全库产出体检（PersonaCorpusAudit，2026-09-15）───────────────────────────
// 覆盖的正是「门禁 O5 扫不到」的那一维：selfClaimRules / realSelfBehaviors 的跨卡重复。
// 门槛 ≥3 张卡（与 O5 同值）；下面每条用例都构造了刚好触发、或刚好不触发的输入。

PersonaDocument CreateAuditCard(string displayName)
{
    auditCardSerial++;
    return new PersonaDocument
    {
        Id = "audit." + displayName,
        DisplayName = displayName,
        Core = "专属自述 #" + auditCardSerial + "：这一段只属于这张卡，不参与跨卡比较。",
        SourcePackId = "audit",
        SelfClaimRules = new List<string>(),
        RealSelfBehaviors = new List<string>(),
        SelfClaimExamples = new List<string>()
    };
}

PersonaCorpusAuditFinding? FindRule(PersonaCorpusAuditReport report, string rule)
{
    return report.Findings.FirstOrDefault(finding => finding.Rule == rule);
}

void CorpusAuditReportsCrossCardTextReuse()
{
    List<PersonaDocument> cards = new List<PersonaDocument>();
    foreach (string name in new[] { "甲", "乙", "丙" })
    {
        PersonaDocument card = CreateAuditCard(name);
        card.RealSelfBehaviors.Add("独处时会检查武器，回忆年轻时的战斗");
        cards.Add(card);
    }

    PersonaCorpusAuditReport report = PersonaCorpusAudit.Analyze(cards);
    PersonaCorpusAuditFinding? finding = FindRule(report, PersonaCorpusAudit.RuleTextReused);
    AssertTrue(finding != null, "三张卡逐字相同的 realSelfBehaviors 必须被判为跨卡复用");
    AssertTrue(finding!.Cards.Count == 3, "跨卡复用应列出全部 3 张卡，实际 " + finding.Cards.Count);
    AssertTrue(!report.IsClean, "存在跨卡复用时报告不应是 clean");
}

void CorpusAuditIgnoresReuseBelowThreshold()
{
    List<PersonaDocument> cards = new List<PersonaDocument>();
    foreach (string name in new[] { "甲", "乙" })
    {
        PersonaDocument card = CreateAuditCard(name);
        card.RealSelfBehaviors.Add("独处时会检查武器，回忆年轻时的战斗");
        cards.Add(card);
    }

    PersonaCorpusAuditReport report = PersonaCorpusAudit.Analyze(cards);
    AssertTrue(FindRule(report, PersonaCorpusAudit.RuleTextReused) == null,
        "只有 2 张卡重复时不应报警（门槛是 3 张）");
}

void CorpusAuditNormalizesDisplayName()
{
    List<PersonaDocument> cards = new List<PersonaDocument>();
    foreach (string name in new[] { "甲", "乙", "丙" })
    {
        PersonaDocument card = CreateAuditCard(name);
        card.SelfClaimRules.Add("自称“我”或“" + name + "”");
        cards.Add(card);
    }

    PersonaCorpusAuditReport report = PersonaCorpusAudit.Analyze(cards);
    AssertTrue(FindRule(report, PersonaCorpusAudit.RuleTextReused) != null,
        "把显示名换成 {N} 后三张卡是同一条，必须判为跨卡复用");
    AssertTrue(FindRule(report, PersonaCorpusAudit.RuleSelfReferenceOnly) != null,
        "「自称我或本名」同时是零信息句，必须单独报出来");
}

void CorpusAuditReportsSkeletonPrefix()
{
    string[] names = { "甲", "乙", "丙" };
    string[] tails = { "，谈人多论利害与退路", "，称族人时直呼其名", "，提及部属时语气生硬" };
    List<PersonaDocument> cards = new List<PersonaDocument>();
    for (int index = 0; index < names.Length; index++)
    {
        PersonaDocument card = CreateAuditCard(names[index]);
        card.SelfClaimRules.Add("称可汗为“蒙楚格”" + tails[index]);
        cards.Add(card);
    }

    PersonaCorpusAuditReport report = PersonaCorpusAudit.Analyze(cards);
    PersonaCorpusAuditFinding? finding = FindRule(report, PersonaCorpusAudit.RuleSkeletonPrefix);
    AssertTrue(finding != null, "开头相同、后半句各异的三条必须被判为槽位骨架");
    AssertEqual("称可汗为“蒙楚格”", finding!.Text,
        "报出来的必须是那段共用的开头本身，不是随便挑一条整句——否则看不出到底哪儿重复");
    AssertTrue(FindRule(report, PersonaCorpusAudit.RuleTextReused) == null,
        "整条并不相同，不应同时报整条复用");
}

void CorpusAuditFlagsSelfReferenceOnlyRule()
{
    PersonaDocument card = CreateAuditCard("甲");
    card.SelfClaimRules.Add("自称“我”或“甲”");

    PersonaCorpusAuditReport report = PersonaCorpusAudit.Analyze(new[] { card });
    PersonaCorpusAuditFinding? finding = FindRule(report, PersonaCorpusAudit.RuleSelfReferenceOnly);
    AssertTrue(finding != null, "「自称我或本名」是零信息句，必须报出来");
    AssertEqual(PersonaCorpusAuditSeverity.Error, finding!.Severity, "零信息句应为 error 级");
}

void CorpusAuditKeepsRuleThatExcludesName()
{
    PersonaDocument card = CreateAuditCard("甲");
    card.SelfClaimRules.Add("自称“我”");

    PersonaCorpusAuditReport report = PersonaCorpusAudit.Analyze(new[] { card });
    AssertTrue(FindRule(report, PersonaCorpusAudit.RuleSelfReferenceOnly) == null,
        "「自称我」排除了本名、带信息，不该被判为零信息句");
}

void CorpusAuditFlagsSampleTextAndRepeats()
{
    PersonaDocument card = CreateAuditCard("甲");
    card.RealSelfBehaviors.Add("对外只自称“我”或角色名。");
    card.SelfClaimExamples.Add("同一句示例");
    card.SelfClaimExamples.Add("同一句示例");

    PersonaCorpusAuditReport report = PersonaCorpusAudit.Analyze(new[] { card });
    AssertTrue(FindRule(report, PersonaCorpusAudit.RuleEditorSampleText) != null,
        "抄了编辑器示例文字「角色名」必须报错");
    AssertTrue(FindRule(report, PersonaCorpusAudit.RuleRepeatedLine) != null,
        "同一张卡里两条完全相同必须报错");
}

void CorpusAuditPassesCleanCorpus()
{
    string[] names = { "甲", "乙", "丙" };
    string[] rules = { "自称“我”，说话朴素平实", "称下属为“伙计们”", "提到家业时称“我的地”" };
    string[] behaviors = { "独处时会去打理自己的兵器", "对受伤的旧部会亲自探望", "在熟人面前才说真心话" };
    List<PersonaDocument> cards = new List<PersonaDocument>();
    for (int index = 0; index < names.Length; index++)
    {
        PersonaDocument card = CreateAuditCard(names[index]);
        card.SelfClaimRules.Add(rules[index]);
        card.RealSelfBehaviors.Add(behaviors[index]);
        cards.Add(card);
    }

    PersonaCorpusAuditReport report = PersonaCorpusAudit.Analyze(cards);
    AssertTrue(report.CardCount == 3, "体检应扫描到 3 张卡");
    AssertTrue(report.IsClean, "干净的三张卡不该有任何发现，实际发现 " + report.Findings.Count + " 条");
}

void AssertEqual(string expected, string actual, string message)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(message + "\nExpected: " + expected + "\nActual: " + actual);
    }
}

void AssertTrue(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

T AssertThrows<T>(Action action, string message) where T : Exception
{
    try
    {
        action();
    }
    catch (T error)
    {
        return error;
    }

    throw new InvalidOperationException(message);
}

string CreateTemporaryWorkspace()
{
    string directory = Path.Combine(Path.GetTempPath(), "persona-workbench-workspace-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    return directory;
}

void DeleteTemporaryWorkspace(string directory)
{
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
}





