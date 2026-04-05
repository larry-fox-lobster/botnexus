# Orchestration: Hermes — Backup CLI Integration Tests

**Timestamp:** 2026-04-02T04:21:22Z  
**Agent:** Hermes (Tester, gpt-5.3-codex)  
**Mode:** background  

## Task

Write 11 backup CLI integration tests covering create/restore/list scenarios.

## Outcome: SUCCESS ✓

### Test Suite Implementation

**Test File:** tests/BotNexus.Tests.Integration/Tests/BackupCliIntegrationTests.cs

**Test Count:** 11 tests, all passing

**Coverage:**
- `backup create` — creates backup, validates directory structure
- `backup list` — lists backups, shows metadata (name, timestamp, size)
- `backup restore` — restores from backup, validates files restored
- Error cases: missing backup ID, invalid backup path, permission errors
- Sibling directory cleanup: verifies backups dir is cleaned up after test

### Test Infrastructure Updates

**CliHomeScope Improvements:**
- Added cleanup for sibling `~/.botnexus-backups` directory
- Ensures test isolation: each test gets clean home + backups dirs
- Prevents cross-test contamination

### Key Patterns

1. **BOTNEXUS_HOME Isolation:** Each test runs with isolated home directory
   - Managed by CliHomeScope (updated by Hermes)
   - Backed by test.runsettings (added by Coordinator)

2. **Sibling Directory Cleanup:** Backups are external to home dir
   - Cleanup logic must handle both `~/.botnexus` and `~/.botnexus-backups`
   - CliHomeScope now does both

## Files Modified

- `tests/BotNexus.Tests.Integration/Tests/BackupCliIntegrationTests.cs` — new test suite
- `tests/BotNexus.Tests.Integration/Tests/CliTestHost.cs` — CliHomeScope cleanup improvements

## Test Results

```
11 backup CLI integration tests: PASS
Total test suite: 465/465 tests PASS
ZERO ~/.botnexus contamination verified
```

## Cross-Agent Impact

- **Farnsworth:** Implementation of backup CLI for these tests
- **Coordinator:** Test infrastructure (test.runsettings, Directory.Build.props)
