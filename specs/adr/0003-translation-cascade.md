# ADR-003 — Translation Cascade Strategy

**Date:** 2026-07-28
**Status:** Approved
**Deciders:** Founder / Architect

## Context

Aurora targets back-office operators who work with domain-specific terminology: AS/400 field codes, SAP transaction labels, company acronyms, and abbreviations. Generic translation engines produce wrong or misleading output for these terms. The application must also support fully offline operation.

## Decision

Implement a three-tier cascade inside `Aurora.Translation` behind the `ITranslationEngine` interface:

1. **Private dictionary** (`private.json`) — operator-specific or site-specific exact key→value lookups. Highest priority; not version-controlled.
2. **Generic dictionary** (`generic.json`) — shared enterprise-level terms, shippable with the application.
3. **Local offline model** — ONNX Runtime + Helsinki-NLP MarianMT (see ADR-004) — handles all residual free text.

If a term matches in tier 1 or tier 2, the cascade stops immediately. The local model is invoked only when neither dictionary has an entry.

## Rationale

- Dictionary lookups are deterministic and instantaneous — critical for operator productivity.
- Hot-reload via `FileSystemWatcher` lets operators update dictionaries without restarting Aurora.
- Separating private from generic dictionaries allows teams to share `generic.json` in version control while keeping site-specific entries in `private.json` (out of VCS).
- Alternatives considered:
  - **Single merged dictionary**: Cannot separate shared vs. private concerns.
  - **Cloud translation API only**: Breaks offline requirement.
  - **Model-only, no dictionaries**: Unpredictable output for domain-specific codes.

## Consequences

- `Aurora.Translation` owns all cascade logic. Callers receive only the translated string — the tier used is an implementation detail.
- `ITranslationEngine.TranslateAsync` is the single entry point; its signature is unchanged by this decision.
- Both JSON files are bundled at first run as empty objects (`{}`).
- Dictionary schema: `{ "SOURCE_TERM": "translated term", ... }` — case-insensitive lookup.
- `FileSystemWatcher` monitors both JSON files; on change, the in-memory dictionary is atomically replaced via `Interlocked.Exchange` on an immutable reference (lock-free reader path).
- Dictionary paths are configurable via `IAppSettings` (see ADR-006).

## Compliance

- No module other than `Aurora.Translation` may read dictionary files directly.
- `Aurora.Translation` reads dictionary paths from `IAppSettings` (ADR-006); `Aurora.Admin` manages dictionary files at those paths — direct dictionary file access is `Aurora.Admin`'s exclusive responsibility.
