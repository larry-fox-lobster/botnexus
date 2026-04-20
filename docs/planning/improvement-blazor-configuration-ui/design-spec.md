---
id: improvement-blazor-configuration-ui
title: "Blazor Configuration UI — Canvas Views, Root Config, Locations"
type: improvement
priority: medium
status: proposed
created: 2025-07-17
tags: [blazor, configuration, ux, locations]
---

# Improvement: Blazor Configuration UI

**Status:** proposed
**Priority:** medium
**Created:** 2025-07-17

## Problem

The Blazor configuration UI has several usability gaps and missing functionality:

1. **Config sections don't open in main canvas** — Clicking a config section item in the sidebar has no effect on the main content area. Section detail should open as a full view in the main canvas.
2. **Root "Configuration" node has no action** — Clicking the root "Configuration" node should open general/world-level configuration settings (e.g., gateway options, default provider, global preferences).
3. **"Locations" section is missing** — There is no UI for managing locations despite `feature-location-management` being shipped. Users cannot view or edit locations from the config UI.
4. **No per-agent location configuration** — Locations can only be set globally. Agents should be able to have agent-level location overrides (e.g., an agent scoped to a specific repo or working directory).

## Requirements

### A. Config Section → Canvas View
- Clicking any config section item in the sidebar opens its detail/edit view in the main canvas area.
- Canvas view should show the full configuration for that section with edit capabilities.
- Sidebar selection state should visually indicate which section is active.

### B. Root Configuration Node
- Clicking "Configuration" (root) opens a world-level settings view.
- World-level view shows: gateway options, default provider, global agent defaults, and other top-level config.

### C. Locations Config Section
- Add "Locations" as a new section in the configuration sidebar tree.
- List all configured locations with name, path, and description.
- Support add/edit/delete of locations.
- Wire to the existing `feature-location-management` backend APIs.

### D. Per-Agent Location Configuration
- In the agent configuration view, add a locations section.
- Allow assigning/overriding locations at the agent level.
- Agent-level locations should merge with or override world-level locations.
- Backend: extend agent config schema to support `locations` property.

## Design

### UI Layout

```
┌─────────────────┬──────────────────────────────────┐
│ Config Sidebar   │  Main Canvas (detail view)       │
│                  │                                   │
│ ▸ Configuration  │  [World-level settings]           │
│   ▸ Providers    │  — or —                           │
│   ▸ Agents       │  [Selected section detail]        │
│   ▸ Locations    │                                   │
│   ▸ Extensions   │                                   │
│                  │                                   │
└─────────────────┴──────────────────────────────────┘
```

### Implementation Phases

**Phase 1:** Canvas routing — clicking any sidebar section opens its view in the main canvas.
**Phase 2:** Root config node → world-level settings view.
**Phase 3:** Add Locations section (CRUD UI wired to existing APIs).
**Phase 4:** Per-agent location configuration (schema extension + UI).

## Scope

- Blazor UI: sidebar navigation, canvas routing, new Locations components
- Backend: agent config schema extension for per-agent locations
- No changes to core location resolution logic (that's already shipped)
