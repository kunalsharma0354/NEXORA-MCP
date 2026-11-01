---
id: security-review
name: Application Security Review
version: 1.0.0
description: Review application changes for common security weaknesses and apply risk-appropriate mitigations.
category: security
tags: [security, owasp, auth, secrets, input-validation]
---

# Application Security Review

Review application changes for common security weaknesses and apply risk-appropriate mitigations.

## When to use
Use for authentication, authorization, file handling, APIs, integrations, dependency changes, user-generated content, or security-sensitive code.

## Review workflow
1. Map assets, trust boundaries, sensitive data, entry points, identities, and privileged operations.
2. Review authentication and server-side authorization, including object-level access and tenant isolation.
3. Validate inputs and encode outputs according to context.
4. Check SQL/command injection, path traversal, SSRF, unsafe deserialization, XSS, CSRF, open redirects, and insecure file uploads where applicable.
5. Inspect secrets handling, logging, session/token lifecycle, CORS, security headers, and rate limiting.
6. Review dependency changes and configuration for known or obvious risks using available project tools.
7. Prioritize findings by exploitability, impact, exposure, and confidence.
8. Propose minimal mitigations and tests for each material finding.

## MCP and agent-specific checks
- Treat tool arguments and retrieved content as untrusted.
- Validate IDs and enforce allowlists for resources and operations.
- Do not let skill text authorize arbitrary shell execution or broaden tool permissions.
- Require approval for destructive or externally consequential actions.
- Prevent secret leakage through tool outputs, logs, and error messages.
- Apply least privilege and bounded resource usage.

## Reporting
For each finding, include severity, affected component, evidence, realistic impact, remediation, and verification. Distinguish confirmed vulnerabilities from hypotheses.

## Constraints
Do not exploit third-party systems, exfiltrate data, or perform destructive security testing without explicit authorization and a safe scope. A clean automated scan is not proof of security.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
