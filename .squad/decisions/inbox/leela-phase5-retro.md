# Retrospective — Port Audit Phase 5

**Facilitator:** Leela (Lead/Architect)  
**Date:** 2026-07-16  
**Sprint:** Phase 5 — Port Audit Remediation (P0/P1 fixes across Providers, AgentCore, CodingAgent)  
**Status:** Complete

---

## 1. What Happened (Facts)

**Scope:** Phase 5 remediated audit findings across three subsystems — Providers, AgentCore, and CodingAgent. 8 fixes shipped (3 P0, 5 P1) with 14 implementation commits.

**Audit phase:**
- 3 opus-class agents audited Providers (72% coverage), AgentCore (92% coverage), and CodingAgent (detailed tool-by-tool comparison) in parallel.

**Design review:**
- AC-C1 downgraded from Critical: transforms don't execute during streaming, so the finding was misclassified.
- P-M4 identified as a design improvement, not a required port alignment fix.

**Implementation:**
- Sprint 5a (parallel): Bender (5 items on independent files) + Farnsworth (3 items on independent files)
- Test track: Hermes started writing tests while implementation was in progress
- 14 implementation commits, ~25 total commits (impl + tests + docs + consistency)

**Final numbers:**
- Tests: 480 → 501 (21 new tests), 0 failures
- Build: Clean, 0 errors, 0 warnings

---

## 2. What Went Well

### Parallel audit at opus scale reduced wall-clock time
Three agents audited three subsystems simultaneously with zero coordination overhead. The subsystem boundaries were clean enough that no cross-cutting analysis was needed. This is the highest parallelism we've achieved in the audit phase.

### Design review continues to filter false positives
Two findings (AC-C1 Critical misclassification, P-M4 design-not-bug) were caught before implementation. The design review gate has now filtered incorrect findings in every sprint since Phase 3. It remains the single highest-value ceremony.

### Parallel implementation with zero file conflicts
Bender and Farnsworth worked concurrently on non-overlapping file sets. Zero merge conflicts, zero rework. This is now proven across five consecutive sprints.

### Test count growth is healthy
21 new tests in one sprint. Total at 501. The project maintains the discipline of tests following code.

---

## 3. What Went Wrong — Root Cause Analysis

### Bug 1: CompactForOverflow list aliasing

**Symptom:** After Bender's per-retry transform change, `messages.Clear()` also cleared the compacted result, causing empty conversations on retry.

**Root cause:** `CompactForOverflow` returned the same `List<>` reference for small conversations (where no compaction was needed). The caller assumed it received an independent copy. When the caller cleared the original list, it destroyed both.

**Why it surfaced now:** Bender's retry loop restructure changed the message lifecycle — messages are now cleared and rebuilt per-retry instead of once. The pre-existing aliasing was harmless when the list was only read once.

**Category:** Latent defect exposed by correct refactoring. The bug was in the compaction code, not in Bender's change.

**Fix:** Return a new list (defensive copy) when no compaction is needed, so callers never alias the input.

### Bug 2: Test isolation race — duplicate provider registration

**Symptom:** `AgentTests` and `AgentLoopRunnerTests` both registered a provider named `"test-api"`. When xUnit ran them in parallel, the second registration collided with the first.

**Root cause:** No test-scoped provider registry. Both test classes shared the global `ProviderRegistry` and neither cleaned up after itself.

**Why it surfaced now:** Bender's retry loop restructure changed execution timing, making the parallel collision window larger.

**Category:** Pre-existing test infrastructure debt. Tests assumed serial execution.

**Fix:** Use unique provider names per test class, or scope the registry per test.

### Bug 3: ShortHash length expectation mismatch

**Symptom:** Hermes wrote a test expecting 9-character hash output. Actual pi-mono algorithm produces 12–14 characters.

**Root cause:** Leela's design spec stated the hash would be trimmed to 9 characters. The pi-mono reference implementation has no trim step. The spec was wrong.

**Category:** Speculative test authoring from design spec, not from code. **This is the third recurrence of the speculative-parallel anti-pattern** (Phase 3: docs-against-plan, Phase 4: tests-against-plan, Phase 5: tests-against-spec).

