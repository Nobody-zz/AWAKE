# P3C Independent Storage/RAG Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. The plan is locked after autonomous user authorization and independent read-only challenge.

**Goal:** Build a net8.0, background-safe SQLite/FTS5 implementation of the existing `MarcusAwakeFramework.Api` storage and keyword-RAG contracts without modifying shared framework, transport, runtime, AWAKE, docs, dist, or game files.

**Architecture:** `SqliteStorageAndRagBackend` owns one SQLite database path and serializes database work behind an async gate; synchronous SQLite calls run only on a thread-pool worker. Campaign and session KV handles retain their namespace binding and revalidate caller/session scope on every operation. RAG collections are isolated by caller/campaign/timeline/collection, use an explicit corpus fingerprint, and search through a SQLite FTS5 table with exact access-scope filtering and deterministic tie-breaking. `Hybrid` and `Semantic` are explicit unsupported modes because no external Marcus implementation is assumed and local embeddings/reranking are forbidden by AWAKE rules.

**Tech Stack:** C# / `net8.0`, `Microsoft.Data.Sqlite` with the bundled `SQLitePCLRaw`/`e_sqlite3` provider, read-only reference to the existing `MarcusAwakeFramework.dll`, and an independent focused console test project with no test-framework package dependency.

---

## Locked Decision Record

- `plan_status=locked`
- `user_signoff=autonomous_authorization`
- `review_status=pending_independent_read_only_challenge`
- Only files under this directory may be created or modified.
- Shared Core remains a read-only binary reference; no source copy or contract shim is permitted.
- Campaign KV identity is `owner + campaignGuid + timelineId + namespace`; session KV adds `sessionId`.
- RAG collection identity is `owner + campaignGuid + timelineId + collectionId`; a new fingerprint conflicts with an existing collection and is rejected atomically.
- Empty `AccessScopes` means all scopes in the already isolated collection; non-empty scopes are exact OR matches.
- Search request owner/campaign/timeline/session fields, when supplied, must agree with `RequestContext`; mismatches fail closed.
- Only `RetrievalMode.Keyword` is implemented. Other modes return `Unsupported` instead of silently claiming semantic behavior.
- Missing SQLite restore/publish assets trigger a minimal in-memory fallback only if needed; the fallback is not persistence- or FTS5-equivalent and will be reported as such.

## Acceptance Matrix

| Case | Observable result | Minimum evidence |
|---|---|---|
| Open campaign/session namespace | Correct handle is returned and data survives a new handle; campaign data survives session-id changes within the same campaign/timeline | Focused test |
| KV set/get/delete | Values round-trip, missing get returns `null`, delete is idempotent, 512 KiB boundary is enforced | Focused test |
| Scope isolation | Wrong caller/session/namespace handle cannot read or mutate another scope | Focused test |
| Document upsert | Same collection/fingerprint updates an existing document without duplicate search hits | Focused test |
| FTS5 keyword search | Terms are matched through FTS5, empty query is an empty success, text byte cap is respected | Focused test plus SQLite schema assertion |
| Access filtering | Only exact requested access scopes are returned; empty scope list sees the isolated collection | Focused test |
| Fingerprint conflict | Different ingest fingerprint is rejected atomically; stale search fingerprint returns conflict | Focused test |
| Deterministic ordering | Equal relevance results are ordered by ordinal document ID and repeat identically | Focused test |
| Mode boundary | `Hybrid` and `Semantic` return `rag.retrieval_mode_unsupported` | Focused test |
| Cancellation/timeout | Missing token, pre-cancelled token, expired deadline, and queued deadline produce typed errors without partial writes | Focused test |
| Copy deployment | Publish output contains SQLite managed/native assets and no Core DLL copy | `dotnet publish` asset audit |
| Scope boundary | No changed path falls outside `MarcusAwakeStorage` | PowerShell path audit |

## Files and Responsibilities

- Create `MarcusAwakeStorage.csproj`: net8.0 library, pinned SQLite package reference, read-only Core DLL reference, deterministic output under this directory.
- Create `src/SqliteStorageAndRagBackend.cs`: public service implementation, async worker boundary, schema initialization, KV operations, FTS5 ingest/search, typed error mapping, disposal.
- Create `src/SqliteStorageOptions.cs`: bounded options for value/result byte limits and SQLite busy timeout; no game or real-path defaults.
- Create `tests/MarcusAwakeStorage.Tests.csproj`: net8.0 focused console test project referencing only the local component and read-only Core DLL.
- Create `tests/Program.cs`: deterministic assertions for the acceptance matrix, temporary database cleanup within the target directory, and pass/fail summary.
- Create `README.md`: embedding contract, deployment requirements, unsupported modes, fallback gap, and Runtime Service integration guidance.
- Create `PLAN-MARCUS-AWAKE-P3C-STORAGE-RAG-REVIEW-LOG.md`: independent review findings, final verdict, and evidence status.

## Implementation Sequence

1. Run the independent read-only challenge against this plan and the existing API only; record severity, correction, and terminal verdict.
2. Verify the exact `Microsoft.Data.Sqlite` package restore and `e_sqlite3` publish assets without changing any external project.
3. Write the failing focused tests for namespace identity, cancellation/deadline, fingerprint conflict, FTS5 filtering, upsert, and deterministic ordering.
4. Implement the smallest SQLite backend until the focused test runner passes.
5. Publish win-x64 from the target project and audit managed/native SQLite assets, Core reference copy behavior, and changed-path containment.
6. Report compile, test, deployment, and unverified game/runtime evidence separately; do not claim AWAKE or game integration.

## Explicit Non-goals

- No edits to `MarcusAwakeFramework`, `MarcusAwakeTransport`, `RuntimeService`, `AWAKE.csproj`, AWAKE source, docs, dist, installed modules, or game files.
- No AWAKE registration, Runtime Service wiring, Named Pipe/HTTP transport, permission broker, MCM, embeddings, semantic reranking, or local model inference.
- No migration of existing databases, save files, package manifests, version numbers, or release artifacts.
- No game launch or in-game verification.
