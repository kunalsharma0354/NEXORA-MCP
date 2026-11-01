---
id: prompt-specification-engineering
name: Prompt & Specification Engineering
version: 1.0.0
description: Transform vague user requests into precise, implementation-ready specifications without changing intent or inventing facts.
category: ai-and-prompts
tags: [prompt-engineering, specification, acceptance-criteria, ai]
---

# Prompt & Specification Engineering

Transform vague user requests into precise, implementation-ready specifications without changing intent or inventing facts.

## When to use
Use when the request is underspecified, intended for a coding agent, or needs clear implementation and verification instructions.

## Workflow
1. Preserve the user's goal and constraints.
2. Inspect project context before selecting stack-specific details.
3. Extract desired behavior, users, inputs, outputs, edge cases, and constraints.
4. Separate facts, assumptions, and unresolved questions.
5. Turn subjective goals into actionable guidance without pretending every preference is objectively measurable.
6. Define acceptance criteria and verification steps.
7. Include existing-code preservation, security, accessibility, and error states when relevant.
8. Keep the specification proportional to task complexity.
9. Avoid contradictory instructions and redundant role-play language.
10. Present the final specification in a copyable structure.

## For coding tasks
Include repository inspection, implementation boundaries, expected behavior, relevant integration points, testing, and a completion report. Do not dictate a framework unless the repository or user requires it.

## Quality check
- Is the intended outcome preserved?
- Are requirements testable where possible?
- Are assumptions visible?
- Are unsafe or destructive actions gated?
- Is the prompt concise enough to be followed but complete enough to prevent ambiguity?

## Avoid
Prompt bloat, repeated “act as an expert” filler, false guarantees, invented project details, and instructions to claim success regardless of evidence.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
