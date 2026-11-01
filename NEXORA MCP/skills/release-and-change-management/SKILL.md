---
id: release-and-change-management
name: Release Notes & Change Management
version: 1.0.0
description: Prepare clear change summaries and release readiness checks based on actual code and verification evidence.
category: documentation
tags: [release, changelog, migration, change-management]
---

# Release Notes & Change Management

Prepare clear change summaries and release readiness checks based on actual code and verification evidence.

## When to use
Use when preparing a release, summarizing a feature, or documenting breaking changes and upgrade steps.

## Workflow
1. Inspect the diff, commit history if available, tests, migrations, and configuration changes.
2. Categorize user-visible features, fixes, internal changes, security changes, and breaking changes.
3. Describe impact in user terms without overstating capabilities.
4. Identify migration or configuration steps and compatibility concerns.
5. Verify reported tests against actual command output.
6. Include known issues and rollback considerations where material.
7. Match the project's existing versioning and changelog conventions.

## Release checklist
- Build and relevant tests status recorded.
- Required configuration documented.
- Database migration sequence reviewed.
- Sensitive data and secrets excluded from artifacts.
- Breaking changes and upgrade steps explicit.
- Deployment/rollback actions authorized and documented.

## Avoid
Inventing release dates, claiming production deployment without evidence, or describing unimplemented roadmap items as shipped.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
