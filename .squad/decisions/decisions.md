# Decisions Log

## Retrospective — Port Audit Phase 3

**Facilitator:** Leela (Lead/Architect)  
**Date:** 2026-04-06  
**Sprint:** Phase 3 — pi-mono packages/ai, packages/agent, packages/coding-agent vs BotNexus  
**Status:** Complete

---

### 1. What Happened (Facts)

**Scope:** Full audit of pi-mono `packages/ai`, `packages/agent`, `packages/coding-agent` against the BotNexus C# port. 9 architecture decisions proposed (AD-9 through AD-17).

**Outcomes:**
- 7 ADs implemented (AD-9, AD-10, AD-11, AD-12, AD-14, AD-15, AD-17)
- 1 AD deferred — AD-13 (OpenRouter/Vercel routing types) per YAGNI: no provider exists yet
- 1 AD already present — AD-16 (maxRetryDelayMs already in codebase)
- 13 commits across 6 agents (Farnsworth, Bender, Kif, Nibbler, Scribe, Leela)
- 415 tests passing (up from 372 — 43 new tests)
- 0 build errors, 0 warnings
- 4 new training modules (06-context-file-discovery, 07-thinking-levels, 08-building-custom-coding-agent, 09-tool-development)
- 22 consistency discrepancies found and fixed in post-sprint review

**Sprint structure:**
- Sprint 3a (parallel): Farnsworth (AD-9 + AD-15) + Bender (AD-11 + AD-12)
- Sprint 3b (sequential): Bender (AD-10 → AD-14 → AD-17)
- Parallel track: Kif (training docs — 4 modules, ~1,325 lines)
- Post-work: Nibbler (consistency review — 22 fixes), Scribe (logs + decision merge)

---

### 2. What Went Well

#### Parallel execution tracks worked
Sprint 3a ran Farnsworth and Bender in parallel on independent subsystems (AgentCore/Providers vs CodingAgent). No merge conflicts. No cross-dependency issues. The design review's boundary analysis (AD assignments by subsystem) made this possible.

#### YAGNI discipline held
AD-13 (OpenRouter routing types) was correctly deferred. No provider exists. Building types for imagined future routing would have added dead code. The team made the right call.

#### Design review → sprint pipeline is maturing
Phase 3 followed the same ceremony as Phase 2: audit → design review → architecture decisions → parallel sprint → consistency review. The cadence is stable and repeatable.

#### Test count growth is healthy
43 new tests in one sprint. Total at 415. Test coverage follows code — not bolted on after the fact.

#### AD-16 and AD-17 caught existing coverage
Two items turned out to be already present in the codebase. The audit correctly identified them rather than duplicating work. AD-17 only needed the `/thinking` slash command addition (the `--thinking` CLI flag already existed).

---

### 3. What Could Improve

#### Documentation was written against planned APIs, not implemented code
This is the root cause of 18 of the 22 consistency issues. Kif wrote training docs during the sprint based on design review decisions (planned signatures) rather than waiting for final implementations. Every new training module had at least one wrong API signature.

#### No handoff checkpoint between code and docs agents
Bender shipped code. Kif wrote docs. Neither verified against the other's output. There is no process gate that says "docs agent must read final code before authoring examples."

#### Consistency review is reactive, not preventive
Nibbler found 22 issues — but only after the sprint was "complete." The fix commit (`e7ff6d8`) is waste: work that wouldn't exist if the docs had been right the first time. We need to catch this before the sprint ends, not after.

#### IAgentTool.ExecuteAsync signature was wrong in 4 separate places
The `toolCallId` parameter was missing from the interface definition AND all examples in `09-tool-development.md`. This suggests Kif was working from an earlier version of the interface, before `toolCallId` was added. The docs agent needs a way to query current code signatures, not rely on its training data.

---

### 4. Root Cause Analysis — 22 Consistency Issues

#### Primary Root Cause: Docs authored from design decisions, not from code

**Evidence:** Nibbler's report shows a clear pattern:
- `07-thinking-levels.md` said `--thinking` didn't exist → it was the primary deliverable of AD-10
- `09-tool-development.md` had wrong ExecuteAsync signature → `toolCallId` parameter was omitted
- `06-context-file-discovery.md` described binary search truncation → code uses char-by-char iteration
- `08-building-custom-coding-agent.md` called SystemPromptBuilder.Build() with non-existent parameters

