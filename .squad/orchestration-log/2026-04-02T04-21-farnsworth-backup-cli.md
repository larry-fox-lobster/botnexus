# Orchestration: Farnsworth — Backup CLI Implementation

**Timestamp:** 2026-04-02T04:21:22Z  
**Agent:** Farnsworth (Platform Dev, gpt-5.3-codex)  
**Mode:** background  

## Task

Implement CLI backup command with subcommands: `backup create`, `backup restore`, `backup list`.

## Outcome: SUCCESS ✓

### Implementation

- **Backup Command Location:** src/BotNexus.Cli/Program.cs
- **Subcommands Implemented:**
  - `backup create` — creates full backup of ~/.botnexus to external backup location
  - `backup restore {backup-id}` — restores from named backup
  - `backup list` — lists available backups with metadata

### Key Decisions

1. **Backup Storage Location:** `~/.botnexus-backups` (sibling directory, external to home dir)
   - Not inside `~/.botnexus` to prevent backup contamination
   - Follows principle: backups are external emergency snapshots
   
2. **Self-Backup Exclusion:** Coordinator discovered bug where backups themselves were being backed up
   - Fixed: Added exclusion logic to skip `~/.botnexus-backups` when creating new backup
   - Prevents recursive backup spirals

3. **Default Path Handling:** Coordinator updated default path to be external (not internal)
   - Ensures safety by design, not by developer caution

## Files Modified

- `src/BotNexus.Cli/Program.cs` — new backup command group with create/restore/list subcommands

## Tests

- Integration tests written by Hermes (11 test cases, all passing)
- CliHomeScope updated to clean up sibling backups directory

## Dependencies & Unblocks

- Unblocks CLI user workflow for data recovery
- Backup location decision informs test isolation patterns (see scribe-test-isolation-pattern decision)

## Cross-Agent Impact

- **Hermes:** Wrote integration tests for backup CLI (BackupCliIntegrationTests)
- **Coordinator:** Fixed self-backup exclusion, validated external path strategy
