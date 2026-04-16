---
id: bug-exec-process-disconnect
title: "ExecTool and ProcessTool Built on Wrong Assumptions"
type: bug
priority: high
status: done
created: 2026-07-18
updated: 2026-07-18
author: nova
tags: [tools, extensions, exec, process, background-processes, testing]
ddd_types: [IAgentTool, ExecTool, ProcessTool]
---

# Bug: ExecTool and ProcessTool Built on Wrong Assumptions

**Type**: Bug
**Priority**: High
**Status**: Done
**Author**: Nova

## Problem

Both tools were built around a shared registry pattern that's wrong for their purpose.

**ExecTool** should run commands and return output (foreground) or a PID (background). Instead it has dead-code state tracking and disposes the Process handle prematurely.

**ProcessTool** should let the agent see and manage all running processes on the machine — like Task Manager as a tool. Instead it queries an empty internal registry, making it completely non-functional.

## What's Wrong

### ExecTool

1. **Dead state tracking**: `ConcurrentDictionary<int, ProcessInfo> BackgroundProcesses` — written to, never read. Remove entirely.
2. **Process handle disposal**: `using var process` disposes the handle on method exit even for background mode. Background processes should be properly detached.

### ProcessTool

1. **Registry-based design is fundamentally wrong**: Queries `ProcessManager.Instance` — a pre-populated registry that nothing populates. The tool should work with OS processes directly.
2. **ProcessManager and ManagedProcess are unnecessary**: The entire wrapper layer exists for a registry bridge that shouldn't exist. Delete it.
3. **Can only see "registered" processes**: Should see ALL processes on the machine.

## Correct Design

### ExecTool

Run a command. Return output (foreground) or PID (background). No tracking, no state.

**Fixes:**
1. Remove `BackgroundProcesses` dictionary and `ProcessInfo` record entirely
2. Fix background mode: detach cleanly without premature disposal
3. No other changes — foreground mode works correctly

### ProcessTool

OS process viewer and manager. Wraps `System.Diagnostics.Process` API.

**Actions:**

- **list**: List running processes on the machine. Support filtering by name, PID, or other criteria. Uses `Process.GetProcesses()` / `Process.GetProcessesByName()`.
- **status(pid)**: Get details for a specific process — running/exited, exit code, process name, start time, memory, CPU.
- **kill(pid)**: Kill a process by PID. Option for process tree kill.

That's it. No registry, no ManagedProcess, no output capture, no stdin. Those are OS-level limitations for processes you didn't launch — and that's fine. The agent can use exec with output redirection if it needs to capture output from a background process.

## Implementation

### ExecTool Changes (Minimal)

