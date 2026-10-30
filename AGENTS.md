# Aurora: agent instructions

Rules for any coding assistant working on this repository. `CLAUDE.md` only imports this file.

<!-- BEGIN common rules: maintained centrally and copied identical into every product; do not edit here -->
## Common rules

The same in every repository of this family, private or public. A project that does something differently writes the
exception next to the rule it breaks, with the reason. Rules of a single project live in its README, "Rules".

### Goal

- **The perfect system**: every part does one thing, well, the same way as the others; nothing superfluous, nothing
  incoherent, nothing untested. When in doubt between two paths, take the one that gets closer to this.
- **Desired state and idempotence**: the wanted state lives in git and the code brings the system there; every step,
  repeated, does nothing; on an aligned system the dry run proposes no action.

### Single neutral source

- Rules live in plain text: `AGENTS.md` for a repository, the README for a project. `CLAUDE.md`, `GEMINI.md` and
  similar files only import them; skills, agents, commands and hooks of a tool never add rules of their own.
- Whatever must always hold (secrets, style, tests) also runs without AI, in pre-commit and in CI, with the same rules
  as the local hooks.
- Nothing useful lives only in a tool's memory: it goes into the repository.

### Language

- English for identifiers, keys, file and folder names and commit messages. Documentation, comments and logs follow
  the repository: see its own rules.

### Safety and operations

- Before every change: can it destroy, replace or overwrite files? Can it touch data that is not its own? If so, stop
  and ask. Every write, move or delete has an explicit path, condition and protection (write only if different, copy
  first, atomic, never outside the expected folders).
- Agents commit, push and merge only when the person driving the session asks; never skip hooks (`--no-verify`); never
  run destructive commands (`rm -rf`, `reset --hard`, `push --force`, recursive deletions).
- Secrets never in the repository, in logs, results, reports or visible process arguments; only their names in the
  code. gitleaks in pre-commit and in CI. Where a secrets file is needed, a `.example` with the same names and no
  values sits next to it, kept aligned by a test.
- Least privilege everywhere: every account, service or command has only the permissions it needs (one database and
  account per project, read-only where enough).

### Code

- **Values in one place**: no numbers, times, paths or thresholds hard-coded; configuration holds them. A path or value
  that differs from one machine to another comes from an environment variable, with its default in configuration.
- **Clean and lean**: remove what is not needed (files, keys, sections), no dead code, no useless work (repeated reads,
  calls or queries, scans where a map is enough, queries without a time filter); the simplest form that does the job,
  no abstraction for a single use. Comments useful but minimal: only the non-obvious why, constraints, edge cases;
  never history dates.
- **Tested locally first**: every project has a `cicd/` folder with its deterministic checks (tests, syntax, alignment
  with configuration), run on the machine before every push by one shared runner; the remote CI repeats the same steps
  and stays minimal (secrets scan on every push, the rest only when needed), so it never drifts from the local one.
- **Testable**: logic in small pure functions, I/O at the edges; a test for every important behaviour (writes,
  idempotence, secrets) and for every fixed bug. Formats exchanged between projects have an example tested by both the
  writer and the reader.
- **Design patterns where they help** (layers, adapters, configuration-driven behaviour), without over-engineering.
- **Reuse**: use what already exists; duplicated code goes to a common place.
- **PowerShell 7 (pwsh) everywhere**, the same on Windows and Linux; the only exception is a Windows first-run script,
  compatible with Windows PowerShell 5.1, which installs pwsh and stops. No absolute machine paths in scripts (relative
  to the script or from `$HOME`), no Windows-only commands without the alternative next to them.
- No new feature without the owner's yes: ideas are proposals.

### Coherence

- **One system**: projects are parts of one coherent whole; the same thing is done the same way everywhere (dry run
  and apply, results and logs, messages that say what to do, names, README structure). A new solution in one project
  holds for all of them, or the reason why not is written.
- **In the same change**: every code change updates README, configuration and tests together.
- **One project per session**: a session declares at the start which project it works on, stays in its folder and asks
  before touching another project; changes that span projects are declared as such.
- **Related projects**: every link (one uses, hosts or reuses another) is in the Dependencies table of the README, on
  both sides.

