---
id: testing-quality-assurance
name: Testing & Quality Assurance
version: 1.0.0
description: Create a risk-based test strategy and verify user-visible behavior with reliable, reproducible evidence.
category: testing
tags: [testing, qa, unit-tests, integration-tests, e2e]
---

# Testing & Quality Assurance

Create a risk-based test strategy and verify user-visible behavior with reliable, reproducible evidence.

## When to use
Use for new features, bug fixes, refactors, release readiness, or regression investigation.

## Strategy
Choose the smallest useful test at each layer:
- Unit tests for isolated business rules and edge cases.
- Integration tests for module/API/database boundaries.
- End-to-end tests for critical user journeys.
- Contract tests when independent systems must agree on schemas.
- Visual regression checks only when screenshot stability and value justify them.

## Workflow
1. Inspect existing test framework, fixtures, scripts, conventions, and CI.
2. Derive tests from acceptance criteria and failure risks.
3. Include success, validation, authorization, empty, loading, timeout, and failure scenarios as relevant.
4. Make tests deterministic: isolate state, control time/randomness/network where appropriate, and clean up resources.
5. Avoid assertions that merely duplicate implementation details.
6. Run targeted tests first, then relevant broader checks.
7. Record exact commands and actual outcomes.
8. Investigate failures; do not weaken assertions or skip tests merely to obtain a green result.

## Browser tests
- Use stable role/label/text locators where practical.
- Wait for meaningful state, not arbitrary sleeps.
- Verify outcomes, not just clicks.
- Capture useful logs/screenshots on failure without leaking sensitive data.
- Keep tests independent and clean up created records.

## Reporting
Separate passed, failed, skipped, and not-run checks. Include reproducible failure details and known environment limitations.

## Avoid
- Fake test results.
- Tests that depend on production data.
- Arbitrary timeouts as a substitute for synchronization.
- Treating a successful build as proof that the feature works.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
