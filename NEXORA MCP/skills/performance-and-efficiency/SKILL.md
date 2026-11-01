---
id: performance-and-efficiency
name: Performance & Resource Efficiency
version: 1.0.0
description: Identify and address measured performance bottlenecks without sacrificing correctness or maintainability.
category: performance
tags: [performance, profiling, caching, optimization, latency]
---

# Performance & Resource Efficiency

Identify and address measured performance bottlenecks without sacrificing correctness or maintainability.

## When to use
Use when a page, API, build, database query, or agent workflow is slow or resource-intensive.

## Workflow
1. Define the user-visible symptom and performance target.
2. Measure a baseline with available profilers, browser tools, query plans, timing, or logs.
3. Identify the dominant bottleneck before changing code.
4. Prioritize high-impact, low-risk improvements.
5. Consider network payload, rendering, query patterns, caching, concurrency, and algorithmic complexity as relevant.
6. Validate that optimization preserves correctness and authorization boundaries.
7. Compare before/after measurements under comparable conditions.
8. Document tradeoffs, cache invalidation behavior, and remaining bottlenecks.

## Common checks
- Avoid repeated requests and unnecessary re-renders.
- Bound concurrency and response sizes.
- Paginate large collections.
- Avoid N+1 queries and unbounded scans.
- Cache only data with clear freshness and user-isolation rules.
- Avoid premature abstraction or speculative micro-optimization.

## Agent/MCP workflows
Bound tool output, skill result size, retry counts, browser steps, and wall-clock budgets. Avoid loading entire skill libraries into context when search and targeted loading suffice.

## Reporting
Include measurement method, environment, baseline, observed change, and limitations. Never fabricate benchmarks.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
