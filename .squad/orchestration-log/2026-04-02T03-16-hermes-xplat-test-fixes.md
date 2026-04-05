# Hermes — Cross-Platform Test Fixes + BOTNEXUS_HOME Isolation

**Timestamp:** 2026-04-02T03:16:47Z  
**Agent:** Hermes (Tester)  
**Mode:** background  
**Model:** gpt-5.3-codex

## Outcome

✅ **All 322 unit tests passing** on GitHub Actions Linux + Windows  
✅ **5 cross-platform compatibility fixes** committed  
✅ **5 BOTNEXUS_HOME isolation violations** found and fixed

## Work Summary

### Cross-Platform Fixes (Applied to 8 test files)

1. **Link creation OS-awareness** — Windows uses `cmd.exe /c mklink /J`, Unix uses .NET symlink APIs
2. **Path-rooted assertions** — Sample paths use platform-native format (Windows: `C:\absolute`, Unix: `/absolute`)
3. **File extension matching** — Case-insensitive via `StringComparison.OrdinalIgnoreCase`
4. **Socket binding exclusivity** — `Socket.ExclusiveAddressUse = true` before bind

### BOTNEXUS_HOME Isolation Fixes

Root cause: 5 test classes missing `BOTNEXUS_HOME` environment variable override.

Files modified:
- `tests/BotNexus.Tests.Unit/Tests/ExtensionLoaderTests.cs`
- `tests/BotNexus.Tests.Unit/Tests/DiagnosticsCheckupsTests.cs`
- `tests/BotNexus.Tests.E2E/Infrastructure/MultiAgentFixture.cs`
- `tests/BotNexus.Tests.E2E/Infrastructure/CronFixture.cs`
- `tests/BotNexus.Tests.Integration/Tests/GatewayApiKeyAuthTests.cs`
- `tests/BotNexus.Tests.Integration/Tests/MultiProviderE2eTests.cs`
- `tests/BotNexus.Tests.Integration/Tests/SlackWebhookE2eTests.cs`

Also modified:
- `src/BotNexus.Agent/AgentWorkspace.cs` — central path resolver enhancements

## Decisions Applied

- ✅ Copilot directive: Agents must commit their work
- ✅ Copilot directive: No tests may touch ~/.botnexus/
- ✅ Hermes decision: Cross-platform test patterns standardized

## CI Status

GitHub Actions workflow now passes on both Linux and Windows runners.