All four HIGH-severity issues stem from the same cause: the docs agent wrote against the plan, not the code.

#### Contributing Factor: No compilation gate for doc examples

Training doc code examples are markdown fenced blocks. They aren't compiled. They aren't tested. A typo in a code example (`string?` vs `IReadOnlyList<string>`) is invisible unless a human (or Nibbler) reads it line by line.

#### Contributing Factor: Sprint parallelism without sync point

Kif and Bender ran in parallel. This was intentional (speed). But it means Kif couldn't see Bender's final code — because it didn't exist yet when Kif started writing.

---

### 5. Action Items for Next Sprint

| # | Action | Owner | Priority |
|---|--------|-------|----------|
| 1 | **Doc Checkpoint Gate:** Docs agent MUST read final code (actual interface files, actual tool implementations) before authoring examples. No exceptions. Add this as a step in the sprint ceremony. | Leela | P0 |
| 2 | **Stagger doc authoring:** Kif starts docs AFTER code commits land, not in parallel. Trade speed for correctness. Parallel doc work is only safe for conceptual/architecture content, not API examples. | Leela | P0 |
| 3 | **Signature extraction script:** Create a small utility that extracts public API signatures from compiled assemblies. Kif can run this to get ground-truth method signatures instead of relying on context. | Farnsworth | P1 |
| 4 | **Doc example validation:** Investigate Roslyn scripting or doctest-style validation for C# code blocks in markdown. Even partial compilation (resolve types, check method signatures) would catch the most common errors. | Hermes | P2 |
| 5 | **Consistency review shifts left:** Nibbler runs a focused check BEFORE the sprint-complete commit, not after. Make this part of the sprint exit criteria, not a post-sprint ceremony. | Leela | P1 |

---

### 6. Architecture Grade Update

#### Grade: **A** (maintained from Phase 2)

**Justification:**
- All planned port gaps from pi-mono `packages/ai`, `packages/agent`, and `packages/coding-agent` are either resolved or consciously deferred (YAGNI)
- 415 tests, 0 warnings — quality gates hold
- 17 architecture decisions locked across 3 phases — design discipline is strong
- Consistency issues were process failures (doc timing), not architecture failures
- The codebase accurately reflects the pi-mono design intent where it matters, and diverges intentionally where C#/.NET idioms are better

**Risk:** The 22-fix consistency commit is a process smell, not an architecture smell. The code is sound. The documentation pipeline needs the gates described above.

#### Cumulative Stats (All 3 Phases)

| Metric | Phase 1 | Phase 2 | Phase 3 | Total |
|--------|---------|---------|---------|-------|
| Commits | 12 | 18 | 13 | 43 |
| ADs locked | — | 8 | 9 | 17 |
| P0s fixed | 10 | 15 | — | 25 |
| Tests | 350 | 372 | 415 | — |
| Training modules | 4 | 2 | 4 | 10 |

---

### Summary

Phase 3 completed its mission: the pi-mono port audit is done. All three source packages have been scanned. The code quality is high — the issues we found were documentation process failures, not code defects. The single most important process improvement is **staggering doc authoring behind code commits** so examples are written against real implementations. This is the concrete change we're making for the next sprint.

---

## Post-Sprint 3 Consistency Review

**Author:** Nibbler (Consistency Reviewer)  
**Date:** 2026-04-03  
**Commit:** e7ff6d8

### Summary

Sprint 3 delivered 7 features (AD-9 through AD-17) with 4 new training docs and multiple API changes. Consistency review found **22 discrepancies** across 7 files — all fixed.

### Pattern Observed

New training docs (06-09) were written based on planned APIs rather than final implementations. Every Sprint 3 training doc had at least one wrong API signature. The most critical gap was 07-thinking-levels.md claiming `--thinking` didn't exist in the CLI — when it was the primary deliverable of AD-10 and AD-17.

### Discrepancies Fixed (by severity)

