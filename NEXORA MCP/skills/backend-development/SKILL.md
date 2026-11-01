# Backend Development

Use this skill for endpoints, services, jobs and any server-side business logic.

## 1. Boundaries

Separate three layers and never let them leak:

- **Transport** (route/controller): parses input, authenticates, maps to a service call, maps the result to a response. No business rules.
- **Service**: business rules, authorization decisions on domain data, orchestration between repositories and external calls.
- **Data access**: queries, transactions, persistence. No business rules, no HTTP concepts.

If a rule lives in the controller, it cannot be reused and cannot be tested without HTTP.

## 2. Validation

Validate at the boundary, then trust inside:

1. Presence, type and range of every input.
2. Normalise before validating (trim, lowercase, parse numbers/dates).
3. Reject unknown or oversized payloads.
4. Never pass raw user input into a query, path or command.
5. Return one consistent error shape: code, message, field (if applicable).

Validation errors are 400-class; missing authentication is 401; authorization failure is 403; missing resource is 404 — do not collapse them into one generic error.

## 3. Transactions and consistency

- Any operation that writes more than one row runs in a transaction.
- Check-then-write races: use a conditional update or a unique constraint, not an `if` in application code.
- Side effects (email, webhook, file write) happen after the commit, or are queued — not inside the transaction.
- Partial failure must leave a documented state, not a half-applied change.

## 4. Error handling

- Catch only what you can handle; let unexpected errors bubble to one central handler.
- Log with enough context to reproduce: operation, ids, duration, error type — never secrets or tokens.
- Do not return stack traces, SQL or internal paths to clients.
- Timeouts and retries: set explicit timeouts on every outbound call; retry only idempotent operations, with backoff.

## 5. Performance

- No query inside a loop; batch or join.
- Paginate every list endpoint with a bounded page size.
- Index columns used in filters and joins; verify with an explain plan, not assumptions.
- Cache only what is safe to be stale; document the invalidation rule.

## 6. Checklist before completion

- [ ] Input validated and normalised at the boundary.
- [ ] Authorization checked on the resource, not only on the route.
- [ ] Multi-write operations transactional.
- [ ] Error shape consistent with existing endpoints.
- [ ] No secrets in logs or responses.
- [ ] Tests or a manual request/response proving the happy path and at least one failure path.

## Definition of done

The endpoint behaves identically for valid input and predictably for invalid input; failures are observable in logs without leaking internals; existing endpoints keep their contracts.
