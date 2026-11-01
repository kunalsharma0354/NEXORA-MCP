---
id: mcp-server-engineering
name: MCP Server Engineering
version: 1.0.0
description: Implement maintainable Model Context Protocol servers with clear tool contracts, validation, bounded outputs, and safe capability boundaries.
category: mcp-and-agents
tags: [mcp, tools, schemas, protocol, agents]
---

# MCP Server Engineering

Implement maintainable Model Context Protocol servers with clear tool contracts, validation, bounded outputs, and safe capability boundaries.

## When to use
Use when building or changing MCP servers, tool definitions, resources, prompts, or MCP client integration.

## Workflow
1. Inspect the project's MCP SDK and version, transport, server lifecycle, configuration, and existing tool conventions.
2. Define narrow tools with clear names, descriptions, input schemas, output contracts, and predictable errors.
3. Validate all arguments at runtime; JSON Schema alone is not a substitute for server-side validation.
4. Bound result sizes, pagination, execution time, and resource consumption.
5. Separate read-only discovery from state-changing or execution tools.
6. Return structured content when supported and useful; include concise human-readable text where appropriate.
7. Keep tool output relevant to the task. Avoid returning whole repositories or giant registries when a query will do.
8. Handle cancellation, timeouts, partial failure, and server shutdown according to the SDK.
9. Test tools through the actual MCP transport/client path, not only direct function calls.
10. Document client setup, permissions, expected inputs, outputs, and limitations.

## Tool design
- One tool should have a coherent responsibility.
- Use stable identifiers and version-aware behavior.
- Reject unknown fields when compatible with the client's schema handling.
- Avoid ambiguous booleans or overloaded parameters.
- Make side effects obvious in the tool description.
- Do not claim an action completed until its result confirms completion.

## Security
Treat tool arguments and retrieved content as untrusted. Apply least privilege, authorization, path allowlists, secret redaction, and explicit approval for consequential actions.

## Compatibility
Do not assume all MCP clients handle resources, prompts, structured output, or tool annotations identically. Verify the actual target client and protocol/SDK support.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
