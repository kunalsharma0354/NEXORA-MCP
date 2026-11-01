# Bug Fixing & Minimal-Diff Engineering

## Mission

Fix the actual bug with the smallest safe, clear, maintainable change. Prefer a correct two-line fix over a needless fifty-line rewrite **when both are equally safe and correct**. Do not optimize for line count at the expense of correctness, security, readability, or future maintainability.

## Required workflow

1. **Understand the report**
   - Identify expected behavior, actual behavior, reproduction steps, relevant inputs, and error output.
   - If essential information is missing, inspect the repository and gather evidence before asking questions.
   - Never invent logs, test results, or a reproduction.

2. **Inspect before editing**
   - Read the relevant implementation, its callers, nearby tests, types, and project conventions.
   - Check the current diff and avoid overwriting unrelated user changes.
   - Trace the data/control flow far enough to identify the cause, not merely the visible symptom.

3. **Form a root-cause hypothesis**
   - State internally what condition causes the failure and what evidence supports it.
   - Prefer the narrowest explanation consistent with the evidence.
   - If there are multiple plausible causes, run a targeted test or inspect more evidence instead of guessing.

4. **Choose the smallest safe fix**
   - Make the minimum change that fully addresses the root cause.
   - Reuse existing helpers, patterns, dependencies, and abstractions when appropriate.
   - Do not add a new framework, dependency, configuration layer, generic utility, or abstraction for a one-off problem unless it is genuinely necessary.
   - Do not rewrite a working module, rename unrelated things, reformat entire files, or refactor unrelated code during a bug fix.
   - Avoid duplicate checks and defensive code that cannot occur under the established contracts.
   - Keep a short fix short when that is clear. Do not artificially compress code if a slightly longer version is safer or easier to understand.
   - If a tiny patch would only hide the symptom, violate an invariant, weaken validation, or create a security hole, implement the smallest complete fix instead.

5. **Protect behavior**
   - Preserve public APIs, data formats, existing behavior, and architecture unless the bug requires a deliberate change.
   - Consider edge cases, async behavior, race conditions, error paths, input validation, and resource cleanup where relevant.
   - Do not suppress exceptions, disable tests, loosen types, remove assertions, or bypass validation merely to make an error disappear.
   - Never hardcode a workaround for a single example when the underlying rule can be fixed correctly.

6. **Verify**
   - Run the narrowest relevant test first, then related tests or type/lint checks when practical.
   - Add or update a regression test when it provides meaningful protection and fits the project.
   - If the environment cannot run a check, say so clearly; do not claim it passed.
   - Inspect the final diff for unrelated edits, unnecessary lines, accidental secrets, and new failure paths.

7. **Report clearly**
   - Summarize the root cause in plain language.
   - List the files changed and why.
   - Report the exact checks run and their real outcomes.
   - Mention remaining uncertainty or unrun checks.
   - Keep the explanation concise; do not produce a long essay for a small patch.

## Decision rule: minimal vs. complete

Before finalizing, ask:

- Does this change fix the root cause rather than just hide the symptom?
- Is every added line necessary for correctness, safety, clarity, or testing?
- Could an existing helper or a smaller local change solve it just as well?
- Did I avoid unrelated refactoring and new dependencies?
- Have I checked the relevant regression risk?

If a two-line change safely fixes the root cause, use it. If a two-line workaround merely masks the issue, do not use it. The target is **minimum sufficient change**, not minimum line count.

## When the bug is unclear

Do not make speculative edits across multiple files. Gather more evidence, create a focused reproduction if possible, inspect logs and call sites, and then choose the smallest evidence-backed fix. If uncertainty remains consequential, explain it and ask one focused question.

## Repository-specific rules

- Follow the repository's existing language, framework, naming, formatting, and test conventions.
- Read relevant project instructions such as `AGENTS.md`, `CONTRIBUTING.md`, or package scripts when present.
- Do not assume a specific package manager or test command; inspect the project.
- Preserve user changes and never reset or overwrite unrelated work.
- Ask before destructive operations or broad migrations.

## Expected output format

**Cause:** one or two sentences.

**Fix:** what changed and why this is the smallest complete fix.

**Verification:** commands/tests actually run and their outcomes.

**Caveats:** only if there are meaningful limitations or remaining risks.
