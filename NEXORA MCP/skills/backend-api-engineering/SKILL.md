---
id: backend-api-engineering
name: Backend & API Engineering
version: 1.0.0
description: Build robust API and service boundaries with validation, authorization, predictable errors, and observable behavior.
category: backend-development
tags: [backend, api, validation, authorization, http]
---

# Backend & API Engineering

Build robust API and service boundaries with validation, authorization, predictable errors, and observable behavior.

## When to use
Use for HTTP APIs, service logic, server actions, webhooks, or backend refactoring.

## Workflow
1. Inspect routing, middleware, authentication, service boundaries, error conventions, and existing API contracts.
2. Define request/response shapes and expected status/error behavior before implementation.
3. Validate untrusted inputs at the boundary with the project's existing validation library or framework.
4. Authenticate the caller and authorize the specific resource/action server-side. Never rely on UI visibility for access control.
5. Keep business rules in reusable service/domain code rather than duplicating them across routes.
6. Use parameterized queries or safe ORM APIs. Apply transactions where multiple writes must succeed or fail together.
7. Add pagination and bounded limits to collection endpoints.
8. Handle timeouts, upstream failures, retries, and idempotency where appropriate.
9. Return safe client errors; avoid leaking stack traces, secrets, SQL, or internal configuration.
10. Add contract, integration, and failure-path tests.

## API quality
- Use consistent naming and response conventions.
- Distinguish invalid input, unauthenticated, unauthorized, missing resource, conflict, rate limiting, and unexpected server failure.
- Do not return more personal or sensitive data than needed.
- Make destructive operations explicit and protect against accidental duplicate submissions.
- Keep logs structured and redact tokens, credentials, and sensitive payloads.

## Verification
Use repository scripts and test infrastructure. Test invalid input, missing identity, insufficient permissions, not-found cases, conflict cases, upstream failure, and normal success.

## Avoid
- Trusting client-supplied roles or ownership claims.
- Returning success before persistence completes.
- Broad exception catches that hide failure.
- Retrying non-idempotent actions blindly.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
