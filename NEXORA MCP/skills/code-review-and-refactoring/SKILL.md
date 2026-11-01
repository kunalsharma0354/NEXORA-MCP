---
id: code-review-and-refactoring
name: Code Review & Safe Refactoring
version: 1.0.0
description: Review code for correctness and maintainability, and refactor incrementally while preserving externally observable behavior.
category: code-quality
tags: [code-review, refactoring, maintainability, diff]
---

# Code Review & Safe Refactoring

Review code for correctness and maintainability, and refactor incrementally while preserving externally observable behavior.

## When to use
Use for pull request review, cleanup, architecture improvements, or refactoring existing modules.

## Review priorities
1. Correctness and regression risk.
2. Security, privacy, and authorization.
3. Data integrity and failure handling.
4. API compatibility and migration impact.
5. Performance and resource usage.
6. Maintainability, clarity, and test quality.

## Workflow
- Read the surrounding code and call sites before judging a change.
- Trace behavior through relevant layers.
- Identify concrete defects with file/line references where available.
- Distinguish confirmed bugs from style preferences and speculative concerns.
- Explain impact and reproduction conditions.
- For refactoring, define behavior to preserve and add tests before broad changes when feasible.
- Make small coherent edits and review the resulting diff.
- Run targeted tests and relevant broader checks.

## Refactoring rules
- Preserve public interfaces unless change is requested and migration is handled.
- Avoid unrelated formatting churn.
- Do not create abstractions without repeated or meaningful complexity.
- Remove dead code only when usage and generated/runtime references are understood.
- Keep behavior changes separate from mechanical refactors where practical.

## Output
For reviews, lead with actionable findings ordered by severity, then questions and a concise summary. If no findings are identified, state scope and limitations rather than claiming the code is defect-free.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
