---
name: orchestration-rules
description: Shared delegation and repair-budget rules for orchestrators.
---

# Orchestration Rules

## Delegation

Invoke subagents sequentially.
Wait for the current subagent to complete before invoking another.

Do not modify an artifact while a subagent is reviewing or validating it.

## Repair Budget

Allow at most five repair rounds per workflow invocation.

The initial design or implementation and its first review or validation
do not consume a repair round.

A repair round begins when findings from review or validation are sent
for correction. It includes the resulting revision and its required
re-review or re-validation.

Findings reconciled together consume one round, regardless of how many
agents reported them.

The budget is shared across workflow phases and never resets when
moving between reviewers, validation, editing, or consistency checks.

Count a repair attempt even if it produces no artifact changes.
Routine clarification without a correction attempt does not consume a round.

## Escalation

If findings remain unresolved after the fifth repair round, stop and
present them to the user. Do not begin a sixth round.

Escalate earlier when required behavior is ambiguous, material findings
cannot be reconciled, or repeated repairs make no progress.

Report:

- repair rounds used;
- unresolved findings;
- the decision or information needed to proceed.
