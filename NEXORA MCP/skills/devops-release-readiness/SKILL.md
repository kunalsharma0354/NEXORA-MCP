---
id: devops-release-readiness
name: DevOps & Release Readiness
version: 1.0.0
description: Prepare changes for reproducible builds, safe configuration, controlled deployment, and rollback-aware releases.
category: devops
tags: [devops, ci, deployment, configuration, release]
---

# DevOps & Release Readiness

Prepare changes for reproducible builds, safe configuration, controlled deployment, and rollback-aware releases.

## When to use
Use for CI changes, release preparation, container/build configuration, deployment plans, or environment setup.

## Workflow
1. Inspect current CI, package scripts, build tooling, environment configuration, and deployment target.
2. Ensure setup and builds are reproducible and dependencies are appropriately locked.
3. Separate development/test/production configuration and validate required variables at startup.
4. Keep secrets out of source control, build artifacts, and logs.
5. Add or update CI checks for lint, type-check, tests, build, and security checks when supported.
6. Assess migration ordering, health checks, readiness, observability, and rollback options.
7. Document deployment prerequisites and expected behavior after release.
8. Verify the build and tests in the available environment.

## Safety
Do not deploy, publish packages, change DNS, rotate production secrets, or alter live infrastructure unless explicitly authorized. Prefer a dry run or staging validation first.

## Completion criteria
- Build/test commands are grounded in repository scripts.
- Configuration requirements are documented.
- Failure and rollback paths are understood.
- Changes do not expose credentials or break backward compatibility without an explicit migration plan.

## Avoid
Assuming a hosting platform, inventing environment variables, or claiming a deployment succeeded when only local code was changed.

## Final response
Summarize what changed, relevant files or components, checks actually run, results, and any remaining limitations. Be concise, specific, and honest.