### Brainstorming

Every project has one `brainstorming.md`: where ideas and doubts land before anything is decided. Two sections:

- **Ideas**: a table `Id | Date | Idea | Status`, one idea per row (`IDEA-n`, the date of origin, status proposed,
  accepted or rejected). An idea becomes work only with the owner's yes; it is never deleted, only its status changes.
- **Open questions**: doubts and contradictions to settle before deciding, one per line, with their date of origin.

What reaches it is already clean: no personal data, customer data or employer data, whatever the repository.

### Best of breed

- For every need, the standard or tool recognised as the best in its category, not an invented format: decisions as
  ADRs, architecture with arc42 and C4, quality with ISO 25010, acceptance criteria as Given/When/Then. Used in the
  reduced form the project needs, not in full.
- An exception (an own format, a less common tool) only with its reason written next to it; when the reason no longer
  holds, switch to the standard.

### Technologies

- The README declares the technologies used (languages, runtimes, services and main versions).
- Preferred: C#, bash and PowerShell 7. Anything else only when needed, with the reason written in the README.
- Dependencies, tools and languages: open source under MIT or Apache 2.0, portable across Windows and Linux; another
  licence, or something that runs on one system only, only with the reason written in the README.

### README

Sections with these names and in this order; a section that is not needed is omitted, never renamed. The first five
say what the project is, the others how to work on it; the project's own sections go between Usage and Contributing.

1. `# <Name>` and one line: what it is.
2. Goal: why it exists, what problem it solves, for whom (1-3 lines).
3. Description: what it does and where it runs, in a few lines.
4. Technologies: languages, runtimes, services, main versions; for anything not C#, bash or PowerShell, the reason.
5. Dependencies: table `From | What it uses | Type` (project, library, service, machine), or "None"; also who depends
   on this project.
6. Architecture: the folder tree (code block, one line per entry), components and flows; a mermaid diagram only if
   needed.
7. Rules: the project's own.
8. Installation (only if something is installed). 9. Usage (only if it is run). 10. The project's own sections.
11. Contributing: change, test, ship (only if there is code). 12. Tests: exact commands and what they cover.
13. Agents: what can run in parallel and who does it, contracts not to touch, the orchestrator's checks, what stays
    with the owner. 14. Miscellaneous: always present ("Nothing yet." is fine). 15. To do (only if needed).

A repository documented in Italian uses the same sections with Italian titles (Obiettivo, Descrizione, Tecnologie,
Dipendenze, Architettura, Regole del progetto, Installazione, Uso, Come intervenire, Prove, Agenti, Miscellanea, Da
fare).

### Working with agents

Roles are by model tier, so they hold with any assistant; the "today" column is the current choice.

| Role | Today (Claude) | Does | Does not |
|---|---|---|---|
| Orchestrator | Opus 5.5, high effort (max for architecture) | plan, contracts and interfaces (first, in series), decisions, wiring, build, tests, commits | mechanical work it can delegate |
| Executor | Sonnet 5.5 | one task at a time, in an isolated worktree, in parallel with the others; touches only its files | build, commits, contract changes |
| Microtask | Haiku 5.5 | searches, test stubs from the criteria, renames, mechanical checks | decisions |
| Reviewer | Sonnet 5.5, fresh agent | reads the diff with fresh eyes, runs the tests, checks the criteria | fixing things itself |

- **Deterministic work goes into tools, not into reasoning**: whatever is done the same way every time (checks,
  scaffolding, counting, formatting, syncing) becomes a script, which costs no tokens and always gives the same
  result; agents run the tools, read their results and keep for themselves only what needs judgment. When an agent
  does the same deterministic job by hand twice, the next step is a tool. A project keeps its tools together in
  `tools/`, each with its usage written at the top, so any agent finds them in the same place.
- Only what touches different files runs in parallel: never two agents on the same file, never two on the same task.
- A new agent's prompt says everything (files to read, boundaries, what to report): it does not see the conversation.
- Every agent reports files changed, tests written or run and doubts; never "done" without saying what was verified.
- Small jobs are done, not delegated. Real data and production: the agent prepares, the owner approves and ships.
<!-- END common rules -->

