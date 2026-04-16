---
id: feature-planning-pipeline
title: "Planning Pipeline Convention"
type: process
priority: n/a
status: active
created: 2026-04-10
updated: 2026-04-10
author: nova
tags: [process, convention, planning]
---

# Design Spec: Planning Pipeline Convention

**Type**: Process / Convention
**Priority**: N/A (already implemented)
**Status**: Active
**Author**: Nova (via Jon)

## Overview

This is not a code feature - it is a convention for how Nova captures and specs out bugs, features, and improvements for the BotNexus platform.

## Convention

### Directory Structure
```
Q:\repos\botnexus\docs\planning\
  <type>-<short-name>/
    research.md      - Background research and analysis
    design-spec.md   - Implementation spec for the squad
```

### Types
- `bug-*` - Broken behavior
- `feature-*` - New capability
- `improvement-*` - Enhancement to existing

### Research Template
```markdown
# Research: <Title>

## Problem Statement
What is the issue or opportunity?

## Current State
How does it work today (or not)?

## Industry Research
How do other platforms handle this? (with sources)

## Key Findings
Summary of important discoveries

## Questions for the Squad
What needs clarification before implementation?
```

### Design Spec Template
```markdown
# Design Spec: <Title>

**Type**: Bug / Feature / Improvement
**Priority**: Critical / High / Medium / Low
**Status**: Draft / Review / Approved / In Progress / Done
**Author**: Nova (via Jon)

## Overview
One paragraph summary.

## Requirements
### Must Have
### Should Have
### Nice to Have

## Proposed Implementation
Technical design details.

## Phases
Incremental delivery plan.

## Testing Plan
How to verify it works.

## Open Questions
Unresolved decisions.
```

### INDEX.md Convention

The index is grouped by **type** (🐛 Bugs → ✨ Features → 🔧 Improvements → 📋 Process), then sorted by **priority** within each group.

**Active section:** Full tables with relative links to design specs, emoji priority indicators, short month/year dates.

**Archived section:** Collapsed `<details>` blocks per type to keep the page scannable.

**Priority legend:** 🔥 critical · 🔴 high · 🟡 medium · 🟢 low · ⚪ unset

**Status flow:** planning → draft → design → ready → in-progress → delivered → done

**Table columns (Active):** `Item | Pri | Status | Since | Description`
**Table columns (Archived):** `Item | Pri | Status | Resolved | Summary`

Keep it visual, scannable, and linkable. The index is the first thing someone reads — make it count.

### Nova's Responsibilities
1. Create planning items proactively when issues are discovered
2. Do real web research (not just from memory)
3. Include industry comparisons with sources
4. Write specs detailed enough for the squad to implement without further context
5. Flag open questions that need Jon's decision
6. Keep INDEX.md grouped by type, sorted by priority

## Status: Active
This convention is live as of 2026-04-10.
