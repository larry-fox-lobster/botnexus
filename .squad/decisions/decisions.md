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
