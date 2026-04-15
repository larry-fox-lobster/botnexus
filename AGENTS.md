# Agent Guidelines for BotNexus

## Document Ownership

Some docs have a YAML front-matter header indicating ownership:

```yaml
---
owner: human          # human | ai | shared
author: Jon Bullen
ai-policy: minimal    # minimal | collaborative | open
---
```

**Respect these headers:**
- `minimal` — Fix typos and broken links only. No restructuring, no content removal. Substantive changes require explicit human approval.
- `collaborative` — May propose additions but must not remove or rewrite existing content without approval.
- `open` (default if no header) — May freely update, but don't delete useful content.

**If a doc has `owner: human` and `ai-policy: minimal`, do not rewrite or remove it during cleanup tasks.** This convention exists because a previous cleanup accidentally removed a human-authored document.

## Test Enforcement

**All tests must pass before any task is considered complete.** No exceptions.

### Rules

1. **Run the full test suite** before committing any code change:

   ```shell
   dotnet test BotNexus.slnx --nologo --tl:off
   ```

2. **Zero failures required.** If any test fails, diagnose and fix the issue before proceeding. Do not commit code with failing tests.

3. **Do not skip or disable tests** to make the suite pass. If a test is failing, the production code or the test itself must be fixed — not removed.

4. **Do not use `--no-verify`** for code changes. The pre-commit hook runs the test suite and must pass.

5. **If you introduce new behavior**, add corresponding tests. If you change existing behavior, update affected tests to match.

## Build

```shell
dotnet build BotNexus.slnx --nologo --tl:off
```

Build the full solution before running tests to avoid stale assembly issues (e.g., CLI integration tests depend on `BotNexus.Cli.dll` being built).

## Configuration

The BotNexus development configuration file is located at:

```
C:\Users\jobullen\.botnexus\config.json
```

Use the BotNexus CLI to manage configuration:

```shell
dotnet run --project src\gateway\BotNexus.Cli -- <command>
```
