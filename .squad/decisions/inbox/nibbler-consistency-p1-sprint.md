# Consistency Review: Gateway P1 Sprint

**Reviewer:** Nibbler (Consistency Reviewer)
**Date:** 2026-04-05
**Scope:** All files in src/gateway/ (5 projects), src/channels/ (3 projects), src/BotNexus.WebUI/, tests/BotNexus.Gateway.Tests/

## Findings

### P0 (Blocking — fix now)

None.

### P1 (Important — fix this sprint)

1. **src/gateway/BotNexus.Gateway.Sessions/FileSessionStore.cs:8** — ConfigureAwait(false) comment scope was misleading. The comment said "Gateway library code uses ConfigureAwait(false)" broadly, but only the Sessions project follows this pattern. The Gateway core project (BotNexus.Gateway) intentionally omits ConfigureAwait(false) because it runs on the generic host thread pool. **FIXED:** Narrowed the comment to be specific about Gateway.Sessions vs Gateway host.

### P2 (Informational — backlog)

1. **src/channels/BotNexus.Channels.Telegram/TelegramServiceCollectionExtensions.cs:25-29** — Options pattern inconsistency. Registers `TelegramOptions` as a raw singleton (`new TelegramOptions()` + `services.AddSingleton(options)`) instead of using the .NET Options pattern (`AddOptions<T>` / `IOptionsMonitor<T>`) used by `GatewayServiceCollectionExtensions`. Acceptable for a Phase 2 stub, but should align when promoted to full implementation.

2. **src/channels/BotNexus.Channels.Telegram/TelegramChannelAdapter.cs, src/channels/BotNexus.Channels.Tui/TuiChannelAdapter.cs** — Both implement `IChannelAdapter` directly instead of extending `ChannelAdapterBase` from Channels.Core. The base class provides allow-list enforcement and common lifecycle patterns. Acceptable for Phase 2 stubs (they don't need the base class complexity yet), but should be refactored when promoted.

3. **src/gateway/BotNexus.Gateway/Isolation/InProcessIsolationStrategy.cs:118** — Lambda uses `ct` as CancellationToken parameter name instead of `cancellationToken`. This is intentional to avoid shadowing the outer method's `cancellationToken` parameter. Not a naming violation — just documenting for awareness.

## Verification Checklist

| Dimension | Status | Notes |
|-----------|--------|-------|
| **CancellationToken naming** | ✅ PASS | All public/interface methods use `cancellationToken`. `stoppingToken` in GatewayHost is idiomatic for BackgroundService. No `ct` in any public methods within scope. |
| **XML doc `<param>` accuracy** | ✅ PASS | All param name tags match actual parameter names across all files. |
| **Test naming pattern** | ✅ PASS | All 10 test files follow `MethodName_Condition_ExpectedResult`. |
| **File-scoped namespaces** | ✅ PASS | All .cs files use file-scoped namespaces. |
| **`sealed` on classes** | ✅ PASS | All non-inheritable concrete classes are sealed. `ChannelAdapterBase` is correctly abstract. |
| **Primary constructors** | ✅ PASS | Used in Telegram/TUI adapters. Traditional constructors elsewhere where field assignment requires it. Both patterns appropriate. |
| **ConfigureAwait(false)** | ⚠️ P1 FIXED | FileSessionStore comment clarified. Policy: Sessions project uses it (library code); Gateway host project omits it (no sync context). |
| **Lock type** | ✅ PASS | All lock fields use C# 13 `Lock` type consistently (InMemoryActivityBroadcaster, DefaultAgentSupervisor, DefaultAgentRegistry, InMemorySessionStore). FileSessionStore uses `SemaphoreSlim` for async locking — correct. |
| **DI registration** | ✅ PASS | `TryAddSingleton` for replaceable defaults, `AddSingleton` for core services. All GatewayHost dependencies registered. |
| **IOptions → IOptionsMonitor** | ✅ PASS | DefaultMessageRouter uses `IOptionsMonitor<GatewayOptions>`. Test mock (DefaultMessageRouterTests) correctly mocks `IOptionsMonitor`. |
| **Test mocks match interfaces** | ✅ PASS | All mocks align with current interface signatures. |
| **Interface remarks accuracy** | ✅ PASS | All `<remarks>` sections on abstraction interfaces accurately describe behavior post-P1. |
| **Channel stub docs** | ✅ PASS | Both stubs clearly document Phase 2 status and describe what full implementations would do vs. what they actually do. |

## Previous Review Regression Check

From the July 2026 review (0 P0, 4 P1, 7 P2):

| Previous Finding | Status |
|-----------------|--------|
| P1: CancellationToken `ct` in API layer | ✅ FIXED — All API controllers now use `cancellationToken` |
| P1: ConfigureAwait(false) undocumented | ⚠️ PARTIALLY — Now documented in FileSessionStore but comment was misleading (fixed this review) |
| P1: Test file names didn't match class names | ✅ FIXED — All 10 test files match their tested class |
| P2: Gateway uses `Lock` while AgentCore uses `object` | ✅ Consistent within Gateway (all use `Lock`) |

## Summary

- **0** P0s, **1** P1 (fixed), **3** P2s found
- Overall consistency: **Good**

The Gateway P1 sprint code quality is high. File-scoped namespaces, sealed classes, CancellationToken naming, XML docs, test naming, DI registration, and interface alignment are all consistent. The one P1 (misleading ConfigureAwait comment) has been fixed in this pass. The P2s are design decisions appropriate for Phase 2 stubs that should be revisited when those stubs are promoted to full implementations.
