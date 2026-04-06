# Hermes Decision — Config path testing approach

- **Date:** 2026-04-06
- **Decision:** Validate config path traversal behavior through CLI command execution (`config get` / `config set`) using isolated `BOTNEXUS_HOME` roots per test, rather than coupling tests to internal helper implementation details.
- **Rationale:** Path traversal logic currently lives in CLI-local methods and is expected to migrate to a dedicated resolver service. CLI-level behavior tests remain valid through this refactor and verify externally observable outcomes (success/failure, conversion, null handling, path errors) independent of implementation.
- **Consequence:** Tests are resilient to extraction/refactoring and continue to enforce user-facing config path behavior as the contract.
