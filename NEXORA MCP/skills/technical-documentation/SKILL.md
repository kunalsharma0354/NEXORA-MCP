---
id: technical-documentation
name: Technical Documentation & Developer Experience
version: 1.0.0
description: Create accurate setup, architecture, API, and troubleshooting documentation that matches the actual implementation.
category: documentation
tags: [documentation, readme, architecture, developer-experience]
---

# Technical Documentation & Developer Experience

Create accurate setup, architecture, API, and troubleshooting documentation that matches the actual implementation.

## When to use
Use when adding features, changing setup, exposing APIs/tools, or improving onboarding.

## Workflow
1. Inspect the actual code, scripts, configuration, and public interfaces.
2. Identify the intended reader and the task they need to complete.
3. Document prerequisites, installation, configuration, startup, verification, and common failures.
4. Explain architecture through boundaries and data flow; avoid documenting speculative internals.
5. Provide minimal working examples with correct names and valid inputs.
6. Document errors, permissions, limitations, and security considerations for APIs and MCP tools.
7. Keep examples consistent with actual schemas and current code.
8. Verify commands and links when the environment permits.
9. Update docs alongside behavior changes.

## Quality rules
- Distinguish required from optional configuration.
- Use placeholders for secrets; never include real credentials.
- Explain what a command does and whether it changes data.
- State when an example is illustrative rather than executable.
- Avoid duplicated documentation that will drift.

## Avoid
Invented flags, unsupported promises, inaccurate architecture diagrams, and claims of successful setup without verification.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
