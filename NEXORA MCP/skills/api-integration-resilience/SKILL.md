---
id: api-integration-resilience
name: External API & Integration Reliability
version: 1.0.0
description: Integrate external services with explicit contracts, secure credentials, timeouts, retries, and observable failures.
category: integrations
tags: [api, integration, timeouts, retries, webhooks]
---

# External API & Integration Reliability

Integrate external services with explicit contracts, secure credentials, timeouts, retries, and observable failures.

## When to use
Use for third-party APIs, OAuth integrations, webhooks, external model providers, and service-to-service calls.

## Workflow
1. Inspect provider docs, existing client abstractions, authentication method, rate limits, and versioning.
2. Define the request/response contract and validate provider responses at runtime.
3. Keep credentials in the project's secret/configuration mechanism; never hardcode or log them.
4. Set explicit timeouts and bounded response sizes.
5. Retry only transient failures and honor Retry-After/rate limits where supported. Use backoff with jitter and a maximum attempt budget.
6. Avoid retrying non-idempotent operations unless the provider supports idempotency or the outcome is safely reconciled.
7. Handle pagination, partial results, provider schema changes, and unavailable services.
8. Verify webhook signatures and defend against replay where supported.
9. Add tests for success, malformed responses, timeout, rate limiting, authentication failure, and provider outage.
10. Document required configuration and operational failure modes.

## Observability
Use structured logs with request/correlation IDs and safe status metadata. Redact credentials and personal data. Track latency and error rates when existing observability supports it.

## Constraints
Do not invent API endpoints or assume a provider feature exists. Do not make real external writes during tests without explicit authorization. Clearly distinguish mocked tests from live integration verification.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
