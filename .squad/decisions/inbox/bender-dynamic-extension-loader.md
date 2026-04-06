# Bender Decision — Gateway Dynamic Extension Loader

## Context
Gateway extension points (channel adapters, isolation strategies, auth/session/router contracts) were statically wired. We needed runtime discovery and manifest-based loading.

## Decision
Implement a gateway-specific IExtensionLoader contract in Gateway.Abstractions and a collectible AssemblyLoadContextExtensionLoader in Gateway.

- Discovery scans configured extension folders for botnexus-extension.json
- Manifest validation enforces id/name/version/entryAssembly and allowed extensionTypes
- Loading uses one collectible AssemblyLoadContext per extension
- Type discovery registers implementations for known gateway extension interfaces
- Startup wiring loads extensions after core service registration and before host startup

## Security Notes
- Entry assembly paths are constrained to stay inside each extension directory
- Missing dependencies and invalid manifests fail that extension only
- Load/unload operations are logged and isolated per extension

## Follow-up
Future hardening can add signature validation and policy-based allow-lists in the same loader pipeline.
