---
id: bug-exec-process-disconnect
title: "ExecTool/ProcessTool - Research"
type: research
created: 2026-07-18
updated: 2026-07-18
author: nova
---

# Research: ExecTool and ProcessTool

## ExecTool Dead Code

BackgroundProcesses dictionary — write-only, never read:
```csharp
private static readonly ConcurrentDictionary<int, ProcessInfo> BackgroundProcesses = new();
internal sealed record ProcessInfo(int Pid, string Command, DateTime StartedUtc);
internal static IReadOnlyDictionary<int, ProcessInfo> GetBackgroundProcesses() => BackgroundProcesses;
internal static void ClearBackgroundProcesses() => BackgroundProcesses.Clear();
```

Handle disposal bug — using var disposes on return for background path:
```csharp
using var process = new Process { StartInfo = startInfo };
if (background)
{
    BackgroundProcesses[pid] = new ProcessInfo(pid, command[0], DateTime.UtcNow);
    return result; // Process disposed here by 'using'
}
```

## ProcessTool Current State (Non-Functional)

Queries empty registry:
```csharp
public ProcessTool() : this(ProcessManager.Instance) { }
// ProcessManager._processes is always empty
```

ProcessManager — 62 lines of code wrapping a ConcurrentDictionary nobody writes to.
ManagedProcess — 130 lines wrapping a Process handle nobody creates.

Both files can be deleted.

## System.Diagnostics.Process API for ProcessTool Rewrite

### List Processes

```csharp
Process.GetProcesses()                    // All processes
Process.GetProcessesByName("node")        // By name
Process.GetProcessById(1234)              // By PID (throws if not found)
```

### Available Properties per Process

| Property | Type | Notes |
|----------|------|-------|
| Id | int | PID |
| ProcessName | string | Executable name without extension |
| HasExited | bool | Whether process has terminated |
| ExitCode | int | Only valid after HasExited = true |
| StartTime | DateTime | When process started |
| WorkingSet64 | long | Physical memory (bytes) |
| TotalProcessorTime | TimeSpan | CPU time consumed |
| MainModule.FileName | string | Full path (may throw on access denied) |
| Threads.Count | int | Thread count |

### Kill

```csharp
process.Kill()                            // Kill single process
process.Kill(entireProcessTree: true)     // Kill process and children
```

### Error Handling

- `Process.GetProcessById()` throws `ArgumentException` if PID not found
- Many properties throw `InvalidOperationException` if process has exited
- `MainModule` can throw `Win32Exception` for access-denied processes
- System/protected processes may not be killable

### Practical List Output

A list of all processes could be large (hundreds). ProcessTool should:
- Default to a summary (PID, name, memory) sorted by memory or CPU
- Support name filter: `list(filter: "node")` 
- Support top-N: `list(limit: 20)` sorted by resource usage
- Keep output concise — agents don't need 500 process rows

## Extension Structure After Fix

```
extensions/tools/
  exec/BotNexus.Extensions.ExecTool/
    ExecTool.cs              <- Run commands, return output or PID
  process/BotNexus.Extensions.ProcessTool/
    ProcessTool.cs           <- OS process viewer/manager
    (ProcessManager.cs)      <- DELETE
    (ManagedProcess.cs)      <- DELETE
```

Zero shared state. Zero cross-references. Two independent tools.