<!-- BEGIN product rules: maintained centrally and copied identical into every product; do not edit here -->
## Product rules

What holds for every product, on top of the common rules.

### A product

- A public repository with its own philosophy, meant for more people: issues, pull requests, contributors.
- **Self-sufficient**: whoever opens it sees only the repository, so everything needed to work on it (architecture,
  decisions, rules) is in it. No personal data, customer data or employer data anywhere, not even in notes or test
  fixtures.
- Documentation, comments and logs in English.

### Workflow

- `main` is protected, for everyone: every change goes through a branch and a pull request, merged only with CI green
  and linear history.
- MIT licence. Community files: `SECURITY.md` (private vulnerability reporting), `CODE_OF_CONDUCT.md` (Contributor
  Covenant 2.1), issue forms, pull request template, Dependabot.

### Spec driven development

The product is built from its specs, following the GitHub Spec Kit model: constitution (`AGENTS.md`), spec, plan,
tasks, implementation. The layout below is the required skeleton; entries marked `(when needed)` and names with `NNN`
are added as the work requires them.

```
|-- README.md, AGENTS.md, CLAUDE.md, CONTRIBUTING.md, LICENSE, SECURITY.md, CODE_OF_CONDUCT.md
|-- .gitignore, .gitleaks.toml, .editorconfig
|-- .githooks/
|   \-- pre-commit               gitleaks on staged files
|-- .github/
|   |-- workflows/               CI: gitleaks, then build and tests
|   |-- ISSUE_TEMPLATE/          bug report and idea forms
|   |-- pull_request_template.md
|   \-- dependabot.yml           updates of dependencies and GitHub Actions
|-- .claude/agents/              executor.md, reviewer.md, microtask.md: model and tools only (when needed)
|-- specs/
|   |-- brainstorming.md
|   |-- vision.md, glossary.md   (when needed)
|   |-- requirements.md          REQ-F / REQ-NF, ISO 25010 (when needed)
|   |-- architecture.md          reduced arc42 + C4 (when needed)
|   |-- decisions/               ADRs (Nygard), never edited: a changed decision is a new ADR
|   |   \-- NNNN-title.md
|   \-- features/                one folder per feature: the unit of work
|       \-- NNN-name/
|           |-- spec.md          stories and Given/When/Then criteria (what)
|           |-- plan.md          how: components, contracts and interfaces it touches, risks
|           \-- tasks.md         tasks: id, files touched, depends on, role, "done when"
|-- cicd/                        the product's local checks, run before every push (common rules, "Code")
|-- src/
|-- tests/
\-- tools/                       deterministic tools for agents and people (common rules, "Working with agents")
```

- `specs/brainstorming.md` is the product's brainstorming (common rules, "Brainstorming") while only the owner works
  on the product; with contributors, GitHub Issues take the place of its ideas.
- An idea becomes a requirement only with the owner's yes. Traceability: an accepted idea becomes a REQ, the REQ cites
  the features and ADRs that implement it; "done when" points to the Given/When/Then criteria, so it is a passing test.
- A file is added when it is needed, not before. The README stays short and points to `specs/`.

### Parallel work from tasks.md

- Tasks are written to be parallel: each declares the files it touches and what it depends on. Two tasks with no
  shared files and no open dependencies run together, one executor each in its own worktree.
- Contracts and interfaces from `plan.md` come first, written by the orchestrator in series; no executor changes them.
- The reviewer checks the diff against the criteria in `spec.md`.
<!-- END product rules -->

## Aurora rules

None yet beyond the common rules. What the product is: `README.md`; what it must do: `specs/`.

## Verification

Commands the orchestrator runs before a commit, from the repository root (`README.md`, "Tests", says what they cover):

```
dotnet build Aurora.slnx
dotnet test --solution Aurora.slnx
pwsh -NoProfile -c 'Invoke-Pester tools'
```

Warnings are errors. A test project with no tests yet ends with "Zero tests ran" (exit code 8) until its task adds
them. The product name is a codename: it is changed only with `tools/rename-product.ps1`, never by hand.
