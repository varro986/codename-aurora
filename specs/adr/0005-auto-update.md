# ADR-005 — Auto-Update Mechanism

**Date:** 2026-07-28
**Status:** Approved
**Deciders:** Founder / Architect

## Context

Aurora is distributed to non-technical back-office operators. Manual update procedures (download, run installer, restart) create friction and increase support burden. A background auto-update mechanism that requires no user intervention is required. The distribution channel must be free and consistent with the open-source model.

## Decision

Use **Velopack** for packaging and auto-update, distributing releases via **GitHub Releases**.

## Rationale

- Velopack is the community-maintained successor to Squirrel.Windows and supports .NET 10 WPF out of the box.
- GitHub Releases provides a zero-cost, public hosting channel; assets are served via GitHub's CDN.
- Velopack handles the full lifecycle: installer creation, delta packages (reduced download size), background update check, and in-process restart.
- Alternatives considered:
  - **NetSparkle**: Solid library, but more setup ceremony; Velopack integrates more cleanly with WinExe output type.
  - **MSIX / Windows Store**: High friction in enterprise environments with GPO restrictions on Store installs.
  - **Manual download prompt only**: Acceptable last resort but not the primary path — operators should not need to think about updates.

## Consequences

- `Aurora.App` calls `VelopackApp.Build().Run()` as the **very first statement** in `Program.Main()` — this is a hard Velopack requirement for update hooks to function correctly (see ADR-007).
- `Aurora.Admin` owns the update-check lifecycle: it checks for a new release on startup (non-blocking) and notifies the user via the tray icon when an update is ready.
- Release artifacts (installer, delta packages) are produced exclusively by the CI pipeline (GitHub Actions) and published to GitHub Releases — no manual releases.
- The update channel (stable / pre-release) is user-configurable via `IAppSettings` (see ADR-006).

## Compliance

- All production releases must go through CI — direct `vpk` invocations on developer machines are for local testing only.
- `Aurora.App` calls `VelopackApp.Build().Run()` exactly once at process startup (required Velopack bootstrapper hook — this is not an update API call, see ADR-007); `Aurora.Admin` is the sole module that calls Velopack update-check and install APIs at runtime.
