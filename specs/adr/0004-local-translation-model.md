# ADR-004 — Local Translation Model

**Date:** 2026-07-28
**Status:** Approved
**Deciders:** Founder / Architect

## Context

The third tier of the translation cascade (ADR-003) requires a local, fully offline model capable of translating residual free text. The choice of runtime and model family directly affects distribution size, translation quality, inference latency, and maintenance burden.

## Decision

Use **ONNX Runtime** (`Microsoft.ML.OnnxRuntime`) with **Helsinki-NLP MarianMT** models sourced from Hugging Face.

## Rationale

- MarianMT models are available in ONNX format from Hugging Face for all major language pairs (e.g., `Helsinki-NLP/opus-mt-it-en`).
- `Microsoft.ML.OnnxRuntime` is a mature NuGet package with zero native-compilation requirements on Windows.
- Model size is ~300 MB per language pair — acceptable for a desktop tool targeting a known, finite set of pairs.
- Inference is fast enough for the back-office use case (individual sentences or short phrases).
- Alternatives considered:
  - **LLamaSharp (GGUF local LLM)**: More flexible and multilingual, but 1–4 GB model size is disproportionate for structured short enterprise text.
  - **Cloud API (DeepL / Google Translate)**: Breaks the hard offline requirement.
  - **Defer to a future ADR**: Rejected — the cascade contract (ADR-003) needs a concrete third tier to be actionable.

## Consequences

- Models are **not bundled** in the installer to keep the download size small. Aurora downloads the required MarianMT ONNX model on first use (or on explicit user request via Admin) and caches it under `%APPDATA%\Aurora\models\`.
- `Aurora.Translation` loads the ONNX `InferenceSession` lazily on first model-tier invocation (warm-up on first translation).
- Supporting a new language pair requires downloading the corresponding MarianMT ONNX model — no code change needed.
- `ITranslationEngine.TranslateAsync` signature is unchanged; model complexity is fully hidden behind the interface.
- Model download orchestration is the responsibility of `Aurora.Admin` (details TBD in a dedicated user story).
- The model cache path is configurable via `IAppSettings` (see ADR-006).

## Compliance

- ONNX `InferenceSession` instantiation lives exclusively in `Aurora.Translation`.
- No other module may reference `Microsoft.ML.OnnxRuntime` directly.
