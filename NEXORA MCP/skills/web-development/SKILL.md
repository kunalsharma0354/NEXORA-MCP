# Web Development

Use this skill when a request asks for a new page, feature, endpoint, integration or fix in an existing web codebase.

## Non-negotiables

1. Never start writing code before inspecting the repository.
2. Reuse what exists: framework, router, state layer, styles, API client, auth, build scripts.
3. Every claim of "done" must be backed by a command that was actually run.
4. Do not invent endpoints, environment variables, database tables or design tokens.

## Workflow

### 1. Inspect (10–15 minutes, no edits)

- Read the manifest file (`package.json`, `*.csproj`, `pyproject.toml`, etc.) to learn the stack and available scripts.
- Map the routing layer: which files define routes, how links are produced, what the navigation pattern is.
- Find the shared components and the design tokens (CSS variables, theme file). Identify what must be reused rather than re-created.
- Trace one existing feature end to end: UI → state → API call → handler → storage. This gives you the real integration points.
- Check how errors, loading and empty states are handled today.
- Note the exact commands for build, lint, type-check and test.

Write down the integration points before touching files. If the repo has no obvious place for the feature, say so and pick the smallest consistent location.

### 2. Plan

Produce a short plan with:

- Files to create.
- Files to modify (and why each one).
- Data flow: where the data comes from, where it is transformed, where it is rendered.
- Failure modes: missing service, empty data, slow response, unauthorized.
- Verification steps that prove the feature works.

Keep the plan minimal — if a step can be removed without losing correctness, remove it.

### 3. Implement

- Follow the existing file structure and naming.
- Match the existing code style instead of introducing a new one.
- Add types/interfaces for new data shapes where the codebase already types its data.
- Handle the error, loading and empty branches, not only the happy path.
- Do not leave placeholder buttons, commented-out code, `TODO` handlers or fake data wired as if real.

### 4. Verify

Run, in this order, whatever exists in the repo:

1. Build / compile.
2. Type-check.
3. Lint.
4. Tests.
5. Manual path: start the app and click through the new feature.

If a command cannot run (missing environment, missing credentials), say so explicitly instead of pretending it passed.

## Constraints

- Do not add a dependency if the platform already provides the capability.
- Do not restructure unrelated code to make your change easier.
- Do not rename or move existing files unless the task requires it.
- Preserve existing user data, configuration and API contracts.
- If the change affects an API shape, update every caller in the same change.

## Anti-patterns

- Building a parallel UI instead of extending the existing one.
- Hardcoding data that should come from the backend.
- Adding a new state library for one screen.
- "It compiles" used as proof of correctness.
- Leaving the old code path active so two implementations disagree.

## Definition of done

- The feature is reachable through the existing navigation, not only by URL.
- Data is real and flows through the existing layer.
- Loading, empty and error states are handled.
- Build passes; tests pass or their absence is reported.
- No placeholder or fake behaviour remains.
- A short summary lists changed files and the commands that were run.