1. Delete `BackgroundProcesses`, `ProcessInfo`, `GetBackgroundProcesses()`, `ClearBackgroundProcesses()`
2. Fix background process detachment (don't `using var` for background path)

### ProcessTool Changes (Rewrite)

1. Delete `ProcessManager.cs` and `ManagedProcess.cs`
2. Rewrite `ProcessTool.cs` against `System.Diagnostics.Process`:
   - `list(filter?)` — `Process.GetProcesses()` with optional name/pid filter, return PID, name, memory, CPU, start time
   - `status(pid)` — `Process.GetProcessById(pid)`, return details
   - `kill(pid)` — `Process.GetProcessById(pid).Kill(entireProcessTree)` 
3. Handle errors gracefully (PID not found, access denied, already exited)

## Test Matrix

After implementation, validate both tools handle all argument combinations correctly — valid, edge-case, and invalid. Use these to stress-test, find bugs, then resolve them.

### ExecTool Test Cases

#### Valid — Foreground

| # | Description | Arguments | Expected |
|---|-------------|-----------|----------|
| E1 | Simple command | `command: ["echo", "hello"]` | stdout: "hello", exit 0 |
| E2 | Command with exit code | `command: ["bash", "-c", "exit 42"]` | exit code 42 |
| E3 | Stderr output | `command: ["bash", "-c", "echo err >&2"]` | stderr captured in output |
| E4 | Mixed stdout/stderr | `command: ["bash", "-c", "echo out; echo err >&2"]` | Both captured |
| E5 | Custom timeout (not hit) | `command: ["sleep", "1"], timeoutMs: 5000` | Completes normally |
| E6 | Timeout triggers | `command: ["sleep", "60"], timeoutMs: 500` | Timeout message, process killed |
| E7 | Stdin piping | `command: ["cat"], input: "hello world"` | stdout: "hello world" |
| E8 | Env vars | `command: ["bash", "-c", "echo $FOO"], env: {"FOO": "bar"}` | stdout: "bar" |
| E9 | Working dir override | `command: ["pwd"], workingDir: "/tmp"` | stdout: "/tmp" |
| E10 | No-output timeout | `command: ["sleep", "60"], noOutputTimeoutMs: 500` | Killed after 500ms silence |
| E11 | Large output (truncation) | `command: ["bash", "-c", "seq 1 100000"]` | Truncated at 100KB, truncation notice |
| E12 | Windows .cmd resolution | `command: ["npm", "--version"]` | npm resolves via .cmd shim |
| E13 | Args with spaces | `command: ["echo", "hello world", "foo bar"]` | Preserves spaces in each arg |
| E14 | Args with quotes | `command: ["echo", "it's a \"test\""]` | Handles embedded quotes |
| E15 | Empty stdout | `command: ["true"]` | "[no output]", exit 0 |
| E16 | Binary/non-UTF8 output | `command: ["bash", "-c", "printf '\\x80\\x81'"]` | Doesn't crash, output may be garbled |
| E17 | Simultaneous timeouts | `command: ["bash", "-c", "sleep 1; echo x; sleep 60"], timeoutMs: 5000, noOutputTimeoutMs: 2000` | Killed by noOutputTimeout after "x" then 2s silence |
| E18 | Stdin + command that ignores it | `command: ["echo", "hi"], input: "data"` | stdout: "hi", input harmlessly ignored |
| E19 | All optional params at once | `command: ["bash", "-c", "echo $X"], timeoutMs: 5000, noOutputTimeoutMs: 3000, input: "", env: {"X": "1"}, workingDir: "/tmp"` | stdout: "1" |

#### Valid — Background

| # | Description | Arguments | Expected |
|---|-------------|-----------|----------|
| E20 | Background launch | `command: ["sleep", "30"], background: true` | JSON with pid + status: "running" |
| E21 | Background with stdin | `command: ["cat"], background: true, input: "data"` | PID returned, stdin written before detach |
| E22 | Background with env | `command: ["bash", "-c", "echo $X > /tmp/bg.log"], background: true, env: {"X": "1"}` | PID returned, env applied |
| E23 | Background ignores timeoutMs | `command: ["sleep", "999"], background: true, timeoutMs: 100` | PID returned immediately (no timeout) |
| E24 | Background + all optional params | `command: ["cat"], background: true, input: "x", env: {"A": "1"}, workingDir: "/tmp"` | All params applied, PID returned |

#### Invalid / Edge Cases — ExecTool

| # | Description | Arguments | Expected |
|---|-------------|-----------|----------|
| E25 | Missing command | `{}` | Error: "Missing required argument: command" |
| E26 | Empty command array | `command: []` | Error: "command array must contain at least one element" |
| E27 | Command not found | `command: ["nonexistent_binary_xyz"]` | Error or non-zero exit, not a crash |
| E28 | Null in command array | `command: ["echo", null, "hi"]` | Graceful: empty string substitution or error |
| E29 | Command as string (wrong type) | `command: "echo hello"` | Error: "must be a string array" |
| E30 | Negative timeoutMs | `command: ["echo"], timeoutMs: -1` | Error: "timeoutMs must be >= 1" |
| E31 | Zero timeoutMs | `command: ["echo"], timeoutMs: 0` | Error or immediate timeout |
| E32 | Negative noOutputTimeoutMs | `command: ["echo"], noOutputTimeoutMs: -1` | Error: "noOutputTimeoutMs must be >= 1" |
| E33 | timeoutMs as string | `command: ["echo"], timeoutMs: "5000"` | Parses to int or clear error |
| E34 | background as string | `command: ["echo"], background: "true"` | Error: "must be a boolean" |
| E35 | env with non-string values | `command: ["echo"], env: {"X": 123}` | Error or coerce to string |
| E36 | env as array (wrong type) | `command: ["echo"], env: ["X=1"]` | Error: "must be an object" |
| E37 | workingDir doesn't exist | `command: ["echo"], workingDir: "/nonexistent/path"` | Error from process start, not a crash |
| E38 | workingDir as number | `command: ["echo"], workingDir: 42` | Error or coerce to string |
| E39 | Very long command arg | `command: ["echo", "<10KB string>"]` | Works or graceful error |
| E40 | Hundreds of env vars | `command: ["env"], env: {200 key-value pairs}` | Works |
| E41 | Permission denied | `command: ["/etc/shadow"]` | Non-zero exit, error message |
| E42 | Single-element command | `command: ["ls"]` | Works (args list is empty) |
| E43 | Command with absolute path | `command: ["/usr/bin/echo", "hi"]` | Works, bypasses PATH resolution |
| E44 | Command with relative path | `command: ["./script.sh"]` | Resolves relative to workingDir |
| E45 | noOutputTimeoutMs without timeoutMs | `command: ["sleep", "60"], noOutputTimeoutMs: 500` | noOutputTimeout fires, default timeoutMs still applies |

### ProcessTool Test Cases (Post-Rewrite)

#### Valid

| # | Description | Arguments | Expected |
|---|-------------|-----------|----------|
| P1 | List all processes | `action: "list"` | Table: PID, name, memory. Sorted sensibly. |
| P2 | List with name filter | `action: "list", filter: "node"` | Only processes matching "node" |
| P3 | List with limit | `action: "list", limit: 10` | Top 10 by resource usage |
| P4 | List with filter + limit | `action: "list", filter: "svc", limit: 5` | Filtered then limited |
| P5 | Status of running process | `action: "status", pid: <valid pid>` | Name, running, memory, CPU, start time |
| P6 | Status of gateway process | `action: "status", pid: <own gateway pid>` | Shows BotNexus process info |
| P7 | Kill a disposable process | `action: "kill", pid: <test sleep process>` | Terminated, confirmation |
| P8 | Kill already exited process | `action: "kill", pid: <exited pid>` | Graceful: "already exited" or "not found" |
| P9 | List shows exec'd background proc | 1. `exec(["sleep", "300"], bg: true)` -> PID X  2. `process(action: "list", filter: "sleep")` | PID X appears in list |

#### Invalid / Edge Cases — ProcessTool

| # | Description | Arguments | Expected |
|---|-------------|-----------|----------|
| P10 | Missing action | `{}` | Error: "action is required" |
| P11 | Invalid action | `action: "restart"` | Error: "Unknown action. Valid: list, status, kill" |
| P12 | Status without pid | `action: "status"` | Error: "pid is required" |
| P13 | Kill without pid | `action: "kill"` | Error: "pid is required" |
| P14 | Non-existent PID | `action: "status", pid: 9999999` | Error: "No process with PID 9999999" |
| P15 | PID zero | `action: "status", pid: 0` | Error or system idle process |
| P16 | Negative PID | `action: "status", pid: -1` | Error: invalid PID |
| P17 | PID as string | `action: "status", pid: "1234"` | Parses to int or clear error |
| P18 | Kill system process | `action: "kill", pid: 4` | Error: access denied (graceful, no crash) |
| P19 | Kill own process | `action: "kill", pid: <own pid>` | Refuse or warn — don't self-terminate |
| P20 | Empty filter | `action: "list", filter: ""` | Same as no filter |
| P21 | Filter matching nothing | `action: "list", filter: "zzz_no_match_zzz"` | Empty list, not an error |
| P22 | Limit zero | `action: "list", limit: 0` | Empty list or error |
| P23 | Negative limit | `action: "list", limit: -5` | Error or treated as no limit |
| P24 | Very large PID | `action: "status", pid: 2147483647` | Error: no such process |
| P25 | Filter with special chars | `action: "list", filter: "*.exe"` | Handled safely, no regex injection |
| P26 | Action as number | `action: 42` | Error: "action must be a string" |
| P27 | Rapid kill then status | 1. `kill(pid: X)` 2. `status(pid: X)` | Kill succeeds, status shows exited/not found |
| P28 | PID as float | `action: "status", pid: 1234.5` | Error or truncate to int |
| P29 | Extra unknown arguments | `action: "list", foo: "bar"` | Ignored, no error |

### Cross-Tool Workflow Tests

Validate the agent experience across both independent tools:

| # | Description | Steps | Expected |
|---|-------------|-------|----------|
| W1 | Launch and find | 1. `exec(["sleep", "300"], bg: true)` -> PID  2. `process("list", filter: "sleep")` | PID visible |
| W2 | Launch and kill | 1. `exec(["sleep", "300"], bg: true)` -> PID  2. `process("kill", pid)` | Killed |
| W3 | Full lifecycle | 1. exec bg -> PID  2. process status -> running  3. process kill -> killed  4. process status -> not found | Clean lifecycle |
| W4 | Foreground invisible | 1. `exec(["echo", "hi"])` (fg)  2. `process("list")` | echo not in list (already exited) |
| W5 | Output via redirect | 1. `exec(["bash", "-c", "echo hello > /tmp/test.log"], bg: true)`  2. `bash("cat /tmp/test.log")` | "hello" |
| W6 | Kill non-exec process | 1. Find PID from `process("list")`  2. `process("kill", pid)` | Works — process manages ANY OS process |
| W7 | Multiple background | 1. exec bg -> PID1  2. exec bg -> PID2  3. process list | Both visible |

## Impact

- **ExecTool**: Dead code removal + handle fix (~10 lines net change)
- **ProcessTool**: Full rewrite but simpler — delete 2 files, rewrite 1 against OS API
- **Risk**: Low — both tools are currently non-functional for their intended purpose, so any change is an improvement
- **Agent impact**: None — process tool output/input actions never worked anyway
