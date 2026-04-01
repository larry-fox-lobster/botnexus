# Ceremonies

> Team meetings that happen before or after work. Each squad configures their own.

## Design Review

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | before |
| **Condition** | multi-agent task involving 2+ agents modifying shared systems |
| **Facilitator** | lead |
| **Participants** | all-relevant |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. Review the task and requirements
2. Agree on interfaces and contracts between components
3. Identify risks and edge cases
4. Assign action items

---

## Consistency Review

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | after |
| **Condition** | any sprint completion, or any change touching architecture, config, or public APIs |
| **Facilitator** | nibbler |
| **Participants** | nibbler (solo — reads everything, fixes everything) |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. Read all docs end-to-end, cross-reference against code
2. Check config defaults in code match documented defaults
3. Grep for stale references (old paths, old names, old behavior)
4. Verify README and public-facing docs are accurate
5. Fix all discrepancies directly
6. Commit fixes with conventional commit format

---

## Retrospective

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | after |
| **Condition** | build failure, test failure, or reviewer rejection |
| **Facilitator** | lead |
| **Participants** | all-involved |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. What happened? (facts only)
2. Root cause analysis
3. What should change?
4. Action items for next iteration
