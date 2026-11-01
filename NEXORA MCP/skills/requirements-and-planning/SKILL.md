---
id: requirements-and-planning
name: Requirements & Delivery Planning
version: 1.0.0
description: Turn ambiguous requests into scoped, testable requirements and an implementation plan grounded in the existing repository.
category: general-development
tags: [requirements, planning, acceptance-criteria, scope]
---

# Requirements & Delivery Planning

Turn ambiguous requests into scoped, testable requirements and an implementation plan grounded in the existing repository.

## When to use
Use at the start of a feature, application, refactor, bug fix, or change request when requirements are incomplete or work spans multiple files.

## Objective
Translate the user's request into a practical plan without inventing requirements or overengineering.

## Workflow
1. Restate the desired outcome in concrete terms.
2. Inspect the repository before proposing implementation details: entry points, framework, routing, data model, tests, conventions, and existing related functionality.
3. Separate confirmed requirements, reasonable assumptions, and open questions. Ask only when an unresolved choice materially changes the solution or creates risk; otherwise choose a conservative default and state it.
4. Identify user journeys and edge cases. Define acceptance criteria that can be observed or tested.
5. Split work into small, dependency-ordered steps. Prefer modifying existing patterns over introducing parallel systems.
6. Identify affected modules, API contracts, persistence changes, permissions, migration needs, and backward-compatibility risks.
7. Define validation commands based on scripts and tooling actually present in the repository.
8. Keep scope aligned to the request. Put unrelated improvements in a separate follow-up list.

## Deliverables
- Goal and scope
- Assumptions and unresolved decisions
- Acceptance criteria
- Ordered implementation plan
- Risks and verification plan

## Quality gates
- Each acceptance criterion is observable and unambiguous.
- No framework, database, library, or route is assumed without repository evidence.
- Existing behavior and user data are accounted for.
- Plan includes failure states, security considerations, and tests.
- Do not claim work is implemented merely because a plan exists.

## Avoid
- Giant rewrites when incremental changes suffice.
- Premature microservices or abstractions.
- Treating a visual mockup as a functioning feature.
- Hiding uncertainty or silently changing user-facing behavior.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
