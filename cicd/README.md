# cicd

The product's local checks, run on the machine before every push: `pwsh -NoProfile -File cicd/run.ps1`. The steps live
in `steps.json`; the remote CI (`.github/workflows/ci.yml`) runs the ones that need no desktop session, with the same
runner.
