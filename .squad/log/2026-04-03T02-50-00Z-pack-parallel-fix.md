# Session Log: pack.ps1 Parallel Publish Fix

**Date:** 2026-04-03T02:50:00Z  
**Agent:** Leela (Lead)  
**Status:** ✅ Complete

## Facts
- **Problem:** pack.ps1 --no-restore parallelization was failing due to obj/ contention
- **Root Cause:** --no-restore still triggered builds; multiple tasks wrote to same directory
- **Fix:** Build once upfront, then parallel publishes with --no-build (I/O only)
- **Parallelism:** Bumped to 8 workers
- **Commit:** 5f4b0bc
- **Result:** Stable parallel publishing
