# Plan: PersonaWorkbench K1A Authoring-v2 Preview

Status: `IMPLEMENTED / E2_VERIFIED`

This is an isolated PersonaWorkbench batch. It must not change the AWAKE runtime, game directory, `dist`, Worldbook, the current `awake-20260829-marcus-embedded-002` candidate, or in-game live Persona-card behavior. Source changes require an independent read-only review returning `VERDICT: APPROVED` and the user's authorization for this batch.

## 1. Goal

Map the current Workbench form snapshot from `persona-workbench.character.v1` to the single `awake.persona.authoring.v2` authoring layer, and expose in Workbench:

1. a user-triggered read-only JSON preview;
2. a user-triggered download of `awake.persona.authoring.v2` JSON;
3. explicit migration warnings, preserved legacy data, and failure diagnostics;
4. deterministic canonical output consumable by the later offline AWAKE adapter.

## 2. Closed loop

`Generate authoring-v2` button -> capture current form and separate expansion text -> Core adapter reads authoritative contract assets and maps -> strict validation and canonical JSON -> browser preview/download.

Failure returns diagnostics only. It must not replace Persona fields, existing DSL, saved drafts, or approval state.

## 3. Confirmed facts

- Workbench's only authoring source is `persona-workbench.character.v1`.
- AWAKE's only authoring contract is `awake.persona.authoring.v2`; its schema, crosswalk, and `tag_registry.json` are authoritative under the AWAKE contract directory.
- The joint contract requires unknown root/nested fields to fail closed, migrated observations to use `legacy_v1` + `needs_review`, and `preserve_only` values to remain migration data rather than runtime tags.
- Workbench v1 has no stable game-selection sidecar. This batch must not fabricate `selection.v1`, `export.v1`, `definition.v1`, or runtime approval.
- The current separate expansion text exists only in the browser until explicitly supplied; it is not persisted in the v1 document.
- The current browser state can contain both a manually authored `core` and a separate expansion buffer; the adapter request must carry them as separate named values plus an explicit expansion-origin marker.
- The existing joint contract names `persona-instruction.v1` and `persona-compiler.v1`, but no standalone instruction/compiler files exist in the authoritative contract directory. K1A treats these as locked identifiers, not as user-selectable or silently discovered assets.

## 4. Locked decisions

### 4.1 Output boundary

- Output only authoring-v2 JSON. Do not produce runtime export, runtime definition, selection sidecar, or any AWAKE/Marcus/Bannerlord runtime call.
- Preview and download are explicit user actions. No automatic save, approval, or status mutation.

### 4.2 Field provenance

- `source.confirmedText` comes from `SourceDescription`, normalized to NFC and hashed as UTF-8 SHA-256.
- `source.expandedText` comes only from the current separate expansion text; if absent, emit an empty string. Never substitute `Core`, `Summary`, or another authored field.
- The Web request carries `core`, `confirmedText`, `expandedText`, and `expandedTextOrigin` independently. `expandedTextOrigin` is `none`, `provider`, or `user_edited`; the origin is diagnostic input metadata and is not written into authoring-v2 because the locked schema has no such field.
- The ten `authored` fields copy their matching v1 fields without AI rewriting. Reaction/commitment prose is copied only; no formal rule is invented.
- `reviewStatus` copies v1 `Status` exactly per the locked crosswalk. Even when the source is `approved`, migrated observations remain `needs_review`; the preview is not AWAKE approval.
- `facts` contains only a sourced `identityNote` when `IdentityFacts` is non-empty. Without a selection sidecar, do not infer role, character identity, or game identity from display name, filename, or v1 `Id`.

### 4.3 Mapping and preservation

- `Tags`, `FacetStrengths`, and non-null values from all 25 axes resolve through the authoritative crosswalk. Mapped values become observations with `legacy_v1`, `needs_review`, and `enabled=true`.
- `preserve_only` values are copied verbatim into `migration.preservedLegacyData` and produce stable warnings. They are never silently dropped or compiled as runtime tags.
- v1 has no persisted provider flags/evidence/rules. This batch creates none and never writes AI guesses into migration output.
- Duplicate observation IDs, unknown tags, unknown crosswalk targets, invalid axes/facets, invalid IDs, and mismatched contract assets fail closed.

