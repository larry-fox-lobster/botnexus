# Decision: Dev Loop Documentation Overhaul

**Author:** Kif  
**Date:** 2026-04-06  
**Status:** Implemented

## Context

The dev loop documentation and scripts had accumulated several accuracy issues: a broken pre-commit hook path, references to non-existent scripts (`pack.ps1`, `install.ps1`) and test projects (`BotNexus.Tests.Unit`), missing script parameters in docs, and a gap in the getting-started-dev.md section numbering.

## Decision

1. **Pre-commit hook targets Gateway tests, not unit tests.** The project has no `BotNexus.Tests.Unit` project. The pre-commit hook now runs `tests/BotNexus.Gateway.Tests` for fast feedback.

2. **Removed phantom script references.** `scripts/pack.ps1` and `scripts/install.ps1` do not exist. Documentation now references only the 4 actual scripts: `dev-loop.ps1`, `start-gateway.ps1`, `export-openapi.ps1`, `install-pre-commit-hook.ps1`.

3. **`dev-loop.md` is the canonical dev loop reference.** Restructured as the single authoritative source for the edit→build→test→run→verify cycle, including live Copilot testing and auth.json setup.

## Rationale

Documentation that references non-existent paths creates friction for both human developers and AI agents following the guides. Accuracy over completeness.