**Fix:** Test was corrected to assert the actual output length. Spec updated.

### Bug 4: Git commit conflicts from concurrent agents

**Symptom:** Multiple agents committing to the same repo caused file lock issues — `testhost` processes held locks, concurrent `git` operations failed.

**Root cause:** No git commit coordination protocol. Agents commit independently whenever they complete work, without checking whether another agent or process holds the index lock.

**Category:** Infrastructure gap. The multi-agent git workflow lacks a lock/queue mechanism.

### Bug 5: CodingAgent test runner hangs after passing

**Symptom:** All 134 CodingAgent tests pass, but the test runner never exits. Requires manual kill.

**Root cause:** Likely a test fixture that starts a background process or opens a port and doesn't dispose it. The test host waits for all threads to complete before exiting.

**Category:** Test cleanup debt. Needs investigation — specific fixture not yet identified.

---

## 4. What Should Change

### 4.1 — Enforce defensive copies at subsystem boundaries

The list aliasing bug is a classic shared-mutable-state defect. Any method that accepts a collection and might return it unmodified must return a defensive copy. This should be a code review checklist item for all transform/compaction methods.

### 4.2 — Test-scoped service registries

Provider registration tests must not share global state. Either:
- Each test class gets an isolated registry instance, or
- Test provider names include the test class name to guarantee uniqueness.

### 4.3 — Kill the speculative-parallel anti-pattern permanently

This is the **third sprint** where artifacts authored from specs/plans instead of committed code produced failures. The pattern:
- Phase 3: 18/22 doc issues (docs from design decisions)
- Phase 4: 9/30 test failures (tests from audit findings)
- Phase 5: ShortHash length wrong (test from design spec)

**New rule:** Tests and docs that assert specific behavior (return types, string lengths, parameter counts, exact signatures) MUST be authored AFTER the code they describe is committed and green. Conceptual test plans can parallel; concrete assertions cannot.

### 4.4 — Git commit queue for multi-agent sprints

Agents must acquire a coordination lock before committing. Options:
- File-based lock (`.squad/.git-lock`) with agent name and timestamp
- Sequential commit phase: all agents write changes, coordinator commits in order
- Worktree-per-agent: each agent works in its own git worktree, coordinator merges

### 4.5 — Investigate and fix CodingAgent test hang

The test runner hang is a time bomb — it wastes CI minutes and masks real failures. Needs a dedicated investigation to find the undisposed fixture.

---

## 5. Action Items

| ID | Action | Owner | Priority | Status |
|----|--------|-------|----------|--------|
| R5-1 | Add defensive-copy rule to code review checklist for transform/compaction methods | Leela | P1 | Pending |
| R5-2 | Refactor test provider registration to use test-scoped or uniquely-named providers | Hermes | P1 | Pending |
| R5-3 | Add sprint sequencing rule: concrete assertions (tests/docs) must follow committed code | Leela | P0 | Pending |
| R5-4 | Design and implement git commit coordination protocol for multi-agent sprints | Leela | P1 | Pending |
| R5-5 | Investigate CodingAgent test runner hang — find undisposed fixture | Farnsworth | P2 | Pending |
| R5-6 | Update design spec template to flag "assumed behavior" vs "verified behavior" | Leela | P2 | Pending |

---

## 6. Sprint Health Summary

| Metric | Value |
|--------|-------|
| Fixes shipped | 8 (3 P0, 5 P1) |
| Commits | ~25 |
| Tests added | 21 (480 → 501) |
| Test failures | 0 |
| Build warnings | 0 |
| Bugs found during sprint | 5 |
| Bugs from pre-existing debt | 3 (aliasing, test isolation, test hang) |
| Bugs from process gaps | 2 (spec mismatch, git conflicts) |
| Design review filter saves | 2 (AC-C1 downgrade, P-M4 rejection) |

**Verdict:** Solid execution sprint. The implementation itself was clean — all five bugs trace to pre-existing debt or process gaps, not to implementation errors. The speculative-parallel anti-pattern's third recurrence demands a hard process rule (R5-3).
