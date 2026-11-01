# AI Project Context Skill

A reusable agent skill that builds and maintains a `.ai/` folder of concise Markdown project notes before making code changes.

## Install

Copy the `ai-project-context` skill folder into the skills directory supported by your coding agent. If the agent uses a different skill format or discovery path, follow that agent's documented installation instructions.

The folder contains `SKILL.md`, which defines the workflow and safety rules.

## What it does

- Inspects project instructions, manifests, structure, and relevant source.
- Creates or reconciles `.ai/` rather than blindly overwriting existing notes.
- Documents verified project facts, architecture, conventions, commands, decisions, and known issues.
- Uses task-specific context to reduce unnecessary repository reads.
- Verifies edits and updates notes after meaningful changes.

## Important

This is an instruction-based skill, not an automatic installer. The target AI coding agent must support custom skills or be configured to load `SKILL.md`. The skill should not be assumed active until installed in the agent's supported location.
