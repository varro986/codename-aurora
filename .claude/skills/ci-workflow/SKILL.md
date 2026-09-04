---
name: ci-workflow
description: dotnet build/test/format commands. Load before running any build, test, or pushing code.
---

# CI Workflow Skill

## Commands

```powershell
# Build
dotnet build [SLN] --configuration Release

# Test — always pass --logger trx (verification-guard reads .trx timestamps)
dotnet test [SLN] --configuration Release --results-directory TestResults/ --logger trx

# Single test
dotnet test [SLN] --filter "FullyQualifiedName~<TestName>"

# Architecture tests only
dotnet test [SLN] --filter "Category=Architecture"

# Format check (CI gate)
dotnet csharpier --check .

# Format apply
dotnet csharpier .

# Restore
dotnet restore [SLN]
```

## Solution file

<!-- [PROJECT-SPECIFIC] Replace [SLN] with the actual solution filename once created. -->
`[TBD].slnx`

## CI/CD parity

<!-- [PROJECT-SPECIFIC] Fill in once .github/workflows/ci.yml is defined. -->

| Local command | CI job |
|---|---|
| `dotnet build ... --configuration Release` | build |
| `dotnet test ... --logger trx` | test |
| `dotnet csharpier --check .` | lint |

## Non-negotiables

- `dotnet build` does NOT compile test projects — never claim "verified" from a green build alone
- Always pass `--logger trx` to `dotnet test` so the verification-guard hook can timestamp the run
- Never run concurrent `dotnet` invocations against the same solution — pass multiple targets to one call
- Subagents do not run builds or pushes; the orchestrator runs the one gate

## Pre-push gate

The `verification-guard.sh` hook blocks `git push` automatically when `.cs` files in the outgoing commits are newer than the last `.trx` results.

To bypass (docs-only changes or config-only changes):

```bash
SKIP_VERIFY_CHECK=1 git push ...
```
