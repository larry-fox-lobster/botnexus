# Agent Guidelines for BotNexus

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
