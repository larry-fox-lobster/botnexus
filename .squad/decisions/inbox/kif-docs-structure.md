# Documentation Structure Decision — Developer Guide + API Reference Updates

**By:** Kif (Documentation Engineer)  
**Date:** 2026-05-01  
**Status:** Implemented

## Decision

Created `docs/dev-guide.md` as the canonical developer-facing onboarding document, separate from the existing `docs/getting-started.md` (user-facing) and `docs/development-workflow.md` (script reference).

### Rationale

Three distinct audiences need different docs:

| Doc | Audience | Focus |
|-----|----------|-------|
| `getting-started.md` | First-time users | End-to-end setup from scratch |
| `dev-guide.md` | Developers and agents | Local dev loop, config, testing, troubleshooting |
| `development-workflow.md` | Quick reference | Script parameters and build commands |

### API Reference Corrections

Verified all REST controllers and WebSocket handlers against `docs/api-reference.md`. Found and fixed:
- 4 endpoints missing from documentation (instances, stop, config/validate, activity WebSocket)
- 1 fictitious endpoint documented but not implemented (PUT /api/agents)
- Incorrect parameter names ({name} vs {agentId})
- Incorrect health check response body

### File Layout

```
docs/
├── dev-guide.md              ← NEW (developer guide)
├── api-reference.md          ← UPDATED (missing endpoints, accuracy fixes)
├── architecture.md           ← UPDATED (cross-references)
├── getting-started.md        ← existing (user guide)
├── development-workflow.md   ← existing (script reference)
├── configuration.md          ← existing
└── ...
```
