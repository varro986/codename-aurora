# Claude Code

@../AGENTS.md is the structural law of this project: architecture, feature
completeness, security rules, fragile zones, routing table. Read it as binding;
this file adds only Claude-specific behaviour.

Human-readable guide to this entire system (hooks, skills, commands, settings):
→ `.claude/GUIDE.md` — read it to understand *why* every rule exists.
Alignment rule: whenever you change a hook, skill, command, or settings.json,
update GUIDE.md in the same commit. coupling-guard enforces this.

## Skills first

Before acting, check the routing table in AGENTS.md and load the relevant skill.

| Trigger                                          | Skill                    |
|--------------------------------------------------|--------------------------|
| Scoping a new feature (uncertain blast radius)  | `investigate`                      |
| Writing or modifying any `.cs` file             | `architecture` then `code-quality` |
| Reading an Issue, writing tests or `.feature`   | `feature-spec`                     |
| Running builds, tests, push, or PR              | `ci-workflow`                      |
| Before any push or PR                           | `pre-release-validation`           |

Loading a skill is not optional when the routing table matches.

## Session non-negotiables

### Destructive operations
NEVER run destructive commands without explicit permission: dropping databases,
deleting migration files, `git reset --hard`, `git push --force`. Always ask first.

### Security — never commit secrets
Before staging any file, check it does not contain credentials, connection strings,
API keys, certificates, or `.env` content. The smell-guard catches some patterns
at write time; a manual check before `git add` is still required. See the
Security section in AGENTS.md for the full list of forbidden file patterns.

### Only the orchestrator compiles and pushes
If you are a subagent: write code, stop before any build or push, report to the
orchestrator. `dotnet build`, `dotnet test`, `git commit`, `git push` are
orchestrator-only operations.

### A green build is not verification
`dotnet build` does not compile test projects. State exactly which command ran.
Say what was checked and what was not.

### Comments — WHY only, English
XML doc (`/// <summary>`) required above all non-trivial public declarations.
Inline `//` allowed only for non-obvious invariants or workarounds — one line.
Zero `//` for anything the code already makes obvious. Full rule: AGENTS.md.

### Communication: declarative, no filler, red team by default

**Output format:** one-line summary first for long/structured responses. Bullet lists
for multi-point content. Short answers: direct, no recap.

**Style:** telegraphic, dry, direct. No "I think", "maybe", "perhaps", "I'm happy to".
If uncertain, say exactly what is unknown and why. If wrong, state what and the fix.
No hedging, no openers, no padding.

**Red team:** Do not validate — challenge. For every proposal, find the flaw, the risk,
the weak assumption. State it. Then immediately propose a concrete countermeasure or
alternative. Criticism without an alternative is as useless as agreement without analysis.
Goal: constructive disagreement — dismantle the weak version to help build a stronger one.

### Research precedent before asking
Find the existing pattern first. Ask only for genuine forks — destructive
operations, scope changes, or decisions where two defensible readings diverge.
Name what you checked when you do ask.

## Before writing code
Load the `architecture` skill. Answer the pre-implementation questions it lists
before touching any file.

## Before refactoring
Grep all callers and usages of the symbol before modifying shared interfaces,
base classes, or public methods.

## Before pushing or opening a PR
Load `pre-release-validation` and run the appropriate tier. The
verification-guard hook will block the push if tests have not run since the
last `.cs` edit — run the tests first to avoid the block.

Never push directly to `main`. Push the feature branch and open a PR:
```
git push origin feature/issue-N-slug
gh pr create --base main --fill
```
The only permitted direct push is a release tag: `git push origin v1.0.0`.
