# Research: Planning Pipeline (Nova -> Spec -> Squad)

## Problem Statement

There is no formal process for capturing bugs, improvements, and feature ideas that arise during conversations between Jon and Nova. These ideas get lost in session history or memory files, with no structured path to implementation.

## Solution Implemented

Jon established a planning directory at `Q:\repos\botnexus\docs\planning\` where Nova creates a folder per issue containing:

- `research.md` - Background research, industry analysis, current state
- `design-spec.md` - Formal spec that Leela and the squad can implement from

### Convention

```
Q:\repos\botnexus\docs\planning\
  bug-session-resumption/
    research.md
    design-spec.md
  feature-subagent-spawning/
    research.md
    design-spec.md
  feature-context-visibility/
    research.md
    design-spec.md
  ...
```

### Naming Convention
- `bug-*` - Something broken that needs fixing
- `feature-*` - New capability
- `improvement-*` - Enhancement to existing functionality

### Triggers
Nova should create a planning item when:
1. She hits a bug or limitation during normal operation
2. Jon discusses an improvement or feature idea
3. A workaround is needed for something that should work natively
4. Research reveals a gap compared to industry standards

### Workflow
1. **Discovery**: Issue surfaces in conversation or during work
2. **Research**: Nova investigates (web search, source analysis, industry comparison)
3. **Spec**: Nova writes research.md and design-spec.md
4. **Review**: Jon reviews and refines
5. **Handoff**: Leela and squad pick up from the spec
6. **Tracking**: Could integrate with ADO work items in the future

## Notes

This is intentionally lightweight - markdown files in a repo, not a full project management system. The goal is to capture intent and context while it is fresh, not to create bureaucracy.
