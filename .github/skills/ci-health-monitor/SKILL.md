---
name: ci-health-monitor
description: >-
  Monitor dotnet/sdk public CI builds on main, investigate failures, categorize
  them as build breaks or intermittent issues, check for existing tracking, and
  produce a structured report with root causes, causal PRs, Known Build Error
  JSON drafts, and recommended fixes. Use when investigating CI health, triaging
  build failures, or driving down intermittent test failures.
---

# CI Health Monitor

Investigate recent build failures on the dotnet/sdk public CI pipeline and
produce a structured report with findings and recommended actions.

## Pipeline Details

- **Organization**: dnceng-public
- **Project**: public
- **Pipeline definition ID**: 101
- **Branch**: refs/heads/main
- **Pipeline URL**: https://dev.azure.com/dnceng-public/public/_build?definitionId=101

## Workflow

### Step 1: Determine Time Window

Query the session store to find the last completed CI Health Monitor session:

```sql
SELECT created_at FROM sessions
WHERE summary ILIKE '%CI Health Monitor%'
ORDER BY created_at DESC LIMIT 1
```

- If found, investigate builds completed after that timestamp.
- If not found (first run), look back 12 hours from now.

### Step 2: Query Recent Builds

Use `hlx-azdo_builds` with:
- `definitionId`: 101
- `org`: "dnceng-public"
- `project`: "public"
- `branch`: "refs/heads/main"
- `status`: "completed"

Filter results to builds within the time window. Identify builds with a
`failed` result.

If there are no failed builds in the window, report "All clear — no failures
in the time window" and stop.

### Step 3: Investigate Each Failed Build

For each failed build, use these tools in order:

1. `hlx-azdo_build` — get build details (result, timing, source version)
2. `hlx-azdo_build_analysis` — check if known issues already match this failure
3. `hlx-azdo_timeline` — find failed stages/jobs/tasks
4. `hlx-azdo_search_log` — search failed step logs for error messages
5. `hlx-azdo_changes` — find commits/PRs included in the build
6. `hlx-azdo_helix_jobs` — if test failures are involved, get Helix job IDs
7. `hlx-helix_status` — get pass/fail summary for Helix jobs
8. `hlx-helix_logs` — get console logs for failed Helix work items

### Step 4: Categorize Each Failure

Classify every failure into exactly one category:

| Category | Criteria |
|----------|----------|
| **Build Break** | Compiler error, MSBuild task failure, or deterministic failure traceable to a specific commit/PR. The same failure reproduces on every run with that code. |
| **Intermittent Failure** | Test or build step that fails non-deterministically. Same code passes on other recent builds or on retry. |
| **Infrastructure** | AzDO/Helix platform issue — network timeout, machine unavailable, disk full, agent crash unrelated to repo code. |
| **Already Tracked** | Build Analysis flagged it as matching an existing Known Build Error issue. |

To distinguish intermittent from deterministic:
- Check if the same pipeline definition has recent *passing* builds at the same
  commit or a nearby commit.
- Check if `hlx-azdo_build_analysis` already flagged it as a known issue.
- Check if the failure is in a test (more likely intermittent) vs. compilation
  (more likely a break).

### Step 5: Gather Details for Each Failure

For every failure, collect:

- **Error signature**: The specific error message or log output that identifies this failure.
- **Root cause analysis**: What went wrong and why.
- **Causal commit/PR**: For build breaks, identify which PR or commit introduced the problem using `hlx-azdo_changes` and correlating with the error.
- **Existing tracking**: Search for existing issues:
  - Use `mihubot-search_dotnet_repos` with the error signature to find related issues/PRs.
  - Check the Known Build Error issues: https://github.com/dotnet/sdk/issues?q=state:open+label:"Known+Build+Error"
- **Recommended action**: What fix would resolve this, with code pointers if possible.

### Step 6: Draft Known Build Error JSON (Intermittent Failures Only)

For intermittent failures that are NOT already tracked by an existing Known
Build Error issue, draft the JSON blob per the
[arcade Known Issue format](https://github.com/dotnet/arcade/blob/main/Documentation/Build%20Analysis/KnownIssueJsonStepByStep.md):

```json
{
  "ErrorMessage": "<substring match, case-insensitive>",
  "BuildRetry": false,
  "ExcludeConsoleLog": false
}
```

Or for regex patterns:

```json
{
  "ErrorPattern": "<regex, case-insensitive, no backtracking>",
  "BuildRetry": false,
  "ExcludeConsoleLog": false
}
```

Guidelines for crafting the pattern:
- Use `ErrorMessage` (simple contains) when the error text is stable.
- Use `ErrorPattern` (regex) when parts of the message vary (paths, versions, class names).
- The match is **single-line** and **case-insensitive**.
- Escape JSON special characters (backslashes, quotes).
- Be specific enough to avoid false matches but general enough to catch all variants.
- Set `BuildRetry: true` only for transient infrastructure-like failures (network, timeout) where a retry is likely to succeed.
- Set `ExcludeConsoleLog: true` if the error only appears in AzDO build logs, not Helix console logs.

### Step 7: Produce the Report

Structure the final output as follows:

---

# CI Health Report — {date/time}

**Time window**: {start} to {end}
**Builds analyzed**: {total count}
**Failed builds**: {failed count}

## Build Breaks

For each build break:
- **Build**: [link to build]
- **Error**: One-line summary
- **Causal PR/commit**: [link] by @author
- **Existing issue**: [link] or "None found"
- **Recommended fix**: Description with code pointers

## Intermittent Failures

For each intermittent failure:
- **Build**: [link to build]
- **Error signature**: The key error text
- **Frequency**: How many times seen in window / how many builds
- **Existing Known Build Error**: [link] or "None found"
- **Draft Known Build Error JSON**:
  ```json
  { ... }
  ```
- **Recommended fix approach**: Description with code pointers

## Infrastructure Issues

For each infra issue:
- **Build**: [link to build]
- **Description**: Brief explanation
- **Action needed**: Transient (ignore) or needs attention

## Already Tracked

List failures that matched existing Known Build Error issues (no action needed).

## Summary & Recommendations

Priority-ordered list of recommended actions, most impactful first.

---

## Important Guidelines

- **Always check for existing issues** before recommending new ones. Do not
  recommend duplicates.
- **Link to specifics**: build URLs, log lines, source code locations.
- **Be precise about error patterns** — they may be used directly for Known
  Build Error matching.
- **Do not create issues or PRs** unless explicitly instructed to do so. This
  skill produces a report for human review.
- When recommending fixes, include the file path, relevant code, and a
  description of the change needed.