### 4.4 Contract assets and canonicalization

- Do not hand-maintain a second schema, registry, or mapping table. Core references/embeds the authoritative AWAKE schema, crosswalk, and registry bytes through project assets and verifies the pinned version and registry SHA-256.
- Output follows `persona-canonical-json.v1`: UTF-8 without BOM, NFC, LF with final LF, duplicate-key rejection, stable property ordering, and declared set ordering. Output hash is response metadata only and is not inserted into the authoring object.
- Pin `registryVersion=awake.persona.tags.v1`, `instructionVersion=persona-instruction.v1`, and `compilerVersion=persona-compiler.v1` to the current joint contract. K1A uses `authoringRevision=1` for each stateless preview lineage; a later source revision or contract migration must supply its own explicit revision rather than pretending the preview has a persisted revision history.
- The schema, crosswalk, registry, and canonicalization contract are linked as embedded resources from the AWAKE authoritative files at build time; the package contains one copy of their exact bytes. The adapter validates their IDs and the registry SHA-256 before mapping. Missing or drifted embedded resources return `persona.migration_required`. The two version identifiers above are exact contract metadata, not a second instruction/compiler asset.

## 5. Planned write set

### Core

- Add a pure v1-to-authoring-v2 adapter, an explicit expansion-origin request DTO, authoritative contract-asset loading, and authoring-v2 semantic checks (unique IDs, target registry closure, source hash, and mapped/preserved partition).
- Reuse existing identity, tag, canonicalization, and JSON utilities. Preserve existing v1 codec, DSL generator, Provider parser, and storage behavior.

### Web

- Add a read-only POST route such as `/api/preview/awake-authoring` returning authoring JSON, canonical bytes/hash, warnings, and diagnostics.
- Accept only a normalized current form snapshot plus optional separate expansion text and origin marker. Do not accept paths, raw file bytes, write files, or call a Provider. This route does not claim to validate duplicate keys or original UTF-8 bytes; persisted v1 files continue to use the existing strict document codec.
- Add `Generate AWAKE authoring-v2 preview` and `Download JSON` actions in the existing free-experiment area, with the existing async stale-result protection.

### Tests and docs

- Core: complete field mapping, mapped/preserved tags/facets/axes, expansion isolation, invalid input, duplicate values, deterministic output, and canonical bytes.
- Web: route/response, failure non-overwrite, preview/download, and regression coverage for existing DSL, save, approval, and Provider paths.
- Update the Workbench usage guide to state that authoring-v2 is a migration preview, not AWAKE approval or a game-loadable file.

## 6. Explicit non-goals

- No changes to AWAKE `src`, `ModuleData`, `dist`, game files, RuntimeBundle, Storage, saves, callers, or the current `002` candidate.
- No implementation of `awake.persona.selection.v1`, `awake.persona.export.v1`, or `awake.persona.definition.v1` promotion.
- No cloud Provider or Ollama generation in the adapter. Ollama is used only after implementation for the existing diagnostic gate.
- No unrelated Provider prompt, DSL budget, historical log, or code-debt refactor.

## 7. Acceptance matrix

| ID | Case | Expected result | Evidence |
|---|---|---|---|
| K1A-01 | Valid sparse v1 form | Non-empty JSON satisfies authoring-v2 shape | Core focused test + contract check |
| K1A-02 | Mixed tags/facets/axes | Mapped values become observations; preserved values stay in migration; output is stable | Golden fixture twice + byte/hash comparison |
| K1A-03 | Separate expansion present | Only `source.expandedText` contains it; `authored.core` is unchanged | Core assertion + Web request capture |
| K1A-04 | Separate expansion absent | `source.expandedText` is empty; no inference from Core | Core assertion |
| K1A-05 | v1 approved | `reviewStatus=approved`, observations remain `needs_review`, UI says not AWAKE approved | Core + Web assertion |
| K1A-06 | Invalid ID, unknown tag, out-of-range axis, registry mismatch | Stable failure; form/DSL/files remain unchanged | Negative tests |
| K1A-07 | Duplicate tag/observation | Stable rejection; no partial authoring output | Negative test |
| K1A-08 | Download | Download bytes exactly equal preview canonical bytes | Browser/static test |
| K1A-09 | Existing functions | DSL, save, approval, and Provider behavior remain unchanged | Existing Core/Web tests |
| K1A-10 | Missing or drifted contract asset | `persona.migration_required`; no fallback mapping | Asset mutation test |
| K1A-11 | Pre-filled Core plus provider/user-edited expansion | Independent request fields remain independent; no source/core substitution | Core + Web request capture |
| K1A-12 | Crosswalk inventory mismatch | Normative `rows` must have exactly one mapping for every consumed v1 scalar/prose field; descriptive inventory mismatch is reported and never used as an alternate mapping table | Contract-closure test |
| K1A-13 | Raw duplicate key or malformed DTO boundary | Route documents normalized DTO scope; persisted-file codec remains the raw-byte fail-closed boundary | Contract/documentation test |
| K1A-14 | Duplicate fact/observation IDs or target references | Semantic validator rejects before preview/download | Core negative test |

