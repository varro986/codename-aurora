# Aurora: agent instructions

Rules for any coding assistant working on this repository. `CLAUDE.md` only imports this file.

<!-- BEGIN common rules: maintained centrally and copied identical into every product; do not edit here -->
## Common rules

These rules are the same in every product of this family. A product that does something differently writes the
exception next to the rule it breaks, with the reason.

### Single neutral source

- Rules live in plain text: `AGENTS.md` for the repo. `CLAUDE.md`, `GEMINI.md` and similar files only import it;
  skills, agents, commands and hooks of a tool never add rules of their own.
- Whatever must always hold (secrets, style, tests) also runs without AI, in pre-commit and in CI, with the same rules
  as the local hooks.
- Nothing useful lives only in a tool's memory: it goes into the repo.

### Language

- English for everything that is code or documentation: identifiers, keys, file and folder names, commit messages,
  README, specs.

### Safety and operations

- Agents commit and push only when the person driving the session asks; never skip hooks (`--no-verify`); never run
  destructive commands (`rm -rf`, `reset --hard`, `push --force`, recursive deletions).
- Secrets never in the repo, in logs or in messages; gitleaks in pre-commit and in CI. Where a secrets file is needed,
  a `.example` with the same names and no values sits next to it, kept aligned by a test.
- Before every change: can it destroy, replace or overwrite files? Can it touch data that is not its own? If so, stop
  and ask.
- No personal data, customer data or employer data anywhere in the repo, not even in notes or test fixtures.

### Code

- Clean and testable (pure logic, I/O at the edges), reuse what exists, every value in one place, idempotent
  operations; scripts and hooks in PowerShell 7 (pwsh), the same on Windows and Linux.
- No absolute paths of a machine in scripts (relative to the script or from `$HOME`), no Windows-only commands without
  the alternative next to them.
- No new feature without the owner's yes: ideas are proposals.

### Best of breed

- For every need, the standard or tool recognised as the best in its category, not an invented format: decisions as
  ADRs, architecture with arc42 and C4, quality with ISO 25010, acceptance criteria as Given/When/Then. Used in the
  reduced form the product needs, not in full.
- An exception (an own format, a less common tool) only with its reason written next to it; when the reason no longer
  holds, switch to the standard.

### Technologies

- The README declares the technologies used (languages, runtimes, services and main versions) in "Technologies".
- Preferred: C#, bash and PowerShell 7. Anything else only when needed, with the reason written in "Technologies".
- Dependencies, tools and languages: open source under MIT or Apache 2.0, portable across Windows and Linux; another
  licence, or something that runs on one system only, only with the reason written in "Technologies".

### README

Sections with these names and in this order; a section that is not needed is omitted, never renamed: Goal,
Description, Technologies, Dependencies (table `From | What it uses | Type`, both directions), Architecture, Rules,
Installation, Usage, product sections, Contributing, Tests, Agents, Miscellaneous (always present), To do. The README
stays short and points to `specs/`.

### Spec driven development

The product is built from its specs, following the GitHub Spec Kit model: constitution (`AGENTS.md`), spec, plan,
tasks, implementation.

```
|-- README.md, AGENTS.md, CLAUDE.md, CONTRIBUTING.md, LICENSE
|-- .gitignore, .gitleaks.toml, .githooks/, .github/ (CI, issue and PR templates)
|-- .claude/agents/    executor.md, reviewer.md, microtask.md: model and tools only, rules stay here
|-- specs/
|   |-- vision.md, ideas.md, questions.md, glossary.md
|   |-- requirements.md          REQ-F / REQ-NF (ISO 25010)
|   |-- architecture.md          reduced arc42 + C4
|   |-- decisions/NNNN-*.md      ADRs (Nygard), never edited: a changed decision is a new ADR
|   \-- features/NNN-name/       one folder per feature: the unit of work
|       |-- spec.md              stories and Given/When/Then criteria (what)
|       |-- plan.md              how: components, contracts and interfaces it touches, risks
|       \-- tasks.md             tasks: id, files touched, depends on, role, "done when"
|-- src/
\-- tests/
```

- `ideas.md` is the inbox (`IDEA-n`, date of origin, status: proposed, accepted, rejected) while only the owner works
  on the product; with contributors, GitHub Issues take its place. `questions.md` holds open questions and
  contradictions.
- An idea becomes a requirement only with the owner's yes. Traceability: an accepted idea becomes a REQ, the REQ cites
  the features and ADRs that implement it; "done when" points to the Given/When/Then criteria, so it is a passing test.
- A file is added when it is needed, not before.

### Working with agents

Roles are by model tier, so they hold with any assistant; the "today" column is the current choice.

| Role | Today (Claude) | Does | Does not |
|---|---|---|---|
| Orchestrator | Opus 5.5, high effort (max for architecture) | plan, contracts and interfaces (first, in series), decisions, wiring, build, tests, commits | mechanical work it can delegate |
| Executor | Sonnet 5.5 | one task at a time, in an isolated worktree, in parallel with the others; touches only its files | build, commits, contract changes |
| Microtask | Haiku 5.5 | searches, test stubs from the criteria, renames, mechanical checks | decisions |
| Reviewer | Sonnet 5.5, fresh agent | reads the diff with fresh eyes, runs the tests, checks the criteria | fixing things itself |

- Tasks are written to be parallel: each declares the files it touches and what it depends on. Two tasks with no
  shared files and no open dependencies run together, one executor each; never two agents on the same file.
- Contracts and interfaces from `plan.md` come first, written by the orchestrator in series; no executor changes them.
- The reviewer checks the diff against the criteria in `spec.md`.
- A new agent's prompt says everything (files to read, boundaries, what to report): it does not see the conversation.
  Every agent reports files changed, tests written or run and doubts; never "done" without saying what was verified.
- Small jobs are done, not delegated; never two agents on the same task. Real data and production: the agent prepares,
  the owner approves and ships.
<!-- END common rules -->

## Aurora rules

None yet beyond the common rules. What the product is: `README.md`; what it must do: `specs/`.

## Verification

Commands the orchestrator runs before a commit: none yet (no code). They are added here with the first code.
