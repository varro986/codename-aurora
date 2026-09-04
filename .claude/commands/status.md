Show the current project state in a single summary.

Run these commands and report the results:

1. `git branch --show-current` — current branch
2. `git status --short` — staged and unstaged changes
3. `git log --oneline -5` — last 5 commits
4. `git log --oneline origin/main..HEAD 2>/dev/null` — outgoing commits not yet on main
5. Find the latest `.trx` file in `TestResults/` — show its name and timestamp (or "no test run found")
6. Check if `TestResults/` exists and has any `.trx` files

Report format:
```
Branch:    <name>
Staged:    <files or "none">
Unstaged:  <files or "none">
Outgoing:  <commits or "up to date">
Last test: <filename @ HH:MM or "none found">
```

No prose. Numbers and names only.
