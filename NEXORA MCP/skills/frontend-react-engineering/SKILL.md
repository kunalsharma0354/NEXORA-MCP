---
id: frontend-react-engineering
name: Frontend & React Engineering
version: 1.0.0
description: Build reliable component-driven interfaces with predictable state, accessible interaction, and maintainable rendering logic.
category: frontend-development
tags: [frontend, react, components, state, typescript]
---

# Frontend & React Engineering

Build reliable component-driven interfaces with predictable state, accessible interaction, and maintainable rendering logic.

## When to use
Use for React or component-based frontend work when the repository confirms the relevant framework.

## First inspect
Read package scripts, framework/version, routing, styling approach, state/data libraries, lint rules, and existing component conventions. Do not introduce React if the project uses another stack.

## Design and implementation
- Define component responsibilities and keep components cohesive.
- Reuse existing design tokens, primitives, and patterns before adding dependencies.
- Keep state as local as possible; lift it only when multiple components genuinely need it.
- Derive values from source state instead of maintaining duplicate state.
- Separate server state from transient UI state where the existing stack supports that distinction.
- Handle loading, empty, error, retry, disabled, and success states.
- Avoid unnecessary effects, stale closures, uncontrolled side effects, and render-time mutations.
- Use stable keys and correct form semantics.
- Type props, API responses, and state meaningfully when TypeScript is present; avoid broad `any` escapes.
- Do not suppress lint/type errors without explaining and narrowly containing the reason.

## Accessibility and responsive behavior
- Use semantic HTML and native controls where possible.
- Ensure keyboard operation, visible focus, labels, error associations, and sensible focus order.
- Check narrow mobile widths, zoom, long text, and empty/loading states.
- Do not communicate status through color alone.

## Verification
- Add tests for important interaction/state transitions.
- Run existing lint, type-check, and build scripts.
- If browser tooling is available, verify key user flows at desktop and mobile viewport sizes.
- Confirm no console errors or failed requests related to the change when those signals are available.

## Avoid
- Rewriting the whole UI for a small feature.
- Adding state libraries or component libraries without a concrete need.
- Clickable-looking elements with no action.
- Index keys for reorderable collections.
- Hardcoded production data masquerading as live API results.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
