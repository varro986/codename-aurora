# Contributing to Aurora

1. **Specs first**: a change starts from `specs/` (an idea in `brainstorming.md`, a feature folder in `specs/features/` with
   `spec.md`, `plan.md` and `tasks.md`); code follows the spec, not the other way round.
2. **GitHub Flow**: a branch per change, a pull request to `main`, CI green (gitleaks included) before merging.
3. **Commits** in English, small and focused; never skip hooks.
4. **No secrets and no real data**: enable the secret scan once per clone with `git config core.hooksPath .githooks`
   (needs gitleaks).
5. Rules for coding assistants: `AGENTS.md`.