## 8. Evidence and gate

- Minimum evidence: E0 plan + independent review; implementation target E1 compile/contract parse and E2 focused Core/Web tests, golden fixture, and local smoke.
- Maximum claim for this batch is E2. It does not claim AWAKE runtime, game entry, save/reload, or live Persona-card evidence.
- Review round 1 returned `REVISE` for expansion provenance, contract/revision closure, crosswalk inventory ambiguity, semantic validation, and raw-byte boundary wording. Those corrections are incorporated above. Round 2 independent read-only review returned `VERDICT: APPROVED`; the user's prior autonomous recommended-path instruction supplies sign-off for this isolated batch. Implementation is allowed within the declared Workbench-only write set.

## 9. Implementation evidence — 2026-08-30

- Core executable regression harness: `PASS`.
- Web executable regression harness: `PASS`.
- Release Web build: `0 warnings / 0 errors`.
- Source HTTP smoke for `/api/preview/awake-authoring`: valid request returned HTTP 200, `schemaVersion=awake.persona.authoring.v2`, canonical UTF-8 bytes `1667`, and matching SHA-256 `EC4E0EBCD573C23D64B59FB4337F4BEBD972452ACE99C38A2559002FDDA0F209`.
- Source HTTP negative smoke: invalid stable ID returned HTTP 400 with `persona.authoring_id_invalid`, empty replacement JSON/bytes, and no destructive overwrite payload.
- Contract assets remained hash-pinned: schema `5B5E704329C8383868D66CD41DFF9E693B67E5A8AED8ACBA0EF5511410340BF9`, crosswalk `DF67ADD21C4A0241706B1BA32C7FDC9F9D36878ED415172BE928E0268963BE49`, canonical contract `B9BE5523DAF6723E45C803157250FD5D96F2847C58C494CC0F5CD405F27E317C`, registry `59CB54D92F32B5CA9BDD5D739367FE7B9964A41EF54978FED9AEA22B950634E6`.
- Worker-low diagnostic with local Ollama `gpt-oss:20b` passed all four serial stages. Model digest: `17052f91a42e97930aa6e28a6c6c06a983e6a58dbb00434885a0cf5313e376f7`. Report: `tools\worker-low-report-k1a-20260830.json`.
- The prescribed fixed r48 route gate stopped before startup because that historical package lacks `BUILD-ID.txt` and `BUILD-SOURCE-MANIFEST.sha256.txt`; this is `PACKAGE_MISSING`, not evidence of a Provider or authoring failure. Its report is retained at `tools\pretest-report-20260830-run2.json`.
- A correctly attributed current package `artifacts\PersonaWorkbench-FreePreview-k1a-20260830` was then tested with the same fixed fixtures, model digest, serial order, and no-retry rule. All four real Workbench stages returned HTTP 200 with accepted non-empty outputs and usage data; route report: `tools\pretest-report-k1a-20260830-run2.json`; paired evidence: `tools\ai-link-evidence-k1a-20260830.json`.
- Current package evidence records BuildId `PWB-20260830-134653Z-32981D08377D`, package manifest hash `DED152FB4BD50386C722B67DDC9889C0B0A7ADE7255EFD11057E53FC3CE4520E`, and complete cleanup with port `51337` free. The effective evidence ceiling remains E2; this still does not claim AWAKE runtime, game entry, save/reload, or live Persona-card behavior.
