# Plan Review Log: Worldbook Studio A3.1 作者档案模板构造 seam
Act 1 (grill) complete — plan locked with autonomous execution authorization. MAX_ROUNDS=5.

## Review status

- Reviewer: `codex-cli 0.147.0`, configured model `gpt-5.6-luna` (fallback metadata warning observed; review completed).
- Round 1: `VERDICT: REVISE`.

### Round 1 critique

- Canonical hash does not prove `JsonObject` insertion order because canonicalization sorts object keys; add full ordered-key assertions at every nested object and distinguish hash evidence from order evidence.
- Pin exact fixture inputs, registry versions/hashes, path extensions and title normalization cases; do not leave “representative input” vague.
- Define YAML/JSON equivalence as complete re-read object, canonical hash, schema diagnostics and diagnostic order, not text equality or selected fields.
- Add negative coverage for `WB-DOC-001..005`, save/read/schema failure boundaries, registry-unavailable-before-factory behavior, no-write-on-failure, path/extension/overwrite semantics and caller-side normalization.
- Mechanically bound factory purity: `internal`, value/snapshot-only inputs, no Workspace/File/Directory dependencies.
- State exact harness count transition and keep existing single entrypoint; use existing read-only A1 smoke without changing CLI/Web production code.
- Golden must be a complete independently authored behavioral oracle, not only selected fields or an implementation-generated snapshot.

### Revision applied

The plan now pins the registry byte hashes and exact inputs, distinguishes canonical hash from insertion-order evidence, defines three-layer ordering checks, adds four focused cases (`83/83` to `87/87`), freezes caller normalization and registry failure precedence, requires negative/path/overwrite coverage, enforces an internal I/O-free factory seam, and keeps A1/A2/CLI/Web production scope unchanged.

### Round 2 critique

- Schema/save/read fault coverage was still impossible through `CreateDocument`; remove the implied fault-injection requirement or name a real existing hook.
- JSON text property order and YAML mapping order needed to be promoted from a risk note into named-case and acceptance evidence.
- Literal registry hashes needed discoverable source paths and a runtime byte-hash assertion.
- Existing-file semantics needed an explicit expected result: successful atomic overwrite and unchanged content on pre-write path rejection.
- Path-policy coverage needed to reuse exact existing `WB-PATH-001/003/004` and `WB-SAVE-001` behavior without changing policy code.
- Protect the existing 83 named tests with a pre/post manifest hash, not only a count.

### Revision applied

The plan now removes unreachable fault injection, promotes all three ordering observations into acceptance, names both registry fixture paths and runtime hash checks, defines atomic overwrite and pre-write preservation semantics, lists exact existing path-policy codes, and fixes the pre-change named-case manifest hash `989ce70865575fbe584975118ed4c9165e078c81a39b3dbd8dc70ffb25a86226` with an `83`-preserved-plus-4-new postcondition.

### Round 3 critique

- The plan was materially sound, but the named-case manifest hash was not reproducible because the text sequence and trailing-newline rule were ambiguous; the stated hash did not match the reviewer’s extraction.
- JSON/YAML order assertions needed an independent expected sequence/text oracle in the golden, not only a comparison between outputs.

### Revision applied

The manifest rule is now exact: all source lines matching `^Run\("`, source order, UTF-8, one LF between lines, no leading/trailing LF; the verified baseline is `d8035a0fd6e13027589e6a0e84a39cf8474967d77ef11a6bef65d4ec1595729b`. The golden now owns independent JSON property-order and YAML mapping-order expectations.

### Round 4 final review

- Reviewer confirmed the manifest extraction rule and baseline hash match the current source.
- Reviewer confirmed the independent JSON/YAML order oracle, reachable save→read→schema coverage, registry/path/overwrite boundaries, exact four-case scope and one internal pure factory budget.
- `VERDICT: APPROVED`.

- Implementation may begin within the locked A3.1 scope.
- No A3.1 implementation files have been changed yet.