#### HIGH (7)
1. **07-thinking-levels.md**: CLI section said "--thinking flag does not exist" — rewrote with actual --thinking, /thinking, and session metadata
2. **09-tool-development.md**: IAgentTool.ExecuteAsync missing `toolCallId` parameter across interface definition and all 4 examples
3. **09-tool-development.md**: GetPromptGuidelines return type wrong (`string?` vs `IReadOnlyList<string>`)
4. **06-context-file-discovery.md**: Truncation algorithm was binary search in docs, char-by-char iteration in code
5. **08-building-custom-coding-agent.md**: SystemPromptBuilder.Build() called with non-existent parameters
6. **03-coding-agent.md**: Missing ListDirectoryTool from tool table, code example, and count
7. **CodingAgent/README.md**: Tool count wrong (6→7), missing --thinking in CLI help

#### MEDIUM (10)
8. **08-building-custom-coding-agent.md**: Missing `using BotNexus.CodingAgent.Utils` namespace import
9. **08-building-custom-coding-agent.md**: SystemPromptBuilder used as static method but it's instance-based
10. **08-building-custom-coding-agent.md**: Cross-ref linked to `08-tool-development.md` instead of `09-tool-development.md`
11. **09-tool-development.md**: EchoTool example ExecuteAsync wrong signature
12. **09-tool-development.md**: CalculatorTool example ExecuteAsync wrong signature
13. **09-tool-development.md**: DatabaseQueryTool example ExecuteAsync wrong signature + wrong callback name
14. **09-tool-development.md**: Error handling example ExecuteAsync wrong signature
15. **09-tool-development.md**: Built-in tools list missing ListDirectoryTool
16. **05-glossary.md**: Duplicate ThinkingLevel entry (lines 432 and 531)
17. **05-glossary.md**: Cross-reference header missing modules 06-09

#### LOW (5)
18. **CodingAgent/README.md**: Opening line missing grep and list_directory from tool list
19. **CodingAgent/README.md**: Missing list_directory tool section
20. **CodingAgent/README.md**: ReadTool params showed `range` instead of `start_line`/`end_line`
21. **08-building-custom-coding-agent.md**: DemoTool GetPromptGuidelines returns wrong type
22. **09-tool-development.md**: DatabaseQueryTool GetPromptGuidelines returns wrong type

### Recommendation

Training docs authored during a sprint should be reviewed against final code BEFORE the sprint is considered complete. The doc-writing agent and the code-writing agent need a handoff checkpoint to catch signature mismatches.

### Validation

- ✅ Build: `dotnet build BotNexus.slnx` — 0 errors, 0 warnings
- ✅ Tests: 415/415 pass across 7 test projects

---

## Design Review — Phase 5: Port Audit Consolidated Findings

**Facilitator:** Leela (Lead/Architect)  
**Date:** 2026-04-06  
**Status:** Approved — ready for implementation  
**Requested by:** sytone (Jon Bullen)

---

### 1. Sprint Scope

#### IN SPRINT — Critical (3 items)

| ID | Finding | Verified | Verdict | Priority |
|----|---------|----------|---------|----------|
| CA-C1 | ShellTool truncates HEAD instead of TAIL | ✅ Confirmed: `ordered.Take(MaxOutputLines)` takes first lines | **ACCEPT — Critical** | P0 |
| CA-C2 | ShellTool 120s default timeout | ✅ Confirmed: `DefaultTimeoutSeconds = 120`, no config override | **ACCEPT — Critical** | P0 |
| P-C1 | Tool call argument validation missing | ✅ Confirmed: raw `JsonElement` passed through, no schema check | **ACCEPT — Critical (upgraded)** | P0 |

**Note on AC-C1 (Partial message in context):** Downgraded from Critical to Deferred. Verified that `transformContext` runs **before** streaming in `AgentLoopRunner.cs:164-176`, not during. The partial message is emitted via `MessageUpdateEvent` but no current consumer needs it in the transform pipeline. This becomes relevant only when mid-stream context management is added.

#### IN SPRINT — Major (5 items)

