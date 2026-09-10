# Review Log: PersonaWorkbench K1A Authoring-v2 Preview

## Batch boundary

- Plan: `PLAN-K1A-AUTHORING-V2-PREVIEW-20260830.md`
- Scope: Workbench-only v1 form snapshot -> authoring-v2 read-only preview/download.
- Excluded: AWAKE runtime, selection/export/definition promotion, game files, `dist`, Storage, Bannerlord, current `002` candidate.
- Review mode: independent read-only reviewer; no source or runtime mutation during review.

## Round 1 — independent read-only review

Result: `VERDICT: REVISE`

Accepted material findings:

1. **P0 — expansion provenance was not closed.** The browser can hold a manually authored Core and a separate expansion buffer, while the existing “use expansion” action can copy expansion into Core. A v1 document alone cannot prove which text was the expansion source.
2. **P0 — contract assets and revision were not closed.** The Workbench project had no authoring-v2 adapter or embedded authoritative contract assets, and the plan fixed `authoringRevision=2` without a stateless preview lineage rule.
3. **P1 — crosswalk inventory ambiguity.** `sourceVocabulary.rootFields` does not list every scalar/prose source field represented by crosswalk rows; the plan needed to distinguish normative rows from descriptive inventory.
4. **P1 — schema semantic checks were missing.** JSON Schema alone does not enforce unique fact/observation/rule IDs, source hash semantics, or mapping partition rules.
5. **P1 — raw-byte boundary was overstated.** A Web DTO route cannot observe original UTF-8 bytes or duplicate JSON keys; that guarantee belongs to the existing persisted-document codec, not the preview route.

## Controller revision

The plan was revised without changing the requested product boundary:

- The adapter request now carries `core`, confirmed source, separate expansion text, and `expandedTextOrigin` independently. The origin is diagnostic metadata only; it is not invented in authoring-v2.
- K1A uses `authoringRevision=1` for a stateless preview lineage. It does not pretend to maintain a persisted revision history; later source/export batches must provide explicit revision inputs.
- Authoring schema, crosswalk, registry, and canonicalization contract bytes are embedded from the AWAKE authoritative files at build time and validated by ID/hash. `persona-instruction.v1` and `persona-compiler.v1` remain exact locked metadata identifiers because no standalone instruction/compiler files exist.
- Normative mapping comes from crosswalk `rows`; the incomplete `sourceVocabulary` inventory is diagnostic metadata and cannot become a second mapping table. Every consumed scalar/prose field must have exactly one row.
- Adapter semantic validation covers unique IDs, registry closure, source hash, and emitted-vs-preserved partition. The route explicitly does not claim raw-byte duplicate-key validation.

## Round 2 — independent read-only review

Result: `VERDICT: APPROVED`

Accepted conclusion:

- The revised plan closes expansion/Core/source provenance, stateless revision semantics, embedded authoritative contract assets, normative crosswalk rows, the Web DTO raw-byte boundary, and semantic acceptance coverage.
- No blocking issue remains for the isolated Workbench-only implementation. The implementation must remain within the plan's write set and must not claim AWAKE runtime or game evidence.

Gate result: `APPROVED / IMPLEMENTATION_ALLOWED`.

## Implementation and verification record — 2026-08-30

- Implemented the reviewed Workbench-only write set: Core authoring-v2 adapter and embedded contract assets; Web preview route; separate expansion provenance; canonical JSON download; focused Core/Web regression coverage.
- Core tests, Web tests, and Release Web build passed with zero build warnings and zero build errors.
- Source route smoke passed for both a valid authoring-v2 request and a destructive-failure case. The valid response returned canonical bytes whose SHA-256 matched the response hash; the invalid response returned diagnostics without replacement JSON or bytes.
- Worker-low serial diagnostic passed for short/long expansion and short/long structured conversion using the required local Ollama model and digest.
- The prescribed fixed-r48 packaged route gate stopped before startup because the historical r48 directory does not contain the runner-required `BUILD-ID.txt` and `BUILD-SOURCE-MANIFEST.sha256.txt`. This remains a package-evidence gap and is not converted into a source implementation pass.
- A fresh package carrying BuildId `PWB-20260830-134653Z-32981D08377D` and complete source/package manifests passed the four real Workbench routes with the fixed short/long fixtures. The paired evidence is recorded at `tools\ai-link-evidence-k1a-20260830.json`; the route runner reports complete cleanup and a free Workbench port.
- The route acceptance verdict means transport, parsing, draft presence, and non-empty output passed. It is not a claim that the local model's prose is semantically perfect; content-quality review remains separate from this transport gate.
