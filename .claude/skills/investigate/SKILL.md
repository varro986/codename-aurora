---
name: investigate
description: Scope a feature or change before writing code. Load when a task touches multiple layers, shared contracts, or uncertain blast radius.
---

# Investigate Skill

Load before any task where you cannot answer: "what changes, in which files, in which order?"

## When to load this skill

- A new feature that touches more than one layer
- A refactor where you haven't located all callers and implementors
- A bug without a confirmed reproduction path
- Any change to a shared interface, base class, or public contract
- Any change that crosses a coupling axis in `scripts/ci/coupling-hotspots.json`

## Investigation steps

Do not write code during this phase. Read, grep, and report.

1. **Restate the ask** — one sentence. If you can't, ask.
2. **Find the entry point** — where does the change start? (Use case, handler, presenter, repository, event?)
3. **Trace the call chain** — Grep the codebase. List every file this touches.
4. **Check the coupling axes** — read `scripts/ci/coupling-hotspots.json`. Does this change trigger any axis?
5. **State the blast radius** — which tests break? Which existing behavior changes?
6. **Propose a plan** — ordered list: file → what changes. Stop here and wait for confirmation.

## Output format

```
Entry point: <file:line>

Files touched:
  - <file> — <what changes>
  - ...

Coupling axes triggered: none | <axis-id: reason>

Blast radius: <tests affected / none>

Plan:
  1. <file> — <change>
  2. <file> — <change>
  ...
```

## Non-negotiables

- No code written during investigation — only grep, read, report.
- If blast radius is larger than expected, stop and surface it before proceeding.
- If two defensible approaches exist, name both with trade-offs. Do not choose silently.
- If the coupling axes block the change, say so and name the bypass path (`Coupling-ack:` trailer or maintainer skip token).