| ID | Finding | Verified | Verdict | Priority |
|----|---------|----------|---------|----------|
| CA-M1 | ListDirectory flat-only | ✅ Confirmed: `SearchOption.TopDirectoryOnly` | **ACCEPT** | P1 |
| CA-M2 | Context discovery misses ancestor walk | ✅ Confirmed: checks root only, no parent traversal | **ACCEPT** | P1 |
| AC-M1 | transformContext/convertToLlm once before retries | ✅ Confirmed: `providerContext` computed outside retry loop | **ACCEPT** | P1 |
| P-M2 | shortHash utility missing | ✅ Confirmed: uses pipe-delimited composition, no hash | **ACCEPT** | P1 |
| P-C3 | MessageTransformer normalizer signature divergent | ✅ Confirmed: callback `Func<string,string>?` vs TS model+source | **ACCEPT** | P1 |

#### DEFERRED — Backlog

| ID | Finding | Reason |
|----|---------|--------|
| AC-C1 | Partial message not in context during streaming | No current consumer. Architecture runs transforms before stream, not during. Revisit when mid-stream context window management is needed. |
| CA-M3 | CLI missing flags | Feature addition. No current CLI consumer. |
| CA-M4 | System prompt guidelines static | Cosmetic. Current prompts are functional. |
| CA-M5 | Session format v2 vs v3 | Migration concern. v2 works. Migrate when v3 features are needed. |
| AC-M2 | Tool lookup case-insensitive | **Already decided (2026-04-05):** Intentional C# improvement. Case-insensitive is more robust. No change. |
| AC-M3 | Proxy stream function | **Already deferred (2026-04-05):** No current consumer. |
| P-M1 | BuiltInModels only ~33 | Low priority. Models added as needed. 828 in TS includes deprecated entries. |
| P-M3 | Faux test provider missing | Nice-to-have. Current unit tests use mocks directly. |
| P-M4 | SupportsXhigh auto-detect by model ID | **REJECT.** Explicit registration via `supportsExtraHighThinking` flag is cleaner than pattern-matching magic. C# approach is better. |

---

### 2. Decisions Log (Phase 5)

| # | Decision | Rationale |
|---|----------|-----------|
| D9 | Downgrade AC-C1 (partial message in context) from Critical to Deferred | Transforms run before streaming, not during. No current consumer. |
| D10 | Upgrade P-C1 (tool call validation) from Deferred to P0 | Previously deferred saying "tools validate own inputs" — but hallucinated args crash tools before self-validation runs. Safety issue. |
| D11 | Reject P-M4 (SupportsXhigh auto-detect) | Explicit `supportsExtraHighThinking` flag is cleaner than pattern-matching on model IDs. C# approach is architecturally superior. |
| D12 | Set ShellTool default timeout to 600s, not infinite | Infinite is dangerous. 600s covers 99% of builds. Config allows override. |
| D13 | Accept AC-M2 (tool lookup case-insensitive) as intentional | Already decided 2026-04-05. More robust than case-sensitive. No change. |
| D14 | Accept P-M1 (BuiltInModels count) as low priority | 33 active models vs 828 includes deprecated. Add as needed. |
| D15 | ToolCallValidator: top-level validation only | Validate required fields and types. No deep nested schema validation. Practical 80/20 approach. |
| D16 | MessageTransformer signature change is breaking — single PR | All call sites updated atomically. No gradual migration. |

---

