---
id: accessibility-inclusive-ui
name: Accessibility & Inclusive UI
version: 1.0.0
description: Improve accessibility using semantic structure, keyboard support, understandable forms, and practical checks.
category: accessibility
tags: [accessibility, a11y, wcag, keyboard, forms]
---

# Accessibility & Inclusive UI

Improve accessibility using semantic structure, keyboard support, understandable forms, and practical checks.

## When to use
Use for UI implementation/review, forms, navigation, dialogs, interactive widgets, and responsive interfaces.

## Implementation checklist
- Use semantic landmarks, meaningful heading order, buttons for actions, and links for navigation.
- Give form controls persistent accessible names; associate help text and errors programmatically.
- Ensure keyboard access and visible focus for all interactive elements.
- Manage focus correctly for dialogs, menus, route changes, and validation failures.
- Use appropriate ARIA only when native HTML semantics are insufficient.
- Provide text alternatives for meaningful images and empty alt for decorative images.
- Do not convey meaning through color alone.
- Check text/background contrast and avoid tiny or cramped interaction targets.
- Respect reduced-motion preferences and avoid flashing content.
- Make tables, status messages, and dynamic updates understandable to assistive technology.

## Testing
Use automated accessibility checks where available, then manually test keyboard navigation and key screen-reader semantics. Automated tools detect only a subset of issues.

## Workflow
Inspect existing components and design tokens, fix shared primitives first, then validate representative pages and critical user flows. Add regression tests where supported.

## Avoid
Adding ARIA roles that conflict with native semantics, removing outlines without replacement, or claiming WCAG conformance without an appropriate audit.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
