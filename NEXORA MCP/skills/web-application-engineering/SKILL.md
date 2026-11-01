---
id: web-application-engineering
name: Production Web Application Engineering
version: 1.0.0
description: Implement maintainable full-stack web features that fit the existing codebase and have clear runtime behavior.
category: web-development
tags: [web, full-stack, architecture, implementation]
---

# Production Web Application Engineering

Implement maintainable full-stack web features that fit the existing codebase and have clear runtime behavior.

## When to use
Use for new pages, full-stack features, application scaffolding within an existing project, or significant web application changes.

## Operating principles
- Inspect the existing repository and preserve its framework, conventions, dependencies, and architecture unless a change is necessary and justified.
- Trace the complete path: UI → client state → API/action → authorization → business logic → persistence → response/error UI.
- Keep boundaries clear: presentation, domain logic, data access, and integration code should not be unnecessarily entangled.
- Prefer small, cohesive modules and established project patterns over speculative abstractions.
- Implement real behavior; do not leave fake data, placeholder handlers, or dead buttons in production paths.

## Implementation workflow
1. Map the existing app structure and identify reusable components/services.
2. Convert requirements into user journeys and acceptance criteria.
3. Design states for loading, success, empty results, validation errors, permission denial, network failure, and retry where appropriate.
4. Implement the smallest coherent vertical slice end to end.
5. Validate inputs at trust boundaries and enforce authorization server-side.
6. Keep secrets and privileged operations on the server.
7. Add or update unit, integration, and end-to-end tests at the appropriate layers.
8. Run the repository's actual lint, type-check, build, and test scripts when available.
9. Review changed files for regressions, accessibility, security, and unnecessary complexity.

## Production checklist
- Correct behavior for normal and edge cases.
- Accessible semantic UI and responsive layouts.
- Explicit error handling; no silent failures.
- Stable API contracts and appropriate HTTP/status semantics.
- No secrets, tokens, or personal data in logs.
- Safe configuration and documented environment variables.
- Database changes are migration-safe and preserve existing data.
- Tests cover important behavior and regression cases.
- Documentation reflects actual implementation.

## Constraints
Do not claim production readiness based only on a successful build. State what was tested, what was not tested, and external dependencies that remain unverified.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
