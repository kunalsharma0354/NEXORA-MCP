---
id: debugging-root-cause
name: Debugging & Root-Cause Analysis
version: 1.0.0
description: Diagnose defects systematically, isolate root causes, make minimal fixes, and prevent regressions.
category: debugging
tags: [debugging, errors, logs, root-cause, regression]
---

# Debugging & Root-Cause Analysis

Diagnose defects systematically, isolate root causes, make minimal fixes, and prevent regressions.

## When to use
Use when behavior is incorrect, tests fail, an exception occurs, or a regression is reported.

## Diagnostic workflow
1. Reproduce the issue and define expected versus actual behavior.
2. Gather relevant stack traces, logs, browser console/network details, test output, recent changes, and environment context.
3. Form a small set of hypotheses and test them one at a time.
4. Trace data/control flow across boundaries; identify the earliest point where behavior diverges.
5. Distinguish root cause from downstream symptoms.
6. Implement the smallest safe fix that addresses the root cause.
7. Add a regression test that would fail before the fix.
8. Run targeted and related tests; inspect the diff for unintended changes.
9. Report root cause, fix, verification, and residual uncertainty.

## Engineering practices
- Prefer reproducible evidence over guesswork.
- Avoid changing multiple unrelated variables simultaneously.
- Do not suppress errors, add broad fallbacks, or swallow exceptions just to hide symptoms.
- Preserve useful error context while redacting secrets.
- Check race conditions, stale state, boundary inputs, timezones, concurrency, and configuration when relevant.

## If reproduction fails
Document the exact attempted steps and environment. Use logs or static analysis to narrow the issue, but label the root cause as unconfirmed until evidence supports it.

## Avoid
Unrelated refactors during urgent fixes, deleting failing tests without justification, or claiming a fix works when no relevant verification ran.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
