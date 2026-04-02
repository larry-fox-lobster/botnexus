### 2026-04-02: Squad lifecycle skill extraction

**By:** Kif (Documentation Engineer)
**What:** Created `.squad/skills/squad-lifecycle/SKILL.md` — extracted ~40% of squad.agent.md (init mode, casting, member management, integration flows, worktree lifecycle, format references) into a dedicated skill file. The coordinator now loads this content on-demand instead of every session.
**Why:** The coordinator agent file was 946 lines. Roughly 40% was first-time setup and lifecycle content that loaded every session but is only needed when `.squad/` needs initialization or roster changes. Extracting it into a skill reduces coordinator context cost and improves session start time. The live agent file already had a pointer at line 25: `Read .squad/skills/squad-lifecycle/SKILL.md for the full setup flow.`
**Impact:** Coordinator context window freed up for operational content. Setup instructions unchanged — faithfully preserved, not summarized.