### 3. Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| **ShellTool TAIL truncation may lose early output** (e.g., a warning at line 1 that explains a later error) | Medium | Include truncation notice with total line count so the agent can re-run with `head` if needed. Consider keeping first 10 lines + last N lines (sandwich approach). Decision: start with pure TAIL; iterate if agents struggle. |
| **600s default timeout still too short for CI-scale builds** | Low | Config-driven. Document that `null` disables timeout. Agents can pass explicit timeout per-call. |
| **ToolCallValidator false positives on flexible schemas** | Medium | Only validate required fields and top-level types. Don't reject `additionalProperties`. Log validation failures via diagnostics before hard-failing — give us data to tune. |
| **Per-retry transform adds latency** | Low | Transforms should be fast (millisecond-scale). Document idempotency requirement. If a transform is slow, that's a bug in the transform, not in the retry loop. |
| **Ancestor walk finds conflicting instructions** | Medium | Closest-to-cwd wins. Document merge precedence. Stop at `.git` boundary. |
| **MessageTransformer normalizer signature is breaking** | High | Must update all call sites in the same PR. Search exhaustively. Add compiler error if old signature used (method overload won't match). |

---

### 4. Implementation Status (Phase 5)

| Work Item | Status | Commits |
|-----------|--------|---------|
| CA-C1 | ✅ Done | `fix(ShellTool): truncate TAIL instead of HEAD` |
| CA-C2 | ✅ Done | `feat(ShellTool): make timeout configurable` |
| P-C1 | ✅ Done | `feat(Providers.Core): add ToolCallValidator` |
| CA-M1 | ✅ Done | `feat(ListDirectory): enumerate 2 levels deep` |
| CA-M2 | ✅ Done | `feat(ContextFileDiscovery): walk ancestor directories` |
| AC-M1 | ✅ Done | `refactor(AgentLoopRunner): move transform into retry loop` |
| P-M2 | ✅ Done | `feat(Providers.Core): add ShortHash utility` |
| P-C3 | ✅ Done | `refactor(MessageTransformer): align normalizer signature` |

**Test coverage:** 22 new tests (all passing)  
**Bugs fixed during testing:** 3 (list aliasing, race condition, hash length)  
**Build:** Clean (0 errors, 0 warnings)  
**Tests:** 475/475 passing

---

### 5. Retrospective — Port Audit Phase 5

**Facilitator:** Leela (Lead/Architect)  
**Date:** 2026-04-05  
**Participants:** Farnsworth, Bender, Hermes, Kif, Nibbler

---

#### Sprint Summary

Full port audit comparing pi-mono TypeScript against BotNexus C# across providers/ai, agent/agent, and coding-agent. Design review reduced 14 raw findings to 8 fixes. Farnsworth and Bender implemented fixes in parallel. Hermes wrote 22 tests. All work completed.

| Metric | Value |
|--------|-------|
| Baseline tests | 453 |
| Final tests | 475 |
| New tests | 22 |
| Commits | 12 (8 features + 4 tests + 3 bugfixes = 15 total work items) |
| Build | Clean, 0 errors |
| Bugs fixed | 3 (regressions caught during testing) |

#### What Went Well

- **Design review gate:** Reduced 14 findings to 8 approved items. Filter rate: 43%.
- **Parallel execution:** Farnsworth + Bender on independent subsystems (Providers vs CodingAgent). No merge conflicts.
- **Test discipline enforced:** Phase 5 followed improved sequencing: Audit → Design → Implementation → Tests → Docs. Tests written against committed code, not design decisions. This fixed the Phase 4 anti-pattern.
- **Bug detection:** 3 regressions caught and fixed same-sprint (list aliasing, race condition, hash length).
- **Conventional commits:** All 15 commits follow format. Build stayed clean throughout.

#### What Didn't Go Well

- (None noted. Phase 5 execution was clean.)

#### Action Items (Carried Forward)

1. **Speculative test authoring rule (from Phase 4 retro):** Tests must follow code, not lead it. Phase 5 enforced this successfully.
2. **Test-after-impl sequencing:** Sprint template explicitly sequences Audit → Design → Impl → Tests. Phase 5 proved this works.
3. **Design review gate:** Mandatory. Continue using.

#### Cumulative Stats (All 5 Phases)

| Metric | Phase 1 | Phase 2 | Phase 3 | Phase 4 | Phase 5 | Total |
|--------|---------|---------|---------|---------|---------|-------|
| Commits | 12 | 18 | 13 | ? | 15 | ~58+ |
| Fixes locked | 10 | 15 | 6 | 5 | 8 | ~44 |
| Tests | 350 | 372 | 415 | ? | 475 | — |
| Bugs caught | — | — | 22 | 9 | 3 | ~34 |
| Design review % | — | — | — | — | 43% | — |

---

### Sign-off

- [x] Design review approved (Leela)
- [x] Implementation complete (Bender, Farnsworth)
- [x] Testing complete (Hermes)
- [x] Bugs fixed (Coordinator)
- [x] All decisions locked
