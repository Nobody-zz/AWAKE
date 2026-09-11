# Worldbook Studio AI Authoring Unification Plan

- Batch: `WORLDBOOK-STUDIO-AI-AUTHORING-20260902`
- Parent: `WORLDBOOK-STUDIO-PUBLIC-CONTRACT-20260902`
- Risk: `high-risk`
- Objective: eliminate Draft/Batch dual authority and reduce repeated user confirmation while preserving evidence, review-only semantics, and recovery boundaries.

## Review scope

- Compare Draft and Batch domain models, provider request construction, result normalization, evidence binding, candidate/document creation, and UI state transitions.
- Identify one authoritative lifecycle for source snapshot -> generation packet -> candidate set -> review decision -> author document.
- Treat existing `facts -> metadata -> expressions` as a compatibility input surface only if it does not force duplicate AI calls or duplicate confirmation.
- Separate this batch from the next UX batch: actual merge/split controls, risk-grouped review, and user-facing interaction polish are follow-up work unless required to make the unified lifecycle usable.

## Acceptance

- A source is read once into a normalized snapshot and referenced by hash thereafter.
- A generation run can yield zero, one, or many candidates; no source-unit-to-entry one-to-one assumption remains in the authoritative path.
- Facts, metadata, and expressions are layers of one candidate/document lifecycle, not independent approval state machines.
- Every candidate carries source/evidence provenance and a stable fingerprint; stale evidence or changed targets invalidate the result visibly.
- Draft and Batch use the same canonical request/result/evidence model or an explicit adapter with one write authority.
- Low-risk generated fields do not require repeated confirmation; high-risk mappings and unresolved evidence remain review gates.
- Disabled provider, cancellation, retry, unknown result, and rejected candidate paths preserve existing documents and do not silently publish.

## Non-goals

- No real Provider access or API key.
- No Bannerlord launch or game-directory sync.
- No automatic canon publication.
- No final UI merge/split interaction redesign; that is batch 6 after this structure is settled.

## Required evidence

- Independent read-only review before implementation.
- Focused model/provider/evidence tests, then full Worldbook Studio regression.
- Draft and Batch HTTP smoke must show identical lifecycle semantics where they overlap.
- Record explicit unresolved UX findings for batch 6 instead of hiding them in this batch.