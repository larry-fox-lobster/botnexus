# Orchestration: Coordinator — Test Isolation & Safety Fixes

**Timestamp:** 2026-04-02T04:21:22Z  
**Role:** Session Coordinator (direct fixes)  
**Mode:** direct  

## Tasks

Fix critical test isolation issues and establish foolproof BOTNEXUS_HOME environment across all test processes.

## Outcome: SUCCESS ✓

### Issues Identified & Fixed

**Problem:** Tests were contaminating `~/.botnexus` on developer machines and CI/CD.
- Root cause: BOTNEXUS_HOME env var not set for test processes
- Scope: 4 critical test classes had no home directory isolation

### Tests Fixed

1. **AgentSessionIntegrationTests** — now uses isolated BOTNEXUS_HOME
2. **ExtensionLoadingE2eTests** — now uses isolated BOTNEXUS_HOME
3. **GatewayProviderLoadingTests** — now uses isolated BOTNEXUS_HOME
4. **MemoryToolsTests** — now uses isolated BOTNEXUS_HOME

### Infrastructure Changes

#### 1. test.runsettings (NEW)
- **Location:** Repository root
- **Purpose:** Foolproof environment variable configuration for all test processes
- **Content:** Sets `BOTNEXUS_HOME` to isolated temp directory
- **Scope:** Applied to all test projects via Directory.Build.props

#### 2. Directory.Build.props (NEW)
- **Location:** Repository root
- **Purpose:** Auto-apply test.runsettings to all test assemblies
- **Effect:** VSTest, dotnet test, and CI/CD all inherit BOTNEXUS_HOME setting
- **Benefit:** Single source of truth — no per-project configuration needed

#### 3. Test Parallelization Disabled
- **Reason:** Process-global env var (BOTNEXUS_HOME) races with parallel test execution
- **Solution:** Disabled parallelization in Unit and Integration test projects
- **Files Modified:**
  - BotNexus.Tests.Unit.csproj — `<ParallelizeTestsWithinCollection>false</ParallelizeTestsWithinCollection>`
  - BotNexus.Tests.Integration.csproj — `<ParallelizeTestsWithinCollection>false</ParallelizeTestsWithinCollection>`

### Verification Results

```
Full test suite: 465/465 tests PASS
ZERO ~/.botnexus contamination detected
ZERO file system leaks to user home directory
Test isolation: VERIFIED
```

### Impact

- **Safety:** Test environment completely isolated from user home directory
- **Reproducibility:** All developers and CI/CD see identical test behavior
- **Maintainability:** New tests inherit isolation automatically via Directory.Build.props
- **Reliability:** No more flaky tests due to leftover state from previous runs

## Decision Documented

See `.squad/decisions/inbox/scribe-test-isolation-pattern.md` for decision pattern and implications for future test work.
