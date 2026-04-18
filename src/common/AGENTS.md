# Common Projects Rules

## Dependency boundary

Projects in `src/common/` depend only on `src/domain/` — pure domain-level utilities with no agent or gateway dependencies.

**Allowed dependencies:**
- `src/domain/` — domain primitives
- NuGet packages

## Project structure

| Project | Purpose |
|---------|---------|
| `BotNexus.Prompts` | System prompt building and context file management |
