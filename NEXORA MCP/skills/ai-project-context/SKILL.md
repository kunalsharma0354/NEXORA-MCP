---
name: ai-project-context
description: Inspect a project, create or maintain a structured .ai/ Markdown context folder, and use that context to make accurate, token-efficient changes without guessing. Use when starting work in an unfamiliar codebase, onboarding to a repository, implementing features, debugging, refactoring, or when project context may be stale.
---

# AI Project Context Skill

## Purpose
Create and maintain a reliable, concise `.ai/` project knowledge base so future work uses verified context instead of repeatedly scanning the entire repository. Reduce unnecessary token use and avoid unsupported assumptions.

## Non-negotiable rules
1. **Inspect before changing.** First inspect the repository root, project instructions, package/build manifests, and relevant source files. Do not edit application code until the task and affected area are understood.
2. **Never invent facts.** Clearly distinguish verified facts, reasonable inferences, and unknowns. Cite paths and, where useful, symbol names when recording findings.
3. **Preserve existing work.** If `.ai/` or its files already exist, read and update them carefully. Do not overwrite useful notes blindly. Do not delete user content.
4. **Respect instructions.** Read applicable `AGENTS.md`, `CLAUDE.md`, `README`, contributor instructions, and nested instructions before acting. Higher-priority instructions take precedence.
5. **Keep context concise.** Record durable, high-value information only. Do not copy whole source files, huge logs, secrets, generated output, or dependency trees into `.ai/`.
6. **Verify changes.** Run the narrowest relevant tests, lint, type-check, or build available. Report commands run and any that could not be run.
7. **No risky side effects without approval.** Do not run destructive commands, migrations against real environments, deploy, publish, or alter secrets/production data unless explicitly authorized.
8. **Update the knowledge base after meaningful work.** Update relevant `.ai/` notes when architecture, behavior, commands, decisions, or known issues change.

## Workflow

### Phase A — Discover
1. Identify the repository root; do not assume the current directory is the root.
2. List top-level files and directories. Exclude or avoid traversing `.git`, dependency folders (`node_modules`, `vendor`, `.venv`, `dist`, `build`, coverage output), caches, and large generated files unless directly relevant.
3. Read repository-level and applicable nested agent instructions.
4. Inspect manifests and entry points (for example `package.json`, lockfiles, `pyproject.toml`, `Cargo.toml`, `go.mod`, solution/project files), README, tests, configuration, and the code involved in the user's task.
5. If there is already a `.ai/` directory, read its index and only the relevant notes; validate important claims against current files when needed.

### Phase B — Create or reconcile `.ai/`
If `.ai/` does not exist, create it. If it exists, preserve and improve it rather than recreating it from scratch.

Use this default structure, adapting it to project size:

```text
.ai/
├── README.md                 # Index, purpose, freshness and navigation
├── PROJECT.md                # Verified project summary and stack
├── ARCHITECTURE.md            # Components, boundaries, data/control flow
├── CONVENTIONS.md             # Code style, patterns, and repository rules
├── COMMANDS.md                # Install, dev, test, lint, build commands
├── TASK_CONTEXT.md            # Current task only; keep short and refresh as needed
├── DECISIONS.md               # Important decisions and rationale
└── KNOWN_ISSUES.md            # Confirmed issues, caveats, and unresolved questions
```

Create only files supported by the project and useful for future work. For a tiny project, combine notes rather than producing empty boilerplate. Do not claim a command works until verified from manifests/docs or actually run successfully. If a fact is unknown, label it `Unknown — not verified`.

### What to record
- **README.md:** what each context file contains, how to keep it current, last reviewed date if available, and links to key repository docs.
- **PROJECT.md:** purpose, language/framework versions when verified, package manager, entry points, important directories, external services/integrations (never credentials).
- **ARCHITECTURE.md:** major modules, responsibilities, boundaries, data flow, API/storage interactions, and paths to source evidence.
- **CONVENTIONS.md:** actual patterns found in the code, naming/style rules, error handling, validation, logging, and testing expectations.
- **COMMANDS.md:** install/run/test/lint/type-check/build commands and their source; mark unverified commands.
- **TASK_CONTEXT.md:** current user request, relevant files, constraints, planned minimal changes, and verification plan. Keep task-specific notes brief and replace stale task details when a new task starts.
- **DECISIONS.md:** durable technical decisions and reasons; do not infer a decision merely because a pattern exists.
- **KNOWN_ISSUES.md:** confirmed issues, reproduction clues, workarounds, and unresolved questions. Do not present guesses as confirmed bugs.

### Accuracy and freshness
- Every non-obvious claim should include source paths, such as `src/api/client.ts`, or be marked as inference/unknown.
- Prefer current source code over stale notes. If notes conflict with code, investigate and correct the notes.
- Avoid duplicate documentation. Link to existing documentation instead of copying it.
- Never store API keys, passwords, tokens, private customer data, `.env` contents, or other secrets in `.ai/`.
- Do not put full source files or broad file dumps in context notes.
- Consider whether `.ai/` should be committed. Do not modify `.gitignore` automatically; explain the trade-off if relevant.
- Do not create every template file mechanically. Create only meaningful files and fill them with project-specific findings.

## Phase C — Plan the task
Before editing application code:
1. Restate the requested outcome internally in concrete acceptance criteria.
2. Identify the smallest relevant set of files and read them, along with directly related tests and callers.
3. Check current `.ai/` notes against those files.
4. Write/update `.ai/TASK_CONTEXT.md` with a short plan, constraints, affected paths, and tests.
5. If the requirement is ambiguous in a way that could materially change the implementation, ask the user one focused question rather than guessing.

## Phase D — Implement carefully
- Make the smallest coherent change that satisfies the request.
- Follow existing architecture and conventions unless the task explicitly requires a change.
- Avoid unrelated refactors, dependency additions, formatting churn, and speculative improvements.
- Do not change public APIs, data schemas, authentication, permissions, or persistence behavior without understanding compatibility impact.
- Never claim files, tests, or behavior were checked when they were not.

## Phase E — Verify and record
1. Inspect the diff and confirm no unrelated changes or secrets were introduced.
2. Run relevant tests/checks where feasible.
3. Update `.ai/` notes if verified facts changed; update `TASK_CONTEXT.md` with outcome and verification.
4. Keep notes concise. Remove stale claims by correcting them, not by deleting unrelated user documentation.
5. In the final response, summarize: what changed, important files, checks run and results, and remaining risks/unknowns.

## Token-efficiency strategy
- Start with `.ai/README.md`, then only the context files relevant to the current task.
- Use repository search and targeted file reads rather than reading every file.
- Expand exploration only when evidence is missing or contradictory.
- Prefer short summaries with file paths over copied code.
- Re-verify critical details in source before making consequential changes.
- Refresh notes when they become stale; context files are a guide, not unquestionable truth.

## First-run checklist
- [ ] Locate repository root and read its instructions.
- [ ] Inspect top-level structure and manifests.
- [ ] Check whether `.ai/` already exists.
- [ ] Create/update useful `.ai/*.md` files with verified findings.
- [ ] Record unknowns instead of guessing.
- [ ] Create/update `TASK_CONTEXT.md` before implementation.
- [ ] Make minimal changes and run relevant checks.
- [ ] Review diff and update context notes.
