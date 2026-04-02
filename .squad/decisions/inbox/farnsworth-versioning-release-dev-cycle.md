# Release + Dev Versioning Decision (Farnsworth)

**Date:** 2026-04-02  
**Requested by:** Jon Bullen  
**Status:** Proposed

## Decision

Adopt a unified versioning model across build, packaging, CLI runtime, and install manifests:

1. **Release builds:** Semantic version from git tag (`vX.Y.Z` → `X.Y.Z`).
2. **Dev builds:** `0.0.0-dev.{short-hash}`.
3. **Dirty dev builds:** `0.0.0-dev.{short-hash}.dirty`.
4. **CI/release override:** `BOTNEXUS_VERSION` always wins.

## Rationale

- Removes ambiguity from `1.0.0.0` defaults and timestamp package versions.
- Makes running code provenance visible in CLI (`--version`, `status`) and install metadata (`version.json`).
- Aligns local dev, CI, and official release flows under one deterministic model.

## Implementation Notes

- Added root `Directory.Build.props` with default `Version` and `InformationalVersion` set to `0.0.0-dev`.
- Added shared PowerShell resolver `scripts/common.ps1::Resolve-Version`.
- `scripts/pack.ps1` now:
  - resolves version using shared resolver
  - passes `/p:Version` and `/p:InformationalVersion` to `dotnet publish`
  - writes resolved version into `.nuspec`
- `scripts/install-cli.ps1` now passes resolved version into `dotnet pack`.
- CLI now supports one-line `--version` output and enhanced `status` output with version comparison.
- `version.json` now contains:
  - `Version`
  - `InstalledAtUtc`
  - `Commit` (short hash)
  - `InstallPath`
  - `Packages`
