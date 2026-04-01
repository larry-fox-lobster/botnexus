# Decision Proposal: Dynamic Extension Assembly Loader

- **Author:** Bender
- **Date:** 2026-04-01
- **Status:** Proposed
- **Scope:** BotNexus.Core + BotNexus.Gateway extension bootstrap

## Decision

Implement dynamic extension loading via `ExtensionLoaderExtensions.AddBotNexusExtensions(IServiceCollection, IConfiguration)` in `BotNexus.Core`, and invoke it from Gateway service registration.

## Design Choices

1. **Configuration-driven discovery only**
   - Loader enumerates configured keys from:
     - `BotNexus:Providers`
     - `BotNexus:Channels:Instances`
     - `BotNexus:Tools:Extensions`
   - No default extension auto-load from disk.

2. **Folder convention**
   - Resolve extension folders as:
     - `{ExtensionsPath}/providers/{key}`
     - `{ExtensionsPath}/channels/{key}`
     - `{ExtensionsPath}/tools/{key}`

3. **Isolation model**
   - One collectible `AssemblyLoadContext` per extension folder.
   - Load every `.dll` in that folder.
   - Keep contexts in a singleton `ExtensionLoadContextStore` for future unload/hot-reload work.

4. **Registration strategy**
   - Prefer registrar-based registration:
     - Find concrete `IExtensionRegistrar` implementations and invoke `Register(IServiceCollection, IConfiguration)` with the extension’s config section.
   - Fallback to convention:
     - Scan for concrete implementations of the expected interface by type:
       - providers → `ILlmProvider`
       - channels → `IChannel`
       - tools → `ITool`
     - Register via DI as interface service (multi-registration supported).

5. **Safety and resilience**
   - Reject extension keys with rooted paths, invalid path chars, and traversal segments (`.`/`..`).
   - Ensure resolved extension paths stay under `ExtensionsPath`.
   - Catch reflection and assembly load exceptions and continue startup.
   - Missing folders or empty folders produce warnings, never crashes.

6. **Operational visibility**
   - Loader emits information/warning/error logs for:
     - root path resolution
     - folder scans
     - assembly loads
     - registrar execution
     - discovered and registered types
     - skipped/rejected extensions

## Validation

- Unit tests added for:
  - happy path loading and registration
  - missing folder handling
  - empty folder handling
  - registrar-based loading
  - convention-based loading
  - path traversal rejection
  - extension config section visibility/binding

